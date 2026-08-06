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
        AuraOnly,

        /// <summary>Membuang semua debuff yang sedang menempel. Tidak menembak apa pun.</summary>
        Cleanse,

        /// <summary>
        /// Menyemburkan peluru ke SEGALA ARAH sekaligus. Satu-satunya cast yang tidak membidik
        /// siapa pun — dia cuma menunggu ada yang masuk jangkauan lalu menyemprot. Arah awalnya
        /// diacak tiap tembakan supaya polanya tidak pernah persis sama.
        /// </summary>
        Radial,

        /// <summary>
        /// Meledakkan SEMUA musuh yang sedang membawa <c>TriggerStatus</c>, sebesar poin yang
        /// menumpuk di masing-masing, lalu mencabut statusnya.
        ///
        /// Ini separuh kedua dari kombo dua skill: satu skill menghabiskan seluruh cast-nya
        /// mengoles ailment murah ke mana-mana, dan yang ini menagihnya sekaligus.
        /// </summary>
        Detonate,

        // --- Ditambahkan DI BELAKANG, tidak pernah disisipkan di tengah. Nilai enum tersimpan
        // sebagai ANGKA di aset; menyisipkan satu entri saja akan menggeser tiap skill di
        // bawahnya menjadi kind yang salah, tanpa error dan tanpa jejak. ---

        /// <summary>
        /// Memunculkan beberapa pecahan yang MENGAMBANG di atas kepala pemain dan menunggu.
        /// Tidak ada yang terjadi sampai ada musuh mendekat; saat itu barulah satu pecahan
        /// meluncur. Kalau tidak ada yang datang, muatannya tetap tersimpan.
        ///
        /// Nilainya bukan di damage-nya, tapi di WAKTUNYA: dayanya sudah dibayar sebelum
        /// gerombolan tiba, bukan saat pemain sudah kewalahan.
        /// </summary>
        Orbit,

        /// <summary>
        /// Melompat menjauh dari titik terpadat. Murni kabur — tidak melukai apa pun.
        ///
        /// Menolak menyala saat lapangan lengang, jadi cooldown-nya masih utuh justru waktu
        /// pemain benar-benar terkepung.
        /// </summary>
        Blink,

        /// <summary>Perisai yang MENYERAP damage sampai jatahnya habis, bukan menyembuhkan.</summary>
        Ward,

        /// <summary>
        /// Menempelkan buff ke pemain sendiri. Satu kind untuk semua penguat — haste, cast
        /// lebih cepat, amuk — karena bedanya cuma isi <c>GrantOnCast</c>, bukan kodenya.
        /// </summary>
        Surge,

        /// <summary>Mengisi ulang mana seketika. Menahan diri saat mana masih penuh.</summary>
        Restore,

        /// <summary>
        /// Menandai tanah dulu, baru menghantam setelah jeda. Telegraf-nya bukan hiasan:
        /// itu yang bikin skill ini punya ritme sendiri di tengah layar yang semuanya instan.
        /// </summary>
        SunStrike,

        /// <summary>
        /// Bola yang MENGGELINDING menembus gerombolan dan melukai semua yang dilewati.
        /// Beda dari Line: dia butuh waktu untuk sampai, dan gerombolan sempat bergerak.
        /// </summary>
        RollingBall,

        /// <summary>
        /// Puting beliung: menyeret musuh ke pusatnya, MENGANGKAT mereka sampai tidak bisa
        /// jalan maupun menembak, sambil terus menggerus. Kontrol kerumunan, bukan damage.
        /// </summary>
        Vortex,

        /// <summary>Gelombang yang MELONTARKAN semua musuh menjauh. Membuka ruang, bukan membunuh.</summary>
        ForcePush
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

        /// <summary>
        /// Kecepatan pemain menghindar. Ditambahkan DI BELAKANG, sebelum Count, supaya nilai enum
        /// yang sudah tersimpan di aset tidak bergeser — Count lama (17) tidak pernah dipakai
        /// sebagai data, jadi slot itu aman diambil.
        /// </summary>
        MoveSpeed,

        /// <summary>Memotong durasi debuff yang ditempelkan musuh. 0.4 = durasinya tinggal 60%.</summary>
        DebuffResist,

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
