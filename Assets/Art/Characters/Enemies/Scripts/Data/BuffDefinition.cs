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

        [Tooltip("Placeholder digenerate lewat Tools/Grimoire/Generate Placeholder Icons. " +
                 "Timpa PNG-nya di Assets/GameData/Icons untuk mengganti art.")]
        public Sprite Icon;

        [Tooltip("Detik. Kena lagi saat masih aktif -> durasi di-refresh.")]
        public float Duration = 6f;

        [Min(1)]
        [Tooltip("Berapa tumpukan yang bisa menumpuk. 1 = buff biasa (kena lagi cuma memperpanjang). " +
                 "Di atas 1 = CHARGE: tiap tumpukan mengalikan efeknya, dan skill tertentu bisa " +
                 "menghabiskannya sekaligus untuk satu pukulan besar.")]
        public int MaxStacks = 1;

        public bool IsCharge => MaxStacks > 1;

        [Tooltip("Stat yang dinaikkan selama buff aktif. Persen ditulis desimal: 0.25 = 25%. " +
                 "Untuk debuff, tulis NEGATIF.")]
        public StatModifier[] Mods;

        [Tooltip("Efek buruk yang ditempelkan musuh, bukan hadiah reaksi. Debuff punya jatah slot " +
                 "sendiri supaya banjir kutukan tidak menendang buff hasil reaksi keluar — kalau " +
                 "sampai begitu, loop inti game ini mati justru saat pemain paling terdesak.")]
        public bool IsDebuff;

        [Tooltip("Khusus debuff: dipotong oleh stat DebuffResist pemain.")]
        public bool ResistShortensDuration = true;

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
