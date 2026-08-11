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
        /// Diukur, bukan ditebak — dan diukur ULANG saat tangga footprint dikecilkan.
        ///
        /// Angka lamanya 5, dan itu benar untuk tangga lama: lapisan skill 7x7 itu 49 petak dan
        /// footprint bintang 5 memakan 8-9, jadi empat saja sudah memenuhinya. Tangga baru
        /// (★1 satu petak, ★2 dua, ★3 tiga) menurunkan rata-rata footprint dari ~4,3 petak
        /// menjadi <b>2,67</b> — papan yang sama sekarang menampung sekitar 1,6 kali lebih banyak
        /// piece, jadi nafsu mana seluruh papan naik sebesar itu juga.
        ///
        /// Membiarkannya di 5 berarti mengulang persis bug yang dulu membuat build bintang 5
        /// terbaik di game menembak di 10% laju nominalnya dan mati di wave 20 sementara angka
        /// damage-nya bilang ia harusnya menyapu.
        /// </summary>
        const float SkillsOnAFullBoard = 8f;

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
            int unknown = 0;

            for (int i = 0; i < db.Pieces.Count; i++)
            {
                var p = db.Pieces[i];
                if (p == null || p.IsRune || p.IsPassive) continue;
                if (!IsWeapon(p.Kind)) continue;

                int star = Mathf.Clamp(p.Stars, 1, 5);
                float targets = ExpectedTargets(p);

                // Kind yang belum punya baris di ExpectedTargets. Dilewati DAN diteriakkan:
                // melewatinya diam-diam berarti angkanya tetap apa adanya di aset, dan itu
                // terlihat persis seperti solver yang sudah menyetujuinya.
                if (targets < 0f)
                {
                    Debug.LogError(
                        $"[BalanceTune] '{p.Id}' memakai CastKind.{p.Kind} yang BELUM didaftarkan " +
                        "di ExpectedTargets/RoleMultiplier. Damage-nya tidak dihitung ulang. " +
                        "Daftarkan dulu, jangan dibiarkan — angka yang tidak disolusikan tidak " +
                        "sebanding dengan satu pun skill lain di tiernya.", p);

                    unknown++;
                    continue;
                }

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
                      $"{TargetDps[1]}/{TargetDps[2]}/{TargetDps[3]}/{TargetDps[4]}/{TargetDps[5]} dps." +
                      (unknown > 0 ? $"  {unknown} DILEWATI karena kind-nya belum terdaftar." : ""));
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

                // --- gelombang kedua perilaku ---

                // Jangkauannya badan pemain sendiri: tidak bisa dibidik, dan memakainya berarti
                // berdiri di tempat yang paling berbahaya di lapangan. Yang dibayar bukan
                // ketidakandalannya melainkan harganya — dia menuntut posisi, bukan cuma petak.
                case CastKind.Orbital: return 0.8f;

                // Menara boleh ditinggal, dan gerombolan boleh pindah dari tempat ia berdiri.
                // Separuh nilainya bergantung pada tebakan pemain soal ke mana musuh akan datang.
                case CastKind.Turret: return 0.75f;

                // Tidak pernah meleset — rudalnya membelok mengejar. Sama dengan Projectile.
                case CastKind.Seeker: return 1.15f;

                // SATU sasaran, seumur hidup buku yang seluruhnya dituning untuk gerombolan.
                // Tanpa premi ini ia bukan pilihan melainkan kesalahan, dan boss — satu-satunya
                // hal yang ia jawab — tidak akan pernah punya jawaban khusus.
                case CastKind.Tether: return 1.25f;

                // Beraba-aba, jadi bisa MELESET setelah dibayar. Aturan yang sama dengan SunStrike.
                case CastKind.Barrage: return 1.2f;

                // Tepinya berjalan, jadi yang cepat bisa lari mendahuluinya.
                case CastKind.Shockwave: return 0.95f;

                // Ruas yang tidak menemukan siapa-siapa memantul di tepi layar dan hangus.
                case CastKind.Ricochet: return 1.05f;

                default: return 1f;
            }
        }

        /// <summary>
        /// Berapa musuh yang disentuh sekali cast. Publik karena <see cref="FootprintPass"/> ikut
        /// memakainya: jumlah petak bintang 4-5 diturunkan dari seberapa OP sebuah skill, dan
        /// "berapa yang kena" adalah setengah dari jawaban itu.
        /// </summary>
        public static float ExpectedTargets(PieceDefinition p)
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

                // ---------------------------------------------------------------------------
                //  gelombang kedua perilaku
                //
                //  Tiap kind WAJIB punya baris di sini. Yang tidak punya jatuh ke `default: 1`,
                //  dan itu bukan nilai netral melainkan pernyataan "skill ini mengenai satu
                //  musuh" — sebuah hujan hantaman yang menyapu tiga puluh musuh akan dikasih
                //  damage milik peluru tunggal, yaitu tiga puluh kali terlalu besar. Gagalnya
                //  senyap total: aset tersimpan rapi, kartunya terlihat wajar, dan yang meledak
                //  adalah keseimbangan seluruh tier.
                // ---------------------------------------------------------------------------

                // Cakram di badan pemain yang menagih berulang sepanjang durasinya. Bentuknya
                // sama persis dengan Zone; yang berbeda cuma bahwa pusatnya ikut berjalan.
                case CastKind.Orbital:
                {
                    float ticks = p.ZoneTickInterval <= 0f ? 1f : p.ZoneDuration / p.ZoneTickInterval;
                    return InRadius(p.Radius) * ticks;
                }

                // Membajak satu jalur, dua kali: pergi dan pulang. Bukan tepat dua — jalur
                // pulangnya tumpang tindih dengan jalur pergi, dan sebagian korbannya sudah mati.
                case CastKind.Boomerang:
                    return Mathf.Min(MaxTargets,
                        p.Range * Mathf.Max(0.8f, p.Radius) * Density * 2f) * 1.8f;

                // Satu ruas per pantulan. Sebagian ruas tidak menemukan musuh dan berakhir di
                // tepi layar — itu yang membuat angkanya di bawah jumlah pantulannya.
                case CastKind.Ricochet: return (1f + p.Bounces) * 0.6f;

                // Berapa kali ia menembak dikali berapa peluru per tembakan. Pantulan peluru ikut
                // dihitung karena menara memakai peluru yang sama dengan Projectile.
                case CastKind.Turret:
                {
                    float volleys = p.ZoneTickInterval <= 0f ? 1f : p.ZoneDuration / p.ZoneTickInterval;
                    return volleys * Mathf.Max(1, p.Hits) * (1f + p.Bounces);
                }

                // Tepinya menyapu seluruh cakram tepat sekali sepanjang perjalanannya keluar.
                case CastKind.Shockwave: return InRadius(p.Radius);

                // Satu rudal satu musuh, dan mereka dilarang mengejar sasaran yang sama.
                case CastKind.Seeker: return Mathf.Max(1, p.Hits) * 0.95f;

                // Satu sasaran, berkali-kali. Angkanya JUMLAH DENYUT, bukan jumlah musuh — dan
                // itu memang yang benar: throughput target dibagi ini menghasilkan damage per
                // denyut, dan denyut-denyut itu semuanya jatuh di satu makhluk.
                case CastKind.Tether:
                    return p.ZoneTickInterval <= 0f ? 1f : p.ZoneDuration / p.ZoneTickInterval;

                // Beberapa lingkaran yang saling bertindih. Sebarannya cuma 2,2 kali radius, jadi
                // hantaman kelima jatuh sebagian besar di tanah yang sudah dihantam.
                case CastKind.Barrage:
                    return InRadius(p.Radius) * Mathf.Max(2, p.Hits) * 0.6f;

                // BELUM DIDAFTARKAN. Sengaja negatif, bukan 1.
                //
                // Dulu barisnya `return 1f`, dan itu diam-diam berarti "skill ini mengenai satu
                // musuh". Tiap kind baru yang lupa didaftarkan otomatis mewarisi arti itu, lalu
                // dikasih damage milik peluru tunggal walaupun ia menyapu tiga puluh musuh. Tidak
                // ada error, tidak ada aset rusak, dan angkanya baru ketahuan salah setelah
                // seseorang memainkannya dan merasa satu skill mematahkan seluruh game.
                default: return -1f;
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
