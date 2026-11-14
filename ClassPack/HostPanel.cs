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
        private const float Height = 420f;

        private bool _open;
        private float _length;
        private float _teachers;
        private float _targetPercent;
        private string _message = "";

        private readonly CursorGrab _cursor = new CursorGrab();

        private GUIStyle _label;
        private GUIStyle _title;
        private GUIStyle _button;
        private GUIStyle _toggle;
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
                bool solo = GUILayout.Toggle(Plugin.Settings.soloClass, "  Let a class start with only me and the bots", _toggle);
                if (solo != Plugin.Settings.soloClass)
                {
                    Plugin.Settings.soloClass = solo;
                    Plugin.Settings.Save();
                }
                bool always = GUILayout.Toggle(Plugin.Settings.nerfAlwaysForTesting, "  Everyone finds a nerf gun every class (for testing)", _toggle);
                if (always != Plugin.Settings.nerfAlwaysForTesting)
                {
                    Plugin.Settings.nerfAlwaysForTesting = always;
                    Plugin.Settings.Save();
                }
                GUI.enabled = true;

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

        private float Slider(string name, float value, RoomRuleData rule, float step, string shown)
        {
            float min, max;
            if (!Rules.TryGetSliderRange(rule, out min, out max))
            {
                return value;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, _label, GUILayout.Width(120f));
            float picked = max > min ? GUILayout.HorizontalSlider(Mathf.Clamp(value, min, max), min, max, GUILayout.Width(190f)) : min;
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
            _background = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _background.hideFlags = HideFlags.HideAndDontSave;
            _background.SetPixel(0, 0, new Color32(28, 31, 38, 242));
            _background.Apply(false, true);

            _box = new GUIStyle(GUI.skin.box);
            _box.normal.background = _background;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            _label.normal.textColor = new Color32(240, 243, 248, 255);
            _title = new GUIStyle(_label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _title.margin = new RectOffset(0, 0, 0, 10);
            _button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            _toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 16 };
            _toggle.normal.textColor = _label.normal.textColor;
            _toggle.onNormal.textColor = _label.normal.textColor;
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
