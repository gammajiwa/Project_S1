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
            BintangTenang
        }

        [Header("Preset")]
        [Tooltip("Ganti untuk mencoba racikan lain. Pilih Custom kalau mau menahan angka yang " +
                 "sudah kamu atur sendiri - preset lain akan menimpanya. " +
                 "Saat bermain, tekan tombol di bawah untuk memutar antar preset tanpa berhenti.")]
        public Preset Racikan = Preset.SedotanPelan;

        [Tooltip("Tombol untuk memutar preset saat bermain. None = mati.")]
        public KeyCode CycleKey = KeyCode.V;

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
        [Tooltip("Objek yang menyedot. Kosong = pakai titik di bawah. " +
                 "Diseret dari scene supaya sedotannya tetap menempel di bukunya walau bukunya " +
                 "digeser - titik dalam angka akan diam-diam meleset begitu tata letaknya berubah.")]
        public RectTransform Sink;

        [Tooltip("Titik sedot terhadap tengah layar, dipakai kalau Sink kosong.")]
        public Vector2 SinkPoint = new Vector2(0f, -40f);

        [Tooltip("Sejauh apa barisan mulai dari titik sedot.")]
        [Min(50f)] public float SpawnRadius = 900f;

        [Tooltip("Sedekat apa ia boleh sampai sebelum dianggap tertelan dan kembali ke pangkal.")]
        [Min(4f)] public float SwallowRadius = 40f;

        [Tooltip("Laju mengalir sepanjang lintasan, piksel per detik.")]
        [Min(1f)] public float InflowSpeed = 150f;

        [Tooltip("Berapa PUTARAN penuh yang ditempuh barisan dari pangkal sampai tertelan. " +
                 "Inilah yang membuatnya BELOK-BELOK alih-alih menukik lurus. Nol = garis lurus " +
                 "ke tengah; 1,5 sudah terbaca jelas sebagai pusaran.")]
        [Range(0f, 4f)] public float Turns = 1.4f;

        [Tooltip("Jarak antar gerbong sepanjang lintasan, sebagai pecahan panjang lintasan. " +
                 "Kecil = rapat mengekor, besar = renggang.")]
        [Range(0.005f, 0.2f)] public float TrainSpacing = 0.045f;

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
            public float Angle;    // sedotan: sudut sekarang, radian
            public float Radius;   // sedotan: jarak ke titik sedot
        }

        RectTransform _area;

        /// <summary>Seberapa jauh barisan sudah berjalan, dalam pecahan panjang lintasan.</summary>
        float _flow;

        Sprite[] _sheet;
        Mote[] _motes;
        System.Random _dice;

        void OnEnable()
        {
            _area = (RectTransform)transform;
            Apply(Racikan);
            Rebuild();
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
                    SpawnRadius = 880f; SwallowRadius = 40f; InflowSpeed = 130f;
                    Turns = 1.2f; TrainSpacing = 0.05f; ShrinkAtSink = 0.32f;
                    AlphaMin = 0.05f; AlphaMax = 0.30f;
                    break;

                case Preset.SedotanDeras:
                    Layout = Mode.Inflow; Count = 40;
                    SizeMin = 26f; SizeMax = 66f; Spin = 9f;
                    SpawnRadius = 960f; SwallowRadius = 36f; InflowSpeed = 260f;
                    Turns = 1.6f; TrainSpacing = 0.026f; ShrinkAtSink = 0.28f;
                    AlphaMin = 0.06f; AlphaMax = 0.38f;
                    break;

                case Preset.PusaranKetat:
                    Layout = Mode.Inflow; Count = 34;
                    SizeMin = 22f; SizeMax = 58f; Spin = 14f;
                    SpawnRadius = 780f; SwallowRadius = 30f; InflowSpeed = 170f;
                    Turns = 2.8f; TrainSpacing = 0.03f; ShrinkAtSink = 0.2f;
                    AlphaMin = 0.05f; AlphaMax = 0.34f;
                    break;

                case Preset.KeretaMendatar:
                    Layout = Mode.Procession; Count = 16;
                    SizeMin = 44f; SizeMax = 78f; Spin = 3f;
                    TrainSpeed = 46f; TrainGap = 132f; TrainWave = 22f; TrainY = -250f;
                    AlphaMin = 0.07f; AlphaMax = 0.32f;
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

                var img = go.AddComponent<Image>();
                img.sprite = _sheet[_dice.Next(_sheet.Length)];
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
                    // Sudut pangkal SAMA untuk semua: mereka satu barisan di satu lintasan, dan
                    // yang membedakan posisinya cuma seberapa jauh masing-masing sudah berjalan.
                    Angle = 0f,
                    Radius = SpawnRadius,
                    Pos = new Vector2(
                        ((float)_dice.NextDouble() - 0.5f) * box.x,
                        ((float)_dice.NextDouble() - 0.5f) * box.y)
                };

                rt.sizeDelta = new Vector2(size, size);
            }
        }

        void Update()
        {
            if (CycleKey != KeyCode.None && Input.GetKeyDown(CycleKey)) Cycle();

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
                    // SATU lintasan, semua gerbong mengekor di atasnya. Itu bedanya dengan
                    // gerombolan: kalau tiap rune punya sudut sendiri, yang terbaca adalah benda
                    // berjatuhan dari segala arah - bukan barisan yang sedang ditarik masuk.
                    float panjang = Mathf.Max(1f, SpawnRadius - SwallowRadius);
                    float jalan = _flow - i * TrainSpacing;

                    // Dibungkus ke [0,1): begitu satu gerbong tertelan ia muncul lagi di pangkal,
                    // jadi barisannya tidak pernah putus.
                    jalan -= Mathf.Floor(jalan);

                    float radius = Mathf.Lerp(SpawnRadius, SwallowRadius, jalan);
                    float sudut = m.Angle + jalan * Turns * Mathf.PI * 2f;

                    var pusat = SinkOffset();
                    m.Rect.anchoredPosition = pusat + new Vector2(
                        Mathf.Cos(sudut) * radius, Mathf.Sin(sudut) * radius);

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
            if (Sink == null || _area == null) return SinkPoint;

            var dunia = Sink.TransformPoint(Sink.rect.center);
            return _area.InverseTransformPoint(dunia);
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
