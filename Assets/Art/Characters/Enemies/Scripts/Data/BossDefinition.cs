using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Boss ular: satu makhluk panjang yang mengitari pemain, sesekali menerjang menggigit, dan
    /// MEMENDEK seiring HP-nya habis.
    ///
    /// Badan yang memendek itu bukan hiasan — itu satu-satunya bar HP yang dimiliki boss ini.
    /// Pemain tidak perlu membaca angka di sudut layar untuk tahu progresnya; panjang ularnya
    /// sendiri yang mengatakannya, dan itu terbaca dari mana pun mata sedang tertuju.
    /// </summary>
    [CreateAssetMenu(fileName = "Boss_", menuName = "Grimoire/Boss")]
    public class BossDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName = "Serpent";

        [Tooltip("Act pertama tempat boss ini boleh tampil di PUNCAK act. 1 (atau 0 di aset " +
                 "lama) = bebas sejak awal. Di atas 1 = boss pamungkas: tidak pernah ikut " +
                 "undian mini-boss elite maupun boss kelipatan-wave, dan di puncak yang " +
                 "memenuhi syarat ia tampil paling depan — naga adalah penutup run, bukan " +
                 "kejutan act satu (laporan pemilik project: \"ketemu naga di bawah\").")]
        public int SummitMinAct = 1;

        [Header("Tubuh")]
        [Tooltip("Ruas badan saat HP penuh, di luar kepala. Ini juga panjang bar HP-nya.")]
        [Min(3)] public int MaxSegments = 22;

        [Tooltip("Ruas paling sedikit yang tersisa menjelang mati. Di bawah tiga, bentuk ularnya " +
                 "hilang dan yang tersisa cuma segumpal bola.")]
        [Min(2)] public int MinSegments = 4;

        [Tooltip("Jarak antar ruas dalam unit dunia. Terlalu rapat = terlihat seperti satu batang; " +
                 "terlalu renggang = terlihat seperti barisan bola yang tidak berhubungan.")]
        public float Spacing = 1.05f;

        [Tooltip("Ukuran kepala.\n\nUntuk yang BERSAYAP ini ukuran seluruh badannya, dalam " +
                 "unit dunia — modelnya diskalakan sendiri supaya setinggi angka ini berapa pun " +
                 "ukuran aslinya kebetulan diekspor.")]
        public float HeadScale = 2.4f;

        [Tooltip("Ukuran ruas terakhir. Yang di antaranya diinterpolasi, jadi badannya meruncing.")]
        public float TailScale = 0.9f;

        [Header("Model")]
        [Tooltip("Mesh kepala. Dikosongkan = boss ini digambar dengan kapsul seperti sebelumnya, " +
                 "dan dua mesh di bawah ikut diabaikan.")]
        public Mesh HeadMesh;

        [Tooltip("Mesh SATU ruas badan. Diulang sebanyak ruas yang sedang hidup, jadi bentuknya " +
                 "harus menyambung ke dirinya sendiri di kedua ujung.")]
        public Mesh BodyMesh;

        [Tooltip("Mesh penutup di ujung ekor.")]
        public Mesh TailMesh;

        [Tooltip("Tekstur warna untuk ketiga mesh di atas. UV-nya keluar dari rentang 0..1, jadi " +
                 "importer teksturnya HARUS Wrap Mode = Repeat, bukan Clamp.")]
        public Texture2D BoneSkin;

        [Tooltip("Pembetulan ukuran ASET kepala — berapa kali mesh mentahnya harus dibesarkan. " +
                 "Terpisah dari HeadScale, yang mengatur ukuran yang dilihat pemain.")]
        public float HeadMeshScale = 12f;

        [Tooltip("Pembetulan ukuran aset badan DAN ekor. Keduanya sengaja dipotong sama panjang, " +
                 "jadi satu angka cukup untuk dua-duanya.")]
        public float BodyMeshScale = 21f;

        [Header("Koreksi orientasi model")]
        // Model tidak selalu diekspor menghadap ke arah yang diasumsikan kode. Kode menganggap
        // +Z = arah jalan dan +Y = atas; SnakeBoss dibuat BERDIRI (dilihat kamera atas ia tampil
        // dari samping) dan kepalanya menghadap berlawanan dengan ekornya.
        //
        // Dibiarkan sebagai DATA, bukan dipatok di kode: begitu art ditukar, yang perlu diubah
        // cuma tiga angka di aset ini — dan tiga slot terpisah karena tidak ada jaminan pengekspor
        // meletakkan ketiga mesh menghadap arah yang sama. Terbukti di aset pertama: memang tidak.

        [Tooltip("Koreksi rotasi mesh KEPALA, dalam derajat. Dipasang sebelum arah hadapnya.")]
        public Vector3 HeadMeshRotation;

        [Tooltip("Koreksi rotasi mesh RUAS BADAN.")]
        public Vector3 BodyMeshRotation;

        [Tooltip("Koreksi rotasi mesh EKOR.")]
        public Vector3 TailMeshRotation;

        [Tooltip("Kecepatan GULINGAN badan di sumbu jalannya sendiri, derajat per detik. " +
                 "0 = tidak berguling (bawaan, dan benar untuk ular).\n\n" +
                 "Kepala berguling ke arah SEBALIKNYA dengan kecepatan yang sama. Itu yang " +
                 "membedakan cacing dari ular: ular meliuk di bidang datar, cacing MENGEBOR — " +
                 "dan yang membaca sebagai mengebor bukan gulingannya sendiri melainkan " +
                 "perlawanan antara kepala dan badan.")]
        public float SpinDegreesPerSecond;

        // =====================================================================================
        //  naga: satu badan bersayap, bukan barisan ruas
        // =====================================================================================
        //
        // Boss di file ini semula selalu makhluk beruas — kepala yang berpikir, badan yang
        // menapaki jejaknya, dan panjang badan itulah bar HP-nya. Naga mematahkan ketiganya
        // sekaligus: badannya satu, ia tidak punya ruas untuk dilepas, dan HP-nya tidak bisa
        // dibaca dari bentuknya.
        //
        // Dibuat sebagai SAKLAR DI ASET, bukan kelas boss kedua, karena yang tidak berubah jauh
        // lebih banyak daripada yang berubah: kolam HP, ruas yang terdaftar sebagai musuh biasa
        // (jadi tiap skill yang bisa mengenai musuh otomatis bisa mengenainya), undian acak, node
        // puncak act, pakta, agresi, bar HP, dan upacara kedatangan semuanya dipakai ulang apa
        // adanya. Yang benar-benar baru cuma cara ia bergerak dan cara ia menyerang.

        public enum BossBody
        {
            /// <summary>Ular, kelabang, cacing — kepala plus ruas yang menapaki jejaknya.</summary>
            Segmented = 0,

            /// <summary>Naga — satu badan beranimasi yang melintas di atas arena.</summary>
            Winged = 1,
        }

        [Header("Bentuk badan")]
        [Tooltip("Beruas = ular/kelabang seperti sebelumnya, memakai tiga slot mesh di atas. " +
                 "Bersayap = naga: satu badan, model panggangan VAT, terbang.")]
        public BossBody Body = BossBody.Segmented;

        [Header("Bersayap: model")]
        [Tooltip("Model terpanggang (Tools/Grimoire/Bake Enemy VAT). WAJIB untuk yang bersayap — " +
                 "tanpa ini ia jatuh kembali ke kapsul.\n\n" +
                 "Perannya dibaca dari kecepatan: Idle = melayang di tempat, Walk = jelajah " +
                 "pelan, Run = jelajah cepat, Attack = menyembur. Panggang dengan pemetaan klip " +
                 "TANGAN — tebakan nama tidak punya kosakata untuk terbang, dan akan memilih " +
                 "klip jalan kaki.")]
        public VatClipSet Vat;

        [Header("Bersayap: terbang")]
        [Tooltip("Ketinggian jelajah. Cukup tinggi supaya tidak tenggelam di antara gerombolan, " +
                 "cukup rendah supaya masih terbaca sebagai ancaman dan bukan hiasan langit.")]
        public float FlyHeight = 7f;

        [Tooltip("Panjang satu lintasan mondar-mandir, ujung ke ujung.\n\n" +
                 "Ia melintas MELEWATI pemain, bukan berhenti di atasnya: yang menggantung di " +
                 "atas kepala tidak memberi pemain apa pun untuk dihindari.")]
        public float StrafeWidth = 18f;

        [Tooltip("Seberapa cepat ia mengejar ketinggian jelajahnya, unit per detik.")]
        public float ClimbRate = 4f;

        [Header("Bersayap: semburan api")]
        [Tooltip("Jeda antar semburan. Nol = tidak menyembur sama sekali.")]
        public float BreathInterval = 6f;

        [Tooltip("Lama satu semburan. Samakan dengan panjang klip Attack-nya — kalau lebih " +
                 "panjang, animasinya berputar ulang di tengah semburan dan terlihat tersendat.")]
        public float BreathDuration = 3f;

        [Tooltip("Jeda antara animasi mulai dan api benar-benar melukai.\n\n" +
                 "Ini telegrafnya, dan tanpa itu semburannya bukan sesuatu yang bisa dihindari — " +
                 "cuma sesuatu yang diterima.")]
        public float BreathWindup = 0.7f;

        [Tooltip("Damage PER DETIK selama pemain berada di dalam kerucut.\n\n" +
                 "Bukan satu hantaman: yang membuat napas api terbaca sebagai napas api adalah " +
                 "harga yang terus naik selama pemain masih berdiri di dalamnya.")]
        public float BreathDamage = 20f;

        [Tooltip("Sejauh apa DI DEPAN naga pemain harus berada supaya semburan dipicu. " +
                 "Bukan lagi panjang kerucut — naga menyembur SAMBIL melintas, dan angka ini " +
                 "menentukan seberapa awal ia mulai membakar jalur sebelum melewati pemain.")]
        public float BreathRange = 15f;

        [Tooltip("Jari-jari sapuan api di TANAH. Pemain yang berdiri sedekat ini dari titik " +
                 "jatuh apinya terbakar. Juga lebar coretan gosong yang ditinggalkan.")]
        public float BreathGroundRadius = 3.2f;

        [Tooltip("SETENGAH sudut kerucutnya, dalam derajat. Yang terlalu lebar tidak bisa " +
                 "dikeluari; yang terlalu sempit tidak pernah kena.")]
        public float BreathAngle = 24f;

        [Tooltip("VFX api yang menyala selama menyembur.\n\n" +
                 "Instansnya ikut BERGERAK dengan mulutnya, jadi pakai yang simulation space-nya " +
                 "Local — partikel World-space tertinggal di belakang naga yang sedang melaju, " +
                 "dan terlihat copot dari mulutnya.")]
        public GameObject BreathVfx;

        [Tooltip("Letak mulut, sebagai PECAHAN dari tinggi naga. Z = ke depan, Y = ke atas.\n\n" +
                 "Pecahan, bukan unit dunia, supaya apinya tetap keluar dari mulut berapa pun " +
                 "HeadScale disetel. Offset yang dipatok dalam unit dunia akan tenggelam ke dalam " +
                 "dada begitu naganya dibesarkan — dan yang membesarkannya tidak punya alasan " +
                 "untuk menduga angka mulut ikut harus diubah.")]
        public Vector3 BreathMuzzle = new Vector3(0f, 0.6f, 0.39f);

        [Tooltip("Ukuran VFX apinya.")]
        public float BreathVfxScale = 3f;

        [Tooltip("Jeda dari semburan mulai sampai VFX apinya menyala, dalam detik.\n\n" +
                 "Klip Attack tidak langsung membuka mulut di frame pertama — ia menarik kepala " +
                 "ke belakang dulu. Api yang menyala bersamaan dengan awal klip keluar dari " +
                 "moncong yang masih terkatup.\n\n" +
                 "Samakan dengan BreathWindup supaya apinya muncul tepat saat ia mulai melukai; " +
                 "itu juga membuat yang TERLIHAT dan yang MENYAKITI jadi satu kejadian, sehingga " +
                 "pemain tidak pernah terbakar oleh api yang belum kelihatan.")]
        public float BreathVfxDelay = 0.7f;

        [Tooltip("Koreksi rotasi VFX api, dalam derajat. Dipasang SESUDAH arah hadapnya.\n\n" +
                 "Ada karena tidak ada kesepakatan soal ke mana sebuah efek api menyala. Kode " +
                 "mengarahkan +Z ke arah semburan, tapi paket 2D Fire VFX menyalakan apinya ke " +
                 "+Y — dipasang mentah, naganya menyalakan lilin di atas kepalanya alih-alih " +
                 "menyembur ke depan.\n\n" +
                 "90 di sumbu X memutar +Y jadi ke depan, dan itu benar untuk paket Cartoon " +
                 "Coffee. Dibiarkan sebagai DATA karena paket berikutnya belum tentu sama, dan " +
                 "yang menukar VFX-nya tidak boleh perlu membuka kode untuk tahu kenapa apinya " +
                 "menghadap ke arah yang salah.")]
        public Vector3 BreathVfxRotation = new Vector3(90f, 0f, 0f);

        [Header("Nyawa")]
        [Tooltip("Dikali HP musuh biasa di wave itu. Boss harus bertahan cukup lama untuk sempat " +
                 "menunjukkan perilakunya, bukan cuma HP besar yang berdiri diam.")]
        public float HpMultiplier = 90f;

        [Header("Gerak")]
        [Tooltip("Jari-jari lingkaran saat mengitari pemain.")]
        public float OrbitRadius = 13f;

        [Tooltip("Kecepatan jelajah kepala.")]
        public float Speed = 6.5f;

        [Tooltip("Kecepatan saat menerjang.")]
        public float LungeSpeed = 15f;

        [Tooltip("Seberapa tajam kepala boleh membelok, derajat per detik. Belokan yang terlalu " +
                 "tajam membuat badannya melipat ke dalam dirinya sendiri.")]
        public float TurnRate = 150f;

        [Tooltip("Seberapa liar jalurnya meliuk saat sedang tidak menerjang. Ini yang bikin dia " +
                 "terbaca sebagai makhluk hidup, bukan sebagai benda yang berputar di rel.")]
        public float Wander = 1.4f;

        [Header("Serangan")]
        [Tooltip("Detik antar terjangan.")]
        public float LungeInterval = 6f;

        [Tooltip("Berapa lama satu terjangan berlangsung.")]
        public float LungeDuration = 1.8f;

        [Tooltip("Damage gigitan. Satu hantaman, bukan per detik.")]
        public float BiteDamage = 26f;

        [Tooltip("Jarak kepala ke pemain yang dihitung sebagai gigitan.")]
        public float BiteRange = 2.6f;

        [Tooltip("Kutukan yang ditempelkan gigitannya. Boleh kosong.")]
        public BuffDefinition Curse;

        // =====================================================================================
        //  kelabang: menyelam ke tanah lalu menyembur keluar
        // =====================================================================================
        //
        // Ini bukan boss kedua yang ditulis dari nol. Badannya sudah menapaki jejak kepalanya, dan
        // jejak itu menyimpan KETINGGIAN juga — jadi begitu kepalanya menukik ke bawah tanah lalu
        // melengkung naik, seluruh badannya mengikuti busur itu sendiri, satu per satu, persis
        // seperti cacing yang menyembur. Tidak ada satu baris pun animasi badan yang perlu ditulis.

        [Header("Menyelam (kelabang)")]
        [Tooltip("Nyalakan untuk boss yang hidup DI BAWAH tanah dan cuma melompat keluar sesekali.")]
        public bool Burrows;

        [Tooltip("Lama SATU lompatan busur, dari menyembul sampai menukik masuk lagi.\n\n" +
                 "Pendek dengan sengaja. Kepala yang bertahan lama di atas tanah tidak terbaca " +
                 "sebagai melompat — terbaca sebagai berjalan-jalan di permukaan.")]
        public float ArcDuration = 1.1f;

        [Tooltip("Setinggi apa lompatannya. Inilah satu-satunya saat ia bisa dipukul.")]
        public float ArcHeight = 4.5f;

        [Tooltip("Berapa kali melompat berturut-turut sebelum menyelam dalam.\n\n" +
                 "Ini yang membuatnya terbaca seperti lumba-lumba: bukan satu lompatan lalu hilang, " +
                 "tapi dua-tiga lompatan beruntun sambil menembus, baru menghilang.")]
        [Min(1)] public int BreachBurst = 3;

        [Tooltip("Celupan DANGKAL di antara lompatan beruntun. Bukan menyelam penuh.")]
        public float DipDuration = 0.45f;

        [Tooltip("Detik ia menghilang dalam-dalam setelah rentetan lompatannya habis. " +
                 "Selama terbenam ia KEBAL — itu harga yang dibayar pemain karena tak bisa mengejar.")]
        public float DiveInterval = 5.5f;

        [Tooltip("Sedalam apa ia menyelam. Harus lebih dalam dari ambang terbenam.")]
        public float DiveDepth = 5f;

        [Header("Semburan racun")]
        [Tooltip("Jeda antar semburan saat berada di permukaan. 0 = tidak menyembur.")]
        public float SpitInterval = 0.85f;

        public float SpitDamage = 14f;

        public float SpitSpeed = 11f;

        [Tooltip("Kutukan yang ditempelkan semburannya ke PEMAIN. Ailment tinggal di musuh; " +
                 "pemain punya jalur debuff-nya sendiri, dan mencampur keduanya cuma bikin satu " +
                 "di antaranya jadi kebohongan.")]
        public BuffDefinition SpitCurse;

        [Header("Anak buah")]
        [Tooltip("Nyalakan untuk versi KECIL: muncul di wave biasa, tidak mengumumkan diri, dan " +
                 "tidak menampilkan bar HP boss.\n\n" +
                 "Seluruh sistemnya dipakai ulang apa adanya — ruas, jejak, menyelam, semburan. " +
                 "Yang membedakan boss dari anak buah cuma ukurannya dan apakah layar berteriak " +
                 "saat ia datang.")]
        public bool Minion;

        [Tooltip("Khusus anak buah: mulai muncul dari wave ini.")]
        [Min(1)] public int MinionFromWave = 6;

        [Tooltip("Khusus anak buah: berapa ekor per wave. Bertambah pelan seiring wave.")]
        [Min(1)] public int MinionCount = 2;

        [Header("Warna")]
        public Color HeadColor = new Color(0.85f, 0.25f, 0.35f);
        public Color BodyColor = new Color(0.45f, 0.18f, 0.3f);

        // Emisi, dan HDR-nya bukan hiasan.
        //
        // Ambang bloom di seluruh look profile ada di 0,8–1,25. Warna emisi bernilai 1,0 tidak
        // akan pernah menyala — ia cuma jadi lebih terang. Yang MENYALA harus lewat ambang itu,
        // dan satu-satunya cara adalah HDR di atas satu. Karena itu pickernya dibuka HDR.
        //
        // Hitam = mati, jadi boss lama tidak berubah tanpa disentuh.
        [Tooltip("Pendar kepala. Hitam = tidak menyala. Perlu HDR di atas 1 supaya kena bloom.")]
        [ColorUsage(false, true)] public Color HeadEmission = Color.black;

        [Tooltip("Pendar ruas badan dan ekor.")]
        [ColorUsage(false, true)] public Color BodyEmission = Color.black;

        // Nama file mesh, bukan referensi mesh.
        //
        // Referensinya sendiri sudah ada di HeadMesh/BodyMesh/TailMesh — tapi BossModelPass
        // menimpanya untuk SEMUA boss tiap kali dijalankan, jadi boss yang memakai model lain
        // akan kehilangan modelnya pada klik menu berikutnya. Yang bertahan harus berupa niat
        // yang tersimpan di aset, bukan hasil yang tersimpan di aset.
        //
        // Kosong = pakai file bawaan. Tanpa ekstensi.
        [Header("Model: berkas khusus (opsional)")]
        [Tooltip("Nama file mesh kepala tanpa .fbx, mis. SK_Snake_Head_Horned. Kosong = bawaan.")]
        public string HeadMeshFile;

        [Tooltip("Nama file mesh ruas badan tanpa .fbx. Kosong = bawaan.")]
        public string BodyMeshFile;

        [Tooltip("Nama file mesh ekor tanpa .fbx. Kosong = bawaan.")]
        public string TailMeshFile;

        [Tooltip("Nama file tekstur tanpa .png, mis. SnakeBoss_Albedo_Charred. Kosong = bawaan.")]
        public string BoneSkinFile;

        void OnValidate()
        {
            if (string.IsNullOrEmpty(Id)) Id = name.ToLowerInvariant().Replace("boss_", "");
            if (MinSegments > MaxSegments) MinSegments = MaxSegments;
            if (Spacing < 0.3f) Spacing = 0.3f;
        }
    }
}
