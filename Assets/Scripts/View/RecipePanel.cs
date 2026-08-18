using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// The ALT card, as a picture instead of a paragraph.
    ///
    /// Result on the left, formula on the right, one row per recipe. Every ingredient you already
    /// own anywhere — board, bag, floor, cursor — is lit; the ones you are missing are dark. That is
    /// the whole question the card exists to answer ("what am I short of?"), and a wall of
    /// <c>[v]</c> and <c>[ ]</c> ticks answered it slowly.
    ///
    /// Duplicates count separately: a recipe asking for two Fireballs is only satisfied by two.
    /// </summary>
    public class RecipePanel
    {
        const int MaxRows = 5;
        const int MaxParts = 3;

        // Dinaikkan TIGA kali atas permintaan pemilik project (ikon 44 -> 54 -> 64 -> 72;
        // huruf 11 -> 13 -> 15 -> 18): kartu ini dibaca sambil ALT ditahan untuk
        // merencanakan farming, bukan dilirik — ukurannya harus ukuran baca.
        const float IconSize = 72f;
        const float RowHeight = 104f;
        const float ResultColumn = 136f;
        const float ArrowColumn = 40f;
        const float PartColumn = 124f;
        const float PadX = 18f;
        const float TitleHeight = 44f;

        const float PanelWidth = PadX * 2f + ResultColumn + ArrowColumn + MaxParts * PartColumn;

        static readonly Color Backdrop = new Color(0.06f, 0.06f, 0.09f, 0.97f);
        static readonly Color Lit = Color.white;
        static readonly Color Dark = new Color(0.28f, 0.28f, 0.34f, 1f);
        static readonly Color LitText = new Color(0.92f, 0.95f, 1f);
        static readonly Color DarkText = new Color(0.48f, 0.48f, 0.56f);
        static readonly Color ResultText = new Color(1f, 0.88f, 0.5f);

        readonly ContentDatabase _db;
        readonly System.Func<PieceDefinition, int> _ownedCount;
        readonly Transform _canvas;
        readonly TMP_FontAsset _font;
        readonly GameObject _cardPrefab;

        /// <summary>Piece yang kartunya sedang terpampang — kunci patokan (lihat Show).</summary>
        PieceDefinition _shownFor;

        /// <summary>Kartu sedang tampil. Dipakai gerbang patokan ALT di GrimoireUI.</summary>
        public bool Visible => _bg != null && _bg.enabled;

        Image _bg;
        TextMeshProUGUI _title;

        readonly Image[] _resultIcon = new Image[MaxRows];
        readonly TextMeshProUGUI[] _resultLabel = new TextMeshProUGUI[MaxRows];
        readonly TextMeshProUGUI[] _arrow = new TextMeshProUGUI[MaxRows];
        readonly Image[] _partIcon = new Image[MaxRows * MaxParts];
        readonly TextMeshProUGUI[] _partLabel = new TextMeshProUGUI[MaxRows * MaxParts];

        readonly List<RecipeDefinition> _rows = new List<RecipeDefinition>(MaxRows);
        readonly Dictionary<PieceDefinition, int> _seen = new Dictionary<PieceDefinition, int>();

        // Kotak hover per ikon+label (satuan kanvas, y dari bawah): kartu yang dipatok bisa
        // ditanya balik — hover ikonnya menjawab "ini item apaan" lewat kartu keterangan.
        const float InspectH = IconSize + 44f;
        readonly Rect[] _resultRect = new Rect[MaxRows];
        readonly Rect[] _partRect = new Rect[MaxRows * MaxParts];

        /// <summary>
        /// Piece milik ikon kartu yang sedang ditunjuk mouse, null kalau tidak ada.
        /// Baris di luar resep yang tampil tidak pernah menjawab.
        /// </summary>
        public PieceDefinition HoverPiece(Vector2 mouse)
        {
            if (!Visible) return null;

            for (int r = 0; r < _rows.Count && r < MaxRows; r++)
            {
                if (_resultRect[r].Contains(mouse)) return _rows[r].Result;

                var parts = _rows[r].Ingredients;
                int count = parts != null ? Mathf.Min(parts.Length, MaxParts) : 0;

                for (int p = 0; p < count; p++)
                {
                    if (parts[p] != null && _partRect[r * MaxParts + p].Contains(mouse))
                        return parts[p];
                }
            }

            return null;
        }

        public RecipePanel(Transform canvas, TMP_FontAsset font, ContentDatabase db,
            System.Func<PieceDefinition, int> ownedCount, GameObject cardPrefab = null)
        {
            _canvas = canvas;
            _font = font;
            _db = db;
            _ownedCount = ownedCount;
            _cardPrefab = cardPrefab;

            Build();
            Hide();
        }

        // ---------- construction ----------

        /// <summary>
        /// Badan kartu: dari PREFAB kalau tema membawa satu — sprite, tint, dan bahan panel
        /// milik tangan user. Ukuran dan posisi TETAP ditulis kode tiap kali tampil, karena
        /// tinggi kartu mengikuti jumlah baris resep dan posisinya mengikuti mouse saat dibuka.
        /// </summary>
        Image MakeBackdrop()
        {
            if (_cardPrefab != null)
            {
                var go = UnityEngine.Object.Instantiate(_cardPrefab, _canvas, false);
                go.name = "RecipeCard";

                var img = go.GetComponent<Image>();
                if (img != null)
                {
                    // Anchor/pivot diseragamkan supaya angka posisi kode berarti sama
                    // apa pun yang disetel di prefab — pola yang sama dengan kartu hover.
                    var rt = img.rectTransform;
                    rt.anchorMin = rt.anchorMax = Vector2.zero;
                    rt.pivot = new Vector2(0f, 1f);
                    rt.sizeDelta = new Vector2(PanelWidth, 200f);
                    return img;
                }

                UnityEngine.Object.Destroy(go);
                Debug.LogWarning("[RecipePanel] RecipeCardPrefab tidak punya Image di root — " +
                                 "kotak gambar-kode dipakai.");
            }

            return MakeImage("RecipeBg", new Vector2(PanelWidth, 200f), Backdrop);
        }

        void Build()
        {
            _bg = MakeBackdrop();
            // Footer petunjuk DIBUANG atas permintaan pemilik project ("di bawah ada text
            // gak jelas") — baris bahan yang menyala/padam sudah menceritakan aturannya.
            _title = MakeText("RecipeTitle", new Vector2(PanelWidth - PadX * 2f, TitleHeight), 24,
                TextAlignmentOptions.TopLeft);

            for (int r = 0; r < MaxRows; r++)
            {
                _resultIcon[r] = MakeImage($"RecipeResult_{r}", new Vector2(IconSize, IconSize), Lit);
                _resultLabel[r] = MakeText($"RecipeResultLabel_{r}", new Vector2(ResultColumn, 44f), 18,
                    TextAlignmentOptions.Top);
                _arrow[r] = MakeText($"RecipeArrow_{r}", new Vector2(ArrowColumn, 40f), 28,
                    TextAlignmentOptions.Top);

                for (int p = 0; p < MaxParts; p++)
                {
                    int i = r * MaxParts + p;
                    _partIcon[i] = MakeImage($"RecipePart_{r}_{p}", new Vector2(IconSize, IconSize), Lit);
                    _partLabel[i] = MakeText($"RecipePartLabel_{r}_{p}", new Vector2(PartColumn, 44f), 18,
                        TextAlignmentOptions.Top);
                }
            }
        }

        Image MakeImage(string name, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas, false);

            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            img.preserveAspect = true;

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            return img;
        }

        TextMeshProUGUI MakeText(string name, Vector2 size, int fontSize, TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) text.font = _font;
            text.fontSize = fontSize;
            text.alignment = align;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;

            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            return text;
        }

        // ---------- showing ----------

        public void Hide()
        {
            _shownFor = null;
            _bg.enabled = false;
            _title.enabled = false;

            for (int r = 0; r < MaxRows; r++)
            {
                _resultIcon[r].enabled = false;
                _resultLabel[r].enabled = false;
                _arrow[r].enabled = false;
            }

            for (int i = 0; i < _partIcon.Length; i++)
            {
                _partIcon[i].enabled = false;
                _partLabel[i].enabled = false;
            }
        }

        /// <summary>
        /// Menampilkan kartu resep sebuah piece.
        /// </summary>
        /// <returns>
        /// <b>False kalau piece ini tidak punya resep sama sekali</b> — dan kartunya TIDAK
        /// ditampilkan. Pemanggil memakai ini untuk jatuh kembali ke kartu keterangan biasa.
        ///
        /// Dulu kartunya tetap muncul dengan tulisan "(belum ada)", dan itu yang dilaporkan pemilik
        /// project sebagai "pencet ALT di rune, evo-nya gak keluar": kartu kosong terbaca sebagai
        /// fitur yang rusak, bukan sebagai jawaban. Enam rune dasar (Chrono, Ember, Frost, Plain,
        /// Spark, Void) memang tidak punya resep, dan NOL resep memakai rune sebagai bahan — jadi
        /// untuk mereka kartu ini tidak punya apa pun untuk dikatakan.
        /// </returns>
        public bool Show(PieceDefinition piece, Vector2 mouse)
        {
            if (piece == null)
            {
                Hide();
                return false;
            }

            // DIPATOK: selama kartu masih menampilkan piece yang sama, jangan ditata ulang —
            // kartu yang mengejar mouse tidak bisa dibaca, dan membaca ("bahan apa yang
            // kurang?") adalah satu-satunya tugas kartu ini. Pindah piece = tata ulang
            // di posisi mouse yang baru.
            if (piece == _shownFor && Visible) return true;
            _shownFor = piece;

            Collect(piece);

            // Tidak punya resep sama sekali = tidak ada yang bisa dikatakan kartu ini. Ia tidak
            // ditampilkan, dan pemanggil kembali ke kartu keterangan biasa.
            if (_rows.Count == 0)
            {
                Hide();
                return false;
            }

            float height = TitleHeight + _rows.Count * RowHeight + 18f;

            // Pivot is the top-left corner, so `top` is where the panel starts and it grows down.
            // Satuan kanvas, bukan piksel mentah — mouse yang diterima panel ini juga sudah
            // dibagi skala kanvas oleh pemanggilnya.
            float left = Mathf.Min(mouse.x + 18f, Mathf.Max(8f, GrimoireLayout.ScreenW - PanelWidth - 8f));
            float top = Mathf.Max(mouse.y + 8f, height + 12f);
            var origin = new Vector2(left, top);

            _bg.enabled = true;
            _bg.rectTransform.sizeDelta = new Vector2(PanelWidth, height);
            _bg.rectTransform.anchoredPosition = origin;

            _title.enabled = true;
            _title.color = ResultText;

            // Lewat Loc, bukan string mentah: "RESEP" hardcoded adalah persis jenis teks yang
            // dilaporkan "gak ke-local". Cabang "(belum ada)" sudah tidak mungkin sampai sini —
            // _rows kosong sudah pulang lebih awal di atas.
            _title.text = Loc.T("hud.origin.recipe") + "  -  " + piece.DisplayName;
            _title.rectTransform.anchoredPosition = origin + new Vector2(PadX, -6f);

            for (int r = 0; r < MaxRows; r++) LayoutRow(r, origin);
            return true;
        }

        /// <summary>
        /// Recipes that BUILD this piece come first — "how do I make this" is the question you ask
        /// while holding something you cannot use yet. Recipes that consume it follow.
        /// </summary>
        void Collect(PieceDefinition piece)
        {
            _rows.Clear();

            for (int i = 0; i < _db.Recipes.Count && _rows.Count < MaxRows; i++)
            {
                var recipe = _db.Recipes[i];
                if (recipe != null && recipe.Result == piece) _rows.Add(recipe);
            }

            for (int i = 0; i < _db.Recipes.Count && _rows.Count < MaxRows; i++)
            {
                var recipe = _db.Recipes[i];
                if (recipe == null || recipe.Result == null || _rows.Contains(recipe)) continue;
                if (Uses(recipe, piece)) _rows.Add(recipe);
            }
        }

        static bool Uses(RecipeDefinition recipe, PieceDefinition piece)
        {
            if (recipe.Ingredients == null) return false;

            for (int i = 0; i < recipe.Ingredients.Length; i++)
            {
                if (recipe.Ingredients[i] == piece) return true;
            }

            return false;
        }

        void LayoutRow(int row, Vector2 origin)
        {
            bool used = row < _rows.Count;

            _resultIcon[row].enabled = used;
            _resultLabel[row].enabled = used;
            _arrow[row].enabled = used;

            for (int p = 0; p < MaxParts; p++)
            {
                int i = row * MaxParts + p;
                _partIcon[i].enabled = false;
                _partLabel[i].enabled = false;
            }

            if (!used) return;

            var recipe = _rows[row];
            float rowTop = -(TitleHeight + row * RowHeight);

            // Ingredients first: whether the result is reachable depends on all of them.
            _seen.Clear();
            bool complete = true;
            int count = Mathf.Min(recipe.Ingredients.Length, MaxParts);

            for (int p = 0; p < count; p++)
            {
                var part = recipe.Ingredients[p];
                if (part == null) continue;

                int already;
                _seen.TryGetValue(part, out already);
                _seen[part] = already + 1;

                bool owned = _ownedCount(part) >= already + 1;
                if (!owned) complete = false;

                int i = row * MaxParts + p;
                float x = PadX + ResultColumn + ArrowColumn + p * PartColumn;

                Paint(_partIcon[i], part, owned);
                _partIcon[i].rectTransform.anchoredPosition =
                    origin + new Vector2(x + (PartColumn - IconSize) * 0.5f, rowTop - 4f);
                _partRect[i] = new Rect(origin.x + x, origin.y + rowTop - 4f - InspectH,
                    PartColumn, InspectH);

                _partLabel[i].enabled = true;
                _partLabel[i].text = part.DisplayName;
                _partLabel[i].color = owned ? LitText : DarkText;
                _partLabel[i].rectTransform.anchoredPosition =
                    origin + new Vector2(x, rowTop - IconSize - 6f);
            }

            Paint(_resultIcon[row], recipe.Result, complete);
            _resultIcon[row].rectTransform.anchoredPosition =
                origin + new Vector2(PadX + (ResultColumn - IconSize) * 0.5f, rowTop - 4f);
            _resultRect[row] = new Rect(origin.x + PadX, origin.y + rowTop - 4f - InspectH,
                ResultColumn, InspectH);

            _resultLabel[row].text = recipe.Result.DisplayName + "\n" +
                                     Shapes.StarText(recipe.Result.Stars);
            _resultLabel[row].color = complete ? ResultText : DarkText;
            _resultLabel[row].rectTransform.anchoredPosition =
                origin + new Vector2(PadX, rowTop - IconSize - 6f);

            _arrow[row].text = "=";
            _arrow[row].color = complete ? ResultText : DarkText;
            _arrow[row].rectTransform.anchoredPosition =
                origin + new Vector2(PadX + ResultColumn, rowTop - 18f);
        }

        /// <summary>
        /// Lit means "you have it". With a sprite the tint multiplies, so white shows the art as
        /// drawn and grey drains it; without one the piece colour stands in for the missing art.
        /// </summary>
        static void Paint(Image target, PieceDefinition piece, bool owned)
        {
            target.enabled = true;
            target.sprite = piece.Icon;

            if (piece.Icon != null) target.color = owned ? Lit : Dark;
            else target.color = owned ? piece.Color : Dark;
        }
    }
}
