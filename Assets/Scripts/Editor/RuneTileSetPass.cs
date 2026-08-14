using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Mengisi <see cref="RuneTileSet"/> dari atlas <c>UI Rune Border.png</c>.
    ///
    /// Atlasnya sudah dipotong tangan di Unity, tapi nama potongannya berupa nomor urut
    /// (<c>UI Rune Border_29</c>, <c>_30</c>, ...) yang tidak menyimpan satu pun petunjuk rune
    /// mana yang mana. Yang menyimpan petunjuk itu adalah LETAKNYA: enam belas petak duduk di
    /// tiga baris, dan urutan baca manusia — baris atas dulu, kiri ke kanan — persis urutan
    /// sheet rune. Jadi pemetaannya dihitung dari koordinat, bukan dari nama.
    ///
    /// Dijalankan ulang kalau atlasnya dipotong ulang. Aman diulang: hasilnya ditimpa penuh.
    /// </summary>
    public static class RuneTileSetPass
    {
        const string AtlasPath = "Assets/Art/UI/UI Rune Border.png";
        const string OutputPath = "Assets/Prefabs/UI/Runes/Resources/RuneTileSet.asset";

        /// <summary>
        /// Berapa petak di tiap baris, dari baris paling ATAS. 6 + 4 + 6 = 16, dan angka itu
        /// bukan kebetulan: enam rune bintang satu, empat bintang dua, lalu dua bintang tiga
        /// ditambah tiga bintang empat ditambah satu bintang lima.
        /// </summary>
        static readonly int[] RowCounts = { 6, 4, 6 };

        [MenuItem("Tools/Grimoire/Isi Rune Tile Set dari Atlas")]
        public static void Run()
        {
            var sprites = new List<Sprite>();

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AtlasPath))
            {
                if (asset is Sprite sprite) sprites.Add(sprite);
            }

            if (sprites.Count == 0)
            {
                EditorUtility.DisplayDialog("Rune Tile Set",
                    "Tidak ada sub-sprite di\n" + AtlasPath +
                    "\n\nAtlasnya harus ber-Sprite Mode Multiple dan sudah dipotong.", "OK");
                return;
            }

            var tiles = PickTiles(sprites, out string problem);
            if (tiles == null)
            {
                // Dicatat ke console DULU, baru ditampilkan. Dialog hilang begitu ditutup dan
                // alat ini juga dijalankan dari skrip, di mana dialognya tidak pernah terlihat
                // sama sekali — kegagalan yang tidak meninggalkan jejak adalah kegagalan yang
                // akan didiagnosis dua kali.
                Debug.LogError("[RuneTileSetPass] " + problem);
                EditorUtility.DisplayDialog("Rune Tile Set", problem, "OK");
                return;
            }

            var set = AssetDatabase.LoadAssetAtPath<RuneTileSet>(OutputPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<RuneTileSet>();
                AssetDatabase.CreateAsset(set, OutputPath);
            }

            set.Tiles = tiles;
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var log = new System.Text.StringBuilder();
            for (int i = 0; i < tiles.Length; i++)
            {
                log.Append("  ").Append(RuneTiles.SheetNameAt(i)).Append("  <-  ")
                   .Append(tiles[i] != null ? tiles[i].name : "KOSONG").Append('\n');
            }

            Debug.Log("[RuneTileSetPass] " + OutputPath + " terisi:\n" + log, set);
            Selection.activeObject = set;
        }

        /// <summary>
        /// Enam belas petak, dipilih dari potongan yang bentuknya PETAK — kira-kira persegi dan
        /// cukup besar. Atlas yang sama juga memuat palang panjang, bintang, dan sudut ornamen;
        /// menyaringnya lewat bentuk jauh lebih tahan banting daripada lewat nomor urut, yang
        /// berubah begitu satu potongan saja ditambah di tengah.
        /// </summary>
        static Sprite[] PickTiles(List<Sprite> all, out string problem)
        {
            var square = new List<Sprite>();

            foreach (var s in all)
            {
                float w = s.rect.width;
                float h = s.rect.height;
                if (w < 100f || h < 100f) continue;

                float aspect = w / h;
                if (aspect < 0.85f || aspect > 1.35f) continue;

                square.Add(s);
            }

            if (square.Count < 16)
            {
                problem = "Cuma ketemu " + square.Count + " potongan berbentuk petak, butuh 16.\n\n" +
                          "Periksa pemotongan atlasnya.";
                return null;
            }

            // Bentuk saja tidak cukup menyaring: atlas yang sama memuat lima bingkai KOSONG dan
            // sebuah mawar kompas yang sama-sama kira-kira persegi dan sama-sama besar. Yang
            // memisahkan mereka adalah letak — enam belas petak rune duduk di tiga baris paling
            // BAWAH atlas, dan tidak ada apa pun di bawahnya.
            //
            // y di ruang sprite dihitung dari bawah, jadi "paling bawah" berarti y paling kecil.
            square.Sort((a, b) => a.rect.y.CompareTo(b.rect.y));

            var rows = new List<List<Sprite>>();
            foreach (var s in square)
            {
                var row = rows.Count > 0 ? rows[rows.Count - 1] : null;

                // Toleransi setengah tinggi petak: potongan di baris yang sama tidak pernah
                // persis sejajar, tapi juga tidak pernah sejauh itu.
                if (row != null && Mathf.Abs(row[0].rect.y - s.rect.y) < row[0].rect.height * 0.5f)
                    row.Add(s);
                else
                    rows.Add(new List<Sprite> { s });
            }

            if (rows.Count < RowCounts.Length)
            {
                problem = "Cuma ketemu " + rows.Count + " baris petak, butuh " + RowCounts.Length + ".";
                return null;
            }

            // Tiga baris terbawah saja, lalu dibalik jadi urutan baca manusia: baris paling atas
            // dari ketiganya lebih dulu, karena itu yang sejajar dengan urutan sheet rune.
            rows = rows.GetRange(0, RowCounts.Length);
            rows.Reverse();

            var tiles = new Sprite[16];
            int at = 0;

            for (int r = 0; r < RowCounts.Length; r++)
            {
                var row = rows[r];
                row.Sort((a, b) => a.rect.x.CompareTo(b.rect.x));

                if (row.Count != RowCounts[r])
                {
                    problem = "Baris ke-" + (r + 1) + " berisi " + row.Count + " petak, seharusnya "
                              + RowCounts[r] + ".\n\nUrutan baris = urutan sheet rune, jadi jumlah "
                              + "yang meleset berarti pemetaannya tidak bisa dipercaya.";
                    return null;
                }

                for (int i = 0; i < row.Count; i++) tiles[at++] = row[i];
            }

            problem = null;
            return tiles;
        }
    }
}
