using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ClassPack
{
    /// <summary>One move of a character: where every bone is in every frame.</summary>
    internal sealed class CharacterClip
    {
        public float Fps;
        public int Frames;
        public Vector3[] Positions;
        public Quaternion[] Rotations;
    }

    /// <summary>A character file once read: the body, its picture and its moves.</summary>
    internal sealed class CharacterData
    {
        public Mesh Mesh;
        public Material Material;
        public int Bones;
        /// <summary>Where each bone is at rest, in whole-body space (1 m tall, facing +z).</summary>
        public Vector3[] Rest;
        public readonly Dictionary<string, CharacterClip> Clips = new Dictionary<string, CharacterClip>();
    }

    /// <summary>
    /// One character standing in the game: a body with bones that this class moves itself, frame
    /// by frame, from the moves in the file. All bones hang side by side under one root, because
    /// the file stores each bone's place in whole-body space.
    /// </summary>
    internal sealed class CharacterView
    {
        public readonly GameObject Root;
        public readonly SkinnedMeshRenderer Renderer;

        private readonly CharacterData _data;
        private readonly Transform[] _bones;
        /// <summary>Upper arm, forearm and hand on each side, or null when they can't be told apart.</summary>
        private readonly int[] _armLeft;
        private readonly int[] _armRight;
        private CharacterClip _clip;
        private CharacterClip _before;
        private string _playing;
        private float _time;
        private float _beforeTime;
        private float _blend = 1f;

        public CharacterView(CharacterData data, string name)
        {
            _data = data;
            Root = new GameObject("[ClassPack " + name + "]");
            _bones = new Transform[data.Bones];
            for (int i = 0; i < data.Bones; i++)
            {
                var bone = new GameObject("bone" + i);
                bone.transform.SetParent(Root.transform, false);
                _bones[i] = bone.transform;
            }
            var skin = new GameObject("skin");
            skin.transform.SetParent(Root.transform, false);
            Renderer = skin.AddComponent<SkinnedMeshRenderer>();
            Renderer.sharedMesh = data.Mesh;
            Renderer.bones = _bones;
            Renderer.rootBone = Root.transform;
            Renderer.sharedMaterial = data.Material;
            Renderer.updateWhenOffscreen = true;
            Renderer.localBounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 1.4f, 1.4f));
            _armLeft = FindArm(data, -1f);
            _armRight = FindArm(data, 1f);
            Play("idle");
            Tick(0f);
        }

        /// <summary>0 arms as the move has them, 1 both arms held out in front (carrying someone).</summary>
        public float Hold;

        /// <summary>How wide the body is (the body slider) and how big the head (the head slider).</summary>
        public float Width = 1f;
        public float Head = 1f;

        /// <summary>
        /// The arm on one side: the bones that hang out to that side between hip and shoulder
        /// height, from the body outwards (upper arm, forearm, hand). Meshy's characters have
        /// exactly three there; anything else and the arms are left alone.
        /// </summary>
        private static int[] FindArm(CharacterData data, float side)
        {
            if (data.Rest == null)
            {
                return null;
            }
            var found = new List<int>();
            for (int i = 0; i < data.Rest.Length; i++)
            {
                Vector3 at = data.Rest[i];
                if (at.x * side > 0.09f && at.y > 0.33f && at.y < 0.8f)
                {
                    found.Add(i);
                }
            }
            if (found.Count != 3)
            {
                return null;
            }
            found.Sort((a, b) => Mathf.Abs(data.Rest[a].x).CompareTo(Mathf.Abs(data.Rest[b].x)));
            return found.ToArray();
        }

        /// <summary>Swings one whole arm about the shoulder joint so it points forward, a little down and in.</summary>
        private void HoldOut(int[] arm, float side)
        {
            Vector3 pivot = _bones[arm[0]].localPosition;
            Vector3 reach = _bones[arm[2]].localPosition - pivot;
            if (reach.sqrMagnitude < 1e-8f)
            {
                return;
            }
            Vector3 want = new Vector3(-side * 0.2f, -0.35f, 1f);
            Quaternion swing = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(reach, want), Hold);
            foreach (int i in arm)
            {
                Transform bone = _bones[i];
                bone.localPosition = pivot + swing * (bone.localPosition - pivot);
                bone.localRotation = swing * bone.localRotation;
            }
        }

        public bool Has(string name)
        {
            return _data.Clips.ContainsKey(name);
        }

        /// <summary>Switches to another move, easing over a fifth of a second.</summary>
        public void Play(string name)
        {
            CharacterClip next;
            if (name == _playing || !_data.Clips.TryGetValue(name, out next))
            {
                return;
            }
            _before = _clip;
            _beforeTime = _time;
            _blend = _clip == null ? 1f : 0f;
            _clip = next;
            _playing = name;
            _time = 0f;
        }

        public void Tick(float seconds)
        {
            if (_clip == null)
            {
                return;
            }
            _time += seconds;
            _beforeTime += seconds;
            _blend = Mathf.Min(1f, _blend + seconds / 0.2f);
            int bones = _data.Bones;
            for (int i = 0; i < bones; i++)
            {
                Vector3 position;
                Quaternion rotation;
                Sample(_clip, _time, i, bones, out position, out rotation);
                if (_blend < 1f && _before != null)
                {
                    Vector3 oldPosition;
                    Quaternion oldRotation;
                    Sample(_before, _beforeTime, i, bones, out oldPosition, out oldRotation);
                    position = Vector3.Lerp(oldPosition, position, _blend);
                    rotation = Quaternion.Slerp(oldRotation, rotation, _blend);
                }
                _bones[i].localPosition = position;
                _bones[i].localRotation = rotation;
            }
            if (Hold > 0f && _armLeft != null && _armRight != null)
            {
                HoldOut(_armLeft, -1f);
                HoldOut(_armRight, 1f);
            }
            // Meshy's 24 bones: 0 to 4 the hips, spine and neck, 5 to 7 the head. The bones hang
            // side by side, so scaling one only changes its own part of the body.
            if (bones == 24)
            {
                var torso = new Vector3(Width, 1f, Width);
                for (int i = 0; i <= 4; i++)
                {
                    _bones[i].localScale = torso;
                }
                Vector3 head = Vector3.one * Head;
                for (int i = 5; i <= 7; i++)
                {
                    _bones[i].localScale = head;
                }
            }
        }

        private static void Sample(CharacterClip clip, float time, int bone, int bones, out Vector3 position, out Quaternion rotation)
        {
            // Moves loop: the frame after the last one is the first again.
            float at = time * clip.Fps;
            int first = (int)at % clip.Frames;
            int second = (first + 1) % clip.Frames;
            float between = at - Mathf.Floor(at);
            position = Vector3.Lerp(clip.Positions[first * bones + bone], clip.Positions[second * bones + bone], between);
            rotation = Quaternion.Slerp(clip.Rotations[first * bones + bone], clip.Rotations[second * bones + bone], between);
        }

        public void Destroy()
        {
            if (Root != null)
            {
                UnityEngine.Object.Destroy(Root);
            }
        }
    }

    /// <summary>
    /// Loads the mod's moving characters (.cpc files in the models folder, made by
    /// tools/convert_character.py from Meshy models). The format is described in that script.
    /// </summary>
    internal static class Characters
    {
        private static readonly Dictionary<string, CharacterData> Cache = new Dictionary<string, CharacterData>();

        /// <summary>A new copy of the named character, or null if its file is missing or broken.</summary>
        public static CharacterView Create(string name)
        {
            CharacterData data;
            if (!Cache.TryGetValue(name, out data))
            {
                try
                {
                    data = Load(name);
                }
                catch (Exception ex)
                {
                    Plugin.Log("character '" + name + "' could not be loaded: " + ex.Message);
                }
                Cache[name] = data;
            }
            return data != null && data.Clips.Count > 0 ? new CharacterView(data, name) : null;
        }

        private static CharacterData Load(string name)
        {
            string folder = Path.Combine(Plugin.Context.ModDirectory, "models");
            string path = Path.Combine(folder, name + ".cpc");
            if (!File.Exists(path))
            {
                Plugin.Log("character file not found: " + path);
                return null;
            }
            CultureInfo plain = CultureInfo.InvariantCulture;
            var data = new CharacterData();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var triangles = new List<int>();
            var restPositions = new List<Vector3>();
            var restRotations = new List<Quaternion>();
            string picture = null;
            CharacterClip clip = null;
            int filled = 0;

            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Length < 3 || line[1] != ' ')
                {
                    continue;
                }
                string[] p = line.Split(' ');
                switch (line[0])
                {
                    case 'x':
                        picture = line.Substring(2).Trim();
                        break;
                    case 'n':
                        data.Bones = int.Parse(p[1], plain);
                        break;
                    case 'r':
                        restPositions.Add(new Vector3(F(p[1]), F(p[2]), F(p[3])));
                        restRotations.Add(new Quaternion(F(p[4]), F(p[5]), F(p[6]), F(p[7])));
                        break;
                    case 'v':
                        vertices.Add(new Vector3(F(p[1]), F(p[2]), F(p[3])));
                        normals.Add(new Vector3(F(p[4]), F(p[5]), F(p[6])));
                        uvs.Add(new Vector2(F(p[7]), F(p[8])));
                        weights.Add(new BoneWeight
                        {
                            boneIndex0 = int.Parse(p[9], plain), weight0 = F(p[10]),
                            boneIndex1 = int.Parse(p[11], plain), weight1 = F(p[12]),
                            boneIndex2 = int.Parse(p[13], plain), weight2 = F(p[14]),
                            boneIndex3 = int.Parse(p[15], plain), weight3 = F(p[16])
                        });
                        break;
                    case 't':
                        triangles.Add(int.Parse(p[1], plain));
                        triangles.Add(int.Parse(p[2], plain));
                        triangles.Add(int.Parse(p[3], plain));
                        break;
                    case 'a':
                        clip = new CharacterClip { Frames = int.Parse(p[2], plain), Fps = F(p[3]) };
                        clip.Positions = new Vector3[clip.Frames * data.Bones];
                        clip.Rotations = new Quaternion[clip.Frames * data.Bones];
                        data.Clips[p[1]] = clip;
                        filled = 0;
                        break;
                    case 'p':
                        if (clip != null && filled < clip.Positions.Length)
                        {
                            clip.Positions[filled] = new Vector3(F(p[1]), F(p[2]), F(p[3]));
                            clip.Rotations[filled] = new Quaternion(F(p[4]), F(p[5]), F(p[6]), F(p[7]));
                            filled++;
                        }
                        break;
                }
            }
            if (vertices.Count == 0 || data.Bones == 0 || restPositions.Count != data.Bones)
            {
                Plugin.Log("character file is incomplete: " + path);
                return null;
            }

            var mesh = new Mesh { name = "ClassPack " + name, hideFlags = HideFlags.HideAndDontSave };
            if (vertices.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.boneWeights = weights.ToArray();
            var bind = new Matrix4x4[data.Bones];
            for (int i = 0; i < data.Bones; i++)
            {
                // Bones sit side by side under the root, so a bone's bind pose is simply its rest place undone.
                bind[i] = Matrix4x4.TRS(restPositions[i], restRotations[i], Vector3.one).inverse;
            }
            mesh.bindposes = bind;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            data.Mesh = mesh;
            data.Rest = restPositions.ToArray();

            Texture2D skin = picture != null ? Models.Picture(Path.Combine(folder, picture)) : null;
            data.Material = skin != null ? Shapes.Textured(skin) : Shapes.Paint(new Color32(230, 200, 170, 255));
            Plugin.Log("character '" + name + "' loaded: " + vertices.Count + " points, " + data.Bones + " bones, moves " + string.Join(", ", new List<string>(data.Clips.Keys).ToArray()));
            return data;
        }

        private static float F(string text)
        {
            return float.Parse(text, CultureInfo.InvariantCulture);
        }
    }
}
