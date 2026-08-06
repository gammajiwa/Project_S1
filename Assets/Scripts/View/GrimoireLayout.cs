using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Every pixel measurement and screen-space rect for the in-run HUD. Pure geometry: no canvas,
    /// no state, nothing to mock. Split out of GrimoireUI so the numbers live in one readable place
    /// instead of being scattered through two thousand lines of drawing code.
    ///
    /// GrimoireUI pulls these in with <c>using static</c>, so call sites read exactly as before.
    /// </summary>
    public static class GrimoireLayout
    {
        // ---------- grimoire grid ----------
        public const int CellSize = 40;
        public const int CellGap = 3;
        public const int Margin = 20;
        public const int SkillInset = 8;

        // ---------- right-hand column ----------
        public const int BagCell = 34;
        public const int BagGap = 3;
        public const int BagY = 20;

        public const int SellW = 182;
        public const int SellH = 40;
        // Clears the 4x4 bag, whose top row plus its label reach y=190.
        public const int SellY = 196;

        // Wide enough for "1. Greater Fireball   34.0 dmg   32.4 dps   1.05s   17 mana".
        // Icon strips, stacked straight under the mana bar (which ends at -90).
        public const float StripIcon = 26f;
        public const float StripBuffY = -96f;
        public const float StripDebuffY = -128f;
        public const float StripAilmentY = -160f;

        public const int SpellPanelW = 380;
        public const int CooldownDiameter = 26;

        public const int SpeedButtonW = 58;
        public const int SpeedButtonH = 34;

        public const int StartButtonW = 280;
        public const int StartButtonH = 56;

        // Loose pieces draw at the exact grid scale so nothing resizes when placed.
        public const int LooseCellSize = CellSize;
        public const int LooseCellGap = CellGap;

        // ---------- shop / recipe panel ----------
        public const int ShopSlotW = 196;
        public const int ShopSlotH = 148;
        public const int PanelW = 632;
        public const int PanelH = 372;

        // ---------- shapes ----------

        public static void ShapeBounds(Vector2Int[] shape, out int w, out int h)
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

        /// <summary>
        /// The cell inside a shape that sits under the cursor. Placement and the on-cursor preview
        /// both use this, otherwise the ghost and the real footprint drift apart.
        /// </summary>
        public static Vector2Int AnchorOffset(PieceDefinition def, int rot)
        {
            ShapeBounds(Shapes.Rotate(def.Cells, rot), out int w, out int h);
            return new Vector2Int((w - 1) / 2, (h - 1) / 2);
        }

        public static Vector2 PieceSize(Vector2Int[] shape)
        {
            ShapeBounds(shape, out int w, out int h);
            float step = LooseCellSize + LooseCellGap;
            return new Vector2(w * step - LooseCellGap, h * step - LooseCellGap);
        }

        // ---------- grid & bag ----------

        public static Vector2 CellAnchor(int x, int y) =>
            new Vector2(Margin + x * (CellSize + CellGap), Margin + y * (CellSize + CellGap));

        public static float GridTop() => Margin + Grimoire.Height * (CellSize + CellGap);

        public static float RightX() => Margin + Grimoire.Width * (CellSize + CellGap) + 12;

        public static Vector2 BagAnchor(int x, int y) =>
            new Vector2(RightX() + x * (BagCell + BagGap), BagY + y * (BagCell + BagGap));

        public static Rect SellRect() => new Rect(RightX(), SellY, SellW, SellH);

        public static Vector2Int ScreenToCell(Vector2 mouse)
        {
            float step = CellSize + CellGap;
            int x = Mathf.FloorToInt((mouse.x - Margin) / step);
            int y = Mathf.FloorToInt((mouse.y - Margin) / step);

            if (x < 0 || x >= Grimoire.Width || y < 0 || y >= Grimoire.Height) return new Vector2Int(-1, -1);

            // Reject the gap between cells, so a click on the seam does not snap to a neighbour.
            float offX = (mouse.x - Margin) - x * step;
            float offY = (mouse.y - Margin) - y * step;
            if (offX > CellSize || offY > CellSize) return new Vector2Int(-1, -1);

            return new Vector2Int(x, y);
        }

        public static Vector2Int ScreenToBagCell(Vector2 mouse)
        {
            float step = BagCell + BagGap;
            int x = Mathf.FloorToInt((mouse.x - RightX()) / step);
            int y = Mathf.FloorToInt((mouse.y - BagY) / step);

            if (x < 0 || x >= Backpack.Width || y < 0 || y >= Backpack.Height) return new Vector2Int(-1, -1);

            float offX = (mouse.x - RightX()) - x * step;
            float offY = (mouse.y - BagY) - y * step;
            if (offX > BagCell || offY > BagCell) return new Vector2Int(-1, -1);

            return new Vector2Int(x, y);
        }

        // ---------- buttons & panels ----------

        public static Rect SpeedRect(int index, int count)
        {
            float right = Screen.width - (Margin + (count - 1 - index) * (SpeedButtonW + 6));
            float top = Screen.height - Margin;
            return new Rect(right - SpeedButtonW, top - SpeedButtonH, SpeedButtonW, SpeedButtonH);
        }

        public static Rect StartButtonRect()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f + 120f;
            return new Rect(cx - StartButtonW * 0.5f, cy - StartButtonH * 0.5f, StartButtonW, StartButtonH);
        }

        public static Rect PanelRect() =>
            new Rect((Screen.width - PanelW) * 0.5f, (Screen.height - PanelH) * 0.5f, PanelW, PanelH);

        public static Rect ShopSlotRect(int i)
        {
            var panel = PanelRect();
            int col = i % 3;
            int row = i / 3;

            float x = panel.xMin + 12f + col * (ShopSlotW + 8f);
            float y = panel.yMax - 46f - (row + 1) * ShopSlotH - row * 8f;
            return new Rect(x, y, ShopSlotW, ShopSlotH);
        }

        public static Rect RerollRect()
        {
            var panel = PanelRect();
            return new Rect(panel.center.x - 120f, panel.yMin + 12f, 240f, 34f);
        }

        public static Rect ShopButtonRect() => new Rect(RightX(), SellY + SellH + 8, 88, 32);

        public static Rect RecipeButtonRect() => new Rect(RightX() + 94, SellY + SellH + 8, 88, 32);

        // ---------- loose drops ----------

        /// <summary>Drops scatter anywhere on screen, clear of the left column and the HUD strip.</summary>
        public static Vector2 RandomScatterPos()
        {
            float left = RightX() + 60f;
            float right = Mathf.Max(left + 160f, Screen.width - 80f);
            float bottom = 70f;
            float top = Mathf.Max(bottom + 120f, Screen.height - 200f);

            return new Vector2(Random.Range(left, right), Random.Range(bottom, top));
        }

        public static Vector2 NearScatterPos(Vector2 near, int index)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f) + index * 1.1f;
            float dist = Random.Range(70f, 120f);

            var pos = near + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
            pos.x = Mathf.Clamp(pos.x, 70f, Mathf.Max(90f, Screen.width - 70f));
            pos.y = Mathf.Clamp(pos.y, 70f, Mathf.Max(90f, Screen.height - 190f));
            return pos;
        }
    }
}
