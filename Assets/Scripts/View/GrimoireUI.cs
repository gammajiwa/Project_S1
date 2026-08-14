using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using static Proto.GrimoireLayout;

namespace Proto
{
    /// <summary>
    /// All prototype UI: the two-layer grimoire (grid snapping, lines hidden), the skill backpack,
    /// the sell box, per-spell cooldown dials, the buff panel, wave banner, speed control and
    /// floating combat text. Built entirely from code â€” prototype, no prefabs.
    /// </summary>
    public class GrimoireUI : MonoBehaviour
    {
        // Pixel measurements and screen rects live in GrimoireLayout, pulled in by `using static`
        // above. What stays here is pool sizing — how many widgets to allocate, not where they go.
        const int MaxSpellRows = 8;
        const int FloatPoolSize = 16;
        const int LoosePoolSize = 24;
        const int MaxCellsPerPiece = 9;

        static readonly float[] Speeds = { 1f, 2f, 3f, 5f };
        static readonly string[] SpeedLabels = { "1x", "2x", "3x", "5x" };

        // Grid lines stay hidden until you are holding something.
        // Warna petak SEBELUM art masuk: abu-abu terang tipis, benar di atas papan gelap.
        // Di atas kertas terang ia lenyap — makanya tema boleh menimpanya dengan tinta gelap.
        static readonly Color HiddenCell = new Color(0.5f, 0.5f, 0.6f, 0.05f);
        static readonly Color ShownCell = new Color(0.5f, 0.5f, 0.6f, 0.16f);
        static readonly Color HiddenBagCell = new Color(0.7f, 0.6f, 0.45f, 0.08f);
        static readonly Color ShownBagCell = new Color(0.7f, 0.6f, 0.45f, 0.2f);
        static readonly Color ValidCell = new Color(0.25f, 0.8f, 0.35f, 0.9f);
        static readonly Color InvalidCell = new Color(0.85f, 0.2f, 0.2f, 0.9f);

        public PlayerCaster Player;
        public EnemyManager Enemies;

        /// <summary>Boleh null: arena tanpa biome tetap jalan, cuma tanpa tombol siang/malam.</summary>
        BiomeDresser _biome;

        ContentDatabase _db;
        GameBalance _balance;
        TooltipBuilder _tooltips;

        Grimoire Book => Player.Book;
        readonly Backpack _bag = new Backpack();

        Canvas _canvas;
        Font _font;
        Camera _camera;

        Image[] _baseCells;
        Image[] _skillCells;
        Image[] _bagCells;

        Sprite _circle;
        Image[] _cdBg;
        Image[] _cdFill;
        float[] _pulse;


        // --- shop / recipes ---
        const int ShopSlots = 6;

        // One group needs a connector per pair of ingredients, not a single box, so the pool has to
        // cover (members - 1) segments for every group on the board at once.
        // Bigger now that the pool also carries the "what can this combine with" cables, which fan
        // out from the cursor to every partner on the board at once.
        const int EvoLinePool = 40;

        const float EvoLineThin = 5f;
        const float EvoLineThick = 8f;


        // Blue while the recipe is short a part, gold once it is complete. These are lines now, not
        // a wash over an area, so they can be opaque — a line has to be followed, not just noticed.
        static readonly Color LinkIncomplete = new Color(0.4f, 0.68f, 1f, 0.85f);
        static readonly Color LinkComplete = new Color(1f, 0.84f, 0.32f, 0.95f);

        // Warna ketiga: grup ini tetap berevolusi, tapi hasilnya tidak muat berdiri di papan dan
        // akan KELUAR untuk dipasang ulang. Bukan peringatan gagal — sebuah janji yang ditepati
        // di tempat lain — jadi warnanya tetap hangat, hanya bergeser dari emas ke jingga.
        static readonly Color LinkSpill = new Color(1f, 0.58f, 0.24f, 0.95f);

        // Browsing the codex belongs to the main menu now. A run only ever writes to it.
        DiscoveryLog _codex;

        bool _shopOpen;
        int _rerollCost;
        readonly PieceDefinition[] _shop = new PieceDefinition[ShopSlots];

        // ---------- peta run & pulau rehat ----------
        RunDirector _run;
        bool _mapOpen;

        /// <summary>
        /// Ruangan singgah. Boleh null — tanpa ini, panel toko/kejadian/slot tampil di atas arena
        /// persis seperti sebelum ruangan ada.
        /// </summary>
        RoomLoader _rooms;

        public void AttachRooms(RoomLoader rooms) => _rooms = rooms;

        Image _mapBg;
        Text _mapTitle;
        Text _mapLegend;
        Image[] _mapEdges = System.Array.Empty<Image>();

        // Penyangga pengukur lengkung, dipakai ulang tiap ruas. Dialokasikan sekali: peta penuh
        // menggambar puluhan ruas per frame, dan dua array baru per ruas adalah sampah yang
        // dihasilkan enam puluh kali sedetik untuk hasil yang dibuang seketika.
        readonly Vector2[] _arcPoints = new Vector2[MapArcSamples + 1];
        readonly float[] _arcLengths = new float[MapArcSamples + 1];
        Image[] _mapNodes = System.Array.Empty<Image>();
        Image[] _mapRings = System.Array.Empty<Image>();
        Text[] _mapGlyphs = System.Array.Empty<Text>();
        Text _mapYou;
        int _mapSig = -1;

        // Bahan tampilan: kertas, bingkai, warna tinta. Boleh null — tiap pemakainya wajib jatuh
        // kembali ke kotak warna datar, supaya art yang belum ada tidak pernah memblokir tes.
        UiTheme _theme;

        /// <summary>Font khusus angka damage. Jatuh ke <c>_font</c> kalau tema tidak memisahkannya.</summary>
        Font _numberFont;

        // Dua lapis bingkai di belakang papan grimoire. Disimpan supaya bisa disembunyikan
        // bersama papannya nanti; sekarang keduanya hidup selama run berjalan.
        Image _grimoireFrame;
        Image _gridFrame;

        // Warna petak yang BERLAKU. Begitu ADA tema, latarnya kertas terang — dan di atas kertas
        // terang yang harus dipakai tinta gelap, bukan abu-abu terang yang benar di papan gelap.
        Color CellIdle => _theme != null ? _theme.GridCellIdle : HiddenCell;
        Color CellShown => _theme != null ? _theme.GridCellShown : ShownCell;

        // Kegelapan yang menggerogoti tepi panel peta. Material sendiri (bukan aset di disk)
        // karena ukurannya harus diberitahu ke shader tiap kali panelnya berubah bentuk, dan
        // dua panel berbeda ukuran tidak boleh saling menimpa nilai itu.
        Image _mapGloom;
        Material _mapGloomMat;

        // Material KEDUA dari shader yang sama, dipasang di perkamennya sendiri: ia yang
        // memakan alpha di pita terluar. Tanpa itu peta tetap berakhir sebagai potongan
        // persegi — menggelapkan tepi saja cuma menghasilkan persegi yang gelap.
        Material _mapPaperMat;
        Vector2 _mapGloomSize = new Vector2(-1f, -1f);

        // ---------- mode MEMILIH di peta (pengganti portal fisik) ----------
        //
        // Peta yang sama punya dua wajah: diintip lewat M (kaca, murni baca) dan mode memilih
        // (dibuka RunDirector begitu layar gelap penuh — klik node = berangkat). Penandanya
        // berjalan dulu di peta, baru tirai menutup dan node dieksekusi dalam gelap.
        bool _mapChoose;
        Image _mapMark;
        int _mapTravelTo = -1;
        float _mapTravelT;

        // Induk semua elemen peta. Satu transform yang bisa diangkat ke atas tirai (peta tampil
        // di layar yang sudah hitam) atau dibiarkan di bawahnya (tirai menelan peta saat node
        // dieksekusi) — tanpa memindahkan enam ratus segmen satu-satu.
        RectTransform _mapRoot;

        // Peta tegak ala STS: lantai menumpuk ke atas dengan jarak TETAP, bukan dipadatkan
        // supaya muat — act yang tidak muat di panel diintip lewat roda mouse ATAU drag.
        float _mapScroll;
        bool _mapDragging;
        Vector2 _mapDragLast;

        // Jarak antar lantai. Renggang itu keputusan, bukan sisa ruang: node yang berdempetan
        // membuat jalur antar lantai jadi garis pendek yang tak terbaca arahnya, dan seluruh
        // act terlihat bantet walau isinya banyak.
        const float MapFloorGap = 170f;

        // Tirai hitam di atas SEGALANYA: menutup pergantian dari peta ke tempat baru.
        Image _fadeCover;
        float _coverT;
        bool _coverRising;

        const float CoverInSeconds = 0.35f;
        const float CoverOutSeconds = 0.45f;

        // Jalur bezier putus-putus ala peta referensi.
        //
        // Dulu tiap ruas dibagi menjadi JUMLAH segmen yang sama (7), dan itu keliru: ruas panjang
        // keluar dengan garis panjang, ruas pendek dengan garis pendek, dan mata membaca
        // perbedaan itu sebagai peta yang digambar asal-asalan. Yang harus tetap sama panjang
        // GARISNYA, bukan berapa banyak.
        //
        // Sekarang jumlahnya diturunkan dari panjang lengkungnya. Panjang garis tetap; yang
        // sedikit menyesuaikan jarak antar garis, supaya tiap ruas berakhir pas di nodenya alih-alih
        // meninggalkan potongan tanggung.
        const float MapDashLength = 17f;
        const float MapDashPitch = 27f;

        /// <summary>Atap segmen per ruas — ini yang menentukan besar kolam Image, bukan jumlah pastinya.</summary>
        const int MapSegsPerEdge = 26;

        /// <summary>Titik pencuplikan bezier untuk mengukur panjang lengkungnya.</summary>
        const int MapArcSamples = 24;

        bool _gambleOpen;
        float _spinLeft;
        int _slotOutcome = -1;
        string _slotResultLine = "";
        Image _slotBg;
        Text _slotTitle;
        readonly Text[] _slotReels = new Text[3];
        Image _slotSpinBg;
        Text _slotSpinLabel;
        Text _slotInfo;

        bool _eventOpen;
        bool _eventDone;
        Image _eventBg;
        Text _eventTitle;
        Text _eventBody;
        Image _eventABg;
        Image _eventBBg;
        Text _eventALabel;
        Text _eventBLabel;
        Image _eventCBg;
        Text _eventCLabel;

        /// <summary>
        /// Dua pakta yang sedang ditawarkan. Diundi SEKALI saat pemain mendarat di pulaunya,
        /// bukan tiap frame gambar: undian per frame berarti kartunya berganti-ganti di depan mata
        /// pemain yang sedang membacanya, dan yang akhirnya diklik bukan yang dibaca.
        /// </summary>
        readonly WorldModifierDefinition[] _pactOffer = new WorldModifierDefinition[2];

        Image[] _evoLines;
        List<EvoPreview> _previews = new List<EvoPreview>();
        List<EvoPreview> _bagPreviews = new List<EvoPreview>();
        float _previewTimer;

        /// <summary>Scratch buffer for ordering one group's ingredients. A recipe takes at most 3.</summary>
        readonly Vector2[] _evoWalk = new Vector2[4];

        List<Vector2> _partners = new List<Vector2>();
        PieceDefinition _partnerFor;
        float _partnerTimer;

        // Last previewed drag position, so the lines can be recomputed the moment the cursor moves
        // instead of waiting out the idle throttle.
        PieceDefinition _ghostDef;
        Vector2Int _ghostOrigin;
        int _ghostRot;

        Image _panelBg;
        Text _panelTitle;
        Image[] _shopSlotBg;
        Text[] _shopSlotText;
        Image _rerollBg;
        Text _rerollLabel;
        Image _shopBtnBg;
        Text _shopBtnLabel;

        // --- layar GAME OVER ---
        Image _overVeil;
        Text _overTitle;
        Text _overInfo;
        Image _overMenuBg;
        Text _overMenuLabel;
        float _overFade;

        // --- umpan balik player kena hit ---
        Image _hurtVeil;
        float _hurtGlow;

        // --- tile rune: SATU tile utuh per PETAK yang diduduki, di papan maupun di tas ---
        RuneTilePool _boardTiles;
        RuneTilePool _bagTiles;
        RuneTilePool _looseTiles;
        readonly System.Collections.Generic.HashSet<RuneInstance> _tiledPieces =
            new System.Collections.Generic.HashSet<RuneInstance>();

        /// <summary>
        /// Every drop lands scattered across the screen. Whatever is still lying around when a
        /// wave starts gets sold.
        /// </summary>
        readonly List<PieceDefinition> _loose = new List<PieceDefinition>();
        readonly List<Vector2> _loosePos = new List<Vector2>();

        /// <summary>Flat pool of cells used to draw loose pieces and the piece on the cursor.</summary>
        Image[] _looseCells;

        PieceDefinition _held;
        int _heldRot;

        /// <summary>
        /// Skills lifted along with the rune currently in hand.
        ///
        /// Dropped the moment the rune is rotated: their offsets are expressed in the rune's
        /// un-rotated frame, and quietly re-deriving them through a rotation is the kind of thing
        /// that looks right until a 3-cell base is turned twice.
        /// </summary>
        List<Grimoire.Rider> _riders = new List<Grimoire.Rider>();

        int _gold;

        // Swallows any stray click/keypress carried in from entering play mode.
        float _inputLock = 0.4f;

        Image[] _spellBg;
        Image[] _spellFill;
        Text[] _spellText;

        /// <summary>Row order for the panel: indices into Book.Spells, sorted by damage.</summary>
        readonly int[] _spellOrder = new int[MaxSpellRows];

        Image[] _speedButtons;
        Text[] _speedLabels;
        int _speedSlot;

        // --- damage meter ---
        readonly DamageMeter _meter = new DamageMeter();
        Text _meterText;
        float _meterTimer;

        StatusStrip _buffStrip;
        StatusStrip _debuffStrip;
        StatusStrip _ailmentStrip;

        /// <summary>Kolom tegak di tepi kanan: pakta run ini. Terpisah dari tiga strip di kiri.</summary>
        StatusStrip _pactStrip;

        /// <summary>
        /// Versi pakta yang terakhir digambar. Strip pakta hanya berubah beberapa kali per RUN,
        /// jadi menyusunnya ulang tiap frame — bersama string tooltip-nya, yang dirakit dari
        /// beberapa potongan teks — adalah kerja sampah untuk jawaban yang sama persis.
        /// </summary>
        int _pactVersionDrawn = -1;

        // Pojok kiri-atas tiap strip, dihitung SEKALI saat dibangun. Kotak penempatnya tidak
        // bergerak saat main, jadi menanyakan sudut dunianya tiap frame cuma menambah kerja untuk
        // jawaban yang sama.
        Vector2 _buffOrigin;
        Vector2 _debuffOrigin;
        Vector2 _ailmentOrigin;
        Vector2 _pactOrigin;

        Text _hudText;
        Image _hpBg;
        Image _hpChip;
        Image _hpFill;

        /// <summary>
        /// Warna diam kedua bar, dicatat SEKALI saat dibangun.
        ///
        /// Perlu dicatat karena kilat kena-pukul dan cerah mana-penuh keduanya menulis ke
        /// <c>Image.color</c> tiap frame. Tanpa titik pulang yang tersimpan, keduanya harus
        /// bertolak dari warna tetap di kode — dan itu membuang pewarnaan yang disetel di
        /// prefab pada kilat pertama, selamanya.
        /// </summary>
        Color _hpFillBase = HpFillColor;

        Color _manaFillBase = ManaFillColor;
        Text _hpLabel;
        Image _manaBg;
        Image _manaFill;
        Text _manaLabel;

        // Kotak yang menyalakan kartu keterangan HP/mana. Boleh sama dengan isiannya; boleh juga
        // kotak lain yang mencakup bingkai bolanya — lihat VitalsRig.
        RectTransform _hpHover;
        RectTransform _manaHover;

        // Material cairan per bola, atau null kalau bolanya tidak memakai isian menegak.
        // Salinan, bukan aset — jadi wajib dibuang sendiri di OnDestroy.
        Material _hpLiquid;
        Material _manaLiquid;

        // Animated bar state. The fill chases the real value; the chip trails behind it so you can
        // see how much was just taken off, which a bar that snaps can never show.
        float _hpShown = 1f;
        float _hpChipShown = 1f;
        float _manaShown = 1f;
        float _hurtFlash;
        static readonly Color HpFillColor = new Color(0.85f, 0.28f, 0.3f, 0.95f);
        static readonly Color HpChipColor = new Color(1f, 0.78f, 0.6f, 0.75f);
        static readonly Color ManaFillColor = new Color(0.35f, 0.6f, 1f, 0.95f);
        Image _tipBg;
        Text _tipText;
        Text _heldText;
        Text _bannerText;
        Text _gridTitle;
        Text _evolveText;
        float _evolveTimer;

        Image _startBg;
        Text _startLabel;

        Text[] _floaters;
        float[] _floatLife;
        Vector3[] _floatWorld;

        DamagePopups _popups;
        EnemyHpBars _enemyBars;
        RecipePanel _recipes;

        readonly StringBuilder _sb = new StringBuilder(256);

        public void Init(PlayerCaster player, EnemyManager enemies, Camera cam,
            ContentDatabase database, GameBalance balance, BiomeDresser biome = null,
            UiTheme theme = null)
        {
            Player = player;
            Enemies = enemies;
            _biome = biome;
            _camera = cam;
            _db = database;
            _balance = balance;
            _theme = theme;
            _tooltips = new TooltipBuilder(balance);
            _rerollCost = balance.RerollCostStart;

            // Font dari tema kalau ada; bawaan Unity kalau tidak. Fallback-nya dipertahankan
            // karena seluruh UI run digambar dari kode — tema yang lupa diisi tidak boleh
            // membuat setengah layar jadi kotak kosong.
            _font = _theme != null && _theme.UiFont != null
                ? _theme.UiFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            _numberFont = _theme != null && _theme.NumberFont != null ? _theme.NumberFont : _font;

            BuildCanvas();

            // Hidup di dunia, bukan di kanvas, jadi ia berdiri sendiri di luar hierarki UI.
            var pickupGo = new GameObject("DropPickups");
            _pickups = pickupGo.AddComponent<DropPickups>();
            _pickups.Init(player.transform, piece => AddLoose(piece));

            // Built first on purpose: on this canvas creation order is draw order, so the damage
            // numbers pass UNDER the grimoire and the panels instead of covering them.
            _popups = new DamagePopups(_canvas.transform, _numberFont, cam);

            // Ikut lewat di bawah grimoire dan panel, alasan yang sama dengan angka damage:
            // palang di atas kepala musuh tidak pernah boleh menutupi papan yang sedang ditata.
            _enemyBars = new EnemyHpBars(_canvas.transform, cam, Enemies,
                Resources.Load<GameObject>("EnemyHpBar"));

            BuildGrid();
            BuildSkillWidgets();
            BuildBackpack();
            BuildLoose();
            BuildShop();
            _codex = DiscoveryLog.Load();
            BuildSpellPanel();
            BuildSpeedControl();
            BuildHud();
            BuildBossBar();
            BuildMeter();
            BuildFloaters();

            // Built last: on this canvas creation order is draw order, and the recipe card has to
            // sit on top of everything it is explaining.
            _recipes = new RecipePanel(_canvas.transform, _font, _db, OwnedCount);

            Enemies.OnWaveCleared += OnWaveCleared;
            Enemies.OnKill += OnEnemyKilled;
            Enemies.OnDamage += _meter.Record;
            // Toggle performa dari menu: langganannya yang tidak dipasang, bukan tiap push
            // diperiksa. Wave besar melahirkan ratusan hit per detik, dan gerbang yang dibayar
            // per event harganya nyata justru di mesin yang toggle ini coba selamatkan.
            if (GameSettings.Load().DamageText) Enemies.OnEnemyDamaged += _popups.Push;

            // VFX hit per skill hidup di PlayerCaster (SpawnHitVfx) — bukan di sini.
            Player.OnHurt += () => _hurtGlow = 1f;

            Player.OnCast += OnSpellCast;
            Enemies.OnReaction += (pos, rx) => PushFloater(pos, rx.DisplayName + "!", rx.FlashColor);

            // Saklar curang tetap menang atas pilihan pemain: itu memang gunanya, dan starter yang
            // dipaksa dari DebugConfig harus tetap dipaksa walau menu barusan memilih yang lain.
            var cheats = Player.Cheats;
            var hero = cheats != null && cheats.LoadoutOverride != null
                ? cheats.LoadoutOverride
                : HeroChoice.Resolve(_db);

            ApplyLoadout(hero);

            // Stat SESUDAH papannya tersusun, dan itu urutan yang penting: langkah ini mengisi HP
            // dan mana sampai penuh, dan "penuh" termasuk bonus dari rune yang barusan didudukkan.
            // Dijalankan lebih dulu, pemain membuka run dengan bola yang tidak pernah penuh —
            // maksimumnya naik karena rune, isinya tertinggal di angka dasar.
            Player.ApplyLoadoutStats(hero);

            for (int i = 0; i < Book.Placed.Count; i++) Discover(Book.Placed[i].Def);

            SetSpeed(0);
            Redraw();

            ApplyCheats(cheats);
        }

        /// <summary>
        /// Membuka peta langsung di awal run, tanpa fase SUSUN GRIMOIRE-MU di depannya.
        ///
        /// <b>Dipanggil dari <see cref="AttachRun"/>, bukan dari <c>Init</c>.</b> Itu bukan selera:
        /// <c>ProtoBootstrap</c> membuat UI LEBIH DULU dari sutradara run — sutradaranya mengecek
        /// <c>WaveActive</c> saat lahir, jadi ia harus lahir belakangan. Dipanggil dari
        /// <c>Init</c>, <c>_run</c> masih null dan seluruh fungsi ini keluar diam-diam: tidak ada
        /// error, tidak ada peringatan, dan yang terlihat cuma peta yang tidak pernah terbuka.
        ///
        /// Memakai <see cref="RunDirector.DepartNow"/> — versi tanpa transisi. Yang berjalan
        /// pelan bukan cuma lambat, ia memaksa menatap arena KOSONG selama <c>MapFadeClose</c>
        /// detik: belum ada satu pun musuh, dan tidak ada apa pun di sana untuk dilihat.
        ///
        /// Dilewati kalau curang meminta langsung lompat ke wave tertentu: yang menyalakannya
        /// sedang menguji pertempuran, dan peta pemilih di depannya cuma penghalang.
        /// </summary>
        void OpenRunOnMap()
        {
            if (_run == null || _balance == null || !_balance.MapOpensRun) return;

            var cheats = Player.Cheats;
            if (cheats != null && cheats.Enabled && cheats.OpeningWave > 0) return;

            _run.DepartNow();
        }

        /// <summary>
        /// Saklar curang yang menyentuh UI dan alur wave. Sengaja dipanggil paling akhir: dia
        /// memulai wave, dan itu tidak boleh terjadi sebelum papan tersusun.
        /// </summary>
        void ApplyCheats(DebugConfig cheats)
        {
            if (cheats == null || !cheats.Enabled) return;

            if (cheats.CheatHideUI) _canvas.enabled = false;

            if (cheats.OpeningTimeScale != 1f) Time.timeScale = cheats.OpeningTimeScale;

            // Melompat ke wave mana pun. Papan sudah terisi di atas, jadi buku sihirnya sudah
            // punya isi saat gerombolan pertama tiba.
            if (cheats.OpeningWave > 0) Enemies.StartWave(cheats.OpeningWave);
        }

        /// <summary>
        /// Seats a hero's opening board. Runes go down first — a skill cannot stand on nothing.
        ///
        /// Positions come from the asset rather than from auto-placement, because where the two
        /// opening skills sit IS the first decision of a run: left apart they keep firing as two,
        /// pushed together they merge into one 2-star at the end of the wave. Auto-placement would
        /// have dropped them side by side and made that choice for the player.
        /// </summary>
        void ApplyLoadout(HeroLoadout hero)
        {
            if (hero == null)
            {
                Debug.LogError("[GrimoireUI] Tidak ada HeroLoadout di ContentDatabase — " +
                               "jalankan Tools/Grimoire/Generate Heroes.", this);
                return;
            }

            for (int pass = 0; pass < 2; pass++)
            {
                bool wantRunes = pass == 0;

                for (int i = 0; i < hero.Placed.Length; i++)
                {
                    var seat = hero.Placed[i];
                    if (seat.Piece == null || seat.Piece.IsRune != wantRunes) continue;

                    if (Book.Place(seat.Piece, seat.Origin, seat.Rot) == null)
                    {
                        Debug.LogWarning($"[GrimoireUI] '{hero.Id}': {seat.Piece.Id} tidak muat di " +
                                         $"{seat.Origin}, dilempar ke lantai.", this);
                        AddLoose(seat.Piece);
                    }
                }
            }

            for (int i = 0; i < hero.Loose.Length; i++) AddLoose(hero.Loose[i]);
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;

            // Salinan material tidak ikut mati bersama GameObject-nya. Scene game bisa dimuat
            // ulang berkali-kali dalam satu sesi (mati, ulangi run), dan dua material bocor per
            // run menumpuk tanpa ada yang pernah melihatnya.
            if (_hpLiquid != null) Destroy(_hpLiquid);
            if (_manaLiquid != null) Destroy(_manaLiquid);
        }

        const string GameSceneName = "Proto";
        const string MainMenuSceneName = "MainMenu";

        /// <summary>By name, not by build index: reloading by index breaks silently the moment the
        /// build list is reordered, and it was already broken while Proto sat outside that list.</summary>
        /// <summary>
        /// Tidak lagi static, dan itu bukan kebetulan: tirai layar muat butuh sigil dari
        /// <see cref="UiTheme"/>, dan tema itu milik instance. Kelima pemanggilnya sudah berada
        /// di dalam metode instance, jadi tidak ada yang berubah di sisi pemanggil.
        ///
        /// timeScale dikembalikan SEBELUM tirai dipanggil. Kembali ke menu bisa terjadi dari
        /// dalam fase menyusun grid yang membekukan waktu, dan tirai yang animasinya memakai
        /// waktu berskala akan menggantung di layar selamanya — meskipun ia sendiri memakai
        /// unscaled, coroutine yang menunggunya tidak boleh bergantung pada nasib itu.
        /// </summary>
        void LoadScene(string sceneName)
        {
            Time.timeScale = 1f;
            LoadingScreen.Go(sceneName, _theme != null ? _theme.LoadingSigil : null,
                new Color(0.72f, 0.5f, 1f, 1f));
        }

        // ---------- construction ----------

        void BuildCanvas()
        {
            var go = new GameObject("Canvas");
            go.transform.SetParent(transform, false);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            // Seluruh UI permainan memakai hit-test sendiri (posisi mouse -> petak), jadi kanvas
            // ini hidup lama tanpa raycaster dan tidak ada yang sadar. Yang menagihnya adalah
            // barang UGUI beneran: halaman setelan yang dibuka ESC, tombol KELUAR KE MENU,
            // stepper, slider. Tanpa GraphicRaycaster, EventSystem tidak menemukan satu pun
            // grafik di kanvas ini dan tombolnya tidak mati dengan error — dia cuma DIAM.
            go.AddComponent<GraphicRaycaster>();
        }

        Image MakeImage(string name, Vector2 pos, Vector2 size, Color color, Vector2 anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);

            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return img;
        }

        Text MakeText(string name, Vector2 pos, Vector2 size, int fontSize, Color color,
            Vector2 anchor, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);

            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return text;
        }

        void BuildGrid()
        {
            int count = Grimoire.Width * Grimoire.Height;
            _baseCells = new Image[count];
            _skillCells = new Image[count];

            // Bingkai DULU, petak belakangan: di kanvas ini urutan bikin adalah urutan gambar,
            // jadi apa pun yang dibuat setelah ini duduk di atasnya. Dua lapis, dari luar ke
            // dalam — sampul buku memeluk seluruh papan, bingkai emas memeluk petaknya saja.
            BuildGridFrames();

            // Dua lapisan dibuat TERPISAH, bukan berselang-seling per petak. Urutan bikin adalah
            // urutan gambar di kanvas ini, dan tile rune harus disisipkan di ANTARA keduanya:
            // di atas kotak warna dasar, tapi di bawah kotak skill — skill berdiri DI ATAS rune,
            // dan tile yang latarnya pekat akan menelannya kalau urutannya terbalik.
            for (int y = 0; y < Grimoire.Height; y++)
            {
                for (int x = 0; x < Grimoire.Width; x++)
                {
                    int i = y * Grimoire.Width + x;

                    _baseCells[i] = MakeImage($"Base_{x}_{y}", CellAnchor(x, y),
                        new Vector2(CellSize, CellSize), CellIdle, Vector2.zero);
                }
            }

            for (int y = 0; y < Grimoire.Height; y++)
            {
                for (int x = 0; x < Grimoire.Width; x++)
                {
                    int i = y * Grimoire.Width + x;

                    _skillCells[i] = MakeImage($"Skill_{x}_{y}",
                        CellAnchor(x, y) + new Vector2(SkillInset, SkillInset),
                        new Vector2(CellSize - SkillInset * 2, CellSize - SkillInset * 2),
                        Color.white, Vector2.zero);
                    _skillCells[i].enabled = false;
                }
            }

            // Tempat bawaannya pojok kiri tepat di atas petak — benar selama papan masih polos,
            // dan langsung salah begitu prefabnya menaruh hiasan di sana.
            var titleAt = new Vector2(GridX, GridTop() + 8);

            if (_gridRig != null && _gridRig.TitleArea != null)
                titleAt = CanvasRectOf(_gridRig.TitleArea).min;

            _gridTitle = MakeText("GridTitle", titleAt, new Vector2(400, 24), 17,
                _theme != null ? _theme.GridTitleInk : new Color(0.85f, 0.82f, 0.95f),
                Vector2.zero, TextAnchor.LowerLeft);
            _gridTitle.text = "GRIMOIRE";

            // Prefab yang sudah membawa ornamen judulnya sendiri mematikan tulisan bawaan.
            // Objeknya tetap DIBUAT, cuma tidak digambar: UpdateHud menulis ke sini tiap frame,
            // dan membiarkannya null berarti menyebar pemeriksaan null ke seluruh pemakainya.
            if (_gridRig != null && !_gridRig.ShowTitle) _gridTitle.enabled = false;

            // Below the three icon strips, which now own the band straight under the mana bar.
            _heldText = MakeText("HeldInfo", new Vector2(Margin, StripAilmentY - 34f),
                new Vector2(880, 22), 13, new Color(0.85f, 0.85f, 0.6f), new Vector2(0f, 1f),
                TextAnchor.UpperLeft);

            _evolveText = MakeText("EvolveInfo", new Vector2(Margin, StripAilmentY - 56f),
                new Vector2(880, 22), 14, new Color(0.55f, 1f, 0.7f), new Vector2(0f, 1f),
                TextAnchor.UpperLeft);
            _evolveText.text = "";
        }

        /// <summary>
        /// Dua lapis bingkai di belakang petak grimoire: sampul buku di luar, bingkai emas di
        /// dalam. Keduanya opsional — tanpa <see cref="UiTheme"/> atau tanpa sprite, papan
        /// kembali jadi petak polos persis seperti sebelum art masuk.
        /// </summary>
        /// <summary>
        /// Penanda dari prefab papan yang sedang berlaku, disimpan karena judul dibangun SESUDAH
        /// bingkainya dan butuh membaca setelan yang sama. Null = tidak ada prefab, atau prefabnya
        /// tidak membawa penanda.
        /// </summary>
        GrimoireGridArea _gridRig;

        /// <summary>Lingkaran rune di papan, kalau prefabnya membawa satu. Boleh null.</summary>
        GrimoireRune _rune;

        void BuildGridFrames()
        {
            // Dikosongkan lebih dulu, SELALU. Play mode tanpa domain reload mempertahankan nilai
            // static, dan papan yang kali ini tidak punya prefab akan diam-diam memakai kotak
            // milik run sebelumnya kalau baris ini tidak ada.
            GridOverride = null;
            GridGapOverride = null;
            _gridRig = null;
            _rune = null;

            if (_theme == null) return;

            var grid = GridRect();

            // --- jalur utama: PREFAB, dan kode tidak boleh ikut campur isinya ---
            //
            // Yang ditentukan kode cuma satu: di mana pojok kiri-bawah papan berada, karena itu
            // yang harus sejajar dengan petak 7x7. Selebihnya — ukuran, anchor, hiasan, urutan
            // anak — milik prefab. Begitu kode ikut menyetel sizeDelta, tiap perubahan di
            // prefab akan tertimpa diam-diam saat run, dan yang mengubahnya tidak akan pernah
            // tahu kenapa.
            if (_theme.GrimoirePanelPrefab != null)
            {
                var go = Instantiate(_theme.GrimoirePanelPrefab, _canvas.transform, false);
                go.name = "GrimoirePanel";

                var rt = go.transform as RectTransform;
                var marker = go.GetComponentInChildren<GrimoireGridArea>(true);
                var area = marker != null ? marker.transform as RectTransform
                                          : FindGridArea(go.transform);

                if (marker != null)
                {
                    _gridRig = marker;
                    GridGapOverride = marker.Gap;
                }

                _rune = go.GetComponentInChildren<GrimoireRune>(true);

                if (area != null)
                {
                    // Prefab yang membawa GridArea memegang kendali PENUH: letak papan, ukuran
                    // papan, dan sekarang juga letak & ukuran petaknya. Kode berhenti menghitung
                    // pojok papan — menggeser papan di prefab menggeser petak 7x7 bersamanya,
                    // yang memang seluruh alasan papan ini dijadikan prefab.
                    GridOverride = CanvasRectOf(area);
                }
                else if (rt != null)
                {
                    // Prefab tanpa GridArea: papan tetap didudukkan sejajar petak hitungan lama,
                    // persis seperti sebelumnya. Ukuran dan anak-anaknya tetap tidak disentuh.
                    rt.anchorMin = rt.anchorMax = Vector2.zero;
                    rt.anchoredPosition = new Vector2(grid.xMin, grid.yMin);
                }

                _grimoireFrame = go.GetComponent<Image>();
                return;
            }

            // --- jalur lama: satu sprite dipelarkan ke kotak hitungan ---
            if (_theme.GrimoireFrame != null)
            {
                _grimoireFrame = MakeFrame("GrimoireFrame", _theme.GrimoireFrame,
                    Expand(grid, _theme.GrimoirePad));
            }
        }

        /// <summary>
        /// Cadangan pencari kotak petak: anak yang cuma BERNAMA <c>GridArea</c>, tanpa komponen.
        ///
        /// Jalur utamanya <see cref="GrimoireGridArea"/> — komponen itu menggambar petaknya di
        /// Scene view, jadi papan bisa ditata sambil dilihat alih-alih ditebak lalu diuji di play
        /// mode. Pencarian lewat nama dipertahankan supaya prefab yang terlanjur dibuat sebelum
        /// komponennya ada tetap jalan. Dua-duanya tidak ketemu = papan kembali ke petak hitungan
        /// lama, bukan error dan bukan petak seukuran nol.
        /// </summary>
        static RectTransform FindGridArea(Transform root)
        {
            var all = root.GetComponentsInChildren<RectTransform>(true);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == "GridArea") return all[i];
            }

            return null;
        }

        /// <summary>
        /// Kotak sebuah RectTransform dalam koordinat yang dipakai seluruh UI ini: piksel dari
        /// pojok KIRI-BAWAH layar.
        ///
        /// Sudutnya diambil di ruang dunia lalu dikembalikan ke ruang kanvas, bukan dibaca dari
        /// <c>anchoredPosition</c>: kotaknya boleh bersarang beberapa lapis di dalam prefab, dan
        /// posisi berjangkar hanya berarti relatif terhadap induk terdekatnya. Titik nol digeser
        /// pakai <c>rect.min</c> milik kanvas — bukan setengah ukurannya — supaya kanvas berpivot
        /// tidak-di-tengah tidak menggeser seluruh papan.
        /// </summary>
        Rect CanvasRectOf(RectTransform area)
        {
            var canvasRt = _canvas.transform as RectTransform;

            var corners = new Vector3[4];
            area.GetWorldCorners(corners);

            if (canvasRt == null)
                return new Rect(corners[0].x, corners[0].y,
                                corners[2].x - corners[0].x, corners[2].y - corners[0].y);

            Vector2 min = canvasRt.InverseTransformPoint(corners[0]);
            Vector2 max = canvasRt.InverseTransformPoint(corners[2]);

            // Kanvas yang baru dibuat di frame ini bisa belum punya rect terhitung; layar adalah
            // jawaban yang benar untuk Overlay dengan scaleFactor 1, yaitu persis kanvas ini.
            Vector2 origin = canvasRt.rect.size.x < 1f || canvasRt.rect.size.y < 1f
                ? new Vector2(-Screen.width * 0.5f, -Screen.height * 0.5f)
                : canvasRt.rect.min;

            return new Rect(min - origin, max - min);
        }

        /// <summary>
        /// Satu gambar bingkai yang didudukkan di kotak layar tertentu.
        ///
        /// <c>Image.Type.Simple</c>, bukan <c>Sliced</c>: sembilan-irisan butuh
        /// <c>spriteBorder</c> yang disetel di import, dan bingkai berhias seperti ini punya
        /// sudut yang tidak boleh melar sama sekali. Selama kotak tujuannya sebangun dengan
        /// gambarnya, melarnya tidak terbaca; begitu <c>spriteBorder</c> diisi nanti, tinggal
        /// tukar satu baris ini.
        /// </summary>
        Image MakeFrame(string name, Sprite sprite, Rect rect)
        {
            var img = MakeImage(name, new Vector2(rect.xMin, rect.yMin), rect.size,
                Color.white, Vector2.zero);
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            return img;
        }

        /// <summary>One radial cooldown dial per active skill, drawn on top of its cells.</summary>
        void BuildSkillWidgets()
        {
            _circle = MakeCircleSprite(64);
            _cdBg = new Image[MaxSpellRows];
            _cdFill = new Image[MaxSpellRows];
            _pulse = new float[MaxSpellRows];

            var size = new Vector2(CooldownDiameter, CooldownDiameter);

            for (int i = 0; i < MaxSpellRows; i++)
            {
                _cdBg[i] = MakeImage($"CdBg_{i}", Vector2.zero, size,
                    new Color(0.04f, 0.04f, 0.07f, 0.8f), Vector2.zero);
                _cdBg[i].sprite = _circle;
                _cdBg[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _cdBg[i].enabled = false;

                _cdFill[i] = MakeImage($"CdFill_{i}", Vector2.zero, size, Color.white, Vector2.zero);
                _cdFill[i].sprite = _circle;
                _cdFill[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _cdFill[i].type = Image.Type.Filled;
                _cdFill[i].fillMethod = Image.FillMethod.Radial360;
                _cdFill[i].fillOrigin = (int)Image.Origin360.Top;
                _cdFill[i].fillClockwise = true;
                _cdFill[i].enabled = false;
            }
        }

        static Sprite MakeCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp
            };

            float r = size * 0.5f;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        void BuildBackpack()
        {
            _bagCells = new Image[Backpack.Width * Backpack.Height];

            for (int y = 0; y < Backpack.Height; y++)
            {
                for (int x = 0; x < Backpack.Width; x++)
                {
                    int i = y * Backpack.Width + x;
                    _bagCells[i] = MakeImage($"Bag_{x}_{y}", BagAnchor(x, y),
                        new Vector2(BagCell, BagCell), HiddenBagCell, Vector2.zero);
                }
            }

            MakeText("BagTitle", new Vector2(RightX(), BagY + Backpack.Height * (BagCell + BagGap) + 2),
                new Vector2(300, 20), 13, new Color(0.85f, 0.8f, 0.6f),
                Vector2.zero, TextAnchor.LowerLeft).text = "TAS";
        }

        void BuildLoose()
        {
            // +1 piece worth of cells so the held piece can ride the cursor.
            // loose pieces + the carried piece + one preview per shop slot
            _looseCells = new Image[(LoosePoolSize + 1 + ShopSlots) * MaxCellsPerPiece];

            for (int i = 0; i < _looseCells.Length; i++)
            {
                _looseCells[i] = MakeImage($"LooseCell_{i}", Vector2.zero,
                    new Vector2(LooseCellSize, LooseCellSize), Color.white, Vector2.zero);
                _looseCells[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _looseCells[i].enabled = false;
            }
        }

        /// <summary>Draws one piece centred on <paramref name="center"/>. Returns cells consumed.</summary>
        int DrawPiece(PieceDefinition def, int rot, Vector2 center, int cursor, float alpha)
        {
            var shape = Shapes.Rotate(def.Cells, rot);
            var size = PieceSize(shape);
            float step = LooseCellSize + LooseCellGap;
            Vector2 origin = center - size * 0.5f;

            bool isSkill = def.Layer == Layer.Skill;
            float inner = isSkill ? LooseCellSize - SkillInset * 2f : LooseCellSize;
            var color = new Color(def.Color.r, def.Color.g, def.Color.b, alpha);

            // Rune sudah berwujud rune SEJAK JATUH, bukan sejak didudukkan di papan. Sebelumnya
            // ia jatuh sebagai kotak warna dan baru berubah jadi tile begitu ditaruh — dan barang
            // yang berganti rupa saat dipindahkan membuat pemain mengira ia mengambil benda yang
            // lain. Petaknya tetap dihitung di sini; yang berbeda cuma apa yang digambar di atasnya.
            bool tiled = RuneTiles.IsRuneGlyph(def.Icon);

            for (int i = 0; i < shape.Length && cursor < _looseCells.Length; i++, cursor++)
            {
                var img = _looseCells[cursor];

                // Kotak warnanya tetap DITATA walau tidak digambar: letak dan ukurannya yang
                // dipakai tile di bawah ini, jadi satu rumus posisi melayani dua rupa.
                img.enabled = !tiled;
                img.color = color;
                img.rectTransform.sizeDelta = new Vector2(inner, inner);
                img.rectTransform.anchoredPosition = origin + new Vector2(
                    shape[i].x * step + LooseCellSize * 0.5f,
                    shape[i].y * step + LooseCellSize * 0.5f);

                if (!tiled || _looseTiles == null) continue;

                var tile = _looseTiles.Take();
                tile.Cover(img.rectTransform);
                tile.Bind(RuneTiles.BakedTileAt(def, i), RuneTiles.GlyphAt(def, i), def.Color, alpha);
            }

            return cursor;
        }

        /// <summary>
        /// Layar GAME OVER: seluruh layar MEMERAH, judul besar, dan satu pintu keluar.
        ///
        /// Tombol ULANG RUN dibuang atas perintah pemilik project — dan labelnya "(SPACE)"
        /// memang bohong sejak lahir: tidak pernah ada handler SPACE, cuma klik. Mati berarti
        /// balik ke menu; layar yang cuma punya satu jawaban tidak butuh pilihan kedua.
        /// </summary>
        void BuildGameOver()
        {
            _overVeil = MakeImage("OverVeil", Vector2.zero, Vector2.zero,
                new Color(0.34f, 0.02f, 0.02f, 0.94f), Vector2.zero);
            _overVeil.rectTransform.anchorMin = Vector2.zero;
            _overVeil.rectTransform.anchorMax = Vector2.one;
            _overVeil.rectTransform.offsetMin = Vector2.zero;
            _overVeil.rectTransform.offsetMax = Vector2.zero;

            _overTitle = MakeText("OverTitle", new Vector2(0f, 170f), new Vector2(1200f, 130f), 92,
                new Color(1f, 0.93f, 0.88f), new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter);
            _overTitle.text = "GAME OVER";

            _overInfo = MakeText("OverInfo", new Vector2(0f, 88f), new Vector2(900f, 34f), 22,
                new Color(1f, 0.76f, 0.7f), new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter);

            _overMenuBg = MakeImage("OverMenuBg", Vector2.zero, Vector2.zero,
                new Color(0.14f, 0.04f, 0.05f, 0.96f), Vector2.zero);
            _overMenuLabel = MakeText("OverMenuLabel", Vector2.zero, new Vector2(OverButtonW, 30f), 24,
                new Color(1f, 0.9f, 0.86f), Vector2.zero, TextAnchor.MiddleCenter);
            _overMenuLabel.text = "KE MENU UTAMA";

            ShowGameOver(false);
        }

        void ShowGameOver(bool on)
        {
            // Dibangun lebih awal daripada HUD lainnya, jadi urutan anaknya menaruhnya DI BAWAH
            // segalanya — termasuk peta layar-penuh, yang juga naik ke puncak saat dibuka.
            // Diangkat hanya kalau BELUM di puncak, bukan tiap frame: memindahkan sibling tiap
            // frame memaksa kanvas membangun ulang batch-nya.
            var last = _overMenuLabel.transform;
            bool onTop = last.GetSiblingIndex() == last.parent.childCount - 1;

            if (on && !onTop)
            {
                _overVeil.transform.SetAsLastSibling();
                _overTitle.transform.SetAsLastSibling();
                _overInfo.transform.SetAsLastSibling();
                _overMenuBg.transform.SetAsLastSibling();
                _overMenuLabel.transform.SetAsLastSibling();
            }

            _overVeil.enabled = on;
            _overTitle.enabled = on;
            _overInfo.enabled = on;
            _overMenuBg.enabled = on;
            _overMenuLabel.enabled = on;
        }

        /// <summary>Menempatkan kotak &amp; label tombolnya. Tiap frame — layar bisa diubah ukurannya.</summary>
        void DrawGameOver()
        {
            if (Player.Alive)
            {
                _overFade = 0f;
                ShowGameOver(false);
                return;
            }

            // Run sudah selesai: tidak ada panel yang masih pantas terbuka di belakang kerudung.
            // Peta pemilih yang tertinggal menyala adalah yang paling menyesatkan — ia menuntut
            // jawaban untuk perjalanan yang sudah tidak ada.
            _mapOpen = false;
            _shopOpen = false;
            _eventOpen = false;
            _gambleOpen = false;

            ShowGameOver(true);

            // Memerah dalam ~0,7 detik, bukan menjeglek. Unscaled: kematian boleh saja
            // terjadi saat timescale sedang diperlambat lewat Ruang Uji.
            _overFade = Mathf.MoveTowards(_overFade, 1f, Time.unscaledDeltaTime * 1.4f);
            SetAlpha(_overVeil, 0.94f * _overFade);
            SetAlpha(_overTitle, _overFade);
            SetAlpha(_overInfo, _overFade);
            SetAlpha(_overMenuLabel, _overFade);

            _overInfo.text = "run berakhir di wave " + Enemies.Wave + "   -   " + _gold + " koin";

            var menu = GameOverMenuRect();
            Seat(_overMenuBg.rectTransform, menu);
            Seat(_overMenuLabel.rectTransform, menu);

            // Menyorot tombolnya: satu-satunya umpan balik yang tersisa di layar ini.
            var mouse = ProtoInput.MousePosition;
            _overMenuBg.color = menu.Contains(mouse)
                ? new Color(0.32f, 0.09f, 0.08f, 0.98f * _overFade)
                : new Color(0.14f, 0.04f, 0.05f, 0.96f * _overFade);
        }

        static void SetAlpha(UnityEngine.UI.Graphic g, float a)
        {
            var c = g.color;
            c.a = a;
            g.color = c;
        }

        /// <summary>
        /// Pinggiran layar memerah TIPIS saat damage benar-benar menembus ke HP — dan cuma
        /// pinggirannya: tengah layar milik pertarungan, bukan milik umpan balik. Kontak
        /// musuh menguras per frame, jadi selama masih ditempel vignette-nya bertahan, dan
        /// baru memudar begitu pemain lepas — itu bukan bug, itu informasinya.
        /// </summary>
        void BuildHurtVeil()
        {
            _hurtVeil = MakeImage("HurtVeil", Vector2.zero, Vector2.zero, Color.white, Vector2.zero);
            _hurtVeil.sprite = VignetteSprite();
            _hurtVeil.rectTransform.anchorMin = Vector2.zero;
            _hurtVeil.rectTransform.anchorMax = Vector2.one;
            _hurtVeil.rectTransform.offsetMin = Vector2.zero;
            _hurtVeil.rectTransform.offsetMax = Vector2.zero;
            _hurtVeil.color = new Color(0.72f, 0.05f, 0.05f, 0f);
            _hurtVeil.enabled = false;
        }

        void DrawHurtVeil()
        {
            if (_hurtVeil == null) return;

            // Mati = layar kematian yang memerah penuh; vignette tipis di bawahnya cuma noise.
            if (!Player.Alive)
            {
                _hurtVeil.enabled = false;
                return;
            }

            _hurtGlow = Mathf.MoveTowards(_hurtGlow, 0f, Time.deltaTime * 2.4f);

            float a = 0.34f * _hurtGlow;  // puncaknya pun tetap tipis — permintaannya "tipis aja"
            _hurtVeil.enabled = a > 0.005f;
            if (!_hurtVeil.enabled) return;

            var c = _hurtVeil.color;
            c.a = a;
            _hurtVeil.color = c;
        }

        /// <summary>
        /// Gradasi radial — bening di tengah, pekat di tepi. Digenerate sekali, bukan aset:
        /// 128 piksel bilinear yang direntangkan sepenuh layar sudah mulus untuk gradasi.
        /// </summary>
        static Sprite VignetteSprite()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - half) / half;
                    float ny = (y - half) / half;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.InverseLerp(0.62f, 1.05f, d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply(false, true);

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static void Seat(RectTransform rect, Rect box)
        {
            rect.anchoredPosition = new Vector2(box.xMin, box.yMin);
            rect.sizeDelta = box.size;
        }

        void BuildShop()
        {
            var shopRect = ShopButtonRect();
            _shopBtnBg = MakeImage("ShopBtn", new Vector2(shopRect.xMin, shopRect.yMin),
                shopRect.size, new Color(0.2f, 0.3f, 0.45f, 0.92f), Vector2.zero);
            _shopBtnLabel = MakeText("ShopBtnLabel", new Vector2(shopRect.xMin, shopRect.yMin + 8f),
                new Vector2(shopRect.width, 20), 14, Color.white, Vector2.zero, TextAnchor.LowerCenter);
            _shopBtnLabel.text = "TOKO";

            // Baris "ALT + hover = lihat resep" dulu duduk di sini: teks selebar 220 px di atas
            // petak yang cuma 88 px, jadi ia menindih tombol PETA di sebelahnya. Bukan tombol,
            // tidak pernah bisa diklik — cuma pengumuman tombol keyboard yang menempel selamanya.
            // ALT + hover tetap bekerja; yang hilang cuma papan namanya.

            _panelBg = MakeImage("PanelBg", Vector2.zero, new Vector2(PanelW, PanelH),
                new Color(0.07f, 0.07f, 0.11f, 0.98f), new Vector2(0.5f, 0.5f));

            // Perkamen kalau temanya punya, kotak datar kalau tidak.
            //
            // Toko, kejadian, dan slot berbagi SATU badan panel — jadi satu sprite di sini
            // mendandani ketiganya sekaligus. Warnanya dijaga tetap dekat putih supaya yang
            // tampil gambarnya apa adanya: tint pada Image itu perkalian, dan warna panel lama
            // (0,07) akan mengalikan perkamennya sampai hampir hitam.
            var paper = _theme != null
                ? (_theme.PanelPaper != null ? _theme.PanelPaper : _theme.MapPaper)
                : null;

            if (paper != null)
            {
                _panelBg.sprite = paper;
                _panelBg.type = Image.Type.Sliced;
                _panelBg.color = new Color(0.82f, 0.79f, 0.72f, 1f);
            }

            _panelBg.enabled = false;

            // Tinta gelap di atas perkamen, tinta terang di atas kotak. Warna judul lama dipilih
            // untuk latar gelap, dan di atas kertas ia praktis tidak terbaca.
            _panelTitle = MakeText("PanelTitle", Vector2.zero, new Vector2(PanelW - 24, 26), 17,
                paper != null ? new Color(0.22f, 0.15f, 0.1f) : new Color(0.9f, 0.88f, 0.98f),
                new Vector2(0.5f, 0.5f), TextAnchor.UpperLeft);
            _panelTitle.enabled = false;

            _shopSlotBg = new Image[ShopSlots];
            _shopSlotText = new Text[ShopSlots];

            for (int i = 0; i < ShopSlots; i++)
            {
                // Cokelat tinta, bukan biru-gelap. Warna lama dipilih waktu badan panelnya
                // masih kotak biru-gelap; di atas perkamen ia terbaca sebagai potongan UI dari
                // game lain yang ditempel di atas kertas. Kontrasnya sama tingginya — yang
                // berubah cuma nadanya, jadi teks putih di atasnya tetap terbaca.
                _shopSlotBg[i] = MakeImage($"ShopSlot_{i}", Vector2.zero, new Vector2(ShopSlotW, ShopSlotH),
                    paper != null
                        ? new Color(0.16f, 0.115f, 0.085f, 0.94f)
                        : new Color(0.13f, 0.13f, 0.18f, 0.95f), Vector2.zero);
                _shopSlotBg[i].enabled = false;

                _shopSlotText[i] = MakeText($"ShopSlotText_{i}", Vector2.zero, new Vector2(ShopSlotW - 10, 40), 13,
                    Color.white, Vector2.zero, TextAnchor.LowerCenter);
                _shopSlotText[i].enabled = false;
            }

            _rerollBg = MakeImage("RerollBg", Vector2.zero, new Vector2(240, 34),
                paper != null
                    ? new Color(0.26f, 0.33f, 0.18f, 0.95f)
                    : new Color(0.32f, 0.45f, 0.28f, 0.95f), Vector2.zero);
            _rerollBg.enabled = false;

            _rerollLabel = MakeText("RerollLabel", Vector2.zero, new Vector2(240, 22), 15,
                Color.white, Vector2.zero, TextAnchor.LowerCenter);
            _rerollLabel.enabled = false;

            BuildHurtVeil();
            BuildGameOver();

            // Piece TERCECER naik ke paling depan, sesudah semua panel dibangun.
            //
            // Kolam ini lahir di baris 947, badan panel di 1110 — dan di UGUI yang lahir duluan
            // digambar duluan, artinya SETIAP piece tercecer tergambar DI BELAKANG panel toko.
            // Barang yang baru dibeli dilempar dekat slotnya, yaitu di dalam panel, jadi ia
            // menghilang di detik ia muncul. Laporan pemilik project: "gw minta itemnya di
            // paling depan, ini malah di belakang".
            //
            // Peta dan layar game over tidak terpengaruh: keduanya menaikkan dirinya sendiri
            // tiap frame, jadi mereka tetap menang di atas ini.
            for (int i = 0; i < _looseCells.Length; i++) _looseCells[i].transform.SetAsLastSibling();

            _evoLines = new Image[EvoLinePool];
            for (int i = 0; i < EvoLinePool; i++)
            {
                _evoLines[i] = MakeImage($"EvoLine_{i}", Vector2.zero, new Vector2(4, 4), Color.white, Vector2.zero);
                _evoLines[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _evoLines[i].enabled = false;
            }

            RollShop();
        }

        /// <summary>Marks a piece as seen. Silent unless it is genuinely new.</summary>
        void Discover(PieceDefinition piece)
        {
            if (piece == null || _codex == null) return;
            if (!_codex.Discover(piece.Id)) return;

            PushFloater(Player.transform.position + Vector3.up * 2.6f,
                "BARU: " + piece.DisplayName, new Color(0.8f, 0.95f, 1f));
        }

        void RollShop()
        {
            for (int i = 0; i < ShopSlots; i++) _shop[i] = _db.ShopRoll(_balance.ShopHighRollChance);
        }

        void BuildSpellPanel()
        {
            _spellBg = new Image[MaxSpellRows];
            _spellFill = new Image[MaxSpellRows];
            _spellText = new Text[MaxSpellRows];

            for (int i = 0; i < MaxSpellRows; i++)
            {
                // Row 0 is the TOP row. The list is sorted by damage, so the heaviest skill has to
                // sit where the eye lands first — the old bottom-up order buried it.
                float y = Margin + (MaxSpellRows - 1 - i) * 44;
                _spellBg[i] = MakeImage($"SpellBg_{i}", new Vector2(-Margin, y), new Vector2(SpellPanelW, 40),
                    new Color(0.1f, 0.1f, 0.14f, 0.85f), new Vector2(1f, 0f));

                _spellFill[i] = MakeImage($"SpellFill_{i}", new Vector2(-Margin, y), new Vector2(SpellPanelW, 40),
                    new Color(0.3f, 0.3f, 0.45f, 0.55f), new Vector2(1f, 0f));
                _spellFill[i].type = Image.Type.Filled;
                _spellFill[i].fillMethod = Image.FillMethod.Horizontal;
                _spellFill[i].fillOrigin = 0;

                _spellText[i] = MakeText($"SpellText_{i}", new Vector2(-Margin - 8, y + 4),
                    new Vector2(SpellPanelW - 10, 36), 13, Color.white, new Vector2(1f, 0f), TextAnchor.LowerRight);
            }

            MakeText("SpellTitle", new Vector2(-Margin, Margin + MaxSpellRows * 44 + 6),
                new Vector2(400, 22), 15, new Color(0.85f, 0.82f, 0.95f),
                new Vector2(1f, 0f), TextAnchor.LowerRight).text = "SPELL AKTIF";
        }

        void BuildSpeedControl()
        {
            _speedButtons = new Image[Speeds.Length];
            _speedLabels = new Text[Speeds.Length];

            for (int i = 0; i < Speeds.Length; i++)
            {
                float x = -(Margin + (Speeds.Length - 1 - i) * (SpeedButtonW + 6));
                var pos = new Vector2(x, -Margin);

                _speedButtons[i] = MakeImage($"Speed_{i}", pos, new Vector2(SpeedButtonW, SpeedButtonH),
                    new Color(0.14f, 0.14f, 0.18f, 0.9f), new Vector2(1f, 1f));

                _speedLabels[i] = MakeText($"SpeedLabel_{i}", pos + new Vector2(0, -7),
                    new Vector2(SpeedButtonW, SpeedButtonH), 16, Color.white,
                    new Vector2(1f, 1f), TextAnchor.UpperCenter);
                _speedLabels[i].text = SpeedLabels[i];
            }

            MakeText("SpeedHint", new Vector2(-Margin, -Margin - SpeedButtonH - 4), new Vector2(300, 20), 12,
                new Color(0.6f, 0.6f, 0.68f), new Vector2(1f, 1f), TextAnchor.UpperRight).text =
                "kecepatan  (tombol 1/2/3/4)";

            BuildTimeControl();
        }

        Image _timeButton;
        Text _timeLabel;

        /// <summary>
        /// Tombol siang/malam. Alat DEBUG, dan hanya muncul kalau arenanya memang punya lebih dari
        /// satu wajah — tombol yang tidak mengubah apa pun lebih buruk daripada tidak ada tombol.
        /// </summary>
        void BuildTimeControl()
        {
            if (_biome == null || _biome.Faces < 2) return;

            float y = -(Margin + SpeedButtonH + 22);
            float width = Speeds.Length * SpeedButtonW + (Speeds.Length - 1) * 6;
            var pos = new Vector2(-Margin, y);

            _timeButton = MakeImage("TimeToggle", pos, new Vector2(width, SpeedButtonH),
                new Color(0.16f, 0.18f, 0.26f, 0.9f), new Vector2(1f, 1f));

            _timeLabel = MakeText("TimeToggleLabel", pos + new Vector2(0, -7),
                new Vector2(width, SpeedButtonH), 15, new Color(0.92f, 0.9f, 0.7f),
                new Vector2(1f, 1f), TextAnchor.UpperCenter);

            RefreshTimeLabel();
        }

        void RefreshTimeLabel()
        {
            if (_timeLabel == null || _biome == null) return;

            var face = _biome.Current;
            _timeLabel.text = (face != null ? face.DisplayName : "?") + "   (T)";
        }

        void ToggleTime()
        {
            if (_biome == null || _biome.Faces < 2) return;

            _biome.Show(_biome.FaceIndex + 1);
            RefreshTimeLabel();
        }

        Image _bossBg;
        Image _bossFill;
        Text _bossLabel;

        /// <summary>
        /// Bar HP boss, di ATAS layar dan lebar penuh.
        ///
        /// Panjang badan ularnya memang sudah menceritakan sisa HP-nya, dan itu bacaan yang bagus —
        /// tapi ia satu-satunya, dan cuma terbaca kalau seluruh ularnya kebetulan sedang di layar.
        /// Di tengah gerombolan tiga ratus musuh, itu hampir tidak pernah terjadi.
        /// </summary>
        void BuildBossBar()
        {
            _bossBg = MakeImage("BossBg", new Vector2(-320f, -14f), new Vector2(640f, 22f),
                new Color(0.1f, 0.03f, 0.05f, 0.92f), new Vector2(0.5f, 1f));

            _bossFill = MakeImage("BossFill", new Vector2(-320f, -14f), new Vector2(640f, 22f),
                new Color(0.85f, 0.2f, 0.24f, 0.95f), new Vector2(0.5f, 1f));
            _bossFill.type = Image.Type.Filled;
            _bossFill.fillMethod = Image.FillMethod.Horizontal;
            _bossFill.fillOrigin = 0;

            _bossLabel = MakeText("BossLabel", new Vector2(-320f, -15f), new Vector2(640f, 22f), 14,
                Color.white, new Vector2(0.5f, 1f), TextAnchor.UpperCenter);

            SetBossBar(false);
        }

        void SetBossBar(bool on)
        {
            _bossBg.enabled = on;
            _bossFill.enabled = on;
            _bossLabel.enabled = on;
        }

        void DrawBossBar()
        {
            var boss = Enemies.Boss;

            if (boss == null || !boss.Alive)
            {
                if (_bossBg.enabled) SetBossBar(false);
                return;
            }

            if (!_bossBg.enabled) SetBossBar(true);

            _bossFill.fillAmount = boss.HpFraction;
            _bossLabel.text = boss.Def.DisplayName + "   " +
                              Mathf.CeilToInt(boss.HpFraction * 100f) + "%";
        }

        /// <summary>
        /// Memasang bar HP & mana dari prefab, kalau temanya membawa satu.
        ///
        /// Yang diambil cuma RUJUKANNYA — kode sesudah ini menulis <c>fillAmount</c> dan teks ke
        /// slot yang sama persis seperti dulu, jadi tidak ada satu pun pemakai di bawah yang
        /// perlu tahu bar itu datang dari mana. Letak, ukuran, sprite, dan arah isian tidak
        /// disentuh sama sekali: menyetelnya dari kode akan menimpa editan prefab diam-diam
        /// saat run, dan itu pelajaran yang sudah dibayar di papan grimoire.
        /// </summary>
        bool BuildVitalsFromPrefab()
        {
            if (_theme == null || _theme.VitalsPrefab == null) return false;

            var go = Instantiate(_theme.VitalsPrefab, _canvas.transform, false);
            go.name = "Vitals";

            var rig = go.GetComponent<VitalsRig>() ?? go.GetComponentInChildren<VitalsRig>(true);

            if (rig == null || !rig.Usable)
            {
                // Prefabnya ada tapi tidak memberi tahu bagian mana yang mengisi. Dibuang dan
                // kembali ke bar bawaan: bar kotak yang jelek masih bisa dibaca, sedangkan bar
                // yang tidak pernah bergerak tidak.
                Debug.LogWarning("[GrimoireUI] VitalsPrefab tidak punya VitalsRig dengan HpFill " +
                                 "terisi — kembali ke bar bawaan.");
                Destroy(go);
                return false;
            }

            _hpFill = rig.HpFill;
            _hpChip = rig.HpChip;
            _hpLabel = rig.HpLabel;
            _manaFill = rig.ManaFill;
            _manaLabel = rig.ManaLabel;

            // Kotak hover jatuh balik ke isiannya sendiri kalau prefabnya tidak menunjuk yang
            // lain. Itu selalu benar walau kadang sempit: isian ADA di dalam bola, jadi kursor
            // yang mengenainya pasti sedang menunjuk bola itu.
            _hpHover = rig.HpHover != null ? rig.HpHover : _hpFill.rectTransform;
            _manaHover = rig.ManaHover != null ? rig.ManaHover
                       : _manaFill != null ? _manaFill.rectTransform
                       : null;

            // Warna yang disetel di prefab jadi titik pulang kilat dan cerah. Dibaca di sini,
            // sekali, sebelum satu frame pun sempat menulis ke atasnya.
            _hpFillBase = _hpFill.color;
            if (_manaFill != null) _manaFillBase = _manaFill.color;

            _hpLiquid = MakeLiquid(_hpFill);
            _manaLiquid = MakeLiquid(_manaFill);

            return true;
        }

        /// <summary>
        /// Material cairan untuk satu bola, atau null kalau bola ini tidak cocok untuknya.
        ///
        /// Hanya untuk isian MENEGAK. Gelombangnya melintang di sepanjang lebar dan mengayun
        /// ketinggian permukaan — di bar mendatar tidak ada permukaan untuk digoyang, dan garis
        /// yang bergerak di ujung kanan bar akan terbaca sebagai kedipan, bukan sebagai cairan.
        /// Bar kotak bawaan lewat jalur ini tanpa berubah sedikit pun.
        ///
        /// Materialnya SALINAN per bola. Dua bola berbagi satu material berarti berbagi satu
        /// <c>_Fill</c> juga, dan yang menulis belakangan menang — bola mana akan menggambar
        /// ketinggian HP.
        /// </summary>
        Material MakeLiquid(Image fill)
        {
            if (fill == null || _theme == null || _theme.LiquidAmp <= 0f) return null;
            if (fill.type != Image.Type.Filled || fill.fillMethod != Image.FillMethod.Vertical) return null;

            var shader = Shader.Find("Grimoire/VitalsLiquid");
            if (shader == null) return null;

            var mat = new Material(shader);

            // Kotak UV sprite di dalam atlasnya. Tanpa ini, sprite yang dipak ke atlas menghitung
            // ketinggian permukaannya memakai koordinat atlas — dan garisnya mendarat di tempat
            // yang sama sekali tidak berhubungan dengan bolanya.
            var uv = fill.sprite != null
                ? UnityEngine.Sprites.DataUtility.GetOuterUV(fill.sprite)
                : new Vector4(0f, 0f, 1f, 1f);

            mat.SetVector("_UvRect", uv);
            mat.SetFloat("_Amp", _theme.LiquidAmp);
            mat.SetFloat("_Speed", _theme.LiquidSpeed);
            mat.SetFloat("_Waves", _theme.LiquidWaves);
            mat.SetFloat("_Crest", _theme.LiquidCrest);

            fill.material = mat;
            return mat;
        }

        void BuildHud()
        {
            _hudText = MakeText("Hud", new Vector2(Margin, -Margin), new Vector2(600, 26), 18,
                Color.white, new Vector2(0f, 1f), TextAnchor.UpperLeft);

            // Prefab lebih dulu. Kalau ia menyediakan bar, blok bawaan di bawah dilewati —
            // membangun keduanya berarti bar kotak datar lama menempel di belakang art baru.
            // Yang di luar blok ini (tooltip, dsb) tetap dibangun apa pun jalurnya.
            if (!BuildVitalsFromPrefab())
            {
                _hpBg = MakeImage("HpBg", new Vector2(Margin, -50), new Vector2(260, 18),
                    new Color(0.16f, 0.07f, 0.08f, 0.9f), new Vector2(0f, 1f));

                // Sits between background and fill: creation order is draw order on this canvas.
                _hpChip = MakeImage("HpChip", new Vector2(Margin, -50), new Vector2(260, 18),
                    HpChipColor, new Vector2(0f, 1f));
                _hpChip.type = Image.Type.Filled;
                _hpChip.fillMethod = Image.FillMethod.Horizontal;
                _hpChip.fillOrigin = 0;

                _hpFill = MakeImage("HpFill", new Vector2(Margin, -50), new Vector2(260, 18),
                    HpFillColor, new Vector2(0f, 1f));
                _hpFill.type = Image.Type.Filled;
                _hpFill.fillMethod = Image.FillMethod.Horizontal;
                _hpFill.fillOrigin = 0;
                _hpLabel = MakeText("HpLabel", new Vector2(Margin + 6, -51), new Vector2(250, 18), 13,
                    Color.white, new Vector2(0f, 1f), TextAnchor.UpperLeft);

                _manaBg = MakeImage("ManaBg", new Vector2(Margin, -72), new Vector2(260, 18),
                    new Color(0.08f, 0.09f, 0.16f, 0.9f), new Vector2(0f, 1f));
                _manaFill = MakeImage("ManaFill", new Vector2(Margin, -72), new Vector2(260, 18),
                    new Color(0.35f, 0.6f, 1f, 0.95f), new Vector2(0f, 1f));
                _manaFill.type = Image.Type.Filled;
                _manaFill.fillMethod = Image.FillMethod.Horizontal;
                _manaFill.fillOrigin = 0;
                _manaLabel = MakeText("ManaLabel", new Vector2(Margin + 6, -73), new Vector2(250, 18), 13,
                    Color.white, new Vector2(0f, 1f), TextAnchor.UpperLeft);

                _hpHover = _hpFill.rectTransform;
                _manaHover = _manaFill.rectTransform;
            }

            _tipBg = MakeImage("TipBg", Vector2.zero, new Vector2(TipWidth, 150),
                new Color(0.06f, 0.06f, 0.09f, 0.96f), Vector2.zero);
            _tipBg.rectTransform.pivot = new Vector2(0f, 1f);
            _tipBg.enabled = false;

            _tipText = MakeText("TipText", Vector2.zero, new Vector2(TipWidth - TipPadX * 2f, 140), 13,
                new Color(0.92f, 0.92f, 0.96f), Vector2.zero, TextAnchor.UpperLeft);
            _tipText.rectTransform.pivot = new Vector2(0f, 1f);

            // Dibungkus, bukan diluberkan. Kartu ini satu-satunya teks panjang di seluruh HUD:
            // dengan Overflow, kalimat blurb berjalan terus melewati tepi kotak gelapnya dan
            // sisanya dibaca di atas rumput.
            _tipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _tipText.supportRichText = true;

            _tipText.enabled = false;

            _bannerText = MakeText("Banner", new Vector2(0, 210), new Vector2(900, 100), 28,
                Color.white, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter);
            _bannerText.text = "";

            _startBg = MakeImage("StartBg", new Vector2(0, 120), new Vector2(StartButtonW, StartButtonH),
                new Color(0.35f, 0.75f, 0.4f, 0.95f), new Vector2(0.5f, 0.5f));

            _startLabel = MakeText("StartLabel", new Vector2(0, 120), new Vector2(StartButtonW, StartButtonH),
                20, new Color(0.06f, 0.12f, 0.07f), new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter);
            _startLabel.text = "MULAI WAVE   (SPACE)";
        }

        void BuildMeter()
        {
            // A single line now, tucked under the panel title — the whole meter block it replaced
            // was a second copy of information the rows already carry.
            _meterText = MakeText("Meter", new Vector2(-Margin, Margin + MaxSpellRows * 44 + 26),
                new Vector2(SpellPanelW + 120, 20), 12, new Color(0.72f, 0.76f, 0.85f),
                new Vector2(1f, 0f), TextAnchor.LowerRight);

            BuildStripAnchors();

            _buffStrip = new StatusStrip(_canvas.transform, _font, 6, StripIcon,
                new Color(1f, 0.92f, 0.55f));

            _debuffStrip = new StatusStrip(_canvas.transform, _font, 4, StripIcon,
                new Color(1f, 0.5f, 0.5f));

            _ailmentStrip = new StatusStrip(_canvas.transform, _font, 8, StripIcon,
                new Color(0.85f, 0.88f, 0.95f));

            // Kapasitas 12: katalognya 22, tapi node kejadian datang beberapa kali per act dan
            // pakta tidak pernah bisa diambil dua kali. Dua belas adalah run yang sangat panjang.
            _pactStrip = new StatusStrip(_canvas.transform, _font, 12, StripIcon,
                new Color(1f, 0.82f, 0.4f), vertical: true);
        }

        /// <summary>
        /// Menentukan pojok kiri-atas ketiga strip ikon, dari prefab penempat kalau temanya
        /// membawa satu.
        ///
        /// Prefabnya cuma berisi kotak kosong — ikonnya tetap dibangun dan digambar kode, dan
        /// tetap menempel di kanvas (bukan di dalam prefab) supaya urutan gambarnya tidak
        /// bergantung pada susunan anak yang ditata orang lain.
        ///
        /// Yang dipindahkan cuma LETAK. Itu perlu sejak bar HP jadi bola: tempat lama ketiga
        /// strip dipilih waktu barnya masih kotak setinggi 18 piksel, dan bola berbingkai
        /// setinggi hampir 190 piksel menelan ketiganya.
        /// </summary>
        void BuildStripAnchors()
        {
            _buffOrigin = new Vector2(Margin, StripBuffY);
            _debuffOrigin = new Vector2(Margin, StripDebuffY);
            _ailmentOrigin = new Vector2(Margin, StripAilmentY);

            // Tepi KANAN, dan tegak. Tiga strip lain berbaris mendatar di kiri bawah bar mana;
            // menaruh yang keempat di ujung barisan itu akan membuatnya terbaca sebagai jenis
            // keempat dari hal yang sama. Pakta bukan hal yang sama — ia dipilih, bukan menimpa,
            // dan tidak akan pernah hilang. Sisi layar yang berbeda mengatakan itu tanpa satu kata.
            _pactOrigin = new Vector2(Screen.width - StripIcon - 18f, StripPactY);

            if (_theme == null || _theme.StatusStripsPrefab == null) return;

            var go = Instantiate(_theme.StatusStripsPrefab, _canvas.transform, false);
            go.name = "StatusStripAnchors";

            var rig = go.GetComponent<StatusStripRig>() ?? go.GetComponentInChildren<StatusStripRig>(true);

            if (rig == null)
            {
                Debug.LogWarning("[GrimoireUI] StatusStripsPrefab tidak punya StatusStripRig — " +
                                 "ketiga strip kembali ke tempat lamanya.");
                Destroy(go);
                return;
            }

            // Sudut dunia baru benar setelah kanvas menghitung tata letaknya. Prefab yang baru
            // ditempel di frame yang sama masih memakai rect bawaannya, dan pangkal yang dibaca
            // dari situ meleset — kadang sampai setengah layar.
            Canvas.ForceUpdateCanvases();

            Vector2 origin;
            if (StatusStripRig.TryOrigin(rig.BuffArea, out origin)) _buffOrigin = origin;
            if (StatusStripRig.TryOrigin(rig.DebuffArea, out origin)) _debuffOrigin = origin;
            if (StatusStripRig.TryOrigin(rig.PactArea, out origin)) _pactOrigin = origin;

            if (StatusStripRig.TryOrigin(rig.AilmentArea, out origin))
            {
                _ailmentOrigin = origin;

                // Dua baris keterangan menumpang di bawah strip ailment. Membiarkannya di angka
                // tetap berarti mereka tertinggal menggantung di tempat strip yang sudah pindah,
                // dan yang memindahkan stripnya tidak akan menduga keduanya ikut terlibat.
                if (_heldText != null)
                    _heldText.rectTransform.anchoredPosition = origin + new Vector2(0f, -34f);

                if (_evolveText != null)
                    _evolveText.rectTransform.anchoredPosition = origin + new Vector2(0f, -56f);
            }
        }

        /// <summary>
        /// One line above the spell panel: the run total, plus whatever damage did NOT come from a
        /// placed skill — reactions and ailments, which have no row of their own.
        /// </summary>
        void DrawMeter()
        {
            // Throttled: the numbers move constantly but nobody reads them four times a frame.
            _meterTimer -= Time.unscaledDeltaTime;
            if (_meterTimer > 0f) return;

            _meterTimer = 0.25f;

            if (_meter.Total <= 0f)
            {
                _meterText.text = "";
                return;
            }

            _sb.Length = 0;
            _sb.Append("total ").Append(Mathf.RoundToInt(_meter.Total));

            string others = _meter.BuildOtherSources(IsPlacedSkill, 4);
            if (others.Length > 0) _sb.Append("      ").Append(others);

            _meterText.text = _sb.ToString();
        }

        /// <summary>True when this damage source already has its own row in the spell panel.</summary>
        bool IsPlacedSkill(string source)
        {
            var spells = Book.Spells;
            for (int i = 0; i < spells.Count; i++)
            {
                if (spells[i].Source.Def.DisplayName == source) return true;
            }

            return false;
        }

        /// <summary>
        /// Three icon strips stacked under the mana bar: what is helping you, what is hurting you,
        /// and what is currently burning through the swarm.
        /// </summary>
        void DrawBuffs()
        {
            PushSlots(_buffStrip, Player.Buffs, _buffOrigin);
            PushSlots(_debuffStrip, Player.Debuffs, _debuffOrigin);

            _ailmentStrip.Begin(_ailmentOrigin);

            var counts = Enemies.StatusCounts;
            for (int i = 0; i < _db.Statuses.Count && i < counts.Length; i++)
            {
                var status = _db.Statuses[i];

                // Only what is actually on the field. A permanent row of zeroes is noise.
                if (status == null || counts[i] <= 0) continue;

                _ailmentStrip.Push(status.Icon, status.Color, counts[i].ToString(),
                    status.DisplayName + "  -  " + counts[i] + " musuh terkena\n" + status.Blurb);
            }

            _ailmentStrip.Apply();

            DrawPacts();
        }

        /// <summary>
        /// Kolom pakta di tepi kanan. Digambar ulang HANYA saat daftarnya berubah.
        ///
        /// Tiga strip di kiri memang harus disusun tiap frame — angkanya hitung mundur. Pakta tidak
        /// punya angka dan tidak punya batas waktu; isinya berubah beberapa kali sepanjang SATU RUN.
        /// Merakit ulang string tooltip-nya enam puluh kali per detik untuk teks yang identik adalah
        /// sampah yang lahir per frame, dan sampah kecil per frame persis bentuk yang menghasilkan
        /// patah GC di wave yang ramai.
        /// </summary>
        void DrawPacts()
        {
            var pacts = Player.Pacts;

            if (pacts == null)
            {
                if (_pactVersionDrawn == 0) return;
                _pactVersionDrawn = 0;
                _pactStrip.Hide();
                return;
            }

            if (pacts.Version == _pactVersionDrawn) return;
            _pactVersionDrawn = pacts.Version;

            _pactStrip.Begin(_pactOrigin);

            var taken = pacts.Taken;
            for (int i = 0; i < taken.Count; i++)
            {
                var p = taken[i];
                if (p == null) continue;

                _sb.Length = 0;
                _sb.Append(p.DisplayName).Append("   (PAKTA - permanen)\n");
                if (!string.IsNullOrEmpty(p.BoonText)) _sb.Append("+ ").Append(p.BoonText).Append('\n');
                if (!string.IsNullOrEmpty(p.BaneText)) _sb.Append("- ").Append(p.BaneText);

                // Tanpa angka: pakta tidak menghitung mundur, dan kotak angka kosong di sebelah
                // tiap ikon cuma melebarkan kolomnya ke dalam layar.
                _pactStrip.Push(p.Icon, p.Color, "", _sb.ToString());
            }

            _pactStrip.Apply();
        }

        void PushSlots(StatusStrip strip, PlayerCaster.BuffSlot[] slots, Vector2 origin)
        {
            strip.Begin(origin);

            for (int i = 0; i < slots.Length; i++)
            {
                var def = slots[i].Def;
                if (def == null) continue;

                // A charge's number is how many you are holding — that is the thing you are
                // watching. A plain buff's number is how long you have left.
                int stacks = Mathf.Max(1, slots[i].Stacks);
                string label = def.IsCharge ? stacks + "x" : slots[i].Remaining.ToString("0.0");

                strip.Push(def.Icon, def.Color, label,
                    def.DisplayName + (def.IsCharge ? "  -  " + stacks + "/" + def.MaxStacks + " charge" : "")
                    + "  -  " + slots[i].Remaining.ToString("0.0") + "s\n" +
                    _tooltips.DescribeMods(def));
            }

            strip.Apply();
        }

        /// <summary>
        /// Kartu keterangan bola HP / mana, atau null kalau kursor tidak di atas keduanya.
        ///
        /// Ada karena bola tidak membawa angka. Bar kotak lama menempelkan tulisan
        /// "87 / 120" di badannya; bola berbingkai tidak punya tempat untuk itu tanpa
        /// mengotori artnya — jadi angkanya pindah ke hover, sama seperti tiap ikon strip
        /// di bawahnya. Satu kebiasaan, bukan dua.
        /// </summary>
        string VitalsTooltip(Vector2 mouse)
        {
            if (HoverHits(_hpHover, mouse))
                return VitalsCard("NYAWA", Player.Hp, Player.MaxHp, Player.HpRegen);

            if (HoverHits(_manaHover, mouse))
                return VitalsCard("MANA", Player.Mana, Player.MaxMana, Player.ManaRegen);

            return null;
        }

        /// <summary>
        /// Kanvas ini Overlay, jadi kameranya null — memberikan kamera arena di sini membuat
        /// setiap pengujian meleset sejauh perbedaan proyeksinya.
        /// </summary>
        static bool HoverHits(RectTransform area, Vector2 mouse) =>
            area != null && area.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(area, mouse, null);

        string VitalsCard(string title, float now, float max, float regen)
        {
            _sb.Length = 0;
            _sb.Append(title).Append('\n');

            // Dibulatkan ke atas: satu HP yang tersisa ditampilkan "0" akan terbaca sebagai sudah
            // mati, dan pemain yang membaca itu berhenti mencari cara bertahan.
            _sb.Append(Mathf.CeilToInt(now)).Append(" / ").Append(Mathf.CeilToInt(max));

            if (max > 0f)
                _sb.Append("      ").Append(Mathf.RoundToInt(now / max * 100f)).Append('%');

            // Baris regen hanya kalau ada. Menulis "pulih 0/dtk" itu janji palsu — pemain yang
            // membacanya akan menunggu isian yang tidak akan pernah datang.
            if (regen > 0f)
                _sb.Append("\npulih ").Append(regen.ToString("0.#")).Append(" per detik");

            return _sb.ToString();
        }

        /// <summary>Description of whichever strip icon the cursor is over, or null.</summary>
        string StripTooltip(Vector2 mouse) =>
            _buffStrip.TooltipAt(mouse) ?? _debuffStrip.TooltipAt(mouse) ??
            _ailmentStrip.TooltipAt(mouse) ?? _pactStrip.TooltipAt(mouse);

        void BuildFloaters()
        {
            _floaters = new Text[FloatPoolSize];
            _floatLife = new float[FloatPoolSize];
            _floatWorld = new Vector3[FloatPoolSize];

            for (int i = 0; i < FloatPoolSize; i++)
            {
                _floaters[i] = MakeText($"Float_{i}", Vector2.zero, new Vector2(300, 28), 20,
                    Color.white, Vector2.zero, TextAnchor.MiddleCenter);
                _floaters[i].text = "";
            }
        }

        int ValueOf(PieceDefinition def) => _balance.SellValueOf(def);

        // ---------- drop routing ----------

        /// <summary>Runes go straight into the grimoire â€” they have no storage.</summary>
        bool AutoPlaceInGrimoire(PieceDefinition def)
        {
            for (int rot = 0; rot < 4; rot++)
            {
                for (int y = 0; y < Grimoire.Height; y++)
                {
                    for (int x = 0; x < Grimoire.Width; x++)
                    {
                        if (Book.Place(def, new Vector2Int(x, y), rot) != null) return true;
                    }
                }
            }

            return false;
        }

        bool AutoStoreInBag(PieceDefinition def)
        {
            for (int rot = 0; rot < 4; rot++)
            {
                for (int y = 0; y < Backpack.Height; y++)
                {
                    for (int x = 0; x < Backpack.Width; x++)
                    {
                        if (_bag.Place(def, new Vector2Int(x, y), rot) != null) return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Kill drops wait here until the wave is over.
        ///
        /// The grimoire is locked while a wave runs, so a piece that lands mid-wave is something you
        /// can only watch — and by the time the wave ends the floor is carpeted with items that
        /// arrived while you had no say. Handing them all over at once, when the board unlocks, is
        /// the moment the player can actually act on them.
        /// </summary>
        readonly List<PieceDefinition> _pendingDrops = new List<PieceDefinition>();

        DropPickups _pickups;

        /// <summary>Value of drops rolled past the per-wave cap. Paid out as coins instead.</summary>
        int _pendingGold;

        void ReleaseDrops()
        {
            // Elite membayar lebih karena ia menuntut lebih. Sebelum ini hadiahnya sama persis
            // dengan pertarungan biasa, dan node yang lebih keras tanpa hadiah lebih besar bukan
            // pilihan melainkan jebakan — tidak ada alasan menginjaknya kecuali kepepet jalur.
            bool elite = _run != null && _run.CurrentKind == RunNodeKind.Elite;

            int rolls = _balance.WaveClearDrops + (elite ? _balance.EliteBonusDrops : 0);
            int starBonus = elite ? _balance.EliteStarBonus : 0;

            for (int i = 0; i < rolls; i++)
            {
                var bonus = _db.RandomDrop(_balance.RuneShareOfDrops, _balance.DropStarWeights,
                    _balance.DropStarMinWave, Enemies.Wave, starBonus);
                if (bonus != null) _pendingDrops.Add(bonus);
            }

            if (elite && _balance.EliteCoinBonus > 0) _pendingGold += _balance.EliteCoinBonus;

            int count = _pendingDrops.Count;

            // Dilempar ke lapangan, bukan dimasukkan langsung. Piece yang tiba-tiba ada di kantong
            // sudah benar secara mekanik dan sama sekali tidak terbaca sebagai hadiah.
            for (int i = 0; i < count; i++) _pickups.Toss(_pendingDrops[i]);
            _pendingDrops.Clear();

            _gold += _pendingGold;

            // One line for the whole haul. The pieces are already scattered on screen to be seen —
            // a floater per drop would just bury them under their own labels.
            if (count > 0 || _pendingGold > 0)
            {
                _sb.Length = 0;
                if (count > 0) _sb.Append(count).Append(" drop");
                if (_pendingGold > 0)
                {
                    if (_sb.Length > 0) _sb.Append("   ");
                    _sb.Append("kelebihan kejual +").Append(_pendingGold).Append(" koin");
                }

                PushFloater(Player.transform.position + Vector3.up * 2.4f,
                    _sb.ToString(), new Color(0.8f, 0.95f, 1f));
            }

            _pendingGold = 0;
        }

        void AddLoose(PieceDefinition def, Vector2? at = null)
        {
            if (_loose.Count >= LoosePoolSize)
            {
                // Screen is carpeted â€” the overflow is sold so nothing silently vanishes.
                _gold += ValueOf(def);
                PushFloater(Player.transform.position + Vector3.up * 2f,
                    "penuh, " + def.DisplayName + " kejual +" + ValueOf(def), new Color(1f, 0.88f, 0.45f));
                return;
            }

            _loose.Add(def);
            _loosePos.Add(at ?? RandomScatterPos());
            Discover(def);
        }

        /// <summary>Kicked-out pieces land right next to where they were, not across the screen.</summary>
        void ScatterAll(List<PieceDefinition> defs, Vector2 near)
        {
            for (int i = 0; i < defs.Count; i++) AddLoose(defs[i], NearScatterPos(near, i));
        }

        void RemoveLoose(int index)
        {
            _loose.RemoveAt(index);
            _loosePos.RemoveAt(index);
        }

        int ScreenToLoose(Vector2 mouse)
        {
            for (int i = _loose.Count - 1; i >= 0; i--)
            {
                var size = PieceSize(Shapes.Rotate(_loose[i].Cells, 0)) * 0.5f;
                var p = _loosePos[i];

                if (mouse.x >= p.x - size.x && mouse.x <= p.x + size.x &&
                    mouse.y >= p.y - size.y && mouse.y <= p.y + size.y)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Apa pun yang masih tergeletak di lantai saat pemain menekan LANJUT / MULAI WAVE tidak
        /// punya rumah — ia dijual. Dipanggil dari kedua tombol keberangkatan, bukan cuma dari
        /// yang memulai wave.
        /// </summary>
        void SellLoose()
        {
            if (_loose.Count == 0) return;

            int value = 0;
            for (int i = 0; i < _loose.Count; i++) value += ValueOf(_loose[i]);

            int sold = _loose.Count;
            _loose.Clear();
            _loosePos.Clear();
            _gold += value;

            PushFloater(Player.transform.position + Vector3.up * 2f,
                sold + " tercecer kejual  +" + value + " koin", new Color(1f, 0.88f, 0.45f));
        }

        // ---------- setelan dalam permainan ----------

        /// <summary>Halaman setelan yang sedang terbuka, atau null. Prefabnya milik menu.</summary>
        GameObject _settingsOverlay;

        float _timeScaleBeforeSettings = 1f;

        /// <summary>
        /// Membuka halaman setelan DI DALAM run. Prefabnya persis yang dipakai menu — satu
        /// panel untuk dua scene, jadi baris baru yang ditambahkan di menu otomatis muncul
        /// di sini tanpa ada yang perlu mengingatnya.
        /// </summary>
        void OpenSettings()
        {
            _settingsOverlay = Instantiate(_theme.SettingsPrefab, _canvas.transform, false);
            _settingsOverlay.name = "Settings";
            _settingsOverlay.SetActive(true);

            var panel = _settingsOverlay.GetComponentInChildren<SettingsPanel>(true);
            if (panel != null) panel.Init(GameSettings.Load());

            // Tombol KEMBALI milik halaman ini di-wire controller MENU; di sini controllernya
            // kita. Dikenali dari nama barisnya ("MenuLine_Back", nama yang diberikan builder) —
            // tombol stepper dan slider sudah di-wire SettingsPanel.Init sendiri.
            foreach (var button in _settingsOverlay.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                // Komponen Button-nya duduk di baris "MenuLine_Back" itu sendiri (lihat
                // NewMenuLine di builder), bukan di anak bernama Hit.
                if (!button.name.EndsWith("Back")) continue;

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(CloseSettings);
            }

            // Pulang ke menu — dulu kerja ESC, sekarang tombol yang disengaja. HANYA ada di
            // overlay dalam-game; di menu, halaman yang sama tidak butuh tombol pulang ke
            // dirinya sendiri.
            BuildExitButton(panel != null ? (RectTransform)panel.transform : null);

            // Dunia berhenti selama setelannya terbuka. unscaled dipakai seluruh UI, jadi
            // panelnya sendiri tetap hidup.
            _timeScaleBeforeSettings = Time.timeScale;
            Time.timeScale = 0f;
        }

        /// <param name="panel">
        /// Badan panel setelan. Tombolnya menempel DI SITU, bukan di layar: versi lama menghitung
        /// tepi panel sendiri dari <c>Screen.width</c> dengan mengasumsikan lebar panel 1180 dan
        /// skala kanvas 1 — dua asumsi yang meleset begitu jendelanya bukan 1920, dan tombolnya
        /// melayang entah di mana. Menumpang di panel membuat posisinya benar tanpa dihitung.
        /// </param>
        void BuildExitButton(RectTransform panel)
        {
            var host = panel != null ? panel : (RectTransform)_settingsOverlay.transform;

            var go = new GameObject("KeluarKeMenu");
            go.transform.SetParent(host, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.45f, 0.16f, 0.14f, 0.95f);

            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);

            // Sudut kanan-bawah panel, sebaris dengan KEMBALI di kiri-bawahnya — dua pintu keluar
            // yang tidak mungkin tertukar posisinya.
            rt.anchoredPosition = new Vector2(-48f, 26f);
            rt.sizeDelta = new Vector2(300f, 46f);

            var label = MakeText("KeluarLabel", Vector2.zero, new Vector2(300f, 46f), 18,
                new Color(1f, 0.85f, 0.8f), new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter);
            label.transform.SetParent(go.transform, false);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.text = "KELUAR KE MENU";

            var button = go.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => LoadScene(MainMenuSceneName));
        }

        void CloseSettings()
        {
            if (_settingsOverlay == null) return;

            // SettingsPanel menyimpan di OnDisable — Destroy saja sudah memicunya lewat
            // penonaktifan, tapi eksplisit lebih jujur daripada mengandalkan urutan teardown.
            var panel = _settingsOverlay.GetComponentInChildren<SettingsPanel>(true);
            if (panel != null) panel.gameObject.SetActive(false);

            Destroy(_settingsOverlay);
            _settingsOverlay = null;

            Time.timeScale = _timeScaleBeforeSettings;

            // Toggle teks damage berlaku SEKARANG, bukan run berikutnya — satu-satunya dari
            // tiga toggle yang bisa dipasang-copot semurah ini.
            var fresh = GameSettings.Load();
            Enemies.OnEnemyDamaged -= _popups.Push;
            if (fresh.DamageText) Enemies.OnEnemyDamaged += _popups.Push;
        }

        // ---------- runtime ----------

        void Update()
        {
            if (_inputLock > 0f) _inputLock -= Time.unscaledDeltaTime;

            if (ProtoInput.BackDown && _inputLock <= 0f)
            {
                // ESC membuka SETELAN, bukan langsung pulang ke menu. Perilaku lama membuang
                // seluruh run karena satu tombol refleks — pulang sekarang lewat tombol di dalam
                // panelnya, satu langkah yang disengaja. Tanpa prefab setelan (tema kosong),
                // perilaku lama dipertahankan: ESC yang tidak melakukan apa-apa lebih buruk.
                if (_settingsOverlay != null) CloseSettings();
                else if (_theme != null && _theme.SettingsPrefab != null) OpenSettings();
                else LoadScene(MainMenuSceneName);

                return;
            }

            // Selama setelan terbuka, dunia berhenti DAN input permainan ikut berhenti — klik
            // di panel tidak boleh tembus jadi klik papan.
            if (_settingsOverlay != null) return;

            bool consumed = HandleSpeed();
            if (!consumed) HandleInput();

            if (_evolveTimer > 0f)
            {
                _evolveTimer -= Time.unscaledDeltaTime;
                if (_evolveTimer <= 0f) _evolveText.text = "";
            }

            // Rune menyala selama ada yang duduk di papan. Dibaca dari Placed, bukan dari
            // Spells: yang terakhir cuma menghitung skill yang benar-benar bisa menembak, dan
            // rune dasar yang baru ditaruh — yang jelas-jelas terlihat di papan — tidak akan
            // menyalakan apa pun.
            if (_rune != null) _rune.SetLit(Book.Placed.Count > 0);

            Redraw();
            TickFloaters(Time.unscaledDeltaTime);
            _popups.Tick(Time.unscaledDeltaTime);
            if (_enemyBars != null) _enemyBars.Tick();
            HandleBanner();
        }

        /// <summary>Returns true when this frame's click landed on a speed button.</summary>
        bool HandleSpeed()
        {
            int key = ProtoInput.SpeedSlotDown;
            if (key >= 0)
            {
                SetSpeed(key);
                return false;
            }

            if (ProtoInput.CycleFaceDown)
            {
                ToggleTime();
                return true;
            }

            if (!ProtoInput.LeftClickDown) return false;

            Vector2 mouse = ProtoInput.MousePosition;
            for (int i = 0; i < Speeds.Length; i++)
            {
                if (SpeedRect(i, Speeds.Length).Contains(mouse))
                {
                    SetSpeed(i);
                    return true;
                }
            }

            if (_timeButton != null && TimeButtonRect(Speeds.Length).Contains(mouse))
            {
                ToggleTime();
                return true;
            }

            return false;
        }

        void SetSpeed(int slot)
        {
            _speedSlot = Mathf.Clamp(slot, 0, Speeds.Length - 1);
            Time.timeScale = Speeds[_speedSlot];

            for (int i = 0; i < Speeds.Length; i++)
            {
                bool on = i == _speedSlot;
                _speedButtons[i].color = on
                    ? new Color(0.9f, 0.75f, 0.3f, 0.95f)
                    : new Color(0.14f, 0.14f, 0.18f, 0.9f);
                _speedLabels[i].color = on ? new Color(0.1f, 0.1f, 0.12f) : Color.white;
            }
        }

        // Dengan sutradara run terpasang, tombol MULAI WAVE pensiun — LANJUT yang memberangkatkan.
        bool CanStartWave() =>
            _run == null && Player.Alive && !Enemies.WaveActive && Book.Spells.Count > 0;

        /// <summary>Tombol LANJUT: membuka peta pemilih lewat transisi gelap.</summary>
        bool CanDepart() =>
            _run != null && _run.ReadyToDepart && Player.Alive && !Enemies.WaveActive &&
            Book.Spells.Count > 0;

        void StartNextWave()
        {
            StashHeld();
            SellLoose();
            Player.ResetCooldowns();
            Enemies.StartWave(Enemies.Wave + 1);
        }

        /// <summary>
        /// LANJUT dengan sutradara peta terpasang.
        ///
        /// Beres-beresnya ada DI SINI, bukan di <see cref="StartNextWave"/>. Dulu barang tercecer
        /// hanya dijual saat wave berikutnya dimulai — dan begitu peta masuk, "lanjut" sering
        /// tidak berarti wave sama sekali: node toko, kejadian, atau slot berangkat lewat
        /// <c>Depart</c> yang tidak pernah menyentuh lantai. Akibatnya barang yang ditinggal
        /// menyeberang ke ruangan berikutnya dan menggantung di sana tanpa pemilik.
        ///
        /// Aturannya sekarang satu kalimat: begitu LANJUT ditekan, apa pun yang tidak berdiri di
        /// papan atau di tas sudah dijual.
        /// </summary>
        void DepartRun()
        {
            StashHeld();
            SellLoose();
            _run.Depart();
        }

        void HandleBanner()
        {
            bool showStart = CanStartWave() || CanDepart();
            _startBg.enabled = showStart;
            _startLabel.enabled = showStart;
            _startLabel.text = _run != null ? "LANJUT   (SPACE)" : "MULAI WAVE   (SPACE)";

            // Panel singgah duduk di tengah layar — persis tempat banner ini. Keduanya menyala
            // bersamaan berarti judul hijau mendarat di atas dagangan toko, dan itulah yang
            // membuat toko terlihat berantakan padahal kartunya tertata rapi. Selama panelnya
            // terbuka, panel itu yang bicara.
            //
            // Disembunyikan lewat `enabled`, BUKAN dengan keluar lebih awal: sisa fungsi ini juga
            // yang mendengar SPACE untuk LANJUT, dan toko yang terbuka tidak boleh mematikannya.
            _bannerText.enabled = !(_shopOpen || _eventOpen || _gambleOpen);

            if (!Player.Alive)
            {
                // Layar GAME OVER yang bicara sekarang; banner di tengah arena cuma akan
                // bertumpuk dengan judulnya sendiri.
                _gridTitle.text = "GRIMOIRE";
                _bannerText.text = "";

                if (ProtoInput.RestartDown) LoadScene(GameSceneName);

                return;
            }

            if (!Enemies.WaveActive)
            {
                _gridTitle.text = "GRIMOIRE   (bisa diubah)";
                _bannerText.color = new Color(0.55f, 0.95f, 0.6f);

                if (_run != null)
                {
                    // Transisi gelap / peta pemilih sedang aktif — biarkan layarnya yang bicara.
                    if (!_run.ReadyToDepart)
                    {
                        _bannerText.text = "";
                        return;
                    }

                    // Baris keduanya dulu berisi instruksi tombol — "klik LANJUT", "M = intip
                    // peta". Tombolnya sendiri sudah kelihatan di pojok layar, jadi yang tersisa
                    // cuma kalimat yang menutupi lapangan tiap kali wave berakhir.
                    if (_run.Resting)
                        _bannerText.text = "PULAU REHAT — " + RunDirector.KindLabel(_run.RestKind);
                    else if (Enemies.Wave == 0)
                        _bannerText.text = "SUSUN GRIMOIRE-MU";
                    else
                        _bannerText.text = "WAVE " + Enemies.Wave + " BERES";

                    if (Book.Spells.Count == 0)
                    {
                        _bannerText.color = new Color(1f, 0.75f, 0.4f);
                        _bannerText.text += "\n\npasang minimal 1 SKILL di atas rune dulu";
                    }
                    else if (ProtoInput.RestartDown && _inputLock <= 0f && CanDepart())
                    {
                        DepartRun();
                    }

                    return;
                }

                if (Enemies.Wave == 0)
                    _bannerText.text = "SUSUN GRIMOIRE-MU";
                else if (ShopEventActive)
                    _bannerText.text = "WAVE " + Enemies.Wave + " BERES   -   TOKO BUKA";
                else
                    _bannerText.text = "WAVE " + Enemies.Wave + " BERES";

                if (!showStart)
                {
                    _bannerText.color = new Color(1f, 0.75f, 0.4f);
                    _bannerText.text += "\n\npasang minimal 1 SKILL di atas rune dulu";
                }
                else if (ProtoInput.RestartDown && _inputLock <= 0f)
                {
                    StartNextWave();
                }

                return;
            }

            _gridTitle.text = "GRIMOIRE   (TERKUNCI - wave lagi jalan)";
            _bannerText.text = "";
        }

        void HandleInput()
        {
            // Peta boleh diintip kapan pun, termasuk saat wave berjalan — ia murni baca.
            // Kecuali sedang mode MEMILIH: peta itu wajib dijawab, M tidak boleh menutupnya.
            if (_run != null && ProtoInput.MapDown && !_mapChoose) _mapOpen = !_mapOpen;

            // Mati: cuma dua tombol layar GAME OVER yang masih hidup. Diperiksa SEBELUM kunci
            // input umum — layar ini justru muncul saat segalanya yang lain berhenti menerima.
            if (!Player.Alive)
            {
                if (!ProtoInput.LeftClickDown) return;

                var dead = ProtoInput.MousePosition;
                if (GameOverMenuRect().Contains(dead)) LoadScene(MainMenuSceneName);

                return;
            }

            if (_inputLock > 0f) return;

            // Grimoire is locked while a wave runs â€” you only watch.
            if (Enemies.WaveActive)
            {
                if (_held != null) StashHeld();
                return;
            }

            Vector2 mouse = ProtoInput.MousePosition;

            // R rotates, right-click locks. They used to share right-click, which meant the same
            // button did two unrelated things depending on whether your hand was full.
            if (ProtoInput.RotateDown)
            {
                if (_held != null)
                {
                    DropRiders();
                    _heldRot++;
                }

                return;
            }

            if (ProtoInput.RightClickDown)
            {
                // A locked piece is never eaten by an evolution, and no connector is ever drawn to
                // it — that is how you protect a piece you actually want to keep.
                var lockCell = ScreenToCell(mouse);
                if (lockCell.x >= 0)
                {
                    var target = Book.SkillAt(lockCell) ?? Book.BaseAt(lockCell);
                    if (target != null) target.Locked = !target.Locked;
                    return;
                }

                var lockBag = ScreenToBagCell(mouse);
                if (lockBag.x >= 0)
                {
                    var stored = _bag.At(lockBag);
                    if (stored != null) stored.Locked = !stored.Locked;
                }

                return;
            }

            if (!ProtoInput.LeftClickDown) return;

            if (HandlePanelClick(mouse)) return;

            if ((CanStartWave() || CanDepart()) && StartButtonRect().Contains(mouse))
            {
                if (_run != null) DepartRun();
                else StartNextWave();

                return;
            }

            if (_held != null)
            {
                var bagTarget = ScreenToBagCell(mouse);
                if (bagTarget.x >= 0)
                {
                    var bagOrigin = bagTarget - AnchorOffset(_held, _heldRot);

                    // The bag has no bases, so a rune cannot go in and its passengers have nowhere
                    // to ride to. They come off here rather than vanishing with the base.
                    if (_bag.Place(_held, bagOrigin, _heldRot) != null)
                    {
                        DropRiders();
                        _held = null;
                    }
                    else if (_bag.CanReplaceAt(_held, bagOrigin, _heldRot))
                    {
                        ScatterAll(_bag.ClearFootprint(_held, bagOrigin, _heldRot), mouse);

                        if (_bag.Place(_held, bagOrigin, _heldRot) != null)
                        {
                            DropRiders();
                            _held = null;
                        }
                    }

                    return;
                }

                var target = ScreenToCell(mouse);
                if (target.x >= 0)
                {
                    var gridOrigin = target - AnchorOffset(_held, _heldRot);

                    if (Book.Place(_held, gridOrigin, _heldRot) != null)
                    {
                        LandRiders(gridOrigin, mouse);
                        _held = null;
                    }
                    else if (Book.CanReplaceAt(_held, gridOrigin, _heldRot))
                    {
                        // Occupied â€” kick the old piece out and take its spot.
                        ScatterAll(Book.ClearFootprint(_held, gridOrigin, _heldRot), mouse);

                        if (Book.Place(_held, gridOrigin, _heldRot) != null)
                        {
                            LandRiders(gridOrigin, mouse);
                            _held = null;
                        }
                    }

                    return;
                }

                // Clicked empty space â€” drop it right there.
                DropRiders();
                AddLoose(_held, mouse);
                _held = null;
                return;
            }

            int looseIndex = ScreenToLoose(mouse);
            if (looseIndex >= 0)
            {
                _held = _loose[looseIndex];
                _heldRot = 0;
                RemoveLoose(looseIndex);
                return;
            }

            var bagCell = ScreenToBagCell(mouse);
            if (bagCell.x >= 0)
            {
                var stored = _bag.At(bagCell);
                if (stored != null)
                {
                    _held = stored.Def;
                    _heldRot = stored.Rot;
                    _bag.Remove(stored);
                }

                return;
            }

            var cell = ScreenToCell(mouse);
            if (cell.x < 0) return;

            // Skills sit on top, so they get picked up first.
            var inst = Book.SkillAt(cell) ?? Book.BaseAt(cell);
            if (inst == null) return;

            _held = inst.Def;
            _heldRot = inst.Rot;

            // Lifting a base takes whatever is standing on it. Anything bridging two bases belongs
            // to neither, so Remove sends those to the floor as it always did.
            _riders = Book.LiftRiders(inst);
            ScatterAll(Book.Remove(inst), mouse);
        }

        /// <summary>Shop / recipe panel clicks. Returns true when the click was consumed.</summary>
        bool ShopEventActive =>
            Player.Alive && !Enemies.WaveActive &&
            (_run != null
                ? _run.Resting && _run.RestKind == RunNodeKind.Shop
                : Enemies.Wave > 0 && Enemies.Wave % _balance.ShopEveryWaves == 0);

        bool HandlePanelClick(Vector2 mouse)
        {
            // Tirai sedang di layar — tidak ada satu pun klik yang boleh tembus.
            if (_coverT > 0f) return true;

            if (_mapChoose)
            {
                // Penanda sedang berjalan: pilihannya sudah jatuh, sisanya animasi.
                if (_mapTravelTo >= 0) return true;

                var map = _run.Map;
                var panel = MapView();
                var reachable = map.Reachable();

                for (int i = 0; i < reachable.Count; i++)
                {
                    Vector2 at = MapNodePos(reachable[i], panel, map.Floors, map.Lanes);
                    if (Vector2.Distance(mouse, at) > 34f) continue;

                    BeginMapTravel(reachable[i]);
                    return true;
                }

                // Klik di luar node = pegangan buat MENGGESER peta. Menutup tetap tidak bisa —
                // memilih itu wajib.
                _mapDragging = true;
                _mapDragLast = mouse;
                return true;
            }

            if (_mapOpen)
            {
                // Peta yang diintip itu kaca: tidak memilih apa-apa — tapi boleh DIGESER.
                // Menutupnya lewat M / tombol PETA.
                _mapDragging = true;
                _mapDragLast = mouse;
                return true;
            }

            if (_eventOpen)
            {
                // Modal sungguhan: kejadian menelan SEMUA klik. Pilihan yang bisa tertutup oleh
                // klik nyasar bukan pilihan.
                if (EventOptionRect(0).Contains(mouse)) TakePact(0);
                else if (EventOptionRect(1).Contains(mouse)) TakePact(1);
                else if (EventRefuseRect().Contains(mouse)) RefusePact();

                return true;
            }

            if (_gambleOpen)
            {
                if (!PanelRect().Contains(mouse))
                {
                    // Klik yang MENUTUP panel tidak ikut ditelan — kecuali tangan sedang
                    // memegang piece.
                    //
                    // Laporan pemilik project: "ui shop masih bloking, kadang gak bisa ditarik".
                    // Sebabnya di sini: klik pertama di luar panel habis dipakai untuk menutup,
                    // jadi mengambil piece butuh DUA klik — dan yang pertama tidak memberi tanda
                    // apa pun bahwa ia sudah terpakai. Yang terbaca pemain bukan "panelnya
                    // tertutup", melainkan "tarikan saya tidak jalan".
                    //
                    // Yang memegang piece tetap ditelan: menjatuhkan barang ke tempat yang baru
                    // saja tertutup adalah kehilangan yang tidak bisa dibatalkan, dan itu jauh
                    // lebih mahal daripada satu klik yang terbuang.
                    _gambleOpen = false;
                    return _held != null;
                }

                if (RerollRect().Contains(mouse) && _spinLeft <= 0f)
                {
                    if (_gold < _balance.GambleCost)
                    {
                        _slotResultLine = "koin kurang";
                        return true;
                    }

                    // Hasil diundi SEKARANG, animasi cuma menunda pengumumannya — gulungan yang
                    // menentukan hasil di frame berhentinya sendiri tidak bisa dites.
                    _gold -= _balance.GambleCost;
                    _slotOutcome = RollGambleOutcome();
                    _spinLeft = 1.15f;
                }

                return true;
            }

            // Tombol LANJUT lolos dari toko, dan itu bukan kemewahan.
            //
            // Panel toko TUMPANG TINDIH dengan tombolnya di pulau rehat, dan dua baris di bawah
            // sini menelan setiap klik yang jatuh di dalam panel ("return true" di ujung) —
            // termasuk klik ke tombol yang sedang terlihat menyala hijau di atasnya. Yang
            // dirasakan pemain: tombol satu-satunya untuk pergi dari toko tidak bisa dipencet,
            // tanpa satu pun tanda kenapa.
            //
            // Ditaruh SESUDAH peta, kejadian, dan slot: ketiganya modal sungguhan yang memang
            // harus menelan segalanya. Toko bukan modal — klik di luarnya saja sudah menutupnya.
            if ((CanStartWave() || CanDepart()) && StartButtonRect().Contains(mouse)) return false;

            if (ShopEventActive && ShopButtonRect().Contains(mouse))
            {
                _shopOpen = !_shopOpen;
                return true;
            }

            if (!_shopOpen) return false;

            // Klik ke PIECE TERCECER selalu lolos, walau jatuhnya di dalam panel. Barang yang
            // barusan dibeli dilempar dekat slotnya — DI DALAM panel — dan tanpa baris ini guard
            // di bawah menelan kliknya: belanjaan tergeletak di depan mata dan tidak bisa
            // diambil, tanpa satu pun tanda kenapa.
            if (ScreenToLoose(mouse) >= 0) return false;

            if (!PanelRect().Contains(mouse))
            {
                // Klik yang MENUTUP panel tidak ikut ditelan — kecuali tangan sedang
                // memegang piece.
                //
                // Laporan pemilik project: "ui shop masih bloking, kadang gak bisa ditarik".
                // Sebabnya di sini: klik pertama di luar panel habis dipakai untuk menutup,
                // jadi mengambil piece butuh DUA klik — dan yang pertama tidak memberi tanda
                // apa pun bahwa ia sudah terpakai. Yang terbaca pemain bukan "panelnya
                // tertutup", melainkan "tarikan saya tidak jalan".
                //
                // Yang memegang piece tetap ditelan: menjatuhkan barang ke tempat yang baru
                // saja tertutup adalah kehilangan yang tidak bisa dibatalkan, dan itu jauh
                // lebih mahal daripada satu klik yang terbuang.
                _shopOpen = false;
                return _held != null;
            }

            if (RerollRect().Contains(mouse))
            {
                if (_gold >= _rerollCost)
                {
                    _gold -= _rerollCost;
                    _rerollCost += _balance.RerollCostIncrement;
                    RollShop();
                }

                return true;
            }

            for (int i = 0; i < ShopSlots; i++)
            {
                if (_shop[i] == null) continue;
                if (!ShopSlotRect(i).Contains(mouse)) continue;

                int price = _balance.PriceOf(_shop[i]);
                if (_gold < price) return true;

                _gold -= price;
                AddLoose(_shop[i], NearScatterPos(ShopSlotRect(i).center, i));
                _shop[i] = null;
                return true;
            }

            return true;
        }

        /// <summary>Re-seats the skills that travelled with a base. Whatever no longer fits falls.</summary>
        void LandRiders(Vector2Int runeOrigin, Vector2 near)
        {
            if (_riders.Count == 0) return;

            ScatterAll(Book.SeatRiders(_riders, runeOrigin), near);
            _riders.Clear();
        }

        /// <summary>Lets go of the passengers without moving the base. They land where you stand.</summary>
        void DropRiders()
        {
            if (_riders.Count == 0) return;

            for (int i = 0; i < _riders.Count; i++) AddLoose(_riders[i].Def);
            _riders.Clear();
        }

        /// <summary>Puts the held piece down on the floor â€” nothing is ever lost silently.</summary>
        void StashHeld()
        {
            if (_held == null) return;

            DropRiders();
            AddLoose(_held);
            _held = null;
        }

        void OnEnemyKilled(Vector3 at)
        {
            if (Random.value > _balance.KillDropChance) return;

            // Kenaikan bintang elite berlaku untuk SELURUH panen wave itu, bukan cuma hadiah
            // penutupnya. Drop dari kill jumlahnya jauh lebih banyak, jadi di sinilah hadiah
            // elite benar-benar terasa — hadiah penutup saja cuma menambah dua barang.
            int starBonus = _run != null && _run.CurrentKind == RunNodeKind.Elite
                ? _balance.EliteStarBonus
                : 0;

            var drop = _db.RandomDrop(_balance.RuneShareOfDrops, _balance.DropStarWeights,
                    _balance.DropStarMinWave, Enemies.Wave, starBonus);
            if (drop == null) return;

            if (_pendingDrops.Count < _balance.MaxDropsPerWave) _pendingDrops.Add(drop);
            else _pendingGold += ValueOf(drop);
        }

        void OnWaveCleared()
        {
            Player.Hp = Mathf.Min(Player.MaxHp, Player.Hp + _balance.HealPerWaveClear);

            // Toko milik PETA sekarang — stoknya dikocok saat mendarat di node toko. Jalur
            // kelipatan-wave lama cuma hidup kalau run berjalan tanpa sutradara peta.
            if (_run == null && Enemies.Wave % _balance.ShopEveryWaves == 0) RollShop();

            ReleaseDrops();

            // Hasil yang tidak dapat tempat TIDAK membatalkan evolusinya — ia dikeluarkan ke sini.
            _spilled = 0;
            var evolutions = Book.ResolveEvolutions(SpillFromBoard);

            // The bag cooks too. Spare copies used to pile up in there with nowhere to go.
            evolutions.AddRange(_bag.ResolveEvolutions(_db, SpillFromBag));
            for (int i = 0; i < Book.Placed.Count; i++) Discover(Book.Placed[i].Def);
            if (evolutions.Count == 0) return;

            _sb.Length = 0;
            _sb.Append("EVOLVE!   ");
            for (int i = 0; i < evolutions.Count; i++)
            {
                if (i > 0) _sb.Append("   |   ");
                _sb.Append(evolutions[i]);
            }

            _evolveText.color = _spilled > 0 ? new Color(1f, 0.78f, 0.45f) : new Color(0.55f, 1f, 0.7f);
            _evolveText.text = _sb.ToString();
            _evolveTimer = 6f;

            // Runenya berputar sekali. Ditaruh di sini, bukan di tempat resep diselesaikan:
            // di sini sudah dipastikan ADA yang benar-benar berevolusi (baris keluar lebih awal
            // kalau daftarnya kosong), jadi runenya tidak pernah berputar untuk apa-apa.
            if (_rune != null) _rune.Celebrate();
            PushFloater(Player.transform.position + Vector3.up * 3f, "EVOLVE!", new Color(0.55f, 1f, 0.7f));
        }

        int _spilled;

        /// <summary>
        /// Hasil evolusi yang tidak dapat tempat, dikeluarkan tepat DI SEBELAH bahannya.
        ///
        /// Jaraknya sengaja pendek — cukup untuk lepas dari petak bekas bahannya, tidak lebih.
        /// Barang yang mendarat jauh dari sumbernya berhenti terbaca sebagai hasil peleburan
        /// yang barusan terjadi, dan pemain harus mencarinya alih-alih memungutnya.
        /// </summary>
        void SpillFromBoard(PieceDefinition def, Vector2 cell) => Spill(def, GridPoint(cell));

        void SpillFromBag(PieceDefinition def, Vector2 cell) => Spill(def, BagPoint(cell));

        void Spill(PieceDefinition def, Vector2 at)
        {
            if (def == null) return;

            _spilled++;

            // Digeser sedikit ke kanan-atas dari titik bahannya supaya tidak menindih petak yang
            // baru saja kosong — masih dalam satu pandangan, tapi jelas "di luar papan".
            var pos = at + new Vector2(0f, CellSize + 26f);

            pos.x = Mathf.Clamp(pos.x, 60f, Mathf.Max(80f, Screen.width - 60f));
            pos.y = Mathf.Clamp(pos.y, 60f, Mathf.Max(80f, Screen.height - 90f));

            AddLoose(def, pos);
            PushFloater(Player.transform.position + Vector3.up * 2.8f,
                def.DisplayName + " kepental keluar", new Color(1f, 0.72f, 0.35f));
        }

        void DrawEvoLines()
        {
            var ghostDef = ResolveGhost(out var ghostOrigin);

            // While a piece is in hand the line has to track the cursor, so a moved ghost forces an
            // immediate rebuild instead of waiting out the idle throttle.
            bool ghostChanged = ghostDef != _ghostDef || ghostOrigin != _ghostOrigin || _heldRot != _ghostRot;
            _ghostDef = ghostDef;
            _ghostOrigin = ghostOrigin;
            _ghostRot = _heldRot;

            _previewTimer -= Time.unscaledDeltaTime;
            if (_previewTimer <= 0f || ghostChanged)
            {
                _previewTimer = 0.25f;
                _previews = Book.FindPendingGroups(ghostDef, ghostOrigin, _heldRot);
                _bagPreviews = _bag.FindPendingGroups(_db);
            }

            int cursor = 0;
            for (int g = 0; g < _previews.Count; g++) cursor = DrawEvoGroup(_previews[g], cursor, GridPoint);

            // The bag evolves as well, so it needs the same connectors — in bag coordinates.
            for (int g = 0; g < _bagPreviews.Count; g++)
                cursor = DrawEvoGroup(_bagPreviews[g], cursor, BagPoint);

            cursor = DrawPartnerLines(cursor);

            for (int i = cursor; i < EvoLinePool; i++) _evoLines[i].enabled = false;
        }

        /// <summary>
        /// While a piece is in hand, blue cables run from the cursor to everything on the board it
        /// could combine with.
        ///
        /// Always blue. These are possibilities, and gold means one thing only in this UI: that
        /// group is going to evolve when the wave ends. Nothing still in your hand can make that
        /// promise, so nothing still in your hand is allowed to be gold.
        ///
        /// Drawn from the CURSOR rather than a grid cell, so the answer is there the instant the
        /// piece leaves the floor instead of only once it already hovers a legal square.
        /// </summary>
        int DrawPartnerLines(int cursor)
        {
            if (_held == null || !Player.Alive || Enemies.WaveActive) return cursor;

            _partnerTimer -= Time.unscaledDeltaTime;
            if (_partnerTimer <= 0f || _partnerFor != _held)
            {
                _partnerTimer = 0.25f;
                _partnerFor = _held;
                _partners = Book.FindPartners(_held);
            }

            var from = ProtoInput.MousePosition;

            for (int i = 0; i < _partners.Count && cursor < EvoLinePool; i++)
            {
                DrawEvoLink(_evoLines[cursor++], from, GridPoint(_partners[i]),
                    LinkIncomplete, EvoLineThin);
            }

            return cursor;
        }

        /// <summary>
        /// Wires one recipe group together. The ingredients are visited nearest-first so the chain
        /// hugs the pieces instead of criss-crossing the board when a group has three parts.
        /// </summary>
        int DrawEvoGroup(EvoPreview preview, int cursor, System.Func<Vector2, Vector2> toPixels)
        {
            var members = preview.Members;
            if (members == null || members.Length < 2) return cursor;

            // Gold is a promise that this group WILL evolve when the wave ends. A group that only
            // adds up because the piece on the cursor is counted in has promised nothing — the
            // player can still drop it somewhere else, so it stays blue until it is actually down.
            bool settled = preview.Complete && !preview.NeedsHeldPiece;

            // Jingga setebal emas: sama-sama janji yang akan ditepati, cuma beda tempat mendarat.
            bool spills = settled && preview.SpillsOut;

            var color = spills ? LinkSpill : settled ? LinkComplete : LinkIncomplete;
            float thickness = settled ? EvoLineThick : EvoLineThin;

            // Walk order: start at the first ingredient, then always hop to the closest one left.
            System.Array.Copy(members, _evoWalk, members.Length);
            int count = members.Length;

            for (int i = 1; i < count; i++)
            {
                int nearest = i;
                float bestSqr = float.MaxValue;

                for (int k = i; k < count; k++)
                {
                    float sqr = (_evoWalk[k] - _evoWalk[i - 1]).sqrMagnitude;
                    if (sqr >= bestSqr) continue;

                    bestSqr = sqr;
                    nearest = k;
                }

                var swap = _evoWalk[i];
                _evoWalk[i] = _evoWalk[nearest];
                _evoWalk[nearest] = swap;

                if (cursor >= EvoLinePool) break;
                DrawEvoLink(_evoLines[cursor++], toPixels(_evoWalk[i - 1]), toPixels(_evoWalk[i]),
                    color, thickness);
            }

            return cursor;
        }

        /// <summary>Both ends in canvas pixels — the caller decides what space it is working in.</summary>
        static void DrawEvoLink(Image line, Vector2 a, Vector2 b, Color color, float thickness)
        {
            var delta = b - a;

            line.enabled = true;
            line.color = color;
            line.rectTransform.anchoredPosition = (a + b) * 0.5f;
            line.rectTransform.sizeDelta = new Vector2(delta.magnitude, thickness);
            line.rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        /// <summary>Centre of a (possibly fractional) grid cell, in canvas pixels.</summary>
        static Vector2 GridPoint(Vector2 cell)
        {
            float step = CellSize + CellGap;
            return new Vector2(GridX + cell.x * step + CellSize * 0.5f,
                               GridY + cell.y * step + CellSize * 0.5f);
        }

        /// <summary>Same, for the backpack — different origin and a smaller cell.</summary>
        static Vector2 BagPoint(Vector2 cell)
        {
            float step = BagCell + BagGap;
            return new Vector2(RightX() + cell.x * step + BagCell * 0.5f,
                               BagY + cell.y * step + BagCell * 0.5f);
        }

        /// <summary>
        /// The grid cell the held piece would occupy right now, or null when nothing is held or the
        /// cursor is off the grid. Only reports a spot the piece could legally take.
        /// </summary>
        PieceDefinition ResolveGhost(out Vector2Int origin)
        {
            origin = default;
            if (_held == null || !Player.Alive || Enemies.WaveActive) return null;

            var cell = ScreenToCell(ProtoInput.MousePosition);
            if (cell.x < 0) return null;

            origin = cell - AnchorOffset(_held, _heldRot);
            return _held;
        }

        // ==================================================================
        //  peta run, pulau rehat, slot, kejadian
        // ==================================================================

        /// <summary>Menyambungkan sutradara run. UI yang memegang gold dan papan, jadi semua
        /// akibat node (stok toko, hadiah slot, tukar nasib) dieksekusi di sini.</summary>
        public void AttachRun(RunDirector run)
        {
            _run = run;

            run.CanEmbark = () => Player.Alive && Book.Spells.Count > 0;

            // Ritual yang dulu milik tombol MULAI WAVE, sekarang milik langkah masuk portal.
            run.OnEmbark = () =>
            {
                StashHeld();
                SellLoose();
                Player.ResetCooldowns();
            };

            run.OnRestEntered = OnRestEntered;
            run.OnAnnounce = (message, color) => Announce(message, color);

            // Layar sudah gelap penuh: panel apa pun yang tersisa ditutup, peta mengambil alih
            // sebagai PEMILIH — klik node = berangkat, bukan sekadar mengintip.
            run.OnMapChoose = () =>
            {
                _shopOpen = false;
                _gambleOpen = false;
                _eventOpen = false;

                _mapOpen = true;
                _mapChoose = true;
                _mapSig = -1;
                _mapTravelTo = -1;

                // Peta pemilih terbuka = singgahnya sudah selesai. Ini titik keluar yang paling
                // dapat dipercaya: apa pun cara pemain meninggalkan toko — tombol LANJUT, SPACE,
                // atau apa pun yang ditambahkan nanti — semuanya bermuara ke sini.
                if (_rooms != null) _rooms.Hide();
            };

            BuildRunPanels();
            BuildShopFromPrefab();

            // Paling akhir: OnMapChoose di atas sudah terpasang, dan membukanya sebelum itu
            // berarti peta berpindah ke mode pemilih tanpa ada yang mendengarnya.
            OpenRunOnMap();
        }

        /// <summary>
        /// Membaca kotak-kotak panel singgah dari prefab tema, kalau ada.
        ///
        /// Override-nya DIBERSIHKAN dulu, apa pun yang terjadi sesudahnya: static bertahan
        /// melintasi pemuatan ulang scene, dan tema yang prefabnya dicopot di tengah sesi tidak
        /// boleh meninggalkan tata letak lama menggantung.
        /// </summary>
        void BuildShopFromPrefab()
        {
            ShopPanelOverride = null;
            ShopSlotsOverride = null;
            ShopRerollOverride = null;
            StartButtonOverride = null;

            if (_theme == null || _theme.ShopPrefab == null) return;

            var go = Instantiate(_theme.ShopPrefab, _canvas.transform, false);
            go.name = "ShopAnchors";

            var rig = go.GetComponent<ShopRig>() ?? go.GetComponentInChildren<ShopRig>(true);

            if (rig == null)
            {
                Debug.LogWarning("[GrimoireUI] ShopPrefab tidak punya ShopRig — tata letak " +
                                 "hitungan lama yang dipakai.");
                Destroy(go);
                return;
            }

            // Sama seperti strip: sudut dunia baru benar setelah kanvas menghitung tata letak.
            Canvas.ForceUpdateCanvases();

            if (rig.Panel != null) ShopPanelOverride = CanvasRectOf(rig.Panel);
            if (rig.Reroll != null) ShopRerollOverride = CanvasRectOf(rig.Reroll);
            if (rig.StartButton != null) StartButtonOverride = CanvasRectOf(rig.StartButton);

            if (rig.Slots != null && rig.Slots.Length > 0)
            {
                var slots = new Rect[rig.Slots.Length];
                bool any = false;

                for (int i = 0; i < rig.Slots.Length; i++)
                {
                    if (rig.Slots[i] == null) continue;
                    slots[i] = CanvasRectOf(rig.Slots[i]);
                    any = true;
                }

                if (any) ShopSlotsOverride = slots;
            }

            // Tombol LANJUT yang TERGAMBAR ikut kotaknya. Hit-test membaca StartButtonRect(),
            // tapi gambarnya ditaruh sekali di BuildHud — tanpa baris ini keduanya berpisah:
            // tombol terlihat di tengah, kliknya didengar di pojok.
            if (StartButtonOverride.HasValue && _startBg != null && _startLabel != null)
            {
                var r = StartButtonOverride.Value;
                var centre = new Vector2(r.center.x - Screen.width * 0.5f,
                                         r.center.y - Screen.height * 0.5f);

                _startBg.rectTransform.anchoredPosition = centre;
                _startBg.rectTransform.sizeDelta = r.size;
                _startLabel.rectTransform.anchoredPosition = centre;
                _startLabel.rectTransform.sizeDelta = r.size;
            }
        }

        void OnRestEntered(RunNodeKind kind)
        {
            _shopOpen = false;
            _gambleOpen = false;
            _eventOpen = false;

            if (kind == RunNodeKind.Shop)
            {
                // Stok DIKOCOK tiap kali singgah — node toko yang isinya itu-itu saja bukan
                // hadiah, cuma etalase.
                RollShop();
                _shopOpen = true;
            }
            else if (kind == RunNodeKind.Gamble)
            {
                _gambleOpen = true;
                _spinLeft = 0f;
                _slotOutcome = -1;
                _slotResultLine = "";
            }
            else if (kind == RunNodeKind.Event)
            {
                _eventOpen = true;
                _eventDone = false;

                // Diundi di sini, sekali. Yang sudah dipegang disaring di dalam RollPacts — tawaran
                // yang berisi pakta yang sudah dimiliki adalah pilihan yang tidak melakukan apa-apa,
                // dan pemain baru tahu setelah mengkliknya, lalu kehilangan seluruh kejadiannya.
                _db.RollPacts(Player.Pacts, _pactOffer);
            }

            ShowRoomFor(kind);
        }

        /// <summary>
        /// Menukar LATAR di belakang panel singgah ke ruangan yang sesuai.
        ///
        /// Panelnya sendiri tidak pindah ke mana-mana — ia tetap digambar di kanvas yang sama
        /// seperti dulu. Yang berubah apa yang ada di belakangnya: panel dagang yang mengambang
        /// di atas rumput yang barusan berdarah tidak pernah terbaca sebagai singgah, cuma
        /// sebagai jeda.
        ///
        /// Tanpa <see cref="RoomLoader"/> — atau tanpa scene ruangannya di Build Settings —
        /// semuanya tetap berjalan persis seperti sebelum ruangan ada.
        /// </summary>
        void ShowRoomFor(RunNodeKind kind)
        {
            if (_rooms == null) return;

            switch (kind)
            {
                case RunNodeKind.Shop: _rooms.Show(RoomLoader.ShopScene); break;
                case RunNodeKind.Event: _rooms.Show(RoomLoader.EventScene); break;
                case RunNodeKind.Gamble: _rooms.Show(RoomLoader.SlotScene); break;
                default: _rooms.Hide(); break;
            }
        }

        void BuildRunPanels()
        {
            int nodeCap = Mathf.Max(1, _balance.MapFloorsPerAct * _balance.MapLanes);

            // Wadah peta: rect kosong di titik nol dengan anchor bawah-kiri — koordinat anak
            // tetap koordinat layar, cuma induknya sekarang satu dan bisa diurutkan ulang.
            var rootGo = new GameObject("MapRoot", typeof(RectTransform));
            _mapRoot = rootGo.GetComponent<RectTransform>();
            _mapRoot.SetParent(_canvas.transform, false);
            _mapRoot.anchorMin = Vector2.zero;
            _mapRoot.anchorMax = Vector2.zero;
            _mapRoot.pivot = Vector2.zero;
            _mapRoot.anchoredPosition = Vector2.zero;
            _mapRoot.sizeDelta = Vector2.zero;

            // PEKAT penuh, bukan 0,97 — di bawahnya ada papan grimoire dan teks banner, dan
            // keduanya membayang tembus di alpha berapa pun selain satu. Berlaku untuk kedua
            // wajah: perkamen pun harus buram, kalau tidak papan grimoire membayang di baliknya.
            var paper = _theme != null ? _theme.MapPaper : null;

            _mapBg = MakeImage("MapBg", Vector2.zero, Vector2.zero,
                paper != null ? Color.white : new Color(0.045f, 0.055f, 0.1f, 1f), Vector2.zero);
            _mapBg.transform.SetParent(_mapRoot, false);

            if (paper != null)
            {
                _mapBg.sprite = paper;

                // Simple, bukan Sliced/Tiled: perkamennya noda tak beraturan, bukan bingkai.
                // Ditarik melar dari potret ke lanskap memang mengubah bentuk nodanya — dan
                // justru itu yang tidak kelihatan, karena tidak ada bentuk yang dijanjikan.
                _mapBg.type = Image.Type.Simple;
                _mapBg.preserveAspect = false;
            }

            // Tinta di atas perkamen. Krem dan biru-muda yang lama dipilih untuk latar
            // biru-gelap; di atas kertas terang keduanya lenyap.
            _mapTitle = MakeText("MapTitle", Vector2.zero, new Vector2(480f, 30f), 22,
                _theme != null ? _theme.MapTitleInk : new Color(1f, 0.9f, 0.6f),
                Vector2.zero, TextAnchor.MiddleCenter);
            _mapLegend = MakeText("MapLegend", Vector2.zero, new Vector2(620f, 40f), 13,
                _theme != null ? _theme.MapLegendInk : new Color(0.8f, 0.85f, 0.9f),
                Vector2.zero, TextAnchor.MiddleCenter);

            _mapTitle.transform.SetParent(_mapRoot, false);
            _mapLegend.transform.SetParent(_mapRoot, false);

            Centre(_mapBg.rectTransform);
            Centre(_mapTitle.rectTransform);
            Centre(_mapLegend.rectTransform);

            // Jalur = bezier PUTUS-PUTUS, pola yang sama dengan peta referensi (RoguelikeMapUI):
            // tiap sambungan dipecah jadi segmen pendek bercelah dengan control point acak
            // ber-seed — jalan setapak yang berkelok, bukan kabel penggaris.
            _mapEdges = new Image[nodeCap * 2 * MapSegsPerEdge];

            for (int i = 0; i < _mapEdges.Length; i++)
            {
                _mapEdges[i] = MakeImage("MapSeg" + i, Vector2.zero, new Vector2(10f, 5f),
                    Color.gray, Vector2.zero);
                _mapEdges[i].transform.SetParent(_mapRoot, false);
                Centre(_mapEdges[i].rectTransform);
                _mapEdges[i].enabled = false;
            }

            _mapNodes = new Image[nodeCap];
            _mapRings = new Image[nodeCap];
            _mapGlyphs = new Text[nodeCap];

            for (int i = 0; i < nodeCap; i++)
            {
                _mapRings[i] = MakeImage("MapRing" + i, Vector2.zero, new Vector2(42f, 42f),
                    Color.white, Vector2.zero);
                _mapRings[i].transform.SetParent(_mapRoot, false);
                Centre(_mapRings[i].rectTransform);
                _mapRings[i].enabled = false;

                _mapNodes[i] = MakeImage("MapNode" + i, Vector2.zero, new Vector2(34f, 34f),
                    Color.white, Vector2.zero);
                _mapNodes[i].transform.SetParent(_mapRoot, false);
                Centre(_mapNodes[i].rectTransform);
                _mapNodes[i].enabled = false;

                _mapGlyphs[i] = MakeText("MapGlyph" + i, Vector2.zero, new Vector2(36f, 30f), 16,
                    Color.black, Vector2.zero, TextAnchor.MiddleCenter);
                _mapGlyphs[i].transform.SetParent(_mapRoot, false);
                Centre(_mapGlyphs[i].rectTransform);
                _mapGlyphs[i].enabled = false;
            }

            _mapYou = MakeText("MapYou", Vector2.zero, new Vector2(120f, 24f), 15,
                _theme != null ? _theme.MapYouInk : new Color(1f, 0.85f, 0.4f),
                Vector2.zero, TextAnchor.MiddleCenter);
            _mapYou.transform.SetParent(_mapRoot, false);
            Centre(_mapYou.rectTransform);
            _mapYou.text = "KAMU";
            _mapYou.enabled = false;

            // KARAKTER pemain di peta: token bulat kuning — warna yang sama dengan kapsul
            // pemain di lapangan, supaya "itu gw" terbaca tanpa dijelaskan. Bulatnya memakai
            // `_circle` yang sudah dibuat BuildSkillWidgets (jalan lebih dulu, lihat Init);
            // sprite bulat bawaan Unity TIDAK bisa dipakai — GetBuiltinResource("UI/Skin/
            // Knob.psd") gagal saat runtime.
            _mapMark = MakeImage("MapMark", Vector2.zero, new Vector2(26f, 26f),
                new Color(1f, 0.8f, 0.25f, 1f), Vector2.zero);
            _mapMark.transform.SetParent(_mapRoot, false);
            Centre(_mapMark.rectTransform);
            _mapMark.sprite = _circle;
            _mapMark.enabled = false;

            // Gloom tepi — dibuat TERAKHIR karena di kanvas ini urutan bikin adalah urutan
            // gambar, dan ia harus jatuh di atas SEMUA isi peta. Ditaruh di bawah node, yang
            // terjadi adalah kertas menghitam sementara node di tepi tetap menyala penuh —
            // dua bahan yang tidak saling kenal. Di atas, seluruh tepi peta surut bersama.
            BuildMapGloom();

            // Tirai hitam satu layar penuh. Dibentangkan lewat anchor, bukan ukuran — resolusi
            // berapa pun tertutup. SetAsLastSibling saat dipakai yang menjaganya tetap teratas.
            _fadeCover = MakeImage("FadeCover", Vector2.zero, Vector2.zero,
                Color.black, Vector2.zero);
            _fadeCover.rectTransform.anchorMin = Vector2.zero;
            _fadeCover.rectTransform.anchorMax = Vector2.one;
            _fadeCover.rectTransform.offsetMin = Vector2.zero;
            _fadeCover.rectTransform.offsetMax = Vector2.zero;
            _fadeCover.enabled = false;

            _mapBg.enabled = false;
            _mapTitle.enabled = false;
            _mapLegend.enabled = false;

            // ---- slot ----
            _slotBg = MakeImage("SlotBg", Vector2.zero, new Vector2(PanelW, PanelH),
                new Color(0.1f, 0.05f, 0.11f, 0.97f), Vector2.zero);
            _slotTitle = MakeText("SlotTitle", Vector2.zero, new Vector2(500f, 30f), 22,
                new Color(1f, 0.45f, 0.85f), Vector2.zero, TextAnchor.MiddleCenter);
            _slotInfo = MakeText("SlotInfo", Vector2.zero, new Vector2(500f, 44f), 15,
                new Color(0.9f, 0.9f, 0.95f), Vector2.zero, TextAnchor.MiddleCenter);
            _slotSpinBg = MakeImage("SlotSpin", Vector2.zero, Vector2.zero,
                new Color(0.75f, 0.2f, 0.5f, 0.95f), Vector2.zero);
            _slotSpinLabel = MakeText("SlotSpinLabel", Vector2.zero, new Vector2(240f, 34f), 18,
                Color.white, Vector2.zero, TextAnchor.MiddleCenter);

            Centre(_slotBg.rectTransform);
            Centre(_slotTitle.rectTransform);
            Centre(_slotInfo.rectTransform);
            Centre(_slotSpinBg.rectTransform);
            Centre(_slotSpinLabel.rectTransform);

            for (int i = 0; i < 3; i++)
            {
                _slotReels[i] = MakeText("SlotReel" + i, Vector2.zero, new Vector2(120f, 80f), 52,
                    Color.white, Vector2.zero, TextAnchor.MiddleCenter);
                Centre(_slotReels[i].rectTransform);
                _slotReels[i].enabled = false;
            }

            _slotBg.enabled = false;
            _slotTitle.enabled = false;
            _slotInfo.enabled = false;
            _slotSpinBg.enabled = false;
            _slotSpinLabel.enabled = false;

            // ---- kejadian ----
            _eventBg = MakeImage("EventBg", Vector2.zero, new Vector2(PanelW, PanelH),
                new Color(0.08f, 0.06f, 0.12f, 0.97f), Vector2.zero);
            _eventTitle = MakeText("EventTitle", Vector2.zero, new Vector2(500f, 30f), 22,
                new Color(0.75f, 0.5f, 1f), Vector2.zero, TextAnchor.MiddleCenter);
            _eventBody = MakeText("EventBody", Vector2.zero, new Vector2(540f, 140f), 17,
                new Color(0.92f, 0.92f, 0.97f), Vector2.zero, TextAnchor.MiddleCenter);
            _eventABg = MakeImage("EventA", Vector2.zero, Vector2.zero,
                new Color(0.2f, 0.4f, 0.25f, 0.95f), Vector2.zero);
            _eventBBg = MakeImage("EventB", Vector2.zero, Vector2.zero,
                new Color(0.4f, 0.24f, 0.45f, 0.95f), Vector2.zero);
            // Kartu pakta membawa tiga baris — nama, berkah, kutuk — jadi kotaknya lebih tinggi
            // dan hurufnya lebih kecil dari label tombol biasa. Dua baris pertama boleh dibaca
            // sekilas; baris kutuk justru yang harus dibaca pelan, dan itu tidak muat di 60 piksel.
            _eventALabel = MakeText("EventALabel", Vector2.zero, new Vector2(276f, 126f), 13,
                Color.white, Vector2.zero, TextAnchor.MiddleCenter);
            _eventBLabel = MakeText("EventBLabel", Vector2.zero, new Vector2(276f, 126f), 13,
                Color.white, Vector2.zero, TextAnchor.MiddleCenter);

            _eventCBg = MakeImage("EventC", Vector2.zero, Vector2.zero,
                new Color(0.22f, 0.22f, 0.26f, 0.9f), Vector2.zero);
            _eventCLabel = MakeText("EventCLabel", Vector2.zero, new Vector2(240f, 30f), 13,
                new Color(0.78f, 0.78f, 0.82f), Vector2.zero, TextAnchor.MiddleCenter);

            Centre(_eventBg.rectTransform);
            Centre(_eventTitle.rectTransform);
            Centre(_eventBody.rectTransform);
            Centre(_eventABg.rectTransform);
            Centre(_eventBBg.rectTransform);
            Centre(_eventALabel.rectTransform);
            Centre(_eventBLabel.rectTransform);
            Centre(_eventCBg.rectTransform);
            Centre(_eventCLabel.rectTransform);

            _eventBg.enabled = false;
            _eventTitle.enabled = false;
            _eventBody.enabled = false;
            _eventABg.enabled = false;
            _eventBBg.enabled = false;
            _eventALabel.enabled = false;
            _eventBLabel.enabled = false;
            _eventCBg.enabled = false;
            _eventCLabel.enabled = false;
        }

        /// <summary>Widget yang diposisikan lewat titik TENGAHNYA — pivot bawaan MakeImage ada
        /// di pojok, dan panel yang dihitung dari pojok selalu meleset separuh ukurannya.</summary>
        static void Centre(RectTransform rt) => rt.pivot = new Vector2(0.5f, 0.5f);

        static Rect EventOptionRect(int side)
        {
            var panel = PanelRect();
            float w = (panel.width - 48f) * 0.5f;
            float x = side == 0 ? panel.xMin + 16f : panel.xMax - 16f - w;
            return new Rect(x, panel.yMin + 58f, w, 132f);
        }

        /// <summary>
        /// Tombol MENOLAK, di bawah kedua kartu pakta.
        ///
        /// Ada supaya pakta tetap sebuah PILIHAN. Dua pakta tanpa jalan keluar bukan keputusan
        /// melainkan pungutan: pemain yang kebetulan diundikan dua pakta yang keduanya mematahkan
        /// build-nya tidak sedang memilih apa pun, ia cuma memilih cara run-nya berakhir.
        /// Bayarannya kecil dengan sengaja — menolak harus terasa seperti melewatkan sesuatu.
        /// </summary>
        static Rect EventRefuseRect()
        {
            var panel = PanelRect();
            return new Rect(panel.center.x - 118f, panel.yMin + 16f, 236f, 32f);
        }

        void DrawRunPanels(float dt)
        {
            bool hasRun = _run != null;

            UpdateMapTransition(dt);
            DrawMapOverlay();
            DrawGamble(dt);
            DrawEvent();
        }

        /// <summary>
        /// Klik node di mode memilih: penanda mulai berjalan dari node sekarang (atau dari tepi
        /// kiri peta di awal act) menuju node terpilih, menyusuri bezier yang SAMA dengan jalur
        /// yang tergambar — seed-nya seed jalur itu juga.
        /// </summary>
        void BeginMapTravel(RunNode node)
        {
            // Titik asalnya TIDAK disimpan — dihitung ulang tiap frame di DrawMapMarker, supaya
            // peta yang digulung di tengah perjalanan tidak meninggalkan penandanya di belakang.
            _mapTravelTo = node.Index;
            _mapTravelT = 0f;
        }

        /// <summary>
        /// Dua animasi kecil yang menjahit peta ke dunia: penanda yang berjalan, lalu tirai
        /// hitam yang menutup sebelum node dieksekusi dan terangkat setelahnya. Gloom yang
        /// membuka kembali diurus RunDirector — tirai ini cuma menyembunyikan pergantian UI.
        /// </summary>
        void UpdateMapTransition(float dt)
        {
            if (_run == null) return;

            if (_mapTravelTo >= 0 && !_coverRising)
            {
                _mapTravelT += dt / Mathf.Max(0.1f, _balance.MapMarkerTravel);

                if (_mapTravelT >= 1f)
                {
                    _mapTravelT = 1f;
                    _coverRising = true;
                }
            }

            if (_coverRising)
            {
                _coverT = Mathf.MoveTowards(_coverT, 1f, dt / CoverInSeconds);

                if (_coverT >= 1f)
                {
                    // Gelap total: BARU sekarang node dieksekusi — teleport, ganti wajah,
                    // dan bongkar-pasang pulau tidak pernah terlihat prosesnya.
                    var node = _run.Map.Nodes[_mapTravelTo];

                    _mapTravelTo = -1;
                    _mapChoose = false;
                    _mapOpen = false;
                    _coverRising = false;

                    _run.PickNode(node);
                }
            }
            else if (_coverT > 0f)
            {
                _coverT = Mathf.MoveTowards(_coverT, 0f, dt / CoverOutSeconds);
            }

            if (_fadeCover != null)
            {
                // Tirai menumpang fase gloom di 45% terakhirnya: penutupan gloom-nya sendiri
                // tetap terlihat menjalar, tapi ujung transisi HITAM TOTAL — bukan "gloom yang
                // sangat gelap" yang masih meloloskan HUD dan siluet pohon.
                float fromFade = _run != null
                    ? Mathf.Clamp01((_run.Fade - 0.55f) / 0.45f)
                    : 0f;

                float alpha = Mathf.Max(_coverT, fromFade);

                _fadeCover.enabled = alpha > 0f;

                if (alpha > 0f)
                {
                    _fadeCover.color = new Color(0f, 0f, 0f, alpha);
                    _fadeCover.rectTransform.SetAsLastSibling();

                    // DrawMapOverlay jalan SESUDAH ini dan mengangkat peta ke atas tirai —
                    // kecuali tirai sedang naik menelan pilihan yang barusan jatuh.
                }
            }
        }

        /// <summary>
        /// Bidang kegelapan yang menggerogoti tepi panel peta, memakai shader UI
        /// <c>Grimoire/GloomEdge</c>. Diam saja kalau shadernya tidak ketemu — peta tanpa gloom
        /// masih peta, dan mematikan seluruh layar peta gara-gara hiasan itu pertukaran yang salah.
        /// </summary>
        void BuildMapGloom()
        {
            var shader = Shader.Find("Grimoire/GloomEdge");

            if (shader == null)
            {
                Debug.LogWarning("[GrimoireUI] shader Grimoire/GloomEdge tidak ketemu — " +
                                 "peta jalan tanpa gloom tepi.");
                return;
            }

            _mapGloomMat = new Material(shader) { name = "GloomEdge (lapisan peta)" };
            Tune(_mapGloomMat);
            _mapGloomMat.SetFloat("_PaperMode", 0f);

            // Perkamennya dipasangi material dari shader yang SAMA, dengan nilai derau yang sama.
            // Kalau keduanya diberi setelan berbeda, lekuk larutnya dan lekuk gelapnya berjalan
            // sendiri-sendiri, dan yang terbaca dua tepi — bukan satu tepi yang menghitam.
            if (_mapBg != null && _mapBg.sprite != null)
            {
                _mapPaperMat = new Material(shader) { name = "GloomEdge (perkamen peta)" };
                Tune(_mapPaperMat);
                _mapPaperMat.SetFloat("_PaperMode", 1f);

                var r = _mapBg.sprite.rect;
                _mapPaperMat.SetFloat("_TexAspect", r.width / Mathf.Max(1f, r.height));

                _mapBg.material = _mapPaperMat;
            }

            _mapGloom = MakeImage("MapGloom", Vector2.zero, Vector2.zero, Color.white, Vector2.zero);
            _mapGloom.transform.SetParent(_mapRoot, false);
            Centre(_mapGloom.rectTransform);
            _mapGloom.material = _mapGloomMat;
            _mapGloom.enabled = false;
        }

        /// <summary>Setelan bersama kedua material gloom peta — satu tempat, supaya tidak bisa beda.</summary>
        void Tune(Material m)
        {
            if (_theme == null) return;
            m.SetColor("_Color", _theme.GloomTint);
            m.SetFloat("_Ceiling", _theme.GloomCeiling);
            m.SetFloat("_Scale", _theme.GloomScale);
            m.SetFloat("_Wobble", _theme.GloomWobble);

            // Kecepatan WAJIB ikut dikirim. Materialnya dibuat runtime dari shader, jadi yang
            // tidak disetel di sini jatuh ke nilai bawaan shader — dan bawaannya lambat sekali
            // sampai tepinya terbaca sebagai gambar diam, bukan sebagai kegelapan yang hidup.
            m.SetFloat("_Churn", _theme.GloomChurn);
            m.SetFloat("_Drift", _theme.GloomDrift);
            m.SetFloat("_TearScale", _theme.TearScale);
            m.SetFloat("_TearFray", _theme.TearFray);
            m.SetFloat("_TearSoft", _theme.TearSoft);
            m.SetFloat("_TearWarp", _theme.TearWarp);
            m.SetFloat("_TearDrift", _theme.TearDrift);
        }

        /// <summary>
        /// Menyetel gloom mengikuti panel yang sedang berlaku. Ukurannya dikirim ke shader dalam
        /// PIKSEL: itu yang membuat pita gelap peta besar dan peta kecil terlihat berasal dari
        /// satu bahan alih-alih satu melar dan satu gepeng.
        ///
        /// Jangkauannya — bukan kepekatannya — yang diperkecil untuk panel intip. Menurunkan
        /// kepekatan akan mengubah warnanya; memendekkan jangkauan menjaga bahannya tetap sama.
        /// </summary>
        void LayoutMapGloom(Rect panel)
        {
            if (_mapGloom == null) return;

            _mapGloom.rectTransform.sizeDelta = panel.size;
            _mapGloom.rectTransform.anchoredPosition = panel.center;

            // Geseran perkamen DI ATAS penjaga di bawah, dan itu bukan detail: penjaga itu
            // menahan seluruh sisa fungsi ini selama ukuran panel tidak berubah — dan scroll
            // justru hal yang berubah tanpa panelnya berubah sedikit pun.
            if (_mapPaperMat != null)
            {
                _mapPaperMat.SetVector("_PaperScroll",
                    new Vector4(0f, panel.height > 1f ? _mapScroll / panel.height : 0f, 0f, 0f));
            }

            if (_mapGloomSize == panel.size) return;
            _mapGloomSize = panel.size;

            float inset = _theme != null ? _theme.GloomInset : 190f;
            float tearDepth = _theme != null ? _theme.TearDepth : 64f;

            if (!_mapChoose && _theme != null)
            {
                // Jangkauannya yang mengecil untuk panel intip, bukan kepekatannya — menurunkan
                // kepekatan mengubah warnanya, memendekkan jangkauan menjaga bahannya sama.
                inset *= _theme.GloomInsetSmallMul;

                // Gigitan sobek TIDAK ikut diperkecil sekuat itu. Ukuran robekan adalah sifat
                // KERTASNYA, bukan sifat panelnya — kertas yang sama disobek dua kali tidak
                // menghasilkan gigitan yang mengecil mengikuti potongannya.
                tearDepth *= Mathf.Lerp(1f, _theme.GloomInsetSmallMul, 0.4f);
            }

            // Dijepit ke seperempat sisi terpendek: kalau tidak, panel sempit bisa membuat kedua
            // sisinya bertemu di tengah dan seluruh peta tertutup gelap.
            inset = Mathf.Min(inset, Mathf.Min(panel.width, panel.height) * 0.25f);

            var size = new Vector4(panel.width, panel.height, 0f, 0f);

            // Gigitan sobek dikirim ke KEDUA material dengan nilai identik — lapisan gelap yang
            // memakai kedalaman berbeda dari kertasnya akan menyisakan tepi gelap menggantung
            // di luar kertas, atau memotong kertas yang masih utuh.
            _mapGloomMat.SetVector("_RectSize", size);
            _mapGloomMat.SetFloat("_Inset", inset);
            _mapGloomMat.SetFloat("_TearDepth", tearDepth);

            if (_mapPaperMat == null) return;

            _mapPaperMat.SetVector("_RectSize", size);
            _mapPaperMat.SetFloat("_Inset", inset);
            _mapPaperMat.SetFloat("_TearDepth", tearDepth);
        }

        /// <summary>
        /// Kotak peta yang BERLAKU SEKARANG. Peta punya dua ukuran, dan seluruh tata letaknya —
        /// lebar pita node, jepitan tepi, batas scroll, uji terlihat-atau-tidak — diturunkan dari
        /// sini. Satu sumber, supaya peta kecil tidak pernah dihitung dengan angka peta besar.
        /// </summary>
        Rect MapView() =>
            _mapChoose || _theme == null
                ? MapPanelRect()
                : MapPeekRect(_theme.PeekScreenFraction);

        void DrawMapOverlay()
        {
            bool open = _mapOpen && _run != null && _mapBg != null;

            if (_mapBg != null)
            {
                _mapBg.enabled = open;
                _mapTitle.enabled = open;
                _mapLegend.enabled = open;
                if (_mapGloom != null) _mapGloom.enabled = open;
            }

            if (!open)
            {
                _mapSig = -1;
                if (_mapYou != null) _mapYou.enabled = false;
                if (_mapMark != null) _mapMark.enabled = false;

                for (int i = 0; i < _mapNodes.Length; i++)
                {
                    if (_mapNodes[i] == null) continue;
                    _mapNodes[i].enabled = false;
                    _mapRings[i].enabled = false;
                    _mapGlyphs[i].enabled = false;
                }

                for (int i = 0; i < _mapEdges.Length; i++)
                {
                    if (_mapEdges[i] != null) _mapEdges[i].enabled = false;
                }

                return;
            }

            // Peta selalu lapisan teratas selama terbuka — di atas tirai hitam (layarnya memang
            // dia) dan di atas panel apa pun. Saat tirai sedang NAIK menelan pilihan, peta
            // dibiarkan di bawahnya supaya ikut tertelan.
            if (_mapRoot != null && !_coverRising) _mapRoot.SetAsLastSibling();

            var map = _run.Map;
            var reachable = map.Reachable();
            var view = MapView();

            // Buka pertama: scroll menjemput posisi pemain — lantai yang sedang dipijak duduk
            // di sepertiga bawah panel, dan sisanya diintip lewat roda mouse.
            if (_mapSig == -1)
            {
                int floor = map.At >= 0 ? map.Nodes[map.At].Floor : 0;
                _mapScroll = Mathf.Clamp(floor * MapFloorGap - (view.height - 220f) * 0.35f,
                    0f, MapScrollMax(map, view));
            }

            // Roda: satu gerigi ≈ satu lantai. Drag: peta nempel di kursor — ditarik ke bawah
            // berarti mengintip ke atas, persis menggeser kertas.
            float wheel = ProtoInput.ScrollY;

            if (wheel != 0f)
            {
                // Satu gerigi = satu lantai. Act panjang butuh langkah scroll yang sepadan;
                // langkah piksel tetap membuat peta 30-an lantai terasa digulung selamanya.
                _mapScroll = Mathf.Clamp(_mapScroll + wheel * MapFloorGap,
                    0f, MapScrollMax(map, view));
            }

            if (_mapDragging)
            {
                if (!ProtoInput.LeftHeld)
                {
                    _mapDragging = false;
                }
                else
                {
                    Vector2 now = ProtoInput.MousePosition;
                    _mapScroll = Mathf.Clamp(_mapScroll - (now.y - _mapDragLast.y),
                        0f, MapScrollMax(map, view));
                    _mapDragLast = now;
                }
            }

            // Tata letak cuma dihitung saat ada yang berubah — enam ratus segmen yang disusun
            // ulang enam puluh kali sedetik adalah harga tanpa barang. Scroll ikut ditandatangani:
            // menggulung = berubah, diam = tidak dihitung ulang.
            int sig = _run.Act * 100000 + (map.At + 2) * 100 + map.Nodes.Count;
            sig = sig * 8191 + Mathf.RoundToInt(_mapScroll);

            // UKURAN panel ikut ditandatangani. Tanpa ini, berpindah antara peta besar dan peta
            // intip — atau sekadar mengubah ukuran jendela — meninggalkan tata letak lama yang
            // dihitung untuk kotak yang sudah tidak ada.
            sig = sig * 31 + Mathf.RoundToInt(view.width) * 7 + Mathf.RoundToInt(view.height);
            sig = sig * 2 + (_mapChoose ? 1 : 0);

            if (sig != _mapSig)
            {
                _mapSig = sig;
                LayoutMap(map, reachable);
            }

            // Denyut per frame — murah, cuma warna: node yang BISA diinjak yang bernapas.
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 5f);

            for (int i = 0; i < map.Nodes.Count && i < _mapNodes.Length; i++)
            {
                var n = map.Nodes[i];

                if (reachable.Contains(n))
                {
                    var tone = RunDirector.KindColor(n.Kind);
                    tone.a = pulse;
                    _mapNodes[i].color = tone;
                    var ringInk = _theme != null ? _theme.MapRingInk : Color.white;
                    ringInk.a = pulse;
                    _mapRings[i].color = ringInk;
                }
                else if (map.At == n.Index)
                {
                    _mapRings[i].color = new Color(1f, 0.85f, 0.4f, 0.6f + 0.4f * pulse);
                }
            }

            DrawMapMarker(map);
        }

        /// <summary>
        /// KARAKTER pemain di peta: token kuning yang berdiri di node sekarang — atau di ruang
        /// tunggu bawah peta sebelum langkah pertama act — dan BERJALAN menyusuri jalur begitu
        /// node berikutnya dipilih. Label KAMU menempel di atasnya.
        /// </summary>
        void DrawMapMarker(RunMap map)
        {
            if (_mapMark == null) return;

            bool travelling = _mapTravelTo >= 0;

            var panel = MapView();
            Vector2 entry = MapEntryPos(panel);
            Vector2 at;

            if (travelling)
            {
                Vector2 to = MapNodePos(map.Nodes[_mapTravelTo], panel, map.Floors, map.Lanes);
                float t = Mathf.SmoothStep(0f, 1f, _mapTravelT);

                if (map.At >= 0)
                {
                    Vector2 from = MapNodePos(map.Nodes[map.At], panel, map.Floors, map.Lanes);

                    // Bezier yang sama dengan jalur yang tergambar — seed-nya pun sama.
                    TrailControls(from, to, map.At * 31 + _mapTravelTo * 7,
                        out Vector2 p1, out Vector2 p2);

                    float u = 1f - t;
                    at = u * u * u * from + 3f * u * u * t * p1
                         + 3f * u * t * t * p2 + t * t * t * to;
                }
                else
                {
                    // Awal act: mendaki dari ruang tunggu, menyusuri jalur emas yang SAMA
                    // dengan yang tergambar — seed-nya seed jalur itu juga.
                    TrailControls(entry, to, EntrySeed(_mapTravelTo),
                        out Vector2 p1, out Vector2 p2);

                    float u = 1f - t;
                    at = u * u * u * entry + 3f * u * u * t * p1
                         + 3f * u * t * t * p2 + t * t * t * to;
                }

                // Peta ikut menjemput: kalau tujuannya di luar jendela, gulung secukupnya
                // supaya penanda tidak pernah berjalan keluar layar.
                float roomTop = panel.yMax - 150f;
                if (to.y > roomTop)
                    _mapScroll = Mathf.Clamp(_mapScroll + (to.y - roomTop) * 0.12f,
                        0f, MapScrollMax(map, panel));
            }
            else
            {
                // Diam: di node yang dipijak, atau di ruang tunggu kalau belum melangkah.
                at = map.At >= 0
                    ? MapNodePos(map.Nodes[map.At], panel, map.Floors, map.Lanes)
                    : entry;
            }

            bool visible = at.y > panel.yMin + 30f && at.y < panel.yMax - 44f;
            _mapMark.enabled = visible;
            _mapMark.rectTransform.anchoredPosition = at;

            if (_mapYou != null)
            {
                _mapYou.enabled = visible;
                _mapYou.rectTransform.anchoredPosition = at + new Vector2(0f, 34f);
            }
        }

        /// <summary>Posisi layar satu node — BAWAH ke ATAS ala Slay the Spire (lajur jadi kolom,
        /// lantai menumpuk dengan jarak tetap), plus jitter ber-seed supaya berhenti terbaca
        /// sebagai kisi. Seed membuatnya diam: peta yang bergoyang tiap frame bukan peta.
        /// Sudah dikurangi <see cref="_mapScroll"/> — semua pemakainya otomatis ikut scroll.</summary>
        Vector2 MapNodePos(RunNode n, Rect panel, int floors, int lanes)
        {
            // Lebar pita node diambil dari LEBAR LAYAR, bukan dari angka piksel mati: pita
            // sempit di layar lebar membuat seluruh act menggumpal di tengah dan dua pertiga
            // monitor kosong. Batas atasnya menjaga layar ultra-lebar tetap terbaca sebagai
            // jalur, bukan sebagai titik-titik yang berjauhan.
            float half = Mathf.Min(panel.width * 0.26f, 560f);
            float left = panel.center.x - half;
            float right = panel.center.x + half;

            // Ruang kosong di bawah lantai pertama itu disengaja: karakter pemain berdiri di
            // sana sebelum langkah pertamanya — dan jaraknya SATU lantai lebih, supaya langkah
            // pembuka terbaca sebagai perjalanan, bukan lompatan sebelah kaki. Angkanya juga
            // yang menjauhkan seluruh peta dari tepi bawah layar.
            float bottom = panel.yMin + 310f;

            float colW = (right - left) / Mathf.Max(1, lanes - 1);

            // BOSS dikunci MATI di tengah, tanpa geser dan tanpa jitter — dia tujuan seluruh
            // act, dan tujuan yang mencong terbaca sebagai kesalahan layout, bukan aksen.
            if (n.Floor == floors - 1)
                return new Vector2(panel.center.x,
                    bottom + n.Floor * MapFloorGap - _mapScroll);

            // Dua lapis ketidakrapian, dua alasan: geser PER LANTAI membuat garis antar lantai
            // selalu miring (kolom yang lurus vertikal terbaca sebagai tabel, bukan jalan),
            // jitter PER NODE memecah sisa keteraturannya. Dua-duanya ber-seed — peta diam.
            //
            // Keduanya diukur dari JARAK ANTAR LAJUR, bukan piksel tetap: dengan pita yang
            // ikut lebar layar, geser ±40 px yang dulu terasa miring berubah jadi nyaris lurus.
            uint fh = (uint)((n.Floor + 1) * 2246822519u);
            float floorShift = ((fh & 0xFF) / 255f - 0.5f) * colW * 0.5f;

            uint h = (uint)((n.Index + 1) * 2654435761u);
            float jx = ((h & 0xFF) / 255f - 0.5f) * colW * 0.35f;
            float jy = (((h >> 8) & 0xFF) / 255f - 0.5f) * 38f;

            // Geser + jitter bisa melempar lajur terluar melewati tepi layar; dijepit di sini
            // supaya node paling pinggir tidak pernah terpotong, resolusi berapa pun.
            float x = Mathf.Clamp(left + n.Lane * colW + floorShift + jx,
                panel.xMin + 44f, panel.xMax - 44f);

            return new Vector2(x, bottom + n.Floor * MapFloorGap + jy - _mapScroll);
        }

        /// <summary>Ruang tunggu pemain di bawah lantai pertama — ikut tergulung bersama peta.</summary>
        Vector2 MapEntryPos(Rect panel) =>
            new Vector2(panel.center.x, panel.yMin + 165f - _mapScroll);

        /// <summary>Seed jalur dari ruang tunggu ke node lantai pertama — stabil per node.</summary>
        static int EntrySeed(int nodeIndex) => 1000003 + nodeIndex * 13;


        /// <summary>Batas atas scroll: sisa tinggi act yang tidak muat di panel.
        /// Konstantanya = ruang tunggu bawah (310) + kepala boss (70) + jendela atas (60).</summary>
        float MapScrollMax(RunMap map, Rect panel) =>
            Mathf.Max(0f, (map.Floors - 1) * MapFloorGap - (panel.height - 440f));

        /// <summary>Jendela vertikal tempat node boleh tergambar — di luarnya disembunyikan,
        /// supaya peta yang di-scroll tidak menimpa judul dan legenda.</summary>
        bool MapInView(Vector2 at, Rect panel) =>
            at.y > panel.yMin + 46f && at.y < panel.yMax - 60f;

        void LayoutMap(RunMap map, List<RunNode> reachable)
        {
            var panel = MapView();

            _mapBg.rectTransform.sizeDelta = panel.size;
            _mapBg.rectTransform.anchoredPosition = panel.center;

            LayoutMapGloom(panel);

            _mapTitle.rectTransform.anchoredPosition = new Vector2(panel.center.x, panel.yMax - 30f);
            _mapTitle.text = _mapChoose
                ? "ACT " + _run.Act + "  -  PILIH TUJUANMU"
                : "PETA RUN  -  ACT " + _run.Act;

            _mapLegend.rectTransform.anchoredPosition = new Vector2(panel.center.x, panel.yMin + 52f);
            _mapLegend.text = _mapChoose
                ? "klik node yang BERDENYUT   -   tarik / scroll buat geser peta\n" +
                  "W wave    E elite    T toko    ? kejadian    S slot    B boss"
                : "W wave    E elite    T toko    ? kejadian    S slot    B boss\n" +
                  "tarik / scroll buat geser peta        M = tutup";

            int seg = 0;

            foreach (var n in map.Nodes)
            {
                Vector2 a = MapNodePos(n, panel, map.Floors, map.Lanes);

                foreach (int nextIndex in n.Next)
                {
                    Vector2 b = MapNodePos(map.Nodes[nextIndex], panel, map.Floors, map.Lanes);

                    bool walked = _run.Trail.Contains(n.Index) && _run.Trail.Contains(nextIndex);
                    bool offered = map.At == n.Index && reachable.Contains(map.Nodes[nextIndex]);

                    Color tone = walked ? new Color(0.55f, 0.85f, 0.55f, 0.85f)
                        : offered ? new Color(1f, 0.85f, 0.3f, 0.95f)
                        : _theme != null ? _theme.MapPathInk
                        : new Color(0.55f, 0.58f, 0.66f, 0.38f);

                    seg = DrawTrail(a, b, n.Index * 31 + nextIndex * 7, tone, seg);
                }
            }

            // Pemain adalah simpul pertama peta: sebelum langkah pertama act, jalur EMAS
            // terbentang dari ruang tunggunya ke SEMUA pilihan lantai pertama — persis
            // coretan pemilik project di screenshot-nya.
            if (map.At < 0)
            {
                Vector2 entry = MapEntryPos(panel);

                foreach (var n in map.Nodes)
                {
                    if (n.Floor != 0) continue;

                    seg = DrawTrail(entry, MapNodePos(n, panel, map.Floors, map.Lanes),
                        EntrySeed(n.Index), new Color(1f, 0.85f, 0.3f, 0.95f), seg);
                }
            }

            for (; seg < _mapEdges.Length; seg++) _mapEdges[seg].enabled = false;

            for (int i = 0; i < _mapNodes.Length; i++)
            {
                bool live = i < map.Nodes.Count;
                if (_mapNodes[i] == null) continue;

                RunNode n = live ? map.Nodes[i] : null;
                Vector2 pos = live ? MapNodePos(n, panel, map.Floors, map.Lanes) : Vector2.zero;

                // Yang tergulung keluar jendela disembunyikan — bukan digambar menimpa judul.
                bool show = live && MapInView(pos, panel);

                _mapNodes[i].enabled = show;
                _mapRings[i].enabled = show;
                _mapGlyphs[i].enabled = show;
                if (!show) continue;

                bool now = map.At == n.Index;
                bool next = reachable.Contains(n);
                bool walked = _run.Trail.Contains(n.Index);

                // Jitter ukuran & rotasi ala peta referensi — ber-seed, jadi diam di tempat.
                uint h = (uint)((n.Index + 1) * 1274126177u);
                float wobble = ((h & 0xFF) / 255f - 0.5f) * 0.24f;
                float twist = (((h >> 8) & 0xFF) / 255f - 0.5f) * 14f;

                // Ukuran membawa DUA hal sekaligus, dan urutannya penting.
                //
                // Yang pertama status: yang sedang diinjak paling besar, yang bisa dituju sedang,
                // sisanya kecil. Itu menjawab "aku boleh ke mana".
                //
                // Yang kedua JENIS, dan ini yang baru. Peta yang semua nodenya sebesar satu sama
                // lain menuntut membaca hurufnya satu per satu untuk tahu ada apa di depan.
                // Ukuran menjawab itu dari jarak pandang: yang besar berarti besar taruhannya.
                float size = (now ? 46f : next ? 38f : 32f) * KindScale(n.Kind);

                // Jitter ber-seed, jadi diam di tempat. Boss dikecualikan: ia satu-satunya node
                // yang ukurannya BERARTI SESUATU secara mutlak, dan boss yang kebetulan diundi
                // kecil akan berhenti terbaca sebagai puncak act.
                if (n.Kind != RunNodeKind.Boss) size *= 1f + wobble;

                var tone = RunDirector.KindColor(n.Kind);
                Color ring;

                if (now) ring = new Color(1f, 0.85f, 0.4f, 1f);
                // Putih lenyap di perkamen — cincin "berikutnya" harus jadi tinta, bukan sorotan.
                else if (next) ring = _theme != null ? _theme.MapRingInk : Color.white;
                else if (walked)
                {
                    ring = new Color(0.55f, 0.85f, 0.55f, 0.7f);
                    tone.a = 0.85f;
                }
                else
                {
                    // Terkunci: DIPUCATKAN, bukan sekadar dipudarkan — warna jenisnya masih
                    // terbaca (buat merencanakan jalur), tapi jelas belum bisa diinjak.
                    tone = Color.Lerp(tone, new Color(0.3f, 0.32f, 0.38f), 0.55f);
                    tone.a = 0.55f;
                    ring = new Color(0f, 0f, 0f, 0.35f);
                }

                _mapRings[i].rectTransform.anchoredPosition = pos;
                _mapRings[i].rectTransform.sizeDelta = new Vector2(size + 8f, size + 8f);
                _mapRings[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, twist);
                _mapRings[i].color = ring;

                _mapNodes[i].rectTransform.anchoredPosition = pos;
                _mapNodes[i].rectTransform.sizeDelta = new Vector2(size, size);
                _mapNodes[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, twist);
                _mapNodes[i].color = tone;

                _mapGlyphs[i].rectTransform.anchoredPosition = pos;
                _mapGlyphs[i].text = RunDirector.KindLabel(n.Kind).Substring(0, 1);
                _mapGlyphs[i].color = new Color(0f, 0f, 0f, 0.85f);

                // Hurufnya ikut membesar bersama kotaknya. Ukuran tetap membuat huruf di node
                // boss yang dua kali lipat terlihat seperti tersasar di tengah kotak kosong —
                // dan justru node itu yang paling perlu terbaca.
                _mapGlyphs[i].fontSize = Mathf.Max(10, Mathf.RoundToInt(size * 0.46f));
                _mapGlyphs[i].rectTransform.sizeDelta = new Vector2(size, size);

                if (now && _mapYou != null)
                {
                    _mapYou.rectTransform.anchoredPosition =
                        pos + new Vector2(0f, size * 0.5f + 16f);
                }
            }
        }

        /// <summary>
        /// Pengali ukuran node menurut jenisnya.
        ///
        /// Boss dua kali lipat pertarungan biasa, bukan sekadar sedikit lebih besar. Ia tujuan
        /// seluruh act, dan mata harus menemukannya dalam sedetik pertama peta terbuka — tanpa
        /// menyusuri lantai demi lantai mencari huruf B.
        ///
        /// Sisanya menurun sesuai seberapa besar taruhannya: elite itu pertarungan yang bisa
        /// membunuh, toko dan slot menghabiskan koin, kejadian cuma menawarkan pilihan. Yang
        /// paling banyak jumlahnya — pertarungan biasa — sengaja jadi yang paling kecil, karena
        /// node yang muncul di mana-mana tidak perlu meminta perhatian.
        /// </summary>
        static float KindScale(RunNodeKind kind)
        {
            switch (kind)
            {
                case RunNodeKind.Boss: return 2.05f;
                case RunNodeKind.Elite: return 1.42f;
                case RunNodeKind.Shop: return 1.22f;
                case RunNodeKind.Gamble: return 1.16f;
                case RunNodeKind.Event: return 1.1f;
                default: return 1f;
            }
        }

        /// <summary>
        /// Satu sambungan sebagai bezier kubik yang di-sample jadi segmen pendek BERCELAH —
        /// jalan setapak, bukan kabel. Control point diacak ber-seed dari indeks node, jadi
        /// kelokannya sama setiap kali peta dibuka.
        /// </summary>
        /// <summary>
        /// Control point bezier satu sambungan. Dipisah dari DrawTrail supaya penanda yang
        /// berjalan bisa menyusuri kurva yang PERSIS sama dengan jalur yang tergambar.
        /// </summary>
        static void TrailControls(Vector2 from, Vector2 to, int seed,
            out Vector2 p1, out Vector2 p2)
        {
            Vector2 span = to - from;
            float length = span.magnitude;
            Vector2 perp = length > 0.001f ? new Vector2(-span.y, span.x) / length : Vector2.zero;

            // NYARIS lurus, satu arah lengkung per garis. Amplitudo besar dengan dua kontrol
            // acak terpisah menghasilkan huruf S yang meliuk beda-beda tiap ruas — "terlalu
            // belok-belok dan tidak seirama" kata pemilik project, dan peta STS rujukannya
            // memang berjalur hampir lurus. Lengkung tipis cukup untuk terasa digambar tangan.
            var rng = new System.Random(seed);
            float bend = (float)(rng.NextDouble() * 2.0 - 1.0) * length * 0.06f;
            p1 = from + span * 0.33f + perp * bend;
            p2 = from + span * 0.67f + perp * (bend * 0.6f);
        }

        int DrawTrail(Vector2 from, Vector2 to, int seed, Color tone, int seg)
        {
            Vector2 span = to - from;
            float length = span.magnitude;
            if (length < 1f) return seg;

            TrailControls(from, to, seed, out Vector2 p1, out Vector2 p2);

            // Lengkungnya dicuplik jadi garis patah dulu, lalu DIUKUR. Membagi rata parameter t
            // bukan hal yang sama dengan membagi rata jarak: bezier bergerak lebih cepat di
            // tengah, jadi t yang berjarak sama menghasilkan potongan yang panjangnya tidak sama
            // bahkan di dalam satu ruas.
            for (int i = 0; i <= MapArcSamples; i++)
            {
                float t = i / (float)MapArcSamples;
                float u = 1f - t;

                _arcPoints[i] = u * u * u * from + 3f * u * u * t * p1 + 3f * u * t * t * p2
                                + t * t * t * to;

                _arcLengths[i] = i == 0
                    ? 0f
                    : _arcLengths[i - 1] + Vector2.Distance(_arcPoints[i - 1], _arcPoints[i]);
            }

            float curve = _arcLengths[MapArcSamples];
            if (curve < 1f) return seg;

            // Jumlah garis dari panjangnya, bukan angka tetap. Minimal dua: satu garis panjang
            // untuk ruas pendek terbaca sebagai sambungan utuh, bukan sebagai putus-putus.
            int count = Mathf.Clamp(Mathf.RoundToInt(curve / MapDashPitch), 2, MapSegsPerEdge);

            // Jarak antar garis diratakan sepanjang ruas ini supaya yang terakhir berakhir pas di
            // nodenya. Panjang GARISNYA tetap — itu yang dilihat mata — kecuali kalau jaraknya
            // sendiri lebih rapat dari panjang garisnya, dan di situ garisnya yang mengalah.
            float pitch = curve / count;
            float dash = Mathf.Min(MapDashLength, pitch * 0.72f);

            var view = MapView();

            for (int i = 0; i < count && seg < _mapEdges.Length; i++)
            {
                float centreS = (i + 0.5f) * pitch;

                Vector2 centre = PointAtArc(centreS, out Vector2 dir);

                // Segmen yang tergulung keluar jendela ikut disembunyikan seperti nodenya.
                if (!MapInView(centre, view))
                {
                    _mapEdges[seg++].enabled = false;
                    continue;
                }

                var img = _mapEdges[seg++];
                img.enabled = true;
                img.color = tone;
                img.rectTransform.anchoredPosition = centre;
                img.rectTransform.sizeDelta = new Vector2(dash, 5f);
                img.rectTransform.localEulerAngles =
                    new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }

            return seg;
        }

        /// <summary>
        /// Titik pada lengkung yang sudah dicuplik, sejauh <paramref name="s"/> diukur SEPANJANG
        /// lengkungnya — bukan sepanjang garis lurus antar ujungnya.
        /// </summary>
        Vector2 PointAtArc(float s, out Vector2 direction)
        {
            for (int i = 1; i <= MapArcSamples; i++)
            {
                if (_arcLengths[i] < s) continue;

                float span = _arcLengths[i] - _arcLengths[i - 1];
                float f = span > 0.0001f ? (s - _arcLengths[i - 1]) / span : 0f;

                direction = _arcPoints[i] - _arcPoints[i - 1];
                return Vector2.LerpUnclamped(_arcPoints[i - 1], _arcPoints[i], f);
            }

            direction = _arcPoints[MapArcSamples] - _arcPoints[MapArcSamples - 1];
            return _arcPoints[MapArcSamples];
        }

        static readonly string[] SlotFaces = { "X", "o", "O", "*2", "*3", "*4" };

        void DrawGamble(float dt)
        {
            bool open = _gambleOpen && _run != null;
            if (_slotBg == null) return;

            _slotBg.enabled = open;
            _slotTitle.enabled = open;
            _slotInfo.enabled = open;
            _slotSpinBg.enabled = open;
            _slotSpinLabel.enabled = open;
            for (int i = 0; i < 3; i++) _slotReels[i].enabled = open;

            if (!open) return;

            var panel = PanelRect();
            _slotBg.rectTransform.anchoredPosition = panel.center;
            _slotTitle.rectTransform.anchoredPosition = new Vector2(panel.center.x, panel.yMax - 28f);
            _slotTitle.text = "MESIN SLOT — " + _balance.GambleCost + " KOIN SEKALI PUTAR";

            for (int i = 0; i < 3; i++)
            {
                _slotReels[i].rectTransform.anchoredPosition =
                    new Vector2(panel.center.x + (i - 1) * 130f, panel.center.y + 24f);
            }

            var spin = RerollRect();
            _slotSpinBg.rectTransform.anchoredPosition = spin.center;
            _slotSpinBg.rectTransform.sizeDelta = spin.size;
            _slotSpinLabel.rectTransform.anchoredPosition = spin.center;
            _slotSpinLabel.text = _spinLeft > 0f ? ". . ." : "PUTAR!";

            _slotInfo.rectTransform.anchoredPosition = new Vector2(panel.center.x, panel.yMin + 58f);
            _slotInfo.text = _slotResultLine + "\nkoin: " + _gold;

            if (_spinLeft > 0f)
            {
                _spinLeft -= dt;

                // Gulungan mengocok wajah dari WAKTU, bukan dari Random gameplay — kocokan visual
                // enam puluh kali sedetik menggeser seluruh sebaran drop tanpa satu pun error.
                for (int i = 0; i < 3; i++)
                {
                    int face = (int)(Time.unscaledTime * 21f + i * 7.3f) % SlotFaces.Length;
                    _slotReels[i].text = SlotFaces[face];
                }

                if (_spinLeft <= 0f) SettleGamble();
            }
        }

        int RollGambleOutcome()
        {
            var weights = _balance.GambleWeights;
            if (weights == null || weights.Length == 0) return 0;

            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += Mathf.Max(0f, weights[i]);
            if (total <= 0f) return 0;

            float roll = Random.value * total;

            for (int i = 0; i < weights.Length; i++)
            {
                roll -= Mathf.Max(0f, weights[i]);
                if (roll <= 0f) return i;
            }

            return 0;
        }

        void SettleGamble()
        {
            _spinLeft = 0f;

            int outcome = _slotOutcome;
            _slotOutcome = -1;
            if (outcome < 0) return;

            bool win = outcome != 0;

            for (int i = 0; i < 3; i++)
            {
                _slotReels[i].text = win ? SlotFaces[outcome] : SlotFaces[(i * 2 + 1) % 3];
            }

            switch (outcome)
            {
                case 1:
                    _gold += _balance.GambleSmallGold;
                    _slotResultLine = "+" + _balance.GambleSmallGold + " koin";
                    break;

                case 2:
                    _gold += _balance.GambleBigGold;
                    _slotResultLine = "+" + _balance.GambleBigGold + " KOIN!";
                    break;

                case 3:
                case 4:
                case 5:
                    var prize = _db.RandomOfStar(outcome - 1, 0.25f);

                    if (prize != null)
                    {
                        AddLoose(prize, NearScatterPos(PanelRect().center, 1));
                        Discover(prize);
                        _slotResultLine = "JACKPOT: " + prize.DisplayName + "!";
                    }

                    break;

                default:
                    _slotResultLine = "zonk.";
                    break;
            }

            Announce(_slotResultLine,
                win ? new Color(1f, 0.84f, 0.32f) : new Color(0.7f, 0.7f, 0.75f));
        }

        /// <summary>
        /// Mengambil pakta yang ditawarkan. Berkah dan kutuknya masuk BERSAMAAN — tidak ada jalan
        /// mengambil separuhnya, dan itu seluruh isi mekanik ini.
        /// </summary>
        void TakePact(int slot)
        {
            var pact = slot >= 0 && slot < _pactOffer.Length ? _pactOffer[slot] : null;

            // Katalog habis (semua sudah dipegang) — kartunya menampilkan tawaran koin, jadi
            // kliknya harus membayar koin itu, bukan diam saja.
            if (pact == null)
            {
                RefusePact();
                return;
            }

            if (Player.Pacts == null || !Player.Pacts.Take(pact)) return;

            Announce(pact.DisplayName, pact.Color);

            _pactOffer[0] = null;
            _pactOffer[1] = null;
            _eventDone = true;
            _eventOpen = false;
        }

        /// <summary>Menolak keduanya. Bayarannya koin — kecil, karena melewatkan harus terasa.</summary>
        void RefusePact()
        {
            _gold += _balance.EventGoldGift;
            Announce("+" + _balance.EventGoldGift + " KOIN", new Color(1f, 0.84f, 0.32f));

            _pactOffer[0] = null;
            _pactOffer[1] = null;
            _eventDone = true;
            _eventOpen = false;
        }

        void DrawEvent()
        {
            bool open = _eventOpen && _run != null;
            if (_eventBg == null) return;

            _eventBg.enabled = open;
            _eventTitle.enabled = open;
            _eventBody.enabled = open;
            _eventABg.enabled = open;
            _eventBBg.enabled = open;
            _eventALabel.enabled = open;
            _eventBLabel.enabled = open;
            _eventCBg.enabled = open;
            _eventCLabel.enabled = open;

            if (!open) return;

            var panel = PanelRect();
            _eventBg.rectTransform.anchoredPosition = panel.center;

            _eventTitle.rectTransform.anchoredPosition = new Vector2(panel.center.x, panel.yMax - 28f);
            _eventTitle.text = "PERTAPA HUTAN";

            _eventBody.rectTransform.anchoredPosition = new Vector2(panel.center.x, panel.yMax - 92f);
            _eventBody.text = "\"Aku tidak menjual berkah, penyihir.\n" +
                              "Aku menukarnya. Satu untuk satu, dan keduanya seumur hidupmu.\"";

            PaintPactCard(0, EventOptionRect(0), _eventABg, _eventALabel);
            PaintPactCard(1, EventOptionRect(1), _eventBBg, _eventBLabel);

            var c = EventRefuseRect();
            _eventCBg.rectTransform.anchoredPosition = c.center;
            _eventCBg.rectTransform.sizeDelta = c.size;
            _eventCLabel.rectTransform.anchoredPosition = c.center;
            _eventCLabel.text = "PERGI SAJA   (+" + _balance.EventGoldGift + " koin)";
        }

        /// <summary>
        /// Satu kartu pakta: nama, sisi untung, sisi rugi.
        ///
        /// Warnanya diambil dari paktanya sendiri dan DIGELAPKAN, bukan dipakai apa adanya — warna
        /// pakta dipilih supaya terbaca sebagai ikon 26 piksel di atas latar gelap, dan bidang
        /// seluas 292x132 dengan warna yang sama menelan tulisan putih di atasnya.
        /// </summary>
        void PaintPactCard(int slot, Rect area, Image bg, Text label)
        {
            bg.rectTransform.anchoredPosition = area.center;
            bg.rectTransform.sizeDelta = area.size;
            label.rectTransform.anchoredPosition = area.center;

            var pact = slot < _pactOffer.Length ? _pactOffer[slot] : null;

            // Katalog kehabisan pakta yang belum dipegang. Kartunya tidak dikosongkan — kartu kosong
            // terbaca sebagai panel yang rusak. Ia jatuh kembali ke tawaran koin lama, dan kliknya
            // memang membayar koin itu.
            if (pact == null)
            {
                bg.color = new Color(0.25f, 0.22f, 0.27f, 0.9f);
                label.text = "TIDAK ADA LAGI YANG\nBISA DITAWARKAN\n\n+" +
                             _balance.EventGoldGift + " koin";
                return;
            }

            var tone = pact.Color;
            bg.color = new Color(tone.r * 0.32f, tone.g * 0.32f, tone.b * 0.32f, 0.96f);

            _sb.Length = 0;
            _sb.Append(pact.DisplayName).Append("\n\n");
            _sb.Append("+  ").Append(pact.BoonText).Append("\n\n");
            _sb.Append("-  ").Append(pact.BaneText);

            label.text = _sb.ToString();
        }

        void Redraw()
        {
            DrawGrid();
            DrawEvoLines();
            DrawSkillWidgets(Time.deltaTime);
            DrawBackpack();
            DrawLoose();
            DrawSpells();
            DrawHud();
            DrawBossBar();
            DrawMeter();
            DrawBuffs();
            DrawRunPanels(Time.deltaTime);
            UpdateTooltip();

            DrawHurtVeil();

            // Paling akhir: kerudungnya harus menutupi SEMUA yang digambar di atas, termasuk
            // kartu hover dan panel yang kebetulan masih terbuka saat pemain mati.
            DrawGameOver();
        }

        void DrawGrid()
        {
            var emptyColor = _held != null ? CellShown : CellIdle;

            for (int y = 0; y < Grimoire.Height; y++)
            {
                for (int x = 0; x < Grimoire.Width; x++)
                {
                    int i = y * Grimoire.Width + x;
                    var cell = new Vector2Int(x, y);

                    var baseRune = Book.BaseAt(cell);

                    // Dinyalakan ulang tiap gambar: lapisan tile di bawah ini MEMATIKAN petak
                    // yang ditutupnya, dan yang tidak pernah dinyalakan lagi akan tetap padam
                    // setelah runenya diangkat.
                    _baseCells[i].enabled = true;
                    _baseCells[i].color = baseRune != null ? Tint(baseRune) : emptyColor;

                    // Ornamennya dibawa TILE yang digambar di atas petak ini — lihat
                    // DrawRuneTiles. Petaknya sendiri kembali jadi kotak warna polos.
                    _baseCells[i].sprite = null;

                    var skill = Book.SkillAt(cell);
                    _skillCells[i].enabled = skill != null;
                    if (skill != null) _skillCells[i].color = Tint(skill);
                }
            }

            DrawRuneTiles();

            UpdateHeldText();

            if (_held == null) return;

            var hover = ScreenToCell(ProtoInput.MousePosition);
            if (hover.x < 0) return;

            var origin = hover - AnchorOffset(_held, _heldRot);
            bool valid = Book.CanPlace(_held, origin, _heldRot);
            var shape = Shapes.Rotate(_held.Cells, _heldRot);
            var tint = valid ? ValidCell : InvalidCell;

            for (int i = 0; i < shape.Length; i++)
            {
                var c = origin + shape[i];
                if (!Grimoire.InBounds(c)) continue;

                int idx = c.y * Grimoire.Width + c.x;
                if (_held.Layer == Layer.Rune)
                {
                    // Dinyalakan lagi: petak yang sedang jadi sasaran harus terbaca boleh atau
                    // tidak, dan itu berlaku juga di atas rune yang sudah duduk di situ - justru
                    // di situlah jawabannya "tidak boleh".
                    _baseCells[idx].enabled = true;
                    _baseCells[idx].color = tint;
                }
                else
                {
                    _skillCells[idx].enabled = true;
                    _skillCells[idx].color = tint;
                }
            }
        }

        /// <summary>Locked pieces are washed out so you can see at a glance what evolution skips.</summary>
        static Color Tint(RuneInstance inst)
        {
            return inst.Locked ? Color.Lerp(inst.Def.Color, Color.white, 0.55f) : inst.Def.Color;
        }

        /// <summary>
        /// Tile rune di atas petak papan: <b>SATU tile utuh per PETAK yang diduduki</b>, disusun
        /// mengikuti bentuk piece-nya. Rune salib jadi lima tile berbentuk salib, rune tiga petak
        /// jadi tiga tile berjajar.
        ///
        /// Yang dipakai sebelumnya adalah satu glyph besar dibentangkan di kotak pembatas
        /// footprint-nya, dan itu berbohong soal bentuk: salib dan blok 3x3 punya kotak pembatas
        /// yang sama persis, jadi dua bentuk yang paling berbeda di seluruh permainan tampil
        /// identik. Petak yang digambar satu per satu tidak pernah bisa berbohong.
        ///
        /// Hanya piece yang ikonnya dari sheet rune (nama "Rune_S...") yang ditile-kan; kotak
        /// warna tetap bahasa dasar papan, tile cuma identitas di atasnya.
        /// </summary>
        void DrawRuneTiles()
        {
            DrawTileLayer(ref _boardTiles, _baseCells, Grimoire.Width, Grimoire.Height,
                c => Book.BaseAt(c));
        }

        void DrawBagTiles()
        {
            DrawTileLayer(ref _bagTiles, _bagCells, Backpack.Width, Backpack.Height,
                c => _bag.At(c));
        }

        /// <summary>
        /// Lapisan tile generik untuk grid petak mana pun (papan &amp; tas). Letak tiap tile
        /// diambil dari petak yang bersangkutan, bukan dihitung ulang dari rumus: begitu papan
        /// digeser atau petaknya diperbesar lewat prefab, tile-nya ikut tanpa diberi tahu.
        /// </summary>
        void DrawTileLayer(ref RuneTilePool pool, Image[] cells, int width, int height,
            System.Func<Vector2Int, RuneInstance> at)
        {
            if (cells == null || cells.Length == 0 || cells[0] == null) return;

            if (pool == null)
            {
                pool = new RuneTilePool(cells[0].transform.parent,
                    cells[cells.Length - 1].transform);
            }

            pool.Begin();
            _tiledPieces.Clear();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var inst = at(new Vector2Int(x, y));
                    if (inst == null || !RuneTiles.IsRuneGlyph(inst.Def.Icon)) continue;

                    // Sekali per PIECE, bukan sekali per petak: piece yang sama akan ditemui lagi
                    // begitu pemindaian sampai ke petaknya yang berikutnya, dan menggambarnya
                    // ulang berarti satu piece 9 petak menghabiskan 81 tile.
                    if (!_tiledPieces.Add(inst)) continue;

                    // Urutan petak diambil dari piece-nya sendiri, bukan dari urutan pemindaian:
                    // glyph petak ke-k ditentukan oleh k, jadi urutan yang salah menukar gambar
                    // antar petak tiap kali piece-nya diputar.
                    var shape = Shapes.Rotate(inst.Def.Cells, inst.Rot);

                    // Piece terkunci dibuat pudar, bukan diwarnai ulang: yang harus terbaca
                    // adalah "evolusi melewati yang ini", dan pudar mengatakan itu tanpa
                    // menghapus warna yang jadi identitasnya.
                    float alpha = inst.Locked ? 0.5f : 1f;

                    for (int k = 0; k < shape.Length; k++)
                    {
                        var c = inst.Origin + shape[k];
                        if (c.x < 0 || c.y < 0 || c.x >= width || c.y >= height) continue;

                        var under = cells[c.y * width + c.x];

                        // Kotak warna di bawahnya DIMATIKAN, bukan sekadar diwarnai netral.
                        // Sprite petaknya punya tepi transparan, jadi apa pun yang tersisa di
                        // belakang menyembul sebagai pita berwarna mengelilingi runenya — dan
                        // pita itu persis "objek primitif" yang diminta hilang.
                        //
                        // Dimatikan DI SINI, oleh yang benar-benar menggambar tile-nya, bukan
                        // lewat syarat kembar di tempat lain: dua syarat yang mengira dirinya
                        // menjawab pertanyaan yang sama akan berbeda pendapat suatu hari, dan
                        // hari itu tidak akan ada yang tahu mana yang salah.
                        under.enabled = false;

                        var tile = pool.Take();
                        tile.Cover(under.rectTransform);
                        tile.Bind(RuneTiles.BakedTileAt(inst.Def, k), RuneTiles.GlyphAt(inst.Def, k),
                            inst.Def.Color, alpha);
                    }
                }
            }

            pool.End();
        }

        void DrawBackpack()
        {
            var emptyColor = _held != null ? ShownBagCell : HiddenBagCell;

            for (int y = 0; y < Backpack.Height; y++)
            {
                for (int x = 0; x < Backpack.Width; x++)
                {
                    int i = y * Backpack.Width + x;
                    var stored = _bag.At(new Vector2Int(x, y));
                    _bagCells[i].color = stored != null ? stored.Def.Color : emptyColor;
                }
            }

            DrawBagTiles();

            if (_held == null) return;

            var hover = ScreenToBagCell(ProtoInput.MousePosition);
            if (hover.x < 0) return;

            var origin = hover - AnchorOffset(_held, _heldRot);
            bool valid = _bag.CanPlace(_held, origin, _heldRot);
            var shape = Shapes.Rotate(_held.Cells, _heldRot);
            var tint = valid ? ValidCell : InvalidCell;

            for (int i = 0; i < shape.Length; i++)
            {
                var c = origin + shape[i];
                if (!Backpack.InBounds(c)) continue;
                _bagCells[c.y * Backpack.Width + c.x].color = tint;
            }
        }

        void DrawLoose()
        {
            // Disisipkan tepat sesudah blok petak tercecer, yang sendirinya sudah dinaikkan ke
            // depan panel saat dibangun. Ditaruh paling belakang ia akan menimpa peta dan layar
            // GAME OVER; ditaruh sebelum blok itu, barang belanjaan hilang di balik panel toko.
            if (_looseTiles == null && _looseCells != null && _looseCells.Length > 0)
            {
                _looseTiles = new RuneTilePool(_canvas.transform,
                    _looseCells[_looseCells.Length - 1].transform);
            }

            if (_looseTiles != null) _looseTiles.Begin();

            int cursor = 0;

            for (int i = 0; i < _loose.Count; i++)
            {
                cursor = DrawPiece(_loose[i], 0, _loosePos[i], cursor, 1f);
            }

            cursor = DrawPanels(cursor);

            // The carried piece rides the cursor, but not over the grid or bag â€” those already
            // show the footprint, and drawing both looks like a double image.
            if (_held != null)
            {
                var mouse = ProtoInput.MousePosition;
                bool overGrid = ScreenToCell(mouse).x >= 0;
                bool overBag = ScreenToBagCell(mouse).x >= 0;

                if (!overGrid && !overBag) cursor = DrawPiece(_held, _heldRot, mouse, cursor, 0.9f);
            }

            for (int i = cursor; i < _looseCells.Length; i++) _looseCells[i].enabled = false;

            if (_looseTiles != null) _looseTiles.End();
        }

        /// <summary>
        /// Baris ini SATU-SATUNYA teks mengambang yang tersisa di kiri layar, dan ia hanya bicara
        /// saat ada yang di tangan.
        ///
        /// Dulu ia tidak pernah diam: aturan penempatan, cara memutar, nasib barang tercecer, dan
        /// pengumuman "wave lagi jalan" bergantian mengisinya sepanjang run. Isinya benar semua —
        /// dan justru itu masalahnya. Kalimat yang selalu ada berhenti dibaca setelah run pertama,
        /// tapi tidak pernah berhenti menempati layar. Aturan yang dipelajari sekali tidak perlu
        /// diulang tiap frame; nama barang yang sedang diangkat perlu.
        /// </summary>
        void UpdateHeldText()
        {
            if (_held == null)
            {
                _heldText.text = "";
                return;
            }

            string kind = _held.Layer == Layer.Rune ? "RUNE" : "SKILL";
            _heldText.text = kind + " - " + _held.DisplayName + "   " + _held.Blurb;
        }

        /// <summary>Detailed hover card + the ground ring showing the hovered skill's reach.</summary>
        void UpdateTooltip()
        {
            PieceDefinition hovered = null;
            CompiledSpell spell = null;
            string origin = "";

            // Strip icons win: they sit in the HUD corner, well away from the board, so a hit there
            // is unambiguous — and their whole reason to exist is answering "what is this".
            string strip = StripTooltip(ProtoInput.MousePosition)
                        ?? VitalsTooltip(ProtoInput.MousePosition);

            if (strip != null)
            {
                _recipes.Hide();
                ShowCard(strip);
                Player.HideRange();
                return;
            }

            if (_held != null)
            {
                // Carrying something â€” the card would just sit in the way.
                _tipBg.enabled = false;
                _tipText.enabled = false;
                _recipes.Hide();
                Player.ShowRange(_held.Range, _held.Color);
                return;
            }

            {
                var mouse = ProtoInput.MousePosition;

                int looseIndex = ScreenToLoose(mouse);
                if (looseIndex >= 0)
                {
                    hovered = _loose[looseIndex];
                    origin = "TERCECER";
                }

                if (hovered == null)
                {
                    var bagCell = ScreenToBagCell(mouse);
                    if (bagCell.x >= 0)
                    {
                        var stored = _bag.At(bagCell);
                        if (stored != null)
                        {
                            hovered = stored.Def;
                            origin = "DI TAS (nggak nembak)";
                        }
                    }
                }

                if (hovered == null)
                {
                    var cell = ScreenToCell(mouse);
                    if (cell.x >= 0)
                    {
                        var inst = Book.SkillAt(cell) ?? Book.BaseAt(cell);
                        if (inst != null)
                        {
                            hovered = inst.Def;
                            // Akibat gemboknya ikut ditulis. "TERKUNCI" saja terbaca sebagai
                            // "aman, tidak akan hilang" — padahal artinya resep BUTA terhadap
                            // piece ini, jadi evolusi yang ditunggu tidak akan pernah jalan
                            // selama gemboknya terpasang.
                            origin = inst.Locked
                                ? "KEPASANG - TERKUNCI, nggak ikut evolusi"
                                : "KEPASANG";
                            spell = FindSpell(inst);
                        }
                    }
                }
            }

            if (hovered == null)
            {
                _tipBg.enabled = false;
                _tipText.enabled = false;
                _recipes.Hide();
                Player.HideRange();
                return;
            }

            // ALT swaps the stat card for the recipe card. They occupy the same corner of the
            // screen, so exactly one of them is ever up.
            if (ProtoInput.AltHeld)
            {
                _tipBg.enabled = false;
                _tipText.enabled = false;
                _recipes.Show(hovered, ProtoInput.MousePosition);
                ShowHoverRange(hovered, spell);
                return;
            }

            _recipes.Hide();
            ShowCard(_tooltips.Build(hovered, spell, origin));
            ShowHoverRange(hovered, spell);
        }

        const float TipWidth = 380f;
        const float TipPadX = 14f;
        const float TipPadY = 12f;

        /// <summary>
        /// Menaruh kartu hover di sebelah kursor, dijepit supaya tidak pernah keluar layar.
        ///
        /// Tingginya DIHITUNG dari teksnya, tidak lagi dipatok 150 piksel. Kotak berukuran mati
        /// itu salah di dua arah sekaligus: segel dengan empat baris menyisakan sepertiga kotak
        /// kosong, sementara skill dengan sepuluh baris menulis keluar dari kotaknya sendiri —
        /// dan yang keluar itu justru baris terakhir, tempat blurb-nya berada.
        /// </summary>
        void ShowCard(string body)
        {
            _tipText.text = body;

            var textRect = _tipText.rectTransform;
            float inner = TipWidth - TipPadX * 2f;

            // Lebar dikunci DULU: preferredHeight tanpa lebar yang pasti akan menjawab untuk
            // teks satu baris panjang, bukan untuk teks yang sudah dibungkus.
            textRect.sizeDelta = new Vector2(inner, 0f);
            float height = _tipText.preferredHeight;
            textRect.sizeDelta = new Vector2(inner, height);

            float boxHeight = height + TipPadY * 2f;
            _tipBg.rectTransform.sizeDelta = new Vector2(TipWidth, boxHeight);

            var m = ProtoInput.MousePosition;
            float x = Mathf.Min(m.x + 18f, Screen.width - TipWidth - 8f);

            // Pivotnya kiri-ATAS, jadi y adalah tepi atas kartu: yang harus dijaga tetap di
            // dalam layar adalah DASARNYA, dan dasar itu bergantung tinggi kartunya.
            float y = Mathf.Clamp(m.y - 12f, boxHeight + 8f, Screen.height - 8f);

            _tipBg.rectTransform.anchoredPosition = new Vector2(x, y);
            textRect.anchoredPosition = new Vector2(x + TipPadX, y - TipPadY);
            _tipBg.enabled = true;
            _tipText.enabled = true;
        }

        void ShowHoverRange(PieceDefinition hovered, CompiledSpell spell)
        {
            if (hovered.Layer == Layer.Skill && hovered.Range > 0f)
            {
                Player.ShowRange(spell != null ? spell.Range : hovered.Range, hovered.Color);
            }
            else
            {
                Player.HideRange();
            }
        }

        CompiledSpell FindSpell(RuneInstance inst)
        {
            var spells = Book.Spells;
            for (int i = 0; i < spells.Count; i++)
            {
                if (spells[i].Source == inst) return spells[i];
            }

            return null;
        }

        void OnSpellCast(RuneInstance inst)
        {
            var spells = Book.Spells;
            for (int i = 0; i < spells.Count && i < MaxSpellRows; i++)
            {
                if (spells[i].Source != inst) continue;
                _pulse[i] = 1f;
                return;
            }
        }

        Vector2 SkillCentroid(RuneInstance inst)
        {
            Vector2 sum = Vector2.zero;
            int n = 0;

            foreach (var c in inst.Cells())
            {
                sum += CellAnchor(c.x, c.y) + new Vector2(CellSize * 0.5f, CellSize * 0.5f);
                n++;
            }

            return n == 0 ? sum : sum / n;
        }

        void DrawSkillWidgets(float dt)
        {
            var spells = Book.Spells;

            for (int i = 0; i < MaxSpellRows; i++)
            {
                bool used = i < spells.Count;
                _cdBg[i].enabled = used;
                _cdFill[i].enabled = used;

                if (!used)
                {
                    _pulse[i] = 0f;
                    continue;
                }

                var s = spells[i];
                float progress = s.Cooldown <= 0f ? 1f : 1f - Mathf.Clamp01(s.Source.CdTimer / s.Cooldown);

                _pulse[i] = Mathf.MoveTowards(_pulse[i], 0f, dt * 3.5f);
                float scale = 1f + _pulse[i] * 0.6f;

                var center = SkillCentroid(s.Source);
                var col = s.Source.Def.Color;

                _cdBg[i].rectTransform.anchoredPosition = center;
                _cdBg[i].rectTransform.localScale = Vector3.one * scale;

                _cdFill[i].rectTransform.anchoredPosition = center;
                _cdFill[i].rectTransform.localScale = Vector3.one * scale;
                _cdFill[i].fillAmount = progress;

                bool manaStarved = progress >= 1f && Player.Mana < s.Source.Def.ManaCost;
                if (manaStarved) _cdFill[i].color = new Color(0.35f, 0.55f, 1f, 0.7f);
                else if (progress >= 1f) _cdFill[i].color = new Color(col.r, col.g, col.b, 0.95f);
                else _cdFill[i].color = new Color(col.r * 0.85f, col.g * 0.85f, col.b * 0.85f, 0.55f);
            }
        }

        int DrawPanels(int cursor)
        {
            bool eventOn = ShopEventActive;
            if (!eventOn) _shopOpen = false;

            _shopBtnBg.enabled = eventOn;
            _shopBtnLabel.enabled = eventOn;
            _shopBtnLabel.text = "TOKO BUKA";

            _panelBg.enabled = _shopOpen;
            _panelTitle.enabled = _shopOpen;
            _rerollBg.enabled = _shopOpen;
            _rerollLabel.enabled = _shopOpen;

            _shopBtnBg.color = _shopOpen
                ? new Color(0.35f, 0.55f, 0.8f, 0.95f)
                : new Color(0.25f, 0.4f, 0.6f, 0.95f);

            for (int i = 0; i < ShopSlots; i++)
            {
                _shopSlotBg[i].enabled = _shopOpen;
                _shopSlotText[i].enabled = _shopOpen;
            }

            if (!_shopOpen) return cursor;

            var panel = PanelRect();
            _panelBg.rectTransform.anchoredPosition = panel.center;

            // Ukurannya ikut kotak prefab, bukan cuma posisinya. Selama ini latar panel dikunci di
            // PanelW x PanelH bawaan sementara kotaknya boleh ditata tangan — dua angka yang
            // kebetulan sama, sampai kotaknya digeser sekali saja dan latarnya berhenti menutupi
            // isinya sendiri.
            _panelBg.rectTransform.sizeDelta = panel.size;

            _panelTitle.rectTransform.anchoredPosition = new Vector2(panel.xMin + 14f, panel.yMax - 8f);
            _panelTitle.rectTransform.pivot = new Vector2(0f, 1f);
            _panelTitle.text = "TOKO   —   " + _gold + " koin";

            for (int i = 0; i < ShopSlots; i++)
            {
                var rect = ShopSlotRect(i);
                _shopSlotBg[i].rectTransform.anchoredPosition = new Vector2(rect.xMin, rect.yMin);
                _shopSlotText[i].rectTransform.anchoredPosition = new Vector2(rect.xMin + 5f, rect.yMin + 6f);

                var def = _shop[i];
                if (def == null)
                {
                    _shopSlotBg[i].color = new Color(0.1f, 0.1f, 0.12f, 0.7f);
                    _shopSlotText[i].text = "(kebeli)";
                    _shopSlotText[i].color = new Color(0.5f, 0.5f, 0.55f);
                    continue;
                }

                int price = _balance.PriceOf(def);
                bool afford = _gold >= price;

                _shopSlotBg[i].color = afford
                    ? new Color(0.15f, 0.16f, 0.22f, 0.95f)
                    : new Color(0.16f, 0.11f, 0.11f, 0.95f);

                _sb.Length = 0;
                _sb.Append(def.DisplayName).Append("  ").Append(Shapes.StarText(def.Stars)).Append('\n');
                _sb.Append(price).Append(" koin");
                _shopSlotText[i].text = _sb.ToString();
                _shopSlotText[i].color = afford ? Color.white : new Color(0.95f, 0.55f, 0.5f);

                cursor = DrawPiece(def, 0, new Vector2(rect.center.x, rect.center.y + 18f), cursor, 1f);
            }

            var reroll = RerollRect();
            _rerollBg.rectTransform.anchoredPosition = new Vector2(reroll.xMin, reroll.yMin);
            _rerollBg.color = _gold >= _rerollCost
                ? new Color(0.32f, 0.45f, 0.28f, 0.95f)
                : new Color(0.3f, 0.18f, 0.18f, 0.95f);

            _rerollLabel.rectTransform.anchoredPosition = new Vector2(reroll.xMin, reroll.yMin + 8f);
            _rerollLabel.text = "REROLL   " + _rerollCost + " koin";

            return cursor;
        }

        /// <summary>How many of this piece the player owns anywhere: grid, bag, floor, or in hand.</summary>
        int OwnedCount(PieceDefinition piece)
        {
            if (piece == null) return 0;
            int n = 0;

            for (int i = 0; i < Book.Placed.Count; i++)
            {
                if (Book.Placed[i].Def == piece) n++;
            }

            for (int i = 0; i < _bag.Placed.Count; i++)
            {
                if (_bag.Placed[i].Def == piece) n++;
            }

            for (int i = 0; i < _loose.Count; i++)
            {
                if (_loose[i] == piece) n++;
            }

            if (_held == piece) n++;
            return n;
        }

        /// <summary>
        /// Orders the panel by damage, heaviest first, into <see cref="_spellOrder"/>.
        ///
        /// Insertion sort on purpose: the list is never longer than the number of skills that fit on
        /// a 7x7 board, and this runs every frame — <c>List.Sort</c> would allocate a comparer on a
        /// hot path to save nothing.
        /// </summary>
        int SortSpellsByDamage()
        {
            var spells = Book.Spells;
            int count = Mathf.Min(spells.Count, _spellOrder.Length);

            for (int i = 0; i < count; i++) _spellOrder[i] = i;

            for (int i = 1; i < count; i++)
            {
                int current = _spellOrder[i];
                int k = i - 1;

                while (k >= 0 && Heavier(spells[current], spells[_spellOrder[k]]))
                {
                    _spellOrder[k + 1] = _spellOrder[k];
                    k--;
                }

                _spellOrder[k + 1] = current;
            }

            return count;
        }

        /// <summary>
        /// Ranked by damage actually DEALT this run, not by the number on the card.
        ///
        /// The printed damage is a guess about a skill; the meter is what it did. A slow nuke and a
        /// fast bolt can share a damage figure and contribute wildly differently, and once the two
        /// panels merged there was no reason to keep ranking by the weaker signal.
        ///
        /// Falls back to per-cast damage until a skill has landed anything, so a freshly placed
        /// piece still sorts sensibly instead of dropping to the bottom.
        /// </summary>
        bool Heavier(CompiledSpell a, CompiledSpell b)
        {
            float dealtA = _meter.DealtBy(a.Source.Def.DisplayName);
            float dealtB = _meter.DealtBy(b.Source.Def.DisplayName);

            if (dealtA > 0f || dealtB > 0f)
            {
                if (!Mathf.Approximately(dealtA, dealtB)) return dealtA > dealtB;
            }

            if (!Mathf.Approximately(a.Damage, b.Damage)) return a.Damage > b.Damage;
            return a.Cooldown < b.Cooldown;
        }

        void DrawSpells()
        {
            var spells = Book.Spells;
            int count = SortSpellsByDamage();

            for (int i = 0; i < MaxSpellRows; i++)
            {
                bool used = i < count;
                _spellBg[i].enabled = used;
                _spellFill[i].enabled = used;
                _spellText[i].enabled = used;
                if (!used) continue;

                var s = spells[_spellOrder[i]];
                float progress = s.Cooldown <= 0f ? 1f : 1f - Mathf.Clamp01(s.Source.CdTimer / s.Cooldown);
                _spellFill[i].fillAmount = progress;
                _spellFill[i].color = new Color(s.Source.Def.Color.r, s.Source.Def.Color.g,
                    s.Source.Def.Color.b, 0.35f);

                _sb.Length = 0;
                _sb.Append(i + 1).Append(". ").Append(s.Source.Def.DisplayName);

                // Share of the run's damage, folded in from what used to be a separate meter panel.
                int share = _meter.ShareOf(s.Source.Def.DisplayName);
                if (share > 0) _sb.Append("  ").Append(share).Append('%');

                _sb.Append("   ").Append(BigNumber.Short(s.Damage)).Append(" dmg");
                _sb.Append("   ").Append(BigNumber.Short(s.Cooldown <= 0f ? 0f : s.Damage / s.Cooldown))
                    .Append(" dps");
                _sb.Append("   ").Append(s.Cooldown.ToString("0.00")).Append('s');
                _sb.Append("   ").Append(Mathf.RoundToInt(s.Source.Def.ManaCost)).Append(" mana");

                if (s.DamageBonus > 0f) _sb.Append("  +").Append(Mathf.RoundToInt(s.DamageBonus * 100f)).Append("%D");
                if (s.CooldownBonus > 0f) _sb.Append("  -").Append(Mathf.RoundToInt(s.CooldownBonus * 100f)).Append("%CD");
                if (s.RadiusBonus > 0f) _sb.Append("  +").Append(Mathf.RoundToInt(s.RadiusBonus * 100f)).Append("%A");

                _spellText[i].text = _sb.ToString();
                _spellText[i].color = s.DamageBonus + s.CooldownBonus + s.RadiusBonus > 0f
                    ? new Color(1f, 0.92f, 0.55f)
                    : Color.white;
            }
        }

        /// <summary>
        /// Unscaled on purpose: at 5x speed the bars would otherwise animate too fast to read, and
        /// during the build phase time is stopped entirely but the bars still need to settle.
        /// </summary>
        void AnimateBars(float dt)
        {
            float hpTarget = Player.MaxHp <= 0f ? 0f : Mathf.Clamp01(Player.Hp / Player.MaxHp);
            float manaTarget = Player.MaxMana <= 0f ? 0f : Mathf.Clamp01(Player.Mana / Player.MaxMana);

            if (hpTarget < _hpShown - 0.0005f) _hurtFlash = 1f;

            _hpShown = Mathf.MoveTowards(_hpShown, hpTarget, dt * 3.2f);
            _manaShown = Mathf.MoveTowards(_manaShown, manaTarget, dt * 2.4f);

            // The chip only lags on the way down; healing should not leave a stale bar behind.
            _hpChipShown = _hpChipShown < _hpShown
                ? _hpShown
                : Mathf.MoveTowards(_hpChipShown, _hpShown, dt * 0.5f);

            // Semua diperiksa null: bar dari prefab boleh menghilangkan bagian mana pun kecuali
            // isian HP-nya. Bola mana tanpa serpihan, tanpa latar, dan tanpa angka tetap bola
            // mana yang sah — dan yang menatanya tidak boleh dihukum error karena memilih itu.
            if (_hpFill != null) _hpFill.fillAmount = _hpShown;
            if (_hpChip != null) _hpChip.fillAmount = _hpChipShown;
            if (_manaFill != null) _manaFill.fillAmount = _manaShown;

            // Shader cairan perlu tahu di mana permukaannya. Nilai yang SAMA dengan fillAmount,
            // bukan nilai sasarannya: yang digoyang harus garis yang benar-benar tergambar.
            if (_hpLiquid != null) _hpLiquid.SetFloat("_Fill", _hpShown);
            if (_manaLiquid != null) _manaLiquid.SetFloat("_Fill", _manaShown);

            _hurtFlash = Mathf.Max(0f, _hurtFlash - dt * 3f);

            // Kilat putih bertolak dari warna ASLI bar itu, bukan dari warna tetap di kode.
            // Memakai warna tetap berarti pukulan pertama membuang pewarnaan yang disetel di
            // prefab, dan warnanya tidak pernah kembali sesudahnya.
            if (_hpFill != null) _hpFill.color = Color.Lerp(_hpFillBase, Color.white, _hurtFlash);

            // Below a third, the bar breathes — readable without stealing attention from the board.
            float pulse = hpTarget <= 0.33f && Player.Alive
                ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)
                : 0f;

            if (_hpBg != null)
                _hpBg.color = Color.Lerp(new Color(0.16f, 0.07f, 0.08f, 0.9f),
                    new Color(0.55f, 0.12f, 0.14f, 0.95f), pulse);

            // Mana reads brighter the moment it is topped up, so "ready to cast" is visible.
            // Sama seperti HP: dicerahkan DARI warna aslinya, bukan ditukar ke warna tetap.
            if (_manaFill != null)
                _manaFill.color = Color.Lerp(_manaFillBase, Color.white,
                    Mathf.InverseLerp(0.9f, 1f, _manaShown) * 0.4f);
        }

        void DrawHud()
        {
            _sb.Length = 0;
            _sb.Append("WAVE ").Append(Enemies.Wave);

            if (Enemies.WaveActive)
            {
                _sb.Append(Enemies.Closing
                    ? "    HABISKAN SISANYA"
                    : "    sisa " + (Enemies.PendingSpawns + Enemies.AliveCount) + "/" + Enemies.WaveTotal);
            }

            _sb.Append("    musuh ").Append(Enemies.AliveCount);
            _sb.Append("    kills ").Append(Enemies.Kills);
            _sb.Append("    koin ").Append(_gold);
            _hudText.text = _sb.ToString();

            AnimateBars(Time.unscaledDeltaTime);

            if (_hpLabel != null)
            _hpLabel.text = "HP  " + Mathf.CeilToInt(Player.Hp) + " / " + Mathf.RoundToInt(Player.MaxHp) +
                            (Player.HpRegen > 0f ? "   (+" + Player.HpRegen.ToString("0.0") + "/s)" : "");

            if (_manaLabel != null)
            _manaLabel.text = "MANA  " + Mathf.FloorToInt(Player.Mana) + " / " + Mathf.RoundToInt(Player.MaxMana) +
                              "   (+" + Player.ManaRegen.ToString("0.0") + "/s)";

            // Ailment tally moved to its own icon strip under the mana bar — see DrawBuffs.
        }

        /// <summary>
        /// Pengumuman sekali lewat, memakai floater yang sama dengan reaksi. Widget baru untuk tiap
        /// kabar penting hanya menambah satu lagi tempat yang harus dipelajari pemain.
        /// </summary>
        public void Announce(string message, Color color, Vector3? at = null)
        {
            PushFloater(at ?? Player.transform.position + Vector3.up * 3f, message, color);
        }

        void PushFloater(Vector3 world, string message, Color color)
        {
            for (int i = 0; i < FloatPoolSize; i++)
            {
                if (_floatLife[i] > 0f) continue;

                _floatLife[i] = 1.1f;
                _floatWorld[i] = world;
                _floaters[i].text = message;
                _floaters[i].color = color;
                return;
            }
        }

        void TickFloaters(float dt)
        {
            for (int i = 0; i < FloatPoolSize; i++)
            {
                if (_floatLife[i] <= 0f)
                {
                    if (_floaters[i].text.Length > 0) _floaters[i].text = "";
                    continue;
                }

                _floatLife[i] -= dt;
                _floatWorld[i] += Vector3.up * (1.4f * dt);

                var screen = _camera.WorldToScreenPoint(_floatWorld[i]);
                _floaters[i].rectTransform.anchoredPosition = new Vector2(screen.x, screen.y);

                var c = _floaters[i].color;
                c.a = Mathf.Clamp01(_floatLife[i]);
                _floaters[i].color = c;
            }
        }
    }
}
