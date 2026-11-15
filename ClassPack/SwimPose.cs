using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Real swimming moves on the game's own characters. The moves ("swim" and "swimidle") come
    /// from Meshy's animation library and are stored for a Meshy skeleton in moves_swim.cpr
    /// (written by tools/convert_character.py). The game's students, bots and teacher have
    /// another skeleton with other bone names and another resting pose, so each move is copied
    /// over joint by joint:
    ///
    ///   the turn a joint has made since its resting pose in the move  (taken from the file)
    ///   after a fixed turn that lines the game's bone up with the move's resting bone
    ///   applied to how the game's bone was turned when the character stood upright.
    ///
    /// This runs after the game's own animation each frame and only for characters that are in
    /// the pool's water. The hips are then put at the water's surface, so the body floats while
    /// the game still walks the character along the pool's floor. A player's own body is left
    /// alone: the game shows the world through its eyes.
    /// </summary>
    internal static class SwimPose
    {
        /// <summary>The game's bone for each joint of the moves file.</summary>
        private static readonly Dictionary<string, string> GameBone = new Dictionary<string, string>
        {
            { "hips", "spine1" }, { "chest", "spine2" }, { "neck", "neck" }, { "head", "head" }, { "headend", "head_end" },
            { "l_shoulder", "shoulder.L" }, { "l_upperarm", "upper_arm.L" }, { "l_forearm", "forearm.L" }, { "l_hand", "hand.L" },
            { "r_shoulder", "shoulder.R" }, { "r_upperarm", "upper_arm.R" }, { "r_forearm", "forearm.R" }, { "r_hand", "hand.R" },
            { "l_thigh", "thigh.L" }, { "l_shin", "shin.L" }, { "l_foot", "foot.L" }, { "l_toe", "toe.L" },
            { "r_thigh", "thigh.R" }, { "r_shin", "shin.R" }, { "r_foot", "foot.R" }, { "r_toe", "toe.R" }
        };

        /// <summary>The joint each joint's bone points at. Joints without one are not turned.</summary>
        private static readonly Dictionary<string, string> PointsAt = new Dictionary<string, string>
        {
            { "hips", "chest" }, { "chest", "neck" }, { "neck", "head" }, { "head", "headend" },
            { "l_shoulder", "l_upperarm" }, { "l_upperarm", "l_forearm" }, { "l_forearm", "l_hand" },
            { "r_shoulder", "r_upperarm" }, { "r_upperarm", "r_forearm" }, { "r_forearm", "r_hand" },
            { "l_thigh", "l_shin" }, { "l_shin", "l_foot" }, { "l_foot", "l_toe" },
            { "r_thigh", "r_shin" }, { "r_shin", "r_foot" }, { "r_foot", "r_toe" }
        };

        private sealed class Move
        {
            public int Frames;
            public float Fps;
            public Vector3[] Positions;
            public Quaternion[] Rotations;
            /// <summary>How high the water stands in the move, for a body 1 m tall.</summary>
            public float Surface;
        }

        private sealed class Rig
        {
            public Transform Model;
            public Transform Top;
            public Vector3 TopHome;
            public Transform[] Bones;
            public Quaternion[] Upright;
            public Quaternion[] Fix;
            public Quaternion[] Saved;
            public float Size;
            public bool Swimming;
            public float Weight;
            public float Clock;
            public float Still;
            public Vector3 LastAt;
            public float Speed;
            public int LastFrame;
            public float RetryAt;
        }

        private static string[] _joints;
        private static int[] _pointsAt;
        private static Vector3[] _restAt;
        private static Quaternion[] _restTurn;
        private static float _restTorso = 0.22f;
        private static Move _swim;
        private static Move _idle;
        private static bool _tried;
        private static readonly Dictionary<ulong, Rig> Rigs = new Dictionary<ulong, Rig>();
        private static readonly List<ulong> Gone = new List<ulong>();
        private static bool _loggedRig;
        private static bool _loggedMissing;

        /// <summary>Called late in every frame, after the game has posed its characters.</summary>
        public static void LateTick()
        {
            if (!Swim.Active)
            {
                if (Rigs.Count > 0)
                {
                    foreach (Rig rig in Rigs.Values)
                    {
                        Release(rig);
                    }
                    Rigs.Clear();
                }
                return;
            }
            UnitManager units = Game.Peek<UnitManager>();
            if (units == null || !Load())
            {
                return;
            }
            Unit me = Game.LocalPlayer;
            int frame = Time.frameCount;
            foreach (Unit unit in units.unitsDict.Values)
            {
                if (unit == null || unit == me || !unit.isActived || unit.transform == null || unit.model == null
                    || !(unit.IsStudent || unit.IsTeacher))
                {
                    continue;
                }
                // The three new teachers have a whole body of their own with its own swim move.
                bool swims = Swim.Swimming(unit) && !Looks.HasOwnBody(unit.UID);
                Rig rig;
                if (!Rigs.TryGetValue(unit.UID, out rig) || rig.Model != unit.model.transform)
                {
                    if (!swims && rig == null)
                    {
                        continue;
                    }
                    if (rig != null)
                    {
                        Release(rig);
                    }
                    rig = Build(unit);
                    rig.RetryAt = Time.time + 0.5f;
                    Rigs[unit.UID] = rig;
                }
                else if (rig.Bones == null && swims && Time.time >= rig.RetryAt)
                {
                    // It could not be set up before (it was lying down or being carried): try again.
                    rig = Build(unit);
                    rig.RetryAt = Time.time + 0.5f;
                    Rigs[unit.UID] = rig;
                }
                rig.LastFrame = frame;
                if (rig.Bones == null)
                {
                    continue;
                }
                Apply(unit, rig, swims);
            }
            // Forget characters that left.
            Gone.Clear();
            foreach (KeyValuePair<ulong, Rig> pair in Rigs)
            {
                if (pair.Value.LastFrame != frame)
                {
                    Gone.Add(pair.Key);
                }
            }
            foreach (ulong uid in Gone)
            {
                Release(Rigs[uid]);
                Rigs.Remove(uid);
            }
        }

        private static void Apply(Unit unit, Rig rig, bool swims)
        {
            float dt = Time.deltaTime;
            Vector3 at = unit.Position;
            if (dt > 0f)
            {
                float moved = Tricks.Flat(at - rig.LastAt).magnitude / dt;
                rig.Speed = Mathf.Lerp(rig.Speed, Mathf.Min(moved, 8f), 1f - Mathf.Exp(-dt * 6f));
            }
            rig.LastAt = at;
            rig.Weight = Mathf.MoveTowards(rig.Weight, swims ? 1f : 0f, dt / 0.3f);
            if (rig.Weight <= 0f)
            {
                if (rig.Swimming)
                {
                    Release(rig);
                }
                return;
            }
            if (!rig.Swimming)
            {
                // Remember how the bones were, to put back the ones the game does not move itself.
                for (int j = 0; j < rig.Bones.Length; j++)
                {
                    rig.Saved[j] = rig.Bones[j] != null ? rig.Bones[j].localRotation : Quaternion.identity;
                }
                rig.Swimming = true;
                rig.Still = rig.Speed > 0.3f ? 0f : 1f;
            }
            // Two moves, eased into each other: strokes while moving, treading water while still.
            rig.Still = Mathf.MoveTowards(rig.Still, rig.Speed > 0.3f ? 0f : 1f, dt / 0.35f);
            rig.Clock += dt * Mathf.Lerp(Mathf.Clamp(rig.Speed / 1.4f, 0.8f, 1.5f), 1f, rig.Still);

            int count = _joints.Length;
            Quaternion facing = Quaternion.Euler(0f, unit.transform.eulerAngles.y, 0f);
            rig.Top.localPosition = rig.TopHome;
            Vector3 hips = Vector3.zero;
            for (int j = 0; j < count; j++)
            {
                Vector3 position;
                Quaternion turn;
                Sample(_swim, rig.Clock, j, count, out position, out turn);
                if (rig.Still > 0f)
                {
                    Vector3 idlePosition;
                    Quaternion idleTurn;
                    Sample(_idle, rig.Clock, j, count, out idlePosition, out idleTurn);
                    position = Vector3.Lerp(position, idlePosition, rig.Still);
                    turn = Quaternion.Slerp(turn, idleTurn, rig.Still);
                }
                if (j == 0)
                {
                    hips = position;
                }
                Transform bone = rig.Bones[j];
                if (bone == null)
                {
                    continue;
                }
                Quaternion world = facing * (turn * Quaternion.Inverse(_restTurn[j])) * rig.Fix[j] * rig.Upright[j];
                Quaternion local = bone.parent != null ? Quaternion.Inverse(bone.parent.rotation) * world : world;
                bone.localRotation = rig.Weight >= 1f ? local : Quaternion.Slerp(bone.localRotation, local, rig.Weight);
            }

            // Float: the hips go where the move has them, measured from the water's surface.
            float surface = Mathf.Lerp(_swim.Surface, _idle.Surface, rig.Still);
            Vector3 target = new Vector3(at.x, Swim.WaterWorldY + (hips.y - surface) * rig.Size, at.z)
                + facing * new Vector3(hips.x, 0f, hips.z) * rig.Size;
            Transform hipBone = rig.Bones[0];
            rig.Top.position += (target - hipBone.position) * rig.Weight;
        }

        private static void Sample(Move move, float time, int joint, int joints, out Vector3 position, out Quaternion turn)
        {
            float at = time * move.Fps;
            int first = (int)at % move.Frames;
            int second = (first + 1) % move.Frames;
            float between = at - Mathf.Floor(at);
            position = Vector3.Lerp(move.Positions[first * joints + joint], move.Positions[second * joints + joint], between);
            turn = Quaternion.Slerp(move.Rotations[first * joints + joint], move.Rotations[second * joints + joint], between);
        }

        private static void Release(Rig rig)
        {
            if (rig == null || !rig.Swimming || rig.Bones == null)
            {
                return;
            }
            rig.Swimming = false;
            rig.Weight = 0f;
            for (int j = 0; j < rig.Bones.Length; j++)
            {
                if (rig.Bones[j] != null)
                {
                    rig.Bones[j].localRotation = rig.Saved[j];
                }
            }
            if (rig.Top != null)
            {
                rig.Top.localPosition = rig.TopHome;
            }
        }

        /// <summary>
        /// Finds the game's bones on one character and notes how they are turned while it stands
        /// upright. Returns a rig without bones when the character can't be used (yet).
        /// </summary>
        private static Rig Build(Unit unit)
        {
            var rig = new Rig { Model = unit.model.transform, LastAt = unit.Position, Clock = (unit.UID % 97) * 0.13f };
            var byName = new Dictionary<string, Transform>();
            foreach (Transform bone in rig.Model.GetComponentsInChildren<Transform>(true))
            {
                if (!byName.ContainsKey(bone.name))
                {
                    byName[bone.name] = bone;
                }
            }
            int count = _joints.Length;
            var bones = new Transform[count];
            int found = 0;
            for (int j = 0; j < count; j++)
            {
                string name;
                Transform bone;
                if (GameBone.TryGetValue(_joints[j], out name) && byName.TryGetValue(name, out bone))
                {
                    bones[j] = bone;
                    found++;
                }
            }
            int hips = 0;
            int neck = System.Array.IndexOf(_joints, "neck");
            int leftArm = System.Array.IndexOf(_joints, "l_upperarm");
            int rightArm = System.Array.IndexOf(_joints, "r_upperarm");
            int leftLeg = System.Array.IndexOf(_joints, "l_thigh");
            int rightLeg = System.Array.IndexOf(_joints, "r_thigh");
            if (neck < 0 || leftArm < 0 || rightArm < 0 || leftLeg < 0 || rightLeg < 0
                || bones[hips] == null || bones[neck] == null || bones[leftArm] == null || bones[rightArm] == null
                || bones[leftLeg] == null || bones[rightLeg] == null)
            {
                if (!_loggedMissing)
                {
                    _loggedMissing = true;
                    Plugin.Log("swim move: this character's bones were not found (" + found + " of " + count + " on " + rig.Model.name + "), it keeps wading");
                }
                return rig;
            }

            // The body's own frame while it stands: up along the spine, right along the shoulders.
            Vector3 up = bones[neck].position - bones[hips].position;
            Vector3 right = bones[rightArm].position - bones[leftArm].position;
            float torso = up.magnitude;
            if (torso < 0.02f || right.magnitude < 0.01f || Vector3.Dot(up.normalized, Vector3.up) < 0.85f)
            {
                // Lying, carried or crawling right now: try again when it stands.
                return rig;
            }
            up.Normalize();
            right = Vector3.ProjectOnPlane(right, up).normalized;
            Vector3 forward = Vector3.Cross(right, up);
            if (Vector3.Dot(forward, unit.transform.forward) < 0f)
            {
                // This skeleton names its sides the other way round: swap left and right.
                for (int j = 0; j < count; j++)
                {
                    if (_joints[j].StartsWith("l_"))
                    {
                        int other = System.Array.IndexOf(_joints, "r_" + _joints[j].Substring(2));
                        if (other >= 0)
                        {
                            Transform swap = bones[j];
                            bones[j] = bones[other];
                            bones[other] = swap;
                        }
                    }
                }
                right = -right;
                forward = -forward;
            }
            Quaternion frame = Quaternion.LookRotation(forward, up);
            Quaternion toBody = Quaternion.Inverse(frame);

            rig.Upright = new Quaternion[count];
            rig.Fix = new Quaternion[count];
            rig.Saved = new Quaternion[count];
            for (int j = 0; j < count; j++)
            {
                rig.Fix[j] = Quaternion.identity;
                rig.Upright[j] = bones[j] != null ? toBody * bones[j].rotation : Quaternion.identity;
            }
            for (int j = 0; j < count; j++)
            {
                int child = _pointsAt[j];
                if (bones[j] == null || child < 0 || bones[child] == null)
                {
                    continue;
                }
                Vector3 mine = toBody * (bones[child].position - bones[j].position);
                Vector3 theirs = _restAt[child] - _restAt[j];
                if (mine.sqrMagnitude > 1e-8f && theirs.sqrMagnitude > 1e-8f)
                {
                    rig.Fix[j] = Quaternion.FromToRotation(mine.normalized, theirs.normalized);
                }
            }
            // A hand carries on the way its forearm points.
            for (int j = 0; j < count; j++)
            {
                if (_joints[j].EndsWith("_hand"))
                {
                    int forearm = System.Array.IndexOf(_joints, _joints[j].Substring(0, 2) + "forearm");
                    if (forearm >= 0)
                    {
                        rig.Fix[j] = rig.Fix[forearm];
                    }
                }
                else if (_pointsAt[j] < 0)
                {
                    // Toes and the top of the head are only there to point at: leave them be.
                    bones[j] = null;
                }
            }

            // The top of the skeleton: the bone right under the character's root that holds the hips.
            Transform top = bones[hips];
            while (top.parent != null && top.parent != rig.Model)
            {
                top = top.parent;
            }
            rig.Top = top;
            rig.TopHome = top.localPosition;
            rig.Size = Mathf.Clamp(torso / _restTorso, 0.6f, 4f);
            rig.Bones = bones;

            if (!_loggedRig)
            {
                _loggedRig = true;
                Plugin.Log("swim move: first character set up, " + found + " of " + count + " bones on " + rig.Model.name
                    + ", skeleton top " + top.name + ", hips under " + (bones[hips].parent != null ? bones[hips].parent.name : "nothing")
                    + ", thigh under " + (bones[leftLeg].parent != null ? bones[leftLeg].parent.name : "nothing")
                    + ", torso " + torso.ToString("F2") + " m, size " + rig.Size.ToString("F2"));
            }
            return rig;
        }

        // ------------------------------------------------------------------------- the file

        private static bool Load()
        {
            if (_tried)
            {
                return _swim != null && _idle != null;
            }
            _tried = true;
            string path = Path.Combine(Plugin.Context.ModDirectory, "models", "moves_swim.cpr");
            try
            {
                if (!File.Exists(path))
                {
                    Plugin.Log("swim move: file not found: " + path);
                    return false;
                }
                CultureInfo plain = CultureInfo.InvariantCulture;
                var restAt = new List<Vector3>();
                var restTurn = new List<Quaternion>();
                var moves = new Dictionary<string, Move>();
                Move current = null;
                var positions = new List<Vector3>();
                var turns = new List<Quaternion>();
                System.Action close = delegate
                {
                    if (current != null)
                    {
                        current.Positions = positions.ToArray();
                        current.Rotations = turns.ToArray();
                    }
                    positions.Clear();
                    turns.Clear();
                };
                foreach (string line in File.ReadAllLines(path))
                {
                    if (line.Length < 3 || line[1] != ' ')
                    {
                        continue;
                    }
                    string[] parts = line.Split(' ');
                    switch (line[0])
                    {
                        case 'j':
                            _joints = line.Substring(2).Trim().Split(' ');
                            break;
                        case 'r':
                            restAt.Add(new Vector3(float.Parse(parts[1], plain), float.Parse(parts[2], plain), float.Parse(parts[3], plain)));
                            restTurn.Add(new Quaternion(float.Parse(parts[4], plain), float.Parse(parts[5], plain), float.Parse(parts[6], plain), float.Parse(parts[7], plain)));
                            break;
                        case 'a':
                            close();
                            current = new Move { Frames = int.Parse(parts[2], plain), Fps = float.Parse(parts[3], plain) };
                            moves[parts[1]] = current;
                            break;
                        case 'p':
                            positions.Add(new Vector3(float.Parse(parts[1], plain), float.Parse(parts[2], plain), float.Parse(parts[3], plain)));
                            turns.Add(new Quaternion(float.Parse(parts[4], plain), float.Parse(parts[5], plain), float.Parse(parts[6], plain), float.Parse(parts[7], plain)));
                            break;
                    }
                }
                close();
                Move swim, idle;
                if (_joints == null || restAt.Count != _joints.Length || _joints[0] != "hips"
                    || !moves.TryGetValue("swim", out swim) || !moves.TryGetValue("swimidle", out idle)
                    || swim.Positions.Length != swim.Frames * _joints.Length || idle.Positions.Length != idle.Frames * _joints.Length)
                {
                    Plugin.Log("swim move: the file is not what was expected: " + path);
                    return false;
                }
                _restAt = restAt.ToArray();
                _restTurn = restTurn.ToArray();
                _pointsAt = new int[_joints.Length];
                for (int j = 0; j < _joints.Length; j++)
                {
                    string child;
                    _pointsAt[j] = PointsAt.TryGetValue(_joints[j], out child) ? System.Array.IndexOf(_joints, child) : -1;
                }
                int neck = System.Array.IndexOf(_joints, "neck");
                if (neck >= 0)
                {
                    _restTorso = Mathf.Max(0.05f, (_restAt[neck] - _restAt[0]).magnitude);
                }
                // Where the water stands in each move: just over the back in the stroke, at the
                // collarbone while treading water.
                swim.Surface = MeanHeight(swim, 0) + 0.07f;
                idle.Surface = neck >= 0 ? MeanHeight(idle, neck) - 0.03f : MeanHeight(idle, 0) + 0.17f;
                _swim = swim;
                _idle = idle;
                Plugin.Log("swim move: loaded " + _joints.Length + " joints, stroke " + swim.Frames + " frames, treading " + idle.Frames
                    + " frames, water at " + swim.Surface.ToString("F2") + " and " + idle.Surface.ToString("F2"));
                return true;
            }
            catch (System.Exception ex)
            {
                Plugin.Log("swim move: could not read " + path + ": " + ex.Message);
                return false;
            }
        }

        private static float MeanHeight(Move move, int joint)
        {
            float sum = 0f;
            int joints = _joints.Length;
            for (int f = 0; f < move.Frames; f++)
            {
                sum += move.Positions[f * joints + joint].y;
            }
            return sum / Mathf.Max(1, move.Frames);
        }

        /// <summary>How high the water stands in a move of the new teachers' own bodies, for a body 1 m tall.</summary>
        public static float SurfaceFor(bool still)
        {
            if (!Load())
            {
                return still ? 0.6f : 0.28f;
            }
            return still ? _idle.Surface : _swim.Surface;
        }
    }
}
