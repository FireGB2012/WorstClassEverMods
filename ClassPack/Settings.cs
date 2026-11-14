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
        // The student's phone and its DoorDash app. doorDashKey pulls the phone up.
        public bool doorDash = true;
        public string doorDashKey = "O";
        public float doorDashWaitSeconds = 10f;
        public float doorDashCooldownSeconds = 45f;
        public float doorDashPoints = 15f;
        // Where the food lands when it has to go through the office door: how far past the
        // door, and which side (0 = work it out, 1 or -1 = force a side).
        public float doorDashOfficeDepth = 2.2f;
        public int doorDashOfficeSide = 0;

        // Teacher eats snacks out of student drawers for stamina
        public bool teacherSnacks = true;
        public string teacherSnackKey = "G";
        public float teacherSnackStamina = 15f;

        // Power cut in the middle of class
        public bool powerCut = true;
        public float powerCutSeconds = 25f;

        // Nerf guns, extra poles at the windows, prank calls from the phone, the fire alarm
        public bool nerfGuns = true;
        public bool windowPoles = true;
        public bool prankCall = true;
        public bool fireAlarm = true;
        // useKey takes or throws away a nerf gun and pulls the fire alarm. fireKey shoots
        // (a click on the mouse wheel shoots too). answerKey picks up a ringing phone.
        public string useKey = "B";
        public string fireKey = "X";
        public string answerKey = "H";

        // Start a class with only one player in the room (the classmates are bots)
        public bool soloClass = true;

        // The room owner's settings window
        public string panelKey = "F6";

        [NonSerialized]
        private string _path;

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
                settings._path = path;
                settings.Save();
            }
            catch (Exception ex)
            {
                Plugin.Log("could not read config.json, using defaults: " + ex.Message);
            }
            return settings;
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(_path))
            {
                return;
            }
            try
            {
                File.WriteAllText(_path, JsonUtility.ToJson(this, true));
            }
            catch (Exception ex)
            {
                Plugin.Log("could not save config.json: " + ex.Message);
            }
        }

        public KeyCode PanelKeyCode => ParseKey(panelKey, KeyCode.F6);

        public KeyCode UseKeyCode => ParseKey(useKey, KeyCode.B);

        public KeyCode FireKeyCode => ParseKey(fireKey, KeyCode.X);

        public KeyCode AnswerKeyCode => ParseKey(answerKey, KeyCode.H);

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
