using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Proto.EditorTools
{
    /// <summary>
    /// Membuat SATU biome hutan dan menyambungkannya ke `_Bootstrap` di scene game.
    ///
    /// Satu, bukan empat. Empat wajah arena yang bergantian terdengar seperti kedalaman, dan yang
    /// dihasilkan justru sebaliknya: tidak ada satu pun yang sempat dikenali, dan tidak ada satu
    /// pun yang bisa dipoles sampai benar-benar bagus. Satu tempat yang digarap sampai selesai
    /// mengalahkan empat tempat yang setengah jadi.
    ///
    /// Idempoten — dicocokkan lewat path aset.
    /// </summary>
    public static class BiomePass
    {
        const string Root = "Assets/GameData";
        const string Folder = Root + "/Biomes";
        const string ScenePath = "Assets/Scenes/Proto.unity";

        // Paket low-poly: satu tekstur palet warna dipakai bersama oleh batu, jamur, kayu, dan
        // batang pohon, jadi seluruhnya rata dan tanpa detail — kebalikan dari aset bertekstur
        // rapat yang terbaca "HD" di kamera sedekat ini.
        const string Nature = "Assets/Plugin/InnerverseInteractive/Ultimate Nature – Starter/";
        const string Env = Nature + "Environment/";
        const string Fir = Env + "Trees/Fir/Prefabs/";
        const string Bushes = Env + "Vegetation/Bushes/Prefabs/";
        const string Shrooms = Env + "Vegetation/Mushrooms/Prefabs/";
        const string Boulders = Env + "Rocks/Forest/Prefabs/";
        const string Pebbles = Env + "Rocks/River/Prefabs/";
        const string Logs = Env + "Props/Logs/Prefabs/";
        const string Branches = Env + "Props/Branches/Prefabs/";
        const string Sky = Env + "Sky/Materials/";

        /// <summary>Memuat aset dan MELAPORKAN kalau tidak ada — slot kosong di sini berarti lantai
        /// atau langit yang diam-diam kembali ke tampilan lama, tanpa satu pun tanda.</summary>
        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError("[BiomePass] " + typeof(T).Name + " tidak ketemu: " + path);
            return asset;
        }

        [MenuItem("Tools/Grimoire/Generate Biomes")]
        public static void Run()
        {
            // Dijaga di DEPAN, bukan dibiarkan meletus di tengah. OpenScene melempar di play mode,
            // dan saat itu aset biome-nya sudah terlanjur ditulis — jadi asetnya baru sementara
            // scene-nya masih menunjuk yang lama, dan tidak ada tanda apa pun bahwa keduanya
            // sekarang tidak cocok.
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[BiomePass] Tidak bisa jalan di play mode. Stop dulu, baru ulangi.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Root, "Biomes");

            EnsureFireflies();
            EnsureGodRay();

            string path = Folder + "/Biome_forest.asset";
            var forest = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(path);

            if (forest == null)
            {
                forest = ScriptableObject.CreateInstance<BiomeDefinition>();
                AssetDatabase.CreateAsset(forest, path);
            }

            forest.Id = "forest";
            forest.DisplayName = "Verdant Hollow";

            // ================================================================================
            //  Cahaya disalin dari Demo_Scene_Day.unity milik paket asetnya.
            // ================================================================================
            //
            // Percobaan sebelumnya membangun cahaya sendiri — matahari hangat 2,4, ambient redup,
            // kabut nyaris hitam mulai di 21 unit — lalu menaruh aset ToonScapes di dalamnya, dan
            // hasilnya dedaunan yang terbaca sebagai coretan gelap. Itu bukan kebetulan: aset
            // bergaya toon dicat DI BAWAH rig cahaya tertentu, dan warnanya sudah termasuk
            // bayangannya. Menyinarinya dengan rig lain berarti membayar bayangan dua kali.
            //
            // Jadi rig-nya diambil apa adanya dari demo, bukan ditafsir ulang.
            forest.GroundColor = new Color(0.5f, 0.58f, 0.42f);   // cadangan; terrain menimpanya
            forest.HorizonColor = new Color(0.32f, 0.62f, 0.86f);

            // MATAHARI HANGAT, BAYANGAN BIRU. Ini seluruh rahasia "cozy"-nya, dan ini juga yang
            // membedakannya dari percobaan sebelumnya.
            //
            // Rig ToonScapes menyinari semuanya dengan putih dari matahari DAN putih dari ambient.
            // Hasilnya benar untuk demo mereka, dan di lapangan sewarna hasilnya adalah hijau tanpa
            // suhu — tidak ada bagian yang lebih hangat, tidak ada yang lebih dingin, jadi tidak
            // ada yang terasa seperti sore. Rig low-poly ini memakai matahari kuning-jingga dan
            // ambient BIRU, jadi tiap benda punya sisi hangat dan sisi dingin sekaligus.
            forest.SunColor = new Color(1f, 0.9f, 0.68f);
            forest.SunPitch = 35f;
            forest.SunYaw = 147f;

            // Diturunkan dari 2,8. Ini lantai HUTAN: cahaya yang sampai ke bawah sudah disaring
            // tajuk, dan lantai hutan yang seterang padang terbuka adalah hal pertama yang
            // membocorkan bahwa tajuknya tidak ada.
            //
            // Penggelapan dikerjakan di SINI, bukan di post-exposure. Post-exposure −0,65 sudah
            // dicoba dan nyaris tidak terlihat, karena tonemap ACES memampatkan ujung terangnya —
            // yang dikurangi cuma sisa jangkauan yang memang sudah tidak terpakai.
            //
            // 2,4 — look siang yang lama, yang dipilih pemilik project setelah membandingkan
            // langsung dengan grade demo. Grade demo tidak dibuang: ia jadi wajah SENJA.
            forest.SunIntensity = 2.4f;

            // Ambient BIRU LANGIT, bukan putih. Di gaya ilustrasi, bagian yang tidak kena matahari
            // tidak boleh gelap — ia harus tetap BERWARNA, dan warnanya harus warna langit.
            forest.AmbientSky = new Color(0.42f, 0.55f, 0.68f);
            forest.AmbientEquator = new Color(0.33f, 0.45f, 0.54f);
            forest.AmbientGround = new Color(0.2f, 0.25f, 0.28f);

            // Kabut BIRU dan JAUH. Yang lama mulai di 21 unit dan berwarna hampir hitam, jadi
            // seluruh separuh atas layar meluruh jadi gelap — itulah "jelek"-nya. Demo memulai
            // kabutnya di 80, dan kamera ini tidak pernah melihat sejauh itu; jadi kabut berhenti
            // membingkai dan kembali jadi apa yang seharusnya, yaitu udara di kejauhan.
            forest.FogEnabled = true;
            forest.FogColor = new Color(0.825f, 1f, 0.994f);
            forest.FogStart = 60f;
            forest.FogEnd = 220f;

            // Lantai terrain, DATAR. Demo-nya berbukit; punya kita tidak boleh, karena kamera
            // ortografis yang menunduk membuat tiap punggung bukit jadi tempat musuh menghilang.
            //
            // Material dikosongkan: terrain memakai shader bawaan URP. Shader terrain ToonScapes
            // menempelkan rumput bertekstur rapat di atas lantai, dan detail sepadat itu di kamera
            // sedekat ini adalah persis yang terbaca sebagai "terlalu HD" di sebelah aset low-poly.
            // Lantai cadangan ini hanya dipakai kalau TIDAK ada Terrain di scene. Begitu
            // Tools/Grimoire/Create Editable Terrain dijalankan, seluruh blok ini tidak terpakai —
            // lantainya milik yang menyusunnya dengan tangan, termasuk lapisannya.
            forest.GroundMaterial = null;
            forest.GroundLayers = System.Array.Empty<TerrainLayer>();

            // Arena 80x60 plus margin kelahiran musuh.
            forest.GroundSize = new Vector2(140f, 120f);
            forest.GroundPatchSize = 26f;
            forest.GroundPatchCoverage = 0.28f;

            forest.Skybox = Load<Material>(Sky + "UNS_Skybox_Panoramic.mat");

            // BAYANGAN AWAN, dan kali ini benar-benar terlihat.
            //
            // Ini yang dikerjakan lapangan hutan: bukan diredupkan merata, tapi dilewati bercak
            // teduh yang bergerak. Satu bidang besar yang mengikuti kamera dengan UV dikunci ke
            // koordinat DUNIA — jadi bidangnya ikut kamera, polanya tidak. Tanpa penguncian itu
            // bayangannya menempel di layar dan terbaca sebagai lensa kotor.
            //
            // Alpha dinaikkan dari 0,16 ke 0,34: di 0,16 ia ada di kode tapi praktis tidak ada di
            // layar. Warnanya biru, bukan hitam — bayangan di lapangan terang harus tetap berwarna.
            // DIPEKATKAN. Alpha mentok di 1 — di atas itu tidak ada lagi yang bisa dinaikkan, jadi
            // sisa ketebalannya diambil dari dua tempat lain: warnanya dibuat jauh lebih gelap
            // (jarak antara terteduh dan tersorot yang sebenarnya menentukan "tebal"), dan
            // cakupannya dinaikkan supaya bagian lapangan yang teduh lebih luas daripada yang terang.
            // Awan TEBAL tapi cakupannya SEDIKIT, dan keduanya harus jalan bersama. Awan pekat yang
            // menutupi dua pertiga lapangan bukan "bayangan awan" lagi, itu mendung — dan lapangan
            // yang seluruhnya teduh kehilangan justru hal yang dibeli bayangan: bedanya dengan
            // bagian yang tersorot.
            // Gumpalannya harus LEBIH KECIL dari layar, dan ini yang paling sering salah.
            //
            // Di 34 unit, satu gumpalan sama besar dengan seluruh lapangan yang terlihat (kamera
            // memperlihatkan ~40 unit) — jadi yang terjadi bukan awan yang lewat, melainkan seluruh
            // layar meredup lalu terang lagi. Di 16 unit ada dua sampai tiga gumpalan sekaligus,
            // dan barulah geraknya punya sesuatu untuk dibandingkan: bagian teduh bergerak melewati
            // bagian yang tersorot.
            forest.CloudColor = new Color(0.05f, 0.07f, 0.13f, 1f);
            forest.CloudSize = 16f;
            forest.CloudCoverage = 0.42f;

            // DUA BELAS unit per detik. Gumpalannya selebar 34 unit, jadi ia bergeser selebar
            // dirinya sendiri dalam kurang dari tiga detik — dan itu ambang di mana mata berhenti
            // menyebutnya diam.
            //
            // Terukur di kamera probe yang dipaku diam dan hanya menggambar lapisan awan:
            // pada 2 unit/detik hanya 69% piksel berubah dalam 15 detik; pada 12 unit/detik
            // 96% berubah dengan selisih rata-rata empat kali lipat.
            forest.CloudSpeed = 7f;

            forest.RayColor = new Color(1f, 0.97f, 0.82f, 0.16f);
            forest.RaySize = 34f;
            forest.RayCoverage = 0.4f;

            // Lampu pengembara DIMATIKAN. Gunanya dulu memberi lantai gelap daerah terang; lantai
            // sekarang sudah terang merata, jadi yang tersisa cuma kolam cahaya yang mengembara
            // tanpa sumber yang terlihat.
            forest.LampCount = 0;
            forest.PlayerLightIntensity = 0f;

            // Kegelapan jarak: MEMBUKA saat pemain mendekat, MENUTUP lagi saat ditinggal.
            forest.GloomLayers = 1;
            forest.GloomColor = new Color(0.012f, 0.02f, 0.045f, 1f);
            forest.GloomStart = 15f;
            forest.GloomEnd = 27f;
            forest.GloomCeiling = 0.92f;

            // Garis batasnya berkelok sejauh SEBELAS unit — hampir separuh jarak antara batas dalam
            // dan batas luarnya. Di angka kecil ia tetap lingkaran yang cuma bergerigi; di sebelas
            // ia benar-benar menjorok masuk dan keluar, dan di situlah ia berhenti terbaca sebagai
            // vignette.
            forest.GloomWobble = 11f;
            forest.GloomSize = 11f;
            forest.GloomChurn = 0.11f;
            forest.GloomDrift = 0.5f;

            // Titiknya JARANG, bukan rapat. Raster serapat sebelumnya mengubah seluruh kegelapan
            // jadi kisi; yang diminta cuma bekas cetakan yang terlihat di tepinya.
            forest.GloomDotSize = 16f;
            forest.GloomSmooth = 0.6f;

            // Kantong asap volumetrik DICOPOT. Bolanya menghasilkan lingkaran yang terlalu rapi
            // tepat di tengah layar — persis hal yang sedang dihindari.
            forest.SmokePocket = null;

            // REM MENDUNG. Cuaca cuma menentukan seberapa mendung (0–1); seberapa gelap
            // "mendung penuh" itu boleh, ditentukan di sini — dan disetel dari Inspector,
            // bukan dari kode.
            //
            // Diturunkan dari 0,85 — di angka itu hujan lebat masih terbaca "agak kurang mendung".
            // 0,72 menggelapkan tanpa menelan siluet musuh; di bawah 0,6 lapangannya jadi senja.
            forest.OvercastSun = 0.72f;
            forest.OvercastAmbient = 1.3f;
            forest.OvercastCloud = 0.8f;

            // Lampu pemain menyala begitu hujan turun.
            forest.RainPlayerLight = 3.5f;

            forest.GloomMaterial = LookMaterial("Gloom", "Grimoire/Gloom");

            // Suasana tempatnya — dan TIDAK semuanya dipakai sekaligus.
            //
            // Tiap entri punya peluangnya sendiri, diundi sekali per sesi. Daftar yang seluruhnya
            // menyala bersamaan membuat tiap run terlihat sama persis, dan tempatnya berhenti punya
            // suasana; yang tersisa cuma daftar isian yang penuh.
            // ================================================================================
            //  MULAI DARI SINI: MILIK PENYETEL, BUKAN MILIK PASS.
            // ================================================================================
            //
            // Diisi SEKALI saat masih kosong, lalu tidak pernah disentuh lagi. Semua angka VFX
            // suasana dan cuaca boleh disetel langsung di Inspector `Biome_forest.asset`, dan
            // menjalankan ulang pass ini tidak akan menghapusnya.
            //
            // Ini bukan kenyamanan, ini syarat. Menyetel partikel menuntut belasan putaran
            // lihat-ubah-lihat, dan pass yang menimpa hasilnya tiap kali dijalankan membuat
            // penyetelan itu mustahil dikerjakan oleh siapa pun selain yang memegang kodenya.
            //
            // Mau kembali ke bawaan? Kosongkan saja daftarnya di Inspector, lalu jalankan lagi.
            bool freshVfx = forest.AmbientVfx == null || forest.AmbientVfx.Length == 0;

            if (freshVfx) forest.AmbientVfx = new[]
            {
                // Kupu-kupu DISEBAR, bukan menempel di pemain — beberapa kantong terpisah, tak
                // satu pun lebih dekat dari 7 unit. Dan CUMA di cuaca cerah: makhluk paling rapuh
                // di lapangan adalah yang pertama berteduh begitu ada apa pun turun dari langit.
                Ambient("Butterflies/Butterflies_ellow", 0.75f, spread: 4, near: 9f, far: 24f,
                    scale: 0.35f, height: 1.2f, hideInRain: true, onlyClear: true),

                Ambient("Butterflies/Butterflies_white", 0.45f, spread: 3, near: 9f, far: 26f,
                    scale: 0.32f, height: 1.4f, hideInRain: true, onlyClear: true),

                // God ray = BEAM MILIK PAKETNYA, dirender APA ADANYA — bentuk, tinggi, dan
                // warnanya DISETEL LANGSUNG DI PREFAB oleh pemilik project. Kode cuma mengurus
                // PENEMPATAN: TIGA titik acak di area, tidak pernah lebih dekat dari 12 unit —
                // "cahayanya dari matahari, jangan di atas kepala player". Lapisan ikut-kamera
                // dari prefab yang sama sudah DIBUANG: dialah yang dulu nangkring di pusat layar.
                // JANGAN tambahkan stretch/tint/follow di sini — itu menimpa setelan tangan.
                // BUKAN OnlyClear lagi: cerah cuma 10% sekarang — god ray yang cuma hidup di
                // situ praktis tidak pernah terlihat. Ia tampil juga saat berangin, tetap
                // ngumpet saat hujan.
                Ambient("Light/Sunlight", 1f, spread: 3, near: 12f, far: 30f,
                    scale: 1f, height: 0f, hideInRain: true),

                // Daun berguguran hampir SELALU ada — dan "hampir" itu yang penting. Sesuatu yang
                // tidak pernah absen berhenti diperhatikan.
                //
                // Dikecilkan ke 0,4 dan DILAHIRKAN 16 UNIT DI ATAS. Kamera duduk 18,5 unit di atas
                // dan menunduk: daun yang lahir setinggi mata tidak pernah terlihat jatuh, ia cuma
                // muncul lalu mendarat. Lahir jauh di atas berarti ia sudah melayang turun sebelum
                // masuk pandangan — dan karena melintas lebih dekat ke kamera lebih dulu, ia
                // terlihat besar dulu lalu mengecil saat turun.
                // Daun MENEMPEL DI KAMERA, bukan ditaruh sebagai kantong. Daun berguguran
                // seharusnya ada di mana-mana dan seragam, jadi menghitungnya di luar layar cuma
                // membayar partikel yang tidak pernah dilihat siapa pun.
                // Ditipiskan dari 0,8 — daun yang selalu ada mengaburkan wave BERANGIN, dan
                // berangin sekarang justru dibaca dari daunnya.
                Ambient("Wind_Leaves_Tornado/Leaves_green", 0.35f,
                    scale: 0.1f, height: 15f, follow: true, offX: -22f),

                Ambient("Wind_Leaves_Tornado/LeavesSpin_orange", 0.15f,
                    scale: 0.09f, height: 14f, follow: true, offX: -26f),

                // Kawanan burung LEWAT, tidak menetap. Burung yang terus-menerus berputar di atas
                // kepala berhenti jadi kejadian dan berubah jadi hiasan; yang membuatnya hidup
                // adalah ia datang, lewat, lalu lama tidak muncul lagi. Lahirnya DI LUAR layar
                // (dipaksa juga oleh Weather dari geometri kameranya) — yang menetas di dalam
                // pandangan cuma "muncul", tidak pernah "datang".
                Ambient("Birds/Birds_flock_V", 0.8f, repeat: 45f, lifetime: 18f,
                    near: 34f, far: 60f, scale: 0.7f, height: 14f),

                Ambient("Birds/Birds_spin", 0.5f, repeat: 75f, lifetime: 16f,
                    near: 34f, far: 58f, scale: 0.6f, height: 12f)
            };

            // Berkas cahaya datar DICOPOT. Alpha nol mematikan lapisannya sama sekali — bidang
            // tidak punya tinggi, dan berkas yang tidak punya tinggi bukan berkas. Penggantinya
            // berkas volumetrik sungguhan dari kabut froxel, disetel di HazePass.
            forest.RayColor = new Color(1f, 0.97f, 0.82f, 0f);

            // Cuaca, diundi tiap wave. Entri pertama SENGAJA kosong — tanpa cuaca cerah di daftar,
            // tiap wave selalu ada sesuatu yang jatuh dari langit, dan hujan berhenti terasa
            // sebagai kejadian karena tidak pernah ada jeda di antaranya.
            // Sama seperti di atas: diisi sekali, lalu milik penyetel.
            bool freshMoods = forest.WeatherMoods == null || forest.WeatherMoods.Length == 0;

            // Sebaran permintaan terbaru: BERANGIN 50%, basah TOTAL 40% (gerimis + hujan +
            // badai), cerah 10%. Bobot ditulis supaya totalnya sepuluh — persentasenya terbaca
            // langsung dari angkanya.
            if (freshMoods) forest.WeatherMoods = new[]
            {
                new WeatherMood { Name = "Cerah", Weight = 1f },

                // SEPOI-SEPOI, bukan badai debu: berangin = daun melayang pelan (pilihan
                // pemilik project). Wind_heavy pensiun dari sini — ia cuma boleh datang
                // menemani hujan dan badai.
                new WeatherMood
                {
                    Name = "Berangin",
                    Weight = 5f,
                    Overcast = 0.15f,
                    Effects = new[]
                    {
                        Fx("Wind_Leaves_Tornado/Leaves_green", 0.1f, 1f, 15f, -22f),
                        Fx("Wind_Leaves_Tornado/Leaves_orange", 0.09f, 1f, 14f, -26f)
                    }
                },

                // Hujan diperbesar 4–6 KALI. Prefab-nya disetel untuk kamera setinggi badan, dan di
                // kamera yang menunduk dari 18 unit ukuran aslinya cuma menetes di satu petak
                // selebar beberapa unit — hujan yang tidak menutupi layar terbaca sebagai bocor.
                //
                // TANPA Sunlight di paket basah mana pun. Dulu ia di sini — satu titik debu cahaya
                // yang menempel di pusat kamera, di bawah langit hujan. Sekarang ia suasana siang
                // cerah, disebar sebagai kantong.
                new WeatherMood
                {
                    Name = "Gerimis",
                    Weight = 2f,
                    Speed = 0.85f,
                    Wet = true,
                    Overcast = 0.35f,
                    Effects = new[] { Fx("Rain/Rain_calm", 2.4f, 1.1f, 0f, 0f, true) }
                },

                new WeatherMood
                {
                    Name = "Hujan",
                    Weight = 1.4f,
                    Wet = true,
                    Overcast = 0.6f,
                    Effects = new[]
                    {
                        Fx("Rain/Rain_average", 2.6f, 1.1f, 0f, 0f, true),
                        Fx("Wind_Leaves_Tornado/Wind_heavy", 1.8f, 0.2f, 12f, -22f)
                    }
                },

                new WeatherMood
                {
                    Name = "Badai",
                    Weight = 0.6f,
                    Speed = 1.25f,
                    Wet = true,
                    Overcast = 0.85f,

                    // TANPA daun di sini, dan itu koreksi. Pengali ukuran cuaca berlaku untuk
                    // SELURUH efeknya, dan badai butuh 6× supaya hujannya menutupi layar — pengali
                    // yang sama mengubah daun jadi selebar dua meter, melayang seperti layang-layang.
                    // Daun sudah diurus lapisan suasana dengan ukurannya sendiri.
                    Effects = new[]
                    {
                        Fx("Rain/Rain_heavy", 3f, 1.2f, 0f, 0f, true),
                        Fx("Wind_Leaves_Tornado/Wind_heavy", 2f, 0.2f, 12f, -26f)
                    }
                }
            };

            // Jarang DAN pendek. Pohon setinggi 8 unit di kamera ortografis menutupi seperempat
            // layar sendirian, dan yang tertutup selalu gerombolan — satu-satunya hal yang harus
            // dibaca pemain. Yang dicari adalah padang terbuka dengan pohon sebagai penanda jarak,
            // bukan rimba yang menghalangi.
            forest.TreeCount = 55;

            // Dinaikkan dari 2,2–4. Cemara itu kerucut: yang lebar cuma pangkalnya, dan dari kamera
            // menunduk yang tergambar adalah bintang kecil — bukan tajuk bundar yang menutup
            // seperempat layar seperti pohon berdaun lebar. Bentuk itulah yang membeli tinggi
            // tambahan tanpa membeli halangan.
            forest.TrunkHeightRange = new Vector2(5f, 8f);
            forest.TrunkWidthRange = new Vector2(0.18f, 0.34f);
            forest.CanopyWidthRatio = new Vector2(0.5f, 0.8f);
            forest.CanopyFlatten = 0.6f;

            forest.TrunkColors = new[]
            {
                new Color(0.32f, 0.26f, 0.22f),
                new Color(0.24f, 0.2f, 0.18f),
                new Color(0.4f, 0.32f, 0.25f)
            };

            // Hijau SEDANG, bukan gelap dan bukan neon. Di referensinya dedaunan duduk cuma
            // sedikit lebih gelap dari rumputnya — yang memisahkan pohon dari lantai adalah
            // bayangannya, bukan warnanya. Satu aksen amber untuk titik hangat.
            forest.CanopyColors = new[]
            {
                new Color(0.26f, 0.42f, 0.24f),
                new Color(0.33f, 0.5f, 0.27f),
                new Color(0.19f, 0.32f, 0.22f),
                new Color(0.55f, 0.42f, 0.18f)
            };

            // Rumput dipangkas keras. Instancing memang murah di GPU, tapi tiap rumpun tetap satu
            // matriks di CPU dan satu bayangan yang dihitung — seribu empat ratus rumpun di
            // dua puluh lima petak adalah puluhan ribu, dan di situlah fps-nya jatuh.
            // Yang dibutuhkan cuma cukup untuk memecah lantai polos, bukan menutupinya.
            // Naik dari 260, dan kali ini kenaikannya dibayar di tempat yang benar. Angka lama
            // dipangkas karena tiap rumpun adalah satu matriks yang disusun ULANG tiap frame;
            // matriksnya sekarang dipanggang, jadi yang tersisa cuma biaya menggambar. Terukur di
            // wave 6: 6 027 prop, 22 draw call, 58 fps dengan 30 musuh di layar.
            //
            // 260 rumpun menyisakan sekitar tujuh puluh helai di layar. Itu bukan lapangan berumput,
            // itu lantai polos dengan tujuh puluh coretan di atasnya.
            // TURUN JAUH dari 1 800, dan itu koreksi atas dua kesalahan sekaligus.
            //
            // Rumput sudah pindah ke sistem detail terrain, jadi angka ini tidak lagi mengurus
            // penutup tanah — yang tersisa cuma benda berukuran sedang: semak, batu, kayu. Benda
            // seperti itu dibaca satu per satu, dan enam ribu di antaranya tidak terbaca sebagai
            // hutan melainkan sebagai barang berserakan.
            //
            // 260 menghasilkan sekitar 780 di seluruh petak dan kira-kira 45 di layar. Cukup untuk
            // membuat tiap layar berbeda, jarang untuk membuat tiap benda punya alasan berada.
            forest.ScatterCount = 260;
            forest.ScatterShape = PrimitiveType.Cube;
            forest.ScatterScaleRange = new Vector2(0.12f, 0.3f);
            forest.ScatterFlatten = 2.4f;

            forest.ScatterColors = new[]
            {
                new Color(0.3f, 0.46f, 0.26f),
                new Color(0.38f, 0.54f, 0.3f),
                new Color(0.23f, 0.36f, 0.24f),
                new Color(0.48f, 0.5f, 0.28f)
            };

            // SATU jenis saja sekarang. Batang+tajuk prosedural tidak lagi muncul: ia tetap ada di
            // kode sebagai cadangan kalau daftar mesh-nya kosong, tapi bola di atas silinder berdiri
            // di sebelah pohon low-poly seperti benda dari game lain.
            forest.MeshTreeShare = 1f;
            forest.MeshTrees = Kit(
                // Paketnya cuma punya DUA pohon, dan keduanya cemara. Kekurangan jenis ditambal
                // dari arah lain: batu besar dan tunggul dimasukkan ke daftar POHON, bukan ke
                // daftar rumput. Yang dibeli sebuah pohon di lapangan ini adalah penanda setinggi
                // badan yang memecah pandangan, dan batu setinggi itu membelinya sama saja.
                //
                // Cemara punya DUA submesh — batang dan daun. Sampai hari ini PropBatch cuma
                // menggambar submesh nol, jadi daftar ini mustahil dipakai tanpa perbaikan itu.
                Prop(Fir + "UNS_Spruce_01.prefab", 2, 2.5f),
                Prop(Fir + "UNS_Spruce_02.prefab", 2, 3.5f),

                // Batu tebing DIBUANG. Diskalakan setinggi pohon ia jadi bongkahan putih selebar
                // tiga unit — benda paling terang dan paling besar di lapangan, dan yang dibelinya
                // cuma satu bentuk yang tidak berarti apa-apa.
                Prop(Logs + "UNS_Stump.prefab", 1, 0.7f, 0.4f));

            forest.MeshScatterShare = 1f;

            // Lebar JADI-nya, bukan lebar aslinya. Batas atasnya bukan soal selera: rumpun setinggi
            // seperempat lebarnya, jadi rumput selebar dua unit masih pendek — tapi semak dan batu
            // di daftar yang sama ikut membesar, dan di situ musuh mulai tertelan.
            forest.MeshScatterWidth = new Vector2(0.7f, 1.5f);

            // Rumput dan bunga TIDAK ADA di sini lagi — keduanya penutup tanah, dan penutup tanah
            // dikuas di terrain, bukan dijatuhkan di titik acak. Yang tersisa cuma benda yang cukup
            // besar untuk dibaca satu per satu, dan tiap satunya harus pantas dilihat.
            forest.MeshScatter = Kit(
                Prop(Bushes + "UNS_Bush.prefab", 1, 3f, 1.1f),

                Prop(Boulders + "UNS_Standard_Rock_01.prefab", 1, 1.5f, 0.8f),
                Prop(Boulders + "UNS_Standard_Rock_05.prefab", 1, 0.8f, 0.95f),

                Prop(Shrooms + "UNS_Mushroom_Patch.prefab", 1, 0.6f, 0.55f),
                Prop(Pebbles + "UNS_Tiny_Rock_01.prefab", 1, 0.7f, 0.6f),

                // Kayu tumbang sengaja LANGKA. Batang dan ranting adalah benda yang paling cepat
                // terbaca sebagai sampah begitu jumlahnya lewat beberapa per layar: bentuknya
                // memanjang dan arahnya acak, jadi otak membacanya sebagai barang yang tercecer.
                Prop(Logs + "UNS_Log.prefab", 1, 0.3f, 0.95f),
                Prop(Branches + "UNS_Branch.prefab", 1, 0.3f, 0.75f));

            // Rumput: LETAKNYA dari kuas, JUMLAHNYA dari sini. LOD2 karena tiap layar berisi
            // ribuan rumpun dan tak satu pun lebih besar dari sekepal di layar.
            forest.MeshGrass = Kit(Prop(Env + "Vegetation/Grass/Prefabs/UNS_Grass.prefab", 2, 1f));
            forest.GrassWidth = new Vector2(0.7f, 1.3f);

            // NOL — rumput dimatikan atas permintaan. Yang dinolkan JUMLAHNYA, bukan daftar
            // mesh-nya: cat di terrain, ukuran, dan pilihan mesh semuanya tetap utuh, jadi
            // menyalakannya lagi cuma soal mengembalikan satu angka. Menghapus daftarnya berarti
            // seluruh penyetelan itu harus dicari ulang nanti.
            forest.GrassPerChunk = 0;

            // Bayangan pohon DINYALAKAN, dan ini bukan hiasan.
            //
            // Lantai terang merata tanpa bayangan terbaca sebagai kain hijau, bukan sebagai
            // lapangan — yang memberi kedalaman di scene acuannya bukan warna tanahnya melainkan
            // bayangan yang jatuh di atasnya. Rumput tetap tanpa bayangan: ribuan pelempar bayangan
            // membuat pass bayangan lebih mahal dari seluruh sisa layarnya, sementara pohon cuma
            // ratusan.
            forest.TreeShadows = true;

            // Lebih lebar dari radius biasanya: pemain memulai di sini, dan wave pertama tidak
            // boleh dibuka dengan pandangan yang terhalang batang pohon.
            forest.ClearingRadius = 10f;

            EditorUtility.SetDirty(forest);
            AdoptSunnyGrade();

            forest.PostProcess = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                "Assets/GameData/Look/PP_Sunny.asset");

            string nightPath = BuildNight(forest);

            var nightAsset = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(nightPath);
            string sorePath = BuildSore(forest);
            string midnightPath = BuildMidnight(nightAsset);

            AssetDatabase.SaveAssets();

            // TIDAK ada Refresh() di sini. Refresh menjadwalkan impor ulang, dan selama impor itu
            // berjalan LoadAssetAtPath mengembalikan null untuk aset yang jelas-jelas ada di disk —
            // yang tersimpan ke scene lalu jadi slot kosong, tanpa satu pun error.
            Attach(new[] { path, nightPath, sorePath, midnightPath });

            Debug.Log($"[BiomePass] '{forest.DisplayName}' siap: {forest.TreeCount} pohon " +
                      $"({forest.MeshTreeShare:P0} bermesh dari {forest.MeshTrees.Length} jenis), " +
                      $"{forest.ScatterCount} semak " +
                      $"({forest.MeshScatterShare:P0} bermesh dari {forest.MeshScatter.Length} jenis). " +
                      "Tersambung ke _Bootstrap.");
        }

        /// <summary>
        /// Menarik SATU mesh dan SATU material dari prefab paket aset, pada tingkat LOD yang
        /// diminta.
        ///
        /// Pemilihan LOD terjadi di sini, sekali, saat aset dibangun — bukan saat bermain. Prefab
        /// ToonScapes membawa LODGroup dengan tiga sampai lima renderer, dan memasangnya sebagai
        /// GameObject berarti tiap rumpun rumput jadi satu objek yang LOD-nya dihitung tiap frame.
        /// Seribu rumpun seperti itu adalah persis beban yang dihapus <c>PropBatch</c>.
        ///
        /// Mengembalikan null kalau path-nya salah, dan itu dilaporkan sebagai error — daftar
        /// dengan satu entri kosong menghasilkan hutan yang lebih jarang tanpa satu pun tanda
        /// bahwa ada yang gagal dimuat.
        /// </summary>
        /// <summary>
        /// Menukar gradasi warna scene game ke gradasi cerah milik demo paket asetnya.
        ///
        /// Profil lama (`PP_GoldenHour`) memasang vignette biru-gelap pada 0,45. Itu dulu separuh
        /// dari pembingkaian gelapnya, dan sekarang ia satu-satunya sisa yang masih menggelapkan
        /// tepi layar setelah kabut dan ambient-nya diganti — mengganti cahaya tanpa menggantinya
        /// menghasilkan lapangan terang di dalam bingkai gelap yang sudah tidak dimaksudkan lagi.
        ///
        /// DISALIN ke GameData, tidak dirujuk langsung. Menunjuk aset di dalam folder paket berarti
        /// penyetelan berikutnya mengotori paket pihak ketiga, dan itu baru ketahuan saat file-nya
        /// muncul di diff.
        /// </summary>
        /// <summary>
        /// Wajah MALAM, dibangun dengan MENYALIN wajah siang lalu menimpa cahayanya saja.
        ///
        /// Menyalin, bukan menulis ulang dari nol. Daftar pohon, daftar rumput, kerapatan, ukuran,
        /// lapisan lantai — semuanya harus persis sama, karena yang berbeda antara siang dan malam
        /// bukan hutannya melainkan cahayanya. Menulis dua daftar terpisah berarti tiap penyetelan
        /// vegetasi harus dikerjakan dua kali, dan yang sebenarnya terjadi adalah dikerjakan sekali
        /// lalu satu wajah diam-diam tertinggal.
        /// </summary>
        static string BuildNight(BiomeDefinition day)
        {
            string path = Folder + "/Biome_forest_night.asset";
            var night = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(path);

            if (night == null)
            {
                night = ScriptableObject.CreateInstance<BiomeDefinition>();
                AssetDatabase.CreateAsset(night, path);
            }

            // Daftar VFX & cuaca malam DISELAMATKAN dulu. CopySerialized menimpa SEMUA field
            // dengan milik siang — termasuk dua daftar yang bukan milik pass ini melainkan milik
            // penyetel. Tanpa ini, guard "masih kosong?" di bawah tidak pernah menyala (daftarnya
            // sudah terisi salinan siang) dan wajah malam selamanya memakai kupu-kupu siang —
            // persis begitulah kunang-kunangnya hilang tanpa satu pun error.
            var keptVfx = night.AmbientVfx;
            var keptMoods = night.WeatherMoods;

            EditorUtility.CopySerialized(day, night);

            night.AmbientVfx = keptVfx;
            night.WeatherMoods = keptMoods;

            // CopySerialized ikut menyalin NAMA objeknya. Untuk aset ScriptableObject, nama objek
            // adalah nama asetnya — tanpa dikembalikan, dua aset berbeda muncul dengan nama sama.
            night.name = "Biome_forest_night";

            night.Id = "forest_night";
            night.DisplayName = "MALAM";

            // BULAN, bukan matahari yang diredupkan. Warnanya bergeser ke biru DAN sudutnya naik:
            // bayangan panjang milik sore, dan malam yang masih punya bayangan panjang terbaca
            // sebagai sore yang gelap.
            night.SunColor = new Color(0.6f, 0.72f, 1f);
            night.SunPitch = 58f;
            night.SunYaw = 300f;
            night.SunIntensity = 0.55f;

            // Dinaikkan dari (0,14; 0,19; 0,32) — "jangan gelap gulita". Malam yang terbaca
            // bukan malam yang hitam, melainkan malam yang cahayanya PUNYA TEMPAT.
            night.AmbientSky = new Color(0.17f, 0.22f, 0.36f);
            night.AmbientEquator = new Color(0.13f, 0.17f, 0.28f);
            night.AmbientGround = new Color(0.07f, 0.09f, 0.15f);

            night.FogEnabled = true;
            night.FogColor = new Color(0.09f, 0.13f, 0.22f);
            night.FogStart = 25f;
            night.FogEnd = 110f;

            // Skybox DICOPOT. Cubemap siang di belakang lapangan malam adalah satu-satunya benda
            // di layar yang masih siang, dan mata menemukannya seketika.
            night.Skybox = null;
            night.HorizonColor = new Color(0.05f, 0.07f, 0.13f);

            // Lampu pengembara DINYALAKAN — di siang hari ia tidak membeli apa pun karena lantainya
            // sudah terang merata. Di malam hari ia justru satu-satunya yang membuat lapangan punya
            // tempat terang dan tempat gelap, dan kabut volumetrik mengubah tiap lampu jadi kolam
            // cahaya yang terlihat isinya.
            // "Cahaya internal": SEDIKIT titik tapi KUAT dan lebar — beberapa bagian lapangan
            // benar-benar terang, sisanya remang. Sepuluh lampu lemah menghasilkan remang merata
            // yang tidak menerangi apa pun; tujuh lampu kuat menghasilkan TEMPAT.
            night.LampCount = 7;
            night.LampColor = new Color(1f, 0.72f, 0.36f);
            night.LampColorCool = new Color(0.36f, 0.58f, 1f);
            night.LampIntensity = 5.5f;
            night.LampRange = 24f;
            night.LampHeight = 5f;

            // Lampu yang dibawa pemain. Dingin, supaya ia terbaca sebagai cahaya sihir yang
            // dibawa — bukan sebagai obor, yang akan menyaingi lampu hangat di lapangan.
            night.PlayerLightIntensity = 5f;
            night.PlayerLightColor = new Color(0.62f, 0.8f, 1f);
            night.PlayerLightRange = 14f;
            night.PlayerLightHeight = 2.2f;
            night.RainPlayerLight = 5.5f;

            // Awan malam jauh lebih tipis: bayangan di atas lapangan yang sudah gelap tidak membeli
            // keteduhan, ia cuma menghapus apa yang masih bisa dibaca.
            night.CloudColor = new Color(0.02f, 0.03f, 0.08f, 0.16f);
            night.CloudSize = 44f;
            night.RayColor = new Color(0.55f, 0.68f, 1f, 0.07f);

            // KUNANG-KUNANG. Varian "_fog" kupu-kupu itu yang bercahaya sendiri, dan di lapangan
            // gelap ia berhenti terbaca sebagai kupu-kupu — yang tersisa cuma titik cahaya yang
            // mengembara, dan itu persis kunang-kunang. Bara api menambah yang naik perlahan.
            bool freshNightVfx = night.AmbientVfx == null || night.AmbientVfx.Length == 0;

            if (freshNightVfx) night.AmbientVfx = new[]
            {
                // KUNANG-KUNANG sungguhan — titik cahaya berkedip yang mengembara, prefab
                // bangkitan EnsureFireflies. Varian _fog kupu-kupu DICOPOT dari malam: ia tetap
                // terbaca sebagai kupu-kupu yang menyala, dan kupu-kupu milik siang. Satu-satunya
                // makhluk yang boleh menyala di malam adalah kunang-kunang.
                // Dikurangi dari 4+3 kantong — "terlalu banyak kunang-kunang". Dua kantong
                // sudah cukup jadi tanda kehidupan; sisanya biar dikerjakan lampu arena.
                Ambient("Assets/GameData/Look/Fireflies", 0.7f, spread: 2, near: 7f, far: 22f,
                    scale: 1f, height: 0.9f, hideInRain: true),

                Ambient("Embers/Embers_calm", 0.35f, spread: 2, near: 6f, far: 18f,
                    scale: 0.5f, height: 0.6f, hideInRain: true),

                // Beam cahaya BULAN milik paket, apa adanya — aturan yang sama dengan siang:
                // bentuk milik prefab (setelan tangan pemilik project), kode cuma menempatkan.
                Ambient("Light/Moonlight", 1f, spread: 3, near: 12f, far: 30f,
                    scale: 1f, height: 0f, hideInRain: true),

                Ambient("Wind_Leaves_Tornado/Leaves_green", 0.3f,
                    scale: 0.1f, height: 15f, follow: true, offX: -22f)
            };

            night.RayColor = new Color(0.55f, 0.68f, 1f, 0f);

            // Cuaca malam lebih tenang. Badai di atas lapangan yang sudah gelap tidak menambah
            // suasana — ia cuma menghapus apa yang masih bisa dibaca.
            bool freshNightMoods = night.WeatherMoods == null || night.WeatherMoods.Length == 0;

            // Sebaran yang sama dengan siang: berangin 50%, basah 40%, sunyi 10%. Badai tetap
            // absen — badai di atas lapangan yang sudah gelap cuma menghapus yang masih terbaca.
            if (freshNightMoods) night.WeatherMoods = new[]
            {
                new WeatherMood { Name = "Sunyi", Weight = 1f },

                new WeatherMood
                {
                    Name = "Berangin",
                    Weight = 5f,
                    Speed = 0.8f,
                    Effects = new[]
                    {
                        Fx("Wind_Leaves_Tornado/Leaves_green", 0.1f, 1f, 15f, -22f),
                        Fx("Wind_Leaves_Tornado/Leaves_orange", 0.09f, 1f, 14f, -26f)
                    }
                },

                // Mendung malam ditahan rendah. Lapangannya sudah gelap; meredupkan bulan lebih
                // jauh tidak menambah suasana, ia cuma menghapus siluet yang masih bisa dibaca.
                new WeatherMood
                {
                    Name = "Gerimis",
                    Weight = 2.4f,
                    Speed = 0.85f,
                    Wet = true,
                    Overcast = 0.25f,
                    Effects = new[] { Fx("Rain/Rain_calm", 2.4f, 1.1f, 0f, 0f, true) }
                },

                new WeatherMood
                {
                    Name = "Hujan",
                    Weight = 1.6f,
                    Wet = true,
                    Overcast = 0.4f,
                    Effects = new[] { Fx("Rain/Rain_average", 2.6f, 1.1f, 0f, 0f, true) }
                }
            };

            night.PostProcess = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                "Assets/GameData/Look/PP_Night.asset");

            if (night.PostProcess == null)
            {
                Debug.LogWarning("[BiomePass] PP_Night belum ada — jalankan " +
                                 "Tools/Grimoire/Install Volumetric Fog, lalu ulangi pass ini.");
            }

            EditorUtility.SetDirty(night);
            return path;
        }

        /// <summary>
        /// PP_Sunny milik penyetel: TIDAK PERNAH ditimpa kalau sudah ada. Pelajaran mahal hari
        /// ini — mengkloninya dari profil demo mengubah SIANG jadi senja tanpa diminta. Grade
        /// demo yang ternyata disukai itu sekarang wajah tersendiri: SENJA (PP_Sore).
        /// </summary>
        static void AdoptSunnyGrade()
        {
            const string copy = "Assets/GameData/Look/PP_Sunny.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(copy);

            if (profile == null)
            {
                const string source = "Assets/Plugin/ToonScapes/Shared Assets/Volume Profiles/" +
                                      "TS_Profile_Sunny_01.asset";

                if (!AssetDatabase.CopyAsset(source, copy))
                {
                    Debug.LogError("[BiomePass] gagal menyalin profil warna dari " + source);
                    return;
                }

                profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(copy);
            }

            var look = AssetDatabase.LoadAssetAtPath<SceneLook>("Assets/GameData/SceneLook_Game.asset");

            if (profile == null || look == null)
            {
                Debug.LogWarning("[BiomePass] profil warna atau SceneLook_Game tidak ketemu — " +
                                 "gradasi lama masih terpasang.");
                return;
            }

            look.PostProcess = profile;
            look.ShadowStrength = 1f;

            EditorUtility.SetDirty(look);
        }

        /// <summary>
        /// Wajah SENJA — lahir dari kecelakaan yang disukai: grade demo UNS yang sempat menimpa
        /// siang. Sekarang ia wajah sendiri: matahari RENDAH keemasan (bayangan panjang adalah
        /// separuh identitas senja), ambient hangat, dan PP_Sore = klon profil demo (ACES, white
        /// balance hangat). Hutan & suasananya menyalin siang — termasuk god ray Sunlight yang
        /// disetel tangan.
        /// </summary>
        static string BuildSore(BiomeDefinition day)
        {
            string path = Folder + "/Biome_forest_sore.asset";
            var sore = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(path);

            if (sore == null)
            {
                sore = ScriptableObject.CreateInstance<BiomeDefinition>();
                AssetDatabase.CreateAsset(sore, path);
            }

            var keptVfx = sore.AmbientVfx;
            var keptMoods = sore.WeatherMoods;

            EditorUtility.CopySerialized(day, sore);

            // Tuning penyetel menang; wajah yang belum pernah disetel mewarisi milik siang.
            if (keptVfx != null && keptVfx.Length > 0) sore.AmbientVfx = keptVfx;
            if (keptMoods != null && keptMoods.Length > 0) sore.WeatherMoods = keptMoods;

            sore.name = "Biome_forest_sore";
            sore.Id = "forest_sore";
            sore.DisplayName = "SENJA";

            sore.SunColor = new Color(1f, 0.72f, 0.42f);
            sore.SunPitch = 16f;
            sore.SunYaw = 147f;
            sore.SunIntensity = 2.5f;

            sore.AmbientSky = new Color(0.55f, 0.42f, 0.4f);
            sore.AmbientEquator = new Color(0.47f, 0.34f, 0.31f);
            sore.AmbientGround = new Color(0.25f, 0.18f, 0.15f);

            sore.FogColor = new Color(0.95f, 0.78f, 0.58f);
            sore.FogStart = 55f;
            sore.FogEnd = 210f;

            sore.PostProcess = SoreGrade();

            EditorUtility.SetDirty(sore);
            return path;
        }

        static VolumeProfile SoreGrade()
        {
            const string path = "Assets/GameData/Look/PP_Sore.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) return existing;

            const string source = Nature + "Demo/Lighting/Forest/UNS_Forest_Volume_Profile.asset";

            if (!AssetDatabase.CopyAsset(source, path))
            {
                Debug.LogError("[BiomePass] gagal menyalin profil demo ke " + path);
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        }

        /// <summary>
        /// Wajah TENGAH MALAM — malam yang lebih pekat: bulan nyaris padam, kabut rapat, lampu
        /// arena tinggal empat (kolam cahayanya justru makin berarti), dan PP_Midnight = salinan
        /// PP_Night yang exposure-nya dijatuhkan. Suasananya menyalin malam — kunang-kunang dan
        /// Moonlight ikut.
        /// </summary>
        static string BuildMidnight(BiomeDefinition night)
        {
            string path = Folder + "/Biome_forest_midnight.asset";
            var mid = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(path);

            if (mid == null)
            {
                mid = ScriptableObject.CreateInstance<BiomeDefinition>();
                AssetDatabase.CreateAsset(mid, path);
            }

            var keptVfx = mid.AmbientVfx;
            var keptMoods = mid.WeatherMoods;

            EditorUtility.CopySerialized(night, mid);

            if (keptVfx != null && keptVfx.Length > 0) mid.AmbientVfx = keptVfx;
            if (keptMoods != null && keptMoods.Length > 0) mid.WeatherMoods = keptMoods;

            mid.name = "Biome_forest_midnight";
            mid.Id = "forest_midnight";
            mid.DisplayName = "TENGAH MALAM";

            mid.SunColor = new Color(0.5f, 0.62f, 1f);
            mid.SunPitch = 66f;
            mid.SunIntensity = 0.3f;

            mid.AmbientSky = new Color(0.08f, 0.11f, 0.2f);
            mid.AmbientEquator = new Color(0.06f, 0.085f, 0.16f);
            mid.AmbientGround = new Color(0.035f, 0.05f, 0.09f);

            mid.FogColor = new Color(0.05f, 0.07f, 0.13f);
            mid.FogStart = 18f;
            mid.FogEnd = 85f;

            mid.LampCount = 4;
            mid.LampIntensity = 4.5f;
            mid.LampRange = 22f;

            mid.PlayerLightIntensity = 6f;

            mid.PostProcess = MidnightGrade();

            EditorUtility.SetDirty(mid);
            return path;
        }

        static VolumeProfile MidnightGrade()
        {
            const string path = "Assets/GameData/Look/PP_Midnight.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) return existing;

            if (!AssetDatabase.CopyAsset("Assets/GameData/Look/PP_Night.asset", path))
            {
                Debug.LogError("[BiomePass] gagal menyalin PP_Night ke " + path);
                return null;
            }

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);

            // Dijatuhkan SEKALI saat lahir, sesudahnya milik penyetel.
            if (profile != null && profile.TryGet<ColorAdjustments>(out var color))
            {
                color.postExposure.overrideState = true;
                color.postExposure.value = -1f;

                color.saturation.overrideState = true;
                color.saturation.value = -22f;

                EditorUtility.SetDirty(profile);
            }

            return profile;
        }

        static MeshProp Prop(string prefabPath, int lod, float weight, float sizeMultiplier = 1f)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (go == null)
            {
                Debug.LogError("[BiomePass] prefab tidak ketemu: " + prefabPath);
                return null;
            }

            Renderer renderer = null;
            var group = go.GetComponentInChildren<LODGroup>(true);

            if (group != null)
            {
                var levels = group.GetLODs();
                var chosen = levels[Mathf.Clamp(lod, 0, levels.Length - 1)].renderers;

                for (int i = 0; i < chosen.Length && renderer == null; i++) renderer = chosen[i];
            }

            if (renderer == null) renderer = go.GetComponentInChildren<MeshRenderer>(true);

            var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
            var mesh = filter != null ? filter.sharedMesh : null;

            // SEMUA material renderer-nya, berurutan menurut submesh. Mengambil sharedMaterial saja
            // menghasilkan pohon yang cuma batang: cemara memisahkan batang dari daun sebagai dua
            // submesh, dan yang kedua tidak akan pernah tergambar.
            var materials = renderer != null ? renderer.sharedMaterials : null;

            if (mesh == null || materials == null || materials.Length == 0 || materials[0] == null)
            {
                Debug.LogError("[BiomePass] " + go.name + " tidak punya mesh/material di LOD " + lod);
                return null;
            }

            if (materials.Length < mesh.subMeshCount)
            {
                Debug.LogWarning("[BiomePass] " + go.name + " punya " + mesh.subMeshCount +
                                 " submesh tapi cuma " + materials.Length + " material — " +
                                 "sebagian bentuknya tidak akan tergambar.");
            }

            return new MeshProp
            {
                Name = go.name + " LOD" + lod,
                Mesh = mesh,
                Materials = materials,
                Weight = weight,
                SizeMultiplier = sizeMultiplier
            };
        }

        const string Vfxs = "Assets/Plugin/Lana Studio/Environment VFX pack/Prefabs/";

        /// <summary>
        /// Memuat sekumpulan prefab VFX dan MELAPORKAN yang tidak ketemu.
        ///
        /// Slot kosong di daftar cuaca gagal dengan cara yang paling sulit dilihat: cuacanya tetap
        /// berganti, namanya tetap muncul, dan yang hilang cuma satu lapis dari beberapa — hujan
        /// tanpa anginnya, badai tanpa daunnya.
        /// </summary>
        /// <summary>Satu entri suasana. <c>repeat &gt; 0</c> berarti ia LEWAT, bukan menetap.</summary>
        /// <summary>
        /// Material tampilan sebagai ASET, dibuat sekali dan TIDAK PERNAH ditimpa.
        ///
        /// Ini syarat supaya shader-nya bisa disetel sama sekali. Material yang dibuat saat bermain
        /// hilang tiap kali play mode dimulai, jadi tiap putaran lihat-ubah-lihat kembali ke nol —
        /// dan menyetel shader menuntut belasan putaran seperti itu.
        /// </summary>
        static Material LookMaterial(string name, string shaderName)
        {
            const string folder = "Assets/GameData/Look";
            string path = folder + "/" + name + ".mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find(shaderName);

            if (shader == null)
            {
                Debug.LogError("[BiomePass] shader tidak ketemu: " + shaderName);
                return null;
            }

            var material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Memuat prefab VFX. Nama pendek dicari di paket Lana; path lengkap berawalan
        /// "Assets/" dipakai apa adanya — paket lain juga punya efek yang layak dipakai.</summary>
        static GameObject LoadVfx(string name)
        {
            string path = name.StartsWith("Assets/") ? name + ".prefab" : Vfxs + name + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null) Debug.LogError("[BiomePass] VFX tidak ketemu: " + path);
            return prefab;
        }

        static AmbientVfxEntry Ambient(string name, float chance, float repeat = 0f,
            float lifetime = 14f, int spread = 1, float near = 6f, float far = 16f,
            float scale = 1f, float height = 0f, bool hideInRain = false, bool follow = false,
            float offX = 0f, float offZ = 0f, bool onlyClear = false, bool coverage = false,
            Color? tint = null, float stretch = 1f, bool cullSheets = false)
        {
            return new AmbientVfxEntry
            {
                Prefab = LoadVfx(name),
                Tint = tint ?? Color.white,
                Stretch = stretch,
                CullSheets = cullSheets,
                Chance = chance,
                RepeatEvery = repeat,
                Lifetime = lifetime,
                Count = spread,
                MinDistance = near,
                Spread = far,
                Scale = scale,
                Height = height,
                HideInRain = hideInRain,
                FollowCamera = follow,
                Offset = new Vector2(offX, offZ),
                OnlyClear = onlyClear,
                CoverageOnly = coverage
            };
        }

        /// <summary>Satu efek CUACA lengkap dengan ukuran, butiran, dan offsetnya sendiri.</summary>
        static AmbientVfxEntry Fx(string name, float scale = 1f, float grain = 1f,
            float height = 0f, float offX = 0f, bool coverage = false)
        {
            return new AmbientVfxEntry
            {
                Prefab = LoadVfx(name),
                Scale = scale,
                Grain = grain,
                Height = height,
                Offset = new Vector2(offX, 0f),
                CoverageOnly = coverage
            };
        }

        /// <summary>
        /// Prefab god ray BUATAN SENDIRI, dibangkitkan sekali.
        ///
        /// Mesh shaft paket (TSI) menyerah di kamera ini: ia dirancang untuk kamera sejajar
        /// mata, dan dari 68 derajat di atas ia memipih jadi GENANGAN cahaya di lantai — bukan
        /// berkas. Yang selalu terbaca sebagai god ray di kamera atas adalah PITA yang menghadap
        /// kamera, miring searah matahari (searah bayangan pohon), pangkal jelas di bawah dan
        /// puncak yang MELURUH habis lewat gradien — jadi sumbernya tidak pernah terlihat.
        /// </summary>
        static void EnsureGodRay()
        {
            const string path = "Assets/GameData/Look/GodRay.prefab";

            // SELALU ditulis ulang — bentuk god ray milik pass ini, dan menimpanya lewat
            // SaveAsPrefabAsset mempertahankan GUID, jadi rujukan di aset biome tidak putus.

            // ---- tekstur gradien: melintang = bel lembut, membujur = pangkal lembut,
            //      badan panjang, lenyap total sebelum ujung ----
            const string texPath = "Assets/GameData/Look/GodRayGradient.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            bool freshTexture = texture == null;

            if (freshTexture)
            {
                texture = new Texture2D(64, 256, TextureFormat.RGBA32, false)
                {
                    name = "GodRayGradient"
                };
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            {
                int w = texture.width;
                int h = texture.height;
                var pixels = new Color32[w * h];

                for (int y = 0; y < h; y++)
                {
                    float v = y / (float)(h - 1);

                    // Membujur: naik lembut di 7% pertama (pangkal TANPA tepi kotak), bertahan,
                    // lalu meluruh dan NOL di 85% — sisa 15% teratas kosong supaya ujungnya tidak
                    // pernah kelihatan, dari mana pun kamera memotongnya.
                    float baseIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.07f));
                    float fadeOut = Mathf.Clamp01(1f - v / 0.85f);
                    float along = baseIn * fadeOut * fadeOut;

                    for (int x = 0; x < w; x++)
                    {
                        float u = x / (float)(w - 1);

                        // Melintang: bel kosinus — tepi kiri-kanan lenyap lembut.
                        float across = Mathf.Sin(u * Mathf.PI);
                        across = Mathf.Pow(across, 1.6f);

                        byte a = (byte)(Mathf.Clamp01(along * across) * 255f);
                        pixels[y * w + x] = new Color32(255, 255, 255, a);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();
            }

            if (freshTexture) AssetDatabase.CreateAsset(texture, texPath);
            else EditorUtility.SetDirty(texture);

            // ---- material: pinjam setelan additive milik shaft paket, tukar teksturnya ----
            const string matPath = "Assets/GameData/Look/GodRay.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            if (material == null)
            {
                Material template = null;
                var tsi = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Plugin/ToonScapes/Spring Isles/Particles/TSI_Sun_Shaft_01A.prefab");

                if (tsi != null)
                {
                    foreach (var r in tsi.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    {
                        if (r.sharedMaterial == null) continue;
                        template = r.sharedMaterial;
                        break;
                    }
                }

                if (template == null)
                {
                    Debug.LogError("[BiomePass] material shaft TSI tidak ketemu — god ray batal.");
                    return;
                }

                material = new Material(template) { name = "GodRay" };
                material.SetTexture("_BaseMap", texture);
                material.SetTexture("_MainTex", texture);
                AssetDatabase.CreateAsset(material, matPath);
            }

            // ---- prefab: TIGA pita menghadap kamera, miring searah matahari ----
            //
            // Euler(68; 0; z): menghadap persis kamera yang menunduk 68 derajat (anchor-nya
            // Gloom — quad Euler(90;0;0) menghadap kamera tegak lurus), lalu z memutarnya DI
            // BIDANG LAYAR. +22 derajat = condong kiri-atas, searah matahari siang (bayangan
            // pohon jatuh kanan-bawah). Tiga pita beda lebar/kemiringan supaya terbaca sebagai
            // cahaya menembus tajuk, bukan satu papan.
            //
            // PANJANG 46 unit — layar cuma 22 unit tingginya: badan pita selalu menembus tepi
            // atas layar, dan gradien sudah menghabisinya sebelum ujung. Sumbernya tak pernah
            // terlihat, persis god ray sungguhan.
            var root = new GameObject("GodRay");

            BuildBlade(root.transform, material, 0f, 3.2f, 46f, 22f, 1f);
            BuildBlade(root.transform, material, -3.4f, 1.6f, 38f, 17f, 0.55f);
            BuildBlade(root.transform, material, 2.9f, 2.1f, 42f, 27f, 0.7f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            Debug.Log("[BiomePass] prefab god ray dibangkitkan: " + path);
        }

        static void BuildBlade(Transform parent, Material material, float side, float width,
            float length, float tilt, float strength)
        {
            var blade = GameObject.CreatePrimitive(PrimitiveType.Quad);
            blade.name = "Blade";

            var collider = blade.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            var rotation = Quaternion.Euler(68f, 0f, tilt);

            blade.transform.SetParent(parent, false);
            blade.transform.localRotation = rotation;

            // Pivot quad di tengah — digeser setengah panjang SEPANJANG sumbu pitanya sendiri,
            // jadi pangkalnya duduk di titik kantong dan seluruh badannya menjulang ke atas layar.
            blade.transform.localPosition = rotation * new Vector3(side, length * 0.5f, 0f);
            blade.transform.localScale = new Vector3(width, length, 1f);

            var renderer = blade.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Kekuatan per pita dibawa lewat skala alpha di property block SAAT spawn — di sini
            // cukup disimpan di nama supaya Weather bisa membacanya tanpa komponen baru.
            blade.name = "Blade " + strength.ToString("0.00");
        }

        /// <summary>
        /// Prefab kunang-kunang, dibangkitkan sekali dan tidak pernah ditimpa. Paket VFX tidak
        /// punya kunang-kunang — varian _fog kupu-kupu sempat dipakai dan tetap terbaca sebagai
        /// kupu-kupu yang menyala. Yang membuat kunang-kunang terbaca adalah KEDIPNYA, dan kedip
        /// itu dikerjakan gradasi alpha bergelombang di Color over Lifetime.
        /// </summary>
        static void EnsureFireflies()
        {
            const string path = "Assets/GameData/Look/Fireflies.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var go = new GameObject("Fireflies");
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
            main.startColor = new Color(1f, 0.86f, 0.38f, 1f);
            main.maxParticles = 120;

            var emission = ps.emission;
            emission.rateOverTime = 8f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(9f, 1.8f, 9f);

            // Mengembara lewat noise, bukan lewat kecepatan awal — kunang-kunang berbelok
            // terus-menerus, tidak meluncur lurus.
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.55f;
            noise.frequency = 0.22f;
            noise.damping = true;

            var lifeColor = ps.colorOverLifetime;
            lifeColor.enabled = true;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.1f),
                    new GradientAlphaKey(0.1f, 0.3f),
                    new GradientAlphaKey(1f, 0.5f),
                    new GradientAlphaKey(0.08f, 0.7f),
                    new GradientAlphaKey(0.9f, 0.88f),
                    new GradientAlphaKey(0f, 1f)
                });
            lifeColor.color = gradient;

            // Material dipinjam dari bara milik paketnya — titik lembut additive, persis yang
            // dibutuhkan. Menulis shader sendiri untuk satu titik glow itu pemborosan.
            var embers = AssetDatabase.LoadAssetAtPath<GameObject>(
                Vfxs + "Embers/Embers_calm.prefab");
            var renderer = go.GetComponent<ParticleSystemRenderer>();

            if (embers != null)
            {
                foreach (var source in embers.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    if (source.sharedMaterial == null) continue;
                    renderer.sharedMaterial = source.sharedMaterial;
                    break;
                }
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            Debug.Log("[BiomePass] prefab kunang-kunang dibangkitkan: " + path);
        }

        static MeshProp[] Kit(params MeshProp[] props)
        {
            var kept = new System.Collections.Generic.List<MeshProp>(props.Length);

            for (int i = 0; i < props.Length; i++)
            {
                if (props[i] != null) kept.Add(props[i]);
            }

            return kept.ToArray();
        }

        static void Attach(string[] paths)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
            var boot = Object.FindFirstObjectByType<ProtoBootstrap>();

            if (boot == null)
            {
                Debug.LogWarning("[BiomePass] _Bootstrap tidak ketemu di " + ScenePath +
                                 " — aset biome sudah dibuat, tapi belum tersambung.");
                return;
            }

            var so = new SerializedObject(boot);

            // Dua tahap, dan urutannya wajib. Mengubah ukuran array lalu langsung mengisi elemennya
            // dalam sesi SerializedObject yang sama akan menyimpan ukurannya saja: referensi
            // objeknya hilang tanpa satu pun error, dan yang tersimpan adalah slot kosong.
            so.FindProperty("_biomes").arraySize = paths.Length;
            so.ApplyModifiedPropertiesWithoutUndo();

            so.Update();
            var list = so.FindProperty("_biomes");

            for (int i = 0; i < paths.Length; i++)
            {
                var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(paths[i]);

                if (biome == null)
                {
                    Debug.LogError("[BiomePass] gagal memuat " + paths[i] +
                                   " — slot biome ini akan kosong. Jalankan ulang pass-nya.");
                    continue;
                }

                list.GetArrayElementAtIndex(i).objectReferenceValue = biome;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(boot);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
    }
}
