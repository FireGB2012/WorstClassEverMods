using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Host-side helpers for the dunce poles: put a student straight onto a free pole, mark a
    /// student as "no rescue", and knock a player over the way a banana peel does.
    /// Everything here goes out through the game's own messages, so every player sees it.
    /// </summary>
    internal static class Poles
    {
        private static readonly HashSet<ulong> Locked = new HashSet<ulong>();

        public static void Tick()
        {
            if (!Game.InClass)
            {
                Locked.Clear();
            }
        }

        public static bool IsLocked(ulong studentUID)
        {
            return Locked.Contains(studentUID);
        }

        /// <summary>A student who is walking around or seated, not already carried or hung.</summary>
        public static bool CanBeHung(Unit student)
        {
            if (student == null || !student.isActived || student.IsDead || !student.IsStudent)
            {
                return false;
            }
            int state = student.CurState();
            return state != (int)EUnitFsmStateType.StudentBeCatched && state != (int)EUnitFsmStateType.StudentPunished;
        }

        public static Unit FindFreePole(Vector3 near)
        {
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null)
            {
                return null;
            }
            Unit best = null;
            float bestDistance = float.MaxValue;
            foreach (Unit unit in units.unitsDict.Values)
            {
                if (unit == null || !unit.isActived || unit.transform == null)
                {
                    continue;
                }
                PunishPillarComponent pole = unit.GetComponent<PunishPillarComponent>();
                if (pole == null || pole.bindStudent != null)
                {
                    continue;
                }
                Vector3 offset = unit.Position - near;
                // Never pick a pole on another floor.
                float distance = new Vector2(offset.x, offset.z).magnitude + Mathf.Abs(offset.y) * 20f;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = unit;
                }
            }
            return best;
        }

        /// <summary>
        /// Host: hang a student on the nearest free pole. With locked set, other students
        /// can't rescue them for the rest of the class. Returns false when no pole is free.
        /// </summary>
        public static bool PutOnPole(Unit student, bool locked)
        {
            RoomManager room = Game.Peek<RoomManager>();
            if (!NetworkServer.active || room == null || !CanBeHung(student))
            {
                return false;
            }
            Unit poleUnit = FindFreePole(student.Position);
            PunishPillarComponent pole = poleUnit != null ? poleUnit.GetComponent<PunishPillarComponent>() : null;
            if (pole == null)
            {
                return false;
            }
            // This is the message the game sends when a teacher hangs a student. The game needs
            // some unit in the "teacher" slot and gives that unit a few teacher-only side
            // effects; the pole itself is a unit those side effects do nothing to.
            var response = new TeacherBindStudentMessageResponse_S2C
            {
                teacherUID = poleUnit.UID,
                pillarUID = poleUnit.UID,
                studentUID = student.UID,
                pillarBindingVersion = pole.BindingVersion + 1,
                recoveredEnergy = 0
            };
            room.GetSkillMessageHandler().OnTeacherBindStudentMessageResponse(response);
            room.BroadcastToAllExceptHost(response);
            if (locked)
            {
                Locked.Add(student.UID);
            }
            return true;
        }

        /// <summary>Host: knock a teacher or student over, exactly like stepping on a banana peel.</summary>
        public static void Slip(Unit player)
        {
            RoomManager room = Game.Peek<RoomManager>();
            if (!NetworkServer.active || room == null || player == null || !player.isActived)
            {
                return;
            }
            var slip = new BananaSlip_S2C { playerUID = player.UID };
            BananaComponent.ApplySlip(slip);
            room.BroadcastToAllExceptHost(slip);
        }

        public static string NameOf(Unit player)
        {
            RoleData data = player != null ? player.GetData<RoleData>() : null;
            string name = data != null ? data.NetData.Name : null;
            return string.IsNullOrEmpty(name) ? "Someone" : name;
        }
    }

    /// <summary>
    /// A student hung by a nerf dart can't be rescued. Rescues all pass through this one
    /// host-side method. The game's own releases (end of class) use rescuer 0 and still work.
    /// </summary>
    [HarmonyPatch(typeof(SkillMessageHandler), "ReleaseStudentOnServer")]
    internal static class NoRescuePatch
    {
        private static bool Prefix(ulong rescuerUID, StudentReleaseMessageRequest_C2S request, ref bool __result)
        {
            if (rescuerUID == 0uL || request.pillarUID == 0uL || !Game.InClass || !Poles.IsLocked(request.studentUID))
            {
                return true;
            }
            UnitManager units = Game.Peek<UnitManager>();
            Unit rescuer = units != null ? units.GetUnitByUID(rescuerUID) : null;
            if (rescuer != null)
            {
                Net.Toast(rescuer, "Nerf darts stick. This one can't be rescued.", 2.5f);
            }
            __result = false;
            return false;
        }
    }
}
