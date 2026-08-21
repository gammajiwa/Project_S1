using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Editor bentuk grid + pratinjau art, LANGSUNG di Inspector tiap
    /// <see cref="PieceDefinition"/> — tanpa window terpisah, tanpa mengetik koordinat.
    ///
    /// Petak diklik untuk dinyalakan/dimatikan; hasilnya ditulis ke
    /// <see cref="PieceDefinition.CustomCells"/> (dengan Undo). Ukuran kotaknya TIDAK
    /// dipatok angka: yang digambar selalu bentuk sekarang PLUS satu cincin petak kosong
    /// di kanan/atasnya — klik cincin itu dan bentuknya melebar, kliknya lagi mengecil.
    /// Bentuk sebesar apa pun muat, hari ini maupun untuk segel raksasa besok.
    ///
    /// Begitu CustomCells terisi, bentuk gambaran tangan MENGALAHKAN Shape preset di
    /// seluruh game (papan, tas, codex, evo) — aturan yang sudah ditanam di
    /// PieceDefinition.Cells. Tombol "Balik ke preset" mengosongkannya lagi.
    /// </summary>
    [CustomEditor(typeof(PieceDefinition))]
    [CanEditMultipleObjects]
    public class PieceShapePreview : Editor
    {
        const float CellPx = 44f;
        const float GapPx = 4f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets.Length > 1) return;

            var def = (PieceDefinition)target;
            var cells = def.Cells;
            if (cells == null) return;

            EditorGUILayout.Space(10);

            string source = def.HasCustomShape
                ? "CUSTOM (gambaran tangan — Shape preset diabaikan)"
                : $"preset '{def.Shape}' — klik petak untuk mulai menggambar tangan";
            EditorGUILayout.LabelField($"Bentuk Grid  ({cells.Length} petak)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(source, EditorStyles.miniLabel);

            var set = new HashSet<Vector2Int>(cells);

            // Kanvas = bentuk sekarang + satu cincin kosong di kanan & atas (dan selalu
            // minimal 3x3). Mau lebih besar? Nyalakan petak di cincin — kanvas ikut
            // melebar di repaint berikutnya. Tidak ada angka batas di mana pun.
            int maxX = 0, maxY = 0;
            foreach (var c in set)
            {
                if (c.x > maxX) maxX = c.x;
                if (c.y > maxY) maxY = c.y;
            }

            int viewW = Mathf.Max(3, maxX + 2);
            int viewH = Mathf.Max(3, maxY + 2);

            float pw = viewW * (CellPx + GapPx) - GapPx;
            float ph = viewH * (CellPx + GapPx) - GapPx;
            var area = GUILayoutUtility.GetRect(pw + 16f, ph + 12f);
            float ox = area.x + Mathf.Max(8f, (area.width - pw) * 0.5f);
            float oy = area.y + 6f;

            var fill = new Color(def.Color.r, def.Color.g, def.Color.b, 0.55f);
            var faint = new Color(0.5f, 0.5f, 0.55f, 0.18f);

            bool changed = false;

            for (int y = 0; y < viewH; y++)
            {
                for (int x = 0; x < viewW; x++)
                {
                    var cell = new Vector2Int(x, y);
                    bool on = set.Contains(cell);

                    // y grid naik ke atas, layar turun ke bawah — dibalik, seperti papan.
                    var r = new Rect(ox + x * (CellPx + GapPx),
                        oy + (viewH - 1 - y) * (CellPx + GapPx), CellPx, CellPx);

                    EditorGUI.DrawRect(r, on ? fill : faint);

                    if (on)
                    {
                        var edge = new Color(def.Color.r, def.Color.g, def.Color.b, 1f);
                        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2f), edge);
                        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2f, r.width, 2f), edge);
                        EditorGUI.DrawRect(new Rect(r.x, r.y, 2f, r.height), edge);
                        EditorGUI.DrawRect(new Rect(r.xMax - 2f, r.y, 2f, r.height), edge);
                    }

                    var e = Event.current;
                    if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
                    {
                        if (on) set.Remove(cell);
                        else set.Add(cell);
                        changed = true;
                        e.Use();
                    }
                }
            }

            if (changed)
            {
                // Petak terakhir tidak boleh dimatikan — piece tanpa petak tidak bisa
                // duduk di mana pun dan merusak seluruh pembaca Cells.
                if (set.Count == 0)
                {
                    set.Add(Vector2Int.zero);
                }

                WriteCells(set.OrderBy(c => c.y).ThenBy(c => c.x).ToArray());
            }

            EditorGUILayout.Space(2);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (def.HasCustomShape &&
                    GUILayout.Button("Balik ke preset Shape", GUILayout.Height(24f)))
                {
                    WriteCells(System.Array.Empty<Vector2Int>());
                }

                if (GUILayout.Button("Geser ke pojok (0,0)", GUILayout.Height(24f)))
                {
                    var current = new HashSet<Vector2Int>(((PieceDefinition)target).Cells);
                    int minX = current.Min(c => c.x);
                    int minY = current.Min(c => c.y);
                    if (minX != 0 || minY != 0)
                    {
                        WriteCells(current.Select(c => new Vector2Int(c.x - minX, c.y - minY))
                            .OrderBy(c => c.y).ThenBy(c => c.x).ToArray());
                    }
                }
            }

            DrawArtPreview(def);
        }

        void WriteCells(Vector2Int[] cells)
        {
            var prop = serializedObject.FindProperty("CustomCells");
            prop.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).vector2IntValue = cells[i];
            }

            serializedObject.ApplyModifiedProperties();
            Repaint();
        }

        /// <summary>Art di atas footprint — cek pas/melencengnya tanpa masuk play mode.</summary>
        void DrawArtPreview(PieceDefinition def)
        {
            var sprite = def.Art != null ? def.Art : def.Icon;
            if (sprite == null) return;

            var cells = def.Cells;
            int maxX = 0, maxY = 0;
            foreach (var c in cells)
            {
                if (c.x > maxX) maxX = c.x;
                if (c.y > maxY) maxY = c.y;
            }

            float pw = (maxX + 1) * (CellPx + GapPx) - GapPx;
            float ph = (maxY + 1) * (CellPx + GapPx) - GapPx;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(
                def.Art != null ? "Pratinjau Art di papan" : "Pratinjau Icon (Art kosong)",
                EditorStyles.miniBoldLabel);

            var area = GUILayoutUtility.GetRect(pw + 16f, ph + 10f);
            float ox = area.x + Mathf.Max(8f, (area.width - pw) * 0.5f);
            float oy = area.y + 4f;

            var faint = new Color(def.Color.r, def.Color.g, def.Color.b, 0.25f);
            foreach (var c in cells)
            {
                EditorGUI.DrawRect(new Rect(ox + c.x * (CellPx + GapPx),
                    oy + (maxY - c.y) * (CellPx + GapPx), CellPx, CellPx), faint);
            }

            var tex = sprite.texture;
            var tr = sprite.textureRect;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height,
                tr.width / tex.width, tr.height / tex.height);

            float artAspect = tr.width / tr.height;
            float boxAspect = pw / ph;
            Rect artArea = artAspect > boxAspect
                ? new Rect(ox, oy + (ph - pw / artAspect) * 0.5f, pw, pw / artAspect)
                : new Rect(ox + (pw - ph * artAspect) * 0.5f, oy, ph * artAspect, ph);

            GUI.DrawTextureWithTexCoords(artArea, tex, uv, true);
        }
    }
}
