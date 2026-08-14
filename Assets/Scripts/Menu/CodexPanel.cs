using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// The codex, now living in the main menu instead of mid-run. It reads the discovery log
    /// straight off disk every time it opens, so a run that just ended is already reflected.
    ///
    /// Isinya dipecah TIGA SEKSI — RUNE DASAR, SEGEL, SKILL — masing-masing dengan judul dan
    /// hitungan ketemunya sendiri, plus deretan tombol saring di atas daftar. Satu gulungan
    /// rata berisi 130-an kartu tidak terbaca sebagai koleksi; tiga rak berjudul terbaca.
    ///
    /// Seluruh perancah (judul seksi, grid per seksi, tombol saring) DIBANGUN SAAT RUNTIME,
    /// bukan ditanam di scene. Sengaja: scene menu tidak boleh diregenerate (lihat memory
    /// respect-user-editor-layout), dan perancah runtime bekerja di scene mana pun tanpa
    /// menyentuh satu aset pun.
    /// </summary>
    public class CodexPanel : MonoBehaviour
    {
        [SerializeField] ContentDatabase _database;
        [SerializeField] TextMeshProUGUI _counter;
        [SerializeField] TextMeshProUGUI _emptyHint;
        [SerializeField] RectTransform _content;

        [Tooltip("Anak yang dinonaktifkan, dipakai sebagai cetakan tiap slot.")]
        [SerializeField] CodexEntry _entryTemplate;

        [Tooltip("Opsional — kalau kosong, cetakan judul seksi dibangun sendiri saat runtime.")]
        [SerializeField] RectTransform _headerTemplate;

        [Tooltip("Opsional — kalau kosong, cetakan badan seksi dibangun sendiri saat runtime.")]
        [SerializeField] RectTransform _sectionTemplate;

        // Dibaca lewat fungsi, bukan disimpan sebagai larik siap pakai: larik static dibekukan
        // sekali seumur permainan, dan bahasa boleh berganti di tengah jalan.
        static readonly string[] SectionKeys = { "codex.section.rune", "codex.section.sigil", "codex.section.skill" };
        static readonly string[] FilterKeys = { "codex.filter.all", "codex.filter.rune", "codex.filter.sigil", "codex.filter.skill" };

        static string SectionTitle(int index) => Loc.T(SectionKeys[Mathf.Clamp(index, 0, SectionKeys.Length - 1)]);
        static string FilterLabel(int index) => Loc.T(FilterKeys[Mathf.Clamp(index, 0, FilterKeys.Length - 1)]);

        // Keluarga warna yang sama dengan HUD in-run dan menu: emas antik di atas indigo-hitam.
        static readonly Color InkGold = new Color(0.85f, 0.72f, 0.45f, 1f);
        static readonly Color InkBone = new Color(0.9f, 0.86f, 0.76f, 1f);
        static readonly Color InkMuted = new Color(0.55f, 0.5f, 0.43f, 1f);
        static readonly Color TileInk = new Color(0.055f, 0.05f, 0.09f, 0.92f);
        static readonly Color EdgeGold = new Color(0.76f, 0.62f, 0.34f, 0.8f);
        static readonly Color ActiveTextInk = new Color(0.12f, 0.09f, 0.05f, 1f);

        readonly List<PieceDefinition> _order = new List<PieceDefinition>();
        readonly List<CodexEntry> _entries = new List<CodexEntry>();
        readonly List<RectTransform> _headers = new List<RectTransform>();
        readonly List<RectTransform> _sections = new List<RectTransform>();
        readonly List<Image> _filterBg = new List<Image>();
        readonly List<TextMeshProUGUI> _filterText = new List<TextMeshProUGUI>();

        /// <summary>-1 = semua seksi; 0..2 = hanya seksi itu.</summary>
        int _filter = -1;

        bool _scaffoldReady;

        void OnEnable() => Refresh();

        /// <summary>Rak untuk sebuah piece: 0 rune dasar, 1 segel (pasif), 2 skill.</summary>
        static int SectionOf(PieceDefinition piece) =>
            piece.IsRune ? 0 : piece.IsPassive ? 1 : 2;

        public void Refresh()
        {
            if (_database == null || _content == null || _entryTemplate == null)
            {
                Debug.LogError("[CodexPanel] referensi belum lengkap di Inspector.", this);
                return;
            }

            EnsureScaffold();
            BuildOrder();

            var log = DiscoveryLog.Load();

            int used = 0;
            int cursor = 0;

            for (int s = 0; s < SectionKeys.Length; s++)
            {
                // _order sudah terurut per seksi, jadi tiap seksi adalah satu rentang utuh.
                int first = cursor;
                while (cursor < _order.Count && SectionOf(_order[cursor]) == s) cursor++;

                int total = cursor - first;
                bool shown = total > 0 && (_filter < 0 || _filter == s);

                var header = TemplateAt(_headers, _headerTemplate, s);
                var section = TemplateAt(_sections, _sectionTemplate, s);
                header.gameObject.SetActive(shown);
                section.gameObject.SetActive(shown);

                // Urutan tampil milik VerticalLayoutGroup = urutan sibling. Dipaku tiap
                // refresh supaya cetakan (yang juga anak _content) tidak pernah nyelip.
                header.SetSiblingIndex(s * 2);
                section.SetSiblingIndex(s * 2 + 1);

                if (!shown) continue;

                int found = 0;

                for (int i = first; i < cursor; i++)
                {
                    bool known = log.Has(_order[i].Id);
                    if (known) found++;
                    EntryAt(used++, section).Bind(_order[i], known);
                }

                SetHeader(header, SectionTitle(s), found, total);
            }

            // Spare entries stay alive but hidden — the list only ever grows when content is added.
            for (int i = used; i < _entries.Count; i++)
            {
                _entries[i].gameObject.SetActive(false);
            }

            if (_counter != null)
            {
                _counter.text = Loc.F("codex.counter", log.Count, _order.Count);
            }

            if (_emptyHint != null)
            {
                _emptyHint.gameObject.SetActive(log.Count == 0);
            }

            PaintFilter();
        }

        public void SetFilter(int section)
        {
            if (_filter == section) return;

            _filter = section;

            // Daftar yang baru disaring selalu dibaca dari atas.
            _content.anchoredPosition = Vector2.zero;
            Refresh();
        }

        void BuildOrder()
        {
            _order.Clear();

            for (int i = 0; i < _database.Pieces.Count; i++)
            {
                if (_database.Pieces[i] != null) _order.Add(_database.Pieces[i]);
            }

            // Stable order: section, then rarity, then name. Scroll position must not shuffle.
            _order.Sort((a, b) =>
            {
                int sa = SectionOf(a);
                int sb = SectionOf(b);
                if (sa != sb) return sa.CompareTo(sb);
                if (a.Stars != b.Stars) return a.Stars.CompareTo(b.Stars);
                return string.CompareOrdinal(a.DisplayName, b.DisplayName);
            });
        }

        // ------------------------------------------------------------------ perancah runtime

        /// <summary>
        /// Menyiapkan seluruh perancah sekali per hidup komponen: menukar grid rata milik
        /// scene lama dengan tumpukan vertikal, membuat cetakan judul & badan seksi,
        /// membesarkan kartu, dan memasang deretan tombol saring.
        /// </summary>
        void EnsureScaffold()
        {
            if (_scaffoldReady) return;
            _scaffoldReady = true;

            if (_headerTemplate == null || _sectionTemplate == null)
            {
                // Grid rata lama diganti tumpukan vertikal; tinggi tiap anak dibaca dari
                // preferredHeight-nya (judul = LayoutElement, seksi = GridLayoutGroup).
                var flat = _content.GetComponent<GridLayoutGroup>();
                if (flat != null) DestroyImmediate(flat);

                if (_content.GetComponent<VerticalLayoutGroup>() == null)
                {
                    var stack = _content.gameObject.AddComponent<VerticalLayoutGroup>();
                    stack.spacing = 10f;
                    stack.padding = new RectOffset(8, 8, 4, 16);
                    stack.childAlignment = TextAnchor.UpperLeft;
                    stack.childControlWidth = true;
                    stack.childControlHeight = true;
                    stack.childForceExpandWidth = true;
                    stack.childForceExpandHeight = false;
                }

                if (_headerTemplate == null) _headerTemplate = BuildHeaderTemplate();
                if (_sectionTemplate == null) _sectionTemplate = BuildSectionTemplate();

                EnlargeEntryTemplate();
            }

            BuildFilterRow();
        }

        RectTransform BuildHeaderTemplate()
        {
            var header = NewRect("HeaderTemplate", _content);

            var height = header.gameObject.AddComponent<LayoutElement>();
            height.minHeight = 48f;
            height.preferredHeight = 48f;

            var title = NewLabel(header, "Title", 24, InkGold, TextAlignmentOptions.BottomLeft);
            title.rectTransform.anchorMin = new Vector2(0f, 0f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(6f, 9f);
            title.rectTransform.offsetMax = new Vector2(-280f, 0f);

            var count = NewLabel(header, "Count", 14, InkMuted, TextAlignmentOptions.BottomRight);
            count.rectTransform.anchorMin = new Vector2(1f, 0f);
            count.rectTransform.anchorMax = new Vector2(1f, 1f);
            count.rectTransform.offsetMin = new Vector2(-266f, 11f);
            count.rectTransform.offsetMax = new Vector2(-6f, 0f);

            var rule = NewRect("Rule", header).gameObject.AddComponent<Image>();
            rule.raycastTarget = false;
            rule.color = EdgeGold;
            rule.rectTransform.anchorMin = new Vector2(0f, 0f);
            rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.sizeDelta = new Vector2(0f, 1f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, 2f);

            header.gameObject.SetActive(false);
            return header;
        }

        RectTransform BuildSectionTemplate()
        {
            var section = NewRect("SectionTemplate", _content);

            var grid = section.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(305f, 132f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(0, 0, 0, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            section.gameObject.SetActive(false);
            return section;
        }

        /// <summary>
        /// Kartu cetakan milik scene lama dirancang untuk sel 250x118 — di sel 305x132 siluet
        /// 52 piksel dan nama selebar 156 kelihatan tenggelam. Yang digeser cuma RectTransform
        /// anak-anak cetakannya, saat runtime; scene di disk tidak berubah.
        /// </summary>
        void EnlargeEntryTemplate()
        {
            var root = _entryTemplate.transform;

            var shape = root.Find("Shape") as RectTransform;
            if (shape != null)
            {
                shape.sizeDelta = new Vector2(64f, 64f);
                shape.anchoredPosition = new Vector2(16f, -16f);

                var grid = shape.GetComponent<GridLayoutGroup>();
                if (grid != null) grid.cellSize = new Vector2(19f, 19f);
            }

            var label = root.Find("Name") as RectTransform;
            if (label != null)
            {
                label.anchoredPosition = new Vector2(94f, -18f);
                label.sizeDelta = new Vector2(196f, 60f);
            }

            var meta = root.Find("Meta") as RectTransform;
            if (meta != null)
            {
                meta.anchoredPosition = new Vector2(16f, 12f);
                meta.sizeDelta = new Vector2(273f, 22f);
            }
        }

        /// <summary>
        /// Deretan tombol saring di atas daftar: SEMUA / RUNE / SEGEL / SKILL. Ruangnya
        /// diambil dengan memendekkan kotak scroll dari atas — panel dan heading tidak digeser.
        /// </summary>
        void BuildFilterRow()
        {
            if (_filterBg.Count > 0) return;

            // _content -> Viewport -> Scroll; kotak scroll itulah yang dipendekkan.
            var scroll = _content.parent != null ? _content.parent.parent as RectTransform : null;
            if (scroll == null) return;

            const float RowHeight = 40f;
            const float Gap = 14f;

            scroll.sizeDelta = new Vector2(scroll.sizeDelta.x, scroll.sizeDelta.y - RowHeight - Gap);
            scroll.anchoredPosition += new Vector2(0f, -(RowHeight + Gap) * 0.5f);

            var row = NewRect("FilterRow", scroll.parent as RectTransform);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0f, 1f);

            // Menempel ke tepi atas kotak scroll yang sudah dipendekkan, rata kiri dengannya.
            row.anchoredPosition = new Vector2(
                scroll.anchoredPosition.x - scroll.sizeDelta.x * 0.5f,
                scroll.anchoredPosition.y + scroll.sizeDelta.y * 0.5f + Gap + RowHeight);
            row.sizeDelta = new Vector2(scroll.sizeDelta.x, RowHeight);

            float x = 0f;

            for (int i = 0; i < FilterKeys.Length; i++)
            {
                float width = FilterLabel(i).Length > 5 ? 130f : 116f;

                var tile = NewRect("Filter_" + FilterLabel(i), row);
                tile.anchorMin = tile.anchorMax = new Vector2(0f, 0f);
                tile.pivot = new Vector2(0f, 0f);
                tile.anchoredPosition = new Vector2(x, 0f);
                tile.sizeDelta = new Vector2(width, RowHeight);
                x += width + 10f;

                var bg = tile.gameObject.AddComponent<Image>();
                bg.color = TileInk;

                var edge = tile.gameObject.AddComponent<Outline>();
                edge.effectColor = EdgeGold;
                edge.effectDistance = new Vector2(1f, 1f);

                var button = tile.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                int section = i - 1;
                button.onClick.AddListener(() => SetFilter(section));

                var label = NewLabel(tile, "Label", 15, InkBone, TextAlignmentOptions.Center);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = Vector2.zero;
                label.rectTransform.offsetMax = Vector2.zero;
                label.text = FilterLabel(i);

                _filterBg.Add(bg);
                _filterText.Add(label);
            }
        }

        void PaintFilter()
        {
            for (int i = 0; i < _filterBg.Count; i++)
            {
                bool on = _filter == i - 1;
                _filterBg[i].color = on ? InkGold : TileInk;
                _filterText[i].color = on ? ActiveTextInk : InkBone;
            }
        }

        // ------------------------------------------------------------------ perkakas kecil

        static RectTransform NewRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        TextMeshProUGUI NewLabel(RectTransform parent, string name, float size, Color color,
            TextAlignmentOptions align)
        {
            var tmp = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;

            // Font diwarisi dari counter supaya seluruh halaman satu huruf; TMP jatuh ke font
            // bawaannya sendiri kalau counter tidak ada.
            if (_counter != null && _counter.font != null) tmp.font = _counter.font;

            return tmp;
        }

        static RectTransform TemplateAt(List<RectTransform> pool, RectTransform template, int index)
        {
            while (pool.Count <= index)
            {
                var clone = Instantiate(template, template.parent);
                clone.name = template.name.Replace("Template", "_" + pool.Count);
                pool.Add(clone);
            }

            return pool[index];
        }

        static void SetHeader(RectTransform header, string title, int found, int total)
        {
            var texts = header.GetComponentsInChildren<TextMeshProUGUI>(true);

            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].name == "Title") texts[i].text = title;
                else if (texts[i].name == "Count") texts[i].text = Loc.F("codex.counter", found, total);
            }
        }

        CodexEntry EntryAt(int index, Transform parent)
        {
            while (_entries.Count <= index)
            {
                var entry = Instantiate(_entryTemplate, parent);
                entry.name = "CodexEntry_" + _entries.Count;
                _entries.Add(entry);
            }

            var slot = _entries[index];
            if (slot.transform.parent != parent) slot.transform.SetParent(parent, false);

            // Grid mengisi menurut urutan sibling; kartu dari pool boleh bekas seksi lain.
            slot.transform.SetAsLastSibling();
            return slot;
        }
    }
}
