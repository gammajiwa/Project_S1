using TMPro;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Satu tempat untuk semua BAHAN tampilan UI: kertas, bingkai, dan warna tinta di atasnya.
    ///
    /// Alasan ia jadi aset, bukan konstanta di <see cref="GrimoireLayout"/>: yang di sana itu
    /// GEOMETRI — piksel, jarak, kotak — dan geometri tidak bisa dilihat hasilnya sampai
    /// dijalankan. Yang di sini BAHAN, dan bahan dinilai dengan mata. Menaruhnya di aset berarti
    /// bisa ditukar sambil play mode berjalan.
    ///
    /// Kosongkan sprite mana pun untuk kembali ke kotak warna datar seperti sebelum ada art —
    /// tiap pemakainya wajib tetap jalan dengan sprite null, supaya art yang belum jadi tidak
    /// pernah memblokir tes gameplay.
    /// </summary>
    [CreateAssetMenu(fileName = "UiTheme", menuName = "Grimoire/UI Theme")]
    public class UiTheme : ScriptableObject
    {
        // ------------------------------------------------------------------ kertas & bingkai

        [Header("Kertas & bingkai")]
        [Tooltip("Perkamen latar peta. Dipakai peta besar (pemilih, satu layar penuh) DAN peta " +
                 "kecil (intip lewat M). Kosong = latar biru-gelap seperti sebelumnya.")]
        public Sprite MapPaper;

        [Tooltip("Sigil yang berputar di tirai layar muat saat pindah scene. Kosong = tirai " +
                 "polos tanpa sigil — tetap menutup, cuma tidak bercerita apa-apa.")]
        public Sprite LoadingSigil;

        [Tooltip("Perkamen latar panel SINGGAH: toko, kejadian, dan slot. Ketiganya berbagi satu " +
                 "badan panel, jadi satu sprite mengurus ketiganya.\n\n" +
                 "Kosong = ikut MapPaper. Kalau MapPaper juga kosong, kembali ke kotak warna " +
                 "datar seperti sebelum art masuk.")]
        public Sprite PanelPaper;

        [Header("Font")]
        [Tooltip("Font legacy — SATU-SATUNYA pemakainya label 3D dunia di pulau singgah " +
                 "(TextMesh milik RunDirector). Seluruh UI kanvas sudah TextMeshPro dan " +
                 "membaca TmpFont; slot ini tidak menyentuh UI mana pun lagi.")]
        public Font UiFont;

        [Tooltip("Font TMP seluruh teks HUD combat yang lahir dari kode — aturan pemilik " +
                 "project: SEMUA teks TextMeshPro, tidak ada lagi UI Text legacy. " +
                 "Kosong = font bawaan TMP.")]
        public TMP_FontAsset TmpFont;

        [Tooltip("Font TMP ANGKA damage melayang. Dipisah dari TmpFont dengan sengaja: popup " +
                 "damage hidup 0,75 detik di atas layar yang penuh, dan yang dibaca di situ " +
                 "bukan kata melainkan besaran. Berat yang lebih tebal terbaca lebih cepat.\n\n" +
                 "Kosong = ikut TmpFont.")]
        public TMP_FontAsset TmpNumberFont;

        [Tooltip("PREFAB papan grimoire — cara yang dipakai sekarang. Isinya bebas: sampul, " +
                 "hiasan, apa pun.\n\n" +
                 "Aturan mainnya satu, dan penting: kode HANYA menempelkannya ke kanvas di " +
                 "posisi papan. Ukuran, anchor, dan susunan anaknya TIDAK pernah disentuh — " +
                 "apa yang kamu atur di prefab itulah yang tampil. Petak papan digambar di " +
                 "atasnya oleh kode, jadi biarkan bagian tengahnya kosong.")]
        public GameObject GrimoirePanelPrefab;

        [Tooltip("Jalur lama: satu sprite yang DIPELARKAN ke kotak hitungan. Dipakai hanya " +
                 "kalau GrimoirePanelPrefab kosong. Melar itu justru yang bikin gambarnya " +
                 "kelihatan ketarik, makanya prefab yang jadi jalur utama.")]
        public Sprite GrimoireFrame;

        [Tooltip("PREFAB bar HP & mana, membawa komponen VitalsRig.\n\n" +
                 "Aturannya sama dengan papan grimoire: kode cuma mengisi fillAmount dan teks. " +
                 "Letak, ukuran, sprite, dan ARAH ISIAN — mendatar, menegak, atau memutar untuk " +
                 "bola — semuanya milik prefab.\n\n" +
                 "Kosong = bar kotak datar bawaan seperti sebelum art masuk.")]
        public GameObject VitalsPrefab;

        [Tooltip("PREFAB penempat tiga strip ikon (buff, kutukan, tally ailment), membawa " +
                 "komponen StatusStripRig.\n\n" +
                 "Isinya cuma kotak kosong — ikonnya tetap digambar kode. Yang dipindahkan " +
                 "prefab ini adalah LETAKNYA, dan itu perlu sejak bar HP jadi bola: tempat lama " +
                 "ketiga strip dipilih waktu barnya masih kotak setinggi 18 piksel, dan bola " +
                 "setinggi 190 piksel menelan ketiganya.\n\n" +
                 "Kosong = ketiganya kembali ke tempat lama di GrimoireLayout.")]
        public GameObject StatusStripsPrefab;

        [Tooltip("PREFAB halaman setelan, disimpan otomatis oleh Tools/Grimoire/Build Main Menu. " +
                 "Dipakai scene game untuk membuka setelan lewat ESC — satu panel untuk dua " +
                 "scene, supaya keduanya tidak pernah saling menyimpang.\n\n" +
                 "Kosong = ESC kembali ke perilaku lama (langsung pulang ke menu).")]
        public GameObject SettingsPrefab;

        [Tooltip("PREFAB penempat panel singgah (toko/kejadian/slot), membawa komponen ShopRig.\n\n" +
                 "Isinya kotak-kotak kosong: badan panel, enam slot dagangan, REROLL, dan tempat " +
                 "tombol LANJUT. Isi panelnya tetap digambar kode — yang kamu tata LETAKNYA.\n\n" +
                 "Kosong = tata letak hitungan lama.")]
        public GameObject ShopPrefab;

        [Tooltip("PREFAB penempat panel KEJADIAN, membawa komponen EventRig.\n\n" +
                 "Kotak-kotak kosong: badan panel, dua kartu pakta, tombol MENOLAK. Isi panelnya " +
                 "tetap digambar kode — yang kamu tata LETAKNYA.\n\nKosong = tata letak hitungan lama.")]
        public GameObject EventPrefab;

        [Tooltip("Latar BADAN panel kejadian — DialogBox dari kit Panels. " +
                 "Kosong = kotak gelap polos lama.")]
        public Sprite DialogPanel;

        [Tooltip("Bingkai KARTU kecil: slot dagangan toko dan kartu pakta kejadian — Chip dari " +
                 "kit Panels. Kosong = kotak warna lama dengan garis Frame.")]
        public Sprite CardChip;

        [Tooltip("Tanda NODE PETA YANG SUDAH DIBERESKAN - gambar X (atau apa pun) yang " +
                 "ditumpangkan di atas icon node yang sudah diinjak.\n\n" +
                 "Kosong = placeholder X generatan kode, supaya tandanya tetap ada sebelum " +
                 "art-nya jadi.")]
        public Sprite MapIconCleared;

        [Tooltip("PREFAB deret tombol KECEPATAN (1x/2x/...). Letak, ukuran, jarak, dan rupa " +
                 "tombolnya SEPENUHNYA milik prefab — kode cuma meng-clone cetakan di dalamnya " +
                 "sebanyak jumlah kecepatan, mengisi labelnya, dan menyalakan keadaan terpilih.\n\n" +
                 "Isi prefab: root ber-HorizontalLayoutGroup, satu anak nonaktif bernama " +
                 "'ButtonTemplate' berisi anak 'Idle', 'Active', dan 'Label' (teks TMP).\n\n" +
                 "Terisi = tombolnya jadi tombol UGUI beneran (klik ikut ke mana pun kamu " +
                 "memindahkannya). Kosong = deret hitungan kode yang lama.")]
        public GameObject SpeedBarPrefab;

        [Tooltip("PREFAB kartu HOVER (keterangan piece, HP/mana, resep). LEBAR dan tepi " +
                 "dalamnya milik prefab — perbesar kartunya di sini kalau kekecilan.\n\n" +
                 "Isi prefab: root ber-Image (bingkai, Sliced) + satu anak bernama 'Body' " +
                 "ber-TextMeshProUGUI; jarak Body ke tepi root itulah tepi dalam kartu.\n\n" +
                 "Tinggi dan posisi tetap dihitung kode: tingginya ikut panjang teks, " +
                 "posisinya mengikuti kursor. Kosong = kartu hitungan lama (lebar 380).")]
        public GameObject TooltipPrefab;

        [Tooltip("PREFAB kartu RESEP (ALT + hover piece). Root ber-Image = badan kartunya — " +
                 "sprite, tint, dan bahan panel milikmu. Ukuran & posisi tetap ditulis kode: " +
                 "tinggi ikut jumlah baris resep, dan kartunya DIPATOK di dekat mouse saat " +
                 "dibuka (diam selama ALT ditahan supaya bisa dibaca).\n\n" +
                 "Kosong = kotak gelap polos gambar-kode.")]
        public GameObject RecipeCardPrefab;

        [Tooltip("PREFAB penata letak HUD COMBAT, membawa komponen CombatHudRig.\n\n" +
                 "Isinya kotak-kotak kosong yang kamu geser sendiri: baris info kiri-atas " +
                 "(wave/gold) + plakatnya, tombol LANJUT (yang muncul saat wave beres), " +
                 "tombol TOKO, bar boss, dan alas TAS. Isi tiap kotak tetap digambar kode — " +
                 "yang kamu tata LETAKNYA.\n\n" +
                 "Kotak yang dibiarkan kosong kembali ke letak hitungan lama.")]
        public GameObject CombatHudPrefab;

        [Tooltip("SATU PREFAB PAYUNG untuk seluruh UI combat. Isinya boleh memuat semua bagian " +
                 "sekaligus sebagai anak: Vitals (VitalsRig), strip status (StatusStripRig), " +
                 "panel singgah (ShopRig), 'SpeedBar', 'SpellPanel', 'TooltipCard', dan kotak " +
                 "HUD (CombatHudRig).\n\n" +
                 "Kalau terisi, SEMUA bagian dicari di dalam prefab ini lebih dulu — slot " +
                 "per-bagian di bawah cuma cadangan untuk bagian yang tidak ada di sini. " +
                 "Satu tempat, satu kali buka, semua ketata.")]
        public GameObject CombatUiPrefab;

        [Tooltip("PREFAB panel DAFTAR SKILL (meter damage kanan layar).\n\n" +
                 "Isi prefab: root (letak & lebar panel) berisi wadah ber-VerticalLayoutGroup, " +
                 "dan di dalamnya satu anak nonaktif bernama 'RowTemplate' dengan anak " +
                 "berurutan: 'Bg' (Image), 'Fill' (Image), 'Text' (teks TMP), 'Notch' (Image).\n\n" +
                 "Urutan itu WAJIB — di UGUI urutan anak adalah urutan gambar, dan takik " +
                 "sengaja paling akhir supaya tergambar di atas teks.\n\n" +
                 "Kode meng-clone cetakan itu lalu cuma mengisi teks, panjang isian, warna " +
                 "takik, dan menyalakan baris terpakai. Kosong = daftar hitungan lama.")]
        public GameObject SpellPanelPrefab;

        [Tooltip("Alas di belakang petak papan. DIKOSONGKAN atas permintaan pemilik project: " +
                 "bingkai berhias di belakang petak membuat dua bingkai bersarang, dan yang " +
                 "dimaksud 'grid' sejak awal adalah petak papannya sendiri — bukan gambar alas. " +
                 "Slotnya dibiarkan ada kalau nanti mau dicoba lagi.")]
        public Sprite GridFrame;

        [Tooltip("Alas panel TAS 4x4 di kanan papan. Sekarang diisi placeholder generatan — " +
                 "tukar sprite-nya di sini begitu art aslinya jadi, tanpa menyentuh kode. " +
                 "Kosong = tas telanjang tanpa alas, seperti sebelum ada slot ini.")]
        public Sprite BagPanel;

        [Tooltip("Bingkai kartu INFO in-run: kartu hover piece, popup keterangan. Diisi salah " +
                 "satu panel kit UIPanel supaya kartu info bicara bahasa yang sama dengan " +
                 "halaman menu. Kosong = kotak gelap polos seperti sebelumnya.")]
        public Sprite InfoPanel;

        [Tooltip("Bingkai TOMBOL kit — speed, tombol waktu, TOKO, REROLL, tombol game over. " +
                 "Kosong = kotak warna lama dengan garis Frame.")]
        public Sprite ButtonFrame;

        [Tooltip("Bingkai tombol MENYALA — keadaan terpilih & ajakan utama (speed aktif, " +
                 "banner MULAI/LANJUT). Kosong = jatuh ke ButtonFrame.")]
        public Sprite ButtonGlow;

        [Tooltip("Plakat kecil — papan wave kiri-atas. Kosong = kotak warna lama.")]
        public Sprite Plaque;

        [Tooltip("Bingkai BAR — baris spell aktif & bar HP boss. Kosong = kotak warna lama.")]
        public Sprite BarFrame;

        [Header("Cairan di bola HP & mana")]
        [Tooltip("Seberapa tinggi permukaan cairan bergoyang, sebagai bagian dari tinggi bola.\n\n" +
                 "Nol = permukaan datar seperti sebelumnya. Goyangannya hanya MENGURANGI isian, " +
                 "tidak pernah menambah — Image bertipe Filled sudah memotong geometrinya di " +
                 "ketinggian isian, jadi gelombang yang mencoba naik akan terpotong rata.")]
        [Range(0f, 0.3f)] public float LiquidAmp = 0.045f;

        [Tooltip("Kecepatan ayunannya. Terlalu cepat terbaca sebagai bergetar, bukan sebagai " +
                 "cairan yang berat.")]
        [Range(0f, 12f)] public float LiquidSpeed = 2.4f;

        [Tooltip("Jumlah gelombang selebar bola. Di bawah 1 permukaannya cuma miring naik-turun; " +
                 "di atas 4 ia jadi riak halus yang hilang di ukuran bola.")]
        [Range(0.5f, 8f)] public float LiquidWaves = 2.3f;

        [Tooltip("Terang tipis tepat di garis permukaan. Tanpa ini goyangannya terbaca sebagai " +
                 "gambar yang bergetar; dengan ini, sebagai permukaan yang punya arah atas.")]
        [Range(0f, 1f)] public float LiquidCrest = 0.35f;

        [Header("Ruang bingkai (piksel)")]
        [Tooltip("Berapa jauh sampul buku melebar keluar dari petak, per sisi: " +
                 "x = kiri, y = bawah, z = kanan, w = atas.\n\n" +
                 "Kiri sengaja jauh lebih besar: di situ punggung bukunya, dan kalau tidak " +
                 "diberi ruang, punggungnya menindih kolom petak pertama.")]
        // Diturunkan dari gambarnya, bukan dikira-kira: halaman dalam sampul menempati x 150-860
        // dari 892 piksel lebar, jadi petak 298 px menuntut bingkai selebar 374 px — 63 px
        // menjulur ke kiri (punggung) dan 13 px ke kanan. GridInset menyediakan ruangnya.
        public Vector4 GrimoirePad = new Vector4(63f, 8f, 13f, 8f);

        [Tooltip("Ruang bingkai emas di sekeliling petak, per sisi (x kiri, y bawah, z kanan, " +
                 "w atas). Gambarnya punya bayangan gelap di luar garis emas — melebihkan " +
                 "sedikit membuat bayangan itu jatuh di luar petak, bukan menggelapkan petaknya.")]
        public Vector4 GridPad = new Vector4(16f, 16f, 16f, 16f);

        [Header("Petak grimoire (di atas bingkai emas)")]
        [Tooltip("Petak kosong. Warna lamanya abu-abu TERANG di alpha 0,05 — pilihan yang benar " +
                 "waktu latarnya papan gelap, dan tak terlihat sama sekali begitu latarnya jadi " +
                 "kertas terang. Di sini ia harus jadi tinta: gelap, tipis, seperti garis mistar.")]
        public Color GridCellIdle = new Color(0.24f, 0.15f, 0.06f, 0.42f);

        [Tooltip("Petak kosong saat ada piece di tangan — lebih tegas lagi, supaya tujuan " +
                 "taruhnya kelihatan tanpa harus menyalakan seluruh papan.")]
        public Color GridCellShown = new Color(0.24f, 0.15f, 0.06f, 0.62f);

        // ------------------------------------------------------------------ gloom tepi panel

        [Header("Gloom tepi panel")]
        [Tooltip("Warna kegelapan yang merambat dari tepi panel. Disetel senada noda paling " +
                 "pekat di perkamen — kalau warnanya keluar dari keluarga itu, ia terbaca " +
                 "sebagai bayangan yang ditempel, bukan sebagai kertas yang menghitam.")]
        public Color GloomTint = new Color(0.16f, 0.10f, 0.05f, 1f);

        [Tooltip("Kepekatan maksimum di tepi. 'Tipis-tipis' hidup di sekitar 0,5 — di atas 0,8 " +
                 "ia berhenti jadi suasana dan mulai memakan isi panel.")]
        [Range(0f, 1f)] public float GloomCeiling = 0.55f;

        [Tooltip("Seberapa jauh ke dalam kegelapannya mulai, dalam piksel dari tepi.")]
        [Min(0f)] public float GloomInset = 190f;

        [Tooltip("Ketakberaturan garis batasnya, dalam piksel. Nol = bingkai gradien rapi " +
                 "(dan langsung terbaca sebagai vignette). Inilah knob yang membuatnya hidup.")]
        [Min(0f)] public float GloomWobble = 90f;

        [Tooltip("Ukuran gumpalan derau dalam piksel. Kecil = tepi berbulu; besar = noda lebar.")]
        [Min(1f)] public float GloomScale = 210f;

        // Tanpa kedua knob di bawah, shadernya memakai nilai bawaannya sendiri — dan bawaannya
        // (Churn 0,05, Drift 6) di atas GloomScale 210 keluar sebagai 0,03 gumpalan per detik:
        // beranimasi kalau diukur, BEKU kalau dilihat. Nilai di sini disamakan dengan Gloom.mat
        // milik kegelapan arena (Churn 0,3, Drift 0,6 pada Scale 3 = 0,2 gumpalan/detik), yang
        // memang sudah dinilai bergerak dengan benar — satu bahan, satu kecepatan.

        [Tooltip("Kecepatan garis batasnya BERGOLAK di tempat. Nol = tepi beku.")]
        [Min(0f)] public float GloomChurn = 0.3f;

        [Tooltip("Kecepatan noda tepi HANYUT melintasi panel, dalam piksel per detik. " +
                 "Dibagi GloomScale di shader, jadi menaikkan GloomScale juga memperlambatnya.")]
        [Min(0f)] public float GloomDrift = 42f;

        [Header("Tepi robek (perkamen peta)")]
        [Tooltip("Seberapa dalam gigitan sobek memakan tepi, dalam piksel.")]
        [Min(0f)] public float TearDepth = 64f;

        [Tooltip("Ukuran gigitan dalam piksel. Kecil = tercabik rapat; besar = koyak lebar. " +
                 "Sengaja jauh lebih kecil dari GloomScale — gumpalan noda seukuran 200 piksel " +
                 "yang dipakai jadi garis sobek cuma menghasilkan tepi bergelombang lembut.")]
        [Min(1f)] public float TearScale = 70f;

        [Tooltip("Serat halus di atas gigitan besar. Nol = garis sobeknya mulus, dan mulus itu " +
                 "yang membuatnya balik terbaca sebagai bentuk, bukan sebagai robekan.")]
        [Range(0f, 1f)] public float TearFray = 0.45f;

        [Tooltip("Kelembutan garis potong dalam piksel. Naikkan sedikit saja kalau tepinya " +
                 "terlihat bergerigi piksel; di atas ~3 ia kembali jadi pudar, bukan sobek.")]
        [Range(0.5f, 8f)] public float TearSoft = 1.5f;

        // Yang dibaca mata sebagai "tepi peta" adalah GARIS POTONGNYA, bukan nodanya. Selama dua
        // knob di bawah nol, tepinya beku berapa pun GloomChurn/GloomDrift disetel — noda boleh
        // mengalir di belakang garis yang tidak pernah berubah bentuk.

        [Tooltip("Seberapa jauh garis sobek MENGGELIAT di tempat. Ini knob gerak yang utama — " +
                 "kecepatannya ikut GloomChurn, karena medan deraunya memang dipinjam dari sana. " +
                 "Nol = tepi robek beku seperti gambar diam.")]
        [Range(0f, 3f)] public float TearWarp = 0.8f;

        [Tooltip("Hanyut garis sobek dalam piksel per detik. Sengaja kecil: tepi yang hanyut " +
                 "satu arah terus terbaca sebagai tekstur yang jalan di belakang lubang, bukan " +
                 "sebagai kertas yang tepinya digerogoti.")]
        [Min(0f)] public float TearDrift = 5f;

        [Tooltip("Pengali gloom untuk peta KECIL (intip). Panel kecil dengan pita gelap setebal " +
                 "peta besar akan tertutup separuhnya — jangkauannya diperkecil, bukan " +
                 "kepekatannya, supaya bahannya tetap terbaca sama.")]
        [Range(0.1f, 1f)] public float GloomInsetSmallMul = 0.45f;

        // ------------------------------------------------------------------ tinta di atas kertas

        [Tooltip("Judul \"GRIMOIRE\" di atas papan. Warna lamanya lavender terang — benar di " +
                 "atas layar gelap, nyaris hilang begitu di belakangnya ada halaman buku.")]
        public Color GridTitleInk = new Color(0.28f, 0.17f, 0.06f, 1f);

        [Header("Tinta peta (di atas perkamen)")]
        [Tooltip("Warna judul peta. Perkamen itu TERANG — warna krem/putih yang dipakai waktu " +
                 "latarnya masih biru-gelap akan hilang total di atasnya.")]
        public Color MapTitleInk = new Color(0.24f, 0.13f, 0.05f, 1f);

        [Tooltip("Warna baris legenda.")]
        public Color MapLegendInk = new Color(0.36f, 0.24f, 0.13f, 1f);

        [Tooltip("Warna ruas jalur antar node yang BELUM dilalui.")]
        public Color MapPathInk = new Color(0.42f, 0.30f, 0.18f, 0.85f);

        [Tooltip("Warna jalur yang SEDANG DITAWARKAN — pilihan langkah berikutnya. Ini baris " +
                 "terpenting di seluruh peta: kalau ia tidak terbaca, pemain tidak tahu ke mana " +
                 "ia boleh pergi. JANGAN kuning/emas — perkamennya sendiri kuning dan keduanya " +
                 "lebur jadi satu.")]
        public Color MapPathOfferedInk = new Color(0.78f, 0.09f, 0.07f, 1f);

        [Tooltip("Warna jalur yang SUDAH dilalui. Hijau pucat hilang di perkamen terang; " +
                 "yang dipakai sekarang hijau tua yang masih terbaca sebagai 'sudah lewat'.")]
        public Color MapPathWalkedInk = new Color(0.16f, 0.42f, 0.20f, 0.9f);

        [Tooltip("Warna label KAMU di atas token pemain.")]
        public Color MapYouInk = new Color(0.45f, 0.20f, 0.03f, 1f);

        [Tooltip("Warna cincin di sekeliling node. Putih hilang di perkamen; ini penggantinya.")]
        public Color MapRingInk = new Color(0.28f, 0.17f, 0.07f, 1f);

        [Header("Icon peta")]
        [Tooltip("Icon per jenis node, silhouette putih — kode yang mewarnainya jadi tinta. " +
                 "Kosongkan salah satu = node itu kembali ke huruf pertama nama jenisnya, " +
                 "seperti sebelum ada art.")]
        public Sprite MapIconFight;
        public Sprite MapIconElite;
        public Sprite MapIconShop;
        public Sprite MapIconEvent;
        public Sprite MapIconBoss;

        [Tooltip("Token pemain di peta. Kosong = bulatan polos seperti sebelumnya.")]
        public Sprite MapIconYou;

        // ------------------------------------------------------------------ garis evolusi

        [Header("Garis evolusi")]
        [Tooltip("Material busur listrik untuk garis penghubung evolusi (shader " +
                 "Grimoire/UiArcBolt). Goyangan, kedip, dan nyalanya disetel di material — " +
                 "bukan di kode. KOSONG = garis lurus polos seperti sebelum ada shader ini, " +
                 "yang tetap benar, cuma diam.")]
        public Material EvoBoltMaterial;

        // ------------------------------------------------------------------ ukuran peta kecil

        [Header("Peta kecil (intip lewat M)")]
        [Tooltip("Ukuran panel sebagai pecahan dari layar. Peta PEMILIH (sesudah wave) tetap " +
                 "satu layar penuh dan tidak dipengaruhi nilai ini — bedanya disengaja: " +
                 "memilih itu keputusan, mengintip itu lirikan.")]
        // Range tidak berlaku untuk Vector2 di inspector Unity — dijepit di pemakainya.
        public Vector2 PeekScreenFraction = new Vector2(0.56f, 0.78f);
    }
}
