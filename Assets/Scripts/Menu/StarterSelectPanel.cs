using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Layar pilih starter: satu kartu di tengah, digeser kiri/kanan.
    ///
    /// Yang ditunjukkan di tengah kartu adalah <b>papan pembukanya</b>, bukan gambar hiasan.
    /// Itu keputusan yang disengaja: dua starter bisa punya nama dan warna berbeda tapi bermain
    /// nyaris sama, sementara SUSUNAN papan langsung memperlihatkan apa yang akan dimainkan —
    /// berapa skill, sebesar apa, dan seberapa rapat mereka duduk. Rapat itu penting karena dua
    /// skill yang bersentuhan melebur di akhir wave pertama, jadi jarak antar piece di preview
    /// ini adalah keputusan desain yang sedang dipamerkan, bukan tata letak.
    ///
    /// Petaknya digambar sendiri di sini, bukan lewat cetakan di scene. Alasannya sepele tapi
    /// menentukan: petak papan dengan piece yang menempati bentuk sembarang butuh satu Image per
    /// sel plus satu per piece, dan merangkai 60-an objek di scene lewat builder membuat tiap
    /// perubahan ukuran jadi pekerjaan tangan. Di sini ia satu rumus.
    /// </summary>
    public class StarterSelectPanel : MonoBehaviour
    {
        [SerializeField] ContentDatabase _database;
        [SerializeField] GameBalance _balance;

        [Header("Scene")]
        [SerializeField] string _gameSceneName = "Proto";

        [Header("Layar muat")]
        [SerializeField] Sprite _loadingSigil;

        [SerializeField] Color _loadingInk = new Color(0.72f, 0.5f, 1f, 1f);

        [Header("Isi kartu")]
        [SerializeField] TextMeshProUGUI _nameLabel;
        [SerializeField] TextMeshProUGUI _blurbLabel;
        [SerializeField] TextMeshProUGUI _statsLabel;
        [SerializeField] TextMeshProUGUI _pageLabel;
        [SerializeField] Image _portrait;

        [Tooltip("Kotak tempat petak papan digambar. Petaknya mengisi kotak ini dan dijaga persegi.")]
        [SerializeField] RectTransform _board;

        [Header("Tombol")]
        [SerializeField] Button _prevButton;
        [SerializeField] Button _nextButton;
        [SerializeField] Button _playButton;

        [Header("Warna petak")]
        // Krem tipis benar waktu papan preview masih kotak gelap, dan tak terlihat sama sekali
        // begitu latarnya jadi halaman perkamen. Di atas kertas terang yang harus dipakai tinta
        // gelap — angka yang sama dengan `UiTheme.GridCellIdle` di papan in-run.
        [SerializeField] Color _cellInk = new Color(0.24f, 0.15f, 0.06f, 0.42f);

        readonly List<HeroLoadout> _heroes = new List<HeroLoadout>();
        readonly List<Image> _cells = new List<Image>();
        RuneTilePool _tiles;
        readonly List<Image> _pips = new List<Image>();

        int _index;

        void Awake()
        {
            Wire(_prevButton, () => Step(-1));
            Wire(_nextButton, () => Step(1));
            Wire(_playButton, Launch);
        }

        void OnEnable()
        {
            BuildOrder();

            // Membuka di starter yang terakhir dipakai, bukan selalu di yang pertama. Pemain yang
            // sedang mengulang run yang sama tidak harus menggeser balik tiap kali mati.
            _index = Mathf.Max(0, IndexOf(HeroChoice.Id));

            Redraw();
        }

        void Update()
        {
            if (_heroes.Count < 2) return;

            int step = ProtoInput.CarouselStepDown;
            if (step != 0) Step(step);
        }

        void BuildOrder()
        {
            _heroes.Clear();
            if (_database == null) return;

            var all = _database.Heroes;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null) _heroes.Add(all[i]);
            }
        }

        int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;

            for (int i = 0; i < _heroes.Count; i++)
            {
                if (_heroes[i].Id == id) return i;
            }

            return 0;
        }

        void Step(int direction)
        {
            if (_heroes.Count == 0) return;

            // Melingkar, bukan mentok. Daftar sependek ini tidak punya "ujung" yang berarti, dan
            // tombol yang mati di ujung membuat pemain mengira dia sudah melihat semuanya padahal
            // baru saja mulai menggeser ke arah yang salah.
            _index = (_index + direction + _heroes.Count) % _heroes.Count;
            Redraw();
        }

        void Launch()
        {
            if (_heroes.Count == 0)
            {
                Debug.LogError("[StarterSelectPanel] tidak ada HeroLoadout di ContentDatabase — " +
                               "jalankan Tools/Grimoire/Generate Heroes.", this);
                return;
            }

            HeroChoice.Id = _heroes[_index].Id;
            LoadingScreen.Go(_gameSceneName, _loadingSigil, _loadingInk);
        }

        // ==================================================================
        //  menggambar
        // ==================================================================

        void Redraw()
        {
            bool any = _heroes.Count > 0;

            if (_playButton != null) _playButton.interactable = any;
            if (_prevButton != null) _prevButton.gameObject.SetActive(_heroes.Count > 1);
            if (_nextButton != null) _nextButton.gameObject.SetActive(_heroes.Count > 1);

            if (!any)
            {
                if (_nameLabel != null) _nameLabel.text = Loc.T("starter.empty.title");
                if (_blurbLabel != null) _blurbLabel.text = Loc.T("starter.empty.hint");
                if (_statsLabel != null) _statsLabel.text = "";
                if (_pageLabel != null) _pageLabel.text = "";
                DrawBoard(null);
                return;
            }

            var hero = _heroes[_index];

            if (_nameLabel != null)
            {
                _nameLabel.text = Loc.T("hero." + hero.Id + ".name",
                    string.IsNullOrEmpty(hero.DisplayName) ? hero.Id : hero.DisplayName);
                _nameLabel.color = hero.Accent;
            }

            if (_blurbLabel != null) _blurbLabel.text = Loc.T("hero." + hero.Id + ".blurb", hero.Blurb);
            if (_statsLabel != null) _statsLabel.text = DescribeStats(hero);
            if (_pageLabel != null) _pageLabel.text = (_index + 1) + " / " + _heroes.Count;

            if (_portrait != null)
            {
                // Tanpa art, slotnya DIMATIKAN — bukan diisi kotak kosong. Kotak abu-abu seukuran
                // portrait terbaca sebagai gambar yang gagal dimuat, dan itu tuduhan yang salah
                // untuk starter yang memang belum punya art.
                _portrait.gameObject.SetActive(hero.Portrait != null);
                _portrait.sprite = hero.Portrait;
            }

            DrawBoard(hero);
        }

        /// <summary>
        /// Angka yang BENAR-BENAR dipakai, bukan isi field mentahnya: starter yang membiarkan
        /// sebuah stat di −1 tetap harus menampilkan berapa nilainya nanti, karena yang sedang
        /// dibandingkan pemain adalah dua starter — bukan dua aset.
        /// </summary>
        string DescribeStats(HeroLoadout hero)
        {
            if (_balance == null) return "";

            float hp = HeroLoadout.Pick(hero.MaxHp, _balance.BaseMaxHp);
            float mana = HeroLoadout.Pick(hero.MaxMana, _balance.BaseMaxMana);
            float manaRegen = HeroLoadout.Pick(hero.ManaRegen, _balance.BaseManaRegen);
            float hpRegen = HeroLoadout.Pick(hero.HpRegen, _balance.BaseHpRegen);
            float speed = HeroLoadout.Pick(hero.MoveSpeed, _balance.BaseMoveSpeed);

            return Loc.F("starter.stats",
                Mathf.RoundToInt(hp), hpRegen.ToString("0.#"),
                Mathf.RoundToInt(mana), manaRegen.ToString("0.#"),
                speed.ToString("0.#"));
        }

        void DrawBoard(HeroLoadout hero)
        {
            if (_board == null) return;

            var r = _board.rect;
            if (r.width < 1f || r.height < 1f) return;

            const float Gap = 4f;

            // Sel dijaga persegi: petak yang gepeng membuat bentuk piece berbohong, dan bentuk
            // adalah separuh isi dari apa yang sedang dipamerkan kartu ini.
            float step = Mathf.Min((r.width + Gap) / Grimoire.Width,
                                   (r.height + Gap) / Grimoire.Height);

            float cell = Mathf.Max(1f, step - Gap);

            // Sisa ruang dibagi dua supaya petaknya duduk di tengah kotak, bukan menempel di pojok.
            float padX = (r.width - (step * Grimoire.Width - Gap)) * 0.5f;
            float padY = (r.height - (step * Grimoire.Height - Gap)) * 0.5f;

            int used = 0;

            // Visual piece (art & ikon) dicatat supaya bisa DINAIKKAN di atas semua tile rune
            // di akhir. Urutan sibling pool tidak bisa dipercaya: tile dibuat sesuai kebutuhan,
            // dan kartu hero yang butuh tile lebih banyak menetaskan tile BARU di atas segalanya
            // — jimat Ward tenggelam di bawah rune persis karena itu.
            var risen = new List<Image>();

            if (_tiles == null) _tiles = new RuneTilePool(_board);
            _tiles.Begin();

            // Petak kosongnya dulu, seluruhnya. Digambar belakangan ia akan menutupi piece —
            // urutan anak DI SINI adalah urutan gambar.
            for (int y = 0; y < Grimoire.Height; y++)
            {
                for (int x = 0; x < Grimoire.Width; x++)
                {
                    var img = CellAt(used++);
                    img.sprite = null;
                    img.preserveAspect = false;
                    img.color = _cellInk;
                    Put(img, padX + x * step, padY + y * step, cell, cell);
                }
            }

            if (hero != null)
            {
                for (int i = 0; i < hero.Placed.Length; i++)
                {
                    var seat = hero.Placed[i];
                    if (seat.Piece == null) continue;

                    // `Cells`, bukan `Shapes.Of(Shape)`: piece bergambar tangan menyimpan
                    // bentuknya di CustomCells, dan membaca Shape mentah membuat preview
                    // menggambar bentuk yang bukan miliknya.
                    var shape = Shapes.Rotate(seat.Piece.Cells, seat.Rot);

                    int minX = int.MaxValue, minY = int.MaxValue;
                    int maxX = int.MinValue, maxY = int.MinValue;
                    int seated = 0;
                    int anchorX = -1, anchorY = -1;

                    for (int c = 0; c < shape.Length; c++)
                    {
                        int gx = seat.Origin.x + shape[c].x;
                        int gy = seat.Origin.y + shape[c].y;

                        // Piece yang menggantung keluar papan dilewati diam-diam di sini, TAPI
                        // bukan berarti tidak apa-apa: yang mendudukkannya saat run akan
                        // melemparnya ke lantai dan menulis peringatan. Preview tidak perlu
                        // meneriakkan hal yang sama dua kali per frame.
                        if (gx < 0 || gy < 0 || gx >= Grimoire.Width || gy >= Grimoire.Height) continue;

                        var img = CellAt(used++);
                        img.sprite = null;
                        img.preserveAspect = false;

                        // BUKAN warna piece: kotak isi warna-warni di belakang ikon terbaca
                        // sebagai placeholder primitif, bukan tapak. Hitam nyaris transparan —
                        // cukup untuk membayangkan footprint di atas perkamen, ikonnya yang
                        // bicara. (Pilihan pemilik project: hide, atau hitam setransparan ini.)
                        img.color = new Color(0f, 0f, 0f, 0.14f);

                        Put(img, padX + gx * step, padY + gy * step, cell, cell);

                        // SATU tile rune utuh per PETAK, sama persis dengan papan in-run dan
                        // codex. Indeksnya `c`, bukan urutan petak yang lolos batas papan:
                        // glyph tiap petak terikat ke posisinya di dalam bentuk piece, jadi
                        // melewatkan satu petak tidak boleh menggeser gambar petak sesudahnya.
                        if (RuneTiles.IsRuneGlyph(seat.Piece.Icon))
                        {
                            // Kotak warnanya DIMATIKAN, walau letak dan ukurannya tetap dipakai
                            // tile di bawah ini. Sprite petak dari atlas punya tepi transparan,
                            // jadi kotak yang dibiarkan menyala menyembul sebagai pita berwarna
                            // mengelilingi runenya.
                            img.enabled = false;

                            var tile = _tiles.Take();
                            tile.Cover(img.rectTransform, Gap / Mathf.Max(1f, cell));
                            tile.Bind(RuneTiles.BakedTileAt(seat.Piece, c), RuneTiles.GlyphAt(seat.Piece, c),
                                RuneTiles.AreaTint(seat.Piece, c, seat.Piece.Color), 1f);
                        }

                        if (gx < minX) minX = gx;
                        if (gy < minY) minY = gy;
                        if (gx > maxX) maxX = gx;
                        if (gy > maxY) maxY = gy;
                        if (anchorX < 0) { anchorX = gx; anchorY = gy; }
                        seated++;
                    }

                    // Rune sudah selesai digambar sebagai tile per petak di atas.
                    if (seated == 0) continue;
                    if (RuneTiles.IsRuneGlyph(seat.Piece.Icon)) continue;

                    // ART papan menang atas ikon — rumusnya SATU dengan papan in-run
                    // (PieceArt.Layout), jadi jimat 3 petak membentang 3 petak di sini juga,
                    // bukan menciut jadi ikon satu sel.
                    Vector2 artCenter, artSize;
                    float artAngle;
                    if (PieceArt.Layout(seat.Piece,
                        new Vector2(padX + seat.Origin.x * step, padY + seat.Origin.y * step),
                        seat.Rot, cell, Gap, out artCenter, out artSize, out artAngle))
                    {
                        var art = CellAt(used++);
                        art.enabled = true;
                        art.sprite = seat.Piece.Art;
                        art.color = Color.white;
                        art.preserveAspect = false;

                        var artRt = art.rectTransform;
                        artRt.pivot = new Vector2(0.5f, 0.5f);
                        artRt.anchoredPosition = artCenter;
                        artRt.sizeDelta = artSize;
                        artRt.localEulerAngles = new Vector3(0f, 0f, artAngle);
                        risen.Add(art);
                        continue;
                    }

                    if (seat.Piece.Icon == null) continue;

                    // SATU ikon per piece, di pusat petaknya. Dulu ikonnya digambar ulang di
                    // tiap sel: rune 2x2 tampil sebagai empat salinan glyph yang sama — wallpaper,
                    // bukan satu benda. Papan in-run sudah lama memakai aturan satu-glyph-per-piece;
                    // layar inilah yang ketinggalan.
                    int cols = maxX - minX + 1;
                    int rows = maxY - minY + 1;

                    // Aturan yang sama dengan papan in-run: ikon besar di tengah hanya untuk
                    // bentuk yang MENGISI PENUH kotak pembatasnya. Bentuk bolong (diagonal, L)
                    // menaruh ikonnya di sel jangkar — ikon yang dibentangkan di tengah kotak
                    // 2x2 milik bentuk diagonal ngambang di antara petak dan menumpangi
                    // petak piece tetangga.
                    bool solid = seated == cols * rows;
                    float side = (solid ? Mathf.Min(cols, rows) * cell : cell) * 0.92f;

                    var glyph = CellAt(used++);
                    glyph.sprite = seat.Piece.Icon;
                    glyph.color = Color.white;
                    glyph.preserveAspect = true;

                    if (solid)
                    {
                        Put(glyph,
                            padX + minX * step + (cols * step - Gap - side) * 0.5f,
                            padY + minY * step + (rows * step - Gap - side) * 0.5f,
                            side, side);
                    }
                    else
                    {
                        Put(glyph,
                            padX + anchorX * step + (cell - side) * 0.5f,
                            padY + anchorY * step + (cell - side) * 0.5f,
                            side, side);
                    }

                    risen.Add(glyph);
                }
            }

            _tiles.End();

            // SETELAH seluruh tile lahir: visual piece dinaikkan paling atas, urut gambar tetap.
            for (int i = 0; i < risen.Count; i++) risen[i].transform.SetAsLastSibling();

            for (int i = used; i < _cells.Count; i++) _cells[i].enabled = false;

            DrawPips();
        }

        /// <summary>Titik-titik penanda "kartu ke berapa dari berapa" di bawah papan.</summary>
        void DrawPips()
        {
            for (int i = 0; i < _pips.Count; i++) _pips[i].enabled = false;
            if (_board == null || _heroes.Count < 2) return;

            const float Size = 9f;
            const float Gap = 8f;

            float total = _heroes.Count * Size + (_heroes.Count - 1) * Gap;
            float left = (_board.rect.width - total) * 0.5f;

            for (int i = 0; i < _heroes.Count; i++)
            {
                var pip = PipAt(i);
                pip.enabled = true;
                pip.color = i == _index
                    ? _heroes[i].Accent
                    : new Color(1f, 1f, 1f, 0.22f);

                Put(pip, left + i * (Size + Gap), -Size - 14f, Size, Size);
            }
        }

        static void Put(Image img, float x, float y, float w, float h)
        {
            img.enabled = true;

            // Pool yang sama dipakai jalur ART (pivot tengah + rotasi) — dikembalikan ke
            // bawaan di sini supaya sel yang kebagian Image bekas art tidak ikut miring.
            var rt = img.rectTransform;
            rt.pivot = Vector2.zero;
            rt.localEulerAngles = Vector3.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        Image CellAt(int index)
        {
            while (_cells.Count <= index) _cells.Add(NewImage("Cell_" + _cells.Count));
            return _cells[index];
        }

        Image PipAt(int index)
        {
            while (_pips.Count <= index) _pips.Add(NewImage("Pip_" + _pips.Count));
            return _pips[index];
        }

        Image NewImage(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_board, false);

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            return img;
        }

        static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
