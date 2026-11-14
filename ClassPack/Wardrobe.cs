using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// The wardrobe window: pick hair, body, clothes, colours, a hat and other extras, and the
    /// teacher type. Opened with a key or from the phone. Because the game never shows a
    /// player their own body, the window has a small picture of the player taken by its own
    /// camera, so every change can be seen straight away.
    /// </summary>
    internal sealed class Wardrobe : MonoBehaviour
    {
        private const float Width = 900f;
        private const float Height = 620f;
        private const int PictureWidth = 300;
        private const int PictureHeight = 460;

        public static bool IsOpen { get; private set; }

        private static readonly string[] TeacherHelp =
        {
            "Normal: the game's own teacher, plus eating snacks from drawers.",
            "Gym teacher: bald on top with a big belly. X throws a slice of pizza that knocks a student over.",
            "Lunch lady: X throws mashed potatoes that stick a student in place. No blackboard: hold B at her pot for stamina.",
            "Janitor: X mops. That clears banana peels, glue and whoopee cushions and leaves a wet floor students slip on.",
            "Bald with a wig: a student behind you can pull the wig off. No chalk until you walk over it and get it back."
        };

        private static bool _openRequested;

        private readonly CursorGrab _cursor = new CursorGrab();
        private Camera _camera;
        private RenderTexture _picture;
        private float _turn;
        private bool _fitChanged;
        private Vector2 _scroll;
        private Texture2D _panel, _card, _cardOn, _cardHover;
        private GUIStyle _panelStyle, _title, _label, _small, _cell, _button;

        /// <summary>Asks for the window from somewhere else, such as the phone.</summary>
        public static void RequestOpen()
        {
            _openRequested = true;
        }

        private static bool InRoom => NetworkClient.active || NetworkServer.active;

        private void Update()
        {
            Unit me = Game.LocalPlayer;
            if (IsOpen && (!InRoom || me == null))
            {
                Close();
            }
            bool key = Input.GetKeyDown(Plugin.Settings.WardrobeKeyCode) && !Game.IsTyping;
            if (IsOpen && (key || Input.GetKeyDown(KeyCode.Escape)))
            {
                Close();
            }
            else if (!IsOpen && (key || _openRequested) && Plugin.Settings.looks && InRoom && me != null && _cursor.Begin())
            {
                Open();
            }
            _openRequested = false;
        }

        private void Open()
        {
            IsOpen = true;
            _turn = 0f;
            int layer = LayerMask.NameToLayer("PlayerSelf");
            if (layer >= 0)
            {
                _picture = new RenderTexture(PictureWidth * 2, PictureHeight * 2, 16) { hideFlags = HideFlags.HideAndDontSave };
                var holder = new GameObject("[ClassPack wardrobe camera]");
                DontDestroyOnLoad(holder);
                _camera = holder.AddComponent<Camera>();
                _camera.cullingMask = 1 << layer;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.17f, 0.19f, 0.25f, 1f);
                _camera.fieldOfView = 30f;
                _camera.nearClipPlane = 0.1f;
                _camera.farClipPlane = 15f;
                _camera.depth = -60f;
                _camera.allowHDR = false;
                _camera.targetTexture = _picture;
            }
        }

        private void Close()
        {
            IsOpen = false;
            _cursor.End();
            if (_fitChanged)
            {
                _fitChanged = false;
                Plugin.Settings.Save();
            }
            if (_camera != null)
            {
                _camera.targetTexture = null;
                Destroy(_camera.gameObject);
                _camera = null;
            }
            if (_picture != null)
            {
                _picture.Release();
                Destroy(_picture);
                _picture = null;
            }
        }

        private void LateUpdate()
        {
            if (!IsOpen)
            {
                return;
            }
            _cursor.Hold();
            Unit me = Game.LocalPlayer;
            if (_camera == null || me == null || me.transform == null)
            {
                return;
            }
            // Look at the player from the front, a bit below the head, and turn with the slider.
            Transform head = me.GetPartRoot(EUnitPartType.HeadPosition);
            Vector3 headAt = head != null ? head.position : me.transform.position + Vector3.up * 1.55f;
            Vector3 feet = me.transform.position;
            Vector3 middle = new Vector3(headAt.x, Mathf.Lerp(feet.y, headAt.y, 0.58f), headAt.z);
            float tall = Mathf.Max(0.8f, headAt.y - feet.y + 0.45f);
            Quaternion around = Quaternion.Euler(0f, me.transform.eulerAngles.y + _turn, 0f);
            float back = tall * 0.5f / Mathf.Tan(15f * Mathf.Deg2Rad) + 0.4f;
            _camera.transform.position = middle + around * Vector3.forward * back + Vector3.up * 0.15f;
            _camera.transform.LookAt(middle);
        }

        private void OnDisable()
        {
            Close();
        }

        private void OnDestroy()
        {
            Close();
            foreach (Texture2D texture in new[] { _panel, _card, _cardOn, _cardHover })
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }
        }

        private int Row(string label, int value, string[] names, int perRow)
        {
            GUILayout.Label(label, _label);
            return GUILayout.SelectionGrid(Mathf.Clamp(value, 0, names.Length - 1), names, perRow, _cell);
        }

        /// <summary>A row where every item can be switched on by itself. names[0] is "None" and is skipped.</summary>
        private int Several(string label, int worn, string[] names)
        {
            GUILayout.Label(label, _label);
            GUILayout.BeginHorizontal();
            for (int i = 1; i < names.Length; i++)
            {
                int bit = 1 << (i - 1);
                bool on = GUILayout.Toggle((worn & bit) != 0, names[i], _cell, GUILayout.Width(120f));
                worn = on ? worn | bit : worn & ~bit;
            }
            if (GUILayout.Button("Take all off", _cell, GUILayout.Width(110f)))
            {
                worn = 0;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return worn;
        }

        private float Slide(string label, float value, float low, float high)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + "  " + value.ToString("F2"), _small, GUILayout.Width(190f));
            float result = GUILayout.HorizontalSlider(value, low, high, GUILayout.Height(18f));
            GUILayout.EndHorizontal();
            return result;
        }

        private void OnGUI()
        {
            if (!IsOpen)
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
                GUI.depth = -9100;
                float width = Screen.width / scale;
                float height = Screen.height / scale;
                var window = new Rect((width - Width) * 0.5f, (height - Height) * 0.5f, Width, Height);
                GUI.Box(window, GUIContent.none, _panelStyle);
                GUI.Label(new Rect(window.x + 20f, window.y + 10f, 400f, 34f), "Wardrobe", _title);
                if (GUI.Button(new Rect(window.xMax - 150f, window.y + 12f, 130f, 30f), "Close (" + Plugin.Settings.WardrobeKeyCode + ")", _button))
                {
                    Close();
                    return;
                }

                var picture = new Rect(window.x + 20f, window.y + 56f, PictureWidth, PictureHeight);
                if (_picture != null)
                {
                    GUI.DrawTexture(picture, _picture, ScaleMode.ScaleToFit, false);
                }
                else
                {
                    GUI.Label(picture, "No picture here, but everybody else sees your outfit.", _small);
                }
                GUI.Label(new Rect(picture.x, picture.yMax + 8f, 60f, 20f), "Turn", _small);
                _turn = GUI.HorizontalSlider(new Rect(picture.x + 50f, picture.yMax + 12f, PictureWidth - 50f, 18f), _turn, -180f, 180f);
                GUI.Label(new Rect(picture.x, picture.yMax + 34f, PictureWidth, 40f), Net.CanSendToHost
                    ? "Everybody with the mod sees this on you. You only see it here."
                    : "The room owner doesn't have Class Pack, so outfits are off in this room.", _small);

                Look look = Looks.Mine;
                int before = look.A;
                int beforeB = look.B;
                Settings fit = Plugin.Settings;
                float fitBefore = fit.headUp + fit.headForward * 7f + fit.headSize * 13f + fit.bodyUp * 17f + fit.bodyOut * 23f;

                GUILayout.BeginArea(new Rect(window.x + 340f, window.y + 54f, Width - 360f, Height - 70f));
                _scroll = GUILayout.BeginScrollView(_scroll);
                look.Hat = Row("Hat", look.Hat, Looks.HatNames, 5);
                look.Face = Several("Face (wear as many as you like)", look.Face, Looks.FaceNames);
                look.Back = Several("Back (wear as many as you like)", look.Back, Looks.BackNames);
                GUILayout.Space(6f);
                look.Hair = Row("Hair (students)", look.Hair, Looks.HairNames, 6);
                look.Body = Row("Face and body (students)", look.Body, Looks.PartNames, 6);
                look.Cloth = Row("Clothes (students)", look.Cloth, Looks.PartNames, 6);
                GUILayout.Space(6f);
                look.HairTint = Row("Hair colour (students)", look.HairTint, Looks.TintNames, 6);
                look.ClothTint = Row("Clothes colour (the whole body on a teacher)", look.ClothTint, Looks.TintNames, 6);
                look.BodyTint = Row("Skin colour (students)", look.BodyTint, Looks.TintNames, 6);
                GUILayout.Space(6f);
                look.Teacher = Row("When you are the teacher", look.Teacher, Looks.TeacherNames, 5);
                GUILayout.Label(TeacherHelp[Mathf.Clamp(look.Teacher, 0, TeacherHelp.Length - 1)], _small);
                GUILayout.Label("To be the teacher: before class, use the game's teacher wardrobe in the school to change role. "
                    + "In class, press " + Plugin.Settings.HelpKeyCode + " to see your keys.", _small);
                GUILayout.Space(8f);
                GUILayout.Label("Fit: move these if hats sit in the wrong place", _label);
                fit.headUp = Slide("Hats up or down", fit.headUp, -0.4f, 0.6f);
                fit.headForward = Slide("Hats forward or back", fit.headForward, -0.4f, 0.4f);
                fit.headSize = Slide("Hat size", fit.headSize, 0.4f, 3f);
                fit.bodyUp = Slide("Backpack and belly up or down", fit.bodyUp, -0.5f, 0.5f);
                fit.bodyOut = Slide("Backpack and belly in or out", fit.bodyOut, 0f, 0.4f);
                GUILayout.EndScrollView();
                GUILayout.EndArea();

                if (look.A != before || look.B != beforeB)
                {
                    Looks.Mine = look;
                    Game.PlaySound(AudioEvents.sfx_UIButtonClick_sfx);
                }
                if (!Mathf.Approximately(fitBefore, fit.headUp + fit.headForward * 7f + fit.headSize * 13f + fit.bodyUp * 17f + fit.bodyOut * 23f))
                {
                    _fitChanged = true;
                }
                if (_fitChanged && Event.current.rawType == EventType.MouseUp)
                {
                    _fitChanged = false;
                    fit.Save();
                }
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.depth = oldDepth;
            }
        }

        private static Texture2D Solid(Color32 color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }

        private void EnsureStyles()
        {
            if (_label != null)
            {
                return;
            }
            Color32 white = new Color32(245, 247, 250, 255);
            Color32 grey = new Color32(170, 178, 190, 255);
            _panel = Solid(new Color32(24, 27, 36, 245));
            _card = Solid(new Color32(48, 54, 68, 255));
            _cardHover = Solid(new Color32(70, 78, 96, 255));
            _cardOn = Solid(new Color32(132, 80, 220, 255));

            _panelStyle = new GUIStyle();
            _panelStyle.normal.background = _panel;
            _title = new GUIStyle { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _title.normal.textColor = white;
            _label = new GUIStyle { fontSize = 14, fontStyle = FontStyle.Bold };
            _label.normal.textColor = white;
            _label.margin = new RectOffset(0, 0, 8, 3);
            _small = new GUIStyle { fontSize = 12, wordWrap = true };
            _small.normal.textColor = grey;

            _cell = new GUIStyle { fontSize = 13, alignment = TextAnchor.MiddleCenter, fixedHeight = 28f };
            _cell.margin = new RectOffset(2, 2, 2, 2);
            _cell.normal.background = _card;
            _cell.normal.textColor = white;
            _cell.hover.background = _cardHover;
            _cell.hover.textColor = white;
            _cell.active.background = _cardHover;
            _cell.active.textColor = white;
            _cell.onNormal.background = _cardOn;
            _cell.onNormal.textColor = white;
            _cell.onHover.background = _cardOn;
            _cell.onHover.textColor = white;
            _cell.onActive.background = _cardOn;
            _cell.onActive.textColor = white;

            _button = new GUIStyle(_cell);
        }
    }
}
