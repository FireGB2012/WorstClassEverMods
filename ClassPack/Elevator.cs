using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// The elevator panel: three big buttons on the classroom wall beside the blackboard that
    /// pick where the next class is, so nobody has to open the room owner's window for it.
    ///
    ///   blue    the pool hall (swim class)
    ///   pink    the dance hall (dance class)
    ///   yellow  the classroom (a normal class)
    ///
    /// Anyone can press one during the break (not while a class runs): look at it and press the
    /// game's pick-up key (or the use key). The host checks it, sets the next class (the same
    /// setting as the room owner's window) and tells everybody; the label of the chosen class
    /// lights up green. Every player's game puts the panel up by itself, in the same spot.
    /// </summary>
    internal static class Elevator
    {
        private const float ButtonGap = 0.155f;
        private const float ButtonRadius = 0.07f;
        private const float Height = 1.25f;

        // Top to bottom, as on the panel; each with the class it picks.
        private static readonly int[] ButtonClass = { Swim.SwimClass, Dance.DanceClass, 0 };
        private static readonly string[] ButtonName = { "POOL", "DANCE HALL", "CLASSROOM" };
        private static readonly Color32 Chosen = new Color32(110, 230, 90, 255);
        private static readonly Color32 Plain = new Color32(255, 255, 255, 255);

        private static GameObject _panel;
        private static readonly TextMesh[] Labels = new TextMesh[3];
        private static int _floor = -1;
        private static bool _placed;
        private static Vector3 _at;
        private static Vector3 _into;
        private static float _nextLookAt;
        private static int _aimed = -1;
        private static int _chosen;
        private static GUIStyle _prompt;
        private static Texture2D _white;

        // Host
        private static int _sent = -1;

        public static void Tick()
        {
            bool inRoom = NetworkClient.active || NetworkServer.active;
            ClassroomManager classroom = inRoom ? Game.Peek<ClassroomManager>() : null;
            if (classroom == null || !Plugin.Settings.elevator)
            {
                Remove();
                return;
            }
            if (!_placed || classroom.currentFloor != _floor)
            {
                if (Time.unscaledTime >= _nextLookAt)
                {
                    _nextLookAt = Time.unscaledTime + 2f;
                    Place(classroom);
                }
            }
            if (NetworkServer.active && Plugin.Settings.nextClass != _sent)
            {
                // Whatever picked it (the panel or the owner's window): everybody's panel shows it.
                _sent = Plugin.Settings.nextClass;
                Net.SendToModded(new CpTell { kind = CpKind.Elevator, i = _sent });
            }
            Aim();
            if (_aimed >= 0 && Input.GetKeyDown(Plugin.Settings.UseKeyCode) && !Game.IsTyping && !CursorGrab.Busy)
            {
                Press();
            }
        }

        private static void Remove()
        {
            if (_panel != null)
            {
                Object.Destroy(_panel);
            }
            _panel = null;
            _placed = false;
            _floor = -1;
            _aimed = -1;
            _sent = -1;
        }

        /// <summary>On the blackboard's wall, a step to its side, at about shoulder height for the game's students.</summary>
        private static void Place(ClassroomManager classroom)
        {
            UnitManager units = Game.Peek<UnitManager>();
            ClassroomItem room = TeacherOffice.FindClassroom(classroom.currentFloor);
            if (units == null || room == null)
            {
                return;
            }
            Vector3 centre = room.transform.position;
            Unit board = null;
            float best = 15f;
            foreach (Unit unit in units.unitsDict.Values)
            {
                if (unit == null || !unit.isActived || unit.GetComponent<BlackBoardComponent>() == null)
                {
                    continue;
                }
                Vector3 offset = unit.Position - centre;
                float distance = new Vector2(offset.x, offset.z).magnitude;
                if (Mathf.Abs(offset.y) < 4f && distance < best)
                {
                    best = distance;
                    board = unit;
                }
            }
            if (board == null)
            {
                return;
            }
            Vector3 into = centre - board.Position;
            into.y = 0f;
            if (into.sqrMagnitude < 0.01f)
            {
                return;
            }
            into.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, into).normalized;
            float half = 1.2f;
            if (board.model != null)
            {
                Renderer[] renderers = board.model.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                    half = Mathf.Clamp(Mathf.Abs(Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(side.x), 0f, Mathf.Abs(side.z)))), 0.5f, 3f);
                }
            }
            Vector3 beside = board.Position + side * (half + 0.6f);
            // The wall's face: a ray from inside the room back towards the wall.
            Vector3 wall = beside;
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(beside.x, centre.y + 1.3f, beside.z) + into * 1.5f, -into, out hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                wall = hit.point;
            }
            float floor = centre.y;
            if (Physics.Raycast(wall + into * 0.5f + Vector3.up * 1.2f, Vector3.down, out hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                floor = hit.point.y;
            }
            _at = new Vector3(wall.x, floor + Height, wall.z) + into * 0.01f;
            _into = into;
            _floor = classroom.currentFloor;
            _placed = true;

            if (_panel != null)
            {
                Object.Destroy(_panel);
            }
            _panel = Models.Build("elevator_panel") ?? new GameObject("[ClassPack elevator panel]");
            _panel.transform.SetPositionAndRotation(_at, Quaternion.LookRotation(into, Vector3.up));
            // A label beside each button, readable from the room.
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Vector3 right = Vector3.Cross(Vector3.up, -into).normalized;
            for (int i = 0; i < 3; i++)
            {
                var holder = new GameObject("label " + ButtonName[i]);
                holder.transform.SetParent(_panel.transform, true);
                holder.transform.SetPositionAndRotation(Button(i) + right * 0.11f, Quaternion.LookRotation(-into, Vector3.up));
                TextMesh text = holder.AddComponent<TextMesh>();
                text.text = ButtonName[i];
                text.font = font;
                text.fontSize = 48;
                text.characterSize = 0.006f;
                text.anchor = TextAnchor.MiddleLeft;
                text.fontStyle = FontStyle.Bold;
                if (font != null)
                {
                    holder.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                }
                Labels[i] = text;
            }
            Paint();
            Plugin.Log("elevator: panel beside the blackboard at " + _at.ToString("F2") + ", facing " + into.ToString("F2"));
        }

        /// <summary>The middle of a button's face, top (0) to bottom (2).</summary>
        private static Vector3 Button(int i)
        {
            return _at + Vector3.up * (ButtonGap * (1 - i)) + _into * 0.035f;
        }

        private static void Paint()
        {
            for (int i = 0; i < 3; i++)
            {
                if (Labels[i] != null)
                {
                    bool on = ButtonClass[i] == _chosen;
                    Labels[i].color = on ? (Color)Chosen : (Color)Plain;
                    Labels[i].text = on ? ButtonName[i] + "  <" : ButtonName[i];
                }
            }
        }

        /// <summary>Which button the player looks at from close by, or -1.</summary>
        private static void Aim()
        {
            _aimed = -1;
            Camera camera = Camera.main;
            Unit me = Game.LocalPlayer;
            if (!_placed || camera == null || me == null || !me.isActived || CursorGrab.Busy)
            {
                return;
            }
            float bestOff = ButtonRadius;
            for (int i = 0; i < 3; i++)
            {
                Vector3 to = Button(i) - camera.transform.position;
                float along = Vector3.Dot(to, camera.transform.forward);
                if (along < 0.2f || along > 3.5f)
                {
                    continue;
                }
                float off = (to - camera.transform.forward * along).magnitude;
                if (off < bestOff)
                {
                    bestOff = off;
                    _aimed = i;
                }
            }
        }

        private static void Press()
        {
            if (Game.InClass)
            {
                Tricks.Tip("The elevator only works in the break, not during class.");
                return;
            }
            Game.PlaySound(AudioEvents.sfx_ItemPickup_sfx);
            Net.SendToHost(new CpAsk { kind = CpKind.ElevatorAsk, i = ButtonClass[_aimed] });
        }

        /// <summary>The game's pick-up key went down: true when it pressed a button on the panel.</summary>
        public static bool TryPressWithPickUpKey()
        {
            if (_aimed < 0 || CursorGrab.Busy || Game.IsTyping)
            {
                return false;
            }
            Press();
            return true;
        }

        // ---------------------------------------------------------------------- host side

        public static void OnAsk(Unit player, CpAsk ask)
        {
            Settings settings = Plugin.Settings;
            if (player == null || !player.isActived || Game.InClass || !settings.elevator)
            {
                return;
            }
            int pick = ask.i;
            if ((pick == Swim.SwimClass && !settings.swimClass) || (pick == Dance.DanceClass && !settings.danceClass)
                || (pick != 0 && pick != Swim.SwimClass && pick != Dance.DanceClass))
            {
                Net.Toast(player, "That class is switched off in the room owner's window.", 3f);
                return;
            }
            if (_placed && Tricks.Flat(player.Position - _at).magnitude > 5f)
            {
                return;
            }
            if (settings.nextClass != pick)
            {
                settings.nextClass = pick;
                settings.Save();
                Plugin.Log("elevator: " + Poles.NameOf(player) + " picked the next class: " + Swim.ClassNames[pick]);
            }
            string where = pick == Swim.SwimClass ? "the pool" : pick == Dance.DanceClass ? "the dance hall" : "the classroom";
            UnitManager units = Game.Peek<UnitManager>();
            if (units != null)
            {
                foreach (Unit someone in new List<Unit>(units.playerList))
                {
                    if (someone != null && someone.isActived && someone.isPlayer)
                    {
                        Net.Toast(someone, Poles.NameOf(player) + " pressed the elevator: next class is in " + where + ".", 3f);
                    }
                }
            }
        }

        /// <summary>Host: somebody with the mod just joined, and their panel should show the pick.</summary>
        public static void OnPeerJoined(NetworkConnectionToClient connection)
        {
            Net.SendTo(connection, new CpTell { kind = CpKind.Elevator, i = Plugin.Settings.nextClass });
        }

        public static void OnTell(CpTell tell)
        {
            _chosen = tell.i;
            Paint();
        }

        /// <summary>The prompt under the crosshair while looking at a button. Drawn in a 1280 by 720 frame.</summary>
        public static void DrawPrompt(float width, float height)
        {
            if (_aimed < 0 || CursorGrab.Busy)
            {
                return;
            }
            if (_white == null)
            {
                _white = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                _white.SetPixel(0, 0, Color.white);
                _white.Apply(false, true);
            }
            if (_prompt == null)
            {
                _prompt = new GUIStyle { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _prompt.normal.textColor = Color.white;
            }
            var box = new Rect(width * 0.5f - 210f, height * 0.5f + 36f, 420f, 34f);
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(box, _white);
            GUI.color = old;
            string text = Game.InClass ? "Elevator: works in the break" : "[" + NerfGuns.PickUpKeyName() + "]  Next class: " + ButtonName[_aimed].ToLowerInvariant();
            GUI.Label(box, text, _prompt);
        }

        /// <summary>Self-test: where to stand and look to see the panel, when it is up.</summary>
        public static bool TestSpot(out Vector3 from, out Vector3 at)
        {
            from = _at + _into * 1.6f + Vector3.up * 0.1f;
            at = _at;
            return _placed;
        }
    }
}
