using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Satu PAKTA: berkah permanen DAN kutuk permanen, diambil sekaligus atau tidak sama sekali.
    ///
    /// Berbeda dari buff dan kutukan, dan bedanya bukan soal durasi.
    ///
    /// Buff itu hadiah reaksi — datang sendiri, pergi sendiri, dan pemain tidak memilihnya. Kutukan
    /// itu tempelan musuh — pemain juga tidak memilihnya, cuma menahannya. Keduanya adalah CUACA:
    /// terjadi pada pemain sambil ia main.
    ///
    /// Pakta adalah KEPUTUSAN. Ia berlaku sampai run berakhir, tidak bisa dicabut oleh cleanse mana
    /// pun, dan tiap satunya menukar sesuatu yang nyata dengan sesuatu yang nyata. Karena itu ia
    /// punya slot UI sendiri: menaruhnya di strip buff akan membuatnya terbaca seperti sesuatu yang
    /// akan hilang sebentar lagi — dan seluruh beratnya justru pada kenyataan bahwa ia tidak akan.
    ///
    /// Sisi musuh dikalikan, sisi pemain ditambahkan. Itu bukan gaya melainkan bentuk datanya:
    /// stat pemain memang sudah berupa jumlah persen di seluruh game, sementara nyawa musuh lahir
    /// dari kurva wave dan satu-satunya cara menggesernya tanpa merusak kurvanya adalah mengalikan.
    /// </summary>
    [CreateAssetMenu(fileName = "Pact_", menuName = "Grimoire/World Modifier")]
    public class WorldModifierDefinition : ScriptableObject
    {
        [Header("Identitas")]
        [Tooltip("Jangan diubah setelah rilis — codex dan simpanan run memakainya sebagai kunci.")]
        public string Id;

        public string DisplayName;

        public Color Color = Color.white;

        [Tooltip("Placeholder digenerate lewat Tools/Grimoire/Generate Placeholder Icons.")]
        public Sprite Icon;

        [Header("Kalimat di kartu")]
        [Tooltip("Sisi untung, ditulis untuk dibaca pemain. Ditulis tangan, bukan dirakit dari " +
                 "daftar stat: 'tiap kill mengembalikan mana' adalah satu kalimat, dan merakitnya " +
                 "dari bidang-bidang di bawah selalu menghasilkan bahasa robot.")]
        [TextArea(1, 3)] public string BoonText;

        [Tooltip("Sisi rugi. WAJIB diisi — pakta tanpa harga bukan pakta, itu hadiah.")]
        [TextArea(1, 3)] public string BaneText;

        [Header("Pemain — ditambahkan ke stat")]
        [Tooltip("Persen ditulis desimal: 0.25 = +25%. Boleh negatif; yang membedakan berkah dari " +
                 "kutuk cuma tandanya, dan memisahkannya jadi dua daftar semata-mata supaya kartunya " +
                 "bisa menggambar dua baris berbeda warna.")]
        public StatModifier[] Boon;

        public StatModifier[] Bane;

        [Header("Musuh — dikalikan")]
        [Min(0.1f)] public float EnemyHpMul = 1f;
        [Min(0.1f)] public float EnemySpeedMul = 1f;
        [Min(0.1f)] public float EnemyDamageMul = 1f;
        [Min(0.1f)] public float EnemyCountMul = 1f;

        [Header("Aturan yang tidak bisa ditulis sebagai stat")]
        [Tooltip("Pengali regen mana pasif. NOL berarti mana tidak pulih sendiri sama sekali — dan " +
                 "itu tidak bisa ditiru lewat StatKind.ManaRegen, karena stat itu PENJUMLAHAN: " +
                 "angka negatif sebesar apa pun tetap bisa dilewati satu segel regen, dan pakta " +
                 "yang bisa dibatalkan satu segel bukan pakta.")]
        [Min(0f)] public float ManaRegenMul = 1f;

        [Tooltip("Pengali regen HP pasif. Aturan yang sama.")]
        [Min(0f)] public float HpRegenMul = 1f;

        [Tooltip("Mana yang pulih TIAP MUSUH MATI. Kecil — pengalinya laju kill, bukan satu. " +
                 "0,4 di 10 kill per detik sudah 4 mana/detik, sebanding seluruh regen dasar.")]
        [Min(0f)] public float ManaPerKill;

        [Tooltip("HP yang pulih tiap musuh mati. Aturan yang sama.")]
        [Min(0f)] public float HpPerKill;

        [Tooltip("Peluang tiap cast menembak DUA KALI, gratis. 0,25 = seperempat cast dobel.\n\n" +
                 "Gemanya tidak pernah menggemakan dirinya sendiri — sekali dobel, selesai. Tanpa " +
                 "aturan itu, dua pakta gema bertumpuk bisa saling memanggil sampai frame-nya mati.")]
        [Range(0f, 1f)] public float EchoChance;

        [Tooltip("Sekali per run, kematian dibatalkan dan HP kembali ke porsi ini. 0 = tidak " +
                 "pernah. Ini pakta paling mahal yang bisa ditawarkan, jadi kutuknya harus berat.")]
        [Range(0f, 1f)] public float ReviveAt;

        [TextArea(2, 4)]
        [Tooltip("Suara si penawar. Muncul di panel kejadian, bukan di strip HUD.")]
        public string Blurb;

        void OnValidate()
        {
            if (string.IsNullOrEmpty(Id)) Id = name.ToLowerInvariant().Replace("pact_", "");
            if (string.IsNullOrEmpty(DisplayName)) DisplayName = Id;
        }
    }
}
