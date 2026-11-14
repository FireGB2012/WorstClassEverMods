using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>A soaked blackboard can't be written on, so it gives the teacher no stamina.</summary>
    [HarmonyLib.HarmonyPatch(typeof(BlackBoardComponent), nameof(BlackBoardComponent.Interect))]
    internal static class SoakedBoardPatch
    {
        private static bool Prefix(BlackBoardComponent __instance, eCmdType __0, eClickType __1)
        {
            if (__0 != eCmdType.Interect1 || __1 != eClickType.Down)
            {
                return true;
            }
            try
            {
                return !(Tricks.BoardIsSoaked(__instance) || Teachers.NoBoard());
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    /// <summary>A teacher with chalk glued to the hand can't throw chalk or the eraser.</summary>
    [HarmonyLib.HarmonyPatch(typeof(SkillComponent), "GetCmdInput")]
    internal static class GluedHandPatch
    {
        private static bool Prefix(SkillComponent __instance, eCmdType __0, eClickType __1)
        {
            if (__0 != eCmdType.UseItem && __0 != eCmdType.UseItem2)
            {
                return true;
            }
            try
            {
                return !(Tricks.HandIsGlued(__instance.owner, __1 == eClickType.Down)
                    || Teachers.NoThrowing(__instance.owner, __1 == eClickType.Down));
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    /// <summary>
    /// Pack 3, the student tricks (ideas 23, 24, 25, 26, 27, 30 and 31).
    ///
    /// All seven are picked from the Tricks app on the phone. Five go in the hand and are
    /// used with the same key that fires a nerf gun: paper airplane, water balloon, super
    /// glue, laser pointer and whoopee cushion. Passing a note and taking a nap start
    /// straight from the phone.
    ///
    /// As everywhere in the mod, the player's own game works out what was aimed at and the
    /// host decides what happens, hands out the mischief and tells everybody.
    /// </summary>
    internal static class Tricks
    {
        public const int Airplane = 1;
        public const int Balloon = 2;
        public const int Glue = 3;
        public const int Laser = 4;
        public const int Cushion = 5;
        public const int Note = 6;
        public const int Nap = 7;
        private const int Kinds = 8;

        public static readonly string[] Names =
        {
            "", "Paper airplane", "Water balloon", "Super glue", "Laser pointer", "Whoopee cushion", "Pass a note", "Take a nap"
        };

        public static readonly string[] Blurbs =
        {
            "",
            "Hit the back of the teacher's head.",
            "Soak the blackboard: no writing, no stamina.",
            "Glue the teacher's chalk hand, or the floor.",
            "Blind the teacher. The red line gives you away.",
            "Leave it on a seat or the floor.",
            "Mischief for every desk it passes.",
            "Slow mischief, but you snore."
        };

        private static readonly string[] ModelNames = { "", "airplane", "balloon", "glue", "laser", "cushion", "note", "" };
        private static readonly string[] UseWords = { "", "throw", "throw", "squirt", "hold to shine", "put down", "", "" };
        private static readonly float[] Waits = { 0f, 15f, 40f, 25f, 0f, 20f, 30f, 0f };
        private static readonly float[] Reach = { 0f, 16f, 14f, 5f, 30f, 4f, 0f, 0f };

        private const int GlueUses = 2;
        private const float LaserBattery = 6f;
        private const float LaserStep = 0.12f;
        private const float SoakSeconds = 20f;
        private const float GluedHandSeconds = 10f;
        private const float GluedFeetSeconds = 4f;

        private static readonly Color32 Red = new Color32(255, 30, 30, 255);

        // ------------------------------------------------------------ what every game knows

        private sealed class Hand
        {
            public int Item;
            public GameObject Model;
        }

        private sealed class Flight
        {
            public int Item;
            public int Result;
            public GameObject Model;
            public Vector3 From;
            public Vector3 To;
            public float Start;
            public float Duration;
            public float Arc;
            public ulong BoardUID;
            public float Soak;
        }

        private sealed class Thing
        {
            public GameObject Model;
            public float RemoveAt;
        }

        private sealed class NoteView
        {
            public GameObject Model;
            public Vector3 From;
            public Vector3 To;
            public float Start;
            public float Duration;
        }

        private sealed class Sleeper
        {
            public float Loud;
            public float Until;
            public Vector3 At;
        }

        private static readonly Dictionary<ulong, Hand> Hands = new Dictionary<ulong, Hand>();
        private static readonly List<Flight> Flights = new List<Flight>();
        private static readonly List<Thing> Leftovers = new List<Thing>();
        private static readonly Dictionary<int, GameObject> Puddles = new Dictionary<int, GameObject>();
        private static readonly Dictionary<int, GameObject> Cushions = new Dictionary<int, GameObject>();
        private static readonly Dictionary<int, NoteView> Notes = new Dictionary<int, NoteView>();
        private static readonly Dictionary<ulong, Sleeper> Sleepers = new Dictionary<ulong, Sleeper>();
        private static readonly Dictionary<ulong, float> Soaked = new Dictionary<ulong, float>();

        // This player's own side of things
        private static readonly float[] ReadyAt = new float[Kinds];
        private static int _glueLeft = GlueUses;
        private static float _battery = LaserBattery;
        private static float _laserHeldAt = -10f;
        private static float _nextLaserAt;
        private static float _whiteUntil;
        private static float _laserNoticeAt = -10f;
        private static float _handGluedUntil;
        private static float _noticeAt = -10f;
        private static bool _napping;
        private static float _napStartedAt;
        private static float _napEarned;
        private static bool _dirty;
        private static Texture2D _white;
        private static GUIStyle _zzz;
        private static GUIStyle _napText;

        // ------------------------------------------------------------- what only the host keeps

        private sealed class Spot
        {
            public int Id;
            public Vector3 At;
            public ulong Owner;
            public float ArmedAt;
            public float Until;
        }

        private sealed class HostNote
        {
            public int Id;
            public Vector3 From;
            public Vector3 To;
            public float Start;
            public float Duration;
            public ulong Owner;
            public float Points;
        }

        private sealed class HostLaser
        {
            public float Battery = LaserBattery;
            public float LastAt;
            public float Blinding;
        }

        private sealed class HostNap
        {
            public float Since;
            public float NextAt;
        }

        private static readonly Dictionary<ulong, int> HostHands = new Dictionary<ulong, int>();
        private static readonly Dictionary<ulong, float[]> HostReadyAt = new Dictionary<ulong, float[]>();
        private static readonly Dictionary<ulong, int> HostGlueUsed = new Dictionary<ulong, int>();
        private static readonly Dictionary<ulong, HostLaser> HostLasers = new Dictionary<ulong, HostLaser>();
        private static readonly Dictionary<ulong, HostNap> HostNaps = new Dictionary<ulong, HostNap>();
        private static readonly List<Spot> HostPuddles = new List<Spot>();
        private static readonly List<Spot> HostCushions = new List<Spot>();
        private static readonly List<HostNote> HostNotes = new List<HostNote>();
        private static int _nextId = 1;
        private static float _nextScanAt;

        // --------------------------------------------------------------------- every frame

        public static void Tick()
        {
            if (!Game.InClass)
            {
                Clear();
                return;
            }
            _dirty = true;
            Animate();
            HandleKeys();
            if (NetworkServer.active)
            {
                HostTick();
            }
        }

        private static void Clear()
        {
            if (!_dirty)
            {
                return;
            }
            _dirty = false;
            foreach (Hand hand in Hands.Values)
            {
                Remove(hand.Model);
            }
            Hands.Clear();
            foreach (Flight flight in Flights)
            {
                Remove(flight.Model);
            }
            Flights.Clear();
            foreach (Thing thing in Leftovers)
            {
                Remove(thing.Model);
            }
            Leftovers.Clear();
            foreach (GameObject model in Puddles.Values)
            {
                Remove(model);
            }
            Puddles.Clear();
            foreach (GameObject model in Cushions.Values)
            {
                Remove(model);
            }
            Cushions.Clear();
            foreach (NoteView note in Notes.Values)
            {
                Remove(note.Model);
            }
            Notes.Clear();
            Sleepers.Clear();
            Soaked.Clear();
            Array.Clear(ReadyAt, 0, ReadyAt.Length);
            _glueLeft = GlueUses;
            _battery = LaserBattery;
            _whiteUntil = 0f;
            _handGluedUntil = 0f;
            _napping = false;

            HostHands.Clear();
            HostReadyAt.Clear();
            HostGlueUsed.Clear();
            HostLasers.Clear();
            HostNaps.Clear();
            HostPuddles.Clear();
            HostCushions.Clear();
            HostNotes.Clear();
        }

        private static void Remove(GameObject model)
        {
            if (model != null)
            {
                UnityEngine.Object.Destroy(model);
            }
        }

        // -------------------------------------------------------------------------- models

        internal static GameObject Build(string name, Color32 fallback, float fallbackSize)
        {
            GameObject model = Models.Build(name);
            if (model != null)
            {
                return model;
            }
            // The model file is missing: a plain ball keeps the trick usable.
            var root = new GameObject("[ClassPack " + name + "]");
            Shapes.Part(root.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * fallbackSize, fallback);
            return root;
        }

        private static GameObject BuildItem(int item)
        {
            return Build(ModelNames[item], new Color32(240, 240, 240, 255), 0.1f);
        }

        /// <summary>The floor, seat or desk top right under a point.</summary>
        internal static Vector3 Settle(Vector3 point)
        {
            RaycastHit hit;
            if (Physics.Raycast(point + Vector3.up * 0.08f, Vector3.down, out hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }
            return point;
        }

        internal static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static Quaternion Facing(Vector3 direction, Quaternion fallback)
        {
            return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : fallback;
        }

        private static void Animate()
        {
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null)
            {
                return;
            }
            float now = Time.time;

            // Whatever a player holds floats in front of their chest, like the nerf guns do.
            foreach (KeyValuePair<ulong, Hand> pair in Hands)
            {
                Hand hand = pair.Value;
                Unit holder = units.GetUnitByUID(pair.Key);
                Transform body = holder != null && holder.isActived ? holder.transform : null;
                if (body == null)
                {
                    if (hand.Model != null)
                    {
                        hand.Model.SetActive(false);
                    }
                    continue;
                }
                if (hand.Model == null)
                {
                    hand.Model = BuildItem(hand.Item);
                }
                hand.Model.SetActive(true);
                hand.Model.transform.position = body.position + body.right * 0.22f + Vector3.up * 1.12f + body.forward * 0.36f;
                hand.Model.transform.rotation = body.rotation;
            }

            for (int i = Flights.Count - 1; i >= 0; i--)
            {
                Flight flight = Flights[i];
                float t = (now - flight.Start) / flight.Duration;
                if (t < 1f)
                {
                    if (flight.Model != null)
                    {
                        Vector3 rise = Vector3.up * flight.Arc;
                        flight.Model.transform.position = Vector3.Lerp(flight.From, flight.To, t) + rise * (4f * t * (1f - t));
                        Vector3 heading = (flight.To - flight.From) / flight.Duration + rise * (4f * (1f - 2f * t)) / flight.Duration;
                        flight.Model.transform.rotation = Facing(heading, flight.Model.transform.rotation);
                    }
                    continue;
                }
                Flights.RemoveAt(i);
                Land(flight);
            }

            for (int i = Leftovers.Count - 1; i >= 0; i--)
            {
                if (now >= Leftovers[i].RemoveAt)
                {
                    Remove(Leftovers[i].Model);
                    Leftovers.RemoveAt(i);
                }
            }

            foreach (NoteView note in Notes.Values)
            {
                if (note.Model == null)
                {
                    continue;
                }
                float t = Mathf.Clamp01((now - note.Start) / note.Duration);
                // It hops from desk to desk rather than gliding.
                float hops = Mathf.Max(2f, Mathf.Round((note.To - note.From).magnitude / 1.2f));
                note.Model.transform.position = Vector3.Lerp(note.From, note.To, t) + Vector3.up * (Mathf.Abs(Mathf.Sin(t * hops * Mathf.PI)) * 0.24f);
                note.Model.transform.rotation = Quaternion.Euler(0f, now * 140f, 0f);
            }

            if (Sleepers.Count > 0)
            {
                var awake = new List<ulong>();
                foreach (KeyValuePair<ulong, Sleeper> pair in Sleepers)
                {
                    if (now >= pair.Value.Until)
                    {
                        awake.Add(pair.Key);
                    }
                }
                foreach (ulong uid in awake)
                {
                    Sleepers.Remove(uid);
                }
            }

            // The laser's battery fills back up once it has been off for half a second.
            if (now - _laserHeldAt > 0.5f && _battery < LaserBattery)
            {
                _battery = Mathf.Min(LaserBattery, _battery + Time.deltaTime * 0.6f);
            }
        }

        private static void Land(Flight flight)
        {
            if (flight.Item == Airplane)
            {
                if (flight.Result == 1 || flight.Result == 2)
                {
                    // It bounced off the teacher.
                    Game.PlaySoundAt(AudioEvents.sfx_ChalkHit_sfx, flight.To);
                    Remove(flight.Model);
                    return;
                }
                // A miss lies on the floor with its nose pointing back at whoever threw it.
                if (flight.Model != null)
                {
                    Vector3 rest = Settle(flight.To) + Vector3.up * 0.03f;
                    flight.Model.transform.position = rest;
                    flight.Model.transform.rotation = Facing(Flat(flight.From - rest), flight.Model.transform.rotation);
                    Leftovers.Add(new Thing { Model = flight.Model, RemoveAt = Time.time + 25f });
                }
                Game.PlaySoundAt(AudioEvents.sfx_FoldPaper_sfx, flight.To);
                return;
            }

            // Water balloon: it bursts and leaves a wet patch facing back the way it came.
            Remove(flight.Model);
            Game.PlaySoundAt(AudioEvents.sfx_InkBottleHit_sfx, flight.To);
            Vector3 way = flight.To - flight.From;
            way = way.sqrMagnitude > 0.0001f ? way.normalized : Vector3.forward;
            // The patch lies flat on whatever it hit: wall, board or floor.
            Vector3 facing = -way;
            RaycastHit hit;
            if (Physics.Raycast(flight.To - way * 0.4f, way, out hit, 0.9f, ~0, QueryTriggerInteraction.Ignore))
            {
                facing = hit.normal;
            }
            GameObject splat = Build("splat", new Color32(60, 140, 235, 255), 0.3f);
            splat.transform.position = flight.To + facing * 0.03f;
            splat.transform.rotation = Quaternion.FromToRotation(Vector3.up, facing);
            bool soaked = flight.Result == 1 && flight.BoardUID != 0uL;
            Leftovers.Add(new Thing { Model = splat, RemoveAt = Time.time + (soaked ? flight.Soak : 4f) });
            if (soaked)
            {
                Soaked[flight.BoardUID] = Time.time + flight.Soak;
            }
        }

        // ------------------------------------------------------------- this player's own keys

        /// <summary>What this player holds, or 0.</summary>
        public static int HeldLocal
        {
            get
            {
                Unit me = Game.LocalPlayer;
                Hand hand;
                return me != null && Hands.TryGetValue(me.UID, out hand) ? hand.Item : 0;
            }
        }

        public static bool Napping => _napping;

        /// <summary>Why the Tricks app can't be used right now, or null when it can.</summary>
        public static string WhyNot()
        {
            Unit me = Game.LocalPlayer;
            if (!Plugin.Settings.tricks)
            {
                return "Tricks are switched off in the mod's settings.";
            }
            if (me == null || !me.IsStudent)
            {
                return "Tricks are for students.";
            }
            if (!Game.InClass)
            {
                return "Wait until class has started.";
            }
            if (!Net.CanSendToHost)
            {
                return "The room owner doesn't have Class Pack.";
            }
            if (me.IsStun || me.BodyActionsBlocked)
            {
                return "You can't do that right now.";
            }
            return null;
        }

        /// <summary>A short status for one trick on the phone, or null when it is ready.</summary>
        public static string StatusOf(int item)
        {
            float now = Time.time;
            if (item == Glue && _glueLeft <= 0)
            {
                return "empty";
            }
            if (item == Laser)
            {
                return _battery >= LaserBattery - 0.05f ? null : Mathf.RoundToInt(_battery / LaserBattery * 100f) + "%";
            }
            if (item == Nap)
            {
                Unit me = Game.LocalPlayer;
                return me != null && me.IsSitting ? null : "sit first";
            }
            if (now < ReadyAt[item])
            {
                return Mathf.CeilToInt(ReadyAt[item] - now) + "s";
            }
            return item == Glue ? _glueLeft + " left" : null;
        }

        public static bool CanPick(int item)
        {
            if (WhyNot() != null)
            {
                return false;
            }
            if (item == Glue)
            {
                return _glueLeft > 0 && Time.time >= ReadyAt[item];
            }
            if (item == Laser)
            {
                return true;
            }
            if (item == Nap)
            {
                Unit me = Game.LocalPlayer;
                return me != null && me.IsSitting;
            }
            return Time.time >= ReadyAt[item];
        }

        /// <summary>Tapped on the phone. True when the phone should be put away afterwards.</summary>
        public static bool Pick(int item)
        {
            if (!CanPick(item))
            {
                return false;
            }
            if (item == Note)
            {
                ReadyAt[Note] = Time.time + Waits[Note];
                Game.PlaySound(AudioEvents.sfx_FoldPaper_sfx);
                Net.SendToHost(new CpAsk { kind = CpKind.NoteAsk });
                return true;
            }
            if (item == Nap)
            {
                _napping = true;
                _napStartedAt = Time.time;
                _napEarned = 0f;
                Net.SendToHost(new CpAsk { kind = CpKind.NapAsk, i = 1 });
                return true;
            }
            if (NerfGuns.HoldingLocal)
            {
                Game.Notify("Your hands are full. Throw the nerf gun away first.", 3f);
                return false;
            }
            Game.PlaySound(AudioEvents.sfx_ItemPickup_sfx);
            Net.SendToHost(new CpAsk { kind = CpKind.TrickHold, i = item });
            return true;
        }

        private static void StopNap()
        {
            if (_napping)
            {
                _napping = false;
                Net.SendToHost(new CpAsk { kind = CpKind.NapAsk, i = 0 });
            }
        }

        private static void HandleKeys()
        {
            Unit me = Game.LocalPlayer;
            if (me == null || !me.isActived || !Net.CanSendToHost)
            {
                return;
            }
            bool blocked = me.IsStun || me.BodyActionsBlocked;
            if (_napping)
            {
                if (blocked || !me.IsSitting || (Time.time > _napStartedAt + 0.7f && Input.anyKeyDown))
                {
                    StopNap();
                }
                return;
            }
            int item = HeldLocal;
            if (item == 0 || CursorGrab.Busy || Game.IsTyping || blocked)
            {
                return;
            }
            if (Input.GetKeyDown(Plugin.Settings.UseKeyCode))
            {
                Net.SendToHost(new CpAsk { kind = CpKind.TrickHold, i = 0 });
                return;
            }
            if (item == Laser)
            {
                if (Input.GetKey(Plugin.Settings.FireKeyCode) || Input.GetMouseButton(2))
                {
                    Shine(me);
                }
                return;
            }
            if (Input.GetKeyDown(Plugin.Settings.FireKeyCode) || Input.GetMouseButtonDown(2))
            {
                Use(me, item);
            }
        }

        internal static void Tip(string text)
        {
            if (Time.time - _noticeAt > 1.5f)
            {
                _noticeAt = Time.time;
                Game.Notify(text, 2.5f);
            }
        }

        private static void Use(Unit me, int item)
        {
            Camera camera = Camera.main;
            UnitManager units = Game.Peek<UnitManager>();
            if (camera == null || units == null)
            {
                return;
            }
            if (Time.time < ReadyAt[item])
            {
                Tip("Not ready for another " + Mathf.CeilToInt(ReadyAt[item] - Time.time) + " seconds.");
                return;
            }
            Vector3 from = camera.transform.position;
            Vector3 end;
            Unit target = NerfGuns.Trace(units, me, from, camera.transform.forward, Reach[item], out end);
            bool touched = target != null || (end - from).magnitude < Reach[item] - 0.02f;
            var ask = new CpAsk { kind = CpKind.TrickUse, i = item, v = from, w = end, t0 = target != null ? target.UID : 0uL };

            if (item == Glue)
            {
                if (target == null || !target.IsTeacher)
                {
                    if (!touched)
                    {
                        Tip("Aim at the teacher, or at the floor close to you.");
                        return;
                    }
                    ask.t0 = 0uL;
                    ask.w = Settle(end);
                }
                _glueLeft--;
            }
            else if (item == Cushion)
            {
                if (!touched)
                {
                    Tip("Aim at a seat or at the floor close to you.");
                    return;
                }
                ask.w = Settle(end);
            }
            else if (item == Balloon)
            {
                ask.t1 = BoardOnTheWay(units, from, camera.transform.forward, (end - from).magnitude + 0.4f);
            }
            ReadyAt[item] = Time.time + Waits[item];
            Net.SendToHost(ask);
        }

        /// <summary>The blackboard the balloon's path runs into, or 0.</summary>
        private static ulong BoardOnTheWay(UnitManager units, Vector3 from, Vector3 direction, float reach)
        {
            foreach (RaycastHit hit in Physics.RaycastAll(from, direction, reach, ~0, QueryTriggerInteraction.Collide))
            {
                ObjectModel model = hit.collider.GetComponentInParent<ObjectModel>();
                Unit unit = model != null ? units.GetUnitByUID(model.UID) : null;
                if (unit != null && unit.GetComponent<BlackBoardComponent>() != null)
                {
                    return unit.UID;
                }
            }
            return 0uL;
        }

        private static void Shine(Unit me)
        {
            _laserHeldAt = Time.time;
            if (_battery <= 0f)
            {
                Tip("The laser's battery is flat. Let it rest for a moment.");
                return;
            }
            _battery = Mathf.Max(0f, _battery - Time.deltaTime);
            if (Time.time < _nextLaserAt)
            {
                return;
            }
            _nextLaserAt = Time.time + LaserStep;
            Camera camera = Camera.main;
            UnitManager units = Game.Peek<UnitManager>();
            if (camera == null || units == null)
            {
                return;
            }
            Vector3 from = camera.transform.position;
            Vector3 end;
            Unit target = NerfGuns.Trace(units, me, from, camera.transform.forward, Reach[Laser], out end);
            Net.SendToHost(new CpAsk { kind = CpKind.LaserAsk, v = from, w = end, t0 = target != null ? target.UID : 0uL });
        }

        // -------------------------------------------------------------------- patches ask

        public static bool BoardIsSoaked(BlackBoardComponent board)
        {
            float until;
            if (board == null || board.owner == null || !Game.InClass || !Soaked.TryGetValue(board.owner.UID, out until))
            {
                return false;
            }
            if (Time.time >= until)
            {
                Soaked.Remove(board.owner.UID);
                return false;
            }
            Tip("The blackboard is soaking wet. " + Mathf.CeilToInt(until - Time.time) + " more seconds.");
            return true;
        }

        public static bool HandIsGlued(Unit teacher, bool say)
        {
            if (teacher == null || !teacher.isLocalPlayer || !teacher.IsTeacher || !Game.InClass || Time.time >= _handGluedUntil)
            {
                return false;
            }
            if (say)
            {
                Tip("The chalk is glued to your hand! " + Mathf.CeilToInt(_handGluedUntil - Time.time) + " more seconds.");
            }
            return true;
        }

        // ------------------------------------------------------------------------ host side

        public static void OnAsk(Unit player, CpAsk ask)
        {
            if (!Plugin.Settings.tricks || !Game.InClass || player == null || !player.isActived || !player.isPlayer || !player.IsStudent)
            {
                return;
            }
            switch (ask.kind)
            {
                case CpKind.TrickHold:
                    if (ask.i == 0 || (ask.i >= Airplane && ask.i <= Cushion && !player.IsStun && !player.BodyActionsBlocked))
                    {
                        HostSetHand(player.UID, ask.i);
                    }
                    break;
                case CpKind.TrickUse:
                    HostUse(player, ask);
                    break;
                case CpKind.LaserAsk:
                    HostShine(player, ask);
                    break;
                case CpKind.NoteAsk:
                    HostPassNote(player);
                    break;
                case CpKind.NapAsk:
                    if (ask.i != 0 && player.IsSitting && !player.IsStun && !player.BodyActionsBlocked)
                    {
                        HostNaps[player.UID] = new HostNap { Since = Time.time, NextAt = Time.time + 2f };
                    }
                    else
                    {
                        HostWake(player.UID);
                    }
                    break;
            }
        }

        private static void HostSetHand(ulong uid, int item)
        {
            if (item == 0)
            {
                if (!HostHands.Remove(uid))
                {
                    return;
                }
            }
            else
            {
                HostHands[uid] = item;
            }
            Net.SendToModded(new CpTell { kind = CpKind.TrickHeld, a = uid, i = item });
        }

        private static bool HostReady(ulong uid, int item, bool start)
        {
            float[] ready;
            if (!HostReadyAt.TryGetValue(uid, out ready))
            {
                ready = new float[Kinds];
                HostReadyAt[uid] = ready;
            }
            // A second of slack, so a player whose clock runs a touch fast isn't refused.
            if (Time.time < ready[item] - 1f)
            {
                return false;
            }
            if (start)
            {
                ready[item] = Time.time + Waits[item];
            }
            return true;
        }

        private static void HostWake(ulong uid)
        {
            if (HostNaps.Remove(uid))
            {
                Net.SendToModded(new CpTell { kind = CpKind.Snore, a = uid, f = -1f });
            }
        }

        private static List<Unit> Teachers(UnitManager units)
        {
            var teachers = new List<Unit>();
            foreach (Unit player in units.playerList)
            {
                if (player != null && player.isActived && player.isPlayer && player.IsTeacher && !player.IsDead)
                {
                    teachers.Add(player);
                }
            }
            return teachers;
        }

        private static bool Near(Vector3 a, Vector3 b, float flat, float tall)
        {
            Vector3 offset = a - b;
            return Mathf.Abs(offset.y) <= tall && offset.x * offset.x + offset.z * offset.z <= flat * flat;
        }

        /// <summary>Is the teacher looking roughly towards this spot?</summary>
        private static bool Faces(Unit teacher, Vector3 spot)
        {
            Transform body = teacher.transform;
            Vector3 toSpot = Flat(spot - teacher.Position);
            return body != null && toSpot.sqrMagnitude > 0.01f && Vector3.Dot(Flat(body.forward).normalized, toSpot.normalized) > 0.2f;
        }

        private static void HostUse(Unit player, CpAsk ask)
        {
            UnitManager units = Game.Peek<UnitManager>();
            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            int item;
            if (units == null || classroom == null || !HostHands.TryGetValue(player.UID, out item) || item != ask.i
                || item < Airplane || item > Cushion || item == Laser || player.IsStun || player.BodyActionsBlocked
                || !HostReady(player.UID, item, false))
            {
                return;
            }
            ulong uid = player.UID;
            Vector3 hand = player.Position + Vector3.up * 1.3f;
            Vector3 to = ask.w;
            if ((to - hand).magnitude > Reach[item] + 4f)
            {
                to = hand + (to - hand).normalized * Reach[item];
            }
            Unit target = ask.t0 != 0uL ? units.GetUnitByUID(ask.t0) : null;
            if (target != null && (!target.isActived || target.IsDead || (target.Position - player.Position).magnitude > Reach[item] + 4f))
            {
                target = null;
            }
            bool teacherHit = target != null && target.IsTeacher;
            string who = Poles.NameOf(player);

            switch (item)
            {
                case Airplane:
                {
                    int result = target != null ? 3 : 0;
                    if (teacherHit && !Faces(target, player.Position))
                    {
                        result = 1;
                        classroom.AddTrickCount(uid, 12f);
                        Net.Toast(player, "Right in the back of the teacher's head!", 3f);
                        Net.Toast(target, "A paper airplane hit the back of your head.", 3.5f);
                    }
                    else if (teacherHit)
                    {
                        result = 2;
                        Net.Toast(player, "The teacher saw that one coming.", 3f);
                        Net.Toast(target, who + " threw a paper airplane at your face.", 4f);
                    }
                    Net.SendToModded(new CpTell { kind = CpKind.Plane, a = uid, i = result, v = hand, w = to });
                    Plugin.Log("tricks: " + who + " threw a paper airplane, result " + result);
                    break;
                }
                case Balloon:
                {
                    // The thrower's game says which board the balloon ran into. Failing that,
                    // any board close to where it burst counts.
                    Unit board = ask.t1 != 0uL ? units.GetUnitByUID(ask.t1) : null;
                    if (board != null && (!board.isActived || board.GetComponent<BlackBoardComponent>() == null
                        || (board.Position - player.Position).magnitude > Reach[Balloon] + 8f))
                    {
                        board = null;
                    }
                    float best = 3.6f;
                    foreach (Unit unit in units.unitsDict.Values)
                    {
                        if (board != null)
                        {
                            break;
                        }
                        if (unit == null || !unit.isActived || unit.GetComponent<BlackBoardComponent>() == null)
                        {
                            continue;
                        }
                        float distance = Flat(unit.Position - to).magnitude;
                        if (distance < best && Mathf.Abs(unit.Position.y - to.y) < 3.5f)
                        {
                            best = distance;
                            board = unit;
                        }
                    }
                    int result = 0;
                    if (board != null)
                    {
                        result = 1;
                        classroom.AddTrickCount(uid, 10f);
                        Net.Toast(player, "The blackboard is soaked for " + Mathf.RoundToInt(SoakSeconds) + " seconds.", 3f);
                        foreach (Unit teacher in Teachers(units))
                        {
                            Net.Toast(teacher, "A water balloon soaked the blackboard. No writing for " + Mathf.RoundToInt(SoakSeconds) + " seconds.", 4f);
                        }
                    }
                    else if (teacherHit)
                    {
                        result = 2;
                        classroom.AddTrickCount(uid, 4f);
                        Net.Toast(player, "Splash! You soaked the teacher.", 3f);
                        Net.Toast(target, "Splash! A water balloon got you.", 3f);
                    }
                    Net.SendToModded(new CpTell
                    {
                        kind = CpKind.Balloon, a = board != null ? board.UID : 0uL, i = result, f = SoakSeconds, v = hand, w = to
                    });
                    Plugin.Log("tricks: " + who + " threw a water balloon, result " + result);
                    break;
                }
                case Glue:
                {
                    int used;
                    HostGlueUsed.TryGetValue(uid, out used);
                    if (used >= GlueUses)
                    {
                        Net.Toast(player, "The tube is empty.", 2.5f);
                        HostSetHand(uid, 0);
                        return;
                    }
                    HostGlueUsed[uid] = used + 1;
                    if (teacherHit)
                    {
                        Net.SendToModded(new CpTell { kind = CpKind.GlueHand, a = target.UID, f = GluedHandSeconds, v = target.Position });
                        classroom.AddTrickCount(uid, 8f);
                        Net.Toast(player, "The teacher's chalk is glued to their hand for " + Mathf.RoundToInt(GluedHandSeconds) + " seconds.", 3.5f);
                        Net.Toast(target, "Super glue! The chalk is stuck to your hand for " + Mathf.RoundToInt(GluedHandSeconds) + " seconds.", 4f);
                    }
                    else
                    {
                        var puddle = new Spot { Id = _nextId++, At = to, Owner = uid, ArmedAt = Time.time + 0.5f, Until = Time.time + 45f };
                        HostPuddles.Add(puddle);
                        Net.SendToModded(new CpTell { kind = CpKind.GluePuddle, i = puddle.Id, j = 1, v = to });
                        Net.Toast(player, "Glue on the floor. Now get the teacher to walk through it.", 3.5f);
                    }
                    Plugin.Log("tricks: " + who + " used super glue on " + (teacherHit ? "the teacher" : "the floor"));
                    break;
                }
                case Cushion:
                {
                    for (int i = HostCushions.Count - 1; i >= 0; i--)
                    {
                        if (HostCushions[i].Owner == uid)
                        {
                            Net.SendToModded(new CpTell { kind = CpKind.Cushion, i = HostCushions[i].Id, j = 0 });
                            HostCushions.RemoveAt(i);
                        }
                    }
                    var cushion = new Spot { Id = _nextId++, At = to, Owner = uid, ArmedAt = Time.time + 2f, Until = Time.time + 240f };
                    HostCushions.Add(cushion);
                    Net.SendToModded(new CpTell { kind = CpKind.Cushion, i = cushion.Id, j = 1, v = to });
                    Net.Toast(player, "Whoopee cushion set. Now wait for somebody to sit or step on it.", 3.5f);
                    Plugin.Log("tricks: " + who + " put down a whoopee cushion at " + to.ToString("F2"));
                    break;
                }
            }
            HostReady(uid, item, true);
            HostSetHand(uid, 0);
        }

        private static void HostShine(Unit player, CpAsk ask)
        {
            UnitManager units = Game.Peek<UnitManager>();
            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            int item;
            if (units == null || classroom == null || !HostHands.TryGetValue(player.UID, out item) || item != Laser
                || player.IsStun || player.BodyActionsBlocked)
            {
                return;
            }
            HostLaser laser;
            if (!HostLasers.TryGetValue(player.UID, out laser))
            {
                laser = new HostLaser { LastAt = Time.time };
                HostLasers[player.UID] = laser;
            }
            float pause = Time.time - laser.LastAt;
            if (pause > 0.5f)
            {
                laser.Battery = Mathf.Min(LaserBattery, laser.Battery + (pause - 0.5f) * 0.6f);
            }
            laser.LastAt = Time.time;
            if (laser.Battery < LaserStep)
            {
                return;
            }
            laser.Battery -= LaserStep;

            Vector3 hand = player.Position + Vector3.up * 1.3f;
            Vector3 to = ask.w;
            if ((to - hand).magnitude > Reach[Laser] + 4f)
            {
                to = hand + (to - hand).normalized * Reach[Laser];
            }
            Unit target = ask.t0 != 0uL ? units.GetUnitByUID(ask.t0) : null;
            bool blinded = target != null && target.isActived && target.IsTeacher && !target.IsDead
                && (target.Position - player.Position).magnitude < Reach[Laser] + 4f && Faces(target, player.Position);
            if (blinded)
            {
                laser.Blinding += LaserStep;
                if (laser.Blinding >= 1f)
                {
                    laser.Blinding -= 1f;
                    classroom.AddTrickCount(player.UID, 1f);
                }
            }
            Net.SendToModded(new CpTell { kind = CpKind.Laser, a = blinded ? target.UID : 0uL, v = hand, w = to });
        }

        private static void HostPassNote(Unit player)
        {
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null || player.IsStun || player.BodyActionsBlocked || !HostReady(player.UID, Note, false))
            {
                return;
            }
            // The note goes to the classmate sitting furthest away on this floor.
            Unit target = null;
            float furthest = 2f;
            foreach (Unit unit in units.unitsDict.Values)
            {
                if (unit == null || unit == player || !unit.isActived || unit.IsDead || !unit.IsStudent
                    || Mathf.Abs(unit.Position.y - player.Position.y) > 2.5f)
                {
                    continue;
                }
                float distance = Flat(unit.Position - player.Position).magnitude;
                if (distance > furthest && distance < 40f)
                {
                    furthest = distance;
                    target = unit;
                }
            }
            if (target == null)
            {
                Net.Toast(player, "There is nobody to pass a note to.", 3f);
                return;
            }
            HostReady(player.UID, Note, true);
            var note = new HostNote
            {
                Id = _nextId++,
                From = player.Position + Vector3.up * 0.95f,
                To = target.Position + Vector3.up * 0.95f,
                Start = Time.time,
                Duration = Mathf.Clamp(furthest / 1.5f, 3f, 16f),
                Owner = player.UID,
                Points = Mathf.Clamp(Mathf.Round(furthest / 1.2f) * 2f, 2f, 16f)
            };
            HostNotes.Add(note);
            Net.SendToModded(new CpTell { kind = CpKind.Note, i = note.Id, j = 1, v = note.From, w = note.To, f = note.Duration });
            Net.Toast(player, "Your note is on its way to " + (target.isPlayer ? Poles.NameOf(target) : "a classmate") + ". Keep the teacher away from it.", 3.5f);
            Plugin.Log("tricks: " + Poles.NameOf(player) + " passed a note " + furthest.ToString("F1") + " m");
        }

        /// <summary>Host: the janitor's mop clears glue and whoopee cushions. Returns how many.</summary>
        public static int HostCleanNear(Vector3 at, float reach)
        {
            int cleaned = 0;
            for (int i = HostPuddles.Count - 1; i >= 0; i--)
            {
                if (Near(HostPuddles[i].At, at, reach, 1.5f))
                {
                    Net.SendToModded(new CpTell { kind = CpKind.GluePuddle, i = HostPuddles[i].Id, j = 0 });
                    HostPuddles.RemoveAt(i);
                    cleaned++;
                }
            }
            for (int i = HostCushions.Count - 1; i >= 0; i--)
            {
                if (Near(HostCushions[i].At, at, reach, 1.5f))
                {
                    Net.SendToModded(new CpTell { kind = CpKind.Cushion, i = HostCushions[i].Id, j = 0 });
                    HostCushions.RemoveAt(i);
                    cleaned++;
                }
            }
            return cleaned;
        }

        private static void HostTick()
        {
            if (Time.time < _nextScanAt)
            {
                return;
            }
            _nextScanAt = Time.time + 0.15f;
            UnitManager units = Game.Peek<UnitManager>();
            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            if (units == null || classroom == null || !Plugin.Settings.tricks)
            {
                return;
            }
            float now = Time.time;

            if (HostPuddles.Count > 0 || HostNotes.Count > 0)
            {
                List<Unit> teachers = Teachers(units);
                for (int i = HostPuddles.Count - 1; i >= 0; i--)
                {
                    Spot puddle = HostPuddles[i];
                    Unit stuck = null;
                    if (now >= puddle.ArmedAt)
                    {
                        foreach (Unit teacher in teachers)
                        {
                            if (Near(teacher.Position, puddle.At, 0.65f, 1.2f))
                            {
                                stuck = teacher;
                                break;
                            }
                        }
                    }
                    if (stuck == null && now < puddle.Until)
                    {
                        continue;
                    }
                    HostPuddles.RemoveAt(i);
                    Net.SendToModded(new CpTell { kind = CpKind.GluePuddle, i = puddle.Id, j = 0 });
                    if (stuck != null)
                    {
                        Net.SendToModded(new CpTell { kind = CpKind.GlueFeet, a = stuck.UID, f = GluedFeetSeconds, v = puddle.At });
                        classroom.AddTrickCount(puddle.Owner, 10f);
                        Net.Toast(stuck, "Super glue! Your shoes are stuck to the floor.", 3.5f);
                        Unit owner = units.GetUnitByUID(puddle.Owner);
                        if (owner != null)
                        {
                            Net.Toast(owner, "The teacher is stuck in your glue!", 3f);
                        }
                        Plugin.Log("tricks: the teacher stepped in super glue");
                    }
                }

                for (int i = HostNotes.Count - 1; i >= 0; i--)
                {
                    HostNote note = HostNotes[i];
                    float t = (now - note.Start) / note.Duration;
                    Vector3 at = Vector3.Lerp(note.From, note.To, Mathf.Clamp01(t));
                    Unit owner = units.GetUnitByUID(note.Owner);
                    Unit grabber = null;
                    foreach (Unit teacher in teachers)
                    {
                        if (Near(teacher.Position, at, 1.4f, 1.8f))
                        {
                            grabber = teacher;
                            break;
                        }
                    }
                    if (grabber != null)
                    {
                        HostNotes.RemoveAt(i);
                        Net.SendToModded(new CpTell { kind = CpKind.Note, i = note.Id, j = 3, v = at });
                        Net.Toast(grabber, "You grabbed a note. " + Poles.NameOf(owner) + " wrote it!", 5f);
                        if (owner != null)
                        {
                            Net.Toast(owner, "The teacher grabbed your note and knows it was you.", 4f);
                        }
                        Plugin.Log("tricks: the teacher grabbed a note");
                    }
                    else if (t >= 1f)
                    {
                        HostNotes.RemoveAt(i);
                        Net.SendToModded(new CpTell { kind = CpKind.Note, i = note.Id, j = 2, v = at });
                        classroom.AddTrickCount(note.Owner, note.Points);
                        if (owner != null)
                        {
                            Net.Toast(owner, "Your note got through.", 3f);
                        }
                    }
                }
            }

            if (HostCushions.Count > 0)
            {
                for (int i = HostCushions.Count - 1; i >= 0; i--)
                {
                    Spot cushion = HostCushions[i];
                    Unit victim = null;
                    if (now >= cushion.ArmedAt)
                    {
                        foreach (Unit unit in units.unitsDict.Values)
                        {
                            if (unit != null && unit.UID != cushion.Owner && unit.isActived && !unit.IsDead
                                && (unit.IsTeacher || unit.IsStudent) && Near(unit.Position, cushion.At, 0.5f, 1.3f))
                            {
                                victim = unit;
                                break;
                            }
                        }
                    }
                    if (victim == null && now < cushion.Until)
                    {
                        continue;
                    }
                    HostCushions.RemoveAt(i);
                    if (victim == null)
                    {
                        Net.SendToModded(new CpTell { kind = CpKind.Cushion, i = cushion.Id, j = 0 });
                        continue;
                    }
                    Net.SendToModded(new CpTell { kind = CpKind.Cushion, i = cushion.Id, j = 2, v = cushion.At });
                    classroom.AddTrickCount(cushion.Owner, victim.IsTeacher ? 15f : 8f);
                    string sitter = victim.IsTeacher ? "The teacher" : victim.isPlayer ? Poles.NameOf(victim) : "A classmate";
                    Unit owner = units.GetUnitByUID(cushion.Owner);
                    if (owner != null)
                    {
                        Net.Toast(owner, sitter + " found your whoopee cushion!", 3.5f);
                    }
                    if (victim.isPlayer)
                    {
                        Net.Toast(victim, "PFFFRRRT. That was a whoopee cushion.", 3.5f);
                    }
                    Plugin.Log("tricks: " + sitter + " set off a whoopee cushion");
                }
            }

            if (HostNaps.Count > 0)
            {
                var woken = new List<ulong>();
                foreach (KeyValuePair<ulong, HostNap> pair in HostNaps)
                {
                    Unit sleeper = units.GetUnitByUID(pair.Key);
                    if (sleeper == null || !sleeper.isActived || !sleeper.IsSitting || sleeper.IsStun || sleeper.BodyActionsBlocked)
                    {
                        woken.Add(pair.Key);
                        continue;
                    }
                    HostNap nap = pair.Value;
                    if (now < nap.NextAt)
                    {
                        continue;
                    }
                    nap.NextAt = now + 2f;
                    // The longer the nap, the more it pays and the louder the snoring.
                    float asleep = now - nap.Since;
                    int points = asleep < 10f ? 1 : asleep < 20f ? 2 : 3;
                    classroom.AddTrickCount(pair.Key, points);
                    Net.SendToModded(new CpTell { kind = CpKind.Snore, a = pair.Key, i = points, f = Mathf.Clamp01(asleep / 30f), v = sleeper.Position });
                }
                foreach (ulong uid in woken)
                {
                    HostWake(uid);
                }
            }
        }

        // --------------------------------------------------------------------- every player

        public static void OnTell(CpTell tell)
        {
            UnitManager units = Game.Peek<UnitManager>();
            Unit me = Game.LocalPlayer;
            bool mine = me != null && me.UID == tell.a;
            switch (tell.kind)
            {
                case CpKind.TrickHeld:
                {
                    Hand old;
                    if (Hands.TryGetValue(tell.a, out old))
                    {
                        Remove(old.Model);
                        Hands.Remove(tell.a);
                    }
                    if (tell.i >= Airplane && tell.i <= Cushion)
                    {
                        Hands[tell.a] = new Hand { Item = tell.i };
                    }
                    break;
                }
                case CpKind.Plane:
                case CpKind.Balloon:
                {
                    bool plane = tell.kind == CpKind.Plane;
                    float distance = Mathf.Max(0.5f, (tell.w - tell.v).magnitude);
                    Flights.Add(new Flight
                    {
                        Item = plane ? Airplane : Balloon,
                        Result = tell.i,
                        Model = BuildItem(plane ? Airplane : Balloon),
                        From = tell.v,
                        To = tell.w,
                        Start = Time.time,
                        Duration = distance / (plane ? 11f : 13f),
                        Arc = plane ? 0.25f : Mathf.Min(1.2f, distance * 0.12f),
                        BoardUID = plane ? 0uL : tell.a,
                        Soak = tell.f
                    });
                    Game.PlaySoundAt(AudioEvents.sfx_ChalkThrow_sfx, tell.v);
                    break;
                }
                case CpKind.GlueHand:
                    Game.PlaySoundAt(AudioEvents.sfx_InkBottleHit_sfx, tell.v);
                    if (mine)
                    {
                        _handGluedUntil = Time.time + Mathf.Clamp(tell.f, 1f, 30f);
                    }
                    break;
                case CpKind.GluePuddle:
                {
                    GameObject old;
                    if (Puddles.TryGetValue(tell.i, out old))
                    {
                        Remove(old);
                        Puddles.Remove(tell.i);
                    }
                    if (tell.j == 1)
                    {
                        GameObject puddle = Build("puddle", new Color32(240, 236, 200, 255), 0.3f);
                        puddle.transform.position = tell.v + Vector3.up * 0.01f;
                        puddle.transform.rotation = Quaternion.Euler(0f, (tell.i * 67) % 360, 0f);
                        Puddles[tell.i] = puddle;
                        Game.PlaySoundAt(AudioEvents.sfx_InkBottleHit_sfx, tell.v);
                    }
                    break;
                }
                case CpKind.GlueFeet:
                {
                    Unit teacher = units != null ? units.GetUnitByUID(tell.a) : null;
                    TeacherStunComponent stun = teacher != null ? teacher.GetComponent<TeacherStunComponent>() : null;
                    if (stun != null)
                    {
                        stun.TryGetProjectileStun(Mathf.Clamp(tell.f, 1f, 10f));
                    }
                    Game.PlaySoundAt(AudioEvents.sfx_InkBottleHit_sfx, tell.v);
                    break;
                }
                case CpKind.Laser:
                    Shapes.Tracer(tell.v + Vector3.down * 0.15f, tell.w, Red, 0.014f, 0.17f);
                    if (mine && me.IsTeacher)
                    {
                        _whiteUntil = Time.time + 0.45f;
                        if (Time.time - _laserNoticeAt > 8f)
                        {
                            _laserNoticeAt = Time.time;
                            Game.Notify("A laser pointer in your eyes! Follow the red line to whoever holds it.", 4f);
                        }
                    }
                    break;
                case CpKind.Cushion:
                {
                    GameObject old;
                    if (Cushions.TryGetValue(tell.i, out old))
                    {
                        Remove(old);
                        Cushions.Remove(tell.i);
                    }
                    if (tell.j == 1)
                    {
                        GameObject cushion = BuildItem(Cushion);
                        cushion.transform.position = tell.v + Vector3.up * 0.005f;
                        cushion.transform.rotation = Quaternion.Euler(0f, (tell.i * 53) % 360, 0f);
                        Cushions[tell.i] = cushion;
                    }
                    else if (tell.j == 2)
                    {
                        Game.PlaySoundAt(AudioEvents.sfx_balloonLeak_sfx, tell.v);
                    }
                    break;
                }
                case CpKind.Note:
                {
                    NoteView old;
                    if (Notes.TryGetValue(tell.i, out old))
                    {
                        Remove(old.Model);
                        Notes.Remove(tell.i);
                    }
                    if (tell.j == 1)
                    {
                        Notes[tell.i] = new NoteView
                        {
                            Model = BuildItem(Note), From = tell.v, To = tell.w, Start = Time.time, Duration = Mathf.Max(0.5f, tell.f)
                        };
                        Game.PlaySoundAt(AudioEvents.sfx_FoldPaper_sfx, tell.v);
                    }
                    else
                    {
                        Game.PlaySoundAt(tell.j == 3 ? AudioEvents.sfx_UnFoldPaper_sfx : AudioEvents.sfx_FoldPaper_sfx, tell.v);
                    }
                    break;
                }
                case CpKind.Snore:
                    if (tell.f < 0f)
                    {
                        Sleepers.Remove(tell.a);
                        if (mine)
                        {
                            _napping = false;
                        }
                        break;
                    }
                    Sleepers[tell.a] = new Sleeper { Loud = Mathf.Clamp01(tell.f), Until = Time.time + 3f, At = tell.v };
                    if (mine)
                    {
                        _napEarned += tell.i;
                    }
                    // A quiet snore is only heard nearby. A loud one carries across the room.
                    if (tell.f >= 0.66f)
                    {
                        Game.PlaySound(AudioEvents.sfx_balloonBlow_sfx);
                    }
                    else
                    {
                        Game.PlaySoundAt(AudioEvents.sfx_balloonBlow_sfx, tell.v);
                    }
                    break;
            }
        }

        // --------------------------------------------------------------------------- screen

        /// <summary>Screen effects and the hint line for the held trick. Drawn in a 1280 by 720 frame.</summary>
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
                _zzz = new GUIStyle { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _zzz.normal.textColor = new Color32(255, 255, 255, 255);
                _napText = new GUIStyle { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _napText.normal.textColor = new Color32(255, 255, 255, 255);
            }
            Color old = GUI.color;
            Unit me = Game.LocalPlayer;

            // "Z z z" over every sleeper, bigger the louder they snore.
            Camera camera = Camera.main;
            if (camera != null && Sleepers.Count > 0 && width > 0f)
            {
                float scale = Screen.width / width;
                foreach (KeyValuePair<ulong, Sleeper> pair in Sleepers)
                {
                    if (me != null && pair.Key == me.UID)
                    {
                        continue;
                    }
                    Vector3 point = camera.WorldToScreenPoint(pair.Value.At + Vector3.up * 1.75f);
                    if (point.z <= 0.2f)
                    {
                        continue;
                    }
                    _zzz.fontSize = Mathf.RoundToInt(16f + pair.Value.Loud * 24f);
                    var spot = new Rect(point.x / scale - 90f, (Screen.height - point.y) / scale - 30f, 180f, 60f);
                    GUI.color = new Color(0f, 0f, 0f, 0.7f);
                    GUI.Label(new Rect(spot.x + 2f, spot.y + 2f, spot.width, spot.height), "Z z z", _zzz);
                    GUI.color = old;
                    GUI.Label(spot, "Z z z", _zzz);
                }
            }

            if (Time.time < _whiteUntil)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.94f);
                GUI.DrawTexture(new Rect(0f, 0f, width, height), _white);
                GUI.color = old;
            }

            if (_napping)
            {
                // Eyelids closing from the top and the bottom.
                float shut = Mathf.Clamp01((Time.time - _napStartedAt) / 1.2f);
                float lid = height * 0.37f * shut;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0f, 0f, width, lid), _white);
                GUI.DrawTexture(new Rect(0f, height - lid, width, lid), _white);
                GUI.color = old;
                GUI.Label(new Rect(0f, height * 0.5f - 20f, width, 40f),
                    "Z z z ...   +" + Mathf.RoundToInt(_napEarned) + " mischief so far.   Press any key to wake up", _napText);
                return null;
            }

            int item = HeldLocal;
            if (item == 0)
            {
                return null;
            }
            string line = Names[item] + "     [" + Plugin.Settings.FireKeyCode + " or wheel click] " + UseWords[item]
                + "     [" + Plugin.Settings.UseKeyCode + "] put away";
            if (item == Laser)
            {
                line += "     battery " + Mathf.RoundToInt(_battery / LaserBattery * 100f) + "%";
            }
            else if (item == Glue)
            {
                line += "     " + _glueLeft + " left";
            }
            return line;
        }
    }
}
