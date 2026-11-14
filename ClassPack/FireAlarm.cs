using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Idea 34: the fire alarm. A red box hangs above a classroom door. A student can pull it
    /// once per class. For a short while the siren goes, the screen flashes red, and the
    /// floor is "wet": a teacher who sprints slips over. The classmates don't react yet, and
    /// there is no water to see; both need models and animations the mod doesn't have.
    /// </summary>
    internal static class FireAlarm
    {
        private const float Seconds = 15f;
        private const float PullReach = 2.6f;
        private const float Mischief = 5f;
        private const float SlipSpeed = 2.3f;

        // Every player
        private static Vector3 _boxAt;
        private static bool _boxKnown;
        private static bool _boxLive;
        private static GameObject _box;
        private static float _alarmUntil;
        private static float _nextSiren;
        private static Texture2D _red;

        // Host
        private static bool _placed;
        private static bool _pulled;
        private static Vector3 _hostBoxAt;
        private static float _hostAlarmUntil;
        private static float _nextSlipCheck;
        private static readonly Dictionary<ulong, Vector3> LastTeacherSpot = new Dictionary<ulong, Vector3>();

        public static void Tick()
        {
            if (!Game.InClass)
            {
                if (_box != null)
                {
                    Object.Destroy(_box);
                }
                _box = null;
                _boxKnown = false;
                _boxLive = false;
                _alarmUntil = 0f;
                _placed = false;
                _pulled = false;
                _hostAlarmUntil = 0f;
                LastTeacherSpot.Clear();
                return;
            }
            if (NetworkServer.active)
            {
                TickHost();
            }
            if (_boxKnown && _box == null)
            {
                _box = BuildBox();
                _box.transform.position = _boxAt;
            }
            if (Time.time < _alarmUntil && Time.time >= _nextSiren)
            {
                _nextSiren = Time.time + 1.3f;
                Game.PlaySound(AudioEvents.sfx_SecurityCameraAlart_sfx);
            }
        }

        private static GameObject BuildBox()
        {
            var root = new GameObject("[ClassPack fire alarm]");
            Shapes.Part(root.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.34f, 0.42f, 0.34f), new Color32(220, 30, 30, 255));
            Shapes.Part(root.transform, PrimitiveType.Cube, new Vector3(0f, -0.02f, 0f), new Vector3(0.36f, 0.09f, 0.36f), new Color32(245, 245, 245, 255));
            Shapes.Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 0.27f, 0f), new Vector3(0.16f, 0.16f, 0.16f), new Color32(255, 200, 40, 255));
            return root;
        }

        private static bool NearBox(Vector3 from, Vector3 box, float reach)
        {
            Vector3 offset = box - from;
            return Mathf.Abs(offset.y) < 3.5f && new Vector2(offset.x, offset.z).magnitude <= reach;
        }

        /// <summary>Called for the shared "use" key when nothing nearer wants it.</summary>
        public static void TryPull(Unit me)
        {
            if (_boxKnown && _boxLive && me.IsStudent && NearBox(me.Position, _boxAt, PullReach))
            {
                Net.SendToHost(new CpAsk { kind = CpKind.AlarmPull });
            }
        }

        // --------------------------------------------------------------- host side

        private static void TickHost()
        {
            if (!_placed)
            {
                _placed = true;
                if (Plugin.Settings.fireAlarm)
                {
                    PlaceBox();
                }
            }
            if (_hostAlarmUntil <= 0f)
            {
                return;
            }
            if (Time.time >= _hostAlarmUntil)
            {
                _hostAlarmUntil = 0f;
                LastTeacherSpot.Clear();
                return;
            }
            if (Time.time < _nextSlipCheck)
            {
                return;
            }
            const float step = 0.5f;
            _nextSlipCheck = Time.time + step;
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null)
            {
                return;
            }
            foreach (Unit teacher in new List<Unit>(units.playerList))
            {
                if (teacher == null || !teacher.isActived || !teacher.IsTeacher)
                {
                    continue;
                }
                Vector3 now = teacher.Position;
                Vector3 before;
                if (LastTeacherSpot.TryGetValue(teacher.UID, out before) && !teacher.IsStun)
                {
                    Vector3 moved = now - before;
                    moved.y = 0f;
                    // Walking is safe. Sprinting on the wet floor is not.
                    if (moved.magnitude / step > SlipSpeed)
                    {
                        Poles.Slip(teacher);
                        Net.Toast(teacher, "The floor is wet from the sprinklers. Don't run!", 2.5f);
                    }
                }
                LastTeacherSpot[teacher.UID] = now;
            }
        }

        private static void PlaceBox()
        {
            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            UnitManager units = Game.Peek<UnitManager>();
            ClassroomItem room = classroom != null ? TeacherOffice.FindClassroom(classroom.currentFloor) : null;
            if (units == null || room == null)
            {
                return;
            }
            Vector3 centre = room.studentPos != null ? room.studentPos.position : room.transform.position;
            Unit best = null;
            float bestDistance = float.MaxValue;
            foreach (Unit unit in units.unitsDict.Values)
            {
                if (unit == null || !unit.isActived || unit.transform == null)
                {
                    continue;
                }
                if (unit.UnitSubType != EUnitSubType.ClassroomDoor && unit.UnitSubType != EUnitSubType.ClassroomBackDoor)
                {
                    continue;
                }
                Vector3 offset = unit.Position - centre;
                if (Mathf.Abs(offset.y) > 3.5f)
                {
                    continue;
                }
                float distance = new Vector2(offset.x, offset.z).magnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = unit;
                }
            }
            if (best == null)
            {
                Plugin.Log("fire alarm: no classroom door found on floor " + classroom.currentFloor);
                return;
            }
            // Above the door frame, where it can be seen from both sides.
            _hostBoxAt = new Vector3(best.Position.x, centre.y + 2.35f, best.Position.z);
            Net.SendToModded(new CpTell { kind = CpKind.AlarmBox, i = 1, v = _hostBoxAt });
            Plugin.Log("fire alarm: box at " + _hostBoxAt.ToString("F2"));
        }

        public static void OnAsk(Unit player)
        {
            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            if (!Plugin.Settings.fireAlarm || classroom == null || !Game.InClass || _pulled || !_placed || _hostBoxAt == Vector3.zero
                || !player.IsStudent || !NearBox(player.Position, _hostBoxAt, PullReach + 1.5f))
            {
                return;
            }
            _pulled = true;
            _hostAlarmUntil = Time.time + Seconds;
            _nextSlipCheck = 0f;
            LastTeacherSpot.Clear();
            Net.SendToModded(new CpTell { kind = CpKind.AlarmBox, i = 0, v = _hostBoxAt });
            Net.SendToModded(new CpTell { kind = CpKind.Alarm, f = Seconds, a = player.UID, s = Poles.NameOf(player) });
            classroom.AddTrickCount(player.UID, Mischief);
            Plugin.Log("fire alarm: pulled by " + Poles.NameOf(player));
        }

        // ------------------------------------------------------------ every player

        public static void OnTell(CpTell tell)
        {
            if (tell.kind == CpKind.AlarmBox)
            {
                _boxAt = tell.v;
                _boxKnown = true;
                _boxLive = tell.i == 1;
                return;
            }
            _alarmUntil = Time.time + Mathf.Clamp(tell.f, 1f, 60f);
            _nextSiren = 0f;
            Unit me = Game.LocalPlayer;
            bool teacher = me != null && me.IsTeacher;
            Game.Notify(teacher ? "FIRE ALARM! The floor is wet: walk, don't run." : "FIRE ALARM! " + tell.s + " pulled it. A running teacher slips.", 4f);
        }

        /// <summary>Red flash while the alarm runs, and the pull hint. Drawn in a 1280 by 720 frame.</summary>
        public static string DrawHud(float width, float height)
        {
            if (!Game.InClass)
            {
                return null;
            }
            if (Time.time < _alarmUntil)
            {
                if (_red == null)
                {
                    _red = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                    _red.SetPixel(0, 0, new Color(1f, 0.05f, 0.05f, 1f));
                    _red.Apply(false, true);
                }
                Color old = GUI.color;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
                GUI.color = new Color(1f, 1f, 1f, 0.05f + 0.13f * pulse);
                GUI.DrawTexture(new Rect(0f, 0f, width, height), _red);
                GUI.color = old;
            }
            Unit me = Game.LocalPlayer;
            if (me != null && me.IsStudent && _boxKnown && _boxLive && NearBox(me.Position, _boxAt, PullReach))
            {
                return "Press " + Plugin.Settings.UseKeyCode + " to pull the fire alarm";
            }
            return null;
        }
    }
}
