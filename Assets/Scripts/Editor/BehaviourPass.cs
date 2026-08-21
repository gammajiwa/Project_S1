using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Delapan belas skill untuk delapan perilaku baru — bilah yang mengitari badan, bumerang,
    /// sinar memantul, menara, gelombang melebar, rudal pengejar, sinar pengunci, hujan hantaman.
    ///
    /// Ini jawaban langsung atas keluhan pemilik project: "banyak banget skill tapi sebenarnya
    /// itu-itu aja". Audit membuktikannya — 74 dari 102 skill duduk di 7 ember, dan di dalam satu
    /// ember bedanya cuma ANGKA. Sembilan skill Nova adalah satu skill dikali sembilan tier.
    ///
    /// Yang ditambahkan di sini sengaja SEDIKIT per perilaku — dua atau tiga, satu per band rarity —
    /// karena menambah sepuluh Orbital hanya akan mengulangi kesalahan yang sama dengan bentuk yang
    /// lebih baru. Yang ditambah keragaman KATA KERJA-nya, bukan jumlah barisnya.
    ///
    /// Damage dan mana sengaja ditinggal kasar: <see cref="BalanceTunePass"/> yang memecahkannya
    /// dari cooldown, radius, dan durasi yang diisi di sini. JALANKAN SOLVER SETELAH PASS INI —
    /// tanpa itu semua skill di bawah menembak di damage 1.
    /// </summary>
    public static class BehaviourPass
    {
        const string Root = "Assets/GameData";
        const string PieceFolder = Root + "/Pieces";
        const string RecipeFolder = Root + "/Recipes";

        [MenuItem("Tools/Grimoire/Generate Behaviour Skills")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[BehaviourPass] ContentDatabase.asset tidak ketemu.");
                return;
            }

            var pieces = new List<PieceDefinition>(db.Pieces);
            var recipes = new List<RecipeDefinition>(db.Recipes);

            int before = pieces.Count;

            BuildOrbital(db, pieces);
            BuildBoomerang(db, pieces);
            BuildRicochet(db, pieces);
            BuildTurret(db, pieces);
            BuildShockwave(db, pieces);
            BuildSeeker(db, pieces);
            BuildTether(db, pieces);
            BuildBarrage(db, pieces);

            db.EditorSet(pieces, recipes);

            int linked = Link(db, recipes);
            db.EditorSet(pieces, recipes);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BehaviourPass] {pieces.Count - before} piece baru, {linked} resep. " +
                      $"Total piece: {pieces.Count}.\n" +
                      "LANJUTKAN: Tools/Grimoire/Footprint by Rarity, lalu Generate Placeholder " +
                      "Icons, lalu Rebalance by Throughput. Solver TERAKHIR.");

            Selection.activeObject = db;
        }

        // =================================================================================
        //  Orbital — bilah yang mengitari badan pemain
        // =================================================================================

        /// <summary>
        /// Satu-satunya keluarga skill yang membayar pemain karena DIDEKATI. Sisanya menghukum.
        ///
        /// Karena itu radius dan durasinya yang naik per tier, bukan cuma damage-nya: yang dibeli
        /// pemain di tier atas adalah ruang aman yang lebih lebar dan bertahan lebih lama, dan itu
        /// mengubah tempat ia berani berdiri — bukan cuma angka di kartunya.
        /// </summary>
        static void BuildOrbital(ContentDatabase db, List<PieceDefinition> pieces)
        {
            var bleed = db.StatusById("bleed");
            var shock = db.StatusById("shock");
            var burn = db.StatusById("burn");

            // Radius diukur terhadap GELEMBUNG MENGHINDAR pemain, bukan terhadap ukuran badannya.
            //
            // Percobaan pertama memberi 2,4 / 3,4 / 4,6 — angka yang terlihat masuk akal untuk
            // "bilah yang mengitari badan". Terukur di wave 8: Blade Dance keluar di 8 dps melawan
            // targetnya 17,6, dan sebabnya bukan damage-nya. PlayerMotor menjauhkan pemain begitu
            // musuh masuk `DangerRadius` (6 unit), jadi bilah berjari-jari 2,4 hidup sepenuhnya di
            // dalam ruang yang MEMANG SUDAH dikosongkan sebelum ada yang sampai. Skill yang
            // seharusnya membayar pemain karena didekati justru dibatalkan oleh mesin yang
            // mencegahnya didekati.
            //
            // Damage per denyut turun sendiri saat radiusnya naik — solver menghitungnya dari
            // luasan — jadi yang berubah bukan seberapa kuat, melainkan apakah ia MENGENAI.
            var dance = Weapon(db, "bladedance", "Blade Dance", 1, ShapeKind.Line2, CastKind.Orbital,
                Element.Arcane, new Color(0.85f, 0.88f, 0.95f),
                cooldown: 5f, radius: 3.6f, range: 0f,
                "Tiga bilah mengitari badanmu selama 4 detik dan melukai apa pun yang tersapu. " +
                "Berdiri di keramaian jadi menguntungkan, bukan bunuh diri.");
            Spin(dance, blades: 3, duration: 4f, tick: 0.35f, edgeSpeed: 7f, push: 0f);
            Ailment(dance, bleed, 3f, 1);
            Save(dance, pieces);

            var circle = Weapon(db, "stormcircle", "Storm Circle", 3, ShapeKind.Cross, CastKind.Orbital,
                Element.Lightning, new Color(0.68f, 0.85f, 1f),
                cooldown: 7f, radius: 5f, range: 0f,
                "Lima bilah petir mengitarimu selama 6 detik. Lingkarannya cukup lebar untuk " +
                "menutupi seluruh jarak yang bisa ditempuh musuh sebelum menyentuhmu.");
            Spin(circle, blades: 5, duration: 6f, tick: 0.3f, edgeSpeed: 9f, push: 0f);
            Ailment(circle, shock, 4f, 1);
            Save(circle, pieces);

            var ruin = Weapon(db, "ringofruin", "Ring of Ruin", 5, ShapeKind.Ring, CastKind.Orbital,
                Element.Fire, new Color(1f, 0.5f, 0.2f),
                cooldown: 9f, radius: 6.6f, range: 0f,
                "Delapan bilah bara mengitarimu selama 8 detik, MELONTARKAN yang tersapu keluar. " +
                "Selama ia berputar, tidak ada yang bisa sampai ke badanmu.");
            Spin(ruin, blades: 8, duration: 8f, tick: 0.25f, edgeSpeed: 11f, push: 4f);
            Ailment(ruin, burn, 5f, 1);
            Save(ruin, pieces);
        }

        // =================================================================================
        //  Boomerang — pergi dan pulang
        // =================================================================================

        static void BuildBoomerang(ContentDatabase db, List<PieceDefinition> pieces)
        {
            var bleed = db.StatusById("bleed");
            var chill = db.StatusById("chill");

            var chakram = Weapon(db, "chakram", "Chakram", 1, ShapeKind.Corner, CastKind.Boomerang,
                Element.Arcane, new Color(0.9f, 0.92f, 0.7f),
                cooldown: 2.2f, radius: 1f, range: 9f,
                "Dilempar keluar, lalu KEMBALI ke tanganmu — dan melukai di kedua kakinya. Yang " +
                "sedang mengejarmu berjalan sendiri ke jalur pulangnya.");
            chakram.TravelSpeed = 14f;
            Ailment(chakram, bleed, 3f, 1);
            Save(chakram, pieces);

            var glaive = Weapon(db, "moonglaive", "Moon Glaive", 3, ShapeKind.Cup, CastKind.Boomerang,
                Element.Ice, new Color(0.7f, 0.88f, 1f),
                cooldown: 3.2f, radius: 1.6f, range: 14f,
                "Sabit bulan yang terbang jauh dan pulang lambat, membekukan seluruh jalurnya " +
                "dua kali.");
            glaive.TravelSpeed = 16f;
            Ailment(glaive, chill, 4f, 1);
            Save(glaive, pieces);
        }

        // =================================================================================
        //  Ricochet — sinar memantul
        // =================================================================================

        /// <summary>
        /// Satu-satunya keluarga skill yang TAMPILANNYA tumbuh bersama build.
        ///
        /// Yang naik per tier adalah jumlah pantulannya, dan pantulan itu yang digambar: tiga
        /// pantulan satu zig-zag, delapan belas mencorat-coret layar. Pemain melihat bahwa
        /// bukunya jadi lebih kuat tanpa membaca satu angka pun.
        /// </summary>
        static void BuildRicochet(ContentDatabase db, List<PieceDefinition> pieces)
        {
            var chill = db.StatusById("chill");
            var shock = db.StatusById("shock");

            var prism = Weapon(db, "prismray", "Prism Ray", 2, ShapeKind.Tee, CastKind.Ricochet,
                Element.Ice, new Color(0.75f, 0.95f, 1f),
                cooldown: 2.4f, radius: 0.5f, range: 11f,
                "Sinar yang memantul 4 kali — dari musuh ke musuh, dan dari tepi layar kalau " +
                "tidak ada yang bisa disambar.");
            Ray(prism, bounces: 4, bounceRange: 7f);
            Ailment(prism, chill, 3f, 1);
            Save(prism, pieces);

            var mirror = Weapon(db, "mirrorlance", "Mirror Lance", 4, ShapeKind.Zed, CastKind.Ricochet,
                Element.Lightning, new Color(0.85f, 0.9f, 1f),
                cooldown: 3.6f, radius: 0.6f, range: 14f,
                "Sembilan pantulan. Lintasannya sudah tidak bisa diikuti mata — yang terbaca " +
                "cuma di mana ia LEWAT.");
            Ray(mirror, bounces: 9, bounceRange: 9f);
            Ailment(mirror, shock, 4f, 1);
            Save(mirror, pieces);

            var scrawl = Weapon(db, "runescrawl", "Runescrawl", 5, ShapeKind.Chunk, CastKind.Ricochet,
                Element.Arcane, new Color(0.85f, 0.6f, 1f),
                cooldown: 4.4f, radius: 0.7f, range: 16f,
                "Delapan belas pantulan. Satu tembakan meninggalkan coretan yang menutupi seluruh " +
                "layar, dan semua yang disentuh garisnya ikut hangus.");
            Ray(scrawl, bounces: 18, bounceRange: 11f);
            Ailment(scrawl, shock, 5f, 2);
            Save(scrawl, pieces);
        }

        // =================================================================================
        //  Turret — menara yang menembak sendiri
        // =================================================================================

        /// <summary>
        /// Radius di sini BUKAN luas ledakan melainkan jangkauan tembak menaranya, dan Range adalah
        /// sejauh mana ia boleh DITANAM. Dua jarak yang berbeda, jadi dua angka yang berbeda —
        /// memakai satu angka untuk keduanya berarti menara yang bisa ditanam jauh otomatis juga
        /// menembak sejauh itu, dan seluruh keputusan penempatannya hilang.
        /// </summary>
        static void BuildTurret(ContentDatabase db, List<PieceDefinition> pieces)
        {
            var burn = db.StatusById("burn");

            var sentry = Weapon(db, "sentryeye", "Sentry Eye", 2, ShapeKind.Ell, CastKind.Turret,
                Element.Arcane, new Color(0.7f, 0.75f, 0.95f),
                cooldown: 6f, radius: 7f, range: 10f,
                "Menanam mata pengawas yang menembak sendiri selama 5 detik. Kamu boleh lari — " +
                "tembakannya tetap berlanjut di tempat yang kamu tinggalkan.");
            Tower(sentry, duration: 5f, interval: 0.6f, shots: 1);
            Save(sentry, pieces);

            var obelisk = Weapon(db, "obelisk", "Obelisk", 4, ShapeKind.Aitch, CastKind.Turret,
                Element.Fire, new Color(1f, 0.62f, 0.3f),
                cooldown: 8f, radius: 9f, range: 13f,
                "Pilar bara yang berdiri 7 detik dan menembak DUA sasaran sekaligus tiap kali " +
                "menyala. Ditanam di tengah gerombolan, ia jadi tempat kedua yang harus mereka urus.");
            Tower(obelisk, duration: 7f, interval: 0.45f, shots: 2);
            Ailment(obelisk, burn, 4f, 1);
            Save(obelisk, pieces);
        }

        // =================================================================================
        //  Shockwave — cincin yang melebar
        // =================================================================================

        static void BuildShockwave(ContentDatabase db, List<PieceDefinition> pieces)
        {
            var burn = db.StatusById("burn");

            // Radius keluarga ini DIUKUR TERHADAP TANGGA YANG SUDAH ADA, bukan dipilih dari rasa.
            //
            // Percobaan pertama memberi Ripple 5,5 dan Quake 9. Terukur di ruang uji: Quake keluar
            // di 680 dps melawan Meteor 413 di bintang yang sama. Sebabnya bukan damage-nya —
            // solver sudah menghitungnya benar — melainkan bahwa radius 9 adalah radius BINTANG
            // EMPAT (Doom Nova 9, Winter's End 8), sementara band bintang tiga berhenti di 7.
            // Radius itu kekuatan papan yang tidak muncul di angka damage mana pun, jadi kelebihan
            // radius adalah kekuatan yang lolos dari seluruh sistem penyeimbang.
            //
            // Band yang dihormati: ★1 3,4-4,5 · ★2 4,5-6,5 · ★3 5,5-7 · ★4 8-9 · ★5 6,8-12.
            var ripple = Weapon(db, "ripple", "Ripple", 1, ShapeKind.Line3, CastKind.Shockwave,
                Element.Arcane, new Color(0.8f, 0.9f, 0.95f),
                cooldown: 3f, radius: 4.5f, range: 0f,
                "Gelombang yang melebar keluar dari kakimu dan MELONTARKAN yang disapunya. " +
                "Sampai ke tepi belakangan, jadi ia mengejar yang sudah kabur.");
            Wave(ripple, speed: 11f, push: 5f);
            Save(ripple, pieces);

            var quake = Weapon(db, "quake", "Quake", 3, ShapeKind.Wedge, CastKind.Shockwave,
                Element.Fire, new Color(1f, 0.55f, 0.25f),
                cooldown: 4.5f, radius: 6.5f, range: 0f,
                "Retakan yang menjalar keluar dari kakimu sampai enam setengah unit, melontarkan " +
                "seluruh gerombolan yang mengepung sekaligus.");
            Wave(quake, speed: 13f, push: 8f);
            Ailment(quake, burn, 4f, 1);
            Save(quake, pieces);
        }

        // =================================================================================
        //  Seeker — rudal pengejar
        // =================================================================================

        static void BuildSeeker(ContentDatabase db, List<PieceDefinition> pieces)
        {
            var shock = db.StatusById("shock");

            var bolts = Weapon(db, "hexbolts", "Hexbolts", 2, ShapeKind.SBend, CastKind.Seeker,
                Element.Arcane, new Color(0.8f, 0.6f, 1f),
                cooldown: 2.8f, radius: 0f, range: 12f,
                "Empat rudal melengkung mengejar EMPAT musuh berbeda. Tidak ada dua yang " +
                "menghantam sasaran yang sama, jadi tidak ada satu pun yang meledak di mayat.");
            bolts.Hits = 4;
            bolts.TravelSpeed = 11f;
            Save(bolts, pieces);

            var storm = Weapon(db, "hexstorm", "Hex Storm", 4, ShapeKind.Fork, CastKind.Seeker,
                Element.Lightning, new Color(0.7f, 0.8f, 1f),
                cooldown: 4f, radius: 0f, range: 16f,
                "Delapan rudal berangkat ke delapan arah lalu berbelok bersamaan. Jawaban " +
                "untuk gerombolan yang berpencar.");
            storm.Hits = 8;
            storm.TravelSpeed = 13f;
            Ailment(storm, shock, 4f, 1);
            Save(storm, pieces);
        }

        // =================================================================================
        //  Tether — sinar pengunci
        // =================================================================================

        /// <summary>
        /// Satu-satunya jalur damage di buku yang menagih SATU makhluk terus-menerus. Ada karena
        /// seluruh sisanya dituning untuk gerombolan, dan itu berarti boss — satu-satunya lawan
        /// yang bukan gerombolan — tidak punya jawaban yang dirancang untuknya.
        /// </summary>
        static void BuildTether(ContentDatabase db, List<PieceDefinition> pieces)
        {
            var bleed = db.StatusById("bleed");

            var siphon = Weapon(db, "siphonbeam", "Siphon Beam", 2, ShapeKind.Square, CastKind.Tether,
                Element.Arcane, new Color(0.6f, 1f, 0.8f),
                cooldown: 5f, radius: 0.3f, range: 10f,
                "Sinar yang MENGUNCI satu musuh dan terus menggerusnya selama 3,5 detik sambil " +
                "menyeretnya mendekat. Mengunci ulang sendiri kalau sasarannya mati.");
            Beam(siphon, duration: 3.5f, tick: 0.25f, pull: 3f);
            Save(siphon, pieces);

            var chain = Weapon(db, "soulchain", "Soul Chain", 4, ShapeKind.Ess, CastKind.Tether,
                Element.Arcane, new Color(0.85f, 0.4f, 0.55f),
                cooldown: 6.5f, radius: 0.45f, range: 14f,
                "Rantai jiwa yang menempel 5 detik penuh dan menagih empat kali per detik. " +
                "Dibuat untuk yang bernyawa tebal — dan di buku ini itu berarti boss.");
            Beam(chain, duration: 5f, tick: 0.2f, pull: 5f);
            Ailment(chain, bleed, 5f, 2);
            Save(chain, pieces);
        }

        // =================================================================================
        //  Barrage — hujan hantaman beraba-aba
        // =================================================================================

        static void BuildBarrage(ContentDatabase db, List<PieceDefinition> pieces)
        {
            var chill = db.StatusById("chill");
            var burn = db.StatusById("burn");

            var starfall = Weapon(db, "starfall", "Starfall", 3, ShapeKind.Cross, CastKind.Barrage,
                Element.Ice, new Color(0.72f, 0.85f, 1f),
                cooldown: 5f, radius: 2.6f, range: 14f,
                "Lima hantaman beraba-aba jatuh BERURUTAN di titik berpencar. Menghindarinya " +
                "adalah gerakan, bukan satu langkah.");
            Rain(starfall, shots: 5, telegraph: 0.7f, gap: 0.22f);
            Ailment(starfall, chill, 4f, 1);
            Save(starfall, pieces);

            var judgement = Weapon(db, "judgement", "Judgement", 5, ShapeKind.Big3, CastKind.Barrage,
                Element.Fire, new Color(1f, 0.78f, 0.35f),
                cooldown: 8f, radius: 3.6f, range: 18f,
                "Sembilan pilar cahaya jatuh beruntun selama hampir tiga detik. Lapangan tempat " +
                "ia turun tidak bisa dilewati sampai selesai.");
            Rain(judgement, shots: 9, telegraph: 0.9f, gap: 0.18f);
            Ailment(judgement, burn, 6f, 2);
            Save(judgement, pieces);
        }

        // =================================================================================
        //  resep
        // =================================================================================

        /// <summary>
        /// Tanpa ini, seluruh tier di atas satu bintang tidak bisa didapat dari mana pun: drop cuma
        /// mengeluarkan bintang satu, dan toko tidak menyimpan semuanya.
        ///
        /// Bahannya sengaja dicampur dengan piece LAMA. Kalau keluarga baru cuma berevolusi ke
        /// dalam dirinya sendiri, ia jadi cabang terpisah yang tidak pernah bertemu isi buku yang
        /// sudah ada — dan pemain yang membuka run dengan Fireball tidak punya satu pun jalan
        /// menuju perilaku baru mana pun.
        /// </summary>
        static int Link(ContentDatabase db, List<RecipeDefinition> recipes)
        {
            int before = recipes.Count;

            // --- 2 bintang: pintu masuk, semuanya dari piece pembuka ---
            R(recipes, db, "prismray_a", "prismray", "chakram", "frostshard");
            R(recipes, db, "sentryeye_a", "sentryeye", "ripple", "sparkbolt");
            R(recipes, db, "hexbolts_a", "hexbolts", "chakram", "sparkbolt");
            R(recipes, db, "siphonbeam_a", "siphonbeam", "bladedance", "minorheal");

            // --- 3 bintang ---
            R(recipes, db, "stormcircle_a", "stormcircle", "bladedance", "bladedance");
            R(recipes, db, "stormcircle_b", "stormcircle", "bladedance", "sparkshards");
            R(recipes, db, "moonglaive_a", "moonglaive", "chakram", "prismray");
            R(recipes, db, "quake_a", "quake", "ripple", "ripple");
            R(recipes, db, "quake_b", "quake", "ripple", "emberburst");
            R(recipes, db, "starfall_a", "starfall", "hexbolts", "hujanapi");

            // --- 4 bintang ---
            R(recipes, db, "mirrorlance_a", "mirrorlance", "prismray", "prismray");
            R(recipes, db, "obelisk_a", "obelisk", "sentryeye", "sentryeye");
            R(recipes, db, "hexstorm_a", "hexstorm", "hexbolts", "hexbolts");
            R(recipes, db, "hexstorm_b", "hexstorm", "hexbolts", "stormcircle");
            R(recipes, db, "soulchain_a", "soulchain", "siphonbeam", "siphonbeam");

            // --- 5 bintang: masing-masing menuntut DUA cabang berbeda bertemu ---
            R(recipes, db, "ringofruin_a", "ringofruin", "stormcircle", "soulchain");
            R(recipes, db, "runescrawl_a", "runescrawl", "mirrorlance", "hexstorm");
            R(recipes, db, "judgement_a", "judgement", "starfall", "obelisk");

            return recipes.Count - before;
        }

        // =================================================================================
        //  perkakas aset
        // =================================================================================

        static void Spin(PieceDefinition a, int blades, float duration, float tick, float edgeSpeed,
            float push)
        {
            a.Hits = blades;
            a.ZoneDuration = duration;
            a.ZoneTickInterval = tick;
            a.TravelSpeed = edgeSpeed;
            a.PushForce = push;
        }

        static void Ray(PieceDefinition a, int bounces, float bounceRange)
        {
            a.Bounces = bounces;
            a.BounceRange = bounceRange;
        }

        static void Tower(PieceDefinition a, float duration, float interval, int shots)
        {
            a.ZoneDuration = duration;
            a.ZoneTickInterval = interval;
            a.Hits = shots;
        }

        static void Wave(PieceDefinition a, float speed, float push)
        {
            a.TravelSpeed = speed;
            a.PushForce = push;
        }

        static void Beam(PieceDefinition a, float duration, float tick, float pull)
        {
            a.ZoneDuration = duration;
            a.ZoneTickInterval = tick;
            a.PushForce = pull;
        }

        static void Rain(PieceDefinition a, int shots, float telegraph, float gap)
        {
            a.Hits = shots;
            a.TelegraphDelay = telegraph;
            a.ZoneTickInterval = gap;
        }

        static void Ailment(PieceDefinition a, StatusDefinition status, float duration, int points)
        {
            if (status == null) return;

            a.AppliedStatus = status;
            a.StatusDuration = duration;
            a.AppliedPoints = points;
        }

        /// <summary>
        /// Skill penyerang. Damage dan mana ditinggal di 1 dengan sengaja — angka aslinya
        /// dipecahkan <see cref="BalanceTunePass"/> dari cooldown, radius, dan durasi di sini.
        /// </summary>
        static PieceDefinition Weapon(ContentDatabase db, string id, string name, int stars,
            ShapeKind shape, CastKind kind, Element element, Color color,
            float cooldown, float radius, float range, string blurb)
        {
            var a = Load(id);

            a.Id = id;
            a.DisplayName = name;
            a.Stars = stars;
            a.Layer = Layer.Skill;
            a.Kind = kind;
            a.Element = element;
            a.Shape = shape;
            a.Color = color;
            a.Trigger = CastTrigger.Cooldown;
            a.BaseCooldown = cooldown;
            a.Radius = radius;
            a.Range = range;
            a.BaseDamage = 1f;
            a.ManaCost = 1f;
            a.Forks = 1;
            a.Bounces = 0;
            a.Hits = 1;
            a.Blurb = blurb;

            // Dibersihkan eksplisit, bukan dibiarkan apa adanya. Pass ini idempoten dan boleh
            // dijalankan ulang setelah angkanya diubah tangan — kalau sisa nilai dari jalannya
            // yang lalu dibiarkan, satu bidang yang dihapus dari kode ini akan tetap hidup di
            // aset selamanya, dan tidak ada yang bisa menebak dari mana asalnya.
            a.AppliedStatus = null;
            a.StatusDuration = 0f;
            a.AppliedPoints = 1;
            a.PushForce = 0f;
            a.ZoneDrift = 0f;
            a.GrantOnCast = null;
            a.GrantOnKill = null;
            a.ConsumesCharge = null;
            a.TriggerStatus = null;

            return a;
        }

        static void Save(PieceDefinition a, List<PieceDefinition> pieces)
        {
            EditorUtility.SetDirty(a);
            if (!pieces.Contains(a)) pieces.Add(a);
        }

        static PieceDefinition Load(string id)
        {
            string path = $"{PieceFolder}/Piece_{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<PieceDefinition>(path);

            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<PieceDefinition>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void R(List<RecipeDefinition> recipes, ContentDatabase db, string fileId,
            string resultId, params string[] ingredientIds)
        {
            var result = db.ById(resultId);
            if (result == null)
            {
                Debug.LogWarning($"[BehaviourPass] hasil '{resultId}' tidak ada, resep dilewati.");
                return;
            }

            var ingredients = new PieceDefinition[ingredientIds.Length];

            for (int i = 0; i < ingredientIds.Length; i++)
            {
                ingredients[i] = db.ById(ingredientIds[i]);

                // Resep dengan bahan kosong akan diam-diam cocok dengan apa pun. Lebih baik tidak
                // ada resepnya sama sekali daripada ada resep yang berperilaku acak.
                if (ingredients[i] != null) continue;

                Debug.LogWarning($"[BehaviourPass] bahan '{ingredientIds[i]}' tidak ada, " +
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
