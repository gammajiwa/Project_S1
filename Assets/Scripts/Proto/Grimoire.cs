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

    /// <summary>A recipe group sitting on the board: the span it covers and whether it is complete.</summary>
    public class EvoPreview
    {
        public Vector2Int From;
        public Vector2Int To;
        public bool Complete;
        public string ResultName;
    }

    /// <summary>
    /// Small single-layer storage grid. Skills only â€” runes have no platform here, so they must go
    /// straight into the grimoire or be sold. Stored skills do not cast.
    /// </summary>
    public class Backpack
    {
        public const int Width = 4;
        public const int Height = 3;

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
        public List<EvoPreview> FindPendingGroups()
        {
            var previews = new List<EvoPreview>();
            var used = new HashSet<RuneInstance>();

            for (int r = 0; r < _db.Recipes.Count; r++)
            {
                var recipe = _db.Recipes[r];
                var result = recipe.Result;
                if (result == null) continue;

                var candidates = new List<RuneInstance>();
                for (int i = 0; i < Placed.Count; i++)
                {
                    var p = Placed[i];
                    if (p.Def.Layer != Layer.Skill || p.Locked || used.Contains(p)) continue;

                    for (int k = 0; k < recipe.Ingredients.Length; k++)
                    {
                        if (p.Def != recipe.Ingredients[k]) continue;
                        candidates.Add(p);
                        break;
                    }
                }

                if (candidates.Count < 2) continue;

                var group = new List<RuneInstance>();

                if (FindLineGroup(recipe, candidates, group, 0, recipe.Ingredients.Length, true))
                {
                    previews.Add(MakePreview(group, true, result.DisplayName));
                    for (int i = 0; i < group.Count; i++) used.Add(group[i]);
                    continue;
                }

                for (int size = recipe.Ingredients.Length - 1; size >= 2; size--)
                {
                    group.Clear();
                    if (!FindLineGroup(recipe, candidates, group, 0, size, false)) continue;

                    previews.Add(MakePreview(group, false, result.DisplayName));
                    for (int i = 0; i < group.Count; i++) used.Add(group[i]);
                    break;
                }
            }

            return previews;
        }

        bool FindLineGroup(RecipeDefinition recipe, List<RuneInstance> candidates,
            List<RuneInstance> group, int startIndex, int targetSize, bool exact)
        {
            if (group.Count == targetSize)
            {
                if (exact ? !MatchesIngredients(recipe, group) : !IsSubsetOfIngredients(recipe, group)) return false;
                return FormsLine(group);
            }

            for (int i = startIndex; i < candidates.Count; i++)
            {
                group.Add(candidates[i]);
                if (FindLineGroup(recipe, candidates, group, i + 1, targetSize, exact)) return true;
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

        static EvoPreview MakePreview(List<RuneInstance> group, bool complete, string resultName)
        {
            var min = new Vector2Int(int.MaxValue, int.MaxValue);
            var max = new Vector2Int(int.MinValue, int.MinValue);

            for (int i = 0; i < group.Count; i++)
            {
                foreach (var c in group[i].Cells())
                {
                    if (c.x < min.x) min.x = c.x;
                    if (c.y < min.y) min.y = c.y;
                    if (c.x > max.x) max.x = c.x;
                    if (c.y > max.y) max.y = c.y;
                }
            }

            return new EvoPreview { From = min, To = max, Complete = complete, ResultName = resultName };
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
                if (!FormsLine(group)) return false;
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

        /// <summary>Tries to seat the evolved skill somewhere inside the footprint the pair vacated.</summary>
        bool SeatEvolved(PieceDefinition evolved, List<Vector2Int> footprint)
        {
            for (int rot = 0; rot < 4; rot++)
            {
                for (int i = 0; i < footprint.Count; i++)
                {
                    if (Place(evolved, footprint[i], rot) != null) return true;
                }
            }

            return false;
        }

        /// <summary>True when the whole group together occupies one contiguous row or column.</summary>
        static bool FormsLine(List<RuneInstance> group)
        {
            var cells = new List<Vector2Int>();
            for (int i = 0; i < group.Count; i++)
            {
                foreach (var c in group[i].Cells()) cells.Add(c);
            }

            if (cells.Count == 0) return false;

            bool sameRow = true, sameCol = true;
            int y0 = cells[0].y, x0 = cells[0].x;

            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].y != y0) sameRow = false;
                if (cells[i].x != x0) sameCol = false;
            }

            int min = int.MaxValue, max = int.MinValue;

            if (sameRow)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].x < min) min = cells[i].x;
                    if (cells[i].x > max) max = cells[i].x;
                }

                return max - min + 1 == cells.Count;
            }

            if (sameCol)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].y < min) min = cells[i].y;
                    if (cells[i].y > max) max = cells[i].y;
                }

                return max - min + 1 == cells.Count;
            }

            return false;
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
            System.Array.Clear(Stats, 0, Stats.Length);

            // Any placed piece may hand out stats: runes, sigils, even skills.
            foreach (var piece in Placed)
            {
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
                if (skill.Def.Kind == CastKind.AuraOnly) continue;
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
