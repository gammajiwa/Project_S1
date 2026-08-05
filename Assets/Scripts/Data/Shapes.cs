using UnityEngine;

namespace Proto
{
    /// <summary>Named grid footprints. An enum keeps the Inspector readable — no raw arrays.</summary>
    public enum ShapeKind
    {
        Dot,
        Line2,
        Line3,
        Square,
        Big3,
        Corner
    }

    /// <summary>Pure geometry helpers. No game data lives here.</summary>
    public static class Shapes
    {
        static readonly Vector2Int[] DotCells = { new Vector2Int(0, 0) };

        static readonly Vector2Int[] Line2Cells = { new Vector2Int(0, 0), new Vector2Int(1, 0) };

        static readonly Vector2Int[] Line3Cells =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0)
        };

        static readonly Vector2Int[] SquareCells =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1)
        };

        static readonly Vector2Int[] Big3Cells =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1),
            new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2)
        };

        static readonly Vector2Int[] CornerCells =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1)
        };

        public static Vector2Int[] Of(ShapeKind kind)
        {
            switch (kind)
            {
                case ShapeKind.Line2: return Line2Cells;
                case ShapeKind.Line3: return Line3Cells;
                case ShapeKind.Square: return SquareCells;
                case ShapeKind.Big3: return Big3Cells;
                case ShapeKind.Corner: return CornerCells;
                default: return DotCells;
            }
        }

        /// <summary>Rotates a footprint by 90 degrees `rot` times and re-normalises it to origin.</summary>
        public static Vector2Int[] Rotate(Vector2Int[] cells, int rot)
        {
            rot = ((rot % 4) + 4) % 4;
            var result = new Vector2Int[cells.Length];

            for (int i = 0; i < cells.Length; i++)
            {
                var c = cells[i];
                for (int r = 0; r < rot; r++) c = new Vector2Int(c.y, -c.x);
                result[i] = c;
            }

            int minX = int.MaxValue, minY = int.MaxValue;
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i].x < minX) minX = result[i].x;
                if (result[i].y < minY) minY = result[i].y;
            }

            var offset = new Vector2Int(minX, minY);
            for (int i = 0; i < result.Length; i++) result[i] -= offset;
            return result;
        }

        public static string StarText(int stars)
        {
            switch (stars)
            {
                case 1: return "*";
                case 2: return "**";
                case 3: return "***";
                case 4: return "****";
                default: return "*****";
            }
        }
    }
}
