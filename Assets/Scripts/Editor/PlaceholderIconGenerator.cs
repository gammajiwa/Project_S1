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

                var captured = piece;
                var sprite = Ensure($"Icon_{piece.Id}", () => Render(captured), overwrite,
                    ref written, ref kept);

                if (sprite == null || piece.Icon == sprite) continue;

                piece.Icon = sprite;
                EditorUtility.SetDirty(piece);
                linked++;
            }

            // Ailments and buffs have no footprint to draw, so they get a pip glyph. Colour alone
            // cannot separate seven ailments at 26 pixels, and telling them apart at a glance is
            // the entire job of the HUD strip.
            for (int i = 0; i < db.Statuses.Count; i++)
            {
                var status = db.Statuses[i];
                if (status == null || string.IsNullOrEmpty(status.Id)) continue;

                var captured = status;
                int pips = i;
                var sprite = Ensure($"Icon_status_{status.Id}", () => Glyph(captured.Color, pips),
                    overwrite, ref written, ref kept);

                if (sprite == null || status.Icon == sprite) continue;

                status.Icon = sprite;
                EditorUtility.SetDirty(status);
                linked++;
            }

            linked += LinkBuffs(db.Buffs, overwrite, ref written, ref kept);
            linked += LinkBuffs(db.Debuffs, overwrite, ref written, ref kept);

            // Pakta memakai glyph pip yang sama dengan buff, dan sengaja dibedakan cuma lewat
            // warnanya sendiri. Bentuknya boleh sama karena letaknya berbeda: pakta punya strip
            // sendiri di HUD, jadi mata tidak pernah harus memisahkan pakta dari buff di dalam
            // satu barisan — yang harus dipisahkan cuma pakta dari pakta lain.
            for (int i = 0; i < db.PactCatalogue.Count; i++)
            {
                var pact = db.PactCatalogue[i];
                if (pact == null || string.IsNullOrEmpty(pact.Id)) continue;

                var captured = pact;
                int pips = i;
                var sprite = Ensure($"Icon_pact_{pact.Id}", () => Glyph(captured.Color, pips),
                    overwrite, ref written, ref kept);

                if (sprite == null || pact.Icon == sprite) continue;

                pact.Icon = sprite;
                EditorUtility.SetDirty(pact);
                linked++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PlaceholderIcons] {written} PNG baru, {kept} dipertahankan (sudah ada), " +
                      $"{linked} aset disambungkan ke ikonnya.");
        }

        static int LinkBuffs(System.Collections.Generic.IReadOnlyList<BuffDefinition> buffs,
            bool overwrite, ref int written, ref int kept)
        {
            int linked = 0;

            for (int i = 0; i < buffs.Count; i++)
            {
                var buff = buffs[i];
                if (buff == null || string.IsNullOrEmpty(buff.Id)) continue;

                var captured = buff;
                int pips = i;
                var sprite = Ensure($"Icon_buff_{buff.Id}", () => Glyph(captured.Color, pips),
                    overwrite, ref written, ref kept);

                if (sprite == null || buff.Icon == sprite) continue;

                buff.Icon = sprite;
                EditorUtility.SetDirty(buff);
                linked++;
            }

            return linked;
        }

        /// <summary>Writes the PNG when missing (or when overwriting), then hands back its sprite.</summary>
        static Sprite Ensure(string fileName, System.Func<byte[]> draw, bool overwrite,
            ref int written, ref int kept)
        {
            string path = $"{IconFolder}/{fileName}.png";

            if (File.Exists(path) && !overwrite) kept++;
            else
            {
                File.WriteAllBytes(path, draw());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                ConfigureImporter(path);
                written++;
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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

        /// <summary>
        /// A coloured disc carrying dice-style pips. Pips because they read instantly at HUD size
        /// and need no font — the count is what separates two ailments whose colours are close.
        /// </summary>
        static byte[] Glyph(Color color, int index)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];

            var clear = new Color32(0, 0, 0, 0);
            var body = (Color32)color;
            var rim = (Color32)Color.Lerp(color, Color.white, 0.55f);
            var pip = (Color32)Color.Lerp(color, Color.black, 0.62f);

            float centre = Size * 0.5f;
            float outer = Size * 0.46f;
            float inner = outer - 3f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float d = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    pixels[y * Size + x] = d > outer ? clear : (d > inner ? rim : body);
                }
            }

            // 1..6 pips, laid out like a die face.
            int count = Mathf.Clamp(index, 0, 5) + 1;
            float step = Size * 0.19f;

            for (int p = 0; p < count; p++)
            {
                float px = centre + PipOffsets[count - 1, p, 0] * step;
                float py = centre + PipOffsets[count - 1, p, 1] * step;
                Blot(pixels, px, py, Size * 0.075f, pip);
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }

        static readonly float[,,] PipOffsets =
        {
            { {0,0}, {0,0}, {0,0}, {0,0}, {0,0}, {0,0} },                           // 1
            { {-1,1}, {1,-1}, {0,0}, {0,0}, {0,0}, {0,0} },                          // 2
            { {-1,1}, {0,0}, {1,-1}, {0,0}, {0,0}, {0,0} },                          // 3
            { {-1,1}, {1,1}, {-1,-1}, {1,-1}, {0,0}, {0,0} },                        // 4
            { {-1,1}, {1,1}, {0,0}, {-1,-1}, {1,-1}, {0,0} },                        // 5
            { {-1,1}, {1,1}, {-1,0}, {1,0}, {-1,-1}, {1,-1} }                        // 6
        };

        static void Blot(Color32[] pixels, float cx, float cy, float radius, Color32 color)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius));
            int x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(cx + radius));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius));
            int y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(cy + radius));

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= radius * radius) pixels[y * Size + x] = color;
                }
            }
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
