using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// One codex cell. Undiscovered pieces still show their footprint as a silhouette — the player
    /// should be able to see that something is missing without learning what it is.
    /// </summary>
    public class CodexEntry : MonoBehaviour
    {
        /// <summary>Footprints never exceed 3x3, so the silhouette grid is a fixed 3x3.</summary>
        public const int ShapeGrid = 3;

        [SerializeField] Image _background;
        [SerializeField] TextMeshProUGUI _name;
        [SerializeField] TextMeshProUGUI _meta;

        [Tooltip("9 kotak, urutan kiri-atas ke kanan-bawah.")]
        [SerializeField] Image[] _shapeCells;

        [Header("Warna")]
        [SerializeField] Color _knownFill = new Color(0.098f, 0.098f, 0.129f, 0.95f);
        [SerializeField] Color _unknownFill = new Color(0.063f, 0.063f, 0.078f, 0.95f);
        [SerializeField] Color _knownText = Color.white;
        [SerializeField] Color _unknownText = new Color(0.45f, 0.45f, 0.5f, 1f);
        [SerializeField] Color _unknownCell = new Color(0.22f, 0.22f, 0.27f, 1f);

        public void Bind(PieceDefinition piece, bool known)
        {
            if (piece == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (_background != null) _background.color = known ? _knownFill : _unknownFill;

            if (_name != null)
            {
                _name.text = known ? piece.DisplayName : "? ? ?";
                _name.color = known ? _knownText : _unknownText;
            }

            if (_meta != null)
            {
                // Rarity is withheld until found: the star count alone would give the piece away.
                _meta.text = known ? Shapes.StarText(piece.Stars) + "   " + KindLabel(piece) : "";
                _meta.color = _unknownText;
            }

            DrawShape(piece, known ? piece.Color : _unknownCell);
        }

        void DrawShape(PieceDefinition piece, Color color)
        {
            if (_shapeCells == null) return;

            for (int i = 0; i < _shapeCells.Length; i++)
            {
                if (_shapeCells[i] != null) _shapeCells[i].enabled = false;
            }

            var cells = Shapes.Rotate(piece.Cells, 0);

            for (int i = 0; i < cells.Length; i++)
            {
                int x = cells[i].x;
                int y = cells[i].y;
                if (x < 0 || x >= ShapeGrid || y < 0 || y >= ShapeGrid) continue;

                // Grid layout fills top-down, but footprint y counts up — flip the row.
                int index = (ShapeGrid - 1 - y) * ShapeGrid + x;
                if (index < 0 || index >= _shapeCells.Length) continue;
                if (_shapeCells[index] == null) continue;

                _shapeCells[index].enabled = true;
                _shapeCells[index].color = color;
            }
        }

        static string KindLabel(PieceDefinition piece)
        {
            if (piece.IsRune) return "RUNE";
            return piece.IsPassive ? "SEGEL" : "SKILL";
        }
    }
}
