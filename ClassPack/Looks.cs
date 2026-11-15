using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClassPack
{
    /// <summary>One player's outfit: which parts, colours, extras and teacher type they picked.</summary>
    internal struct Look
    {
        // 0 means "whatever the game picked". Hair 5 is bald.
        public int Hair, Body, Cloth;
        // 0 means no colour change, otherwise a colour from Looks.TintNames.
        public int HairTint, ClothTint, BodyTint;
        // Hat is one pick. Face and Back are sets: one bit per item, so several can be worn.
        public int Hat, Face, Back;
        // Only matters while the player is the teacher. See the constants in Looks.
        public int Teacher;

        public int A => (Hair & 7) | (Body & 7) << 3 | (Cloth & 7) << 6 | (HairTint & 15) << 9 | (ClothTint & 15) << 13
            | (BodyTint & 15) << 17 | (Teacher & 7) << 21;

        public int B => (Hat & 63) | (Face & 63) << 6 | (Back & 63) << 12;

        public static Look From(int a, int b)
        {
            return new Look
            {
                Hair = Mathf.Min(a & 7, 5),
                Body = Mathf.Min((a >> 3) & 7, 4),
                Cloth = Mathf.Min((a >> 6) & 7, 4),
                HairTint = Mathf.Min((a >> 9) & 15, Looks.TintNames.Length - 1),
                ClothTint = Mathf.Min((a >> 13) & 15, Looks.TintNames.Length - 1),
                BodyTint = Mathf.Min((a >> 17) & 15, Looks.TintNames.Length - 1),
                Teacher = Mathf.Min((a >> 21) & 7, Looks.TeacherNames.Length - 1),
                Hat = Mathf.Min(b & 63, Looks.HatNames.Length - 1),
                Face = (b >> 6) & ((1 << (Looks.FaceNames.Length - 1)) - 1),
                Back = (b >> 12) & ((1 << (Looks.BackNames.Length - 1)) - 1)
            };
        }
    }

    /// <summary>The game just rebuilt a student's body, so the outfit has to go back on.</summary>
    [HarmonyLib.HarmonyPatch(typeof(Unit), nameof(Unit.RefreshBodyMesh))]
    internal static class BodyRefreshedPatch
    {
        private static void Postfix(Unit __instance)
        {
            try
            {
                Looks.OnGameRefreshed(__instance, true);
            }
            catch (Exception)
            {
                // Never let the outfit code break the game's own body set-up.
            }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Unit), nameof(Unit.SetPlayerLayer))]
    internal static class LayerSetPatch
    {
        private static void Postfix(Unit __instance)
        {
            try
            {
                Looks.OnGameRefreshed(__instance, false);
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>
    /// Idea 2 (customizable characters) and the looks of the new teachers.
    ///
    /// Students are built by the game from four hair, body and clothes pieces; the wardrobe
    /// lets a player mix them, go bald, recolour each piece and add a hat, a face extra and
    /// something on the back. Teachers are one fused model, so they get a colour, the extras
    /// and a teacher type (gym teacher, lunch lady, janitor, bald with a wig).
    ///
    /// Every player's pick goes to the host, who passes it on, so everybody with the mod sees
    /// the same thing. Hats and other extras are separate models that follow the head.
    /// </summary>
    internal static class Looks
    {
        public const int Gym = 1;
        public const int Lunch = 2;
        public const int Janitor = 3;
        public const int Wig = 4;

        public static readonly string[] HairNames = { "Game's pick", "1", "2", "3", "4", "Bald" };
        public static readonly string[] PartNames = { "Game's pick", "1", "2", "3", "4" };
        public static readonly string[] TintNames =
        {
            "None", "Red", "Orange", "Yellow", "Green", "Blue", "Purple", "Pink", "White", "Black", "Gold"
        };
        public static readonly string[] HatNames =
        {
            "None", "Cap", "Beanie", "Top hat", "Crown", "Cowboy hat", "Party hat", "Traffic cone", "Chef hat"
        };
        public static readonly string[] FaceNames = { "None", "Sunglasses", "Clown nose", "Moustache" };
        public static readonly string[] BackNames = { "None", "Backpack", "Cape", "Wings" };
        public static readonly string[] TeacherNames = { "Normal", "Gym teacher", "Lunch lady", "Janitor", "Bald with a wig" };

        private static readonly string[] HatModels =
        {
            "", "hat_cap", "hat_beanie", "hat_top", "hat_crown", "hat_cowboy", "hat_party", "hat_cone", "hat_chef"
        };
        private static readonly string[] FaceModels = { "", "face_shades", "face_nose", "face_moustache" };
        private static readonly string[] BackModels = { "", "back_pack", "back_cape", "back_wings" };

        private static readonly Color32[] Tints =
        {
            new Color32(255, 255, 255, 255), new Color32(230, 60, 55, 255), new Color32(245, 140, 40, 255),
            new Color32(250, 215, 60, 255), new Color32(70, 190, 90, 255), new Color32(70, 130, 240, 255),
            new Color32(150, 90, 220, 255), new Color32(245, 130, 185, 255), new Color32(255, 255, 255, 255),
            new Color32(45, 45, 52, 255), new Color32(235, 190, 70, 255)
        };

        private const int Head = 0;
        private const int OnBack = 1;
        private const int Front = 2;
        private const int InHand = 3;

        private sealed class Piece
        {
            public string Name;
            public int Anchor;
            public GameObject Model;
        }

        private sealed class Worn
        {
            public Look Look;
            public bool Dirty = true;
            public int ModelId;
            public bool WasLocal;
            public bool WasPreview;
            public bool WasTeacher;
            public bool PartsTouched;
            public Transform ScaledRoot;
            public Vector3 BaseScale;
            // A new teacher's whole body, shown in place of the game's teacher.
            public CharacterView Body;
            public readonly List<Renderer> HiddenGame = new List<Renderer>();
            public float NeckMax;
            public float Lift;
            public Vector3 LastAt;
            public float Speed;
            public Transform HeadBone;
            public int UpAxis = -1;
            public int ForwardAxis;
            public float UpSign;
            public float ForwardSign;
            public readonly List<Piece> Pieces = new List<Piece>();
            public readonly HashSet<Renderer> Tinted = new HashSet<Renderer>();
        }

        private static readonly Dictionary<ulong, Worn> Worns = new Dictionary<ulong, Worn>();
        private static readonly Dictionary<ulong, int> GameIndex = new Dictionary<ulong, int>();
        private static readonly Dictionary<Shader, int> ColorProps = new Dictionary<Shader, int>();
        private static readonly List<SkinnedMeshRenderer> PreviewFlipped = new List<SkinnedMeshRenderer>();
        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        private static readonly Dictionary<ulong, long> HostLooks = new Dictionary<ulong, long>();

        private static ulong _sentUid;
        private static int _sentA = -1;
        private static int _sentB = -1;
        private static NetworkConnection _sentTo;
        private static float _nextSendAt;
        private static bool _loggedAnchors;
        private static bool _loggedTeacher;

        /// <summary>This player's own pick, kept in the settings file.</summary>
        public static Look Mine
        {
            get { return Look.From(Plugin.Settings.lookA, Plugin.Settings.lookB); }
            set
            {
                Plugin.Settings.lookA = value.A;
                Plugin.Settings.lookB = value.B;
                Plugin.Settings.Save();
            }
        }

        /// <summary>The teacher type a player picked, as everybody in the room knows it.</summary>
        /// <summary>True for a teacher shown as one of the mod's whole characters, not the game's own body.</summary>
        public static bool HasOwnBody(ulong uid)
        {
            Worn worn;
            return Worns.TryGetValue(uid, out worn) && worn.Body != null;
        }

        /// <summary>
        /// Late in the frame, after the swimming move has posed the swimmers: put what they wear
        /// back on their heads, which are somewhere else now.
        /// </summary>
        public static void LateTick()
        {
            UnitManager units = Game.Peek<UnitManager>();
            if ((!Swim.Active && !Dance.Active) || units == null || !Plugin.Settings.looks)
            {
                return;
            }
            foreach (KeyValuePair<ulong, Worn> pair in Worns)
            {
                Worn worn = pair.Value;
                if (worn.Pieces.Count == 0 || worn.Body != null)
                {
                    continue;
                }
                Unit unit = units.GetUnitByUID(pair.Key);
                if (unit != null && unit.isActived && unit.model != null && unit.transform != null
                    && (!unit.isLocalPlayer || SelfView.Showing) && (Swim.Swimming(unit) || Dance.IsDancing(unit)))
                {
                    Place(unit, worn, true);
                }
            }
        }

        public static int KindOf(ulong uid)
        {
            Worn worn;
            return Worns.TryGetValue(uid, out worn) ? worn.Look.Teacher : 0;
        }

        /// <summary>Makes the outfit go back on at the next frame.</summary>
        public static void Touch(ulong uid)
        {
            Worn worn;
            if (Worns.TryGetValue(uid, out worn))
            {
                worn.Dirty = true;
            }
        }

        public static void TouchAll()
        {
            foreach (Worn worn in Worns.Values)
            {
                worn.Dirty = true;
            }
        }

        public static void OnGameRefreshed(Unit unit, bool bodyRebuilt)
        {
            if (unit == null)
            {
                return;
            }
            if (bodyRebuilt)
            {
                // Remember which of the four bodies the game itself chose for this student.
                Transform root = unit.GetPartRoot(EUnitPartType.RenderRoot);
                if (root != null)
                {
                    for (int i = 1; i <= 4; i++)
                    {
                        Transform body = root.Find("Body" + i);
                        if (body != null && body.gameObject.activeSelf)
                        {
                            GameIndex[unit.UID] = i;
                            break;
                        }
                    }
                }
            }
            Touch(unit.UID);
        }

        // --------------------------------------------------------------------- every frame

        public static void Tick()
        {
            if (!NetworkClient.active && !NetworkServer.active)
            {
                if (Worns.Count > 0 || HostLooks.Count > 0)
                {
                    foreach (Worn worn in Worns.Values)
                    {
                        Strip(worn);
                    }
                    Worns.Clear();
                    GameIndex.Clear();
                    HostLooks.Clear();
                    PreviewFlipped.Clear();
                }
                _sentUid = 0uL;
                _sentTo = null;
                return;
            }
            if (!NetworkServer.active && HostLooks.Count > 0)
            {
                HostLooks.Clear();
            }
            SendMine();

            UnitManager units = Game.Peek<UnitManager>();
            if (units == null || !Plugin.Settings.looks)
            {
                return;
            }
            // While a player watches themselves swim, their own body has to be drawn like in the
            // wardrobe's picture, also when they wear nothing from the mod.
            Unit me = units.localPlayer;
            if (SelfView.Showing && me != null && !Worns.ContainsKey(me.UID))
            {
                Worns[me.UID] = new Worn();
            }
            foreach (KeyValuePair<ulong, Worn> pair in Worns)
            {
                Worn worn = pair.Value;
                Unit unit = units.GetUnitByUID(pair.Key);
                if (unit == null || !unit.isActived || unit.model == null || unit.transform == null)
                {
                    if (worn.Pieces.Count > 0 || worn.Body != null)
                    {
                        DropPieces(worn);
                    }
                    worn.Dirty = true;
                    continue;
                }
                bool local = unit.isLocalPlayer;
                bool preview = local && (Wardrobe.IsOpen || SelfView.Showing);
                int modelId = unit.model.GetInstanceID();
                if (worn.Dirty || modelId != worn.ModelId || local != worn.WasLocal || preview != worn.WasPreview
                    || unit.IsTeacher != worn.WasTeacher)
                {
                    worn.Dirty = false;
                    worn.ModelId = modelId;
                    worn.WasLocal = local;
                    worn.WasPreview = preview;
                    worn.WasTeacher = unit.IsTeacher;
                    Apply(unit, worn, local, preview);
                }
                Place(unit, worn);
            }
        }

        private static void SendMine()
        {
            Unit me = Game.LocalPlayer;
            if (me == null || !Net.CanSendToHost)
            {
                _sentUid = 0uL;
                return;
            }
            Look mine = Mine;
            NetworkConnection connection = NetworkClient.connection;
            if (me.UID == _sentUid && mine.A == _sentA && mine.B == _sentB && connection == _sentTo)
            {
                return;
            }
            if (Time.unscaledTime < _nextSendAt)
            {
                return;
            }
            _nextSendAt = Time.unscaledTime + 0.4f;
            _sentUid = me.UID;
            _sentA = mine.A;
            _sentB = mine.B;
            _sentTo = connection;
            Net.SendToHost(new CpAsk { kind = CpKind.LookSet, i = mine.A, t0 = (ulong)mine.B });
        }

        // ------------------------------------------------------------------------- network

        public static void OnAsk(Unit player, CpAsk ask)
        {
            if (player == null)
            {
                return;
            }
            int b = (int)(ask.t0 & 0xFFFFFFuL);
            HostLooks[player.UID] = ((long)ask.i << 32) | (uint)b;
            Net.SendToModded(new CpTell { kind = CpKind.Look, a = player.UID, i = ask.i, j = b });
        }

        /// <summary>Host: a player with the mod just joined and needs everybody's outfit.</summary>
        public static void OnPeerJoined(NetworkConnectionToClient connection)
        {
            foreach (KeyValuePair<ulong, long> pair in HostLooks)
            {
                Net.SendTo(connection, new CpTell
                {
                    kind = CpKind.Look, a = pair.Key, i = (int)(pair.Value >> 32), j = (int)(pair.Value & 0xFFFFFFFFL)
                });
            }
        }

        public static void OnTell(CpTell tell)
        {
            Worn worn;
            if (!Worns.TryGetValue(tell.a, out worn))
            {
                worn = new Worn();
                Worns[tell.a] = worn;
            }
            worn.Look = Look.From(tell.i, tell.j);
            worn.Dirty = true;
        }

        // ------------------------------------------------------------------- putting it on

        private static void Strip(Worn worn)
        {
            DropPieces(worn);
            if (worn.ScaledRoot != null)
            {
                worn.ScaledRoot.localScale = worn.BaseScale;
            }
            worn.ScaledRoot = null;
            foreach (Renderer renderer in worn.Tinted)
            {
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(null);
                }
            }
            worn.Tinted.Clear();
        }

        private static void DropPieces(Worn worn)
        {
            foreach (Piece piece in worn.Pieces)
            {
                if (piece.Model != null)
                {
                    UnityEngine.Object.Destroy(piece.Model);
                }
            }
            worn.Pieces.Clear();
            if (worn.Body != null)
            {
                worn.Body.Destroy();
                worn.Body = null;
            }
            foreach (Renderer hidden in worn.HiddenGame)
            {
                if (hidden != null)
                {
                    hidden.forceRenderingOff = false;
                }
            }
            worn.HiddenGame.Clear();
        }

        private static void Add(Worn worn, string name, int anchor, int layer)
        {
            GameObject model = Models.Build(name);
            if (model == null)
            {
                return;
            }
            model.layer = layer;
            worn.Pieces.Add(new Piece { Name = name, Anchor = anchor, Model = model });
        }

        private static void Apply(Unit unit, Worn worn, bool local, bool preview)
        {
            DropPieces(worn);
            if (worn.ScaledRoot != null)
            {
                worn.ScaledRoot.localScale = worn.BaseScale;
                worn.ScaledRoot = null;
            }
            Look look = worn.Look;
            Transform root = unit.GetPartRoot(EUnitPartType.RenderRoot);
            if (root == null && unit.IsTeacher && unit.model != null)
            {
                // The game's teacher has no such part: its whole model is the body.
                root = unit.model.transform;
            }
            int selfLayer = LayerMask.NameToLayer("PlayerSelf");
            int otherLayer = LayerMask.NameToLayer("Player");

            if (root != null && unit.IsStudent)
            {
                StudentParts(unit, worn, root, look, local, preview, local ? selfLayer : otherLayer);
            }
            if (root != null)
            {
                Paint(unit, worn, root, look);
            }

            // The player's own body is hidden from their own view by the game (it only casts a
            // shadow). While the wardrobe is open it has to be drawn for the preview picture.
            if (local && root != null)
            {
                if (preview)
                {
                    foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                    {
                        if (renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly)
                        {
                            renderer.shadowCastingMode = ShadowCastingMode.On;
                            PreviewFlipped.Add(renderer);
                        }
                    }
                }
                else if (PreviewFlipped.Count > 0)
                {
                    foreach (SkinnedMeshRenderer renderer in PreviewFlipped)
                    {
                        if (renderer != null)
                        {
                            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                        }
                    }
                    PreviewFlipped.Clear();
                }
            }

            // Extras are their own models. On the player's own body they sit on the layer the
            // player's camera skips, so a hat never blocks the view.
            int layer = local && selfLayer >= 0 ? selfLayer : 0;
            int teacher = unit.IsTeacher && Plugin.Settings.teachers ? look.Teacher : 0;

            // The gym teacher, lunch lady and janitor are whole characters of their own. The
            // game's teacher keeps running underneath, unseen, and the new body follows it.
            string bodyName = teacher == Gym ? "teacher_gym" : teacher == Lunch ? "teacher_lunch" : teacher == Janitor ? "teacher_janitor" : null;
            worn.NeckMax = 0f;
            worn.Speed = 0f;
            worn.LastAt = unit.transform.position;
            if (bodyName != null)
            {
                worn.Body = Characters.Create(bodyName);
            }
            if (worn.Body != null)
            {
                if (root != null)
                {
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        // Only the teacher's own skin, not something another unit hangs on it.
                        if (!renderer.forceRenderingOff && renderer is SkinnedMeshRenderer
                            && renderer.GetComponentInParent<ObjectModel>() == unit.model)
                        {
                            renderer.forceRenderingOff = true;
                            worn.HiddenGame.Add(renderer);
                        }
                    }
                }
                worn.Body.Root.layer = layer;
                worn.Body.Renderer.gameObject.layer = layer;
                // Like the game's own body: the player does not see it, but it casts a shadow
                // and shows in the wardrobe picture.
                worn.Body.Renderer.shadowCastingMode = local && !preview ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            }
            switch (worn.Body != null ? -1 : teacher)
            {
                case -1:
                    // A whole body needs none of the single pieces.
                    break;
                case Gym:
                    // The bald cap covers the top of the head and leaves the teacher's own hair
                    // showing around the sides: a monk cut.
                    Add(worn, "hat_baldcap", Head, layer);
                    Add(worn, "body_belly", Front, layer);
                    if (root != null)
                    {
                        worn.ScaledRoot = root;
                        worn.BaseScale = root.localScale;
                        root.localScale = new Vector3(worn.BaseScale.x * 1.16f, worn.BaseScale.y, worn.BaseScale.z * 1.16f);
                    }
                    break;
                case Lunch:
                    Add(worn, "hat_hairnet", Head, layer);
                    Add(worn, "body_apron", Front, layer);
                    Add(worn, "hand_ladle", InHand, layer);
                    break;
                case Janitor:
                    Add(worn, "hat_janitor", Head, layer);
                    Add(worn, "hand_mop", InHand, layer);
                    break;
                case Wig:
                    Add(worn, "hat_baldcap", Head, layer);
                    Add(worn, "hat_wig", Head, layer);
                    break;
                default:
                    if (look.Hat > 0)
                    {
                        Add(worn, HatModels[look.Hat], Head, layer);
                    }
                    break;
            }
            for (int i = 1; worn.Body == null && i < FaceModels.Length; i++)
            {
                if ((look.Face & (1 << (i - 1))) != 0)
                {
                    Add(worn, FaceModels[i], Head, layer);
                }
            }
            for (int i = 1; worn.Body == null && i < BackModels.Length; i++)
            {
                if ((look.Back & (1 << (i - 1))) != 0)
                {
                    Add(worn, BackModels[i], OnBack, layer);
                }
            }

            // What a player sees of their own outfit while playing: a student sees what is on
            // their body but not what is on their head (a hat or glasses would be in the way),
            // and a teacher sees none of it. Hidden pieces still cast a shadow, and everything
            // shows in the wardrobe picture.
            foreach (Piece piece in worn.Pieces)
            {
                bool hideFromSelf = local && !preview && (piece.Anchor == Head || unit.IsTeacher);
                foreach (Renderer renderer in piece.Model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = hideFromSelf ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.Off;
                }
            }

            // The head bone is looked up again for a new body.
            worn.HeadBone = null;
            worn.UpAxis = -1;

            // Once for a student body and once for a teacher body: what the game's anchors are.
            if (local && (unit.IsTeacher ? !_loggedTeacher : !_loggedAnchors))
            {
                if (unit.IsTeacher)
                {
                    _loggedTeacher = true;
                }
                else
                {
                    _loggedAnchors = true;
                }
                Transform head = unit.GetPartRoot(EUnitPartType.HeadPosition);
                Transform bone = head != null ? FindHeadBone(head) : null;
                Transform hand = unit.GetPartRoot(EUnitPartType.HandPosition);
                Plugin.Log("looks: my " + (unit.IsTeacher ? "teacher" : "student") + " body root is "
                    + (root != null ? root.name : "missing") + ", head anchor "
                    + (head != null ? head.name + " at height " + (head.position.y - unit.transform.position.y).ToString("F2") : "missing")
                    + ", head bone " + (bone != null ? bone.name : "none")
                    + ", hand " + (hand != null ? hand.name + " at height " + (hand.position.y - unit.transform.position.y).ToString("F2") : "missing")
                    + ", layers self " + selfLayer + " other " + otherLayer);
            }
        }

        private static void StudentParts(Unit unit, Worn worn, Transform root, Look look, bool local, bool preview, int layer)
        {
            bool wantsParts = look.Hair != 0 || look.Body != 0 || look.Cloth != 0;
            if (!wantsParts && !worn.PartsTouched)
            {
                return;
            }
            // Two floors dress everybody in a set outfit. The game's rule wins there.
            ClassroomManager classroom = Game.Peek<ClassroomManager>();
            if (classroom != null && (classroom.currentFloor == 6 || classroom.currentFloor == 8)
                && classroom.curState != EClassroomState.BeforeClass)
            {
                return;
            }
            int game;
            if (!GameIndex.TryGetValue(unit.UID, out game) || game < 1 || game > 4)
            {
                RoleData data = unit.GetData<RoleData>();
                game = data != null ? data.NetData.BodyMeshIndex : 0;
                if (game < 1 || game > 4)
                {
                    game = 1;
                }
            }
            int hair = look.Hair == 0 ? game : look.Hair;
            int body = look.Body == 0 ? game : look.Body;
            int cloth = look.Cloth == 0 ? game : look.Cloth;
            Transform gameHand = root.Find("Hand" + game);
            int handLayer = gameHand != null ? gameHand.gameObject.layer : layer;
            for (int i = 1; i <= 4; i++)
            {
                Part(root.Find("Hair" + i), i == hair, layer, true, local, preview);
                Part(root.Find("Body" + i), i == body, layer, true, local, preview);
                Part(root.Find("Cloth" + i), i == cloth, layer, true, local, preview);
                // Hands are what a player sees of their own body, so they keep the game's layer.
                Part(root.Find("Hand" + i), i == body, handLayer, false, local, preview);
            }
            worn.PartsTouched = wantsParts;
        }

        private static void Part(Transform part, bool on, int layer, bool hideFromSelf, bool local, bool preview)
        {
            if (part == null)
            {
                return;
            }
            if (part.gameObject.activeSelf != on)
            {
                part.gameObject.SetActive(on);
            }
            if (!on || layer < 0)
            {
                return;
            }
            SetLayer(part, layer);
            if (hideFromSelf)
            {
                foreach (SkinnedMeshRenderer renderer in part.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    renderer.shadowCastingMode = local && !preview ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
                }
            }
        }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayer(root.GetChild(i), layer);
            }
        }

        private static void Paint(Unit unit, Worn worn, Transform root, Look look)
        {
            if (unit.IsStudent)
            {
                for (int i = 1; i <= 4; i++)
                {
                    PaintPart(worn, root.Find("Hair" + i), look.HairTint);
                    PaintPart(worn, root.Find("Cloth" + i), look.ClothTint);
                    PaintPart(worn, root.Find("Body" + i), look.BodyTint);
                    PaintPart(worn, root.Find("Hand" + i), look.BodyTint);
                }
            }
            else
            {
                // A teacher is one piece, so one colour covers all of it.
                PaintPart(worn, root, look.ClothTint != 0 ? look.ClothTint : look.BodyTint);
            }
        }

        private static void PaintPart(Worn worn, Transform part, int tint)
        {
            if (part == null)
            {
                return;
            }
            foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer))
                {
                    continue;
                }
                if (tint <= 0)
                {
                    if (worn.Tinted.Remove(renderer))
                    {
                        renderer.SetPropertyBlock(null);
                    }
                    continue;
                }
                Material material = renderer.sharedMaterial;
                int property = material != null ? ColorProperty(material.shader) : 0;
                if (property == 0)
                {
                    continue;
                }
                Block.Clear();
                Block.SetColor(property, Tints[tint]);
                renderer.SetPropertyBlock(Block);
                worn.Tinted.Add(renderer);
            }
        }

        /// <summary>The colour setting a character's material answers to. Differs per shader.</summary>
        private static int ColorProperty(Shader shader)
        {
            int id;
            if (shader == null)
            {
                return 0;
            }
            if (ColorProps.TryGetValue(shader, out id))
            {
                return id;
            }
            string chosen = null;
            var colors = new List<string>();
            int count = shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                if (shader.GetPropertyType(i) == ShaderPropertyType.Color)
                {
                    colors.Add(shader.GetPropertyName(i));
                }
            }
            foreach (string wanted in new[] { "_BaseColor", "_Color", "_MainColor", "_TintColor", "_Tint", "_BaseTint" })
            {
                if (colors.Contains(wanted))
                {
                    chosen = wanted;
                    break;
                }
            }
            if (chosen == null)
            {
                foreach (string name in colors)
                {
                    string lower = name.ToLowerInvariant();
                    if (lower.Contains("tint") || (lower.Contains("color") && !lower.Contains("outline")
                        && !lower.Contains("shadow") && !lower.Contains("emiss") && !lower.Contains("spec") && !lower.Contains("rim")))
                    {
                        chosen = name;
                        break;
                    }
                }
            }
            id = chosen != null ? Shader.PropertyToID(chosen) : 0;
            ColorProps[shader] = id;
            Plugin.Log("looks: shader '" + shader.name + "' is coloured through " + (chosen ?? "nothing")
                + " (colour settings: " + string.Join(", ", colors.ToArray()) + ")");
            return id;
        }

        // ------------------------------------------------------------------ following along

        /// <summary>Keeps a whole-body character on the game's teacher and picks its move from how fast it goes.</summary>
        private static void PlaceBody(Unit unit, Worn worn)
        {
            Transform body = unit.transform;
            Transform neck = unit.GetPartRoot(EUnitPartType.HeadPosition);
            if (neck != null)
            {
                // The tallest the neck has been is the standing height.
                worn.NeckMax = Mathf.Max(worn.NeckMax, neck.position.y - body.position.y);
            }
            float tall = worn.NeckMax > 0.3f ? Mathf.Clamp(worn.NeckMax + 0.28f, 1.0f, 2.4f) : 1.7f;
            Transform character = worn.Body.Root.transform;
            character.rotation = Quaternion.Euler(0f, body.eulerAngles.y, 0f);
            character.localScale = Vector3.one * tall;

            Vector3 moved = body.position - worn.LastAt;
            moved.y = 0f;
            worn.LastAt = body.position;
            if (Time.deltaTime > 0f)
            {
                worn.Speed = Mathf.Lerp(worn.Speed, moved.magnitude / Time.deltaTime, 0.15f);
            }
            string move = worn.Speed < 0.25f ? "idle" : worn.Speed < 3.4f ? "walk" : "run";
            float lift = 0f;
            if (Swim.Swimming(unit) && worn.Body.Has("swim") && worn.Body.Has("swimidle"))
            {
                // In the pool: strokes while moving, treading water while still, floating at the surface.
                // (The self-test makes its own player stroke while standing still.)
                bool still = worn.Speed < 0.3f && !(SwimPose.ForceMoving && unit == Game.LocalPlayer);
                move = still ? "swimidle" : "swim";
                lift = Swim.WaterWorldY - SwimPose.SurfaceFor(still) * tall - body.position.y;
            }
            worn.Lift = Mathf.MoveTowards(worn.Lift, lift, Time.deltaTime * 2.5f);
            character.position = body.position + Vector3.up * worn.Lift;
            worn.Body.Play(move);
            worn.Body.Tick(Time.deltaTime);
        }

        private static void Place(Unit unit, Worn worn, bool late = false)
        {
            if (worn.Body != null && !late)
            {
                PlaceBody(unit, worn);
            }
            if (worn.Pieces.Count == 0)
            {
                return;
            }
            // The game's head point is the neck, 0.76 m up on a student. Everything worn is
            // placed from there: the head's middle is a hand's width above it, the back and the
            // belly a little below it.
            Transform body = unit.transform;
            Transform neck = unit.GetPartRoot(EUnitPartType.HeadPosition);
            Vector3 neckAt = neck != null ? neck.position : body.position + Vector3.up * 0.76f;
            Quaternion yaw = Quaternion.Euler(0f, body.eulerAngles.y, 0f);
            Settings fit = Plugin.Settings;
            float size = Mathf.Clamp(fit.headSize, 0.3f, 4f);

            // Hats turn and tilt with the head once it is known which way the head bone's own
            // axes point. That is worked out while the head is upright and facing forward.
            Quaternion headTurn = yaw;
            if (neck != null)
            {
                if (worn.HeadBone == null)
                {
                    worn.HeadBone = FindHeadBone(neck) ?? neck;
                    worn.UpAxis = -1;
                }
                if (worn.UpAxis < 0)
                {
                    Calibrate(worn, yaw * Vector3.forward);
                }
                if (worn.UpAxis >= 0)
                {
                    Vector3 up = Axis(worn.HeadBone, worn.UpAxis) * worn.UpSign;
                    Vector3 forward = Axis(worn.HeadBone, worn.ForwardAxis) * worn.ForwardSign;
                    if (up.sqrMagnitude > 0.5f && forward.sqrMagnitude > 0.5f)
                    {
                        headTurn = Quaternion.LookRotation(forward, up);
                    }
                }
            }
            Vector3 headMiddle = neckAt + headTurn * new Vector3(0f, fit.headUp, fit.headForward);
            Vector3 headAt = neckAt;
            bool wigOff = Teachers.WigIsOff(unit.UID);
            foreach (Piece piece in worn.Pieces)
            {
                if (piece.Model == null)
                {
                    continue;
                }
                Transform model = piece.Model.transform;
                if (piece.Name == "hat_wig")
                {
                    if (piece.Model.activeSelf == wigOff)
                    {
                        piece.Model.SetActive(!wigOff);
                    }
                }
                model.rotation = yaw;
                switch (piece.Anchor)
                {
                    case Head:
                        model.position = headMiddle;
                        model.localScale = Vector3.one * size;
                        model.rotation = headTurn;
                        break;
                    case OnBack:
                        model.position = headAt + yaw * new Vector3(0f, fit.bodyUp - 0.13f, -fit.bodyOut);
                        break;
                    case Front:
                        model.position = headAt + yaw * new Vector3(0f, fit.bodyUp - 0.24f, fit.bodyOut - 0.02f);
                        break;
                    default:
                        Transform hand = unit.GetPartRoot(EUnitPartType.HandPosition);
                        model.position = hand != null ? hand.position : body.position + yaw * new Vector3(0.2f, 0.45f, 0.15f);
                        break;
                }
            }
        }

        private static Vector3 Axis(Transform bone, int axis)
        {
            return axis == 0 ? bone.right : axis == 1 ? bone.up : bone.forward;
        }

        /// <summary>The bone that turns with the head: the first one below the neck with "head" in its name.</summary>
        private static Transform FindHeadBone(Transform neck)
        {
            var queue = new Queue<Transform>();
            queue.Enqueue(neck);
            int looked = 0;
            while (queue.Count > 0 && looked < 40)
            {
                Transform bone = queue.Dequeue();
                looked++;
                if (bone != neck && bone.name.ToLowerInvariant().Contains("head"))
                {
                    return bone;
                }
                for (int i = 0; i < bone.childCount; i++)
                {
                    queue.Enqueue(bone.GetChild(i));
                }
            }
            return null;
        }

        /// <summary>
        /// Finds which of the head bone's own axes point up and forward. Only decided while the
        /// head is close to upright and facing the way the body faces; until then hats just
        /// follow the body.
        /// </summary>
        private static void Calibrate(Worn worn, Vector3 bodyForward)
        {
            Transform bone = worn.HeadBone;
            int upAxis = -1;
            float upBest = 0.85f;
            float upSign = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                float dot = Vector3.Dot(Axis(bone, axis), Vector3.up);
                if (Mathf.Abs(dot) > upBest)
                {
                    upBest = Mathf.Abs(dot);
                    upAxis = axis;
                    upSign = Mathf.Sign(dot);
                }
            }
            if (upAxis < 0)
            {
                return;
            }
            int forwardAxis = -1;
            float forwardBest = 0.8f;
            float forwardSign = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                float dot = Vector3.Dot(Axis(bone, axis), bodyForward);
                if (axis != upAxis && Mathf.Abs(dot) > forwardBest)
                {
                    forwardBest = Mathf.Abs(dot);
                    forwardAxis = axis;
                    forwardSign = Mathf.Sign(dot);
                }
            }
            if (forwardAxis < 0)
            {
                return;
            }
            worn.UpAxis = upAxis;
            worn.UpSign = upSign;
            worn.ForwardAxis = forwardAxis;
            worn.ForwardSign = forwardSign;
        }
    }
}
