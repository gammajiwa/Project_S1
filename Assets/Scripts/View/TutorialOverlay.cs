using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Proto.GrimoireLayout;

namespace Proto
{
    /// <summary>
    /// Tutorial gambar satu-kali: layar digelapkan, SATU objek dibiarkan terang (lubang di
    /// antara empat panel gelap), bingkai emas berdenyut memeluknya, dan satu kalimat pendek
    /// menjelaskan. Klik di mana pun = langkah berikutnya; langkah habis = selesai selamanya.
    ///
    /// Ditandai lewat PlayerPrefs (<see cref="BeginOnce"/>), bukan file save run: perintah
    /// pemilik project — "muncul harus di awal dan gak pernah muncul lagi", lintas run.
    ///
    /// Bukan MonoBehaviour: GrimoireUI yang memanggil Draw/Advance dari Update-nya sendiri,
    /// pola yang sama dengan StatusStrip dan RecipePanel. Dibangun PALING AKHIR di kanvas
    /// supaya duduk di atas seluruh HUD (urutan pembuatan = urutan gambar).
    /// </summary>
    public sealed class TutorialOverlay
    {
        public readonly struct Step
        {
            /// <summary>Kotak layar (satuan kanvas) yang disorot. Func, bukan Rect beku:
            /// papan bisa berpindah/berubah ukuran di antara dua frame tutorial.</summary>
            public readonly System.Func<Rect> Area;

            /// <summary>Kunci Loc kalimat penjelasnya.</summary>
            public readonly string Key;

            public Step(System.Func<Rect> area, string key)
            {
                Area = area;
                Key = key;
            }
        }

        const float FrameThickness = 3f;
        const float FramePad = 6f;
        const float PlaqueW = 660f;
        const float PlaqueH = 118f;

        readonly GameObject _root;
        readonly Image[] _dim = new Image[4];
        readonly Image[] _frame = new Image[4];
        readonly Image _plaque;
        readonly TextMeshProUGUI _text;
        readonly TextMeshProUGUI _hint;

        Step[] _steps;
        int _at;

        public bool Active { get; private set; }

        const string PrefPrefix = "GrimTut_";

        /// <summary>Semua babak yang dikenal — dipakai <see cref="ResetSeen"/> supaya babak
        /// baru cukup didaftarkan di sini dan tombol reset otomatis ikut menghapusnya.</summary>
        static readonly string[] Chapters = { "intro", "rest" };

        /// <summary>
        /// Sekali seumur install: true HANYA pada panggilan pertama untuk <paramref name="key"/>.
        /// Menandai SAAT MULAI, bukan saat selesai — pemain yang menutup jendela di tengah
        /// tutorial tetap dianggap sudah melihatnya; tutorial yang memaksa diulang lebih
        /// menyebalkan daripada tutorial yang terlewat separuh.
        /// </summary>
        public static bool BeginOnce(string key)
        {
            string pref = PrefPrefix + key;
            if (PlayerPrefs.GetInt(pref, 0) != 0) return false;

            PlayerPrefs.SetInt(pref, 1);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>
        /// Menghapus tanda "sudah pernah" untuk SEMUA babak. Dipanggil tombol reset di
        /// setelan (satu tombol dengan hapus codex) supaya tutorialnya bisa diuji
        /// berulang-ulang — permintaan pemilik project.
        /// </summary>
        public static void ResetSeen()
        {
            for (int i = 0; i < Chapters.Length; i++)
                PlayerPrefs.DeleteKey(PrefPrefix + Chapters[i]);

            PlayerPrefs.Save();
        }

        public TutorialOverlay(Transform canvas, TMP_FontAsset font)
        {
            _root = new GameObject("Tutorial", typeof(RectTransform));
            var rootRt = (RectTransform)_root.transform;
            rootRt.SetParent(canvas, false);
            rootRt.anchorMin = rootRt.anchorMax = Vector2.zero;
            rootRt.pivot = Vector2.zero;
            rootRt.anchoredPosition = Vector2.zero;
            rootRt.sizeDelta = Vector2.zero;

            // Empat panel gelap mengepung lubang sorotan. Lubang sungguhan (bukan shader,
            // bukan mask): yang di dalamnya tetap tergambar oleh kanvas seperti biasa.
            //
            // KHUSUS panel gelap, raycastTarget DINYALAKAN: hit-test permainan memang milik
            // ProtoInput, TAPI deret tombol kecepatan dari prefab adalah Button UGUI sungguhan
            // — tanpa perisai ini, klik "lanjut tutorial" yang jatuh di atasnya ikut mengganti
            // kecepatan permainan di balik dim (temuan verifikasi adversarial). Panel setelan
            // tidak terhalang: ia di-Instantiate belakangan, duduk di atas overlay ini.
            // Lubang sorotan tetap bolong untuk raycast juga — tidak ada tombol UGUI yang
            // pernah jadi sasaran sorot, jadi tidak ada yang bocor lewat lubangnya.
            for (int i = 0; i < 4; i++)
            {
                _dim[i] = MakeImage($"Dim_{i}", new Color(0f, 0f, 0f, 0.72f));
                _dim[i].raycastTarget = true;
            }

            for (int i = 0; i < 4; i++)
                _frame[i] = MakeImage($"Frame_{i}", new Color(1f, 0.84f, 0.4f, 0.9f));

            _plaque = MakeImage("Plaque", new Color(0.08f, 0.065f, 0.11f, 0.97f));
            var edge = _plaque.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(1f, 0.84f, 0.4f, 0.65f);
            edge.effectDistance = new Vector2(1.5f, 1.5f);

            _text = MakeText("Text", font, 23, new Color(0.93f, 0.9f, 0.82f),
                TextAlignmentOptions.Center);
            _hint = MakeText("Hint", font, 15, new Color(0.75f, 0.7f, 0.55f),
                TextAlignmentOptions.Center);

            _root.SetActive(false);
        }

        public void Show(Step[] steps)
        {
            if (steps == null || steps.Length == 0) return;

            _steps = steps;
            _at = 0;
            Active = true;
            _root.SetActive(true);
            Seat();
        }

        public void Advance()
        {
            if (!Active) return;

            _at++;
            if (_at >= _steps.Length)
            {
                Hide();
                return;
            }

            Seat();
        }

        public void Hide()
        {
            Active = false;
            _steps = null;
            _root.SetActive(false);
        }

        /// <summary>Sekali per frame selama aktif: geometri disegarkan (sasarannya bisa
        /// berpindah) dan bingkainya berdenyut supaya terbaca hidup, bukan macet.</summary>
        public void Draw()
        {
            if (!Active) return;

            Seat();

            float pulse = 0.62f + 0.38f * Mathf.Sin(Time.unscaledTime * 4.2f);
            for (int i = 0; i < 4; i++)
            {
                var c = _frame[i].color;
                c.a = 0.55f + 0.45f * pulse;
                _frame[i].color = c;
            }

            var hc = _hint.color;
            hc.a = 0.5f + 0.5f * pulse;
            _hint.color = hc;
        }

        void Seat()
        {
            var step = _steps[_at];
            float w = ScreenW;
            float h = ScreenH;

            var target = step.Area();

            // Sasaran dijepit ke layar: Func yang mengembalikan kotak aneh (layar diciutkan,
            // prefab hilang) tidak boleh membalik panel gelapnya jadi ukuran negatif.
            target.xMin = Mathf.Clamp(target.xMin, 0f, w);
            target.xMax = Mathf.Clamp(target.xMax, target.xMin, w);
            target.yMin = Mathf.Clamp(target.yMin, 0f, h);
            target.yMax = Mathf.Clamp(target.yMax, target.yMin, h);

            Put(_dim[0], 0f, 0f, w, target.yMin);                                // bawah
            Put(_dim[1], 0f, target.yMax, w, h - target.yMax);                   // atas
            Put(_dim[2], 0f, target.yMin, target.xMin, target.height);           // kiri
            Put(_dim[3], target.xMax, target.yMin, w - target.xMax, target.height); // kanan

            var f = Expand(target, new Vector4(FramePad, FramePad, FramePad, FramePad));
            Put(_frame[0], f.xMin, f.yMin, f.width, FrameThickness);                   // bawah
            Put(_frame[1], f.xMin, f.yMax - FrameThickness, f.width, FrameThickness); // atas
            Put(_frame[2], f.xMin, f.yMin, FrameThickness, f.height);                  // kiri
            Put(_frame[3], f.xMax - FrameThickness, f.yMin, FrameThickness, f.height); // kanan

            // Plakat di sisi layar yang TIDAK ditempati sasaran, supaya tidak pernah menutupi
            // benda yang sedang dijelaskannya sendiri.
            float px = Mathf.Clamp(target.center.x - PlaqueW * 0.5f, 24f, w - PlaqueW - 24f);
            float py = target.center.y >= h * 0.5f
                ? Mathf.Max(24f, f.yMin - 30f - PlaqueH)
                : Mathf.Min(h - PlaqueH - 24f, f.yMax + 30f);

            Put(_plaque, px, py, PlaqueW, PlaqueH);
            Put(_text, px + 18f, py + 26f, PlaqueW - 36f, PlaqueH - 34f);
            Put(_hint, px, py + 6f, PlaqueW, 20f);

            _text.text = Loc.T(_steps[_at].Key);
            _hint.text = Loc.F("tut.next", _at + 1, _steps.Length);
        }

        // ---------- perkakas bangun ----------

        Image MakeImage(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root.transform, false);

            var img = go.AddComponent<Image>();
            img.color = color;

            // Hit-test permainan ini milik ProtoInput, bukan GraphicRaycaster — raycast UGUI
            // dimatikan supaya panel setelan (satu-satunya pemakai EventSystem) tidak terhalang.
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            return img;
        }

        TextMeshProUGUI MakeText(string name, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root.transform, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;

            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            return text;
        }

        static void Put(Graphic g, float x, float y, float w, float h)
        {
            var rt = g.rectTransform;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
        }
    }
}
