using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Idea 1: nerf guns for teacher and students. Four guns float around the classroom at the
    /// start of class. Anyone can take one. A student who is hit goes straight up on a pole
    /// and can't be rescued; a teacher who is hit falls over like on a banana peel. The RPG
    /// has one shot and hits everybody in the class, the shooter included.
    ///
    /// The shooter's own game works out what the dart hit; the host checks it and applies it.
    /// The guns are block models built in code until there are real ones.
    /// </summary>
    internal static class NerfGuns
    {
        public const int Pistol = 1;
        public const int Shotgun = 2;
        public const int Sniper = 3;
        public const int Rpg = 4;

        private static readonly string[] Names = { "", "Nerf pistol", "Nerf shotgun", "Nerf sniper", "Nerf RPG" };
        private static readonly int[] Shots = { 0, 15, 6, 5, 1 };
        private static readonly float[] Reach = { 0f, 22f, 8f, 70f, 0f };
        private static readonly float[] Wait = { 0f, 0.45f, 1.1f, 1.6f, 1f };
        private static readonly Color32[] Tint =
        {
            new Color32(255, 255, 255, 255), new Color32(255, 130, 20, 255), new Color32(40, 120, 255, 255),
            new Color32(60, 200, 80, 255), new Color32(230, 40, 40, 255)
        };

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
        private static float _nextShotAt;
        private static float _flashUntil;
        private static Texture2D _white;

        // What only the host keeps
        private sealed class HostGun
        {
            public int Gun;
            public int Shots;
            public float NextAt;
        }

        private static readonly int[] HostCrateGun = new int[CrateCount];
        private static readonly Vector3[] HostCratePosition = new Vector3[CrateCount];
        private static readonly Dictionary<ulong, HostGun> HostGuns = new Dictionary<ulong, HostGun>();
        private static bool _spawned;

        public static void Tick()
        {
            if (!Game.InClass)
            {
                Clear();
                return;
            }
            if (NetworkServer.active && !_spawned)
            {
                _spawned = true;
                if (Plugin.Settings.nerfGuns)
                {
                    SpawnCrates();
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
            HostGuns.Clear();
            _spawned = false;
        }

        // ------------------------------------------------------------------ models

        private static GameObject BuildGun(int gun)
        {
            var root = new GameObject("[ClassPack " + Names[gun] + "]");
            Transform t = root.transform;
            Color32 orange = new Color32(255, 130, 20, 255);
            Color32 blue = new Color32(40, 120, 255, 255);
            Color32 yellow = new Color32(250, 215, 40, 255);
            Color32 green = new Color32(60, 200, 80, 255);
            Color32 grey = new Color32(120, 125, 135, 255);
            Color32 dark = new Color32(30, 30, 34, 255);
            Color32 red = new Color32(230, 40, 40, 255);
            switch (gun)
            {
                case Pistol:
                    Shapes.Part(t, PrimitiveType.Cube, new Vector3(0f, 0f, 0.10f), new Vector3(0.05f, 0.07f, 0.24f), orange);
                    Shapes.Part(t, PrimitiveType.Cube, new Vector3(0f, -0.08f, 0.02f), new Vector3(0.045f, 0.11f, 0.06f), blue);
                    Shapes.Part(t, PrimitiveType.Cube, new Vector3(0f, 0f, 0.24f), new Vector3(0.03f, 0.03f, 0.04f), yellow);
                    break;
                case Shotgun:
                    Shapes.Part(t, PrimitiveType.Cube, new Vector3(0f, 0f, 0.25f), new Vector3(0.07f, 0.08f, 0.55f), blue);
                    Shapes.Part(t, PrimitiveType.Cube, new Vector3(0f, -0.05f, 0.35f), new Vector3(0.06f, 0.05f, 0.16f), orange);
                    Shapes.Part(t, PrimitiveType.Cube, new Vector3(0f, -0.03f, -0.08f), new Vector3(0.05f, 0.10f, 0.18f), orange);
                    break;
                case Sniper:
                    Shapes.Part(t, PrimitiveType.Cube, new Vector3(0f, 0f, 0.45f), new Vector3(0.04f, 0.04f, 0.90f), green);
                    Shapes.Part(t, PrimitiveType.Cube, new Vector3(0f, 0f, 0.10f), new Vector3(0.06f, 0.09f, 0.40f), yellow);
                    Shapes.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0.15f), new Vector3(0.05f, 0.09f, 0.05f), dark, new Vector3(90f, 0f, 0f));
                    Shapes.Part(t, PrimitiveType.Cube, new Vector3(0f, -0.03f, -0.20f), new Vector3(0.05f, 0.10f, 0.25f), green);
                    break;
                default:
                    Shapes.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.25f), new Vector3(0.13f, 0.40f, 0.13f), grey, new Vector3(90f, 0f, 0f));
                    Shapes.Part(t, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.72f), new Vector3(0.20f, 0.20f, 0.30f), red);
                    Shapes.Part(t, PrimitiveType.Cube, new Vector3(0f, -0.12f, 0.15f), new Vector3(0.04f, 0.14f, 0.05f), orange);
                    break;
            }
            return root;
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
                    crate.Model.transform.localScale = Vector3.one * 1.6f;
                }
                crate.Model.transform.position = crate.Position + Vector3.up * (1.05f + bob);
                crate.Model.transform.rotation = Quaternion.Euler(-15f, spin, 0f);
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
                // Carried at chest height, pointing where the body faces.
                held.Model.SetActive(true);
                held.Model.transform.position = body.position + body.right * 0.26f + Vector3.up * 1.12f + body.forward * 0.36f;
                held.Model.transform.rotation = body.rotation;
            }
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
                if (mine == null && near >= 0)
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
                _nextShotAt = Time.time + Wait[mine.Gun];
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

        private static void SpawnCrates()
        {
            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            UnitManager units = Game.Peek<UnitManager>();
            if (classroom == null || units == null)
            {
                return;
            }
            ClassroomItem room = TeacherOffice.FindClassroom(classroom.currentFloor);
            var spots = new List<Vector3>();
            Vector3 centre = room != null ? room.transform.position : (Game.LocalPlayer != null ? Game.LocalPlayer.Position : Vector3.zero);
            float floorY = centre.y;
            if (room != null && room.studentPos != null)
            {
                floorY = room.studentPos.position.y;
                AddSpot(spots, room.studentPos.position, floorY);
            }
            if (room != null && room.teacherPos != null)
            {
                AddSpot(spots, room.teacherPos.position, floorY);
            }
            foreach (Unit unit in units.unitsDict.Values)
            {
                if (unit == null || !unit.isActived || unit.transform == null)
                {
                    continue;
                }
                EUnitSubType kind = unit.UnitSubType;
                if (kind != EUnitSubType.ClassroomDoor && kind != EUnitSubType.ClassroomBackDoor && kind != EUnitSubType.BlackBoard)
                {
                    continue;
                }
                Vector3 offset = unit.Position - centre;
                if (Mathf.Abs(offset.y) > 3.5f || new Vector2(offset.x, offset.z).magnitude > 35f)
                {
                    continue;
                }
                Vector3 spot = unit.Position;
                if (kind == EUnitSubType.BlackBoard && room != null && room.studentPos != null)
                {
                    // Step off the wall, towards the seats.
                    Vector3 toSeats = room.studentPos.position - spot;
                    toSeats.y = 0f;
                    spot += toSeats.normalized * 1.6f;
                }
                AddSpot(spots, spot, floorY);
            }

            int[] guns = { Pistol, Shotgun, Sniper, Rpg };
            for (int i = guns.Length - 1; i > 0; i--)
            {
                int swap = UnityEngine.Random.Range(0, i + 1);
                int keep = guns[i];
                guns[i] = guns[swap];
                guns[swap] = keep;
            }
            int count = Mathf.Min(CrateCount, spots.Count);
            for (int i = 0; i < count; i++)
            {
                HostCrateGun[i] = guns[i];
                HostCratePosition[i] = spots[i];
                Net.SendToModded(new CpTell { kind = CpKind.Crate, i = i, j = guns[i], v = spots[i] });
                Plugin.Log("nerf guns: " + Names[guns[i]] + " at " + spots[i].ToString("F2"));
            }
            if (count == 0)
            {
                Plugin.Log("nerf guns: found nowhere to put them on floor " + classroom.currentFloor);
            }
        }

        private static void AddSpot(List<Vector3> spots, Vector3 spot, float floorY)
        {
            spot.y = floorY;
            foreach (Vector3 other in spots)
            {
                if ((other - spot).sqrMagnitude < 2.5f * 2.5f)
                {
                    return;
                }
            }
            spots.Add(spot);
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
                    if (gun == null && ask.i >= 0 && ask.i < CrateCount && HostCrateGun[ask.i] != 0
                        && (player.Position - HostCratePosition[ask.i]).sqrMagnitude < 4.5f * 4.5f)
                    {
                        int taken = HostCrateGun[ask.i];
                        HostCrateGun[ask.i] = 0;
                        HostGuns[player.UID] = new HostGun { Gun = taken, Shots = Shots[taken] };
                        Net.SendToModded(new CpTell { kind = CpKind.Crate, i = ask.i, j = 0, v = HostCratePosition[ask.i] });
                        Net.SendToModded(new CpTell { kind = CpKind.GunState, a = player.UID, i = taken, j = Shots[taken] });
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
                    if (gun != null && gun.Shots > 0 && Time.time >= gun.NextAt && !player.IsStun && !player.BodyActionsBlocked)
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
            gun.NextAt = Time.time + Wait[gun.Gun] - 0.1f;
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
                    if (tell.i >= 0 && tell.i < CrateCount)
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

        /// <summary>Crosshair, ammo line and pick-up hint. Drawn in a 1280 by 720 frame.</summary>
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
                return Names[mine.Gun] + "  " + mine.Shots + "/" + Shots[mine.Gun] + "     [" + Plugin.Settings.FireKeyCode
                    + " or wheel click] shoot     [" + Plugin.Settings.UseKeyCode + "] throw away";
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
