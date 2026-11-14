using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Ideas 6, 19, 20, 3 and 22: the new teachers. A player picks a teacher type in the
    /// wardrobe; it only counts while they are the teacher. The look is put on by Looks.cs,
    /// this file is what each type can do.
    ///
    ///   Gym teacher  throws a pizza slice: it lands on a student's face and knocks them over.
    ///   Lunch lady   throws mashed potatoes that stick a student in place, can't write on the
    ///                blackboard and gets stamina back by stirring the big pot instead.
    ///   Janitor      mops: clears banana peels, glue and whoopee cushions around him and
    ///                leaves a wet floor that students slip on.
    ///   Bald + wig   a student sneaking up from behind (or holding the plunger) can yank the
    ///                wig off. No chalk or eraser until the teacher walks over and gets it back.
    /// </summary>
    internal static class Teachers
    {
        private const float ThrowWait = 8f;
        private const float MopWait = 6f;
        private const float ThrowReach = 24f;
        private const float EffectSeconds = 4f;
        private const float WetSeconds = 20f;
        private const float StirReach = 2.4f;

        private sealed class Shot
        {
            public GameObject Model;
            public Vector3 From;
            public Vector3 To;
            public float Start;
            public float Duration;
        }

        private sealed class WetSpot
        {
            public int Id;
            public Vector3 At;
            public float Until;
            public readonly Dictionary<ulong, float> Slipped = new Dictionary<ulong, float>();
        }

        // What every game knows
        private static readonly List<Shot> Shots = new List<Shot>();
        private static readonly Dictionary<int, GameObject> Wet = new Dictionary<int, GameObject>();
        private static readonly Dictionary<ulong, GameObject> WigsOnFloor = new Dictionary<ulong, GameObject>();
        private static bool _potThere;
        private static Vector3 _pot;
        private static GameObject _potModel;
        private static bool _dirty;

        // This player's own side
        private static float _readyAt;
        private static float _pizzaUntil;
        private static float _nextStirAt;
        private static float _nextStirSoundAt;
        private static Texture2D _pizza;

        // What only the host keeps
        private static readonly Dictionary<ulong, float> HostReadyAt = new Dictionary<ulong, float>();
        private static readonly List<WetSpot> HostWet = new List<WetSpot>();
        private static readonly Dictionary<ulong, Vector3> HostWigs = new Dictionary<ulong, Vector3>();
        private static bool _hostPot;
        private static int _nextId = 1;
        private static float _nextScanAt;

        public static bool WigIsOff(ulong teacherUID)
        {
            return WigsOnFloor.ContainsKey(teacherUID);
        }

        private static int KindOf(Unit unit)
        {
            return unit != null && unit.IsTeacher && Plugin.Settings.teachers ? Looks.KindOf(unit.UID) : 0;
        }

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
            foreach (Shot shot in Shots)
            {
                Remove(shot.Model);
            }
            Shots.Clear();
            foreach (GameObject model in Wet.Values)
            {
                Remove(model);
            }
            Wet.Clear();
            foreach (GameObject model in WigsOnFloor.Values)
            {
                Remove(model);
            }
            WigsOnFloor.Clear();
            Remove(_potModel);
            _potModel = null;
            _potThere = false;
            _readyAt = 0f;
            _pizzaUntil = 0f;
            HostReadyAt.Clear();
            HostWet.Clear();
            HostWigs.Clear();
            _hostPot = false;
        }

        private static void Remove(GameObject model)
        {
            if (model != null)
            {
                UnityEngine.Object.Destroy(model);
            }
        }

        private static void Animate()
        {
            float now = Time.time;
            for (int i = Shots.Count - 1; i >= 0; i--)
            {
                Shot shot = Shots[i];
                float t = (now - shot.Start) / shot.Duration;
                if (t >= 1f)
                {
                    Remove(shot.Model);
                    Shots.RemoveAt(i);
                    Game.PlaySoundAt(AudioEvents.sfx_InkBottleHit_sfx, shot.To);
                    continue;
                }
                if (shot.Model != null)
                {
                    float rise = Mathf.Min(0.8f, (shot.To - shot.From).magnitude * 0.08f);
                    shot.Model.transform.position = Vector3.Lerp(shot.From, shot.To, t) + Vector3.up * (rise * 4f * t * (1f - t));
                    shot.Model.transform.rotation = Quaternion.Euler(now * 500f, now * 300f, 0f);
                }
            }
        }

        // --------------------------------------------------------------- this player's keys

        private static void HandleKeys()
        {
            Unit me = Game.LocalPlayer;
            if (me == null || !me.isActived || !Net.CanSendToHost || CursorGrab.Busy || Game.IsTyping || me.IsStun || me.BodyActionsBlocked)
            {
                return;
            }
            if (me.IsStudent)
            {
                if (Input.GetKeyDown(Plugin.Settings.UseKeyCode))
                {
                    Unit teacher = WigToGrab(me);
                    if (teacher != null)
                    {
                        Net.SendToHost(new CpAsk { kind = CpKind.WigPull, t0 = teacher.UID });
                    }
                }
                return;
            }
            int kind = KindOf(me);
            if (kind == 0)
            {
                return;
            }
            bool fire = Input.GetKeyDown(Plugin.Settings.FireKeyCode) || Input.GetMouseButtonDown(2);
            if (fire && !NerfGuns.HoldingLocal && kind >= Looks.Gym && kind <= Looks.Janitor)
            {
                if (Time.time < _readyAt)
                {
                    Tricks.Tip("Ready again in " + Mathf.CeilToInt(_readyAt - Time.time) + " seconds.");
                }
                else if (kind == Looks.Janitor)
                {
                    _readyAt = Time.time + MopWait;
                    Net.SendToHost(new CpAsk { kind = CpKind.Mop });
                }
                else
                {
                    Camera camera = Camera.main;
                    UnitManager units = Game.Peek<UnitManager>();
                    if (camera != null && units != null)
                    {
                        Vector3 from = camera.transform.position;
                        Vector3 end;
                        Unit target = NerfGuns.Trace(units, me, from, camera.transform.forward, ThrowReach, out end);
                        _readyAt = Time.time + ThrowWait;
                        Net.SendToHost(new CpAsk { kind = CpKind.TeacherThrow, i = kind, v = from, w = end, t0 = target != null ? target.UID : 0uL });
                    }
                }
            }

            // The lunch lady keeps the stir key held next to her pot.
            if (kind == Looks.Lunch && _potThere && Input.GetKey(Plugin.Settings.UseKeyCode)
                && Tricks.Flat(me.Position - _pot).magnitude < StirReach && Time.time >= _nextStirAt)
            {
                _nextStirAt = Time.time + 0.25f;
                SkillComponent skill = me.GetComponent<SkillComponent>();
                if (skill != null)
                {
                    skill.AddSkillEnergy(1f);
                }
                if (Time.time >= _nextStirSoundAt)
                {
                    _nextStirSoundAt = Time.time + 1.4f;
                    Game.PlaySoundAt(AudioEvents.sfx_hotPot_sfx, _pot);
                }
            }
        }

        /// <summary>The wig teacher this student could yank the wig off right now, or null.</summary>
        private static Unit WigToGrab(Unit me)
        {
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null || !Plugin.Settings.teachers)
            {
                return null;
            }
            StudentItemUseComponent items = me.GetComponent<StudentItemUseComponent>();
            bool plunger = items != null && items.currentHoldUnit != null && items.currentHoldUnit.GetComponent<PlungerComponent>() != null;
            foreach (Unit teacher in units.playerList)
            {
                if (teacher == null || !teacher.isActived || !teacher.IsTeacher || Looks.KindOf(teacher.UID) != Looks.Wig
                    || WigIsOff(teacher.UID) || teacher.transform == null)
                {
                    continue;
                }
                Vector3 offset = me.Position - teacher.Position;
                if (Mathf.Abs(offset.y) > 1.5f)
                {
                    continue;
                }
                float distance = Tricks.Flat(offset).magnitude;
                bool behind = Vector3.Dot(Tricks.Flat(teacher.transform.forward).normalized, Tricks.Flat(offset).normalized) < -0.1f;
                // The plunger reaches further and works from any side.
                if ((plunger && distance < 3.6f) || (behind && distance < 2.2f))
                {
                    return teacher;
                }
            }
            return null;
        }

        // ------------------------------------------------------------------ patches ask

        /// <summary>True while this teacher has lost the wig: no chalk, no eraser.</summary>
        public static bool NoThrowing(Unit teacher, bool say)
        {
            if (teacher == null || !teacher.isLocalPlayer || !teacher.IsTeacher || !Game.InClass || !WigIsOff(teacher.UID))
            {
                return false;
            }
            if (say)
            {
                Tricks.Tip("Not without your wig! Walk over to it to put it back on.");
            }
            return true;
        }

        /// <summary>True when the local teacher is the lunch lady, who never writes on the board.</summary>
        public static bool NoBoard()
        {
            Unit me = Game.LocalPlayer;
            if (!Game.InClass || KindOf(me) != Looks.Lunch)
            {
                return false;
            }
            Tricks.Tip("The lunch lady doesn't write. Stir the big pot for stamina.");
            return true;
        }

        // ------------------------------------------------------------------------ host side

        private static bool HostReady(ulong uid, float wait)
        {
            float readyAt;
            if (HostReadyAt.TryGetValue(uid, out readyAt) && Time.time < readyAt - 1f)
            {
                return false;
            }
            HostReadyAt[uid] = Time.time + wait;
            return true;
        }

        public static void OnAsk(Unit player, CpAsk ask)
        {
            UnitManager units = Game.Peek<UnitManager>();
            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            if (!Plugin.Settings.teachers || !Game.InClass || units == null || classroom == null || player == null
                || !player.isActived || !player.isPlayer || player.IsStun || player.BodyActionsBlocked)
            {
                return;
            }
            switch (ask.kind)
            {
                case CpKind.TeacherThrow:
                {
                    int kind = KindOf(player);
                    if ((kind != Looks.Gym && kind != Looks.Lunch) || kind != ask.i || !HostReady(player.UID, ThrowWait))
                    {
                        return;
                    }
                    Vector3 hand = player.Position + Vector3.up * 1.4f;
                    Vector3 to = ask.w;
                    if ((to - hand).magnitude > ThrowReach + 4f)
                    {
                        to = hand + (to - hand).normalized * ThrowReach;
                    }
                    Unit target = ask.t0 != 0uL ? units.GetUnitByUID(ask.t0) : null;
                    if (target != null && (!target.isActived || target.IsDead || !target.IsStudent
                        || (target.Position - player.Position).magnitude > ThrowReach + 4f))
                    {
                        target = null;
                    }
                    Net.SendToModded(new CpTell
                    {
                        kind = CpKind.Thrown, i = kind, a = target != null ? target.UID : 0uL, f = EffectSeconds, v = hand, w = to
                    });
                    if (target != null && kind == Looks.Gym)
                    {
                        Poles.Slip(target);
                        if (target.isPlayer)
                        {
                            Net.Toast(target, "A slice of pizza, right in the face!", 3f);
                        }
                    }
                    else if (target != null && target.isPlayer)
                    {
                        Net.Toast(target, "Mashed potatoes! You're stuck for a moment.", 3f);
                    }
                    Plugin.Log("teachers: " + Poles.NameOf(player) + " threw " + (kind == Looks.Gym ? "pizza" : "mashed potatoes")
                        + (target != null ? " and hit" : " and missed"));
                    break;
                }
                case CpKind.Mop:
                {
                    if (KindOf(player) != Looks.Janitor || !HostReady(player.UID, MopWait))
                    {
                        return;
                    }
                    Vector3 at = player.Position;
                    int cleaned = Tricks.HostCleanNear(at, 3f);
                    RoomManager room = Game.Peek<RoomManager>();
                    foreach (Unit unit in new List<Unit>(units.unitsDict.Values))
                    {
                        BananaComponent banana = unit != null && unit.isActived ? unit.GetComponent<BananaComponent>() : null;
                        if (banana != null && banana.State == BananaState.ThrownPeel && room != null
                            && (unit.Position - at).magnitude < 3f)
                        {
                            room.GetUnitMessageHandler().OnUnitDestroyMessageRequest(null, new UnitDestroyMessageRequest_C2S { unitUID = unit.UID });
                            cleaned++;
                        }
                    }
                    var wet = new WetSpot { Id = _nextId++, At = at, Until = Time.time + WetSeconds };
                    HostWet.Add(wet);
                    Net.SendToModded(new CpTell { kind = CpKind.Wet, i = wet.Id, j = 1, v = at });
                    Net.Toast(player, cleaned > 0 ? "Mopped up " + cleaned + " thing" + (cleaned == 1 ? "" : "s") + ". The floor is wet now."
                        : "The floor is wet now.", 3f);
                    Plugin.Log("teachers: the janitor mopped, cleaned " + cleaned);
                    break;
                }
                case CpKind.WigPull:
                {
                    Unit teacher = ask.t0 != 0uL ? units.GetUnitByUID(ask.t0) : null;
                    if (!player.IsStudent || teacher == null || !teacher.isActived || KindOf(teacher) != Looks.Wig
                        || HostWigs.ContainsKey(teacher.UID) || (teacher.Position - player.Position).magnitude > 4.6f)
                    {
                        return;
                    }
                    float angle = UnityEngine.Random.Range(0f, 360f);
                    Vector3 lands = teacher.Position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 2.8f + Vector3.up * 0.4f;
                    HostWigs[teacher.UID] = lands;
                    Net.SendToModded(new CpTell { kind = CpKind.Wig, a = teacher.UID, j = 1, v = lands });
                    classroom.AddTrickCount(player.UID, 20f);
                    Net.Toast(player, "You yanked the teacher's wig off! Big mischief.", 3.5f);
                    Net.Toast(teacher, Poles.NameOf(player) + " pulled your wig off! No chalk until you walk over and get it.", 5f);
                    Plugin.Log("teachers: " + Poles.NameOf(player) + " pulled the wig off");
                    break;
                }
            }
        }

        private static void HostTick()
        {
            if (Time.time < _nextScanAt)
            {
                return;
            }
            _nextScanAt = Time.time + 0.15f;
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null || !Plugin.Settings.teachers)
            {
                return;
            }
            float now = Time.time;

            // The lunch lady's pot is put out the first time a lunch lady is in the class.
            if (!_hostPot)
            {
                foreach (Unit player in units.playerList)
                {
                    Vector3 spot;
                    if (player != null && player.isActived && KindOf(player) == Looks.Lunch && FindPotSpot(units, player, out spot))
                    {
                        _hostPot = true;
                        Net.SendToModded(new CpTell { kind = CpKind.Pot, j = 1, v = spot });
                        Net.Toast(player, "Your pot is out by the blackboard. Hold " + Plugin.Settings.UseKeyCode + " next to it to stir for stamina.", 5f);
                        Plugin.Log("teachers: the pot is at " + spot.ToString("F2"));
                        break;
                    }
                }
            }

            for (int i = HostWet.Count - 1; i >= 0; i--)
            {
                WetSpot wet = HostWet[i];
                if (now >= wet.Until)
                {
                    HostWet.RemoveAt(i);
                    Net.SendToModded(new CpTell { kind = CpKind.Wet, i = wet.Id, j = 0 });
                    continue;
                }
                foreach (Unit unit in new List<Unit>(units.unitsDict.Values))
                {
                    if (unit == null || !unit.isActived || unit.IsDead || !unit.IsStudent || unit.IsSitting)
                    {
                        continue;
                    }
                    Vector3 offset = unit.Position - wet.At;
                    float last;
                    if (Mathf.Abs(offset.y) < 1.2f && offset.x * offset.x + offset.z * offset.z < 1.1f * 1.1f
                        && (!wet.Slipped.TryGetValue(unit.UID, out last) || now - last > 5f))
                    {
                        wet.Slipped[unit.UID] = now;
                        Poles.Slip(unit);
                    }
                }
            }

            if (HostWigs.Count > 0)
            {
                foreach (ulong uid in new List<ulong>(HostWigs.Keys))
                {
                    Unit teacher = units.GetUnitByUID(uid);
                    Vector3 offset = teacher != null ? teacher.Position - HostWigs[uid] : Vector3.zero;
                    if (teacher == null || !teacher.isActived || KindOf(teacher) != Looks.Wig
                        || (Mathf.Abs(offset.y) < 1.6f && offset.x * offset.x + offset.z * offset.z < 1.1f * 1.1f))
                    {
                        HostWigs.Remove(uid);
                        Net.SendToModded(new CpTell { kind = CpKind.Wig, a = uid, j = 0 });
                        if (teacher != null)
                        {
                            Net.Toast(teacher, "Wig back on. You can throw again.", 3f);
                        }
                    }
                }
            }
        }

        /// <summary>A place on the floor a step out from the blackboard, towards the desks.</summary>
        private static bool FindPotSpot(UnitManager units, Unit teacher, out Vector3 spot)
        {
            spot = Vector3.zero;
            Unit board = null;
            float best = float.MaxValue;
            Vector3 desks = Vector3.zero;
            int count = 0;
            foreach (Unit unit in units.unitsDict.Values)
            {
                if (unit == null || !unit.isActived)
                {
                    continue;
                }
                if (unit.IsStudent && Mathf.Abs(unit.Position.y - teacher.Position.y) < 2.5f)
                {
                    desks += unit.Position;
                    count++;
                }
                else if (unit.GetComponent<BlackBoardComponent>() != null)
                {
                    float distance = (unit.Position - teacher.Position).magnitude;
                    if (distance < best)
                    {
                        best = distance;
                        board = unit;
                    }
                }
            }
            if (board == null)
            {
                return false;
            }
            Vector3 towards = count > 0 ? Tricks.Flat(desks / count - board.Position) : Tricks.Flat(teacher.Position - board.Position);
            towards = towards.sqrMagnitude > 0.01f ? towards.normalized : Vector3.forward;
            spot = board.Position + towards * 1.3f;
            spot.y = count > 0 ? desks.y / count + 0.6f : teacher.Position.y + 0.6f;
            return true;
        }

        // --------------------------------------------------------------------- every player

        public static void OnTell(CpTell tell)
        {
            UnitManager units = Game.Peek<UnitManager>();
            Unit me = Game.LocalPlayer;
            switch (tell.kind)
            {
                case CpKind.Thrown:
                {
                    float distance = Mathf.Max(0.5f, (tell.w - tell.v).magnitude);
                    Shots.Add(new Shot
                    {
                        Model = Tricks.Build(tell.i == Looks.Gym ? "shot_pizza" : "shot_potato", new Color32(245, 220, 120, 255), 0.14f),
                        From = tell.v, To = tell.w, Start = Time.time, Duration = distance / 15f
                    });
                    Game.PlaySoundAt(AudioEvents.sfx_ChalkThrow_sfx, tell.v);
                    if (tell.a == 0uL)
                    {
                        break;
                    }
                    if (tell.i == Looks.Gym)
                    {
                        if (me != null && me.UID == tell.a)
                        {
                            _pizzaUntil = Time.time + Mathf.Clamp(tell.f, 1f, 8f);
                        }
                    }
                    else
                    {
                        Unit victim = units != null ? units.GetUnitByUID(tell.a) : null;
                        StudentStunComponent stun = victim != null ? victim.GetComponent<StudentStunComponent>() : null;
                        if (stun != null)
                        {
                            stun.TryGetProjectileStun(Mathf.Clamp(tell.f, 1f, 8f));
                        }
                    }
                    break;
                }
                case CpKind.Pot:
                    Remove(_potModel);
                    _potModel = null;
                    _potThere = tell.j == 1;
                    if (_potThere)
                    {
                        _pot = Tricks.Settle(tell.v);
                        _potModel = Tricks.Build("prop_pot", new Color32(90, 94, 104, 255), 0.6f);
                        _potModel.transform.position = _pot;
                    }
                    break;
                case CpKind.Wet:
                {
                    GameObject old;
                    if (Wet.TryGetValue(tell.i, out old))
                    {
                        Remove(old);
                        Wet.Remove(tell.i);
                    }
                    if (tell.j == 1)
                    {
                        GameObject patch = Tricks.Build("splat", new Color32(60, 140, 235, 255), 0.5f);
                        patch.transform.position = Tricks.Settle(tell.v + Vector3.up * 0.3f) + Vector3.up * 0.012f;
                        patch.transform.rotation = Quaternion.Euler(0f, (tell.i * 71) % 360, 0f);
                        patch.transform.localScale = new Vector3(3.2f, 1f, 3.2f);
                        Wet[tell.i] = patch;
                        Game.PlaySoundAt(AudioEvents.sfx_toiletFlushing_sfx, tell.v);
                    }
                    break;
                }
                case CpKind.Wig:
                {
                    GameObject old;
                    if (WigsOnFloor.TryGetValue(tell.a, out old))
                    {
                        Remove(old);
                        WigsOnFloor.Remove(tell.a);
                    }
                    if (tell.j == 1)
                    {
                        GameObject wig = Tricks.Build("hat_wig", new Color32(230, 200, 90, 255), 0.25f);
                        wig.transform.position = Tricks.Settle(tell.v) + Vector3.up * 0.06f;
                        wig.transform.localScale = Vector3.one * Mathf.Clamp(Plugin.Settings.headSize, 0.3f, 4f);
                        WigsOnFloor[tell.a] = wig;
                        Game.PlaySoundAt(AudioEvents.sfx_toiletUnClogging_sfx, tell.v);
                    }
                    break;
                }
            }
        }

        // --------------------------------------------------------------------------- screen

        private static Texture2D Pizza()
        {
            if (_pizza != null)
            {
                return _pizza;
            }
            const int size = 128;
            _pizza = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var pepperoni = new[] { new Vector2(40f, 44f), new Vector2(86f, 40f), new Vector2(64f, 76f), new Vector2(34f, 88f), new Vector2(92f, 84f) };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(64f, 64f));
                    Color32 pixel = new Color32(0, 0, 0, 0);
                    if (distance < 62f)
                    {
                        pixel = distance > 54f ? new Color32(196, 136, 62, 255) : new Color32(250, 204, 70, 255);
                        if (distance <= 54f)
                        {
                            foreach (Vector2 slice in pepperoni)
                            {
                                if (Vector2.Distance(new Vector2(x, y), slice) < 11f)
                                {
                                    pixel = new Color32(196, 50, 40, 255);
                                }
                            }
                        }
                    }
                    _pizza.SetPixel(x, y, pixel);
                }
            }
            _pizza.Apply(false, true);
            return _pizza;
        }

        /// <summary>Screen effects and the hint line for teachers and wig thieves. Drawn in a 1280 by 720 frame.</summary>
        public static string DrawHud(float width, float height)
        {
            if (!Game.InClass)
            {
                return null;
            }
            if (Time.time < _pizzaUntil)
            {
                // The slice covers most of the screen and slowly slides down.
                float left = _pizzaUntil - Time.time;
                float side = height * 1.15f;
                float slide = Mathf.Clamp01(1f - left / 1.5f) * height * 0.9f;
                GUI.DrawTexture(new Rect((width - side) * 0.5f, (height - side) * 0.5f + slide, side, side), Pizza());
            }
            Unit me = Game.LocalPlayer;
            if (me == null || !me.isActived || !Plugin.Settings.teachers)
            {
                return null;
            }
            if (me.IsStudent)
            {
                return WigToGrab(me) != null ? "Press " + Plugin.Settings.UseKeyCode + " to yank the teacher's wig off" : null;
            }
            int kind = KindOf(me);
            if (kind == 0)
            {
                return null;
            }
            string key = "[" + Plugin.Settings.FireKeyCode + " or wheel click] ";
            string wait = Time.time < _readyAt ? "  (" + Mathf.CeilToInt(_readyAt - Time.time) + "s)" : "";
            switch (kind)
            {
                case Looks.Gym:
                    return "Gym teacher     " + key + "throw pizza" + wait;
                case Looks.Lunch:
                    bool atPot = _potThere && Tricks.Flat(me.Position - _pot).magnitude < StirReach;
                    return "Lunch lady     " + key + "throw mashed potatoes" + wait + "     "
                        + (atPot ? "[" + Plugin.Settings.UseKeyCode + "] hold to stir the pot" : "stir the pot for stamina");
                case Looks.Janitor:
                    return "Janitor     " + key + "mop the floor" + wait;
                case Looks.Wig:
                    return WigIsOff(me.UID) ? "Your wig is on the floor. Walk over to it to throw again." : null;
            }
            return null;
        }
    }
}
