using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Prefab penata art skill — SATU per piece, ditata TANGAN dengan gizmo Unity
    /// biasa (W geser, E putar, R skala; apa pun boleh). Saudara kandung
    /// <see cref="ShopRig"/>: kode tidak menata apa pun di sini, ia hanya MEMBACA
    /// hasil tataan.
    ///
    /// Kontraknya dikunci: <b>satu petak papan = satu unit dunia</b>. Gizmo
    /// menggambar footprint polyomino piece-nya sebagai petak satu unit; child
    /// <see cref="Art"/> ditata bebas sampai gambarnya duduk pas. Renderer papan
    /// membaca localPosition, localRotation, dan localScale milik Art lalu
    /// mengalikannya dengan CellSize saat menggambar di layar.
    ///
    /// Petak (0,0) footprint duduk di ORIGIN prefab; y footprint naik ke +y dunia.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Grimoire/Skill Art Rig")]
    public class SkillArtRig : MonoBehaviour
    {
        [Tooltip("Piece pemilik bentuk. Gizmo menggambar footprint milik piece INI.")]
        public PieceDefinition Piece;

        [Tooltip("Child pemegang SpriteRenderer art. Tata dengan tangan; kode membaca " +
                 "localPosition + localRotation + localScale-nya apa adanya.")]
        public Transform Art;

        [Header("Bantuan menata (editor saja)")]
        public bool ShowGuide = true;

        public Color GuideInk = new Color(1f, 0.72f, 0.35f, 1f);

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!ShowGuide || Piece == null) return;

            var cells = Piece.Cells;
            if (cells == null) return;

            var old = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            // Petak satu unit per sel, pusat di (x+0.5, y+0.5) — pojok kiri-bawah
            // footprint persis di origin prefab.
            Gizmos.color = GuideInk;
            foreach (var c in cells)
            {
                Gizmos.DrawWireCube(new Vector3(c.x + 0.5f, c.y + 0.5f, 0f),
                    new Vector3(1f, 1f, 0f));
            }

            // Garis dalam yang pudar — batas antar petak tetap terbaca di balik art.
            var faint = GuideInk;
            faint.a *= 0.35f;
            Gizmos.color = faint;
            foreach (var c in cells)
            {
                Gizmos.DrawWireCube(new Vector3(c.x + 0.5f, c.y + 0.5f, 0f),
                    new Vector3(0.92f, 0.92f, 0f));
            }

            Gizmos.matrix = old;
        }
#endif
    }
}
