using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>A rune or skill placed on the grid at a position and rotation.</summary>
    public class RuneInstance
    {
        public PieceDefinition Def;
        public Vector2Int Origin;
        public int Rot;
        public float CdTimer;

        /// <summary>Locked pieces are never consumed by an evolution.</summary>
        public bool Locked;

        public IEnumerable<Vector2Int> Cells()
        {
            var shape = Shapes.Rotate(Def.Cells, Rot);
            for (int i = 0; i < shape.Length; i++) yield return Origin + shape[i];
        }
    }

    /// <summary>A skill compiled into flat numbers. Combat only ever reads this.</summary>
    public class CompiledSpell
    {
        public RuneInstance Source;
        public float Damage;
        public float Cooldown;
        public float Radius;
        public float Range;

        // Kept purely so the HUD can show WHERE the numbers came from.
        public float DamageBonus;
        public float CooldownBonus;
        public float RadiusBonus;
    }

    /// <summary>A recipe group sitting on the board: which pieces are in it, and whether it is complete.</summary>
    public class EvoPreview
    {
        /// <summary>
        /// Centre of each ingredient, in grid-cell space (so a 2x2 piece at the origin sits at
        /// 0.5, 0.5). The UI draws the connectors between these points.
        ///
        /// This used to be a bounding box instead, which the UI filled in as a coloured rectangle.
        /// A box says "something around here"; it cannot say WHICH pieces belong to the recipe, and
        /// with several groups on the board at once the boxes overlap and answer nothing.
        /// </summary>
        public Vector2[] Members;

        public bool Complete;

        /// <summary>
        /// True when this group only holds together because the piece on the cursor is counted as
        /// if it were already seated. Such a group is a proposal, not a promise — nothing evolves
        /// until the piece is actually put down, so the UI must not paint it as settled.
        /// </summary>
        public bool NeedsHeldPiece;

        public string ResultName;
    }

    /// <summary>
    /// Small single-layer storage grid. Skills only â€” runes have no platform here, so they must go
    /// straight into the grimoire or be sold. Stored skills do not cast.
    /// </summary>
    public class Backpack
    {
        public const int Width = 4;
        public const int Height = 4;

        public readonly List<RuneInstance> Placed = new List<RuneInstance>();

        readonly Dictionary<Vector2Int, RuneInstance> _occupancy = new Dictionary<Vector2Int, RuneInstance>();

        public static bool InBounds(Vector2Int c) =>
            c.x >= 0 && c.x < Width && c.y >= 0 && c.y < Height;

        public RuneInstance At(Vector2Int cell) =>
            _occupancy.TryGetValue(cell, out var r) ? r : null;

        public bool CanPlace(PieceDefinition def, Vector2Int origin, int rot)
        {
            if (def.Layer != Layer.Skill) return false;

            var shape = Shapes.Rotate(def.Cells, rot);
            for (int i = 0; i < shape.Length; i++)
            {
                var c = origin + shape[i];
                if (!InBounds(c)) return false;
                if (_occupancy.ContainsKey(c)) return false;
            }

            return true;
        }

        public RuneInstance Place(PieceDefinition def, Vector2Int origin, int rot)
        {
            if (!CanPlace(def, origin, rot)) return null;

            var inst = new RuneInstance { Def = def, Origin = origin, Rot = rot };
            Placed.Add(inst);
            Rebuild();
            return inst;
        }

        public void Remove(RuneInstance inst)
        {
            if (inst == null) return;
            Placed.Remove(inst);
            Rebuild();
        }

        /// <summary>True when the spot would fit once whatever is already there is kicked out.</summary>
        public bool CanReplaceAt(PieceDefinition def, Vector2Int origin, int rot)
        {
            if (def.Layer != Layer.Skill) return false;

            var shape = Shapes.Rotate(def.Cells, rot);
            for (int i = 0; i < shape.Length; i++)
            {
                if (!InBounds(origin + shape[i])) return false;
            }

            return true;
        }

        /// <summary>Evicts everything sitting on the footprint. Returns the evicted definitions.</summary>
        public List<PieceDefinition> ClearFootprint(PieceDefinition def, Vector2Int origin, int rot)
        {
            var displaced = new List<PieceDefinition>();
            var shape = Shapes.Rotate(def.Cells, rot);

            bool again = true;
            while (again)
            {
                again = false;

                for (int i = 0; i < shape.Length; i++)
                {
                    var occupant = At(origin + shape[i]);
                    if (occupant == null) continue;

                    displaced.Add(occupant.Def);
                    Remove(occupant);
                    again = true;
                    break;
                }
            }

            return displaced;
        }

        void Rebuild()
        {
            _occupancy.Clear();
            foreach (var r in Placed)
            {
                foreach (var c in r.Cells()) _occupancy[c] = r;
            }
        }

        // ---------- evolution ----------
        //
        // The bag evolves too. It was pure storage, which made it a dead end: spare copies piled up
        // there with no way to become anything, and the only place a recipe could ever resolve was
        // the board — where every ingredient also has to stand on a rune and compete for the same
        // cells the casting build needs. Merging in the bag is how you cook parts without giving up
        // firing positions.

        public List<string> ResolveEvolutions(ContentDatabase db) =>
            RecipeResolver.Resolve(db, Placed, Rebuild, Place, CouldSeat);

        public List<EvoPreview> FindPendingGroups(ContentDatabase db) =>
            RecipeResolver.Preview(db, Placed, CouldSeat);

        /// <summary>
        /// No runes here, so the only question is whether the footprint lands inside the bag on
        /// cells that are either empty or about to be freed by this very recipe.
        /// </summary>
        bool CouldSeat(PieceDefinition result, List<RuneInstance> group)
        {
            if (result == null || result.Layer != Layer.Skill) return false;

            var freed = new HashSet<Vector2Int>();
            for (int i = 0; i < group.Count; i++)
            {
                foreach (var c in group[i].Cells()) freed.Add(c);
            }

            for (int rot = 0; rot < 4; rot++)
            {
                var shape = Shapes.Rotate(result.Cells, rot);

                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        var origin = new Vector2Int(x, y);
                        bool ok = true;

                        for (int i = 0; i < shape.Length; i++)
                        {
                            var c = origin + shape[i];
                            if (!InBounds(c) || (_occupancy.ContainsKey(c) && !freed.Contains(c)))
                            {
                                ok = false;
                                break;
                            }
                        }

                        if (ok) return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Two-layer grid. Base runes are the platform; skills must stand entirely on top of them and
    /// inherit the bonus of whatever rune is under each of their cells.
    /// Everything is resolved once in <see cref="Compile"/> whenever the layout changes.
    /// </summary>
    public class Grimoire
    {
        public const int Width = 7;
        public const int Height = 7;

        public readonly List<RuneInstance> Placed = new List<RuneInstance>();
        public readonly List<CompiledSpell> Spells = new List<CompiledSpell>();

        readonly ContentDatabase _db;

        public Grimoire(ContentDatabase database)
        {
            _db = database;
        }

        /// <summary>
        /// Every stat handed out by placed pieces, indexed by StatKind. One flat array — no
        /// dictionary lookups anywhere in the hot path.
        /// </summary>
        public readonly float[] Stats = new float[(int)StatKind.Count];

        public float Stat(StatKind kind) => Stats[(int)kind];

        /// <summary>
        /// Buffs handed out on every kill by whatever is currently placed. Compiled once when the
        /// grid changes so the kill handler — which fires hundreds of times a second at high waves
        /// — never has to walk the board.
        /// </summary>
        public readonly List<BuffDefinition> KillGrants = new List<BuffDefinition>();

        /// <summary>
        /// Total mana yang pulih per musuh mati, dijumlah dari semua piece terpasang yang membawa
        /// efek panen. AGREGAT dengan sengaja, bukan per-skill-pembunuh: event kill cuma membawa
        /// posisi, tidak pernah membawa skill mana yang membunuh — dan menjahitkan atribusi ke
        /// seluruh jalur damage (proyektil, zone yang hidup 6 detik, chain, detonasi) demi satu
        /// efek adalah harga yang salah. Memasang piece-nya = efeknya menyala; itu tetap terasa,
        /// dan tetap satu keputusan papan.
        /// </summary>
        public float KillRestoreMana;

        /// <summary>Total HP yang pulih per musuh mati. Aturan agregat yang sama.</summary>
        public float KillRestoreHp;

        public float BonusMaxHp => Stats[(int)StatKind.MaxHp];
        public float BonusMaxMana => Stats[(int)StatKind.MaxMana];
        public float BonusManaRegen => Stats[(int)StatKind.ManaRegen];
        public float BonusHpRegen => Stats[(int)StatKind.HpRegen];
        public float Defense => Stats[(int)StatKind.Defense];
        public float ManaCostMultiplier => Mathf.Clamp(1f - Stats[(int)StatKind.ManaCostPct], 0.2f, 1f);
        public int BonusAilmentPoints => Mathf.RoundToInt(Stats[(int)StatKind.AilmentPoints]);

        readonly Dictionary<Vector2Int, RuneInstance> _base = new Dictionary<Vector2Int, RuneInstance>();
        readonly Dictionary<Vector2Int, RuneInstance> _skill = new Dictionary<Vector2Int, RuneInstance>();

        public static bool InBounds(Vector2Int c) =>
            c.x >= 0 && c.x < Width && c.y >= 0 && c.y < Height;

        public RuneInstance BaseAt(Vector2Int cell) =>
            _base.TryGetValue(cell, out var r) ? r : null;

        public RuneInstance SkillAt(Vector2Int cell) =>
            _skill.TryGetValue(cell, out var r) ? r : null;

        public bool CanPlace(PieceDefinition def, Vector2Int origin, int rot)
        {
            var shape = Shapes.Rotate(def.Cells, rot);

            for (int i = 0; i < shape.Length; i++)
            {
                var c = origin + shape[i];
                if (!InBounds(c)) return false;

                if (def.Layer == Layer.Rune)
                {
                    if (_base.ContainsKey(c)) return false;
                }
                else
                {
                    // A skill needs a rune under every cell, and no other skill in the way.
                    if (!_base.ContainsKey(c)) return false;
                    if (_skill.ContainsKey(c)) return false;
                }
            }

            return true;
        }

        public RuneInstance Place(PieceDefinition def, Vector2Int origin, int rot)
        {
            if (!CanPlace(def, origin, rot)) return null;

            var inst = new RuneInstance { Def = def, Origin = origin, Rot = rot };
            Placed.Add(inst);
            Rebuild();
            return inst;
        }

        /// <summary>
        /// Removes an instance. Pulling a base rune out from under a skill orphans that skill â€”
        /// orphans are removed too and returned so the caller can drop them back in the tray.
        /// </summary>
        /// <summary>True when the spot would fit once whatever is already there is kicked out.</summary>
        public bool CanReplaceAt(PieceDefinition def, Vector2Int origin, int rot)
        {
            var shape = Shapes.Rotate(def.Cells, rot);

            for (int i = 0; i < shape.Length; i++)
            {
                var c = origin + shape[i];
                if (!InBounds(c)) return false;

                // A skill still needs a rune under every cell â€” only other skills can be evicted.
                if (def.Layer == Layer.Skill && !_base.ContainsKey(c)) return false;
            }

            return true;
        }

        /// <summary>Evicts everything of the same layer on the footprint. Returns the evicted defs.</summary>
        public List<PieceDefinition> ClearFootprint(PieceDefinition def, Vector2Int origin, int rot)
        {
            var displaced = new List<PieceDefinition>();
            var shape = Shapes.Rotate(def.Cells, rot);

            bool again = true;
            while (again)
            {
                again = false;

                for (int i = 0; i < shape.Length; i++)
                {
                    var c = origin + shape[i];
                    if (!InBounds(c)) continue;

                    var occupant = def.Layer == Layer.Rune ? BaseAt(c) : SkillAt(c);
                    if (occupant == null) continue;

                    displaced.Add(occupant.Def);
                    displaced.AddRange(Remove(occupant));
                    again = true;
                    break;
                }
            }

            return displaced;
        }

        /// <summary>A skill that travels with the rune it is standing on.</summary>
        public struct Rider
        {
            public PieceDefinition Def;

            /// <summary>Its origin relative to the rune's origin, so it can be re-seated.</summary>
            public Vector2Int Offset;

            public int Rot;
        }

        /// <summary>
        /// Skills standing ENTIRELY on this rune, lifted off with it.
        ///
        /// Entirely is the whole rule. A skill bridging two runes belongs to neither, so carrying it
        /// with one of them would silently tear it off the other — those are released to the floor
        /// instead, which is what already happened to every skill when a base moved.
        ///
        /// Removes the riders from the board; the caller owns them from here.
        /// </summary>
        public List<Rider> LiftRiders(RuneInstance rune)
        {
            var riders = new List<Rider>();
            if (rune == null || rune.Def.Layer != Layer.Rune) return riders;

            var floor = new HashSet<Vector2Int>();
            foreach (var c in rune.Cells()) floor.Add(c);

            var travelling = new List<RuneInstance>();

            for (int i = 0; i < Placed.Count; i++)
            {
                var skill = Placed[i];
                if (skill.Def.Layer != Layer.Skill) continue;

                bool wholly = true;
                foreach (var c in skill.Cells())
                {
                    if (floor.Contains(c)) continue;
                    wholly = false;
                    break;
                }

                if (!wholly) continue;

                travelling.Add(skill);
                riders.Add(new Rider
                {
                    Def = skill.Def,
                    Offset = skill.Origin - rune.Origin,
                    Rot = skill.Rot
                });
            }

            for (int i = 0; i < travelling.Count; i++) Placed.Remove(travelling[i]);
            if (travelling.Count > 0) Rebuild();

            return riders;
        }

        /// <summary>Puts riders back down around a rune that has just been seated.</summary>
        /// <returns>Riders that would not fit, for the caller to scatter.</returns>
        public List<PieceDefinition> SeatRiders(List<Rider> riders, Vector2Int runeOrigin)
        {
            var homeless = new List<PieceDefinition>();
            if (riders == null) return homeless;

            for (int i = 0; i < riders.Count; i++)
            {
                var rider = riders[i];
                if (Place(rider.Def, runeOrigin + rider.Offset, rider.Rot) == null)
                {
                    homeless.Add(rider.Def);
                }
            }

            return homeless;
        }

        public List<PieceDefinition> Remove(RuneInstance inst)
        {
            var orphans = new List<PieceDefinition>();
            if (inst == null) return orphans;

            Placed.Remove(inst);
            Rebuild();

            if (inst.Def.Layer != Layer.Rune) return orphans;

            bool again = true;
            while (again)
            {
                again = false;
                for (int i = 0; i < Placed.Count; i++)
                {
                    var candidate = Placed[i];
                    if (candidate.Def.Layer != Layer.Skill) continue;

                    bool grounded = true;
                    foreach (var c in candidate.Cells())
                    {
                        if (!_base.ContainsKey(c))
                        {
                            grounded = false;
                            break;
                        }
                    }

                    if (grounded) continue;

                    orphans.Add(candidate.Def);
                    Placed.RemoveAt(i);
                    Rebuild();
                    again = true;
                    break;
                }
            }

            return orphans;
        }

        /// <summary>
        /// Merges every pair of identical skills that sit adjacent AND form one unbroken straight
        /// line. Runs until nothing changes, so four in a row evolve twice. Returns a log for the UI.
        /// </summary>
        public List<string> ResolveEvolutions()
        {
            var log = new List<string>();
            bool changed = true;

            while (changed)
            {
                changed = false;

                for (int r = 0; r < _db.Recipes.Count && !changed; r++)
                {
                    changed = TryRecipe(_db.Recipes[r], log);
                }
            }

            return log;
        }

        /// <summary>
        /// Recipe groups currently sitting on the board, for the connector lines.
        /// Complete groups will merge at the end of the wave; partial ones are just a hint.
        /// </summary>
        public List<EvoPreview> FindPendingGroups() => FindPendingGroups(null, default, 0);

        /// <summary>
        /// Centres of every piece on the board that shares a recipe with the one being carried.
        ///
        /// This answers the question at the moment it is actually asked. The evolution connectors
        /// only appear once the cursor is over a legal cell, which is one move too late: by then
        /// you have already picked the spot. Lifting a piece off the floor should be enough to see
        /// what it wants to stand next to.
        ///
        /// No "would this complete it" flag comes back on purpose. These are possibilities, and the
        /// UI paints possibilities blue — gold is reserved for a group that is actually going to
        /// evolve, which nothing being held can be.
        /// </summary>
        public List<Vector2> FindPartners(PieceDefinition held)
        {
            var centers = new List<Vector2>();
            if (held == null || held.Layer != Layer.Skill) return centers;

            var seen = new HashSet<RuneInstance>();

            for (int r = 0; r < _db.Recipes.Count; r++)
            {
                var recipe = _db.Recipes[r];
                if (recipe == null || recipe.Result == null || !UsesPiece(recipe, held)) continue;

                for (int i = 0; i < Placed.Count; i++)
                {
                    var piece = Placed[i];
                    if (piece.Def.Layer != Layer.Skill || piece.Locked) continue;
                    if (!UsesPiece(recipe, piece.Def)) continue;
                    if (!seen.Add(piece)) continue;

                    centers.Add(CenterOf(piece));
                }
            }

            return centers;
        }

        /// <summary>
        /// Would the result actually fit once this group is consumed?
        ///
        /// Gold promises an evolution, and without this it was promising one it could not keep: the
        /// preview only ever asked "are the ingredients here and touching". If the board is full,
        /// <see cref="SeatEvolved"/> finds nowhere to put the result, the merge is rolled back, and
        /// the player watches a gold group survive the wave untouched with no explanation.
        ///
        /// Pure — it reasons about the cells the group would free rather than removing anything.
        /// </summary>
        bool CouldSeat(PieceDefinition result, List<RuneInstance> group)
        {
            if (result == null) return false;

            var freed = new HashSet<Vector2Int>();
            for (int i = 0; i < group.Count; i++)
            {
                foreach (var c in group[i].Cells()) freed.Add(c);
            }

            for (int rot = 0; rot < 4; rot++)
            {
                var shape = Shapes.Rotate(result.Cells, rot);

                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        if (Fits(result, shape, new Vector2Int(x, y), freed)) return true;
                    }
                }
            }

            return false;
        }

        bool Fits(PieceDefinition result, Vector2Int[] shape, Vector2Int origin,
            HashSet<Vector2Int> freed)
        {
            for (int i = 0; i < shape.Length; i++)
            {
                var c = origin + shape[i];
                if (!InBounds(c)) return false;

                if (result.Layer == Layer.Rune)
                {
                    if (_base.ContainsKey(c) && !freed.Contains(c)) return false;
                    continue;
                }

                // A skill still needs a rune under every cell, and the only skills it may overlap
                // are the ones this very recipe is about to eat.
                if (!_base.ContainsKey(c)) return false;
                if (_skill.ContainsKey(c) && !freed.Contains(c)) return false;
            }

            return true;
        }

        static bool UsesPiece(RecipeDefinition recipe, PieceDefinition def)
        {
            if (recipe.Ingredients == null) return false;

            for (int i = 0; i < recipe.Ingredients.Length; i++)
            {
                if (recipe.Ingredients[i] == def) return true;
            }

            return false;
        }

        static Vector2 CenterOf(RuneInstance inst)
        {
            Vector2 sum = Vector2.zero;
            int cells = 0;

            foreach (var c in inst.Cells())
            {
                sum += new Vector2(c.x, c.y);
                cells++;
            }

            return cells == 0 ? sum : sum / cells;
        }

        /// <summary>
        /// Same, but the piece the player is currently dragging counts as if it were already seated
        /// at <paramref name="heldOrigin"/>. Without it the line only appears after the drop, which
        /// is one move too late to help you aim.
        /// </summary>
        public List<EvoPreview> FindPendingGroups(PieceDefinition heldDef, Vector2Int heldOrigin, int heldRot)
        {
            var previews = new List<EvoPreview>();
            var used = new HashSet<RuneInstance>();

            // Only preview a spot the piece could actually take: on runes, nothing else in the way.
            RuneInstance ghost = null;
            if (heldDef != null && heldDef.Layer == Layer.Skill && CanPlace(heldDef, heldOrigin, heldRot))
            {
                ghost = new RuneInstance { Def = heldDef, Origin = heldOrigin, Rot = heldRot };
            }

            var candidates = new List<RuneInstance>();
            var group = new List<RuneInstance>();

            // Grup EMAS yang sudah duduk dikunci PALING DULU, tanpa ghost. Emas adalah janji —
            // "grup ini AKAN berevolusi" — dan piece di tangan tidak boleh mencuri anggotanya.
            // Dulu ghost dicari lebih dulu: mengangkat kembaran salah satu bahan langsung membajak
            // pasangan emas di papan — anggotanya ditarik ke grup kursor, garisnya berubah biru
            // (NeedsHeldPiece), dan pemain melihat "sudah kuning kok jadi biru" tanpa menyentuh
            // pasangannya sama sekali. Hanya lock yang boleh membubarkan grup emas.
            //
            // Diulang per resep sampai kering, bukan sekali — dua pasang bahan yang sama di papan
            // adalah dua janji, dan keduanya berhak atas garisnya.
            for (int r = 0; r < _db.Recipes.Count; r++)
            {
                var recipe = _db.Recipes[r];
                if (recipe.Result == null) continue;

                while (true)
                {
                    CollectCandidates(recipe, null, used, candidates);
                    if (candidates.Count < recipe.Ingredients.Length) break;

                    group.Clear();
                    if (!FindClusterGroup(recipe, candidates, group, 0,
                            recipe.Ingredients.Length, true, null)) break;

                    // Cuma yang BENAR-BENAR emas (lengkap DAN hasilnya muat duduk) yang boleh
                    // mengunci anggota. Yang lengkap tapi tidak muat tetap diurus loop utama di
                    // bawah — birunya jujur, dan anggotanya masih boleh dipakai ghost.
                    if (!CouldSeat(recipe.Result, group)) break;

                    previews.Add(MakePreview(group, true, recipe.Result.DisplayName, ghost));
                    for (int i = 0; i < group.Count; i++) used.Add(group[i]);
                }
            }

            // A finished recipe that uses the dragged piece is the answer the player is hunting for,
            // so it is found next — recipe order must not bury it under someone else's partial.
            if (ghost != null)
            {
                for (int r = 0; r < _db.Recipes.Count; r++)
                {
                    var recipe = _db.Recipes[r];
                    if (recipe.Result == null) continue;

                    CollectCandidates(recipe, ghost, used, candidates);
                    if (candidates.Count < recipe.Ingredients.Length) continue;

                    group.Clear();
                    if (!FindClusterGroup(recipe, candidates, group, 0, recipe.Ingredients.Length, true, ghost)) continue;

                    previews.Add(MakePreview(group, CouldSeat(recipe.Result, group),
                        recipe.Result.DisplayName, ghost));
                    for (int i = 0; i < group.Count; i++) used.Add(group[i]);
                    break;
                }
            }

            for (int r = 0; r < _db.Recipes.Count; r++)
            {
                var recipe = _db.Recipes[r];
                var result = recipe.Result;
                if (result == null) continue;

                CollectCandidates(recipe, ghost, used, candidates);
                if (candidates.Count < 2) continue;

                group.Clear();

                if (FindClusterGroup(recipe, candidates, group, 0, recipe.Ingredients.Length, true, null))
                {
                    // Complete on the board is not the same as able to evolve — the result still
                    // has to land somewhere. Gold only when both are true.
                    previews.Add(MakePreview(group, CouldSeat(result, group), result.DisplayName, ghost));
                    for (int i = 0; i < group.Count; i++) used.Add(group[i]);
                    continue;
                }

                for (int size = recipe.Ingredients.Length - 1; size >= 2; size--)
                {
                    group.Clear();
                    if (!FindClusterGroup(recipe, candidates, group, 0, size, false, null)) continue;

                    previews.Add(MakePreview(group, false, result.DisplayName, ghost));
                    for (int i = 0; i < group.Count; i++) used.Add(group[i]);
                    break;
                }
            }

            return previews;
        }

        void CollectCandidates(RecipeDefinition recipe, RuneInstance ghost,
            HashSet<RuneInstance> used, List<RuneInstance> into)
        {
            into.Clear();

            for (int i = 0; i < Placed.Count; i++) AddIfIngredient(Placed[i], recipe, used, into);
            if (ghost != null) AddIfIngredient(ghost, recipe, used, into);
        }

        static void AddIfIngredient(RuneInstance piece, RecipeDefinition recipe,
            HashSet<RuneInstance> used, List<RuneInstance> into)
        {
            if (piece.Def.Layer != Layer.Skill || piece.Locked || used.Contains(piece)) return;

            for (int k = 0; k < recipe.Ingredients.Length; k++)
            {
                if (piece.Def != recipe.Ingredients[k]) continue;
                into.Add(piece);
                return;
            }
        }

        /// <param name="require">When set, only groups containing this piece count as a match.</param>
        bool FindClusterGroup(RecipeDefinition recipe, List<RuneInstance> candidates,
            List<RuneInstance> group, int startIndex, int targetSize, bool exact, RuneInstance require)
        {
            if (group.Count == targetSize)
            {
                if (require != null && !group.Contains(require)) return false;
                if (exact ? !MatchesIngredients(recipe, group) : !IsSubsetOfIngredients(recipe, group)) return false;
                return FormsCluster(group);
            }

            for (int i = startIndex; i < candidates.Count; i++)
            {
                group.Add(candidates[i]);
                if (FindClusterGroup(recipe, candidates, group, i + 1, targetSize, exact, require)) return true;
                group.RemoveAt(group.Count - 1);
            }

            return false;
        }

        static bool IsSubsetOfIngredients(RecipeDefinition recipe, List<RuneInstance> group)
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

        static EvoPreview MakePreview(List<RuneInstance> group, bool complete, string resultName,
            RuneInstance ghost)
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

        /// <summary>Finds a matching, unlocked, in-line group for one recipe and merges it.</summary>
        bool TryRecipe(RecipeDefinition recipe, List<string> log)
        {
            var result = recipe.Result;
            if (result == null) return false;

            var candidates = new List<RuneInstance>();
            for (int i = 0; i < Placed.Count; i++)
            {
                var p = Placed[i];
                if (p.Def.Layer != Layer.Skill || p.Locked) continue;

                for (int k = 0; k < recipe.Ingredients.Length; k++)
                {
                    if (p.Def != recipe.Ingredients[k]) continue;
                    candidates.Add(p);
                    break;
                }
            }

            if (candidates.Count < recipe.Ingredients.Length) return false;

            var group = new List<RuneInstance>();
            return SearchGroup(recipe, candidates, group, 0, result, log);
        }

        bool SearchGroup(RecipeDefinition recipe, List<RuneInstance> candidates,
            List<RuneInstance> group, int startIndex, PieceDefinition result, List<string> log)
        {
            if (group.Count == recipe.Ingredients.Length)
            {
                if (!MatchesIngredients(recipe, group)) return false;
                if (!FormsCluster(group)) return false;
                return Merge(group, result, log);
            }

            for (int i = startIndex; i < candidates.Count; i++)
            {
                group.Add(candidates[i]);
                if (SearchGroup(recipe, candidates, group, i + 1, result, log)) return true;
                group.RemoveAt(group.Count - 1);
            }

            return false;
        }

        static bool MatchesIngredients(RecipeDefinition recipe, List<RuneInstance> group)
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

        bool Merge(List<RuneInstance> group, PieceDefinition result, List<string> log)
        {
            var footprint = new List<Vector2Int>();
            for (int i = 0; i < group.Count; i++)
            {
                foreach (var c in group[i].Cells()) footprint.Add(c);
            }

            for (int i = 0; i < group.Count; i++) Placed.Remove(group[i]);
            Rebuild();

            if (!SeatEvolved(result, footprint))
            {
                for (int i = 0; i < group.Count; i++) Placed.Add(group[i]);
                Rebuild();
                return false;
            }

            var line = new System.Text.StringBuilder();
            for (int i = 0; i < group.Count; i++)
            {
                if (i > 0) line.Append(" + ");
                line.Append(group[i].Def.DisplayName);
            }

            line.Append("  ->  ").Append(result.DisplayName);
            log.Add(line.ToString());
            return true;
        }

        /// <summary>
        /// Seats the evolved skill, preferring the ground its ingredients just vacated so the build
        /// keeps its shape.
        ///
        /// The board-wide fallback matters now that results are bigger and stranger than the parts
        /// that make them: a Ring needs eight rune cells in a very particular arrangement, and the
        /// cells the ingredients freed up will often not contain one. Without the fallback the merge
        /// simply fails, the ingredients are put back, and the player sees a completed gold recipe
        /// produce nothing at all — with no way to tell why.
        /// </summary>
        bool SeatEvolved(PieceDefinition evolved, List<Vector2Int> footprint)
        {
            for (int rot = 0; rot < 4; rot++)
            {
                for (int i = 0; i < footprint.Count; i++)
                {
                    if (Place(evolved, footprint[i], rot) != null) return true;
                }
            }

            for (int rot = 0; rot < 4; rot++)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        if (Place(evolved, new Vector2Int(x, y), rot) != null) return true;
                    }
                }
            }

            return false;
        }

        static readonly Vector2Int[] Neighbours =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        /// <summary>
        /// True when the group's cells form one orthogonally connected blob. Ingredients only have
        /// to touch each other, not line up.
        ///
        /// This replaces an older "one straight row or column" rule. That rule made every recipe
        /// with a 2x2, 3x3 or L ingredient impossible to ever trigger, because such a piece already
        /// spans two rows on its own — six of the fourteen recipes could never fire.
        /// </summary>
        static bool FormsCluster(List<RuneInstance> group)
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
                    if (!cells.Contains(next)) continue;
                    if (!seen.Add(next)) continue;

                    frontier.Push(next);
                }
            }

            // Anything unreached means the group sits in two separate blobs.
            return seen.Count == cells.Count;
        }

        void Rebuild()
        {
            _base.Clear();
            _skill.Clear();

            foreach (var r in Placed)
            {
                var map = r.Def.Layer == Layer.Rune ? _base : _skill;
                foreach (var c in r.Cells()) map[c] = r;
            }

            Compile();
        }

        /// <summary>Sums the rune bonus under each skill cell into flat per-spell numbers.</summary>
        void Compile()
        {
            Spells.Clear();
            KillGrants.Clear();
            KillRestoreMana = 0f;
            KillRestoreHp = 0f;
            System.Array.Clear(Stats, 0, Stats.Length);

            // Any placed piece may hand out stats: runes, sigils, even skills.
            foreach (var piece in Placed)
            {
                if (piece.Def.GrantOnKill != null) KillGrants.Add(piece.Def.GrantOnKill);

                // Panen dijumlahkan saat kompilasi, bukan saat kill: handler kill berjalan
                // ratusan kali per detik di wave tinggi dan tidak boleh menyentuh papan.
                KillRestoreMana += piece.Def.RestoreManaOnKill;
                KillRestoreHp += piece.Def.RestoreHpOnKill;

                if (piece.Def.Stat != StatKind.None && piece.Def.Stat != StatKind.Count)
                {
                    Stats[(int)piece.Def.Stat] += piece.Def.StatValue;
                }

                var mods = piece.Def.Stats;
                if (mods == null) continue;

                for (int i = 0; i < mods.Length; i++)
                {
                    if (mods[i].Type == StatKind.None || mods[i].Type == StatKind.Count) continue;
                    Stats[(int)mods[i].Type] += mods[i].Value;
                }
            }

            float fireDmg = Stats[(int)StatKind.FireDamagePct];
            float iceDmg = Stats[(int)StatKind.IceDamagePct];
            float lightningDmg = Stats[(int)StatKind.LightningDamagePct];
            float globalDmg = Stats[(int)StatKind.DamagePct];
            float globalCdr = Stats[(int)StatKind.CooldownPct];
            float globalArea = Stats[(int)StatKind.AreaPct];
            float globalRange = Stats[(int)StatKind.RangePct];

            foreach (var skill in Placed)
            {
                if (skill.Def.Layer != Layer.Skill) continue;

                // Sigils and aura-only pieces take up a slot but never cast.
                if (skill.Def.Kind == CastKind.Passive) continue;
                if (skill.Def.Kind == CastKind.AuraOnly) continue;

                float dmg = 0f, cdr = 0f, rad = 0f;

                foreach (var cell in skill.Cells())
                {
                    var under = BaseAt(cell);
                    if (under == null) continue;

                    // A rune's aura is its TOTAL, split evenly across its own cells. Standing on
                    // one cell of a three-cell rune therefore grants exactly one third.
                    float share = 1f / Mathf.Max(1, under.Def.Cells.Length);

                    switch (under.Def.Aura)
                    {
                        case AuraKind.DamagePct: dmg += under.Def.AuraValue * share; break;
                        case AuraKind.CooldownPct: cdr += under.Def.AuraValue * share; break;
                        case AuraKind.RadiusPct: rad += under.Def.AuraValue * share; break;
                    }

                    // Matching element on the rune underneath pays extra.
                    if (under.Def.ElementMatchBonus > 0f && under.Def.Element == skill.Def.Element)
                    {
                        dmg += under.Def.ElementMatchBonus * share;
                    }
                }

                float elemental = 0f;
                switch (skill.Def.Element)
                {
                    case Element.Fire: elemental = fireDmg; break;
                    case Element.Ice: elemental = iceDmg; break;
                    case Element.Lightning: elemental = lightningDmg; break;
                }

                Spells.Add(new CompiledSpell
                {
                    Source = skill,
                    Damage = skill.Def.BaseDamage * (1f + dmg + elemental + globalDmg),
                    Cooldown = Mathf.Max(0.15f, skill.Def.BaseCooldown * (1f - Mathf.Min(0.75f, cdr + globalCdr))),
                    Radius = skill.Def.Radius * (1f + rad + globalArea),
                    Range = skill.Def.Range * (1f + rad + globalRange),
                    DamageBonus = dmg + elemental,
                    CooldownBonus = cdr,
                    RadiusBonus = rad
                });
            }
        }
    }
}
