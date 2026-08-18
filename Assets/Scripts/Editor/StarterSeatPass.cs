using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Menata ulang seat ketiga starter mengikuti hukum papan dua lapis: setiap sel skill/segel
    /// harus berdiri di atas sel rune, tanpa tumpang tindih selapis, di dalam papan.
    ///
    /// Ada karena bentuk piece adalah GAMBARAN TANGAN pemilik project dan berubah kapan pun ia
    /// menggambar — seat yang ditata hari ini bisa mustahil besok pagi. Daripada agen menebak
    /// ulang tiap kali (dan menebak dari data basi), tombol ini dijalankan SETELAH sesi
    /// menggambar selesai, membaca bentuk yang sedang berlaku.
    ///
    /// Yang TIDAK disentuh: bentuk piece, isi loadout, rotasi rune. Hanya Origin/Rot seat
    /// skill/segel yang duduknya sudah tidak sah. Kalau platform runenya memang kurang luas,
    /// ia MELAPOR, bukan memaksa — menukar rune adalah keputusan desain, bukan urusan pass.
    /// </summary>
    public static class StarterSeatPass
    {
        [MenuItem("Tools/Grimoire/Tata Ulang Seat Starter")]
        public static void Run()
        {
            // Keluar play mode TIDAK me-reload domain: sesi play yang mengambil pakta ADDENDUM
            // meninggalkan Grimoire.Width = 7 di edit mode, dan pass ini akan memvalidasi/
            // menulis seat starter untuk papan yang salah — Origin bisa tersimpan di luar 6x6.
            Grimoire.ResetSize();

            var guids = AssetDatabase.FindAssets("t:HeroLoadout", new[] { "Assets/GameData/Heroes" });
            var report = new System.Text.StringBuilder();
            int moved = 0, stuck = 0;

            foreach (var g in guids)
            {
                var hero = AssetDatabase.LoadAssetAtPath<HeroLoadout>(AssetDatabase.GUIDToAssetPath(g));
                if (hero == null || hero.Placed == null) continue;

                bool dirty = false;

                var runeCells = new HashSet<Vector2Int>();
                for (int i = 0; i < hero.Placed.Length; i++)
                {
                    var seat = hero.Placed[i];
                    if (seat.Piece == null || !seat.Piece.IsRune) continue;

                    foreach (var c in Shapes.Rotate(seat.Piece.Cells, seat.Rot))
                    {
                        var cell = seat.Origin + c;
                        if (cell.x < 0 || cell.y < 0 ||
                            cell.x >= Grimoire.Width || cell.y >= Grimoire.Height)
                        {
                            report.AppendLine($"{hero.name}: rune {seat.Piece.Id} keluar papan di {cell} — geser runenya dulu.");
                            continue;
                        }

                        runeCells.Add(cell);
                    }
                }

                // Dua lintasan: yang sudah sah dipaku dulu supaya tempatnya tidak dicuri
                // oleh yang sedang dicarikan tempat.
                var used = new HashSet<Vector2Int>();

                for (int pass = 0; pass < 2; pass++)
                {
                    for (int i = 0; i < hero.Placed.Length; i++)
                    {
                        var seat = hero.Placed[i];
                        if (seat.Piece == null || seat.Piece.IsRune) continue;

                        var current = Shapes.Rotate(seat.Piece.Cells, seat.Rot);
                        bool valid = true;

                        foreach (var c in current)
                        {
                            var cell = seat.Origin + c;
                            if (!runeCells.Contains(cell) || used.Contains(cell)) { valid = false; break; }
                        }

                        if (pass == 0)
                        {
                            if (valid) foreach (var c in current) used.Add(seat.Origin + c);
                            continue;
                        }

                        if (valid) continue;

                        bool placed = false;

                        for (int rot = 0; rot < 4 && !placed; rot++)
                        {
                            var shape = Shapes.Rotate(seat.Piece.Cells, rot);

                            for (int y = 0; y < Grimoire.Height && !placed; y++)
                            for (int x = 0; x < Grimoire.Width && !placed; x++)
                            {
                                var origin = new Vector2Int(x, y);
                                bool fits = true;

                                foreach (var c in shape)
                                {
                                    var cell = origin + c;
                                    if (!runeCells.Contains(cell) || used.Contains(cell)) { fits = false; break; }
                                }

                                if (!fits) continue;

                                hero.Placed[i].Origin = origin;
                                hero.Placed[i].Rot = rot;
                                foreach (var c in shape) used.Add(origin + c);
                                report.AppendLine($"{hero.name}: {seat.Piece.Id} -> {origin} rot{rot}");
                                dirty = true;
                                moved++;
                                placed = true;
                            }
                        }

                        if (!placed)
                        {
                            report.AppendLine($"{hero.name}: {seat.Piece.Id} ({seat.Piece.Cells.Length} sel) " +
                                $"TIDAK MUAT — platform rune {runeCells.Count} sel, sisa {runeCells.Count - used.Count}. " +
                                "Perbesar/tukar rune loadout-nya.");
                            stuck++;
                        }
                    }
                }

                if (dirty) EditorUtility.SetDirty(hero);
            }

            AssetDatabase.SaveAssets();

            if (report.Length == 0) report.Append("Semua seat starter sudah sah — tidak ada yang digeser.");
            Debug.Log($"[StarterSeatPass] digeser {moved}, mentok {stuck}.\n{report}");
        }
    }
}
