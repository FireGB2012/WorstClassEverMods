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
    ///     v x y z nx ny nz     a vertex with its normal
    ///     t a b c              a triangle
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
            var groups = new List<List<int>>();
            var colors = new List<Color32>();
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
                        break;
                    case 'v':
                        vertices.Add(new Vector3(float.Parse(parts[1], plain), float.Parse(parts[2], plain), float.Parse(parts[3], plain)));
                        normals.Add(new Vector3(float.Parse(parts[4], plain), float.Parse(parts[5], plain), float.Parse(parts[6], plain)));
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
            mesh.subMeshCount = groups.Count;
            var materials = new Material[groups.Count];
            for (int i = 0; i < groups.Count; i++)
            {
                mesh.SetTriangles(groups[i], i);
                materials[i] = Shapes.Paint(colors[i]);
            }
            mesh.RecalculateBounds();
            return new Loaded { Mesh = mesh, Materials = materials };
        }
    }
}
