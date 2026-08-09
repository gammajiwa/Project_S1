using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Lingkaran rune di papan grimoire: <b>menyala selama ada yang terpasang</b>, dan
    /// <b>berputar sekali</b> setiap kali sesuatu berevolusi.
    ///
    /// Dua peran itu sengaja dipisah tegas. Yang pertama KEADAAN — papan kosong versus papan
    /// yang sedang bekerja — dan keadaan harus bisa dibaca sekilas tanpa menunggu apa pun
    /// terjadi. Yang kedua KEJADIAN, dan kejadian harus punya awal dan akhir yang jelas supaya
    /// terbaca sebagai "barusan ada sesuatu", bukan sebagai hiasan yang kebetulan bergerak.
    ///
    /// Putarannya menumpang di atas nyalanya, tidak menggantikannya: begitu putaran selesai,
    /// runenya kembali ke terang atau redup sesuai keadaan papan saat itu — tanpa perlu
    /// diberi tahu ulang.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Grimoire/Grimoire Rune")]
    public class GrimoireRune : MonoBehaviour
    {
        [Tooltip("Gambar runenya. Kosong = Graphic yang menempel di objek ini sendiri.")]
        public Graphic Rune;

        [Header("Redup / menyala")]
        [Tooltip("Warna saat papan KOSONG. Jangan dibuat transparan penuh — rune yang lenyap " +
                 "sama sekali membuat papan terlihat rusak, bukan tidur.")]
        public Color Dim = new Color(1f, 1f, 1f, 0.12f);

        [Tooltip("Warna saat ada yang terpasang di papan.")]
        public Color Lit = new Color(1f, 0.86f, 0.72f, 0.8f);

        [Tooltip("Lama peralihan redup <-> menyala, dalam detik. Nyala yang menyentak terbaca " +
                 "sebagai lampu yang dipencet; yang perlahan terbaca sebagai sihir yang bangun.")]
        [Min(0.01f)] public float FadeSeconds = 0.4f;

        [Header("Napas saat menyala")]
        [Tooltip("Sebesar apa kepekatannya naik-turun selagi menyala. Nol = nyala mati (rata).")]
        [Range(0f, 1f)] public float Breathe = 0.18f;

        [Tooltip("Lama satu tarikan napas, dalam detik.")]
        [Min(0.1f)] public float BreatheSeconds = 3.2f;

        [Header("Putaran saat evolusi")]
        [Tooltip("Lama satu putaran penuh, dalam detik. Lingkaran sihir seukuran papan butuh " +
                 "waktu untuk terbaca BERAT — di bawah ~1,5 detik ia cuma terlihat menyentak.")]
        [Min(0.1f)] public float SpinSeconds = 2.2f;

        [Tooltip("Berapa derajat sekali putar. Negatif = berlawanan arah jarum jam.")]
        public float SpinDegrees = 360f;

        [Tooltip("Warna puncak saat berputar. Ini yang membuat putarannya terbaca sebagai " +
                 "'sesuatu barusan terjadi' alih-alih sebagai gambar yang kebetulan berputar.")]
        public Color Flash = new Color(1f, 0.95f, 0.75f, 1f);

        RectTransform _rect;
        bool _lit;
        float _fade;          // 0 = redup, 1 = menyala
        float _breatheClock;
        float _spin = -1f;    // < 0 = tidak sedang berputar

        void Awake()
        {
            _rect = (RectTransform)transform;
            if (Rune == null) Rune = GetComponent<Graphic>();
        }

        void OnEnable()
        {
            if (_rect == null) _rect = (RectTransform)transform;
            if (Rune == null) Rune = GetComponent<Graphic>();
        }

        /// <summary>Papan sedang berisi atau tidak. Aman dipanggil tiap frame.</summary>
        public void SetLit(bool on) => _lit = on;

        /// <summary>
        /// Satu putaran penuh sambil berkilau. Dipanggil ulang selagi masih berputar akan
        /// MENGULANG dari awal, bukan menumpuk — dua evolusi beruntun harus terbaca sebagai
        /// dua kejadian, dan putaran yang saling menimpa cuma jadi getaran.
        /// </summary>
        public void Celebrate() => _spin = 0f;

        void Update()
        {
            if (Rune == null) return;

            // Tak berskala: papan tetap hidup saat permainan dijeda, dan tidak ikut ngebut
            // saat pemain menyalakan kecepatan 5x.
            float dt = Time.unscaledDeltaTime;

            _fade = Mathf.MoveTowards(_fade, _lit ? 1f : 0f, dt / Mathf.Max(0.01f, FadeSeconds));

            var colour = Color.Lerp(Dim, Lit, _fade);

            // Napas hanya berlaku sejauh runenya memang sedang menyala — papan kosong yang
            // ikut bernapas menyiratkan ada yang berjalan padahal tidak ada.
            if (Breathe > 0.0001f && _fade > 0.0001f)
            {
                _breatheClock += dt / Mathf.Max(0.1f, BreatheSeconds);
                float wave = (Mathf.Sin(_breatheClock * Mathf.PI * 2f) + 1f) * 0.5f;
                colour.a *= Mathf.Lerp(1f - Breathe, 1f, wave) * Mathf.Lerp(1f, 1f, _fade);
            }

            if (_spin >= 0f)
            {
                _spin += dt;
                float t = Mathf.Clamp01(_spin / Mathf.Max(0.1f, SpinSeconds));

                // Berangkat pelan, cepat di tengah, mengendap pelan. Percobaan pertama memakai
                // ease-out (melesat lalu melambat) dan itu terbaca aneh: putarannya seperti
                // disentak lalu ditinggalkan. Benda seberat lingkaran sihir tidak berangkat
                // dari diam ke kecepatan puncak dalam satu frame.
                float eased = Mathf.SmoothStep(0f, 1f, t);
                _rect.localEulerAngles = new Vector3(0f, 0f, -SpinDegrees * eased);

                // Kilau naik lalu turun dalam satu gerak, tanpa jeda di puncaknya.
                colour = Color.Lerp(colour, Flash, Mathf.Sin(t * Mathf.PI));

                if (t >= 1f)
                {
                    // Dikembalikan ke nol PERSIS, bukan ke hasil putaran terakhir: sisa
                    // sepersekian derajat yang menumpuk tiap evolusi akan memiringkan runenya
                    // sedikit demi sedikit sepanjang satu run.
                    _rect.localEulerAngles = Vector3.zero;
                    _spin = -1f;
                }
            }

            Rune.color = colour;
        }
    }
}
