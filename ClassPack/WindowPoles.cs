using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace ClassPack
{
    /// <summary>
    /// Idea 7: the windows are extra places to hang caught students. At the start of class the
    /// host stands an extra dunce pole at up to three windows of the classroom. They are the
    /// game's own poles, so the teacher hangs students on them and students rescue from them
    /// exactly as usual. They stand inside, right at the window: a pole outside the glass
    /// could leave a student stranded outside the building when they are let go.
    ///
    /// A student hung on a pole by a window is shown hanging OUTSIDE the window by their
    /// fingers (setting windowHanging): every player's game sees which poles stand at a window
    /// (Watch), hides such a pole while someone hangs on it, and the hanging pose itself is
    /// drawn by SwimPose. From inside only the fingers on the sill show.
    /// </summary>
    internal static class WindowPoles
    {
        private const int Most = 3;

        private static readonly List<ulong> Spawned = new List<ulong>();
        private static bool _doneThisClass;

        /// <summary>Where a student hung at a window hangs: the middle of the glass's lower edge, and the way out.</summary>
        public struct Hang
        {
            public Vector3 Sill;
            public Vector3 Out;
        }

        // Every player's game: who hangs at a window now, which poles stand at a window, which are hidden.
        private static readonly Dictionary<ulong, Hang> Hanging = new Dictionary<ulong, Hang>();
        private static readonly Dictionary<ulong, bool> PoleChecked = new Dictionary<ulong, bool>();
        private static readonly Dictionary<ulong, Hang> PoleWindow = new Dictionary<ulong, Hang>();
        private static readonly Dictionary<ulong, List<Renderer>> HiddenPoles = new Dictionary<ulong, List<Renderer>>();
        private static float _nextWatchAt;
        private static bool _loggedHang;

        /// <summary>True while somebody hangs out of a window.</summary>
        public static bool AnyHanging => Hanging.Count > 0;

        /// <summary>True, with where, while this student hangs out of a window.</summary>
        public static bool HangOf(ulong student, out Hang hang)
        {
            return Hanging.TryGetValue(student, out hang);
        }

        public static void Tick()
        {
            Watch();
            if (!NetworkServer.active)
            {
                Spawned.Clear();
                _doneThisClass = false;
                return;
            }
            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            if (classroom == null)
            {
                return;
            }
            if (classroom.curState == EClassroomState.Classing)
            {
                if (!_doneThisClass)
                {
                    _doneThisClass = true;
                    if (Plugin.Settings.windowPoles)
                    {
                        Spawn(classroom);
                    }
                }
            }
            else if (classroom.curState == EClassroomState.BeforeClass)
            {
                // Class is over and the game has let everyone go: take the extra poles away.
                _doneThisClass = false;
                if (Spawned.Count > 0)
                {
                    Remove();
                }
            }
        }

        private static void Spawn(ClassroomManager classroom)
        {
            UnitManager units = Game.Peek<UnitManager>();
            ClassroomItem room = TeacherOffice.FindClassroom(classroom.currentFloor);
            if (units == null || room == null)
            {
                return;
            }
            Vector3 centre = room.transform.position;
            Unit model = Poles.FindFreePole(centre);
            if (model == null || Mathf.Abs(model.Position.y - centre.y) > 4f)
            {
                Plugin.Log("window poles: no pole on this floor to copy");
                return;
            }

            var poles = new List<Vector3>();
            var windows = new List<Unit>();
            foreach (Unit unit in units.unitsDict.Values)
            {
                if (unit == null || !unit.isActived || unit.transform == null)
                {
                    continue;
                }
                if (unit.GetComponent<PunishPillarComponent>() != null)
                {
                    poles.Add(unit.Position);
                }
                else if (unit.GetComponent<WindowComponent>() != null)
                {
                    Vector3 offset = unit.Position - centre;
                    if (Mathf.Abs(offset.y) < 4f && new Vector2(offset.x, offset.z).magnitude < 30f)
                    {
                        windows.Add(unit);
                    }
                }
            }
            windows.Sort((a, b) => (a.Position - centre).sqrMagnitude.CompareTo((b.Position - centre).sqrMagnitude));

            int made = 0;
            foreach (Unit window in windows)
            {
                if (made >= Most)
                {
                    break;
                }
                Vector3 inward;
                if (!TryFindInside(room, window, out inward))
                {
                    continue;
                }
                Vector3 spot = window.Position + inward * 0.75f;
                spot.y = model.Position.y;
                bool crowded = false;
                foreach (Vector3 pole in poles)
                {
                    if ((pole - spot).sqrMagnitude < 1.6f * 1.6f)
                    {
                        crowded = true;
                        break;
                    }
                }
                if (crowded)
                {
                    continue;
                }
                float yaw = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg;
                UnitDataNet data = units.CreateUnitNetData(model.ID, spot, new Vector3(0f, yaw, 0f), null, 0uL, true);
                Unit pillar = units.CreateCommonUnit(data, true);
                if (pillar != null)
                {
                    Spawned.Add(pillar.UID);
                    poles.Add(spot);
                    made++;
                    Plugin.Log("window poles: pole at " + spot.ToString("F2"));
                }
            }
            if (made == 0)
            {
                Plugin.Log("window poles: " + windows.Count + " windows near the classroom, none usable");
            }
        }

        // ------------------------------------------------------------- hanging out of the window

        /// <summary>Every player's game: which students hang on a pole that stands at a window.</summary>
        private static void Watch()
        {
            if (!Plugin.Settings.windowHanging || !Game.InClass)
            {
                if (Hanging.Count > 0 || HiddenPoles.Count > 0 || PoleChecked.Count > 0)
                {
                    Hanging.Clear();
                    ShowAllPoles();
                    PoleChecked.Clear();
                    PoleWindow.Clear();
                }
                return;
            }
            if (Time.time < _nextWatchAt)
            {
                return;
            }
            _nextWatchAt = Time.time + 0.2f;
            UnitManager units = Game.Peek<UnitManager>();
            Hanging.Clear();
            if (units == null)
            {
                return;
            }
            foreach (Unit unit in units.unitsDict.Values)
            {
                PunishPillarComponent pole = unit != null && unit.isActived ? unit.GetComponent<PunishPillarComponent>() : null;
                if (pole == null)
                {
                    continue;
                }
                Hang hang;
                Unit student = pole.bindStudent;
                bool hung = WindowAt(units, unit, out hang) && student != null && student.isActived && student.IsStudent;
                if (hung)
                {
                    Hanging[student.UID] = hang;
                    if (!_loggedHang)
                    {
                        _loggedHang = true;
                        Plugin.Log("window poles: " + Poles.NameOf(student) + " hangs out of the window, sill at " + hang.Sill.ToString("F2")
                            + ", out " + hang.Out.ToString("F2"));
                    }
                }
                HidePole(unit, hung);
            }
        }

        /// <summary>The window a pole stands at (within a metre), worked out once per pole.</summary>
        private static bool WindowAt(UnitManager units, Unit pole, out Hang hang)
        {
            bool known;
            if (PoleChecked.TryGetValue(pole.UID, out known))
            {
                PoleWindow.TryGetValue(pole.UID, out hang);
                return known;
            }
            hang = new Hang();
            known = false;
            foreach (Unit unit in units.unitsDict.Values)
            {
                WindowComponent window = unit != null && unit.isActived ? unit.GetComponent<WindowComponent>() : null;
                if (window == null || window.Config == null || window.Config.rendererList == null)
                {
                    continue;
                }
                Vector3 offset = unit.Position - pole.Position;
                if (Mathf.Abs(offset.y) > 3f || new Vector2(offset.x, offset.z).magnitude > 1.1f)
                {
                    continue;
                }
                // The glass: its lower edge is the sill, its middle is in the wall.
                bool any = false;
                var glass = new Bounds();
                foreach (ClassroomWindowConfig.GlassRenderer pane in window.Config.rendererList)
                {
                    if (pane == null || pane.renderer == null)
                    {
                        continue;
                    }
                    if (!any)
                    {
                        glass = pane.renderer.bounds;
                        any = true;
                    }
                    else
                    {
                        glass.Encapsulate(pane.renderer.bounds);
                    }
                }
                if (!any)
                {
                    continue;
                }
                Vector3 outward = glass.center - pole.Position;
                outward.y = 0f;
                if (outward.sqrMagnitude < 0.01f)
                {
                    continue;
                }
                hang = new Hang { Sill = new Vector3(glass.center.x, glass.min.y, glass.center.z), Out = outward.normalized };
                known = true;
                break;
            }
            PoleChecked[pole.UID] = known;
            if (known)
            {
                PoleWindow[pole.UID] = hang;
            }
            return known;
        }

        /// <summary>The pole by the window is hidden while its student hangs outside.</summary>
        private static void HidePole(Unit pole, bool hide)
        {
            List<Renderer> hidden;
            bool isHidden = HiddenPoles.TryGetValue(pole.UID, out hidden);
            if (hide == isHidden || pole.model == null)
            {
                return;
            }
            if (hide)
            {
                hidden = new List<Renderer>();
                foreach (Renderer renderer in pole.model.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.forceRenderingOff)
                    {
                        renderer.forceRenderingOff = true;
                        hidden.Add(renderer);
                    }
                }
                HiddenPoles[pole.UID] = hidden;
            }
            else
            {
                foreach (Renderer renderer in hidden)
                {
                    if (renderer != null)
                    {
                        renderer.forceRenderingOff = false;
                    }
                }
                HiddenPoles.Remove(pole.UID);
            }
        }

        private static void ShowAllPoles()
        {
            foreach (List<Renderer> hidden in HiddenPoles.Values)
            {
                foreach (Renderer renderer in hidden)
                {
                    if (renderer != null)
                    {
                        renderer.forceRenderingOff = false;
                    }
                }
            }
            HiddenPoles.Clear();
        }

        /// <summary>
        /// Self-test (host): hangs this student on the first window pole, and says where to look
        /// from inside the room and from outside. False when there is no window pole.
        /// </summary>
        public static bool TestHang(Unit student, out Vector3 inside, out Vector3 outside, out Vector3 sill)
        {
            inside = outside = sill = Vector3.zero;
            UnitManager units = Game.Peek<UnitManager>();
            Unit pole = units != null && Spawned.Count > 0 ? units.GetUnitByUID(Spawned[0]) : null;
            Hang hang;
            RoomManager room = Game.Peek<RoomManager>();
            if (pole == null || room == null || student == null || !WindowAt(units, pole, out hang))
            {
                return false;
            }
            // Brought to the pole first, the way a teacher carries a student there. A bot walks
            // on the game's walkable area, so it is moved the way the dance class moves bots.
            Vector3 spot = pole.Position - hang.Out * 0.5f;
            var facing = new Vector3(0f, Mathf.Atan2(hang.Out.x, hang.Out.z) * Mathf.Rad2Deg, 0f);
            NavMeshAgent agent = student.model != null ? student.model.GetComponent<NavMeshAgent>() : null;
            if (agent != null)
            {
                agent.enabled = false;
            }
            student.SetPosition(spot);
            student.SetRotation(facing);
            if (agent != null)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh)
                {
                    agent.Warp(spot);
                }
            }
            ForceComponent force = student.GetComponent<ForceComponent>();
            if (force != null && force.rigidBody != null)
            {
                force.rigidBody.position = spot;
            }
            SyncTransformComponet sync = student.GetComponent<SyncTransformComponet>();
            if (sync != null)
            {
                sync.ApplyTransformImmediately(spot, facing);
                sync.ForceSendTransformSync();
            }
            if (!Poles.PutOnPole(student, false, pole))
            {
                return false;
            }
            sill = hang.Sill;
            inside = hang.Sill - hang.Out * 2.6f + Vector3.up * 0.5f;
            outside = hang.Sill + hang.Out * 2.6f - Vector3.up * 0.3f;
            return true;
        }

        /// <summary>Which way from the window leads into the classroom.</summary>
        private static bool TryFindInside(ClassroomItem room, Unit window, out Vector3 inward)
        {
            Vector3[] axes = { window.transform.forward, window.transform.right };
            foreach (Vector3 axis in axes)
            {
                Vector3 flat = new Vector3(axis.x, 0f, axis.z);
                if (flat.sqrMagnitude < 0.01f)
                {
                    continue;
                }
                flat.Normalize();
                bool ahead = room.Contains(window.Position + flat * 0.9f);
                bool behind = room.Contains(window.Position - flat * 0.9f);
                if (ahead != behind)
                {
                    inward = ahead ? flat : -flat;
                    return true;
                }
            }
            inward = Vector3.zero;
            return false;
        }

        private static void Remove()
        {
            UnitManager units = Game.Peek<UnitManager>();
            RoomManager room = Game.Peek<RoomManager>();
            if (units != null)
            {
                foreach (ulong uid in Spawned)
                {
                    Unit pillar = units.GetUnitByUID(uid);
                    if (pillar == null || !pillar.isActived)
                    {
                        continue;
                    }
                    PunishPillarComponent pole = pillar.GetComponent<PunishPillarComponent>();
                    if (pole != null && pole.bindStudent != null && room != null)
                    {
                        // Should not happen, the game frees everyone when class ends. If someone
                        // is still on it, let them go the game's way before the pole is removed.
                        room.GetSkillMessageHandler().OnStudentReleaseMessageRequest(null, new StudentReleaseMessageRequest_C2S
                        {
                            pillarUID = uid,
                            studentUID = pole.bindStudent.UID
                        });
                    }
                    pillar.SyncBack();
                }
            }
            Spawned.Clear();
        }
    }
}
