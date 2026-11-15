using System.Collections.Generic;
using Mirror;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ClassPack
{
    /// <summary>
    /// Idea 4: swim class. The room owner picks it in the owner window; at the next bell the
    /// mod builds a pool hall in an empty spot of the school's level and the host sends every
    /// player who runs the mod there. The game's own classroom, desks and bots stay behind.
    ///
    ///   Students  walk off the diving board into the pool, splash a teacher who stands near
    ///             the water, and dive under by crawling in the pool while their air lasts.
    ///   Teacher   catches students as usual; the game's own poles stand along the pool, and
    ///             the lifeguard chair fills stamina, since there is no blackboard.
    ///   Everyone  wades slowly in the water. The ramp at the shallow end leads out.
    ///
    /// The hall is plain tiled boxes (Rooms.cs); the things in it are models from Meshy.
    /// All distances below are in the hall's own frame: x along the pool, z across it, and
    /// y = 0 at the deck.
    /// </summary>
    internal static class Swim
    {
        public const int NormalClass = 0;
        public const int SwimClass = 1;
        public static readonly string[] ClassNames = { "Normal class", "Swim class" };

        // ---- the hall
        private const float HalfLength = 14f;
        private const float HalfWidth = 9f;
        private const float Height = 6f;
        private const float PoolX = 8f;
        private const float PoolZ = 4.5f;
        private const float Depth = 0.85f;
        private const float WaterY = -0.15f;
        private const float RampRun = 3f;

        // ---- the diving board, measured on its model: the plank's top, where the plank starts
        // and ends along the pool, where its block stands, and the ladder on its pole side
        private const float BoardTop = 1.0f;
        private const float BoardTip = 6.0f;
        private const float BoardBack = BoardTip + 3.21f;
        private const float BoardBlock = BoardTip + 2.08f;
        private const float BoardHalfWidth = 0.43f;
        private const float BoardLadderX = BoardTip + 2.7f;

        private static readonly Vector3 ChairAt = new Vector3(0f, 0f, 6.7f);

        // ---- the teachers' lounge behind the deep end wall, through a doorway in that wall
        private const float DoorZ = -6f;
        private const float DoorHalf = 0.8f;
        private const float DoorHeight = 2.3f;
        private const float LoungeDepth = 6f;
        private const float LoungeHalf = 3f;
        private const float LoungeHeight = 3f;
        private const float LoungeNear = HalfLength + 0.5f;
        private const float TableTop = 0.4f;
        private static readonly Vector3 LoungeSofa = new Vector3(LoungeNear + LoungeDepth - 0.5f, 0f, DoorZ);
        private static readonly Vector3 LoungeTable = new Vector3(LoungeNear + LoungeDepth - 1.8f, 0f, DoorZ);
        private static readonly float[] PoleX = { -6f, -2f, 2f, 6f };
        private const float PoleZ = -6.9f;

        // ---- the rules
        private const float WadeFactor = 0.6f;
        private const float AirSeconds = 8f;
        private const float BreathSeconds = 3f;
        private const float UnderPoints = 2f;
        private const float DivePoints = 12f;
        private const float DiveWait = 15f;
        private const float SplashPoints = 15f;
        private const float SplashWait = 6f;
        private const float SplashReach = 3.8f;
        private const float SplashBlind = 3f;
        private const float ChairReach = 2.4f;
        // Peeing in the pool, the swim class's way to make mischief: hold the key this long in
        // the water, hardly able to move meanwhile, so a teacher close by can catch you at it.
        // What it pays and the wait before the next go are the room owner's settings.
        private const float PeeHold = 3f;
        private const float PeeSlow = 0.25f;
        /// <summary>How far the yellow cloud spreads, and so how close a teacher in the water has to be.</summary>
        private const float PeeReach = 2.5f;
        private const float CloudLife = 16f;

        private static float PeeWait => Mathf.Clamp(Plugin.Settings.peeWaitSeconds, 5f, 120f);

        private static float PeePoints => Mathf.Clamp(Plugin.Settings.peePoints, 0f, 50f);

        // ---- what every player with the mod keeps
        private static bool _on;
        private static Vector3 _origin;
        private static GameObject _hall;
        private static readonly List<Object> Made = new List<Object>();
        private static readonly List<Light> Lamps = new List<Light>();
        private static readonly List<float> LampShare = new List<float>();
        private static readonly Dictionary<ulong, float> FootDrop = new Dictionary<ulong, float>();
        private static Transform _lounge;
        private static readonly List<Transform> Floats = new List<Transform>();
        private static readonly List<Vector3> FloatHome = new List<Vector3>();
        private static Transform _water;
        private static Material _waterPaint;
        private static float _lampStrength = -1f;
        private static float _hideAt;
        private static float _overAt;
        private static float _air = AirSeconds;
        private static float _breathUntil;
        private static bool _wasUnder;
        private static float _nextSplashAt;
        private static float _wetScreenUntil;
        private static bool _slowed;
        private static float _nextChairAt;
        private static bool _atChair;
        private static bool _inWater;
        private static bool _under;
        private static string _crawlKey;
        private static Texture2D _blob;
        private static float _peeHeld;
        private static float _nextPeeAt;
        private static bool _peeWaitForRelease;

        /// <summary>A yellow cloud spreading in the water where someone peed: a sheet at the surface and a fainter one below.</summary>
        private sealed class Cloud
        {
            public Transform Top;
            public Transform Low;
            public Material TopPaint;
            public Material LowPaint;
            public float Born;
            public float Turn;
        }

        private static readonly List<Cloud> Clouds = new List<Cloud>();
        private static Texture2D _cloudPicture;

        private sealed class Drop
        {
            public Transform Model;
            public Vector3 Speed;
            public float Born;
            public float Life;
            public float Size;
        }

        private static readonly List<Drop> Drops = new List<Drop>();

        // ---- what the host keeps
        private sealed class Swimmer
        {
            public float OnBoardAt = -99f;
            public float DiveReadyAt;
            public float Air = AirSeconds;
            public float UnderPaid;
            public float SplashReadyAt;
            public float SplashFxAt;
            public float PeeReadyAt;
            public bool Wet;
        }

        /// <summary>
        /// Peeing in the pool is the swim class's mischief. Diving off the board, splashing the
        /// teacher and staying under water still work, but only pay mischief when the room owner
        /// switches this on in the owner window.
        /// </summary>
        private static bool OldMischief => Plugin.Settings.poolOldMischief;

        private static bool _hostOn;
        private static bool _hostDecided;
        private static Vector3 _hostOrigin;
        private static float _beginAt;
        private static float _bringAt;
        private static float _nextRulesAt;
        private static float _nextStragglersAt;
        private static readonly List<ulong> HostPoles = new List<ulong>();
        private static readonly HashSet<ulong> HostBots = new HashSet<ulong>();
        private static NavMeshSurface _nav;
        private static PlaygroundNavigationArea _navArea;
        private static bool _navTried;
        private static float _nextBotsAt;
        private static bool _loggedPoolPick;
        private static bool _loggedPoolMiss;
        /// <summary>The share of a bot's picks on the deck that are moved into the pool instead.</summary>
        private const float PoolPickShare = 0.6f;
        private static readonly Dictionary<ulong, Swimmer> Swimmers = new Dictionary<ulong, Swimmer>();

        /// <summary>True while this player's game shows the pool hall for a swim class.</summary>
        public static bool Active => _on;

        /// <summary>How high the pool's water stands, in the world.</summary>
        public static float WaterWorldY => _origin.y + WaterY;

        /// <summary>Where the hall stands in the world: the middle of the pool at deck height.</summary>
        public static Vector3 Origin => _origin;

        /// <summary>A line about the host's side of things, for the self-test's report.</summary>
        /// <summary>For the self-test: how much of the hall the bots can walk on, and whether the pool can be reached.</summary>
        public static string WalkableReport()
        {
            if (_nav == null || _navArea == null)
            {
                return "walkable area: none";
            }
            NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
            float deck = 0f, ramp = 0f, pool = 0f, other = 0f;
            int count = mesh.indices != null ? mesh.indices.Length / 3 : 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 a = mesh.vertices[mesh.indices[i * 3]] - _hostOrigin;
                Vector3 b = mesh.vertices[mesh.indices[i * 3 + 1]] - _hostOrigin;
                Vector3 c = mesh.vertices[mesh.indices[i * 3 + 2]] - _hostOrigin;
                Vector3 middle = (a + b + c) / 3f;
                if (Mathf.Abs(middle.x) > HalfLength + 8f || Mathf.Abs(middle.z) > HalfWidth + 2f || Mathf.Abs(middle.y) > 8f)
                {
                    continue;
                }
                float size = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                if (middle.y > 0.3f || middle.y < -1.2f)
                {
                    other += size;
                }
                else if (middle.y > -0.15f)
                {
                    deck += size;
                }
                else if (middle.y > -0.7f)
                {
                    ramp += size;
                }
                else
                {
                    pool += size;
                }
            }
            var filter = new NavMeshQueryFilter { agentTypeID = _nav.agentTypeID, areaMask = NavMesh.AllAreas };
            var path = new NavMeshPath();
            bool found = NavMesh.CalculatePath(_hostOrigin + new Vector3(-11f, 0f, 0f), _hostOrigin + new Vector3(2f, -Depth, 0f), filter, path);
            NavMeshBuildSettings walker = NavMesh.GetSettingsByID(_nav.agentTypeID);
            return "walkable area in square metres: deck " + deck.ToString("F0") + ", ramp " + ramp.ToString("F0") + ", pool floor " + pool.ToString("F0")
                + ", elsewhere " + other.ToString("F0") + "; way from the deck into the pool: " + (found ? path.status.ToString() : "none")
                + "; walker radius " + walker.agentRadius.ToString("F2") + ", height " + walker.agentHeight.ToString("F2")
                + ", step " + walker.agentClimb.ToString("F2") + ", slope " + walker.agentSlope.ToString("F0");
        }

        public static string TestState()
        {
            return "host class on " + _hostOn + ", bots brought " + HostBots.Count + ", in the water now " + BotsInWater() + ", walkable area "
                + (_navArea != null ? "ready" : _navTried ? "FAILED" : "not asked for yet") + ", poles " + HostPoles.Count;
        }

        /// <summary>In water deep enough to swim, and free to: not carried, hung, seated or diving under.</summary>
        public static bool Swimming(Unit unit)
        {
            if (!_on || unit == null || unit.transform == null)
            {
                return false;
            }
            int state = unit.CurState();
            if (state == (int)EUnitFsmStateType.StudentBeCatched || state == (int)EUnitFsmStateType.StudentPunished || unit.IsSitting)
            {
                return false;
            }
            Vector3 feet = Feet(unit) - _origin;
            return InWater(feet) && feet.y < WaterY - 0.45f && !Crawling(unit);
        }

        /// <summary>Host: where DoorDash puts the food during a swim class: on the lounge's coffee table.</summary>
        public static bool TryLoungeSpot(out Vector3 spot)
        {
            spot = _hostOrigin + LoungeTable + Vector3.up * (TableTop + 0.15f);
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
                Swimmers.Clear();
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
                    if (Plugin.Settings.swimClass && Plugin.Settings.nextClass == SwimClass)
                    {
                        // Give the game a moment to finish handing out the roles first.
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
            if (state == EClassroomState.Classing && Time.time >= _nextRulesAt)
            {
                _nextRulesAt = Time.time + 0.2f;
                HostRules(classroom, 0.2f);
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
            Swimmers.Clear();
            HostBots.Clear();
            Net.SendToModded(new CpTell { kind = CpKind.Pool, j = 1, v = _hostOrigin });
            SpawnPoles(units, centre);
            _bringAt = Time.time + 0.6f;
            Plugin.Log("swim: class starts, pool hall at " + _hostOrigin.ToString("F1") + ", classroom at " + centre.ToString("F1"));
        }

        /// <summary>
        /// An empty spot for the hall, far from the school. The game sends heights as whole
        /// centimetres in a small number, so the hall has to stay within about 300 m up or down.
        /// </summary>
        private static Vector3 FindSpot(Vector3 centre)
        {
            float y = Mathf.Clamp(Mathf.Round(centre.y) + 100f, -220f, 220f);
            Vector3[] tries =
            {
                new Vector3(300f, 0f, 0f), new Vector3(-300f, 0f, 0f), new Vector3(0f, 0f, 300f), new Vector3(0f, 0f, -300f),
                new Vector3(400f, 0f, 400f), new Vector3(-400f, 0f, -400f)
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
            return new Vector3(Mathf.Round(centre.x) + 300f, y, Mathf.Round(centre.z));
        }

        /// <summary>Stands four of the game's own poles along the pool, so catching works as always.</summary>
        private static void SpawnPoles(UnitManager units, Vector3 classroomCentre)
        {
            HostPoles.Clear();
            Unit model = Poles.FindFreePole(classroomCentre);
            if (model == null)
            {
                Plugin.Log("swim: no pole in the school to copy, the hall has none");
                return;
            }
            // How high a pole's own middle point sits above the floor it stands on.
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
                Vector3 spot = _hostOrigin + new Vector3(x, lift, PoleZ);
                UnitDataNet data = units.CreateUnitNetData(model.ID, spot, Vector3.zero, null, 0uL, true);
                Unit pillar = units.CreateCommonUnit(data, true);
                if (pillar != null)
                {
                    HostPoles.Add(pillar.UID);
                }
            }
            Plugin.Log("swim: " + HostPoles.Count + " poles along the pool, " + lift.ToString("F2") + " m above the deck");
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
            Swimmers.Clear();
            HostBots.Clear();
            RemovePoles();
            Net.SendToModded(new CpTell { kind = CpKind.Pool, j = 0 });
            Plugin.Log("swim: class over, the pool hall is taken away");
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
                Vector3 p = player.Position - _hostOrigin;
                bool inside = Mathf.Abs(p.x) < HalfLength + 1f && Mathf.Abs(p.z) < HalfWidth + 1f && p.y > -3f && p.y < Height + 1f;
                bool inLounge = p.x > HalfLength - 1f && p.x < LoungeNear + LoungeDepth + 1f && Mathf.Abs(p.z - DoorZ) < LoungeHalf + 1f
                    && p.y > -3f && p.y < LoungeHeight + 1f;
                inside |= inLounge;
                if (inside)
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
                    at = new Vector3(-1.2f + teachers * 1.2f, 0.05f, 5.3f);
                    yaw = 180f;
                    teachers++;
                }
                else
                {
                    at = new Vector3(-12f + (students / 8) * 1.1f, 0.05f, -3.85f + (students % 8) * 1.1f);
                    yaw = 90f;
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
                Plugin.Log("swim: sent " + students + " student(s) and " + teachers + " teacher(s) to the pool, " + without + " without the mod stay behind");
                Unit me = Game.LocalPlayer;
                if (without > 0 && me != null)
                {
                    Net.Toast(me, without + (without == 1 ? " player doesn't" : " players don't") + " run Class Pack and stay in the classroom.", 6f);
                }
            }
        }

        private static void HostRules(ClassroomManager classroom, float dt)
        {
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null)
            {
                return;
            }
            foreach (Unit player in new List<Unit>(units.playerList))
            {
                if (player == null || !player.isPlayer || !player.isActived || player.IsDead || player.transform == null)
                {
                    continue;
                }
                Swimmer swimmer;
                if (!Swimmers.TryGetValue(player.UID, out swimmer))
                {
                    swimmer = new Swimmer();
                    Swimmers[player.UID] = swimmer;
                }
                Vector3 feet = Feet(player) - _hostOrigin;
                int state = player.CurState();
                bool free = state != (int)EUnitFsmStateType.StudentBeCatched && state != (int)EUnitFsmStateType.StudentPunished;
                bool wet = InWater(feet);
                if (player.IsStudent && free && OnBoard(feet))
                {
                    swimmer.OnBoardAt = Time.time;
                }
                if (wet && !swimmer.Wet)
                {
                    Vector3 where = _hostOrigin + new Vector3(feet.x, WaterY, feet.z);
                    bool dive = player.IsStudent && free && Time.time - swimmer.OnBoardAt < 3.5f;
                    if (dive && Time.time >= swimmer.DiveReadyAt)
                    {
                        swimmer.DiveReadyAt = Time.time + DiveWait;
                        Net.SendToModded(new CpTell { kind = CpKind.Splash, i = 1, a = player.UID, v = where, w = where, f = 1.5f });
                        if (OldMischief)
                        {
                            classroom.AddTrickCount(player.UID, DivePoints);
                            Net.Toast(player, "Cannonball! +" + DivePoints + " mischief", 2.5f);
                        }
                        else
                        {
                            Net.Toast(player, "Cannonball!", 2f);
                        }
                    }
                    else if (Time.time >= swimmer.SplashFxAt)
                    {
                        if (dive && OldMischief)
                        {
                            Net.Toast(player, "The board pays again in " + Mathf.CeilToInt(swimmer.DiveReadyAt - Time.time) + " seconds.", 2.5f);
                        }
                        swimmer.SplashFxAt = Time.time + 1.5f;
                        Net.SendToModded(new CpTell { kind = CpKind.Splash, i = 0, a = player.UID, v = where, w = where, f = dive ? 1.2f : 0.6f });
                    }
                }
                swimmer.Wet = wet;

                if (!player.IsStudent)
                {
                    continue;
                }
                bool under = wet && free && feet.y < -Depth + 0.35f && Crawling(player);
                if (under && swimmer.Air > 0f)
                {
                    swimmer.Air -= dt;
                    swimmer.UnderPaid += dt;
                    if (swimmer.UnderPaid >= 1f)
                    {
                        swimmer.UnderPaid -= 1f;
                        if (OldMischief)
                        {
                            classroom.AddTrickCount(player.UID, UnderPoints);
                        }
                    }
                }
                else if (!under)
                {
                    swimmer.Air = Mathf.Min(AirSeconds, swimmer.Air + dt * 2f);
                }
            }
        }

        /// <summary>
        /// Brings the game's own bot classmates to the pool. On its playground floor the game
        /// already lets bots roam a walkable area by themselves; here the pool hall is made
        /// such an area (deck, ramp and pool floor, worked out from the hall's solid boxes) and
        /// each bot is put on it and told to roam. The game runs them from there on, and takes
        /// them away itself when the class ends.
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
                    // Already at a desk: have it get up, and take it along on the next round.
                    bot.StopNPCAction(true);
                    continue;
                }
                // A spot somewhere on the deck around the pool.
                Vector3 want = Vector3.zero;
                for (int tries = 0; tries < 12; tries++)
                {
                    want = new Vector3(Random.Range(-HalfLength + 1f, HalfLength - 1f), 0f, Random.Range(-HalfWidth + 1f, HalfWidth - 1f));
                    if (Mathf.Abs(want.x) > PoolX + 0.6f || Mathf.Abs(want.z) > PoolZ + 0.6f)
                    {
                        break;
                    }
                }
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
                    Plugin.Log("swim: first bot brought to the pool at " + hit.position.ToString("F1") + ", on the walkable area: " + agent.isOnNavMesh);
                }
            }
        }

        /// <summary>Works out where bots can walk in the hall, for the kind of walker the game's bots are.</summary>
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
                    Plugin.Log("swim: the game has no roaming area for bots, the pool stays without them");
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
                    Plugin.Log("swim: the walkable area for bots could not be worked out");
                    return false;
                }
                _navArea = new PlaygroundNavigationArea(_nav, area);
                AddRampLink(agent.agentTypeID, area);
                Bounds reach = _nav.navMeshData.sourceBounds;
                Plugin.Log("swim: walkable area for bots ready, " + reach.size.x.ToString("F0") + " by " + reach.size.z.ToString("F0") + " m, walker type " + agent.agentTypeID);
                return true;
            }
            catch (System.Exception ex)
            {
                Plugin.Log("swim: bots can't come to the pool: " + ex.Message);
                _navArea = null;
                return false;
            }
        }

        /// <summary>
        /// The game's bots can't walk any slope at all, so on its own the ramp leaves the pool's
        /// floor an island they never reach. A wide link over the ramp joins the deck at its top
        /// to the pool's floor at its foot. The game's bot code walks a bot straight along a link
        /// (StudentNPCComponent.MoveAcrossLink), which here is straight down or up the ramp.
        /// </summary>
        private static void AddRampLink(int agentType, int area)
        {
            var holder = new GameObject("ramp link for bots");
            holder.transform.SetParent(_hall.transform, false);
            NavMeshLink link = holder.AddComponent<NavMeshLink>();
            link.agentTypeID = agentType;
            link.area = area;
            link.bidirectional = true;
            link.width = PoolZ * 2f - 1.2f;
            link.startPoint = new Vector3(-PoolX - 0.5f, 0f, 0f);
            link.endPoint = new Vector3(-PoolX + RampRun + 0.5f, -Depth, 0f);
            Plugin.Log("swim: link for bots over the ramp, " + link.width.ToString("F1") + " m wide");
        }

        /// <summary>
        /// Host, called from the game's own bot code with the spot a roaming bot picked to walk
        /// to next. The deck is about three times as big as the pool's floor, so left alone only
        /// a quarter of the picks would be in the water; a share of the deck picks is moved into
        /// the pool, so that about half of the bots are in the water at any time.
        /// </summary>
        public static void SteerBot(PlaygroundNavigationArea area, NavMeshAgent agent, NavMeshPath path, ref Vector3 destination)
        {
            if (area == null || area != _navArea || !_hostOn || agent == null || path == null)
            {
                return;
            }
            if (destination.y - _hostOrigin.y < -Depth * 0.5f || Random.value > PoolPickShare)
            {
                return;
            }
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            bool pathChanged = false;
            for (int tries = 0; tries < 6; tries++)
            {
                var want = new Vector3(Random.Range(-PoolX + RampRun + 0.6f, PoolX - 0.6f), -Depth, Random.Range(-PoolZ + 0.6f, PoolZ - 0.6f));
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(_hostOrigin + want, out hit, 0.6f, filter) || hit.position.y - _hostOrigin.y > -Depth * 0.5f)
                {
                    continue;
                }
                pathChanged = true;
                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    destination = hit.position;
                    if (!_loggedPoolPick)
                    {
                        _loggedPoolPick = true;
                        Plugin.Log("swim: first bot sent into the pool, to " + (hit.position - _hostOrigin).ToString("F1"));
                    }
                    return;
                }
            }
            if (pathChanged)
            {
                // No way into the pool was found: put back the way to the game's own pick.
                agent.CalculatePath(destination, path);
                if (!_loggedPoolMiss)
                {
                    _loggedPoolMiss = true;
                    Plugin.Log("swim: a bot found no way into the pool from " + (agent.transform.position - _hostOrigin).ToString("F1"));
                }
            }
        }

        /// <summary>For the self-test: how many of the bots brought to the pool are in the water now.</summary>
        public static int BotsInWater()
        {
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null)
            {
                return 0;
            }
            int count = 0;
            foreach (ulong uid in HostBots)
            {
                Unit unit;
                if (units.unitsDict.TryGetValue(uid, out unit) && unit != null && Swimming(unit))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Keeps something out of the bots' walkable area (and the bots out of it).</summary>
        private static GameObject NoBots(GameObject thing)
        {
            thing.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            return thing;
        }

        public static void OnAsk(Unit player, CpAsk ask)
        {
            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            UnitManager units = Game.Peek<UnitManager>();
            if (ask.kind == CpKind.PeeAsk)
            {
                OnPeeAsk(player, classroom, units);
                return;
            }
            if (ask.kind != CpKind.SplashAsk || !_hostOn || !Game.InClass || classroom == null || units == null
                || player == null || !player.IsStudent || !player.isActived)
            {
                return;
            }
            int state = player.CurState();
            Vector3 feet = Feet(player) - _hostOrigin;
            if (state == (int)EUnitFsmStateType.StudentBeCatched || state == (int)EUnitFsmStateType.StudentPunished || !InWater(feet))
            {
                return;
            }
            Swimmer swimmer;
            if (!Swimmers.TryGetValue(player.UID, out swimmer))
            {
                swimmer = new Swimmer();
                Swimmers[player.UID] = swimmer;
            }
            if (Time.time < swimmer.SplashReadyAt)
            {
                return;
            }
            swimmer.SplashReadyAt = Time.time + SplashWait - 0.5f;

            Unit hit = null;
            float best = SplashReach;
            foreach (Unit other in units.playerList)
            {
                if (other == null || !other.isActived || !other.IsTeacher || other.transform == null)
                {
                    continue;
                }
                Vector3 offset = other.Position - player.Position;
                float distance = Tricks.Flat(offset).magnitude;
                if (distance < best && Mathf.Abs(offset.y) < 3f)
                {
                    best = distance;
                    hit = other;
                }
            }
            Vector3 from = _hostOrigin + new Vector3(feet.x, WaterY, feet.z);
            Vector3 to;
            if (hit != null)
            {
                to = hit.Position + Vector3.up * 1.2f;
                if (OldMischief)
                {
                    classroom.AddTrickCount(player.UID, SplashPoints);
                    Net.Toast(player, "You soaked the teacher! +" + SplashPoints + " mischief", 2.5f);
                }
                else
                {
                    Net.Toast(player, "You soaked the teacher!", 2.5f);
                }
                Net.Toast(hit, Poles.NameOf(player) + " splashed you!", 2.5f);
            }
            else
            {
                Vector3 aim = Tricks.Flat(ask.w);
                to = from + (aim.sqrMagnitude > 0.01f ? aim.normalized : player.transform.forward) * 2.5f + Vector3.up * 0.6f;
            }
            Net.SendToModded(new CpTell { kind = CpKind.Splash, i = 2, a = hit != null ? hit.UID : 0uL, v = from, w = to, f = SplashBlind });
        }

        /// <summary>
        /// Host: a student peed in the pool. Mischief for it, more when a teacher is in the water
        /// inside the yellow cloud (who is told who did it), and everyone sees the cloud spread.
        /// </summary>
        private static void OnPeeAsk(Unit player, ClassroomManager classroom, UnitManager units)
        {
            if (!_hostOn || !Game.InClass || classroom == null || units == null || player == null || !player.IsStudent || !player.isActived)
            {
                return;
            }
            int state = player.CurState();
            Vector3 feet = Feet(player) - _hostOrigin;
            if (state == (int)EUnitFsmStateType.StudentBeCatched || state == (int)EUnitFsmStateType.StudentPunished || !InWater(feet))
            {
                return;
            }
            Swimmer swimmer;
            if (!Swimmers.TryGetValue(player.UID, out swimmer))
            {
                swimmer = new Swimmer();
                Swimmers[player.UID] = swimmer;
            }
            if (Time.time < swimmer.PeeReadyAt)
            {
                return;
            }
            float wait = PeeWait;
            float points = PeePoints;
            swimmer.PeeReadyAt = Time.time + wait - 0.5f;
            classroom.AddTrickCount(player.UID, points);
            int teachers = 0;
            foreach (Unit other in units.playerList)
            {
                if (other == null || !other.isActived || !other.IsTeacher || other.transform == null)
                {
                    continue;
                }
                Vector3 theirs = Feet(other) - _hostOrigin;
                if (InWater(theirs) && Tricks.Flat(theirs - feet).magnitude < PeeReach)
                {
                    teachers++;
                    Net.Toast(other, Poles.NameOf(player) + " peed in the pool right next to you!", 3f);
                }
            }
            if (teachers > 0)
            {
                // Double for peeing right next to a teacher in the water.
                classroom.AddTrickCount(player.UID, points);
                Net.Toast(player, "You peed in the pool and the teacher is swimming in it! +" + Mathf.RoundToInt(points * 2f) + " mischief", 3f);
            }
            else
            {
                Net.Toast(player, "You peed in the pool! +" + Mathf.RoundToInt(points) + " mischief", 2.5f);
            }
            // f tells the student's game how long the wait is, so its countdown matches the host's.
            Net.SendToModded(new CpTell { kind = CpKind.Pee, a = player.UID, v = _hostOrigin + new Vector3(feet.x, WaterY, feet.z), f = wait });
        }

        /// <summary>Self-test: pee in the pool right away, without holding the key.</summary>
        public static void TestPee()
        {
            Unit me = Game.LocalPlayer;
            if (me != null)
            {
                _nextPeeAt = Time.time + PeeWait;
                Net.SendToHost(new CpAsk { kind = CpKind.PeeAsk, v = me.Position });
            }
        }

        /// <summary>A player's game joined the room while a swim class runs: show them the hall too.</summary>
        public static void OnPeerJoined(NetworkConnectionToClient connection)
        {
            if (_hostOn)
            {
                Net.SendTo(connection, new CpTell { kind = CpKind.Pool, j = 1, v = _hostOrigin });
            }
        }

        // ------------------------------------------------------------------------- where things are

        /// <summary>The lowest point of a player, whatever point of its body the game counts as its place.</summary>
        internal static Vector3 Feet(Unit unit)
        {
            Vector3 at = unit.Position;
            float drop;
            if (!FootDrop.TryGetValue(unit.UID, out drop))
            {
                // Measured once and remembered: the swimming move lifts and turns the body later.
                drop = 0f;
                CapsuleCollider capsule = unit.transform != null ? unit.transform.GetComponentInChildren<CapsuleCollider>() : null;
                if (capsule != null)
                {
                    float half = capsule.direction == 1 ? Mathf.Max(capsule.height * 0.5f, capsule.radius) : capsule.radius;
                    float bottom = capsule.transform.TransformPoint(capsule.center).y - half * Mathf.Abs(capsule.transform.lossyScale.y);
                    // Only trust it when it is a body-sized capsule around the player's own place.
                    if (Mathf.Abs(bottom - at.y) < 1.5f)
                    {
                        drop = at.y - bottom;
                    }
                    FootDrop[unit.UID] = drop;
                }
            }
            at.y -= drop;
            return at;
        }

        /// <summary>Standing in the pool with the water well over the ankles.</summary>
        private static bool InWater(Vector3 feet)
        {
            return Mathf.Abs(feet.x) < PoolX && Mathf.Abs(feet.z) < PoolZ && feet.y < WaterY - 0.25f && feet.y > -Depth - 1f;
        }

        private static bool OnBoard(Vector3 feet)
        {
            return feet.y > BoardTop - 0.3f && feet.y < BoardTop + 1f && feet.x > BoardTip - 0.2f && feet.x < BoardBack + 0.2f
                && Mathf.Abs(feet.z) < BoardHalfWidth + 0.35f;
        }

        private static bool Crawling(Unit unit)
        {
            StudentCrawlComponent crawl = unit.GetComponent<StudentCrawlComponent>();
            return crawl != null && crawl.IsCrawling;
        }

        // ------------------------------------------------------------------------- every player

        public static void OnTell(CpTell tell)
        {
            switch (tell.kind)
            {
                case CpKind.Pool:
                    if (tell.j == 1)
                    {
                        _origin = tell.v;
                        _hideAt = 0f;
                        _overAt = 0f;
                        _air = AirSeconds;
                        _breathUntil = 0f;
                        _wetScreenUntil = 0f;
                        Build();
                        _on = _hall != null;
                        Unit me = Game.LocalPlayer;
                        if (me != null && me.IsSitting)
                        {
                            me.ExitSitState();
                        }
                        Game.Notify("Swim class! Everyone to the pool.", 4f);
                    }
                    else if (_on && _hideAt == 0f)
                    {
                        // The game sends everyone back first; the hall goes a moment later.
                        _hideAt = Time.time + 1.5f;
                    }
                    break;
                case CpKind.Splash:
                {
                    if (!_on)
                    {
                        break;
                    }
                    Unit me = Game.LocalPlayer;
                    if (tell.i == 2)
                    {
                        Spray(tell.v, tell.w);
                        Game.PlaySoundAt(AudioEvents.sfx_foodIntoHotpot_sfx, tell.v);
                        if (me != null && tell.a != 0uL && me.UID == tell.a)
                        {
                            _wetScreenUntil = Time.time + Mathf.Clamp(tell.f, 1f, 6f);
                            Game.PlaySound(AudioEvents.sfx_bubbleExplode_sfx);
                        }
                    }
                    else
                    {
                        Burst(tell.v, Mathf.Clamp(tell.f, 0.3f, 2f));
                        Game.PlaySoundAt(tell.i == 1 ? AudioEvents.sfx_foodIntoHotpot_sfx : AudioEvents.sfx_ItemDrop_sfx, tell.v);
                    }
                    break;
                }
                case CpKind.Pee:
                    if (_on)
                    {
                        AddCloud(tell.v);
                        Game.PlaySoundAt(AudioEvents.sfx_balloonLeak_sfx, tell.v);
                        Unit peer = Game.LocalPlayer;
                        if (peer != null && peer.UID == tell.a && tell.f > 0f)
                        {
                            _nextPeeAt = Time.time + tell.f;
                        }
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
            // Never leave the hall standing after class, even if the host's word got lost.
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
            if (_hall == null || (_hideAt > 0f && Time.time >= _hideAt))
            {
                Take();
                return;
            }
            Animate();

            Unit me = Game.LocalPlayer;
            if (me == null || !me.isActived || me.transform == null)
            {
                Slow(null, 1f);
                return;
            }
            Vector3 feet = Feet(me) - _origin;
            int state = me.CurState();
            bool free = state != (int)EUnitFsmStateType.StudentBeCatched && state != (int)EUnitFsmStateType.StudentPunished;
            _inWater = InWater(feet);
            _under = _inWater && free && me.IsStudent && feet.y < -Depth + 0.35f && Crawling(me);
            // Crawling is slow by itself, so only wading is slowed.
            // Peeing roots you in place: the teacher can catch you at it.
            Slow(me, _peeHeld > 0f ? PeeSlow : _inWater && free && !_under ? WadeFactor : 1f);

            if (_under)
            {
                if (!_wasUnder)
                {
                    Game.PlaySound(AudioEvents.sfx_bubbleBlow_sfx);
                }
                _air -= Time.deltaTime;
                if (_air <= 0f || Time.time < _breathUntil)
                {
                    if (_air <= 0f && Time.time >= _breathUntil)
                    {
                        _breathUntil = Time.time + BreathSeconds;
                        Game.Notify("Out of air! Catch your breath.", 2.5f);
                    }
                    StudentCrawlComponent crawl = me.GetComponent<StudentCrawlComponent>();
                    if (crawl != null)
                    {
                        crawl.SetCrawlingFromInput(false);
                    }
                }
            }
            else
            {
                _air = Mathf.Min(AirSeconds, _air + Time.deltaTime * 2f);
            }
            _wasUnder = _under;

            bool inClass = Game.InClass;
            if (inClass && me.IsStudent && _inWater && free && !_under && !Game.IsTyping && !CursorGrab.Busy
                && Input.GetKeyDown(Plugin.Settings.UseKeyCode) && Tricks.HeldLocal == 0 && !NerfGuns.HoldingLocal)
            {
                if (Time.time < _nextSplashAt)
                {
                    Tricks.Tip("You can splash again in " + Mathf.CeilToInt(_nextSplashAt - Time.time) + " seconds.");
                }
                else
                {
                    _nextSplashAt = Time.time + SplashWait;
                    Camera camera = Camera.main;
                    Net.SendToHost(new CpAsk
                    {
                        kind = CpKind.SplashAsk,
                        v = me.Position,
                        w = camera != null ? camera.transform.forward : me.transform.forward
                    });
                }
            }

            // Peeing in the pool: hold the key for a moment while in the water.
            KeyCode peeKey = Plugin.Settings.PeeKeyCode;
            bool peeHeld = Input.GetKey(peeKey);
            bool canPee = inClass && me.IsStudent && _inWater && free && !Game.IsTyping && !CursorGrab.Busy;
            if (!peeHeld)
            {
                _peeHeld = 0f;
                _peeWaitForRelease = false;
            }
            else if (!canPee || _peeWaitForRelease)
            {
                _peeHeld = 0f;
            }
            else if (Time.time < _nextPeeAt)
            {
                _peeHeld = 0f;
                if (Input.GetKeyDown(peeKey))
                {
                    Tricks.Tip("You can pee again in " + Mathf.CeilToInt(_nextPeeAt - Time.time) + " seconds.");
                }
            }
            else
            {
                _peeHeld += Time.deltaTime;
                if (_peeHeld >= PeeHold)
                {
                    _peeHeld = 0f;
                    _peeWaitForRelease = true;
                    _nextPeeAt = Time.time + PeeWait;
                    Net.SendToHost(new CpAsk { kind = CpKind.PeeAsk, v = me.Position });
                }
            }

            _atChair = me.IsTeacher && Tricks.Flat(feet - ChairAt).magnitude < ChairReach && feet.y > -0.3f;
            if (inClass && _atChair && Time.time >= _nextChairAt)
            {
                _nextChairAt = Time.time + 0.25f;
                SkillComponent skill = me.GetComponent<SkillComponent>();
                if (skill != null)
                {
                    skill.AddSkillEnergy(1.25f);
                }
            }
        }

        /// <summary>Wading: the game has a walking-speed factor of its own that nothing else uses.</summary>
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

        // ------------------------------------------------------------------------- building the hall

        private static void Build()
        {
            Take();
            _hall = new GameObject("[ClassPack pool hall]");
            Object.DontDestroyOnLoad(_hall);
            _hall.transform.position = _origin;
            Transform room = _hall.transform;

            const float glow = 0.08f;
            Texture2D sandPicture = Keep(Rooms.Tiles(new Color32(196, 180, 152, 255), new Color32(164, 148, 122, 255), 11));
            Texture2D wallPicture = Keep(Rooms.Tiles(new Color32(200, 206, 212, 255), new Color32(164, 175, 188, 255), 23));
            Texture2D poolPicture = Keep(Rooms.Tiles(new Color32(112, 192, 230, 255), new Color32(80, 158, 206, 255), 37));
            Texture2D ripples = Keep(Rooms.Ripples());
            Material deck = Keep(Rooms.Tiled(sandPicture, 0.2f, glow));
            Material wall = Keep(Rooms.Tiled(wallPicture, 0.35f, glow));
            Material basin = Keep(Rooms.Tiled(poolPicture, 0.4f, glow));
            Material stripe = Keep(Rooms.Plain(new Color32(58, 128, 200, 255), 0.4f, glow));
            Material white = Keep(Rooms.Plain(new Color32(226, 226, 226, 255), 0.4f, glow));
            Material lane = Keep(Rooms.Plain(new Color32(38, 84, 164, 255), 0.4f, glow));
            Material ceiling = Keep(Rooms.Plain(new Color32(214, 220, 228, 255), 0.1f, 0.3f));
            Material glass = Keep(Rooms.Plain(new Color32(150, 205, 245, 255), 0.6f, 0.5f));
            Material bulb = Keep(Rooms.Plain(new Color32(255, 250, 236, 255), 0.2f, 1f));
            _waterPaint = Keep(Rooms.Water(new Color(0.22f, 0.6f, 0.92f, 0.62f), ripples));
            if (deck == null || _waterPaint == null)
            {
                Plugin.Log("swim: the game's shader was not found, the hall can't be built");
                Take();
                return;
            }

            float deckX = HalfLength - PoolX;
            float deckZ = HalfWidth - PoolZ;
            // The deck around the pool; its inner sides are the pool's walls.
            Rooms.Box(room, "deck shallow end", new Vector3(-(PoolX + deckX * 0.5f), -0.6f, 0f), new Vector3(deckX, 1.2f, HalfWidth * 2f), deck, 0.5f, true);
            Rooms.Box(room, "deck deep end", new Vector3(PoolX + deckX * 0.5f, -0.6f, 0f), new Vector3(deckX, 1.2f, HalfWidth * 2f), deck, 0.5f, true);
            Rooms.Box(room, "deck pole side", new Vector3(0f, -0.6f, -(PoolZ + deckZ * 0.5f)), new Vector3(PoolX * 2f, 1.2f, deckZ), deck, 0.5f, true);
            Rooms.Box(room, "deck chair side", new Vector3(0f, -0.6f, PoolZ + deckZ * 0.5f), new Vector3(PoolX * 2f, 1.2f, deckZ), deck, 0.5f, true);

            // The pool: floor, blue walls, the ramp out at the shallow end, lane lines, a white rim.
            Rooms.Box(room, "pool floor", new Vector3(0f, -Depth - 0.1f, 0f), new Vector3(PoolX * 2f, 0.2f, PoolZ * 2f), basin, 0.33f, true);
            float wallHeight = Depth - 0.02f;
            float wallY = -Depth + wallHeight * 0.5f;
            Rooms.Box(room, "pool wall", new Vector3(-PoolX + 0.02f, wallY, 0f), new Vector3(0.04f, wallHeight, PoolZ * 2f), basin, 0.33f, false);
            Rooms.Box(room, "pool wall", new Vector3(PoolX - 0.02f, wallY, 0f), new Vector3(0.04f, wallHeight, PoolZ * 2f), basin, 0.33f, false);
            Rooms.Box(room, "pool wall", new Vector3(0f, wallY, -PoolZ + 0.02f), new Vector3(PoolX * 2f, wallHeight, 0.04f), basin, 0.33f, false);
            Rooms.Box(room, "pool wall", new Vector3(0f, wallY, PoolZ - 0.02f), new Vector3(PoolX * 2f, wallHeight, 0.04f), basin, 0.33f, false);
            float rampLength = Mathf.Sqrt(RampRun * RampRun + Depth * Depth);
            float rampAngle = Mathf.Atan2(Depth, RampRun) * Mathf.Rad2Deg;
            Vector3 rampUp = Quaternion.Euler(0f, 0f, -rampAngle) * Vector3.up;
            Rooms.Box(room, "pool ramp", new Vector3(-PoolX + RampRun * 0.5f, -Depth * 0.5f, 0f) - rampUp * 0.06f,
                new Vector3(rampLength, 0.12f, PoolZ * 2f - 0.1f), basin, 0.33f, true, new Vector3(0f, 0f, -rampAngle));
            float laneFrom = -PoolX + RampRun + 0.4f;
            float laneTo = PoolX - 0.6f;
            foreach (float z in new[] { -PoolZ * 0.5f, 0f, PoolZ * 0.5f })
            {
                Rooms.Box(room, "lane line", new Vector3((laneFrom + laneTo) * 0.5f, -Depth + 0.01f, z), new Vector3(laneTo - laneFrom, 0.02f, 0.22f), lane, 1f, false);
            }
            const float rim = 0.28f;
            Rooms.Box(room, "pool rim", new Vector3(0f, 0.012f, -PoolZ - rim * 0.5f), new Vector3(PoolX * 2f + rim * 2f, 0.024f, rim), white, 1f, false);
            Rooms.Box(room, "pool rim", new Vector3(0f, 0.012f, PoolZ + rim * 0.5f), new Vector3(PoolX * 2f + rim * 2f, 0.024f, rim), white, 1f, false);
            Rooms.Box(room, "pool rim", new Vector3(-PoolX - rim * 0.5f, 0.012f, 0f), new Vector3(rim, 0.024f, PoolZ * 2f), white, 1f, false);
            Rooms.Box(room, "pool rim", new Vector3(PoolX + rim * 0.5f, 0.012f, 0f), new Vector3(rim, 0.024f, PoolZ * 2f), white, 1f, false);

            // Walls with a blue band, big bright windows on the chair side, and the ceiling.
            float wallMiddle = Height * 0.5f;
            Rooms.Box(room, "wall", new Vector3(-HalfLength - 0.25f, wallMiddle, 0f), new Vector3(0.5f, Height, HalfWidth * 2f + 1f), wall, 0.5f, true);
            // The deep end wall has the doorway to the teachers' lounge in it.
            float wallFrom = -HalfWidth - 0.5f;
            float wallTo = HalfWidth + 0.5f;
            float doorFrom = DoorZ - DoorHalf;
            float doorTo = DoorZ + DoorHalf;
            Rooms.Box(room, "wall", new Vector3(HalfLength + 0.25f, wallMiddle, (wallFrom + doorFrom) * 0.5f), new Vector3(0.5f, Height, doorFrom - wallFrom), wall, 0.5f, true);
            Rooms.Box(room, "wall", new Vector3(HalfLength + 0.25f, wallMiddle, (doorTo + wallTo) * 0.5f), new Vector3(0.5f, Height, wallTo - doorTo), wall, 0.5f, true);
            Rooms.Box(room, "wall", new Vector3(HalfLength + 0.25f, (DoorHeight + Height) * 0.5f, DoorZ), new Vector3(0.5f, Height - DoorHeight, DoorHalf * 2f), wall, 0.5f, true);
            Rooms.Box(room, "wall", new Vector3(0f, wallMiddle, -HalfWidth - 0.25f), new Vector3(HalfLength * 2f, Height, 0.5f), wall, 0.5f, true);
            Rooms.Box(room, "wall", new Vector3(0f, wallMiddle, HalfWidth + 0.25f), new Vector3(HalfLength * 2f, Height, 0.5f), wall, 0.5f, true);
            Rooms.Box(room, "band", new Vector3(-HalfLength + 0.015f, 1.25f, 0f), new Vector3(0.03f, 0.3f, HalfWidth * 2f), stripe, 1f, false);
            Rooms.Box(room, "band", new Vector3(HalfLength - 0.015f, 1.25f, (-HalfWidth + doorFrom - 0.14f) * 0.5f), new Vector3(0.03f, 0.3f, doorFrom - 0.14f + HalfWidth), stripe, 1f, false);
            Rooms.Box(room, "band", new Vector3(HalfLength - 0.015f, 1.25f, (doorTo + 0.14f + HalfWidth) * 0.5f), new Vector3(0.03f, 0.3f, HalfWidth - doorTo - 0.14f), stripe, 1f, false);
            Rooms.Box(room, "band", new Vector3(0f, 1.25f, -HalfWidth + 0.015f), new Vector3(HalfLength * 2f, 0.3f, 0.03f), stripe, 1f, false);
            Rooms.Box(room, "band", new Vector3(0f, 1.25f, HalfWidth - 0.015f), new Vector3(HalfLength * 2f, 0.3f, 0.03f), stripe, 1f, false);
            foreach (float x in new[] { -8.5f, 0f, 8.5f })
            {
                Rooms.Box(room, "window frame", new Vector3(x, 3.7f, HalfWidth - 0.02f), new Vector3(5.6f, 2.8f, 0.04f), white, 1f, false);
                Rooms.Box(room, "window", new Vector3(x, 3.7f, HalfWidth - 0.045f), new Vector3(5.2f, 2.4f, 0.03f), glass, 1f, false);
                Rooms.Box(room, "window bar", new Vector3(x, 3.7f, HalfWidth - 0.065f), new Vector3(0.1f, 2.4f, 0.02f), white, 1f, false);
            }
            // Nothing reaches the ceiling, so it is for looks only: a solid one would count as
            // a floor the bots could stand on.
            Rooms.Box(room, "ceiling", new Vector3(0f, Height + 0.1f, 0f), new Vector3(HalfLength * 2f + 1f, 0.2f, HalfWidth * 2f + 1f), ceiling, 1f, false);

            // Lamps: a bright panel in the ceiling and a light under each.
            Lamps.Clear();
            LampShare.Clear();
            foreach (float x in new[] { -9f, 0f, 9f })
            {
                foreach (float z in new[] { -4.5f, 4.5f })
                {
                    Rooms.Box(room, "lamp panel", new Vector3(x, Height - 0.02f, z), new Vector3(2.4f, 0.04f, 0.7f), bulb, 1f, false);
                    Lamps.Add(Rooms.Lamp(room, new Vector3(x, Height - 0.9f, z), 17f, 1f, new Color(1f, 0.97f, 0.9f)));
                    LampShare.Add(1f);
                }
            }
            _lampStrength = -1f;

            BuildLounge(room, white, bulb);

            GameObject water = Rooms.Sheet(room, "water", new Vector3(0f, WaterY, 0f), PoolX * 2f, PoolZ * 2f, _waterPaint, 3f);
            _water = water.transform;

            // Everything made so far is the mod's own and goes away with the hall. The models
            // added below are shared with the rest of the mod and stay loaded.
            foreach (MeshFilter filter in _hall.GetComponentsInChildren<MeshFilter>())
            {
                Made.Add(filter.sharedMesh);
            }
            Furnish(room);
            LogLight();
        }

        /// <summary>
        /// The teachers' lounge behind the deep end wall: a small warm room with a sofa and a
        /// coffee table. In a swim class DoorDash drops the food on that table, so a student
        /// has to sneak in for it. Bots are kept out.
        /// </summary>
        private static void BuildLounge(Transform room, Material white, Material bulb)
        {
            var holder = new GameObject("lounge");
            holder.transform.SetParent(room, false);
            NoBots(holder);
            _lounge = holder.transform;

            Texture2D woodPicture = Keep(Rooms.Tiles(new Color32(180, 136, 92, 255), new Color32(140, 100, 64, 255), 53));
            Material wood = Keep(Rooms.Tiled(woodPicture, 0.3f, 0.08f));
            Material cream = Keep(Rooms.Plain(new Color32(220, 206, 178, 255), 0.15f, 0.08f));
            Material rug = Keep(Rooms.Plain(new Color32(196, 76, 58, 255), 0.05f, 0.08f));
            Material frame = Keep(Rooms.Plain(new Color32(140, 92, 56, 255), 0.3f, 0.08f));

            float far = LoungeNear + LoungeDepth;
            float middle = (LoungeNear + far) * 0.5f;
            float wide = LoungeHalf * 2f;
            // The floor runs from under the doorway to under the far wall.
            Rooms.Box(_lounge, "lounge floor", new Vector3((HalfLength + far + 0.5f) * 0.5f, -0.6f, DoorZ), new Vector3(far + 0.5f - HalfLength, 1.2f, wide + 1f), wood, 0.6f, true);
            Rooms.Box(_lounge, "lounge wall", new Vector3(far + 0.25f, LoungeHeight * 0.5f, DoorZ), new Vector3(0.5f, LoungeHeight, wide + 1f), cream, 1f, true);
            Rooms.Box(_lounge, "lounge wall", new Vector3(middle, LoungeHeight * 0.5f, DoorZ - LoungeHalf - 0.25f), new Vector3(LoungeDepth, LoungeHeight, 0.5f), cream, 1f, true);
            Rooms.Box(_lounge, "lounge wall", new Vector3(middle, LoungeHeight * 0.5f, DoorZ + LoungeHalf + 0.25f), new Vector3(LoungeDepth, LoungeHeight, 0.5f), cream, 1f, true);
            // The pool wall's own back, painted like the room.
            float side = LoungeHalf - DoorHalf;
            Rooms.Box(_lounge, "lounge wall", new Vector3(LoungeNear + 0.01f, LoungeHeight * 0.5f, DoorZ - DoorHalf - side * 0.5f), new Vector3(0.02f, LoungeHeight, side), cream, 1f, false);
            Rooms.Box(_lounge, "lounge wall", new Vector3(LoungeNear + 0.01f, LoungeHeight * 0.5f, DoorZ + DoorHalf + side * 0.5f), new Vector3(0.02f, LoungeHeight, side), cream, 1f, false);
            Rooms.Box(_lounge, "lounge wall", new Vector3(LoungeNear + 0.01f, (DoorHeight + LoungeHeight) * 0.5f, DoorZ), new Vector3(0.02f, LoungeHeight - DoorHeight, DoorHalf * 2f), cream, 1f, false);
            Rooms.Box(_lounge, "lounge ceiling", new Vector3(middle, LoungeHeight + 0.1f, DoorZ), new Vector3(LoungeDepth + 1f, 0.2f, wide + 1f), white, 1f, false);
            Rooms.Box(_lounge, "rug", new Vector3(LoungeTable.x, 0.008f, DoorZ), new Vector3(2f, 0.016f, 2.8f), rug, 1f, false);
            // A wooden door frame on the pool side, so the doorway is easy to spot.
            Rooms.Box(_lounge, "door frame", new Vector3(HalfLength - 0.03f, DoorHeight * 0.5f, DoorZ - DoorHalf - 0.07f), new Vector3(0.06f, DoorHeight + 0.14f, 0.14f), frame, 1f, false);
            Rooms.Box(_lounge, "door frame", new Vector3(HalfLength - 0.03f, DoorHeight * 0.5f, DoorZ + DoorHalf + 0.07f), new Vector3(0.06f, DoorHeight + 0.14f, 0.14f), frame, 1f, false);
            Rooms.Box(_lounge, "door frame", new Vector3(HalfLength - 0.03f, DoorHeight + 0.07f, DoorZ), new Vector3(0.06f, 0.14f, DoorHalf * 2f + 0.28f), frame, 1f, false);
            Rooms.Box(_lounge, "lamp panel", new Vector3(middle, LoungeHeight - 0.02f, DoorZ), new Vector3(1.6f, 0.04f, 0.6f), bulb, 1f, false);
            Lamps.Add(Rooms.Lamp(_lounge, new Vector3(middle, LoungeHeight - 0.6f, DoorZ), 8f, 1f, new Color(1f, 0.92f, 0.78f)));
            LampShare.Add(0.3f);
        }

        /// <summary>The models in the hall, and the unseen blocks that make them solid.</summary>
        private static void Furnish(Transform room)
        {
            int missing = 0;

            // Diving board: its block stands on the deck and the plank reaches out over the deep
            // end. The ladder is on the side of the poles; an unseen slope over it lets players
            // walk up (the game has no climbing or jumping).
            if (Rooms.Prop(room, "pool_board", new Vector3(BoardTip, 0f, 0f), 0f) == null)
            {
                missing++;
            }
            float plank = BoardBack - BoardTip;
            NoBots(Rooms.Block(room, "board plank", new Vector3(BoardTip + plank * 0.5f, BoardTop - 0.08f, 0f), new Vector3(plank, 0.16f, BoardHalfWidth * 2f)));
            float block = BoardBack - BoardBlock;
            Rooms.Block(room, "board block", new Vector3(BoardBlock + block * 0.5f, 0.42f, 0f), new Vector3(block, 0.84f, BoardHalfWidth * 2f));
            const float climbRun = 1f;
            float climb = Mathf.Sqrt(climbRun * climbRun + BoardTop * BoardTop);
            float climbAngle = Mathf.Atan2(BoardTop, climbRun) * Mathf.Rad2Deg;
            Quaternion climbTurn = Quaternion.Euler(-climbAngle, 0f, 0f);
            NoBots(Rooms.Block(room, "board ladder", new Vector3(BoardLadderX, BoardTop * 0.5f, -BoardHalfWidth - climbRun * 0.5f) - climbTurn * Vector3.up * 0.05f,
                new Vector3(0.95f, 0.1f, climb), new Vector3(-climbAngle, 0f, 0f)));

            // Lifeguard chair, facing the pool.
            if (Rooms.Prop(room, "pool_chair", ChairAt, 0f) == null)
            {
                missing++;
            }
            Rooms.Block(room, "chair", ChairAt + Vector3.up * 0.95f, new Vector3(0.9f, 1.9f, 1f));

            // Benches along the window wall and the shallow end.
            foreach (float x in new[] { -10.5f, -6.5f, 6.5f, 10.5f })
            {
                if (Rooms.Prop(room, "pool_bench", new Vector3(x, 0f, HalfWidth - 0.55f), 0f) == null)
                {
                    missing++;
                }
                Rooms.Block(room, "bench", new Vector3(x, 0.25f, HalfWidth - 0.55f), new Vector3(1.9f, 0.5f, 0.52f));
            }
            foreach (float z in new[] { -5.5f, 5.5f })
            {
                Rooms.Prop(room, "pool_bench", new Vector3(-HalfLength + 0.55f, 0f, z), 90f);
                Rooms.Block(room, "bench", new Vector3(-HalfLength + 0.55f, 0.25f, z), new Vector3(0.52f, 0.5f, 1.9f));
            }

            // Ladders near the deep end, one on each side. A ladder's long legs are on its -Z side,
            // so the one on the pole side is turned round to stand in the water.
            if (Rooms.Prop(room, "pool_ladder", new Vector3(3.5f, 0f, -PoolZ), 180f) == null)
            {
                missing++;
            }
            Rooms.Prop(room, "pool_ladder", new Vector3(3.5f, 0f, PoolZ), 0f);

            // Life rings on the walls.
            if (Rooms.Prop(room, "pool_ring", new Vector3(-7f, 2.1f, -HalfWidth + 0.09f), 0f) == null)
            {
                missing++;
            }
            Rooms.Prop(room, "pool_ring", new Vector3(7f, 2.1f, -HalfWidth + 0.09f), 0f);
            Rooms.Prop(room, "pool_ring", new Vector3(HalfLength - 0.09f, 2.1f, 3.2f), 90f);
            Rooms.Prop(room, "pool_ring", new Vector3(HalfLength - 0.09f, 2.1f, -3.2f), 90f);

            // The lounge: a sofa against the far wall, facing the door, and the coffee table in
            // front of it. DoorDash puts the food on the table.
            if (_lounge != null)
            {
                if (Rooms.Prop(_lounge, "lounge_sofa", LoungeSofa, 90f) == null)
                {
                    missing++;
                }
                Rooms.Block(_lounge, "sofa", LoungeSofa + Vector3.up * 0.45f, new Vector3(0.95f, 0.9f, 1.8f));
                if (Rooms.Prop(_lounge, "lounge_table", LoungeTable, 90f) == null)
                {
                    missing++;
                }
                Rooms.Block(_lounge, "table", LoungeTable + Vector3.up * (TableTop * 0.5f), new Vector3(0.72f, TableTop, 1.3f));
            }

            // Rubber ducks that float around.
            Floats.Clear();
            FloatHome.Clear();
            Vector3[] ducks = { new Vector3(-2.5f, WaterY, 1.8f), new Vector3(2.8f, WaterY, -2.2f), new Vector3(5.6f, WaterY, 2.9f) };
            for (int i = 0; i < ducks.Length; i++)
            {
                GameObject duck = Rooms.Prop(room, "pool_duck", ducks[i], 40f + i * 115f);
                if (duck == null)
                {
                    missing++;
                    break;
                }
                Floats.Add(duck.transform);
                FloatHome.Add(ducks[i]);
            }
            if (missing > 0)
            {
                Plugin.Log("swim: " + missing + " kind(s) of pool model are missing from the models folder");
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

        /// <summary>Writes down how the game lights things, in case the hall comes out too dark or bright.</summary>
        private static void LogLight()
        {
            try
            {
                var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                Plugin.Log("swim: hall built. ambient " + RenderSettings.ambientMode + " " + RenderSettings.ambientLight
                    + " x" + RenderSettings.ambientIntensity.ToString("F2")
                    + ", extra lights " + (pipeline != null ? pipeline.additionalLightsRenderingMode + " max " + pipeline.maxAdditionalLightsCount : "unknown")
                    + ", colour space " + QualitySettings.activeColorSpace);
            }
            catch (System.Exception ex)
            {
                Plugin.Log("swim: hall built (light details unavailable: " + ex.Message + ")");
            }
        }

        /// <summary>Takes the hall away and puts the player's walking speed back.</summary>
        private static void Take()
        {
            _on = false;
            _hideAt = 0f;
            _overAt = 0f;
            _inWater = false;
            _under = false;
            _atChair = false;
            _wasUnder = false;
            _wetScreenUntil = 0f;
            Slow(null, 1f);
            foreach (Drop drop in Drops)
            {
                if (drop.Model != null)
                {
                    Object.Destroy(drop.Model.gameObject);
                }
            }
            Drops.Clear();
            foreach (Cloud cloud in Clouds)
            {
                DropCloud(cloud);
            }
            Clouds.Clear();
            _peeHeld = 0f;
            Lamps.Clear();
            LampShare.Clear();
            FootDrop.Clear();
            Floats.Clear();
            FloatHome.Clear();
            _lounge = null;
            _nav = null;
            _navArea = null;
            _navTried = false;
            _water = null;
            _waterPaint = null;
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

        // ------------------------------------------------------------------------- moving things

        private static void Animate()
        {
            float now = Time.time;
            float strength = 14f * Mathf.Clamp(Plugin.Settings.poolLight, 0.1f, 4f);
            if (!Mathf.Approximately(strength, _lampStrength))
            {
                _lampStrength = strength;
                for (int i = 0; i < Lamps.Count; i++)
                {
                    if (Lamps[i] != null)
                    {
                        Lamps[i].intensity = strength * (i < LampShare.Count ? LampShare[i] : 1f);
                    }
                }
            }
            float swell = Mathf.Sin(now * 1.3f) * 0.012f;
            if (_water != null)
            {
                _water.localPosition = new Vector3(0f, WaterY + swell, 0f);
            }
            if (_waterPaint != null)
            {
                var drift = new Vector2(now * 0.021f, now * 0.013f);
                if (_waterPaint.HasProperty("_BaseMap"))
                {
                    _waterPaint.SetTextureOffset("_BaseMap", drift);
                }
                else if (_waterPaint.HasProperty("_MainTex"))
                {
                    _waterPaint.SetTextureOffset("_MainTex", drift);
                }
            }
            for (int i = 0; i < Floats.Count; i++)
            {
                Transform duck = Floats[i];
                if (duck == null)
                {
                    continue;
                }
                Vector3 home = FloatHome[i];
                float phase = i * 2.1f;
                duck.localPosition = new Vector3(
                    home.x + Mathf.Sin(now * 0.11f + phase) * 1.1f,
                    WaterY + swell + Mathf.Sin(now * 1.7f + phase) * 0.015f,
                    home.z + Mathf.Cos(now * 0.09f + phase) * 0.8f);
                duck.localEulerAngles = new Vector3(Mathf.Sin(now * 1.1f + phase) * 3f, now * 6f + i * 115f, Mathf.Cos(now * 0.9f + phase) * 3f);
            }

            for (int i = Drops.Count - 1; i >= 0; i--)
            {
                Drop drop = Drops[i];
                float age = now - drop.Born;
                if (drop.Model == null || age >= drop.Life)
                {
                    if (drop.Model != null)
                    {
                        Object.Destroy(drop.Model.gameObject);
                    }
                    Drops.RemoveAt(i);
                    continue;
                }
                drop.Speed += Vector3.down * 9.8f * Time.deltaTime;
                drop.Model.position += drop.Speed * Time.deltaTime;
                drop.Model.localScale = Vector3.one * drop.Size * Mathf.Clamp01(1.4f - age / drop.Life);
            }

            for (int i = Clouds.Count - 1; i >= 0; i--)
            {
                if (!TickCloud(Clouds[i], now - Clouds[i].Born))
                {
                    DropCloud(Clouds[i]);
                    Clouds.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------------- yellow water

        private static Mesh _cloudMesh;

        /// <summary>A yellow cloud spreading in the water where someone peed.</summary>
        private static void AddCloud(Vector3 at)
        {
            if (_hall == null)
            {
                return;
            }
            if (_cloudPicture == null)
            {
                _cloudPicture = MakeCloudPicture();
            }
            while (Clouds.Count >= 12)
            {
                DropCloud(Clouds[0]);
                Clouds.RemoveAt(0);
            }
            Material top = Rooms.Water(new Color(1f, 0.85f, 0.12f, 0f), _cloudPicture);
            Material low = Rooms.Water(new Color(1f, 0.78f, 0.08f, 0f), _cloudPicture);
            if (top == null || low == null)
            {
                if (top != null)
                {
                    Object.Destroy(top);
                }
                if (low != null)
                {
                    Object.Destroy(low);
                }
                return;
            }
            // Drawn after the water itself, the one at the surface last.
            low.renderQueue = (int)RenderQueue.Transparent + 1;
            top.renderQueue = (int)RenderQueue.Transparent + 2;
            Vector3 local = at - _origin;
            var cloud = new Cloud
            {
                TopPaint = top,
                LowPaint = low,
                Top = MakeCloudSheet("pee cloud", new Vector3(local.x, WaterY + 0.012f, local.z), top),
                Low = MakeCloudSheet("pee cloud below", new Vector3(local.x, WaterY - 0.3f, local.z), low),
                Born = Time.time,
                Turn = Random.Range(0f, 360f)
            };
            Clouds.Add(cloud);
            TickCloud(cloud, 0f);
        }

        /// <summary>Spreads and fades a cloud. Returns false once it is gone.</summary>
        private static bool TickCloud(Cloud cloud, float age)
        {
            if (cloud.Top == null || cloud.Low == null || age >= CloudLife)
            {
                return false;
            }
            float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / 3f), 3f);
            float size = Mathf.Lerp(0.4f, PeeReach * 2f, grow);
            float fade = Mathf.Clamp01((CloudLife - age) / 6f);
            float turn = cloud.Turn + age * 6f;
            cloud.Top.localScale = new Vector3(size, 1f, size);
            cloud.Top.localRotation = Quaternion.Euler(0f, turn, 0f);
            cloud.Low.localScale = new Vector3(size * 0.85f, 1f, size * 0.85f);
            cloud.Low.localRotation = Quaternion.Euler(0f, -turn * 0.7f, 0f);
            Tint(cloud.TopPaint, new Color(1f, 0.85f, 0.12f, 0.62f * fade));
            Tint(cloud.LowPaint, new Color(1f, 0.78f, 0.08f, 0.4f * fade));
            return true;
        }

        private static void Tint(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static void DropCloud(Cloud cloud)
        {
            if (cloud.Top != null)
            {
                Object.Destroy(cloud.Top.gameObject);
            }
            if (cloud.Low != null)
            {
                Object.Destroy(cloud.Low.gameObject);
            }
            if (cloud.TopPaint != null)
            {
                Object.Destroy(cloud.TopPaint);
            }
            if (cloud.LowPaint != null)
            {
                Object.Destroy(cloud.LowPaint);
            }
        }

        /// <summary>A flat square, 1 m across, lying in the water; all clouds share its mesh.</summary>
        private static Transform MakeCloudSheet(string name, Vector3 local, Material paint)
        {
            if (_cloudMesh == null)
            {
                _cloudMesh = new Mesh { name = "pee cloud", hideFlags = HideFlags.HideAndDontSave };
                _cloudMesh.SetVertices(new List<Vector3>
                {
                    new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f)
                });
                _cloudMesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
                _cloudMesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
                _cloudMesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                _cloudMesh.RecalculateBounds();
            }
            var sheet = new GameObject(name);
            sheet.transform.SetParent(_hall.transform, false);
            sheet.transform.localPosition = local;
            sheet.AddComponent<MeshFilter>().sharedMesh = _cloudMesh;
            MeshRenderer renderer = sheet.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = paint;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            return sheet.transform;
        }

        /// <summary>A soft yellow blot with a wobbly edge, so it reads as something spreading in the water.</summary>
        private static Texture2D MakeCloudPicture()
        {
            const int size = 128;
            var picture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - size * 0.5f + 0.5f) / (size * 0.5f);
                    float dy = (y - size * 0.5f + 0.5f) / (size * 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);
                    float edge = 0.8f + 0.1f * Mathf.Sin(angle * 5f + 0.7f) + 0.06f * Mathf.Sin(angle * 9f + 2.1f);
                    float alpha = Mathf.Clamp01((edge - d) / 0.4f);
                    alpha *= alpha;
                    float patches = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.06f + 3.1f, y * 0.06f + 7.7f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255f * alpha * patches));
                }
            }
            picture.SetPixels32(pixels);
            picture.Apply(false, false);
            return picture;
        }

        private static void AddDrop(Vector3 at, Vector3 speed, float size, float life)
        {
            if (Drops.Count >= 90)
            {
                return;
            }
            GameObject model = Shapes.Part(null, PrimitiveType.Sphere, at, Vector3.one * size, new Color32(206, 236, 255, 255));
            Drops.Add(new Drop { Model = model.transform, Speed = speed, Born = Time.time, Life = life, Size = size });
        }

        /// <summary>Water thrown straight up, as when someone lands in the pool.</summary>
        private static void Burst(Vector3 at, float size)
        {
            int count = Mathf.RoundToInt(10f * size) + 4;
            for (int i = 0; i < count; i++)
            {
                float turn = Random.value * Mathf.PI * 2f;
                float out_ = Random.Range(0.6f, 2.2f) * size;
                var speed = new Vector3(Mathf.Cos(turn) * out_, Random.Range(2.5f, 4.6f) * Mathf.Sqrt(size), Mathf.Sin(turn) * out_);
                AddDrop(at + new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * 0.2f, speed, Random.Range(0.07f, 0.15f) * Mathf.Sqrt(size), Random.Range(0.6f, 0.95f));
            }
        }

        /// <summary>Water thrown from one place at another, as when a student splashes.</summary>
        private static void Spray(Vector3 from, Vector3 to)
        {
            const float flight = 0.5f;
            for (int i = 0; i < 16; i++)
            {
                Vector3 target = to + Random.insideUnitSphere * 0.5f;
                Vector3 speed = (target - from) / flight;
                speed.y += 0.5f * 9.8f * flight;
                AddDrop(from + Random.insideUnitSphere * 0.15f, speed * Random.Range(0.85f, 1.1f), Random.Range(0.08f, 0.16f), flight + Random.Range(0f, 0.25f));
            }
        }

        // ------------------------------------------------------------------------- on screen

        /// <summary>Draws the under-water look, the air bar and a wet screen. Returns the hint line.</summary>
        public static string DrawHud(float width, float height)
        {
            Unit me = Game.LocalPlayer;
            if (!_on || me == null)
            {
                return null;
            }
            Color old = GUI.color;
            if (_under)
            {
                GUI.color = new Color(0.1f, 0.45f, 0.85f, 0.3f);
                GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
            }
            if (_under || _air < AirSeconds - 0.05f)
            {
                var bar = new Rect(width * 0.5f - 120f, height - 156f, 240f, 16f);
                GUI.color = new Color(0f, 0f, 0f, 0.5f);
                GUI.DrawTexture(new Rect(bar.x - 2f, bar.y - 2f, bar.width + 4f, bar.height + 4f), Texture2D.whiteTexture);
                float left = Mathf.Clamp01(_air / AirSeconds);
                GUI.color = left > 0.3f ? new Color(0.45f, 0.82f, 1f, 0.95f) : new Color(1f, 0.45f, 0.35f, 0.95f);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * left, bar.height), Texture2D.whiteTexture);
                GUI.color = old;
                Skin.Ensure();
                Skin.Outlined(new Rect(bar.x, bar.y - 24f, bar.width, 22f), "AIR", Skin.Hint, Skin.White, Skin.Outline, 1.5f);
            }
            if (_peeHeld > 0f)
            {
                var bar = new Rect(width * 0.5f - 120f, height - 206f, 240f, 16f);
                GUI.color = new Color(0f, 0f, 0f, 0.5f);
                GUI.DrawTexture(new Rect(bar.x - 2f, bar.y - 2f, bar.width + 4f, bar.height + 4f), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.86f, 0.15f, 0.95f);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(_peeHeld / PeeHold), bar.height), Texture2D.whiteTexture);
                GUI.color = old;
                Skin.Ensure();
                Skin.Outlined(new Rect(bar.x, bar.y - 24f, bar.width, 22f), "PEEING...", Skin.Hint, Skin.White, Skin.Outline, 1.5f);
            }
            if (Time.time < _wetScreenUntil)
            {
                if (_blob == null)
                {
                    _blob = MakeBlob();
                }
                float left = _wetScreenUntil - Time.time;
                float fade = Mathf.Clamp01(left / 0.8f);
                GUI.color = new Color(0.55f, 0.8f, 1f, 0.3f * fade);
                GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
                var random = new System.Random(7);
                for (int i = 0; i < 22; i++)
                {
                    float size = 120f + (float)random.NextDouble() * 260f;
                    float x = (float)random.NextDouble() * width - size * 0.5f;
                    float y = (float)random.NextDouble() * height - size * 0.5f + (SplashBlind - left) * (20f + (float)random.NextDouble() * 60f);
                    GUI.color = new Color(0.75f, 0.9f, 1f, (0.55f + (float)random.NextDouble() * 0.35f) * fade);
                    GUI.DrawTexture(new Rect(x, y, size, size * 1.15f), _blob);
                }
            }
            GUI.color = old;

            if (me.IsStudent)
            {
                if (_under)
                {
                    return OldMischief ? "Under water: mischief every second while your air lasts" : "Under water while your air lasts";
                }
                if (Time.time < _breathUntil)
                {
                    return "Catching your breath...";
                }
                if (_inWater)
                {
                    string peeWait = Time.time < _nextPeeAt ? " (" + Mathf.CeilToInt(_nextPeeAt - Time.time) + "s)" : "";
                    string wait = Time.time < _nextSplashAt ? " (" + Mathf.CeilToInt(_nextSplashAt - Time.time) + "s)" : "";
                    return "[" + Plugin.Settings.PeeKeyCode + "] hold to pee in the pool" + peeWait + "     [" + Plugin.Settings.UseKeyCode + "] splash" + wait
                        + "     [" + CrawlKey() + "] dive under";
                }
                return "Swim class     get in the water and pee in the pool for mischief     the ramp at the shallow end leads in";
            }
            if (me.IsTeacher)
            {
                if (_atChair)
                {
                    return "Lifeguard chair: your stamina is filling up";
                }
                // A teacher with a power of their own sees that line instead.
                return Plugin.Settings.teachers && Looks.KindOf(me.UID) >= Looks.Gym && Looks.KindOf(me.UID) <= Looks.Janitor
                    ? null
                    : "Swim class     catch them as usual     stand at the lifeguard chair for stamina";
            }
            return null;
        }

        private static string CrawlKey()
        {
            if (_crawlKey == null)
            {
                try
                {
                    _crawlKey = MonoSingletonT<InputManager>.Instance.BindingName(eCmdType.Crawl);
                }
                catch (System.Exception)
                {
                    _crawlKey = null;
                }
                if (string.IsNullOrEmpty(_crawlKey))
                {
                    _crawlKey = "crawl key";
                }
            }
            return _crawlKey;
        }

        /// <summary>A soft round blot, for drops of water on the screen.</summary>
        private static Texture2D MakeBlob()
        {
            const int size = 64;
            var picture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - size * 0.5f + 0.5f) / (size * 0.5f);
                    float dy = (y - size * 0.5f + 0.5f) / (size * 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((1f - d) * 3f);
                    // A brighter edge, like a drop on glass.
                    float edge = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) * 6f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * (110f + edge * 145f)));
                }
            }
            picture.SetPixels32(pixels);
            picture.Apply(false, false);
            return picture;
        }
    }

    /// <summary>
    /// The game's roaming bots pick their next spot here. During a swim or dance class a share of those
    /// picks is moved into the pool (Swim.SteerBot), so that about half of the bots go swimming.
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(PlaygroundNavigationArea), "TryGetDestination")]
    internal static class PoolBotDestinations
    {
        private static bool _loggedError;

        private static void Postfix(PlaygroundNavigationArea __instance, NavMeshAgent agent, NavMeshPath path, ref Vector3 destination, bool __result)
        {
            if (!__result)
            {
                return;
            }
            try
            {
                Swim.SteerBot(__instance, agent, path, ref destination);
                Dance.SteerBot(__instance, agent, path, ref destination);
            }
            catch (System.Exception ex)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Plugin.Log("swim: error in sending a bot into the pool: " + ex);
                }
            }
        }
    }
}
