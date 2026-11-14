using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    // The game refuses to start a class with one player in two places: when the bell is rung,
    // and when it picks who teaches. These two patches let a lone host through both, so the
    // mod can be tried with only the computer-controlled classmates in the room.
    // The lone player keeps the role picked at the wardrobe: as a student there is no teacher,
    // as the teacher there are only bots to watch.

    [HarmonyPatch(typeof(ClassroomManager), nameof(ClassroomManager.SendStartClassMsg))]
    internal static class SoloBellPatch
    {
        private static bool Prefix(ClassroomManager __instance, ulong ringUID)
        {
            UnitManager units = Game.Peek<UnitManager>();
            if (!Plugin.Settings.soloClass || !NetworkServer.active || units == null || units.playerList.Count > 1)
            {
                return true;
            }
            __instance.ClassPrepare(ringUID);
            return false;
        }
    }

    [HarmonyPatch(typeof(ClassroomManager), "AssignClassTeacher")]
    internal static class SoloRolePatch
    {
        private static readonly MethodInfo Teleport = AccessTools.Method(typeof(ClassroomManager), "TeleportClassPlayer");

        private static bool Prefix(ClassroomManager __instance, List<Unit> players, Vector3 teacherPosition, ref bool __result)
        {
            if (!Plugin.Settings.soloClass || players == null || players.Count != 1 || players[0] == null)
            {
                return true;
            }
            Unit me = players[0];
            if (me.IsTeacher && Teleport != null)
            {
                // The game walks every teacher to the teacher's spot before class.
                Teleport.Invoke(__instance, new object[] { me, teacherPosition, Vector3.zero, 0 });
            }
            Plugin.Log("solo class: starting with one player as " + (me.IsTeacher ? "teacher" : "student"));
            __result = true;
            return false;
        }
    }
}
