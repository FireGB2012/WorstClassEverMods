using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Loads the mod's 3D models from the "models" folder next to the dll. The files are in
    /// the mod's own small text format (.cpm), written by tools/make_nerf_models.py:
    ///     m r g b              start a colour group
    ///     x file.png           the picture wrapped around this group (optional)
    ///     v x y z nx ny nz     a vertex with its normal, plus "u v" when the group has a picture
    ///     t a b c              a triangle
    /// Plain models come from tools/make_nerf_models.py, models with a picture (made in
    /// Meshy) from tools/convert_meshy.py.
    /// Each model is read once and shared by every copy shown in the game.
    /// </summary>
    internal static class Models
    {
        private sealed class Loaded
        {
            public Mesh Mesh;
            public Material[] Materials;
        }

        private static readonly Dictionary<string, Loaded> Cache = new Dictionary<string, Loaded>();
        private static readonly Dictionary<string, Texture2D> Pictures = new Dictionary<string, Texture2D>();

        internal static Texture2D Picture(string path)
        {
            Texture2D picture;
            if (Pictures.TryGetValue(path, out picture))
            {
                return picture;
            }
            if (File.Exists(path))
            {
                picture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { hideFlags = HideFlags.HideAndDontSave };
                if (!ImageConversion.LoadImage(picture, File.ReadAllBytes(path), true))
                {
                    picture = null;
                }
            }
            if (picture == null)
            {
                Plugin.Log("model picture could not be loaded: " + path);
            }
            Pictures[path] = picture;
            return picture;
        }

        /// <summary>A new object showing the named model, or null if the file is missing or broken.</summary>
        public static GameObject Build(string name)
        {
            Loaded loaded;
            if (!Cache.TryGetValue(name, out loaded))
            {
                try
                {
                    loaded = Load(name);
                }
                catch (Exception ex)
                {
                    Plugin.Log("model '" + name + "' could not be loaded: " + ex.Message);
                }
                // Remember failures too, so a missing file is not read again every frame.
                Cache[name] = loaded;
            }
            if (loaded == null || loaded.Mesh == null)
            {
                return null;
            }
            var holder = new GameObject("[ClassPack " + name + "]");
            holder.AddComponent<MeshFilter>().sharedMesh = loaded.Mesh;
            MeshRenderer renderer = holder.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = loaded.Materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return holder;
        }

        private static Loaded Load(string name)
        {
            string path = Path.Combine(Plugin.Context.ModDirectory, "models", name + ".cpm");
            if (!File.Exists(path))
            {
                Plugin.Log("model file not found: " + path);
                return null;
            }
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var groups = new List<List<int>>();
            var colors = new List<Color32>();
            var pictures = new List<string>();
            CultureInfo plain = CultureInfo.InvariantCulture;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Length < 3 || line[1] != ' ')
                {
                    continue;
                }
                string[] parts = line.Split(' ');
                switch (line[0])
                {
                    case 'm':
                        colors.Add(new Color32(byte.Parse(parts[1], plain), byte.Parse(parts[2], plain), byte.Parse(parts[3], plain), 255));
                        groups.Add(new List<int>());
                        pictures.Add(null);
                        break;
                    case 'x':
                        if (pictures.Count > 0)
                        {
                            pictures[pictures.Count - 1] = line.Substring(2).Trim();
                        }
                        break;
                    case 'v':
                        vertices.Add(new Vector3(float.Parse(parts[1], plain), float.Parse(parts[2], plain), float.Parse(parts[3], plain)));
                        normals.Add(new Vector3(float.Parse(parts[4], plain), float.Parse(parts[5], plain), float.Parse(parts[6], plain)));
                        uvs.Add(parts.Length >= 9 ? new Vector2(float.Parse(parts[7], plain), float.Parse(parts[8], plain)) : Vector2.zero);
                        break;
                    case 't':
                        if (groups.Count > 0)
                        {
                            List<int> triangles = groups[groups.Count - 1];
                            triangles.Add(int.Parse(parts[1], plain));
                            triangles.Add(int.Parse(parts[2], plain));
                            triangles.Add(int.Parse(parts[3], plain));
                        }
                        break;
                }
            }
            if (vertices.Count == 0 || groups.Count == 0)
            {
                Plugin.Log("model file is empty: " + path);
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
            mesh.subMeshCount = groups.Count;
            var materials = new Material[groups.Count];
            for (int i = 0; i < groups.Count; i++)
            {
                mesh.SetTriangles(groups[i], i);
                Texture2D picture = pictures[i] != null ? Picture(Path.Combine(Path.GetDirectoryName(path), pictures[i])) : null;
                materials[i] = picture != null ? Shapes.Textured(picture) : Shapes.Paint(colors[i]);
            }
            mesh.RecalculateBounds();
            return new Loaded { Mesh = mesh, Materials = materials };
        }
    }
}
