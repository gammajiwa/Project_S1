using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Saklar curang untuk merekam video dan menguji, dalam satu aset yang bisa dinyalakan tanpa
    /// menyentuh kode.
    ///
    /// Selama ini setiap kali sebuah build perlu dipamerkan atau sebuah bug perlu dikurung, jalannya
    /// selalu sama: masuk play mode, lalu mengoprek field lewat refleksi. Itu tidak bisa diulang,
    /// tidak bisa diwariskan ke sesi lain, dan tidak ada jejaknya.
    ///
    /// <b><see cref="Enabled"/> adalah gerbangnya, dan defaultnya mati.</b> Satu saklar induk berarti
    /// seluruh isi aset ini bisa dimatikan sekaligus — tanpa itu, satu centang yang lupa dimatikan
    /// akan ikut terkirim ke client sebagai build yang tidak bisa kalah, dan tidak ada yang sadar
    /// sampai ada yang memainkannya.
    /// </summary>
    [CreateAssetMenu(fileName = "DebugConfig", menuName = "Grimoire/Debug Config")]
    public class DebugConfig : ScriptableObject
    {
        [Header("GERBANG INDUK")]
        [Tooltip("MATI = seluruh isi aset ini diabaikan, apa pun yang dicentang di bawah. " +
                 "Selalu matikan sebelum build dikirim ke siapa pun.")]
        public bool Enabled;

        [Header("Bertahan hidup")]
        [Tooltip("Pemain tidak bisa kehilangan HP. Buat merekam wave tinggi tanpa harus jago.")]
        public bool Invulnerable;

        [Tooltip("Mana tidak pernah habis. Ini yang memperlihatkan seperti apa sebuah build " +
                 "SEHARUSNYA menembak, terlepas dari rem manaanya.")]
        public bool InfiniteMana;

        [Tooltip("Cooldown diabaikan. Semua skill menembak begitu mana cukup.")]
        public bool NoCooldowns;

        [Header("Kekuatan")]
        [Tooltip("Pengali damage semua skill pemain. 1 = normal. Naikkan buat memperlihatkan " +
                 "ledakan tanpa menunggu build lengkap.")]
        [Min(0f)] public float DamageMultiplier = 1f;

        [Tooltip("Pengali HP semua musuh. Turunkan ke 0.1 supaya semuanya meledak sekali sentuh.")]
        [Min(0.01f)] public float EnemyHpMultiplier = 1f;

        [Header("Wave")]
        [Tooltip("Wave berapa run dimulai. 0 = normal (mulai dari 1). Buat langsung merekam endgame.")]
        [Min(0)] public int StartAtWave;

        [Tooltip("Pengali jumlah musuh per wave. Naikkan buat rekaman yang benar-benar padat.")]
        [Min(0.1f)] public float EnemyCountMultiplier = 1f;

        [Tooltip("Musuh berhenti berdatangan. Yang sudah ada tetap hidup — buat memotret satu " +
                 "gerombolan tanpa terus ditimpa yang baru.")]
        public bool FreezeSpawns;

        [Header("Loadout")]
        [Tooltip("Kalau diisi, papan langsung disusun dari sini, menimpa loadout bawaan. " +
                 "Bikin beberapa aset HeroLoadout (misal build bintang 5) lalu tukar di sini.")]
        public HeroLoadout ForceLoadout;

        [Header("Panel demo")]
        [Tooltip("Bilah ganti wajah & cuaca di pojok kanan-atas. Ikut gerbang induk, jadi build " +
                 "yang dikirim (Enabled mati) tidak akan pernah membawanya — dulu saklarnya " +
                 "field terpisah di ProtoBootstrap yang harus diingat untuk dimatikan.")]
        public bool ShowDemoBar = true;

        [Header("Rekaman")]
        [Tooltip("Menyembunyikan seluruh UI. Buat menangkap gameplay bersih tanpa panel menutupi.")]
        public bool HideUI;

        [Tooltip("Kecepatan waktu saat run dimulai. 1 = normal, 0.35 = gerak lambat buat " +
                 "memperlihatkan bentuk sebuah skill.")]
        [Range(0.1f, 4f)] public float StartTimeScale = 1f;

        // Semua pembaca memanggil lewat properti ini, tidak pernah membaca field-nya langsung,
        // supaya gerbang induk mustahil terlewat di satu tempat.
        public bool CheatInvulnerable => Enabled && Invulnerable;
        public bool CheatInfiniteMana => Enabled && InfiniteMana;
        public bool CheatNoCooldowns => Enabled && NoCooldowns;
        public bool CheatFreezeSpawns => Enabled && FreezeSpawns;
        public bool CheatHideUI => Enabled && HideUI;
        public bool DemoBarVisible => Enabled && ShowDemoBar;

        public float DamageScale => Enabled ? Mathf.Max(0f, DamageMultiplier) : 1f;
        public float EnemyHpScale => Enabled ? Mathf.Max(0.01f, EnemyHpMultiplier) : 1f;
        public float EnemyCountScale => Enabled ? Mathf.Max(0.1f, EnemyCountMultiplier) : 1f;
        public int OpeningWave => Enabled ? Mathf.Max(0, StartAtWave) : 0;
        public HeroLoadout LoadoutOverride => Enabled ? ForceLoadout : null;
        public float OpeningTimeScale => Enabled ? Mathf.Clamp(StartTimeScale, 0.1f, 4f) : 1f;
    }
}
