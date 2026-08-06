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
