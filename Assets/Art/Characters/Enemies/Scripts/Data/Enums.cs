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
        ForcePush,

        // --- Gelombang kedua perilaku. Aturan yang sama: DI BELAKANG, tidak pernah disisipkan. ---

        /// <summary>
        /// Bilah yang MENGITARI badan pemain dan melukai apa pun yang tersapu, selama beberapa
        /// detik. Satu-satunya skill yang jangkauannya adalah TUBUH pemain sendiri.
        ///
        /// Nilainya membalik seluruh cara main: setiap skill lain menghukum pemain karena didekati,
        /// yang ini membayarnya. Musuh yang menempel adalah musuh yang tersapu terus-menerus, jadi
        /// build yang memakainya sengaja berdiri di tempat yang paling ramai.
        /// </summary>
        Orbital,

        /// <summary>
        /// Dilempar keluar, melengkung, lalu KEMBALI ke pemain — dan melukai di kedua kakinya.
        ///
        /// Bedanya dari Projectile bukan bentuk melainkan siapa yang menentukan hasilnya: peluru
        /// biasa dibayar sekali dan mengenai apa yang kebetulan ada di jalurnya. Yang ini menagih
        /// dua kali di jalur yang sama, jadi gerombolan yang MENGEJAR pemain berjalan masuk sendiri
        /// ke kaki pulangnya.
        /// </summary>
        Boomerang,

        /// <summary>
        /// Sinar yang MEMANTUL — dari musuh ke musuh, dan dari tepi layar kalau tidak ada musuh
        /// yang bisa disambar. Tiap pantulan menggambar satu ruas garis, dan seluruh ruasnya
        /// tinggal sebagai satu coretan sampai memudar.
        ///
        /// Inilah satu-satunya skill yang tampilannya TUMBUH bersama build: 3 pantulan itu satu
        /// zig-zag, 20 pantulan mencorat-coret seluruh layar. Angka yang menumbuhkannya
        /// <see cref="PieceDefinition.Bounces"/>, jadi segel penambah pantulan terasa langsung
        /// di mata, bukan cuma di angka damage.
        /// </summary>
        Ricochet,

        /// <summary>
        /// Menanam MENARA yang menembak sendiri selama beberapa detik, dari tempat ia ditanam.
        ///
        /// Satu-satunya sumber damage di game ini yang tidak berangkat dari badan pemain. Itu yang
        /// membuatnya beda: pemain boleh kabur, dan tembakannya tetap berlanjut di tempat yang
        /// ditinggalkan — jadi menaruhnya di tengah gerombolan lalu lari adalah permainan yang sah.
        /// </summary>
        Turret,

        /// <summary>
        /// Cincin yang MELEBAR keluar dari pemain, dan cuma tepinya yang melukai.
        ///
        /// Beda dari Nova pada apa yang dibaca: Nova mengisi lingkaran seketika, jadi yang jauh
        /// selalu selamat. Yang ini menyapu dari dalam ke luar dan sampai ke tepi belakangan, jadi
        /// ia mengenai musuh yang saat cast masih di luar jangkauan mana pun.
        /// </summary>
        Shockwave,

        /// <summary>
        /// Rudal yang MELENGKUNG mengejar sasarannya, satu rudal satu musuh. Tidak ada satu pun
        /// yang menghantam sasaran yang sama.
        ///
        /// Ini jawaban untuk musuh yang bergerak: setiap cast lain membidik ke tempat musuh BERADA,
        /// dan gerombolan cepat sudah pergi saat tembakannya sampai.
        /// </summary>
        Seeker,

        /// <summary>
        /// Sinar yang MENGUNCI satu musuh dan terus membakar selama tersambung, sambil menyeretnya
        /// mendekat. Melepas dan mengunci ulang sendiri kalau sasarannya mati.
        ///
        /// Satu-satunya damage yang berjalan TERUS, bukan meletus. Itu membuatnya jawaban untuk
        /// satu musuh bernyawa tebal — boss — sementara seluruh isi buku dituning untuk gerombolan.
        /// </summary>
        Tether,

        /// <summary>
        /// Hujan hantaman beraba-aba: beberapa lingkaran jatuh BERURUTAN di titik-titik berpencar,
        /// bukan satu pukulan besar sekaligus.
        ///
        /// SunStrike menanyakan "sempat menyingkir tidak?" satu kali. Yang ini menanyakannya lima
        /// kali berturut-turut di tempat yang berbeda, jadi menghindarinya adalah gerakan, bukan
        /// satu langkah.
        /// </summary>
        Barrage
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

        // --- Stat yang mengubah PERILAKU, bukan besaran. Ditambahkan sebelum Count; nilai Count
        //     sendiri tidak pernah disimpan sebagai data, jadi menggesernya aman. ---

        /// <summary>
        /// Pantulan tambahan untuk peluru DAN untuk sinar memantul.
        ///
        /// Ini stat pertama di game ini yang membeli KATA KERJA, bukan angka. Sebuah peluru yang
        /// mati di sasaran pertama dan peluru yang memantul tiga kali bukan skill yang sama dengan
        /// dua tingkat kekuatan — mereka membaca lapangan secara berbeda. Dan di sinar memantul,
        /// angka ini yang menumbuhkan coretannya di layar: pemain melihat bukunya menguat tanpa
        /// membaca satu angka pun.
        /// </summary>
        BonusBounces,

        /// <summary>Cabang tambahan untuk rantai petir. Melebarkan, bukan memperdalam.</summary>
        BonusForks,

        /// <summary>
        /// Tambahan untuk apa pun yang dihitung sebagai "berapa banyak": arah semburan radial,
        /// bilah yang mengitari, rudal pengejar, hantaman dalam satu hujan, pecahan mengambang,
        /// dan peluru per tembakan menara.
        ///
        /// SENGAJA tidak menyentuh jumlah lompatan rantai. Rantai sudah punya
        /// <see cref="BonusForks"/>, dan menambah keduanya lewat satu segel berarti sebuah item
        /// menggandakan sasaran rantai secara kuadrat sementara ia menambah linear ke semua yang lain.
        /// </summary>
        BonusHits,

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
