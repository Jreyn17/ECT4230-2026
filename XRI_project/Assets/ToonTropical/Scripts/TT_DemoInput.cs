// Toon Tropical: tiny input layer for the walk-around demo. Works with the new Input System
// package (when the project uses it) and with the old Input Manager, so the demo scripts
// compile and run in any project setup.
using UnityEngine;
#if TT_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ToonTropical
{
    public static class TT_DemoInput
    {
        /// When set, Move() returns this instead of reading devices (automated tests, AI agents).
        public static Vector2? moveOverride;

        /// WASD / arrow keys, -1..1 on each axis (x = right, y = forward).
        public static Vector2 Move()
        {
            if (moveOverride.HasValue) return moveOverride.Value;
            var v = Vector2.zero;
#if TT_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
            }
            var g = Gamepad.current;
            if (g != null) v += g.leftStick.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
            v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
            return Vector2.ClampMagnitude(v, 1f);
        }

        public static bool Run()
        {
#if TT_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            var g = Gamepad.current;
            return (k != null && k.leftShiftKey.isPressed) || (g != null && g.leftStickButton.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftShift);
#else
            return false;
#endif
        }

        /// True on the frame a key (or gamepad button) goes down: 'E' wave, 'F' dig.
        public static bool Pressed(KeyCode key)
        {
#if TT_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            var g = Gamepad.current;
            switch (key)
            {
                case KeyCode.E: return (k != null && k.eKey.wasPressedThisFrame) || (g != null && g.buttonNorth.wasPressedThisFrame);
                case KeyCode.F: return (k != null && k.fKey.wasPressedThisFrame) || (g != null && g.buttonWest.wasPressedThisFrame);
                case KeyCode.T: return k != null && k.tKey.wasPressedThisFrame;
                case KeyCode.N: return k != null && k.nKey.wasPressedThisFrame;
                case KeyCode.H: return k != null && k.hKey.wasPressedThisFrame;
                default: return false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }

        /// Mouse movement while a mouse button is held (orbit), plus the right stick.
        public static Vector2 Look()
        {
#if TT_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var v = Vector2.zero;
            var m = Mouse.current;
            if (m != null && (m.leftButton.isPressed || m.rightButton.isPressed))
                v += m.delta.ReadValue() * 0.1f;
            var g = Gamepad.current;
            if (g != null) v += g.rightStick.ReadValue() * 2.5f;
            return v;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
                return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            return Vector2.zero;
#else
            return Vector2.zero;
#endif
        }

        /// Mouse wheel, positive = zoom in.
        public static float Zoom()
        {
#if TT_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m != null ? Mathf.Clamp(m.scroll.ReadValue().y, -1f, 1f) : 0f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }
    }
}
