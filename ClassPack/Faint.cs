using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Nerf darts knock people out. Whoever is hit faints and falls over like a rag doll: a
    /// real physics doll made on the spot from the character's own skeleton, on every
    /// player's screen.
    ///
    ///   a student player   faints for 3 seconds, then goes on a pole and can't be rescued
    ///                      (when every pole is taken they just get up again)
    ///   the teacher        faints for 5 seconds, then gets up
    ///   a bot              faints for 4 seconds, then gets up
    ///
    /// The host decides and tells everybody (CpKind.Faint). Each player's game then stuns a
    /// fainted student the game's own way so it can't walk, and builds the doll. The teacher
    /// is knocked over by the host like on a banana peel and held still by their own game.
    ///
    /// The doll is a handful of capsules joined at the neck, shoulders, elbows, hips and
    /// knees, plus a ball for the head. Every frame, after the game's animation, each bone is
    /// turned the way its capsule lies and the hips are put where the doll's pelvis is. When
    /// the time is up the body eases back into the game's own pose. A player who fainted sees
    /// themselves from behind (SelfView) until they are up.
    /// </summary>
    internal static class Faint
    {
        public const float StudentSeconds = 3f;
        public const float TeacherSeconds = 5f;
        public const float BotSeconds = 4f;
        /// <summary>How long getting up takes, from the doll back to the game's own pose.</summary>
        private const float RiseSeconds = 0.5f;
        /// <summary>The "Ignore Raycast" layer: the doll is solid, but the game's own aiming looks through it.</summary>
        private const int QuietLayer = 2;

        /// <summary>One capsule of the doll, and the bones it turns.</summary>
        private sealed class Part
        {
            public Transform Body;
            public Rigidbody Rb;
            public Collider Shape;
            public Transform[] Bones;
            /// <summary>Each bone's turn measured from the capsule's.</summary>
            public Quaternion[] Offsets;
            public Quaternion[] Anim;
            public Quaternion[] Saved;
        }

        /// <summary>A hand or a foot: they hang under the skeleton's root, not under the arm or leg.</summary>
        private sealed class End
        {
            public Transform T;
            public Transform Lower;
            public Vector3 Offset;
            public Quaternion Turn;
            public Vector3 AnimAt;
            public Quaternion AnimTurn;
            public Vector3 SavedAt;
            public Quaternion SavedTurn;
        }

        private sealed class Doll
        {
            public Transform Model;
            public Transform Hips;
            public Vector3 HipsSaved;
            public GameObject Holder;
            public Part Pelvis;
            public readonly List<Part> Parts = new List<Part>();
            public readonly List<End> Ends = new List<End>();
            public Vector3 StoodAt;
            public float Started;
            public float Until;
        }

        private static readonly Dictionary<ulong, Doll> Dolls = new Dictionary<ulong, Doll>();
        private static readonly List<ulong> Gone = new List<ulong>();
        private static readonly HashSet<Collider> DollShapes = new HashSet<Collider>();
        /// <summary>Everybody who is out right now, and until when (on every player's game).</summary>
        private static readonly Dictionary<ulong, float> Down = new Dictionary<ulong, float>();

        // Host only: students who go on a pole when they wake up, when, and who shot them.
        private static readonly Dictionary<ulong, float> HostPoleAt = new Dictionary<ulong, float>();
        private static readonly Dictionary<ulong, string> HostPoleBy = new Dictionary<ulong, string>();

        private static bool _held;
        private static bool _loggedDoll;
        private static bool _loggedMissing;
        private static GUIStyle _big;

        // ------------------------------------------------------------------ what others ask

        /// <summary>True while this character lies knocked out.</summary>
        public static bool IsDown(Unit unit)
        {
            float until;
            return unit != null && Down.TryGetValue(unit.UID, out until) && Time.time < until;
        }

        /// <summary>True while this player lies knocked out.</summary>
        public static bool LocalDown => IsDown(Game.LocalPlayer);

        /// <summary>True for the doll's own capsules, which nerf darts fly through.</summary>
        public static bool IsDollPart(Collider collider)
        {
            return collider != null && DollShapes.Contains(collider);
        }

        // ---------------------------------------------------------------------- host side

        /// <summary>
        /// Host: a nerf dart hit someone, fired from where the shooter stands. They faint, and a
        /// student player goes on a pole afterwards. False when they can't faint right now:
        /// already out, carried or hung.
        /// </summary>
        public static bool Knock(Unit target, Unit shooter, Vector3 from)
        {
            if (!NetworkServer.active || !Game.InClass || target == null || !target.isActived || target.IsDead)
            {
                return false;
            }
            float until;
            if (Down.TryGetValue(target.UID, out until) && Time.time < until + RiseSeconds)
            {
                return false;
            }
            float seconds;
            if (target.IsTeacher)
            {
                seconds = TeacherSeconds;
            }
            else if (target.IsStudent && Poles.CanBeHung(target))
            {
                seconds = target.isPlayer ? StudentSeconds : BotSeconds;
            }
            else
            {
                return false;
            }

            // The body falls away from the shooter.
            Vector3 push = target.Position - from;
            push.y = 0f;
            if (push.sqrMagnitude < 0.01f)
            {
                push = -target.transform.forward;
                push.y = 0f;
            }
            push.Normalize();
            // Every player's game (the host's too) knocks the character down from this; a teacher
            // gets the game's own slip there, held for the whole faint.
            Net.SendToModded(new CpTell { kind = CpKind.Faint, a = target.UID, f = seconds, v = push });

            bool own = shooter == target;
            string by = own ? "Your own RPG" : Poles.NameOf(shooter);
            if (target.isPlayer && target.IsStudent)
            {
                HostPoleAt[target.UID] = Time.time + seconds;
                HostPoleBy[target.UID] = by;
                Net.Toast(target, by + (own ? " got you!" : " got you with a nerf dart!") + " You fainted, and you wake up on a pole with no rescue.", 4f);
            }
            else if (target.isPlayer)
            {
                Net.Toast(target, by + (own ? " got you!" : " got you with a nerf dart!") + " You fainted for " + seconds.ToString("F0") + " seconds.", 4f);
            }
            Plugin.Log("faint: " + Poles.NameOf(target) + " fainted for " + seconds.ToString("F0") + " s, hit by " + Poles.NameOf(shooter));
            return true;
        }

        public static void Tick()
        {
            if (!Game.InClass)
            {
                if (Dolls.Count > 0 || Down.Count > 0 || HostPoleAt.Count > 0 || _held)
                {
                    Clear();
                }
                return;
            }
            if (NetworkServer.active && HostPoleAt.Count > 0)
            {
                HostWake();
            }
            HoldLocal();
        }

        /// <summary>Host: students whose faint is over wake up on a pole.</summary>
        private static void HostWake()
        {
            UnitManager units = Game.Peek<UnitManager>();
            Gone.Clear();
            foreach (KeyValuePair<ulong, float> pair in HostPoleAt)
            {
                if (Time.time >= pair.Value)
                {
                    Gone.Add(pair.Key);
                }
            }
            foreach (ulong uid in Gone)
            {
                HostPoleAt.Remove(uid);
                HostPoleBy.Remove(uid);
                Unit student = units != null ? units.GetUnitByUID(uid) : null;
                if (student != null && Poles.PutOnPole(student, true))
                {
                    Net.Toast(student, "You woke up on a pole. Nerf darts stick: nobody can rescue you.", 4f);
                    Plugin.Log("faint: " + Poles.NameOf(student) + " woke up on a pole");
                }
            }
            Gone.Clear();
        }

        /// <summary>A fainted player can't walk off: the game's own stun only holds students.</summary>
        private static void HoldLocal()
        {
            Unit me = Game.LocalPlayer;
            bool down = me != null && IsDown(me);
            if (!down && !_held)
            {
                return;
            }
            InputComponent input = me != null ? me.GetComponent<InputComponent>() : null;
            if (input != null)
            {
                input.speedFac = down ? 0f : 1f;
            }
            _held = down;
        }

        private static void Clear()
        {
            foreach (Doll doll in Dolls.Values)
            {
                Drop(doll);
            }
            Dolls.Clear();
            DollShapes.Clear();
            Down.Clear();
            HostPoleAt.Clear();
            HostPoleBy.Clear();
            if (_held)
            {
                Unit me = Game.LocalPlayer;
                InputComponent input = me != null ? me.GetComponent<InputComponent>() : null;
                if (input != null)
                {
                    input.speedFac = 1f;
                }
                _held = false;
            }
        }

        // ------------------------------------------------------------------- every player

        public static void OnTell(CpTell tell)
        {
            UnitManager units = Game.Peek<UnitManager>();
            Unit unit = units != null ? units.GetUnitByUID(tell.a) : null;
            if (unit == null || !unit.isActived)
            {
                return;
            }
            float seconds = Mathf.Clamp(tell.f, 0.5f, 10f);
            Down[unit.UID] = Time.time + seconds;
            StudentStunComponent stun = unit.GetComponent<StudentStunComponent>();
            if (stun != null)
            {
                stun.TryGetProjectileStun(seconds);
            }
            TeacherStunComponent teacherStun = unit.IsTeacher ? unit.GetComponent<TeacherStunComponent>() : null;
            if (teacherStun != null)
            {
                // The game's own slip: the teacher can't move or catch, and drops a student
                // being carried. It lasts a couple of seconds in the game; here the whole faint.
                teacherStun.GetSlip();
                try
                {
                    HarmonyLib.Traverse.Create(teacherStun).Field("remaining").SetValue(seconds);
                }
                catch (System.Exception)
                {
                    // The game's own length then; the player's own game still holds them still.
                }
            }
            Game.PlaySoundAt(AudioEvents.sfx_BananaSlipp_sfx, unit.Position);

            Doll old;
            if (Dolls.TryGetValue(unit.UID, out old))
            {
                Drop(old);
                Dolls.Remove(unit.UID);
            }
            // The new teachers' own bodies hide the game's skeleton: there is nothing to see a doll on.
            if (unit.model == null || Looks.HasOwnBody(unit.UID))
            {
                return;
            }
            try
            {
                Doll doll = Build(unit, tell.v);
                if (doll != null)
                {
                    doll.Started = Time.time;
                    doll.Until = Time.time + seconds;
                    Dolls[unit.UID] = doll;
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log("faint: no rag doll for " + Poles.NameOf(unit) + ": " + ex);
            }
        }

        /// <summary>Called late in every frame, after the game's animation and the swim and dance moves.</summary>
        public static void LateTick()
        {
            if (Dolls.Count == 0)
            {
                return;
            }
            UnitManager units = Game.Peek<UnitManager>();
            float now = Time.time;
            Gone.Clear();
            foreach (KeyValuePair<ulong, Doll> pair in Dolls)
            {
                Doll doll = pair.Value;
                Unit unit = units != null ? units.GetUnitByUID(pair.Key) : null;
                bool here = unit != null && unit.isActived && unit.model != null && unit.model.transform == doll.Model
                    && doll.Hips != null && doll.Pelvis != null && doll.Pelvis.Body != null;
                // Put on a pole or sent somewhere else: the doll is done at once.
                if (!here || now >= doll.Until + RiseSeconds || (unit.Position - doll.StoodAt).sqrMagnitude > 9f
                    || doll.Pelvis.Body.position.y < doll.StoodAt.y - 4f)
                {
                    Gone.Add(pair.Key);
                    continue;
                }
                float weight = now < doll.Until ? 1f : 1f - (now - doll.Until) / RiseSeconds;
                Pose(doll, Mathf.SmoothStep(0f, 1f, weight));
            }
            foreach (ulong uid in Gone)
            {
                Drop(Dolls[uid]);
                Dolls.Remove(uid);
            }
            Gone.Clear();
        }

        private static void Pose(Doll doll, float weight)
        {
            // The pose the game (or the swim or dance move) gave this frame, to ease back into.
            foreach (Part part in doll.Parts)
            {
                for (int i = 0; i < part.Bones.Length; i++)
                {
                    part.Anim[i] = part.Bones[i].rotation;
                }
            }
            foreach (End end in doll.Ends)
            {
                end.AnimAt = end.T.position;
                end.AnimTurn = end.T.rotation;
            }
            doll.Hips.position = Vector3.Lerp(doll.Hips.position, doll.Pelvis.Body.position, weight);
            // Parents before children: each bone gets its turn in the world, so the order of
            // the parts (pelvis, chest, head, arms, legs) keeps every joint in place.
            foreach (Part part in doll.Parts)
            {
                for (int i = 0; i < part.Bones.Length; i++)
                {
                    Quaternion want = part.Body.rotation * part.Offsets[i];
                    part.Bones[i].rotation = weight >= 1f ? want : Quaternion.Slerp(part.Anim[i], want, weight);
                }
            }
            foreach (End end in doll.Ends)
            {
                Vector3 place = end.Lower.position + end.Lower.rotation * end.Offset;
                Quaternion turn = end.Lower.rotation * end.Turn;
                end.T.position = weight >= 1f ? place : Vector3.Lerp(end.AnimAt, place, weight);
                end.T.rotation = weight >= 1f ? turn : Quaternion.Slerp(end.AnimTurn, turn, weight);
            }
        }

        /// <summary>Puts the bones back as they were and throws the doll away.</summary>
        private static void Drop(Doll doll)
        {
            foreach (Part part in doll.Parts)
            {
                if (part.Shape != null)
                {
                    DollShapes.Remove(part.Shape);
                }
                for (int i = 0; i < part.Bones.Length; i++)
                {
                    if (part.Bones[i] != null)
                    {
                        part.Bones[i].localRotation = part.Saved[i];
                    }
                }
            }
            foreach (End end in doll.Ends)
            {
                if (end.T != null)
                {
                    end.T.localPosition = end.SavedAt;
                    end.T.localRotation = end.SavedTurn;
                }
            }
            if (doll.Hips != null)
            {
                doll.Hips.localPosition = doll.HipsSaved;
            }
            if (doll.Holder != null)
            {
                Object.Destroy(doll.Holder);
            }
        }

        // ------------------------------------------------------------------- making the doll

        private static Doll Build(Unit unit, Vector3 push)
        {
            Transform model = unit.model.transform;
            var byName = new Dictionary<string, Transform>();
            foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
            {
                if (!byName.ContainsKey(bone.name))
                {
                    byName[bone.name] = bone;
                }
            }
            Transform hips = Find(byName, "gupen", "hips", "Hips");
            Transform spine1 = Find(byName, "spine1", "spine", "Spine");
            Transform spine2 = Find(byName, "spine2", "chest", "Spine1");
            Transform neck = Find(byName, "neck", "Neck");
            Transform head = Find(byName, "head", "Head");
            if (hips == null || neck == null || head == null)
            {
                if (!_loggedMissing)
                {
                    _loggedMissing = true;
                    Plugin.Log("faint: no hips, neck or head bone on " + model.name + ", it faints without a rag doll");
                }
                return null;
            }
            float torso = (neck.position - hips.position).magnitude;
            if (torso < 0.05f)
            {
                return null;
            }
            if (spine1 == hips)
            {
                spine1 = null;
            }
            if (spine2 == hips || spine2 == spine1)
            {
                spine2 = null;
            }
            Transform chest = spine2 ?? spine1;

            var doll = new Doll { Model = model, Hips = hips, HipsSaved = hips.localPosition, StoodAt = unit.Position };
            doll.Holder = new GameObject("[ClassPack faint " + unit.UID + "]");
            int layer = PickLayer(unit, hips.position);

            // The pelvis stands in for the hips and the lower spine, pointing up the back.
            Vector3 up = (neck.position - hips.position) / torso;
            Quaternion pelvisTurn = spine1 != null ? spine1.rotation : Quaternion.LookRotation(Flat(unit.transform.forward, up), up);
            float pelvisLength = chest != null && chest != spine1 ? (chest.position - hips.position).magnitude : torso * 0.5f;
            var pelvisBones = new List<Transform> { hips };
            if (spine1 != null && spine1 != chest)
            {
                pelvisBones.Add(spine1);
            }
            doll.Pelvis = MakePart(doll, "pelvis", hips.position, pelvisTurn, pelvisLength, torso * 0.3f, 3f, false, null, 0f, layer, pelvisBones.ToArray());

            // The chest up to the neck.
            Part chestPart = doll.Pelvis;
            if (chest != null)
            {
                chestPart = MakePart(doll, "chest", chest.position, chest.rotation, (neck.position - chest.position).magnitude, torso * 0.3f, 2.5f,
                    false, doll.Pelvis, 30f, layer, chest, neck);
            }
            else
            {
                doll.Pelvis.Bones = new[] { hips, neck };
                doll.Pelvis.Offsets = new[] { doll.Pelvis.Offsets[0], Quaternion.Inverse(pelvisTurn) * neck.rotation };
                doll.Pelvis.Anim = new Quaternion[2];
                doll.Pelvis.Saved = new[] { doll.Pelvis.Saved[0], neck.localRotation };
            }
            // The game's students have big heads: a heavy ball on the neck.
            MakePart(doll, "head", head.position, head.rotation, 0f, torso * 0.45f, 1.5f, true, chestPart, 35f, layer, head);

            foreach (string side in new[] { "L", "R" })
            {
                Transform upper = Find(byName, "upper_arm." + side);
                Transform lower = Find(byName, "forearm." + side);
                Transform hand = Find(byName, "shouzhang2." + side, "hand." + side);
                if (upper != null && lower != null)
                {
                    float upperLength = (lower.position - upper.position).magnitude;
                    bool loose = hand != null && !hand.IsChildOf(lower);
                    float lowerLength = loose ? (hand.position - lower.position).magnitude : upperLength * 0.9f;
                    Part arm = MakePart(doll, "upper arm " + side, upper.position, upper.rotation, upperLength, torso * 0.1f, 0.5f,
                        false, chestPart, 70f, layer, upper);
                    MakePart(doll, "forearm " + side, lower.position, lower.rotation, lowerLength, torso * 0.09f, 0.4f,
                        false, arm, 70f, layer, lower);
                    if (loose)
                    {
                        AddEnd(doll, hand, lower);
                    }
                }
                Transform thigh = Find(byName, "thigh." + side);
                Transform shin = Find(byName, "shin." + side);
                Transform foot = Find(byName, "foot." + side);
                if (thigh != null && shin != null)
                {
                    float thighLength = (shin.position - thigh.position).magnitude;
                    bool loose = foot != null && !foot.IsChildOf(shin);
                    float shinLength = loose ? (foot.position - shin.position).magnitude : thighLength;
                    Part leg = MakePart(doll, "thigh " + side, thigh.position, thigh.rotation, thighLength, torso * 0.13f, 1.2f,
                        false, doll.Pelvis, 50f, layer, thigh);
                    MakePart(doll, "shin " + side, shin.position, shin.rotation, shinLength, torso * 0.11f, 0.8f,
                        false, leg, 60f, layer, shin);
                    if (loose)
                    {
                        AddEnd(doll, foot, shin);
                    }
                }
            }

            // The doll's own parts don't knock into each other or into the character's own
            // collider, which stays standing where it fainted.
            var shapes = new List<Collider>();
            foreach (Part part in doll.Parts)
            {
                shapes.Add(part.Shape);
                DollShapes.Add(part.Shape);
            }
            for (int i = 0; i < shapes.Count; i++)
            {
                for (int j = i + 1; j < shapes.Count; j++)
                {
                    Physics.IgnoreCollision(shapes[i], shapes[j]);
                }
            }
            foreach (Collider own in unit.transform.GetComponentsInChildren<Collider>(true))
            {
                foreach (Collider shape in shapes)
                {
                    Physics.IgnoreCollision(shape, own);
                }
            }

            // Knocked back away from the shooter: the higher up, the harder, so the body tips over.
            foreach (Part part in doll.Parts)
            {
                float height = Mathf.Clamp01((part.Body.position.y - hips.position.y) / torso);
                part.Rb.velocity = push * (0.5f + 1.6f * height) + Vector3.up * 0.3f;
            }

            if (!_loggedDoll)
            {
                _loggedDoll = true;
                Plugin.Log("faint: first rag doll on " + model.name + ", " + doll.Parts.Count + " parts, " + doll.Ends.Count
                    + " hands and feet, torso " + torso.ToString("F2") + " m, layer " + layer);
            }
            return doll;
        }

        private static Part MakePart(Doll doll, string name, Vector3 at, Quaternion turn, float length, float radius, float mass,
            bool ball, Part parent, float swing, int layer, params Transform[] bones)
        {
            var holder = new GameObject(name) { layer = layer };
            holder.transform.SetParent(doll.Holder.transform, false);
            holder.transform.SetPositionAndRotation(at, turn);
            Collider shape;
            if (ball)
            {
                var sphere = holder.AddComponent<SphereCollider>();
                sphere.radius = radius;
                sphere.center = new Vector3(0f, radius * 0.9f, 0f);
                shape = sphere;
            }
            else
            {
                // Every bone points along its own up axis.
                var capsule = holder.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.radius = radius;
                capsule.height = Mathf.Max(length, radius * 2f);
                capsule.center = new Vector3(0f, length * 0.5f, 0f);
                shape = capsule;
            }
            var body = holder.AddComponent<Rigidbody>();
            body.mass = mass;
            body.drag = 0.1f;
            body.angularDrag = 0.8f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations = 12;
            body.maxDepenetrationVelocity = 2f;
            if (parent != null)
            {
                var joint = holder.AddComponent<CharacterJoint>();
                joint.connectedBody = parent.Rb;
                joint.anchor = Vector3.zero;
                joint.axis = Vector3.up;
                joint.swingAxis = Vector3.right;
                joint.lowTwistLimit = new SoftJointLimit { limit = -20f };
                joint.highTwistLimit = new SoftJointLimit { limit = 20f };
                joint.swing1Limit = new SoftJointLimit { limit = swing };
                joint.swing2Limit = new SoftJointLimit { limit = swing };
                joint.enableProjection = true;
                joint.enablePreprocessing = false;
            }
            var part = new Part
            {
                Body = holder.transform,
                Rb = body,
                Shape = shape,
                Bones = bones,
                Offsets = new Quaternion[bones.Length],
                Anim = new Quaternion[bones.Length],
                Saved = new Quaternion[bones.Length]
            };
            Quaternion back = Quaternion.Inverse(turn);
            for (int i = 0; i < bones.Length; i++)
            {
                part.Offsets[i] = back * bones[i].rotation;
                part.Saved[i] = bones[i].localRotation;
            }
            doll.Parts.Add(part);
            return part;
        }

        private static void AddEnd(Doll doll, Transform end, Transform lower)
        {
            Quaternion back = Quaternion.Inverse(lower.rotation);
            doll.Ends.Add(new End
            {
                T = end,
                Lower = lower,
                Offset = back * (end.position - lower.position),
                Turn = back * end.rotation,
                SavedAt = end.localPosition,
                SavedTurn = end.localRotation
            });
        }

        /// <summary>
        /// A layer the doll lies on: one that hits the floor under the character, best of all
        /// the one the game's own aiming looks through.
        /// </summary>
        private static int PickLayer(Unit unit, Vector3 at)
        {
            int floor = -1;
            float nearest = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(at + Vector3.up * 0.3f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(unit.transform) || DollShapes.Contains(hit.collider))
                {
                    continue;
                }
                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    floor = hit.collider.gameObject.layer;
                }
            }
            if (floor < 0 || !Physics.GetIgnoreLayerCollision(QuietLayer, floor))
            {
                return QuietLayer;
            }
            return !Physics.GetIgnoreLayerCollision(0, floor) ? 0 : floor;
        }

        private static Vector3 Flat(Vector3 forward, Vector3 up)
        {
            Vector3 flat = forward - up * Vector3.Dot(forward, up);
            return flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward;
        }

        private static Transform Find(Dictionary<string, Transform> byName, params string[] names)
        {
            foreach (string name in names)
            {
                Transform found;
                if (byName.TryGetValue(name, out found))
                {
                    return found;
                }
            }
            return null;
        }

        // ------------------------------------------------------------------------- on screen

        /// <summary>The big "You fainted!" while this player is out. Drawn in a 1280 by 720 frame.</summary>
        public static string DrawHud(float width, float height)
        {
            Unit me = Game.LocalPlayer;
            float until;
            if (me == null || !Down.TryGetValue(me.UID, out until) || Time.time >= until)
            {
                return null;
            }
            Skin.Ensure();
            if (_big == null)
            {
                _big = new GUIStyle(Skin.Hint) { fontSize = 64, alignment = TextAnchor.MiddleCenter };
            }
            Skin.Outlined(new Rect(0f, height * 0.26f, width, 90f), "You fainted!", _big, new Color32(255, 232, 90, 255), Skin.Outline, 3f);
            int left = Mathf.Max(1, Mathf.CeilToInt(until - Time.time));
            return me.IsStudent
                ? "Knocked out by a nerf dart. You wake up on a pole in " + left + " s"
                : "Knocked out by a nerf dart. Back on your feet in " + left + " s";
        }

        // ------------------------------------------------------------------------- self-test

        /// <summary>Self-test (host): knocks someone out as if a dart had come from in front of them.</summary>
        public static bool TestKnock(Unit target)
        {
            return target != null && Knock(target, target, target.Position + target.transform.forward * 3f);
        }

        /// <summary>Self-test: how many dolls lie about right now.</summary>
        public static int DollCount => Dolls.Count;
    }
}
