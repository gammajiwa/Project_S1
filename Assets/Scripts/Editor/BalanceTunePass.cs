using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Solves every skill's damage and mana cost from a target throughput, instead of picking
    /// numbers per skill and hoping the ladder holds.
    ///
    /// Hand-picked damage cannot stay even, because the archetypes reach wildly different numbers
    /// of enemies: 26 damage on a detonator that cashes in two dozen marks beat 340 on a blast that
    /// covers eight, and the printed numbers said the opposite. The audit measured the result —
    /// 3-star to 4-star was a x1.5 step while every other step was near x4, and single tiers spanned
    /// 10 to 62 dps.
    ///
    /// So the direction is inverted here. Pick what a tier is WORTH, divide by how many enemies the
    /// archetype touches, and let the damage number fall out. Role multipliers keep it from being
    /// perfectly flat, and each one is a statement about reliability rather than a nudge:
    /// a single-target hit never misses, a zone can be walked out of, a detonator needs a partner
    /// piece on the board before it does anything at all.
    ///
    /// Idempotent, and it deliberately leaves Heal, Cleanse and sigils alone — they are not damage.
    /// </summary>
    public static class BalanceTunePass
    {
        const string Root = "Assets/GameData";

        const float Density = 0.12f;
        const float MaxTargets = 50f;
        const float TypicalPoints = 3f;

        /// <summary>Throughput each tier is meant to be worth, in damage per second.</summary>
        static readonly float[] TargetDps = { 0f, 22f, 68f, 190f, 520f, 1350f };

        /// <summary>
        /// Mana per second each tier is meant to cost — for the WHOLE BOARD, not for one skill.
        ///
        /// This used to be a per-skill figure, and the model never asked how many skills a player
        /// actually runs at once. A full 5-star board came out needing 96 mana a second against a
        /// regen of 10, so the best build in the game fired at a tenth of its printed rate and died
        /// on wave 20 while its damage numbers said it should sweep the field. Measured back to
        /// back on the same wave with only mana regen changed: mana-starved it died at 20 seconds
        /// with 145 kills; fuelled it held full health with 499.
        /// </summary>
        static readonly float[] TargetManaPerSecond = { 0f, 5f, 8.5f, 12f, 15f, 18f };

        /// <summary>
        /// How many skills a filled board runs at once. The per-tier budget above is divided by
        /// this, because it describes the whole book's appetite rather than one page of it.
        ///
        /// Measured, not assumed: a 7x7 skill layer is 49 cells and 5-star footprints are 8-9, so
        /// four of them fill it. Smaller pieces push it toward six.
        /// </summary>
        const float SkillsOnAFullBoard = 5f;

        /// <summary>Charges a spender is assumed to be holding when it finally fires.</summary>
        const float AssumedCharges = 4f;

        [MenuItem("Tools/Grimoire/Rebalance by Throughput")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[BalanceTune] ContentDatabase.asset tidak ketemu.");
                return;
            }

            var balance = AssetDatabase.LoadAssetAtPath<GameBalance>(Root + "/GameBalance.asset");
            if (balance == null)
            {
                Debug.LogError("[BalanceTune] GameBalance.asset tidak ketemu.");
                return;
            }

            // Biaya mana lahir dari "mana per detik x cooldown", dan itu meledak di cooldown panjang:
            // skill 10 detik keluar di 180 sementara mana dasar cuma 120, jadi ia TIDAK PERNAH bisa
            // dinyalakan sama sekali. Kegagalannya senyap — piece-nya ada di papan, terlihat sehat,
            // dan tidak pernah menembak seumur hidup run itu.
            float manaCeiling = balance.BaseMaxMana * 0.75f;

            // Detonators scale with enemy count and nothing else caps them, so the cap IS their
            // rarity. Without it a 2-star outproduces a 5-star the moment a wave gets big.
            Cap(db, "sunder", 8);
            Cap(db, "rupture", 14);
            Cap(db, "reckoning", 22);

            int tuned = 0;

            for (int i = 0; i < db.Pieces.Count; i++)
            {
                var p = db.Pieces[i];
                if (p == null || p.IsRune || p.IsPassive) continue;
                if (!IsWeapon(p.Kind)) continue;

                int star = Mathf.Clamp(p.Stars, 1, 5);
                float targets = ExpectedTargets(p);
                if (targets <= 0f || p.BaseCooldown <= 0f) continue;

                float wanted = TargetDps[star] * RoleMultiplier(p);

                // A charge spender refuses to fire until it is loaded, so the loaded number is its
                // real number — its base has to be divided back down by what it will be multiplied by.
                if (p.ConsumesCharge != null)
                {
                    wanted /= 1f + AssumedCharges * p.DamagePerCharge;
                }

                p.BaseDamage = Round(wanted * p.BaseCooldown / targets);
                p.ManaCost = Mathf.Round(Mathf.Clamp(
                    TargetManaPerSecond[star] / SkillsOnAFullBoard * p.BaseCooldown, 1f, manaCeiling));

                EditorUtility.SetDirty(p);
                tuned++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BalanceTune] {tuned} skill dihitung ulang dari throughput target " +
                      $"{TargetDps[1]}/{TargetDps[2]}/{TargetDps[3]}/{TargetDps[4]}/{TargetDps[5]} dps.");
            Selection.activeObject = db;
        }

        static void Cap(ContentDatabase db, string id, int max)
        {
            var piece = db.ById(id);
            if (piece == null) return;

            piece.MaxDetonations = max;
            EditorUtility.SetDirty(piece);
        }

        /// <summary>
        /// Whether this kind's BaseDamage means "damage" at all.
        ///
        /// It does not on a Ward (the number is absorption), a Heal (health), a Restore (mana) or a
        /// Surge (nothing — the buff asset carries the effect). Solving those against a damage
        /// throughput target would hand a 5-star shield a 1350-point absorb and quietly make the
        /// defensive half of the book strictly better than the offensive half.
        /// </summary>
        static bool IsWeapon(CastKind kind)
        {
            switch (kind)
            {
                case CastKind.Heal:
                case CastKind.Cleanse:
                case CastKind.Ward:
                case CastKind.Surge:
                case CastKind.Restore:
                case CastKind.Blink:
                    return false;

                default:
                    return true;
            }
        }

        /// <summary>
        /// How reliable this archetype is, expressed as a share of its tier's budget. Reliability
        /// is what a player actually pays for: a hit that always lands is worth more per point of
        /// damage than one that might catch nothing.
        /// </summary>
        static float RoleMultiplier(PieceDefinition p)
        {
            switch (p.Kind)
            {
                // Never wastes a cast, never overkills a crowd it cannot reach.
                case CastKind.Projectile: return 1.15f;

                // Damage arrives over seconds and the crowd can simply leave.
                case CastKind.Zone: return 0.85f;

                // Dead weight until a marker piece is also on the board.
                case CastKind.Detonate: return 0.9f;

                // Half the arms find nothing when the field is thin.
                case CastKind.Radial: return 0.95f;

                // Telegraphed, so the crowd gets a chance to walk out from under it. It is the only
                // cast in the book that can miss after it has already been paid for.
                case CastKind.SunStrike: return 1.2f;

                // Buys time rather than kills. Its lift is worth more than its damage, so the
                // damage budget has to come down or it beats a pure attack at both jobs.
                case CastKind.Vortex: return 0.5f;

                // Sold on the knockback. The damage is a courtesy.
                case CastKind.ForcePush: return 0.55f;

                default: return 1f;
            }
        }

        static float ExpectedTargets(PieceDefinition p)
        {
            switch (p.Kind)
            {
                case CastKind.Projectile: return 1f + p.Bounces;
                case CastKind.Chain: return Mathf.Max(1, p.Forks) * Mathf.Max(1, p.Hits);
                case CastKind.Radial: return Mathf.Max(2, p.Hits) * (1f + p.Bounces) * 0.55f;

                case CastKind.Nova:
                case CastKind.AreaAtTarget: return InRadius(p.Radius);

                case CastKind.Line:
                    return Mathf.Min(MaxTargets, p.Range * Mathf.Max(0.6f, p.Radius) * Density * 2f);

                case CastKind.Zone:
                {
                    float ticks = p.ZoneTickInterval <= 0f ? 1f : p.ZoneDuration / p.ZoneTickInterval;
                    return InRadius(p.Radius) * ticks;
                }

                case CastKind.Detonate: return p.MaxDetonations * TypicalPoints;

                // Each shard is one enemy, and only the ones that actually launch pay off.
                case CastKind.Orbit: return Mathf.Max(1, p.Hits) * 0.8f;

                case CastKind.SunStrike: return InRadius(p.Radius);
                case CastKind.ForcePush: return InRadius(p.Radius);

                // Ploughs a lane: length by width, same shape as a Line.
                case CastKind.RollingBall:
                    return Mathf.Min(MaxTargets, p.Range * Mathf.Max(0.8f, p.Radius) * Density * 2f);

                case CastKind.Vortex:
                {
                    float ticks = p.ZoneDuration / 0.25f;
                    return InRadius(p.Radius) * ticks;
                }

                default: return 1f;
            }
        }

        static float InRadius(float radius) =>
            Mathf.Min(MaxTargets, Mathf.PI * radius * radius * Density);

        /// <summary>Readable numbers. Nobody wants to read 47.3183 on a card.</summary>
        static float Round(float value)
        {
            if (value < 1f) return Mathf.Max(0.5f, Mathf.Round(value * 10f) / 10f);
            if (value < 20f) return Mathf.Round(value);
            if (value < 100f) return Mathf.Round(value / 2f) * 2f;
            return Mathf.Round(value / 5f) * 5f;
        }
    }
}
