using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// One codex cell. Undiscovered pieces still show their footprint as a silhouette — the player
    /// should be able to see that something is missing without learning what it is.
    /// </summary>
    public class CodexEntry : MonoBehaviour
    {
        /// <summary>Footprints never exceed 3x3, so the silhouette grid is a fixed 3x3.</summary>
        public const int ShapeGrid = 3;

        [SerializeField] Image _background;
        [SerializeField] TextMeshProUGUI _name;
        [SerializeField] TextMeshProUGUI _meta;

        [Tooltip("9 kotak, urutan kiri-atas ke kanan-bawah.")]
        [SerializeField] Image[] _shapeCells;

        [Header("Warna")]
        [SerializeField] Color _knownFill = new Color(0.098f, 0.098f, 0.129f, 0.95f);
        [SerializeField] Color _unknownFill = new Color(0.063f, 0.063f, 0.078f, 0.95f);
        [SerializeField] Color _knownText = Color.white;
        [SerializeField] Color _unknownText = new Color(0.45f, 0.45f, 0.5f, 1f);
        [SerializeField] Color _unknownCell = new Color(0.22f, 0.22f, 0.27f, 1f);

        RuneTilePool _tiles;

        /// <summary>
        /// Ikon ASET piece di atas siluet — dibuat sekali saat runtime, sejajar kotak siluet.
        /// Sengaja BUKAN anak kotak siluet: GridLayoutGroup di sana menata semua anaknya
        /// sebagai sel 3x3, dan ikon yang dititipkan ke situ ikut dijejalkan ke grid.
        /// </summary>
        Image _icon;

        Image IconImage()
        {
            if (_icon != null) return _icon;
            if (_shapeCells == null || _shapeCells.Length == 0 || _shapeCells[0] == null) return null;

            var box = _shapeCells[0].transform.parent as RectTransform;
            if (box == null) return null;

            var go = new GameObject("Icon", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(box.parent, false);
            rt.SetSiblingIndex(box.GetSiblingIndex() + 1);
            rt.anchorMin = box.anchorMin;
            rt.anchorMax = box.anchorMax;
            rt.pivot = box.pivot;
            rt.anchoredPosition = box.anchoredPosition;
            rt.sizeDelta = box.sizeDelta;

            _icon = go.AddComponent<Image>();
            _icon.raycastTarget = false;
            _icon.preserveAspect = true;
            return _icon;
        }

        public void Bind(PieceDefinition piece, bool known)
        {
            if (piece == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (_background != null) _background.color = known ? _knownFill : _unknownFill;

            if (_name != null)
            {
                _name.text = known ? piece.DisplayName : "? ? ?";
                _name.color = known ? _knownText : _unknownText;
            }

            if (_meta != null)
            {
                // Rarity is withheld until found: the star count alone would give the piece away.
                _meta.text = known ? Shapes.StarText(piece.Stars) + "   " + KindLabel(piece) : "";
                _meta.color = _unknownText;
            }

            // Ikon ASETNYA yang bicara di kartu; siluet turun jadi bayangan tapak redup di
            // belakangnya. Rune tetap bahasa tile (DrawTiles), dan yang belum ditemukan tetap
            // siluet buta — kontrak codex tidak berubah, cuma kotak warna-warni yang pensiun.
            bool tiledRune = RuneTiles.IsRuneGlyph(piece.Icon);
            var icon = IconImage();
            bool iconShown = icon != null && known && !tiledRune && piece.Icon != null;

            if (icon != null)
            {
                icon.enabled = iconShown;
                if (iconShown) icon.sprite = piece.Icon;
            }

            // Kartu ber-ikon dibersihkan TOTAL dari siluet — permintaan pemilik project: di
            // codex, ikonnya saja yang bicara. Siluet tinggal untuk yang belum ditemukan
            // (kontrak "ada yang belum kamu punya") dan piece yang belum punya ikon.
            if (iconShown && _shapeCells != null)
            {
                for (int i = 0; i < _shapeCells.Length; i++)
                {
                    if (_shapeCells[i] != null) _shapeCells[i].enabled = false;
                }
            }
            else
            {
                DrawShape(piece, known ? piece.Color : _unknownCell);
            }

            DrawTiles(piece, known);
        }

        /// <summary>
        /// Tile rune di atas siluet bentuknya — <b>satu tile per petak</b>, sama persis dengan
        /// papan in-run. Hanya untuk rune yang SUDAH ditemukan; yang belum tetap siluet buta,
        /// sesuai kontrak codex.
        ///
        /// Dulu di sini ada SATU gambar yang dibentangkan menutupi seluruh kotak 3x3, dan itu
        /// membuat entri codex berbohong dua kali: bentuknya hilang di balik gambar, dan berapa
        /// petak yang dimakan rune itu di papan jadi tidak terbaca sama sekali.
        /// </summary>
        void DrawTiles(PieceDefinition piece, bool known)
        {
            if (_shapeCells == null || _shapeCells.Length == 0 || _shapeCells[0] == null) return;

            var parent = _shapeCells[0].transform.parent;
            if (_tiles == null) _tiles = new RuneTilePool(parent);

            _tiles.Begin();

            if (known && RuneTiles.IsRuneGlyph(piece.Icon))
            {
                // Kotak siluetnya ditata GridLayoutGroup, dan letak tiap sel baru benar SETELAH
                // layout dihitung — sedangkan entri ini diisi saat panel dibuka, sebelum itu.
                // Tanpa baris ini tile mendarat di posisi sel dari entri SEBELUMNYA.
                var box = parent as RectTransform;
                if (box != null) LayoutRebuilder.ForceRebuildLayoutImmediate(box);

                // Celahnya dibaca dari layout yang sedang berlaku, bukan ditulis sebagai angka:
                // kotak siluet ini ditata GridLayoutGroup, dan menyalin ukurannya ke sini berarti
                // menaruh angka yang akan diam-diam salah begitu petaknya diperbesar di scene.
                float bleed = 0f;
                var grid = parent.GetComponent<GridLayoutGroup>();
                if (grid != null && grid.cellSize.x > 0.001f) bleed = grid.spacing.x / grid.cellSize.x;

                // Dinormalkan ke titik nol, karena bentuk gambaran tangan boleh disimpan
                // dengan petak yang tidak menempel di sumbu.
                var cells = Shapes.Rotate(piece.Cells, 0);

                for (int i = 0; i < cells.Length; i++)
                {
                    int index = IndexOf(cells[i]);
                    if (index < 0) continue;

                    // Siluetnya dimatikan di petak yang ditutup tile: tile membawa latarnya
                    // sendiri, dan yang tersisa di belakang cuma bocor lewat tepi transparannya.
                    _shapeCells[index].enabled = false;

                    var tile = _tiles.Take();
                    tile.Cover(_shapeCells[index].rectTransform, bleed);
                    tile.Bind(RuneTiles.BakedTileAt(piece, i), RuneTiles.GlyphAt(piece, i),
                        RuneTiles.AreaTint(piece, i, piece.Color), 1f);
                }
            }

            _tiles.End();
        }

        void DrawShape(PieceDefinition piece, Color color)
        {
            if (_shapeCells == null) return;

            for (int i = 0; i < _shapeCells.Length; i++)
            {
                if (_shapeCells[i] != null) _shapeCells[i].enabled = false;
            }

            var cells = Shapes.Rotate(piece.Cells, 0);

            for (int i = 0; i < cells.Length; i++)
            {
                int index = IndexOf(cells[i]);
                if (index < 0) continue;

                _shapeCells[index].enabled = true;
                _shapeCells[index].color = color;

                // Ornamennya milik tile di atasnya sekarang (lihat DrawTiles); petak siluet
                // kembali jadi kotak polos supaya piece yang BUKAN rune tidak ikut kebagian
                // bingkai yang bukan bahasanya.
                _shapeCells[index].sprite = null;
            }
        }

        /// <summary>
        /// Petak footprint -&gt; slot di kotak 3x3. Grid layout mengisi dari atas ke bawah
        /// sedangkan y footprint menghitung ke atas, jadi barisnya dibalik. -1 = di luar kotak.
        /// </summary>
        static int IndexOf(Vector2Int cell)
        {
            if (cell.x < 0 || cell.x >= ShapeGrid || cell.y < 0 || cell.y >= ShapeGrid) return -1;
            return (ShapeGrid - 1 - cell.y) * ShapeGrid + cell.x;
        }

        static string KindLabel(PieceDefinition piece)
        {
            if (piece.IsRune) return "RUNE";
            return piece.IsPassive ? "SEGEL" : "SKILL";
        }
    }
}
