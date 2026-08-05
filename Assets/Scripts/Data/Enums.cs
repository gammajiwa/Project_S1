namespace Proto
{
    public enum Element { Fire, Ice, Lightning, Arcane }

    public enum StatusType { None, Burn, Chill, Shock }

    public enum CastKind
    {
        /// <summary>Peluru ke musuh terdekat.</summary>
        Projectile,

        /// <summary>Ledakan melingkar di posisi pemain.</summary>
        Nova,

        /// <summary>Menyambar beberapa musuh terdekat.</summary>
        Chain,

        Heal,

        /// <summary>Ledakan melingkar yang jatuh di GEROMBOLAN paling padat, bukan di pemain.</summary>
        AreaAtTarget,

        /// <summary>Menyapu garis lurus dari pemain ke arah musuh terdekat.</summary>
        Line,

        /// <summary>Meninggalkan kubangan yang menyakiti berulang selama beberapa detik.</summary>
        Zone,

        Passive,
        AuraOnly
    }

    /// <summary>
    /// Kapan sebuah skill meletus. Cooldown = jalan sendiri. StatusThreshold = skill ini
    /// menunggu ailment tertentu menumpuk sampai ambang batas di satu musuh.
    /// </summary>
    public enum CastTrigger { Cooldown, StatusThreshold }

    public enum AuraKind { None, DamagePct, CooldownPct, RadiusPct }

    /// <summary>Rune = the base tile you build on. Skill = what stands on top of it.</summary>
    public enum Layer { Rune, Skill }

    /// <summary>
    /// Semua stat yang bisa dinaikkan sebuah piece. Rune, segel, maupun skill boleh membawa
    /// beberapa sekaligus lewat daftar <see cref="StatModifier"/>.
    /// </summary>
    public enum StatKind
    {
        None,

        // bertahan
        MaxHp,
        HpRegen,
        Defense,

        // sumber daya
        MaxMana,
        ManaRegen,
        ManaCostPct,

        // menyerang
        DamagePct,
        CooldownPct,
        AreaPct,
        RangePct,
        CritChance,
        CritDamage,

        // elemen
        FireDamagePct,
        IceDamagePct,
        LightningDamagePct,

        /// <summary>Menambah POIN tiap kali skill menempelkan ailment. Bikin pemicu cepat penuh.</summary>
        AilmentPoints,

        Count
    }

    /// <summary>Satu baris stat di sebuah piece. Nilai persen ditulis desimal: 0.15 = 15%.</summary>
    [System.Serializable]
    public struct StatModifier
    {
        public StatKind Type;
        public float Value;
    }
}
