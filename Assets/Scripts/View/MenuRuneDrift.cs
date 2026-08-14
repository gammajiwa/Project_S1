using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Rune yang melayang di latar menu — sebagai bintang yang bertebaran, atau sebagai barisan
    /// yang mengalir seperti kereta.
    ///
    /// Memakai gambar rune yang sudah ada di <c>Resources/Runes</c> apa adanya. Gambar-gambar itu
    /// SUDAH berwarna dan sudah berpendar sendiri, jadi ia digambar dengan tint putih: mengalikan
    /// warna lain ke atasnya cuma akan mengeruhkan pendar yang memang sudah digambar di sana.
    ///
    /// Semuanya hidup di kanvas UI, bukan sebagai partikel. Bukan karena partikel salah, tapi
    /// karena latar menu ini kanvas: partikel dunia butuh kamera, urutan render sendiri, dan
    /// penyesuaian tiap kali resolusi berubah — sementara Image di kanvas mengikuti apa pun yang
    /// dilakukan kanvasnya tanpa diberi tahu.
    ///
    /// <b>Tidak pernah menghalangi klik.</b> Ia menutupi seluruh layar, dan lapisan sedekoratif ini
    /// yang menelan satu klik tombol saja sudah membuat menunya terasa rusak.
    /// </summary>
    [AddComponentMenu("Grimoire/Menu Rune Drift")]
    [RequireComponent(typeof(RectTransform))]
    public class MenuRuneDrift : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>Tersebar acak, melayang pelan, berkedip seperti bintang.</summary>
            Scatter,

            /// <summary>Berbaris satu jalur dan mengalir terus, seperti gerbong.</summary>
            Procession,

            /// <summary>Melengkung masuk ke satu titik, seperti tersedot ke dalam buku.</summary>
            Inflow
        }

        /// <summary>Racikan siap pakai. Dibuat untuk DIBANDINGKAN, bukan untuk dipilih sekali.</summary>
        public enum Preset
        {
            /// <summary>Angka apa pun yang sedang kamu atur sendiri - preset tidak menyentuhnya.</summary>
            Custom,

            /// <summary>Kereta melengkung pelan masuk ke buku. Paling tenang.</summary>
            SedotanPelan,

            /// <summary>Kereta yang sama, lebih deras dan lebih rapat.</summary>
            SedotanDeras,

            /// <summary>Pusaran ketat - hampir tiga putaran sebelum tertelan.</summary>
            PusaranKetat,

            /// <summary>Barisan lurus mendatar di bawah menu.</summary>
            KeretaMendatar,

            /// <summary>Bintang bertebaran, tipis dan lambat.</summary>
            BintangTenang,

            /// <summary>Empat sungai melengkung masuk dari empat penjuru, warna campur.</summary>
            EmpatSungai,

            /// <summary>Lima jalur, satu warna untuk tiap jalur.</summary>
            LimaWarna,

            /// <summary>Tiga jalur rapat dan deras, warna campur.</summary>
            TigaDeras,

            /// <summary>EMPAT aliran angin pelan, satu warna untuk tiap aliran.</summary>
            AnginEmpatWarna,

            /// <summary>Empat aliran angin pelan, warna campur di semuanya.</summary>
            AnginCampur,

            /// <summary>Enam aliran tipis, lengkungnya lebar, paling lembut.</summary>
            AnginLebar
        }

        [Header("Preset")]
        [Tooltip("Ganti untuk mencoba racikan lain. Pilih Custom kalau mau menahan angka yang " +
                 "sudah kamu atur sendiri - preset lain akan menimpanya. " +
                 "Saat bermain, tekan tombol di bawah untuk memutar antar preset tanpa berhenti.")]
        public Preset Racikan = Preset.SedotanPelan;

        [Tooltip("Memutar preset saat bermain lewat tombol V. Matikan kalau sudah ketemu " +
                 "racikan yang pas - ia cuma alat banding, bukan fitur permainan.")]
        public bool CycleWithV = true;

        [Header("Bentuk")]
        public Mode Layout = Mode.Scatter;

        [Tooltip("Berapa rune di layar. Banyak-banyak bukan berarti ramai - lewat sekitar 40 ia " +
                 "berhenti terbaca sebagai rune dan mulai terbaca sebagai noise.")]
        [Range(4, 80)] public int Count = 26;

        [Header("Ukuran & gerak")]
        [Min(4f)] public float SizeMin = 26f;
        [Min(4f)] public float SizeMax = 96f;

        [Tooltip("Arah hanyut untuk mode Scatter. Tidak perlu dinormalkan.")]
        public Vector2 Drift = new Vector2(0.22f, 1f);

        [Min(0f)] public float SpeedMin = 5f;
        [Min(0f)] public float SpeedMax = 20f;

        [Tooltip("Putaran pelan, derajat per detik. Nol = tegak semua.")]
        [Range(0f, 40f)] public float Spin = 6f;

        [Header("Kereta")]
        [Min(0f)] public float TrainSpeed = 42f;

        [Tooltip("Jarak antar rune di barisan, dalam piksel kanvas.")]
        [Min(8f)] public float TrainGap = 118f;

        [Tooltip("Seberapa jauh barisan bergelombang naik-turun.")]
        [Min(0f)] public float TrainWave = 26f;

        [Tooltip("Tinggi jalurnya dari tengah layar. Negatif = ke bawah. " +
                 "Ada karena barisan yang lewat tepat di tengah akan memotong daftar menu, dan " +
                 "dua benda bergerak yang saling menyilang selalu terbaca berantakan - sekalipun " +
                 "keduanya tipis.")]
        public float TrainY = -250f;

        [Header("Sedotan ke buku")]
        [Tooltip("Objek DUNIA yang menyedot - bukunya. Diikuti tiap frame, jadi sedotannya tetap " +
                 "menempel walau dioramanya bergerak. Kosong = dicari sendiri saat mulai.")]
        public Transform SinkWorld;

        [Tooltip("Nama objek yang dicari kalau SinkWorld dikosongkan. Yang pertama cocok dipakai.")]
        public string SinkName = "L1b_MagicCircleInner";

        [Tooltip("Objek UI yang menyedot. Dipakai hanya kalau SinkWorld kosong.")]
        public RectTransform Sink;

        [Tooltip("Titik sedot terhadap tengah layar, dipakai kalau Sink kosong.")]
        public Vector2 SinkPoint = new Vector2(0f, -40f);

        [Tooltip("Sejauh apa barisan mulai dari titik sedot.")]
        [Min(50f)] public float SpawnRadius = 900f;

        [Tooltip("Sedekat apa ia boleh sampai sebelum dianggap tertelan dan kembali ke pangkal.")]
        [Min(4f)] public float SwallowRadius = 40f;

        [Tooltip("Laju mengalir sepanjang lintasan, piksel per detik.")]
        [Min(1f)] public float InflowSpeed = 80f;

        [Tooltip("Seberapa melengkung tiap aliran, sebagai pecahan jaraknya ke buku. Nol = garis " +
                 "lurus; 0,5 sudah terbaca sebagai tiupan angin. Ini BUKAN putaran - alirannya " +
                 "membelok lalu MAJU ke buku, bukan mengitarinya.")]
        [Range(0f, 1.2f)] public float Bend = 0.45f;

        [Tooltip("Aliran berselang-seling membelok ke kiri dan ke kanan. Mati = semuanya " +
                 "membelok ke arah yang sama, dan itu mulai terbaca sebagai pusaran lagi.")]
        public bool AlternateBend = true;

        [Tooltip("Jarak antar gerbong sepanjang lintasan, sebagai pecahan panjang lintasan. " +
                 "Kecil = rapat mengekor, besar = renggang.")]
        [Range(0.005f, 0.2f)] public float TrainSpacing = 0.045f;

        [Tooltip("Berapa SUNGAI yang menuju buku, masing-masing datang dari arah berbeda. " +
                 "Satu jalur terbaca sebagai satu barisan; beberapa jalur terbaca sebagai sesuatu " +
                 "yang benar-benar sedang menarik dari segala penjuru.")]
        [Range(1, 6)] public int Lanes = 4;

        [Tooltip("Tiap jalur memakai SATU kelompok warna - jalur emas, jalur hijau, jalur biru, " +
                 "dan seterusnya. Mati = tiap jalur campur warna.")]
        public bool ColourPerLane;

        [Tooltip("Mengecil sambil mendekat. 1 = tetap seukuran sampai tertelan.")]
        [Range(0.05f, 1f)] public float ShrinkAtSink = 0.35f;

        [Header("Cahaya")]
        [Range(0f, 1f)] public float AlphaMin = 0.05f;
        [Range(0f, 1f)] public float AlphaMax = 0.30f;

        [Tooltip("Lama satu tarikan kedip, dalam detik. Kedip yang terlalu cepat terbaca sebagai " +
                 "kerlip lampu rusak, bukan sebagai sihir.")]
        [Min(0.5f)] public float TwinkleSeconds = 5f;

        // ------------------------------------------------------------------ dalaman

        static readonly string[] SheetNames =
        {
            "Rune_S1_1", "Rune_S1_2", "Rune_S1_3", "Rune_S1_4", "Rune_S1_5", "Rune_S1_6",
            "Rune_S2_1", "Rune_S2_2", "Rune_S2_3", "Rune_S2_4",
            "Rune_S3_1", "Rune_S3_2",
            "Rune_S4_1", "Rune_S4_2", "Rune_S4_3",
            "Rune_S5_1"
        };

        /// <summary>
        /// Batas tiap kelompok warna di sheet: awal dan panjang. Gambar rune sudah diwarnai per
        /// tingkat kelangkaan, jadi "satu warna per jalur" tidak butuh pewarnaan sama sekali -
        /// cukup memilih dari kelompok yang benar.
        /// </summary>
        static readonly int[,] Tiers =
        {
            { 0, 6 },   // S1 emas-putih
            { 6, 4 },   // S2 hijau
            { 10, 2 },  // S3 biru
            { 12, 3 },  // S4 magenta
            { 15, 1 },  // S5 emas
        };

        struct Mote
        {
            public RectTransform Rect;
            public Image Art;
            public Vector2 Pos;
            public float Speed;
            public float Spin;
            public float Phase;
            public float Size;
            public float Rail;     // kereta: geser sepanjang barisan
            public float Angle;    // sedotan: sudut pangkal jalurnya, radian
            public float Radius;   // sedotan: jarak ke titik sedot
            public int Lane;       // aliran ke berapa
            public int Slot;       // urutan di dalam alirannya
            public float BendSign; // ke mana alirannya membelok, -1 atau +1
        }

        RectTransform _area;
        Canvas _canvas;
        Camera _lens;

        /// <summary>Seberapa jauh barisan sudah berjalan, dalam pecahan panjang lintasan.</summary>
        float _flow;

        Sprite[] _sheet;
        Mote[] _motes;
        System.Random _dice;

        void OnEnable()
        {
            _area = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
            _lens = Camera.main;

            FindSink();
            Apply(Racikan);
            Rebuild();
        }

        /// <summary>
        /// Mencari objek penyedot kalau belum diseret tangan. Dicari lewat NAMA, sekali saat
        /// mulai - bukan tiap frame: pencarian di seluruh scene tiap frame adalah harga yang
        /// tidak masuk akal untuk satu titik yang objeknya tidak pernah berganti.
        /// </summary>
        void FindSink()
        {
            if (SinkWorld != null || string.IsNullOrEmpty(SinkName)) return;

            var hit = GameObject.Find(SinkName);
            if (hit != null) { SinkWorld = hit.transform; return; }

            // Objek nonaktif tidak ketemu lewat Find; disapu manual sebelum menyerah.
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name != SinkName) continue;
                SinkWorld = t;
                return;
            }
        }

        /// <summary>
        /// Menuang satu racikan ke angka-angka di atas. <see cref="Preset.Custom"/> tidak
        /// menyentuh apa pun - itu jalan keluarnya begitu kamu mulai mengatur sendiri.
        /// </summary>
        public void Apply(Preset preset)
        {
            switch (preset)
            {
                case Preset.SedotanPelan:
                    Layout = Mode.Inflow; Count = 22;
                    SizeMin = 30f; SizeMax = 74f; Spin = 5f;
                    SpawnRadius = 880f; SwallowRadius = 40f; InflowSpeed = 72f;
                    Bend = 0.35f; AlternateBend = true; TrainSpacing = 0.05f; ShrinkAtSink = 0.32f;
                    AlphaMin = 0.05f; AlphaMax = 0.30f;
                    Lanes = 1; ColourPerLane = false;
                    break;

                case Preset.SedotanDeras:
                    Layout = Mode.Inflow; Count = 40;
                    SizeMin = 26f; SizeMax = 66f; Spin = 9f;
                    SpawnRadius = 960f; SwallowRadius = 36f; InflowSpeed = 120f;
                    Bend = 0.55f; AlternateBend = true; TrainSpacing = 0.026f; ShrinkAtSink = 0.28f;
                    AlphaMin = 0.06f; AlphaMax = 0.38f;
                    Lanes = 2; ColourPerLane = false;
                    break;

                case Preset.PusaranKetat:
                    Layout = Mode.Inflow; Count = 34;
                    SizeMin = 22f; SizeMax = 58f; Spin = 14f;
                    SpawnRadius = 780f; SwallowRadius = 30f; InflowSpeed = 88f;
                    Bend = 0.9f;  AlternateBend = true; TrainSpacing = 0.03f; ShrinkAtSink = 0.2f;
                    AlphaMin = 0.05f; AlphaMax = 0.34f;
                    Lanes = 1; ColourPerLane = false;
                    break;

                case Preset.KeretaMendatar:
                    Layout = Mode.Procession; Count = 16;
                    SizeMin = 44f; SizeMax = 78f; Spin = 3f;
                    TrainSpeed = 46f; TrainGap = 132f; TrainWave = 22f; TrainY = -250f;
                    AlphaMin = 0.07f; AlphaMax = 0.32f;
                    break;

                case Preset.EmpatSungai:
                    Layout = Mode.Inflow; Count = 40; Lanes = 4; ColourPerLane = false;
                    SizeMin = 28f; SizeMax = 66f; Spin = 7f;
                    SpawnRadius = 980f; SwallowRadius = 40f; InflowSpeed = 85f;
                    Bend = 0.4f;  AlternateBend = true; TrainSpacing = 0.075f; ShrinkAtSink = 0.3f;
                    AlphaMin = 0.06f; AlphaMax = 0.34f;
                    break;

                case Preset.LimaWarna:
                    Layout = Mode.Inflow; Count = 45; Lanes = 5; ColourPerLane = true;
                    SizeMin = 30f; SizeMax = 62f; Spin = 6f;
                    SpawnRadius = 940f; SwallowRadius = 38f; InflowSpeed = 80f;
                    Bend = 0.5f;  AlternateBend = true; TrainSpacing = 0.07f; ShrinkAtSink = 0.3f;
                    AlphaMin = 0.07f; AlphaMax = 0.36f;
                    break;

                case Preset.TigaDeras:
                    Layout = Mode.Inflow; Count = 48; Lanes = 3; ColourPerLane = false;
                    SizeMin = 24f; SizeMax = 58f; Spin = 11f;
                    SpawnRadius = 1000f; SwallowRadius = 34f; InflowSpeed = 115f;
                    Bend = 0.65f; AlternateBend = true; TrainSpacing = 0.038f; ShrinkAtSink = 0.24f;
                    AlphaMin = 0.06f; AlphaMax = 0.4f;
                    break;

                case Preset.AnginEmpatWarna:
                    Layout = Mode.Inflow; Count = 36; Lanes = 4; ColourPerLane = true;
                    SizeMin = 32f; SizeMax = 66f; Spin = 4f;
                    SpawnRadius = 1020f; SwallowRadius = 44f; InflowSpeed = 78f;
                    Bend = 0.45f; AlternateBend = true;
                    TrainSpacing = 0.1f; ShrinkAtSink = 0.32f;
                    AlphaMin = 0.07f; AlphaMax = 0.36f;
                    break;

                case Preset.AnginCampur:
                    Layout = Mode.Inflow; Count = 36; Lanes = 4; ColourPerLane = false;
                    SizeMin = 32f; SizeMax = 66f; Spin = 4f;
                    SpawnRadius = 1020f; SwallowRadius = 44f; InflowSpeed = 78f;
                    Bend = 0.45f; AlternateBend = true;
                    TrainSpacing = 0.1f; ShrinkAtSink = 0.32f;
                    AlphaMin = 0.07f; AlphaMax = 0.36f;
                    break;

                case Preset.AnginLebar:
                    Layout = Mode.Inflow; Count = 48; Lanes = 6; ColourPerLane = true;
                    SizeMin = 26f; SizeMax = 54f; Spin = 3f;
                    SpawnRadius = 1080f; SwallowRadius = 46f; InflowSpeed = 62f;
                    Bend = 0.75f; AlternateBend = true;
                    TrainSpacing = 0.12f; ShrinkAtSink = 0.3f;
                    AlphaMin = 0.06f; AlphaMax = 0.3f;
                    break;

                case Preset.BintangTenang:
                    Layout = Mode.Scatter; Count = 24;
                    SizeMin = 24f; SizeMax = 88f; Spin = 5f;
                    Drift = new Vector2(0.22f, 1f); SpeedMin = 4f; SpeedMax = 15f;
                    AlphaMin = 0.04f; AlphaMax = 0.22f;
                    break;
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (SizeMax < SizeMin) SizeMax = SizeMin;
            if (SpeedMax < SpeedMin) SpeedMax = SpeedMin;
            if (AlphaMax < AlphaMin) AlphaMax = AlphaMin;

            // Menyusun ulang di tengah OnValidate bisa jatuh ke tengah impor aset; ditunda satu
            // langkah supaya Inspector tetap bisa diputar tanpa peringatan.
            if (isActiveAndEnabled) UnityEditor.EditorApplication.delayCall += SafeRebuild;
        }

        void SafeRebuild()
        {
            if (this != null && isActiveAndEnabled) Rebuild();
        }
#endif

        /// <summary>Membangun ulang seluruh kolam. Aman dipanggil kapan saja.</summary>
        public void Rebuild()
        {
            EnsureSheet();
            if (_sheet == null || _sheet.Length == 0) return;

            // Seed TETAP: latar menu yang susunannya berubah tiap kali dibuka tidak terbaca
            // sebagai latar, ia terbaca sebagai layar yang belum selesai memuat.
            _dice = new System.Random(20260814);

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            _motes = new Mote[Mathf.Max(1, Count)];
            var box = Box();

            for (int i = 0; i < _motes.Length; i++)
            {
                var go = new GameObject("Rune_" + i, typeof(RectTransform));
                go.transform.SetParent(transform, false);
                go.hideFlags = HideFlags.DontSave;

                int lane = Lanes <= 1 ? 0 : i % Lanes;
                int slot = Lanes <= 1 ? i : i / Lanes;

                var img = go.AddComponent<Image>();
                img.sprite = _sheet[PickSprite(lane)];
                img.preserveAspect = true;
                img.raycastTarget = false;

                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                float size = Mathf.Lerp(SizeMin, SizeMax, (float)_dice.NextDouble());

                _motes[i] = new Mote
                {
                    Rect = rt,
                    Art = img,
                    Size = size,
                    Speed = Mathf.Lerp(SpeedMin, SpeedMax, (float)_dice.NextDouble()),
                    Spin = ((float)_dice.NextDouble() * 2f - 1f) * Spin,
                    Phase = (float)_dice.NextDouble() * 100f,
                    Rail = i * TrainGap,
                    // Tiap JALUR punya sudut pangkalnya sendiri, disebar merata mengelilingi
                    // buku. Di dalam satu jalur sudutnya sama, dan yang membedakan posisi cuma
                    // seberapa jauh masing-masing sudah berjalan - itu yang membuatnya berbaris.
                    Angle = Lanes <= 1 ? 0f : lane * (Mathf.PI * 2f / Lanes),
                    Radius = SpawnRadius,
                    Lane = lane,
                    Slot = slot,
                    BendSign = !AlternateBend || (lane % 2 == 0) ? 1f : -1f,
                    Pos = new Vector2(
                        ((float)_dice.NextDouble() - 0.5f) * box.x,
                        ((float)_dice.NextDouble() - 0.5f) * box.y)
                };

                rt.sizeDelta = new Vector2(size, size);
            }
        }

        void Update()
        {
            if (CycleWithV && CyclePressed()) Cycle();

            if (_motes == null || _motes.Length == 0) return;

            // Tak berskala: menu tidak punya timescale, dan kalaupun nanti punya, latar yang ikut
            // membeku saat permainan dijeda terbaca sebagai layar yang hang.
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;

            var box = Box();
            var dir = Drift.sqrMagnitude < 0.0001f ? Vector2.up : Drift.normalized;

            if (Layout == Mode.Inflow)
            {
                float panjang = Mathf.Max(1f, SpawnRadius - SwallowRadius);
                _flow += InflowSpeed / panjang * dt;
                if (_flow > 1f) _flow -= Mathf.Floor(_flow);
            }

            for (int i = 0; i < _motes.Length; i++)
            {
                var m = _motes[i];
                if (m.Rect == null) continue;

                if (Layout == Mode.Scatter)
                {
                    m.Pos += dir * (m.Speed * dt);

                    // Membungkus di tepi, dengan kelonggaran seukuran runenya sendiri supaya ia
                    // tidak pernah terlihat MUNCUL di dalam layar - ia harus masuk dari luar.
                    float padX = box.x * 0.5f + m.Size;
                    float padY = box.y * 0.5f + m.Size;

                    if (m.Pos.x > padX) m.Pos.x = -padX;
                    else if (m.Pos.x < -padX) m.Pos.x = padX;

                    if (m.Pos.y > padY) m.Pos.y = -padY;
                    else if (m.Pos.y < -padY) m.Pos.y = padY;

                    m.Rect.anchoredPosition = m.Pos;
                }
                else if (Layout == Mode.Inflow)
                {
                    // Lintasan MELENGKUNG yang berujung di buku, bukan orbit yang mengitarinya.
                    // Bedanya menentukan: koordinat polar membuat rune berputar mengelilingi
                    // pusat - itu pusaran. Kurva Bezier membuatnya membelok sekali lalu MAJU
                    // terus ke satu titik, dan itu yang terbaca sebagai tiupan angin.
                    float jalan = _flow - m.Slot * TrainSpacing;
                    jalan -= Mathf.Floor(jalan);

                    var pusat = SinkOffset();
                    var arah = new Vector2(Mathf.Cos(m.Angle), Mathf.Sin(m.Angle));
                    var pangkal = pusat + arah * SpawnRadius;

                    // Titik kendali digeser TEGAK LURUS dari garis lurusnya. Sejauh apa ia
                    // digeser adalah seberapa melengkung alirannya.
                    var tegak = new Vector2(-arah.y, arah.x) * (m.BendSign * Bend * SpawnRadius);
                    var kendali = pusat + arah * (SpawnRadius * 0.55f) + tegak;

                    float sisa = 1f - jalan;
                    var titik = sisa * sisa * pangkal
                              + 2f * sisa * jalan * kendali
                              + jalan * jalan * pusat;

                    m.Rect.anchoredPosition = titik;

                    // Mengecil dan memudar sambil mendekat - itu yang membuatnya terbaca TERTELAN
                    // alih-alih menghilang begitu saja di atas bukunya. Memudarnya juga di
                    // PANGKAL, supaya ia tidak terlihat muncul dari ketiadaan.
                    float dekat = 1f - jalan;
                    float skala = Mathf.Lerp(ShrinkAtSink, 1f, dekat);
                    m.Rect.sizeDelta = new Vector2(m.Size * skala, m.Size * skala);

                    float tepi = Mathf.Min(Mathf.InverseLerp(0f, 0.12f, jalan),
                                           Mathf.InverseLerp(1f, 0.88f, jalan));
                    var warna = m.Art.color;
                    warna.a = Mathf.Lerp(AlphaMin, AlphaMax, Mathf.SmoothStep(0f, 1f, tepi));
                    m.Art.color = warna;

                    if (m.Spin != 0f)
                        m.Rect.localRotation = Quaternion.Euler(0f, 0f,
                            m.Rect.localEulerAngles.z + m.Spin * dt);

                    _motes[i] = m;
                    continue;
                }
                else
                {
                    m.Rail -= TrainSpeed * dt;

                    float span = _motes.Length * TrainGap;
                    if (m.Rail < -span * 0.5f) m.Rail += span;

                    float wave = Mathf.Sin((m.Rail / Mathf.Max(1f, TrainGap)) * 0.9f + m.Phase) * TrainWave;
                    m.Rect.anchoredPosition = new Vector2(m.Rail, TrainY + wave);
                }

                if (m.Spin != 0f)
                    m.Rect.localRotation = Quaternion.Euler(0f, 0f, m.Rect.localEulerAngles.z + m.Spin * dt);

                // Kedip. Tiap rune punya fasenya sendiri; tanpa itu seluruh latar bernapas
                // serempak dan terbaca sebagai layar yang berkedip, bukan sebagai bintang.
                float wavelet = (Mathf.Sin((now / Mathf.Max(0.5f, TwinkleSeconds) + m.Phase) * Mathf.PI * 2f) + 1f) * 0.5f;
                var c = m.Art.color;
                c.a = Mathf.Lerp(AlphaMin, AlphaMax, wavelet);
                m.Art.color = c;

                _motes[i] = m;
            }
        }

        /// <summary>
        /// Titik sedot dalam koordinat lokal kotak ini. Objek yang diseret menang atas angka -
        /// ia ikut kalau bukunya digeser, angka tidak.
        /// </summary>
        Vector2 SinkOffset()
        {
            if (_area == null) return SinkPoint;

            // Objek DUNIA menang. Bukunya hidup di scene 3D dan dioramanya menggesernya
            // terus-menerus; titik yang ditulis sebagai angka akan meleset beberapa detik setelah
            // permainan mulai, dan melesetnya pelan sehingga tidak pernah terlihat seperti bug.
            if (SinkWorld != null)
            {
                var kamera = _lens != null ? _lens : Camera.main;
                if (kamera != null)
                {
                    var layar = kamera.WorldToScreenPoint(SinkWorld.position);

                    // Kanvas Overlay memakai koordinat layar apa adanya, jadi kameranya null.
                    // Mengirim kamera di sana justru menggeser hasilnya.
                    var kanvasCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                        ? _canvas.worldCamera : null;

                    Vector2 lokal;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            _area, layar, kanvasCam, out lokal))
                        return lokal;
                }
            }

            if (Sink != null) return _area.InverseTransformPoint(Sink.TransformPoint(Sink.rect.center));
            return SinkPoint;
        }

        /// <summary>
        /// Gambar untuk sebuah jalur. Dengan <see cref="ColourPerLane"/> menyala, jalur ke-n
        /// mengambil dari kelompok warna ke-n; kalau tidak, dari seluruh sheet.
        /// </summary>
        int PickSprite(int lane)
        {
            if (!ColourPerLane || _sheet.Length < 16) return _dice.Next(_sheet.Length);

            int tier = lane % (Tiers.Length / 2);
            int mulai = Tiers[tier, 0];
            int panjang = Tiers[tier, 1];

            return Mathf.Min(_sheet.Length - 1, mulai + _dice.Next(panjang));
        }

        /// <summary>
        /// Preset berikutnya, melingkar, melewati Custom. Ada supaya membandingkan racikan tidak
        /// perlu keluar-masuk play mode - dua tampilan yang dilihat terpisah beberapa detik hampir
        /// mustahil dibandingkan dengan jujur.
        /// </summary>
        public void Cycle()
        {
            var all = (Preset[])System.Enum.GetValues(typeof(Preset));

            int at = System.Array.IndexOf(all, Racikan);
            do { at = (at + 1) % all.Length; } while (all[at] == Preset.Custom);

            Racikan = all[at];
            Apply(Racikan);
            Rebuild();

            Debug.Log("[MenuRuneDrift] preset: " + Racikan, this);
        }

        /// <summary>
        /// Tombol V, lewat Input System - project ini memakai package-nya, dan kelas Input
        /// warisan melempar exception TIAP FRAME kalau dipakai di situ. Pagarnya mengikuti pola
        /// yang sudah dipakai <see cref="ProtoInput"/>, jadi berpindah kembali ke Input lama
        /// tidak akan menyisakan satu berkas yang ketinggalan.
        /// </summary>
        static bool CyclePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.vKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.V);
#endif
        }

        Vector2 Box()
        {
            var size = _area != null ? _area.rect.size : new Vector2(1920f, 1080f);
            if (size.x < 1f || size.y < 1f) size = new Vector2(1920f, 1080f);
            return size;
        }

        void EnsureSheet()
        {
            if (_sheet != null && _sheet.Length > 0) return;

            var found = new System.Collections.Generic.List<Sprite>(SheetNames.Length);
            for (int i = 0; i < SheetNames.Length; i++)
            {
                var s = Resources.Load<Sprite>("Runes/" + SheetNames[i]);
                if (s != null) found.Add(s);
            }

            if (found.Count == 0)
            {
                Debug.LogWarning("[MenuRuneDrift] tidak ada gambar rune di Resources/Runes — " +
                                 "latar menu dibiarkan kosong.", this);
            }

            _sheet = found.ToArray();
        }
    }
}
