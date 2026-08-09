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

        /// <summary>Buka-tutup peta run. Cuma mengintip — memilih tetap lewat portal.</summary>
        public static bool MapDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && kb.mKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.M);
#endif
            }
        }

        /// <summary>
        /// Putaran roda mouse frame ini, dalam GERIGI (±1 per klik roda). Positif = ke atas.
        ///
        /// Dinormalisasi di sini karena backend-nya tidak sepakat: Input System lama memberi
        /// ±120 per gerigi ala Windows, versi baru menormalkan ke ±1 — dan pemakai yang menebak
        /// salah satunya berakhir dengan scroll yang "mati" (bergerak kurang dari sepiksel).
        /// </summary>
        public static float ScrollY
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = UnityEngine.InputSystem.Mouse.current;
                float raw = mouse != null ? mouse.scroll.ReadValue().y : 0f;
                return Mathf.Abs(raw) > 10f ? raw / 120f : raw;
#else
                return Input.mouseScrollDelta.y;
#endif
            }
        }

        /// <summary>Tombol kiri mouse sedang DITAHAN — dipakai drag-menggeser peta.</summary>
        public static bool LeftHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = UnityEngine.InputSystem.Mouse.current;
                return mouse != null && mouse.leftButton.isPressed;
#else
                return Input.GetMouseButton(0);
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

        /// <summary>
        /// Geser satu langkah ke kiri/kanan di sebuah carousel. -1, 0, atau 1.
        ///
        /// Panah dan A/D dua-duanya, karena keduanya sama-sama benar dan pemain tidak akan
        /// membaca petunjuk untuk tahu yang mana: tangan yang sedang di WASD mencoba A/D, tangan
        /// yang sedang di mouse mencoba panah.
        /// </summary>
        public static int CarouselStepDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb == null) return 0;
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) return -1;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) return 1;
                return 0;
#else
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) return -1;
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) return 1;
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
