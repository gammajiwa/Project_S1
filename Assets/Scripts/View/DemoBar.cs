using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Bilah tombol di bawah layar untuk MEMPERLIHATKAN game ke orang lain: ganti wajah arena
    /// (siang / senja / malam / tengah malam) dan rasa cuaca (cerah / berangin / hujan / badai)
    /// seketika, tanpa menunggu undian wave.
    ///
    /// Ada karena permintaan pemilik project menjelang demo ke klien. Cuaca dan siang-malam
    /// diundi deterministik per nomor wave — bagus untuk permainan, mustahil untuk presentasi:
    /// memperlihatkan hujan berarti memainkan belasan wave sampai hujannya kebetulan keluar.
    ///
    /// Ini menimpa TAMPILAN, bukan aturannya: wave berikutnya tetap mengundi seperti biasa.
    /// Kanvasnya sendiri, terpisah dari <c>GrimoireUI</c>, supaya bilah demo tidak pernah bisa
    /// merusak HUD yang sesungguhnya — dan supaya mencabutnya nanti cukup satu baris.
    /// </summary>
    public class DemoBar : MonoBehaviour
    {
        BiomeDresser _dresser;
        Weather _weather;
        Text _label;

        readonly List<Button> _faceButtons = new List<Button>();
        readonly List<Button> _moodButtons = new List<Button>();

        static readonly string[] FaceKeys =
            { "demo.face.day", "demo.face.night", "demo.face.dusk", "demo.face.midnight" };

        static string FaceName(int index) => Loc.T(FaceKeys[Mathf.Clamp(index, 0, FaceKeys.Length - 1)]);

        public void Init(BiomeDresser dresser, Weather weather, int faceCount)
        {
            _dresser = dresser;
            _weather = weather;

            Build(faceCount);
        }

        void Build(int faceCount)
        {
            var canvasGo = new GameObject("DemoBarCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Di ATAS HUD. Bilah yang tertimbun panel spell adalah bilah yang tidak bisa dipencet
            // justru saat sedang dipamerkan.
            canvas.sortingOrder = 500;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasGo.AddComponent<GraphicRaycaster>();

            // POJOK KANAN ATAS, di bawah tombol nama biome — bukan bilah selebar layar di bawah.
            // Yang pertama menutupi tas, panel spell, dan lantai tempat barang jatuh mendarat;
            // pemilik project menyebutnya "terlalu blocking", dan memang alat bantu tidak boleh
            // menghalangi hal yang sedang dipamerkan.
            var panel = new GameObject("Panel").AddComponent<RectTransform>();
            panel.SetParent(canvasGo.transform, false);
            panel.anchorMin = new Vector2(1f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);

            // y -120, bukan -84: tombol siang/malam milik HUD berakhir di y -110, dan di -84
            // bilah ini duduk persis di atasnya — dua label saling tindih dan dua-duanya
            // tidak terbaca. Angka ini mengekor GrimoireLayout (Margin 20 + 34 + 22 + 34).
            panel.anchoredPosition = new Vector2(-14f, -120f);
            panel.sizeDelta = new Vector2(258f, 92f);

            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.055f, 0.05f, 0.09f, 0.85f);

            var edge = panel.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.76f, 0.62f, 0.34f, 0.6f);
            edge.effectDistance = new Vector2(1f, 1f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 3f;
            layout.padding = new RectOffset(5, 5, 4, 4);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            _label = MakeLabel(panel, "DEMO");

            // Dua baris: wajah di atas, cuaca di bawah. Nama dipendekkan jadi 3-4 huruf —
            // di lebar 258 piksel, "TENGAH MALAM" tidak muat dan yang penting cuma bisa dibedakan.
            var faceRow = MakeRow(panel);
            for (int i = 0; i < faceCount && i < FaceNames.Length; i++)
            {
                int index = i;
                _faceButtons.Add(MakeButton(faceRow, Short(FaceName(i)), () =>
                {
                    _dresser.Show(index);

                    // Daftar cuacanya ikut berganti bersama wajahnya — tombolnya dibangun ulang
                    // dulu, baru pilihan lama dipasang kembali LEWAT NAMA.
                    SyncMoods();

                    int keep = IndexOfSelected();
                    if (keep >= 0) _weather.Force(keep);

                    Refresh();
                }));
            }

            _moodRow = MakeRow(panel);
            SyncMoods();

            Refresh();
        }

        RectTransform _moodRow;
        readonly List<string> _moodNames = new List<string>();

        /// <summary>
        /// Membangun ulang deretan tombol cuaca kalau daftar cuacanya berganti.
        ///
        /// Wajib, dan ini bug yang benar-benar terjadi: **tiap wajah arena punya daftar cuaca
        /// sendiri**. Tombol yang dibuat sekali dari daftar SIANG akan menunjuk indeks yang
        /// artinya berbeda begitu malam datang — menekan "BADAI" memanggil mood "Sunyi" yang
        /// tidak punya hujan sama sekali, dan yang terlihat pemilik project adalah "hujannya
        /// nggak jalan".
        /// </summary>
        void SyncMoods()
        {
            if (_weather == null || _moodRow == null) return;

            int count = _weather.MoodCount;

            bool same = count == _moodNames.Count;
            for (int i = 0; same && i < count; i++)
            {
                if (_moodNames[i] != _weather.MoodNameAt(i)) same = false;
            }

            if (same) return;

            for (int i = _moodRow.childCount - 1; i >= 0; i--) Destroy(_moodRow.GetChild(i).gameObject);

            _moodButtons.Clear();
            _moodNames.Clear();

            for (int i = 0; i < count; i++)
            {
                int index = i;
                string full = _weather.MoodNameAt(i);
                if (string.IsNullOrEmpty(full)) full = "C" + (i + 1);

                _moodNames.Add(full);

                _moodButtons.Add(MakeButton(_moodRow, Short(full), () =>
                {
                    // Yang diingat NAMANYA, bukan nomornya — supaya "Hujan" tetap hujan setelah
                    // pemain menekan MALAM, selama wajah malam juga punya hujan.
                    SelectedName = _weather.MoodNameAt(index);
                    _weather.Force(index);
                    Refresh();
                }));
            }
        }

        /// <summary>Nama cuaca yang sedang dipilih tangan, dipertahankan melewati ganti wajah.</summary>
        string SelectedName;

        /// <summary>Indeks cuaca yang namanya cocok dengan pilihan tangan, −1 kalau tidak ada.</summary>
        int IndexOfSelected()
        {
            if (_weather == null || string.IsNullOrEmpty(SelectedName)) return -1;

            for (int i = 0; i < _weather.MoodCount; i++)
            {
                if (_weather.MoodNameAt(i) == SelectedName) return i;
            }

            return -1;
        }

        /// <summary>Tiga huruf pertama, cukup untuk membedakan dan muat di tombol sempit.</summary>
        static string Short(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";

            // "TENGAH MALAM" -> "T.MLM": dua kata dipendekkan lewat inisial + potongan kata kedua,
            // karena tiga huruf pertamanya ("TEN") tidak memberi tahu apa pun.
            int space = name.IndexOf(' ');
            if (space > 0 && space + 1 < name.Length)
            {
                string tail = name.Substring(space + 1);
                return (name[0] + "." + tail.Substring(0, Mathf.Min(3, tail.Length))).ToUpperInvariant();
            }

            return name.Substring(0, Mathf.Min(4, name.Length)).ToUpperInvariant();
        }

        static RectTransform MakeRow(RectTransform parent)
        {
            var row = new GameObject("Baris").AddComponent<RectTransform>();
            row.SetParent(parent, false);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 26f;
            le.preferredHeight = 26f;

            return row;
        }

        void Refresh()
        {
            if (_label == null) return;

            string face = _dresser != null ? _dresser.CurrentName : "";
            string mood = _weather != null ? _weather.MoodName : "";

            _label.text = string.IsNullOrEmpty(mood) ? face : face + "  ·  " + mood;
        }

        void Update()
        {
            // Wajah dan cuaca berganti sendiri tiap wave baru diundi. Tanpa ini bilahnya
            // menampilkan nama basi, dan — lebih buruk — tombol cuaca peninggalan wajah lama
            // tetap terpasang sampai ada yang menekannya.
            if (Time.frameCount % 30 != 0) return;

            SyncMoods();
            Refresh();
        }

        Text MakeLabel(RectTransform parent, string text)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);

            var label = go.AddComponent<Text>();
            label.font = BarFont();
            label.fontSize = 13;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.93f, 0.72f);
            label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 18f;
            le.preferredHeight = 18f;

            return label;
        }

        Button MakeButton(RectTransform parent, string caption, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(caption);
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.11f, 0.1f, 0.16f, 0.95f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(onClick);

            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 44f;
            le.flexibleWidth = 1f;

            var textGo = new GameObject("Teks");
            textGo.transform.SetParent(go.transform, false);

            var text = textGo.AddComponent<Text>();
            text.font = BarFont();
            text.fontSize = 12;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.9f, 0.86f, 0.76f);
            text.text = caption;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return button;
        }

        /// <summary>Tema UI, disuntik composition root. Boleh null — jatuh ke font bawaan Unity.</summary>
        public UiTheme Theme;

        Font BarFont() =>
            Theme != null && Theme.UiFont != null
                ? Theme.UiFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
