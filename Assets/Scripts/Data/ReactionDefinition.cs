using UnityEngine;

namespace Proto
{
    /// <summary>
    /// "Kalau musuh kena A dan B sekaligus, terjadi sesuatu." Ini yang bikin dua skill biasa
    /// jadi kombo â€” dan semuanya diatur dari asset, bukan dari kode.
    /// </summary>
    [CreateAssetMenu(fileName = "Reaction_", menuName = "Grimoire/Reaction")]
    public class ReactionDefinition : ScriptableObject
    {
        [Header("Syarat")]
        public string DisplayName = "REAKSI";
        public StatusDefinition A;
        public StatusDefinition B;

        [Min(1)] [UnityEngine.Serialization.FormerlySerializedAs("MinStacksA")] public int MinPointsA = 1;
        [Min(1)] [UnityEngine.Serialization.FormerlySerializedAs("MinStacksB")] public int MinPointsB = 1;

        [Header("Bahan dipakai habis?")]
        [Tooltip("WAJIB minimal salah satu true. Kalau dua-duanya false, reaksi meledak tiap frame.")]
        public bool ConsumeA = true;

        public bool ConsumeB = true;

        [Header("Ledakan")]
        public float BurstDamage;

        [UnityEngine.Serialization.FormerlySerializedAs("BurstDamagePerStackA")]
        [Tooltip("Tambahan damage per POIN A. Bikin menumpuk poin terasa berharga.")]
        public float BurstDamagePerPointA;

        public float BurstRadius = 3f;

        [Header("Efek lanjutan")]
        [Tooltip("Status yang ditempel setelah reaksi. Boleh kosong.")]
        public StatusDefinition ApplyStatus;

        public float ApplyDuration = 3f;
        [Min(1)] [UnityEngine.Serialization.FormerlySerializedAs("ApplyStacks")] public int ApplyPoints = 1;

        [Tooltip("Tempel status di atas ke SEMUA musuh dalam radius, bukan cuma targetnya.")]
        public bool SpreadToNearby;

        [Header("Hadiah buat pemain")]
        [Tooltip("Buff yang didapat pemain saat reaksi ini terjadi. Boleh kosong.")]
        public BuffDefinition GrantBuff;

        public Color FlashColor = Color.white;

        public bool IsValid => A != null && B != null && A != B && (ConsumeA || ConsumeB);

        void OnValidate()
        {
            if (A != null && B != null && A == B)
            {
                Debug.LogError($"[{name}] A dan B tidak boleh status yang sama.", this);
            }

            if (!ConsumeA && !ConsumeB)
            {
                Debug.LogError($"[{name}] minimal satu bahan harus dikonsumsi, " +
                               "kalau tidak reaksinya jalan terus tiap frame.", this);
            }
        }
    }
}
