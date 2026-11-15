using System;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// The student's phone: a small screen you pull up with a key. It has a home screen with
    /// app icons for DoorDash, prank calls and the bag of tricks. The rules of each app live
    /// in its own file; this file only draws the phone and passes on the taps.
    /// </summary>
    internal sealed class Phone : MonoBehaviour
    {
        private const float PhoneWidth = 300f;
        private const float PhoneHeight = 610f;

        private enum Screen
        {
            Home,
            DoorDash,
            Call,
            Tricks
        }

        private readonly CursorGrab _cursor = new CursorGrab();
        private bool _open;
        private Screen _screen;

        private Texture2D _bezel, _wall, _red, _green, _purple, _blue, _card, _cardHover, _muted;
        private GUIStyle _bezelStyle, _wallStyle, _header, _headerGreen, _text, _small, _clock, _icon, _iconGreen, _iconLabel, _dish, _plain, _note,
            _headerPurple, _iconPurple, _iconBlue, _trick;

        private static bool InRoom => NetworkClient.active || NetworkServer.active;

        private void Update()
        {
            Unit me = Game.LocalPlayer;
            if (_open && (!InRoom || me == null || me.IsStun || me.BodyActionsBlocked))
            {
                Close();
            }
            if (Input.GetKeyDown(Plugin.Settings.DoorDashKeyCode) && !Game.IsTyping)
            {
                if (_open)
                {
                    Close();
                }
                else if (InRoom && me != null && !me.IsStun && !me.BodyActionsBlocked && _cursor.Begin())
                {
                    _open = true;
                    _screen = Screen.Home;
                }
            }
            else if (_open && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
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

        private void Close()
        {
            _open = false;
            _cursor.End();
        }

        private void OnGUI()
        {
            if (!_open)
            {
                return;
            }
            float scale = Mathf.Min(UnityEngine.Screen.width / 1280f, UnityEngine.Screen.height / 720f);
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
                float width = UnityEngine.Screen.width / scale;
                float height = UnityEngine.Screen.height / scale;
                var body = new Rect(width - PhoneWidth - 70f, (height - PhoneHeight) * 0.5f, PhoneWidth, PhoneHeight);
                GUI.Box(body, GUIContent.none, _bezelStyle);
                var screen = new Rect(body.x + 10f, body.y + 10f, body.width - 20f, body.height - 20f);
                GUI.Box(screen, GUIContent.none, _wallStyle);
                GUILayout.BeginArea(screen);
                DrawStatusBar();
                if (_screen == Screen.DoorDash)
                {
                    DrawDoorDash();
                }
                else if (_screen == Screen.Call)
                {
                    DrawCall();
                }
                else if (_screen == Screen.Tricks)
                {
                    DrawTricks();
                }
                else
                {
                    DrawHome();
                }
                GUILayout.EndArea();
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.depth = oldDepth;
            }
        }

        private void DrawStatusBar()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(26f));
            GUILayout.Space(12f);
            GUILayout.Label(DateTime.Now.ToString("HH:mm"), _clock);
            GUILayout.FlexibleSpace();
            GUILayout.Label("5G", _clock);
            GUILayout.Space(12f);
            GUILayout.EndHorizontal();
        }

        private void DrawHome()
        {
            GUILayout.Space(26f);
            GUILayout.BeginHorizontal();
            GUILayout.Space(13f);
            if (DrawApp("DD", "DoorDash", _icon))
            {
                _screen = Screen.DoorDash;
            }
            GUILayout.Space(13f);
            if (DrawApp("Call", "Prank call", _iconGreen))
            {
                _screen = Screen.Call;
            }
            GUILayout.Space(13f);
            if (DrawApp("Tricks", "Tricks", _iconPurple))
            {
                _screen = Screen.Tricks;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(14f);
            GUILayout.BeginHorizontal();
            GUILayout.Space(13f);
            bool wardrobe = DrawApp("Looks", "Wardrobe", _iconBlue);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (wardrobe)
            {
                // The wardrobe is its own big window, so the phone goes away first.
                Close();
                Wardrobe.RequestOpen();
            }

            GUILayout.FlexibleSpace();
            DrawPutAway();
        }

        private bool DrawApp(string icon, string label, GUIStyle style)
        {
            GUILayout.BeginVertical(GUILayout.Width(76f));
            bool tapped = GUILayout.Button(icon, style, GUILayout.Width(76f), GUILayout.Height(76f));
            GUILayout.Label(label, _iconLabel, GUILayout.Width(76f));
            GUILayout.EndVertical();
            return tapped;
        }

        private void DrawTricks()
        {
            GUILayout.Label("Tricks", _headerPurple, GUILayout.Height(44f));
            GUILayout.BeginHorizontal();
            GUILayout.Space(12f);
            GUILayout.BeginVertical();
            GUILayout.Space(6f);
            string blocked = Tricks.WhyNot();
            if (blocked != null)
            {
                GUILayout.Label(blocked, _note);
            }
            bool putAway = false;
            for (int item = Tricks.Airplane; item <= Tricks.Nap; item++)
            {
                string status = Tricks.StatusOf(item);
                string label = Tricks.Names[item] + (status != null ? "   <color=#d2601a>" + status + "</color>" : "")
                    + "\n<size=11><color=#8c765e>" + Tricks.Blurbs[item] + "</color></size>";
                GUI.enabled = Tricks.CanPick(item);
                if (GUILayout.Button(label, _trick, GUILayout.Height(46f)) && Tricks.Pick(item))
                {
                    putAway = true;
                }
            }
            GUI.enabled = true;
            GUILayout.EndVertical();
            GUILayout.Space(12f);
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.Space(12f);
            if (GUILayout.Button("< Home", _plain, GUILayout.Height(32f), GUILayout.Width(90f)))
            {
                _screen = Screen.Home;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            DrawPutAway();
            if (putAway)
            {
                // The trick is in your hand now, or already under way.
                Close();
            }
        }

        private void DrawDoorDash()
        {
            GUILayout.Label("DoorDash", _header, GUILayout.Height(44f));
            GUILayout.BeginHorizontal();
            GUILayout.Space(12f);
            GUILayout.BeginVertical();
            GUILayout.Space(8f);
            int points = Mathf.RoundToInt(Plugin.Settings.doorDashPoints);
            GUILayout.Label("We deliver " + DoorDash.WhereFood + ". Go and get it, then eat it for " + points + " mischief.", _small);
            GUILayout.Space(6f);

            DoorDash.Stage stage = DoorDash.Current;
            if (stage == DoorDash.Stage.OnTheWay)
            {
                int left = Mathf.Max(0, Mathf.CeilToInt(DoorDash.ArrivesAt - Time.time));
                GUILayout.Label("Your " + DoorDash.MenuNames[DoorDash.OrderedFood] + " is on the way.", _text);
                GUILayout.Label(left > 0 ? "Arrives in " + left + "s" : "Arriving now...", _note);
            }
            else
            {
                if (stage == DoorDash.Stage.Arrived)
                {
                    GUILayout.Label("Delivered! Your " + DoorDash.MenuNames[DoorDash.OrderedFood] + " is " + DoorDash.WhereFood + ".", _note);
                }
                else if (stage == DoorDash.Stage.Failed)
                {
                    GUILayout.Label("That order was cancelled.", _note);
                }
                string blocked = DoorDash.WhyNot();
                if (blocked != null)
                {
                    GUILayout.Label(blocked, _small);
                }
                GUI.enabled = blocked == null;
                for (int i = 0; i < DoorDash.MenuNames.Length; i++)
                {
                    if (GUILayout.Button(DoorDash.MenuNames[i], _dish, GUILayout.Height(38f)))
                    {
                        DoorDash.Order(i);
                    }
                }
                GUI.enabled = true;
            }
            GUILayout.EndVertical();
            GUILayout.Space(12f);
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.Space(12f);
            if (GUILayout.Button("< Home", _plain, GUILayout.Height(32f), GUILayout.Width(90f)))
            {
                _screen = Screen.Home;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            DrawPutAway();
        }

        private void DrawCall()
        {
            GUILayout.Label("Prank call", _headerGreen, GUILayout.Height(44f));
            GUILayout.BeginHorizontal();
            GUILayout.Space(12f);
            GUILayout.BeginVertical();
            GUILayout.Space(8f);
            GUILayout.Label("Ring the teacher's phone. If they don't pick up in time they lose stamina. You get a little mischief for the call.", _small);
            GUILayout.Space(8f);
            string blocked = PrankCall.WhyNot();
            if (blocked != null)
            {
                GUILayout.Label(blocked, _note);
            }
            GUI.enabled = blocked == null;
            if (GUILayout.Button("Call the teacher", _dish, GUILayout.Height(44f)))
            {
                PrankCall.Call();
            }
            GUI.enabled = true;
            GUILayout.EndVertical();
            GUILayout.Space(12f);
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.Space(12f);
            if (GUILayout.Button("< Home", _plain, GUILayout.Height(32f), GUILayout.Width(90f)))
            {
                _screen = Screen.Home;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            DrawPutAway();
        }

        private void DrawPutAway()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(12f);
            if (GUILayout.Button("Put phone away (" + Plugin.Settings.DoorDashKeyCode + ")", _plain, GUILayout.Height(32f)))
            {
                Close();
            }
            GUILayout.Space(12f);
            GUILayout.EndHorizontal();
            GUILayout.Space(12f);
        }

        private static Texture2D Solid(Color32 color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }

        private static GUIStyle Filled(Texture2D normal, Texture2D hover)
        {
            var style = new GUIStyle();
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = hover;
            return style;
        }

        private static GUIStyle Coloured(Color32 fill, Color32 edge, int radius, int size)
        {
            GUIStyle style = Skin.Box(fill, edge, radius, 3);
            style.fontSize = size;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Skin.White;
            style.hover.textColor = Skin.White;
            style.active.textColor = Skin.White;
            return style;
        }

        private void EnsureStyles()
        {
            if (_text != null)
            {
                return;
            }
            Skin.Ensure();
            var red = new Color32(232, 72, 52, 255);
            var redDark = new Color32(160, 40, 28, 255);
            var purple = new Color32(150, 100, 226, 255);
            var purpleDark = new Color32(96, 58, 160, 255);
            var blue = new Color32(72, 142, 236, 255);
            var blueDark = new Color32(38, 88, 168, 255);

            // The phone itself is dark; its screen is the game's cream paper.
            _bezelStyle = Skin.Box(new Color32(52, 42, 34, 255), new Color32(26, 20, 16, 255), 26, 4);
            _wallStyle = Skin.Box(Skin.Cream, Skin.Line, 18, 2);

            _text = new GUIStyle(Skin.Label);
            _small = new GUIStyle(Skin.Small) { fontSize = 13 };
            _note = new GUIStyle(Skin.Note);
            _note.margin = new RectOffset(0, 0, 4, 8);
            _clock = new GUIStyle(Skin.Small) { alignment = TextAnchor.MiddleCenter, wordWrap = false };

            _header = Coloured(red, redDark, 12, 20);
            _headerGreen = Coloured(Skin.Green, Skin.GreenDark, 12, 20);
            _headerPurple = Coloured(purple, purpleDark, 12, 20);
            _icon = Coloured(red, redDark, 18, 30);
            _iconGreen = Coloured(Skin.Green, Skin.GreenDark, 18, 22);
            _iconPurple = Coloured(purple, purpleDark, 18, 18);
            _iconBlue = Coloured(blue, blueDark, 18, 18);
            _iconLabel = new GUIStyle(Skin.Label) { fontSize = 12, alignment = TextAnchor.UpperCenter, wordWrap = false };
            _iconLabel.margin = new RectOffset(0, 0, 4, 0);

            _dish = new GUIStyle(Skin.Button) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            _dish.padding = new RectOffset(12, 12, 0, 0);
            _dish.margin = new RectOffset(0, 0, 3, 3);
            _trick = new GUIStyle(_dish) { richText = true };
            _trick.margin = new RectOffset(0, 0, 2, 2);
            _plain = new GUIStyle(Skin.Button) { fontSize = 13 };
            _plain.margin = new RectOffset(0, 0, 4, 4);
        }

        private void OnDestroy()
        {
            Close();
            foreach (Texture2D texture in new[] { _bezel, _wall, _red, _green, _purple, _blue, _card, _cardHover, _muted })
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }
        }
    }
}
