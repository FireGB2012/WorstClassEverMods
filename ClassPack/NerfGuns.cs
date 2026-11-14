using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Idea 1: nerf guns for teacher and students.
    ///
    /// Guns are rare finds, not something everyone gets. At the start of class the host rolls
    /// for each player: a student who wins finds a gun lying in their desk drawer, a teacher
    /// who wins finds one floating in the teacher's office. Every gun has one shot.
    ///
    /// A student who is hit goes straight up on a pole and can't be rescued; a teacher who is
    /// hit falls over like on a banana peel. The RPG hits everybody in the class, the shooter
    /// included. The shooter's own game works out what the dart hit; the host checks it and
    /// applies it.
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
        // Shrink factors that make each gun fit in a desk drawer.
        private static readonly float[] DrawerScale = { 1f, 0.85f, 0.5f, 0.3f, 0.3f };
        private static readonly Color32[] Tint =
        {
            new Color32(255, 255, 255, 255), new Color32(255, 130, 20, 255), new Color32(40, 120, 255, 255),
            new Color32(60, 200, 80, 255), new Color32(230, 40, 40, 255)
        };

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
        private static bool _drawerGunInReach;
        private static float _nextShotAt;
        private static float _flashUntil;
        private static Texture2D _white;

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
        private static bool _dealt;

        public static void Tick()
        {
            if (!Game.InClass)
            {
                Clear();
                return;
            }
            if (NetworkServer.active && !_dealt)
            {
                _dealt = true;
                if (Plugin.Settings.nerfGuns)
                {
                    DealGuns();
                }
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
            _dealt = false;
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
            _drawerGunInReach = false;
            if (_drawerModel != null)
            {
                UnityEngine.Object.Destroy(_drawerModel);
                _drawerModel = null;
            }
        }

        /// <summary>The drawer of the desk the player sits at, or null.</summary>
        private static DrawerComponent MyDrawer(Unit me)
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

            UnitManager units = Game.Peek<UnitManager>();
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
                }
                held.Model.SetActive(true);
                if (held.Gun == Rpg)
                {
                    // Carried on the right shoulder.
                    held.Model.transform.position = body.position + body.right * 0.22f + Vector3.up * 1.42f + body.forward * 0.12f;
                }
                else
                {
                    // Held out in front at chest height, pointing where the body faces.
                    held.Model.transform.position = body.position + body.right * 0.24f + Vector3.up * 1.10f + body.forward * 0.34f;
                }
                held.Model.transform.rotation = body.rotation;
            }

            // The gun this player found lies in their own desk drawer and shows while it is open.
            _drawerGunInReach = false;
            if (_drawerGun == 0)
            {
                return;
            }
            DrawerComponent drawer = MyDrawer(Game.LocalPlayer);
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
            _drawerModel.SetActive(true);
            _drawerModel.transform.position = spot + Vector3.up * 0.07f;
            // Lying on its side, along the width of the drawer.
            _drawerModel.transform.rotation = tray.rotation * Quaternion.Euler(0f, 90f, 90f);
            _drawerGunInReach = true;
        }

        // -------------------------------------------------------------- local keys

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
            if (Input.GetKeyDown(Plugin.Settings.UseKeyCode) && !blocked)
            {
                int near = NearestCrate(me.Position);
                if (mine == null && _drawerGunInReach)
                {
                    Net.SendToHost(new CpAsk { kind = CpKind.GunTake, i = DrawerSlot });
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
        private static Unit Trace(UnitManager units, Unit me, Vector3 from, Vector3 direction, float reach, out Vector3 end)
        {
            end = from + direction * reach;
            RaycastHit[] hits = Physics.RaycastAll(from, direction, reach, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
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

        /// <summary>Host, once per class: decide who finds a gun.</summary>
        private static void DealGuns()
        {
            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            UnitManager units = Game.Peek<UnitManager>();
            if (classroom == null || units == null)
            {
                return;
            }
            Settings settings = Plugin.Settings;
            float studentChance = settings.nerfAlwaysForTesting ? 1f : Mathf.Clamp01(settings.nerfStudentChance);
            float teacherChance = settings.nerfAlwaysForTesting ? 1f : Mathf.Clamp01(settings.nerfTeacherChance);
            int teacherGuns = 0;
            foreach (Unit player in new List<Unit>(units.playerList))
            {
                if (player == null || !player.isActived || !player.isPlayer)
                {
                    continue;
                }
                if (player.IsStudent)
                {
                    if (UnityEngine.Random.value < studentChance)
                    {
                        int gun = RollKind();
                        HostDrawerGun[player.UID] = gun;
                        Net.TellPlayer(player, new CpTell { kind = CpKind.Crate, i = DrawerSlot, j = gun });
                        Plugin.Log("nerf guns: " + Poles.NameOf(player) + " has a " + Names[gun] + " in their drawer");
                    }
                }
                else if (player.IsTeacher && UnityEngine.Random.value < teacherChance)
                {
                    teacherGuns++;
                }
            }

            ClassroomItem room = TeacherOffice.FindClassroom(classroom.currentFloor);
            if (teacherGuns > 0 && (room == null || room.teacherPos == null))
            {
                Plugin.Log("nerf guns: no teacher start point on floor " + classroom.currentFloor + ", teacher gun skipped");
                teacherGuns = 0;
            }
            for (int i = 0; i < Mathf.Min(teacherGuns, CrateCount); i++)
            {
                int gun = RollKind();
                // In the teacher's office, where the teacher stands before class.
                Vector3 spot = room.teacherPos.position + room.teacherPos.right * (i * 0.9f);
                HostCrateGun[i] = gun;
                HostCratePosition[i] = spot;
                Net.SendToModded(new CpTell { kind = CpKind.Crate, i = i, j = gun, v = spot });
                Plugin.Log("nerf guns: " + Names[gun] + " for the teacher at " + spot.ToString("F2"));
            }
            if (HostDrawerGun.Count == 0 && teacherGuns == 0)
            {
                Plugin.Log("nerf guns: nobody found one this class");
            }
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
            if (target == null || !target.isActived || target.IsDead)
            {
                return;
            }
            if (target.IsTeacher)
            {
                Poles.Slip(target);
                return;
            }
            if (!target.IsStudent)
            {
                return;
            }
            if (!target.isPlayer)
            {
                // A computer-controlled classmate just gets knocked over.
                if (!blast)
                {
                    Poles.Slip(target);
                }
                return;
            }
            if (Poles.PutOnPole(target, true))
            {
                Net.Toast(target, target == shooter ? "Your own RPG got you. No rescue from this pole." : Poles.NameOf(shooter) + " hit you with a nerf dart. No rescue from this pole.", 4f);
            }
            else if (Poles.CanBeHung(target))
            {
                // Every pole is taken, so the dart only knocks them over.
                Poles.Slip(target);
            }
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
                            Game.Notify("There is something in your desk drawer...", 4f);
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
            if (_drawerGunInReach)
            {
                return "Press " + Plugin.Settings.UseKeyCode + " to take the " + Names[_drawerGun] + " from your drawer";
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
