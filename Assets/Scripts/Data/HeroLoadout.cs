using UnityEngine;

namespace Proto
{
    /// <summary>
    /// What a hero opens a run holding.
    ///
    /// Data rather than four hardcoded ids in the UI, because "hero" is going to be a choice: the
    /// second one is a different asset, not a different branch. The starting board is also the
    /// game's only tutorial, so it has to be authorable rather than incidental.
    /// </summary>
    [CreateAssetMenu(fileName = "Hero_", menuName = "Grimoire/Hero Loadout")]
    public class HeroLoadout : ScriptableObject
    {
        [System.Serializable]
        public struct Seat
        {
            public PieceDefinition Piece;

            [Tooltip("Petak grid, bukan piksel. Runes dipasang lebih dulu, jadi skill bisa berdiri " +
                     "di atasnya.")]
            public Vector2Int Origin;

            public int Rot;
        }

        public string Id;
        public string DisplayName;

        [TextArea(2, 4)]
        public string Blurb;

        [Header("Tampilan di layar pilih")]
        [Tooltip("Gambar besar di kartu starter. Kosong = kartu menggambar susunan petaknya saja, " +
                 "dan itu bukan jalur usang: susunan papan justru KETERANGAN yang paling jujur " +
                 "tentang apa yang akan dimainkan.")]
        public Sprite Portrait;

        [Tooltip("Warna aksen kartu — garis, judul, dan sorotan pilihan. Dipakai supaya tiap " +
                 "starter punya identitas di carousel tanpa menunggu art jadi.")]
        public Color Accent = new Color(1f, 0.78f, 0.35f, 1f);

        // ------------------------------------------------------------------ stat awal
        //
        // Semua −1 = "jangan ganggu, pakai GameBalance". Sentinel, bukan sepasang bool+angka per
        // stat: yang kedua menggandakan barisnya di inspector, dan blok setinggi itu mengundang
        // orang mengisi semuanya "biar lengkap" — padahal starter yang cuma berbeda di satu angka
        // justru yang paling gampang diseimbangkan.
        //
        // Nol tidak bisa dipakai sebagai penanda: nol itu nilai yang SAH untuk regen, dan starter
        // tanpa regen adalah desain yang masuk akal. −1 tidak pernah sah untuk satu pun angka di
        // sini, jadi ia tidak bisa tertukar dengan nilai sungguhan.

        [Header("Stat awal (−1 = ikut GameBalance)")]
        [Tooltip("Nyawa maksimum. −1 = pakai BaseMaxHp dari GameBalance.")]
        public float MaxHp = -1f;

        [Tooltip("Mana maksimum. −1 = pakai BaseMaxMana.")]
        public float MaxMana = -1f;

        [Tooltip("Mana yang pulih per detik. −1 = pakai BaseManaRegen.\n\n" +
                 "Knob paling tajam di blok ini: regen yang melebihi harga skill membuat mana " +
                 "berhenti jadi sumber daya sama sekali — bolanya diam di penuh sepanjang run.")]
        public float ManaRegen = -1f;

        [Tooltip("Nyawa yang pulih per detik. −1 = pakai BaseHpRegen.")]
        public float HpRegen = -1f;

        [Tooltip("Kecepatan jalan pemain. −1 = pakai BaseMoveSpeed.")]
        public float MoveSpeed = -1f;

        /// <summary>
        /// Nilai yang dipakai untuk satu stat: milik starter kalau ia memang menyebutkan satu,
        /// milik <see cref="GameBalance"/> kalau tidak.
        /// </summary>
        public static float Pick(float loadoutValue, float fallback) =>
            loadoutValue < 0f ? fallback : loadoutValue;

        [Tooltip("Posisinya eksplisit, bukan auto-place. Jarak antar skill pembuka adalah " +
                 "keputusan desain: dua skill yang bersentuhan akan otomatis melebur di akhir " +
                 "wave pertama, dan itu menghapus pilihan yang seharusnya dibuat pemain.")]
        public Seat[] Placed;

        [Tooltip("Dilempar ke lantai, harus dipungut sendiri.")]
        public PieceDefinition[] Loose;

        void OnValidate()
        {
            if (string.IsNullOrEmpty(Id)) Id = name.ToLowerInvariant().Replace("hero_", "");
            if (string.IsNullOrEmpty(DisplayName)) DisplayName = Id;
        }
    }
}
