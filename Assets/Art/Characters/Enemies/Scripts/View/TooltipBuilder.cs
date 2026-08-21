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

        // Ukuran huruf kartu. Sama alasannya dengan warna di bawah: angka yang diketik ulang di
        // tiap baris adalah cara termurah membuat kartu ini berantakan lagi — dan itu memang yang
        // sudah terjadi sekali.
        //
        // Badan teks kartu (TooltipCard.prefab -> Body) berukuran 22. Dulu judulnya ditulis
        // <size=17> — LEBIH KECIL daripada isinya — jadi kartu ini tidak punya "atas": mata tidak
        // menemukan tempat memulai, dan seluruhnya terbaca sebagai satu gumpalan. Dan sub-barisnya
        // <size=11>, tepat separuh badan; di kartu selebar 520 px itu bukan teks kecil, itu teks
        // yang tidak terbaca sama sekali. Baris yang tidak terbaca bukan informasi, ia kebisingan
        // yang memakan tempat.
        //
        // Sekarang tiga tingkat saja, dan urutannya benar: judul > angka > keterangan.
        const string Head = "<size=26>";   // nama benda. Satu-satunya yang lebih besar dari angka.
        const string Sub = "<size=20>";    // keterangan: jenis, asal, harga, catatan.
                                           // 20, bukan 16: badan kartu 22, dan 16 masih
                                           // dilaporkan tidak kebaca. Yang membedakan
                                           // tingkatan sekarang WARNA (abu redup), bukan
                                           // ukuran - ukuran cuma perlu cukup beda untuk
                                           // terbaca sebagai keterangan, bukan untuk
                                           // memaksa mata menyipit.
        const string SizeEnd = "</size>";

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
        /// 1. <b>Kepala</b> (26) — nama dan bintang, lalu satu baris keterangan (16): jenis benda
        ///    dan dari mana ia datang. Ini yang menjawab "benda apa ini".
        /// 2. <b>Angka</b> (22, ukuran badan kartu) — tanpa hiasan. Ini yang dicari mata saat
        ///    membandingkan dua piece, jadi ia tidak boleh berbagi ukuran dengan apa pun di atas
        ///    atau di bawahnya.
        /// 3. <b>Kaki</b> (16, redup) — harga jual. Satu baris, tanpa garis pemisah.
        ///
        /// <b>Kartu ini pernah dirombak karena "kebanyakan informasi, layoutnya bikin pusing".</b>
        /// Yang dibuang: bentuk dan jumlah petak (sedang dipandangi pemain saat itu juga), garis
        /// pemisah (pita gelap yang membelah kartu demi satu angka kecil), dan blurb (kalimat
        /// suasana pada ukuran yang tidak terbaca). Yang diperbaiki: judulnya dulu <c>size=17</c>
        /// sementara badan kartu 22 — <b>judulnya lebih kecil daripada isinya</b>, jadi kartu ini
        /// tidak punya tempat untuk mulai membaca dan seluruhnya terbaca sebagai satu gumpalan.
        ///
        /// Aturan yang menyusul dari itu: <b>kalau sebuah baris tidak cukup besar untuk dibaca,
        /// ia bukan informasi — ia kebisingan yang memakan tempat.</b> Buang atau besarkan; jangan
        /// dikecilkan supaya "muat".
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

            _sb.Append(Head).Append("<b>").Append(Loc.T("piece." + def.Id + ".name", def.DisplayName))
               .Append("</b>").Append(SizeEnd);
            if (def.Stars > 0)
                _sb.Append("  ").Append(Gold).Append(Shapes.StarText(def.Stars)).Append(End);
            _sb.Append('\n');

            _sb.Append(Sub).Append(Grey).Append(TypeLine(def)).Append(End);
            if (!string.IsNullOrEmpty(tag))
                _sb.Append(Grey).Append("  ·  ").Append(End).Append(Blue).Append(tag).Append(End);
            _sb.Append(SizeEnd).Append('\n');

            if (note != null)
                _sb.Append(Sub).Append(Warn).Append(note).Append(End).Append(SizeEnd).Append('\n');

            _sb.Append('\n');

            if (def.Layer == Layer.Rune) AppendRune(def);
            else if (def.Kind == CastKind.Passive) AppendSigil(def, spell, origin);
            else AppendSkill(def, spell);

            // Kaki kartu: harga jual saja, satu baris.
            //
            // GARIS PEMISAH DICABUT. Di kartu selebar ini ia jadi pita gelap selebar penuh yang
            // membelah kartu jadi dua benda — padahal yang dipisahkannya cuma satu angka kecil.
            // Turun ukuran dan turun warna sudah memisahkan dengan sendirinya, tanpa menggambar
            // apa pun.
            //
            // BLURB DICABUT. Ia kalimat suasana, bukan angka yang dipakai memutuskan, dan ia
            // ditulis pada ukuran yang tidak terbaca — jadi ia menghabiskan dua sampai tiga baris
            // kartu untuk sesuatu yang bahkan tidak sampai ke mata pemain. Teksnya sendiri tidak
            // hilang dari project: `def.Blurb` tetap ada dan tetap dipakai di tempat lain.
            _sb.Append(Sub).Append(Dim)
               .Append(Loc.F("tip.sell", _balance.SellValueOf(def)))
               .Append(End).Append(SizeEnd);

            return _sb.ToString();
        }

        /// <summary>
        /// Jenis benda ini, dan untuk rune juga elemennya. Itu saja.
        ///
        /// <b>Bentuk dan jumlah petak DICABUT dari baris ini.</b> Dulu ia berbunyi
        /// "base rune · LINE 3 · 3 cells · Lightning" — empat fakta berjejal di baris terkecil di
        /// kartu, dan dua di antaranya menjawab pertanyaan yang tidak sedang ditanyakan siapa pun:
        /// bentuk piece-nya sedang DIPANDANGI pemain saat itu juga — kartunya muncul karena
        /// kursornya ada di atas piece itu — dan jumlah petak tinggal dihitung dari bentuk yang
        /// sama. Menuliskannya ulang tidak menambah apa pun, tapi ia memakan baris yang sama
        /// dengan jenis dan asal, yang keduanya TIDAK bisa dilihat dari gambar.
        ///
        /// Elemen tetap tinggal untuk rune, karena dialah yang menentukan skill mana yang dapat
        /// bonus di atasnya — dan itu tidak kelihatan dari bentuknya sama sekali.
        /// </summary>
        static string TypeLine(PieceDefinition def)
        {
            string kind = def.Layer == Layer.Rune ? Loc.T("tip.kind.rune")
                : def.Kind == CastKind.Passive ? Loc.T("tip.kind.sigil")
                : KindName(def.Kind);

            if (def.Layer == Layer.Rune) kind += Loc.F("tip.typeline.element", def.Element);
            return kind;
        }

        void AppendRune(PieceDefinition def)
        {
            int cells = Mathf.Max(1, def.Cells.Length);
            int perCell = Mathf.RoundToInt(def.AuraValue / cells * 100f);

            switch (def.Aura)
            {
                case AuraKind.DamagePct:
                    AppendAura("+", Loc.T("tip.aura.damage"), def.AuraValue, perCell);
                    break;
                case AuraKind.CooldownPct:
                    AppendAura("-", Loc.T("tip.aura.cooldown"), def.AuraValue, perCell);
                    break;
                case AuraKind.RadiusPct:
                    AppendAura("+", Loc.T("tip.aura.area"), def.AuraValue, perCell);
                    break;
                default:
                    _sb.Append(Loc.T("tip.rune.plain")).Append('\n');
                    break;
            }

            if (def.ElementMatchBonus > 0f)
            {
                _sb.Append(Loc.F("tip.rune.element", def.Element,
                    Mathf.RoundToInt(def.ElementMatchBonus * 100f))).Append('\n');
            }

            AppendStats(def);
        }

        void AppendAura(string sign, string what, float value, int perCell)
        {
            _sb.Append(Loc.F("tip.aura", sign, Mathf.RoundToInt(value * 100f), what, perCell))
                .Append('\n');
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

            _sb.Append(Sub).Append(aktif ? Good : Warn)
                .Append(Loc.T(aktif ? "tip.sigil.active" : "tip.sigil.inactive"))
                .Append(End).Append(SizeEnd).Append('\n');
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
                case StatKind.MaxHp: return Loc.F("stat.maxhp", mod.Value.ToString("0"));
                case StatKind.MaxMana: return Loc.F("stat.maxmana", mod.Value.ToString("0"));
                case StatKind.ManaRegen: return Loc.F("stat.manaregen", mod.Value.ToString("0.0"));
                case StatKind.HpRegen: return Loc.F("stat.hpregen", mod.Value.ToString("0.0"));
                case StatKind.Defense: return Loc.F("stat.defense", mod.Value.ToString("0.#"));
                case StatKind.AilmentPoints: return Loc.F("stat.ailmentpoints", mod.Value.ToString("0"));
                case StatKind.MoveSpeed: return Loc.F("stat.movespeed", Sign(mod.Value), Mathf.Abs(mod.Value).ToString("0.0"));
                case StatKind.DebuffResist: return Loc.F("stat.debuffresist", Sign(mod.Value), Pct(mod.Value));
                // Tandanya DIBACA, bukan diasumsikan. Keduanya dipakai sebagai `1 − nilai`, jadi
                // nilai negatif berarti LEBIH MAHAL / LEBIH LAMBAT — dan baris yang selalu menulis
                // "−" mengabarkan kebalikan persis dari yang sedang terjadi. Sebelum ada kutukan
                // dan pakta, tidak ada yang pernah negatif, jadi kebohongannya belum bisa muncul.
                case StatKind.ManaCostPct:
                    return Loc.F("stat.manacost", mod.Value < 0f ? "+" : "-", Pct(mod.Value));

                case StatKind.CooldownPct:
                    return Loc.F("stat.cooldown", mod.Value < 0f ? "+" : "-", Pct(mod.Value));
                case StatKind.DamagePct: return Loc.F("stat.damage", Pct(mod.Value));
                case StatKind.AreaPct: return Loc.F("stat.area", Pct(mod.Value));
                case StatKind.RangePct: return Loc.F("stat.range", Pct(mod.Value));
                case StatKind.CritChance: return Loc.F("stat.critchance", Pct(mod.Value));
                case StatKind.CritDamage: return Loc.F("stat.critdamage", Pct(mod.Value));
                case StatKind.FireDamagePct: return Loc.F("stat.firedamage", Pct(mod.Value));
                case StatKind.IceDamagePct: return Loc.F("stat.icedamage", Pct(mod.Value));
                case StatKind.LightningDamagePct: return Loc.F("stat.lightningdamage", Pct(mod.Value));

                // Stat perilaku. Ditulis sebagai kalimat, bukan sebagai angka bertanda: yang dibeli
                // bukan besaran melainkan kata kerja, dan "+2 BonusBounces" tidak memberi tahu
                // pemain bahwa pelurunya sekarang MEMANTUL.
                case StatKind.BonusBounces:
                    return Loc.F("stat.bounces", mod.Value.ToString("0"));

                case StatKind.BonusForks:
                    return Loc.F("stat.forks", mod.Value.ToString("0"));

                case StatKind.BonusHits:
                    return Loc.F("stat.hits", mod.Value.ToString("0"));

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

            if (!string.IsNullOrEmpty(def.Blurb))
                _sb.Append('\n').Append(Loc.T("buff." + def.Id + ".blurb", def.Blurb));
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
            _sb.Append(Loc.F(def.Kind == CastKind.Heal ? "tip.skill.heal" : "tip.skill.damage",
                BigNumber.Short(dmg)));
            _sb.Append(Loc.F("tip.skill.cd", cd.ToString("0.00")));
            _sb.Append(Loc.F("tip.skill.mana", Mathf.RoundToInt(def.ManaCost))).Append('\n');

            if (range > 0f) _sb.Append(Loc.F("tip.skill.range", range.ToString("0.0")));
            if (def.Kind == CastKind.Nova)
                _sb.Append(Loc.F("tip.skill.radius", radius.ToString("0.0")));
            if (def.Hits > 1)
            {
                _sb.Append(Loc.F(def.Kind == CastKind.Radial ? "tip.skill.dirs" : "tip.skill.jumps",
                    def.Hits));
            }

            if (def.Forks > 1) _sb.Append(Loc.F("tip.skill.forks", def.Forks));
            if (def.Bounces > 0) _sb.Append(Loc.F("tip.skill.bounces", def.Bounces));
            if (def.ZoneDrift > 0f) _sb.Append(Loc.T("tip.skill.drift"));

            if (def.Kind == CastKind.Detonate && def.TriggerStatus != null)
            {
                _sb.Append(Loc.F("tip.skill.detonate",
                    Loc.T("status." + def.TriggerStatus.Id + ".name", def.TriggerStatus.DisplayName)));
            }
            if (range > 0f || def.Hits > 1) _sb.Append('\n');

            if (def.AppliedStatus != null)
            {
                _sb.Append(Loc.F("tip.skill.applies",
                    Loc.T("status." + def.AppliedStatus.Id + ".name", def.AppliedStatus.DisplayName)));
                if (def.AppliedPoints > 1)
                    _sb.Append(Loc.F("tip.skill.applies.points", def.AppliedPoints));
                _sb.Append(Loc.F("tip.skill.applies.dur", def.StatusDuration.ToString("0.0"))).Append('\n');
            }

            AppendStats(def);

            if (spell == null)
            {
                _sb.Append(Sub).Append(Warn)
                    .Append(Loc.T("tip.skill.base")).Append(End).Append(SizeEnd).Append('\n');
                return;
            }

            if (spell.DamageBonus <= 0f && spell.CooldownBonus <= 0f && spell.RadiusBonus <= 0f)
            {
                _sb.Append(Sub).Append(Grey)
                    .Append(Loc.T("tip.skill.nobuff")).Append(End).Append(SizeEnd).Append('\n');
                return;
            }

            _sb.Append(Blue).Append(Loc.T("tip.skill.frombelow"));
            if (spell.DamageBonus > 0f)
                _sb.Append(Loc.F("tip.skill.dmgbonus", Mathf.RoundToInt(spell.DamageBonus * 100f)));
            if (spell.CooldownBonus > 0f)
                _sb.Append(Loc.F("tip.skill.cdbonus", Mathf.RoundToInt(spell.CooldownBonus * 100f)));
            if (spell.RadiusBonus > 0f)
                _sb.Append(Loc.F("tip.skill.areabonus", Mathf.RoundToInt(spell.RadiusBonus * 100f)));
            _sb.Append(End).Append('\n');
        }

        public static string KindName(CastKind kind)
        {
            switch (kind)
            {
                case CastKind.Projectile: return Loc.T("kind.projectile");
                case CastKind.Nova: return Loc.T("kind.nova");
                case CastKind.Chain: return Loc.T("kind.chain");
                case CastKind.Heal: return Loc.T("kind.heal");
                case CastKind.AreaAtTarget: return Loc.T("kind.areaattarget");
                case CastKind.Line: return Loc.T("kind.line");
                case CastKind.Zone: return Loc.T("kind.zone");
                case CastKind.Passive: return Loc.T("kind.passive");
                case CastKind.Cleanse: return Loc.T("kind.cleanse");
                case CastKind.Radial: return Loc.T("kind.radial");
                case CastKind.Detonate: return Loc.T("kind.detonate");
                default: return Loc.T("kind.rune");
            }
        }

    }
}
