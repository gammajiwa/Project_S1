using System.Text;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Turns a piece into the text shown on hover. Pulled out of GrimoireUI because none of it
    /// touches the canvas — it is string building, and it was a fifth of that file.
    ///
    /// Recipes moved out to <see cref="RecipePanel"/>, which draws them as icons — this is the stat
    /// card only, and it no longer needs to know what the player owns.
    /// </summary>
    public class TooltipBuilder
    {
        readonly GameBalance _balance;
        readonly StringBuilder _sb = new StringBuilder(512);

        public TooltipBuilder(GameBalance balance)
        {
            _balance = balance;
        }

        // Warna kartu. Dipisah jadi konstanta karena tiap potongan dipakai di beberapa tempat,
        // dan kode hex yang diketik ulang di tiap baris adalah cara termurah membuat dua baris
        // yang seharusnya sederajat pelan-pelan jadi beda warna.
        const string Grey = "<color=#8a8a9a>";
        const string Dim = "<color=#74747f>";
        const string Gold = "<color=#e6b45a>";
        const string Warn = "<color=#e8c05a>";
        const string Good = "<color=#7fd48a>";
        const string Blue = "<color=#8fb6e8>";
        const string End = "</color>";

        /// <summary>
        /// Kartu hover dalam TIGA lapis, dan urutannya yang menentukan apakah ia terbaca:
        ///
        /// 1. <b>Kepala</b> — nama (paling besar), bintang, jenis/bentuk/petak, dan status.
        ///    Ini yang menjawab "benda apa ini".
        /// 2. <b>Angka</b> — ukuran normal, tanpa hiasan. Ini yang dicari mata saat membandingkan
        ///    dua piece, jadi ia tidak boleh berbagi ukuran dan warna dengan apa pun di atas atau
        ///    di bawahnya.
        /// 3. <b>Kaki</b> — harga jual dan blurb, di balik garis, kecil dan redup. Keduanya bukan
        ///    angka yang dipakai mengambil keputusan; dulu mereka duduk di antara baris-baris
        ///    efek dengan ukuran dan warna yang sama, dan itu yang membuat seluruh kartu terbaca
        ///    sebagai satu blok teks tanpa awal.
        /// </summary>
        public string Build(PieceDefinition def, CompiledSpell spell, string origin)
        {
            _sb.Length = 0;

            // Status panjang dipecah: labelnya ikut baris jenis, alasannya turun sendiri. Tanpa
            // ini "KEPASANG - TERKUNCI, nggak ikut evolusi" membungkus jadi dua baris di tengah
            // kepala kartu dan mendorong namanya keluar dari pandangan.
            string tag = origin;
            string note = null;
            if (!string.IsNullOrEmpty(origin))
            {
                int dash = origin.IndexOf(" - ");
                if (dash > 0)
                {
                    tag = origin.Substring(0, dash);
                    note = origin.Substring(dash + 3);
                }
            }

            _sb.Append("<size=17><b>").Append(def.DisplayName).Append("</b></size>");
            if (def.Stars > 0)
                _sb.Append("  ").Append(Gold).Append(Shapes.StarText(def.Stars)).Append(End);
            _sb.Append('\n');

            _sb.Append("<size=11>").Append(Grey).Append(TypeLine(def)).Append(End);
            if (!string.IsNullOrEmpty(tag))
                _sb.Append(Grey).Append("  ·  ").Append(End).Append(Blue).Append(tag).Append(End);
            _sb.Append("</size>\n");

            if (note != null)
                _sb.Append("<size=11>").Append(Warn).Append(note).Append(End).Append("</size>\n");

            _sb.Append('\n');

            if (def.Layer == Layer.Rune) AppendRune(def);
            else if (def.Kind == CastKind.Passive) AppendSigil(def, spell, origin);
            else AppendSkill(def, spell);

            _sb.Append("<size=11>").Append(Dim).Append("————————————————————\n");
            _sb.Append("jual ").Append(_balance.SellValueOf(def)).Append(" koin").Append(End).Append("</size>");

            if (!string.IsNullOrEmpty(def.Blurb))
                _sb.Append("\n<size=11><i>").Append(Dim).Append(def.Blurb).Append(End).Append("</i></size>");

            return _sb.ToString();
        }

        /// <summary>Jenis, bentuk, dan luas — tiga fakta yang selalu ada, dalam satu baris.</summary>
        static string TypeLine(PieceDefinition def)
        {
            string kind = def.Layer == Layer.Rune ? "rune alas"
                : def.Kind == CastKind.Passive ? "segel pasif"
                : KindName(def.Kind);

            string line = kind + "  ·  " + Shapes.NameOf(def.Shape) + "  ·  " + def.Cells.Length + " petak";
            if (def.Layer == Layer.Rune) line += "  ·  " + def.Element;
            return line;
        }

        void AppendRune(PieceDefinition def)
        {
            int cells = Mathf.Max(1, def.Cells.Length);
            int perCell = Mathf.RoundToInt(def.AuraValue / cells * 100f);

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
            // Reads Stats[], not the retired Stat/StatValue pair — sigils were migrated onto the
            // array, and this panel silently went blank for every one of them until it followed.
            AppendStats(def);

            // Diperiksa lewat awalannya, bukan disamakan persis: labelnya membawa keterangan
            // tambahan saat piece-nya terkunci, dan perbandingan persis membuat segel terkunci
            // yang SUDAH kepasang diberi tahu bahwa ia belum berdiri di atas rune.
            bool aktif = spell == null && origin != null && origin.StartsWith("KEPASANG");

            _sb.Append("<size=11>").Append(aktif ? Good : Warn)
                .Append(aktif ? "aktif" : "harus berdiri di atas rune biar aktif")
                .Append(End).Append("</size>\n");
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
                case StatKind.MoveSpeed: return Sign(mod.Value) + mod.Value.ToString("0.0") + " kecepatan menghindar";
                case StatKind.DebuffResist: return Sign(mod.Value) + Pct(mod.Value) + "% tahan kutukan";
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

        /// <summary>Every stat a buff or curse changes, one per line. Used by the HUD strips.</summary>
        public string DescribeMods(BuffDefinition def)
        {
            if (def == null || def.Mods == null || def.Mods.Length == 0) return def?.Blurb ?? "";

            _sb.Length = 0;
            for (int i = 0; i < def.Mods.Length; i++)
            {
                if (_sb.Length > 0) _sb.Append('\n');
                _sb.Append(Describe(def.Mods[i]));
            }

            if (!string.IsNullOrEmpty(def.Blurb)) _sb.Append('\n').Append(def.Blurb);
            return _sb.ToString();
        }

        static string Pct(float value) => Mathf.RoundToInt(Mathf.Abs(value) * 100f).ToString();

        /// <summary>Debuff mods are negative, so the sign has to be read rather than assumed.</summary>
        static string Sign(float value) => value < 0f ? "-" : "+";

        void AppendSkill(PieceDefinition def, CompiledSpell spell)
        {
            float dmg = spell != null ? spell.Damage : def.BaseDamage;
            float cd = spell != null ? spell.Cooldown : def.BaseCooldown;
            float range = spell != null ? spell.Range : def.Range;
            float radius = spell != null ? spell.Radius : def.Radius;

            // Bentuk piece-nya sudah dibawa baris jenis di kepala kartu; yang di sini murni angka.
            _sb.Append(def.Kind == CastKind.Heal ? "heal " : "damage ").Append(BigNumber.Short(dmg));
            _sb.Append("  ·  cd ").Append(cd.ToString("0.00")).Append('s');
            _sb.Append("  ·  mana ").Append(Mathf.RoundToInt(def.ManaCost)).Append('\n');

            if (range > 0f) _sb.Append("jangkauan ").Append(range.ToString("0.0"));
            if (def.Kind == CastKind.Nova) _sb.Append("  ·  radius ledak ").Append(radius.ToString("0.0"));
            if (def.Hits > 1)
            {
                _sb.Append(def.Kind == CastKind.Radial ? "  ·  arah " : "  ·  lompatan ")
                    .Append(def.Hits);
            }

            if (def.Forks > 1) _sb.Append("  ·  cabang ").Append(def.Forks);
            if (def.Bounces > 0) _sb.Append("  ·  MEMANTUL ").Append(def.Bounces).Append('x');
            if (def.ZoneDrift > 0f) _sb.Append("  ·  MENGEMBARA");

            if (def.Kind == CastKind.Detonate && def.TriggerStatus != null)
            {
                _sb.Append("\nmeledakkan semua musuh ber-").Append(def.TriggerStatus.DisplayName)
                    .Append(", damage x POIN yang menumpuk");
            }
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
                _sb.Append("<size=11>").Append(Warn)
                    .Append("angka dasar - belum kepasang di atas rune").Append(End).Append("</size>\n");
                return;
            }

            if (spell.DamageBonus <= 0f && spell.CooldownBonus <= 0f && spell.RadiusBonus <= 0f)
            {
                _sb.Append("<size=11>").Append(Grey)
                    .Append("rune di bawahnya nggak ngasih buff").Append(End).Append("</size>\n");
                return;
            }

            _sb.Append(Blue).Append("dari rune di bawah:");
            if (spell.DamageBonus > 0f)
                _sb.Append("  +").Append(Mathf.RoundToInt(spell.DamageBonus * 100f)).Append("% DMG");
            if (spell.CooldownBonus > 0f)
                _sb.Append("  -").Append(Mathf.RoundToInt(spell.CooldownBonus * 100f)).Append("% CD");
            if (spell.RadiusBonus > 0f)
                _sb.Append("  +").Append(Mathf.RoundToInt(spell.RadiusBonus * 100f)).Append("% AREA");
            _sb.Append(End).Append('\n');
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
                case CastKind.Cleanse: return "pembersih kutukan";
                case CastKind.Radial: return "semburan segala arah";
                case CastKind.Detonate: return "peledak ailment";
                default: return "alas";
            }
        }

    }
}
