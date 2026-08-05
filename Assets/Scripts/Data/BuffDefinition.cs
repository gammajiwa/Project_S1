using UnityEngine;

namespace Proto
{
    /// <summary>
    /// A timed boost on the PLAYER. Mostly handed out by reactions — that is what turns a reaction
    /// from "damage that happened" into "something worth building toward".
    /// </summary>
    [CreateAssetMenu(fileName = "Buff_", menuName = "Grimoire/Buff")]
    public class BuffDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public Color Color = Color.white;

        [Tooltip("Detik. Kena lagi saat masih aktif -> durasi di-refresh, efek TIDAK ditumpuk.")]
        public float Duration = 6f;

        [Tooltip("Stat yang dinaikkan selama buff aktif. Persen ditulis desimal: 0.25 = 25%.")]
        public StatModifier[] Mods;

        [TextArea(2, 3)]
        public string Blurb;

        void OnValidate()
        {
            if (string.IsNullOrEmpty(Id)) Id = name.ToLowerInvariant().Replace("buff_", "");
            if (string.IsNullOrEmpty(DisplayName)) DisplayName = Id;
            if (Duration < 0.5f) Duration = 0.5f;
        }
    }
}
