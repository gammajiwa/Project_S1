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
