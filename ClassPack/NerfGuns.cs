using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// The host hears about every item a player finishes using. When that item is a snack
    /// eaten by a student, the nerf guns get their chance to turn up.
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(UnitMessageHandler), nameof(UnitMessageHandler.OnItemUseCompleted))]
    internal static class SnackEatenPatch
    {
        // Looked up before the game handles the message, while the snack still exists.
        private static void Prefix(ItemUseCompleted_C2S request, out Unit __state)
        {
            __state = null;
            if (!NetworkServer.active || !Game.InClass)
            {
                return;
            }
            UnitManager units = Game.Peek<UnitManager>();
            Unit user = units != null ? units.GetUnitByUID(request.userUID) : null;
            Unit item = units != null ? units.GetUnitByUID(request.itemUID) : null;
            if (user != null && user.isPlayer && user.IsStudent && item != null && item.UnitSubType == EUnitSubType.Food)
            {
                __state = user;
            }
        }

        private static void Postfix(Unit __state)
        {
            if (__state != null)
            {
                NerfGuns.OnStudentAte(__state);
                Phones.OnStudentAte(__state);
            }
        }
    }

    /// <summary>
    /// The game's own pick-up key, the one that grabs a snack. When the player is looking at
    /// the nerf gun over their drawer, that press takes the gun and the game does nothing
    /// else with it (otherwise the same press would also shut the drawer).
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(InputManager), "SendMsg")]
    internal static class PickUpKeyPatch
    {
        private static bool Prefix(eCmdType command, eClickType click)
        {
            if (command != eCmdType.Interect1 || click != eClickType.Down)
            {
                return true;
            }
            try
            {
                return !(NerfGuns.TryTakeWithPickUpKey() || Phones.TryTakeWithPickUpKey() || Swim.TryClimbWithPickUpKey()
                    || Elevator.TryPressWithPickUpKey());
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    /// <summary>
    /// Idea 1: nerf guns for teacher and students.
    ///
    /// Guns are rare finds that turn up the way snacks do. Each time a student finishes a
    /// snack there is a small chance a gun appears in their desk drawer, and the teacher gets
    /// a warning. A teacher who eats a snack out of a student's drawer has a smaller chance
    /// of finding one hidden there. Every gun has one shot.
    ///
    /// Taking the drawer gun is meant to be easy: it hovers over the open drawer, big and
    /// turning, and is picked up like a snack (look at it, press the pick-up key). The mod's
    /// own key also takes it from anywhere in the seat, drawer open or shut.
    ///
    /// Whoever is hit falls over like a rag doll (Faint.cs): a student or bot for 4 seconds,
    /// during which the teacher can grab them, the teacher for 5 seconds. The RPG hits every
    /// player in the class, the shooter included. The shooter's own game works out what the
    /// dart hit; the host checks it and applies it.
    /// </summary>
    internal static class NerfGuns
    {
        public const int Pistol = 1;
        public const int Shotgun = 2;
        public const int Sniper = 3;
        public const int Rpg = 4;

        private const int DrawerSlot = -1;

        private static readonly string[] Names = { "", "Nerf pistol", "Nerf shotgun", "Nerf sniper", "Nerf RPG" };
        private static readonly string[] ModelNames = { "", "pistol", "shotgun", "sniper", "rpg" };
        private static readonly float[] Reach = { 0f, 22f, 8f, 70f, 0f };
        // How often each kind turns up when a gun is found, out of 100.
        private static readonly int[] Rarity = { 0, 45, 25, 22, 8 };
        // Sizes for the gun hovering over the desk drawer: big enough to spot at a glance.
        private static readonly float[] DrawerScale = { 1f, 1.15f, 0.7f, 0.48f, 0.48f };
        // How far off the middle of the gun the player may look and still pick it up, in metres.
        private const float AimSlack = 0.26f;
        private static readonly Color32[] Tint =
        {
            new Color32(255, 255, 255, 255), new Color32(255, 130, 20, 255), new Color32(40, 120, 255, 255),
            new Color32(60, 200, 80, 255), new Color32(230, 40, 40, 255)
        };

        // Sizes in the hand: the game's characters are small, so the long guns are shrunk.
        private static readonly float[] HeldScale = { 1f, 0.8f, 0.6f, 0.55f, 0.6f };

        private const int ShotsPerGun = 1;
        private const int CrateCount = 4;
        private const float TakeReach = 2.2f;

        // What every player's game knows
        private sealed class Crate
        {
            public int Gun;
            public Vector3 Position;
            public GameObject Model;
        }

        private sealed class Held
        {
            public int Gun;
            public int Shots;
            public GameObject Model;
        }

        private static readonly Crate[] Crates = new Crate[CrateCount];
        private static readonly Dictionary<ulong, Held> Guns = new Dictionary<ulong, Held>();
        private static int _drawerGun;
        private static GameObject _drawerModel;
        private static Vector3 _drawerModelMiddle;
        // True while the player sits at their desk with a gun waiting in it.
        private static bool _drawerGunWaiting;
        // True while the player is also looking at the gun over the open drawer.
        private static bool _drawerGunAimed;
        private static bool _drawerGunLogged;
        private static float _nextShotAt;
        private static float _flashUntil;
        private static Texture2D _white;
        private static GUIStyle _prompt;
        private static string _pickUpKey;
        private static float _pickUpKeyReadAt = -100f;

        // What only the host keeps
        private sealed class HostGun
        {
            public int Gun;
            public int Shots;
        }

        private static readonly int[] HostCrateGun = new int[CrateCount];
        private static readonly Vector3[] HostCratePosition = new Vector3[CrateCount];
        private static readonly Dictionary<ulong, int> HostDrawerGun = new Dictionary<ulong, int>();
        private static readonly Dictionary<ulong, HostGun> HostGuns = new Dictionary<ulong, HostGun>();

        public static void Tick()
        {
            if (!Game.InClass)
            {
                Clear();
                return;
            }
            Animate();
            HandleKeys();
        }

        private static void Clear()
        {
            for (int i = 0; i < CrateCount; i++)
            {
                if (Crates[i] != null && Crates[i].Model != null)
                {
                    UnityEngine.Object.Destroy(Crates[i].Model);
                }
                Crates[i] = null;
                HostCrateGun[i] = 0;
            }
            foreach (Held held in Guns.Values)
            {
                if (held.Model != null)
                {
                    UnityEngine.Object.Destroy(held.Model);
                }
            }
            Guns.Clear();
            SetDrawerGun(0);
            HostGuns.Clear();
            HostDrawerGun.Clear();
        }

        // ------------------------------------------------------------------ models

        private static GameObject BuildGun(int gun)
        {
            GameObject model = Models.Build(ModelNames[gun]);
            if (model != null)
            {
                return model;
            }
            // The model file is missing: fall back to a plain coloured block so the gun still shows.
            var root = new GameObject("[ClassPack " + Names[gun] + "]");
            float length = gun == Pistol ? 0.24f : gun == Shotgun ? 0.55f : 0.9f;
            Shapes.Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.04f, length * 0.4f), new Vector3(0.06f, 0.08f, length), Tint[gun]);
            Shapes.Part(root.transform, PrimitiveType.Cube, new Vector3(0f, -0.06f, 0f), new Vector3(0.045f, 0.12f, 0.06f), new Color32(40, 42, 48, 255));
            return root;
        }

        private static void SetDrawerGun(int gun)
        {
            _drawerGun = gun;
            _drawerGunWaiting = false;
            _drawerGunAimed = false;
            _drawerGunLogged = false;
            if (_drawerModel != null)
            {
                UnityEngine.Object.Destroy(_drawerModel);
                _drawerModel = null;
            }
        }

        /// <summary>The drawer of the desk the player sits at, or null.</summary>
        internal static DrawerComponent MyDrawer(Unit me)
        {
            if (me == null || !me.IsSitting || me.currentSitUnit == null)
            {
                return null;
            }
            ChairComponent chair = me.currentSitUnit.GetComponent<ChairComponent>();
            if (chair == null || chair.config == null)
            {
                return null;
            }
            Unit drawerUnit = chair.config.GetDrawerUnit();
            return drawerUnit != null ? drawerUnit.GetComponent<DrawerComponent>() : null;
        }

        private static void Animate()
        {
            float spin = Time.time * 70f;
            float bob = Mathf.Sin(Time.time * 2.2f) * 0.07f;
            for (int i = 0; i < CrateCount; i++)
            {
                Crate crate = Crates[i];
                if (crate == null || crate.Gun == 0)
                {
                    continue;
                }
                if (crate.Model == null)
                {
                    crate.Model = BuildGun(crate.Gun);
                    crate.Model.transform.localScale = Vector3.one * (crate.Gun == Pistol ? 1.5f : 0.9f);
                }
                crate.Model.transform.position = crate.Position + Vector3.up * (1.15f + bob);
                crate.Model.transform.rotation = Quaternion.Euler(-12f, spin, 0f);
            }

            // The gun this player found waits in their own desk. It can be taken from the seat
            // at any time, and it shows, hovering and turning, while the drawer is open.
            _drawerGunWaiting = false;
            _drawerGunAimed = false;
            if (_drawerGun == 0)
            {
                return;
            }
            Unit me = Game.LocalPlayer;
            _drawerGunWaiting = me != null && me.isActived && me.IsSitting;
            DrawerComponent drawer = MyDrawer(me);
            bool open = drawer != null && drawer.IsActive && drawer.openState && drawer.owner != null && drawer.owner.transform != null;
            if (!open)
            {
                if (_drawerModel != null)
                {
                    _drawerModel.SetActive(false);
                }
                return;
            }
            if (_drawerModel == null)
            {
                _drawerModel = BuildGun(_drawerGun);
                _drawerModel.transform.localScale = Vector3.one * DrawerScale[_drawerGun];
                MeshFilter filter = _drawerModel.GetComponent<MeshFilter>();
                _drawerModelMiddle = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.center : Vector3.zero;
            }
            Transform tray = drawer.owner.transform;
            Vector3 spot = tray.position;
            List<Transform> snacks = drawer.GetItemTransforms();
            if (snacks != null && snacks.Count > 0)
            {
                // Rest it on top of whatever else is in the drawer.
                Vector3 sum = Vector3.zero;
                int counted = 0;
                foreach (Transform snack in snacks)
                {
                    if (snack != null)
                    {
                        sum += snack.position;
                        counted++;
                    }
                }
                if (counted > 0)
                {
                    spot = sum / counted;
                }
            }
            // Hovering a hand's width over the snacks and turning slowly around its own middle.
            Vector3 middle = spot + Vector3.up * (0.2f + bob * 0.35f);
            Quaternion turn = Quaternion.Euler(-10f, spin * 0.6f, 0f);
            _drawerModel.SetActive(true);
            _drawerModel.transform.rotation = turn;
            _drawerModel.transform.position = middle - turn * (_drawerModelMiddle * DrawerScale[_drawerGun]);
            if (!_drawerGunLogged)
            {
                _drawerGunLogged = true;
                Plugin.Log("nerf guns: showing the " + Names[_drawerGun] + " over the drawer at " + middle.ToString("F2"));
            }

            Camera camera = Camera.main;
            if (camera != null)
            {
                Vector3 to = middle - camera.transform.position;
                float along = Vector3.Dot(to, camera.transform.forward);
                float off = (to - camera.transform.forward * along).magnitude;
                _drawerGunAimed = _drawerGunWaiting && along > 0.05f && along < 4f && off < AimSlack;
            }
        }

        /// <summary>
        /// Late in the frame, after the game has moved and animated everybody: the guns go into
        /// their holders' hands. (Placed any earlier, a gun trails a frame behind and shakes.)
        /// </summary>
        public static void LateTick()
        {
            UnitManager units = Guns.Count > 0 && Game.InClass ? Game.Peek<UnitManager>() : null;
            if (units == null)
            {
                return;
            }
            foreach (KeyValuePair<ulong, Held> pair in Guns)
            {
                Held held = pair.Value;
                Unit holder = units.GetUnitByUID(pair.Key);
                Transform body = holder != null && holder.isActived ? holder.transform : null;
                if (body == null)
                {
                    if (held.Model != null)
                    {
                        held.Model.SetActive(false);
                    }
                    continue;
                }
                if (held.Model == null)
                {
                    held.Model = BuildGun(held.Gun);
                    held.Model.transform.localScale = Vector3.one * HeldScale[held.Gun];
                }
                held.Model.SetActive(true);
                Transform neck = holder.GetPartRoot(EUnitPartType.HeadPosition);
                Vector3 neckAt = neck != null ? neck.position : body.position + Vector3.up * 0.76f;
                if (held.Gun == Rpg)
                {
                    // Carried on the right shoulder.
                    held.Model.transform.position = neckAt + body.right * 0.17f + Vector3.up * 0.05f + body.forward * 0.08f;
                }
                else
                {
                    // Held out in front just under the neck, pointing where the body faces.
                    held.Model.transform.position = neckAt + body.right * 0.19f - Vector3.up * 0.04f + body.forward * 0.34f;
                }
                held.Model.transform.rotation = body.rotation;
            }
        }

        private static void TakeFromDrawer(string how)
        {
            Plugin.Log("nerf guns: asking for the " + Names[_drawerGun] + " from the drawer (" + how + ")");
            Net.SendToHost(new CpAsk { kind = CpKind.GunTake, i = DrawerSlot });
        }

        /// <summary>
        /// Called when the game's pick-up key goes down. True when the player was looking at
        /// the gun over their drawer, in which case the gun is taken and the press is used up.
        /// </summary>
        public static bool TryTakeWithPickUpKey()
        {
            if (!_drawerGunAimed || _drawerGun == 0 || !Game.InClass || CursorGrab.Busy || !Net.CanSendToHost)
            {
                return false;
            }
            Unit me = Game.LocalPlayer;
            if (me == null || !me.isActived || me.IsStun || me.BodyActionsBlocked || Mine() != null)
            {
                return false;
            }
            TakeFromDrawer("pick-up key");
            return true;
        }

        internal static string PickUpKeyName()
        {
            if (_pickUpKey == null || Time.unscaledTime - _pickUpKeyReadAt > 3f)
            {
                _pickUpKeyReadAt = Time.unscaledTime;
                _pickUpKey = null;
                try
                {
                    if (MonoSingletonT<InputManager>.IsSingletonActive())
                    {
                        _pickUpKey = MonoSingletonT<InputManager>.Instance.BindingName(eCmdType.Interect1);
                    }
                }
                catch (Exception)
                {
                    // Fall through to the plain wording below.
                }
                if (string.IsNullOrEmpty(_pickUpKey) || _pickUpKey == eCmdType.Interect1.ToString())
                {
                    _pickUpKey = "pick-up key";
                }
            }
            return _pickUpKey;
        }

        // -------------------------------------------------------------- local keys

        /// <summary>True while this player carries a nerf gun.</summary>
        public static bool HoldingLocal => Mine() != null;

        /// <summary>True while a nerf gun waits in this player's desk drawer.</summary>
        public static bool DrawerGunWaiting => _drawerGun != 0;

        private static Held Mine()
        {
            Unit me = Game.LocalPlayer;
            Held held;
            return me != null && Guns.TryGetValue(me.UID, out held) ? held : null;
        }

        private static int NearestCrate(Vector3 from)
        {
            int best = -1;
            float bestDistance = TakeReach * TakeReach;
            for (int i = 0; i < CrateCount; i++)
            {
                if (Crates[i] == null || Crates[i].Gun == 0)
                {
                    continue;
                }
                Vector3 offset = Crates[i].Position - from;
                offset.y = Mathf.Abs(offset.y) < 2.5f ? 0f : offset.y;
                if (offset.sqrMagnitude <= bestDistance)
                {
                    bestDistance = offset.sqrMagnitude;
                    best = i;
                }
            }
            return best;
        }

        private static void HandleKeys()
        {
            Unit me = Game.LocalPlayer;
            if (me == null || !me.isActived || CursorGrab.Busy || Game.IsTyping || !Net.CanSendToHost)
            {
                return;
            }
            bool blocked = me.IsStun || me.BodyActionsBlocked;
            Held mine = Mine();
            // With a trick in hand the same key puts the trick away instead (see Tricks.cs).
            if (Input.GetKeyDown(Plugin.Settings.UseKeyCode) && !blocked && Tricks.HeldLocal == 0)
            {
                int near = NearestCrate(me.Position);
                if (mine == null && _drawerGunWaiting)
                {
                    TakeFromDrawer(Plugin.Settings.UseKeyCode + " key");
                }
                else if (mine == null && near >= 0)
                {
                    Net.SendToHost(new CpAsk { kind = CpKind.GunTake, i = near });
                }
                else if (mine != null)
                {
                    Net.SendToHost(new CpAsk { kind = CpKind.GunDrop });
                }
                else
                {
                    FireAlarm.TryPull(me);
                }
            }
            bool fire = Input.GetKeyDown(Plugin.Settings.FireKeyCode) || Input.GetMouseButtonDown(2);
            if (mine != null && fire && !blocked && Time.time >= _nextShotAt)
            {
                _nextShotAt = Time.time + 0.5f;
                Fire(me, mine.Gun);
            }
        }

        private static void Fire(Unit me, int gun)
        {
            Camera camera = Camera.main;
            UnitManager units = Game.Peek<UnitManager>();
            if (camera == null || units == null)
            {
                return;
            }
            Vector3 from = camera.transform.position;
            Vector3 forward = camera.transform.forward;
            var ask = new CpAsk { kind = CpKind.GunFire, v = from, w = from + forward * 12f };
            if (gun != Rpg)
            {
                var hits = new List<ulong>();
                // The shotgun sprays a small cone; the others fire one straight dart.
                int darts = gun == Shotgun ? 7 : 1;
                for (int i = 0; i < darts; i++)
                {
                    Vector3 direction = forward;
                    if (i > 0)
                    {
                        float angle = (i - 1) * 60f;
                        Quaternion spread = Quaternion.AngleAxis(angle, forward) * Quaternion.AngleAxis(7f, camera.transform.right);
                        direction = spread * forward;
                    }
                    Vector3 end;
                    Unit target = Trace(units, me, from, direction, Reach[gun], out end);
                    if (i == 0)
                    {
                        ask.w = end;
                    }
                    if (target != null && !hits.Contains(target.UID))
                    {
                        hits.Add(target.UID);
                    }
                }
                ask.t0 = hits.Count > 0 ? hits[0] : 0uL;
                ask.t1 = hits.Count > 1 ? hits[1] : 0uL;
                ask.t2 = hits.Count > 2 ? hits[2] : 0uL;
                ask.t3 = hits.Count > 3 ? hits[3] : 0uL;
                ask.t4 = hits.Count > 4 ? hits[4] : 0uL;
                ask.t5 = hits.Count > 5 ? hits[5] : 0uL;
            }
            Net.SendToHost(ask);
        }

        /// <summary>Follows one dart until it hits a player, a classmate or something solid.</summary>
        internal static Unit Trace(UnitManager units, Unit me, Vector3 from, Vector3 direction, float reach, out Vector3 end)
        {
            end = from + direction * reach;
            RaycastHit[] hits = Physics.RaycastAll(from, direction, reach, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (Faint.IsDollPart(hit.collider))
                {
                    // Someone already knocked out: the dart flies over them.
                    continue;
                }
                ObjectModel model = hit.collider.GetComponentInParent<ObjectModel>();
                Unit unit = model != null ? units.GetUnitByUID(model.UID) : null;
                if (unit == me)
                {
                    continue;
                }
                if (unit != null && unit.isActived && (unit.IsStudent || unit.IsTeacher))
                {
                    end = hit.point;
                    return unit;
                }
                if (hit.collider.isTrigger)
                {
                    continue;
                }
                end = hit.point;
                return null;
            }
            return null;
        }

        // --------------------------------------------------------------- host side

        private static int RollKind()
        {
            int roll = UnityEngine.Random.Range(0, 100);
            for (int gun = Pistol; gun <= Rpg; gun++)
            {
                roll -= Rarity[gun];
                if (roll < 0)
                {
                    return gun;
                }
            }
            return Pistol;
        }

        /// <summary>
        /// Host: a student just finished a snack. Now and then a nerf gun turns up in their
        /// desk drawer, the same moment the drawer would restock. The teacher is warned.
        /// </summary>
        public static void OnStudentAte(Unit student)
        {
            Settings settings = Plugin.Settings;
            UnitManager units = Game.Peek<UnitManager>();
            if (!settings.nerfGuns || !Game.InClass || units == null || student == null || !student.isActived
                || HostDrawerGun.ContainsKey(student.UID) || HostGuns.ContainsKey(student.UID))
            {
                return;
            }
            float chance = settings.nerfAlwaysForTesting ? 1f : Mathf.Clamp01(settings.nerfChancePerSnack);
            if (UnityEngine.Random.value >= chance)
            {
                return;
            }
            int gun = RollKind();
            HostDrawerGun[student.UID] = gun;
            Net.TellPlayer(student, new CpTell { kind = CpKind.Crate, i = DrawerSlot, j = gun });
            foreach (Unit player in new List<Unit>(units.playerList))
            {
                if (player != null && player.isActived && player.isPlayer && player.IsTeacher)
                {
                    Net.Toast(player, "Warning: a nerf gun just turned up in a student's desk!", 5f);
                }
            }
            Plugin.Log("nerf guns: " + Poles.NameOf(student) + " has a " + Names[gun] + " in their drawer");
        }

        /// <summary>
        /// Host: the teacher just ate a snack out of a student's drawer. Rarely there was a
        /// nerf gun hidden under it, and the teacher walks off with it.
        /// </summary>
        public static void OnTeacherAte(Unit teacher)
        {
            Settings settings = Plugin.Settings;
            if (!settings.nerfGuns || !Game.InClass || teacher == null || !teacher.isActived || HostGuns.ContainsKey(teacher.UID))
            {
                return;
            }
            float chance = settings.nerfAlwaysForTesting ? 1f : Mathf.Clamp01(settings.nerfTeacherChancePerSnack);
            if (UnityEngine.Random.value >= chance)
            {
                return;
            }
            int gun = RollKind();
            HostGuns[teacher.UID] = new HostGun { Gun = gun, Shots = ShotsPerGun };
            Net.SendToModded(new CpTell { kind = CpKind.GunState, a = teacher.UID, i = gun, j = ShotsPerGun });
            Net.Toast(teacher, "There was a " + Names[gun] + " hidden under the snacks. It's yours now.", 5f);
            Plugin.Log("nerf guns: teacher " + Poles.NameOf(teacher) + " found a " + Names[gun]);
        }

        public static void OnAsk(Unit player, CpAsk ask)
        {
            if (!Plugin.Settings.nerfGuns || !Game.InClass || !player.isActived)
            {
                return;
            }
            HostGun gun;
            HostGuns.TryGetValue(player.UID, out gun);
            switch (ask.kind)
            {
                case CpKind.GunTake:
                    int taken = 0;
                    if (gun != null)
                    {
                        break;
                    }
                    if (ask.i == DrawerSlot)
                    {
                        if (HostDrawerGun.TryGetValue(player.UID, out taken))
                        {
                            HostDrawerGun.Remove(player.UID);
                            Net.TellPlayer(player, new CpTell { kind = CpKind.Crate, i = DrawerSlot, j = 0 });
                        }
                    }
                    else if (ask.i >= 0 && ask.i < CrateCount && HostCrateGun[ask.i] != 0
                        && (player.Position - HostCratePosition[ask.i]).sqrMagnitude < 4.5f * 4.5f)
                    {
                        taken = HostCrateGun[ask.i];
                        HostCrateGun[ask.i] = 0;
                        Net.SendToModded(new CpTell { kind = CpKind.Crate, i = ask.i, j = 0, v = HostCratePosition[ask.i] });
                    }
                    if (taken != 0)
                    {
                        HostGuns[player.UID] = new HostGun { Gun = taken, Shots = ShotsPerGun };
                        Net.SendToModded(new CpTell { kind = CpKind.GunState, a = player.UID, i = taken, j = ShotsPerGun });
                        Plugin.Log("nerf guns: " + Poles.NameOf(player) + " took the " + Names[taken]);
                    }
                    break;
                case CpKind.GunDrop:
                    if (gun != null)
                    {
                        HostGuns.Remove(player.UID);
                        Net.SendToModded(new CpTell { kind = CpKind.GunState, a = player.UID, i = 0, j = 0 });
                    }
                    break;
                case CpKind.GunFire:
                    if (gun != null && gun.Shots > 0 && !player.IsStun && !player.BodyActionsBlocked)
                    {
                        Shoot(player, gun, ask);
                    }
                    break;
            }
        }

        private static void Shoot(Unit shooter, HostGun gun, CpAsk ask)
        {
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null)
            {
                return;
            }
            gun.Shots--;
            int fired = gun.Gun;
            Net.SendToModded(new CpTell { kind = CpKind.GunFx, a = shooter.UID, i = fired, v = ask.v, w = ask.w });
            if (gun.Shots <= 0)
            {
                HostGuns.Remove(shooter.UID);
                Net.SendToModded(new CpTell { kind = CpKind.GunState, a = shooter.UID, i = 0, j = 0 });
            }
            else
            {
                Net.SendToModded(new CpTell { kind = CpKind.GunState, a = shooter.UID, i = fired, j = gun.Shots });
            }

            if (fired == Rpg)
            {
                Plugin.Log("nerf guns: " + Poles.NameOf(shooter) + " fired the RPG");
                foreach (Unit player in new List<Unit>(units.playerList))
                {
                    Hit(shooter, player, true);
                    if (player != null && player.isActived)
                    {
                        Net.Toast(player, Poles.NameOf(shooter) + " fired the nerf RPG!", 4f);
                    }
                }
                return;
            }

            int most = fired == Shotgun ? 6 : 1;
            ulong[] targets = { ask.t0, ask.t1, ask.t2, ask.t3, ask.t4, ask.t5 };
            var done = new HashSet<ulong>();
            for (int i = 0; i < targets.Length && done.Count < most; i++)
            {
                Unit target = targets[i] != 0uL ? units.GetUnitByUID(targets[i]) : null;
                if (target == null || target == shooter || !done.Add(target.UID))
                {
                    continue;
                }
                // The shooter's game says who was hit; the host only accepts targets in reach.
                if ((target.Position - shooter.Position).magnitude <= Reach[fired] + 4f)
                {
                    Hit(shooter, target, false);
                }
            }
            Plugin.Log("nerf guns: " + Poles.NameOf(shooter) + " fired the " + Names[fired] + " and hit " + done.Count);
        }

        private static void Hit(Unit shooter, Unit target, bool blast)
        {
            if (target == null || !target.isActived || target.IsDead || !(target.IsTeacher || target.IsStudent))
            {
                return;
            }
            // The RPG's blast reaches the players; the bots in the class are left alone.
            if (blast && !target.isPlayer)
            {
                return;
            }
            // Everybody hit is knocked out like a rag doll for a few seconds (Faint.cs).
            Faint.Knock(target, shooter, shooter != null ? shooter.Position : target.Position);
        }

        // ------------------------------------------------------------ every player

        public static void OnTell(CpTell tell)
        {
            switch (tell.kind)
            {
                case CpKind.Crate:
                    if (tell.i == DrawerSlot)
                    {
                        SetDrawerGun(Mathf.Clamp(tell.j, 0, Rpg));
                        if (tell.j != 0)
                        {
                            Game.PlaySound(AudioEvents.sfx_GameAddItem_sfx);
                            Game.Notify("A " + Names[Mathf.Clamp(tell.j, 0, Rpg)] + " turned up in your desk drawer! Press "
                                + Plugin.Settings.UseKeyCode + " in your seat to grab it. The teacher has been warned.", 7f);
                        }
                    }
                    else if (tell.i >= 0 && tell.i < CrateCount)
                    {
                        if (Crates[tell.i] != null && Crates[tell.i].Model != null)
                        {
                            UnityEngine.Object.Destroy(Crates[tell.i].Model);
                        }
                        Crates[tell.i] = new Crate { Gun = Mathf.Clamp(tell.j, 0, Rpg), Position = tell.v };
                    }
                    break;
                case CpKind.GunState:
                    Held old;
                    if (Guns.TryGetValue(tell.a, out old))
                    {
                        if (old.Gun == tell.i && tell.i != 0)
                        {
                            old.Shots = tell.j;
                            break;
                        }
                        if (old.Model != null)
                        {
                            UnityEngine.Object.Destroy(old.Model);
                        }
                        Guns.Remove(tell.a);
                    }
                    if (tell.i > 0 && tell.i <= Rpg)
                    {
                        Guns[tell.a] = new Held { Gun = tell.i, Shots = tell.j };
                        Unit me = Game.LocalPlayer;
                        if (me != null && me.UID == tell.a)
                        {
                            Game.PlaySound(AudioEvents.sfx_ItemPickup_sfx);
                        }
                    }
                    break;
                case CpKind.GunFx:
                    int gun = Mathf.Clamp(tell.i, Pistol, Rpg);
                    if (gun == Rpg)
                    {
                        _flashUntil = Time.time + 0.6f;
                        Game.PlaySoundAt(AudioEvents.sfx_bubbleExplode_sfx, tell.v);
                        Game.PlaySoundAt(AudioEvents.sfx_Flash_sfx, tell.v);
                    }
                    else
                    {
                        // Start the dart a little below the eyes so the shooter can see it too.
                        Shapes.Tracer(tell.v + Vector3.down * 0.18f, tell.w, Tint[gun], gun == Sniper ? 0.05f : 0.03f, gun == Sniper ? 0.35f : 0.15f);
                        Game.PlaySoundAt(AudioEvents.sfx_TaserFire_sfx, tell.v);
                        Game.PlaySoundAt(AudioEvents.sfx_TaserHit_sfx, tell.w);
                    }
                    break;
            }
        }

        /// <summary>Crosshair, gun line and pick-up hint. Drawn in a 1280 by 720 frame.</summary>
        public static string DrawHud(float width, float height)
        {
            if (!Game.InClass)
            {
                return null;
            }
            if (_white == null)
            {
                _white = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                _white.SetPixel(0, 0, Color.white);
                _white.Apply(false, true);
            }
            if (Time.time < _flashUntil)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((_flashUntil - Time.time) / 0.6f));
                GUI.DrawTexture(new Rect(0f, 0f, width, height), _white);
                GUI.color = old;
            }
            Unit me = Game.LocalPlayer;
            Held mine = Mine();
            if (mine != null)
            {
                Color old = GUI.color;
                GUI.color = Tint[mine.Gun];
                float x = width * 0.5f;
                float y = height * 0.5f;
                GUI.DrawTexture(new Rect(x - 9f, y - 1f, 6f, 2f), _white);
                GUI.DrawTexture(new Rect(x + 3f, y - 1f, 6f, 2f), _white);
                GUI.DrawTexture(new Rect(x - 1f, y - 9f, 2f, 6f), _white);
                GUI.DrawTexture(new Rect(x - 1f, y + 3f, 2f, 6f), _white);
                GUI.color = old;
                return Names[mine.Gun] + ", one shot     [" + Plugin.Settings.FireKeyCode
                    + " or wheel click] shoot     [" + Plugin.Settings.UseKeyCode + "] throw away";
            }
            if (_drawerGunWaiting)
            {
                if (_drawerGunAimed && !CursorGrab.Busy)
                {
                    // The same kind of prompt a snack gets, right under the crosshair.
                    if (_prompt == null)
                    {
                        _prompt = new GUIStyle { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                        _prompt.normal.textColor = new Color32(255, 255, 255, 255);
                    }
                    var box = new Rect(width * 0.5f - 170f, height * 0.5f + 36f, 340f, 34f);
                    Color old = GUI.color;
                    GUI.color = new Color(0f, 0f, 0f, 0.6f);
                    GUI.DrawTexture(box, _white);
                    GUI.color = old;
                    GUI.Label(box, "[" + PickUpKeyName() + "]  Pick up " + Names[_drawerGun], _prompt);
                }
                return "A " + Names[_drawerGun] + " is in your desk.     [" + Plugin.Settings.UseKeyCode + "] grab it";
            }
            if (me != null)
            {
                int near = NearestCrate(me.Position);
                if (near >= 0)
                {
                    return "Press " + Plugin.Settings.UseKeyCode + " to take the " + Names[Crates[near].Gun];
                }
            }
            return null;
        }
    }
}
