using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Satu efek suasana, beserta seberapa sering ia MUNCUL sama sekali.
    ///
    /// Ada dua sifat yang berbeda dan dulu dicampur jadi satu:
    ///
    /// <b>Yang menetap</b> — kupu-kupu, berkas cahaya. Ia ada sepanjang waktu, tapi tidak harus ada
    /// di setiap run. Kalau seluruh daftar selalu menyala bersamaan, tiap run terlihat sama persis
    /// dan tempatnya berhenti punya suasana; yang tersisa cuma daftar isian yang penuh.
    ///
    /// <b>Yang lewat</b> — kawanan burung. Ia justru rusak kalau menetap: burung yang terus-menerus
    /// terbang berputar berhenti jadi kejadian dan berubah jadi hiasan. Yang membuatnya terasa hidup
    /// adalah ia datang, lewat, lalu hilang — dan lama tidak muncul lagi.
    /// </summary>
    [System.Serializable]
    public class AmbientVfxEntry
    {
        public GameObject Prefab;

        [Tooltip("Peluang efek ini dipakai sama sekali di sesi ini. 1 = selalu.")]
        [Range(0f, 1f)] public float Chance = 1f;

        [Tooltip("0 = menetap, menyala terus.\n\n" +
                 "Di atas 0 = LEWAT: ditetaskan sekali lalu dibuang, dan diulang kira-kira tiap " +
                 "sekian detik. Jedanya diacak setengah sampai satu setengah kali angka ini — " +
                 "jeda yang tepat sama terbaca sebagai jadwal, bukan sebagai kebetulan.")]
        [Min(0f)] public float RepeatEvery;

        [Tooltip("Umur satu kemunculan, untuk yang LEWAT. Diabaikan oleh yang menetap.")]
        [Min(1f)] public float Lifetime = 14f;

        [Tooltip("Berapa titik terpisah yang ditetaskan. Di atas 1 efeknya jadi beberapa kantong " +
                 "yang berjauhan alih-alih satu gerombolan.")]
        [Range(1, 6)] public int Count = 1;

        [Tooltip("Jarak TERDEKAT dari pemain. Nol berarti menempel di kaki pemain — dan segerombol " +
                 "kupu-kupu yang mengelilingi pemain ke mana pun ia pergi tidak terbaca sebagai " +
                 "kupu-kupu, melainkan sebagai efek yang menempel di karakter.")]
        [Min(0f)] public float MinDistance = 6f;

        [Tooltip("Jarak TERJAUH. Di luar ini titiknya dipindahkan ke tempat baru, jadi ia selalu " +
                 "cukup dekat untuk terlihat tanpa pernah menempel.")]
        [Min(1f)] public float Spread = 16f;

        [Tooltip("Pengali ukuran. Prefab paket aset disetel untuk kamera setinggi badan; di kamera " +
                 "yang menunduk dari 18 unit, ukuran aslinya terbaca raksasa.")]
        [Min(0.01f)] public float Scale = 1f;

        [Tooltip("Pengali ukuran BUTIRAN saja. Lembaran kabut raksasa (startSize di atas 5 unit) " +
                 "sengaja dilewati — mengalikannya membuat satu billboard menutupi seluruh layar.")]
        [Min(0.05f)] public float Grain = 1f;

        [Tooltip("Nyala = skala hanya memperluas CAKUPAN, butirannya tetap. Mati = butiran ikut " +
                 "membesar. Hujan butuh yang pertama; daun butuh yang kedua.")]
        public bool CoverageOnly;

        [Tooltip("Ketinggian tempat efeknya LAHIR. Daun yang lahir setinggi mata tidak pernah " +
                 "terlihat jatuh — ia cuma muncul lalu mendarat. Lahir jauh di atas layar berarti " +
                 "ia sudah melayang turun sebelum masuk pandangan, dan mengecil sepanjang jalan.")]
        public float Height;

        [Tooltip("Geseran mendatar titik lahirnya, relatif terhadap kamera. X negatif = ke kiri.\n\n" +
                 "Untuk yang tertiup angin ini WAJIB tidak nol. Kalau ia lahir tepat di atas " +
                 "pemain, angin membawanya menjauh ke satu arah saja — dan sisi yang berlawanan " +
                 "selalu kosong, karena tidak ada satu pun daun yang pernah lahir di sana.")]
        public Vector2 Offset;

        [Tooltip("Disembunyikan saat cuaca basah. Kupu-kupu yang tetap beterbangan di tengah hujan " +
                 "adalah hal pertama yang membocorkan bahwa cuacanya cuma lapisan yang ditumpuk.")]
        public bool HideInRain;

        [Tooltip("Cuma muncul saat cuaca CERAH — bukan sekadar tidak hujan. Berkas matahari yang " +
                 "tetap menyala di bawah langit berangin-mendung membocorkan lapisannya sama " +
                 "seperti kupu-kupu di tengah hujan.")]
        public bool OnlyClear;

        [Tooltip("Menempel di kamera alih-alih ditaruh sebagai kantong di titik dunia.\n\n" +
                 "Untuk yang CAKUPANNYA LUAS dan seragam — daun berguguran, hujan. Yang seperti itu " +
                 "seharusnya ada di mana-mana, jadi menghitungnya di luar layar cuma membayar " +
                 "partikel yang tidak pernah dilihat siapa pun.\n\n" +
                 "JANGAN untuk kupu-kupu atau burung: begitu menempel di kamera, ia mengikuti " +
                 "pemain ke mana pun dan berhenti terbaca sebagai makhluk yang punya tempat.")]
        public bool FollowCamera;

        public bool Valid => Prefab != null;
    }
}
