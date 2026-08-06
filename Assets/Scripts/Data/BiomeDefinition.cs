using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Satu wajah arena: warna tanah, langit, cahaya, dan barang-barang yang berserakan di atasnya.
    ///
    /// Arena sekarang 80x60 unit dan pemain benar-benar menjelajahinya. Lantai polos sebesar itu
    /// bukan cuma membosankan — ia menghapus rasa berpindah tempat sama sekali, karena tidak ada
    /// satu pun titik acuan untuk mengukur bahwa kamu sudah bergerak.
    ///
    /// Props di sini sengaja TANPA collider. Yang dibeli adalah kedalaman visual dan titik acuan,
    /// bukan halangan: gerak pemain sudah otomatis dan musuh sudah saling mendorong, jadi menambah
    /// rintangan padat hanya akan membuat keduanya tersangkut dengan cara yang tidak bisa dibaca.
    /// </summary>
    [CreateAssetMenu(fileName = "Biome_", menuName = "Grimoire/Biome")]
    public class BiomeDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName = "Forest";

        [Header("Permukaan")]
        public Color GroundColor = new Color(0.13f, 0.18f, 0.12f);
        public Color HorizonColor = new Color(0.06f, 0.09f, 0.07f);

        [Header("Cahaya")]
        public Color SunColor = new Color(1f, 0.93f, 0.72f);

        [Tooltip("Sudut matahari. Rendah = bayangan panjang, dan itu separuh dari tampilannya.")]
        [Range(8f, 80f)] public float SunPitch = 28f;

        [Range(0f, 360f)] public float SunYaw = 45f;

        [Range(0f, 3f)] public float SunIntensity = 1.25f;

        public Color AmbientColor = new Color(0.19f, 0.24f, 0.2f);

        // =========================================================================
        //  pohon
        // =========================================================================
        //
        // Batang dan tajuk dipisah karena satu bentuk saja tidak pernah terbaca sebagai pohon:
        // silinder sendirian jadi tiang, bola sendirian jadi gundukan. Yang membuatnya terbaca
        // sebagai pohon adalah HUBUNGAN keduanya — tajuk duduk tepat di atas batangnya sendiri.

        [Header("Pohon")]
        [Tooltip("Jarang, bukan rapat. Hutan yang padat menutupi gerombolan dan menghapus " +
                 "satu-satunya hal yang harus dibaca pemain: musuh datang dari arah mana.")]
        [Min(0)] public int TreeCount = 90;

        public Vector2 TrunkHeightRange = new Vector2(3.5f, 7f);

        public Vector2 TrunkWidthRange = new Vector2(0.35f, 0.7f);

        [Tooltip("Lebar tajuk dibanding tinggi batangnya.")]
        public Vector2 CanopyWidthRatio = new Vector2(0.7f, 1.15f);

        [Tooltip("Seberapa pipih tajuknya. Di bawah 1 = melebar seperti kanopi, bukan bola.")]
        [Range(0.3f, 1.5f)] public float CanopyFlatten = 0.75f;

        public Color[] TrunkColors =
        {
            new Color(0.19f, 0.14f, 0.11f),
            new Color(0.24f, 0.18f, 0.13f)
        };

        public Color[] CanopyColors =
        {
            new Color(0.16f, 0.32f, 0.17f),
            new Color(0.21f, 0.4f, 0.2f),
            new Color(0.13f, 0.26f, 0.15f),
            new Color(0.26f, 0.44f, 0.22f)
        };

        // =========================================================================
        //  semak & batu
        // =========================================================================

        [Header("Rumput & semak")]
        [Min(0)] public int ScatterCount = 1200;

        public PrimitiveType ScatterShape = PrimitiveType.Cube;

        [Tooltip("LEBAR tiap rumpun. Tingginya diatur ScatterFlatten.")]
        public Vector2 ScatterScaleRange = new Vector2(0.16f, 0.42f);

        [Tooltip("Tinggi dibanding lebar. Di atas 1 = tegak seperti rumput; di bawah 1 = " +
                 "gepeng seperti batu.")]
        [Range(0.2f, 5f)] public float ScatterFlatten = 2.6f;

        public Color[] ScatterColors =
        {
            new Color(0.15f, 0.25f, 0.14f),
            new Color(0.2f, 0.3f, 0.16f),
            new Color(0.22f, 0.21f, 0.18f)
        };

        [Header("Tata letak")]
        [Tooltip("Radius kosong di tengah arena. Pemain memulai di sana; props yang menimpa titik " +
                 "mulai membuat wave pertama dibuka dengan pandangan yang terhalang.")]
        public float ClearingRadius = 9f;

        void OnValidate()
        {
            if (string.IsNullOrEmpty(Id)) Id = name.ToLowerInvariant().Replace("biome_", "");

            if (TrunkHeightRange.y < TrunkHeightRange.x) TrunkHeightRange.y = TrunkHeightRange.x;
            if (TrunkWidthRange.y < TrunkWidthRange.x) TrunkWidthRange.y = TrunkWidthRange.x;
            if (ScatterScaleRange.y < ScatterScaleRange.x) ScatterScaleRange.y = ScatterScaleRange.x;
        }
    }
}
