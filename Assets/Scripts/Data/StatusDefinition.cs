using UnityEngine;

namespace Proto
{
    /// <summary>
    /// One ailment that can sit on an enemy: burn, chill, shock, bleed, poisonâ€¦
    /// Pure data â€” the tick loop lives in EnemyManager and never allocates.
    /// </summary>
    [CreateAssetMenu(fileName = "Status_", menuName = "Grimoire/Status")]
    public class StatusDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        public Color Color = Color.white;

        [Tooltip("Placeholder digenerate lewat Tools/Grimoire/Generate Placeholder Icons. " +
                 "Timpa PNG-nya di Assets/GameData/Icons untuk mengganti art.")]
        public Sprite Icon;

        [Header("Stack")]
        [Min(1)]
        [UnityEngine.Serialization.FormerlySerializedAs("MaxStacks")]
        [Tooltip("Poin maksimum yang bisa menumpuk di satu musuh.")]
        public int MaxPoints = 1;

        [Tooltip("Kena lagi saat masih aktif -> durasi di-refresh (bukan ditambah).")]
        public bool RefreshOnReapply = true;

        [Header("Damage over time")]
        [Tooltip("Detik antar tick. 0 = tidak pernah tick.")]
        public float TickInterval = 0.5f;

        [UnityEngine.Serialization.FormerlySerializedAs("DamagePerTickPerStack")]
        [Tooltip("Damage tiap tick, DIKALI jumlah poin.")]
        public float DamagePerTickPerPoint;

        [Header("Efek pasif selama aktif")]
        [Tooltip("1 = kecepatan normal, 0.45 = melambat 55%.")]
        public float MoveSpeedMultiplier = 1f;

        [Tooltip("1 = normal, 1.3 = menerima damage +30%.")]
        public float DamageTakenMultiplier = 1f;

        [Tooltip("Unit per detik musuh ditarik ke titik asal ailment ini. 0 = tidak menarik.")]
        public float PullStrength;

        [TextArea(2, 3)]
        public string Blurb;

        void OnValidate()
        {
            if (string.IsNullOrEmpty(Id)) Id = name.ToLowerInvariant().Replace("status_", "");
            if (string.IsNullOrEmpty(DisplayName)) DisplayName = Id;
            if (TickInterval < 0.05f && DamagePerTickPerPoint > 0f) TickInterval = 0.05f;
        }
    }
}
