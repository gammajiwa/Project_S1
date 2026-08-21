using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Ritual visual evolusi yang BERHASIL, dimainkan tepat di footprint tempat hasilnya
    /// duduk ("pertama jadi putih dulu, sudah itu di tengah mulai keluar gambar yang baru
    /// ... udah beres ada nge-pop sambil ngeluarin vfx distorsi ruang" — pemilik project).
    ///
    /// Empat babak dalam satu overlay: (1) siluet hasil menyala PUTIH, (2) ditahan sejenak,
    /// (3) warna aslinya TUMBUH dari pusat — dikerjakan shader Grimoire/UiEvoReveal,
    /// (4) POP: sentakan skala + cincin kejut Grimoire/UiShockwave, lalu overlay pudar dan
    /// yang tersisa adalah piece sungguhan yang sudah digambar papan sejak merge terjadi.
    /// Karena papan menggambar hasilnya SEJAK FRAME PERTAMA, efek ini murni hiasan —
    /// mati (shader hilang, kolam penuh) tidak pernah menyembunyikan keadaan papan.
    ///
    /// Kelas polos yang di-tick GrimoireUI, bukan MonoBehaviour — pola yang sama dengan
    /// DamagePopups: satu pemilik, satu urutan gambar, tanpa lifecycle Unity tersembunyi.
    ///
    /// Waktunya UNSCALED: evolusi terjadi saat wave beres, tepat ketika dunia sedang
    /// dihentikan — pakai waktu dunia, ritualnya beku selamanya di babak pertama.
    /// </summary>
    public class EvoRevealFx
    {
        /// <summary>Evolusi berantai terpanjang yang masuk akal dalam satu wave. Lebih dari
        /// ini, slot tertua direbut — ritual keenam lebih penting dari sisa ritual pertama.</summary>
        const int PoolSize = 6;

        const float FlashIn = 0.28f;
        const float Hold = 0.14f;
        const float RevealTime = 0.62f;
        const float PopTime = 0.45f;

        static float Duration => FlashIn + Hold + RevealTime + PopTime;

        /// <summary>Detik ke berapa POP-nya jatuh sejak Play — GrimoireUI menjadwalkan sweep
        /// outline hasil evolusi di titik ini, supaya cahaya kelilingnya lahir dari letupan.</summary>
        public static float PopAt => FlashIn + Hold + RevealTime;

        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int ProgressId = Shader.PropertyToID("_Progress");

        class Fx
        {
            public Image Art;
            public Image Ring;
            public Material ArtMat;
            public Material RingMat;
            public float Age;
            public bool Live;

            /// <summary>Mode CINCIN SAJA: satu gelombang distorsi sekejap, tanpa art —
            /// dipakai letupan pakta di tengah layar ("distorsi doang, sebentar").</summary>
            public bool RingOnly;
        }

        /// <summary>Umur gelombang cincin-saja. Sekejap — permintaan pemilik project.</summary>
        const float RingOnlyTime = 0.7f;

        readonly Fx[] _pool = new Fx[PoolSize];
        readonly RectTransform _layer;
        readonly Shader _artShader;
        readonly Shader _ringShader;

        public EvoRevealFx(Transform canvas)
        {
            // Shader dicari SEKALI di sini, bukan per Play: hilang dua-duanya berarti efek
            // ini diam seumur sesi, dan itu keadaan yang sah — papan tetap benar tanpanya.
            _artShader = Shader.Find("Grimoire/UiEvoReveal");
            _ringShader = Shader.Find("Grimoire/UiShockwave");

            var go = new GameObject("EvoRevealFx");
            go.transform.SetParent(canvas, false);
            _layer = go.AddComponent<RectTransform>();
            _layer.anchorMin = Vector2.zero;
            _layer.anchorMax = Vector2.one;
            _layer.offsetMin = Vector2.zero;
            _layer.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Satu ritual di satu footprint. <paramref name="center"/> dan <paramref name="size"/>
        /// dalam piksel kanvas (ruang yang sama dengan GridPoint); <paramref name="sprite"/>
        /// boleh null — ritualnya jatuh ke kotak cahaya polos, tetap terbaca sebagai kejadian.
        /// </summary>
        public void Play(Vector2 center, Vector2 size, Sprite sprite) =>
            Setup(center, size, sprite, Color.white, 2.6f, 0f, true);

        /// <summary>Ritual yang sama, diwarnai — dipakai ikon pakta di posisi kartunya.</summary>
        public void Play(Vector2 center, Vector2 size, Sprite sprite, Color tint) =>
            Setup(center, size, sprite, tint, 2.6f, 0f, true);

        /// <summary>
        /// Ritual dengan GEOMETRI PERSIS milik art papan — ukuran, pusat, dan SUDUT dari
        /// PieceArt.Layout, tanpa preserveAspect. Overlay harus lahir persis di atas gambar
        /// yang akan ditinggalkannya; kotak pembatas + preserveAspect membuat gambarnya
        /// "melompat" saat overlay pudar ("gak ngikutin scale dari gambar di grid" —
        /// pemilik project).
        /// </summary>
        public void PlayMatched(Vector2 center, Vector2 size, float angle, Sprite sprite) =>
            Setup(center, size, sprite, Color.white, 2.6f, angle, false);

        /// <summary>
        /// SATU gelombang distorsi sekejap, tanpa art — letupan pakta di tengah layar.
        /// Dulu pakta memutar ritual sigil besar di tengah kamera; pemilik project menolak:
        /// "distorsi doang di tengah, icon-nya di tempatnya aja".
        /// </summary>
        public void PlayRingOnly(Vector2 center, float diameter)
        {
            if (_ringShader == null) return;

            var fx = Take();
            if (fx.Ring == null) return;

            fx.Live = true;
            fx.Age = 0f;
            fx.RingOnly = true;
            fx.Art.enabled = false;

            fx.Ring.rectTransform.anchoredPosition = center;
            fx.Ring.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            fx.RingMat.SetFloat(ProgressId, 0f);
            fx.Ring.enabled = true;

            _layer.SetAsLastSibling();
        }

        void Setup(Vector2 center, Vector2 size, Sprite sprite, Color tint, float ringMul,
            float angle, bool preserveAspect)
        {
            if (_artShader == null) return;

            var fx = Take();
            fx.Live = true;
            fx.Age = 0f;
            fx.RingOnly = false;

            fx.Art.sprite = sprite;
            fx.Art.preserveAspect = preserveAspect && sprite != null;
            fx.Art.color = new Color(tint.r, tint.g, tint.b, 1f);
            fx.Art.enabled = true;
            fx.ArtMat.SetFloat(FlashId, 0f);
            fx.ArtMat.SetFloat(RevealId, 0f);

            var art = fx.Art.rectTransform;
            art.anchoredPosition = center;
            art.sizeDelta = size;
            art.localScale = Vector3.one * 0.9f;

            // Selalu ditulis, jangan hanya saat berotasi: slot kolam bekas art miring akan
            // menulari ritual berikutnya kalau sudutnya tidak dikembalikan.
            art.localEulerAngles = new Vector3(0f, 0f, angle);

            if (fx.Ring != null)
            {
                // Cincinnya butuh ruang melebar jauh melewati piece-nya; quad-nya digambar
                // prosedural, jadi memperbesar kotak tidak menurunkan ketajaman apa pun.
                float d = Mathf.Max(size.x, size.y) * ringMul;
                fx.Ring.rectTransform.anchoredPosition = center;
                fx.Ring.rectTransform.sizeDelta = new Vector2(d, d);
                fx.Ring.enabled = false;
            }

            // Di kanvas ini urutan anak = urutan gambar, dan panel lain menaikkan dirinya
            // tiap frame — ritual yang tenggelam di balik papan bukan ritual.
            _layer.SetAsLastSibling();
        }

        /// <summary>Slot mati pertama, atau yang paling tua kalau semuanya masih hidup.</summary>
        Fx Take()
        {
            Fx oldest = null;

            for (int i = 0; i < PoolSize; i++)
            {
                if (_pool[i] == null) _pool[i] = Build(i);
                if (!_pool[i].Live) return _pool[i];
                if (oldest == null || _pool[i].Age > oldest.Age) oldest = _pool[i];
            }

            return oldest;
        }

        Fx Build(int index)
        {
            var fx = new Fx();

            var go = new GameObject($"EvoReveal_{index}");
            go.transform.SetParent(_layer, false);
            fx.Art = go.AddComponent<Image>();
            fx.Art.raycastTarget = false;
            fx.Art.enabled = false;
            fx.ArtMat = new Material(_artShader);
            fx.Art.material = fx.ArtMat;

            var rt = fx.Art.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            if (_ringShader != null)
            {
                var ringGo = new GameObject($"EvoRing_{index}");
                ringGo.transform.SetParent(_layer, false);
                fx.Ring = ringGo.AddComponent<Image>();
                fx.Ring.raycastTarget = false;
                fx.Ring.enabled = false;
                fx.RingMat = new Material(_ringShader);
                fx.Ring.material = fx.RingMat;

                var ringRt = fx.Ring.rectTransform;
                ringRt.anchorMin = ringRt.anchorMax = Vector2.zero;
                ringRt.pivot = new Vector2(0.5f, 0.5f);
            }

            return fx;
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var fx = _pool[i];
                if (fx == null || !fx.Live) continue;

                fx.Age += dt;
                float age = fx.Age;

                // Gelombang cincin-saja: hidup, melebar, mati — tidak ikut babak ritual.
                if (fx.RingOnly)
                {
                    if (age >= RingOnlyTime || fx.Ring == null)
                    {
                        fx.Live = false;
                        if (fx.Ring != null) fx.Ring.enabled = false;
                    }
                    else
                    {
                        fx.RingMat.SetFloat(ProgressId, age / RingOnlyTime);
                    }

                    continue;
                }

                if (age >= Duration)
                {
                    fx.Live = false;
                    fx.Art.enabled = false;
                    if (fx.Ring != null) fx.Ring.enabled = false;
                    continue;
                }

                if (age < FlashIn)
                {
                    // Memutih sambil sedikit MENGEMBANG — perubahan wujud dimulai dengan
                    // menarik napas, bukan dengan berganti gambar begitu saja.
                    float t = age / FlashIn;
                    fx.ArtMat.SetFloat(FlashId, t * t);
                    fx.Art.rectTransform.localScale = Vector3.one * (0.9f + 0.14f * t);
                }
                else if (age < FlashIn + Hold)
                {
                    fx.ArtMat.SetFloat(FlashId, 1f);
                    fx.Art.rectTransform.localScale = Vector3.one * 1.04f;
                }
                else if (age < FlashIn + Hold + RevealTime)
                {
                    // _Flash turun BERSAMA naiknya _Reveal: putihnya menyingkir persis
                    // secepat warna barunya tumbuh, tidak pernah menyisakan siluet kusam.
                    float t = (age - FlashIn - Hold) / RevealTime;
                    fx.ArtMat.SetFloat(FlashId, 1f - t);
                    fx.ArtMat.SetFloat(RevealId, t);
                    fx.Art.rectTransform.localScale = Vector3.one * (1.04f - 0.04f * t);
                }
                else
                {
                    float t = (age - FlashIn - Hold - RevealTime) / PopTime;

                    fx.ArtMat.SetFloat(FlashId, 0f);
                    fx.ArtMat.SetFloat(RevealId, 1f);

                    // Sentakan setengah sinus: membesar lalu kembali — "nge-pop"-nya.
                    fx.Art.rectTransform.localScale =
                        Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.16f);

                    // Overlay pudar di paruh kedua; di bawahnya piece asli sudah menunggu.
                    var c = fx.Art.color;
                    c.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, t));
                    fx.Art.color = c;

                    if (fx.Ring != null)
                    {
                        fx.Ring.enabled = true;
                        fx.RingMat.SetFloat(ProgressId, t);
                    }
                }
            }
        }
    }
}
