using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Matematika penempatan ART papan sebuah piece — SATU sumber untuk papan in-run, tas,
    /// piece tercecer, dan preview starter di menu. Sebelumnya rumus ini hidup di GrimoireUI
    /// saja, dan layar lain yang menggambar piece memakai caranya sendiri: preview starter
    /// menyusutkan art 3 petak jadi ikon satu sel, dan pemilik project menangkapnya di detik
    /// pertama layar itu dibuka.
    /// </summary>
    public static class PieceArt
    {
        /// <summary>
        /// Kotak art untuk piece yang origin bentuknya duduk di <paramref name="shapeBottomLeft"/>
        /// (piksel pojok kiri-bawah sel origin). Matematikanya SAMA dengan jendela Bentuk Grid:
        /// ukuran nol = kotak pembatas bentuk, offset dalam satuan petak, putaran positif searah
        /// jarum jam (dibalik ke arah UGUI di <paramref name="angle"/>). Rotasi piece di papan
        /// ikut memutar art beserta offsetnya.
        ///
        /// False = piece tidak membawa Art; pemanggil jatuh ke jalur ikon.
        /// </summary>
        public static bool Layout(PieceDefinition def, Vector2 shapeBottomLeft, int rot,
            float cellPx, float gapPx, out Vector2 center, out Vector2 size, out float angle)
        {
            center = Vector2.zero;
            size = Vector2.zero;
            angle = 0f;

            if (def == null || def.Art == null) return false;

            float step = cellPx + gapPx;

            // bbox bentuk TANPA rotasi — ruang tempat Offset/Ukuran disetel di editor.
            int maxX0 = 0, maxY0 = 0;
            foreach (var c in def.Cells)
            {
                if (c.x > maxX0) maxX0 = c.x;
                if (c.y > maxY0) maxY0 = c.y;
            }

            Vector2 sizeCells = def.ArtSize;
            if (sizeCells.x <= 0f || sizeCells.y <= 0f)
                sizeCells = new Vector2(maxX0 + 1, maxY0 + 1);

            float w = sizeCells.x * cellPx + Mathf.Max(0f, sizeCells.x - 1f) * gapPx;
            float h = sizeCells.y * cellPx + Mathf.Max(0f, sizeCells.y - 1f) * gapPx;

            float bw0 = (maxX0 + 1) * cellPx + maxX0 * gapPx;
            float bh0 = (maxY0 + 1) * cellPx + maxY0 * gapPx;

            // Pusat art relatif pusat bbox — vektor inilah yang ikut berputar bersama piece.
            var local = new Vector2(
                def.ArtOffset.x * step + w * 0.5f - bw0 * 0.5f,
                def.ArtOffset.y * step + h * 0.5f - bh0 * 0.5f);

            var shape = Shapes.Rotate(def.Cells, rot);
            int maxXr = 0, maxYr = 0;
            foreach (var c in shape)
            {
                if (c.x > maxXr) maxXr = c.x;
                if (c.y > maxYr) maxYr = c.y;
            }

            float bwR = (maxXr + 1) * cellPx + maxXr * gapPx;
            float bhR = (maxYr + 1) * cellPx + maxYr * gapPx;

            // Shapes.Rotate memutar SEARAH JARUM JAM per langkah ((x,y) -> (y,-x)), jadi offset
            // dan sudut art HARUS ikut searah jarum jam juga. Dulu keduanya diputar berlawanan
            // (rumus CCW standar + sudut positif UGUI) — bentuk muter ke satu arah, gambarnya
            // ke arah sebaliknya, dan tiap piece ber-art yang dirotasi tampil acak-acakan.
            float rad = -rot * 90f * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            var turned = new Vector2(local.x * cos - local.y * sin,
                                     local.x * sin + local.y * cos);

            center = shapeBottomLeft + new Vector2(bwR * 0.5f, bhR * 0.5f) + turned;
            size = new Vector2(w, h);

            // Editor memutar SEARAH jarum jam untuk nilai positif; UGUI kebalikannya —
            // dan rotasi piece (searah jarum jam) berarti sudut UGUI-nya negatif.
            angle = -def.ArtRotation - rot * 90f;
            return true;
        }
    }
}
