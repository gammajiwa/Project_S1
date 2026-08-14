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
        const string Hovl = "Assets/Art/VFX/Packs/Hovl Studio/Magic effects pack/Prefabs/";

        // Paket Lana yang UTUH — di Plugin/, TEMPAT ASLINYA, bukan salinan kedua di Packs/.
        // Sempat ada dua salinan pack ini dan class script demonya langsung bentrok (CS0101);
        // yang benar adalah melengkapi pack lama di tempatnya, bukan menaruh kembarannya.
        // `Lana` di atas cuma lima berkas yang dulu disalin keluar; orb-orbnya ada di sini.
        const string LanaPack = "Assets/Plugin/Lana Studio/Environment VFX pack/Prefabs/";

        /// <summary>
        /// (nama file piece, prefab paket default, CastVfxScale, skala anak di dalam wrapper).
        /// childScale hampir selalu 1 — satu-satunya pengecualian dibakar ke wrapper supaya
        /// pemilik project yang membukanya melihat angka sebenarnya, bukan perkalian tersembunyi.
        /// </summary>
        static readonly (string piece, string path, float scale, float childScale)[] Map =
        {
            // ---------- PROJECTILE ----------
            ("fireball",        Cfxr + "Fire/CFXR3 Fireball A + Fire Trail.prefab",  1f, 1f),
            // 1.5: "kurang gede, gak terlalu beda sama fireball biasa" — lebih besar itu
            // satu-satunya bedanya, jadi harus KELIHATAN lebih besar.
            ("greaterfireball", Cfxr + "Fire/CFXR3 Fireball B + Fire Trail.prefab",  1.5f, 1f),
            ("frostshard",      Cfxr + "Ice/CFXR3 Iceball A + Ice Trail.prefab",     1f, 1f),
            ("glacialspike",    Cfxr + "Ice/CFXR3 Iceball B + Ice Trail.prefab",     1f, 1f),
            // Trail Vefects dicabut (2026-08-12): 0 partikel, cuma garis saat bergerak —
            // "spark bolt jelek banget". Lightball = bola listrik ber-ekor yang terlihat.
            ("sparkbolt",       Cfxr + "Light/CFXR3 Lightball A + Trail.prefab",     1f, 1f),

            // ---------- NOVA ----------
            ("emberburst",  Cfxr + "Explosions/CFXR3 Fire Explosion A.prefab",           1f, 1f),
            ("frostnova",   Cfxr + "Ice/CFXR3 Hit Ice A (Ground).prefab",                1f, 1f),
            ("staticfield", Cfxr + "Electric/CFXR Electric Explosion.prefab",            1f, 1f),
            ("steamburst",  Ga + "vfx_ImpactAoE03_Water.prefab",                         0.8f, 1f),
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
            // Bara Pantul — dulu Line api kedua ("flame lash masih aneh, udah kebanyakan
            // skill kaya gini", 2026-08-12), sekarang RICOCHET api: mengisi anak tangga
            // b1 yang kosong (es b2, listrik b4, arcane b5). VFX mengikuti pola
            // keluarganya: ImpactAoE elemennya sendiri, disembur di titik pantul.
            ("flamelash",    Ga + "vfx_ImpactAoE01_Fire.prefab",                        0.45f, 1f),
            ("icelance",     Cfxr + "Ice/CFXR3 Hit Ice B (Air).prefab",                     1f, 1f),
            ("sabetanpetir", Hovl + "Slash effects/Electro slash.prefab",                   0.9f, 1f),
            ("infernowave",  Cfxr + "Fire/CFXR4 Flamethrower + Smoke.prefab",               1f, 1f),
            ("voidlance",    Cfxr + "Explosions/CFXR4 Monster Explosion Purple (Small).prefab", 0.9f, 1f),
            ("ragnarok",     Cfxr + "Fire/CFXR Fire Breath.prefab",                         1.2f, 1f),

            // ---------- ZONE ----------
            ("cinderpatch",    Cfxr + "Fire/CFXR4 Burning Fire.prefab",        1f, 1f),
            ("stormcell",      Cfxr + "Electric/CFXR Electric Surface.prefab", 1f, 1f),
            ("kubanganracun",  LanaPack + "MagicField/MagicField_Poison Variant.prefab", 1f, 1f),
            ("frostbitefield", Cfxr + "Nature/CFXR4 Snow 'Splashes'.prefab",   1f, 1f),
            ("badaisalju",     Cfxr + "Nature/CFXR4 Snow Falling.prefab",      1f, 1f),
            ("plaguebloom",    Cfxr + "Misc/CFXR4 Flies Cloud.prefab",         1f, 1f),
            ("singularity",    Cfxr + "Misc/CFXR Portal.prefab",               1f, 1f),
            // Rockfall aslinya memenuhi layar — "ashfall terlalu besar, areanya kecil"
            // (2026-08-10). Dikecilkan DI DALAM wrapper supaya terlihat saat prefabnya dibuka.
            ("ashfall",        Lana + "Rockfall.prefab",                       1f, 0.22f),
            // Electric Surface bekas stormcell (kini item pasif) — MagicField_Stun tampak
            // belum jadi ("kotak2"). 0.5: prefabnya sendiri lebar, di skala 1 dua kali
            // lipat cakram penandanya ("terlalu gede", 2026-08-12).
            ("ionstorm",       Cfxr + "Electric/CFXR Electric Surface.prefab",  0.5f, 1f),

            // ---------- CLEANSE ----------
            ("cahayapembersih", Cfxr + "Light/CFXR3 Hit Light A (Air).prefab",    1f, 1f),
            ("fajarpembersih",  Cfxr + "Light/CFXR3 Hit Light Fireworks.prefab",  1f, 1f),

            // ---------- RADIAL ----------
            ("belatiberputar", Cfxr + "Light/CFXR3 Lightball B + Trail.prefab", 0.8f, 1f),
            ("absolutezero",   Cfxr + "Ice/CFXR3 Iceball A + Ice Trail.prefab", 0.9f, 1f),

            // ---------- DETONATE ----------
            ("sunder",    Cfxr + "Impacts/CFXR2 Hit (Contrast).prefab",     1f, 1f),
            ("rupture",   Cfxr + "Explosions/CFXR4 Explosion Quick.prefab", 1f, 1f),
            ("reckoning", Cfxr + "Fire/CFXR3 Hit Fire C (Air).prefab",      1f, 1f),

            // ---------- ORBIT — orb petir yang sama dengan Storm Circle, satu bahasa ----------
            ("sparkshards", LanaPack + "Orb/Orb_lightning.prefab", 0.7f, 1f),
            ("stormshards", LanaPack + "Orb/Orb_lightning.prefab", 0.8f, 1f),

            // ---------- BLINK ----------
            ("blinkstep", Cfxr + "Misc/CFXR Magic Poof.prefab",                              1f, 1f),
            ("voidstep",  Cfxr + "Space/CFXR4 Teleporter Rings Upwards (HDR, Purple).prefab", 1f, 1f),

            // ---------- WARD ----------
            ("wardpetty", Hovl + "Magic shields/Magic shield blue.prefab",   0.9f, 1f),
            ("wardaegis", Hovl + "Magic shields/Magic shield yellow.prefab", 1f,   1f),
            ("bulwark",   Hovl + "Magic shields/Magic shield pink.prefab",   1.1f, 1f),

            // ---------- SURGE / RESTORE ----------
            ("quickfoot",      Ga + "vfx_BuffAoE08_Speed.prefab",  0.7f, 1f),
            ("quickcastsigil", Ga + "vfx_BuffAoE07_Arcane.prefab", 0.7f, 1f),
            ("manawell",       Ga + "vfx_BuffAoE03_Water.prefab",  0.7f, 1f),

            // ---------- SUNSTRIKE ----------
            ("sunlance",   Ga + "vfx_DebuffVertical01_Fire.prefab",     0.9f, 1f),
            ("sunstrike",  Ga + "vfx_ImpactAoE01_Fire.prefab",          0.9f, 1f),
            ("solarflare", Ga + "vfx_SingleComet01_Fire Variant.prefab", 0.9f, 1f),

            // ---------- ROLLING BALL ----------
            // Api unggun menggelinding, bukan komet — "terlalu sama kaya fireball".
            ("emberroll",   Cfxr + "Fire/CFXR4 Burning Fire.prefab",        1f, 1f),
            ("chaosmeteor", Cfxr + "Fire/CFXR2 Fireball.prefab",             1.2f, 1f),

            // ---------- VORTEX ----------
            // Tangga vortex: asap -> pasir -> salju. Tornado Lana KEMBALI atas keputusan
            // pemilik project — "kejujuran elemen" kalah dari tornado yang memang cakep.
            ("whirlwind", Hovl + "Smoke effects/Smoke vortex.prefab",             0.9f,  1f),
            ("tornado",   LanaPack + "Wind_Leaves_Tornado/Tornado_sand.prefab",   1f,    1f),
            ("maelstrom", LanaPack + "Wind_Leaves_Tornado/Tornado_snow.prefab",   1.15f, 1f),

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

            // ---------- ORBITAL: wrapper dipasang SATU PER BILAH (EnsureBlades), jadi harus
            //            kecil, loop, dan diam di tempat — kode yang menggerakkannya ----------
            ("bladedance",  Cfxr + "Light/CFXR3 Lightball B + Trail.prefab",  0.85f, 1f),
            ("stormcircle", LanaPack + "Orb/Orb_lightning.prefab",            0.55f, 1f),
            ("ringofruin",  Cfxr + "Fire/CFXR Fireball + Fire Trail.prefab",  0.75f, 1f),

            // ---------- BOOMERANG: badan yang terbang ----------
            //
            // WAJIB efek yang DIAM di tempatnya sendiri. Komet — yang dipakai di sini sebelumnya —
            // punya gerak jatuh bawaan; dipasang ke badan yang sudah digeret kode, dua gerakan itu
            // bertabrakan dan bumerangnya terlihat menukik ke tanah sambil melayang menyamping.
            // Orb hanya berputar di porosnya, jadi seluruh perpindahannya murni milik kode.
            ("chakram",    LanaPack + "Orb/Orb_lightning.prefab", 0.6f,  1f),
            ("moonglaive", LanaPack + "Orb/Orb_snow.prefab",      0.75f, 1f),

            // ---------- RICOCHET: disembur di titik pantul, bukan di sepanjang garis ----------
            ("prismray",    Ga + "vfx_ImpactAoE02_Ice.prefab",         0.45f, 1f),
            ("mirrorlance", Ga + "vfx_ImpactAoE05_Electricity.prefab", 0.5f,  1f),
            ("runescrawl",  Ga + "vfx_ImpactAoE07_Arcane.prefab",      0.55f, 1f),

            // ---------- TURRET: berdiri di tanah selama beberapa detik, jadi harus LOOP ----------
            ("sentryeye", Hovl + "Magic circles/Magic circle.prefab",       0.8f, 1f),
            ("obelisk",   Hovl + "AoE effects/Red energy explosion.prefab", 0.8f, 1f),

            // ---------- SHOCKWAVE: cincin yang melebar ----------
            ("ripple", Ga + "vfx_ImpactAoE08_Speed.prefab", 0.8f, 1f),
            ("quake",  Ga + "vfx_ImpactAoE01_Fire.prefab",  0.9f, 1f),

            // ---------- SEEKER: satu efek per rudal, jadi harus KECIL ----------
            //
            // Cacat yang sama dengan bumerang: rudal juga dikemudikan kode, jadi komet berjatuhan
            // di sini pun salah. Ditemukan saat memperbaiki bumerang, bukan dilaporkan terpisah.
            ("hexbolts", LanaPack + "Orb/Orb_lightning.prefab", 0.45f, 1f),
            ("hexstorm", LanaPack + "Orb/Orb_lightning.prefab", 0.55f, 1f),

            // ---------- TETHER: disembur di ujung sinar tiap denyut, jadi harus kecil & cepat ----------
            ("siphonbeam", Ga + "vfx_ImpactAoE09_Heal.prefab",  0.4f, 1f),
            ("soulchain",  Ga + "vfx_ImpactAoE04_Poison.prefab", 0.5f, 1f),

            // ---------- BARRAGE: hujan berurutan, satu semburan per hantaman ----------
            ("starfall",  Ga + "vfx_MeteorRain02_Ice.prefab",  0.5f, 1f),
            ("judgement", Ga + "vfx_ArrowRain01_Fire.prefab",  0.6f, 1f),
        };

        /// <summary>
        /// Sumber yang DIGANTI karena tidak nyambung dengan skillnya, bukan karena jelek.
        /// Batch yang sudah diterapkan tidak disimpan di sini — riwayatnya ada di git; array ini
        /// selalu berisi batch KOREKSI BERIKUTNYA yang belum/baru saja dijalankan.
        ///
        /// Batch 2026-08-12 — audit loop-vs-sekali-main atas seluruh 92 skill. Temuannya satu
        /// pola: skill yang HIDUP LAMA (zone, vortex, turret, orbital, ward) menggendong efek
        /// sekali-main, jadi efeknya mati duluan dan sisa hidup skillnya bisu. Arah sebaliknya
        /// aman — pool memotong efek loop di skill sekali-main pada 0,85 detik.
        ///
        /// Pilihan penggantinya dari dua paket yang baru masuk (Hovl = momen besar bergaya,
        /// Lana MagicField = field status ber-elemen yang loop 20 detik), dua-duanya sudah
        /// diverifikasi loop lewat inspeksi ParticleSystem, bukan dari namanya.
        /// </summary>
        static readonly (string piece, string path, float scale)[] Corrections =
        {
            // Batch 2026-08-12b — hasil QA pemilik project atas batch sebelumnya.
            //
            // ---------- LINE: semburan dari badan pemain ke sasaran ----------
            // "cariin vfx yg nembakin laser atau yg nyembur api dari player ke target".
            // Titik lahir VFX garis sudah dipindah ke badan pemain (PlayerCaster), jadi
            // prefab semburan yang memanjang ke depan sekarang benar-benar keluar dari tangan.
            // flamelash sudah berubah kelamin jadi Ricochet ("Bara Pantul") — wrapper
            // lamanya bernama folder "Flame Lash" dan dihapus saat batch ini jalan.
            ("flamelash",    Ga + "vfx_ImpactAoE01_Fire.prefab",              0.45f),
            ("sabetanpetir", Hovl + "Slash effects/Electro slash.prefab",     0.9f),

            // ---------- LISTRIK: Vefects trail (0 partikel) diganti benda sungguhan ----------
            // Trail hanya menggambar saat digerakkan; sebagai peluru ia garis tipis nyaris
            // tak terlihat. Lightball = bola listrik dengan ekor; pecahan orbit memakai
            // orb petir yang sama dengan Storm Circle — satu bahasa untuk keluarga listrik.
            ("sparkbolt",   Cfxr + "Light/CFXR3 Lightball A + Trail.prefab", 1f),
            ("sparkshards", LanaPack + "Orb/Orb_lightning.prefab",           0.7f),
            ("stormshards", LanaPack + "Orb/Orb_lightning.prefab",           0.8f),

            // ---------- API: tiap anggota keluarga harus beda silau ----------
            // Greater fireball cuma terasa lebih besar kalau MEMANG lebih besar; rolling
            // ember bukan peluru, jadi bentuknya api unggun menggelinding, bukan komet.
            ("greaterfireball", Cfxr + "Fire/CFXR3 Fireball B + Fire Trail.prefab", 1.5f),
            ("emberroll",       Cfxr + "Fire/CFXR4 Burning Fire.prefab",            1f),

            // ---------- ZONE listrik: MagicField_Stun tampak belum jadi ("kotak2") ----------
            // Electric Surface bekas stormcell (sekarang item pasif, slotnya bebas).
            ("ionstorm", Cfxr + "Electric/CFXR Electric Surface.prefab", 0.5f),

            // ---------- VORTEX: keputusan pemilik project — tornado Lana KEMBALI ----------
            // Batch kemarin menggantinya demi "kejujuran elemen" (pasir untuk skill bukan
            // pasir), dan itu keliru arah: yang penting tornadonya CAKEP dan terbaca
            // berputar. Sand untuk tornado, snow untuk maelstrom, dan whirlwind ambil
            // pusaran asap supaya tangga vortex-nya kebaca: asap -> pasir -> salju.
            ("whirlwind", Hovl + "Smoke effects/Smoke vortex.prefab",                 0.9f),
            ("tornado",   LanaPack + "Wind_Leaves_Tornado/Tornado_sand.prefab",       1f),
            ("maelstrom", LanaPack + "Wind_Leaves_Tornado/Tornado_snow.prefab",       1.15f),
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

        /// <summary>
        /// HIT VFX per skill: efek di titik musuh kena, wrapper <c>HitVfx_&lt;nama&gt;</c> di folder
        /// skill yang sama dengan wrapper cast-nya, kontrak yang sama — wrapper yang sudah ada
        /// TIDAK dibangun ulang, pemilik project bebas menukar isinya lewat prefab.
        ///
        /// Elemen tiap skill DISIMPULKAN dari path prefab cast-nya di <see cref="Map"/> — tabel
        /// itu sudah dikurasi tangan per keluarga elemen, jadi dialah sumber kebenaran termurah.
        /// Kandidat prefab per keluarga diperiksa keberadaannya dulu; varian dirotasi supaya
        /// dua skill sekeluarga sebisanya tidak kembar persis.
        /// </summary>
        [MenuItem("Tools/Grimoire/Assign Skill HIT VFX")]
        public static void RunHit()
        {
            var families = new (string key, string[] candidates, float scale)[]
            {
                ("fire", new[] {
                    Cfxr + "Fire/CFXR3 Hit Fire A (Air).prefab",
                    Cfxr + "Fire/CFXR3 Hit Fire B (Air).prefab",
                    Cfxr + "Fire/CFXR3 Hit Fire C (Air).prefab" }, 0.6f),
                ("ice", new[] {
                    Cfxr + "Ice/CFXR3 Hit Ice A (Air).prefab",
                    Cfxr + "Ice/CFXR3 Hit Ice B (Air).prefab" }, 0.6f),
                ("electric", new[] {
                    Cfxr + "Electric/CFXR3 Hit Electric A (Air).prefab",
                    Cfxr + "Electric/CFXR3 Hit Electric B (Air).prefab",
                    Cfxr + "Electric/CFXR3 Hit Electric C (Air).prefab" }, 0.6f),
                ("light", new[] {
                    Cfxr + "Light/CFXR3 Hit Light A (Air).prefab",
                    Cfxr + "Light/CFXR3 Hit Light Fireworks.prefab" }, 0.6f),
                ("poison", new[] { Ga + "vfx_ImpactAoE04_Poison.prefab" }, 0.35f),
                ("void", new[] {
                    Ga + "vfx_ImpactAoE06_Void.prefab",
                    Cfxr + "Explosions/CFXR4 Monster Explosion Purple (Small).prefab" }, 0.4f),
                ("arcane", new[] { Ga + "vfx_ImpactAoE07_Arcane.prefab" }, 0.4f),
                ("water", new[] { Ga + "vfx_ImpactAoE03_Water.prefab" }, 0.35f),
                ("plain", new[] { Cfxr + "Impacts/CFXR2 Hit (Contrast).prefab" }, 0.7f),
            };

            // Saring kandidat yang beneran ada — nama file paket bukan kontrak.
            var live = new Dictionary<string, (List<string> paths, float scale)>();
            foreach (var (key, candidates, scale) in families)
            {
                var ok = new List<string>();
                foreach (var c in candidates)
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(c) != null) ok.Add(c);
                if (ok.Count == 0) ok.Add(Cfxr + "Impacts/CFXR2 Hit (Contrast).prefab");
                live[key] = (ok, scale);
            }

            System.Func<string, string> familyOf = castPath =>
            {
                string p = castPath.ToLowerInvariant();
                if (p.Contains("heal") || p.Contains("buff")) return null;  // tidak memukul musuh
                if (p.Contains("fire") || p.Contains("flame") || p.Contains("burning") ||
                    p.Contains("comet") || p.Contains("explosion orange")) return "fire";
                if (p.Contains("ice") || p.Contains("snow") || p.Contains("frost")) return "ice";
                if (p.Contains("electr") || p.Contains("lightning") || p.Contains("lightball") ||
                    p.Contains("sparks") || p.Contains("orb_lightning")) return "electric";
                if (p.Contains("poison") || p.Contains("flies")) return "poison";
                if (p.Contains("void") || p.Contains("purple") || p.Contains("portal")) return "void";
                if (p.Contains("arcane")) return "arcane";
                if (p.Contains("water") || p.Contains("steam")) return "water";
                if (p.Contains("/light/") || p.Contains("hit light")) return "light";
                return "plain";
            };

            int built = 0, repointed = 0, kept = 0, skipped = 0;
            var problems = new List<string>();
            var rotation = new Dictionary<string, int>();

            foreach (var (piece, path, _, _) in Map)
            {
                var def = AssetDatabase.LoadAssetAtPath<PieceDefinition>(
                    $"{PieceFolder}/Piece_{piece}.asset");
                if (def == null) { problems.Add($"piece hilang: {piece}"); continue; }
                if (def.IsPassive || def.IsRune) { skipped++; continue; }

                string family = familyOf(path);
                if (family == null) { skipped++; continue; }

                var (paths, scale) = live[family];
                if (!rotation.ContainsKey(family)) rotation[family] = 0;
                string chosen = paths[rotation[family] % paths.Count];
                rotation[family]++;

                string folder = Sanitize(def.DisplayName);
                string wrapperPath = $"{SkillRoot}/{folder}/HitVfx_{folder}.prefab";

                var wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(wrapperPath);
                if (wrapper == null)
                {
                    wrapper = BuildWrapper(wrapperPath, folder, chosen, 1f, problems);
                    if (wrapper == null) continue;
                    built++;
                }

                if (def.HitVfx == wrapper && Mathf.Approximately(def.HitVfxScale, scale))
                {
                    kept++;
                    continue;
                }

                def.HitVfx = wrapper;
                def.HitVfxScale = scale;
                EditorUtility.SetDirty(def);
                repointed++;
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"[VfxPass] HIT: wrapper dibangun {built}, pointer dibetulkan {repointed}, " +
                      $"sudah benar {kept}, dilewati {skipped}, masalah {problems.Count}." +
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

            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(wrapperPath));
            try
            {
                var child = (GameObject)PrefabUtility.InstantiatePrefab(pack);

                // Script hilang dibuang SEBELUM disimpan. Beberapa prefab paket (varian
                // MagicField Lana, contohnya) membawa MonoBehaviour dari project asalnya yang
                // tidak pernah ikut — GUID-nya bahkan tidak ada di project_b. Unity MENOLAK
                // menyimpan prefab yang memuat script hilang, jadi tanpa ini wrappernya gagal
                // dibangun; partikelnya sendiri tidak butuh script itu.
                //
                // Harganya: instance harus di-unpack (komponen milik prefab tertutup rapat),
                // dan wrapper kehilangan tautan nested ke prefab paketnya. Karena itu HANYA
                // dilakukan pada prefab yang memang membawa script hilang.
                bool missing = false;
                foreach (var t in child.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                    {
                        missing = true;
                        break;
                    }
                }

                if (missing)
                {
                    PrefabUtility.UnpackPrefabInstance(child, PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                    foreach (var t in child.GetComponentsInChildren<Transform>(true))
                    {
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                    }
                }

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
