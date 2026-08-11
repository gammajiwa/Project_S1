using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Satu folder per skill, satu prefab VFX per skill — dan piece menunjuk ke prefab MILIKNYA,
    /// bukan langsung ke aset paket.
    ///
    /// Bentuknya wrapper: root kosong bernama skill-nya, prefab paket bersarang di dalamnya.
    /// Itu permintaan pemilik project ("buatin folder tiap skill, buatin juga prefabnya, tar gw
    /// pasang sendiri beberapa yang jelek") dan sekaligus kontrak pass ini: <b>wrapper yang sudah
    /// ada TIDAK PERNAH dibangun ulang</b>. Mengganti efek sebuah skill = buka prefabnya, hapus
    /// anaknya, seret prefab paket lain ke dalamnya — tanpa kode, dan pass ini tidak akan
    /// menimpanya. Menjalankan ulang pass hanya (1) membangun wrapper yang belum ada, dan
    /// (2) meluruskan pointer <c>CastVfx</c> yang belum menunjuk wrapper.
    ///
    /// Pemilihan default per skill (buat wrapper yang belum pernah dibangun):
    /// - Elemen dipegang teguh: Fire api, Ice es, Lightning listrik, Arcane ungu/void,
    ///   arcane-racun pakai racun.
    /// - Kind menentukan bentuk: peluru = badan ber-ekor; nova = ledakan; AoE jatuhan = hujan
    ///   meteor GabrielAguiar; zone = loop yang betah di tanah; ward = kubah barrier.
    /// - Skill petir bertubuh peluru memakai Vefects Trail — permintaan langsung: yang bulat
    ///   solid terlalu gemuk, trail itu ramping.
    /// </summary>
    public static class VfxPass
    {
        const string PieceFolder = "Assets/GameData/Pieces";
        const string SkillRoot = "Assets/Prefabs/Skills";

        const string Ga = "Assets/Art/VFX/Packs/GabrielAguiarProductions/UniqueMagicAbilitiesVol_2/Prefabs/";
        const string Cfxr = "Assets/Art/VFX/Packs/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/";
        const string Vf = "Assets/Art/VFX/Packs/Vefects/Trails VFX URP/VFX/Particles/";
        const string Lana = "Assets/Prefabs/Effects/";

        /// <summary>
        /// (nama file piece, prefab paket default, CastVfxScale, skala anak di dalam wrapper).
        /// childScale hampir selalu 1 — satu-satunya pengecualian dibakar ke wrapper supaya
        /// pemilik project yang membukanya melihat angka sebenarnya, bukan perkalian tersembunyi.
        /// </summary>
        static readonly (string piece, string path, float scale, float childScale)[] Map =
        {
            // ---------- PROJECTILE ----------
            ("fireball",        Cfxr + "Fire/CFXR3 Fireball A + Fire Trail.prefab",  1f, 1f),
            ("greaterfireball", Cfxr + "Fire/CFXR3 Fireball B + Fire Trail.prefab",  1f, 1f),
            ("frostshard",      Cfxr + "Ice/CFXR3 Iceball A + Ice Trail.prefab",     1f, 1f),
            ("glacialspike",    Cfxr + "Ice/CFXR3 Iceball B + Ice Trail.prefab",     1f, 1f),
            // Petir = trail ramping, bukan bola. "3 skill petir buat ramping, yang sekarang
            // solid banget waktu nembak" (2026-08-10).
            ("sparkbolt",       Vf + "VFX_Trail_Electric.prefab",                    1f, 1f),

            // ---------- NOVA ----------
            ("emberburst",  Cfxr + "Explosions/CFXR3 Fire Explosion A.prefab",           1f, 1f),
            ("frostnova",   Cfxr + "Ice/CFXR3 Hit Ice A (Ground).prefab",                1f, 1f),
            ("staticfield", Cfxr + "Electric/CFXR Electric Explosion.prefab",            1f, 1f),
            ("steamburst",  Cfxr + "Misc/CFXR Smoke Poof Circle Flat.prefab",            1f, 1f),
            ("blizzard",    Cfxr + "Ice/CFXR3 Hit Ice B (Ground).prefab",                1f, 1f),
            ("thunderclap", Cfxr + "Electric/CFXR4 Sparks Explosion.prefab",             1f, 1f),
            ("rimenova",    Ga + "vfx_ImpactAoE02_Ice.prefab",                           0.8f, 1f),
            ("nullsphere",  Ga + "vfx_ImpactAoE06_Void.prefab",                          0.8f, 1f),
            ("novakiamat",  Cfxr + "Explosions/CFXR4 Explosion Orange (HDR) + Smoke.prefab", 1f, 1f),

            // ---------- CHAIN ----------
            ("arcbolt",        Cfxr + "Electric/CFXR3 Hit Electric A (Air).prefab",              1f, 1f),
            ("riftchain",      Cfxr + "Electric/Variants/CFXR3 Hit Electric A (Air, Purple).prefab", 1f, 1f),
            ("chainlightning", Cfxr + "Electric/CFXR3 Hit Electric B (Air).prefab",              1f, 1f),
            ("prismabeku",     Cfxr + "Ice/CFXR3 Hit Ice A (Air).prefab",                        1f, 1f),
            ("thundercrown",   Cfxr + "Electric/CFXR3 Hit Electric C (Air).prefab",              1f, 1f),
            ("stormbreaker",   Cfxr + "Electric/CFXR Lightning Impact.prefab",                   1f, 1f),

            // ---------- HEAL ----------
            ("minorheal",   Ga + "vfx_BuffAoE09_Heal.prefab",   0.7f, 1f),
            ("greaterheal", Ga + "vfx_ImpactAoE09_Heal.prefab", 0.7f, 1f),

            // ---------- AREA AT TARGET ----------
            ("hujanapi",      Ga + "vfx_MeteorRain01_Fire.prefab",          0.7f, 1f),
            ("hailstorm",     Ga + "vfx_MeteorRain02_Ice.prefab",           0.7f, 1f),
            ("plaguebrand",   Ga + "vfx_DebuffAoE04_Poison.prefab",         0.8f, 1f),
            ("pusaran",       Ga + "vfx_DebuffAoE06_Void.prefab",           0.8f, 1f),
            ("gravitywell",   Ga + "vfx_DebuffVertical06_Void.prefab",      0.8f, 1f),
            ("firestormcore", Cfxr + "Explosions/CFXR3 Fire Explosion B.prefab", 1f, 1f),
            ("lepasamuk",     Cfxr + "Explosions/CFXR4 Explosion Quick.prefab",  1f, 1f),
            ("meteor",        Ga + "vfx_SingleComet01_Fire.prefab",         0.8f, 1f),
            ("tempest",       Cfxr + "Electric/CFXR Lightning Strike + Impact.prefab", 1f, 1f),
            ("wintersend",    Ga + "vfx_ArrowRain02_Ice.prefab",            0.7f, 1f),
            ("cataclysm",     Ga + "vfx_MeteorRain01_Fire Variant.prefab",  0.8f, 1f),

            // ---------- LINE ----------
            // Sabetan pedang api, bukan flamethrower — "flame lash masih gak cocok" (2026-08-10).
            ("flamelash",    Cfxr + "Sword Trails/Fire/CFXR4 Sword Hit FIRE (Slash).prefab", 1f, 1f),
            ("icelance",     Cfxr + "Ice/CFXR3 Hit Ice B (Air).prefab",                     1f, 1f),
            ("sabetanpetir", Cfxr + "Impacts/CFXR Slash (Blue).prefab",                     1f, 1f),
            ("infernowave",  Cfxr + "Fire/CFXR4 Flamethrower + Smoke.prefab",               1f, 1f),
            ("voidlance",    Cfxr + "Explosions/CFXR4 Monster Explosion Purple (Small).prefab", 0.9f, 1f),
            ("ragnarok",     Cfxr + "Fire/CFXR Fire Breath.prefab",                         1.2f, 1f),

            // ---------- ZONE ----------
            ("cinderpatch",    Cfxr + "Fire/CFXR4 Burning Fire.prefab",        1f, 1f),
            ("stormcell",      Cfxr + "Electric/CFXR Electric Surface.prefab", 1f, 1f),
            ("kubanganracun",  Cfxr + "Liquids/CFXR2 Potion Bubbles (Loop).prefab", 1f, 1f),
            ("frostbitefield", Cfxr + "Nature/CFXR4 Snow 'Splashes'.prefab",   1f, 1f),
            ("badaisalju",     Cfxr + "Nature/CFXR4 Snow Falling.prefab",      1f, 1f),
            ("plaguebloom",    Cfxr + "Misc/CFXR4 Flies Cloud.prefab",         1f, 1f),
            ("singularity",    Cfxr + "Misc/CFXR Portal.prefab",               1f, 1f),
            // Rockfall aslinya memenuhi layar — "ashfall terlalu besar, areanya kecil"
            // (2026-08-10). Dikecilkan DI DALAM wrapper supaya terlihat saat prefabnya dibuka.
            ("ashfall",        Lana + "Rockfall.prefab",                       1f, 0.22f),
            ("ionstorm",       Lana + "Orb_lightning.prefab",                  1f, 1f),

            // ---------- CLEANSE ----------
            ("cahayapembersih", Cfxr + "Light/CFXR3 Hit Light A (Air).prefab",    1f, 1f),
            ("fajarpembersih",  Cfxr + "Light/CFXR3 Hit Light Fireworks.prefab",  1f, 1f),

            // ---------- RADIAL ----------
            ("belatiberputar", Cfxr + "Light/CFXR3 Lightball B + Trail.prefab", 0.8f, 1f),
            ("absolutezero",   Cfxr + "Ice/CFXR3 Iceball A + Ice Trail.prefab", 0.9f, 1f),

            // ---------- DETONATE ----------
            ("sunder",    Cfxr + "Impacts/CFXR2 Hit (Contrast).prefab",     1f, 1f),
            ("rupture",   Cfxr + "Liquids/CFXR2 Blood Shape Splash.prefab", 1f, 1f),
            ("reckoning", Cfxr + "Fire/CFXR3 Hit Fire C (Air).prefab",      1f, 1f),

            // ---------- ORBIT — pecahan petir juga ramping ----------
            ("sparkshards", Vf + "VFX_Trail_Electric.prefab", 0.7f, 1f),
            ("stormshards", Vf + "VFX_Trail_Electric.prefab", 1f, 1.25f),

            // ---------- BLINK ----------
            ("blinkstep", Cfxr + "Misc/CFXR Magic Poof.prefab",                              1f, 1f),
            ("voidstep",  Cfxr + "Space/CFXR4 Teleporter Rings Upwards (HDR, Purple).prefab", 1f, 1f),

            // ---------- WARD ----------
            ("wardpetty", Cfxr + "Electric/CFXR Electric Barrier Simple (HDR).prefab",        1f, 1f),
            ("wardaegis", Cfxr + "Electric/CFXR Electric Barrier (HDR).prefab",               1f, 1f),
            ("bulwark",   Cfxr + "Electric/Variants/CFXR Electric Barrier (HDR, Purple).prefab", 1.1f, 1f),

            // ---------- SURGE / RESTORE ----------
            ("quickfoot",      Ga + "vfx_BuffAoE08_Speed.prefab",  0.7f, 1f),
            ("quickcastsigil", Ga + "vfx_BuffAoE07_Arcane.prefab", 0.7f, 1f),
            ("manawell",       Ga + "vfx_BuffAoE03_Water.prefab",  0.7f, 1f),

            // ---------- SUNSTRIKE ----------
            ("sunlance",   Ga + "vfx_DebuffVertical01_Fire.prefab",     0.9f, 1f),
            ("sunstrike",  Ga + "vfx_ImpactAoE01_Fire.prefab",          0.9f, 1f),
            ("solarflare", Ga + "vfx_SingleComet01_Fire Variant.prefab", 0.9f, 1f),

            // ---------- ROLLING BALL ----------
            ("emberroll",   Cfxr + "Fire/CFXR Fireball + Fire Trail.prefab", 1f, 1f),
            ("chaosmeteor", Cfxr + "Fire/CFXR2 Fireball.prefab",             1.2f, 1f),

            // ---------- VORTEX ----------
            ("whirlwind", Cfxr + "Nature/CFXR4 Wind Zone.prefab", 1f, 1f),
            ("tornado",   Lana + "Tornado_sand.prefab",           1f, 1f),
            ("maelstrom", Lana + "Tornado_snow.prefab",           1f, 1f),

            // ---------- FORCE PUSH ----------
            ("shove", Cfxr + "Explosions/CFXR4 Wave Explosion Purple.prefab", 1f, 1f),

            // =========================================================================
            //  DELAPAN PERILAKU BARU
            //
            //  Semuanya diambil dari SATU paket — GabrielAguiar — dan itu keputusan, bukan
            //  kemalasan. Audit menyebut tiga sebab efek lama terbaca jelek, dan yang pertama
            //  adalah gaya paket yang campur aduk: CFXR itu kartun terang ala mobile, GA itu
            //  stylized RPG, dan keduanya di satu layar membuat keduanya terlihat salah.
            //  GA punya bentuk yang sama untuk DELAPAN elemen, jadi satu keluarga skill bisa
            //  konsisten elemennya tanpa pernah keluar dari satu bahasa visual.
            // =========================================================================

            // ---------- ORBITAL: aura melingkar di kaki, dilebarkan ke radius cincinnya ----------
            ("bladedance",  Ga + "vfx_BuffAoE07_Arcane.prefab",      0.9f, 1f),
            ("stormcircle", Ga + "vfx_BuffAoE05_Electricity.prefab", 1f,   1f),
            ("ringofruin",  Ga + "vfx_BuffAoE01_Fire.prefab",        1.1f, 1f),

            // ---------- BOOMERANG: badan yang terbang, jadi komet ----------
            ("chakram",    Ga + "vfx_SingleComet07_Arcane.prefab", 0.6f, 1f),
            ("moonglaive", Ga + "vfx_SingleComet02_Ice.prefab",    0.75f, 1f),

            // ---------- RICOCHET: disembur di titik pantul, bukan di sepanjang garis ----------
            ("prismray",    Ga + "vfx_ImpactAoE02_Ice.prefab",         0.45f, 1f),
            ("mirrorlance", Ga + "vfx_ImpactAoE05_Electricity.prefab", 0.5f,  1f),
            ("runescrawl",  Ga + "vfx_ImpactAoE07_Arcane.prefab",      0.55f, 1f),

            // ---------- TURRET: berdiri di tanah selama beberapa detik, jadi harus LOOP ----------
            ("sentryeye", Ga + "vfx_DebuffVertical07_Arcane.prefab", 0.8f, 1f),
            ("obelisk",   Ga + "vfx_DebuffVertical01_Fire.prefab",   0.9f, 1f),

            // ---------- SHOCKWAVE: cincin yang melebar ----------
            ("ripple", Ga + "vfx_ImpactAoE08_Speed.prefab", 0.8f, 1f),
            ("quake",  Ga + "vfx_ImpactAoE01_Fire.prefab",  0.9f, 1f),

            // ---------- SEEKER: satu efek per rudal, jadi harus KECIL ----------
            ("hexbolts", Ga + "vfx_SingleComet06_Void.prefab",        0.45f, 1f),
            ("hexstorm", Ga + "vfx_SingleComet05_Electricity.prefab", 0.5f,  1f),

            // ---------- TETHER: disembur di ujung sinar tiap denyut, jadi harus kecil & cepat ----------
            ("siphonbeam", Ga + "vfx_ImpactAoE09_Heal.prefab",  0.4f, 1f),
            ("soulchain",  Ga + "vfx_ImpactAoE04_Poison.prefab", 0.5f, 1f),

            // ---------- BARRAGE: hujan berurutan, satu semburan per hantaman ----------
            ("starfall",  Ga + "vfx_MeteorRain02_Ice.prefab",  0.5f, 1f),
            ("judgement", Ga + "vfx_ArrowRain01_Fire.prefab",  0.6f, 1f),
        };

        /// <summary>
        /// Sumber yang DIGANTI karena tidak nyambung dengan skillnya, bukan karena jelek.
        ///
        /// Audit 2026-08-11 menamai satu per satu, dan semuanya jenis kesalahan yang sama: prefab
        /// dipilih karena kebetulan ada, bukan karena menggambarkan skillnya. Guruh yang keluar
        /// sebagai percikan generik dan peledak darah yang menyemburkan cipratan darah beneran
        /// membuat pemain membaca kejadian yang salah, dan itu lebih mahal daripada sekadar jelek.
        ///
        /// Dipisah dari <see cref="Map"/> supaya bisa dijalankan SENDIRI: wrapper-nya sudah
        /// terlanjur ada, dan kontrak pass utama adalah tidak pernah menimpanya. Yang di sini
        /// memang harus menimpa — itu seluruh gunanya.
        /// </summary>
        static readonly (string piece, string path, float scale)[] Corrections =
        {
            // Percikan generik -> sambaran petir sungguhan.
            ("thunderclap", Cfxr + "Electric/CFXR Lightning Strike + Impact.prefab", 1f),

            // Cipratan DARAH untuk peledak BLEED terdengar masuk akal sampai dilihat: yang
            // tergambar organ, bukan ledakan. Peledak harus terbaca sebagai LEDAKAN.
            ("rupture", Cfxr + "Explosions/CFXR4 Explosion Quick.prefab", 1f),

            // Gelembung ramuan -> kubangan racun sungguhan yang betah di tanah.
            ("kubanganracun", Ga + "vfx_DebuffAoE04_Poison.prefab", 0.9f),

            // Awan lalat -> mekar wabah. Lalatnya lucu, dan skill ini bukan lelucon.
            ("plaguebloom", Ga + "vfx_DebuffAoE04_Poison.prefab", 1f),

            // Tornado PASIR untuk skill yang bukan pasir, dan tornado SALJU untuk yang bukan salju.
            // Keduanya petir/angin — dijadikan satu keluarga angin yang konsisten.
            ("tornado",   Ga + "vfx_DebuffAoE08_Speed.prefab", 1.1f),
            ("maelstrom", Ga + "vfx_DebuffAoE05_Electricity.prefab", 1.2f),

            // Asap poof untuk ledakan uap: uapnya benar, ledakannya hilang.
            ("steamburst", Ga + "vfx_ImpactAoE03_Water.prefab", 0.8f),

            // Ledakan monster ungu -> tusukan void. Skillnya TOMBAK, bukan monster meledak.
            ("voidlance", Ga + "vfx_DebuffVertical06_Void.prefab", 0.8f),
        };

        /// <summary>
        /// Menimpa isi wrapper untuk piece yang sumbernya salah pilih. Terpisah dari
        /// <see cref="Run"/> karena ini SATU-SATUNYA jalur di pass ini yang membuang art yang
        /// sudah ada — dan itu harus jadi keputusan sadar, bukan efek samping menjalankan menu
        /// yang lain.
        /// </summary>
        [MenuItem("Tools/Grimoire/Fix Mismatched Skill VFX (TIMPA wrapper)")]
        public static void FixMismatched()
        {
            bool ok = EditorUtility.DisplayDialog(
                "Timpa wrapper yang sumbernya meleset?",
                Corrections.Length + " wrapper VFX akan diisi ulang dengan prefab yang cocok " +
                "dengan skillnya.\n\nKalau kamu sudah mengganti salah satunya sendiri, " +
                "gantinya akan HILANG.",
                "Timpa", "Batal");

            if (!ok) return;

            ApplyCorrections();
        }

        /// <summary>
        /// Badan <see cref="FixMismatched"/> tanpa dialognya. Terpisah supaya bisa dijalankan dari
        /// skrip — dialog modal membekukan editor sampai ada yang mengklik, dan itu berarti
        /// jalur ini tidak akan pernah bisa diuji otomatis.
        /// </summary>
        public static void ApplyCorrections()
        {
            int fixedCount = 0;
            var problems = new List<string>();

            foreach (var (piece, path, scale) in Corrections)
            {
                var def = AssetDatabase.LoadAssetAtPath<PieceDefinition>(
                    $"{PieceFolder}/Piece_{piece}.asset");

                if (def == null)
                {
                    problems.Add($"piece hilang: {piece}");
                    continue;
                }

                // Piece yang sudah berubah jadi segel tidak punya cast untuk digambar. Dilewati
                // diam-diam bukan pilihan — yang menjalankan ini harus tahu kenapa jumlahnya kurang.
                if (def.IsPassive || def.IsRune)
                {
                    problems.Add($"{piece} sekarang bukan skill lagi, dilewati");
                    continue;
                }

                string folder = Sanitize(def.DisplayName);
                string wrapperPath = $"{SkillRoot}/{folder}/Vfx_{folder}.prefab";

                AssetDatabase.DeleteAsset(wrapperPath);

                var wrapper = BuildWrapper(wrapperPath, folder, path, 1f, problems);
                if (wrapper == null) continue;

                def.CastVfx = wrapper;
                def.CastVfxScale = scale;
                EditorUtility.SetDirty(def);
                fixedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[VfxPass] {fixedCount} sumber VFX diluruskan." +
                      (problems.Count > 0 ? "\n - " + string.Join("\n - ", problems) : ""));
        }

        [MenuItem("Tools/Grimoire/Assign Skill VFX")]
        public static void Run()
        {
            int built = 0, repointed = 0, kept = 0;
            var problems = new List<string>();

            foreach (var (piece, path, scale, childScale) in Map)
            {
                var def = AssetDatabase.LoadAssetAtPath<PieceDefinition>(
                    $"{PieceFolder}/Piece_{piece}.asset");
                if (def == null)
                {
                    problems.Add($"piece hilang: {piece}");
                    continue;
                }

                string folder = Sanitize(def.DisplayName);
                string wrapperPath = $"{SkillRoot}/{folder}/Vfx_{folder}.prefab";

                var wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(wrapperPath);
                if (wrapper == null)
                {
                    wrapper = BuildWrapper(wrapperPath, folder, path, childScale, problems);
                    if (wrapper == null) continue;
                    built++;
                }

                if (def.CastVfx == wrapper && Mathf.Approximately(def.CastVfxScale, scale))
                {
                    kept++;
                    continue;
                }

                def.CastVfx = wrapper;
                def.CastVfxScale = scale;
                EditorUtility.SetDirty(def);
                repointed++;
            }

            AssetDatabase.SaveAssets();
            Audit(problems);

            Debug.Log($"[VfxPass] wrapper dibangun {built}, pointer dibetulkan {repointed}, " +
                      $"sudah benar {kept}, masalah {problems.Count}." +
                      (problems.Count > 0 ? "\n - " + string.Join("\n - ", problems) : ""));
        }

        /// <summary>
        /// Root kosong + prefab paket bersarang di dalamnya. InstantiatePrefab, bukan Instantiate:
        /// hanya yang pertama yang menjaga tautan nested-prefab, dan tautan itulah yang membuat
        /// isi wrapper bisa diganti dengan seret-lepas di editor.
        /// </summary>
        static GameObject BuildWrapper(string wrapperPath, string folder, string packPath,
            float childScale, List<string> problems)
        {
            var pack = AssetDatabase.LoadAssetAtPath<GameObject>(packPath);
            if (pack == null)
            {
                problems.Add($"prefab paket hilang: {packPath}");
                return null;
            }

            string dir = $"{SkillRoot}/{folder}";
            if (!AssetDatabase.IsValidFolder(SkillRoot))
            {
                AssetDatabase.CreateFolder("Assets/Art/VFX", "Skills");
            }

            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(SkillRoot, folder);

            var root = new GameObject("Vfx_" + folder);
            try
            {
                var child = (GameObject)PrefabUtility.InstantiatePrefab(pack);
                child.transform.SetParent(root.transform, false);
                child.transform.localScale = Vector3.one * childScale;

                return PrefabUtility.SaveAsPrefabAsset(root, wrapperPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static string Sanitize(string name)
        {
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(System.Array.IndexOf(System.IO.Path.GetInvalidFileNameChars(), c) >= 0
                    ? '_' : c);
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Dua pemeriksaan yang gagalnya senyap tanpa audit: skill aktif yang masih polos, dan
        /// kind gendong (Zone/Vortex/Ward) yang efeknya sekali-main — main sebentar lalu diam
        /// padahal kubangannya masih hidup. TrailRenderer dihitung hidup: dia menggambar terus
        /// selama digerakkan, tidak peduli ada partikel loop atau tidak.
        /// </summary>
        static void Audit(List<string> problems)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:PieceDefinition", new[] { PieceFolder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<PieceDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || def.Layer != Layer.Skill) continue;
                if (def.Kind == CastKind.Passive || def.Kind == CastKind.AuraOnly) continue;

                if (def.CastVfx == null)
                {
                    problems.Add($"masih polos: {def.name} ({def.Kind})");
                    continue;
                }

                bool needsLoop = def.Kind == CastKind.Zone || def.Kind == CastKind.Vortex ||
                                 def.Kind == CastKind.Ward;
                if (!needsLoop) continue;

                bool alive = def.CastVfx.GetComponentsInChildren<TrailRenderer>(true).Length > 0;
                foreach (var ps in def.CastVfx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (!ps.main.loop) continue;
                    alive = true;
                    break;
                }

                if (!alive) problems.Add($"kind gendong tapi efeknya sekali-main: {def.name} <- {def.CastVfx.name}");
            }
        }
    }
}
