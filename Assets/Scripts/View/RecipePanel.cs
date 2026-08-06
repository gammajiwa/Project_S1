using System.Collections.Generic;
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

        const float IconSize = 44f;
        const float RowHeight = 62f;
        const float ResultColumn = 88f;
        const float ArrowColumn = 26f;
        const float PartColumn = 78f;
        const float PadX = 12f;
        const float TitleHeight = 26f;
        const float FooterHeight = 20f;

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
        readonly Font _font;

        Image _bg;
        Text _title;
        Text _footer;

        readonly Image[] _resultIcon = new Image[MaxRows];
        readonly Text[] _resultLabel = new Text[MaxRows];
        readonly Text[] _arrow = new Text[MaxRows];
        readonly Image[] _partIcon = new Image[MaxRows * MaxParts];
        readonly Text[] _partLabel = new Text[MaxRows * MaxParts];

        readonly List<RecipeDefinition> _rows = new List<RecipeDefinition>(MaxRows);
        readonly Dictionary<PieceDefinition, int> _seen = new Dictionary<PieceDefinition, int>();

        public RecipePanel(Transform canvas, Font font, ContentDatabase db,
            System.Func<PieceDefinition, int> ownedCount)
        {
            _canvas = canvas;
            _font = font;
            _db = db;
            _ownedCount = ownedCount;

            Build();
            Hide();
        }

        // ---------- construction ----------

        void Build()
        {
            _bg = MakeImage("RecipeBg", new Vector2(PanelWidth, 200f), Backdrop);
            _title = MakeText("RecipeTitle", new Vector2(PanelWidth - PadX * 2f, TitleHeight), 14,
                TextAnchor.UpperLeft);
            _footer = MakeText("RecipeFooter", new Vector2(PanelWidth - PadX * 2f, FooterHeight), 11,
                TextAnchor.UpperLeft);

            for (int r = 0; r < MaxRows; r++)
            {
                _resultIcon[r] = MakeImage($"RecipeResult_{r}", new Vector2(IconSize, IconSize), Lit);
                _resultLabel[r] = MakeText($"RecipeResultLabel_{r}", new Vector2(ResultColumn, 26f), 11,
                    TextAnchor.UpperCenter);
                _arrow[r] = MakeText($"RecipeArrow_{r}", new Vector2(ArrowColumn, 24f), 16,
                    TextAnchor.UpperCenter);

                for (int p = 0; p < MaxParts; p++)
                {
                    int i = r * MaxParts + p;
                    _partIcon[i] = MakeImage($"RecipePart_{r}_{p}", new Vector2(IconSize, IconSize), Lit);
                    _partLabel[i] = MakeText($"RecipePartLabel_{r}_{p}", new Vector2(PartColumn, 26f), 11,
                        TextAnchor.UpperCenter);
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

        Text MakeText(string name, Vector2 size, int fontSize, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas, false);

            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = align;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            return text;
        }

        // ---------- showing ----------

        public void Hide()
        {
            _bg.enabled = false;
            _title.enabled = false;
            _footer.enabled = false;

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

        public void Show(PieceDefinition piece, Vector2 mouse)
        {
            if (piece == null)
            {
                Hide();
                return;
            }

            Collect(piece);

            float height = TitleHeight + _rows.Count * RowHeight + FooterHeight + 14f;
            if (_rows.Count == 0) height = TitleHeight + FooterHeight + 20f;

            // Pivot is the top-left corner, so `top` is where the panel starts and it grows down.
            float left = Mathf.Min(mouse.x + 18f, Mathf.Max(8f, Screen.width - PanelWidth - 8f));
            float top = Mathf.Max(mouse.y + 8f, height + 12f);
            var origin = new Vector2(left, top);

            _bg.enabled = true;
            _bg.rectTransform.sizeDelta = new Vector2(PanelWidth, height);
            _bg.rectTransform.anchoredPosition = origin;

            _title.enabled = true;
            _title.color = ResultText;
            _title.text = _rows.Count > 0
                ? "RESEP  -  " + piece.DisplayName
                : "RESEP  -  " + piece.DisplayName + "   (belum ada)";
            _title.rectTransform.anchoredPosition = origin + new Vector2(PadX, -6f);

            for (int r = 0; r < MaxRows; r++) LayoutRow(r, origin);

            _footer.enabled = true;
            _footer.color = DarkText;
            _footer.text = "taruh bahannya BERSENTUHAN di grimoire  -  garis biru = kurang, emas = jadi";
            _footer.rectTransform.anchoredPosition =
                origin + new Vector2(PadX, -(height - FooterHeight + 2f));
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

                _partLabel[i].enabled = true;
                _partLabel[i].text = part.DisplayName;
                _partLabel[i].color = owned ? LitText : DarkText;
                _partLabel[i].rectTransform.anchoredPosition =
                    origin + new Vector2(x, rowTop - IconSize - 6f);
            }

            Paint(_resultIcon[row], recipe.Result, complete);
            _resultIcon[row].rectTransform.anchoredPosition =
                origin + new Vector2(PadX + (ResultColumn - IconSize) * 0.5f, rowTop - 4f);

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
