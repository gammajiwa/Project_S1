using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Mata di sampul grimoire: MENGAWASI kursor, sesekali melepaskan pandangannya kembali ke
    /// depan, dan berkedip.
    ///
    /// Kenapa tidak menatap terus-menerus: mata yang mengunci kursor tanpa jeda berhenti terbaca
    /// sebagai makhluk dan mulai terbaca sebagai widget — geraknya jadi fungsi kursor, dan apa
    /// pun yang geraknya bisa ditebak sempurna berhenti terasa hidup. Jeda memandang ke depan
    /// itulah yang mengembalikan kesan ada yang MEMILIH untuk melihat.
    ///
    /// Dipasang di objek mata (yang membawa gambar bola matanya sebagai anak). Semua yang
    /// dibutuhkannya dicari sendiri kalau slotnya dibiarkan kosong, jadi menempelkannya saja
    /// sudah cukup.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Grimoire/Grimoire Eye")]
    public class GrimoireEye : MonoBehaviour
    {
        [Header("Bagian-bagiannya")]
        [Tooltip("Bola matanya — yang bergerak. Kosong = anak pertama yang punya Image dipakai.")]
        public RectTransform Pupil;

        // Masking SENGAJA tidak diurus di sini.
        //
        // Memotong bola mata itu pekerjaan UGUI, dan UGUI sudah punya alatnya: taruh Mask atau
        // RectMask2D di objek pembungkus bola matanya, lalu tata lubangnya di jendela prefab.
        // Kode yang ikut menyusun hierarki mask cuma menambah satu sumber kebenaran kedua —
        // yang disusun tangan bisa ditimpa diam-diam saat run, dan yang mengubahnya tidak akan
        // pernah tahu kenapa.
        //
        // Yang dituntut komponen ini dari hierarkinya cuma satu: <see cref="Pupil"/> boleh
        // berada di mana saja, karena yang digerakkan anchoredPosition-nya sendiri.

        [Header("Jangkauan lirikan (pecahan setengah-sisi mata)")]
        [Tooltip("Seberapa jauh bola mata boleh menyimpang mendatar. Dijaga dalam ELIPS, bukan " +
                 "kotak — lirikan serong ke sudut kalau dijepit per sumbu akan pergi lebih jauh " +
                 "daripada lirikan lurus, dan itu terlihat sebagai mata yang juling di diagonal.")]
        [Range(0f, 1f)] public float ReachX = 0.22f;

        [Range(0f, 1f)] public float ReachY = 0.16f;

        [Tooltip("Kecepatan bola mata MENGIKUTI kursor. Rendah = malas dan berat, " +
                 "tinggi = waspada.")]
        [Min(0.5f)] public float Follow = 7f;

        [Header("Bangun kalau kursor mendekat")]
        [Tooltip("Jarak kursor ke mata, dalam piksel, di mana matanya masih peduli. Di luar ini " +
                 "ia berhenti mengikuti dan mulai celingukan sendiri.\n\n" +
                 "Ini yang membuatnya terbaca sebagai makhluk: mata yang mengunci kursor dari " +
                 "seberang layar geraknya jadi fungsi kursor semata, dan apa pun yang bisa " +
                 "ditebak sempurna berhenti terasa hidup.")]
        [Min(1f)] public float WakeRadius = 300f;

        [Tooltip("Lebar pita peralihannya, dalam piksel. Nol = matanya menyentak antara " +
                 "mengikuti dan celingukan tepat di garis batas.")]
        [Min(0f)] public float WakeFeather = 120f;

        [Header("Celingukan saat kursor jauh")]
        [Tooltip("Matikan kalau maunya diam memandang lurus ke depan saat tidak ada kursor dekat.")]
        public bool Wander = true;

        [Tooltip("Jarak antar pindah pandangan, dalam detik (acak).\n\n" +
                 "Sengaja panjang. Angka pertama (0,9-2,6 detik) membuatnya gelisah — mata yang " +
                 "berpindah pandang tiap satu-dua detik terbaca sebagai gugup, bukan sebagai " +
                 "sesuatu yang mengamati. Diamnya yang lama itu justru yang bikin merinding.")]
        public Vector2 GlanceEvery = new Vector2(3f, 7.5f);

        [Tooltip("Peluang satu lirikan jatuh ke TENGAH — memandang lurus ke depan.")]
        [Range(0f, 1f)] public float GlanceCentre = 0.3f;

        [Tooltip("Peluang satu lirikan jatuh ke BAWAH, seolah membaca halamannya sendiri. " +
                 "Sisa peluangnya dipakai arah bebas.")]
        [Range(0f, 1f)] public float GlanceDown = 0.22f;

        [Tooltip("Kecepatan pindah saat celingukan. Sengaja jauh lebih tinggi dari Follow: mata " +
                 "sungguhan berpindah pandang dengan SENTAKAN lalu diam, tidak meluncur pelan. " +
                 "Meluncur terbaca sebagai benda yang digeser, bukan mata yang melirik.")]
        [Min(0.5f)] public float GlanceSnap = 16f;

        // Kedipnya digerakkan dari GAMBAR kelopak, bukan dari memipihkan matanya.
        //
        // Percobaan pertama memakai squash sumbu Y pada seluruh mata, dan itu tidak pernah bisa
        // bagus: sprite matanya membawa bingkai berduri, jadi yang memipih bukan kelopak
        // melainkan durinya juga. Dua kelopak yang saling menghampiri memindahkan seluruh
        // pekerjaan ke aset — dan cuma aset yang bisa punya bentuk kelopak.

        [Header("Kedip (dua kelopak)")]
        [Tooltip("Kelopak ATAS. Taruh di posisi TERBUKA di prefab — posisi itu yang dicatat " +
                 "sebagai titik istirahatnya. Kosong = tidak ada kedip sama sekali.")]
        public RectTransform LidTop;

        [Tooltip("Kelopak BAWAH, juga ditaruh di posisi terbuka.")]
        public RectTransform LidBottom;

        [Tooltip("Ketinggian tempat kedua kelopak BERTEMU, dalam koordinat lokal mata. " +
                 "Nol = bertemu tepat di pusat mata. Geser kalau garis temunya mau lebih " +
                 "tinggi atau lebih rendah dari pusat.")]
        public float MeetAt;

        [Tooltip("Jarak antar kedipan, dalam detik (acak).")]
        public Vector2 BlinkEvery = new Vector2(4f, 11f);

        [Tooltip("Lama satu kedipan penuh, menutup DAN membuka. Kedip sungguhan itu cepat — " +
                 "di atas ~0,3 detik ia berhenti terbaca sebagai kedip dan mulai terbaca " +
                 "sebagai mata yang mengantuk.")]
        [Range(0.05f, 0.5f)] public float BlinkSeconds = 0.18f;

        [Tooltip("Bagian dari waktu itu yang dipakai MENUTUP. Sengaja di bawah setengah: " +
                 "kelopak sungguhan jatuh lebih cepat daripada ia terangkat, dan kedip yang " +
                 "simetris justru itu yang terbaca sebagai mesin.")]
        [Range(0.1f, 0.9f)] public float CloseShare = 0.35f;

        [Header("Pendar")]
        [Tooltip("Anak yang memakai material Grimoire/UiGlow. Kosong = tidak ada pendar, dan " +
                 "matanya tetap jalan normal.")]
        public Graphic Glow;

        [Tooltip("Kepekatan pendar saat paling redup dan paling terang.")]
        public Vector2 GlowPulse = new Vector2(0.45f, 1f);

        [Tooltip("Lama satu tarikan napas pendar, dalam detik.")]
        [Min(0.1f)] public float GlowSeconds = 2.6f;

        RectTransform _rect;
        Vector2 _home;
        Vector2 _target;
        Vector2 _at;

        Vector2 _glance;
        float _glanceLeft;
        bool _wasWatching;

        Vector2 _lidTopHome;
        Vector2 _lidBottomHome;
        float _blinkLeft;
        float _blinkPlaying = -1f;
        float _shut;

        float _glowClock;

        // Undian sendiri, TIDAK memakai UnityEngine.Random: yang di sana urutan acak milik
        // gameplay (jenis musuh, sebaran drop, arah semburan skill), dan mengambil satu angka
        // dari sana untuk hiasan menggeser seluruh sisanya.
        System.Random _dice;

        void Awake()
        {
            _rect = (RectTransform)transform;
            _dice = new System.Random(GetInstanceID());

            if (Pupil == null)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i);
                    if (child.GetComponent<Graphic>() == null) continue;
                    if (Glow != null && child.gameObject == Glow.gameObject) continue;

                    Pupil = child as RectTransform;
                    break;
                }
            }

            if (Pupil != null)
            {
                _home = Pupil.anchoredPosition;
                _at = _home;
            }

            // Titik istirahat kelopak dibaca dari prefab, bukan ditulis di kode: yang menata
            // matanya yang tahu di mana kelopak terbuka itu berhenti, dan angkanya berubah tiap
            // kali gambarnya diganti.
            if (LidTop != null) _lidTopHome = LidTop.anchoredPosition;
            if (LidBottom != null) _lidBottomHome = LidBottom.anchoredPosition;

            _glanceLeft = Roll(GlanceEvery);
            _blinkLeft = Roll(BlinkEvery);
        }

        float Roll(Vector2 range)
        {
            float lo = Mathf.Min(range.x, range.y);
            float hi = Mathf.Max(range.x, range.y);
            return lo + (float)_dice.NextDouble() * (hi - lo);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            AimPupil(dt);
            TickBlink(dt);
            TickGlow(dt);
        }

        void AimPupil(float dt)
        {
            if (Pupil == null) return;

            var size = _rect.rect.size;
            if (size.x < 1f || size.y < 1f) return;

            Vector2 half = size * 0.5f;

            // Satu kali konversi dipakai untuk DUA hal: seberapa jauh kursornya, dan ke arah
            // mana. Kanvas ini ConstantPixelSize skala 1, jadi satuan lokalnya memang piksel.
            Vector2 toCursor;
            bool haveCursor = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rect, ProtoInput.MousePosition, null, out toCursor);

            float interest = 0f;
            if (haveCursor)
            {
                float far = Mathf.Max(1f, WakeRadius);
                float near = Mathf.Max(0f, far - WakeFeather);
                interest = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(far, near, toCursor.magnitude));
            }

            // Kursor yang PERGI melepaskan pandangannya ke depan dulu.
            //
            // Tanpa ini, mata yang barusan mengunci kursor langsung menyambar arah acak begitu
            // kursornya menjauh — dan sambaran itu terbaca sebagai kehilangan kendali, bukan
            // sebagai kehilangan minat. Melepas ke tengah dulu yang membuatnya terbaca berhenti
            // memperhatikan.
            bool watching = interest > 0.5f;

            if (_wasWatching && !watching)
            {
                _glance = Vector2.zero;
                _glanceLeft = Roll(GlanceEvery);
            }

            _wasWatching = watching;

            Vector2 look = haveCursor ? Clamped(toCursor, half) : Vector2.zero;
            Vector2 idle = TickGlance(dt, half);

            // Bukan saklar melainkan campuran: kursor yang mendekat mengambil alih pandangan
            // secara bertahap. Bertukar mendadak di satu garis jarak terbaca sebagai patah.
            _target = Vector2.Lerp(idle, look, interest);

            // Menyusul kursor itu MENGIKUTI (halus); berpindah saat celingukan itu MENYENTAK.
            float speed = Mathf.Lerp(GlanceSnap, Follow, interest);

            // Waktu TAK BERSKALA: matanya tetap hidup saat permainan dijeda atau dipercepat.
            // Dipercepat 5x, mata yang ikut skala akan melirik dengan kecepatan yang konyol.
            _at = Vector2.Lerp(_at, _target, 1f - Mathf.Exp(-speed * dt));
            Pupil.anchoredPosition = _home + _at;
        }

        /// <summary>
        /// Ke mana matanya memandang saat tidak ada kursor di dekatnya: berpindah-pindah,
        /// sesekali lurus ke depan, sesekali menunduk ke halamannya sendiri.
        /// </summary>
        Vector2 TickGlance(float dt, Vector2 half)
        {
            if (!Wander) return Vector2.zero;

            _glanceLeft -= dt;
            if (_glanceLeft > 0f) return _glance;

            _glanceLeft = Roll(GlanceEvery);

            double pick = _dice.NextDouble();

            if (pick < GlanceCentre)
            {
                _glance = Vector2.zero;
            }
            else if (pick < GlanceCentre + GlanceDown)
            {
                // Menunduk, dengan sedikit simpangan mendatar — menunduk lurus persis setiap
                // kali membuat pengulangannya ketahuan.
                float sway = (float)(_dice.NextDouble() * 2.0 - 1.0) * 0.35f;
                _glance = Clamped(new Vector2(sway * half.x, -half.y), half);
            }
            else
            {
                float ang = (float)(_dice.NextDouble() * System.Math.PI * 2.0);
                // Akar dari undian: tanpa itu titiknya menggerombol di pusat elips, dan
                // lirikannya jadi kecil-kecil semua.
                float len = Mathf.Sqrt((float)_dice.NextDouble());
                _glance = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len;
                _glance = Clamped(new Vector2(_glance.x * half.x, _glance.y * half.y), half);
            }

            return _glance;
        }

        /// <summary>
        /// Simpangan bola mata dijepit ke dalam ELIPS jangkauannya.
        ///
        /// Elips, bukan dua sumbu terpisah: menjepit per sumbu membiarkan sudut diagonal
        /// menampung simpangan penuh di KEDUA sumbu sekaligus, dan bola mata yang melirik ke
        /// pojok akan menonjol lebih jauh daripada yang melirik lurus — terbaca sebagai juling.
        /// </summary>
        Vector2 Clamped(Vector2 local, Vector2 half)
        {
            // Diukur dalam setengah-sisi supaya jangkauannya bisa ditulis sebagai pecahan dan
            // tetap benar berapa pun matanya dibesarkan di prefab.
            Vector2 n = new Vector2(local.x / Mathf.Max(1f, half.x), local.y / Mathf.Max(1f, half.y));

            float reachX = Mathf.Max(0.0001f, ReachX);
            float reachY = Mathf.Max(0.0001f, ReachY);

            float reach = Mathf.Sqrt(n.x * n.x / (reachX * reachX) + n.y * n.y / (reachY * reachY));
            if (reach > 1f) n /= reach;

            return new Vector2(n.x * half.x, n.y * half.y);
        }

        /// <summary>
        /// Kedip: kedua kelopak berjalan dari tempat istirahatnya menuju satu garis temu, lalu
        /// kembali. Diam saja kalau kelopaknya belum ada.
        /// </summary>
        void TickBlink(float dt)
        {
            if (LidTop == null && LidBottom == null) return;

            if (_blinkPlaying < 0f)
            {
                _blinkLeft -= dt;
                if (_blinkLeft > 0f) return;

                _blinkPlaying = 0f;
            }

            _blinkPlaying += dt;

            float t = Mathf.Clamp01(_blinkPlaying / Mathf.Max(0.01f, BlinkSeconds));
            float close = Mathf.Clamp(CloseShare, 0.05f, 0.95f);

            // Menutup CEPAT, membuka lebih lambat. Kurva simetris membuat kedipnya terbaca
            // sebagai mesin — kelopak sungguhan jatuh lebih cepat daripada ia terangkat.
            float raw = t < close ? t / close : 1f - (t - close) / (1f - close);
            _shut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(raw));

            if (LidTop != null)
                LidTop.anchoredPosition = Vector2.Lerp(
                    _lidTopHome, new Vector2(_lidTopHome.x, MeetAt), _shut);

            if (LidBottom != null)
                LidBottom.anchoredPosition = Vector2.Lerp(
                    _lidBottomHome, new Vector2(_lidBottomHome.x, MeetAt), _shut);

            if (t < 1f) return;

            // Dikembalikan PERSIS ke titik istirahatnya, bukan dibiarkan di hasil lerp terakhir:
            // sisa sepersekian piksel yang menumpuk tiap kedipan akan menurunkan kelopaknya
            // sedikit demi sedikit sepanjang sesi.
            if (LidTop != null) LidTop.anchoredPosition = _lidTopHome;
            if (LidBottom != null) LidBottom.anchoredPosition = _lidBottomHome;

            _shut = 0f;
            _blinkPlaying = -1f;
            _blinkLeft = Roll(BlinkEvery);
        }

        void TickGlow(float dt)
        {
            if (Glow == null) return;

            _glowClock += dt / Mathf.Max(0.1f, GlowSeconds);

            float wave = (Mathf.Sin(_glowClock * Mathf.PI * 2f) + 1f) * 0.5f;
            float lit = Mathf.Lerp(Mathf.Min(GlowPulse.x, GlowPulse.y),
                                   Mathf.Max(GlowPulse.x, GlowPulse.y), wave);

            // Pendarnya ikut padam saat terpejam. Mata tertutup yang tetap memancarkan cahaya
            // dari balik kelopaknya membatalkan seluruh kedipannya.
            lit *= 1f - _shut;

            var c = Glow.color;
            c.a = lit;
            Glow.color = c;
        }
    }
}
