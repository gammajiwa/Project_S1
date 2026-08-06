using System.IO;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Writes one placeholder PNG per piece and wires it into <see cref="PieceDefinition.Icon"/>.
    ///
    /// These are real files on disk, not textures built at runtime, and that is the whole point:
    /// replacing the art means dropping a new 64x64 PNG over the old one. Nothing in code has to
    /// change, and the generator never overwrites a file that already exists — so a hand-drawn icon
    /// survives every future run of this menu item.
    ///
    /// The drawing itself is the piece's own footprint in the piece's own colour, which is enough to
    /// tell a 2x2 apart from an L at icon size.
    /// </summary>
    public static class PlaceholderIconGenerator
    {
        const string Root = "Assets/GameData";
        const string IconFolder = Root + "/Icons";

        const int Size = 64;
        const int Padding = 6;

        /// <summary>Footprints never exceed 3x3, so the icon grid is a fixed 3x3.</summary>
        const int Grid = 3;

        [MenuItem("Tools/Grimoire/Generate Placeholder Icons")]
        public static void Run() => Run(false);

        /// <summary>
        /// Redraws every placeholder, replacing what is on disk. Needed after a footprint change —
        /// the icon is a picture of the shape, so a piece that changed shape is now illustrated
        /// wrongly and the create-only path will never notice.
        ///
        /// Separate menu item, and named for what it does: the create-only rule is the only thing
        /// protecting hand-drawn art from being flattened by a routine re-run.
        /// </summary>
        [MenuItem("Tools/Grimoire/Regenerate Placeholder Icons (TIMPA art)")]
        public static void Overwrite()
        {
            bool ok = EditorUtility.DisplayDialog(
                "Timpa semua ikon?",
                "Semua PNG di Assets/GameData/Icons akan ditulis ulang dari bentuk piece-nya.\n\n" +
                "Art yang sudah kamu ganti sendiri akan HILANG.",
                "Timpa", "Batal");

            if (ok) Run(true);
        }

        static void Run(bool overwrite)
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[PlaceholderIcons] ContentDatabase.asset tidak ketemu.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(IconFolder)) AssetDatabase.CreateFolder(Root, "Icons");

            int written = 0, linked = 0, kept = 0;

            for (int i = 0; i < db.Pieces.Count; i++)
            {
                var piece = db.Pieces[i];
                if (piece == null || string.IsNullOrEmpty(piece.Id)) continue;

                string path = $"{IconFolder}/Icon_{piece.Id}.png";

                if (File.Exists(path) && !overwrite) kept++;
                else
                {
                    File.WriteAllBytes(path, Render(piece));
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    ConfigureImporter(path);
                    written++;
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null || piece.Icon == sprite) continue;

                piece.Icon = sprite;
                EditorUtility.SetDirty(piece);
                linked++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PlaceholderIcons] {written} PNG baru, {kept} dipertahankan (sudah ada), " +
                      $"{linked} piece disambungkan ke ikonnya.");
        }

        static void ConfigureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Colours go in as written. The importer flags the PNG as sRGB, so what the artist picked
        /// in the colour field is what shows on screen — which is not true of the colours this
        /// project pushes straight from script into a linear-space material.
        /// </summary>
        static byte[] Render(PieceDefinition piece)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];

            var plate = new Color32(22, 22, 30, 235);
            var fill = (Color32)piece.Color;
            var edge = (Color32)Color.Lerp(piece.Color, Color.white, 0.45f);

            for (int i = 0; i < pixels.Length; i++) pixels[i] = plate;

            var cells = Shapes.Rotate(piece.Cells, 0);
            ShapeBounds(cells, out int w, out int h);

            // Centre the footprint inside the 3x3 grid so a 1-cell piece is not stuck in a corner.
            int offsetX = (Grid - w) / 2;
            int offsetY = (Grid - h) / 2;

            int span = (Size - Padding * 2) / Grid;

            for (int c = 0; c < cells.Length; c++)
            {
                int gx = cells[c].x + offsetX;
                int gy = cells[c].y + offsetY;
                if (gx < 0 || gx >= Grid || gy < 0 || gy >= Grid) continue;

                int x0 = Padding + gx * span;
                int y0 = Padding + gy * span;

                for (int y = 0; y < span - 2; y++)
                {
                    for (int x = 0; x < span - 2; x++)
                    {
                        bool border = x == 0 || y == 0 || x == span - 3 || y == span - 3;
                        pixels[(y0 + y) * Size + (x0 + x)] = border ? edge : fill;
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }

        static void ShapeBounds(Vector2Int[] shape, out int w, out int h)
        {
            int maxX = 0, maxY = 0;
            for (int i = 0; i < shape.Length; i++)
            {
                if (shape[i].x > maxX) maxX = shape[i].x;
                if (shape[i].y > maxY) maxY = shape[i].y;
            }

            w = maxX + 1;
            h = maxY + 1;
        }
    }
}
