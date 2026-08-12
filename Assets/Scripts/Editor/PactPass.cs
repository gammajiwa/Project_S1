using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Dua puluh PAKTA: berkah permanen berpasangan dengan kutuk permanen, diambil sekaligus.
    ///
    /// Menggantikan node kejadian yang isinya "+80 koin" atau "bayar 45 koin dapat piece" — dua
    /// pilihan yang keduanya bukan keputusan. Yang pertama gratis, jadi tidak ada yang menolaknya;
    /// yang kedua cuma harga. Tidak satu pun mengubah cara run itu dimainkan.
    ///
    /// <b>Aturan penulisan yang dipegang seluruh daftar ini:</b>
    ///
    /// 1. Tidak ada pakta yang cuma untung. Yang tanpa harga bukan pakta, itu hadiah — dan hadiah
    ///    yang selalu diambil sama saja dengan tidak menawarkan apa-apa.
    /// 2. Tidak ada pakta yang cuma rugi. Yang tidak akan pernah dipilih siapa pun cuma memakan
    ///    slot di undian dan mengencerkan yang sungguhan.
    /// 3. Angkanya BESAR. Pakta yang menggeser lima persen tidak akan pernah terasa, dan pemain
    ///    berhenti membacanya setelah yang kedua. Yang ditawarkan di sini menggeser 30-100%, dan
    ///    beberapa di antaranya mengubah dari mana sumber daya berasal sama sekali.
    ///
    /// <b>Angka DATAR, bukan persen.</b> MaxHp, MaxMana, ManaRegen dan MoveSpeed dijumlahkan datar
    /// di seluruh game, jadi "nyawa −40%" ditulis −40 (dari dasar 100), dan "lari +80%" ditulis
    /// +2,2 (dari dasar 2,8). Yang benar-benar persen cuma yang berakhiran Pct.
    ///
    /// Tanda CooldownPct dan ManaCostPct TERBALIK dari dugaan: keduanya dipakai sebagai
    /// <c>1 − nilai</c>, jadi POSITIF berarti lebih cepat / lebih murah, dan NEGATIF berarti lebih
    /// lambat / lebih mahal.
    /// </summary>
    public static class PactPass
    {
        const string Root = "Assets/GameData";
        const string Folder = Root + "/Pacts";

        [MenuItem("Tools/Grimoire/Generate Pacts")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[PactPass] ContentDatabase.asset tidak ketemu.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Root, "Pacts");

            var pacts = new List<WorldModifierDefinition>();

            BuildTrades(pacts);
            BuildResource(pacts);
            BuildBody(pacts);
            BuildWorld(pacts);

            db.EditorSetPacts(pacts);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PactPass] {pacts.Count} pakta ditulis ke {Folder}.\n" +
                      "LANJUTKAN: Tools/Grimoire/Generate Placeholder Icons supaya tiap pakta " +
                      "punya ikon di strip HUD.");

            Selection.activeObject = db;
        }

        // =================================================================================
        //  tukar tambah lurus: dunia lebih galak, kamu lebih kuat
        // =================================================================================

        static void BuildTrades(List<WorldModifierDefinition> into)
        {
            var thick = P(into, "darahtebal", "DARAH TEBAL", new Color(0.75f, 0.2f, 0.22f),
                boon: "Damage seluruh bukumu +35%",
                bane: "Semua musuh bernyawa +40%",
                blurb: "\"Kutebalkan darah mereka. Kutebalkan juga tanganmu. Adil, bukan?\"");
            thick.Boon = Mods(M(StatKind.DamagePct, 0.35f));
            thick.EnemyHpMul = 1.40f;

            var glass = P(into, "kaca", "KACA", new Color(0.6f, 0.85f, 0.95f),
                boon: "Semua cooldown −25%",
                bane: "Musuh memukul 60% lebih keras",
                blurb: "\"Cepat. Tapi kaca yang cepat tetap kaca.\"");
            glass.Boon = Mods(M(StatKind.CooldownPct, 0.25f));
            glass.EnemyDamageMul = 1.60f;

            var chains = P(into, "rantaiberat", "RANTAI BERAT", new Color(0.45f, 0.42f, 0.5f),
                boon: "Damage +90%, area +25%",
                bane: "Semua cooldown 30% LEBIH LAMBAT",
                blurb: "\"Ayunan yang jarang boleh menghancurkan. Yang sering, tidak.\"");
            chains.Boon = Mods(M(StatKind.DamagePct, 0.9f), M(StatKind.AreaPct, 0.25f));
            chains.Bane = Mods(M(StatKind.CooldownPct, -0.30f));

            var blind = P(into, "kebutaan", "KEBUTAAN", new Color(0.2f, 0.2f, 0.26f),
                boon: "Damage BERLIPAT — +100%",
                bane: "Musuh 50% lebih cepat dan memukul 25% lebih keras",
                blurb: "\"Kuambil matamu, kuberi kau petir. Kau tidak akan melihat mereka datang.\"");
            blind.Boon = Mods(M(StatKind.DamagePct, 1f));
            blind.EnemySpeedMul = 1.50f;
            blind.EnemyDamageMul = 1.25f;

            var warlock = P(into, "kutuktukangsihir", "KUTUK TUKANG SIHIR",
                new Color(0.6f, 0.3f, 0.8f),
                boon: "Damage +70%, damage crit +80%",
                bane: "Semua skill 60% lebih mahal",
                blurb: "\"Kuasa selalu ada harganya. Harganya mana.\"");
            warlock.Boon = Mods(M(StatKind.DamagePct, 0.7f), M(StatKind.CritDamage, 0.8f));
            warlock.Bane = Mods(M(StatKind.ManaCostPct, -0.60f));

            var storm = P(into, "badai", "BADAI", new Color(0.55f, 0.75f, 1f),
                boon: "Area SEMUA skill +45%",
                bane: "Musuh bergerak 35% lebih cepat",
                blurb: "\"Angin membesarkan apimu. Angin juga mendorong mereka ke arahmu.\"");
            storm.Boon = Mods(M(StatKind.AreaPct, 0.45f));
            storm.EnemySpeedMul = 1.35f;
        }

        // =================================================================================
        //  sumber daya: mengubah DARI MANA mana dan nyawa datang
        // =================================================================================

        /// <summary>
        /// Kelompok paling ekstrem, dan sengaja.
        ///
        /// Yang lain menggeser angka; yang ini mencabut sumbernya dan menaruhnya di tempat lain.
        /// Pemain yang mengambil KELAPARAN tidak main lebih kuat atau lebih lemah — ia main game
        /// yang berbeda, karena mana tidak lagi datang dari menunggu melainkan dari membunuh, dan
        /// tiap keputusan papan sesudahnya diukur ulang terhadap itu.
        /// </summary>
        static void BuildResource(List<WorldModifierDefinition> into)
        {
            var hunger = P(into, "kelaparan", "KELAPARAN", new Color(0.85f, 0.6f, 0.25f),
                boon: "Tiap musuh mati mengembalikan 1,2 mana",
                bane: "Mana TIDAK PULIH SENDIRI sama sekali",
                blurb: "\"Berhenti menunggu. Mulai makan.\"");
            hunger.ManaRegenMul = 0f;
            hunger.ManaPerKill = 1.2f;

            var bloodpact = P(into, "perjanjiandarah", "PERJANJIAN DARAH",
                new Color(0.7f, 0.15f, 0.3f),
                boon: "Tiap musuh mati memulihkan 0,8 nyawa",
                bane: "Nyawa tidak pulih sendiri, dan musuh memukul 30% lebih keras",
                blurb: "\"Lukamu menutup dengan darah mereka. Hanya dengan darah mereka.\"");
            bloodpact.HpRegenMul = 0f;
            bloodpact.HpPerKill = 0.8f;
            bloodpact.EnemyDamageMul = 1.30f;

            var rapids = P(into, "arusderas", "ARUS DERAS", new Color(0.35f, 0.7f, 1f),
                boon: "Mana pulih 2,5 KALI lebih cepat",
                bane: "Nyawa maksimum −30",
                blurb: "\"Kubuka bendungannya. Jangan berdiri terlalu dekat.\"");
            rapids.ManaRegenMul = 2.5f;
            rapids.Bane = Mods(M(StatKind.MaxHp, -30f));

            var silence = P(into, "sumpahsunyi", "SUMPAH SUNYI", new Color(0.55f, 0.6f, 0.75f),
                boon: "Semua skill 45% lebih murah",
                bane: "Mana maksimum −40",
                blurb: "\"Wadah yang kecil boleh diisi sesering apa pun.\"");
            silence.Boon = Mods(M(StatKind.ManaCostPct, 0.45f));
            silence.Bane = Mods(M(StatKind.MaxMana, -40f));

            var flood = P(into, "banjir", "BANJIR", new Color(0.3f, 0.55f, 0.7f),
                boon: "Tiap kill mengembalikan 0,5 mana dan 0,35 nyawa",
                bane: "Musuh yang datang 60% LEBIH BANYAK",
                blurb: "\"Kubuka pintunya lebar-lebar. Semoga tanganmu cukup cepat.\"");
            flood.ManaPerKill = 0.5f;
            flood.HpPerKill = 0.35f;
            flood.EnemyCountMul = 1.60f;
        }

        // =================================================================================
        //  badan: apa yang kamu tukar dari tubuhmu sendiri
        // =================================================================================

        static void BuildBody(List<WorldModifierDefinition> into)
        {
            var fasting = P(into, "puasa", "PUASA", new Color(0.9f, 0.85f, 0.6f),
                boon: "Crit +25%, damage crit +50%",
                bane: "Nyawa maksimum −40",
                blurb: "\"Yang lapar memukul lebih tepat. Yang lapar juga lebih mudah patah.\"");
            fasting.Boon = Mods(M(StatKind.CritChance, 0.25f), M(StatKind.CritDamage, 0.5f));
            fasting.Bane = Mods(M(StatKind.MaxHp, -40f));

            var sacrifice = P(into, "tumbal", "TUMBAL", new Color(0.55f, 0.1f, 0.15f),
                boon: "Damage +80%",
                bane: "Nyawa maksimum −60, dan nyawa tidak pulih sendiri",
                blurb: "\"Sisakan cukup untuk berdiri. Sisanya milikku.\"");
            sacrifice.Boon = Mods(M(StatKind.DamagePct, 0.8f));
            sacrifice.Bane = Mods(M(StatKind.MaxHp, -60f));
            sacrifice.HpRegenMul = 0f;

            var stone = P(into, "kulitbatu", "KULIT BATU", new Color(0.5f, 0.5f, 0.45f),
                boon: "Pertahanan +40, nyawa maksimum +80",
                bane: "Kecepatan menghindar TURUN DRASTIS — lebih lambat dari musuh",
                blurb: "\"Kau tidak akan lari lagi. Kau tidak perlu.\"");
            stone.Boon = Mods(M(StatKind.Defense, 40f), M(StatKind.MaxHp, 80f));
            stone.Bane = Mods(M(StatKind.MoveSpeed, -1.2f));

            var flash = P(into, "kilat", "KILAT", new Color(0.95f, 0.95f, 0.5f),
                boon: "Lari hampir dua kali lipat, cooldown −20%",
                bane: "Nyawa maksimum −45",
                blurb: "\"Cepat sekali. Sekali saja tersentuh, habis.\"");
            flash.Boon = Mods(M(StatKind.MoveSpeed, 2.2f), M(StatKind.CooldownPct, 0.2f));
            flash.Bane = Mods(M(StatKind.MaxHp, -45f));

            var thirdeye = P(into, "mataketiga", "MATA KETIGA", new Color(0.7f, 0.5f, 0.9f),
                boon: "Jangkauan +50%, crit +15%",
                bane: "Area semua skill −30%",
                blurb: "\"Kau akan melihat jauh. Kau tidak akan melihat luas.\"");
            thirdeye.Boon = Mods(M(StatKind.RangePct, 0.5f), M(StatKind.CritChance, 0.15f));
            thirdeye.Bane = Mods(M(StatKind.AreaPct, -0.30f));

            // Pakta crit yang MENUNTUT dibangun ke arahnya, bukan sekadar menambah angka.
            //
            // Tiga pakta lama sudah menyentuh crit, tapi ketiganya memberi crit sebagai bonus di
            // atas build apa pun — diambil, damage naik, selesai. Yang ini menaruh taruhannya:
            // pukulan biasa dipotong hampir separuh, dan yang mengembalikannya cuma crit. Diambil
            // tanpa satu pun piece crit lain, ia MERUGIKAN. Diambil di atas Whetstone + Razor
            // Sigil, ia jadi build terkeras di buku.
            //
            // Hitungannya di angka bawaan: non-crit 0,55x; crit 0,55 x (1,5 + 1,5) = 1,65x. Pada
            // 40% peluang, nilai harapannya hampir persis 1,0 — jadi yang dibeli bukan damage,
            // melainkan VARIANSI. Naikkan peluangnya sedikit saja dan seluruhnya jadi keuntungan.
            var tremor = P(into, "tangangemetar", "TANGAN GEMETAR", new Color(0.85f, 0.25f, 0.25f),
                boon: "Crit +40%, damage crit +150%",
                bane: "Semua damage −45%",
                blurb: "\"Tanganmu tidak lagi bisa diam. Sesekali ia berhenti tepat di tempat yang benar.\"");
            tremor.Boon = Mods(M(StatKind.CritChance, 0.40f), M(StatKind.CritDamage, 1.5f));
            tremor.Bane = Mods(M(StatKind.DamagePct, -0.45f));

            var venom = P(into, "bisa", "BISA", new Color(0.45f, 0.8f, 0.35f),
                boon: "Tiap tempelan ailment membawa 2 POIN tambahan",
                bane: "Damage langsung −30%",
                blurb: "\"Racun tidak butuh pukulan keras. Racun butuh waktu.\"");
            venom.Boon = Mods(M(StatKind.AilmentPoints, 2f));
            venom.Bane = Mods(M(StatKind.DamagePct, -0.30f));
        }

        // =================================================================================
        //  dunia: yang mengubah bentuk gerombolan, bukan bentukmu
        // =================================================================================

        static void BuildWorld(List<WorldModifierDefinition> into)
        {
            var echo = P(into, "gema", "GEMA", new Color(0.8f, 0.75f, 1f),
                boon: "30% dari tiap tembakan berangkat DUA KALI, gratis",
                bane: "Musuh yang datang 30% lebih banyak",
                blurb: "\"Setiap kata yang kau ucapkan di sini terucap dua kali.\"");
            echo.EchoChance = 0.30f;
            echo.EnemyCountMul = 1.30f;

            var revival = P(into, "kebangkitan", "KEBANGKITAN", new Color(1f, 0.85f, 0.4f),
                boon: "SEKALI per run, kematian dibatalkan — bangkit di separuh nyawa",
                bane: "Musuh bernyawa +50% DAN memukul 40% lebih keras",
                blurb: "\"Satu nyawa lagi. Kubuat sisanya lebih pantas kau bayar.\"");
            revival.ReviveAt = 0.5f;
            revival.EnemyHpMul = 1.50f;
            revival.EnemyDamageMul = 1.40f;

            var drought = P(into, "musimkering", "MUSIM KERING", new Color(0.85f, 0.75f, 0.45f),
                boon: "Semua musuh bernyawa 35% LEBIH TIPIS",
                bane: "Damage kamu −25%, dan musuh yang datang 80% lebih banyak",
                blurb: "\"Kulemahkan mereka satu per satu. Lalu kukirim jauh lebih banyak.\"");
            drought.EnemyHpMul = 0.65f;
            drought.EnemyCountMul = 1.80f;
            drought.Bane = Mods(M(StatKind.DamagePct, -0.25f));

            var deep = P(into, "perutbumi", "PERUT BUMI", new Color(0.4f, 0.32f, 0.28f),
                boon: "Musuh bergerak 40% LEBIH LAMBAT",
                bane: "Musuh bernyawa +70%",
                blurb: "\"Kutarik kaki mereka ke dalam tanah. Tanah juga yang mengeraskan kulitnya.\"");
            deep.EnemySpeedMul = 0.60f;
            deep.EnemyHpMul = 1.70f;

            var swarm = P(into, "sarang", "SARANG", new Color(0.65f, 0.6f, 0.3f),
                boon: "Musuh bernyawa 45% lebih tipis dan bergerak 15% lebih lambat",
                bane: "Musuh yang datang DUA KALI LIPAT",
                blurb: "\"Bukan yang kuat. Yang banyak.\"");
            swarm.EnemyHpMul = 0.55f;
            swarm.EnemySpeedMul = 0.85f;
            swarm.EnemyCountMul = 2f;
        }

        // =================================================================================
        //  perkakas aset
        // =================================================================================

        static WorldModifierDefinition P(List<WorldModifierDefinition> into, string id, string name,
            Color color, string boon, string bane, string blurb)
        {
            string path = $"{Folder}/Pact_{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<WorldModifierDefinition>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<WorldModifierDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Id = id;
            asset.DisplayName = name;
            asset.Color = color;
            asset.BoonText = boon;
            asset.BaneText = bane;
            asset.Blurb = blurb;

            // Dinolkan eksplisit. Pass ini idempoten dan boleh dijalankan ulang setelah sebuah
            // bidang dicabut dari kode di atas — tanpa pembersihan ini, bidang yang sudah dihapus
            // tetap hidup di aset selamanya dan tidak ada yang bisa menebak dari mana asalnya.
            asset.Boon = new StatModifier[0];
            asset.Bane = new StatModifier[0];
            asset.EnemyHpMul = 1f;
            asset.EnemySpeedMul = 1f;
            asset.EnemyDamageMul = 1f;
            asset.EnemyCountMul = 1f;
            asset.ManaRegenMul = 1f;
            asset.HpRegenMul = 1f;
            asset.ManaPerKill = 0f;
            asset.HpPerKill = 0f;
            asset.EchoChance = 0f;
            asset.ReviveAt = 0f;

            EditorUtility.SetDirty(asset);
            into.Add(asset);
            return asset;
        }

        static StatModifier[] Mods(params StatModifier[] mods) => mods;

        static StatModifier M(StatKind kind, float value) =>
            new StatModifier { Type = kind, Value = value };
    }
}
