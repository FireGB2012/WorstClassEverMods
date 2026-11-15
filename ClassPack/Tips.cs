using System.Collections.Generic;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// How-to cards. The first time a player does something new (plays one of the new teachers,
    /// sits in a class as a student with the mod) a card with the keys shows on screen. It goes
    /// away after a while or on the help key, and the help key brings the card for what the
    /// player is doing right now back at any time. Which cards were seen is kept in the settings.
    /// </summary>
    internal static class Tips
    {
        private const float ShowSeconds = 25f;

        private static string _title;
        private static string[] _lines;
        private static float _until;
        private static Texture2D _back;
        private static GUIStyle _titleStyle, _lineStyle, _footStyle;

        private static string Use => Plugin.Settings.UseKeyCode.ToString();
        private static string Fire => "Your eraser key, or " + Plugin.Settings.FireKeyCode + ",";

        /// <summary>The card for a way of playing: "student", or "teacher" plus the teacher type.</summary>
        private static bool Card(string id, out string title, out string[] lines)
        {
            title = null;
            lines = null;
            switch (id)
            {
                case "student15":
                    title = "Class Pack: what you can do as a student";
                    lines = new[]
                    {
                        "Eat snacks: a phone or a nerf gun may turn up in your desk. Grab it with the pick-up key or  " + Use,
                        "With a phone in your hand,  " + Plugin.Settings.DoorDashKeyCode
                            + "  uses it once: DoorDash, a prank call or a trick. The teacher hears you scribbling",
                        "Tricks go in your hand:  " + Plugin.Settings.FireKeyCode + " or a wheel click  uses one,  " + Use + "  puts it away",
                        "In the break,  " + Plugin.Settings.WardrobeKeyCode + "  opens the wardrobe, and the panel by the blackboard picks the next class"
                    };
                    return true;
                case "teacher0":
                    title = "Class Pack: what you can do as the teacher";
                    lines = new[]
                    {
                        Plugin.Settings.TeacherSnackKeyCode + "  next to a student's desk eats a snack from the drawer for stamina",
                        Plugin.Settings.AnswerKeyCode + "  answers the phone when a student prank calls you",
                        Plugin.Settings.WardrobeKeyCode + "  opens the wardrobe in the break. Pick a teacher type there: gym teacher, lunch lady, janitor or wig"
                    };
                    return true;
                case "teacher1":
                    title = "You are the GYM TEACHER";
                    lines = new[]
                    {
                        Fire + "  throws a slice of pizza where you look",
                        "A student who is hit gets pizza over their screen and falls over",
                        "You can throw again after 8 seconds",
                        Plugin.Settings.TeacherSnackKeyCode + "  next to a desk eats a student's snack for stamina"
                    };
                    return true;
                case "teacher2":
                    title = "You are the LUNCH LADY";
                    lines = new[]
                    {
                        Fire + "  throws mashed potatoes where you look",
                        "A student who is hit is stuck for 4 seconds",
                        "You can't write on the blackboard. Your pot stands on the teacher's stand at the front",
                        "Just stand next to the pot: you stir it by yourself and your stamina fills up"
                    };
                    return true;
                case "teacher3":
                    title = "You are the JANITOR";
                    lines = new[]
                    {
                        Fire + "  mops the floor around you",
                        "That clears banana peels, glue puddles and whoopee cushions near you",
                        "The floor stays wet for 20 seconds, and students who walk on it slip",
                        "You can mop again after 6 seconds"
                    };
                    return true;
                case "swimpee3":
                    title = "SWIM CLASS: what you can do as a student";
                    lines = new[]
                    {
                        "In the water, hold  " + Plugin.Settings.PeeKeyCode + "  to pee in the pool for mischief. It takes 3 seconds and you can hardly move",
                        "Pee right next to the teacher while they are in the water: double mischief. Then you have to wait a while",
                        "In the water,  " + Use + "  splashes the teacher's screen. Crawl in the pool to dive under",
                        "The ramp at the shallow end leads in and out; at a ladder or the diving board's steps the pick-up key climbs up",
                        "Now and then a phone turns up on the coffee table in the teachers' lounge, through the door at the deep end",
                        "The first to grab it gets it: one DoorDash order (the food lands in the lounge), prank call or trick"
                    };
                    return true;
                case "swimteacher3":
                    title = "SWIM CLASS: what you can do as the teacher";
                    lines = new[]
                    {
                        "Catch students as usual. The poles stand along the pool",
                        "Students pee in the pool for mischief: keep out of the yellow water",
                        "No blackboard here: your stamina drains at half speed, and fills fast at the lifeguard chair",
                        "The water slows you down too. Stay back from the edge, or you get splashed",
                        "Now and then a phone turns up in your lounge (the door at the deep end): students race for it",
                        "A splash covers your screen with water for a few seconds"
                    };
                    return true;
                case "faint":
                    title = "KNOCKED OUT BY A NERF DART";
                    lines = new[]
                    {
                        "Whoever a nerf dart hits falls over like a rag doll for a few seconds",
                        "While a student lies there, the teacher can walk up and grab them",
                        "Students can shoot the teacher too: the teacher is out for 5 seconds",
                        "Nerf guns turn up in desks when students eat snacks. One shot each"
                    };
                    return true;
                case "dance3":
                    title = "DANCE CLASS: what you can do as a student";
                    lines = new[]
                    {
                        "Everyone has to dance! You always do the class dance and can shuffle slowly",
                        Plugin.Settings.WormKeyCode + "  does the worm: mischief every few seconds while you keep it up",
                        "Doing the worm you only creep along: the teacher can catch you at it",
                        Plugin.Settings.DanceKeyCode + "  gets you back up to the class dance",
                        "Now and then a phone turns up on the snack table. The first to grab it gets it",
                        "One use: a DoorDash order (the food lands on the snack table), a prank call or a trick"
                    };
                    return true;
                case "danceteacher3":
                    title = "DANCE CLASS: what you can do as the teacher";
                    lines = new[]
                    {
                        "Everyone has to dance. Students doing the worm make mischief: catch them!",
                        Plugin.Settings.DanceKeyCode + "  lets you dance too, just for fun",
                        "The poles stand along the wall",
                        "No blackboard here: your stamina drains at half speed, and fills fast at the DJ booth",
                        "Now and then a phone turns up on the snack table: students race for it"
                    };
                    return true;
                case "teacher4":
                    title = "You are the BALD TEACHER WITH A WIG";
                    lines = new[]
                    {
                        "You teach like a normal teacher, but watch your back",
                        "A student right behind you can yank your wig off for big mischief",
                        "Without the wig you can't throw chalk or the eraser",
                        "Walk over the wig on the floor to put it back on"
                    };
                    return true;
            }
            return false;
        }

        private static string CurrentId()
        {
            Unit me = Game.LocalPlayer;
            if (me == null)
            {
                return null;
            }
            if (Faint.IsDown(me))
            {
                return "faint";
            }
            if (Dance.Active && (me.IsTeacher || me.IsStudent))
            {
                return me.IsTeacher ? "danceteacher3" : "dance3";
            }
            if (Swim.Active && (me.IsTeacher || me.IsStudent))
            {
                return me.IsTeacher ? "swimteacher3" : "swimpee3";
            }
            if (me.IsTeacher)
            {
                return "teacher" + (Plugin.Settings.teachers ? Looks.KindOf(me.UID) : 0);
            }
            return me.IsStudent ? "student15" : null;
        }

        private static void Show(string id)
        {
            string title;
            string[] lines;
            if (id != null && Card(id, out title, out lines))
            {
                _title = title;
                _lines = lines;
                _until = Time.unscaledTime + ShowSeconds;
            }
        }

        public static void Tick()
        {
            Settings settings = Plugin.Settings;
            if (!settings.tips || !Game.InClass || !Net.CanSendToHost)
            {
                _until = 0f;
                return;
            }
            string id = CurrentId();
            if (Input.GetKeyDown(settings.HelpKeyCode) && !Game.IsTyping)
            {
                if (Time.unscaledTime < _until)
                {
                    _until = 0f;
                }
                else
                {
                    Show(id);
                }
                return;
            }
            // The first time for this way of playing: show its card without being asked.
            if (id != null && Time.unscaledTime >= _until)
            {
                var seen = new List<string>((settings.seenTips ?? string.Empty).Split(','));
                if (!seen.Contains(id))
                {
                    seen.Add(id);
                    settings.seenTips = string.Join(",", seen.ToArray()).Trim(',');
                    settings.Save();
                    Show(id);
                }
            }
        }

        /// <summary>Draws the card near the top of the screen. Called in a 1280 by 720 frame.</summary>
        public static void Draw(float width, float height)
        {
            if (Time.unscaledTime >= _until || _lines == null || CursorGrab.Busy)
            {
                return;
            }
            Skin.Ensure();
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(Skin.Title) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
                _lineStyle = new GUIStyle(Skin.Label) { fontSize = 18, alignment = TextAnchor.MiddleLeft, wordWrap = false };
                _footStyle = new GUIStyle(Skin.Small) { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            }
            // As wide as the longest line needs (they don't wrap), within the screen.
            float panelWidth = 780f;
            foreach (string line in _lines)
            {
                panelWidth = Mathf.Max(panelWidth, _lineStyle.CalcSize(new GUIContent("-  " + line)).x + 100f);
            }
            panelWidth = Mathf.Min(panelWidth, width - 40f);
            float panelHeight = 104f + _lines.Length * 32f;
            var panel = new Rect((width - panelWidth) * 0.5f, 70f, panelWidth, panelHeight);
            GUI.Box(panel, GUIContent.none, Skin.Panel);
            Skin.Outlined(new Rect(panel.x, panel.y + 12f, panel.width, 38f), _title, _titleStyle, Skin.Orange, Skin.Outline, 2f);
            for (int i = 0; i < _lines.Length; i++)
            {
                GUI.Label(new Rect(panel.x + 36f, panel.y + 60f + i * 32f, panel.width - 64f, 30f), "-  " + _lines[i], _lineStyle);
            }
            int left = Mathf.CeilToInt(_until - Time.unscaledTime);
            GUI.Label(new Rect(panel.x, panel.yMax - 36f, panel.width, 26f),
                "Press " + Plugin.Settings.HelpKeyCode + " to hide this. Press it again any time to see it.   (" + left + "s)", _footStyle);
        }
    }
}
