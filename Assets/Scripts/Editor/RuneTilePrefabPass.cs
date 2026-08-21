using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Proto.EditorTools
{
    /// <summary>
    /// Membangun ulang prefab <c>RuneTile_&lt;id&gt;</c> — satu per rune — dari bentuk grid yang
    /// BERLAKU SAAT INI.
    ///
    /// Gunanya menyusul Editor Bentuk Grid. Begitu bentuk sebuah rune diubah di sana, prefab
    /// tile-nya menggambarkan bentuk yang sudah tidak ada lagi, dan prefab yang berbohong lebih
    /// buruk daripada prefab yang tidak ada: ia terlihat seperti kebenaran. Jalankan ini sesudah
    /// mengubah bentuk, dan prefabnya menyusul.
    ///
    /// Yang dibangun di sini adalah ASET UNTUK DILIHAT dan ditata tangan — permainan sendiri
    /// tidak memuatnya. Papan in-run, codex, dan layar pilih starter menyusun tile-nya langsung
    /// dari <see cref="RuneCellView"/> + <c>RuneCell.prefab</c> saat berjalan, jadi bentuk yang
    /// baru diubah sudah benar di layar bahkan sebelum tombol di sini ditekan. Prefab ini ada
    /// supaya bentuknya bisa dilihat utuh di Project window tanpa masuk permainan.
    /// </summary>
    public static class RuneTilePrefabPass
    {
        const string CellPrefabPath = "Assets/Prefabs/UI/Runes/Resources/RuneCell.prefab";
        const string OutputFolder = "Assets/Prefabs/UI/Runes";

        /// <summary>Sisi satu petak, dan jaraknya ke petak sebelah. Angka yang sama dengan prefab yang sudah ada.</summary>
        const float Cell = 128f;
        const float Gap = 10f;
        const float Step = Cell + Gap;

        [MenuItem("Tools/Grimoire/Rebuild Rune Tile Prefabs")]
        public static void Run()
        {
            var cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath);
            if (cellPrefab == null)
            {
                EditorUtility.DisplayDialog("Rune Tile",
                    "RuneCell.prefab tidak ketemu di\n" + CellPrefabPath +
                    "\n\nItu cetakan satu petaknya — tanpa itu tidak ada yang bisa disusun.", "OK");
                return;
            }

            var runes = CollectRunes();
            if (runes.Count == 0)
            {
                EditorUtility.DisplayDialog("Rune Tile",
                    "Tidak ada piece rune yang ikonnya dari sheet rune (nama \"Rune_S...\").", "OK");
                return;
            }

            int built = 0;
            var log = new System.Text.StringBuilder();

            try
            {
                for (int i = 0; i < runes.Count; i++)
                {
                    var def = runes[i];
                    EditorUtility.DisplayProgressBar("Rune Tile", def.Id, (float)i / runes.Count);

                    var path = OutputFolder + "/RuneTile_" + def.Id + ".prefab";
                    var root = Build(def, cellPrefab);

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Object.DestroyImmediate(root);

                    built++;
                    log.Append("  ").Append(def.Id).Append("  ")
                       .Append(Shapes.NameOf(def.Shape)).Append(def.HasCustomShape ? " (tangan)" : "")
                       .Append("  ").Append(Shapes.Rotate(def.Cells, 0).Length).Append(" petak\n");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[RuneTilePrefabPass] " + built + " prefab tile dibangun ulang di " +
                      OutputFolder + ":\n" + log);
        }

        /// <summary>
        /// Rune yang ikonnya BUKAN dari sheet rune dilewati diam-diam. Tile-nya digambar dari
        /// sheet itu — tanpa titik masuk ke sheet, tidak ada yang bisa disusun, dan prefab
        /// berisi sembilan kotak kosong bukan bantuan buat siapa pun.
        /// </summary>
        static List<PieceDefinition> CollectRunes()
        {
            var found = new List<PieceDefinition>();
            var guids = AssetDatabase.FindAssets("t:PieceDefinition");

            for (int i = 0; i < guids.Length; i++)
            {
                var def = AssetDatabase.LoadAssetAtPath<PieceDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));

                if (def == null || !def.IsRune) continue;
                if (!RuneTiles.IsRuneGlyph(def.Icon)) continue;

                found.Add(def);
            }

            found.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return found;
        }

        static GameObject Build(PieceDefinition def, GameObject cellPrefab)
        {
            var cells = Shapes.Rotate(def.Cells, 0);

            int cols = 1, rows = 1;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].x + 1 > cols) cols = cells[i].x + 1;
                if (cells[i].y + 1 > rows) rows = cells[i].y + 1;
            }

            var root = new GameObject("RuneTile_" + def.Id, typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(cols * Step - Gap, rows * Step - Gap);

            for (int i = 0; i < cells.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(cellPrefab, root.transform);
                go.name = "Cell_" + cells[i].x + "_" + cells[i].y;

                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(Cell, Cell);

                // Dipusatkan di kotak pembatasnya. Prefab lama menumpuk petaknya dari pojok, dan
                // itu membuat rune bentuk apa pun duduk melenceng dari titik tengah prefabnya —
                // menyusahkan begitu prefabnya ditaruh di layar mana pun.
                rect.anchoredPosition = new Vector2(
                    (cells[i].x - (cols - 1) * 0.5f) * Step,
                    (cells[i].y - (rows - 1) * 0.5f) * Step);

                AddGlyph(go.transform, RuneTiles.GlyphAt(def, i));
            }

            return root;
        }

        /// <summary>
        /// Glyph ditambahkan sebagai anak baru, bukan disetel di prefab petaknya: <c>RuneCell</c>
        /// dipakai bersama oleh keenam belas rune, dan menaruh gambar di sana berarti keenam
        /// belasnya memakai gambar yang sama.
        /// </summary>
        static void AddGlyph(Transform parent, Sprite glyph)
        {
            if (glyph == null) return;

            var go = new GameObject("Glyph", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.sprite = glyph;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.17f, 0.17f);
            rect.anchorMax = new Vector2(0.83f, 0.83f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
