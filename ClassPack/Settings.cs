using System;
using System.IO;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Player-editable settings, saved as config.json in the mod's config folder.
    /// Missing fields keep the defaults below, so new settings can be added later.
    /// </summary>
    [Serializable]
    public sealed class Settings
    {
        // DoorDash on the computers
        public bool doorDash = true;
        public string doorDashKey = "O";
        public float doorDashWaitSeconds = 10f;
        public float doorDashCooldownSeconds = 45f;

        // Teacher eats snacks out of student drawers for stamina
        public bool teacherSnacks = true;
        public string teacherSnackKey = "G";
        public float teacherSnackStamina = 15f;

        // Power cut in the middle of class
        public bool powerCut = true;
        public float powerCutSeconds = 25f;

        public static Settings Load(string configDirectory)
        {
            var settings = new Settings();
            try
            {
                string path = Path.Combine(configDirectory, "config.json");
                if (File.Exists(path))
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(path), settings);
                }
                File.WriteAllText(path, JsonUtility.ToJson(settings, true));
            }
            catch (Exception ex)
            {
                Plugin.Log("could not read config.json, using defaults: " + ex.Message);
            }
            return settings;
        }

        public KeyCode DoorDashKeyCode => ParseKey(doorDashKey, KeyCode.O);

        public KeyCode TeacherSnackKeyCode => ParseKey(teacherSnackKey, KeyCode.G);

        private static KeyCode ParseKey(string name, KeyCode fallback)
        {
            try
            {
                return (KeyCode)Enum.Parse(typeof(KeyCode), name, true);
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}
