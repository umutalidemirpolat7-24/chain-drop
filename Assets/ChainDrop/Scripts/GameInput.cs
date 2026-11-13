using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ChainDrop
{
    /// <summary>Works with both the new Input System and the legacy Input Manager (mouse + touch).</summary>
    public static class GameInput
    {
        public static bool PressedThisFrame(out Vector2 screenPos)
        {
#if ENABLE_INPUT_SYSTEM
            var p = Pointer.current;
            screenPos = p != null ? p.position.ReadValue() : Vector2.zero;
            return p != null && p.press.wasPressedThisFrame;
#else
            screenPos = Input.mousePosition;
            return Input.GetMouseButtonDown(0);
#endif
        }

        public static bool Held(out Vector2 screenPos)
        {
#if ENABLE_INPUT_SYSTEM
            var p = Pointer.current;
            screenPos = p != null ? p.position.ReadValue() : Vector2.zero;
            return p != null && p.press.isPressed;
#else
            screenPos = Input.mousePosition;
            return Input.GetMouseButton(0);
#endif
        }
    }
}
