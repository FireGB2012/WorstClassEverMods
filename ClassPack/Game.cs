using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>Small helpers for reading the game's state without disturbing it.</summary>
    internal static class Game
    {
        private static class Holder<T> where T : MonoSingletonT<T>
        {
            public static readonly FieldInfo Field =
                typeof(MonoSingletonT<T>).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        }

        private static readonly Regex HostHasModPattern =
            new Regex("\"id\"\\s*:\\s*\"" + Regex.Escape(Plugin.ModId) + "\"", RegexOptions.CultureInvariant);

        /// <summary>The list of gameplay mods the room's host sent us, or null if none arrived yet.</summary>
        public static string HostManifest;

        /// <summary>
        /// Reads a game manager without creating it. The game's own Instance getter builds
        /// the manager on first use, which a mod must never trigger on the menu screen.
        /// </summary>
        public static T Peek<T>() where T : MonoSingletonT<T>
        {
            var value = Holder<T>.Field?.GetValue(null) as T;
            return (Object)value != null ? value : null;
        }

        public static bool IsHost => NetworkServer.active;

        /// <summary>
        /// True when this room should run the mod's gameplay: we host it, or the host has the
        /// mod too. Keeps the mod from giving an edge in rooms that don't use it.
        /// </summary>
        public static bool RoomHasMod
        {
            get
            {
                if (NetworkServer.active)
                {
                    return true;
                }
                return NetworkClient.active && HostManifest != null && HostHasModPattern.IsMatch(HostManifest);
            }
        }

        public static Unit LocalPlayer
        {
            get
            {
                var units = Peek<UnitManager>();
                return units != null ? units.localPlayer : null;
            }
        }

        public static bool InClass
        {
            get
            {
                var classroom = Peek<ClassroomManager>();
                return classroom != null && classroom.curState == EClassroomState.Classing;
            }
        }

        public static void Tick()
        {
            if (!NetworkClient.active && !NetworkServer.active)
            {
                HostManifest = null;
            }
        }

        /// <summary>Shows one of the game's own pop-up notices to the local player.</summary>
        public static void Notify(string text, float seconds = 3.5f)
        {
            var notify = Peek<GameNotifyManager>();
            if (notify != null)
            {
                notify.ShowCommonNotify(text, null, seconds);
            }
        }

        public static void PlaySound(AudioEvents sound)
        {
            var audio = Peek<AudioManager>();
            if (audio != null)
            {
                audio.PlayOneShot(sound);
            }
        }

        /// <summary>True while the player is typing into a text box, so hotkeys stay quiet.</summary>
        public static bool IsTyping
        {
            get
            {
                var events = UnityEngine.EventSystems.EventSystem.current;
                var selected = events != null ? events.currentSelectedGameObject : null;
                if (selected == null)
                {
                    return false;
                }
                return selected.GetComponent<UnityEngine.UI.InputField>() != null
                    || selected.GetComponent<TMPro.TMP_InputField>() != null;
            }
        }
    }

    /// <summary>Remembers which gameplay mods the host runs, sent when we join a room.</summary>
    [HarmonyPatch(typeof(RoomSettingsMessageHandler), "OnRoomRuleSettingsResponse")]
    internal static class HostManifestPatch
    {
        private static void Postfix(RoomRuleSettings_S2C response)
        {
            if (response.NetworkModManifest != null)
            {
                Game.HostManifest = response.NetworkModManifest;
            }
        }
    }
}
