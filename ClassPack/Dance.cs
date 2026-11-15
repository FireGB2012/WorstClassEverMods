using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace ClassPack
{
    /// <summary>
    /// Idea 9: dance class. The room owner picks it in the owner window; at the next bell the
    /// mod builds a dance hall in an empty spot of the school's level and the host sends every
    /// player who runs the mod there, with the bots. The game's own music changes to one of
    /// its party songs.
    ///
    ///   Students  always dance. The class dance (a silly chicken dance, Q) lets them shuffle
    ///             around slowly. The naughty dance is the worm (J): down on the floor,
    ///             wiggling, mischief every few seconds while they keep at it, but they can only
    ///             creep along, so the teacher can catch them at it. Q gets them back up.
    ///   Teacher   doesn't have to dance (Q dances for fun), catches as usual; the DJ booth on
    ///             the stage fills stamina, since there is no blackboard.
    ///   Bots      always do the class dance, and mostly hang around on the dance floor.
    ///
    /// The moves are made in code on the game's skeleton (SwimPose). All distances below are
    /// in the hall's own frame: x along the hall, z across it, and y = 0 at the floor.
    /// </summary>
    internal static class Dance
    {
        public const int DanceClass = 2;

        /// <summary>What a character dances: nothing, the class dance or the worm.</summary>
        public const int None = 0;
        public const int ClassDance = 1;
        public const int Worm = 2;

        private const float HalfLength = 12f;
        private const float HalfWidth = 9f;
        private const float Height = 6f;
        // The light-up dance floor: tiles of 1 m around its middle.
        private static readonly Vector3 FloorMiddle = new Vector3(2f, 0f, 0f);
        private const int FloorTiles = 8;
        private const float FloorHalf = FloorTiles * 0.5f;
        // The stage at the far end, steps towards the dance floor, the DJ booth on it.
        private const float StageX = -HalfLength + 1.65f;
        private const float StageTop = 0.62f;
        private const float StageHalfWidth = 1.38f;
        private const float StageBack = -HalfLength + 0.05f;
        private const float StageFront = -9.5f;
        private const float StepsEnd = -8.6f;
        private const float StepsHalfWidth = 0.6f;
        private static readonly Vector3 BoothAt = new Vector3(-11f, StageTop, 0f);
        private static readonly Vector3 BoothSpot = new Vector3(-10.1f, StageTop, 0f);
        private const float BoothReach = 1.4f;
        private static readonly Vector3 SnackAt = new Vector3(7.5f, 0f, HalfWidth - 0.9f);
        private const float SnackTop = 0.5f;
        private static readonly float[] PoleX = { -4f, 0f, 4f, 8f };
        private const float PoleZ = -HalfWidth + 1.4f;
        /// <summary>The share of the bots' picks that is moved onto the dance floor.</summary>
        private const float BotShare = 0.7f;
        /// <summary>How fast one can move while doing the class dance, and the worm.</summary>
        private const float ShuffleSpeed = 0.6f;
        private const float WormSpeed = 0.3f;

        private static readonly AudioEvents[] Songs = { AudioEvents.bgm_Hajimi1, AudioEvents.bgm_Hajimi2 };

        // ---- every player
        private static bool _on;
        private static Vector3 _origin;
        private static GameObject _hall;
        private static float _hideAt;
        private static float _overAt;
        private static int _song;
        private static bool _musicOn;
        private static AudioEvents _musicBefore;
        private static bool _hadMusic;
        private static float _nextMusicCheckAt;
        private static bool _slowed;
        private static float _nextKeyAt;
        private static float _nextBoothAt;
        private static bool _atBooth;
        private static float _wormShown;
        private static Transform _ball;
        private static readonly List<Object> Made = new List<Object>();
        private static readonly List<Material> TilePaint = new List<Material>();
        private static readonly List<Light> PartyLights = new List<Light>();
        /// <summary>Who dances what, as the host said (players and bots alike).</summary>
        private static readonly Dictionary<ulong, int> Kinds = new Dictionary<ulong, int>();

        private static readonly Color[] Palette =
        {
            new Color(1f, 0.25f, 0.6f), new Color(0.2f, 0.85f, 1f), new Color(1f, 0.85f, 0.2f),
            new Color(0.6f, 0.35f, 1f), new Color(0.3f, 1f, 0.45f), new Color(1f, 0.55f, 0.15f)
        };

        // ---- what the host keeps
        private sealed class Wormer
        {
            public float Time;
            public bool Told;
        }

        private static bool _hostOn;
        private static bool _hostDecided;
        private static Vector3 _hostOrigin;
        private static float _beginAt;
        private static float _bringAt;
        private static float _nextRulesAt;
        private static float _nextStragglersAt;
        private static float _nextBotsAt;
        private static readonly List<ulong> HostPoles = new List<ulong>();
        private static readonly HashSet<ulong> HostBots = new HashSet<ulong>();
        private static readonly Dictionary<ulong, Wormer> Wormers = new Dictionary<ulong, Wormer>();
        private static NavMeshSurface _nav;
        private static PlaygroundNavigationArea _navArea;
        private static bool _navTried;
        private static bool _loggedPick;

        /// <summary>True while this player's game shows the dance hall.</summary>
        public static bool Active => _on;

        /// <summary>Where the hall stands in the world: the middle of its floor.</summary>
        public static Vector3 Origin => _origin;

        /// <summary>What this character dances right now (None, ClassDance or Worm).</summary>
        public static int Kind(Unit unit)
        {
            int kind;
            return _on && unit != null && Kinds.TryGetValue(unit.UID, out kind) ? kind : None;
        }

        public static bool IsDancing(Unit unit)
        {
            return Kind(unit) != None;
        }

        /// <summary>For the self-test: how the host's side of things stands.</summary>
        public static string TestState()
        {
            int dancing = 0;
            int worming = 0;
            foreach (KeyValuePair<ulong, int> pair in Kinds)
            {
                if (pair.Value == ClassDance)
                {
                    dancing++;
                }
                else if (pair.Value == Worm)
                {
                    worming++;
                }
            }
            return "host class on " + _hostOn + ", bots brought " + HostBots.Count + ", doing the class dance " + dancing
                + ", doing the worm " + worming + ", walkable area " + (_navArea != null ? "ready" : _navTried ? "FAILED" : "not asked for yet")
                + ", poles " + HostPoles.Count;
        }

        /// <summary>Host: where DoorDash puts the food during a dance class: on the snack table.</summary>
        public static bool TrySnackSpot(out Vector3 spot)
        {
            spot = _hostOrigin + SnackAt + new Vector3(0.3f, SnackTop + 0.15f, -0.05f);
            return _hostOn;
        }

        public static void Tick()
        {
            bool session = NetworkClient.active || NetworkServer.active;
            ClassroomManager classroom = session ? Game.Peek<ClassroomManager>() : null;
            if (classroom == null)
            {
                _hostOn = false;
                _hostDecided = false;
                _beginAt = 0f;
                _bringAt = 0f;
                HostPoles.Clear();
                HostBots.Clear();
                Wormers.Clear();
                if (_on || _hall != null)
                {
                    Take();
                }
                return;
            }
            if (NetworkServer.active)
            {
                TickHost(classroom);
            }
            TickLocal(classroom);
        }

        // ------------------------------------------------------------------------- host

        private static void TickHost(ClassroomManager classroom)
        {
            EClassroomState state = classroom.curState;
            bool starting = state == EClassroomState.Prepare || state == EClassroomState.Classing;
            if (starting)
            {
                if (!_hostDecided)
                {
                    _hostDecided = true;
                    if (Plugin.Settings.danceClass && Plugin.Settings.nextClass == DanceClass)
                    {
                        _beginAt = Time.time + 1f;
                    }
                }
            }
            else if (state == EClassroomState.BeforeClass)
            {
                _hostDecided = false;
                _beginAt = 0f;
                if (_hostOn)
                {
                    End();
                }
            }
            if (_beginAt > 0f && Time.time >= _beginAt && starting)
            {
                _beginAt = 0f;
                Begin(classroom);
            }
            if (!_hostOn)
            {
                return;
            }
            if (_bringAt > 0f)
            {
                if (Time.time >= _bringAt)
                {
                    _bringAt = 0f;
                    _nextStragglersAt = Time.time + 2f;
                    Bring(true);
                }
                return;
            }
            if (starting && Time.time >= _nextStragglersAt)
            {
                _nextStragglersAt = Time.time + 2f;
                Bring(false);
            }
            if (starting && Plugin.Settings.poolBots && Time.time >= _nextBotsAt)
            {
                _nextBotsAt = Time.time + 0.3f;
                BringBots();
            }
            if (Time.time >= _nextRulesAt)
            {
                _nextRulesAt = Time.time + 0.5f;
                HostRules(classroom, 0.5f);
            }
        }

        private static void Begin(ClassroomManager classroom)
        {
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null)
            {
                return;
            }
            ClassroomItem room = TeacherOffice.FindClassroom(classroom.currentFloor);
            Unit me = Game.LocalPlayer;
            Vector3 centre = room != null ? room.transform.position : (me != null ? me.Position : Vector3.zero);
            _hostOrigin = FindSpot(centre);
            _hostOn = true;
            HostBots.Clear();
            Wormers.Clear();
            _song = (_song + 1) % Songs.Length;
            Net.SendToModded(new CpTell { kind = CpKind.DanceHall, j = 1, i = _song, v = _hostOrigin });
            SpawnPoles(units, centre);
            _bringAt = Time.time + 0.6f;
            Plugin.Log("dance: class starts, dance hall at " + _hostOrigin.ToString("F1") + ", classroom at " + centre.ToString("F1"));
        }

        /// <summary>An empty spot for the hall, far from the school (see Swim.FindSpot for why it stays within 300 m).</summary>
        private static Vector3 FindSpot(Vector3 centre)
        {
            float y = Mathf.Clamp(Mathf.Round(centre.y) + 100f, -220f, 220f);
            Vector3[] tries =
            {
                new Vector3(-300f, 0f, 0f), new Vector3(300f, 0f, 0f), new Vector3(0f, 0f, -300f), new Vector3(0f, 0f, 300f),
                new Vector3(-400f, 0f, 400f), new Vector3(400f, 0f, -400f)
            };
            var half = new Vector3(HalfLength + 4f, Height * 0.5f + 3f, HalfWidth + 4f);
            foreach (Vector3 offset in tries)
            {
                var spot = new Vector3(Mathf.Round(centre.x + offset.x), y, Mathf.Round(centre.z + offset.z));
                if (!Physics.CheckBox(spot + Vector3.up * (Height * 0.5f), half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    return spot;
                }
            }
            return new Vector3(Mathf.Round(centre.x) - 300f, y, Mathf.Round(centre.z));
        }

        /// <summary>Stands four of the game's own poles along one wall, so catching works as always.</summary>
        private static void SpawnPoles(UnitManager units, Vector3 classroomCentre)
        {
            HostPoles.Clear();
            Unit model = Poles.FindFreePole(classroomCentre);
            if (model == null)
            {
                Plugin.Log("dance: no pole in the school to copy, the hall has none");
                return;
            }
            float lift = 0f;
            float nearest = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(model.Position + Vector3.up * 1.5f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(model.transform) || hit.distance >= nearest)
                {
                    continue;
                }
                nearest = hit.distance;
                lift = Mathf.Clamp(model.Position.y - hit.point.y, -0.2f, 1.5f);
            }
            foreach (float x in PoleX)
            {
                UnitDataNet data = units.CreateUnitNetData(model.ID, _hostOrigin + new Vector3(x, lift, PoleZ), Vector3.zero, null, 0uL, true);
                Unit pillar = units.CreateCommonUnit(data, true);
                if (pillar != null)
                {
                    HostPoles.Add(pillar.UID);
                }
            }
            Plugin.Log("dance: " + HostPoles.Count + " poles along the wall");
        }

        private static void RemovePoles()
        {
            UnitManager units = Game.Peek<UnitManager>();
            RoomManager room = Game.Peek<RoomManager>();
            if (units != null)
            {
                foreach (ulong uid in HostPoles)
                {
                    Unit pillar = units.GetUnitByUID(uid);
                    if (pillar == null || !pillar.isActived)
                    {
                        continue;
                    }
                    PunishPillarComponent pole = pillar.GetComponent<PunishPillarComponent>();
                    if (pole != null && pole.bindStudent != null && room != null)
                    {
                        room.GetSkillMessageHandler().OnStudentReleaseMessageRequest(null, new StudentReleaseMessageRequest_C2S
                        {
                            pillarUID = uid,
                            studentUID = pole.bindStudent.UID
                        });
                    }
                    pillar.SyncBack();
                }
            }
            HostPoles.Clear();
        }

        private static void End()
        {
            _hostOn = false;
            _bringAt = 0f;
            HostBots.Clear();
            Wormers.Clear();
            RemovePoles();
            Net.SendToModded(new CpTell { kind = CpKind.DanceHall, j = 0 });
            Plugin.Log("dance: class over, the dance hall is taken away");
        }

        /// <summary>
        /// Sends players into the hall: everyone at the start of class, and afterwards anyone
        /// who turns up outside it (joined late, or fell out through a gap).
        /// </summary>
        private static void Bring(bool everyone)
        {
            UnitManager units = Game.Peek<UnitManager>();
            RoomManager room = Game.Peek<RoomManager>();
            if (units == null || room == null)
            {
                return;
            }
            int students = 0;
            int teachers = 0;
            int without = 0;
            foreach (Unit player in new List<Unit>(units.playerList))
            {
                if (player == null || !player.isPlayer || !player.isActived || player.IsDead || player.transform == null)
                {
                    continue;
                }
                if (!Net.IsModded(player))
                {
                    without++;
                    continue;
                }
                if (InHall(player.Position - _hostOrigin))
                {
                    continue;
                }
                int state = player.CurState();
                if (!everyone && (state == (int)EUnitFsmStateType.StudentBeCatched || state == (int)EUnitFsmStateType.StudentPunished))
                {
                    continue;
                }
                Vector3 at;
                float yaw;
                if (player.IsTeacher)
                {
                    at = new Vector3(BoothSpot.x + 0.2f, StageTop + 0.05f, -0.8f + teachers * 0.8f);
                    yaw = 90f;
                    teachers++;
                }
                else
                {
                    at = new Vector3(9.5f - (students / 6) * 1.1f, 0.05f, -3f + (students % 6) * 1.2f);
                    yaw = 270f;
                    students++;
                }
                var message = new UnitTeleportPositionMessageResponse_S2C
                {
                    unitUID = player.UID,
                    position = _hostOrigin + at,
                    rotation = new Vector3(0f, yaw, 0f),
                    floor = 0
                };
                room.GetUnitMessageHandler().OnUnitTeleportPositionMessageResponse(message);
                room.BroadcastToAllExceptHost(message);
            }
            if (everyone)
            {
                Plugin.Log("dance: sent " + students + " student(s) and " + teachers + " teacher(s) to the dance hall, " + without + " without the mod stay behind");
                Unit me = Game.LocalPlayer;
                if (without > 0 && me != null)
                {
                    Net.Toast(me, without + (without == 1 ? " player doesn't" : " players don't") + " run Class Pack and stay in the classroom.", 6f);
                }
            }
        }

        /// <summary>
        /// Host, every half second: every student in the hall dances (the class dance unless
        /// they do the worm), nobody dances while caught, and the worm pays the owner's dance
        /// mischief every few seconds while a student keeps at it.
        /// </summary>
        private static void HostRules(ClassroomManager classroom, float dt)
        {
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null)
            {
                return;
            }
            bool classing = classroom.curState == EClassroomState.Classing;
            float every = Mathf.Clamp(Plugin.Settings.danceSeconds, 2f, 60f);
            float points = Mathf.Clamp(Plugin.Settings.dancePoints, 0f, 20f);
            foreach (Unit player in new List<Unit>(units.playerList))
            {
                if (player == null || !player.isPlayer || !player.isActived || player.transform == null)
                {
                    continue;
                }
                int state = player.CurState();
                bool free = state != (int)EUnitFsmStateType.StudentBeCatched && state != (int)EUnitFsmStateType.StudentPunished && !player.IsSitting;
                bool inside = InHall(Swim.Feet(player) - _hostOrigin);
                int kind = KindOf(player.UID);
                if (!free || !inside)
                {
                    SetKind(player.UID, None);
                    continue;
                }
                if (!player.IsStudent)
                {
                    continue;
                }
                // Everyone has to dance.
                if (kind == None)
                {
                    SetKind(player.UID, ClassDance);
                    continue;
                }
                if (kind != Worm || !classing)
                {
                    Wormers.Remove(player.UID);
                    continue;
                }
                Wormer wormer;
                if (!Wormers.TryGetValue(player.UID, out wormer))
                {
                    wormer = new Wormer();
                    Wormers[player.UID] = wormer;
                }
                wormer.Time += dt;
                if (wormer.Time >= every)
                {
                    wormer.Time -= every;
                    classroom.AddTrickCount(player.UID, points);
                    if (!wormer.Told)
                    {
                        wormer.Told = true;
                        Net.Toast(player, "The worm! +" + Mathf.RoundToInt(points) + " mischief every " + Mathf.RoundToInt(every)
                            + " seconds while you keep it up", 4f);
                    }
                }
            }
            // Bots do the class dance the whole time.
            foreach (ulong uid in HostBots)
            {
                Unit bot = units.GetUnitByUID(uid);
                if (bot == null || !bot.isActived || bot.transform == null)
                {
                    continue;
                }
                int state = bot.CurState();
                bool free = state != (int)EUnitFsmStateType.StudentBeCatched && state != (int)EUnitFsmStateType.StudentPunished && !bot.IsSitting;
                SetKind(uid, free && InHall(bot.Position - _hostOrigin) ? ClassDance : None);
            }
        }

        private static int KindOf(ulong uid)
        {
            int kind;
            return Kinds.TryGetValue(uid, out kind) ? kind : None;
        }

        /// <summary>Host: sets what a character dances and tells every modded player.</summary>
        private static void SetKind(ulong uid, int kind)
        {
            if (kind != Worm)
            {
                Wormers.Remove(uid);
            }
            if (KindOf(uid) == kind)
            {
                return;
            }
            Net.SendToModded(new CpTell { kind = CpKind.Dancing, a = uid, i = kind });
        }

        public static void OnAsk(Unit player, CpAsk ask)
        {
            if (ask.kind != CpKind.DanceAsk || !_hostOn || player == null || !player.isActived || !(player.IsStudent || player.IsTeacher))
            {
                return;
            }
            int state = player.CurState();
            if (state == (int)EUnitFsmStateType.StudentBeCatched || state == (int)EUnitFsmStateType.StudentPunished || player.IsSitting
                || !InHall(Swim.Feet(player) - _hostOrigin))
            {
                return;
            }
            int kind = Mathf.Clamp(ask.i, None, Worm);
            if (player.IsStudent)
            {
                // A student always dances: the class dance or the worm.
                SetKind(player.UID, kind == Worm ? Worm : ClassDance);
            }
            else
            {
                // The teacher may dance for fun, and stop again.
                SetKind(player.UID, KindOf(player.UID) == ClassDance ? None : ClassDance);
            }
        }

        /// <summary>A player's game joined the room while a dance class runs: show them the hall and who dances what.</summary>
        public static void OnPeerJoined(NetworkConnectionToClient connection)
        {
            if (!_hostOn)
            {
                return;
            }
            Net.SendTo(connection, new CpTell { kind = CpKind.DanceHall, j = 1, i = _song, v = _hostOrigin });
            foreach (KeyValuePair<ulong, int> pair in Kinds)
            {
                Net.SendTo(connection, new CpTell { kind = CpKind.Dancing, a = pair.Key, i = pair.Value });
            }
        }

        private static bool InHall(Vector3 local)
        {
            return Mathf.Abs(local.x) < HalfLength && Mathf.Abs(local.z) < HalfWidth && local.y > -1f && local.y < Height;
        }

        // ------------------------------------------------------------------------- bots

        /// <summary>
        /// Brings the game's own bot classmates to the hall, the way the swim class does it: the
        /// hall's floor is made a roaming area for them, and each bot is put on it.
        /// </summary>
        private static void BringBots()
        {
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null || _hall == null || (_navTried && _navArea == null))
            {
                return;
            }
            foreach (Unit unit in new List<Unit>(units.unitsDict.Values))
            {
                if (unit == null || !unit.isActived || unit.transform == null || unit.model == null || HostBots.Contains(unit.UID))
                {
                    continue;
                }
                StudentNPCComponent bot = unit.GetComponent<StudentNPCComponent>();
                NavMeshAgent agent = bot != null ? unit.model.GetComponent<NavMeshAgent>() : null;
                if (agent == null)
                {
                    continue;
                }
                if (!EnsureWalkable(agent))
                {
                    return;
                }
                int state = unit.CurState();
                if (state == (int)EUnitFsmStateType.StudentBeCatched || state == (int)EUnitFsmStateType.StudentPunished)
                {
                    continue;
                }
                if (bot.IsSitting)
                {
                    bot.StopNPCAction(true);
                    continue;
                }
                var want = new Vector3(Random.Range(-6f, HalfLength - 1.5f), 0f, Random.Range(-HalfWidth + 2.5f, HalfWidth - 2f));
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(_hostOrigin + want, out hit, 2.5f, filter))
                {
                    continue;
                }
                var facing = new Vector3(0f, Random.Range(0f, 360f), 0f);
                agent.enabled = false;
                unit.SetPosition(hit.position);
                unit.SetRotation(facing);
                agent.enabled = true;
                if (agent.isOnNavMesh)
                {
                    agent.Warp(hit.position);
                }
                ForceComponent force = unit.GetComponent<ForceComponent>();
                if (force != null && force.rigidBody != null)
                {
                    force.rigidBody.position = hit.position;
                }
                SyncTransformComponet sync = unit.GetComponent<SyncTransformComponet>();
                if (sync != null)
                {
                    sync.ApplyTransformImmediately(hit.position, facing);
                    sync.ForceSendTransformSync();
                }
                bot.SetPlaygroundArea(_navArea);
                HostBots.Add(unit.UID);
                if (HostBots.Count == 1)
                {
                    Plugin.Log("dance: first bot brought to the hall at " + (hit.position - _hostOrigin).ToString("F1"));
                }
            }
        }

        private static bool EnsureWalkable(NavMeshAgent agent)
        {
            if (_navTried)
            {
                return _navArea != null;
            }
            _navTried = true;
            try
            {
                int area = NavMesh.GetAreaFromName("PlaygroundArea");
                if (area < 0)
                {
                    Plugin.Log("dance: the game has no roaming area for bots, the hall stays without them");
                    return false;
                }
                _nav = _hall.AddComponent<NavMeshSurface>();
                _nav.agentTypeID = agent.agentTypeID;
                _nav.collectObjects = CollectObjects.Children;
                _nav.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                _nav.layerMask = ~0;
                _nav.defaultArea = area;
                _nav.BuildNavMesh();
                if (_nav.navMeshData == null)
                {
                    Plugin.Log("dance: the walkable area for bots could not be worked out");
                    return false;
                }
                _navArea = new PlaygroundNavigationArea(_nav, area);
                Plugin.Log("dance: walkable area for bots ready");
                return true;
            }
            catch (System.Exception ex)
            {
                Plugin.Log("dance: bots can't come to the hall: " + ex.Message);
                _navArea = null;
                return false;
            }
        }

        /// <summary>
        /// Host, called from the game's own bot code with the spot a roaming bot picked to walk to
        /// next (see PoolBotDestinations): most picks are moved onto the dance floor.
        /// </summary>
        public static void SteerBot(PlaygroundNavigationArea area, NavMeshAgent agent, NavMeshPath path, ref Vector3 destination)
        {
            if (area == null || area != _navArea || !_hostOn || agent == null || path == null || Random.value > BotShare)
            {
                return;
            }
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            for (int tries = 0; tries < 6; tries++)
            {
                Vector3 want = _hostOrigin + FloorMiddle + new Vector3(Random.Range(-FloorHalf + 0.6f, FloorHalf - 0.6f), 0f, Random.Range(-FloorHalf + 0.6f, FloorHalf - 0.6f));
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(want, out hit, 0.5f, filter) || (agent.transform.position - hit.position).sqrMagnitude < 1f)
                {
                    continue;
                }
                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    destination = hit.position;
                    if (!_loggedPick)
                    {
                        _loggedPick = true;
                        Plugin.Log("dance: first bot sent to the dance floor, to " + (hit.position - _hostOrigin).ToString("F1"));
                    }
                    return;
                }
            }
            // Nothing found: put back the way to the game's own pick.
            agent.CalculatePath(destination, path);
        }

        // ------------------------------------------------------------------------- every player

        public static void OnTell(CpTell tell)
        {
            switch (tell.kind)
            {
                case CpKind.DanceHall:
                    if (tell.j == 1)
                    {
                        _origin = tell.v;
                        _song = Mathf.Clamp(tell.i, 0, Songs.Length - 1);
                        _hideAt = 0f;
                        _overAt = 0f;
                        Kinds.Clear();
                        Build();
                        _on = _hall != null;
                        if (_on)
                        {
                            StartMusic();
                        }
                    }
                    else if (_on && _hideAt == 0f)
                    {
                        _hideAt = Time.time + 1.5f;
                    }
                    break;
                case CpKind.Dancing:
                    if (tell.i == None)
                    {
                        Kinds.Remove(tell.a);
                    }
                    else
                    {
                        Kinds[tell.a] = Mathf.Clamp(tell.i, ClassDance, Worm);
                    }
                    break;
            }
        }

        private static void TickLocal(ClassroomManager classroom)
        {
            if (!_on)
            {
                return;
            }
            if (classroom.curState == EClassroomState.BeforeClass)
            {
                if (_overAt == 0f)
                {
                    _overAt = Time.time;
                }
                if (_hideAt == 0f && Time.time - _overAt > 4f)
                {
                    _hideAt = Time.time;
                }
            }
            else
            {
                _overAt = 0f;
            }
            if (_hideAt > 0f && Time.time >= _hideAt)
            {
                Take();
                return;
            }
            Animate();
            KeepMusic();

            Unit me = Game.LocalPlayer;
            if (me == null || !me.isActived || me.transform == null)
            {
                return;
            }
            int state = me.CurState();
            bool free = state != (int)EUnitFsmStateType.StudentBeCatched && state != (int)EUnitFsmStateType.StudentPunished;
            Vector3 feet = Swim.Feet(me) - _origin;
            int kind = Kind(me);
            // The class dance is a shuffle; the worm only creeps along.
            Slow(me, !free ? 1f : kind == Worm ? WormSpeed : kind == ClassDance ? ShuffleSpeed : 1f);

            if (Game.InClass && (me.IsStudent || me.IsTeacher) && free && InHall(feet) && !Game.IsTyping && !CursorGrab.Busy && Time.time >= _nextKeyAt)
            {
                if (Input.GetKeyDown(Plugin.Settings.DanceKeyCode))
                {
                    _nextKeyAt = Time.time + 0.25f;
                    Net.SendToHost(new CpAsk { kind = CpKind.DanceAsk, i = ClassDance, v = me.Position });
                }
                else if (me.IsStudent && Input.GetKeyDown(Plugin.Settings.WormKeyCode))
                {
                    _nextKeyAt = Time.time + 0.25f;
                    Net.SendToHost(new CpAsk { kind = CpKind.DanceAsk, i = kind == Worm ? ClassDance : Worm, v = me.Position });
                }
            }

            _atBooth = me.IsTeacher && Tricks.Flat(feet - BoothSpot).magnitude < BoothReach && feet.y > StageTop - 0.3f;
            if (Game.InClass && me.IsTeacher && InHall(feet) && Time.time >= _nextBoothAt)
            {
                // A little all the time anywhere in the dance hall, as much as a blackboard at the booth.
                _nextBoothAt = Time.time + 0.25f;
                SkillComponent skill = me.GetComponent<SkillComponent>();
                float perSecond = _atBooth ? Plugin.Settings.hallStationStaminaPerSecond : Plugin.Settings.hallStaminaPerSecond;
                if (skill != null && perSecond > 0f)
                {
                    skill.AddSkillEnergy(Mathf.Clamp(perSecond, 0f, 50f) * 0.25f);
                }
            }

            // How long this student has done the worm, for the bar on screen (the host keeps
            // the real count, which only runs in class).
            if (kind == Worm && me.IsStudent && classroom.curState == EClassroomState.Classing)
            {
                float every = Mathf.Clamp(Plugin.Settings.danceSeconds, 2f, 60f);
                _wormShown += Time.deltaTime;
                if (_wormShown >= every)
                {
                    _wormShown -= every;
                }
            }
            else
            {
                _wormShown = 0f;
            }
        }

        private static void Slow(Unit me, float factor)
        {
            if (factor >= 1f && !_slowed)
            {
                return;
            }
            Unit player = me ?? Game.LocalPlayer;
            InputComponent input = player != null ? player.GetComponent<InputComponent>() : null;
            if (input != null)
            {
                input.speedFac = factor;
            }
            _slowed = factor < 1f;
        }

        // ------------------------------------------------------------------------- music

        private static void StartMusic()
        {
            AudioManager audio = Game.Peek<AudioManager>();
            if (audio == null)
            {
                return;
            }
            try
            {
                Traverse sound = Traverse.Create(audio);
                _musicBefore = sound.Field("currentBGMPath").GetValue<AudioEvents>();
                _hadMusic = sound.Field("currentBGMInstance").GetValue() != null;
                audio.PlayBGM(Songs[_song]);
                _musicOn = true;
                _nextMusicCheckAt = Time.time + 2f;
                Plugin.Log("dance: music " + Songs[_song] + " (before: " + (_hadMusic ? _musicBefore.ToString() : "none") + ")");
            }
            catch (System.Exception ex)
            {
                Plugin.Log("dance: the music could not be changed: " + ex.Message);
            }
        }

        /// <summary>The game changes its music by itself at times (the bell): put the party song back.</summary>
        private static void KeepMusic()
        {
            if (!_musicOn || Time.time < _nextMusicCheckAt)
            {
                return;
            }
            _nextMusicCheckAt = Time.time + 2f;
            AudioManager audio = Game.Peek<AudioManager>();
            if (audio == null)
            {
                return;
            }
            try
            {
                AudioEvents now = Traverse.Create(audio).Field("currentBGMPath").GetValue<AudioEvents>();
                if (now != Songs[_song])
                {
                    _musicBefore = now;
                    _hadMusic = true;
                    audio.PlayBGM(Songs[_song]);
                }
            }
            catch (System.Exception)
            {
                _musicOn = false;
            }
        }

        private static void StopMusic()
        {
            if (!_musicOn)
            {
                return;
            }
            _musicOn = false;
            AudioManager audio = Game.Peek<AudioManager>();
            if (audio == null)
            {
                return;
            }
            try
            {
                if (_hadMusic && _musicBefore != Songs[_song])
                {
                    audio.PlayBGM(_musicBefore);
                }
                else
                {
                    audio.StopBGM(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log("dance: the music could not be put back: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------------- building the hall

        private static void Build()
        {
            Take();
            _hall = new GameObject("[ClassPack dance hall]");
            Object.DontDestroyOnLoad(_hall);
            _hall.transform.position = _origin;
            Transform room = _hall.transform;

            Texture2D floorPicture = Keep(Rooms.Tiles(new Color32(70, 58, 96, 255), new Color32(52, 42, 74, 255), 41));
            Texture2D wallPicture = Keep(Rooms.Tiles(new Color32(64, 70, 120, 255), new Color32(48, 52, 96, 255), 43));
            Material floor = Keep(Rooms.Tiled(floorPicture, 0.45f, 0.05f));
            Material wall = Keep(Rooms.Tiled(wallPicture, 0.3f, 0.05f));
            Material ceiling = Keep(Rooms.Plain(new Color32(28, 26, 48, 255), 0.1f, 0f));
            Material neonPink = Keep(Rooms.Plain(new Color32(255, 70, 190, 255), 0.4f, 1.2f));
            Material neonBlue = Keep(Rooms.Plain(new Color32(60, 200, 255, 255), 0.4f, 1.2f));
            Material white = Keep(Rooms.Plain(new Color32(236, 236, 236, 255), 0.4f, 0.6f));
            if (floor == null || wall == null)
            {
                Plugin.Log("dance: the game's shader was not found, the hall can't be built");
                Take();
                return;
            }

            // The floor, walls and a dark ceiling.
            Rooms.Box(room, "floor", new Vector3(0f, -0.6f, 0f), new Vector3(HalfLength * 2f, 1.2f, HalfWidth * 2f), floor, 1f, true);
            float wallMiddle = Height * 0.5f;
            Rooms.Box(room, "wall", new Vector3(-HalfLength - 0.25f, wallMiddle, 0f), new Vector3(0.5f, Height, HalfWidth * 2f + 1f), wall, 1f, true);
            Rooms.Box(room, "wall", new Vector3(HalfLength + 0.25f, wallMiddle, 0f), new Vector3(0.5f, Height, HalfWidth * 2f + 1f), wall, 1f, true);
            Rooms.Box(room, "wall", new Vector3(0f, wallMiddle, -HalfWidth - 0.25f), new Vector3(HalfLength * 2f, Height, 0.5f), wall, 1f, true);
            Rooms.Box(room, "wall", new Vector3(0f, wallMiddle, HalfWidth + 0.25f), new Vector3(HalfLength * 2f, Height, 0.5f), wall, 1f, true);
            // Nothing reaches the ceiling, so it is for looks only (a solid one would be a floor for bots).
            Rooms.Box(room, "ceiling", new Vector3(0f, Height + 0.1f, 0f), new Vector3(HalfLength * 2f + 1f, 0.2f, HalfWidth * 2f + 1f), ceiling, 1f, false);
            // Neon strips along the walls.
            foreach (float y in new[] { 2.6f, 0.35f })
            {
                Material neon = y > 1f ? neonPink : neonBlue;
                Rooms.Box(room, "neon", new Vector3(0f, y, -HalfWidth + 0.02f), new Vector3(HalfLength * 2f, 0.08f, 0.04f), neon, 1f, false);
                Rooms.Box(room, "neon", new Vector3(0f, y, HalfWidth - 0.02f), new Vector3(HalfLength * 2f, 0.08f, 0.04f), neon, 1f, false);
                Rooms.Box(room, "neon", new Vector3(HalfLength - 0.02f, y, 0f), new Vector3(0.04f, 0.08f, HalfWidth * 2f), neon, 1f, false);
                Rooms.Box(room, "neon", new Vector3(-HalfLength + 0.02f, y, 0f), new Vector3(0.04f, 0.08f, HalfWidth * 2f), neon, 1f, false);
            }

            // The light-up dance floor: four groups of tiles whose colours go round with the beat.
            TilePaint.Clear();
            for (int g = 0; g < 4; g++)
            {
                Material tile = Keep(Rooms.Plain(new Color32(255, 255, 255, 255), 0.6f, 0.9f));
                TilePaint.Add(tile);
            }
            for (int i = 0; i < FloorTiles; i++)
            {
                for (int j = 0; j < FloorTiles; j++)
                {
                    int group = (i + j) % 2 == 0 ? (i / 2 + j / 2) % 2 : 2 + (i / 2 + j / 2) % 2;
                    var at = new Vector3(FloorMiddle.x - FloorHalf + i + 0.5f, 0.012f, FloorMiddle.z - FloorHalf + j + 0.5f);
                    Rooms.Box(room, "dance tile", at, new Vector3(0.94f, 0.024f, 0.94f), TilePaint[group], 1f, false);
                }
            }
            Rooms.Box(room, "floor edge", FloorMiddle + new Vector3(0f, 0.015f, -FloorHalf - 0.05f), new Vector3(FloorTiles + 0.2f, 0.03f, 0.1f), white, 1f, false);
            Rooms.Box(room, "floor edge", FloorMiddle + new Vector3(0f, 0.015f, FloorHalf + 0.05f), new Vector3(FloorTiles + 0.2f, 0.03f, 0.1f), white, 1f, false);
            Rooms.Box(room, "floor edge", FloorMiddle + new Vector3(-FloorHalf - 0.05f, 0.015f, 0f), new Vector3(0.1f, 0.03f, FloorTiles), white, 1f, false);
            Rooms.Box(room, "floor edge", FloorMiddle + new Vector3(FloorHalf + 0.05f, 0.015f, 0f), new Vector3(0.1f, 0.03f, FloorTiles), white, 1f, false);

            // Light: two soft lamps, a warm one over the stage and four coloured party lights that
            // circle over the dance floor.
            Rooms.Lamp(room, new Vector3(-5f, Height - 0.8f, 0f), 18f, 0.7f, new Color(0.9f, 0.88f, 1f));
            Rooms.Lamp(room, new Vector3(7f, Height - 0.8f, 0f), 18f, 0.7f, new Color(0.9f, 0.88f, 1f));
            Rooms.Lamp(room, new Vector3(StageX + 1f, 3.6f, 0f), 7f, 1.3f, new Color(1f, 0.9f, 0.7f));
            PartyLights.Clear();
            Color[] party = { new Color(1f, 0.2f, 0.4f), new Color(0.2f, 0.5f, 1f), new Color(0.25f, 1f, 0.4f), new Color(0.85f, 0.3f, 1f) };
            foreach (Color color in party)
            {
                PartyLights.Add(Rooms.Lamp(room, FloorMiddle + Vector3.up * 3.2f, 9f, 2.4f, color));
            }

            // Everything made so far is the mod's own and goes away with the hall.
            foreach (MeshFilter filter in _hall.GetComponentsInChildren<MeshFilter>())
            {
                Made.Add(filter.sharedMesh);
            }
            Furnish(room);
            Animate();
        }

        private static void Furnish(Transform room)
        {
            int missing = 0;
            // The stage at the far end, its steps towards the dance floor, with an unseen block to
            // stand on and an unseen slope over the steps (the game has no climbing or jumping).
            // Bots stay off it: its top is an island they can't walk up to.
            if (Rooms.Prop(room, "dance_stage", new Vector3(StageX, 0f, 0f), 270f) == null)
            {
                missing++;
            }
            float deep = StageFront - StageBack;
            Rooms.Block(room, "stage", new Vector3((StageBack + StageFront) * 0.5f, StageTop * 0.5f, 0f), new Vector3(deep, StageTop, StageHalfWidth * 2f));
            float run = StepsEnd - StageFront;
            float slope = Mathf.Sqrt(run * run + StageTop * StageTop);
            float angle = Mathf.Atan2(StageTop, run) * Mathf.Rad2Deg;
            Vector3 slopeUp = Quaternion.Euler(0f, 0f, -angle) * Vector3.up;
            Rooms.Block(room, "stage steps", new Vector3((StageFront + StepsEnd) * 0.5f, StageTop * 0.5f, 0f) - slopeUp * 0.05f,
                new Vector3(slope, 0.1f, StepsHalfWidth * 2f), new Vector3(0f, 0f, -angle));

            // The DJ booth on the stage, facing the dance floor. The teacher's stamina spot is in front of it.
            if (Rooms.Prop(room, "dance_djbooth", BoothAt, 270f) == null)
            {
                missing++;
            }
            Rooms.Block(room, "dj booth", BoothAt + Vector3.up * 0.41f, new Vector3(0.8f, 0.82f, 1.9f));

            // Speakers on either side of the stage.
            foreach (float z in new[] { -3.4f, 3.4f })
            {
                if (Rooms.Prop(room, "dance_speaker", new Vector3(-HalfLength + 0.6f, 0f, z), 270f) == null)
                {
                    missing++;
                }
                Rooms.Block(room, "speaker", new Vector3(-HalfLength + 0.6f, 0.75f, z), new Vector3(0.66f, 1.5f, 0.9f));
            }

            // The disco ball hangs over the middle of the dance floor.
            GameObject ball = Rooms.Prop(room, "dance_discoball", FloorMiddle + Vector3.up * (Height - 0.02f), 0f);
            if (ball == null)
            {
                missing++;
            }
            _ball = ball != null ? ball.transform : null;

            // The snack table by the long wall, facing the room. DoorDash puts the food on it.
            if (Rooms.Prop(room, "dance_snacks", SnackAt, 0f) == null)
            {
                missing++;
            }
            Rooms.Block(room, "snack table", SnackAt + Vector3.up * (SnackTop * 0.5f), new Vector3(1.2f, SnackTop, 0.72f));
            if (Rooms.Prop(room, "dance_boombox", new Vector3(5.7f, 0f, HalfWidth - 0.6f), 195f) == null)
            {
                missing++;
            }
            Rooms.Block(room, "boombox", new Vector3(5.7f, 0.2f, HalfWidth - 0.6f), new Vector3(0.6f, 0.4f, 0.3f));

            // Benches along the long wall and the near end.
            foreach (float x in new[] { -4f, 0f, 3.4f })
            {
                Rooms.Prop(room, "pool_bench", new Vector3(x, 0f, HalfWidth - 0.55f), 0f);
                Rooms.Block(room, "bench", new Vector3(x, 0.25f, HalfWidth - 0.55f), new Vector3(1.9f, 0.5f, 0.52f));
            }
            foreach (float z in new[] { -5.5f, 5.5f })
            {
                Rooms.Prop(room, "pool_bench", new Vector3(HalfLength - 0.55f, 0f, z), 90f);
                Rooms.Block(room, "bench", new Vector3(HalfLength - 0.55f, 0.25f, z), new Vector3(0.52f, 0.5f, 1.9f));
            }
            if (missing > 0)
            {
                Plugin.Log("dance: " + missing + " kind(s) of dance model are missing from the models folder");
            }
        }

        private static T Keep<T>(T made) where T : Object
        {
            if (made != null)
            {
                Made.Add(made);
            }
            return made;
        }

        /// <summary>The beat: the tiles change colour, the party lights circle and pulse, the ball turns.</summary>
        private static void Animate()
        {
            float now = Time.time;
            const float beatsPerSecond = 2f;
            float beat = now * beatsPerSecond;
            int step = Mathf.FloorToInt(beat);
            float pulse = 1f - (beat - step);
            for (int g = 0; g < TilePaint.Count; g++)
            {
                Material tile = TilePaint[g];
                if (tile == null)
                {
                    continue;
                }
                Color color = Palette[(g * 2 + step) % Palette.Length];
                Color lit = color * (0.55f + 0.6f * pulse);
                if (tile.HasProperty("_BaseColor"))
                {
                    tile.SetColor("_BaseColor", color);
                }
                if (tile.HasProperty("_Color"))
                {
                    tile.SetColor("_Color", color);
                }
                if (tile.HasProperty("_EmissionColor"))
                {
                    tile.SetColor("_EmissionColor", lit);
                }
            }
            for (int i = 0; i < PartyLights.Count; i++)
            {
                Light party = PartyLights[i];
                if (party == null)
                {
                    continue;
                }
                float turn = now * 0.9f + i * Mathf.PI * 0.5f;
                party.transform.localPosition = FloorMiddle + new Vector3(Mathf.Cos(turn) * 3.2f, 3.2f, Mathf.Sin(turn) * 3.2f);
                party.intensity = 1.6f + 1.4f * (((step + i) % 2 == 0) ? pulse : 1f - pulse);
            }
            if (_ball != null)
            {
                _ball.localEulerAngles = new Vector3(0f, now * 25f, 0f);
            }
        }

        private static void Take()
        {
            StopMusic();
            Slow(null, 1f);
            _on = false;
            _hideAt = 0f;
            _overAt = 0f;
            _atBooth = false;
            _wormShown = 0f;
            Kinds.Clear();
            TilePaint.Clear();
            PartyLights.Clear();
            _ball = null;
            _nav = null;
            _navArea = null;
            _navTried = false;
            if (_hall != null)
            {
                Object.Destroy(_hall);
                _hall = null;
            }
            foreach (Object made in Made)
            {
                if (made != null)
                {
                    Object.Destroy(made);
                }
            }
            Made.Clear();
        }

        // ------------------------------------------------------------------------- on screen

        /// <summary>Draws the worm's mischief bar. Returns the hint line.</summary>
        public static string DrawHud(float width, float height)
        {
            Unit me = Game.LocalPlayer;
            if (!_on || me == null)
            {
                return null;
            }
            KeyCode dance = Plugin.Settings.DanceKeyCode;
            KeyCode worm = Plugin.Settings.WormKeyCode;
            int kind = Kind(me);
            if (me.IsStudent && kind == Worm && _wormShown > 0f)
            {
                Color old = GUI.color;
                float every = Mathf.Clamp(Plugin.Settings.danceSeconds, 2f, 60f);
                var bar = new Rect(width * 0.5f - 120f, height - 156f, 240f, 16f);
                GUI.color = new Color(0f, 0f, 0f, 0.5f);
                GUI.DrawTexture(new Rect(bar.x - 2f, bar.y - 2f, bar.width + 4f, bar.height + 4f), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.35f, 0.75f, 0.95f);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(_wormShown / every), bar.height), Texture2D.whiteTexture);
                GUI.color = old;
                Skin.Ensure();
                Skin.Outlined(new Rect(bar.x, bar.y - 24f, bar.width, 22f), "THE WORM", Skin.Hint, Skin.White, Skin.Outline, 1.5f);
            }
            if (me.IsStudent)
            {
                if (kind == Worm)
                {
                    return "The worm! Mischief while you keep it up     [" + dance + "] back to the class dance";
                }
                return "Dance class: everyone has to dance     [" + worm + "] the worm for mischief     [" + dance + "] class dance";
            }
            if (me.IsTeacher)
            {
                if (_atBooth)
                {
                    return "DJ booth: your stamina is filling up";
                }
                return Plugin.Settings.teachers && Looks.KindOf(me.UID) >= Looks.Gym && Looks.KindOf(me.UID) <= Looks.Janitor
                    ? null
                    : "Dance class     catch the students doing the worm     stand at the DJ booth for stamina";
            }
            return null;
        }

        // ------------------------------------------------------------------------- self-test

        /// <summary>Self-test: ask for the class dance or the worm without the keys.</summary>
        public static void TestDance(int kind)
        {
            Unit me = Game.LocalPlayer;
            if (me != null)
            {
                Net.SendToHost(new CpAsk { kind = CpKind.DanceAsk, i = kind, v = me.Position });
            }
        }

        /// <summary>Self-test: a bot that dances right now, or null.</summary>
        public static Unit DancingBot(int skip)
        {
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null)
            {
                return null;
            }
            foreach (ulong uid in Kinds.Keys)
            {
                Unit unit = units.GetUnitByUID(uid);
                if (unit != null && unit.isActived && !unit.isPlayer && skip-- <= 0)
                {
                    return unit;
                }
            }
            return null;
        }
    }
}
