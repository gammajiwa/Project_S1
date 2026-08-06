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

        [Header("Tubuh")]
        [Tooltip("Ruas badan saat HP penuh, di luar kepala. Ini juga panjang bar HP-nya.")]
        [Min(3)] public int MaxSegments = 22;

        [Tooltip("Ruas paling sedikit yang tersisa menjelang mati. Di bawah tiga, bentuk ularnya " +
                 "hilang dan yang tersisa cuma segumpal bola.")]
        [Min(2)] public int MinSegments = 4;

        [Tooltip("Jarak antar ruas dalam unit dunia. Terlalu rapat = terlihat seperti satu batang; " +
                 "terlalu renggang = terlihat seperti barisan bola yang tidak berhubungan.")]
        public float Spacing = 1.05f;

        [Tooltip("Ukuran kepala.")]
        public float HeadScale = 2.4f;

        [Tooltip("Ukuran ruas terakhir. Yang di antaranya diinterpolasi, jadi badannya meruncing.")]
        public float TailScale = 0.9f;

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

        void OnValidate()
        {
            if (string.IsNullOrEmpty(Id)) Id = name.ToLowerInvariant().Replace("boss_", "");
            if (MinSegments > MaxSegments) MinSegments = MaxSegments;
            if (Spacing < 0.3f) Spacing = 0.3f;
        }
    }
}
