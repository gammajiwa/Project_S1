using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Turns a piece into the text shown on hover. Pulled out of GrimoireUI because none of it
    /// touches the canvas — it is string building, and it was a fifth of that file.
    ///
    /// Owning the inventory is left to the caller: <paramref name="ownedCount"/> is injected so this
    /// class never has to know about the grimoire, the bag, the floor and the cursor separately.
    /// </summary>
    public class TooltipBuilder
    {
        readonly ContentDatabase _db;
        readonly GameBalance _balance;
        readonly System.Func<PieceDefinition, int> _ownedCount;
        readonly StringBuilder _sb = new StringBuilder(512);

        public TooltipBuilder(ContentDatabase db, GameBalance balance,
            System.Func<PieceDefinition, int> ownedCount)
        {
            _db = db;
            _balance = balance;
            _ownedCount = ownedCount;
        }

        public string Build(PieceDefinition def, CompiledSpell spell, string origin)
        {
            _sb.Length = 0;
            _sb.Append(def.DisplayName).Append("  ").Append(Shapes.StarText(def.Stars));
            _sb.Append("   [").Append(origin).Append("]\n");

            if (def.Layer == Layer.Rune) AppendRune(def);
            else if (def.Kind == CastKind.Passive) AppendSigil(def, spell, origin);
            else AppendSkill(def, spell);

            _sb.Append("harga jual ").Append(_balance.SellValueOf(def)).Append(" koin\n");
            _sb.Append(def.Blurb);
            return _sb.ToString();
        }

        void AppendRune(PieceDefinition def)
        {
            int cells = Mathf.Max(1, def.Cells.Length);
            int perCell = Mathf.RoundToInt(def.AuraValue / cells * 100f);

            _sb.Append("RUNE - alas, ").Append(cells).Append(" petak, elemen ")
                .Append(def.Element).Append('\n');

            switch (def.Aura)
            {
                case AuraKind.DamagePct:
                    AppendAura("+", "damage", def.AuraValue, perCell);
                    break;
                case AuraKind.CooldownPct:
                    AppendAura("-", "cooldown", def.AuraValue, perCell);
                    break;
                case AuraKind.RadiusPct:
                    AppendAura("+", "area", def.AuraValue, perCell);
                    break;
                default:
                    _sb.Append("alas polos, nggak ngasih aura\n");
                    break;
            }

            if (def.ElementMatchBonus > 0f)
            {
                _sb.Append("skill ber-elemen ").Append(def.Element).Append(" di atasnya: +")
                    .Append(Mathf.RoundToInt(def.ElementMatchBonus * 100f))
                    .Append("% damage TOTAL\n");
            }

            AppendStats(def);
        }

        void AppendAura(string sign, string what, float value, int perCell)
        {
            _sb.Append(sign).Append(Mathf.RoundToInt(value * 100f)).Append("% ").Append(what)
                .Append(" TOTAL  (").Append(perCell).Append("% per petak)\n");
        }

        void AppendSigil(PieceDefinition def, CompiledSpell spell, string origin)
        {
            _sb.Append("SEGEL pasif - ").Append(def.Cells.Length).Append(" petak, nggak nembak\n");

            // Reads Stats[], not the retired Stat/StatValue pair — sigils were migrated onto the
            // array, and this panel silently went blank for every one of them until it followed.
            AppendStats(def);

            _sb.Append(spell == null && origin == "KEPASANG"
                ? "aktif\n"
                : "harus berdiri di atas rune biar aktif\n");
        }

        void AppendStats(PieceDefinition def)
        {
            if (def.Stats == null || def.Stats.Length == 0) return;

            for (int i = 0; i < def.Stats.Length; i++)
            {
                _sb.Append(Describe(def.Stats[i])).Append('\n');
            }
        }

        static string Describe(StatModifier mod)
        {
            switch (mod.Type)
            {
                case StatKind.MaxHp: return "+" + mod.Value.ToString("0") + " HP maksimum";
                case StatKind.MaxMana: return "+" + mod.Value.ToString("0") + " mana maksimum";
                case StatKind.ManaRegen: return "+" + mod.Value.ToString("0.0") + " mana / detik";
                case StatKind.HpRegen: return "+" + mod.Value.ToString("0.0") + " HP / detik";
                case StatKind.Defense: return "+" + mod.Value.ToString("0.#") + " pertahanan";
                case StatKind.AilmentPoints: return "+" + mod.Value.ToString("0") + " poin ailment per tempel";
                case StatKind.ManaCostPct: return "-" + Pct(mod.Value) + "% biaya mana";
                case StatKind.CooldownPct: return "-" + Pct(mod.Value) + "% cooldown";
                case StatKind.DamagePct: return "+" + Pct(mod.Value) + "% damage";
                case StatKind.AreaPct: return "+" + Pct(mod.Value) + "% area";
                case StatKind.RangePct: return "+" + Pct(mod.Value) + "% jangkauan";
                case StatKind.CritChance: return "+" + Pct(mod.Value) + "% peluang crit";
                case StatKind.CritDamage: return "+" + Pct(mod.Value) + "% damage crit";
                case StatKind.FireDamagePct: return "+" + Pct(mod.Value) + "% damage skill API";
                case StatKind.IceDamagePct: return "+" + Pct(mod.Value) + "% damage skill ES";
                case StatKind.LightningDamagePct: return "+" + Pct(mod.Value) + "% damage skill PETIR";
                default: return mod.Type + " " + mod.Value.ToString("0.##");
            }
        }

        static string Pct(float value) => Mathf.RoundToInt(value * 100f).ToString();

        void AppendSkill(PieceDefinition def, CompiledSpell spell)
        {
            float dmg = spell != null ? spell.Damage : def.BaseDamage;
            float cd = spell != null ? spell.Cooldown : def.BaseCooldown;
            float range = spell != null ? spell.Range : def.Range;
            float radius = spell != null ? spell.Radius : def.Radius;

            _sb.Append(KindName(def.Kind)).Append(" - ").Append(def.Cells.Length).Append(" petak\n");
            _sb.Append(def.Kind == CastKind.Heal ? "heal " : "damage ").Append(dmg.ToString("0.0"));
            _sb.Append("     cooldown ").Append(cd.ToString("0.00")).Append('s');
            _sb.Append("     mana ").Append(Mathf.RoundToInt(def.ManaCost)).Append('\n');

            if (range > 0f) _sb.Append("jangkauan ").Append(range.ToString("0.0"));
            if (def.Kind == CastKind.Nova) _sb.Append("     radius ledak ").Append(radius.ToString("0.0"));
            if (def.Hits > 1) _sb.Append("     target ").Append(def.Hits);
            if (range > 0f || def.Hits > 1) _sb.Append('\n');

            if (def.AppliedStatus != null)
            {
                _sb.Append("nempel ").Append(def.AppliedStatus.DisplayName);
                if (def.AppliedPoints > 1) _sb.Append(" ").Append(def.AppliedPoints).Append(" poin");
                _sb.Append(' ').Append(def.StatusDuration.ToString("0.0")).Append("s\n");
            }

            AppendStats(def);

            if (spell == null)
            {
                _sb.Append("(angka dasar - belum kepasang di atas rune)\n");
                return;
            }

            if (spell.DamageBonus <= 0f && spell.CooldownBonus <= 0f && spell.RadiusBonus <= 0f)
            {
                _sb.Append("rune di bawahnya nggak ngasih buff\n");
                return;
            }

            _sb.Append("dari rune di bawah:");
            if (spell.DamageBonus > 0f)
                _sb.Append("  +").Append(Mathf.RoundToInt(spell.DamageBonus * 100f)).Append("% DMG");
            if (spell.CooldownBonus > 0f)
                _sb.Append("  -").Append(Mathf.RoundToInt(spell.CooldownBonus * 100f)).Append("% CD");
            if (spell.RadiusBonus > 0f)
                _sb.Append("  +").Append(Mathf.RoundToInt(spell.RadiusBonus * 100f)).Append("% AREA");
            _sb.Append('\n');
        }

        public static string KindName(CastKind kind)
        {
            switch (kind)
            {
                case CastKind.Projectile: return "proyektil";
                case CastKind.Nova: return "ledakan melingkar";
                case CastKind.Chain: return "sambaran beruntun";
                case CastKind.Heal: return "penyembuh";
                case CastKind.AreaAtTarget: return "ledakan di gerombolan";
                case CastKind.Line: return "sapuan garis";
                case CastKind.Zone: return "kubangan";
                case CastKind.Passive: return "segel pasif";
                default: return "alas";
            }
        }

        /// <summary>ALT + hover: every recipe this piece appears in, with a tick per owned part.</summary>
        public string BuildRecipeCard(PieceDefinition def)
        {
            _sb.Length = 0;
            _sb.Append("RESEP yang pakai ").Append(def.DisplayName).Append('\n');

            int found = 0;
            var seen = new Dictionary<PieceDefinition, int>();

            for (int i = 0; i < _db.Recipes.Count; i++)
            {
                var recipe = _db.Recipes[i];
                if (recipe.Result == null || !Uses(recipe, def)) continue;

                found++;
                seen.Clear();

                for (int k = 0; k < recipe.Ingredients.Length; k++)
                {
                    var ingredient = recipe.Ingredients[k];
                    if (ingredient == null) continue;

                    // Count duplicates separately: two Fireballs need two in hand, not one.
                    seen.TryGetValue(ingredient, out int used);
                    seen[ingredient] = used + 1;

                    if (k > 0) _sb.Append("  +  ");
                    _sb.Append(_ownedCount(ingredient) >= used + 1 ? "[v] " : "[ ] ")
                        .Append(ingredient.DisplayName);
                }

                _sb.Append("\n        =  ").Append(recipe.Result.DisplayName)
                    .Append("  ").Append(Shapes.StarText(recipe.Result.Stars)).Append('\n');
            }

            if (found == 0) _sb.Append("(belum ada resep yang pakai ini)\n");

            _sb.Append("\ntaruh bahannya BERSENTUHAN di grimoire - bentuk bebas.\n");
            _sb.Append("sorotan biru = belum lengkap, emas = jadi evo pas wave beres.");
            return _sb.ToString();
        }

        static bool Uses(RecipeDefinition recipe, PieceDefinition def)
        {
            for (int k = 0; k < recipe.Ingredients.Length; k++)
            {
                if (recipe.Ingredients[k] == def) return true;
            }

            return false;
        }
    }
}
