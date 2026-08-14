using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Aturan gambar rune di seluruh permainan: <b>satu petak papan = satu tile rune utuh</b>.
    ///
    /// Rune 3 petak digambar sebagai TIGA tile berdampingan, rune salib sebagai LIMA tile
    /// tersusun salib. Bukan satu glyph besar yang dibentangkan di atas kotak pembatasnya —
    /// permintaan pemilik project, dan itu keputusan yang benar: kotak pembatas berbohong soal
    /// bentuk. Salib dan kotak 3x3 punya kotak pembatas yang sama persis, jadi satu gambar besar
    /// membuat dua bentuk yang sangat berbeda terlihat identik. Petak yang digambar satu per satu
    /// tidak pernah bisa berbohong: yang kelihatan ADALAH yang ditempati.
    ///
    /// Tampang tile-nya milik <c>RuneCell.prefab</c>, bukan kode. Ganti bingkai, warna, atau
    /// lapisannya di prefab itu dan ketiga layar — papan in-run, codex, layar pilih starter —
    /// ikut tanpa satu baris pun diubah di sini.
    /// </summary>
    public static class RuneTiles
    {
        /// <summary>
        /// Urutan sheet rune, dan urutan inilah yang menentukan glyph tiap petak: petak ke-0
        /// memakai ikon piece-nya sendiri, petak berikutnya melangkah satu di daftar ini dan
        /// memutar balik ke awal setelah yang terakhir.
        ///
        /// Aturan itu bukan karangan di sini — ia dibaca balik dari prefab
        /// <c>RuneTile_*</c> yang sudah ditata tangan, dan cocok untuk keenam belasnya. Menaruh
        /// urutannya di satu tempat berarti rune yang bentuknya diubah lewat Editor Bentuk Grid
        /// langsung dapat glyph yang benar untuk petak barunya, tanpa aset apa pun disentuh.
        /// </summary>
        static readonly string[] SheetNames =
        {
            "Rune_S1_1", "Rune_S1_2", "Rune_S1_3", "Rune_S1_4", "Rune_S1_5", "Rune_S1_6",
            "Rune_S2_1", "Rune_S2_2", "Rune_S2_3", "Rune_S2_4",
            "Rune_S3_1", "Rune_S3_2",
            "Rune_S4_1", "Rune_S4_2", "Rune_S4_3",
            "Rune_S5_1"
        };

        /// <summary>
        /// Seberapa jauh TINTA tiap rune meleset dari pusat kanvas PNG-nya, dalam pecahan sisi
        /// gambar (+x kanan, +y atas). Urutannya sejajar <see cref="SheetNames"/>.
        ///
        /// Ada karena runenya digambar tidak terpusat di kanvasnya sendiri, dan melesetnya
        /// BERBEDA-BEDA: enam rune bintang satu turun 11-15%, yang bintang tiga ke atas justru
        /// naik ~9%. Satu offset seragam akan membenarkan sebagian dan memperburuk sisanya.
        ///
        /// Diukur dengan memindai alpha tiap PNG (ambang 25/255), bukan dibaca dari mesh Tight
        /// milik sprite-nya: mesh itu memeluk GLOW-nya, bukan tintanya, dan melaporkan lebar
        /// tinta 1,000 untuk gambar yang tintanya cuma separuh kanvas. Kalau art runenya
        /// digambar ulang, angka di sini harus ikut diukur ulang.
        /// </summary>
        static readonly Vector2[] SheetInk =
        {
            new Vector2(-0.002f, -0.111f), new Vector2( 0.053f, -0.125f),
            new Vector2( 0.059f, -0.116f), new Vector2( 0.025f, -0.116f),
            new Vector2( 0.020f, -0.147f), new Vector2( 0.084f, -0.120f),

            new Vector2( 0.002f, -0.040f), new Vector2( 0.107f, -0.021f),
            new Vector2(-0.004f, -0.043f), new Vector2( 0.076f, -0.037f),

            new Vector2( 0.066f,  0.094f), new Vector2(-0.076f,  0.088f),

            new Vector2( 0.043f,  0.087f), new Vector2( 0.018f,  0.089f),
            new Vector2( 0.031f,  0.088f),

            new Vector2( 0.039f,  0.089f)
        };

        static Sprite[] _sheet;
        static GameObject _cellPrefab;
        static bool _cellPrefabTried;

        /// <summary>Ikon yang berasal dari sheet rune, satu-satunya yang digambar sebagai tile.</summary>
        public static bool IsRuneGlyph(Sprite icon)
        {
            return icon != null && icon.name.StartsWith("Rune_S");
        }

        /// <summary>
        /// Cetakan satu petak. Null = prefabnya hilang dari Resources; pemanggilnya harus tetap
        /// jalan tanpa itu — papan yang kehilangan hiasan masih papan, papan yang melempar
        /// NullReference tiap frame bukan apa-apa lagi.
        /// </summary>
        public static GameObject CellPrefab
        {
            get
            {
                if (!_cellPrefabTried)
                {
                    _cellPrefab = Resources.Load<GameObject>("RuneCell");
                    _cellPrefabTried = true;

                    if (_cellPrefab == null)
                    {
                        Debug.LogWarning("[RuneTiles] RuneCell.prefab tidak ada di Resources — " +
                                         "petak rune digambar polos. Prefabnya ada di " +
                                         "Assets/Prefabs/UI/Runes/Resources/RuneCell.prefab.");
                    }
                }

                return _cellPrefab;
            }
        }

        /// <summary>
        /// Glyph untuk petak ke-<paramref name="index"/> sebuah piece, dihitung dari ikonnya
        /// sendiri. Piece yang ikonnya bukan dari sheet rune mengembalikan ikonnya apa adanya —
        /// pemanggilnya yang memutuskan mau menggambarnya atau tidak.
        /// </summary>
        public static Sprite GlyphAt(PieceDefinition def, int index)
        {
            if (def == null || def.Icon == null) return null;
            if (!IsRuneGlyph(def.Icon)) return def.Icon;

            EnsureSheet();

            int at = -1;
            for (int i = 0; i < SheetNames.Length; i++)
            {
                if (SheetNames[i] == def.Icon.name) { at = i; break; }
            }

            // Ikon rune yang namanya tidak ada di daftar: sheet-nya bertambah tanpa daftar ini
            // ikut diperbarui. Jatuh balik ke ikonnya sendiri di semua petak — salah, tapi
            // terbaca; melempar exception di tengah gambar papan tidak.
            if (at < 0) return def.Icon;

            int n = SheetNames.Length;
            var glyph = _sheet[(((at + index) % n) + n) % n];
            return glyph != null ? glyph : def.Icon;
        }

        /// <summary>
        /// Koreksi yang harus DITERAPKAN supaya tintanya duduk di tengah: kebalikan dari
        /// melesetnya. Sprite di luar sheet mengembalikan nol - tidak tahu bukan alasan untuk
        /// menggeser gambar orang.
        /// </summary>
        public static Vector2 InkOffset(Sprite glyph)
        {
            if (glyph == null) return Vector2.zero;

            for (int i = 0; i < SheetNames.Length; i++)
            {
                if (SheetNames[i] == glyph.name) return SheetInk[i];
            }

            return Vector2.zero;
        }

        static void EnsureSheet()
        {
            if (_sheet != null) return;

            _sheet = new Sprite[SheetNames.Length];
            for (int i = 0; i < SheetNames.Length; i++)
            {
                _sheet[i] = Resources.Load<Sprite>("Runes/" + SheetNames[i]);
            }
        }
    }

    /// <summary>
    /// Kolam tile rune yang menempel di satu induk. Dipakai ulang tiap gambar ulang, tidak
    /// pernah dibuat-buang: papan in-run menggambar ulang TIAP FRAME, dan Instantiate per frame
    /// adalah cara paling cepat mengubah papan yang mulus jadi papan yang tersendat.
    /// </summary>
    public sealed class RuneTilePool
    {
        readonly Transform _parent;
        readonly Transform _after;
        readonly List<RuneCellView> _pool = new List<RuneCellView>();
        int _used;

        /// <param name="after">
        /// Tile baru disisipkan tepat SESUDAH objek ini di antara saudaranya. Null = ditaruh
        /// paling belakang, artinya digambar paling atas — benar untuk kotak yang isinya cuma
        /// petak, salah untuk kanvas in-run yang di ujungnya ada panel, kartu hover, dan kerudung
        /// layar mati. Tile yang menimpa layar GAME OVER adalah bug yang sulit dipercaya.
        /// </param>
        public RuneTilePool(Transform parent, Transform after = null)
        {
            _parent = parent;
            _after = after;
        }

        /// <summary>Mulai satu ronde gambar. Wajib dipasangkan dengan <see cref="End"/>.</summary>
        public void Begin()
        {
            _used = 0;
        }

        /// <summary>Satu tile siap pakai. Tidak pernah null.</summary>
        public RuneCellView Take()
        {
            while (_pool.Count <= _used)
            {
                var prefab = RuneTiles.CellPrefab;

                GameObject go;
                if (prefab != null)
                {
                    go = Object.Instantiate(prefab, _parent, false);
                }
                else
                {
                    go = new GameObject("RuneCell", typeof(RectTransform));
                    go.transform.SetParent(_parent, false);
                }

                go.name = "RuneTile_" + _pool.Count;

                // Disisipkan satu per satu di titik yang sama: tile tidak pernah saling menimpa,
                // jadi urutan di antara mereka sendiri tidak menentukan apa-apa.
                if (_after != null && _after.parent == _parent)
                    go.transform.SetSiblingIndex(_after.GetSiblingIndex() + 1);

                var view = go.GetComponent<RuneCellView>();
                if (view == null) view = go.AddComponent<RuneCellView>();

                _pool.Add(view);
            }

            var tile = _pool[_used++];
            if (!tile.gameObject.activeSelf) tile.gameObject.SetActive(true);
            return tile;
        }

        /// <summary>Menyembunyikan sisa kolam yang tidak terpakai ronde ini.</summary>
        public void End()
        {
            for (int i = _used; i < _pool.Count; i++)
            {
                if (_pool[i] != null && _pool[i].gameObject.activeSelf)
                    _pool[i].gameObject.SetActive(false);
            }
        }
    }
}
