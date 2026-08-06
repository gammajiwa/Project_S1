using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Makes rarity cost board space.
    ///
    /// Before this, Doom Nova (4 stars) took three cells and Meteor (3 stars) took two — the same
    /// footprint as a starting Fireball. Rarity bought raw power and nothing else, so the only
    /// question a strong piece asked was "do I own it", never "what do I give up for it". With a
    /// 7x7 board and every skill costing two or three cells, there was nothing to give up.
    ///
    /// The ladder: 1 star takes 2-3 cells, 2 stars take 4, 3 stars take 5, 4 stars takes 7, and
    /// 5 stars take 8-9. The shapes get deliberately awkward on the way up — a Ring wastes its own
    /// centre, a Cup leaves a notch nothing else can reach — so the cost is not just area but the
    /// dead space each piece strands around it.
    ///
    /// Runes are left alone on purpose. They are the platform every skill has to stand on; making
    /// both layers awkward at the same time multiplies the difficulty instead of adding to it. They
    /// already scale anyway — the two 3x3 bases are the widest footprints in the game.
    ///
    /// Idempotent: matched by asset id.
    /// </summary>
    public static class FootprintPass
    {
        const string Root = "Assets/GameData";

        struct Footprint
        {
            public string Id;
            public ShapeKind Shape;
            public string Blurb;
        }

        /// <summary>
        /// Blurbs no longer describe the footprint. The tooltip reads the shape straight off the
        /// piece now, so prose like "3 long" could only ever drift out of date — and already had.
        /// </summary>
        static readonly Footprint[] Table =
        {
            // ---- 1 star: 2-3 cells. Cheap enough to still carpet the early board. ----
            new Footprint { Id = "fireball",       Shape = ShapeKind.Dot,
                Blurb = "Fast single bolt. One cell — it fits wherever nothing else will." },
            new Footprint { Id = "belatiberputar", Shape = ShapeKind.Line2,
                Blurb = "Spins around you. Close-range guard, and the source of BLEED." },
            new Footprint { Id = "minorheal",      Shape = ShapeKind.Dot,
                Blurb = "A small top-up on a long cooldown. One cell." },
            new Footprint { Id = "frostnova",      Shape = ShapeKind.Corner,
                Blurb = "Freezing burst centred on you. Applies CHILL." },
            new Footprint { Id = "arcbolt",        Shape = ShapeKind.Corner,
                Blurb = "Leaps from enemy to enemy. Applies SHOCK." },
            new Footprint { Id = "hujanapi",       Shape = ShapeKind.Line2,
                Blurb = "Rains fire onto the densest pack. Applies BURN." },
            new Footprint { Id = "sabetanpetir",   Shape = ShapeKind.Line3,
                Blurb = "Sweeps a line of enemies. Applies SHOCK." },
            new Footprint { Id = "kubanganracun",  Shape = ShapeKind.Corner,
                Blurb = "Leaves a lingering pool. Applies POISON." },
            new Footprint { Id = "pusaran",        Shape = ShapeKind.Line3,
                Blurb = "Drags enemies together. Applies DRAG - the setup for mass reactions." },

            // ---- 2 stars: 4 cells, all bent. ----
            // 2x2, and that is load-bearing: it is the hero's opening upgrade, and it has to fit
            // exactly on the 2x2 base the run starts with. A T-shape needs 3 cells across, so the
            // promised upgrade was quietly impossible — the recipe completed and could never seat.
            new Footprint { Id = "greaterfireball", Shape = ShapeKind.Square,
                Blurb = "EVOLVED. Far heavier than the bolt it came from, and no slower. " +
                        "Fills a 2x2 base exactly." },
            new Footprint { Id = "blizzard",        Shape = ShapeKind.SBend,
                Blurb = "EVOLVED. Wide freezing blast centred on you. Applies CHILL." },
            // 2x2 for the same reason Greater Fireball is: it is an opening upgrade, so it has to
            // land on the 2x2 base a run starts with. An L needs three cells of reach and would
            // have made the hero's advertised first choice impossible to actually take.
            new Footprint { Id = "steamburst",      Shape = ShapeKind.Square,
                Blurb = "EVOLVED. Scalding burst - fire and ice meeting at once. Fills a 2x2 base." },
            new Footprint { Id = "badaisalju",      Shape = ShapeKind.Square,
                Blurb = "EVOLVED. A freezing storm that stays on the ground. Applies CHILL." },
            new Footprint { Id = "greaterheal",     Shape = ShapeKind.SBend,
                Blurb = "EVOLVED. A real heal, but you wait a long time for it." },

            // ---- 3 stars: 5 cells. ----
            new Footprint { Id = "meteor",     Shape = ShapeKind.Cross,
                Blurb = "EVOLVED. Falls out of the sky onto the densest pack. Applies BURN." },
            new Footprint { Id = "prismabeku", Shape = ShapeKind.Cup,
                Blurb = "EVOLVED. Shattering ice, chaining through five enemies. Applies CHILL." },

            // ---- 4 stars: 7 cells. ----
            new Footprint { Id = "novakiamat", Shape = ShapeKind.Hook,
                Blurb = "EVOLVED. The heaviest blast in the book. Applies BURN." },

            // ---- 5 stars: 8-9 cells. A fifth of the board, each. ----
            new Footprint { Id = "cataclysm",    Shape = ShapeKind.Big3,
                Blurb = "EVOLVED. The sky opens. Nothing inside the crater survives. Applies BURN." },
            new Footprint { Id = "absolutezero", Shape = ShapeKind.Ring,
                Blurb = "EVOLVED. Reaches further than anything else in the book. Applies CHILL." },
            new Footprint { Id = "singularity",  Shape = ShapeKind.Chunk,
                Blurb = "EVOLVED. Drags the swarm into itself and grinds. Applies DRAG." }
        };

        /// <summary>
        /// The one-cell basics. A board where the smallest thing is two cells has no filler, so
        /// every leftover gap is dead space and packing stops being a puzzle. These are the pieces
        /// you slot into the holes everything else leaves behind.
        /// </summary>
        static readonly string[] SingleCell =
        {
            "fireball", "frostshard", "sparkbolt", "minorheal", "segelmata", "segelpenangkal"
        };

        /// <summary>
        /// Two-cell pieces. Every 1-star sigil lives here: a sigil is the thing you tuck into the
        /// gap an awkward skill left behind, and one that eats three cells is not filling a gap,
        /// it is competing for the board.
        /// </summary>
        static readonly string[] TwoCell =
        {
            "segelvitalitas", "segelsumur", "segelnadi", "segelbara", "tintamengalir",
            "emberburst", "staticfield", "hailstorm", "cinderpatch", "belatiberputar"
        };

        /// <summary>
        /// Shapes allowed at each rarity, so a piece this pass has no opinion about still lands on
        /// a footprint the right SIZE. Without it, anything authored by another pass kept whatever
        /// shape that pass happened to give it and the ladder quietly had holes in it.
        /// </summary>
        static readonly ShapeKind[][] ByStar =
        {
            null,
            new[] { ShapeKind.Line2, ShapeKind.Line2, ShapeKind.Line2, ShapeKind.Corner, ShapeKind.Line3 },
            new[] { ShapeKind.Tee, ShapeKind.SBend, ShapeKind.Ell, ShapeKind.Square },
            new[] { ShapeKind.Cross, ShapeKind.Cup, ShapeKind.Wedge },
            // 4 stars is where the footprints stop being merely big and start being obstructive:
            // an H strands two single dead cells, a Z strands two notches, an S offsets both ways.
            new[] { ShapeKind.Ess, ShapeKind.Fork, ShapeKind.Zed, ShapeKind.Aitch, ShapeKind.Hook },
            new[] { ShapeKind.Ring, ShapeKind.Chunk, ShapeKind.Big3 }
        };

        [MenuItem("Tools/Grimoire/Footprint by Rarity")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[FootprintPass] ContentDatabase.asset tidak ketemu.");
                return;
            }

            int changed = 0;

            // Named overrides first — these carry rewritten blurbs too.
            var named = new System.Collections.Generic.HashSet<string>();

            for (int i = 0; i < Table.Length; i++)
            {
                named.Add(Table[i].Id);

                var piece = db.ById(Table[i].Id);
                if (piece == null)
                {
                    Debug.LogWarning($"[FootprintPass] piece '{Table[i].Id}' tidak ada, dilewati.");
                    continue;
                }

                piece.Shape = Table[i].Shape;
                piece.Blurb = Table[i].Blurb;
                EditorUtility.SetDirty(piece);
                changed++;
            }

            changed += Force(db, named, SingleCell, ShapeKind.Dot);
            changed += Force(db, named, TwoCell, ShapeKind.Line2);

            // Everything else: sized by rarity, varied deterministically so a re-run is stable.
            for (int i = 0; i < db.Pieces.Count; i++)
            {
                var piece = db.Pieces[i];
                if (piece == null || piece.IsRune || named.Contains(piece.Id)) continue;

                var options = ByStar[Mathf.Clamp(piece.Stars, 1, 5)];
                piece.Shape = options[Mathf.Abs(piece.Id.GetHashCode()) % options.Length];
                EditorUtility.SetDirty(piece);
                changed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FootprintPass] {changed} piece diubah bentuknya.\n{Report(db)}");
            Selection.activeObject = db;
        }

        static int Force(ContentDatabase db, System.Collections.Generic.HashSet<string> named,
            string[] ids, ShapeKind shape)
        {
            int n = 0;

            foreach (var id in ids)
            {
                named.Add(id);

                var piece = db.ById(id);
                if (piece == null) continue;

                piece.Shape = shape;
                EditorUtility.SetDirty(piece);
                n++;
            }

            return n;
        }

        /// <summary>Cells per star, so the ladder can be read back instead of trusted.</summary>
        static string Report(ContentDatabase db)
        {
            var byStar = new SortedDictionary<int, List<string>>();

            for (int i = 0; i < db.Pieces.Count; i++)
            {
                var piece = db.Pieces[i];
                if (piece == null || piece.IsRune) continue;

                if (!byStar.ContainsKey(piece.Stars)) byStar[piece.Stars] = new List<string>();
                byStar[piece.Stars].Add($"{piece.DisplayName} {Shapes.NameOf(piece.Shape)}" +
                                        $"({piece.Cells.Length})");
            }

            var sb = new System.Text.StringBuilder();
            foreach (var pair in byStar)
            {
                sb.Append(pair.Key).Append(" bintang: ").Append(string.Join("  ", pair.Value.ToArray()))
                    .Append('\n');
            }

            return sb.ToString();
        }
    }
}
