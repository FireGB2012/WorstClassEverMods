using UnityEngine;
using UnityEngine.Rendering;

namespace ClassPack
{
    /// <summary>
    /// Lets a player see their own body swim. The game shows the world through the player's
    /// eyes and hides their body, so a swimming move on yourself would be invisible. While the
    /// player swims, the picture is taken from a few metres behind and a little above the
    /// head instead, and the body (with whatever it wears) is drawn.
    ///
    /// The game's camera is not changed: it is moved back just before each picture is drawn
    /// and put back right after, so aiming, picking up and everything else that looks along
    /// the camera still works from the player's eyes.
    /// </summary>
    internal static class SelfView
    {
        private const float Back = 3.1f;
        private const float Rise = 0.7f;
        private const float EaseSeconds = 0.5f;

        private static float _weight;
        private static bool _hooked;
        private static bool _moved;
        private static Camera _camera;
        private static Vector3 _homeAt;
        private static Quaternion _homeTurn;
        private static int _walls = -1;

        /// <summary>True once the camera is far enough back that the player's own body should be drawn.</summary>
        public static bool Showing => _weight > 0.5f;

        public static void Tick()
        {
            Unit me = Game.LocalPlayer;
            bool want = Plugin.Settings.swimSelfView && me != null && me.isActived && ((Swim.Active && Swim.Swimming(me)) || Dance.Kind(me) == Dance.Worm);
            _weight = Mathf.MoveTowards(_weight, want ? 1f : 0f, Time.unscaledDeltaTime / EaseSeconds);
            if (_weight > 0f && !_hooked)
            {
                RenderPipelineManager.beginCameraRendering += Before;
                RenderPipelineManager.endCameraRendering += After;
                _hooked = true;
            }
            else if (_weight <= 0f && _hooked)
            {
                RenderPipelineManager.beginCameraRendering -= Before;
                RenderPipelineManager.endCameraRendering -= After;
                _hooked = false;
                PutBack();
            }
        }

        private static void Before(ScriptableRenderContext context, Camera camera)
        {
            if (_weight <= 0f || _moved || camera == null || camera != Camera.main)
            {
                return;
            }
            Transform view = camera.transform;
            _camera = camera;
            _homeAt = view.position;
            _homeTurn = view.rotation;
            _moved = true;

            float eased = Mathf.SmoothStep(0f, 1f, _weight);
            Vector3 offset = -view.forward * Back + Vector3.up * Rise;
            float reach = offset.magnitude * eased;
            Vector3 way = offset.normalized;
            if (_walls < 0)
            {
                _walls = LayerMask.GetMask("Ground", "Wall", "Default", "Obstacle");
            }
            // Never through a wall, the deck or the ceiling.
            RaycastHit hit;
            if (reach > 0.05f && Physics.SphereCast(_homeAt, 0.25f, way, out hit, reach, _walls, QueryTriggerInteraction.Ignore))
            {
                reach = Mathf.Max(0f, hit.distance - 0.05f);
            }
            view.position = _homeAt + way * reach;
        }

        private static void After(ScriptableRenderContext context, Camera camera)
        {
            if (_moved && camera == _camera)
            {
                PutBack();
            }
        }

        private static void PutBack()
        {
            if (_moved && _camera != null)
            {
                _camera.transform.SetPositionAndRotation(_homeAt, _homeTurn);
            }
            _moved = false;
        }
    }
}
