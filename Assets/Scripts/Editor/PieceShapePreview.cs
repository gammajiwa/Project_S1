using UnityEditor;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Pratinjau bentuk grid + art di Inspector tiap <see cref="PieceDefinition"/>.
    ///
    /// Ada karena permintaan pemilik project: art skill baru harus MENGIKUTI footprint
    /// polyomino piece-nya, dan menilai "sudah pas atau belum" lewat angka Cells itu
    /// mustahil — yang bisa dinilai mata hanyalah gambar petak dengan art di atasnya.
    /// Di sinilah keduanya digambar bertumpuk, langsung saat asetnya diklik.
    ///
    /// Murni pembaca — tidak mengubah apa pun. Menaruh art-nya tetap kerja tangan
    /// user lewat field Icon biasa.
    /// </summary>
    [CustomEditor(typeof(PieceDefinition))]
    [CanEditMultipleObjects]
    public class PieceShapePreview : Editor
    {
        const float Cell = 56f;
        const float Gap = 4f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var def = (PieceDefinition)target;
            var cells = def.Cells;
            if (cells == null || cells.Length == 0) return;

            int maxX = 0, maxY = 0;
            foreach (var c in cells)
            {
                if (c.x > maxX) maxX = c.x;
                if (c.y > maxY) maxY = c.y;
            }

            int w = maxX + 1, h = maxY + 1;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(
                $"Bentuk: {def.Shape}  ({w}x{h}, {cells.Length} petak)", EditorStyles.boldLabel);

            float pw = w * Cell + (w - 1) * Gap;
            float ph = h * Cell + (h - 1) * Gap;
            var area = GUILayoutUtility.GetRect(pw + 16f, ph + 16f);
            float ox = area.x + (area.width - pw) * 0.5f;
            float oy = area.y + 8f;

            // Petak footprint. Baris y footprint menghitung KE ATAS (konvensi papan),
            // layar menghitung ke bawah — makanya dibalik, sama seperti codex.
            var fill = new Color(def.Color.r, def.Color.g, def.Color.b, 0.28f);
            var edge = new Color(def.Color.r, def.Color.g, def.Color.b, 0.9f);

            foreach (var c in cells)
            {
                var r = new Rect(ox + c.x * (Cell + Gap),
                    oy + (maxY - c.y) * (Cell + Gap), Cell, Cell);
                EditorGUI.DrawRect(r, fill);
                EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2f), edge);
                EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2f, r.width, 2f), edge);
                EditorGUI.DrawRect(new Rect(r.x, r.y, 2f, r.height), edge);
                EditorGUI.DrawRect(new Rect(r.xMax - 2f, r.y, 2f, r.height), edge);
            }

            // Art di atasnya, dibentangkan ke kotak pelukan footprint — persis
            // bagaimana ia akan menutupi petak-petaknya di papan.
            if (def.Icon != null)
            {
                var sprite = def.Icon;
                var tex = sprite.texture;
                var tr = sprite.textureRect;
                var uv = new Rect(tr.x / tex.width, tr.y / tex.height,
                    tr.width / tex.width, tr.height / tex.height);

                var artArea = new Rect(ox, oy, pw, ph);

                // Aspek art dipertahankan di dalam kotak footprint, biar kelihatan
                // apakah proporsinya cocok dengan bentuknya atau melenceng.
                float artAspect = tr.width / tr.height;
                float boxAspect = pw / ph;
                if (artAspect > boxAspect)
                {
                    float newH = pw / artAspect;
                    artArea = new Rect(ox, oy + (ph - newH) * 0.5f, pw, newH);
                }
                else
                {
                    float newW = ph * artAspect;
                    artArea = new Rect(ox + (pw - newW) * 0.5f, oy, newW, ph);
                }

                GUI.DrawTextureWithTexCoords(artArea, tex, uv, true);
            }
            else
            {
                EditorGUI.LabelField(new Rect(ox, oy + ph * 0.5f - 8f, pw, 16f),
                    "(Icon kosong)", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Petak = footprint di papan. Art dibentangkan ke kotak pelukan footprint " +
                "dengan aspek asli — kalau proporsinya tidak menutup petak dengan pas, " +
                "ganti/edit art-nya lalu lihat lagi di sini.", MessageType.None);
        }
    }
}
