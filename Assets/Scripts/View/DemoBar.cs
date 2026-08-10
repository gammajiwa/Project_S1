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

        static readonly string[] FaceNames = { "SIANG", "MALAM", "SENJA", "TENGAH MALAM" };

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

            var row = new GameObject("Baris").AddComponent<RectTransform>();
            row.SetParent(canvasGo.transform, false);
            row.anchorMin = new Vector2(0.5f, 0f);
            row.anchorMax = new Vector2(0.5f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, 14f);
            row.sizeDelta = new Vector2(1200f, 76f);

            var bg = row.gameObject.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0.72f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            _label = MakeLabel(row, "DEMO");

            for (int i = 0; i < faceCount && i < FaceNames.Length; i++)
            {
                int index = i;
                _faceButtons.Add(MakeButton(row, FaceNames[i], () =>
                {
                    _dresser.Show(index);

                    // Cuaca disetel ULANG setelah wajah berganti: Apply() membangun Weather dari
                    // nol, jadi rasa yang sedang dipilih ikut terhapus kalau tidak dipasang lagi.
                    _weather.Force(SelectedMood);
                    Refresh();
                }));
            }

            Separator(row);

            int moods = _weather != null ? _weather.MoodCount : 0;
            for (int i = 0; i < moods; i++)
            {
                int index = i;
                string caption = _weather.MoodNameAt(i);
                if (string.IsNullOrEmpty(caption)) caption = "CUACA " + (i + 1);

                _moodButtons.Add(MakeButton(row, caption.ToUpperInvariant(), () =>
                {
                    SelectedMood = index;
                    _weather.Force(index);
                    Refresh();
                }));
            }

            Refresh();
        }

        /// <summary>Rasa cuaca yang sedang dipilih tangan, dipertahankan melewati ganti wajah.</summary>
        int SelectedMood;

        void Refresh()
        {
            if (_label == null) return;

            string face = _dresser != null ? _dresser.CurrentName : "";
            string mood = _weather != null ? _weather.MoodName : "";

            _label.text = string.IsNullOrEmpty(mood) ? face : face + "  ·  " + mood;
        }

        void Update()
        {
            // Nama cuaca berubah sendiri tiap wave baru diundi; tanpa ini bilahnya menampilkan
            // nama yang sudah basi sampai ada tombol yang dipencet.
            if (Time.frameCount % 30 == 0) Refresh();
        }

        static Text MakeLabel(RectTransform parent, string text)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);

            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color(1f, 0.93f, 0.72f);
            label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 300f;
            le.preferredWidth = 300f;

            return label;
        }

        static void Separator(RectTransform parent)
        {
            var go = new GameObject("Pemisah");
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.18f);

            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 2f;
            le.preferredWidth = 2f;
        }

        static Button MakeButton(RectTransform parent, string caption, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(caption);
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.16f, 0.18f, 0.26f, 0.95f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(onClick);

            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 108f;
            le.preferredWidth = 108f;

            var textGo = new GameObject("Teks");
            textGo.transform.SetParent(go.transform, false);

            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 15;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.92f, 0.94f, 1f);
            text.text = caption;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return button;
        }
    }
}
