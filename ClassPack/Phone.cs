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

        private Texture2D _bezel, _wall, _red, _green, _purple, _card, _cardHover, _muted;
        private GUIStyle _bezelStyle, _wallStyle, _header, _headerGreen, _text, _small, _clock, _icon, _iconGreen, _iconLabel, _dish, _plain, _note,
            _headerPurple, _iconPurple, _trick;

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
                string label = Tricks.Names[item] + (status != null ? "   <color=#ffc85a>" + status + "</color>" : "")
                    + "\n<size=11><color=#aab2be>" + Tricks.Blurbs[item] + "</color></size>";
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
            GUILayout.Label("We deliver to the teacher's office. Go and get it, then eat it for " + points + " mischief.", _small);
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
                    GUILayout.Label("Delivered! Your " + DoorDash.MenuNames[DoorDash.OrderedFood] + " is in the teacher's office.", _note);
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

        private void EnsureStyles()
        {
            if (_text != null)
            {
                return;
            }
            Color32 white = new Color32(245, 247, 250, 255);
            Color32 grey = new Color32(170, 178, 190, 255);
            _bezel = Solid(new Color32(12, 13, 16, 255));
            _wall = Solid(new Color32(30, 34, 44, 255));
            _red = Solid(new Color32(235, 23, 0, 255));
            _card = Solid(new Color32(48, 54, 68, 255));
            _cardHover = Solid(new Color32(70, 78, 96, 255));
            _muted = Solid(new Color32(40, 45, 57, 255));

            _bezelStyle = Filled(_bezel, _bezel);
            _wallStyle = Filled(_wall, _wall);

            _text = new GUIStyle { fontSize = 15, wordWrap = true };
            _text.normal.textColor = white;
            _small = new GUIStyle(_text) { fontSize = 13 };
            _small.normal.textColor = grey;
            _note = new GUIStyle(_text) { fontSize = 14, fontStyle = FontStyle.Bold };
            _note.normal.textColor = new Color32(255, 200, 90, 255);
            _note.margin = new RectOffset(0, 0, 4, 8);
            _clock = new GUIStyle(_text) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            _clock.normal.textColor = grey;

            _header = Filled(_red, _red);
            _header.fontSize = 20;
            _header.fontStyle = FontStyle.Bold;
            _header.alignment = TextAnchor.MiddleCenter;
            _header.normal.textColor = white;

            _icon = Filled(_red, _red);
            _icon.fontSize = 34;
            _icon.fontStyle = FontStyle.Bold;
            _icon.alignment = TextAnchor.MiddleCenter;
            _icon.normal.textColor = white;
            _icon.hover.textColor = white;
            _icon.active.textColor = white;
            _green = Solid(new Color32(40, 180, 90, 255));
            _headerGreen = new GUIStyle(_header);
            _headerGreen.normal.background = _green;
            _iconGreen = new GUIStyle(_icon) { fontSize = 24 };
            _iconGreen.normal.background = _green;
            _iconGreen.hover.background = _green;
            _iconGreen.active.background = _green;
            _iconLabel = new GUIStyle(_text) { fontSize = 12, alignment = TextAnchor.UpperCenter };
            _iconLabel.margin = new RectOffset(0, 0, 4, 0);
            _purple = Solid(new Color32(132, 80, 220, 255));
            _headerPurple = new GUIStyle(_header);
            _headerPurple.normal.background = _purple;
            _iconPurple = new GUIStyle(_icon) { fontSize = 19 };
            _iconPurple.normal.background = _purple;
            _iconPurple.hover.background = _purple;
            _iconPurple.active.background = _purple;

            _dish = Filled(_card, _cardHover);
            _dish.fontSize = 15;
            _dish.alignment = TextAnchor.MiddleLeft;
            _dish.padding = new RectOffset(12, 12, 0, 0);
            _dish.margin = new RectOffset(0, 0, 3, 3);
            _dish.normal.textColor = white;
            _dish.hover.textColor = white;
            _dish.active.textColor = white;

            _trick = new GUIStyle(_dish) { richText = true };
            _trick.margin = new RectOffset(0, 0, 2, 2);

            _plain = Filled(_muted, _cardHover);
            _plain.fontSize = 13;
            _plain.alignment = TextAnchor.MiddleCenter;
            _plain.margin = new RectOffset(0, 0, 4, 4);
            _plain.normal.textColor = grey;
            _plain.hover.textColor = white;
            _plain.active.textColor = white;
        }

        private void OnDestroy()
        {
            Close();
            foreach (Texture2D texture in new[] { _bezel, _wall, _red, _green, _purple, _card, _cardHover, _muted })
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }
        }
    }
}
