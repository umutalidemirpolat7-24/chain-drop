using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ChainDrop
{
    /// <summary>Works with both the new Input System and the legacy Input Manager (mouse + touch + keyboard).</summary>
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

        /// <summary>WASD / arrow keys, handy for testing in the editor.</summary>
        public static Vector2 Keyboard2D()
        {
            Vector2 v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return v;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
#else
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) v.x -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) v.x += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v.y -= 1;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v.y += 1;
#endif
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }
    }
}
