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

        [Tooltip("PREFAB papan grimoire — cara yang dipakai sekarang. Isinya bebas: sampul, " +
                 "hiasan, apa pun.\n\n" +
                 "Aturan mainnya satu, dan penting: kode HANYA menempelkannya ke kanvas di " +
                 "posisi papan. Ukuran, anchor, dan susunan anaknya TIDAK pernah disentuh — " +
                 "apa yang kamu atur di prefab itulah yang tampil. Petak 7x7 digambar di " +
                 "atasnya oleh kode, jadi biarkan bagian tengahnya kosong.")]
        public GameObject GrimoirePanelPrefab;

        [Tooltip("Jalur lama: satu sprite yang DIPELARKAN ke kotak hitungan. Dipakai hanya " +
                 "kalau GrimoirePanelPrefab kosong. Melar itu justru yang bikin gambarnya " +
                 "kelihatan ketarik, makanya prefab yang jadi jalur utama.")]
        public Sprite GrimoireFrame;

        [Tooltip("Alas di belakang petak 7x7. DIKOSONGKAN atas permintaan pemilik project: " +
                 "bingkai berhias di belakang petak membuat dua bingkai bersarang, dan yang " +
                 "dimaksud 'grid' sejak awal adalah petak 7x7-nya sendiri — bukan gambar alas. " +
                 "Slotnya dibiarkan ada kalau nanti mau dicoba lagi.")]
        public Sprite GridFrame;

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

        [Tooltip("Warna label KAMU di atas token pemain.")]
        public Color MapYouInk = new Color(0.45f, 0.20f, 0.03f, 1f);

        [Tooltip("Warna cincin di sekeliling node. Putih hilang di perkamen; ini penggantinya.")]
        public Color MapRingInk = new Color(0.28f, 0.17f, 0.07f, 1f);

        // ------------------------------------------------------------------ ukuran peta kecil

        [Header("Peta kecil (intip lewat M)")]
        [Tooltip("Ukuran panel sebagai pecahan dari layar. Peta PEMILIH (sesudah wave) tetap " +
                 "satu layar penuh dan tidak dipengaruhi nilai ini — bedanya disengaja: " +
                 "memilih itu keputusan, mengintip itu lirikan.")]
        // Range tidak berlaku untuk Vector2 di inspector Unity — dijepit di pemakainya.
        public Vector2 PeekScreenFraction = new Vector2(0.56f, 0.78f);
    }
}
