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
        // A hairstyle model worn instead of the game's hair (0 none, see Looks.HairstyleNames),
        // and the face: eye, pupil and head size as steps (0 is normal size, see Size).
        public int Hairstyle, Eyes, Pupils, HeadSize;

        public int A => (Hair & 7) | (Body & 7) << 3 | (Cloth & 7) << 6 | (HairTint & 15) << 9 | (ClothTint & 15) << 13
            | (BodyTint & 15) << 17 | (Teacher & 7) << 21;

        public int B => (Hat & 63) | (Face & 63) << 6 | (Back & 63) << 12;

        // Small enough to travel exactly in a float of the network message.
        public int C => (Hairstyle & 7) | (Eyes & 31) << 3 | (Pupils & 31) << 8 | (HeadSize & 31) << 13;

        public float EyeScale => Size(Eyes);
        public float PupilScale => Size(Pupils);
        public float HeadScale => Size(HeadSize);

        /// <summary>A size step as a scale: 0 is normal (1.0), then 1 is 0.5 and every step adds 0.1.</summary>
        public static float Size(int step)
        {
            return step <= 0 ? 1f : 0.5f + (step - 1) * 0.1f;
        }

        /// <summary>The step for a scale; 0 when it is (about) normal.</summary>
        public static int Step(float scale)
        {
            if (Mathf.Abs(scale - 1f) < 0.05f)
            {
                return 0;
            }
            return Mathf.Clamp(Mathf.RoundToInt((scale - 0.5f) / 0.1f) + 1, 1, 31);
        }

        public static Look From(int a, int b, int c)
        {
            Look look = From(a, b);
            look.Hairstyle = Mathf.Min(c & 7, Looks.HairstyleNames.Length - 1);
            look.Eyes = (c >> 3) & 31;
            look.Pupils = (c >> 8) & 31;
            look.HeadSize = (c >> 13) & 31;
            return look;
        }

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
        public static readonly string[] HairstyleNames = { "None", "Afro", "Dreads" };
        private static readonly string[] HairstyleModels = { "", "hair_afro", "hair_dreads" };
        /// <summary>The afro's and dreads' colour when no hair colour is picked: a natural dark brown.</summary>
        private static readonly Color32 NaturalHair = new Color32(78, 54, 40, 255);

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
            // The face sliders' bones on this body (see FaceTick).
            public FaceRig Face;
        }

        private static readonly Dictionary<ulong, Worn> Worns = new Dictionary<ulong, Worn>();
        private static readonly Dictionary<ulong, int> GameIndex = new Dictionary<ulong, int>();
        private static readonly Dictionary<Shader, int> ColorProps = new Dictionary<Shader, int>();
        private static readonly List<SkinnedMeshRenderer> PreviewFlipped = new List<SkinnedMeshRenderer>();
        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        private static readonly Dictionary<ulong, long> HostLooks = new Dictionary<ulong, long>();
        /// <summary>Host: everybody's third look number (hairstyle and face sizes), next to HostLooks.</summary>
        private static readonly Dictionary<ulong, int> HostLooksC = new Dictionary<ulong, int>();
        /// <summary>Host: which player each bot was dressed like.</summary>
        private static readonly Dictionary<ulong, ulong> BotSource = new Dictionary<ulong, ulong>();
        private static float _nextBotDressAt;
        private static int _sentC = -1;

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
            get { return Look.From(Plugin.Settings.lookA, Plugin.Settings.lookB, Plugin.Settings.lookC); }
            set
            {
                Plugin.Settings.lookA = value.A;
                Plugin.Settings.lookB = value.B;
                Plugin.Settings.lookC = value.C;
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
                    HostLooksC.Clear();
                    BotSource.Clear();
                    PreviewFlipped.Clear();
                }
                _sentUid = 0uL;
                _sentTo = null;
                return;
            }
            if (!NetworkServer.active && HostLooks.Count > 0)
            {
                HostLooks.Clear();
                HostLooksC.Clear();
                BotSource.Clear();
            }
            SendMine();

            UnitManager units = Game.Peek<UnitManager>();
            if (units == null || !Plugin.Settings.looks)
            {
                return;
            }
            DressBots(units);
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
            if (me.UID == _sentUid && mine.A == _sentA && mine.B == _sentB && mine.C == _sentC && connection == _sentTo)
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
            _sentC = mine.C;
            _sentTo = connection;
            Net.SendToHost(new CpAsk { kind = CpKind.LookSet, i = mine.A, t0 = (ulong)mine.B, t1 = (ulong)mine.C });
        }

        /// <summary>
        /// Host: every bot is dressed exactly like one of the players, picked at random. A
        /// player's "game's pick" parts are the parts the game gave that player, so the copy is
        /// exact. A player who changes outfit changes their copies too (see OnAsk).
        /// </summary>
        private static void DressBots(UnitManager units)
        {
            if (!NetworkServer.active || !Plugin.Settings.botsCopyPlayers || Time.unscaledTime < _nextBotDressAt)
            {
                return;
            }
            _nextBotDressAt = Time.unscaledTime + 1f;
            var players = new List<Unit>();
            foreach (Unit player in units.playerList)
            {
                if (player != null && player.isActived && player.isPlayer)
                {
                    players.Add(player);
                }
            }
            if (players.Count == 0)
            {
                return;
            }
            foreach (Unit unit in units.unitsDict.Values)
            {
                if (unit == null || !unit.isActived || unit.isPlayer || BotSource.ContainsKey(unit.UID)
                    || unit.GetComponent<StudentNPCComponent>() == null)
                {
                    continue;
                }
                DressBot(unit.UID, players[UnityEngine.Random.Range(0, players.Count)]);
            }
        }

        private static void DressBot(ulong botUid, Unit source)
        {
            if (source == null)
            {
                return;
            }
            Look look = default(Look);
            long ab;
            if (HostLooks.TryGetValue(source.UID, out ab))
            {
                int c;
                HostLooksC.TryGetValue(source.UID, out c);
                look = Look.From((int)(ab >> 32), (int)(ab & 0xFFFFFFFFL), c);
            }
            int game = GameIndexOf(source);
            if (look.Hair == 0)
            {
                look.Hair = game;
            }
            if (look.Body == 0)
            {
                look.Body = game;
            }
            if (look.Cloth == 0)
            {
                look.Cloth = game;
            }
            look.Teacher = 0;
            BotSource[botUid] = source.UID;
            HostLooks[botUid] = ((long)look.A << 32) | (uint)look.B;
            HostLooksC[botUid] = look.C;
            Net.SendToModded(new CpTell { kind = CpKind.Look, a = botUid, i = look.A, j = look.B, f = look.C });
            if (BotSource.Count == 1)
            {
                Plugin.Log("looks: bots copy the players' looks; the first one copies " + Poles.NameOf(source));
            }
        }

        /// <summary>The body, hair and clothes set (1 to 4) the game gave a student.</summary>
        private static int GameIndexOf(Unit unit)
        {
            int game;
            if (unit != null && GameIndex.TryGetValue(unit.UID, out game) && game >= 1 && game <= 4)
            {
                return game;
            }
            RoleData data = unit != null ? unit.GetData<RoleData>() : null;
            game = data != null ? data.NetData.BodyMeshIndex : 0;
            return game >= 1 && game <= 4 ? game : 1;
        }

        // ------------------------------------------------------------------------- network

        public static void OnAsk(Unit player, CpAsk ask)
        {
            if (player == null)
            {
                return;
            }
            int b = (int)(ask.t0 & 0xFFFFFFuL);
            int c = (int)(ask.t1 & 0xFFFFFFuL);
            HostLooks[player.UID] = ((long)ask.i << 32) | (uint)b;
            HostLooksC[player.UID] = c;
            Net.SendToModded(new CpTell { kind = CpKind.Look, a = player.UID, i = ask.i, j = b, f = c });
            // The bots dressed like this player change with them.
            foreach (KeyValuePair<ulong, ulong> pair in new List<KeyValuePair<ulong, ulong>>(BotSource))
            {
                if (pair.Value == player.UID)
                {
                    DressBot(pair.Key, player);
                }
            }
        }

        /// <summary>Host: a player with the mod just joined and needs everybody's outfit.</summary>
        public static void OnPeerJoined(NetworkConnectionToClient connection)
        {
            foreach (KeyValuePair<ulong, long> pair in HostLooks)
            {
                int c;
                HostLooksC.TryGetValue(pair.Key, out c);
                Net.SendTo(connection, new CpTell
                {
                    kind = CpKind.Look, a = pair.Key, i = (int)(pair.Value >> 32), j = (int)(pair.Value & 0xFFFFFFFFL), f = c
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
            worn.Look = Look.From(tell.i, tell.j, Mathf.RoundToInt(tell.f));
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
            // A hairstyle (students): the afro or dreads, in the hair colour.
            if (worn.Body == null && unit.IsStudent && look.Hairstyle > 0 && look.Hairstyle < HairstyleModels.Length)
            {
                int before = worn.Pieces.Count;
                Add(worn, HairstyleModels[look.Hairstyle], Head, layer);
                if (worn.Pieces.Count > before)
                {
                    Recolour(worn.Pieces[worn.Pieces.Count - 1].Model, look.HairTint > 0 ? (Color)Tints[look.HairTint] : (Color)NaturalHair);
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
            bool wantsParts = look.Hair != 0 || look.Body != 0 || look.Cloth != 0 || look.Hairstyle != 0;
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
            // A hairstyle model takes the place of the game's hair (5 is none of the four).
            int hair = look.Hairstyle > 0 ? 5 : look.Hair == 0 ? game : look.Hair;
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
            // A bigger head (the face sliders) carries its hats higher and bigger.
            float headScale = unit.IsStudent ? worn.Look.HeadScale : 1f;
            float size = Mathf.Clamp(fit.headSize, 0.3f, 4f) * headScale;

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
            Vector3 headMiddle = neckAt + headTurn * new Vector3(0f, fit.headUp * headScale, fit.headForward * headScale);
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

        // ------------------------------------------------------------------- the face sliders

        // How the game's faces are made (from the self-test's face file): the eyes are balls
        // on the bones eye.L and eye.R, with the pupil painted on, inside eyelid shells on
        // yanpi1 and yanpi2. All of them are part of the face's mesh.
        //
        //   eye size    the eyes and their lids grow across the face but hardly in depth, so
        //               they don't bulge out, and move apart once they would touch.
        //   pupil size  the eyes' points are moved on the face's picture, towards the middle
        //               of the pupil to make it bigger, away from it to make it smaller. Each
        //               face mesh with each pupil size is made once and shared.
        //   head size   the head bone, with everything on it.
        //
        // Sizes and places start from what the game's own animation gave the bone, so a
        // blink or a look to the side still plays.

        /// <summary>An eye or eyelid bone the eye slider changes.</summary>
        private sealed class EyePart
        {
            public Transform T;
            public bool IsEye;
            public Vector3 GameScale;
            public Vector3 WrittenScale;
            public Vector3 GameAt;
            public Vector3 WrittenAt;
            /// <summary>Which of the bone's own axes points out of the face (0 x, 1 y, 2 z), and which way.</summary>
            public int Depth;
            public float DepthSign;
            /// <summary>Away from the middle of the face, in the parent's space, as long as from eye to eye.</summary>
            public Vector3 Out;
        }

        /// <summary>The bones and meshes the face sliders change on one student body.</summary>
        private sealed class FaceRig
        {
            public Transform Model;
            public Transform Head;
            public Vector3 HeadGame;
            public Vector3 HeadWritten;
            public readonly List<EyePart> Parts = new List<EyePart>();
            /// <summary>The meshes that draw the eyes (the face, in each of the game's variants).</summary>
            public readonly List<SkinnedMeshRenderer> Skins = new List<SkinnedMeshRenderer>();
        }

        /// <summary>The eyes' points in one face mesh, and where on the picture each eye's pupil is.</summary>
        private sealed class EyeMap
        {
            public int[] Points;
            /// <summary>For each eye point, the middle of its eye's pupil on the picture.</summary>
            public Vector2[] Middles;
            /// <summary>For each eye point, how far the front half of its eye reaches on the picture.</summary>
            public float[] Reach;
            public Vector2[] Uv;
        }

        private static readonly Dictionary<Mesh, EyeMap> EyeMaps = new Dictionary<Mesh, EyeMap>();
        /// <summary>The face meshes with other pupil sizes, by the game's mesh and the size step.</summary>
        private static readonly Dictionary<long, Mesh> PupilCopies = new Dictionary<long, Mesh>();
        /// <summary>For each such copy, the game's mesh it was made from.</summary>
        private static readonly Dictionary<Mesh, Mesh> PupilSource = new Dictionary<Mesh, Mesh>();

        private static FaceRig FindFace(Transform model)
        {
            Transform head = null;
            var eyes = new List<Transform>();
            var lids = new List<Transform>();
            foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
            {
                switch (part.name)
                {
                    case "head":
                        if (head == null)
                        {
                            head = part;
                        }
                        break;
                    case "eye.L":
                    case "eye.R":
                        eyes.Add(part);
                        break;
                    case "yanpi1.L":
                    case "yanpi1.R":
                    case "yanpi2.L":
                    case "yanpi2.R":
                        lids.Add(part);
                        break;
                }
            }
            if (head == null)
            {
                return null;
            }
            var rig = new FaceRig { Model = model, Head = head, HeadGame = head.localScale, HeadWritten = head.localScale };
            if (eyes.Count == 2)
            {
                foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Transform[] used = skin.bones;
                    if (skin.sharedMesh != null && used != null && System.Array.IndexOf(used, eyes[0]) >= 0)
                    {
                        rig.Skins.Add(skin);
                    }
                }
                // The face mesh's resting pose says which way each bone faces, even when an
                // eyelid is half shut in a blink right now.
                Transform[] faceBones = rig.Skins.Count > 0 ? rig.Skins[0].bones : null;
                Matrix4x4[] binds = rig.Skins.Count > 0 ? rig.Skins[0].sharedMesh.bindposes : null;
                int headIndex = faceBones != null ? System.Array.IndexOf(faceBones, head) : -1;

                // The front of the face in the head bone's own space, whichever way the head is
                // tipped right now: the head bone points up its own y axis.
                Vector3 middle = (eyes[0].position + eyes[1].position) * 0.5f;
                Vector3 frontInHead = head.InverseTransformDirection(middle - head.position);
                frontInHead.y = 0f;
                frontInHead = frontInHead.sqrMagnitude > 1e-8f ? frontInHead.normalized : head.InverseTransformDirection(model.forward);
                Vector3 front = head.TransformDirection(frontInHead);
                float gap = (eyes[0].position - eyes[1].position).magnitude;
                var bones = new List<Transform>(eyes);
                bones.AddRange(lids);
                var said = new List<string>();
                foreach (Transform bone in bones)
                {
                    if (bone.parent == eyes[0] || bone.parent == eyes[1])
                    {
                        // An eyelid hanging under its eye follows the eye anyway.
                        continue;
                    }
                    Transform eye = bone.name.EndsWith(".L") == eyes[0].name.EndsWith(".L") ? eyes[0] : eyes[1];
                    Vector3 away = eye.position - middle;
                    away = away.sqrMagnitude > 1e-8f ? away.normalized * gap : Vector3.zero;
                    Vector3 local = bone.InverseTransformDirection(front);
                    int index = faceBones != null ? System.Array.IndexOf(faceBones, bone) : -1;
                    if (index >= 0 && headIndex >= 0 && index < binds.Length && headIndex < binds.Length)
                    {
                        Vector3 rested = binds[index].MultiplyVector(binds[headIndex].inverse.MultiplyVector(frontInHead));
                        if (rested.sqrMagnitude > 1e-12f)
                        {
                            local = rested.normalized;
                        }
                    }
                    said.Add(bone.name + " " + local.ToString("F2") + " under " + (bone.parent != null ? bone.parent.name : "nothing"));
                    int depth = 0;
                    for (int axis = 1; axis < 3; axis++)
                    {
                        if (Mathf.Abs(local[axis]) > Mathf.Abs(local[depth]))
                        {
                            depth = axis;
                        }
                    }
                    rig.Parts.Add(new EyePart
                    {
                        T = bone,
                        IsEye = bone == eyes[0] || bone == eyes[1],
                        GameScale = bone.localScale,
                        WrittenScale = bone.localScale,
                        GameAt = bone.localPosition,
                        WrittenAt = bone.localPosition,
                        Depth = depth,
                        DepthSign = Mathf.Sign(local[depth]),
                        Out = bone.parent != null ? bone.parent.InverseTransformVector(away) : away
                    });
                }
                if (!_loggedFace)
                {
                    _loggedFace = true;
                    Plugin.Log("looks: face on " + model.name + ", " + rig.Skins.Count + " face meshes, the front in each bone: " + string.Join("; ", said.ToArray()));
                }
            }
            return rig;
        }

        private static bool _loggedFace;

        /// <summary>
        /// Late in every frame, after the game's own animation: the face sliders on every student
        /// who wears them.
        /// </summary>
        public static void FaceTick()
        {
            if (Worns.Count == 0)
            {
                return;
            }
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null)
            {
                return;
            }
            foreach (KeyValuePair<ulong, Worn> pair in Worns)
            {
                Worn worn = pair.Value;
                Look look = worn.Look;
                bool changed = look.Eyes != 0 || look.Pupils != 0 || look.HeadSize != 0;
                if (!changed && worn.Face == null)
                {
                    continue;
                }
                Unit unit = units.GetUnitByUID(pair.Key);
                Transform model = unit != null && unit.isActived && unit.model != null && unit.IsStudent ? unit.model.transform : null;
                if (worn.Face != null && (!changed || worn.Face.Model != model))
                {
                    // Back to normal, or another body now: the old one is put back as the game had it.
                    ResetFace(worn.Face);
                    worn.Face = null;
                }
                if (!changed || model == null)
                {
                    continue;
                }
                if (worn.Face == null)
                {
                    worn.Face = FindFace(model);
                    if (worn.Face == null)
                    {
                        continue;
                    }
                }
                try
                {
                    ShapeFace(worn.Face, look);
                }
                catch (System.Exception ex)
                {
                    Plugin.Log("looks: the face sliders failed on " + model.name + ": " + ex.Message);
                    worn.Look.Eyes = 0;
                    worn.Look.Pupils = 0;
                    worn.Look.HeadSize = 0;
                }
            }
        }

        private static void ShapeFace(FaceRig face, Look look)
        {
            if (face.Head != null)
            {
                Vector3 now = face.Head.localScale;
                if (now != face.HeadWritten)
                {
                    face.HeadGame = now;
                }
                face.HeadWritten = face.HeadGame * look.HeadScale;
                face.Head.localScale = face.HeadWritten;
            }

            float eyes = look.EyeScale;
            // Across the face the eyes grow fully, in depth only a little.
            float deep = 1f + 0.3f * (eyes - 1f);
            // An eye is about 0.365 of the way to the other eye across; past about 1.3 times
            // they would touch, so each moves out by half of what they would overlap.
            float apart = Mathf.Max(0f, (0.73f * eyes + 0.08f - 1f) * 0.5f);
            foreach (EyePart part in face.Parts)
            {
                if (part.T == null)
                {
                    continue;
                }
                Vector3 nowScale = part.T.localScale;
                if (nowScale != part.WrittenScale)
                {
                    part.GameScale = nowScale;
                }
                Vector3 nowAt = part.T.localPosition;
                if (nowAt != part.WrittenAt)
                {
                    part.GameAt = nowAt;
                }
                Vector3 grow = new Vector3(eyes, eyes, eyes);
                grow[part.Depth] = deep;
                part.WrittenScale = Vector3.Scale(part.GameScale, grow);
                part.WrittenAt = part.GameAt + part.Out * apart;
                part.T.localScale = part.WrittenScale;
                part.T.localPosition = part.WrittenAt;
            }

            foreach (SkinnedMeshRenderer skin in face.Skins)
            {
                Mesh now = skin != null ? skin.sharedMesh : null;
                if (now == null)
                {
                    continue;
                }
                Mesh original;
                if (!PupilSource.TryGetValue(now, out original))
                {
                    original = now;
                }
                Mesh want = look.Pupils == 0 ? original : PupilCopy(skin, original, look.Pupils, face);
                if (want != now)
                {
                    skin.sharedMesh = want;
                }
            }
        }

        /// <summary>Puts a face back the way the game has it.</summary>
        private static void ResetFace(FaceRig face)
        {
            if (face.Head != null)
            {
                face.Head.localScale = face.HeadGame;
            }
            foreach (EyePart part in face.Parts)
            {
                if (part.T != null)
                {
                    part.T.localScale = part.GameScale;
                    part.T.localPosition = part.GameAt;
                }
            }
            foreach (SkinnedMeshRenderer skin in face.Skins)
            {
                Mesh original;
                if (skin != null && skin.sharedMesh != null && PupilSource.TryGetValue(skin.sharedMesh, out original))
                {
                    skin.sharedMesh = original;
                }
            }
        }

        /// <summary>The game's face mesh with the pupils at another size, made the first time it is needed.</summary>
        private static Mesh PupilCopy(SkinnedMeshRenderer skin, Mesh original, int step, FaceRig face)
        {
            long key = (long)original.GetInstanceID() * 64L + (step & 31);
            Mesh copy;
            if (PupilCopies.TryGetValue(key, out copy))
            {
                return copy != null ? copy : original;
            }
            EyeMap map;
            if (!EyeMaps.TryGetValue(original, out map))
            {
                map = MakeEyeMap(skin, original, face);
                EyeMaps[original] = map;
            }
            copy = null;
            if (map != null)
            {
                try
                {
                    copy = UnityEngine.Object.Instantiate(original);
                    copy.name = original.name + " (pupils " + Look.Size(step).ToString("F1") + ")";
                    var uv = (Vector2[])map.Uv.Clone();
                    float zoom = 1f / Look.Size(step);
                    for (int n = 0; n < map.Points.Length; n++)
                    {
                        int i = map.Points[n];
                        Vector2 middle = map.Middles[n];
                        Vector2 to = middle + (uv[i] - middle) * zoom;
                        // Smaller pupils show more of the picture round the eye: no further than the eye's own white.
                        if (zoom > 1f && map.Reach[n] > 0f && (to - middle).magnitude > map.Reach[n])
                        {
                            to = middle + (to - middle).normalized * map.Reach[n];
                        }
                        uv[i] = to;
                    }
                    copy.uv = uv;
                    PupilSource[copy] = original;
                }
                catch (System.Exception ex)
                {
                    Plugin.Log("looks: could not make pupils " + Look.Size(step).ToString("F1") + " on " + original.name + ": " + ex.Message);
                    copy = null;
                }
            }
            PupilCopies[key] = copy;
            return copy != null ? copy : original;
        }

        /// <summary>Finds the eyes' points in a face mesh and the middle of each pupil on its picture.</summary>
        private static EyeMap MakeEyeMap(SkinnedMeshRenderer skin, Mesh mesh, FaceRig face)
        {
            try
            {
                Transform[] bones = skin.bones;
                BoneWeight[] weights = mesh.boneWeights;
                Vector3[] points = mesh.vertices;
                Vector2[] uv = mesh.uv;
                Matrix4x4[] binds = mesh.bindposes;
                if (uv == null || uv.Length != points.Length || weights.Length != points.Length)
                {
                    Plugin.Log("looks: " + mesh.name + " has no picture places for its points, pupils stay as they are");
                    return null;
                }
                var found = new List<int>();
                var middles = new List<Vector2>();
                var reach = new List<float>();
                var said = new List<string>();
                foreach (EyePart eye in face.Parts)
                {
                    int bone = eye.IsEye ? System.Array.IndexOf(bones, eye.T) : -1;
                    if (bone < 0 || bone >= binds.Length)
                    {
                        continue;
                    }
                    var mine = new List<int>();
                    var depth = new List<float>();
                    float front = float.MinValue;
                    float back = float.MaxValue;
                    for (int i = 0; i < points.Length; i++)
                    {
                        if (weights[i].boneIndex0 != bone || weights[i].weight0 < 0.5f)
                        {
                            continue;
                        }
                        // How far the point is towards the front of the eye, in the eye bone's own space.
                        float d = binds[bone].MultiplyPoint3x4(points[i])[eye.Depth] * eye.DepthSign;
                        mine.Add(i);
                        depth.Add(d);
                        front = Mathf.Max(front, d);
                        back = Mathf.Min(back, d);
                    }
                    if (mine.Count == 0)
                    {
                        continue;
                    }
                    // The pupil is painted on the front of the eye: the middle of the front points' places.
                    float cut = front - (front - back) * 0.15f;
                    float half = (front + back) * 0.5f;
                    Vector2 sum = Vector2.zero;
                    int count = 0;
                    for (int k = 0; k < mine.Count; k++)
                    {
                        if (depth[k] >= cut)
                        {
                            sum += uv[mine[k]];
                            count++;
                        }
                    }
                    Vector2 middle = sum / Mathf.Max(1, count);
                    float far = 0f;
                    for (int k = 0; k < mine.Count; k++)
                    {
                        if (depth[k] >= half)
                        {
                            far = Mathf.Max(far, (uv[mine[k]] - middle).magnitude);
                        }
                    }
                    foreach (int i in mine)
                    {
                        found.Add(i);
                        middles.Add(middle);
                        reach.Add(far);
                    }
                    said.Add(eye.T.name + " " + mine.Count + " points, pupil at " + middle.ToString("F3") + ", reach " + far.ToString("F3"));
                }
                if (found.Count == 0)
                {
                    Plugin.Log("looks: no eye points found in " + mesh.name + ", pupils stay as they are");
                    return null;
                }
                Plugin.Log("looks: eyes in " + mesh.name + ": " + string.Join("; ", said.ToArray()));
                return new EyeMap { Points = found.ToArray(), Middles = middles.ToArray(), Reach = reach.ToArray(), Uv = uv };
            }
            catch (System.Exception ex)
            {
                Plugin.Log("looks: could not read the eyes in " + mesh.name + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Colours one of the mod's models, such as a hairstyle in the hair colour.</summary>
        private static void Recolour(GameObject model, Color color)
        {
            if (model == null)
            {
                return;
            }
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material material = renderer.sharedMaterial;
                int property = material != null ? ColorProperty(material.shader) : 0;
                if (property == 0)
                {
                    continue;
                }
                Block.Clear();
                Block.SetColor(property, color);
                renderer.SetPropertyBlock(Block);
            }
        }

        // ------------------------------------------------------------------- self-test

        /// <summary>Self-test: dress one character in a look, as if the host had said so.</summary>
        public static void TestDress(ulong uid, Look look)
        {
            Worn worn;
            if (!Worns.TryGetValue(uid, out worn))
            {
                worn = new Worn();
                Worns[uid] = worn;
            }
            worn.Look = look;
            worn.Dirty = true;
        }

        /// <summary>Self-test: how many bots have been dressed like a player.</summary>
        public static int BotsDressed => BotSource.Count;

        /// <summary>Self-test: hides everybody's worn pieces but one character's, or shows them all again.</summary>
        public static void TestHideAllBut(ulong keep, bool hide)
        {
            foreach (KeyValuePair<ulong, Worn> pair in Worns)
            {
                foreach (Piece piece in pair.Value.Pieces)
                {
                    if (piece.Model == null)
                    {
                        continue;
                    }
                    foreach (Renderer renderer in piece.Model.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.forceRenderingOff = hide && pair.Key != keep;
                    }
                }
            }
        }
    }
}
