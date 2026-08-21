using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// The recipe search, shared by the board and the bag.
    ///
    /// Both grids ask the identical question — "are these ingredients here, unlocked, and touching"
    /// — and answer it against different placement rules: a skill on the board needs a rune under
    /// every cell, a skill in the bag needs nothing. Only the placement rule differs, so only the
    /// placement rule is injected.
    ///
    /// It is shared rather than copied because the preview and the merge MUST agree. They drifted
    /// apart once already, and the symptom was a gold connector that survived the wave without
    /// evolving — the preview said yes to a group the merge could not seat.
    /// </summary>
    public static class RecipeResolver
    {
        public delegate RuneInstance PlaceFunc(PieceDefinition def, Vector2Int origin, int rot);

        public delegate bool SeatCheck(PieceDefinition result, List<RuneInstance> group);

        /// <summary>
        /// Merges every complete group it can find. Returns a log line per merge.
        /// </summary>
        /// <param name="spill">
        /// Hasil yang tidak dapat tempat di grid ini. Evolusinya tetap terjadi — lihat
        /// <see cref="Grimoire.ResolveEvolutions"/> untuk alasannya.
        /// </param>
        /// <param name="evolved">Dipanggil untuk tiap hasil yang berhasil DUDUK di grid ini,
        /// membawa instance-nya — UI memainkan ritual reveal di footprint-nya. Hasil yang
        /// keluar lewat <paramref name="spill"/> tidak lewat sini.</param>
        public static List<string> Resolve(ContentDatabase db, List<RuneInstance> placed,
            System.Action rebuild, PlaceFunc place, SeatCheck couldSeat,
            System.Action<PieceDefinition, Vector2> spill = null,
            System.Action<RuneInstance> evolved = null)
        {
            var log = new List<string>();
            if (db == null) return log;

            var candidates = new List<RuneInstance>();
            var group = new List<RuneInstance>();

            bool changed = true;
            while (changed)
            {
                changed = false;

                for (int r = 0; r < db.Recipes.Count && !changed; r++)
                {
                    var recipe = db.Recipes[r];
                    if (recipe == null || recipe.Result == null) continue;

                    Collect(recipe, placed, null, null, candidates);
                    if (candidates.Count < recipe.Ingredients.Length) continue;

                    group.Clear();
                    if (!Search(recipe, candidates, group, 0, recipe.Ingredients.Length, true, null)) continue;

                    changed = Merge(group, recipe.Result, placed, rebuild, place, log, spill,
                        evolved);
                }
            }

            return log;
        }

        /// <summary>Groups sitting on the grid right now, for the connector lines.</summary>
        public static List<EvoPreview> Preview(ContentDatabase db, List<RuneInstance> placed,
            SeatCheck couldSeat)
        {
            var previews = new List<EvoPreview>();
            if (db == null) return previews;

            var used = new HashSet<RuneInstance>();
            var candidates = new List<RuneInstance>();
            var group = new List<RuneInstance>();

            for (int r = 0; r < db.Recipes.Count; r++)
            {
                var recipe = db.Recipes[r];
                if (recipe == null || recipe.Result == null) continue;

                Collect(recipe, placed, null, used, candidates);
                if (candidates.Count < 2) continue;

                group.Clear();

                if (Search(recipe, candidates, group, 0, recipe.Ingredients.Length, true, null))
                {
                    // Lengkap berarti berevolusi; yang ditanyakan couldSeat cuma di mana hasilnya
                    // mendarat — di grid ini, atau keluar untuk dipasang ulang.
                    var preview = Describe(group, true, recipe.Result.DisplayName);
                    preview.SpillsOut = !couldSeat(recipe.Result, group);

                    previews.Add(preview);
                    foreach (var g in group) used.Add(g);
                    continue;
                }

                for (int size = recipe.Ingredients.Length - 1; size >= 2; size--)
                {
                    group.Clear();
                    if (!Search(recipe, candidates, group, 0, size, false, null)) continue;

                    previews.Add(Describe(group, false, recipe.Result.DisplayName));
                    foreach (var g in group) used.Add(g);
                    break;
                }
            }

            return previews;
        }

        // ---------- shared search ----------

        public static void Collect(RecipeDefinition recipe, List<RuneInstance> placed,
            RuneInstance ghost, HashSet<RuneInstance> used, List<RuneInstance> into)
        {
            into.Clear();

            for (int i = 0; i < placed.Count; i++) AddIfIngredient(placed[i], recipe, used, into);
            if (ghost != null) AddIfIngredient(ghost, recipe, used, into);
        }

        static void AddIfIngredient(RuneInstance piece, RecipeDefinition recipe,
            HashSet<RuneInstance> used, List<RuneInstance> into)
        {
            // Locked pieces are invisible to every recipe. That is the whole contract of the lock:
            // it is not consumed, and nothing is ever drawn pointing at it.
            if (piece.Def.Layer != Layer.Skill || piece.Locked) return;
            if (used != null && used.Contains(piece)) return;

            for (int k = 0; k < recipe.Ingredients.Length; k++)
            {
                if (piece.Def != recipe.Ingredients[k]) continue;
                into.Add(piece);
                return;
            }
        }

        /// <param name="require">When set, only groups containing this piece count as a match.</param>
        public static bool Search(RecipeDefinition recipe, List<RuneInstance> candidates,
            List<RuneInstance> group, int startIndex, int targetSize, bool exact, RuneInstance require)
        {
            if (group.Count == targetSize)
            {
                if (require != null && !group.Contains(require)) return false;
                if (exact ? !Matches(recipe, group) : !IsSubset(recipe, group)) return false;
                return FormsCluster(group);
            }

            for (int i = startIndex; i < candidates.Count; i++)
            {
                group.Add(candidates[i]);
                if (Search(recipe, candidates, group, i + 1, targetSize, exact, require)) return true;
                group.RemoveAt(group.Count - 1);
            }

            return false;
        }

        public static bool Matches(RecipeDefinition recipe, List<RuneInstance> group)
        {
            var pool = new List<PieceDefinition>(recipe.Ingredients);

            for (int i = 0; i < group.Count; i++)
            {
                int at = pool.IndexOf(group[i].Def);
                if (at < 0) return false;
                pool.RemoveAt(at);
            }

            return pool.Count == 0;
        }

        public static bool IsSubset(RecipeDefinition recipe, List<RuneInstance> group)
        {
            var pool = new List<PieceDefinition>(recipe.Ingredients);

            for (int i = 0; i < group.Count; i++)
            {
                int at = pool.IndexOf(group[i].Def);
                if (at < 0) return false;
                pool.RemoveAt(at);
            }

            return true;
        }

        /// <summary>
        /// True when the group's cells form one orthogonally connected blob. Ingredients only have
        /// to touch each other, not line up — an older "one straight row" rule made every recipe
        /// with a 2x2 or L ingredient impossible to trigger.
        /// </summary>
        public static bool FormsCluster(List<RuneInstance> group)
        {
            var cells = new HashSet<Vector2Int>();
            for (int i = 0; i < group.Count; i++)
            {
                foreach (var c in group[i].Cells()) cells.Add(c);
            }

            if (cells.Count == 0) return false;

            var frontier = new Stack<Vector2Int>();
            var seen = new HashSet<Vector2Int>();

            foreach (var start in cells)
            {
                frontier.Push(start);
                seen.Add(start);
                break;
            }

            while (frontier.Count > 0)
            {
                var at = frontier.Pop();

                for (int d = 0; d < Neighbours.Length; d++)
                {
                    var next = at + Neighbours[d];
                    if (!cells.Contains(next) || !seen.Add(next)) continue;
                    frontier.Push(next);
                }
            }

            return seen.Count == cells.Count;
        }

        static readonly Vector2Int[] Neighbours =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        public static EvoPreview Describe(List<RuneInstance> group, bool complete, string resultName,
            RuneInstance ghost = null)
        {
            var members = new Vector2[group.Count];

            for (int i = 0; i < group.Count; i++)
            {
                Vector2 sum = Vector2.zero;
                int cells = 0;

                foreach (var c in group[i].Cells())
                {
                    sum += new Vector2(c.x, c.y);
                    cells++;
                }

                members[i] = cells == 0 ? sum : sum / cells;
            }

            return new EvoPreview
            {
                Members = members,
                Complete = complete,
                NeedsHeldPiece = ghost != null && group.Contains(ghost),
                ResultName = resultName
            };
        }

        /// <summary>Memakan bahannya dan melahirkan hasilnya. <b>Tidak pernah dibatalkan.</b></summary>
        static bool Merge(List<RuneInstance> group, PieceDefinition result, List<RuneInstance> placed,
            System.Action rebuild, PlaceFunc place, List<string> log,
            System.Action<PieceDefinition, Vector2> spill,
            System.Action<RuneInstance> evolved)
        {
            var footprint = new List<Vector2Int>();
            for (int i = 0; i < group.Count; i++)
            {
                foreach (var c in group[i].Cells()) footprint.Add(c);
            }

            for (int i = 0; i < group.Count; i++) placed.Remove(group[i]);
            rebuild();

            var seatedInst = Seat(result, footprint, place);
            bool seated = seatedInst != null;

            if (seated) evolved?.Invoke(seatedInst);
            else spill?.Invoke(result, Middle(footprint));

            var line = new System.Text.StringBuilder();
            for (int i = 0; i < group.Count; i++)
            {
                if (i > 0) line.Append(" + ");
                line.Append(group[i].Def.DisplayName);
            }

            line.Append("  ->  ").Append(result.DisplayName);
            if (!seated) line.Append(" (keluar - pasang ulang)");

            log.Add(line.ToString());
            return true;
        }

        /// <summary>Titik tengah sekumpulan petak, dalam koordinat petak.</summary>
        static Vector2 Middle(List<Vector2Int> cells)
        {
            if (cells.Count == 0) return Vector2.zero;

            Vector2 sum = Vector2.zero;
            for (int i = 0; i < cells.Count; i++) sum += new Vector2(cells[i].x, cells[i].y);
            return sum / cells.Count;
        }

        /// <summary>Prefers the ground the ingredients just vacated, then anywhere it fits.</summary>
        static RuneInstance Seat(PieceDefinition evolved, List<Vector2Int> footprint, PlaceFunc place)
        {
            for (int rot = 0; rot < 4; rot++)
            {
                for (int i = 0; i < footprint.Count; i++)
                {
                    var inst = place(evolved, footprint[i], rot);
                    if (inst != null) return inst;
                }
            }

            for (int rot = 0; rot < 4; rot++)
            {
                for (int y = -2; y < 12; y++)
                {
                    for (int x = -2; x < 12; x++)
                    {
                        var inst = place(evolved, new Vector2Int(x, y), rot);
                        if (inst != null) return inst;
                    }
                }
            }

            return null;
        }
    }
}
