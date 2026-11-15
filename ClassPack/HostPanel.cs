using System;
using System.Collections.Generic;
using System.Globalization;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// A small settings window for the room owner: class length, number of teachers and
    /// points to win. It writes to the game's own room rules, the same ones the game's
    /// room settings screen edits, so every player gets the change the normal way.
    /// </summary>
    internal sealed class HostPanel : MonoBehaviour
    {
        private const float Width = 520f;
        private const float Height = 540f;

        private bool _open;
        private float _length;

        /// <summary>The one window, for the self-test's picture of it.</summary>
        public static HostPanel Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>Self-test: open or close the window to take a picture of it.</summary>
        public void TestShow(bool show)
        {
            if (show && !_open && InRoom)
            {
                Open();
            }
            else if (!show && _open)
            {
                Close();
            }
        }
        private float _teachers;
        private float _targetPercent;
        private string _message = "";
        private bool _lightChanged;

        private readonly CursorGrab _cursor = new CursorGrab();

        private GUIStyle _label;
        private GUIStyle _title;
        private GUIStyle _button;
        private GUIStyle _toggle;
        private GUIStyle _pick;
        private GUIStyle _note;
        private GUIStyle _box;
        private Texture2D _background;

        private static RoomRuleDataManager Rules => SingletonT<RoomRuleDataManager>.Instance;

        private static bool InRoom => NetworkClient.active || NetworkServer.active;

        private void Update()
        {
            if (_open && !InRoom)
            {
                Close();
            }
            if (Input.GetKeyDown(Plugin.Settings.PanelKeyCode) && !Game.IsTyping)
            {
                if (_open)
                {
                    Close();
                }
                else if (InRoom)
                {
                    Open();
                }
            }
        }

        private void LateUpdate()
        {
            if (_open)
            {
                _cursor.Hold();
            }
        }

        private void OnDisable()
        {
            Close();
        }

        private void Open()
        {
            // Only one of the mod's windows can have the mouse at a time.
            if (!_cursor.Begin())
            {
                return;
            }
            LoadCurrentValues();
            _message = "";
            _open = true;
        }

        private void Close()
        {
            if (_lightChanged)
            {
                _lightChanged = false;
                Plugin.Settings.Save();
            }
            _open = false;
            _cursor.End();
        }

        private void LoadCurrentValues()
        {
            _length = ReadRule(ERoomRuleId.ClassDuration, 480f);
            _teachers = ReadRule(ERoomRuleId.ClassTeacherCount, 1f);
            _targetPercent = ReadRule(ERoomRuleId.ClassWZDDD, 100f);
        }

        private static float ReadRule(ERoomRuleId rule, float fallback)
        {
            float value;
            string text = Rules.GetValueForTab(rule, Rules.CurrentTab);
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private void OnGUI()
        {
            if (!_open)
            {
                return;
            }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            if (scale <= 0f)
            {
                return;
            }
            Matrix4x4 oldMatrix = GUI.matrix;
            int oldDepth = GUI.depth;
            try
            {
                EnsureStyles();
                GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
                GUI.depth = -9000;
                var area = new Rect((Screen.width / scale - Width) * 0.5f, (Screen.height / scale - Height) * 0.5f, Width, Height);
                GUI.Box(area, GUIContent.none, _box);
                GUILayout.BeginArea(new Rect(area.x + 20f, area.y + 14f, area.width - 40f, area.height - 28f));
                Draw();
                GUILayout.EndArea();
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.depth = oldDepth;
            }
        }

        private void Draw()
        {
            GUILayout.Label("Class Pack: room settings", _title);

            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            bool isHost = NetworkServer.active;
            bool betweenClasses = classroom != null && classroom.curState == EClassroomState.BeforeClass;
            RoomRuleData lengthRule, teacherRule, targetRule;
            bool rulesReady = Rules.TryGetRuleData(ERoomRuleId.ClassDuration, out lengthRule)
                & Rules.TryGetRuleData(ERoomRuleId.ClassTeacherCount, out teacherRule)
                & Rules.TryGetRuleData(ERoomRuleId.ClassWZDDD, out targetRule);

            if (!rulesReady)
            {
                GUILayout.Label("The room's rules are not loaded yet. Try again in a moment.", _label);
            }
            else
            {
                bool canEdit = isHost && betweenClasses;
                if (!isHost)
                {
                    GUILayout.Label("Only the room owner can change these.", _label);
                }
                else if (!betweenClasses)
                {
                    GUILayout.Label("These can be changed between classes, not during one.", _label);
                }
                GUI.enabled = canEdit;

                _length = Slider("Class length", _length, lengthRule, 10f, FormatTime(_length));
                _teachers = Slider("Teachers", _teachers, teacherRule, 1f, Mathf.RoundToInt(_teachers).ToString());
                _targetPercent = Slider("Points to win", _targetPercent, targetRule, 5f, DescribeTarget());

                GUILayout.Space(6f);
                bool solo = GUILayout.Toggle(Plugin.Settings.soloClass, "Let a class start with only me and the bots", _toggle);
                if (solo != Plugin.Settings.soloClass)
                {
                    Plugin.Settings.soloClass = solo;
                    Plugin.Settings.Save();
                }
                bool always = GUILayout.Toggle(Plugin.Settings.nerfAlwaysForTesting, "Every snack eaten gives a nerf gun and a phone (testing)", _toggle);
                if (always != Plugin.Settings.nerfAlwaysForTesting)
                {
                    Plugin.Settings.nerfAlwaysForTesting = always;
                    Plugin.Settings.Save();
                }
                bool copy = GUILayout.Toggle(Plugin.Settings.botsCopyPlayers, "Bots dress exactly like us (each copies one player)", _toggle);
                if (copy != Plugin.Settings.botsCopyPlayers)
                {
                    Plugin.Settings.botsCopyPlayers = copy;
                    Plugin.Settings.Save();
                }
                GUI.enabled = true;

                DrawClassPicker(isHost);

                GUILayout.Space(8f);
                GUILayout.BeginHorizontal();
                GUI.enabled = canEdit;
                if (GUILayout.Button("Save", _button, GUILayout.Height(34f)))
                {
                    Save(lengthRule, teacherRule, targetRule);
                }
                GUI.enabled = true;
                if (GUILayout.Button("Close", _button, GUILayout.Height(34f)))
                {
                    Close();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4f);
            GUILayout.Label(_message.Length > 0 ? _message : "Press " + Plugin.Settings.PanelKeyCode + " to open or close this window.", _label);
        }

        /// <summary>
        /// Which kind of class the next bell starts, for the room owner. The lamp slider under
        /// it is each player's own, so it also shows for a guest while a swim class runs.
        /// </summary>
        private void DrawClassPicker(bool isHost)
        {
            Settings settings = Plugin.Settings;
            if (!settings.swimClass)
            {
                return;
            }
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Next class", _label, GUILayout.Width(120f));
            GUI.enabled = isHost;
            for (int i = 0; i < Swim.ClassNames.Length; i++)
            {
                bool picked = settings.nextClass == i;
                if (GUILayout.Toggle(picked, Swim.ClassNames[i], _pick, GUILayout.Width(112f)) && !picked)
                {
                    settings.nextClass = i;
                    settings.Save();
                    _message = i == Swim.SwimClass ? "The next bell starts a swim class in the pool hall."
                        : i == Dance.DanceClass ? "The next bell starts a dance class in the dance hall."
                        : "The next bell starts a normal class.";
                    Plugin.Log("next class picked in the panel: " + Swim.ClassNames[i]);
                }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            bool swim = isHost && settings.nextClass == Swim.SwimClass;
            if (swim)
            {
                GUILayout.Label("At the bell everyone who runs Class Pack goes to the pool hall, and the bots come along.", _note);
            }
            if (swim || Swim.Active)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Pool lamps", _label, GUILayout.Width(120f));
                float light = GUILayout.HorizontalSlider(Mathf.Clamp(settings.poolLight, 0.2f, 3f), 0.2f, 3f, Skin.Track, Skin.Thumb, GUILayout.Width(190f));
                light = Mathf.Round(light * 10f) / 10f;
                GUILayout.Space(12f);
                GUILayout.Label(light < 0.95f ? "dimmer" : light > 1.05f ? "brighter" : "normal", _label);
                GUILayout.EndHorizontal();
                if (!Mathf.Approximately(light, settings.poolLight))
                {
                    settings.poolLight = light;
                    _lightChanged = true;
                }
                bool behind = GUILayout.Toggle(settings.swimSelfView, "Show me from behind while I swim, so I see myself", _toggle);
                if (behind != settings.swimSelfView)
                {
                    settings.swimSelfView = behind;
                    _lightChanged = true;
                }
                if (isHost)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Pee mischief", _label, GUILayout.Width(120f));
                    float pee = GUILayout.HorizontalSlider(Mathf.Clamp(settings.peePoints, 1f, 10f), 1f, 10f, Skin.Track, Skin.Thumb, GUILayout.Width(190f));
                    pee = Mathf.Round(pee);
                    GUILayout.Space(12f);
                    GUILayout.Label("+" + pee, _label);
                    GUILayout.EndHorizontal();
                    if (!Mathf.Approximately(pee, settings.peePoints))
                    {
                        settings.peePoints = pee;
                        _lightChanged = true;
                    }
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Pee wait", _label, GUILayout.Width(120f));
                    float wait = GUILayout.HorizontalSlider(Mathf.Clamp(settings.peeWaitSeconds, 10f, 60f), 10f, 60f, Skin.Track, Skin.Thumb, GUILayout.Width(190f));
                    wait = Mathf.Round(wait / 5f) * 5f;
                    GUILayout.Space(12f);
                    GUILayout.Label(wait + " s", _label);
                    GUILayout.EndHorizontal();
                    if (!Mathf.Approximately(wait, settings.peeWaitSeconds))
                    {
                        settings.peeWaitSeconds = wait;
                        _lightChanged = true;
                    }
                    GUILayout.Space(6f);
                    GUILayout.Label("Peeing in the pool pays this, double next to a teacher in the water, then the student waits.", _note);
                    bool oldWays = GUILayout.Toggle(settings.poolOldMischief, "Diving, splashing and diving under pay mischief too", _toggle);
                    if (oldWays != settings.poolOldMischief)
                    {
                        settings.poolOldMischief = oldWays;
                        _lightChanged = true;
                    }
                }
            }
            bool dance = isHost && settings.nextClass == Dance.DanceClass;
            if (dance)
            {
                GUILayout.Label("At the bell everyone who runs Class Pack goes to the dance hall, and the bots come along.", _note);
            }
            if (isHost && (dance || Dance.Active))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Worm pays", _label, GUILayout.Width(120f));
                float points = GUILayout.HorizontalSlider(Mathf.Clamp(settings.dancePoints, 1f, 5f), 1f, 5f, Skin.Track, Skin.Thumb, GUILayout.Width(190f));
                points = Mathf.Round(points);
                GUILayout.Space(12f);
                GUILayout.Label("+" + points, _label);
                GUILayout.EndHorizontal();
                if (!Mathf.Approximately(points, settings.dancePoints))
                {
                    settings.dancePoints = points;
                    _lightChanged = true;
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label("Worm every", _label, GUILayout.Width(120f));
                float every = GUILayout.HorizontalSlider(Mathf.Clamp(settings.danceSeconds, 3f, 15f), 3f, 15f, Skin.Track, Skin.Thumb, GUILayout.Width(190f));
                every = Mathf.Round(every);
                GUILayout.Space(12f);
                GUILayout.Label(every + " s", _label);
                GUILayout.EndHorizontal();
                if (!Mathf.Approximately(every, settings.danceSeconds))
                {
                    settings.danceSeconds = every;
                    _lightChanged = true;
                }
                GUILayout.Space(6f);
                GUILayout.Label("Everyone has to dance. A student doing the worm earns this, again and again while they keep it up.", _note);
            }
        }

        private float Slider(string name, float value, RoomRuleData rule, float step, string shown)
        {
            float min, max;
            if (!Rules.TryGetSliderRange(rule, out min, out max))
            {
                return value;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, _label, GUILayout.Width(120f));
            float picked = max > min ? GUILayout.HorizontalSlider(Mathf.Clamp(value, min, max), min, max, Skin.Track, Skin.Thumb, GUILayout.Width(190f)) : min;
            GUILayout.Space(12f);
            GUILayout.Label(shown, _label);
            GUILayout.EndHorizontal();
            picked = Mathf.Round(picked / step) * step;
            return Mathf.Clamp(picked, min, max);
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.RoundToInt(seconds);
            return (total / 60) + ":" + (total % 60).ToString("00") + " min";
        }

        /// <summary>The game stores the target as a percentage of its normal target for the
        /// number of students, so show what that comes to in points.</summary>
        private string DescribeTarget()
        {
            UnitManager units = Game.Peek<UnitManager>();
            int players = units != null ? units.playerList.Count : 1;
            int students = players <= 1 ? 1 : Mathf.Max(1, players - Mathf.RoundToInt(_teachers));
            cfg.StudentNumBalance balance = StudentNumBalanceConfig.GetForStudentCount(students);
            float normal = balance != null ? Convert.ToSingle(balance.StudentTargetScore) : 0f;
            int points = Mathf.RoundToInt(normal * _targetPercent / 100f);
            return points + " points (" + Mathf.RoundToInt(_targetPercent) + "% for " + students + (students == 1 ? " student)" : " students)");
        }

        private void Save(RoomRuleData lengthRule, RoomRuleData teacherRule, RoomRuleData targetRule)
        {
            RoomRuleDataManager rules = Rules;
            rules.DiscardDraftValues();
            SetDraft(rules, lengthRule, _length);
            SetDraft(rules, teacherRule, _teachers);
            SetDraft(rules, targetRule, _targetPercent);

            List<RoomRuleValue> values;
            if (!rules.TrySaveRules(ERoomRuleTab.Custom, out values) || values == null)
            {
                rules.DiscardDraftValues();
                _message = "Could not save. Are you the room owner, between classes?";
                return;
            }
            RoomManager room = Game.Peek<RoomManager>();
            if (room != null)
            {
                room.BroadcastToAllExceptHost(new RoomRuleSettings_S2C
                {
                    NotifyRulesChanged = true,
                    IsCustomRules = rules.CurrentTab == ERoomRuleTab.Custom,
                    RuleValues = values
                });
            }
            LoadCurrentValues();
            _message = "Saved: " + FormatTime(_length) + ", " + Mathf.RoundToInt(_teachers) + " teacher(s), " + DescribeTarget();
            Plugin.Log("room settings saved from the panel: length " + _length + ", teachers " + _teachers + ", target " + _targetPercent + "%");
        }

        private static void SetDraft(RoomRuleDataManager rules, RoomRuleData rule, float wanted)
        {
            float value;
            if (rules.TryNormalizeSliderValue(rule, wanted, out value))
            {
                rules.SetDraftValue(rule.Category, rule.RuleId, 0, value.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        private void EnsureStyles()
        {
            if (_label != null)
            {
                return;
            }
            Skin.Ensure();
            _box = Skin.Panel;
            _label = new GUIStyle(Skin.Label) { fontSize = 16 };
            _title = new GUIStyle(Skin.Label) { fontSize = 24 };
            _title.normal.textColor = Skin.Orange;
            _title.margin = new RectOffset(0, 0, 0, 10);
            _button = Skin.GreenButton;
            _toggle = new GUIStyle(Skin.Cell) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            _toggle.padding = new RectOffset(12, 12, 2, 2);
            _pick = new GUIStyle(Skin.Cell) { fontSize = 15 };
            _note = new GUIStyle(Skin.Small) { fontSize = 14, wordWrap = true };
        }

        private void OnDestroy()
        {
            Close();
            if (_background != null)
            {
                Destroy(_background);
            }
        }
    }
}
