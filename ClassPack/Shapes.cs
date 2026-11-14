using System.Collections.Generic;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Builds simple coloured block models in code, for things the mod has no real 3D model
    /// for yet (nerf guns, the fire alarm box). They are placeholders: swap them for proper
    /// models later without touching the gameplay code.
    /// </summary>
    internal static class Shapes
    {
        private static readonly Dictionary<Color32, Material> Paints = new Dictionary<Color32, Material>();
        private static Shader _shader;

        /// <summary>A flat-coloured material that works with the game's renderer.</summary>
        public static Material Paint(Color32 color)
        {
            Material material;
            if (Paints.TryGetValue(color, out material) && material != null)
            {
                return material;
            }
            if (_shader == null)
            {
                _shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            if (_shader == null)
            {
                _shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (_shader == null)
            {
                _shader = Shader.Find("Sprites/Default");
            }
            if (_shader == null)
            {
                // Last resort: borrow whatever shader some object in the room already uses.
                MeshRenderer any = Object.FindFirstObjectByType<MeshRenderer>();
                _shader = any != null && any.sharedMaterial != null ? any.sharedMaterial.shader : null;
            }
            if (_shader == null)
            {
                return null;
            }
            material = new Material(_shader) { hideFlags = HideFlags.HideAndDontSave };
            Color c = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", c);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", c);
            }
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            }
            Paints[color] = material;
            return material;
        }

        public static GameObject Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 size, Color32 color, Vector3 euler = default(Vector3))
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                // Looks only: the block must never get in a player's way or stop a dart.
                Object.Destroy(collider);
            }
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = euler;
            part.transform.localScale = size;
            Renderer renderer = part.GetComponent<Renderer>();
            Material paint = Paint(color);
            if (renderer != null && paint != null)
            {
                renderer.sharedMaterial = paint;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return part;
        }

        /// <summary>A thin glowing line between two points that removes itself.</summary>
        public static void Tracer(Vector3 from, Vector3 to, Color32 color, float width, float seconds)
        {
            var holder = new GameObject("[ClassPack tracer]");
            LineRenderer line = holder.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = width;
            line.endWidth = width * 0.6f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material paint = Paint(color);
            if (paint != null)
            {
                line.sharedMaterial = paint;
            }
            line.startColor = color;
            line.endColor = color;
            Object.Destroy(holder, seconds);
        }
    }
}
