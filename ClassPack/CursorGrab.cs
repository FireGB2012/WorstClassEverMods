using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// Frees the mouse for one of the mod's windows and puts it back afterwards. The game
    /// re-locks the mouse every frame while you walk around, so Hold() has to be called every
    /// frame the window is open. This is the same thing the game's own debug window does.
    /// Only one window may hold the mouse at a time.
    /// </summary>
    internal sealed class CursorGrab
    {
        private static CursorGrab _owner;

        private CursorLockMode _previousLock;
        private bool _previousVisible;
        private bool _previousInputShowing;
        private bool _capturedInput;

        public bool Active => _owner == this;

        public static bool Busy => _owner != null;

        public bool Begin()
        {
            if (_owner != null)
            {
                return false;
            }
            _owner = this;
            _previousLock = Cursor.lockState;
            _previousVisible = Cursor.visible;
            _capturedInput = MonoSingletonT<InputManager>.IsSingletonActive();
            if (_capturedInput)
            {
                _previousInputShowing = MonoSingletonT<InputManager>.Instance.isCursorShowing;
            }
            Hold();
            return true;
        }

        public void Hold()
        {
            if (_owner != this)
            {
                return;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (MonoSingletonT<InputManager>.IsSingletonActive())
            {
                MonoSingletonT<InputManager>.Instance.isCursorShowing = true;
            }
        }

        public void End()
        {
            if (_owner != this)
            {
                return;
            }
            _owner = null;
            Cursor.lockState = _previousLock;
            Cursor.visible = _previousVisible;
            if (_capturedInput && MonoSingletonT<InputManager>.IsSingletonActive())
            {
                MonoSingletonT<InputManager>.Instance.isCursorShowing = _previousInputShowing;
            }
        }
    }
}
