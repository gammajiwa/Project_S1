using TMPro;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Everything about how the main menu looks. The scene holds the layout, this holds the art —
    /// so swapping placeholders means editing this asset, never the scene or the code.
    /// Only <c>MainMenuBuilder</c> reads it: at runtime the scene is already dressed.
    /// </summary>
    [CreateAssetMenu(fileName = "MenuTheme", menuName = "Grimoire/Menu Theme")]
    public class MenuTheme : ScriptableObject
    {
        [Header("Font")]
        [Tooltip("Kosongkan untuk pakai font bawaan TMP (LiberationSans SDF).")]
        public TMP_FontAsset TitleFont;

        public TMP_FontAsset BodyFont;

        [Header("Ukuran teks")]
        public float TitleSize = 82f;
        public float TaglineSize = 19f;
        public float MenuItemSize = 30f;
        public float HeadingSize = 38f;
        public float BodySize = 19f;
        public float SmallSize = 14f;

        [Header("Warna")]
        public Color Backdrop = new Color(0.043f, 0.043f, 0.063f, 1f);

        [Tooltip("Lapisan gelap di atas diorama supaya teks tetap kebaca.")]
        public Color BackdropVeil = new Color(0.03f, 0.03f, 0.05f, 0.55f);

        public Color TextIdle = new Color(0.929f, 0.890f, 0.824f, 1f);
        public Color TextMuted = new Color(0.55f, 0.545f, 0.58f, 1f);
        public Color Accent = new Color(0.851f, 0.643f, 0.255f, 1f);

        [Tooltip("Sengaja tidak tembus pandang: diorama yang nembus di balik panel kebaca sebagai bug.")]
        public Color PanelFill = new Color(0.055f, 0.055f, 0.075f, 1f);
        public Color PanelLine = new Color(1f, 1f, 1f, 0.07f);

        [Header("Warna slot codex")]
        public Color SlotKnown = new Color(0.098f, 0.098f, 0.129f, 0.95f);
        public Color SlotUnknown = new Color(0.063f, 0.063f, 0.078f, 0.95f);
        public Color SilhouetteCell = new Color(0.22f, 0.22f, 0.27f, 1f);

        // Cahaya dan warna diorama pindah ke SceneLook, dipakai bareng scene game — dua sumber
        // kebenaran untuk hal yang sama cuma bikin menu dan game pelan-pelan beda sendiri.

        [Header("Sprite - kosongkan untuk kotak polos")]
        [Tooltip("Latar panel codex/setelan.")]
        public Sprite PanelSprite;

        [Tooltip("Latar tombol. Gaya menunya sengaja tanpa kotak, jadi biasanya dibiarkan kosong.")]
        public Sprite ButtonSprite;

        [Tooltip("Gambar latar layar penuh. Kosong = diorama 3D yang kelihatan.")]
        public Sprite BackdropArt;

        [Tooltip("Penanda kecil yang muncul di kiri item menu saat di-hover.")]
        public Sprite MarkerSprite;

        [Header("Tata letak")]
        public Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        public float MenuSpacing = 6f;

        [Tooltip("Sejauh mana item menu geser ke kanan saat di-hover.")]
        public float HoverSlide = 16f;

        public TMP_FontAsset ResolveTitleFont() => TitleFont != null ? TitleFont : DefaultFont;

        public TMP_FontAsset ResolveBodyFont() => BodyFont != null ? BodyFont : DefaultFont;

        static TMP_FontAsset DefaultFont =>
            TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }
}
