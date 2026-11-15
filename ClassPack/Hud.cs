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
                string swim = Swim.DrawHud(width, height) ?? Dance.DrawHud(width, height);
                string teacher = Teachers.DrawHud(width, height);
                Tips.Draw(width, height);
                string call = PrankCall.DrawHud();
                // One line at a time: a ringing phone first, then the gun, the trick in hand,
                // the swim class, the teacher's own power or a wig to grab, the alarm box.
                string line = call ?? gun ?? trick ?? swim ?? teacher ?? hint;
                if (!string.IsNullOrEmpty(line) && !CursorGrab.Busy)
                {
                    Skin.Ensure();
                    var area = new Rect(0f, height - 118f, width, 28f);
                    Skin.Outlined(area, line, Skin.Hint, Skin.White, Skin.Outline, 1.5f);
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
