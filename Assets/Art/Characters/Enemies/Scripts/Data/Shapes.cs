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
        Fork,

        // =================================================================================
        //  Gelombang ketiga. Ditambahkan supaya bentuk piece BERAGAM, bukan satu bentuk per
        //  bintang — permintaan pemilik project: "gw mau bentuknya bisa beda-beda, gak harus
        //  ada rule, se aneh apa pun bentuk gridnya itu posibel."
        //
        //  Dua di antaranya TERPUTUS (Diag3, Twin) — petaknya tidak bersentuhan sama sekali.
        //  Itu sah: papan cuma memeriksa tiap petak satu per satu, tidak pernah menuntut
        //  bentuknya menyatu. Yang terputus justru paling menyulitkan dipak, dan itu gunanya.
        //
        //  Semua tetap muat di kotak 3x3 dan maksimal 9 petak. Itu batas keras, bukan gaya:
        //  siluet codex kotak 3x3 dan kolam petak piece tercecer berukuran sembilan.
        // =================================================================================

        /// <summary>2 petak, DIAGONAL dan terputus. Sepasang petak yang cuma bersentuhan di sudut.</summary>
        Diag2,

        /// <summary>3 petak diagonal, terputus semua. Paling sulit dipak dari seluruh bentuk 3 petak.</summary>
        Diag3,

        /// <summary>4 petak — cermin dari SBend. Rotasi tidak pernah bisa menghasilkan cermin.</summary>
        Zee,

        /// <summary>4 petak — cermin dari Ell.</summary>
        Jay,

        /// <summary>4 petak — T pendek yang batangnya di tengah sisi panjang.</summary>
        Nub,

        /// <summary>5 petak — empat sudut plus pusat. Menyisakan empat petak tunggal di sisi-sisinya.</summary>
        Ex,

        /// <summary>5 petak — anak panah.</summary>
        Arrow,

        /// <summary>5 petak — kilat zig-zag.</summary>
        Bolt,

        /// <summary>6 petak — dua tiang terpisah, tidak pernah bersentuhan.</summary>
        Twin,

        /// <summary>6 petak — tangga naik.</summary>
        Stair,

        /// <summary>7 petak — sisir E: tiga gigi dari satu punggung.</summary>
        Comb,

        /// <summary>7 petak — pusaran yang melingkar setengah putaran.</summary>
        Spiral,

        /// <summary>8 petak — 3x3 kurang satu petak TENGAH SISI, bukan sudut.</summary>
        Maw
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

        // ---- gelombang ketiga ----

        //   . X
        //   X .
        static readonly Vector2Int[] Diag2Cells =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 1)
        };

        //   . . X
        //   . X .
        //   X . .
        static readonly Vector2Int[] Diag3Cells =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(2, 2)
        };

        //   X X .
        //   . X X
        static readonly Vector2Int[] ZeeCells =
        {
            new Vector2Int(1, 0), new Vector2Int(2, 0),
            new Vector2Int(0, 1), new Vector2Int(1, 1)
        };

        // CERMIN dari Ell, bukan putarannya — huruf L dan J memang dua tetromino berbeda, dan
        // memutar L berapa kali pun tidak pernah menghasilkan J.
        //   . X
        //   . X
        //   X X
        static readonly Vector2Int[] JayCells =
        {
                                  new Vector2Int(1, 2),
                                  new Vector2Int(1, 1),
            new Vector2Int(0, 0), new Vector2Int(1, 0)
        };

        // TERPUTUS. Di dalam kotak 3x3 cuma ada enam tetromino yang muat (I butuh 4 melintang),
        // dan keenamnya sudah terpakai — jadi bentuk 4 petak ketujuh HARUS yang tidak menyatu.
        //   X . X
        //   X . X
        static readonly Vector2Int[] NubCells =
        {
            new Vector2Int(0, 1),                       new Vector2Int(2, 1),
            new Vector2Int(0, 0),                       new Vector2Int(2, 0)
        };

        //   X . X
        //   . X .
        //   X . X
        static readonly Vector2Int[] ExCells =
        {
            new Vector2Int(0, 2),                       new Vector2Int(2, 2),
                                  new Vector2Int(1, 1),
            new Vector2Int(0, 0),                       new Vector2Int(2, 0)
        };

        //   . . X
        //   X X X
        //   . . X   -> kepala panah menumpu di SATU sisi, bukan simetris seperti Cross
        static readonly Vector2Int[] ArrowCells =
        {
                                                        new Vector2Int(2, 2),
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1),
                                                        new Vector2Int(2, 0)
        };

        //   . X X
        //   . X .
        //   X X .
        static readonly Vector2Int[] BoltCells =
        {
                                  new Vector2Int(1, 2), new Vector2Int(2, 2),
                                  new Vector2Int(1, 1),
            new Vector2Int(0, 0), new Vector2Int(1, 0)
        };

        //   X . X
        //   X . X
        //   X . X
        static readonly Vector2Int[] TwinCells =
        {
            new Vector2Int(0, 2),                       new Vector2Int(2, 2),
            new Vector2Int(0, 1),                       new Vector2Int(2, 1),
            new Vector2Int(0, 0),                       new Vector2Int(2, 0)
        };

        //   . . X
        //   . X X
        //   X X .
        static readonly Vector2Int[] StairCells =
        {
                                                        new Vector2Int(2, 2),
                                  new Vector2Int(1, 1), new Vector2Int(2, 1),
            new Vector2Int(0, 0), new Vector2Int(1, 0)
        };

        //   X X X
        //   X X .
        //   X . X   -> coakannya di dua tempat berbeda, jadi tidak pernah sama dengan Zed
        //               diputar berapa kali pun
        static readonly Vector2Int[] CombCells =
        {
            new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2),
            new Vector2Int(0, 1), new Vector2Int(1, 1),
            new Vector2Int(0, 0),                       new Vector2Int(2, 0)
        };

        //   X X X
        //   X . X
        //   X X .
        static readonly Vector2Int[] SpiralCells =
        {
            new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2),
            new Vector2Int(0, 1),                       new Vector2Int(2, 1),
            new Vector2Int(0, 0), new Vector2Int(1, 0)
        };

        //   X X X
        //   X X X
        //   X . X   -> coakan di TENGAH sisi, jadi lubangnya terjepit dua sisi
        static readonly Vector2Int[] MawCells =
        {
            new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2),
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1),
            new Vector2Int(0, 0),                       new Vector2Int(2, 0)
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

                case ShapeKind.Diag2: return Diag2Cells;
                case ShapeKind.Diag3: return Diag3Cells;
                case ShapeKind.Zee: return ZeeCells;
                case ShapeKind.Jay: return JayCells;
                case ShapeKind.Nub: return NubCells;
                case ShapeKind.Ex: return ExCells;
                case ShapeKind.Arrow: return ArrowCells;
                case ShapeKind.Bolt: return BoltCells;
                case ShapeKind.Twin: return TwinCells;
                case ShapeKind.Stair: return StairCells;
                case ShapeKind.Comb: return CombCells;
                case ShapeKind.Spiral: return SpiralCells;
                case ShapeKind.Maw: return MawCells;

                default: return DotCells;
            }
        }

        /// <summary>
        /// Label pendek untuk tooltip, supaya bentuknya tidak perlu hidup di dalam prosa.
        ///
        /// Kuncinya diturunkan dari nama enum-nya, bukan ditulis satu per satu: bentuk baru cuma
        /// perlu satu baris di enum dan satu baris di tabel bahasa, dan tidak ada switch ketiga
        /// yang bisa lupa diperbarui. Nama enum sendiri jadi cadangan, jadi bentuk yang belum
        /// punya terjemahan tampil sebagai "Cross", bukan sebagai kekosongan.
        /// </summary>
        public static string NameOf(ShapeKind kind)
            => Loc.T("shape." + kind.ToString().ToLowerInvariant(), kind.ToString());

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
