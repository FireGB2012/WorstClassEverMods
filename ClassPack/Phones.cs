using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// The phone as a thing you find and hold.
    ///
    /// Phones turn up in desk drawers the way the nerf guns do: each time a student finishes a
    /// snack there is a 7.4% chance (phoneChancePerSnack) that a phone is in their drawer. It
    /// hovers over the open drawer, big and turning, and is taken with the game's pick-up key
    /// (or the use key from the seat). Then it is held in the hand, where everybody can see it.
    ///
    /// The phone key (O, in class) pulls it up in front of the face, so the teacher can see who
    /// is on their phone, and opens its screen (Phone.cs): DoorDash, a prank call or a trick.
    /// Using one of them uses the phone up. While a phone is up it makes a scribbling sound
    /// that can be heard nearby, and the teacher's screen says someone is on their phone.
    ///
    /// The host keeps who has a phone; DoorDash, prank calls and tricks ask HostUse before
    /// they go ahead. In the swim and dance halls there are no drawers, so no phones.
    /// </summary>
    internal static class Phones
    {
        public const int Gone = 0;
        public const int InHand = 1;
        public const int Up = 2;

        private const float DrawerScale = 1.6f;
        private const float HeldScale = 1.25f;
        private const float AimSlack = 0.26f;

        private sealed class Held
        {
            public int State;
            public GameObject Model;
            public float NextSoundAt;
        }

        // Every player's game
        private static readonly Dictionary<ulong, Held> Holders = new Dictionary<ulong, Held>();
        private static bool _drawerPhone;
        private static GameObject _drawerModel;
        private static bool _drawerWaiting;
        private static bool _drawerAimed;
        private static Vector3 _drawerMiddle;
        private static float _nextTipAt;
        private static GUIStyle _prompt;
        private static Texture2D _white;

        // Host only: who has a phone waiting in the drawer, who holds one (and whether it is up).
        private static readonly HashSet<ulong> HostDrawer = new HashSet<ulong>();
        private static readonly Dictionary<ulong, bool> HostHeld = new Dictionary<ulong, bool>();
        private static readonly Dictionary<ulong, float> HostTeacherToldAt = new Dictionary<ulong, float>();

        // --------------------------------------------------------------- what others ask

        /// <summary>True while this player holds a phone.</summary>
        public static bool HasLocal
        {
            get
            {
                Unit me = Game.LocalPlayer;
                Held held;
                return me != null && Holders.TryGetValue(me.UID, out held) && held.State != Gone;
            }
        }

        /// <summary>The phone screen opened (true) or closed (false) on this player's game.</summary>
        public static void SetUp(bool up)
        {
            if (HasLocal && Net.CanSendToHost)
            {
                Net.SendToHost(new CpAsk { kind = CpKind.PhoneAsk, i = up ? 1 : 2 });
            }
        }

        /// <summary>A short line when the phone key is pressed in class without a phone.</summary>
        public static void TipNoPhone()
        {
            if (Time.unscaledTime >= _nextTipAt)
            {
                _nextTipAt = Time.unscaledTime + 3f;
                Tricks.Tip("No phone in your hand. Phones turn up in desk drawers when you eat snacks.");
            }
        }

        // ---------------------------------------------------------------------- host side

        /// <summary>Host: a student finished a snack. Now and then a phone is in their drawer.</summary>
        public static void OnStudentAte(Unit student)
        {
            Settings settings = Plugin.Settings;
            if (!Game.InClass || student == null || !student.isActived || !student.isPlayer || !student.IsStudent
                || HostDrawer.Contains(student.UID) || HostHeld.ContainsKey(student.UID))
            {
                return;
            }
            float chance = settings.nerfAlwaysForTesting ? 1f : Mathf.Clamp01(settings.phoneChancePerSnack);
            if (Random.value >= chance)
            {
                return;
            }
            HostDrawer.Add(student.UID);
            Net.TellPlayer(student, new CpTell { kind = CpKind.PhoneTell, i = 0, j = 1 });
            Plugin.Log("phones: " + Poles.NameOf(student) + " has a phone in their drawer");
        }

        public static void OnAsk(Unit player, CpAsk ask)
        {
            if (!Game.InClass || player == null || !player.isActived || !player.IsStudent)
            {
                return;
            }
            bool up;
            switch (ask.i)
            {
                case 0:
                    if (HostDrawer.Remove(player.UID) && !HostHeld.ContainsKey(player.UID))
                    {
                        HostHeld[player.UID] = false;
                        Net.TellPlayer(player, new CpTell { kind = CpKind.PhoneTell, i = 0, j = 0 });
                        Net.SendToModded(new CpTell { kind = CpKind.PhoneTell, i = 1, a = player.UID, j = InHand });
                        Plugin.Log("phones: " + Poles.NameOf(player) + " took their phone");
                    }
                    break;
                case 1:
                case 2:
                    if (HostHeld.TryGetValue(player.UID, out up) && up != (ask.i == 1))
                    {
                        HostHeld[player.UID] = ask.i == 1;
                        Net.SendToModded(new CpTell { kind = CpKind.PhoneTell, i = 1, a = player.UID, j = ask.i == 1 ? Up : InHand });
                        if (ask.i == 1)
                        {
                            TellTeachers(player);
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Host: an app on this player's phone is about to do something (an order, a call, a
        /// trick). True, and the phone is used up, when they hold one; false when they don't.
        /// </summary>
        public static bool HostUse(Unit player)
        {
            if (player == null || !HostHeld.Remove(player.UID))
            {
                return false;
            }
            Net.SendToModded(new CpTell { kind = CpKind.PhoneTell, i = 1, a = player.UID, j = Gone });
            Net.Toast(player, "That used up your phone. Another one may turn up in your drawer.", 3f);
            Plugin.Log("phones: " + Poles.NameOf(player) + " used up their phone");
            return true;
        }

        private static void TellTeachers(Unit student)
        {
            float last;
            if (HostTeacherToldAt.TryGetValue(student.UID, out last) && Time.time - last < 15f)
            {
                return;
            }
            HostTeacherToldAt[student.UID] = Time.time;
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null)
            {
                return;
            }
            foreach (Unit player in new List<Unit>(units.playerList))
            {
                if (player != null && player.isActived && player.isPlayer && player.IsTeacher)
                {
                    Net.Toast(player, "Someone is on their phone! Listen for the scribbling.", 3f);
                }
            }
        }

        // ------------------------------------------------------------------- every player

        public static void OnTell(CpTell tell)
        {
            if (tell.i == 0)
            {
                // This player's own drawer.
                bool had = _drawerPhone;
                _drawerPhone = tell.j == 1;
                if (_drawerPhone && !had)
                {
                    Game.PlaySound(AudioEvents.sfx_GameAddItem_sfx);
                    Game.Notify("A phone turned up in your desk drawer! Grab it with the pick-up key, or "
                        + Plugin.Settings.UseKeyCode + " from your seat.", 6f);
                }
                if (!_drawerPhone)
                {
                    DropDrawerModel();
                }
                return;
            }
            Held held;
            if (!Holders.TryGetValue(tell.a, out held))
            {
                if (tell.j == Gone)
                {
                    return;
                }
                held = new Held();
                Holders[tell.a] = held;
            }
            held.State = Mathf.Clamp(tell.j, Gone, Up);
            Unit me = Game.LocalPlayer;
            bool mine = me != null && me.UID == tell.a;
            if (held.State == Gone)
            {
                if (held.Model != null)
                {
                    Object.Destroy(held.Model);
                }
                Holders.Remove(tell.a);
                return;
            }
            if (mine && tell.j == InHand && held.Model == null)
            {
                Game.PlaySound(AudioEvents.sfx_ItemPickup_sfx);
            }
            if (held.State == Up)
            {
                UnitManager units = Game.Peek<UnitManager>();
                Unit holder = units != null ? units.GetUnitByUID(tell.a) : null;
                if (holder != null)
                {
                    Game.PlaySoundAt(AudioEvents.sfx_StudentPhone_sfx, holder.Position);
                }
            }
        }

        public static void Tick()
        {
            if (!Game.InClass)
            {
                Clear();
                return;
            }
            AnimateDrawer();
            UnitManager units = Game.Peek<UnitManager>();
            Unit me = Game.LocalPlayer;
            if (me != null && me.isActived && me.IsSitting && _drawerPhone && !CursorGrab.Busy && !Game.IsTyping
                && Input.GetKeyDown(Plugin.Settings.UseKeyCode) && !NerfGuns.DrawerGunWaiting)
            {
                Take("use key");
            }
            // A phone that is up scribbles away where its holder is, for everybody nearby to hear.
            if (units == null)
            {
                return;
            }
            foreach (KeyValuePair<ulong, Held> pair in Holders)
            {
                if (pair.Value.State != Up || Time.time < pair.Value.NextSoundAt)
                {
                    continue;
                }
                pair.Value.NextSoundAt = Time.time + 1.1f;
                Unit holder = units.GetUnitByUID(pair.Key);
                if (holder != null && holder.isActived)
                {
                    Game.PlaySoundAt(AudioEvents.sfx_WriteTest_sfx, holder.Position);
                }
            }
        }

        private static void Clear()
        {
            if (Holders.Count == 0 && !_drawerPhone && HostDrawer.Count == 0 && HostHeld.Count == 0)
            {
                return;
            }
            foreach (Held held in Holders.Values)
            {
                if (held.Model != null)
                {
                    Object.Destroy(held.Model);
                }
            }
            Holders.Clear();
            _drawerPhone = false;
            DropDrawerModel();
            HostDrawer.Clear();
            HostHeld.Clear();
            HostTeacherToldAt.Clear();
        }

        private static void DropDrawerModel()
        {
            if (_drawerModel != null)
            {
                Object.Destroy(_drawerModel);
                _drawerModel = null;
            }
            _drawerWaiting = false;
            _drawerAimed = false;
        }

        private static GameObject Build()
        {
            GameObject model = Models.Build("phone");
            if (model != null)
            {
                return model;
            }
            var root = new GameObject("[ClassPack phone]");
            Shapes.Part(root.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.08f, 0.15f, 0.02f), new Color32(220, 40, 40, 255));
            return root;
        }

        /// <summary>The phone hovering over this player's open drawer, and whether they look at it.</summary>
        private static void AnimateDrawer()
        {
            _drawerWaiting = false;
            _drawerAimed = false;
            if (!_drawerPhone)
            {
                return;
            }
            Unit me = Game.LocalPlayer;
            _drawerWaiting = me != null && me.isActived && me.IsSitting;
            DrawerComponent drawer = NerfGuns.MyDrawer(me);
            bool open = drawer != null && drawer.IsActive && drawer.openState && drawer.owner != null && drawer.owner.transform != null;
            if (!open)
            {
                if (_drawerModel != null)
                {
                    _drawerModel.SetActive(false);
                }
                return;
            }
            if (_drawerModel == null)
            {
                _drawerModel = Build();
                _drawerModel.transform.localScale = Vector3.one * DrawerScale;
                MeshFilter filter = _drawerModel.GetComponent<MeshFilter>();
                _drawerMiddle = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.center : Vector3.zero;
            }
            Transform tray = drawer.owner.transform;
            // Beside a nerf gun waiting in the same drawer, not inside it.
            Vector3 middle = tray.position + Vector3.up * (0.22f + Mathf.Sin(Time.time * 2.2f) * 0.025f)
                + (NerfGuns.DrawerGunWaiting ? tray.right * 0.22f : Vector3.zero);
            Quaternion turn = Quaternion.Euler(-10f, Time.time * 50f, 0f);
            _drawerModel.SetActive(true);
            _drawerModel.transform.rotation = turn;
            _drawerModel.transform.position = middle - turn * (_drawerMiddle * DrawerScale);
            Camera camera = Camera.main;
            if (camera != null)
            {
                Vector3 to = middle - camera.transform.position;
                float along = Vector3.Dot(to, camera.transform.forward);
                float off = (to - camera.transform.forward * along).magnitude;
                _drawerAimed = _drawerWaiting && along > 0.05f && along < 4f && off < AimSlack;
            }
        }

        private static void Take(string how)
        {
            Plugin.Log("phones: asking for the phone in my drawer (" + how + ")");
            Net.SendToHost(new CpAsk { kind = CpKind.PhoneAsk, i = 0 });
        }

        /// <summary>The game's pick-up key went down: true when it took the phone over the drawer.</summary>
        public static bool TryTakeWithPickUpKey()
        {
            if (!_drawerAimed || !_drawerPhone || !Game.InClass || CursorGrab.Busy || !Net.CanSendToHost)
            {
                return false;
            }
            Unit me = Game.LocalPlayer;
            if (me == null || !me.isActived || me.IsStun || me.BodyActionsBlocked)
            {
                return false;
            }
            Take("pick-up key");
            return true;
        }

        /// <summary>
        /// Late in the frame, after the game has moved everybody: each phone in its holder's hand,
        /// or held up in front of the face while it is in use.
        /// </summary>
        public static void LateTick()
        {
            UnitManager units = Holders.Count > 0 && Game.InClass ? Game.Peek<UnitManager>() : null;
            if (units == null)
            {
                return;
            }
            Unit me = Game.LocalPlayer;
            foreach (KeyValuePair<ulong, Held> pair in Holders)
            {
                Held held = pair.Value;
                Unit holder = units.GetUnitByUID(pair.Key);
                Transform body = holder != null && holder.isActived ? holder.transform : null;
                // The player's own phone is on their screen while it is up, not in front of their eyes.
                bool show = body != null && !(holder == me && held.State == Up && !SelfView.Showing);
                if (!show)
                {
                    if (held.Model != null)
                    {
                        held.Model.SetActive(false);
                    }
                    continue;
                }
                if (held.Model == null)
                {
                    held.Model = Build();
                    held.Model.transform.localScale = Vector3.one * HeldScale;
                }
                held.Model.SetActive(true);
                Transform neck = holder.GetPartRoot(EUnitPartType.HeadPosition);
                Vector3 neckAt = neck != null ? neck.position : body.position + Vector3.up * 0.76f;
                if (held.State == Up)
                {
                    // Up in front of the face, screen to the eyes, tipped back a little: easy to spot.
                    held.Model.transform.position = neckAt + Vector3.up * 0.16f + body.forward * 0.3f + body.right * 0.03f;
                    held.Model.transform.rotation = body.rotation * Quaternion.Euler(-15f, 0f, 0f);
                }
                else
                {
                    // In the hand by the hip, like food.
                    held.Model.transform.position = neckAt + body.right * 0.2f - Vector3.up * 0.2f + body.forward * 0.22f;
                    held.Model.transform.rotation = body.rotation * Quaternion.Euler(20f, 0f, 0f);
                }
            }
        }

        /// <summary>The pick-up prompt over the drawer, and the hint line. Drawn in a 1280 by 720 frame.</summary>
        public static string DrawHud(float width, float height)
        {
            if (!Game.InClass)
            {
                return null;
            }
            Unit me = Game.LocalPlayer;
            if (me != null && me.IsTeacher && AnyUp())
            {
                return "Someone is on their phone: listen for the scribbling and catch them!";
            }
            if (HasLocal)
            {
                return "Phone in your hand     [" + Plugin.Settings.DoorDashKeyCode + "] use it once: DoorDash, a prank call or a trick";
            }
            if (!_drawerWaiting)
            {
                return null;
            }
            if (_drawerAimed && !CursorGrab.Busy)
            {
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
                var box = new Rect(width * 0.5f - 170f, height * 0.5f + 76f, 340f, 34f);
                Color old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(box, _white);
                GUI.color = old;
                GUI.Label(box, "[" + NerfGuns.PickUpKeyName() + "]  Pick up phone", _prompt);
            }
            return "A phone is in your desk.     [" + NerfGuns.PickUpKeyName() + " or " + Plugin.Settings.UseKeyCode + "] grab it";
        }

        private static bool AnyUp()
        {
            foreach (Held held in Holders.Values)
            {
                if (held.State == Up)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Self-test (host): puts a phone in this player's drawer.</summary>
        public static void TestGive(Unit student)
        {
            if (student != null && NetworkServer.active && !HostHeld.ContainsKey(student.UID))
            {
                HostDrawer.Add(student.UID);
                Net.TellPlayer(student, new CpTell { kind = CpKind.PhoneTell, i = 0, j = 1 });
            }
        }

        /// <summary>Self-test: takes the phone from the drawer as if the pick-up key was pressed.</summary>
        public static void TestTake()
        {
            Take("self-test");
        }
    }
}
