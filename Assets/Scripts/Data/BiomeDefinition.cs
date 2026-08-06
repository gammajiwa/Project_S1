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

        // Ambient dipecah jadi tiga arah, bukan satu warna datar.
        //
        // SceneLook menyalakan mode Trilight, dan di mode itu RenderSettings.ambientLight — satu
        // warna datar — DIABAIKAN sepenuhnya. Selama ini biome mengisi field itu, jadi seluruh
        // pengaturan ambient-nya tidak pernah berpengaruh sama sekali, tanpa satu pun peringatan.
        //
        // Tiga arah juga yang membuat tampilan bergaya ilustrasi bisa terjadi: cahaya langit yang
        // kuat dari atas mengangkat bagian yang tidak kena matahari, sehingga bayangan jadi
        // berwarna alih-alih hitam.
        [Tooltip("Cahaya dari atas. Ini yang paling menentukan terang-gelapnya keseluruhan.")]
        public Color AmbientSky = new Color(0.55f, 0.68f, 0.72f);

        [Tooltip("Cahaya dari samping, setinggi mata.")]
        public Color AmbientEquator = new Color(0.55f, 0.6f, 0.45f);

        [Tooltip("Pantulan dari tanah. Warnanya sebaiknya dekat dengan warna tanahnya.")]
        public Color AmbientGround = new Color(0.35f, 0.42f, 0.25f);

        [Header("Kabut")]
        [Tooltip("Kabut jarak jauh. Di gaya ilustrasi ini gunanya bukan menyembunyikan, tapi " +
                 "memberi kedalaman: yang jauh memudar ke warna langit.")]
        public bool FogEnabled = true;

        public Color FogColor = new Color(0.72f, 0.78f, 0.62f);

        public float FogStart = 45f;

        public float FogEnd = 140f;

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

        // =========================================================================
        //  awan & berkas cahaya
        // =========================================================================

        [Header("Bayangan awan")]
        [Tooltip("Warna DAN kepekatannya. Alpha yang menentukan seberapa gelap bayangannya.")]
        public Color CloudColor = new Color(0.05f, 0.06f, 0.12f, 0.55f);

        [Tooltip("Lebar satu gumpalan awan dalam unit dunia.")]
        public float CloudSize = 42f;

        [Tooltip("Seberapa banyak langit tertutup. 0,3 = cerah berawan, 0,8 = mendung.")]
        [Range(0.05f, 0.95f)] public float CloudCoverage = 0.55f;

        [Tooltip("Kecepatan hanyut. Pelan sekali — awan yang bergerak cepat terbaca sebagai " +
                 "tekstur yang bergeser, bukan sebagai cuaca.")]
        public float CloudSpeed = 0.012f;

        [Header("Berkas cahaya")]
        [Tooltip("Warna berkasnya. Ditambahkan, bukan ditimpa, jadi warnanya langsung jadi cahaya.")]
        public Color RayColor = new Color(1f, 0.82f, 0.45f, 0.32f);

        [Tooltip("Jarak antar berkas dalam unit dunia.")]
        public float RaySize = 30f;

        [Range(0.05f, 0.95f)] public float RayCoverage = 0.4f;

        [Header("Lampu arena")]
        [Tooltip("Lampu titik lembut yang mengembara di lapangan. Matahari menyinari semuanya " +
                 "sama rata; lampu inilah yang membuat lantai punya daerah terang dan daerah " +
                 "teduh. 0 = tidak ada.")]
        [Range(0, 8)] public int LampCount = 5;

        public Color LampColor = new Color(1f, 0.82f, 0.55f);

        [Range(0f, 12f)] public float LampIntensity = 3.2f;

        [Tooltip("Jari-jari jangkauan. Lebar dan lembut, bukan sempit dan tajam.")]
        public float LampRange = 26f;

        [Tooltip("Tinggi lampu. Rendah = kolam cahaya kecil dan pekat; tinggi = luas dan lembut.")]
        public float LampHeight = 7f;

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
