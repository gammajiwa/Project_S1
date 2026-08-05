using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Every tuning number in one asset. If a value is not here and not on a piece,
    /// it is a bug — it means something is still hardcoded.
    /// </summary>
    [CreateAssetMenu(fileName = "GameBalance", menuName = "Grimoire/Game Balance")]
    public class GameBalance : ScriptableObject
    {
        [Header("Grid")]
        public int GridWidth = 7;
        public int GridHeight = 7;
        public int BagWidth = 5;
        public int BagHeight = 4;

        [Header("Stat dasar pemain")]
        public float BaseMaxHp = 100f;
        public float BaseMaxMana = 60f;
        public float BaseManaRegen = 5f;
        public float BaseHpRegen = 0f;
        public float HealPerWaveClear = 20f;

        [Header("Wave")]
        public int EnemiesBase = 5;
        public int EnemiesPerWave = 3;
        public float EnemyHpBase = 16f;
        public float EnemyHpPerWave = 7f;
        public float EnemySpeedMin = 1.5f;
        public float EnemySpeedMax = 2.1f;
        public float EnemySpeedPerWave = 0.04f;
        public float EnemyContactDps = 14f;
        public float SpawnIntervalBase = 0.6f;
        public float SpawnIntervalPerWave = 0.02f;
        public float SpawnIntervalMin = 0.2f;

        [Tooltip("Batas musuh hidup bersamaan.")]
        public int MaxAliveEnemies = 200;

        [Header("Loot")]
        [Range(0f, 1f)] public float KillDropChance = 0.04f;
        public int WaveClearDrops = 1;
        [Range(0f, 1f)] public float RuneShareOfDrops = 0.25f;

        [Header("Toko")]
        public int ShopEveryWaves = 3;
        public int ShopSlots = 6;
        public int RerollCostStart = 20;
        public int RerollCostIncrement = 15;
        [Range(0f, 1f)] public float ShopHighRollChance = 0.18f;

        [Header("Harga")]
        public int PriceRune = 20;
        public int PriceSigil = 35;
        public int PriceStar1 = 30;
        public int PriceHighBase = 40;
        public int PriceHighPerStar = 45;

        [Header("Harga jual")]
        public int SellRune = 10;
        public int SellSkill = 15;

        public int PriceOf(PieceDefinition piece)
        {
            if (piece == null) return 0;
            if (piece.IsRune) return PriceRune;
            if (piece.IsPassive) return PriceSigil;
            return piece.Stars <= 1 ? PriceStar1 : PriceHighBase + piece.Stars * PriceHighPerStar;
        }

        public int SellValueOf(PieceDefinition piece)
        {
            if (piece == null) return 0;
            return piece.IsRune ? SellRune : SellSkill;
        }
    }
}
