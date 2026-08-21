using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Mengubah skill KEMBAR jadi ITEM (segel), bukan menghapusnya.
    ///
    /// Audit 2026-08-11 menghitungnya: 74 dari 102 skill duduk di tujuh ember, dan di dalam satu
    /// ember bedanya cuma angka. Sembilan skill Nova adalah satu skill dikali sembilan tier.
    /// Permintaan pemilik project setelahnya jelas: lebih sedikit skill tapi tiap satunya unik,
    /// dan kekurangannya diganti item.
    ///
    /// <b>Diubah, tidak dihapus, dan itu keputusan teknis bukan kompromi.</b> Menghapus aset
    /// memutus tiga hal sekaligus yang tidak punya jalan pulih: resep yang MENGHASILKAN piece itu
    /// jadi resep tanpa hasil, resep yang MEMAKAINYA jadi resep tanpa bahan, dan codex pemain
    /// (`codex.json`, satu-satunya yang hidup antar run) menyimpan id yang tidak lagi ada. Id-nya
    /// tetap hidup di sini, jadi seluruh jaring itu utuh — yang berubah cuma apa yang piece itu
    /// LAKUKAN saat didudukkan.
    ///
    /// <b>Yang tidak boleh disentuh</b>, dan alasannya masing-masing:
    /// piece pembuka hero (fireball, frostshard) dan dua upgrade yang dijanjikannya
    /// (greaterfireball, steamburst); penanda dan peledak kombo Detonate (plaguebrand dan tiga
    /// peledaknya); pemakan charge (lepasamuk); penempel DRAG yang jadi pembuka reaksi massal
    /// (pusaran); dan seluruh puncak ★5. Mengubah salah satunya bukan mengurangi pilihan, itu
    /// memutus satu-satunya jalan yang dijanjikan game ke sesuatu.
    ///
    /// Item yang dihasilkan sengaja BUKAN tempelan stat. Audit yang sama menyebut 28 segel yang
    /// sudah ada sebagai penyumbang kebosanan terbesar justru karena mereka nol perilaku. Yang di
    /// bawah membeli KATA KERJA: peluru yang tadinya mati di sasaran pertama jadi memantul, rantai
    /// jadi bercabang, semburan jadi lebih banyak arah — dan sinar memantul menggambar coretan
    /// yang lebih lebar di layar.
    /// </summary>
    public static class ClonesToItemsPass
    {
        const string Root = "Assets/GameData";

        struct Conversion
        {
            public string Id;
            public string Name;
            public StatKind Kind;
            public float Value;

            /// <summary>Stat kedua, kalau segelnya membawa dua. None = cuma satu.</summary>
            public StatKind Second;

            public float SecondValue;
            public string Blurb;
        }

        /// <summary>
        /// Enam belas klon, dipilih dari ember yang paling penuh, dan tidak satu pun dari daftar
        /// tak-boleh-disentuh di atas.
        ///
        /// Nilainya kecil dengan sengaja. Satu segel +1 pantulan menggandakan sasaran sebuah
        /// Projectile, dan solver keseimbangan tidak tahu apa-apa soal segel — ia memecahkan damage
        /// dari `Bounces` yang tertulis DI ASET. Jadi tiap angka di sini adalah kekuatan yang lolos
        /// dari seluruh sistem penyeimbang, dan satu-satunya rem yang tersisa adalah petak papan
        /// yang dimakannya.
        /// </summary>
        static readonly Conversion[] Table =
        {
            // ---- dari ember Nova (9 anggota, disisakan 4) ----
            new Conversion { Id = "staticfield", Name = "Segel Percikan",
                Kind = StatKind.BonusBounces, Value = 1f,
                Blurb = "Setiap peluru memantul sekali lagi. Sinar memantul menggambar satu ruas " +
                        "lebih panjang di layar." },

            // thunderclap & rimenova, BUKAN frostnova & blizzard.
            //
            // Keduanya yang terakhir punya blurb tertulis tangan di FootprintPass.Table, dan pass
            // itu menimpa Blurb tiap kali dijalankan. Mengubahnya jadi segel di sini akan bertahan
            // tepat sampai seseorang menjalankan Footprint by Rarity — sesudahnya segelnya
            // menyandang keterangan skill Nova yang sudah tidak ia lakukan, tanpa satu pun error.
            new Conversion { Id = "thunderclap", Name = "Segel Guruh",
                Kind = StatKind.BonusHits, Value = 1f,
                Second = StatKind.LightningDamagePct, SecondValue = 0.15f,
                Blurb = "Satu arah semburan lagi, satu bilah lagi, satu rudal lagi." },

            new Conversion { Id = "rimenova", Name = "Segel Embun Beku",
                Kind = StatKind.BonusHits, Value = 1f,
                Second = StatKind.AreaPct, SecondValue = 0.12f,
                Blurb = "Menambah satu ke apa pun yang dihitung banyaknya, dan melebarkan semuanya." },

            new Conversion { Id = "nullsphere", Name = "Segel Hampa",
                Kind = StatKind.BonusBounces, Value = 2f,
                Second = StatKind.ManaCostPct, SecondValue = -0.12f,
                Blurb = "Dua pantulan lagi. Bukan gratis: semua skill jadi sedikit lebih mahal." },

            // ---- dari ember AreaAtTarget (11 anggota) ----
            new Conversion { Id = "hailstorm", Name = "Segel Hujan Es",
                Kind = StatKind.BonusHits, Value = 1f,
                Blurb = "Satu hantaman lagi di tiap hujan, satu pecahan lagi mengambang." },

            new Conversion { Id = "gravitywell", Name = "Segel Gravitasi",
                Kind = StatKind.BonusBounces, Value = 1f,
                Second = StatKind.RangePct, SecondValue = 0.2f,
                Blurb = "Pantulan mencari sasaran dari lebih jauh." },

            new Conversion { Id = "firestormcore", Name = "Segel Inti Badai",
                Kind = StatKind.BonusHits, Value = 2f,
                Second = StatKind.CooldownPct, SecondValue = -0.1f,
                Blurb = "Dua lagi, di semua yang dihitung banyaknya. Menahannya membuat semua " +
                        "cooldown sedikit lebih panjang." },

            new Conversion { Id = "tempest", Name = "Segel Amuk Angin",
                Kind = StatKind.BonusForks, Value = 1f,
                Second = StatKind.LightningDamagePct, SecondValue = 0.18f,
                Blurb = "Satu cabang petir lagi berangkat, dan cabang tidak pernah menyambar " +
                        "musuh yang sama." },

            // ---- dari ember Zone (9 anggota) ----
            new Conversion { Id = "cinderpatch", Name = "Segel Bara",
                Kind = StatKind.AilmentPoints, Value = 1f,
                Second = StatKind.FireDamagePct, SecondValue = 0.15f,
                Blurb = "Tiap tempelan ailment membawa satu poin lagi — pemicu jadi lebih cepat penuh." },

            new Conversion { Id = "stormcell", Name = "Segel Sel Badai",
                Kind = StatKind.BonusForks, Value = 1f,
                Blurb = "Satu cabang petir lagi." },

            new Conversion { Id = "frostbitefield", Name = "Segel Gigit Beku",
                Kind = StatKind.BonusHits, Value = 1f,
                Second = StatKind.CritChance, SecondValue = 0.08f,
                Blurb = "Satu lagi di semua yang dihitung banyaknya." },

            new Conversion { Id = "plaguebloom", Name = "Segel Wabah",
                Kind = StatKind.AilmentPoints, Value = 2f,
                Second = StatKind.DamagePct, SecondValue = -0.08f,
                Blurb = "Dua poin ailment lagi per tempel. Dibayar dengan damage langsung." },

            // ---- dari ember Line (6 anggota) ----
            new Conversion { Id = "icelance", Name = "Segel Tombak Es",
                Kind = StatKind.BonusBounces, Value = 1f,
                Second = StatKind.IceDamagePct, SecondValue = 0.18f,
                Blurb = "Satu pantulan lagi." },

            new Conversion { Id = "infernowave", Name = "Segel Gelombang Api",
                Kind = StatKind.BonusHits, Value = 1f,
                Second = StatKind.FireDamagePct, SecondValue = 0.2f,
                Blurb = "Satu lagi di semua yang dihitung banyaknya." },

            new Conversion { Id = "voidlance", Name = "Segel Tombak Hampa",
                Kind = StatKind.BonusBounces, Value = 2f,
                Second = StatKind.MaxMana, SecondValue = -10f,
                Blurb = "Dua pantulan lagi, ditukar dengan wadah mana yang lebih kecil." },

            // ---- dari ember Chain (6 anggota) ----
            new Conversion { Id = "riftchain", Name = "Segel Rantai Retak",
                Kind = StatKind.BonusForks, Value = 1f,
                Second = StatKind.RangePct, SecondValue = 0.15f,
                Blurb = "Satu cabang lagi, dan lompatan pertamanya menjangkau lebih jauh." }
        };

        [MenuItem("Tools/Grimoire/Convert Clone Skills To Items")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[ClonesToItems] ContentDatabase.asset tidak ketemu.");
                return;
            }

            int converted = 0, missing = 0;

            for (int i = 0; i < Table.Length; i++)
            {
                var row = Table[i];
                var piece = db.ById(row.Id);

                if (piece == null)
                {
                    Debug.LogWarning($"[ClonesToItems] '{row.Id}' tidak ada, dilewati.");
                    missing++;
                    continue;
                }

                Convert(piece, row);
                converted++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ClonesToItems] {converted} skill kembar jadi segel berperilaku" +
                      (missing > 0 ? $", {missing} tidak ketemu" : "") + ".\n" +
                      "LANJUTKAN: Footprint by Rarity (segel ★1 wajib 2 petak), lalu Regenerate " +
                      "Placeholder Icons (bentuknya berubah, jadi ikonnya sekarang salah gambar), " +
                      "lalu Rebalance by Throughput.");

            Selection.activeObject = db;
        }

        static void Convert(PieceDefinition piece, Conversion row)
        {
            piece.DisplayName = row.Name;
            piece.Kind = CastKind.Passive;

            // Seluruh bidang cast dinolkan. Sebuah piece pasif yang masih menyimpan radius, durasi
            // kubangan, dan status tempelan akan terus MENAMPILKAN semua itu di kartunya — pemain
            // membaca janji yang tidak akan pernah ditepati kodenya, dan tidak ada yang error.
            piece.BaseDamage = 0f;
            piece.ManaCost = 0f;
            piece.Radius = 0f;
            piece.Range = 0f;
            piece.Hits = 1;
            piece.Forks = 1;
            piece.Bounces = 0;
            piece.ZoneDuration = 0f;
            piece.ZoneDrift = 0f;
            piece.AppliedStatus = null;
            piece.StatusDuration = 0f;
            piece.TriggerStatus = null;
            piece.Trigger = CastTrigger.Cooldown;
            piece.GrantOnCast = null;
            piece.PushForce = 0f;
            piece.LiftDuration = 0f;
            piece.TravelSpeed = 0f;

            // Cooldown TIDAK boleh nol — OnValidate menjepitnya ke 0,05 dan panel spell membagi
            // dengannya untuk menghitung dps. Pasif tidak pernah masuk daftar spell, tapi angka
            // satu jauh lebih murah daripada mengandalkan itu tetap benar selamanya.
            piece.BaseCooldown = 1f;

            piece.Stats = row.Second == StatKind.None
                ? new[] { Mod(row.Kind, row.Value) }
                : new[] { Mod(row.Kind, row.Value), Mod(row.Second, row.SecondValue) };

            piece.Blurb = row.Blurb;

            // Ikon lama menggambar BENTUK piece-nya, dan bentuknya berubah saat ia jadi segel ★1
            // dua petak. Dikosongkan supaya generator menggambarnya ulang; dibiarkan terisi, ia
            // akan dilewati (generator tidak pernah menimpa yang sudah ada) dan segelnya memakai
            // gambar skill yang sudah tidak ada.
            piece.Icon = null;

            EditorUtility.SetDirty(piece);
        }

        static StatModifier Mod(StatKind kind, float value) =>
            new StatModifier { Type = kind, Value = value };
    }
}
