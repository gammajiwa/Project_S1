using UnityEngine;

namespace Proto
{
    /// <summary>Input wrapper so the prototype works with either input backend.</summary>
    public static class ProtoInput
    {
        public static Vector2 MousePosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = UnityEngine.InputSystem.Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }

        public static bool LeftClickDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = UnityEngine.InputSystem.Mouse.current;
                return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        public static bool RightClickDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = UnityEngine.InputSystem.Mouse.current;
                return mouse != null && mouse.rightButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(1);
#endif
            }
        }

        public static bool RotateDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && kb.rKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.R);
#endif
            }
        }

        public static bool RestartDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && kb.spaceKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Space);
#endif
            }
        }

        /// <summary>Ganti wajah arena — siang/malam. Alat debug, bukan mekanik.</summary>
        public static bool CycleFaceDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && kb.tKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.T);
#endif
            }
        }

        /// <summary>Back out of a menu page, or leave a run for the main menu.</summary>
        public static bool BackDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Escape);
#endif
            }
        }

        /// <summary>Held to inspect an item's recipes instead of its stats.</summary>
        public static bool AltHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
#else
                return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
#endif
            }
        }

        /// <summary>Naik/turun satu baris di sebuah daftar. -1, 0, atau 1.</summary>
        public static int ListStepDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb == null) return 0;
                if (kb.upArrowKey.wasPressedThisFrame) return -1;
                if (kb.downArrowKey.wasPressedThisFrame) return 1;
                return 0;
#else
                if (Input.GetKeyDown(KeyCode.UpArrow)) return -1;
                if (Input.GetKeyDown(KeyCode.DownArrow)) return 1;
                return 0;
#endif
            }
        }

        /// <summary>Melebarkan atau merapatkan sesuatu. -1, 0, atau 1.</summary>
        public static int SpreadStepDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb == null) return 0;
                if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) return 1;
                if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) return -1;
                return 0;
#else
                if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) return 1;
                if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) return -1;
                return 0;
#endif
            }
        }

        /// <summary>Ditahan untuk memperlambat waktu di ruang uji.</summary>
        public static bool SlowMotionHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
#else
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
            }
        }

        /// <summary>Returns the time-scale slot 0..3 requested this frame, or -1.</summary>
        public static int SpeedSlotDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb == null) return -1;
                if (kb.digit1Key.wasPressedThisFrame) return 0;
                if (kb.digit2Key.wasPressedThisFrame) return 1;
                if (kb.digit3Key.wasPressedThisFrame) return 2;
                if (kb.digit4Key.wasPressedThisFrame) return 3;
                return -1;
#else
                if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
                if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
                if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
                if (Input.GetKeyDown(KeyCode.Alpha4)) return 3;
                return -1;
#endif
            }
        }
    }
}
