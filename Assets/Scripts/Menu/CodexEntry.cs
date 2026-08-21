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
        /// Ikon ASET piece di atas siluet. Diisi dari PREFAB (anak "Icon" di CodexCard) supaya
        /// posisi/ukurannya milik tangan user — pembuatan runtime di bawah tinggal cadangan
        /// untuk scene warisan yang kartunya belum punya anak Icon.
        /// Sengaja BUKAN anak kotak siluet: GridLayoutGroup di sana menata semua anaknya
        /// sebagai sel 3x3, dan ikon yang dititipkan ke situ ikut dijejalkan ke grid.
        /// </summary>
        [Tooltip("Image ikon piece. Kosong = dibuat runtime sejajar kotak siluet (jalur lama).")]
        [SerializeField] Image _icon;

        [Tooltip("Deretan icon bintang rarity (maks 5, urut kiri ke kanan). Terisi = teks " +
                 "meta tidak lagi menulis '*', bintang tampil sebagai gambar ini.")]
        [SerializeField] Image[] _stars;

        [Tooltip("Pusatkan bentuk di dalam kotak siluet. Geserannya terjadi DI DALAM kotak " +
                 "(lewat padding grid), jadi kotaknya sendiri tidak pernah pindah dan tidak " +
                 "mungkin menabrak teks. Matikan kalau mau bentuk nempel pojok kiri-atas.")]
        [SerializeField] bool _centerShape = true;

        [Header("Tata letak kartu — semua boleh disetel di sini")]
        [Tooltip("MATI secara bawaan: posisi tiap anak kartu sepenuhnya milik tanganmu di " +
                 "prefab, komponen ini tidak menyentuhnya. Nyalakan cuma kalau kamu mau " +
                 "menata lewat angka-angka di bawah.")]
        [SerializeField] bool _applyLayout;

        [Tooltip("Tepi dalam kartu — x=kiri, y=kanan, z=atas, w=bawah.")]
        [SerializeField] Vector4 _padding = new Vector4(14f, 12f, 10f, 10f);

        [Tooltip("Sisi kolom gambar (ikon & siluet). 0 = setinggi kartu dikurangi tepi atas-bawah.")]
        [SerializeField] float _artSize = 0f;

        [Tooltip("Jarak kolom gambar ke kolom teks.")]
        [SerializeField] float _gap = 14f;

        [Tooltip("Sisi satu icon bintang.")]
        [SerializeField] float _starSize = 13f;

        [Tooltip("Jarak antar titik bintang (dari kiri bintang ke kiri bintang berikutnya).")]
        [SerializeField] float _starStep = 16f;

        [Tooltip("Jarak deret bintang ke label jenis di sebelahnya.")]
        [SerializeField] float _metaGap = 10f;

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

            bool iconStars = _stars != null && _stars.Length > 0;

            if (_meta != null)
            {
                // Rarity is withheld until found: the star count alone would give the piece away.
                _meta.text = known
                    ? (iconStars ? KindLabel(piece) : Shapes.StarText(piece.Stars) + "   " + KindLabel(piece))
                    : "";
                _meta.color = _unknownText;
            }

            if (iconStars)
            {
                for (int i = 0; i < _stars.Length; i++)
                {
                    if (_stars[i] != null) _stars[i].enabled = known && i < piece.Stars;
                }
            }

            // Yang DITEMUKAN = IKON PENUH, TANPA petak ("gak mau ada grid-grid, buat
            // iconnya gede-gede aja" — pemilik project; percobaan gaya-papan sebelumnya
            // justru ditolak). Ikonnya dibesarkan mengisi SELURUH kotak seni, bukan
            // seukuran petak. Grid tinggal milik yang BELUM ditemukan (siluet buta "???")
            // — dan rune tetap bahasa tile karena begitulah rupanya di mana pun.
            bool tiledRune = RuneTiles.IsRuneGlyph(piece.Icon);
            var icon = IconImage();
            var full = piece.Icon != null ? piece.Icon : piece.Art;
            bool iconShown = icon != null && known && !tiledRune && full != null;

            if (icon != null)
            {
                icon.enabled = iconShown;
                if (iconShown)
                {
                    icon.sprite = full;
                    icon.preserveAspect = true;
                    FitIconToShapeBox(icon);
                }
            }

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
        /// Ikon dibesarkan MENGISI kotak siluet — "buat dia gede-gede aja", bukan seukuran
        /// petak. Kotaknya sendiri tetap milik prefab; ikon cuma meminjam tempat & luasnya.
        /// Kalau ikon dan kotak beda induk (prefab menata sendiri), hanya UKURANNYA yang
        /// disamakan — posisinya tetap milik tangan user.
        /// </summary>
        void FitIconToShapeBox(Image icon)
        {
            if (_shapeBox == null && _shapeCells != null && _shapeCells.Length > 0 &&
                _shapeCells[0] != null)
            {
                _shapeBox = _shapeCells[0].transform.parent as RectTransform;
            }
            if (_shapeBox == null) return;

            var rt = icon.rectTransform;
            if (rt.parent == _shapeBox.parent)
            {
                rt.anchorMin = _shapeBox.anchorMin;
                rt.anchorMax = _shapeBox.anchorMax;
                rt.pivot = _shapeBox.pivot;
                rt.anchoredPosition = _shapeBox.anchoredPosition;
                rt.sizeDelta = _shapeBox.sizeDelta;
            }
            else
            {
                rt.sizeDelta = _shapeBox.rect.size;
            }
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
                var off = GridOffset(cells);
                CenterShapeBox(cells);

                for (int i = 0; i < cells.Length; i++)
                {
                    int index = IndexOf(cells[i] + off);
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
            var off = GridOffset(cells);
            CenterShapeBox(cells);

            for (int i = 0; i < cells.Length; i++)
            {
                int index = IndexOf(cells[i] + off);
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

        /// <summary>
        /// Geseran supaya bentuk DUDUK DI TENGAH kotak 3x3, bukan nemplok di pojok — piece
        /// 1 petak tampil di sel tengah, garis 2 petak di baris tengah, dst. Berlaku untuk
        /// siluet maupun tile rune karena keduanya lewat pemetaan ini.
        /// </summary>
        static Vector2Int GridOffset(Vector2Int[] cells)
        {
            int maxX = 0, maxY = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].x > maxX) maxX = cells[i].x;
                if (cells[i].y > maxY) maxY = cells[i].y;
            }

            // x dibulatkan ke bawah (isi condong kiri), y ke atas (isi condong ke baris atas).
            // Sisa setengah petaknya ditutup CenterShapeBox lewat padding — dua-duanya positif.
            return new Vector2Int(
                Mathf.Max(0, (ShapeGrid - 1 - maxX) / 2),
                Mathf.Clamp((ShapeGrid - maxY) / 2, 0, ShapeGrid - 1 - maxY));
        }

        RectTransform _shapeBox;
        RectOffset _gridPadBase;

        /// <summary>
        /// Bentuk selebar/setinggi 2 petak tidak pernah pas tengah di grid 3 yang diskrit —
        /// mencong setengah petak. Sisa setengah itu ditutup lewat PADDING GRID, bukan dengan
        /// memindahkan kotaknya: kotak siluet tetap duduk di tempat yang ditata prefab, jadi
        /// pergeseran ini mustahil menabrak teks di sebelahnya. Nol lagi untuk bentuk ganjil.
        /// </summary>
        void CenterShapeBox(Vector2Int[] cells)
        {
            if (_shapeCells == null || _shapeCells.Length == 0 || _shapeCells[0] == null) return;
            if (_shapeBox == null) _shapeBox = _shapeCells[0].transform.parent as RectTransform;
            if (_shapeBox == null) return;

            var grid = _shapeBox.GetComponent<GridLayoutGroup>();
            if (grid == null) return;

            if (_gridPadBase == null)
            {
                var p = grid.padding;
                _gridPadBase = new RectOffset(p.left, p.right, p.top, p.bottom);
            }

            if (!_centerShape)
            {
                grid.padding = new RectOffset(_gridPadBase.left, _gridPadBase.right,
                    _gridPadBase.top, _gridPadBase.bottom);
                return;
            }

            int maxX = 0, maxY = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].x > maxX) maxX = cells[i].x;
                if (cells[i].y > maxY) maxY = cells[i].y;
            }

            float stepX = grid.cellSize.x + grid.spacing.x;
            float stepY = grid.cellSize.y + grid.spacing.y;
            var off = GridOffset(cells);
            float mid = (ShapeGrid - 1) * 0.5f;

            // Sisa setengah petak: mendatar isi condong ke kiri (didorong ke kanan),
            // menurun isi condong ke atas (didorong ke bawah). Dua-duanya tak pernah negatif.
            float colCenter = off.x + maxX * 0.5f;
            float rowCenter = (ShapeGrid - 1) - off.y - maxY * 0.5f;

            int shiftX = Mathf.Max(0, Mathf.RoundToInt((mid - colCenter) * stepX));
            int shiftY = Mathf.Max(0, Mathf.RoundToInt((mid - rowCenter) * stepY));

            grid.padding = new RectOffset(_gridPadBase.left + shiftX, _gridPadBase.right,
                _gridPadBase.top + shiftY, _gridPadBase.bottom);
        }

        /// <summary>
        /// Menata anak-anak kartu dari angka-angka di Inspector: kolom gambar di kiri, kolom
        /// teks di kanan. Batas kolom teks DIHITUNG dari tepi + lebar gambar + jarak, jadi
        /// gambar dan teks tidak mungkin saling tindih berapa pun angkanya. Matikan
        /// <see cref="_applyLayout"/> kalau mau menata tiap anak sendiri.
        /// </summary>
        public void ApplyLayout()
        {
            if (!_applyLayout) return;

            var card = transform as RectTransform;
            if (card == null) return;

            float w = card.rect.width;
            float h = card.rect.height;
            if (w < 1f || h < 1f) return;

            float padL = _padding.x, padR = _padding.y, padT = _padding.z, padB = _padding.w;
            float art = _artSize > 0.5f ? _artSize : Mathf.Max(8f, h - padT - padB);
            float textLeft = padL + art + _gap;
            float starsW = _starSize + _starStep * 4f;
            float bottomBand = padB + _starSize + 6f;

            PlaceArt(_icon != null ? _icon.rectTransform : null, padL, art);
            if (_shapeBox == null && _shapeCells != null && _shapeCells.Length > 0 &&
                _shapeCells[0] != null)
            {
                _shapeBox = _shapeCells[0].transform.parent as RectTransform;
            }
            PlaceArt(_shapeBox, padL, art);

            if (_shapeBox != null)
            {
                var grid = _shapeBox.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    float cell = (art - grid.spacing.x * (ShapeGrid - 1)) / ShapeGrid;
                    grid.cellSize = new Vector2(cell, cell);
                    _gridPadBase = null;   // padding dasar dihitung ulang dengan sel baru
                }
            }

            if (_name != null)
            {
                var rt = _name.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(textLeft, bottomBand);
                rt.offsetMax = new Vector2(-padR, -padT);
            }

            if (_stars != null && _stars.Length > 0 && _stars[0] != null)
            {
                var row = _stars[0].transform.parent as RectTransform;
                if (row != null)
                {
                    row.anchorMin = row.anchorMax = Vector2.zero;
                    row.pivot = Vector2.zero;
                    row.anchoredPosition = new Vector2(textLeft, padB);
                    row.sizeDelta = new Vector2(starsW, _starSize);
                }

                for (int i = 0; i < _stars.Length; i++)
                {
                    if (_stars[i] == null) continue;
                    var srt = _stars[i].rectTransform;
                    srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
                    srt.pivot = new Vector2(0f, 0.5f);
                    srt.anchoredPosition = new Vector2(i * _starStep, 0f);
                    srt.sizeDelta = new Vector2(_starSize, _starSize);
                }
            }

            if (_meta != null)
            {
                bool hasStars = _stars != null && _stars.Length > 0;
                float metaLeft = hasStars ? textLeft + starsW + _metaGap : textLeft;
                var rt = _meta.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.offsetMin = new Vector2(metaLeft, padB);
                rt.offsetMax = new Vector2(-padR, padB + _starSize + 4f);
            }
        }

        void PlaceArt(RectTransform rt, float left, float size)
        {
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(left, 0f);
            rt.sizeDelta = new Vector2(size, size);
        }

        void Awake() => ApplyLayout();

#if UNITY_EDITOR
        void OnValidate()
        {
            // Ditunda satu frame: mengubah RectTransform langsung di dalam OnValidate
            // membuat Unity mengeluh soal SendMessage saat prefab sedang dimuat.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                ApplyLayout();
            };
        }
#endif

        static string KindLabel(PieceDefinition piece)
        {
            if (piece.IsRune) return Loc.T("held.kind.rune");
            return Loc.T(piece.IsPassive ? "codex.kind.sigil" : "held.kind.skill");
        }
    }
}
