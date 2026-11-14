using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClassPack
{
    /// <summary>
    /// Building blocks for rooms the mod builds itself (the pool hall): tiled floors and walls,
    /// water, lamps. A room is plain boxes with a tile picture on them; the things standing in
    /// it are real models from the "models" folder.
    /// </summary>
    internal static class Rooms
    {
        /// <summary>How many tiles one copy of a tile picture holds, each way.</summary>
        private const int TilesPerPicture = 4;

        private static Mesh _quad;

        /// <summary>
        /// A picture of four by four tiles with grout lines between them. Each tile gets a
        /// slightly different shade so a big floor doesn't look like one flat colour.
        /// </summary>
        public static Texture2D Tiles(Color32 tile, Color32 grout, int seed)
        {
            const int size = 256;
            const int cell = size / TilesPerPicture;
            const int line = 3;
            var picture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            var random = new System.Random(seed);
            var shade = new float[TilesPerPicture * TilesPerPicture];
            for (int i = 0; i < shade.Length; i++)
            {
                shade[i] = 0.94f + (float)random.NextDouble() * 0.08f;
            }
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool onLine = x % cell < line || y % cell < line;
                    if (onLine)
                    {
                        pixels[y * size + x] = grout;
                        continue;
                    }
                    float s = shade[(y / cell) * TilesPerPicture + x / cell];
                    pixels[y * size + x] = new Color32(
                        (byte)Mathf.Clamp(tile.r * s, 0f, 255f),
                        (byte)Mathf.Clamp(tile.g * s, 0f, 255f),
                        (byte)Mathf.Clamp(tile.b * s, 0f, 255f), 255);
                }
            }
            picture.SetPixels32(pixels);
            picture.Apply(true, false);
            return picture;
        }

        /// <summary>Soft light and dark blotches that drift over the water.</summary>
        public static Texture2D Ripples()
        {
            const int size = 128;
            var picture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear
            };
            var pixels = new Color32[size * size];
            const float turn = Mathf.PI * 2f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Whole numbers of waves each way, so the picture repeats without a seam.
                    float wave = Mathf.Sin(x * turn * 3f + Mathf.Sin(y * turn * 2f) * 1.6f)
                        + Mathf.Sin(y * turn * 4f + Mathf.Sin(x * turn * 3f) * 1.3f)
                        + Mathf.Sin((x + y) * turn * 5f);
                    float light = Mathf.Clamp01(0.5f + wave / 6f);
                    float crest = Mathf.Pow(light, 3f);
                    byte white = (byte)(205 + crest * 50f);
                    pixels[y * size + x] = new Color32(white, white, 255, (byte)(150 + crest * 70f));
                }
            }
            picture.SetPixels32(pixels);
            picture.Apply(true, false);
            return picture;
        }

        /// <summary>
        /// A material showing a tile picture. "glow" makes it give off a little of its own
        /// light, so a room far from the school's lamps is never pitch dark.
        /// </summary>
        public static Material Tiled(Texture2D picture, float shine, float glow)
        {
            Material plain = Shapes.Paint(new Color32(255, 255, 255, 255));
            if (plain == null)
            {
                return null;
            }
            var material = new Material(plain) { hideFlags = HideFlags.HideAndDontSave };
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", picture);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", picture);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", shine);
            }
            Glow(material, picture, new Color(glow, glow, glow));
            return material;
        }

        public static Material Plain(Color32 color, float shine, float glow)
        {
            Material paint = Shapes.Paint(color);
            if (paint == null)
            {
                return null;
            }
            var material = new Material(paint) { hideFlags = HideFlags.HideAndDontSave };
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", shine);
            }
            Color c = color;
            Glow(material, null, new Color(c.r * glow, c.g * glow, c.b * glow));
            return material;
        }

        private static void Glow(Material material, Texture2D picture, Color color)
        {
            if (color.maxColorComponent <= 0.001f || !material.HasProperty("_EmissionColor"))
            {
                return;
            }
            // If the game was built without this kind of material the switch does nothing,
            // and the room is simply lit by its lamps alone.
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetColor("_EmissionColor", color);
            if (picture != null && material.HasProperty("_EmissionMap"))
            {
                material.SetTexture("_EmissionMap", picture);
            }
        }

        /// <summary>See-through water with drifting ripples.</summary>
        public static Material Water(Color color, Texture2D ripples)
        {
            Material plain = Shapes.Paint(new Color32(255, 255, 255, 255));
            if (plain == null)
            {
                return null;
            }
            var material = new Material(plain) { hideFlags = HideFlags.HideAndDontSave };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", ripples);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", ripples);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.9f);
            }
            SetIfThere(material, "_Surface", 1f);
            SetIfThere(material, "_Blend", 0f);
            SetIfThere(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetIfThere(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetIfThere(material, "_SrcBlendAlpha", (float)BlendMode.One);
            SetIfThere(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            SetIfThere(material, "_ZWrite", 0f);
            SetIfThere(material, "_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("DepthOnly", false);
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static void SetIfThere(Material material, string name, float value)
        {
            if (material.HasProperty(name))
            {
                material.SetFloat(name, value);
            }
        }

        /// <summary>
        /// A box with its tiles laid out by where it stands in the room, so the tiles of two
        /// boxes side by side line up. "tile" is the size of one tile in metres. A solid box
        /// can be walked on and stops players; the others are for looks only.
        /// </summary>
        public static GameObject Box(Transform room, string name, Vector3 centre, Vector3 size, Material paint, float tile, bool solid, Vector3 euler = default(Vector3))
        {
            var box = new GameObject(name);
            box.layer = 0;
            box.transform.SetParent(room, false);
            box.transform.localPosition = centre;
            box.transform.localEulerAngles = euler;

            Vector3 h = size * 0.5f;
            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var triangles = new List<int>(36);
            float per = Mathf.Max(0.01f, tile) * TilesPerPicture;
            // A turned box keeps its tiles straight along its own sides.
            Vector3 shift = euler == Vector3.zero ? centre : Vector3.zero;

            // For each of the six sides: which way it faces, and the two directions across it.
            Vector3[] facing = { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            Vector3[] across = { Vector3.right, Vector3.right, Vector3.forward, Vector3.forward, Vector3.right, Vector3.right };
            Vector3[] along = { Vector3.forward, Vector3.forward, Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            for (int side = 0; side < 6; side++)
            {
                Vector3 n = facing[side];
                Vector3 a = across[side];
                Vector3 b = along[side];
                Vector3 middle = Vector3.Scale(n, h);
                Vector3 da = Vector3.Scale(a, h);
                Vector3 db = Vector3.Scale(b, h);
                int first = vertices.Count;
                Vector3[] corners = { middle - da - db, middle + da - db, middle + da + db, middle - da + db };
                foreach (Vector3 corner in corners)
                {
                    vertices.Add(corner);
                    normals.Add(n);
                    Vector3 placed = corner + shift;
                    uvs.Add(new Vector2(Vector3.Dot(placed, a) / per, Vector3.Dot(placed, b) / per));
                }
                // A triangle faces the way of (second - first) x (third - first). Going round the
                // corners in order that is "across x along"; turn it over when the side faces the other way.
                bool asListed = Vector3.Dot(Vector3.Cross(a, b), n) > 0f;
                if (asListed)
                {
                    triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                }
                else
                {
                    triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
                }
            }
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            box.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = box.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = paint;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // The school's baked light knows nothing about a room this far away.
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (solid)
            {
                box.AddComponent<BoxCollider>().size = size;
            }
            return box;
        }

        /// <summary>A flat sheet facing up, for the water.</summary>
        public static GameObject Sheet(Transform room, string name, Vector3 centre, float length, float width, Material paint, float tile)
        {
            var sheet = new GameObject(name);
            sheet.layer = 0;
            sheet.transform.SetParent(room, false);
            sheet.transform.localPosition = centre;
            float hx = length * 0.5f;
            float hz = width * 0.5f;
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz)
            });
            mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(length / tile, 0f), new Vector2(length / tile, width / tile), new Vector2(0f, width / tile)
            });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateBounds();
            sheet.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = sheet.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = paint;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return sheet;
        }

        /// <summary>A lamp that lights everything around it, players included.</summary>
        public static Light Lamp(Transform room, Vector3 at, float range, float strength, Color color)
        {
            var holder = new GameObject("lamp");
            holder.transform.SetParent(room, false);
            holder.transform.localPosition = at;
            Light lamp = holder.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = range;
            lamp.intensity = strength;
            lamp.color = color;
            lamp.shadows = LightShadows.None;
            lamp.renderMode = LightRenderMode.ForcePixel;
            return lamp;
        }

        /// <summary>One of the mod's models, standing in the room. Null when its file is missing.</summary>
        public static GameObject Prop(Transform room, string model, Vector3 at, float yaw, float scale = 1f)
        {
            GameObject prop = Models.Build(model);
            if (prop == null)
            {
                return null;
            }
            prop.transform.SetParent(room, false);
            prop.transform.localPosition = at;
            prop.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
            prop.transform.localScale = Vector3.one * scale;
            foreach (Renderer renderer in prop.GetComponentsInChildren<Renderer>())
            {
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            return prop;
        }

        /// <summary>Something unseen that players bump into or walk on (a chair's legs, a ramp).</summary>
        public static void Block(Transform room, string name, Vector3 centre, Vector3 size, Vector3 euler = default(Vector3))
        {
            var block = new GameObject(name);
            block.layer = 0;
            block.transform.SetParent(room, false);
            block.transform.localPosition = centre;
            block.transform.localEulerAngles = euler;
            block.AddComponent<BoxCollider>().size = size;
        }
    }
}
