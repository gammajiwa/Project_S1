using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Skill yang bukan "damage dalam suatu bentuk": kabur, bertahan, mengendalikan, dan beberapa
    /// serangan dengan ritme khas sendiri.
    ///
    /// Sebelum ini seluruh buku isinya menyerang. Kedengarannya sesuai untuk game yang seluruh
    /// isinya ledakan, tapi akibatnya cuma satu: tiap petak papan menjawab pertanyaan yang sama,
    /// jadi seluruh keputusan build mengerucut jadi "mana yang dps-nya paling besar". Tidak ada
    /// yang perlu dipikirkan, cuma perlu diurutkan.
    ///
    /// Yang ditambahkan di sini menjawab pertanyaan LAIN — apa yang pemain lakukan setelah
    /// rencananya gagal — dan itulah yang membuat papan punya lebih dari satu jawaban benar.
    ///
    /// Damage skill penyerang di sini sengaja dibiarkan kasar: <see cref="BalanceTunePass"/> yang
    /// menghitungnya dari throughput per bintang. Yang BUKAN penyerang diatur tangan di sini,
    /// karena angkanya bukan damage dan tuner memang tidak menyentuhnya.
    /// </summary>
    public static class SignaturePass
    {
        const string Root = "Assets/GameData";
        const string PieceFolder = Root + "/Pieces";
        const string RecipeFolder = Root + "/Recipes";

        [MenuItem("Tools/Grimoire/Generate Signature Skills")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[SignaturePass] ContentDatabase.asset tidak ketemu.");
                return;
            }

            var pieces = new List<PieceDefinition>(db.Pieces);
            var recipes = new List<RecipeDefinition>(db.Recipes);

            int before = pieces.Count;

            BuildDefensive(db, pieces);
            BuildMobility(db, pieces);
            BuildControl(db, pieces);
            BuildSignatureAttacks(db, pieces);

            db.EditorSet(pieces, recipes);

            int linked = Link(db, recipes);
            db.EditorSet(pieces, recipes);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SignaturePass] {pieces.Count - before} piece baru, {linked} resep. " +
                      $"Total piece: {pieces.Count}. Jalankan Rebalance by Throughput setelah ini.");
            Selection.activeObject = db;
        }

        // =================================================================================
        //  bertahan
        // =================================================================================

        static void BuildDefensive(ContentDatabase db, List<PieceDefinition> pieces)
        {
            var steel = Buff("fortify", "Fortify", 8f, new Color(0.62f, 0.72f, 0.85f),
                "Kulit mengeras. Tiap pukulan yang masuk dipangkas.",
                Mod(StatKind.Defense, 14f));

            // Serapan, bukan penyembuhan. Bedanya penting: heal membalas kerusakan setelah terjadi,
            // perisai menolaknya sebelum terjadi — dan cuma yang kedua yang menyelamatkan pemain
            // dari satu tembakan yang seharusnya mematikan.
            var petty = Utility(db, "wardpetty", "Lesser Ward", 1, ShapeKind.Line2, CastKind.Ward,
                Element.Arcane, new Color(0.55f, 0.78f, 0.95f),
                amount: 38f, cooldown: 11f, mana: 16f,
                "Perisai menyerap 38 damage selama 8 detik. Menolak dipasang ulang selama " +
                "perisai lama masih tebal.");
            petty.ZoneDuration = 8f;
            Save(petty, pieces);

            var aegis = Utility(db, "wardaegis", "Aegis", 2, ShapeKind.Square, CastKind.Ward,
                Element.Arcane, new Color(0.5f, 0.82f, 0.98f),
                amount: 95f, cooldown: 13f, mana: 26f,
                "Perisai menyerap 95 damage selama 9 detik.");
            aegis.ZoneDuration = 9f;
            Save(aegis, pieces);

            var bulwark = Utility(db, "bulwark", "Bulwark", 4, ShapeKind.Slab, CastKind.Ward,
                Element.Arcane, new Color(0.72f, 0.85f, 1f),
                amount: 320f, cooldown: 15f, mana: 52f,
                "Perisai menyerap 320 damage selama 10 detik, DAN mengeraskan kulit selama " +
                "perisainya berdiri.");
            bulwark.ZoneDuration = 10f;
            bulwark.GrantOnCast = steel;
            Save(bulwark, pieces);

            // Mengisi mana, dan biayanya nol. Skill pemulih sumber daya yang menagih sumber daya
            // itu sendiri akan mengunci diri persis pada satu-satunya saat ia dibutuhkan.
            var well = Utility(db, "manawell", "Mana Well", 2, ShapeKind.Ell, CastKind.Restore,
                Element.Arcane, new Color(0.45f, 0.6f, 1f),
                amount: 0f, cooldown: 20f, mana: 0f,
                "Mengisi mana sampai PENUH. Gratis, tapi menunggu sampai manamu tinggal di bawah " +
                "60% sebelum mau menyala.");
            Save(well, pieces);
        }

        // =================================================================================
        //  bergerak
        // =================================================================================

        static void BuildMobility(ContentDatabase db, List<PieceDefinition> pieces)
        {
            var swift = Buff("swift", "Quickfoot", 5f, new Color(0.65f, 0.95f, 0.7f),
                "Kaki ringan. Menghindar jadi jauh lebih cepat.",
                Mod(StatKind.MoveSpeed, 1.5f));

            var quick = Buff("quickcast", "Quickcast", 6f, new Color(0.95f, 0.85f, 0.5f),
                "Semua cooldown di buku memendek.",
                Mod(StatKind.CooldownPct, 0.3f));

            var afterimage = Buff("afterimage", "Afterimage", 4f, new Color(0.75f, 0.5f, 1f),
                "Bayangan yang tertinggal ikut memukul. Damage naik sesaat setelah berkedip.",
                Mod(StatKind.DamagePct, 0.4f), Mod(StatKind.MoveSpeed, 0.8f));

            // Berkedip ke arah yang paling lengang. Sengaja MENOLAK menyala saat lapangan kosong,
            // jadi cooldown-nya masih utuh justru pada detik pemain benar-benar terkepung — dan
            // itu satu-satunya detik skill ini ada gunanya.
            var step = Utility(db, "blinkstep", "Blink Step", 1, ShapeKind.Line2, CastKind.Blink,
                Element.Arcane, new Color(0.7f, 0.55f, 0.95f),
                amount: 0f, cooldown: 7f, mana: 14f,
                "Melompat 7 unit menjauh dari kerumunan terpadat. Diam saja kalau tidak ada yang " +
                "mengepung.");
            step.Range = 7f;
            Save(step, pieces);

            var voidstep = Utility(db, "voidstep", "Void Step", 3, ShapeKind.Cup, CastKind.Blink,
                Element.Arcane, new Color(0.62f, 0.35f, 0.95f),
                amount: 0f, cooldown: 5.5f, mana: 24f,
                "Melompat 11 unit menjauh dan meninggalkan bayangan: +40% damage dan lari lebih " +
                "cepat selama 4 detik.");
            voidstep.Range = 11f;
            voidstep.GrantOnCast = afterimage;
            Save(voidstep, pieces);

            var quickfoot = Utility(db, "quickfoot", "Fleetfoot", 1, ShapeKind.Line2, CastKind.Surge,
                Element.Arcane, new Color(0.6f, 0.95f, 0.68f),
                amount: 0f, cooldown: 9f, mana: 12f,
                "Kecepatan menghindar naik drastis selama 5 detik.");
            quickfoot.GrantOnCast = swift;
            Save(quickfoot, pieces);

            var quickcast = Utility(db, "quickcastsigil", "Quickcast", 2, ShapeKind.Tee, CastKind.Surge,
                Element.Arcane, new Color(0.98f, 0.86f, 0.45f),
                amount: 0f, cooldown: 13f, mana: 24f,
                "SEMUA skill di buku menembak 30% lebih sering selama 6 detik. Nilainya ikut " +
                "sebesar apa pun isi papanmu.");
            quickcast.GrantOnCast = quick;
            Save(quickcast, pieces);
        }

        // =================================================================================
        //  mengendalikan
        // =================================================================================

        static void BuildControl(ContentDatabase db, List<PieceDefinition> pieces)
        {
            // Melontar, bukan membunuh. Yang dibeli pemain adalah RUANG, dan itu satu-satunya
            // barang yang tidak bisa dibeli skill damage berapa pun besarnya.
            var shove = Weapon(db, "shove", "Shove", 1, ShapeKind.Line3, CastKind.ForcePush,
                Element.Arcane, new Color(0.8f, 0.85f, 0.9f),
                cooldown: 6f, radius: 4.2f, range: 4.2f,
                "Melontarkan semua yang menempel ke luar. Damage-nya kecil — yang kamu beli " +
                "adalah jarak.");
            shove.PushForce = 15f;
            Save(shove, pieces);

            var whirl = Weapon(db, "whirlwind", "Whirlwind", 3, ShapeKind.Cross, CastKind.Vortex,
                Element.Lightning, new Color(0.7f, 0.9f, 0.95f),
                cooldown: 6.5f, radius: 3.2f, range: 12f,
                "Pusaran berjalan yang menyeret musuh ke tengahnya dan MENGANGKAT yang sampai ke " +
                "mata. Yang terangkat tidak berjalan dan tidak menembak.");
            Twister(whirl, duration: 4f, lift: 1.2f, pull: 9f, drift: 1.6f);
            Save(whirl, pieces);

            var tornado = Weapon(db, "tornado", "Tornado", 4, ShapeKind.Ring, CastKind.Vortex,
                Element.Lightning, new Color(0.62f, 0.88f, 1f),
                cooldown: 8f, radius: 4.2f, range: 14f,
                "Puting beliung. Menyeret lebih kuat, mengangkat lebih lama, dan mengembara " +
                "sendiri melintasi lapangan.");
            Twister(tornado, duration: 5f, lift: 1.8f, pull: 13f, drift: 2.3f);
            Save(tornado, pieces);

            var maelstrom = Weapon(db, "maelstrom", "Maelstrom", 5, ShapeKind.Chunk, CastKind.Vortex,
                Element.Lightning, new Color(0.5f, 0.8f, 1f),
                cooldown: 10f, radius: 5.6f, range: 16f,
                "Badai yang menelan setengah lapangan. Apa pun yang sampai ke matanya menggantung " +
                "tidak berdaya sampai badainya lewat.");
            Twister(maelstrom, duration: 7f, lift: 2.4f, pull: 17f, drift: 2.6f);
            Save(maelstrom, pieces);
        }

        // =================================================================================
        //  serangan berciri khas
        // =================================================================================

        static void BuildSignatureAttacks(ContentDatabase db, List<PieceDefinition> pieces)
        {
            var burn = db.StatusById("burn");
            var shock = db.StatusById("shock");

            // Muatan yang menunggu di atas kepala. Dayanya dibayar SEBELUM gerombolan tiba, bukan
            // saat pemain sudah kewalahan — itu satu-satunya skill di buku yang waktunya bisa
            // diatur pemain, bukan oleh cooldown.
            var shards = Weapon(db, "sparkshards", "Spark Shards", 1, ShapeKind.Line3, CastKind.Orbit,
                Element.Lightning, new Color(0.85f, 0.9f, 0.45f),
                cooldown: 2.6f, radius: 0f, range: 7f,
                "Tiga pecahan mengambang di atas kepalamu dan MENUNGGU. Begitu ada yang mendekat, " +
                "satu meluncur sendiri.");
            shards.Hits = 3;
            shards.TravelSpeed = 13f;
            shards.AppliedStatus = shock;
            shards.StatusDuration = 3f;
            Save(shards, pieces);

            var storm = Weapon(db, "stormshards", "Storm Shards", 3, ShapeKind.Zed, CastKind.Orbit,
                Element.Lightning, new Color(0.75f, 0.85f, 1f),
                cooldown: 3.4f, radius: 0f, range: 9.5f,
                "Lima pecahan mengambang dan menunggu, dan menjemput dari lebih jauh.");
            storm.Hits = 5;
            storm.TravelSpeed = 16f;
            storm.AppliedStatus = shock;
            storm.StatusDuration = 4f;
            Save(storm, pieces);

            // Satu-satunya cast yang bisa MELESET setelah dibayar. Aba-abanya bukan hiasan: itu
            // harga yang dibayar untuk pukulan sebesar ini, dan satu-satunya ritme di layar yang
            // tidak instan.
            var lance = Weapon(db, "sunlance", "Sun Lance", 2, ShapeKind.SBend, CastKind.SunStrike,
                Element.Fire, new Color(1f, 0.8f, 0.3f),
                cooldown: 3.2f, radius: 3.4f, range: 12f,
                "Menandai tanah, lalu menghantam 0.9 detik kemudian. Yang sempat menyingkir, " +
                "selamat.");
            lance.TelegraphDelay = 0.9f;
            lance.AppliedStatus = burn;
            lance.StatusDuration = 4f;
            Save(lance, pieces);

            var strike = Weapon(db, "sunstrike", "Sun Strike", 3, ShapeKind.Wedge, CastKind.SunStrike,
                Element.Fire, new Color(1f, 0.72f, 0.22f),
                cooldown: 4.2f, radius: 4.4f, range: 14f,
                "Pilar matahari. Lingkarannya lebih lebar dan aba-abanya lebih pendek.");
            strike.TelegraphDelay = 0.75f;
            strike.AppliedStatus = burn;
            strike.StatusDuration = 5f;
            Save(strike, pieces);

            var flare = Weapon(db, "solarflare", "Solar Flare", 5, ShapeKind.Big3, CastKind.SunStrike,
                Element.Fire, new Color(1f, 0.62f, 0.15f),
                cooldown: 7f, radius: 6.8f, range: 17f,
                "Langit terbuka. Satu detik penuh aba-aba, lalu seperempat lapangan hangus.");
            flare.TelegraphDelay = 1.1f;
            flare.AppliedStatus = burn;
            flare.StatusDuration = 7f;
            Save(flare, pieces);

            // Menggelinding, bukan menyambar. Bedanya dari Line bukan bentuk tapi WAKTU: bolanya
            // butuh sedetik untuk sampai, dan gerombolan sempat bergerak selama itu.
            var ember = Weapon(db, "emberroll", "Rolling Ember", 2, ShapeKind.Ell, CastKind.RollingBall,
                Element.Fire, new Color(1f, 0.55f, 0.25f),
                cooldown: 3.2f, radius: 1.3f, range: 12f,
                "Bola bara yang MENGGELINDING menembus gerombolan dan melindas semua yang " +
                "dilewatinya. Satu musuh cuma bisa dilindas sekali per bola.");
            ember.TravelSpeed = 8f;
            ember.AppliedStatus = burn;
            ember.StatusDuration = 4f;
            Save(ember, pieces);

            var chaos = Weapon(db, "chaosmeteor", "Chaos Meteor", 4, ShapeKind.Aitch, CastKind.RollingBall,
                Element.Fire, new Color(1f, 0.4f, 0.2f),
                cooldown: 5.5f, radius: 2.3f, range: 18f,
                "Meteor yang jatuh lalu terus menggelinding melintasi lapangan, membakar seluruh " +
                "jalurnya.");
            chaos.TravelSpeed = 7.5f;
            chaos.AppliedStatus = burn;
            chaos.StatusDuration = 6f;
            Save(chaos, pieces);
        }

        // =================================================================================
        //  resep
        // =================================================================================

        /// <summary>
        /// Tanpa ini seluruh tier di atas satu bintang tidak bisa didapat sama sekali: drop cuma
        /// mengeluarkan bintang satu, dan toko tidak menyimpan semuanya.
        /// </summary>
        static int Link(ContentDatabase db, List<RecipeDefinition> recipes)
        {
            int before = recipes.Count;

            R(recipes, db, "wardaegis_a", "wardaegis", "wardpetty", "wardpetty");
            R(recipes, db, "manawell_a", "manawell", "wardpetty", "quickfoot");
            R(recipes, db, "quickcast_a", "quickcastsigil", "quickfoot", "quickfoot");
            R(recipes, db, "bulwark_a", "bulwark", "wardaegis", "wardaegis");

            R(recipes, db, "voidstep_a", "voidstep", "blinkstep", "quickcastsigil");
            R(recipes, db, "voidstep_b", "voidstep", "blinkstep", "blinkstep", "quickfoot");

            R(recipes, db, "sunlance_a", "sunlance", "sparkshards", "fireball");
            R(recipes, db, "emberroll_a", "emberroll", "shove", "fireball");
            R(recipes, db, "stormshards_a", "stormshards", "sparkshards", "sunlance");
            R(recipes, db, "sunstrike_a", "sunstrike", "sunlance", "sunlance");

            R(recipes, db, "whirlwind_a", "whirlwind", "shove", "emberroll");
            R(recipes, db, "whirlwind_b", "whirlwind", "shove", "shove", "sparkshards");
            R(recipes, db, "tornado_a", "tornado", "whirlwind", "whirlwind");

            R(recipes, db, "chaosmeteor_a", "chaosmeteor", "emberroll", "sunstrike");
            R(recipes, db, "solarflare_a", "solarflare", "sunstrike", "chaosmeteor");
            R(recipes, db, "maelstrom_a", "maelstrom", "tornado", "whirlwind");

            return recipes.Count - before;
        }

        // =================================================================================
        //  perkakas aset
        // =================================================================================

        /// <summary>
        /// Skill yang angkanya BUKAN damage — serapan, mana, atau tidak ada sama sekali. Diatur
        /// tangan karena <see cref="BalanceTunePass"/> memang melewatinya.
        /// </summary>
        static PieceDefinition Utility(ContentDatabase db, string id, string name, int stars,
            ShapeKind shape, CastKind kind, Element element, Color color,
            float amount, float cooldown, float mana, string blurb)
        {
            var a = Load(id);

            Common(a, id, name, stars, shape, kind, element, color, blurb);
            a.BaseDamage = amount;
            a.BaseCooldown = cooldown;
            a.ManaCost = mana;
            a.Radius = 0f;
            a.Range = 0f;

            return a;
        }

        /// <summary>
        /// Skill penyerang. Damage dan mana sengaja dibiarkan nol — angka aslinya dipecahkan
        /// <see cref="BalanceTunePass"/> dari cooldown dan jangkauan yang diisi di sini.
        /// </summary>
        static PieceDefinition Weapon(ContentDatabase db, string id, string name, int stars,
            ShapeKind shape, CastKind kind, Element element, Color color,
            float cooldown, float radius, float range, string blurb)
        {
            var a = Load(id);

            Common(a, id, name, stars, shape, kind, element, color, blurb);
            a.BaseCooldown = cooldown;
            a.Radius = radius;
            a.Range = range;
            a.BaseDamage = 1f;
            a.ManaCost = 1f;

            return a;
        }

        static void Common(PieceDefinition a, string id, string name, int stars, ShapeKind shape,
            CastKind kind, Element element, Color color, string blurb)
        {
            a.Id = id;
            a.DisplayName = name;
            a.Stars = stars;
            a.Layer = Layer.Skill;
            a.Kind = kind;
            a.Element = element;
            a.Shape = shape;
            a.Color = color;
            a.Trigger = CastTrigger.Cooldown;
            a.Hits = 1;
            a.Forks = 1;
            a.Bounces = 0;
            a.Blurb = blurb;
        }

        static void Twister(PieceDefinition a, float duration, float lift, float pull, float drift)
        {
            a.ZoneDuration = duration;
            a.LiftDuration = lift;
            a.PushForce = pull;
            a.ZoneDrift = drift;
        }

        static void Save(PieceDefinition a, List<PieceDefinition> pieces)
        {
            EditorUtility.SetDirty(a);
            if (!pieces.Contains(a)) pieces.Add(a);
        }

        static BuffDefinition Buff(string id, string name, float duration, Color color,
            string blurb, params StatModifier[] mods)
        {
            string path = $"{Root}/Buffs/Buff_{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<BuffDefinition>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BuffDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Id = id;
            asset.DisplayName = name;
            asset.Color = color;
            asset.Duration = duration;
            asset.MaxStacks = 1;
            asset.Mods = mods;
            asset.IsDebuff = false;
            asset.Blurb = blurb;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        static StatModifier Mod(StatKind kind, float value) =>
            new StatModifier { Type = kind, Value = value };

        static PieceDefinition Load(string id)
        {
            string path = $"{PieceFolder}/Piece_{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<PieceDefinition>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PieceDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        static void R(List<RecipeDefinition> recipes, ContentDatabase db, string fileId,
            string resultId, params string[] ingredientIds)
        {
            var result = db.ById(resultId);
            if (result == null)
            {
                Debug.LogWarning($"[SignaturePass] hasil '{resultId}' tidak ada, resep dilewati.");
                return;
            }

            var ingredients = new PieceDefinition[ingredientIds.Length];

            for (int i = 0; i < ingredientIds.Length; i++)
            {
                ingredients[i] = db.ById(ingredientIds[i]);

                // Resep dengan bahan kosong akan diam-diam cocok dengan apa pun. Lebih baik tidak
                // ada resepnya sama sekali daripada ada resep yang berperilaku acak.
                if (ingredients[i] != null) continue;

                Debug.LogWarning($"[SignaturePass] bahan '{ingredientIds[i]}' tidak ada, " +
                                 $"resep '{fileId}' dilewati.");
                return;
            }

            string path = $"{RecipeFolder}/Recipe_{fileId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RecipeDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Ingredients = ingredients;
            asset.Result = result;
            EditorUtility.SetDirty(asset);

            if (!recipes.Contains(asset)) recipes.Add(asset);
        }
    }
}
