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
        // Chance that a gun turns up each time a student finishes a snack, and each time the
        // teacher eats a snack out of a student's drawer. 0.08 means about one snack in
        // twelve. Every gun has one shot.
        public float nerfChancePerSnack = 0.08f;
        public float nerfTeacherChancePerSnack = 0.05f;
        // For trying things out: every snack eaten gives a gun.
        public bool nerfAlwaysForTesting = false;
        public bool windowPoles = true;
        public bool prankCall = true;
        public bool fireAlarm = true;
        // The student tricks on the phone: paper airplane, water balloon, super glue, laser
        // pointer, whoopee cushion, passing notes and napping.
        public bool tricks = true;
        // The wardrobe (hair, clothes, colours, hats and other extras) and the new teacher
        // types. lookA and lookB are this player's own outfit, written by the wardrobe.
        public bool looks = true;
        public bool teachers = true;
        public string wardrobeKey = "F7";
        public int lookA = 0;
        public int lookB = 0;
        // Where hats sit compared with the game's own head point, and how big they are.
        // The wardrobe has sliders for these.
        // The game's head point is the neck. With the hair, a student's head is about 0.19 m
        // in radius, so its middle is that far above the neck and hats are 1.7 times the size
        // they were modelled for.
        public float headUp = 0.19f;
        public float headForward = 0.01f;
        public float headSize = 1.7f;
        public float bodyUp = 0f;
        // How far the back and the belly are from the middle of the body.
        public float bodyOut = 0.11f;
        // Raised whenever the fit numbers above get new defaults, so old saved ones are replaced.
        public int fitVersion = 0;

        // The how-to cards: shown the first time, and again with helpKey. seenTips lists
        // the cards this player has already been shown.
        // Swim class: picked by the room owner in the owner window. nextClass 0 is a normal
        // class, 1 a swim class. poolLight makes the pool hall's lamps brighter or dimmer.
        public bool swimClass = true;
        public int nextClass = 0;
        public float poolLight = 1f;

        public bool tips = true;
        public string helpKey = "F1";
        public string seenTips = "";
        // useKey takes or throws away a nerf gun and pulls the fire alarm. fireKey shoots
        // (a click on the mouse wheel shoots too). answerKey picks up a ringing phone.
        public string useKey = "B";
        public string fireKey = "X";
        public string answerKey = "H";

        // Start a class with only one player in the room (the classmates are bots)
        public bool soloClass = true;

        // The room owner's settings window
        public string panelKey = "F6";

        private const int CurrentFit = 3;

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
                if (settings.fitVersion < CurrentFit)
                {
                    var fresh = new Settings();
                    settings.headUp = fresh.headUp;
                    settings.headForward = fresh.headForward;
                    settings.headSize = fresh.headSize;
                    settings.bodyUp = fresh.bodyUp;
                    settings.bodyOut = fresh.bodyOut;
                    settings.fitVersion = CurrentFit;
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

        public KeyCode WardrobeKeyCode => ParseKey(wardrobeKey, KeyCode.F7);

        public KeyCode HelpKeyCode => ParseKey(helpKey, KeyCode.F1);

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
