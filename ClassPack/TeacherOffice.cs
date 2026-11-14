using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Works out a spot on the floor of the teacher's office on the floor where class is held.
    /// Runs on the host. The game has no "office" marker, so this reasons from two things it
    /// does have: the point where teachers are placed before class, and the office door.
    /// Every decision is written to the log so the spot can be tuned after a play test.
    /// </summary>
    internal static class TeacherOffice
    {
        private const float DropHeight = 0.7f;

        public static bool TryFind(out Vector3 spot, out string how)
        {
            spot = Vector3.zero;
            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            UnitManager units = Game.Peek<UnitManager>();
            if (classroom == null || units == null)
            {
                how = "game not ready";
                return false;
            }

            ClassroomItem room = FindClassroom(classroom.currentFloor);
            Vector3 anchor = room != null ? room.transform.position : (Game.LocalPlayer != null ? Game.LocalPlayer.Position : Vector3.zero);
            TeacherRoomDoorComponent door = FindNearestOfficeDoor(units, anchor);

            // 1. Teachers are placed somewhere before class. If that point is outside the
            //    classroom itself, it is the office they walk out of when the bell goes.
            if (room != null && room.teacherPos != null)
            {
                Vector3 start = room.teacherPos.position;
                if (!room.Contains(start))
                {
                    spot = start + Vector3.up * DropHeight;
                    how = "teacher start point, outside the classroom"
                        + (door != null ? ", " + Vector3.Distance(DoorCentre(door), start).ToString("F1") + "m from the office door" : ", no office door found");
                    return true;
                }
            }

            // 2. Otherwise go through the office door, to the side that is not the classroom.
            if (door != null)
            {
                Vector3 centre = DoorCentre(door);
                Vector3 normal = DoorNormal(door);
                float depth = Mathf.Clamp(Plugin.Settings.doorDashOfficeDepth, 0.5f, 8f);
                Vector3 a = centre + normal * depth;
                Vector3 b = centre - normal * depth;
                bool aInClass = room != null && room.Contains(a);
                bool bInClass = room != null && room.Contains(b);
                int side = Plugin.Settings.doorDashOfficeSide;
                string reason;
                if (side == 0)
                {
                    if (aInClass != bInClass)
                    {
                        side = aInClass ? -1 : 1;
                        reason = "the other side is the classroom";
                    }
                    else
                    {
                        side = 1;
                        reason = "guessed, both sides look the same";
                    }
                }
                else
                {
                    reason = "side set in config";
                }
                spot = (side > 0 ? a : b) + Vector3.up * DropHeight;
                how = "through the office door at " + centre.ToString("F2") + ", side " + side + " (" + reason + ")";
                return true;
            }

            how = "no office door and no teacher start point on floor " + classroom.currentFloor;
            return false;
        }

        private static ClassroomItem FindClassroom(int floor)
        {
            ClassroomConfig config = Object.FindFirstObjectByType<ClassroomConfig>();
            if (config == null || config.classroomTransList == null)
            {
                return null;
            }
            foreach (ClassroomItem item in config.classroomTransList)
            {
                if (item != null && item.floorIndex == floor)
                {
                    return item;
                }
            }
            return null;
        }

        private static TeacherRoomDoorComponent FindNearestOfficeDoor(UnitManager units, Vector3 anchor)
        {
            TeacherRoomDoorComponent best = null;
            float bestScore = float.MaxValue;
            foreach (Unit unit in units.unitsDict.Values)
            {
                if (unit == null || !unit.isActived || unit.transform == null)
                {
                    continue;
                }
                TeacherRoomDoorComponent door = unit.GetComponent<TeacherRoomDoorComponent>();
                if (door == null)
                {
                    continue;
                }
                Vector3 offset = unit.Position - anchor;
                // A door on another floor is a long way off in height; make that count heavily.
                float score = new Vector2(offset.x, offset.z).magnitude + Mathf.Abs(offset.y) * 6f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = door;
                }
            }
            return best;
        }

        /// <summary>The middle of the doorway: halfway between the two door leaves' hinges.</summary>
        private static Vector3 DoorCentre(TeacherRoomDoorComponent door)
        {
            if (door.door_l != null && door.door_r != null)
            {
                return (door.door_l.position + door.door_r.position) * 0.5f;
            }
            return door.owner.Position;
        }

        /// <summary>A flat direction pointing straight through the doorway.</summary>
        private static Vector3 DoorNormal(TeacherRoomDoorComponent door)
        {
            if (door.door_l != null && door.door_r != null)
            {
                Vector3 across = door.door_r.position - door.door_l.position;
                across.y = 0f;
                if (across.sqrMagnitude > 0.01f)
                {
                    return Vector3.Cross(Vector3.up, across.normalized).normalized;
                }
            }
            Vector3 forward = door.owner.transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
        }
    }
}
