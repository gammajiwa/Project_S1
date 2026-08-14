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
        [Tooltip("Hanya dipakai kalau GroundMaterial kosong. Terrain punya teksturnya sendiri.")]
        public Color GroundColor = new Color(0.13f, 0.18f, 0.12f);

        [Tooltip("Hanya dipakai kalau Skybox kosong.")]
        public Color HorizonColor = new Color(0.06f, 0.09f, 0.07f);

        // =========================================================================
        //  lantai
        // =========================================================================
        //
        // Bidang datar berwarna dengan tekstur derau buatan sendiri tidak akan pernah menyamai
        // lantai yang dicat tangan, seberapa pun deraunya disetel — yang hilang bukan variasinya
        // melainkan gambarnya. Kalau paket asetnya sudah membawa tekstur tanah, pakai teksturnya,
        // dan pakai lewat Terrain karena shader terrain-nya memang menuntut splatmap.

        [Header("Lantai terrain")]
        [Tooltip("Material terrain. Diisi = lantainya dibangun sebagai Terrain sungguhan dan " +
                 "GroundColor berhenti berlaku. Kosong = bidang datar berwarna seperti dulu.")]
        public Material GroundMaterial;

        [Tooltip("Lapisan tanah. Yang pertama jadi dasar, sisanya dicat sebagai bercak lewat derau.")]
        public TerrainLayer[] GroundLayers;

        [Tooltip("Sisi lantai dalam unit dunia. Wajib lebih besar dari arena PLUS margin " +
                 "kelahiran musuh — musuh yang lahir di luar tepi lantai berjalan masuk sambil " +
                 "melayang di atas kekosongan.")]
        public Vector2 GroundSize = new Vector2(140f, 120f);

        [Tooltip("Seberapa besar bercak lapisan kedua, dalam unit dunia.")]
        [Min(1f)] public float GroundPatchSize = 26f;

        [Tooltip("Seberapa luas lantai yang tertutup lapisan kedua. 0 = dasar polos.")]
        [Range(0f, 1f)] public float GroundPatchCoverage = 0.3f;

        [Tooltip("Langit. Diisi = kamera membersihkan layar dengan skybox, bukan HorizonColor.")]
        public Material Skybox;

        [Tooltip("Gradasi warna & kabut volumetrik untuk wajah ini. Disimpan sebagai VolumeProfile " +
                 "biasa, bukan sebagai angka-angka kabut — dengan begitu paket kabut apa pun boleh " +
                 "dipasang atau dicopot tanpa satu baris pun kode runtime ikut berubah.")]
        public UnityEngine.Rendering.VolumeProfile PostProcess;

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
        //  pohon bermesh
        // =========================================================================
        //
        // Jumlah pohonnya TIDAK bertambah. Yang berubah cuma sebagian slot pohon diisi mesh alih-alih
        // batang+tajuk, dan tingginya diambil dari TrunkHeightRange yang sama. Itu disengaja: kalau
        // yang bermesh muncul lebih banyak atau lebih besar, yang dibandingkan bukan lagi bentuknya
        // melainkan kerapatan dan ukurannya, dan pertanyaan "mana yang lebih bagus" jadi tidak bisa
        // dijawab.

        [Header("Pohon bermesh (campuran)")]
        [Tooltip("Porsi slot pohon yang diisi mesh. 0 = semua prosedural, 1 = semua mesh, " +
                 "0,5 = campur rata untuk dibandingkan berdampingan.")]
        [Range(0f, 1f)] public float MeshTreeShare = 0.5f;

        public MeshProp[] MeshTrees;

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

        [Header("Rumput & semak bermesh")]
        [Tooltip("Porsi slot rumput yang diisi mesh. 1 = kubus prosedural tidak muncul sama sekali.")]
        [Range(0f, 1f)] public float MeshScatterShare = 1f;

        [Tooltip("LEBAR jadi di dunia, bukan pengali. Mesh sumbernya berukuran 0,9–3,4 unit dan " +
                 "berbeda-beda; menyimpan pengali berarti tiap kali daftarnya diubah semua " +
                 "ukurannya harus disetel ulang satu per satu.")]
        public Vector2 MeshScatterWidth = new Vector2(0.55f, 1.35f);

        public MeshProp[] MeshScatter;

        // =========================================================================
        //  rumput dari kuas
        // =========================================================================
        //
        // Rumput TIDAK disebar acak seperti prop lain. Letaknya diambil dari peta detail terrain —
        // yaitu persis apa yang dikuas dengan alat Paint Details bawaan Unity.
        //
        // Yang menggambarnya tetap renderer prop di sini, bukan mesin detail Unity. Mesin itu tidak
        // menggambar apa pun di project ini: datanya tertulis, shader-nya ada, jaraknya benar, dan
        // layarnya tetap kosong — di kamera ortografis maupun perspektif. Jadi yang dipakai cuma
        // PETA-nya; menggambarnya lewat jalur yang sudah terbukti.
        //
        // Bagi hasilnya jadi jelas: kuas menentukan DI MANA, angka di bawah menentukan SEBERAPA.

        [Header("Rumput (mengikuti kuas terrain)")]
        [Tooltip("Kosong = tidak ada rumput sama sekali.")]
        public MeshProp[] MeshGrass;

        [Tooltip("Jumlah rumpun per petak 24x24 di tempat yang dikuas PENUH. Yang dikuas separuh " +
                 "dapat separuhnya.")]
        [Min(0)] public int GrassPerChunk = 260;

        [Tooltip("LEBAR jadi tiap rumpun di dunia.")]
        public Vector2 GrassWidth = new Vector2(0.7f, 1.3f);

        [Header("Bayangan prop")]
        [Tooltip("Bayangan pohon. Berlaku untuk yang prosedural DAN yang bermesh sekaligus — " +
                 "menyalakannya cuma di salah satu membuat perbandingannya tidak jujur.")]
        public bool TreeShadows;

        // =========================================================================
        //  awan & berkas cahaya
        // =========================================================================

        // =========================================================================
        //  kegelapan jarak
        // =========================================================================
        //
        // Yang jauh dari pemain menggelap, bertepi titik-titik raster komik. Ini milik DUNIA, bukan
        // layar: satu tempat tetap gelap sampai pemain benar-benar mendatanginya.

        // =========================================================================
        //  suasana
        // =========================================================================

        [Header("VFX suasana")]
        [Tooltip("Suasana yang melekat pada tempatnya, bukan pada wave. Tiap entri punya peluang " +
                 "sendiri untuk dipakai, jadi tidak semuanya muncul bersamaan — dan dua run tidak " +
                 "pernah terlihat sama persis.")]
        public AmbientVfxEntry[] AmbientVfx;

        [Tooltip("Diundi ulang tiap wave. Ini yang membuat dua wave berturut-turut tidak pernah " +
                 "terasa sama. Sertakan satu entri KOSONG sebagai cuaca cerah — tanpa itu, tiap " +
                 "wave selalu ada sesuatu yang jatuh dari langit.")]
        public WeatherMood[] WeatherMoods;

        [Header("Mendung — seberapa jauh cuaca boleh menggelapkan")]
        [Tooltip("Pengali matahari saat mendung PENUH. 1 = tidak diredupkan sama sekali.\n\n" +
                 "Ini rem-nya. Cuaca cuma menentukan SEBERAPA mendung (0–1); seberapa gelap " +
                 "mendung penuh itu boleh, ditentukan di sini.")]
        [Range(0.2f, 1f)] public float OvercastSun = 0.85f;

        [Tooltip("Pengali ambient saat mendung penuh. DI ATAS 1, dan itu memang benar: saat " +
                 "mendung, matahari hilang tapi seluruh kubah langit berubah jadi lampu.")]
        [Range(0.5f, 2f)] public float OvercastAmbient = 1.3f;

        [Tooltip("Kepekatan awan saat mendung penuh. 0 = awan tidak ikut menebal.")]
        [Range(0f, 1f)] public float OvercastCloud = 0.75f;

        [Tooltip("Intensitas lampu pemain SAAT HUJAN. Nol = tidak dinyalakan.\n\n" +
                 "Di siang cerah lampu pemain mati karena tidak membeli apa pun. Begitu mendung " +
                 "turun ia justru jadi satu-satunya sumber hangat di layar — dan sekaligus yang " +
                 "menjaga pemain tetap terbaca saat lapangannya meredup.")]
        [Range(0f, 12f)] public float RainPlayerLight = 3.5f;

        [Header("Asap volumetrik")]
        [Tooltip("Kantong udara bersih yang menempel di pemain. Asapnya sendiri adalah kabut " +
                 "global; yang ini MENGURANGI kepadatannya di sekitar pemain, jadi yang tersisa " +
                 "lingkaran bersih yang ikut berjalan. Kosong = tidak ada kantong, dan pemain " +
                 "berdiri di dalam asap yang sama pekatnya dengan kejauhan.")]
        public GameObject SmokePocket;

        [Header("Kegelapan jarak (raster komik)")]
        [Tooltip("0 = mati, 1 = nyala.")]
        [Range(0, 1)] public int GloomLayers = 1;

        [Tooltip("Seberapa jauh GARIS BATASNYA berkelok, dalam unit dunia. Ini yang membuatnya " +
                 "berhenti terbaca sebagai vignette — bukan warnanya, bukan bentuk dasarnya.")]
        public float GloomWobble = 11f;

        [Tooltip("Kepekatan tertinggi. Di bawah 1 kejauhan tetap menyisakan siluet.")]
        [Range(0f, 1f)] public float GloomCeiling = 0.92f;

        [Tooltip("Pusat bagian terangnya. Nyala = apa yang sedang disorot kamera; mati = pemain.\n\n" +
                 "Kamera punya zona mati, jadi keduanya tidak sama: kalau pusatnya pemain, " +
                 "lingkaran terang ikut menyimpang tiap kali pemain bergeser di dalam zona mati, " +
                 "dan tepi layar yang berlawanan diam-diam menggelap.")]
        public bool GloomFollowsCamera = true;

        [Tooltip("Material kegelapan jarak. ASET, bukan dibuat saat bermain.\n\n" +
                 "Versi sebelumnya membuat material baru tiap kali wajah arena dipasang, dan itu " +
                 "berarti setiap penyetelan di Inspector hilang begitu play mode dimulai — " +
                 "menyetel shader jadi mustahil. Karena ini aset, apa pun yang disetel di sini " +
                 "bertahan, termasuk yang disetel SAAT sedang bermain.")]
        public Material GloomMaterial;

        [Tooltip("Dipakai hanya kalau GloomMaterial kosong.")]
        public Color GloomColor = new Color(0.012f, 0.02f, 0.045f, 1f);

        [Tooltip("Jarak dari pemain tempat kegelapan MULAI. Di dalam ini lapangan bersih.")]
        public float GloomStart = 14f;

        [Tooltip("Jarak tempat kegelapan penuh.")]
        public float GloomEnd = 30f;

        [Tooltip("Lebar gumpalan tepinya dalam unit dunia.")]
        public float GloomSize = 9f;

        [Tooltip("Seberapa cepat tepinya bergolak di tempat.")]
        public float GloomChurn = 0.12f;

        [Tooltip("Seberapa cepat kegelapannya mengalir, unit per detik.")]
        public float GloomDrift = 0.6f;

        [Tooltip("Jarak antar titik raster dalam PIKSEL — titik cetak berukuran tetap, jadi ia " +
                 "tidak ikut membesar saat kamera menjauh.")]
        [Range(2f, 40f)] public float GloomDotSize = 16f;

        [Tooltip("0 = titik komik penuh, 1 = gradasi halus tanpa titik. 0,6 menyisakan titiknya " +
                 "sebagai tekstur di tepi tanpa membuat seluruh kegelapan jadi kisi.")]
        [Range(0f, 1f)] public float GloomSmooth = 0.6f;

        [Header("Bayangan awan")]
        [Tooltip("Warna DAN kepekatannya. Alpha yang menentukan seberapa gelap bayangannya.")]
        public Color CloudColor = new Color(0.05f, 0.06f, 0.12f, 0.55f);

        [Tooltip("Lebar satu gumpalan awan dalam unit dunia.")]
        public float CloudSize = 42f;

        [Tooltip("Seberapa banyak langit tertutup. 0,3 = cerah berawan, 0,8 = mendung.")]
        [Range(0.05f, 0.95f)] public float CloudCoverage = 0.55f;

        [Tooltip("Kecepatan hanyut dalam UNIT DUNIA PER DETIK — bukan pengali.\n\n" +
                 "Dulu ia pengali yang dikalikan CloudSize di dalam kode, dan itu menyembunyikan " +
                 "angka sebenarnya: 0,06 terlihat wajar padahal artinya 2 unit/detik, dan satu " +
                 "gumpalan selebar 34 unit butuh 17 detik untuk bergeser selebar dirinya sendiri. " +
                 "Beranimasi, tapi terbaca diam.")]
        public float CloudSpeed = 12f;

        [Header("Lampu arena")]
        [Tooltip("Lampu titik lembut yang mengembara di lapangan. Matahari menyinari semuanya " +
                 "sama rata; lampu inilah yang membuat lantai punya daerah terang dan daerah " +
                 "teduh. 0 = tidak ada.")]
        [Range(0, 8)] public int LampCount = 5;

        public Color LampColor = new Color(1f, 0.82f, 0.55f);

        [Tooltip("Warna lampu berselang-seling dengan LampColor. Hangat dan dingin bergantian — " +
                 "satu suhu untuk semua lampu berarti tidak ada yang bisa dibandingkan.")]
        public Color LampColorCool = new Color(0.45f, 0.65f, 1f);

        [Range(0f, 12f)] public float LampIntensity = 3.2f;

        [Tooltip("Jari-jari jangkauan. Lebar dan lembut, bukan sempit dan tajam.")]
        public float LampRange = 26f;

        [Tooltip("Tinggi lampu. Rendah = kolam cahaya kecil dan pekat; tinggi = luas dan lembut.")]
        public float LampHeight = 7f;

        [Header("Lampu pemain")]
        [Tooltip("Cahaya yang dibawa pemain. Satu-satunya lampu yang boleh mengikuti pemain — di " +
                 "malam hari ia yang menjaga pemain tetap terbaca saat menjauh dari semua lampu " +
                 "arena. Intensitas 0 = mati, dan itu yang benar untuk siang.")]
        [Range(0f, 12f)] public float PlayerLightIntensity;

        public Color PlayerLightColor = new Color(0.7f, 0.85f, 1f);

        public float PlayerLightRange = 12f;

        public float PlayerLightHeight = 2.2f;

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
