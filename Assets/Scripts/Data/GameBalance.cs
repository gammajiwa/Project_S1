using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Every tuning number in one asset. If a value is not here and not on a piece,
    /// it is a bug — it means something is still hardcoded.
    /// </summary>
    [CreateAssetMenu(fileName = "GameBalance", menuName = "Grimoire/Game Balance")]
    public class GameBalance : ScriptableObject
    {
        [Header("Grid")]
        public int GridWidth = 7;
        public int GridHeight = 7;
        public int BagWidth = 5;
        public int BagHeight = 4;

        [Header("Stat dasar pemain")]
        public float BaseMaxHp = 100f;
        public float BaseMaxMana = 60f;
        public float BaseManaRegen = 5f;
        public float BaseHpRegen = 0f;
        public float HealPerWaveClear = 20f;

        [Header("Gerak pemain")]
        [Tooltip("Kecepatan dasar menghindar. Bandingkan dengan EnemySpeedMin/Max: kalau terlalu " +
                 "jauh di atasnya, pemain tidak pernah tersentuh dan seluruh build HP/Defense mati.")]
        public float BaseMoveSpeed = 3.2f;

        [Tooltip("Jarak musuh mulai mendorong pemain menjauh.")]
        public float DangerRadius = 6f;

        [Tooltip("Seberapa cepat arah larinya berbelok. Rendah = terasa berat dan bisa terpojok.")]
        public float TurnRate = 6f;

        [Header("Arena")]
        [Tooltip("Batas gerak pemain, berbentuk elips supaya pas dengan layar lebar — kamera diam, " +
                 "jadi seluruh arena harus selalu kelihatan.")]
        public float ArenaHalfX = 16f;

        public float ArenaHalfZ = 9f;

        [Header("Wave")]
        // Menanjak pelan dengan sengaja. Wave 1 sekitar 33 musuh, bukan 125: awal run harus bisa
        // dibaca dulu sebelum jadi rusuh.
        [Tooltip("Musuh per DETIK di wave 0, sebelum tanjakan dalam-wave.")]
        public float SpawnRateBase = 0.8f;

        public float SpawnRatePerWave = 0.28f;

        [Min(1f)] public float SpawnRateGrowth = 1.03f;

        public float SurgeSpawnMultiplier = 1.25f;

        [Tooltip("Laju di awal dan di akhir wave, dikali laju dasarnya. Tiap wave menanjak sendiri " +
                 "menuju puncaknya, bukan datar dari detik pertama.")]
        public float WaveRampStart = 0.55f;

        public float WaveRampEnd = 1.6f;

        // 14, bukan 18. Ini suku KONSTAN kurva HP: pengaruhnya paling besar di wave-wave awal
        // dan tenggelam oleh suku pertumbuhan begitu wave naik — jadi menurunkannya melunakkan
        // node bawah ("agak asan dikit") tanpa menyentuh dinding endgame yang sudah diukur.
        public float EnemyHpBase = 14f;
        public float EnemyHpPerWave = 4f;

        /// <summary>Musuh per detik di wave ini, sebelum tanjakan dalam-wave.</summary>
        public float SpawnRateFor(int wave)
        {
            float rate = (SpawnRateBase + wave * SpawnRatePerWave) * Mathf.Pow(SpawnRateGrowth, wave);
            if (IsSurgeWave(wave)) rate *= SurgeSpawnMultiplier;
            return Mathf.Max(0.1f, rate);
        }

        /// <summary>Pengali laju di titik <paramref name="t"/> (0..1) sepanjang wave.</summary>
        public float RampAt(float t) => Mathf.Lerp(WaveRampStart, WaveRampEnd, Mathf.Clamp01(t));

        /// <summary>Perkiraan berapa detik satu wave berjalan, di laju rata-ratanya.</summary>
        public float ExpectedWaveSeconds(int wave)
        {
            float rate = SpawnRateFor(wave) * (WaveRampStart + WaveRampEnd) * 0.5f;
            return rate <= 0f ? 0f : EnemyCountFor(wave) / rate;
        }
        public float EnemySpeedMin = 1.5f;
        public float EnemySpeedMax = 2.1f;
        public float EnemySpeedPerWave = 0.04f;

        [Tooltip("Menumpuk PER musuh yang menempel. Lima musuh = lima kali angka ini.")]
        public float EnemyContactDps = 14f;

        [Tooltip("Tambahan damage sentuh per wave. Tanpa ini musuh wave 40 menyakiti persis " +
                 "sama seperti wave 1, sementara HP dan damage pemain sudah naik berkali lipat — " +
                 "jadi satu-satunya yang tumbuh adalah kebosanan.")]
        public float EnemyContactDpsPerWave = 1.7f;

        [Tooltip("Pengali damage tembakan musuh per wave. Sama alasannya dengan yang di atas.")]
        public float EnemyDamageGrowth = 1.055f;

        /// <summary>Damage sentuh per detik, per musuh, di wave ini.</summary>
        public float ContactDpsFor(int wave) =>
            EnemyContactDps + Mathf.Max(0, wave) * EnemyContactDpsPerWave;

        /// <summary>Pengali damage untuk apa pun yang ditembakkan musuh di wave ini.</summary>
        public float EnemyDamageScale(int wave) =>
            Mathf.Pow(Mathf.Max(1f, EnemyDamageGrowth), Mathf.Max(0, wave));
        [Header("Isi wave")]
        // Jumlah, bukan jam. Wave berbasis waktu membuat pemain menatap hitung mundur alih-alih
        // menatap lapangan, dan "sisa berapa" adalah satu-satunya angka yang benar-benar bisa
        // ditindaklanjuti. Lajunya tetap ada, tapi cuma mengatur kecepatan datangnya.
        public int EnemiesBase = 12;

        public int EnemiesPerWave = 6;

        [Min(1f)] public float EnemyCountGrowth = 1.05f;

        public float SurgeCountMultiplier = 1.25f;

        /// <summary>Berapa musuh yang datang di wave ini. Habis ini, wave menutup.</summary>
        public int EnemyCountFor(int wave)
        {
            float count = (EnemiesBase + wave * EnemiesPerWave) * Mathf.Pow(EnemyCountGrowth, wave);
            if (IsSurgeWave(wave)) count *= SurgeCountMultiplier;
            return Mathf.Max(1, Mathf.RoundToInt(count));
        }

        [Header("Penutupan wave")]
        [Tooltip("Pengali kecepatan musuh setelah spawn berhenti. Dibiarkan 1: sprint di akhir " +
                 "wave terbaca sebagai musuh yang tiba-tiba ngebut, dan itu mengganggu. Ekor mati " +
                 "sudah ditangani dua hal lain — spawn yang lebih dekat, dan berhentinya gerakan " +
                 "memutar begitu wave menutup.")]
        public float ClosingSpeedMultiplier = 1f;


        [Header("Spawn")]
        [Tooltip("Setengah-lebar KOTAK tempat musuh boleh muncul. Musuh dimunculkan di titik keluar " +
                 "kotak ini, bukan di lingkaran — area yang terlihat itu kotak lebar, jadi lingkaran " +
                 "selalu memunculkan sebagian musuh tepat di tepi layar.")]
        public float SpawnBoundsX = 21f;

        public float SpawnBoundsZ = 13f;

        [Tooltip("Musuh yang langsung ditaruh saat wave dimulai, lebih dekat dari biasanya. Tanpa " +
                 "ini setiap wave dibuka dengan belasan detik lapangan kosong: laju spawn memang " +
                 "sengaja pelan di awal, DAN yang pertama muncul masih harus berjalan masuk.")]
        public int WaveOpenerCount = 6;

        [Range(0.2f, 1f)]
        [Tooltip("Jarak pembuka, sebagai porsi jarak spawn normal.")]
        public float WaveOpenerDistance = 0.6f;

        [Tooltip("Boss muncul tiap kelipatan wave ini. 0 = tidak pernah.")]
        public int BossEveryWaves = 10;

        [Tooltip("Peluang satu wave jatuh di MALAM hari. Diundi per wave dan diseed dari nomor " +
                 "wave-nya — wave yang sama selalu siang/malam yang sama di setiap run, persis " +
                 "aturan yang dipakai cuaca. 0 = selalu siang. Cuma berlaku kalau biome-nya dua.")]
        [Range(0f, 1f)] public float NightChance = 0.5f;

        [Tooltip("Porsi wave SIANG yang jatuh sebagai SENJA (grade emas, matahari rendah). " +
                 "Butuh wajah arena ketiga terpasang.")]
        [Range(0f, 1f)] public float DuskChance = 0.3f;

        [Tooltip("Porsi wave MALAM yang jatuh sebagai TENGAH MALAM (lebih pekat, lampu lebih " +
                 "sedikit). Butuh wajah arena keempat terpasang.")]
        [Range(0f, 1f)] public float MidnightChance = 0.3f;

        [Header("Kamera")]
        [Tooltip("Besar zona mati kamera, sebagai porsi setengah layar. Pemain boleh bergerak " +
                 "sebebasnya di dalam zona ini tanpa kamera ikut sama sekali.\n\n" +
                 "Terlalu besar dan kamera nyaris tidak pernah bergerak; terlalu kecil dan kamera " +
                 "menempel di pemain, yang menghapus rasa bahwa PEMAIN yang berpindah, bukan " +
                 "dunianya yang bergeser.")]
        [Range(0.05f, 0.8f)] public float CameraDeadZone = 0.22f;

        // Knob "musuh terkutuk" sudah dipindah ke aset EnemyArchetype — kapan muncul, seberapa
        // sering, sebesar apa, dan kutukan mana, semuanya milik arketipe sekarang. Dua mekanisme
        // varian yang jalan bersamaan cuma bikin satu di antaranya jadi kebohongan.

        [Header("Gerak musuh")]
        [Tooltip("Jarak musuh saling mendorong. Ini yang mencegah mereka berbaris jadi ular: tanpa " +
                 "ini semua menempuh jalur yang sama persis ke titik yang sama persis.")]
        public float EnemySeparation = 1.3f;

        public float SeparationWeight = 1.7f;

        [Tooltip("Berapa detik ke depan musuh membidik posisi pemain. 0 = selalu mengejar buntut " +
                 "dan tidak pernah memotong jalan, yang membuat pemain bisa di-kiting selamanya.")]
        public float InterceptLead = 1.1f;

        [Tooltip("Seberapa lebar musuh memutar sebelum menutup. Ini yang mengubah kejar-kejaran " +
                 "jadi pengepungan — nol berarti semuanya menuju satu titik yang sama.")]
        public float FlankWidth = 6f;

        [Tooltip("Di bawah jarak ini musuh berhenti memutar dan langsung menerjang.")]
        public float FlankFade = 5f;

        [Tooltip("Batas musuh hidup bersamaan.")]
        public int MaxAliveEnemies = 200;

        [Header("Kurva wave")]
        [Tooltip("Pengali BERANAK per wave untuk HP musuh. 1 = lurus.")]
        [Min(1f)] public float EnemyHpGrowth = 1.05f;

        [Tooltip("Tiap berapa wave sekali datang gelombang besar. 0 = mati.")]
        public int SurgeEveryWaves = 5;

        public float SurgeHpMultiplier = 1.15f;

        public bool IsSurgeWave(int wave) =>
            SurgeEveryWaves > 0 && wave > 0 && wave % SurgeEveryWaves == 0;

        /// <summary>
        /// HP musuh: linear + beranak.
        ///
        /// Yang linear murni pada akhirnya kalah: kekuatan build pemain tumbuh secara PERKALIAN
        /// (aura rune, buff, evolusi), jadi tanjakan yang cuma menambah angka tetap lama-lama
        /// menjadi turunan. Bagian yang beranak inilah yang menahannya.
        /// </summary>
        public float EnemyHpFor(int wave)
        {
            float hp = (EnemyHpBase + wave * EnemyHpPerWave) * Mathf.Pow(EnemyHpGrowth, wave);
            if (IsSurgeWave(wave)) hp *= SurgeHpMultiplier;
            return hp;
        }

        [Header("Efek cast")]
        [Tooltip("Jarak maksimum satu lompatan rantai petir, dari musuh ke musuh berikutnya. " +
                 "Lompatan PERTAMA tetap memakai Range skill-nya.")]
        public float ChainJumpRange = 5.5f;

        [Tooltip("Ketinggian awal skill yang jatuh dari langit.")]
        public float SkyFallHeight = 14f;

        [Tooltip("Kecepatan jatuhnya. Damage baru meletus pas menyentuh tanah, jadi angka ini " +
                 "menentukan berapa lama musuh sempat bergeser dari titik bidik.")]
        public float SkyFallSpeed = 52f;

        [Tooltip("Pengali harga mana SELURUH skill. Satu knob, bukan 74 aset.\n\n" +
                 "Ada karena harga di aset ditulis relatif satu sama lain — Fireball 1, Meteor " +
                 "24 — dan perbandingan itu SUDAH benar; yang salah skalanya terhadap regen. " +
                 "Dua skill pembuka menghabiskan 1,74 mana/detik melawan regen 5/detik, jadi " +
                 "mana tidak pernah bergerak sedikit pun sepanjang run.\n\n" +
                 "Menaikkan harga di tiap aset akan menghapus perbandingan yang sudah benar itu " +
                 "dan menuntut 74 keputusan ulang. Angka di sini menggeser semuanya sekaligus " +
                 "tanpa mengubah satu pun perbandingan.\n\n" +
                 "Rune dan segel berharga nol dan tetap nol — nol dikali berapa pun tetap nol, " +
                 "jadi yang pasif tidak pernah ikut terseret.")]
        [Min(0f)] public float ManaCostScale = 3f;

        [Header("Loot")]
        [Range(0f, 1f)] public float KillDropChance = 0.04f;
        public int WaveClearDrops = 1;
        [Range(0f, 1f)] public float RuneShareOfDrops = 0.25f;

        [Tooltip("Bobot bintang drop, indeks 0 = ★1. Dinormalkan sendiri, totalnya bebas.\n\n" +
                 "★5 dipatok NOL dan itu aturan desain: bintang lima hanya lahir dari resep — " +
                 "puncak menara harus dibangun, bukan dipungut. ★4 boleh jatuh tapi langka.")]
        public float[] DropStarWeights = { 82f, 12f, 4.5f, 1.5f, 0f };

        [Tooltip("Wave paling awal tiap bintang boleh JATUH, indeks 0 = ★1.\n\n" +
                 "Bobot saja tidak cukup, dan itu terbukti dipakai: ★4 berbobot 1,5 tetap bisa " +
                 "keluar di wave 1, dan satu skill bintang empat di wave pertama menyelesaikan " +
                 "seluruh act itu sendirian. Yang salah bukan seberapa SERING — 1,5 memang " +
                 "langka — melainkan bahwa peluangnya ADA sama sekali sebelum ada yang dibangun.\n\n" +
                 "Bintang yang belum waktunya bobotnya dianggap nol lalu sisanya dinormalkan " +
                 "ulang, jadi drop awal tidak berkurang jumlahnya — cuma turun mutunya.")]
        public int[] DropStarMinWave = { 1, 1, 5, 11, 99 };

        [Tooltip("Batas drop dari kill yang ditahan sampai wave beres. Kelebihannya langsung jadi " +
                 "koin — tanpa ini wave besar menumpahkan dua puluh item sekaligus ke layar.")]
        public int MaxDropsPerWave = 8;

        [Header("Hadiah elite")]
        // Sebelum ini node elite cuma LEBIH KERAS: nyawa, jumlah, dan gigitan dikalikan, hadiahnya
        // sama persis dengan pertarungan biasa. Itu bukan pilihan melainkan jebakan — tidak ada
        // alasan menginjaknya kecuali kepepet jalur. Tiga angka di bawah yang membuatnya jadi
        // taruhan: lebih banyak, lebih bagus, dan lebih berkoin.

        [Tooltip("Drop tambahan saat wave elite beres, di luar WaveClearDrops.")]
        [Min(0)] public int EliteBonusDrops = 2;

        [Tooltip("Kenaikan bintang untuk SEMUA drop dari wave elite.\n\n" +
                 "Ini bagian hadiah yang paling terasa, dan sekaligus yang paling perlu hati-hati: " +
                 "batas wave per bintang tetap berlaku sesudah kenaikan ini, jadi elite di wave 2 " +
                 "tidak bisa menyelundupkan ★3 lewat pintu belakang.")]
        [Range(0, 2)] public int EliteStarBonus = 1;

        [Tooltip("Koin tambahan saat wave elite beres.")]
        [Min(0)] public int EliteCoinBonus = 45;

        [Header("Toko")]
        public int ShopEveryWaves = 3;
        public int ShopSlots = 6;
        public int RerollCostStart = 20;
        public int RerollCostIncrement = 15;
        [Range(0f, 1f)] public float ShopHighRollChance = 0.18f;

        [Header("Peta Run")]
        [Tooltip("Jumlah lantai per act; boss di puncak. Nomor wave global terus berjalan " +
                 "lintas act, jadi kurva kesulitan lama tidak tersentuh.")]
        [Min(3)] public int MapFloorsPerAct = 15;

        [Range(2, 5)] public int MapLanes = 3;

        [Tooltip("Peluang jenis node istimewa per node — sisanya pertarungan biasa.")]
        [Range(0f, 1f)] public float MapEliteChance = 0.15f;
        [Range(0f, 1f)] public float MapShopChance = 0.12f;
        [Range(0f, 1f)] public float MapEventChance = 0.1f;
        [Range(0f, 1f)] public float MapGambleChance = 0.08f;

        [Tooltip("Elite baru boleh muncul dari lantai ini. Elite di lantai awal bukan " +
                 "pilihan melainkan tembok.\n\n" +
                 "Dinaikkan 3 -> 5 setelah laporan pemilik project: elite di node bawah " +
                 "\"langsung mati\". Tiga terlalu dini — di lantai 3 papan pemain masih " +
                 "berisi piece pembuka, dan wave elite yang 42% lebih berat tidak punya " +
                 "jawaban di papan seukuran itu. Lima memberi dua lantai lagi untuk " +
                 "mengumpulkan piece sebelum tembok pertama muncul.")]
        public int MapEliteMinFloor = 5;

        [Tooltip("Peta langsung terbuka begitu run mulai, tanpa fase SUSUN GRIMOIRE-MU di depan.\n\n" +
                 "Masuk akal sejak papan pembuka ditentukan di layar pilih starter: fase susun " +
                 "pertama jadi layar yang tidak menanyakan apa-apa, karena keputusannya sudah " +
                 "dibuat satu layar sebelumnya.\n\n" +
                 "Matikan untuk kembali ke pembuka lama. Fase susun ANTAR wave tidak dipengaruhi " +
                 "nilai ini — yang dilewati hanya yang paling pertama.")]
        public bool MapOpensRun = true;

        [Header("Transisi peta (gloom)")]
        [Tooltip("Lama kegelapan MENUTUP sebelum peta terbuka, detik. Gloom mengecil dari " +
                 "nilai standarnya sampai menelan seluruh layar.")]
        [Min(0.1f)] public float MapFadeClose = 1.1f;

        [Tooltip("Lama kegelapan MEMBUKA kembali ke nilai standar setelah node dieksekusi.")]
        [Min(0.1f)] public float MapFadeOpen = 1.4f;

        [Tooltip("Lama penanda pemain berjalan di peta menuju node yang dipilih.")]
        [Min(0.1f)] public float MapMarkerTravel = 0.8f;

        [Header("Elite & boss puncak")]
        [Tooltip("Pengali wave ELITE: keras lewat nyawa, jumlah, dan gigitan — bukan lewat " +
                 "kecepatan, karena kecepatan mengubah rasa kontrol, bukan tingkat bahaya.")]
        public float EliteHpMul = 2.2f;

        public float EliteCountMul = 1.25f;

        public float EliteDamageMul = 1.3f;

        [Tooltip("Peluang sebuah elite berisi MINI-BOSS (satu boss menumpang wave biasa) " +
                 "alih-alih sekadar wave yang dikeraskan.")]
        [Range(0f, 1f)] public float EliteBossChance = 0.4f;

        [Tooltip("Boss puncak act 1: berapa ekor sekaligus. Act berikutnya menambah satu " +
                 "per act sampai mentok jumlah jenis boss yang ada.")]
        public int BossNodeCount = 2;

        [Tooltip("Pengali nyawa boss puncak. Puncak harus TEBAL — yang diuji bukan refleks " +
                 "melainkan seluruh mesin damage yang dirakit sepanjang act.")]
        public float BossNodeHpMul = 2.5f;

        [Tooltip("Pengali agresi boss puncak: jeda serangannya dibagi angka ini.")]
        public float BossNodeAggroMul = 1.6f;

        [Header("Judi (slot)")]
        public int GambleCost = 40;

        [Tooltip("Bobot hasil slot, urutannya: kosong / koin kecil / koin besar / " +
                 "piece ★2 / piece ★3 / piece ★4. Dinormalkan sendiri.")]
        public float[] GambleWeights = { 30f, 25f, 15f, 18f, 9f, 3f };

        public int GambleSmallGold = 25;

        public int GambleBigGold = 90;

        [Header("Kejadian")]
        [Tooltip("Koin dari pilihan aman kejadian.")]
        public int EventGoldGift = 80;

        [Tooltip("Harga pilihan tukar nasib — bayar ini, dapat piece ★3 acak.")]
        public int EventTradeCost = 45;

        [Header("Harga")]
        public int PriceRune = 20;
        public int PriceSigil = 35;
        public int PriceStar1 = 30;
        public int PriceHighBase = 40;
        public int PriceHighPerStar = 45;

        [Header("Harga jual")]
        public int SellRune = 10;
        public int SellSkill = 15;

        public int PriceOf(PieceDefinition piece)
        {
            if (piece == null) return 0;
            if (piece.IsRune) return PriceRune;
            if (piece.IsPassive) return PriceSigil;
            return piece.Stars <= 1 ? PriceStar1 : PriceHighBase + piece.Stars * PriceHighPerStar;
        }

        public int SellValueOf(PieceDefinition piece)
        {
            if (piece == null) return 0;
            return piece.IsRune ? SellRune : SellSkill;
        }
    }
}
