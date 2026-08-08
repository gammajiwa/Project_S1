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

        [Tooltip("Potong bola mata mengikuti bentuk gambar matanya. Tanpa ini bola matanya bisa " +
                 "menyeberang keluar ke bingkai saat melirik jauh.")]
        public bool MaskToEye = true;

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

        [Tooltip("Jarak antar pindah pandangan, dalam detik (acak).")]
        public Vector2 GlanceEvery = new Vector2(0.9f, 2.6f);

        [Tooltip("Peluang satu lirikan jatuh ke TENGAH — memandang lurus ke depan.")]
        [Range(0f, 1f)] public float GlanceCentre = 0.3f;

        [Tooltip("Peluang satu lirikan jatuh ke BAWAH, seolah membaca halamannya sendiri. " +
                 "Sisa peluangnya dipakai arah bebas.")]
        [Range(0f, 1f)] public float GlanceDown = 0.22f;

        [Tooltip("Kecepatan pindah saat celingukan. Sengaja jauh lebih tinggi dari Follow: mata " +
                 "sungguhan berpindah pandang dengan SENTAKAN lalu diam, tidak meluncur pelan. " +
                 "Meluncur terbaca sebagai benda yang digeser, bukan mata yang melirik.")]
        [Min(0.5f)] public float GlanceSnap = 16f;

        [Header("Kedip")]
        public bool Blink = true;

        [Tooltip("Jarak antar kedipan, dalam detik (acak).")]
        public Vector2 BlinkEvery = new Vector2(2.4f, 6.5f);

        [Tooltip("Lama satu kedipan penuh, turun DAN naik. Kedip sungguhan itu cepat — " +
                 "di atas ~0,25 detik ia berhenti terbaca sebagai kedip dan mulai terbaca " +
                 "sebagai mata yang mengantuk.")]
        [Range(0.05f, 0.4f)] public float BlinkSeconds = 0.13f;

        [Tooltip("Setipis apa matanya saat terpejam. Nol persis membuatnya lenyap satu frame; " +
                 "menyisakan sedikit membuat garisnya tetap ada.")]
        [Range(0f, 0.4f)] public float BlinkSquash = 0.06f;

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

        float _blinkLeft;
        float _blinkPlaying = -1f;

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

            if (MaskToEye && GetComponent<Image>() != null && GetComponent<Mask>() == null)
            {
                // showMaskGraphic: gambar matanya sendiri HARUS tetap tergambar. Mask bawaan
                // UGUI menyembunyikan grafisnya secara default, dan yang tersisa cuma bola
                // mata melayang tanpa mata.
                var mask = gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = true;
            }

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

        void TickBlink(float dt)
        {
            if (!Blink)
            {
                transform.localScale = Vector3.one;
                return;
            }

            if (_blinkPlaying >= 0f)
            {
                _blinkPlaying += dt;
                float t = Mathf.Clamp01(_blinkPlaying / Mathf.Max(0.01f, BlinkSeconds));

                // Turun lalu naik dalam satu gerak: sin memberi kedua arahnya tanpa cabang,
                // dan tanpa jeda di titik terpejam — jeda di sana yang membuatnya terbaca
                // sebagai mengantuk.
                float shut = Mathf.Sin(t * Mathf.PI);
                float squash = Mathf.Lerp(1f, BlinkSquash, shut);

                transform.localScale = new Vector3(1f, squash, 1f);

                if (t >= 1f)
                {
                    _blinkPlaying = -1f;
                    _blinkLeft = Roll(BlinkEvery);
                    transform.localScale = Vector3.one;
                }

                return;
            }

            _blinkLeft -= dt;
            if (_blinkLeft <= 0f) _blinkPlaying = 0f;
        }

        void TickGlow(float dt)
        {
            if (Glow == null) return;

            _glowClock += dt / Mathf.Max(0.1f, GlowSeconds);

            float wave = (Mathf.Sin(_glowClock * Mathf.PI * 2f) + 1f) * 0.5f;
            float lit = Mathf.Lerp(Mathf.Min(GlowPulse.x, GlowPulse.y),
                                   Mathf.Max(GlowPulse.x, GlowPulse.y), wave);

            // Pendarnya ikut padam saat terpejam. Mata terpejam yang tetap memancarkan cahaya
            // dari balik kelopaknya membatalkan seluruh kedipannya.
            if (_blinkPlaying >= 0f)
            {
                float t = Mathf.Clamp01(_blinkPlaying / Mathf.Max(0.01f, BlinkSeconds));
                lit *= 1f - Mathf.Sin(t * Mathf.PI);
            }

            var c = Glow.color;
            c.a = lit;
            Glow.color = c;
        }
    }
}
