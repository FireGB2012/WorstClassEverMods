using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Draws the mod's on-screen extras during class: the nerf crosshair, screen flashes and
    /// one line of key hints at the bottom of the screen. Each feature says what it wants
    /// shown; this only does the drawing.
    /// </summary>
    internal sealed class Hud : MonoBehaviour
    {
        private GUIStyle _hint;
        private GUIStyle _shadow;

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !Game.InClass)
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
                GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
                GUI.depth = -8000;
                float width = Screen.width / scale;
                float height = Screen.height / scale;

                string hint = FireAlarm.DrawHud(width, height);
                string gun = NerfGuns.DrawHud(width, height);
                string trick = Tricks.DrawHud(width, height);
                string call = PrankCall.DrawHud();
                // One line at a time: a ringing phone first, then the gun, the trick in hand, the alarm box.
                string line = call ?? gun ?? trick ?? hint;
                if (!string.IsNullOrEmpty(line) && !CursorGrab.Busy)
                {
                    if (_hint == null)
                    {
                        _hint = new GUIStyle { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                        _hint.normal.textColor = new Color32(255, 255, 255, 255);
                        _shadow = new GUIStyle(_hint);
                        _shadow.normal.textColor = new Color32(0, 0, 0, 200);
                    }
                    var area = new Rect(0f, height - 118f, width, 28f);
                    GUI.Label(new Rect(area.x + 1.5f, area.y + 1.5f, area.width, area.height), line, _shadow);
                    GUI.Label(area, line, _hint);
                }
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.depth = oldDepth;
            }
        }
    }
}
