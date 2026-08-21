using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Menggambar bentuk grid sebuah piece dengan MENGKLIK petaknya, dan meluruskan art-nya di
    /// atas bentuk itu sampai pas — dalam satu jendela.
    ///
    /// Permintaan pemilik project: "buatin gw grid editor jadi gw bisa ngedit bentuk-bentuk dari
    /// gridnya", dan menyusul "gw bakal bikin bentuk asetnya menyerupai gridnya, jadi sediakan
    /// image juga yang bisa gw geser agar bentuknya sama kaya gridnya".
    ///
    /// Dua-duanya harus ada di SATU layar, dan itu bukan kenyamanan: art digeser supaya cocok
    /// dengan bentuk gridnya, jadi menggeser art tanpa melihat gridnya adalah menebak. Petak yang
    /// menyala dan art yang membentang digambar di kotak yang sama persis dengan yang dipakai
    /// papan sungguhan.
    ///
    /// Yang digambar di sini ditulis ke <see cref="PieceDefinition.CustomCells"/>, dan begitu
    /// terisi, <see cref="FootprintPass"/> berhenti menyentuh piece itu selamanya — bentuk
    /// gambaran tangan adalah keputusan, dan generator tidak boleh membatalkan keputusan.
    /// </summary>
    public class ShapeEditorWindow : EditorWindow
    {
        const int Grid = 3;
        const float CellPx = 58f;
        const float Gap = 3f;

        PieceDefinition _piece;
        Vector2 _scroll;
        bool _showArt = true;
        bool _snap = true;

        readonly HashSet<Vector2Int> _cells = new HashSet<Vector2Int>();

        [MenuItem("Tools/Grimoire/Editor Bentuk Grid")]
        public static void Open()
        {
            var w = GetWindow<ShapeEditorWindow>("Bentuk Grid");
            w.minSize = new Vector2(430f, 560f);
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUI.BeginChangeCheck();
            _piece = (PieceDefinition)EditorGUILayout.ObjectField(
                "Piece", _piece, typeof(PieceDefinition), false);

            if (EditorGUI.EndChangeCheck()) Load();

            if (_piece == null)
            {
                EditorGUILayout.HelpBox(
                    "Seret sebuah Piece ke atas, atau pilih asetnya di Project window.\n\n" +
                    "Piece ada di Assets/GameData/Pieces.",
                    MessageType.Info);

                EditorGUILayout.EndScrollView();
                return;
            }

            Header();
            EditorGUILayout.Space(6f);

            DrawBoard();

            EditorGUILayout.Space(8f);
            Tools();

            EditorGUILayout.Space(10f);
            ArtControls();

            EditorGUILayout.Space(12f);
            SaveBar();

            EditorGUILayout.EndScrollView();
        }

        void Header()
        {
            EditorGUILayout.LabelField(
                $"{_piece.DisplayName}   {Shapes.StarText(_piece.Stars)}",
                EditorStyles.boldLabel);

            string source = _piece.HasCustomShape
                ? "GAMBARAN TANGAN — generator Footprint tidak akan menimpanya"
                : $"dari generator: {Shapes.NameOf(_piece.Shape)}";

            EditorGUILayout.LabelField($"{_cells.Count} petak   ·   {source}");

            // Tangga ukuran yang sedang berlaku, supaya yang menggambar tahu ia sedang keluar
            // jalur atau tidak — tanpa harus membuka FootprintPass untuk mengingatnya.
            string wanted = _piece.Stars <= 3
                ? $"bintang {_piece.Stars} biasanya {_piece.Stars} petak (kadang {_piece.Stars + 1})"
                : $"bintang {_piece.Stars}: bebas, jumlahnya ikut seberapa OP";

            EditorGUILayout.LabelField(wanted, EditorStyles.miniLabel);
        }

        /// <summary>
        /// Petak yang bisa diklik, dengan art dibentangkan di atasnya persis seperti di papan.
        /// </summary>
        void DrawBoard()
        {
            float side = Grid * CellPx + (Grid - 1) * Gap;
            Rect box = GUILayoutUtility.GetRect(side, side, GUILayout.ExpandWidth(false));

            // Art digambar DULU kalau ia di belakang, supaya urutannya sama dengan papan.
            if (_showArt && _piece.Art != null && _piece.ArtBehindCells) DrawArt(box);

            for (int y = 0; y < Grid; y++)
            {
                for (int x = 0; x < Grid; x++)
                {
                    // y dibalik: petak (0,0) ada di KIRI BAWAH di papan, sementara GUI menghitung
                    // dari kiri atas. Menggambarnya apa adanya menghasilkan bentuk yang terbalik
                    // vertikal terhadap yang benar-benar muncul di papan.
                    var rect = new Rect(
                        box.x + x * (CellPx + Gap),
                        box.y + (Grid - 1 - y) * (CellPx + Gap),
                        CellPx, CellPx);

                    var cell = new Vector2Int(x, y);
                    bool on = _cells.Contains(cell);

                    EditorGUI.DrawRect(rect, on
                        ? new Color(_piece.Color.r, _piece.Color.g, _piece.Color.b, 0.85f)
                        : new Color(0.18f, 0.19f, 0.23f, 1f));

                    // Garis tepi tipis: tanpa itu dua petak bersebelahan yang menyala menyatu jadi
                    // satu bidang, dan yang menggambar tidak bisa menghitung berapa petak terpakai.
                    Handles.color = new Color(0f, 0f, 0f, 0.45f);
                    Handles.DrawAAPolyLine(2f,
                        new Vector3(rect.xMin, rect.yMin), new Vector3(rect.xMax, rect.yMin),
                        new Vector3(rect.xMax, rect.yMax), new Vector3(rect.xMin, rect.yMax),
                        new Vector3(rect.xMin, rect.yMin));

                    if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) Toggle(cell);
                }
            }

            if (_showArt && _piece.Art != null && !_piece.ArtBehindCells) DrawArt(box);
        }

        /// <summary>
        /// Art dibentangkan memakai OFFSET dan UKURAN yang tersimpan di piece — bukan sekadar
        /// dipaskan ke kotak. Kalau ia dipaskan otomatis di sini, angka yang disetel tidak akan
        /// pernah kelihatan efeknya sampai masuk play mode, dan seluruh gunanya jendela ini hilang.
        /// </summary>
        void DrawArt(Rect box)
        {
            Vector2 size = _piece.ArtSize;
            if (size.x <= 0f || size.y <= 0f) size = BoundsInCells();

            float w = size.x * CellPx + Mathf.Max(0f, size.x - 1f) * Gap;
            float h = size.y * CellPx + Mathf.Max(0f, size.y - 1f) * Gap;

            float ox = _piece.ArtOffset.x * (CellPx + Gap);
            float oy = _piece.ArtOffset.y * (CellPx + Gap);

            var rect = new Rect(box.x + ox, box.yMax - h - oy, w, h);

            var old = GUI.matrix;

            if (!Mathf.Approximately(_piece.ArtRotation, 0f))
            {
                GUIUtility.RotateAroundPivot(_piece.ArtRotation, rect.center);
            }

            GUI.DrawTexture(rect, _piece.Art.texture, ScaleMode.StretchToFill, true);
            GUI.matrix = old;
        }

        void Tools()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Kosongkan")) _cells.Clear();

                if (GUILayout.Button("Isi 3x3"))
                {
                    _cells.Clear();
                    for (int y = 0; y < Grid; y++)
                    for (int x = 0; x < Grid; x++) _cells.Add(new Vector2Int(x, y));
                }

                if (GUILayout.Button("Putar 90°")) Rotate();

                if (GUILayout.Button("Cermin")) Mirror();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ambil dari ShapeKind"))
                {
                    _cells.Clear();
                    foreach (var c in Shapes.Of(_piece.Shape)) _cells.Add(c);
                }

                _snap = GUILayout.Toggle(_snap, "Rapatkan ke pojok", EditorStyles.miniButton);
                _showArt = GUILayout.Toggle(_showArt, "Tampilkan art", EditorStyles.miniButton);
            }
        }

        void ArtControls()
        {
            EditorGUILayout.LabelField("Art di papan", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            var art = (Sprite)EditorGUILayout.ObjectField("Gambar", _piece.Art, typeof(Sprite), false);
            var offset = EditorGUILayout.Vector2Field("Geser (petak)", _piece.ArtOffset);
            var size = EditorGUILayout.Vector2Field("Ukuran (petak)", _piece.ArtSize);
            float rot = EditorGUILayout.Slider("Putar", _piece.ArtRotation, -180f, 180f);
            bool behind = EditorGUILayout.Toggle("Di belakang petak", _piece.ArtBehindCells);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_piece, "Setel art piece");
                _piece.Art = art;
                _piece.ArtOffset = offset;
                _piece.ArtSize = size;
                _piece.ArtRotation = rot;
                _piece.ArtBehindCells = behind;
                EditorUtility.SetDirty(_piece);
            }

            // Skala tunggal di atas Ukuran: satu geseran membesar-kecilkan proporsional.
            // Nilainya DIBAKAR ke ArtSize (bbox bentuk x skala) — tidak ada field baru di
            // data, dan renderer tidak perlu tahu slider ini pernah ada.
            EditorGUI.BeginChangeCheck();

            var bbox = BoundsInCells();
            float current = _piece.ArtSize.x <= 0f || bbox.x <= 0f
                ? 1f
                : _piece.ArtSize.x / bbox.x;
            float scale = EditorGUILayout.Slider("Skala", current, 0.25f, 3f);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_piece, "Skala art piece");
                _piece.ArtSize = bbox * scale;
                EditorUtility.SetDirty(_piece);
            }

            if (GUILayout.Button("Paskan ukuran ke bentuk"))
            {
                Undo.RecordObject(_piece, "Paskan art");
                _piece.ArtSize = BoundsInCells();
                _piece.ArtOffset = Vector2.zero;
                EditorUtility.SetDirty(_piece);
            }

            EditorGUILayout.LabelField(
                "Geser 0,5 = setengah petak. Ukuran nol = seukuran kotak pembatas bentuknya.",
                EditorStyles.miniLabel);
        }

        void SaveBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _cells.Count > 0;

                if (GUILayout.Button("SIMPAN BENTUK", GUILayout.Height(28f))) Save();

                GUI.enabled = _piece.HasCustomShape;

                if (GUILayout.Button("Kembalikan ke generator", GUILayout.Height(28f)))
                {
                    Undo.RecordObject(_piece, "Hapus bentuk tangan");
                    _piece.CustomCells = null;
                    EditorUtility.SetDirty(_piece);
                    AssetDatabase.SaveAssets();
                    Load();
                }

                GUI.enabled = true;
            }

            if (_cells.Count == 0)
            {
                EditorGUILayout.HelpBox("Bentuk kosong tidak bisa disimpan — piece tanpa petak " +
                                        "tidak bisa didudukkan di mana pun.", MessageType.Warning);
            }
        }

        // =================================================================================
        //  isi
        // =================================================================================

        void Toggle(Vector2Int cell)
        {
            if (!_cells.Remove(cell)) _cells.Add(cell);
            Repaint();
        }

        void Rotate()
        {
            var list = new List<Vector2Int>(_cells);
            var turned = Shapes.Rotate(list.ToArray(), 1);

            _cells.Clear();
            foreach (var c in turned) _cells.Add(c);
        }

        void Mirror()
        {
            int maxX = 0;
            foreach (var c in _cells) maxX = Mathf.Max(maxX, c.x);

            var flipped = new List<Vector2Int>();
            foreach (var c in _cells) flipped.Add(new Vector2Int(maxX - c.x, c.y));

            _cells.Clear();
            foreach (var c in flipped) _cells.Add(c);
        }

        /// <summary>Kotak pembatas bentuknya, dalam petak. Minimal 1x1 supaya art tidak pernah nol.</summary>
        Vector2 BoundsInCells()
        {
            if (_cells.Count == 0) return Vector2.one;

            int maxX = 0, maxY = 0;
            foreach (var c in _cells)
            {
                maxX = Mathf.Max(maxX, c.x);
                maxY = Mathf.Max(maxY, c.y);
            }

            return new Vector2(maxX + 1, maxY + 1);
        }

        void Load()
        {
            _cells.Clear();
            if (_piece == null) return;

            foreach (var c in _piece.Cells) _cells.Add(c);
        }

        void Save()
        {
            var list = new List<Vector2Int>(_cells);

            // Dirapatkan ke pojok sebelum disimpan. Bentuk yang mengambang jauh dari titik nol
            // tetap SAH secara aturan, dan tetap salah: seluruh sistem lain — pratinjau evolusi,
            // ikon, jangkauan penempatan — mengukur dari petak nol, jadi bentuk yang mengambang
            // membawa petak kosong ke mana pun ia diletakkan.
            if (_snap)
            {
                int minX = int.MaxValue, minY = int.MaxValue;
                foreach (var c in list)
                {
                    minX = Mathf.Min(minX, c.x);
                    minY = Mathf.Min(minY, c.y);
                }

                for (int i = 0; i < list.Count; i++) list[i] -= new Vector2Int(minX, minY);
            }

            Undo.RecordObject(_piece, "Simpan bentuk grid");
            _piece.CustomCells = list.ToArray();
            EditorUtility.SetDirty(_piece);
            AssetDatabase.SaveAssets();

            Load();
            Debug.Log($"[BentukGrid] '{_piece.DisplayName}' disimpan: {list.Count} petak. " +
                      "Footprint by Rarity tidak akan menimpanya lagi.", _piece);
        }

        /// <summary>Membuka jendela ini langsung dari piece yang sedang dipilih di Project window.</summary>
        [MenuItem("CONTEXT/PieceDefinition/Edit Bentuk Grid")]
        static void FromContext(MenuCommand command)
        {
            var w = GetWindow<ShapeEditorWindow>("Bentuk Grid");
            w._piece = (PieceDefinition)command.context;
            w.Load();
            w.Show();
        }
    }
}
