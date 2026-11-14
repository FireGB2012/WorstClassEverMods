using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Idea 7: the windows are extra places to hang caught students. At the start of class the
    /// host stands an extra dunce pole at up to three windows of the classroom. They are the
    /// game's own poles, so the teacher hangs students on them and students rescue from them
    /// exactly as usual. They stand inside, right at the window: a pole outside the glass
    /// could leave a student stranded outside the building when they are let go.
    /// </summary>
    internal static class WindowPoles
    {
        private const int Most = 3;

        private static readonly List<ulong> Spawned = new List<ulong>();
        private static bool _doneThisClass;

        public static void Tick()
        {
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
