using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Named grid footprints. An enum keeps the Inspector readable — no raw arrays.
    ///
    /// New entries are only ever APPENDED. The value is what gets serialised into every piece asset,
    /// so inserting one in the middle silently reshapes half the content.
    ///
    /// Every footprint fits inside a 3x3 box. That is a hard limit, not a style choice: the codex
    /// silhouette is a fixed 3x3 grid, the backpack is 4x3, and the loose-piece cell pool is sized
    /// at nine cells per piece. A four-wide shape breaks all three quietly.
    /// </summary>
    public enum ShapeKind
    {
        Dot,
        Line2,
        Line3,
        Square,
        Big3,
        Corner,

        // --- awkward footprints, added so rarity can cost board space ---
        SBend,
        Tee,
        Ell,
        Cross,
        Cup,
        Wedge,
        Slab,
        Hook,
        Ring,
        Chunk,

        // --- bentuk yang sengaja menyulitkan: berlubang di tengah, bukan cuma besar ---
        Zed,
        Aitch,
        Ess,
        Fork
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

        // 4 cells — an S/Z kink. Never sits flush against another one of itself.
        static readonly Vector2Int[] SBendCells =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0),
            new Vector2Int(1, 1), new Vector2Int(2, 1)
        };

        // 4 cells — T. The stem is what makes it hard to pack.
        static readonly Vector2Int[] TeeCells =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
            new Vector2Int(1, 1)
        };

        // 4 cells — L, standing tall.
        static readonly Vector2Int[] EllCells =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, 2)
        };

        // 5 cells — a plus. Wastes all four corners of its 3x3 box.
        static readonly Vector2Int[] CrossCells =
        {
            new Vector2Int(1, 0),
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1),
            new Vector2Int(1, 2)
        };

        // 5 cells — U. The notch has to be fed by something else, or wasted.
        static readonly Vector2Int[] CupCells =
        {
            new Vector2Int(0, 0), new Vector2Int(2, 0),
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1)
        };

        // 5 cells — V, hugging one corner.
        static readonly Vector2Int[] WedgeCells =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, 2)
        };

        // 6 cells — a solid 3x2 slab.
        static readonly Vector2Int[] SlabCells =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1)
        };

        // 7 cells — a C. Needs almost a full 3x3 of runes but leaves two corners dead.
        static readonly Vector2Int[] HookCells =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0),
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1),
            new Vector2Int(0, 2), new Vector2Int(1, 2)
        };

        // 8 cells — 3x3 with the middle punched out. The hole cannot be filled: a skill needs a free
        // rune cell under it, and nothing else can reach in there.
        static readonly Vector2Int[] RingCells =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
            new Vector2Int(0, 1),                       new Vector2Int(2, 1),
            new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2)
        };

        // 8 cells — 3x3 less one corner.
        static readonly Vector2Int[] ChunkCells =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1),
            new Vector2Int(0, 2), new Vector2Int(1, 2)
        };

        // 7 cells — a Z. Two full bars joined by one diagonal cell, so both notches have to be
        // fed by something else or wasted.
        //   X X X
        //   . . X
        //   X X X
        static readonly Vector2Int[] ZedCells =
        {
            new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2),
                                                        new Vector2Int(2, 1),
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0)
        };

        // 7 cells — an H. Both side notches are single dead cells that only a 1-cell piece can use.
        //   X . X
        //   X X X
        //   X . X
        static readonly Vector2Int[] AitchCells =
        {
            new Vector2Int(0, 2),                       new Vector2Int(2, 2),
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1),
            new Vector2Int(0, 0),                       new Vector2Int(2, 0)
        };

        // 6 cells — an S. Bar, side, bar, offset the other way.
        //   X X X
        //   X . .
        //   X X .
        static readonly Vector2Int[] EssCells =
        {
            new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2),
            new Vector2Int(0, 1),
            new Vector2Int(0, 0), new Vector2Int(1, 0)
        };

        // 6 cells — a trident. Three prongs off one spine; the two gaps are single cells.
        //   X . X
        //   X X X
        //   . . X
        static readonly Vector2Int[] ForkCells =
        {
            new Vector2Int(0, 2),                       new Vector2Int(2, 2),
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1),
                                                        new Vector2Int(2, 0)
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
                case ShapeKind.SBend: return SBendCells;
                case ShapeKind.Tee: return TeeCells;
                case ShapeKind.Ell: return EllCells;
                case ShapeKind.Cross: return CrossCells;
                case ShapeKind.Cup: return CupCells;
                case ShapeKind.Wedge: return WedgeCells;
                case ShapeKind.Slab: return SlabCells;
                case ShapeKind.Hook: return HookCells;
                case ShapeKind.Ring: return RingCells;
                case ShapeKind.Chunk: return ChunkCells;
                case ShapeKind.Zed: return ZedCells;
                case ShapeKind.Aitch: return AitchCells;
                case ShapeKind.Ess: return EssCells;
                case ShapeKind.Fork: return ForkCells;
                default: return DotCells;
            }
        }

        /// <summary>Short label for the tooltip, so the footprint does not have to live in prose.</summary>
        public static string NameOf(ShapeKind kind)
        {
            switch (kind)
            {
                case ShapeKind.Dot: return "TITIK";
                case ShapeKind.Line2: return "GARIS 2";
                case ShapeKind.Line3: return "GARIS 3";
                case ShapeKind.Square: return "KOTAK 2x2";
                case ShapeKind.Big3: return "BLOK 3x3";
                case ShapeKind.Corner: return "SIKU";
                case ShapeKind.SBend: return "BENGKOK S";
                case ShapeKind.Tee: return "HURUF T";
                case ShapeKind.Ell: return "HURUF L";
                case ShapeKind.Cross: return "SALIB";
                case ShapeKind.Cup: return "CAWAN";
                case ShapeKind.Wedge: return "SUDUT V";
                case ShapeKind.Slab: return "LEMPENG 3x2";
                case ShapeKind.Hook: return "HURUF C";
                case ShapeKind.Ring: return "CINCIN";
                case ShapeKind.Chunk: return "BONGKAH";
                case ShapeKind.Zed: return "HURUF Z";
                case ShapeKind.Aitch: return "HURUF H";
                case ShapeKind.Ess: return "HURUF S";
                case ShapeKind.Fork: return "TRISULA";
                default: return kind.ToString();
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
