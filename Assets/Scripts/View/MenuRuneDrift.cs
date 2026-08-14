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
            Procession
        }

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
        }

        RectTransform _area;
        Sprite[] _sheet;
        Mote[] _motes;
        System.Random _dice;

        void OnEnable()
        {
            _area = (RectTransform)transform;
            Rebuild();
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
                    Pos = new Vector2(
                        ((float)_dice.NextDouble() - 0.5f) * box.x,
                        ((float)_dice.NextDouble() - 0.5f) * box.y)
                };

                rt.sizeDelta = new Vector2(size, size);
            }
        }

        void Update()
        {
            if (_motes == null || _motes.Length == 0) return;

            // Tak berskala: menu tidak punya timescale, dan kalaupun nanti punya, latar yang ikut
            // membeku saat permainan dijeda terbaca sebagai layar yang hang.
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;

            var box = Box();
            var dir = Drift.sqrMagnitude < 0.0001f ? Vector2.up : Drift.normalized;

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
                else
                {
                    m.Rail -= TrainSpeed * dt;

                    float span = _motes.Length * TrainGap;
                    if (m.Rail < -span * 0.5f) m.Rail += span;

                    float wave = Mathf.Sin((m.Rail / Mathf.Max(1f, TrainGap)) * 0.9f + m.Phase) * TrainWave;
                    m.Rect.anchoredPosition = new Vector2(m.Rail, wave);
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
