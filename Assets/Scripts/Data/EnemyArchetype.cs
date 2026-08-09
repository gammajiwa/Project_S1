using UnityEngine;

namespace Proto
{
    /// <summary>
    /// One kind of enemy.
    ///
    /// This replaces the ad-hoc "cursed" flag that came before it. That flag was a stepping stone
    /// and it only ever worked for one variant — a second and third kind would have meant a second
    /// and third set of bespoke fields on the manager. Everything that makes an enemy different
    /// lives here instead, so adding a kind is authoring an asset, not editing the swarm loop.
    ///
    /// Per-enemy stats are still filled in exactly one place (<c>EnemyManager.SpawnOne</c>); this is
    /// just what it reads from. A boss is one of these with the numbers turned up.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy_", menuName = "Grimoire/Enemy Archetype")]
    public class EnemyArchetype : ScriptableObject
    {
        public string Id;
        public string DisplayName;

        [Header("Kapan muncul")]
        [Tooltip("Wave pertama yang boleh memunculkannya.")]
        public int FromWave = 1;

        [Tooltip("Bobot undian relatif terhadap arketipe lain yang sedang aktif.")]
        public float Weight = 1f;

        public float WeightPerWave;
        public float WeightMax = 20f;

        [Header("Tubuh")]
        public float HpMultiplier = 1f;
        public float SpeedMultiplier = 1f;
        public float Scale = 1f;

        [Tooltip("Di atas nol berarti terbang. Musuh tetap kena AoE darat — jarak dihitung datar, " +
                 "dan skill yang tidak bisa menyentuh yang terbang cuma akan bikin frustrasi.")]
        public float HoverHeight;

        [Tooltip("Warna khusus saat tidak sedang kena ailment. Ailment tetap menang: warna adalah " +
                 "bacaan 'sebentar lagi meledak', dan itu lebih mendesak daripada jenis musuh.")]
        public bool UseTint;

        public Color Tint = Color.white;

        [Tooltip("Model terpanggang khusus archetype ini (Tools/Grimoire/Bake Enemy VAT).\n\n" +
                 "Kosong = memakai model bawaan gerombolan yang dipasang di ProtoBootstrap. " +
                 "Itu perilaku sebelum slot ini ada, dan tetap benar: archetype yang belum punya " +
                 "model sendiri harus tetap terlihat, bukan hilang dari lapangan.\n\n" +
                 "Harganya satu batch tambahan per model yang benar-benar dipakai — bukan per " +
                 "musuh. Empat model = empat panggilan instanced untuk lima ratus musuh, masih " +
                 "jauh di bawah anggaran yang dulu dibayar 200 kapsul.\n\n" +
                 "Tingginya TIDAK perlu disamakan: tiap model diskalakan sendiri supaya semuanya " +
                 "berdiri setinggi BodyHeight, jadi model raksasa tidak diam-diam mengubah " +
                 "seberapa besar musuh terasa. Pakai 'Scale' di atas kalau memang mau lebih besar.")]
        public VatClipSet Vat;

        [Header("Perilaku")]
        [Tooltip("Nol = mengejar sampai menempel. Di atas nol = berhenti di jarak ini dan menembak.")]
        public float PreferredRange;

        [Tooltip("Ikut memutar mengepung. Yang menukik lurus (terbang) sebaiknya tidak.")]
        public bool Flanks = true;

        [Header("Tembakan (butuh PreferredRange > 0)")]
        [Tooltip("Jeda antar tembakan. Nol = tidak menembak.")]
        public float AttackInterval;

        public float AttackDamage = 8f;
        public float ShotSpeed = 11f;
        public Color ShotColor = new Color(0.7f, 1f, 0.5f);

        [Header("Kutukan")]
        [Tooltip("Ditempelkan ke pemain saat menyentuh. Kosong = tidak mengutuk.")]
        public BuffDefinition Curse;

        [TextArea(2, 3)]
        public string Blurb;

        public bool Shoots => PreferredRange > 0f && AttackInterval > 0f;

        /// <summary>Bobot undian di wave ini. Nol berarti belum boleh muncul.</summary>
        public float WeightAt(int wave)
        {
            if (wave < FromWave) return 0f;
            return Mathf.Min(WeightMax, Weight + (wave - FromWave) * WeightPerWave);
        }

        void OnValidate()
        {
            if (string.IsNullOrEmpty(Id)) Id = name.ToLowerInvariant().Replace("enemy_", "");
            if (string.IsNullOrEmpty(DisplayName)) DisplayName = Id;
            if (Scale < 0.1f) Scale = 0.1f;

            if (AttackInterval > 0f && PreferredRange <= 0f)
            {
                Debug.LogWarning($"[{name}] menembak tapi PreferredRange 0 — dia akan menempel " +
                                 "ke pemain dan menembak dari jarak nol.", this);
            }
        }
    }
}
