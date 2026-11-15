using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Swimming on the game's own characters (students, bots and the teacher), made in code on
    /// the game's skeleton every frame, after the game's own animation:
    ///
    ///   moving  front crawl: the body lies flat just under the surface with the head up out of
    ///           the water, the arms go round in big circles one after the other (a straight pull
    ///           under the body, a bent elbow over the water) and the legs kick.
    ///   still   treading water: upright with the shoulders at the surface, the hands scull to
    ///           and fro and the legs pedal.
    ///
    /// Each bone is aimed at a direction worked out in the swimmer's own frame (forward, right,
    /// up), turned the shortest way from a fixed resting direction: the spine up, the arms out to
    /// the side, the legs down. None of the moves ever points a bone straight against its resting
    /// direction, so an arm can go all the way round without flipping.
    ///
    /// What the game's skeleton is like (from the self-test's skeleton files): the hips bone is
    /// "gupen", and spine1 and the thighs hang under it. The hands ("shouzhang2") and the feet
    /// hang under "Root", not under the forearms and shins (the game's animations place them with
    /// IK), so they are put at the wrist and the ankle by hand after the arms and legs are posed.
    /// Every bone points along its own up axis.
    ///
    /// A player's own body is left alone unless they watch themselves from behind (SelfView).
    /// The three new teachers have whole bodies of their own with their own swim clips (Looks.cs).
    /// </summary>
    internal static class SwimPose
    {
        // Where the hips are held, measured from the water's surface in lengths of the
        // character's own torso (hips to neck).
        private const float CrawlHips = -0.17f;
        private const float TreadHips = -0.92f;
        /// <summary>One full arm stroke at walking pace takes about 1.5 seconds.</summary>
        private const float StrokeSpeed = 4.2f;

        private sealed class Bone
        {
            public Transform T;
            /// <summary>The resting direction, in the swimmer's frame (no turn about the vertical).</summary>
            public Vector3 RefDir;
            /// <summary>How the bone is turned when it points exactly along RefDir, in the swimmer's frame.</summary>
            public Quaternion RefTurn;
            /// <summary>This frame's turn from the game's own animation (world).</summary>
            public Quaternion Anim;
            /// <summary>The local turn to put back when the character leaves the water.</summary>
            public Quaternion Saved;
        }

        private sealed class Limb
        {
            public Bone Upper;
            public Bone Lower;
            /// <summary>The hand or foot, which does not hang under the limb.</summary>
            public Transform End;
            /// <summary>Where the hand or foot sits, in the lower bone's turn (world lengths).</summary>
            public Vector3 EndOffset;
            /// <summary>The hand's or foot's turn measured from the lower bone's.</summary>
            public Quaternion EndTurn;
            public Vector3 AnimEndAt;
            public Quaternion AnimEndTurn;
            public Vector3 SavedEndAt;
            public Quaternion SavedEndTurn;
            /// <summary>The hand's or foot's turn while standing, in the character's frame (feet stay flat in the dance).</summary>
            public Quaternion EndRest;
            /// <summary>-1 for the character's left, +1 for its right.</summary>
            public float Side;
            /// <summary>Lengths of the upper and lower part, for keeping the feet on the floor when the knees bend.</summary>
            public float UpperLength;
            public float LowerLength;
            /// <summary>The upper bone's own size, put back after a hang stretched the arm.</summary>
            public Vector3 UpperScale;
        }

        private sealed class Rig
        {
            public Transform Model;
            public Transform Top;
            public Vector3 TopHome;
            public Transform HipBone;
            public Bone Hips;
            public Bone Spine1;
            public Bone Spine2;
            public Bone Neck;
            public Bone Head;
            public Limb[] Arms;
            public Limb[] Legs;
            public List<Bone> All;
            public float Torso;
            public bool Ready;
            /// <summary>True while this code poses the character (swimming or dancing).</summary>
            public bool Swimming;
            /// <summary>What it does: 1 swims, 2 dances, 3 the worm, 4 hangs out of a window. Kept while easing back out of it.</summary>
            public int Mode;
            /// <summary>The window it hangs out of (Mode 4).</summary>
            public WindowPoles.Hang Hang;
            public bool LoggedHang;
            /// <summary>How far the top of the head (with its hair or hat) is over the head bone while standing.</summary>
            public float HeadTop;
            /// <summary>True while a hang has the arms stretched.</summary>
            public bool Stretched;
            public float Weight;
            public float Clock;
            public float Still;
            public Vector3 LastAt;
            public float Speed;
            public int LastFrame;
            public float RetryAt;
        }

        private static readonly Dictionary<ulong, Rig> Rigs = new Dictionary<ulong, Rig>();
        private static readonly List<ulong> Gone = new List<ulong>();
        private static bool _loggedRig;
        private static bool _loggedMissing;
        private static bool _loggedError;

        /// <summary>Self-test only: show this player's own body doing strokes although it stands still.</summary>
        public static bool ForceMoving;

        /// <summary>Self-test only: this character stands still for a close picture of its face.</summary>
        public static ulong TestStill;

        /// <summary>Called late in every frame, after the game has posed its characters.</summary>
        public static void LateTick()
        {
            if (!Swim.Active && !Dance.Active && !WindowPoles.AnyHanging)
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
            if (units == null)
            {
                return;
            }
            Unit me = Game.LocalPlayer;
            int frame = Time.frameCount;
            foreach (Unit unit in units.unitsDict.Values)
            {
                if (unit == null || !unit.isActived || unit.transform == null || unit.model == null
                    || !(unit.IsStudent || unit.IsTeacher))
                {
                    continue;
                }
                // A player's own body only swims while they watch themselves from behind;
                // seen through its own eyes it stays as the game has it.
                if (unit == me && !SelfView.Showing)
                {
                    continue;
                }
                bool swims = Swim.Swimming(unit) && !Looks.HasOwnBody(unit.UID);
                int dances = !swims && !Looks.HasOwnBody(unit.UID) ? Dance.Kind(unit) : Dance.None;
                if (TestStill != 0uL && unit.UID == TestStill)
                {
                    swims = false;
                    dances = Dance.None;
                }
                // Hung on a pole at a window: shown hanging outside it by the fingers.
                WindowPoles.Hang hang = default(WindowPoles.Hang);
                bool hangs = unit.IsStudent && !Looks.HasOwnBody(unit.UID) && WindowPoles.HangOf(unit.UID, out hang);
                if (hangs)
                {
                    swims = false;
                    dances = Dance.None;
                }
                bool posed = swims || dances != Dance.None || hangs;
                Rig rig;
                if (!Rigs.TryGetValue(unit.UID, out rig) || rig.Model != unit.model.transform)
                {
                    if (!posed && rig == null)
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
                else if (!rig.Ready && posed && Time.time >= rig.RetryAt)
                {
                    // It could not be set up before (it was lying down or being carried): try again.
                    rig = Build(unit);
                    rig.RetryAt = Time.time + 0.5f;
                    Rigs[unit.UID] = rig;
                }
                rig.LastFrame = frame;
                if (!rig.Ready)
                {
                    continue;
                }
                if (hangs)
                {
                    if (rig.Mode != 4)
                    {
                        // Just put on the pole and still standing: how tall the head is now,
                        // with whatever it wears, so the hang can keep it under the sill.
                        MeasureHead(rig);
                    }
                    rig.Hang = hang;
                    rig.Mode = 4;
                }
                try
                {
                    if (unit == me && ForceMoving)
                    {
                        rig.Speed = 1.3f;
                        rig.LastAt = unit.Position;
                        Apply(unit, rig, swims, dances, true);
                    }
                    else
                    {
                        Apply(unit, rig, swims, dances, false);
                    }
                }
                catch (System.Exception ex)
                {
                    if (!_loggedError)
                    {
                        _loggedError = true;
                        Plugin.Log("swim move: error in the swimming move: " + ex);
                    }
                    rig.Ready = false;
                }
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

        private static void Apply(Unit unit, Rig rig, bool swims, int dances, bool keepSpeed)
        {
            float dt = Time.deltaTime;
            Vector3 at = unit.Position;
            if (dt > 0f && !keepSpeed)
            {
                float moved = Tricks.Flat(at - rig.LastAt).magnitude / dt;
                rig.Speed = Mathf.Lerp(rig.Speed, Mathf.Min(moved, 8f), 1f - Mathf.Exp(-dt * 6f));
            }
            rig.LastAt = at;
            // Still hanging? Let go, the body eases back from the last place it hung.
            WindowPoles.Hang now = default(WindowPoles.Hang);
            bool hangs = rig.Mode == 4 && WindowPoles.HangOf(unit.UID, out now);
            if (hangs)
            {
                rig.Hang = now;
            }
            if (swims || dances != Dance.None)
            {
                rig.Mode = swims ? 1 : dances == Dance.Worm ? 3 : 2;
            }
            rig.Weight = Mathf.MoveTowards(rig.Weight, swims || dances != Dance.None || hangs ? 1f : 0f, dt / 0.3f);
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
                // Remember how the bones were, to put them back when the character leaves the water.
                foreach (Bone bone in rig.All)
                {
                    bone.Saved = bone.T.localRotation;
                }
                foreach (Limb limb in Ends(rig))
                {
                    limb.SavedEndAt = limb.End.localPosition;
                    limb.SavedEndTurn = limb.End.localRotation;
                }
                rig.Swimming = true;
                rig.Still = rig.Speed > 0.3f ? 0f : 1f;
            }
            if (rig.Mode != 4)
            {
                Unstretch(rig);
            }
            if (rig.Mode == 2)
            {
                rig.Clock += dt;
                ApplyDance(unit, rig, rig.Weight);
                return;
            }
            if (rig.Mode == 3)
            {
                // The worm waves a little faster while it creeps along.
                rig.Clock += dt * (rig.Speed > 0.15f ? 1.4f : 1f);
                ApplyWorm(unit, rig, rig.Weight);
                return;
            }
            if (rig.Mode == 4)
            {
                rig.Clock += dt;
                ApplyHang(rig, rig.Weight);
                return;
            }
            // Two moves, eased into each other: the crawl while moving, treading water while still.
            rig.Still = Mathf.MoveTowards(rig.Still, rig.Speed > 0.3f ? 0f : 1f, dt / 0.4f);
            rig.Clock += dt * Mathf.Lerp(Mathf.Clamp(rig.Speed / 1.4f, 0.8f, 1.5f), 1f, rig.Still);
            float weight = rig.Weight;
            float still = rig.Still;

            Quaternion yaw = Quaternion.Euler(0f, unit.transform.eulerAngles.y, 0f);
            Vector3 f = yaw * Vector3.forward;
            Vector3 r = yaw * Vector3.right;
            Vector3 u = Vector3.up;

            // The game's own pose this frame, for easing in and out of the water.
            rig.Top.localPosition = rig.TopHome;
            foreach (Bone bone in rig.All)
            {
                bone.Anim = bone.T.rotation;
            }

            // Float: lift the whole skeleton so the hips sit at the right height under the surface.
            float hipsY = Swim.WaterWorldY + Mathf.Lerp(CrawlHips, TreadHips, still) * rig.Torso;
            rig.Top.position += u * ((hipsY - rig.HipBone.position.y) * weight);
            foreach (Limb limb in Ends(rig))
            {
                limb.AnimEndAt = limb.End.position;
                limb.AnimEndTurn = limb.End.rotation;
            }

            float a = rig.Clock * StrokeSpeed;

            // The body: flat and a little head-up in the crawl, upright when treading water.
            Vector3 torso = Blend(f + u * 0.18f, u + f * 0.12f, still);
            Vector3 neck = Blend(f * 0.6f + u * 0.8f, u + f * 0.05f, still);
            Vector3 head = Blend(u + f * 0.3f, u + f * 0.05f, still);
            Pose(rig.Hips, torso, yaw, weight);
            Pose(rig.Spine1, torso, yaw, weight);
            Pose(rig.Spine2, torso, yaw, weight);
            Pose(rig.Neck, neck, yaw, weight);
            Pose(rig.Head, head, yaw, weight);

            foreach (Limb arm in rig.Arms)
            {
                float s = arm.Side;
                // Crawl: the arm goes forward, down under the body, back to the hip and up over
                // the water to the front again; the other arm is half a turn behind.
                float turn = a + (s > 0f ? Mathf.PI : 0f);
                float cos = Mathf.Cos(turn);
                float sin = Mathf.Sin(turn);
                Vector3 crawlUpper = f * cos - u * sin + r * (s * 0.35f);
                float overWater = Mathf.Max(0f, -sin);
                Vector3 crawlLower = crawlUpper.normalized + (f * 0.9f - u * 0.7f) * overWater;
                // Treading water: the arms reach out to the sides and the hands scull to and fro.
                float scull = Mathf.Sin(a * 0.9f);
                Vector3 treadUpper = Quaternion.AngleAxis(s * 28f * scull, u) * (r * (s * 0.8f) + f * 0.3f - u * 0.5f);
                Vector3 treadLower = f * 0.75f + r * (s * (0.2f + 0.4f * scull)) - u * 0.15f;
                Pose(arm.Upper, Blend(crawlUpper, treadUpper, still), yaw, weight);
                Pose(arm.Lower, Blend(crawlLower, treadLower, still), yaw, weight);
                PlaceEnd(arm, weight, 0f, Vector3.zero);
            }

            foreach (Limb leg in rig.Legs)
            {
                float s = leg.Side;
                float phase = s > 0f ? Mathf.PI : 0f;
                // Crawl: a flutter kick, the shin whipping after the thigh.
                float kick = Mathf.Sin(a * 2f + phase);
                float whip = Mathf.Sin(a * 2f + phase - 0.9f);
                Vector3 crawlUpper = -f + u * (0.22f * kick) + r * (s * 0.1f);
                Vector3 crawlLower = -f + u * (0.4f * whip - 0.08f) + r * (s * 0.06f);
                // Treading water: knees forward, the feet pedalling round under them.
                float pedal = a * 1.1f + phase;
                Vector3 treadUpper = -u * 0.7f + f * 0.55f + r * (s * 0.3f);
                Vector3 treadLower = -u + (r * (s * Mathf.Cos(pedal)) + f * Mathf.Sin(pedal)) * 0.5f - f * 0.1f;
                Vector3 shin = Blend(crawlLower, treadLower, still);
                Pose(leg.Upper, Blend(crawlUpper, treadUpper, still), yaw, weight);
                Pose(leg.Lower, shin, yaw, weight);
                // Toes pointed in the crawl.
                PlaceEnd(leg, weight, 0.75f * (1f - still), shin);
            }
        }

        /// <summary>Beats a second of the dance, the same as the dance hall's lights.</summary>
        private const float DanceBeats = 2f;

        /// <summary>
        /// The dance class's silly chicken dance: elbows out and flapping like wings with the
        /// hands tucked in at the chest, the head pecking forward on the beat, the knees bouncing
        /// (the body goes down with them, so the feet stay flat on the floor) and a sway from
        /// side to side.
        /// </summary>
        private static void ApplyDance(Unit unit, Rig rig, float weight)
        {
            Quaternion yaw = Quaternion.Euler(0f, unit.transform.eulerAngles.y, 0f);
            Vector3 f = yaw * Vector3.forward;
            Vector3 r = yaw * Vector3.right;
            Vector3 u = Vector3.up;

            rig.Top.localPosition = rig.TopHome;
            foreach (Bone bone in rig.All)
            {
                bone.Anim = bone.T.rotation;
            }

            float beat = rig.Clock * DanceBeats * Mathf.PI * 2f;
            float bounce = 0.5f - 0.5f * Mathf.Cos(beat);
            float sway = Mathf.Sin(beat * 0.5f);
            float peck = Mathf.Max(0f, Mathf.Sin(beat));
            float flap = 0.5f + 0.5f * Mathf.Sin(beat * 2f);

            // The knees: the thighs tip forward and the shins back by the same angle, and the
            // whole body goes down by as much as the legs get shorter.
            float knee = (8f + 22f * bounce) * Mathf.Deg2Rad;
            float drop = 0f;
            foreach (Limb leg in rig.Legs)
            {
                drop = Mathf.Max(drop, (leg.UpperLength + leg.LowerLength) * (1f - Mathf.Cos(knee)));
            }
            rig.Top.position -= u * (drop * weight);
            foreach (Limb limb in Ends(rig))
            {
                limb.AnimEndAt = limb.End.position;
                limb.AnimEndTurn = limb.End.rotation;
            }

            Vector3 torso = (u + f * 0.15f + r * (0.12f * sway)).normalized;
            Pose(rig.Hips, torso, yaw, weight);
            Pose(rig.Spine1, torso, yaw, weight);
            Pose(rig.Spine2, torso, yaw, weight);
            Pose(rig.Neck, (u + f * (0.2f + 0.3f * peck)).normalized, yaw, weight);
            Pose(rig.Head, (u + f * (0.1f + 0.45f * peck) + r * (0.08f * sway)).normalized, yaw, weight);

            foreach (Limb arm in rig.Arms)
            {
                float s = arm.Side;
                float lift = (5f + 40f * flap) * Mathf.Deg2Rad;
                Vector3 upper = r * (s * Mathf.Cos(lift)) + u * Mathf.Sin(lift) - f * 0.25f;
                Vector3 lower = f * 0.6f - r * (s * 0.7f) + u * 0.05f;
                Pose(arm.Upper, upper.normalized, yaw, weight);
                Pose(arm.Lower, lower.normalized, yaw, weight);
                PlaceEnd(arm, weight, 0f, Vector3.zero);
            }
            foreach (Limb leg in rig.Legs)
            {
                float s = leg.Side;
                Vector3 thigh = -u * Mathf.Cos(knee) + f * Mathf.Sin(knee) + r * (s * 0.06f);
                Vector3 shin = -u * Mathf.Cos(knee) - f * Mathf.Sin(knee) + r * (s * 0.03f);
                Pose(leg.Upper, thigh.normalized, yaw, weight);
                Pose(leg.Lower, shin.normalized, yaw, weight);
                PlaceFlat(leg, weight, yaw);
            }
        }

        /// <summary>Waves a second of the worm.</summary>
        private const float WormWaves = 1.2f;

        /// <summary>
        /// The dance class's naughty dance, the worm: face down on the floor, a wave rolling
        /// through the body from the feet to the head (the legs, then the hips, the chest and
        /// the head come up in turn), elbows out with the hands down by the shoulders.
        /// </summary>
        private static void ApplyWorm(Unit unit, Rig rig, float weight)
        {
            Quaternion yaw = Quaternion.Euler(0f, unit.transform.eulerAngles.y, 0f);
            Vector3 f = yaw * Vector3.forward;
            Vector3 r = yaw * Vector3.right;
            Vector3 u = Vector3.up;

            rig.Top.localPosition = rig.TopHome;
            foreach (Bone bone in rig.All)
            {
                bone.Anim = bone.T.rotation;
            }

            float t = rig.Clock * WormWaves * Mathf.PI * 2f;
            float legs = Mathf.Sin(t + 1.2f);
            float hipsUp = Mathf.Sin(t);
            float chestUp = Mathf.Sin(t - 1.2f);
            float headUp = Mathf.Sin(t - 2.4f);

            // Down on the floor: the hips just over it, coming up a little as the wave passes.
            float hipsY = unit.Position.y + rig.Torso * (0.38f + 0.3f * Mathf.Max(0f, hipsUp));
            rig.Top.position += u * ((hipsY - rig.HipBone.position.y) * weight);
            foreach (Limb limb in Ends(rig))
            {
                limb.AnimEndAt = limb.End.position;
                limb.AnimEndTurn = limb.End.rotation;
            }

            Vector3 hips = (f + u * (0.45f * hipsUp)).normalized;
            Vector3 chest = (f + u * (0.55f * chestUp)).normalized;
            Pose(rig.Hips, hips, yaw, weight);
            Pose(rig.Spine1, hips, yaw, weight);
            Pose(rig.Spine2, chest, yaw, weight);
            Pose(rig.Neck, (f + u * (0.7f + 0.5f * headUp)).normalized, yaw, weight);
            Pose(rig.Head, (u + f * (0.55f - 0.3f * headUp)).normalized, yaw, weight);

            foreach (Limb arm in rig.Arms)
            {
                float s = arm.Side;
                Vector3 upper = r * (s * 0.75f) - u * (0.45f + 0.25f * Mathf.Max(0f, chestUp)) - f * 0.15f;
                Vector3 lower = f * 0.55f - u * 0.7f - r * (s * 0.2f);
                Pose(arm.Upper, upper.normalized, yaw, weight);
                Pose(arm.Lower, lower.normalized, yaw, weight);
                PlaceEnd(arm, weight, 0f, Vector3.zero);
            }
            foreach (Limb leg in rig.Legs)
            {
                float s = leg.Side;
                Vector3 thigh = -f + u * (0.35f * legs) + r * (s * 0.06f);
                Vector3 shin = (-f + u * (0.55f * Mathf.Sin(t + 2.4f)) + r * (s * 0.04f)).normalized;
                Pose(leg.Upper, thigh.normalized, yaw, weight);
                Pose(leg.Lower, shin, yaw, weight);
                // Toes pointed back.
                PlaceEnd(leg, weight, 0.8f, shin);
            }
        }

        /// <summary>How far under the sill the top of the head (or the hair) is kept, so nobody inside sees it.</summary>
        private const float HangHide = 0.1f;

        private static int _groundMask = -1;

        /// <summary>
        /// Hung at a window: outside it, hanging by the hands from the sill. The body hangs
        /// outside the wall below the window, facing in, so far down that the top of the head is
        /// under the sill: from inside the room only the fingers show over it. The arms reach up
        /// to the sill straight, stretched as long as they need to be (these characters have
        /// big heads and short arms), the head is tipped back looking up and the legs dangle,
        /// kicking a little. Where the ground outside is too near for that (a classroom on the
        /// ground floor), the student kneels on it instead.
        /// </summary>
        private static void ApplyHang(Rig rig, float weight)
        {
            WindowPoles.Hang hang = rig.Hang;
            Vector3 f = -hang.Out;
            Quaternion yaw = Quaternion.LookRotation(f, Vector3.up);
            Vector3 r = yaw * Vector3.right;
            Vector3 u = Vector3.up;

            rig.Top.localPosition = rig.TopHome;
            Unstretch(rig);
            foreach (Bone bone in rig.All)
            {
                bone.Anim = bone.T.rotation;
            }
            foreach (Limb limb in Ends(rig))
            {
                limb.AnimEndAt = limb.End.position;
                limb.AnimEndTurn = limb.End.rotation;
            }

            float t = rig.Clock;
            float sway = Mathf.Sin(t * 1.7f);
            PoseHangBody(rig, yaw, f, r, u, sway, 0f, weight);

            // Where the hips go: so low that the top of the head is under the sill, and the
            // shoulders at least an arm's length under the hands on it; a little outside the wall,
            // so the big head is clear of it.
            Vector3 hips = rig.HipBone.position;
            Vector3 shoulders = (rig.Arms[0].Upper.T.position + rig.Arms[1].Upper.T.position) * 0.5f;
            float reach = (rig.Arms[0].UpperLength + rig.Arms[0].LowerLength) * 0.96f;
            float hipsY = Mathf.Min(hang.Sill.y - HangHide - (HeadTopY(rig) - hips.y), hang.Sill.y + 0.03f - reach - (shoulders.y - hips.y));
            Vector3 spot = hang.Sill + hang.Out * 0.38f;
            float ground;
            bool grounded = GroundUnder(spot, hang.Sill.y, out ground);
            float lean = 0f;
            if (grounded)
            {
                float lowest = ground + Mathf.Max(0.07f, rig.Legs[0].UpperLength * 0.4f);
                if (hipsY < lowest)
                {
                    // Too near the ground to hang that low (a classroom on the ground floor): kneel
                    // on it, leaning back from the wall until the head is under the sill.
                    hipsY = lowest;
                    float tallest = hang.Sill.y - HangHide - hipsY;
                    float low = 0f, high = 55f;
                    for (int i = 0; i < 7; i++)
                    {
                        float mid = (low + high) * 0.5f;
                        PoseHangBody(rig, yaw, f, r, u, sway, mid, weight);
                        if (HeadTopY(rig) - rig.HipBone.position.y > tallest)
                        {
                            low = mid;
                        }
                        else
                        {
                            high = mid;
                        }
                    }
                    lean = high;
                    PoseHangBody(rig, yaw, f, r, u, sway, lean, weight);
                }
            }
            Vector3 hipsBefore = rig.HipBone.position;
            rig.Top.position += (new Vector3(spot.x, hipsY, spot.z) - rig.HipBone.position) * weight;

            float most = 1f;
            foreach (Limb arm in rig.Arms)
            {
                // The hands over the sill's edge, a shoulder's width apart.
                Vector3 hand = hang.Sill - hang.Out * 0.03f + u * 0.03f + r * (arm.Side * 0.13f);
                Vector3 shoulder = arm.Upper.T.position;
                Vector3 direction = (hand - shoulder).normalized;
                Pose(arm.Upper, direction, yaw, weight);
                Pose(arm.Lower, direction, yaw, weight);
                // An arm too short to reach the sill from down there is stretched along its length
                // (the forearm, pointing the same way, stretches with the upper arm).
                float stretch = Mathf.Clamp((hand - shoulder).magnitude / Mathf.Max(0.01f, arm.UpperLength + arm.LowerLength), 1f, 3f);
                stretch = Mathf.Lerp(1f, stretch, weight);
                if (stretch > 1.001f)
                {
                    arm.Upper.T.localScale = Vector3.Scale(arm.UpperScale, new Vector3(1f, stretch, 1f));
                    rig.Stretched = true;
                }
                most = Mathf.Max(most, stretch);
                PlaceEnd(arm, weight, 0f, Vector3.zero, stretch);
            }

            hipsY = rig.HipBone.position.y;
            foreach (Limb leg in rig.Legs)
            {
                float kick = Mathf.Sin(t * 2.4f + (leg.Side > 0f ? Mathf.PI : 0f));
                float thighLength = Mathf.Max(0.01f, leg.UpperLength);
                float shinLength = Mathf.Max(0.01f, leg.LowerLength);
                // How far down the ankle may go before the foot is on the ground.
                float room = grounded ? hipsY - ground - 0.07f : 99f;
                Vector3 thigh, shin;
                if (room >= thighLength + shinLength)
                {
                    // Dangling, kicking a little.
                    thigh = -u + f * (0.06f + 0.1f * kick) + r * (leg.Side * 0.05f);
                    shin = -u - f * (0.04f + 0.12f * kick);
                }
                else if (room >= thighLength)
                {
                    // The thigh down, the shin bent back so the foot stays on the ground.
                    float down = Mathf.Clamp01((room - thighLength) / shinLength);
                    thigh = -u + f * 0.08f + r * (leg.Side * 0.06f);
                    shin = -u * down - f * Mathf.Sqrt(1f - down * down);
                }
                else
                {
                    // Kneeling: the knees forward against the wall, the shins flat on the ground behind.
                    float down = Mathf.Clamp(room / thighLength, 0.15f, 1f);
                    thigh = -u * down + f * Mathf.Sqrt(1f - down * down) + r * (leg.Side * 0.08f);
                    shin = -f - u * 0.08f;
                }
                shin = shin.normalized;
                Pose(leg.Upper, thigh.normalized, yaw, weight);
                Pose(leg.Lower, shin, yaw, weight);
                PlaceEnd(leg, weight, 0.6f, shin);
            }

            if (!rig.LoggedHang && weight >= 1f)
            {
                rig.LoggedHang = true;
                float topOver = HeadTopY(rig) - hang.Sill.y;
                Plugin.Log("swim move: hanging at a window: body from " + hipsBefore.ToString("F2") + " to " + rig.HipBone.position.ToString("F2")
                    + ", sill " + hang.Sill.ToString("F2") + ", top of the head " + topOver.ToString("F2") + " m over the sill (under 0 is hidden)"
                    + ", head " + rig.HeadTop.ToString("F2") + " m over its bone, arms stretched x" + most.ToString("F1")
                    + (grounded ? ", ground outside at " + ground.ToString("F2") + ", leaning back " + lean.ToString("F0") + " degrees" : ", no ground under it"));
            }
        }

        /// <summary>
        /// The hang's body: the torso up, leaning back from the wall by lean degrees, the head
        /// tipped back looking up at the hands.
        /// </summary>
        private static void PoseHangBody(Rig rig, Quaternion yaw, Vector3 f, Vector3 r, Vector3 u, float sway, float lean, float weight)
        {
            // A turn about the right-hand side by minus the angle tips "up" away from the wall.
            Quaternion back = Quaternion.AngleAxis(-lean, r);
            Vector3 torso = back * (u + f * 0.06f + r * (0.05f * sway)).normalized;
            Pose(rig.Hips, torso, yaw, weight);
            Pose(rig.Spine1, torso, yaw, weight);
            Pose(rig.Spine2, torso, yaw, weight);
            Pose(rig.Neck, back * (u + f * 0.04f).normalized, yaw, weight);
            Pose(rig.Head, back * (u - f * 0.3f).normalized, yaw, weight);
        }

        /// <summary>
        /// The height of the top of the head as it is posed now: the head taken as a ball HeadTop
        /// across sitting on its bone, so tipping it back brings its top down.
        /// </summary>
        private static float HeadTopY(Rig rig)
        {
            Transform head = rig.Head.T;
            float half = rig.HeadTop * 0.5f;
            return head.position.y + (head.rotation * Vector3.up).y * half + half;
        }

        /// <summary>Puts the arms back to their own length after a hang stretched them.</summary>
        private static void Unstretch(Rig rig)
        {
            if (!rig.Stretched)
            {
                return;
            }
            rig.Stretched = false;
            foreach (Limb arm in rig.Arms)
            {
                if (arm.Upper.T != null)
                {
                    arm.Upper.T.localScale = arm.UpperScale;
                }
            }
        }

        /// <summary>The height of the ground (or a floor) under a spot outside a window, if there is one within 3 m.</summary>
        private static bool GroundUnder(Vector3 at, float sillY, out float ground)
        {
            ground = 0f;
            if (_groundMask < 0)
            {
                _groundMask = LayerMask.GetMask("Ground", "Obstacle", "Wall", "Default");
            }
            // Straight down from just under the sill, so a ledge outside the window doesn't count.
            Vector3 start = new Vector3(at.x, sillY - 0.1f, at.z);
            RaycastHit hit;
            if (Physics.Raycast(start, Vector3.down, out hit, 3f, _groundMask, QueryTriggerInteraction.Ignore))
            {
                ground = hit.point.y;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Notes how far the top of the head, with the hair, hat or anything else on it, is over
        /// the head bone, while the character stands upright (otherwise it keeps what it had).
        /// </summary>
        private static void MeasureHead(Rig rig)
        {
            Transform head = rig.Head != null ? rig.Head.T : null;
            Transform neck = rig.Neck != null ? rig.Neck.T : null;
            if (head == null || neck == null || rig.HipBone == null || rig.Model == null)
            {
                return;
            }
            Vector3 spine = neck.position - rig.HipBone.position;
            if (spine.sqrMagnitude < 0.0004f || Vector3.Dot(spine.normalized, Vector3.up) < 0.85f)
            {
                return;
            }
            float bone = head.position.y;
            float top = bone;
            foreach (Renderer part in rig.Model.GetComponentsInChildren<Renderer>())
            {
                if (part == null || !part.enabled || part.forceRenderingOff || !(part is SkinnedMeshRenderer || part is MeshRenderer)
                    || part.GetComponent<TextMesh>() != null || part.GetComponent("TextMeshPro") != null)
                {
                    continue;
                }
                Bounds box = part.bounds;
                // Anything floating over the head (a name, a sign) is not part of it.
                if (box.min.y > bone + rig.Torso)
                {
                    continue;
                }
                top = Mathf.Max(top, box.max.y);
            }
            rig.HeadTop = Mathf.Clamp(top - bone, rig.Torso * 0.5f, rig.Torso * 2.5f);
        }

        /// <summary>Puts a foot at the ankle, kept flat the way it was while the character stood.</summary>
        private static void PlaceFlat(Limb limb, float weight, Quaternion yaw)
        {
            if (limb.End == null)
            {
                return;
            }
            Transform lower = limb.Lower.T;
            Vector3 place = lower.position + lower.rotation * limb.EndOffset;
            Quaternion turn = yaw * limb.EndRest;
            limb.End.position = Vector3.Lerp(limb.AnimEndAt, place, weight);
            limb.End.rotation = weight >= 1f ? turn : Quaternion.Slerp(limb.AnimEndTurn, turn, weight);
        }

        private static Vector3 Blend(Vector3 crawl, Vector3 tread, float still)
        {
            Vector3 one = crawl.normalized;
            Vector3 two = tread.normalized;
            if (still <= 0f)
            {
                return one;
            }
            if (still >= 1f)
            {
                return two;
            }
            return Vector3.Slerp(one, two, still).normalized;
        }

        /// <summary>Turns a bone (the shortest way from its resting direction) to point along a direction.</summary>
        private static void Pose(Bone bone, Vector3 direction, Quaternion yaw, float weight)
        {
            if (bone == null)
            {
                return;
            }
            Quaternion want = Quaternion.FromToRotation(yaw * bone.RefDir, direction) * (yaw * bone.RefTurn);
            bone.T.rotation = weight >= 1f ? want : Quaternion.Slerp(bone.Anim, want, weight);
        }

        /// <summary>Puts a hand at the wrist or a foot at the ankle, and points a foot along the shin.</summary>
        private static void PlaceEnd(Limb limb, float weight, float point, Vector3 along, float stretch = 1f)
        {
            if (limb.End == null)
            {
                return;
            }
            Transform lower = limb.Lower.T;
            Vector3 place = lower.position + lower.rotation * (limb.EndOffset * stretch);
            Quaternion turn = lower.rotation * limb.EndTurn;
            if (point > 0f)
            {
                Vector3 toes = turn * Vector3.up;
                turn = Quaternion.FromToRotation(toes, Vector3.Slerp(toes, along, point)) * turn;
            }
            limb.End.position = Vector3.Lerp(limb.AnimEndAt, place, weight);
            limb.End.rotation = weight >= 1f ? turn : Quaternion.Slerp(limb.AnimEndTurn, turn, weight);
        }

        private static IEnumerable<Limb> Ends(Rig rig)
        {
            foreach (Limb limb in rig.Arms)
            {
                if (limb.End != null)
                {
                    yield return limb;
                }
            }
            foreach (Limb limb in rig.Legs)
            {
                if (limb.End != null)
                {
                    yield return limb;
                }
            }
        }

        private static void Release(Rig rig)
        {
            if (rig == null || !rig.Swimming || !rig.Ready)
            {
                return;
            }
            rig.Swimming = false;
            rig.Weight = 0f;
            Unstretch(rig);
            foreach (Bone bone in rig.All)
            {
                if (bone.T != null)
                {
                    bone.T.localRotation = bone.Saved;
                }
            }
            foreach (Limb limb in Ends(rig))
            {
                limb.End.localPosition = limb.SavedEndAt;
                limb.End.localRotation = limb.SavedEndTurn;
            }
            if (rig.Top != null)
            {
                rig.Top.localPosition = rig.TopHome;
            }
        }

        // ------------------------------------------------------------------------- setting up

        /// <summary>
        /// Finds the game's bones on one character and notes how each is turned while it stands
        /// upright. Returns a rig that is not ready when the character can't be used (yet).
        /// </summary>
        private static Rig Build(Unit unit)
        {
            var rig = new Rig { Model = unit.model.transform, LastAt = unit.Position, Clock = (unit.UID % 97) * 0.13f };
            var byName = new Dictionary<string, Transform>();
            foreach (Transform part in rig.Model.GetComponentsInChildren<Transform>(true))
            {
                if (!byName.ContainsKey(part.name))
                {
                    byName[part.name] = part;
                }
            }
            Transform hips = Find(byName, "gupen", "hips", "Hips");
            Transform spine1 = Find(byName, "spine1", "spine", "Spine");
            Transform spine2 = Find(byName, "spine2", "chest", "Spine1");
            Transform neck = Find(byName, "neck", "Neck");
            Transform head = Find(byName, "head", "Head");
            Transform upperL = Find(byName, "upper_arm.L"), lowerL = Find(byName, "forearm.L");
            Transform upperR = Find(byName, "upper_arm.R"), lowerR = Find(byName, "forearm.R");
            Transform thighL = Find(byName, "thigh.L"), shinL = Find(byName, "shin.L");
            Transform thighR = Find(byName, "thigh.R"), shinR = Find(byName, "shin.R");
            if (hips == null || neck == null || head == null || upperL == null || lowerL == null || upperR == null
                || lowerR == null || thighL == null || shinL == null || thighR == null || shinR == null)
            {
                if (!_loggedMissing)
                {
                    _loggedMissing = true;
                    Plugin.Log("swim move: this character's bones were not found on " + rig.Model.name + ", it keeps wading");
                }
                return rig;
            }

            Vector3 spine = neck.position - hips.position;
            float torso = spine.magnitude;
            if (torso < 0.02f || Vector3.Dot(spine / torso, Vector3.up) < 0.85f)
            {
                // Lying, carried or crawling right now: try again when it stands.
                return rig;
            }
            Quaternion yaw = Quaternion.Euler(0f, unit.transform.eulerAngles.y, 0f);
            Vector3 right = yaw * Vector3.right;

            rig.HipBone = hips;
            rig.Torso = torso;
            rig.All = new List<Bone>();
            // The hips bone sits where the spine starts; its own axis says nothing, so the
            // whole torso's direction stands in for it.
            rig.Hips = MakeBone(rig, hips, spine / torso, Vector3.up, yaw);
            rig.Spine1 = spine1 != null && spine1 != hips ? MakeBone(rig, spine1, Axis(spine1), Vector3.up, yaw) : null;
            rig.Spine2 = spine2 != null && spine2 != spine1 ? MakeBone(rig, spine2, Axis(spine2), Vector3.up, yaw) : null;
            rig.Neck = MakeBone(rig, neck, Axis(neck), Vector3.up, yaw);
            rig.Head = MakeBone(rig, head, Axis(head), Vector3.up, yaw);

            float sideL = Mathf.Sign(Vector3.Dot(upperL.position - hips.position, right));
            float sideR = Mathf.Sign(Vector3.Dot(upperR.position - hips.position, right));
            if (sideL == sideR)
            {
                sideL = -1f;
                sideR = 1f;
            }
            rig.Arms = new[]
            {
                MakeLimb(rig, upperL, lowerL, Find(byName, "shouzhang2.L", "hand.L"), new Vector3(sideL, 0f, 0f), sideL, yaw),
                MakeLimb(rig, upperR, lowerR, Find(byName, "shouzhang2.R", "hand.R"), new Vector3(sideR, 0f, 0f), sideR, yaw)
            };
            rig.Legs = new[]
            {
                MakeLimb(rig, thighL, shinL, Find(byName, "foot.L"), Vector3.down, sideL, yaw),
                MakeLimb(rig, thighR, shinR, Find(byName, "foot.R"), Vector3.down, sideR, yaw)
            };

            // The top of the skeleton: the part right under the character's root that holds the hips.
            Transform top = hips;
            while (top.parent != null && top.parent != rig.Model)
            {
                top = top.parent;
            }
            rig.Top = top;
            rig.TopHome = top.localPosition;
            rig.Ready = true;
            rig.HeadTop = torso * 1.3f;
            MeasureHead(rig);

            if (!_loggedRig)
            {
                _loggedRig = true;
                Plugin.Log("swim move: first character set up on " + rig.Model.name + ", skeleton top " + top.name
                    + ", torso " + torso.ToString("F2") + " m, hands " + (rig.Arms[0].End != null ? rig.Arms[0].End.name : "none")
                    + ", feet " + (rig.Legs[0].End != null ? rig.Legs[0].End.name : "none") + ", sides " + sideL + "/" + sideR);
            }
            return rig;
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

        /// <summary>The way a bone points: on the game's skeleton every bone points along its up axis.</summary>
        private static Vector3 Axis(Transform bone)
        {
            return bone.rotation * Vector3.up;
        }

        /// <summary>Notes how a bone would be turned if it pointed exactly along its resting direction.</summary>
        private static Bone MakeBone(Rig rig, Transform bone, Vector3 pointsNow, Vector3 restInFrame, Quaternion yaw)
        {
            Vector3 rest = yaw * restInFrame;
            Quaternion atRest = Quaternion.FromToRotation(pointsNow, rest) * bone.rotation;
            var made = new Bone
            {
                T = bone,
                RefDir = restInFrame,
                RefTurn = Quaternion.Inverse(yaw) * atRest,
                Saved = bone.localRotation
            };
            rig.All.Add(made);
            return made;
        }

        private static Limb MakeLimb(Rig rig, Transform upper, Transform lower, Transform end, Vector3 restInFrame, float side, Quaternion yaw)
        {
            var limb = new Limb
            {
                Upper = MakeBone(rig, upper, Axis(upper), restInFrame, yaw),
                Lower = MakeBone(rig, lower, Axis(lower), restInFrame, yaw),
                Side = side,
                UpperLength = (lower.position - upper.position).magnitude,
                UpperScale = upper.localScale
            };
            // A hand or foot that hangs under the limb follows it anyway; only one that hangs
            // elsewhere has to be put in place.
            if (end != null && !end.IsChildOf(lower))
            {
                limb.End = end;
                Quaternion back = Quaternion.Inverse(lower.rotation);
                limb.EndOffset = back * (end.position - lower.position);
                limb.EndTurn = back * end.rotation;
                limb.EndRest = Quaternion.Inverse(yaw) * end.rotation;
                limb.SavedEndAt = end.localPosition;
                limb.SavedEndTurn = end.localRotation;
                limb.LowerLength = limb.EndOffset.magnitude;
            }
            if (limb.LowerLength <= 0.001f)
            {
                limb.LowerLength = limb.UpperLength;
            }
            return limb;
        }

        // ------------------------------------------------------------------------- the new teachers

        // The three new teachers' own bodies play Meshy's swim clips (Looks.cs). How high the
        // water stands in those clips comes from the same clips, stored in moves_swim.cpr.

        private static bool _tried;
        private static float _strokeSurface = 0.28f;
        private static float _treadSurface = 0.6f;

        /// <summary>How high the water stands in a move of the new teachers' own bodies, for a body 1 m tall.</summary>
        public static float SurfaceFor(bool still)
        {
            LoadSurfaces();
            return still ? _treadSurface : _strokeSurface;
        }

        private static void LoadSurfaces()
        {
            if (_tried)
            {
                return;
            }
            _tried = true;
            string path = Path.Combine(Plugin.Context.ModDirectory, "models", "moves_swim.cpr");
            try
            {
                if (!File.Exists(path))
                {
                    Plugin.Log("swim move: file not found: " + path);
                    return;
                }
                CultureInfo plain = CultureInfo.InvariantCulture;
                string[] joints = null;
                string move = null;
                int hips = 0, neck = -1, joint = 0;
                float strokeSum = 0f, treadSum = 0f;
                int strokeCount = 0, treadCount = 0;
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
                            joints = line.Substring(2).Trim().Split(' ');
                            neck = System.Array.IndexOf(joints, "neck");
                            break;
                        case 'a':
                            move = parts[1];
                            joint = 0;
                            break;
                        case 'p':
                            if (joints == null)
                            {
                                break;
                            }
                            float y = float.Parse(parts[2], plain);
                            int which = joint % joints.Length;
                            if (move == "swim" && which == hips)
                            {
                                strokeSum += y;
                                strokeCount++;
                            }
                            else if (move == "swimidle" && which == (neck >= 0 ? neck : hips))
                            {
                                treadSum += y;
                                treadCount++;
                            }
                            joint++;
                            break;
                    }
                }
                // Just over the back in the stroke, at the collarbone while treading water.
                if (strokeCount > 0)
                {
                    _strokeSurface = strokeSum / strokeCount + 0.07f;
                }
                if (treadCount > 0)
                {
                    _treadSurface = treadSum / treadCount + (neck >= 0 ? -0.03f : 0.17f);
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log("swim move: could not read " + path + ": " + ex.Message);
            }
        }
    }
}
