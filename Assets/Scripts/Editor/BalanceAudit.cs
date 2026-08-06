using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Estimates what every skill is actually worth, so the rarity ladder can be READ instead of
    /// hoped for.
    ///
    /// Raw damage numbers lie badly here, because the archetypes differ in how many enemies they
    /// touch. A 26-damage detonator that fires at forty marked enemies outproduces a 340-damage
    /// blast that reaches eight, and comparing the two printed numbers tells you the opposite. So
    /// this scores <b>throughput</b>: damage x expected targets / cooldown.
    ///
    /// The target model is deliberately crude and stated out loud rather than tuned to flatter
    /// anything — it exists to catch a 2-star beating a 5-star, not to predict a real fight.
    /// </summary>
    public static class BalanceAudit
    {
        const string Root = "Assets/GameData";

        /// <summary>Enemies per square unit at a busy mid-run moment.</summary>
        const float Density = 0.12f;

        /// <summary>No blast realistically catches more than this, however wide it is drawn.</summary>
        const float MaxTargets = 50f;

        /// <summary>Points a mark typically carries when a detonator cashes it in.</summary>
        const float TypicalPoints = 3f;

        [MenuItem("Tools/Grimoire/Audit Balance")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[BalanceAudit] ContentDatabase.asset tidak ketemu.");
                return;
            }

            var byStar = new SortedDictionary<int, List<string>>();
            var totals = new SortedDictionary<int, List<float>>();

            for (int i = 0; i < db.Pieces.Count; i++)
            {
                var p = db.Pieces[i];
                if (p == null || p.IsRune || p.IsPassive) continue;
                if (p.Kind == CastKind.Heal || p.Kind == CastKind.Cleanse) continue;

                float targets = ExpectedTargets(p);
                float perCast = p.BaseDamage * targets;
                float dps = p.BaseCooldown <= 0f ? 0f : perCast / p.BaseCooldown;
                float manaPerSecond = p.BaseCooldown <= 0f ? 0f : p.ManaCost / p.BaseCooldown;

                int star = Mathf.Clamp(p.Stars, 1, 5);
                if (!byStar.ContainsKey(star))
                {
                    byStar[star] = new List<string>();
                    totals[star] = new List<float>();
                }

                totals[star].Add(dps);
                byStar[star].Add(
                    $"    {p.DisplayName,-18} {p.Kind,-13} dmg {p.BaseDamage,6:0} x {targets,5:0.0} sasaran" +
                    $" / {p.BaseCooldown,4:0.0}s = {dps,8:0} dps   {manaPerSecond,4:0.0} mana/dtk");
            }

            var sb = new System.Text.StringBuilder("[BalanceAudit] throughput per bintang\n");
            sb.Append("model: sasaran = kepadatan ").Append(Density)
                .Append("/unit persegi, plafon ").Append(MaxTargets)
                .Append(", tanda dianggap ").Append(TypicalPoints).Append(" poin\n\n");

            float previousMedian = 0f;

            foreach (var pair in byStar)
            {
                var values = totals[pair.Key];
                values.Sort();

                float median = values[values.Count / 2];
                float ratio = previousMedian <= 0f ? 0f : median / previousMedian;

                sb.Append(pair.Key).Append(" bintang  -  median ").Append(median.ToString("0"))
                    .Append(" dps");

                if (ratio > 0f) sb.Append("   (x").Append(ratio.ToString("0.0")).Append(" dari tier bawah)");
                sb.Append('\n');

                pair.Value.Sort();
                foreach (var line in pair.Value) sb.Append(line).Append('\n');
                sb.Append('\n');

                previousMedian = median;
            }

            sb.Append(Outliers(byStar, totals));
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// How many enemies one cast is expected to touch. This is where the archetypes stop being
        /// comparable by their damage field alone.
        /// </summary>
        static float ExpectedTargets(PieceDefinition p)
        {
            switch (p.Kind)
            {
                case CastKind.Projectile:
                    return 1f + p.Bounces;

                case CastKind.Chain:
                    return Mathf.Max(1, p.Forks) * Mathf.Max(1, p.Hits);

                case CastKind.Radial:
                    // Not every arm finds something, and bounces only pay off in a crowd.
                    return Mathf.Max(2, p.Hits) * (1f + p.Bounces) * 0.55f;

                case CastKind.Nova:
                case CastKind.AreaAtTarget:
                    return InRadius(p.Radius);

                case CastKind.Line:
                    return Mathf.Min(MaxTargets, p.Range * Mathf.Max(0.6f, p.Radius) * Density * 2f);

                case CastKind.Zone:
                {
                    float ticks = p.ZoneTickInterval <= 0f ? 1f : p.ZoneDuration / p.ZoneTickInterval;
                    return InRadius(p.Radius) * ticks;
                }

                case CastKind.Detonate:
                    // Damage is per POINT, so the point count is part of the multiplier.
                    return p.MaxDetonations * TypicalPoints;

                default:
                    return 1f;
            }
        }

        static float InRadius(float radius) =>
            Mathf.Min(MaxTargets, Mathf.PI * radius * radius * Density);

        /// <summary>Anything that outproduces the tier above it, which is the failure this catches.</summary>
        static string Outliers(SortedDictionary<int, List<string>> byStar,
            SortedDictionary<int, List<float>> totals)
        {
            var sb = new System.Text.StringBuilder("MELENCENG:\n");
            bool any = false;

            foreach (var pair in totals)
            {
                if (!totals.ContainsKey(pair.Key + 1)) continue;

                var above = totals[pair.Key + 1];
                above.Sort();
                float nextMedian = above[above.Count / 2];

                foreach (var value in pair.Value)
                {
                    if (value <= nextMedian) continue;

                    sb.Append("  ada ").Append(pair.Key).Append(" bintang dengan ")
                        .Append(value.ToString("0")).Append(" dps, melewati median ")
                        .Append(pair.Key + 1).Append(" bintang (").Append(nextMedian.ToString("0"))
                        .Append(")\n");

                    any = true;
                }
            }

            return any ? sb.ToString() : "MELENCENG: tidak ada. Tangganya menanjak rapi.\n";
        }
    }
}
