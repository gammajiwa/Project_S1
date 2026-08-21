using System.Collections.Generic;
using System.Text;
using TMPro;
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
        Camera _camera;

        Image[] _baseCells;
        Image[] _skillCells;
        Image[] _bagCells;

        Sprite _circle;

        /// <summary>Kotak garis tepi untuk petak piece yang belum dipasang — line art, bukan isi.</summary>
        Sprite _cellFrame;

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

        /// <summary>
        /// Pengali tinggi kotak saat garis evolusi digambar sebagai busur listrik.
        ///
        /// Petirnya bergoyang KELUAR dari sumbu garis, dan goyangan itu hidup di dalam kotak
        /// Image-nya sendiri — bukan di geometri baru. Kotak setipis garisnya akan memotong
        /// goyangan itu sampai habis dan yang tersisa cuma garis lurus yang berkedip.
        /// Tebal INTI yang terlihat tetap segaris ukuran lama; shader yang mengurusnya lewat
        /// pecahan tinggi kotak, jadi angka ini tidak menggemukkan garisnya.
        /// </summary>
        const float EvoBoltHeightMul = 4.5f;

        /// <summary>Benar kalau tema membawa material busur listrik untuk garis evolusi.</summary>
        bool _evoBolt;


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

        /// <summary>Pengeras suara UI, dititip composition root. Null = UI bisu, bukan rusak.</summary>
        public AudioDirector Sfx;

        /// <summary>Sasaran hover terakhir yang sudah dibunyikan — gerbang anti-desis.</summary>
        PieceDefinition _lastHovered;

        /// <summary>Frame saat panel ditutup lewat klik-luar. Klik yang sama boleh lolos ke
        /// logika pungut (disengaja), tapi bunyi pungutnya mengalah — satu klik satu bunyi.</summary>
        int _panelCloseFrame = -1;

        // ---------- kilat hadiah & detak gulungan ----------
        Image _flashVeil;
        float _flashAlpha;
        Color _flashInk = new Color(1f, 0.85f, 0.4f);

        /// <summary>
        /// Kilat sekejap menutup layar — emas untuk hadiah, hijau untuk evolve. VFX termurah
        /// yang terbaca di atas panel apa pun, dan memudar sendiri dalam sepertiga detik.
        /// </summary>
        void Flash(float strength, Color? ink = null)
        {
            if (_flashVeil == null)
            {
                _flashVeil = MakeImage("RewardFlash", Vector2.zero, Vector2.zero,
                    Color.clear, Vector2.zero);
                _flashVeil.rectTransform.anchorMin = Vector2.zero;
                _flashVeil.rectTransform.anchorMax = Vector2.one;
                _flashVeil.rectTransform.offsetMin = Vector2.zero;
                _flashVeil.rectTransform.offsetMax = Vector2.zero;
            }

            // Selalu ke paling depan: panel slot dan toko lahir belakangan dari kanvas ini,
            // dan kilat yang tertimbun panel adalah kilat yang tidak pernah ada.
            _flashVeil.transform.SetAsLastSibling();
            _flashInk = ink ?? new Color(1f, 0.85f, 0.4f);
            _flashAlpha = Mathf.Max(_flashAlpha, Mathf.Clamp01(strength));
        }

        void DrawFlash(float dt)
        {
            if (_flashVeil == null) return;

            _flashAlpha = Mathf.Max(0f, _flashAlpha - dt * 2.6f);
            _flashVeil.color = new Color(_flashInk.r, _flashInk.g, _flashInk.b,
                _flashAlpha * 0.5f);
            _flashVeil.enabled = _flashAlpha > 0.001f;
        }

        bool _shopOpen;
        int _rerollCost;
        readonly PieceDefinition[] _shop = new PieceDefinition[ShopSlots];

        /// <summary>
        /// Sisa detik judul toko menyalakan penolakan "koin kurang".
        ///
        /// Ada karena penolakan yang DIAM tidak terbaca sebagai penolakan. Dulu klik ke barang
        /// yang tidak terbeli mengembalikan true dan berhenti di situ: tidak ada suara, tidak
        /// ada tulisan, tidak ada yang bergerak. Yang dilaporkan pemilik project bukan "uang
        /// saya kurang" melainkan <b>"shop gak bisa di-drag itemnya"</b> — dan itu memang yang
        /// terlihat.
        /// </summary>
        float _shopNag;

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
        Image[] _mapEdges = System.Array.Empty<Image>();

        // Penyangga pengukur lengkung, dipakai ulang tiap ruas. Dialokasikan sekali: peta penuh
        // menggambar puluhan ruas per frame, dan dua array baru per ruas adalah sampah yang
        // dihasilkan enam puluh kali sedetik untuk hasil yang dibuang seketika.
        readonly Vector2[] _arcPoints = new Vector2[MapArcSamples + 1];
        readonly float[] _arcLengths = new float[MapArcSamples + 1];
        Image[] _mapNodes = System.Array.Empty<Image>();
        Image[] _mapRings = System.Array.Empty<Image>();
        TextMeshProUGUI[] _mapGlyphs = System.Array.Empty<TextMeshProUGUI>();
        Image[] _mapIcons = System.Array.Empty<Image>();

        /// <summary>Tanda "sudah dibereskan" di atas node yang telah diinjak. Sprite dari tema;
        /// placeholder X generatan kode selama art-nya belum ada.</summary>
        Image[] _mapClearedMarks = System.Array.Empty<Image>();
        Sprite _clearedPlaceholder;
        int _mapSig = -1;

        // Bahan tampilan: kertas, bingkai, warna tinta. Boleh null — tiap pemakainya wajib jatuh
        // kembali ke kotak warna datar, supaya art yang belum ada tidak pernah memblokir tes.
        UiTheme _theme;

        /// <summary>Font TMP seluruh teks gambar-kode; null = font bawaan TMP Settings.</summary>
        TMP_FontAsset TmpFont => _theme != null ? _theme.TmpFont : null;

        /// <summary>Font khusus angka damage. Jatuh ke <see cref="TmpFont"/> kalau tema tidak memisahkannya.</summary>
        TMP_FontAsset _numberFont;

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
        // 210 (dulu 170): node sekarang seukuran ibu jari dan boss empat kali lipatnya —
        // di jarak lantai yang lama, kaki boss menindih node di bawahnya.
        const float MapFloorGap = 210f;

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


        bool _eventOpen;
        bool _eventDone;
        Image _eventBg;
        TextMeshProUGUI _eventTitle;
        TextMeshProUGUI _eventBody;
        Image _eventABg;
        Image _eventBBg;
        TextMeshProUGUI _eventALabel;
        TextMeshProUGUI _eventBLabel;
        Image _eventCBg;
        TextMeshProUGUI _eventCLabel;

        /// <summary>
        /// Dua pakta yang sedang ditawarkan. Diundi SEKALI saat pemain mendarat di pulaunya,
        /// bukan tiap frame gambar: undian per frame berarti kartunya berganti-ganti di depan mata
        /// pemain yang sedang membacanya, dan yang akhirnya diklik bukan yang dibaca.
        /// </summary>
        readonly WorldModifierDefinition[] _pactOffer = new WorldModifierDefinition[2];

        /// <summary>
        /// Pakta yang PERNAH TAMPIL di run ini — diambil ataupun ditolak. Aturan pemilik
        /// project: satu pakta cuma boleh muncul sekali per run; yang ditolak tidak antre lagi
        /// di kejadian berikutnya. Umurnya sepanjang UI ini (= sepanjang run), reset natural
        /// bersama scene run baru.
        /// </summary>
        readonly HashSet<WorldModifierDefinition> _pactsOffered =
            new HashSet<WorldModifierDefinition>();

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
        TextMeshProUGUI _panelTitle;

        /// <summary>Warna judul panel yang dipilih tema, dipegang untuk dipulihkan setelah nag.</summary>
        Color _panelTitleInk = Color.white;
        Image[] _shopSlotBg;
        TextMeshProUGUI[] _shopSlotText;
        Image _rerollBg;
        TextMeshProUGUI _rerollLabel;
        /// <summary>Kartu pakta memakai sprite Chip - resep pewarnaannya beda dari kotak polos.</summary>
        bool _eventCardsSkinned;

        /// <summary>
        /// Kotak pembatas gambar per slot toko, diukur SAAT ISINYA BERGANTI. <see cref="PieceBounds"/>
        /// memutar bentuk dan menata ulang art untuk mengukur — kerja yang hasilnya tidak berubah
        /// selama stoknya sama, jadi tidak ada alasan mengulanginya enam kali tiap frame.
        /// </summary>
        readonly PieceDefinition[] _shopBoundsFor = new PieceDefinition[ShopSlots];
        readonly Vector2[] _shopBoundsSize = new Vector2[ShopSlots];
        readonly Vector2[] _shopBoundsOffset = new Vector2[ShopSlots];

        /// <summary>
        /// Visual panel toko datang dari PREFAB (ShopPanel.prefab membawa Image/TMP sendiri).
        /// Kode berhenti menulis posisi/ukurannya - "TERISI = milik prefab", aturan yang sama
        /// dengan CombatHud. Kode tinggal mengisi teks, warna keadaan, dan menggambar isi slot.
        /// </summary>
        bool _shopVisualsFromPrefab;

        /// <summary>Kembarannya untuk panel kejadian.</summary>
        bool _eventVisualsFromPrefab;

        /// <summary>
        /// Akar instance prefab toko di kanvas. Buka-tutup panel MEMATIKAN SATU POHON lewat
        /// SetActive, bukan memilih komponen satu per satu: apa pun yang ditata tangan di
        /// prefab — header, ornamen, garis — ikut hidup dan mati tanpa perlu dikenali kode.
        /// Kotak-kotak tata letak sudah dibaca ke Rect statis saat build, jadi pohon yang
        /// mati tidak menghilangkan letak apa pun.
        /// </summary>
        GameObject _shopRigRoot;

        /// <summary>Kembarannya untuk panel kejadian.</summary>
        GameObject _eventRigRoot;

        /// <summary>Slot dagangan memakai sprite Chip - sama alasannya.</summary>
        bool _shopSlotsSkinned;

        Image _shopBtnBg;
        TextMeshProUGUI _shopBtnLabel;

        // --- layar GAME OVER ---
        Image _overVeil;
        TextMeshProUGUI _overTitle;
        TextMeshProUGUI _overInfo;
        Image _overMenuBg;
        TextMeshProUGUI _overMenuLabel;
        Image _overReviveBg;
        TextMeshProUGUI _overReviveLabel;
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
        /// Slot toko asal barang yang sedang dibawa, atau −1. Selama ini terisi, barangnya
        /// BELUM DIBAYAR: uang berpindah saat ia terpasang di papan/tas, dan batal menaruh
        /// memulangkannya ke slot ini tanpa transaksi.
        /// </summary>
        int _heldShopSlot = -1;

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
        Image[] _spellNotch;
        TextMeshProUGUI[] _spellText;

        /// <summary>Baris kolaps "+N" di bawah baris terakhir — yang tak kebagian tempat.</summary>
        TextMeshProUGUI _spellMore;

        /// <summary>Jumlah baris spell yang terakhir ditata; -1 memaksa penataan pertama.</summary>
        int _spellRowsShown = -1;

        Image _hudPlaque;

        /// <summary>
        /// Plakat HUD datang dari prefab, jadi ukurannya BUKAN urusan kode lagi.
        /// Lihat tempat ia diset dan tempat Redraw memeriksanya.
        /// </summary>
        bool _hudPlaqueOwnsSize;

        /// <summary>Row order for the panel: indices into Book.Spells, sorted by damage.</summary>
        readonly int[] _spellOrder = new int[MaxSpellRows];

        Image[] _speedButtons;
        TextMeshProUGUI[] _speedLabels;
        int _speedSlot;

        // --- damage meter ---
        readonly DamageMeter _meter = new DamageMeter();

        StatusStrip _buffStrip;

        /// <summary>
        /// Buff yang PERNAH menyala di run ini. Strip buff tidak lagi menghapus entri yang
        /// jeda - ia meredupkannya, supaya peta build pemain tetap terbaca. Yang tetap
        /// menghilang sepenuhnya hanya debuff musuh ke kita (strip sebelah).
        /// </summary>
        readonly List<BuffDefinition> _seenBuffs = new List<BuffDefinition>();

        /// <summary>Ailment (index database) yang pernah menempel di lapangan run ini.</summary>
        readonly HashSet<int> _seenAilments = new HashSet<int>();
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

        TextMeshProUGUI _hudText;
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
        TextMeshProUGUI _hpLabel;
        Image _manaBg;
        Image _manaFill;
        TextMeshProUGUI _manaLabel;

        // Kotak yang menyalakan kartu keterangan HP/mana. Boleh sama dengan isiannya; boleh juga
        // kotak lain yang mencakup bingkai bolanya — lihat VitalsRig.
        RectTransform _hpHover;
        RectTransform _manaHover;

        // Seluruh POHON kotak di bawah daerah hover, di-cache sekali saat dibangun.
        // Art bola di prefab tataan tangan boleh lebih besar atau bergeser dari kotak
        // hover aslinya — dan menyapu pohonnya lewat GetComponentsInChildren tiap frame
        // adalah alokasi enam puluh kali sedetik. Kosong = uji kotak tunggalnya saja
        // (jalur bar gambar-kode).
        RectTransform[] _hpHoverRects = System.Array.Empty<RectTransform>();
        RectTransform[] _manaHoverRects = System.Array.Empty<RectTransform>();

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

        // ---------- pakaian HUD: satu keluarga warna untuk semua panel yang digambar kode ----------
        // Diambil dari menu utama (emas antik di atas indigo-hitam), supaya HUD run dan menu
        // terbaca sebagai satu game. Panel gambar-kode yang keluar dari keluarga ini langsung
        // terbaca sebagai alat debug — persis keluhan atas HUD yang lama.
        static readonly Color PanelInk = new Color(0.055f, 0.05f, 0.09f, 0.88f);
        static readonly Color PanelEdge = new Color(0.76f, 0.62f, 0.34f, 0.8f);
        static readonly Color TextBone = new Color(0.9f, 0.86f, 0.76f, 1f);
        static readonly Color TextGold = new Color(0.89f, 0.75f, 0.46f, 1f);
        static readonly Color TextDim = new Color(0.58f, 0.53f, 0.45f, 1f);
        Image _tipBg;
        TextMeshProUGUI _tipText;
        TextMeshProUGUI _heldText;
        TextMeshProUGUI _bannerText;
        TextMeshProUGUI _gridTitle;
        TextMeshProUGUI _evolveText;
        float _evolveTimer;

        Image _startBg;
        TextMeshProUGUI _startLabel;

        TextMeshProUGUI[] _floaters;
        float[] _floatLife;
        Vector3[] _floatWorld;
        float[] _floatMax;   // umur lahir — buat menghitung sentakan pop di awal hidupnya
        float[] _floatScale; // pengali ukuran; di atas 1 = pengumuman besar (reaksi)

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

            // Kebangkitan pakta HARUS berteriak. Satu-satunya umpan baliknya dulu kilat kecil
            // di kaki pemain — di tengah ledakan wave tinggi tidak pernah terlihat, dan pemain
            // yang tidak tahu jatahnya sudah terpakai menyebut kematian berikutnya "revive gak
            // jalan". (OnRevived selama ini TIDAK punya satu pun pendengar.)
            Player.OnRevived += () =>
                Announce(Loc.T("hud.revive.announce"), new Color(1f, 0.88f, 0.45f));

            // SEMUA teks TMP — tidak ada lagi UI Text legacy (aturan pemilik project). Font
            // dari tema kalau ada; null berarti font bawaan TMP Settings, jadi tema yang lupa
            // diisi tetap tidak pernah membuat setengah layar jadi kotak kosong.
            _numberFont = _theme != null && _theme.TmpNumberFont != null
                ? _theme.TmpNumberFont
                : TmpFont;

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

            // Payung UI combat DIPASANG PALING AWAL: semua pembangun di bawah mencari
            // bagiannya di sini lebih dulu, dan yang lahir belakangan otomatis tergambar
            // di atasnya (urutan pembuatan = urutan gambar di kanvas ini).
            AttachCombatUi();

            BuildGrid();
            BuildSkillWidgets();
            BuildBackpack();
            BuildLoose();
            BuildShop();
            _codex = DiscoveryLog.Load();
            BuildSpellPanel();
            BuildSpeedControl();
            BuildHud();
            BuildMeter();
            BuildFloaters();

            // PALING AKHIR di antara pembangun HUD: kotak-kotak tataan tangan menang atas
            // letak hitungan mana pun yang sudah tertulis di atas, tanpa perlu tiap pembangun
            // tahu-menahu soal rig.
            ApplyCombatHudSeats();

            // Built last: on this canvas creation order is draw order, and the recipe card has to
            // sit on top of everything it is explaining.
            _recipes = new RecipePanel(_canvas.transform, TmpFont, _db, OwnedCount,
                _theme != null ? _theme.RecipeCardPrefab : null);

            // Kartu hover DIANGKAT ke atas kartu resep: saat meneliti resep (ALT), ikon di
            // dalamnya bisa ditanya — dan kartu jawabannya harus tergambar DI ATAS kartu
            // resep, bukan tenggelam di baliknya (laporan: "hover evo malah di atas").
            if (_tipBg != null) _tipBg.transform.SetAsLastSibling();
            if (_tipText != null) _tipText.transform.SetAsLastSibling();

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
            // Reaksi = pengumuman, bukan kabar biasa. Skala 2,2 ≈ font 44 — sekelas judul,
            // karena reaksi memang kejadian paling keren di lapangan dan hurufnya harus
            // sepadan dengan ledakannya.
            Enemies.OnReaction += (pos, rx) => PushFloater(pos, ReactionName(rx) + "!", rx.FlashColor, 2.2f);

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

            // PALING PUCUK kanvas, setelah semua pembangun lain (urutan pembuatan = urutan
            // gambar): tutorial menggelapkan seluruh HUD dan menyorot satu objek, jadi tidak
            // boleh ada apa pun yang lahir di atasnya kecuali layar GAME OVER (yang menaikkan
            // dirinya sendiri — dan memang berhak menang).
            _tut = new TutorialOverlay(_canvas.transform, TmpFont);

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

            // ScaleWithScreenSize, rujukan QHD — resolusi tempat seluruh UI ini ditata mata.
            // Di 2560x1440 skalanya tepat 1 (tidak ada yang bergeser sepiksel pun); resolusi
            // lain ikut proporsi, bukan membesar-mengecil sesukanya seperti saat masih
            // ConstantPixelSize. Match penuh ke TINGGI: lebar boleh lapang di layar lebar.
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560f, UiRefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            UiScale = Screen.height / UiRefHeight;

            // Seluruh UI permainan memakai hit-test sendiri (posisi mouse -> petak), jadi kanvas
            // ini hidup lama tanpa raycaster dan tidak ada yang sadar. Yang menagihnya adalah
            // barang UGUI beneran: halaman setelan yang dibuka ESC, tombol KELUAR KE MENU,
            // stepper, slider. Tanpa GraphicRaycaster, EventSystem tidak menemukan satu pun
            // grafik di kanvas ini dan tombolnya tidak mati dengan error — dia cuma DIAM.
            go.AddComponent<GraphicRaycaster>();
        }

        /// <summary>
        /// Pasang bingkai kit UI pada kotak yang lahir polos. Sprite null = false, dan kotak
        /// warnanya bertahan apa adanya — art yang belum dipasang tidak boleh memblok tes.
        /// Pemanggil memakai nilai baliknya untuk melewati garis Frame lama: bingkai art plus
        /// outline tebal terbaca sebagai dua bingkai bertumpuk.
        /// </summary>
        bool Skin(Image img, Sprite sprite, float alpha = 1f)
        {
            if (img == null || sprite == null) return false;

            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(1f, 1f, 1f, alpha);
            return true;
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

        /// <summary>
        /// Teks TMP — aturan pemilik project: SEMUA teks pakai TextMeshPro, tidak pernah lagi
        /// UI Text legacy. Fontnya slot UiTheme.TmpFont; kosong = font bawaan TMP.
        /// </summary>
        TextMeshProUGUI MakeTmp(string name, Vector2 pos, Vector2 size, float fontSize,
            Color color, Vector2 anchor, TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            if (_theme != null && _theme.TmpFont != null) text.font = _theme.TmpFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;

            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return text;
        }

        /// <summary>
        /// Bingkai emas serambut di tepi sebuah Graphic, lewat komponen Outline — bukan empat
        /// Image anak. Sengaja: Outline ikut mati saat Graphic-nya di-.enabled = false,
        /// sedangkan GameObject anak tetap tergambar dan meninggalkan bingkai hantu.
        /// </summary>
        static void Frame(Graphic g, float px = 1f)
        {
            var line = g.gameObject.AddComponent<Outline>();
            line.effectColor = PanelEdge;
            line.effectDistance = new Vector2(px, px);
        }

        void BuildGrid()
        {
            // Dialokasikan sampai KAPASITAS (8x8), bukan ukuran papan hari ini: pakta ADDENDUM
            // menumbuhkan papan di tengah run, dan di kanvas ini urutan pembuatan = urutan
            // gambar — petak yang lahir belakangan pasti mendarat di pucuk kanvas, menutupi
            // panel. Petak cadangan lahir sekarang, nonaktif, dan cuma DIDUDUKKAN ULANG saat
            // papan berubah (ReseatBoardCells). Indeks petak selalu y * Grimoire.Width + x
            // dengan Width HARI INI, jadi pemetaan indeks ikut berubah bersama ukuran papan —
            // aman karena DrawGrid menulis ulang warna/enabled SEMUA petak aktif tiap frame.
            int cap = Grimoire.MaxWidth * Grimoire.MaxHeight;
            _baseCells = new Image[cap];
            _skillCells = new Image[cap];

            // Bingkai DULU, petak belakangan: di kanvas ini urutan bikin adalah urutan gambar,
            // jadi apa pun yang dibuat setelah ini duduk di atasnya. Dua lapis, dari luar ke
            // dalam — sampul buku memeluk seluruh papan, bingkai emas memeluk petaknya saja.
            BuildGridFrames();

            // Dua lapisan dibuat TERPISAH, bukan berselang-seling per petak. Urutan bikin adalah
            // urutan gambar di kanvas ini, dan tile rune harus disisipkan di ANTARA keduanya:
            // di atas kotak warna dasar, tapi di bawah kotak skill — skill berdiri DI ATAS rune,
            // dan tile yang latarnya pekat akan menelannya kalau urutannya terbalik.
            // Lapisan art DI BAWAH petak — untuk piece ber-ArtBehindCells. Dibuat sebelum
            // petak karena di kanvas ini urutan bikin adalah urutan gambar.
            _artBehindLayer = MakeArtLayer("PieceArtBehind");

            for (int i = 0; i < cap; i++)
            {
                _baseCells[i] = MakeImage($"Base_{i}", Vector2.zero,
                    new Vector2(CellSize, CellSize), CellIdle, Vector2.zero);
                _baseCells[i].enabled = false;
            }

            for (int i = 0; i < cap; i++)
            {
                _skillCells[i] = MakeImage($"Skill_{i}", Vector2.zero,
                    new Vector2(CellSize - SkillInset * 2, CellSize - SkillInset * 2),
                    Color.white, Vector2.zero);
                _skillCells[i].enabled = false;
            }

            ReseatBoardCells();

            // Lapisan art DI ATAS petak (bawaan) — sebelum widget skill, supaya cincin
            // cooldown tetap terbaca di atas art.
            _artFrontLayer = MakeArtLayer("PieceArtFront");

            // Tempat bawaannya pojok kiri tepat di atas petak — benar selama papan masih polos,
            // dan langsung salah begitu prefabnya menaruh hiasan di sana.
            var titleAt = new Vector2(GridX, GridTop() + 8);

            if (_gridRig != null && _gridRig.TitleArea != null)
                titleAt = CanvasRectOf(_gridRig.TitleArea).min;

            _gridTitle = MakeTmp("GridTitle", titleAt, new Vector2(400, 24), 17,
                _theme != null ? _theme.GridTitleInk : new Color(0.85f, 0.82f, 0.95f),
                Vector2.zero, TextAlignmentOptions.BottomLeft);
            _gridTitle.text = Loc.T("hud.grid.title");

            // Prefab yang sudah membawa ornamen judulnya sendiri mematikan tulisan bawaan.
            // Objeknya tetap DIBUAT, cuma tidak digambar: UpdateHud menulis ke sini tiap frame,
            // dan membiarkannya null berarti menyebar pemeriksaan null ke seluruh pemakainya.
            if (_gridRig != null && !_gridRig.ShowTitle) _gridTitle.enabled = false;

            // Posisi awal cuma placeholder — ApplyCombatHudSeats mendudukkannya di bawah-tengah
            // layar. PUTIH + 23 + outline hitam: teks ini melayang di atas arena (rumput terang
            // sampai hutan gelap), dan tanpa tepi gelap ia hilang persis di latar yang salah
            // ("gak kebaca" — screenshot pemilik project 2026-08-19).
            _heldText = MakeTmp("HeldInfo", new Vector2(Margin, StripAilmentY - 62f),
                new Vector2(880, 30), 23, new Color(0.96f, 0.96f, 0.96f), new Vector2(0f, 1f),
                TextAlignmentOptions.Center);
            _heldText.outlineWidth = 0.18f;
            _heldText.outlineColor = new Color32(0, 0, 0, 220);

            _evolveText = MakeTmp("EvolveInfo", new Vector2(Margin, StripAilmentY - 84f),
                new Vector2(880, 30), 23, new Color(0.55f, 1f, 0.7f), new Vector2(0f, 1f),
                TextAlignmentOptions.Center);
            _evolveText.outlineWidth = 0.18f;
            _evolveText.outlineColor = new Color32(0, 0, 0, 220);
            _evolveText.text = "";
        }

        /// <summary>
        /// Mendudukkan petak papan untuk ukuran Grimoire SAAT INI: petak aktif ke posisi &amp;
        /// ukuran barunya (CellAnchor/CellSize sudah membaca lebar papan baru), petak cadangan
        /// dimatikan. Dipanggil sekali di BuildGrid dan tiap papan berubah ukuran (ADDENDUM).
        /// Cukup reseat, tanpa membuat objek: DrawGrid menulis ulang warna/enabled petak aktif
        /// tiap frame, jadi sisa pekerjaan sudah beres dengan sendirinya.
        /// </summary>
        void ReseatBoardCells()
        {
            if (_baseCells == null) return;

            float inner = CellSize - SkillInset * 2;

            for (int y = 0; y < Grimoire.Height; y++)
            {
                for (int x = 0; x < Grimoire.Width; x++)
                {
                    int i = y * Grimoire.Width + x;
                    var at = CellAnchor(x, y);

                    var baseRt = _baseCells[i].rectTransform;
                    baseRt.anchoredPosition = at;
                    baseRt.sizeDelta = new Vector2(CellSize, CellSize);

                    var skillRt = _skillCells[i].rectTransform;
                    skillRt.anchoredPosition = at + new Vector2(SkillInset, SkillInset);
                    skillRt.sizeDelta = new Vector2(inner, inner);
                }
            }

            // Cadangan di luar papan hari ini: padam. DrawGrid tidak pernah menyentuh indeks
            // di atas Width*Height, jadi yang tidak dipadamkan di sini menyala selamanya.
            for (int i = Grimoire.Width * Grimoire.Height; i < _baseCells.Length; i++)
            {
                _baseCells[i].enabled = false;
                _skillCells[i].enabled = false;
            }
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
            // yang harus sejajar dengan petak papan. Selebihnya — ukuran, anchor, hiasan, urutan
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
                    // pojok papan — menggeser papan di prefab menggeser petak papan bersamanya,
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
                ? new Vector2(-ScreenW * 0.5f, -ScreenH * 0.5f)
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

        /// <summary>
        /// Jam cooldown tidak lagi punya widget sendiri: art piece yang terpasang dirangkap
        /// jadi jamnya — lapis redup + lapis isi ber-fillAmount, digambar DrawPlacedArt.
        /// Sprite bulatan tetap dibuat: penanda pemain di peta memakainya sebagai cadangan.
        /// </summary>
        void BuildSkillWidgets()
        {
            _circle = MakeCircleSprite(64);
            _pulse = new float[MaxSpellRows];
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

        /// <summary>Kotak garis tepi: border pekat, tengah kosong. Untuk petak line-art.</summary>
        static Sprite MakeOutlineSprite(int size, int border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < border || y < border || x >= size - border || y >= size - border;
                    pixels[y * size + x] = new Color32(255, 255, 255, edge ? (byte)255 : (byte)0);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        void BuildBackpack()
        {
            var hudRig = AttachCombatHudRig();

            // Alas lebih dulu — urutan pembuatan adalah urutan gambar, dan alas yang lahir
            // setelah selnya akan menutupi seluruh isi tas.
            //
            // Dari rig HUD kalau prefabnya membawa kotak BagPanel yang aktif: sprite, warna,
            // letak, dan ukuran alas milik prefab — tukar art tas = tukar sprite di prefab,
            // tanpa menyentuh kode. Petak-petak tas tetap digambar kode di tempat hitungan
            // GrimoireLayout (janji di tooltip rig).
            if (hudRig != null && hudRig.BagPanel != null)
            {
                // Nonaktif pun tetap diadopsi: un-check di prefab = tas tanpa alas, bukan
                // alas gambar-kode yang bangkit lagi dari sprite tema.
                _bagFrame = hudRig.BagPanel;

                // Diangkat KELUAR dari rig ke kanvas, di titik build yang sama dengan alas
                // gambar-kode: rig diangkat ke pucuk tumpukan di ApplyCombatHudSeats supaya
                // TOMBOLNYA menang, dan alas yang ikut terangkat akan menutupi petak + isi
                // tas — persis keluhan "alasnya di depan grid".
                _bagFrame.rectTransform.SetParent(_canvas.transform, true);
                _bagFrame.rectTransform.SetAsLastSibling();

                // RECT-nya MILIK KODE, memeluk petak tas — beda dengan kotak rig lain.
                // Petak dihitung dari GridOverride milik prefab buku (RightX/BagY berubah
                // mengikuti papan), jadi alas yang letaknya ditulis tangan PASTI meleset
                // begitu papan bergeser — itulah "alasnya gak rata" yang sudah dua kali
                // dilaporkan. Sprite, warna, dan bahan tetap milik prefab.
                // Posisi & ukurannya diisi ReseatBag() di bawah — satu rumus untuk build
                // pertama dan untuk papan yang berubah ukuran di tengah run (ADDENDUM).
                var bagRt = _bagFrame.rectTransform;
                bagRt.anchorMin = bagRt.anchorMax = Vector2.zero;
                bagRt.pivot = Vector2.zero;
            }
            else if (_theme != null && _theme.BagPanel != null)
            {
                float w = Backpack.Width * (BagCell + BagGap) - BagGap;
                float h = Backpack.Height * (BagCell + BagGap) - BagGap;

                // Pelukannya 14 per sisi: cukup untuk terbaca sebagai panel, tidak cukup untuk
                // menyentuh sampul buku di kirinya (celahnya 40).
                _bagFrame = MakeFrame("BagPanel", _theme.BagPanel,
                    new Rect(GrimoireLayout.BagOrigin.x - 14f, GrimoireLayout.BagOrigin.y - 14f, w + 28f, h + 28f));
            }

            // Kapasitas 6x6, alasan yang sama dengan petak papan: pakta DEEP POCKETS
            // menumbuhkan tas di tengah run, dan objek yang lahir belakangan di kanvas ini
            // mendarat di pucuk — menutupi panel. Sel cadangan lahir sekarang, dipadamkan
            // ReseatBag, dan cuma didudukkan ulang saat tasnya berubah ukuran.
            _bagCells = new Image[Backpack.MaxWidth * Backpack.MaxHeight];

            for (int i = 0; i < _bagCells.Length; i++)
            {
                _bagCells[i] = MakeImage($"Bag_{i}", Vector2.zero,
                    new Vector2(BagCell, BagCell), HiddenBagCell, Vector2.zero);
            }

            ReseatBag();
        }

        /// <summary>
        /// Alas + petak tas didudukkan dari ukuran sel papan HARI INI (BagCell => CellSize).
        /// Papan yang berubah ukuran (ADDENDUM 7x7) mengecilkan sel di kotak prefab yang sama,
        /// dan tas wajib ikut di frame yang sama — RightX() sendiri nyaris tidak bergeser,
        /// jadi yang berubah cuma ukuran sel tas, bukan letak kolomnya.
        /// </summary>
        void ReseatBag()
        {
            if (_bagFrame != null)
            {
                float bagW = Backpack.Width * (BagCell + BagGap) - BagGap;
                float bagH = Backpack.Height * (BagCell + BagGap) - BagGap;
                var bagRt = _bagFrame.rectTransform;
                bagRt.anchoredPosition = GrimoireLayout.BagOrigin + new Vector2(-14f, -14f);
                bagRt.sizeDelta = new Vector2(bagW + 28f, bagH + 28f);
            }

            if (_bagCells == null) return;

            for (int y = 0; y < Backpack.Height; y++)
            {
                for (int x = 0; x < Backpack.Width; x++)
                {
                    int i = y * Backpack.Width + x;

                    // Dinyalakan DI SINI, bukan di DrawBackpack: penggambar tas hanya menulis
                    // warna, dan sel yang baru masuk jangkauan tas 5x5 harus bangun sendiri.
                    _bagCells[i].enabled = true;

                    var rt = _bagCells[i].rectTransform;
                    rt.anchoredPosition = BagAnchor(x, y);
                    rt.sizeDelta = new Vector2(BagCell, BagCell);
                }
            }

            for (int i = Backpack.Width * Backpack.Height; i < _bagCells.Length; i++)
                _bagCells[i].enabled = false;
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

            _cellFrame = MakeOutlineSprite(32, 2);
            _artLooseLayer = MakeArtLayer("PieceArtLoose");
        }

        /// <summary>Draws one piece centred on <paramref name="center"/>. Returns cells consumed.
        /// <paramref name="scale"/> mengecilkan seluruh piece; 1 = ukuran petak papan. Ada untuk
        /// kotak yang lebih sempit dari papan (slot toko) — piece 3x3 seukuran papan tidak pernah
        /// muat di sana, dan tanpa ini ia meluber menutupi teks harga dan slot tetangganya.</summary>
        int DrawPiece(PieceDefinition def, int rot, Vector2 center, int cursor, float alpha,
            float scale = 1f)
        {
            var shape = Shapes.Rotate(def.Cells, rot);
            var size = PieceSize(shape) * scale;
            float step = (LooseCellSize + LooseCellGap) * scale;
            Vector2 origin = center - size * 0.5f;

            bool isSkill = def.Layer == Layer.Skill;
            float inner = (isSkill ? LooseCellSize - SkillInset * 2f : LooseCellSize) * scale;
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

                // Piece ber-visual (art ATAU ikon) tidak lagi tampil sebagai kotak ISI:
                // gambarnya yang di depan, petaknya tinggal GARIS TEPI tipis — cukup untuk
                // membaca bentuk grid tanpa bersaing. Kotak isi hanya untuk piece yang
                // benar-benar tidak punya gambar apa pun.
                bool outline = def.Art != null || def.Icon != null;
                img.sprite = outline ? _cellFrame : null;
                img.color = outline ? new Color(color.r, color.g, color.b, alpha * 0.55f) : color;
                img.rectTransform.sizeDelta = new Vector2(inner, inner);
                img.rectTransform.anchoredPosition = origin + new Vector2(
                    shape[i].x * step + LooseCellSize * scale * 0.5f,
                    shape[i].y * step + LooseCellSize * scale * 0.5f);

                if (!tiled || _looseTiles == null) continue;

                var tile = _looseTiles.Take();
                tile.Cover(img.rectTransform, LooseCellGap / Mathf.Max(1f, LooseCellSize));
                // Tercecer = belum terpasang: rune diperlakukan sama dengan skill - tile-nya
                // disemu ke abu dulu, warna aslinya baru menyala saat duduk di papan.
                tile.Bind(RuneTiles.BakedTileAt(def, i), RuneTiles.GlyphAt(def, i),
                    Color.Lerp(RuneTiles.AreaTint(def, i, def.Color),
                        new Color(0.45f, 0.45f, 0.5f), 0.55f), alpha);
            }

            // Visual piece (art atau ikon terpusat) ikut menempel di piece yang menggeletak/
            // dipajang, skala mengikuti. Lapisan LOOSE, bukan lapisan depan biasa: petak
            // tercecer dinaikkan ke atas panel, dan art di lapisan biasa tenggelam di baliknya.
            // Tercecer di tanah = BELUM TERPASANG, jadi diredupkan sama seperti isi tas.
            // Yang menyala penuh hanya yang sudah duduk di papan — itu satu-satunya jawaban
            // yang dicari mata: mana yang sudah kepasang, mana yang belum.
            if (!tiled)
                DrawPieceVisual(def, origin, rot, LooseCellSize * scale, LooseCellGap * scale,
                    alpha, loose: true, dim: true);

            return cursor;
        }

        /// <summary>
        /// Kotak pembatas piece pada skala 1 — PETAKNYA DAN ART-nya sekaligus.
        ///
        /// Art boleh lebih besar dari footprint petaknya (ArtSize di SO), dan mengukur muat-tidaknya
        /// dari jumlah petak saja adalah sebab piece ber-art besar menindih baris nama & harga di
        /// kartu toko: petaknya muat, gambarnya tidak. Seluruh matematika PieceArt linear terhadap
        /// ukuran petak, jadi kotak yang diukur di sini boleh dikalikan skala berapa pun.
        ///
        /// <paramref name="offset"/> = pergeseran pusat kotak gabungan dari pusat bbox PETAK, yaitu
        /// titik yang dipakai <see cref="DrawPiece"/> sebagai center. Kurangkan (sesudah dikali
        /// skala) untuk menaruh gambarnya benar-benar di tengah kotak tujuan.
        /// </summary>
        static void PieceBounds(PieceDefinition def, int rot, out Vector2 size, out Vector2 offset)
        {
            var shape = Shapes.Rotate(def.Cells, rot);
            var cells = PieceSize(shape);

            float xMin = 0f, yMin = 0f, xMax = cells.x, yMax = cells.y;

            // Rune tampil sebagai tile per petak dan art-nya TIDAK ikut tergambar (lihat penjaga
            // `if (!tiled)` di DrawPiece). Menghitungnya di sini akan menyusutkan runenya supaya
            // muat untuk gambar yang tidak pernah muncul.
            bool tiled = RuneTiles.IsRuneGlyph(def.Icon);

            if (!tiled && PieceArt.Layout(def, Vector2.zero, rot, LooseCellSize, LooseCellGap,
                    out var artCentre, out var artSize, out var artAngle))
            {
                // Art boleh miring: yang dipakai kotak SEJAJAR SUMBU-nya, bukan lebar mentahnya —
                // gambar 45 derajat memakan ruang lebih besar dari sisi-sisinya sendiri.
                float rad = artAngle * Mathf.Deg2Rad;
                float cos = Mathf.Abs(Mathf.Cos(rad)), sin = Mathf.Abs(Mathf.Sin(rad));
                float aw = artSize.x * cos + artSize.y * sin;
                float ah = artSize.x * sin + artSize.y * cos;

                xMin = Mathf.Min(xMin, artCentre.x - aw * 0.5f);
                xMax = Mathf.Max(xMax, artCentre.x + aw * 0.5f);
                yMin = Mathf.Min(yMin, artCentre.y - ah * 0.5f);
                yMax = Mathf.Max(yMax, artCentre.y + ah * 0.5f);
            }

            size = new Vector2(Mathf.Max(1f, xMax - xMin), Mathf.Max(1f, yMax - yMin));
            offset = new Vector2((xMin + xMax) * 0.5f - cells.x * 0.5f,
                                 (yMin + yMax) * 0.5f - cells.y * 0.5f);
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
                new Color(0.17f, 0.02f, 0.03f, 1f), Vector2.zero);
            _overVeil.rectTransform.anchorMin = Vector2.zero;
            _overVeil.rectTransform.anchorMax = Vector2.one;
            _overVeil.rectTransform.offsetMin = Vector2.zero;
            _overVeil.rectTransform.offsetMax = Vector2.zero;

            _overTitle = MakeTmp("OverTitle", new Vector2(0f, 170f), new Vector2(1200f, 130f), 92,
                new Color(1f, 0.93f, 0.88f), new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center);
            _overTitle.text = Loc.T("hud.gameover.title");

            _overInfo = MakeTmp("OverInfo", new Vector2(0f, 88f), new Vector2(900f, 48f), 34,
                new Color(1f, 0.76f, 0.7f), new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center);

            _overMenuBg = MakeImage("OverMenuBg", Vector2.zero, Vector2.zero,
                new Color(0.14f, 0.04f, 0.05f, 0.96f), Vector2.zero);
            Skin(_overMenuBg, _theme != null ? _theme.ButtonFrame : null);
            _overMenuLabel = MakeTmp("OverMenuLabel", Vector2.zero, new Vector2(OverButtonW, 30f), 24,
                new Color(1f, 0.9f, 0.86f), Vector2.zero, TextAlignmentOptions.Center);
            _overMenuLabel.text = Loc.T("hud.gameover.menu");

            // Tombol IDUP LAGI — cuma tampil selama pakta kebangkitan masih punya jatah
            // (ShowGameOver yang menimbang). Hijau, bukan merah keluarga tombol pulang:
            // satu-satunya tombol di layar ini yang MENERUSKAN run, dan warnanya yang bilang.
            _overReviveBg = MakeImage("OverReviveBg", Vector2.zero, Vector2.zero,
                new Color(0.06f, 0.14f, 0.05f, 0.96f), Vector2.zero);
            Skin(_overReviveBg, _theme != null ? _theme.ButtonFrame : null);
            _overReviveLabel = MakeTmp("OverReviveLabel", Vector2.zero,
                new Vector2(OverButtonW, 30f), 24,
                new Color(0.85f, 1f, 0.8f), Vector2.zero, TextAlignmentOptions.Center);
            _overReviveLabel.text = Loc.T("hud.gameover.revive");

            // Kalimatnya membawa keterangan "(pakai pakta)" dan beberapa bahasa menuliskannya
            // panjang — menyusut lebih baik daripada tumpah keluar tombol (pola _hudText).
            _overReviveLabel.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            _overReviveLabel.enableAutoSizing = true;
            _overReviveLabel.fontSizeMax = 24f;
            _overReviveLabel.fontSizeMin = 14f;

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
                _overReviveBg.transform.SetAsLastSibling();
                _overReviveLabel.transform.SetAsLastSibling();
                _overMenuBg.transform.SetAsLastSibling();
                _overMenuLabel.transform.SetAsLastSibling();
            }

            _overVeil.enabled = on;
            _overTitle.enabled = on;
            _overInfo.enabled = on;
            _overMenuBg.enabled = on;
            _overMenuLabel.enabled = on;

            // Ditimbang tiap panggilan, bukan sekali: jatahnya habis begitu tombolnya ditekan,
            // dan kematian BERIKUTNYA di run yang sama harus tampil tanpa tombol ini.
            bool revive = on && Player != null && Player.CanRevive;
            _overReviveBg.enabled = revive;
            _overReviveLabel.enabled = revive;
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

            // Fanfare kalah SEKALI, di frame pertama kerudung mulai memerah — _overFade
            // masih nol hanya di frame itu (di-nol-kan lagi tiap frame selama masih hidup).
            // TIDAK berbunyi selagi tombol IDUP LAGI masih ditawarkan: nada kalah untuk
            // kematian yang mungkin dibatalkan itu bohong, dan ia mematikan musik combat
            // yang harus tetap jalan kalau pemain memilih bangkit.
            if (_overFade <= 0f && Sfx != null && !Player.CanRevive) Sfx.GameOverFanfare();

            ShowGameOver(true);

            // Memerah dalam ~0,7 detik, bukan menjeglek. Unscaled: kematian boleh saja
            // terjadi saat timescale sedang diperlambat lewat Ruang Uji.
            _overFade = Mathf.MoveTowards(_overFade, 1f, Time.unscaledDeltaTime * 1.4f);
            // Turun dari 0,94 merah pekat. Layar mati harus MENUTUP, bukan mengecat: yang
            // pekat menghapus arena yang baru saja membunuh pemain, dan justru itu satu-satunya
            // hal yang masih ingin dilihatnya sedetik setelah mati.
            SetAlpha(_overVeil, 0.66f * _overFade);
            SetAlpha(_overTitle, _overFade);
            SetAlpha(_overInfo, _overFade);
            SetAlpha(_overMenuLabel, _overFade);

            // Koin DICABUT dari layar mati atas permintaan pemilik project. Angka yang tidak
            // bisa dipakai lagi setelah run berakhir bukan hasil, ia cuma keterangan.
            _overInfo.text = Loc.F("hud.gameover.info", Enemies.Wave);

            var menu = GameOverMenuRect();
            Seat(_overMenuBg.rectTransform, menu);
            Seat(_overMenuLabel.rectTransform, menu);

            // Menyorot tombolnya: satu-satunya umpan balik yang tersisa di layar ini.
            var mouse = UiMouse;
            _overMenuBg.color = menu.Contains(mouse)
                ? new Color(0.32f, 0.09f, 0.08f, 0.98f * _overFade)
                : new Color(0.14f, 0.04f, 0.05f, 0.96f * _overFade);

            if (Player.CanRevive)
            {
                var again = GameOverReviveRect();
                Seat(_overReviveBg.rectTransform, again);
                Seat(_overReviveLabel.rectTransform, again);
                SetAlpha(_overReviveLabel, _overFade);
                _overReviveBg.color = again.Contains(mouse)
                    ? new Color(0.14f, 0.34f, 0.11f, 0.98f * _overFade)
                    : new Color(0.06f, 0.14f, 0.05f, 0.96f * _overFade);
            }
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

            float a = 0.20f * _hurtGlow;  // puncaknya pun tetap tipis — permintaannya "tipis aja"
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

                    // Mulai jauh lebih ke luar (0,62 -> 0,86) dan meluruh lebih tajam (pangkat
                    // dua -> tiga. Yang lama menjalar sampai sepertiga layar dari tiap tepi, dan
                    // kabut selebar itu bukan lagi tanda "barusan kena" - ia menutupi arena
                    // persis saat pemain paling perlu melihat apa yang memukulnya.
                    float a = Mathf.InverseLerp(0.86f, 1.12f, d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * a));
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
            var hudRig = AttachCombatHudRig();

            // TOMBOL TOKO DICABUT TOTAL atas perintah pemilik project ("ngapain shop bisa
            // dibuka-tutup"). Panel dagang terbuka sendiri begitu singgah di node toko dan
            // tertutup saat berangkat — tidak ada lagi saklar di tengah.
            //
            // Versi prefab ikut DIMATIKAN, bukan sekadar tidak di-wire: objek rig yang tidak
            // disentuh siapa pun akan tinggal di layar dengan rupa design-time-nya, dan itu
            // persis "tombol misterius" yang beberapa kali harus dicabut dari layar ini.
            _shopBtnBg = null;
            _shopBtnLabel = null;
            if (hudRig != null && hudRig.ShopToggle != null)
                hudRig.ShopToggle.gameObject.SetActive(false);

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

            // PanelPaper keluarga UIPanel itu GELAP bernada teal — set warna "tinta cokelat
            // di atas perkamen" hanya berlaku saat jatuh ke MapPaper (perkamen terang).
            bool darkPanel = _theme != null && _theme.PanelPaper != null;

            if (paper != null)
            {
                _panelBg.sprite = paper;
                _panelBg.type = Image.Type.Sliced;
                _panelBg.color = darkPanel ? Color.white : new Color(0.82f, 0.79f, 0.72f, 1f);
            }

            _panelBg.enabled = false;

            // Tinta gelap di atas perkamen, tinta terang di atas kotak. Warna judul lama dipilih
            // untuk latar gelap, dan di atas kertas ia praktis tidak terbaca.
            _panelTitle = MakeTmp("PanelTitle", Vector2.zero, new Vector2(PanelW - 24, 26), 17,
                paper != null && !darkPanel ? new Color(0.22f, 0.15f, 0.1f) : new Color(0.9f, 0.88f, 0.98f),
                new Vector2(0.5f, 0.5f), TextAlignmentOptions.TopLeft);
            _panelTitle.enabled = false;

            // Warna asalnya DISIMPAN, bukan dihitung ulang di tempat pemakaian: baris judul
            // sesekali memerah untuk menolak pembelian, dan warna pulangnya harus warna yang
            // dipilih TEMA ini — menuliskannya lagi sebagai konstanta akan membuat judul di
            // atas perkamen berubah jadi tinta terang yang tidak terbaca.
            _panelTitleInk = _panelTitle.color;

            _shopSlotBg = new Image[ShopSlots];
            _shopSlotText = new TextMeshProUGUI[ShopSlots];

            for (int i = 0; i < ShopSlots; i++)
            {
                // Cokelat tinta, bukan biru-gelap. Warna lama dipilih waktu badan panelnya
                // masih kotak biru-gelap; di atas perkamen ia terbaca sebagai potongan UI dari
                // game lain yang ditempel di atas kertas. Kontrasnya sama tingginya — yang
                // berubah cuma nadanya, jadi teks putih di atasnya tetap terbaca.
                _shopSlotBg[i] = MakeImage($"ShopSlot_{i}", Vector2.zero, new Vector2(ShopSlotW, ShopSlotH),
                    paper != null && !darkPanel
                        ? new Color(0.16f, 0.115f, 0.085f, 0.94f)
                        : new Color(0.13f, 0.13f, 0.18f, 0.95f), Vector2.zero);
                // Chip dari kit Panels; garis Frame lama hanya kalau art belum dipasang.
                _shopSlotsSkinned = Skin(_shopSlotBg[i], _theme != null ? _theme.CardChip : null);
                if (!_shopSlotsSkinned) Frame(_shopSlotBg[i]);
                _shopSlotBg[i].enabled = false;

                _shopSlotText[i] = MakeTmp($"ShopSlotText_{i}", Vector2.zero, new Vector2(ShopSlotW - 10, 40), 16,
                    Color.white, Vector2.zero, TextAlignmentOptions.Bottom);
                _shopSlotText[i].enabled = false;
            }

            _rerollBg = MakeImage("RerollBg", Vector2.zero, new Vector2(240, 34),
                paper != null && !darkPanel
                    ? new Color(0.26f, 0.33f, 0.18f, 0.95f)
                    : new Color(0.32f, 0.45f, 0.28f, 0.95f), Vector2.zero);
            if (!Skin(_rerollBg, _theme != null ? _theme.ButtonFrame : null))
                Frame(_rerollBg);
            _rerollBg.enabled = false;

            _rerollLabel = MakeTmp("RerollLabel", Vector2.zero, new Vector2(240, 24), 17,
                Color.white, Vector2.zero, TextAlignmentOptions.Bottom);
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

            // Lapisan art loose ikut naik, SETELAH petaknya — supaya gambar item duduk DI DEPAN
            // kotak petak, bukan tenggelam di baliknya.
            if (_artLooseLayer != null) _artLooseLayer.SetAsLastSibling();

            // Satu material dipakai BERSAMA seluruh kolam, bukan satu salinan per garis: bentuk
            // petirnya sudah dibedakan di dalam shader (beda fase per panjang garis), jadi
            // menyalin materialnya cuma memecah batch tanpa menambah satu pun perbedaan yang
            // terlihat. Kosong = garis lurus polos seperti sebelumnya — bukan garis hilang.
            var bolt = _theme != null ? _theme.EvoBoltMaterial : null;
            _evoBolt = bolt != null;

            // Lapisan cahaya outline piece terpasang — dibuat SEBELUM garis evolusi dengan
            // sengaja: busur resep adalah informasi (grup mana yang akan melebur), outline
            // cuma suasana, dan informasi harus menang saat keduanya bertumpuk. Materialnya
            // dibuat malas per KASTA bintang di OutlineMatFor; fase tiap segmen dititip
            // lewat warna vertex (lihat shader).
            _outlineShader = Shader.Find("Grimoire/UiRuneOutline");

            var outlineGo = new GameObject("RuneOutlines");
            outlineGo.transform.SetParent(_canvas.transform, false);
            _outlineLayer = outlineGo.AddComponent<RectTransform>();
            _outlineLayer.anchorMin = Vector2.zero;
            _outlineLayer.anchorMax = Vector2.one;
            _outlineLayer.offsetMin = Vector2.zero;
            _outlineLayer.offsetMax = Vector2.zero;

            _evoLines = new Image[EvoLinePool];
            for (int i = 0; i < EvoLinePool; i++)
            {
                _evoLines[i] = MakeImage($"EvoLine_{i}", Vector2.zero, new Vector2(4, 4), Color.white, Vector2.zero);
                _evoLines[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _evoLines[i].enabled = false;
                if (bolt != null) _evoLines[i].material = bolt;
            }

            // Ritual reveal evolusi — dibuat paling akhir supaya overlay-nya lahir di atas
            // segala yang sudah ada; saat main ia tetap menaikkan dirinya sendiri per Play.
            _evoFx = new EvoRevealFx(_canvas.transform);

            // Banner WAVE COMPLETE dari tema. Kosong = tanpa banner — perilaku lama.
            if (_theme != null && _theme.WaveCompletePrefab != null)
            {
                _waveBanner = Instantiate(_theme.WaveCompletePrefab, _canvas.transform);
                _waveBannerRt = _waveBanner.GetComponent<RectTransform>();

                var glow = _waveBanner.transform.Find("Glow");
                if (glow != null) _waveBannerGlow = glow.GetComponent<Image>();

                var label = _waveBanner.transform.Find("Label");
                if (label != null)
                {
                    var tmp = label.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = Loc.T("hud.wavecomplete", "WAVE COMPLETE!");
                }

                _waveBanner.SetActive(false);
            }

            RollShop();
        }

        /// <summary>Marks a piece as seen. Silent unless it is genuinely new.</summary>
        void Discover(PieceDefinition piece)
        {
            if (piece == null || _codex == null) return;
            if (!_codex.Discover(piece.Id)) return;

            PushFloater(Player.transform.position + Vector3.up * 2.6f,
                Loc.F("hud.newpiece", PieceName(piece)), new Color(0.8f, 0.95f, 1f));
        }

        void RollShop()
        {
            for (int i = 0; i < ShopSlots; i++) _shop[i] = _db.ShopRoll(_balance.ShopHighRollChance);
        }

        // Jarak antar baris daftar skill. Dibaca dari CETAKAN prefab kalau ada, supaya
        // membesarkan baris di prefab ikut menggeser ekornya ("+N" dan meter) — angka 44
        // di bawah cuma jatuhan kalau prefabnya tidak dipasang.
        float _spellRowPitch = 44f;

        void BuildSpellPanel()
        {
            _spellBg = new Image[MaxSpellRows];
            _spellFill = new Image[MaxSpellRows];
            _spellNotch = new Image[MaxSpellRows];
            _spellText = new TextMeshProUGUI[MaxSpellRows];

            if (BuildSpellPanelFromPrefab()) return;

            for (int i = 0; i < MaxSpellRows; i++)
            {
                // Row 0 is the TOP row. The list is sorted by damage, so the heaviest skill has to
                // sit where the eye lands first — the old bottom-up order buried it.
                // Dari judul ke bawah posisinya PAKU MATI: baris terpakai selalu 0..shown-1,
                // jadi tidak ada yang perlu ditata ulang saat jumlahnya berubah — cuma ekor
                // ("+N" dan meter) yang mengikuti, di DrawSpells.
                float y = SpellPanelTop - 26f - i * _spellRowPitch;
                _spellBg[i] = MakeImage($"SpellBg_{i}", new Vector2(SpellPanelRight, y),
                    new Vector2(SpellPanelW, 40), PanelInk, new Vector2(1f, 1f));
                Skin(_spellBg[i], _theme != null ? _theme.BarFrame : null, 0.92f);

                _spellFill[i] = MakeImage($"SpellFill_{i}", new Vector2(SpellPanelRight, y),
                    new Vector2(SpellPanelW, 40), new Color(0.3f, 0.3f, 0.45f, 0.55f), new Vector2(1f, 1f));
                _spellFill[i].type = Image.Type.Filled;
                _spellFill[i].fillMethod = Image.FillMethod.Horizontal;
                _spellFill[i].fillOrigin = 0;

                _spellText[i] = MakeTmp($"SpellText_{i}", new Vector2(SpellPanelRight - 8, y - 2),
                    new Vector2(SpellPanelW - 10, 36), 17, TextBone, new Vector2(1f, 1f), TextAlignmentOptions.MidlineRight);

                // Takik warna piece di bibir kiri baris — dibuat SETELAH teks supaya tergambar
                // paling atas. Warnanya diisi DrawSpells dari warna piece yang menempati baris.
                _spellNotch[i] = MakeImage($"SpellNotch_{i}",
                    new Vector2(SpellPanelRight - (SpellPanelW - 3), y),
                    new Vector2(3, 40), Color.clear, new Vector2(1f, 1f));
            }

            // Judul panel DIBUANG atas permintaan pemilik project — label kecil di pojok
            // cuma terbaca sebagai kotoran layar.

            // Baris kolaps: lima teratas (urut damage) sudah menceritakan build-nya; sisanya
            // cukup diakui jumlahnya. Menampilkan SEMUA baris adalah alasan panel lama memanjang
            // sampai apa pun yang duduk di dekatnya tertimpa.
            _spellMore = MakeTmp("SpellMore", new Vector2(SpellPanelRight - 8, SpellPanelTop - 26f),
                new Vector2(SpellPanelW, 22), 16, TextDim, new Vector2(1f, 1f), TextAlignmentOptions.MidlineRight);
            _spellMore.text = "";
        }

        /// <summary>
        /// Daftar skill dari CETAKAN prefab: satu baris ditata tangan, kode meng-clone-nya
        /// sebanyak baris yang disediakan. Rupa, ukuran, font, warna dasar, dan susunan anak
        /// (latar → isian → teks → takik) semuanya milik prefab; kode cuma mengisi teks,
        /// panjang isian, warna takik, dan menyalakan baris yang terpakai.
        ///
        /// Urutan anak di dalam cetakan WAJIB dipertahankan: takik sengaja lahir terakhir
        /// supaya tergambar paling atas — di UGUI urutan anak adalah urutan gambar.
        /// </summary>
        bool BuildSpellPanelFromPrefab()
        {
            var prefab = _theme != null ? _theme.SpellPanelPrefab : null;
            if (prefab == null) return false;

            var panel = Instantiate(prefab, _canvas.transform, false);
            panel.name = "SpellPanel";

            // Dicari sampai ke dalam, bukan cuma anak langsung — cetakannya boleh kamu
            // pindah-pindah ke wadah mana pun di dalam prefab.
            var template = FindDeep(panel.transform, "RowTemplate") as RectTransform;
            if (template == null)
            {
                Debug.LogError("[GrimoireUI] SpellPanelPrefab butuh anak 'RowTemplate'.", panel);
                Destroy(panel);
                return false;
            }

            // Keempat anak diperiksa DI CETAKANNYA, sebelum satu pun baris di-clone: cetakan
            // yang bagiannya kurang (mis. 'Text' masih UI Text legacy, bukan TMP) dulu lolos
            // sampai runtime dan meledak sebagai NullReference TIAP FRAME di DrawSpells —
            // membekukan seluruh HUD, termasuk peta. Sekarang ia gagal SEKALI, bersuara, dan
            // jatuh ke panel hitungan kode yang tetap berfungsi.
            if (FindPart<Image>(template, "Bg") == null ||
                FindPart<Image>(template, "Fill") == null ||
                FindPart<TextMeshProUGUI>(template, "Text") == null ||
                FindPart<Image>(template, "Notch") == null)
            {
                Debug.LogError("[GrimoireUI] SpellPanelPrefab: 'RowTemplate' butuh anak 'Bg' " +
                               "(Image), 'Fill' (Image), 'Text' (teks TMP), dan 'Notch' (Image) " +
                               "— kembali ke panel hitungan kode.", panel);
                Destroy(panel);
                return false;
            }

            var stack = template.parent as RectTransform;
            var group = stack != null ? stack.GetComponent<VerticalLayoutGroup>() : null;
            _spellRowPitch = template.rect.height + (group != null ? group.spacing : 4f);

            for (int i = 0; i < MaxSpellRows; i++)
            {
                var row = Instantiate(template.gameObject, template.parent, false);
                row.name = $"SpellRow_{i}";
                row.SetActive(true);

                _spellBg[i] = FindPart<Image>(row.transform, "Bg");
                _spellFill[i] = FindPart<Image>(row.transform, "Fill");
                _spellNotch[i] = FindPart<Image>(row.transform, "Notch");
                _spellText[i] = FindPart<TextMeshProUGUI>(row.transform, "Text");

                if (_spellFill[i] != null)
                {
                    _spellFill[i].type = Image.Type.Filled;
                    _spellFill[i].fillMethod = Image.FillMethod.Horizontal;
                    _spellFill[i].fillOrigin = 0;
                }
            }

            template.gameObject.SetActive(false);

            // Ekor "+N" tetap kode: posisinya dihitung ulang mengikuti berapa baris yang
            // sedang tampil. (Judul panel DIBUANG atas permintaan pemilik project.)
            _spellMore = MakeTmp("SpellMore", new Vector2(SpellPanelRight - 8, SpellPanelTop - 26f),
                new Vector2(SpellPanelW, 22), 16, TextDim, new Vector2(1f, 1f), TextAlignmentOptions.MidlineRight);
            _spellMore.text = "";
            return true;
        }

        static T FindPart<T>(Transform row, string name) where T : Component
        {
            var t = FindDeep(row, name);
            return t != null ? t.GetComponent<T>() : null;
        }

        /// <summary>Cari anak bernama <paramref name="name"/> di kedalaman berapa pun.</summary>
        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }

            return null;
        }

        // Deret kecepatan versi PREFAB: keadaan terpilih hidup sebagai anak Idle/Active di
        // cetakan, jadi kode tidak pernah menulis sprite, warna, posisi, atau ukuran apa pun.
        GameObject[] _speedIdle;
        GameObject[] _speedActive;
        bool _speedFromPrefab;

        void BuildSpeedControl()
        {
            _speedButtons = new Image[Speeds.Length];
            _speedLabels = new TextMeshProUGUI[Speeds.Length];

            if (BuildSpeedBarFromPrefab())
            {
                BuildTimeControl();
                return;
            }

            for (int i = 0; i < Speeds.Length; i++)
            {
                float x = -(Margin + (Speeds.Length - 1 - i) * (SpeedButtonW + 6));
                var pos = new Vector2(x, -Margin);

                _speedButtons[i] = MakeImage($"Speed_{i}", pos, new Vector2(SpeedButtonW, SpeedButtonH),
                    PanelInk, new Vector2(1f, 1f));
                if (!Skin(_speedButtons[i], _theme != null ? _theme.ButtonFrame : null))
                    Frame(_speedButtons[i]);

                _speedLabels[i] = MakeTmp($"SpeedLabel_{i}", pos + new Vector2(0, -7),
                    new Vector2(SpeedButtonW, SpeedButtonH), 15, TextBone,
                    new Vector2(1f, 1f), TextAlignmentOptions.Top);
                _speedLabels[i].text = SpeedLabels[i];
            }

            BuildTimeControl();
        }

        /// <summary>
        /// Deret kecepatan dari cetakan prefab. Kode TIDAK menyentuh RectTransform apa pun:
        /// letak bar dan ukuran tombol milik prefab, jaraknya milik layout group di dalamnya.
        /// Kliknya jadi tombol UGUI beneran — kalau tidak, tes klik lama (SpeedRect, kotak
        /// layar hasil hitungan konstanta) akan meleset begitu tombolnya dipindah tangan.
        /// </summary>
        bool BuildSpeedBarFromPrefab()
        {
            var prefab = _theme != null ? _theme.SpeedBarPrefab : null;
            if (prefab == null) return false;

            var bar = Instantiate(prefab, _canvas.transform, false);
            bar.name = "SpeedBar";

            var template = bar.transform.Find("ButtonTemplate");
            if (template == null)
            {
                Debug.LogError("[GrimoireUI] SpeedBarPrefab tidak punya anak 'ButtonTemplate'.", bar);
                Destroy(bar);
                return false;
            }

            _speedIdle = new GameObject[Speeds.Length];
            _speedActive = new GameObject[Speeds.Length];

            for (int i = 0; i < Speeds.Length; i++)
            {
                // Tombol NYATA di prefab menang. Dulu SATU template di-clone empat kali di sini,
                // dan itu setengah dari keluhan "tombolnya balik lagi": tiga tombol yang ditata
                // tangan di prefab tidak pernah dibaca sama sekali. (Setengahnya lagi
                // HorizontalLayoutGroup di root, yang menata ulang anak tiap frame — sudah
                // dibuang dari prefabnya.) Clone tinggal jadi jaring untuk prefab lama.
                var pre = bar.transform.Find($"Speed_{i}");
                var clone = pre != null ? pre.gameObject
                    : Instantiate(template.gameObject, template.parent, false);
                clone.name = $"Speed_{i}";
                clone.SetActive(true);

                var idle = clone.transform.Find("Idle");
                var active = clone.transform.Find("Active");
                _speedIdle[i] = idle != null ? idle.gameObject : null;
                _speedActive[i] = active != null ? active.gameObject : null;

                var label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = SpeedLabels[i];
                _speedLabels[i] = label;

                var image = idle != null ? idle.GetComponent<Image>() : clone.GetComponent<Image>();
                _speedButtons[i] = image;

                var button = clone.GetComponent<Button>() ?? clone.AddComponent<Button>();
                button.targetGraphic = image;
                int slot = i;
                button.onClick.AddListener(() => SetSpeed(slot));
            }

            template.gameObject.SetActive(false);
            _speedFromPrefab = true;
            return true;
        }

        Image _timeButton;
        TextMeshProUGUI _timeLabel;

        /// <summary>
        /// Tombol siang/malam. Alat DEBUG — DISEMBUNYIKAN atas permintaan pemilik project
        /// ("itu BTN DEBUG, hide"). Kodenya dibiarkan utuh: balikkan saklar di bawah ke true
        /// untuk memunculkannya lagi. Jalur kliknya sudah berpagar <c>_timeButton != null</c>,
        /// jadi tanpa tombol tidak ada klik hantu; DemoBar tetap bisa mengganti wajah arena.
        /// </summary>
        static readonly bool ShowTimeDebugButton = false;

        void BuildTimeControl()
        {
            if (!ShowTimeDebugButton) return;
            if (_biome == null || _biome.Faces < 2) return;

            // 30 menyamai TimeButtonRect — teks petunjuk kecepatan hidup di celah ini.
            float y = -(Margin + SpeedButtonH + 30);
            float width = Speeds.Length * SpeedButtonW + (Speeds.Length - 1) * 6;
            var pos = new Vector2(-Margin, y);

            _timeButton = MakeImage("TimeToggle", pos, new Vector2(width, SpeedButtonH),
                PanelInk, new Vector2(1f, 1f));
            if (!Skin(_timeButton, _theme != null ? _theme.ButtonFrame : null))
                Frame(_timeButton);

            _timeLabel = MakeTmp("TimeToggleLabel", pos + new Vector2(0, -7),
                new Vector2(width, SpeedButtonH), 15, TextGold,
                new Vector2(1f, 1f), TextAlignmentOptions.Top);

            RefreshTimeLabel();
        }

        void RefreshTimeLabel()
        {
            if (_timeLabel == null || _biome == null) return;

            var face = _biome.Current;
            _timeLabel.text = Loc.F("hud.time.suffix", face != null ? face.DisplayName : "?");
        }

        void ToggleTime()
        {
            if (_biome == null || _biome.Faces < 2) return;

            _biome.Show(_biome.FaceIndex + 1);
            RefreshTimeLabel();
        }

        // Bar HP boss selebar layar DICABUT atas perintah pemilik project: "hp bar bos cabut,
        // buat dia punya hp kaya yg lain di atas kepalanya kalo ke-hit".
        //
        // Penggantinya ada di EnemyHpBars — satu palang di atas KEPALA ular, muncul saat kena
        // dan diam di sisa waktunya, aturan yang sama persis dengan seluruh musuh lain. Yang
        // hilang bersamanya: nama boss dan angka persen. Keduanya keterangan panggung, bukan
        // jawaban atas pertanyaan yang sedang ditanyakan pemain di tengah perkelahian —
        // "berapa pukulan lagi" — dan palang di atas kepala menjawab itu di tempat mata
        // pemain sudah berada.
        //
        // Kotak `CombatHudRig.BossBar` di prefab dibiarkan: ia tidak lagi diisi apa pun, dan
        // membuangnya berarti menyentuh prefab HUD milik pekerjaan yang sedang berjalan.

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

            // Menunjuk bagian MANA PUN dari bola = menunjuk bolanya: seluruh pohon kotak
            // daerah hover ikut diuji. Di prefab sekarang art bolanya (189px) memang lebih
            // besar dan bergeser dari kotak hover aslinya (110px) — hover yang cuma
            // menyala di kotak kecil itulah yang dilaporkan "susah banget".
            _hpHoverRects = _hpHover != null
                ? _hpHover.GetComponentsInChildren<RectTransform>(true)
                : System.Array.Empty<RectTransform>();
            _manaHoverRects = _manaHover != null
                ? _manaHover.GetComponentsInChildren<RectTransform>(true)
                : System.Array.Empty<RectTransform>();

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

        CombatHudRig _hudRig;

        // Payung UI combat: SATU prefab berisi semua bagian HUD sebagai barang beneran.
        // Semua pembangun mencari bagiannya di sini lebih dulu.
        Transform _combatUi;

        /// <summary>
        /// Memasang prefab payung <see cref="UiTheme.CombatUiPrefab"/> sekali, paling awal.
        /// Bagian apa pun yang ada di dalamnya (VitalsRig, StatusStripRig, ShopRig,
        /// CombatHudRig, "SpeedBar", "SpellPanel", "TooltipCard") dipakai langsung — slot
        /// per-bagian di tema tinggal cadangan.
        /// </summary>
        void AttachCombatUi()
        {
            var prefab = _theme != null ? _theme.CombatUiPrefab : null;
            if (prefab == null) return;

            var go = Instantiate(prefab, _canvas.transform, false);
            go.name = "CombatUI";
            _combatUi = go.transform;

            // Rig HUD ikut diambil sekarang juga: pembangun paling awal (tas, tombol TOKO)
            // sudah butuh tahu bagian mana yang diambil alih prefab.
            AttachCombatHudRig();
        }

        /// <summary>Bagian rig dianggap dipakai kalau terisi DAN objeknya aktif.</summary>
        static bool PartOn(Component part) => part != null && part.gameObject.activeSelf;

        /// <summary>
        /// Rig HUD combat berisi BARANG BENERAN (Image ber-sprite, teks TMP): bagian yang
        /// terisi dipakai langsung oleh kode — sprite, font, warna, dan letaknya milik prefab.
        /// </summary>
        CombatHudRig AttachCombatHudRig()
        {
            if (_hudRig != null) return _hudRig;

            _hudRig = _combatUi != null
                ? _combatUi.GetComponentInChildren<CombatHudRig>(true)
                : null;

            if (_hudRig == null)
            {
                var prefab = _theme != null ? _theme.CombatHudPrefab : null;
                if (prefab == null) return null;

                var go = Instantiate(prefab, _canvas.transform, false);
                go.name = "CombatHudRig";
                _hudRig = go.GetComponent<CombatHudRig>();

                if (_hudRig == null)
                {
                    Debug.LogError("[GrimoireUI] CombatHudPrefab tidak membawa CombatHudRig.", go);
                    Destroy(go);
                    return null;
                }
            }

            Canvas.ForceUpdateCanvases();
            return _hudRig;
        }

        // Alas panel tas — punya kode ATAU adopsi dari rig; dipegang untuk pelukan lebar.
        Image _bagFrame;

        /// <summary>
        /// Penutup pemasangan HUD: hit-test tombol diarahkan ke barang rig yang diadopsi, dan
        /// seluruh rig diangkat ke atas tumpukan gambar supaya tombol LANJUT tidak tenggelam
        /// di bawah panel toko yang lahir lebih akhir.
        /// </summary>
        void ApplyCombatHudSeats()
        {
            var rig = _hudRig;
            if (rig == null) return;

            Canvas.ForceUpdateCanvases();

            // Urutan anak = urutan gambar. Rig lahir paling awal (payung dipasang pertama),
            // padahal isinya barang era-BuildHud — diangkat ke pucuk kanvas, tepat sebelum
            // panel resep dibuat.
            rig.transform.SetParent(_canvas.transform, true);
            rig.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();

            RefreshHudSeats();
        }

        /// <summary>
        /// Kotak klik tombol rig, DISEGARKAN TIAP FRAME — bukan dipatok sekali saat build.
        /// Nilai yang di-cache membeku basi (prefab digeser, sesi lain menimpa aset, urutan
        /// build berubah), dan akibatnya GANDA: tombol pojok tidak bisa diklik, sementara
        /// rect default lama di TENGAH layar diam-diam menelan klik — persis di badan panel
        /// toko, sehingga "beli sekali, meninya hilang" karena kliknya terbaca BERANGKAT.
        /// </summary>
        void RefreshHudSeats()
        {
            var rig = _hudRig;
            if (rig == null) return;

            if (PartOn(rig.StartButton))
                StartButtonOverride = CanvasRectOf(rig.StartButton.rectTransform);
            else if (rig.StartButton != null)
                // Tombolnya DIMATIKAN user: kotak kliknya ikut mati — tanpa ini, klik di
                // rect hitungan lama tetap memberangkatkan wave dari tombol yang tak terlihat.
                StartButtonOverride = new Rect(0f, 0f, 0f, 0f);

            if (PartOn(rig.ShopToggle))
                ShopButtonOverride = CanvasRectOf(rig.ShopToggle.rectTransform);
            else if (rig.ShopToggle != null)
                ShopButtonOverride = new Rect(0f, 0f, 0f, 0f);

            // Kotak tas: selama kotaknya hidup di prefab, petak + alas + hit-test drag duduk
            // di kotak itu — digeser di prefab, tasnya ikut FRAME INI JUGA. Un-check kotaknya
            // = kembali ke rumus kolom kanan papan.
            if (rig.BagArea != null && rig.BagArea.gameObject.activeInHierarchy)
            {
                var was = GrimoireLayout.BagAreaOverride;
                GrimoireLayout.BagAreaOverride = CanvasRectOf(rig.BagArea);
                if (was != GrimoireLayout.BagAreaOverride) ReseatBag();
            }
            else if (GrimoireLayout.BagAreaOverride != null)
            {
                GrimoireLayout.BagAreaOverride = null;
                ReseatBag();
            }
        }

        void BuildHud()
        {
            // Plakat di belakang baris wave — teks putih telanjang di atas rumput terbaca
            // sebagai overlay debug, bukan HUD. Lebarnya dipaskan tiap frame di DrawHud
            // karena kalimatnya berubah-ubah ("HABISKAN SISANYA" jauh lebih panjang).
            var hudRig = AttachCombatHudRig();

            // ADOPSI, bukan duplikasi: kalau prefab membawa barangnya, itu yang dipakai —
            // sprite, font, warna, letak, semua milik prefab. Kode cuma menulis teksnya.
            // TERISI = milik prefab, titik — termasuk saat objeknya DINONAKTIFKAN: un-check
            // di prefab berarti "bagian ini hilang", bukan "kembalikan versi gambar-kode".
            // Fallback kode hanya untuk slot yang benar-benar kosong (prefab belum lengkap).
            if (hudRig != null && hudRig.HudPlaque != null)
            {
                _hudPlaque = hudRig.HudPlaque;

                // Ukuran plakat jadi milik PREFAB, bukan kode. Selama ini Redraw menimpanya tiap
                // frame dengan (lebar teks + 24) x 36 — tinggi 36 itu DIPAKU, jadi berapa pun yang
                // disetel tangan di prefab akan menyusut balik ke 36 sebelum sempat terlihat, dan
                // tidak ada satu pun pesan yang menjelaskan kenapa.
                _hudPlaqueOwnsSize = true;
            }
            else
            {
                _hudPlaque = MakeImage("HudPlaque", new Vector2(Margin - 12, -(Margin - 8)),
                    new Vector2(430, 36), PanelInk, new Vector2(0f, 1f));
                if (!Skin(_hudPlaque, _theme != null ? _theme.Plaque : null))
                    Frame(_hudPlaque);
            }

            if (hudRig != null && hudRig.HudLine != null)
            {
                _hudText = hudRig.HudLine;

                // Plakatnya sekarang BERUKURAN TETAP (milik prefab), jadi kalimatnya yang harus
                // muat — bukan sebaliknya. Tanpa ini, baris terpanjang tumpah lewat tepi kanan
                // plakat dan ekornya ("0 coins") terpotong; itu persis regresi yang lahir begitu
                // plakat berhenti melebar mengikuti teks.
                //
                // Ukuran yang disetel tangan jadi BATAS ATAS, bukan digeser: kalimat yang sudah
                // muat tidak pernah berubah sedikit pun, dan hanya yang kepanjangan yang menyusut.
                // Turun paling banyak ke 72% — di bawah itu ia berhenti terbaca, dan teks yang
                // muat tapi tak terbaca sama saja dengan tidak muat.
                _hudText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                _hudText.overflowMode = TextOverflowModes.Overflow;
                _hudText.enableAutoSizing = true;
                _hudText.fontSizeMax = _hudText.fontSize;
                _hudText.fontSizeMin = Mathf.Max(8f, _hudText.fontSize * 0.72f);
            }
            else
            {
                _hudText = MakeTmp("Hud", new Vector2(Margin, -Margin), new Vector2(600, 26), 17,
                    TextBone, new Vector2(0f, 1f), TextAlignmentOptions.TopLeft);
            }

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
                _hpLabel = MakeTmp("HpLabel", new Vector2(Margin + 6, -51), new Vector2(250, 20), 15,
                    Color.white, new Vector2(0f, 1f), TextAlignmentOptions.TopLeft);

                _manaBg = MakeImage("ManaBg", new Vector2(Margin, -72), new Vector2(260, 18),
                    new Color(0.08f, 0.09f, 0.16f, 0.9f), new Vector2(0f, 1f));
                _manaFill = MakeImage("ManaFill", new Vector2(Margin, -72), new Vector2(260, 18),
                    new Color(0.35f, 0.6f, 1f, 0.95f), new Vector2(0f, 1f));
                _manaFill.type = Image.Type.Filled;
                _manaFill.fillMethod = Image.FillMethod.Horizontal;
                _manaFill.fillOrigin = 0;
                _manaLabel = MakeTmp("ManaLabel", new Vector2(Margin + 6, -73), new Vector2(250, 20), 15,
                    Color.white, new Vector2(0f, 1f), TextAlignmentOptions.TopLeft);

                _hpHover = _hpFill.rectTransform;
                _manaHover = _manaFill.rectTransform;
            }

            if (!BuildTooltipFromPrefab())
            {
                _tipBg = MakeImage("TipBg", Vector2.zero, new Vector2(_tipWidth, 150),
                    new Color(0.06f, 0.06f, 0.09f, 0.96f), Vector2.zero);
                _tipBg.rectTransform.pivot = new Vector2(0f, 1f);
                _tipBg.enabled = false;

                // Bingkai kit UI kalau temanya membawa — kartu info memakai bahasa panel yang
                // sama dengan halaman menu, bukan kotak gelap telanjang. Sliced: tingginya
                // berubah mengikuti isi, ornamen sudutnya tidak boleh ikut molor.
                if (_theme != null && _theme.InfoPanel != null)
                {
                    _tipBg.sprite = _theme.InfoPanel;
                    _tipBg.type = Image.Type.Sliced;
                    _tipBg.color = Color.white;
                }

                _tipText = MakeTmp("TipText", Vector2.zero,
                    new Vector2(_tipWidth - _tipPadX * 2f, 140), 22,
                    new Color(0.92f, 0.92f, 0.96f), Vector2.zero, TextAlignmentOptions.TopLeft);
                _tipText.rectTransform.pivot = new Vector2(0f, 1f);
            }

            // TMP membungkus dan membaca rich text secara bawaan — kartu ini satu-satunya teks
            // panjang di HUD, dan blurb yang meluber keluar kotak itulah alasan pindah ke wrap.
            _tipText.enabled = false;
            if (_tipBg != null) _tipBg.enabled = false;

            _bannerText = MakeTmp("Banner", new Vector2(0, 210), new Vector2(900, 100), 28,
                Color.white, new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center);
            _bannerText.text = "";

            // Banner melayang tanpa panel di atas arena yang bisa sangat terang — outline SDF
            // tipis ini yang membuatnya tetap terbaca di rumput hijau muda. (Komponen Shadow
            // legacy tidak berpengaruh pada TMP; outline di material penggantinya.)
            _bannerText.fontSharedMaterial = new Material(_bannerText.fontSharedMaterial);
            _bannerText.fontSharedMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.18f);
            _bannerText.fontSharedMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.8f));

            if (_hudRig != null && _hudRig.StartButton != null)
            {
                // Tombol dari prefab: badan dan labelnya barang beneran milik tangan user.
                // Objek nonaktif tetap DIADOPSI — ia tinggal tidak terlihat dan tidak bisa
                // diklik, bukan digantikan tombol gambar-kode di tengah layar.
                _startBg = _hudRig.StartButton;
                _startLabel = _hudRig.StartLabel;

                // Prefab boleh membawa badan tanpa label — label darurat ditempel MENJADI ANAK
                // badannya, supaya ikut ke mana pun kotaknya digeser. DrawBanner menulis
                // .enabled dan .text ke sini tanpa periksa null, jadi slot kosong bukan pilihan.
                if (_startLabel == null)
                {
                    _startLabel = MakeTmp("StartLabel", Vector2.zero,
                        new Vector2(StartButtonW, StartButtonH), 20, TextGold,
                        new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center);
                    _startLabel.rectTransform.SetParent(_startBg.rectTransform, false);
                    _startLabel.rectTransform.anchorMin = Vector2.zero;
                    _startLabel.rectTransform.anchorMax = Vector2.one;
                    _startLabel.rectTransform.offsetMin = Vector2.zero;
                    _startLabel.rectTransform.offsetMax = Vector2.zero;
                }

                _startLabel.text = Loc.T("hud.start.wave");
            }
            else
            {
                // Cadangan tanpa prefab pun tetap di POJOK KANAN-BAWAH, gaya baris menu.
                // Tombol tengah layar yang lama DIBUANG atas perintah pemilik project
                // ("ada button ketinggalan dan harus dibuang") — dialah sumber klik nyasar
                // yang menutup toko dan membuka peta dari tengah layar.
                var seat = StartButtonRect();
                _startBg = MakeImage("StartBg", new Vector2(seat.xMin, seat.yMin), seat.size,
                    new Color(0f, 0f, 0f, 0f), Vector2.zero);

                _startLabel = MakeTmp("StartLabel", new Vector2(seat.xMin, seat.yMin),
                    seat.size, 30, Color.white, Vector2.zero, TextAlignmentOptions.MidlineRight);
                _startLabel.text = Loc.T("hud.start.wave");
            }
        }

        void BuildMeter()
        {
            // BARIS METER DICABUT atas permintaan pemilik project ("gak ada text lagi info dmg").
            // Ia dulu menempel di bawah "+N more" dan mengulang rincian damage yang sudah dibawa
            // baris-baris spell di atasnya. DamageMeter sendiri TETAP HIDUP - ia yang mengurutkan
            // baris spell dan menghitung persentasenya (DealtBy / ShareOf); yang dibuang hanya
            // teksnya di layar.

            BuildStripAnchors();

            _buffStrip = new StatusStrip(_canvas.transform, TmpFont, 6, StripIcon,
                new Color(1f, 0.92f, 0.55f));

            _debuffStrip = new StatusStrip(_canvas.transform, TmpFont, 4, StripIcon,
                new Color(1f, 0.5f, 0.5f));

            _ailmentStrip = new StatusStrip(_canvas.transform, TmpFont, 8, StripIcon,
                new Color(0.85f, 0.88f, 0.95f));

            // Kapasitas 12: katalognya 22, tapi node kejadian datang beberapa kali per act dan
            // pakta tidak pernah bisa diambil dua kali. Dua belas adalah run yang sangat panjang.
            // DISAMAKAN dengan buff & kutukan — permintaan pemilik project: ukuran ikon,
            // jarak slot, dan arah tumbuh persis sama. Yang membedakan pakta tinggal
            // warnanya dan barisnya sendiri di paling bawah.
            // Ikon pakta SATU TINGKAT LEBIH BESAR dari buff/debuff biasa - permintaan pemilik
            // project: pakta itu aturan dunia sepanjang run, bukan efek lewat.
            _pactStrip = new StatusStrip(_canvas.transform, TmpFont, 12, StripIcon * 1.35f,
                new Color(1f, 0.82f, 0.4f));
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

            // KIRI, di bawah ketiga strip — dikumpulkan jadi satu keluarga bacaan di samping
            // bar mana, permintaan pemilik project. Yang membedakan pakta cukup ukuran ikonnya
            // dan arah tumbuhnya yang menurun. Kotak PactArea di prefab yang berkuasa; angka
            // ini cuma cadangan saat prefabnya absen.
            _pactOrigin = new Vector2(Margin, StripPactY);

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
            }

            // Dua baris keterangan (info piece dipegang + pesan evolusi) — BAWAH-TENGAH layar.
            // Sejarahnya pindah tiga kali: kolom kiri (ketutup strip), atas mata grimoire
            // (118 → 154 → 190, tetap menimpa arena gelap dan "gak kebaca" — screenshot
            // 2026-08-19), dan sekarang bawah-tengah atas tunjuk tangan pemilik project:
            // jalur itu selalu kosong — LANJUT di kanan, tas di kiri, keduanya tidak pernah
            // sampai ke tengah. Anchor teks (0,1) = y-negatif-dari-atas, konversi lewat ScreenH.
            {
                float cx = ScreenW * 0.5f - 440f;                     // lebar teks 880, pivot kiri

                if (_heldText != null)
                    _heldText.rectTransform.anchoredPosition = new Vector2(cx, 156f - ScreenH);

                if (_evolveText != null)
                    _evolveText.rectTransform.anchoredPosition = new Vector2(cx, 122f - ScreenH);
            }
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
            // Buff KITA menetap (redup saat jeda). Debuff musuh ke kita tetap lenyap saat
            // habis - dialah satu-satunya yang memang harus hilang, perintah pemilik project.
            PushSlots(_buffStrip, Player.Buffs, _buffOrigin, _seenBuffs);
            PushSlots(_debuffStrip, Player.Debuffs, _debuffOrigin);

            _ailmentStrip.Begin(_ailmentOrigin);

            var counts = Enemies.StatusCounts;
            for (int i = 0; i < _db.Statuses.Count && i < counts.Length; i++)
            {
                var status = _db.Statuses[i];
                if (status == null) continue;

                // Ailment KITA menetap begitu pernah menempel: build pemain tetap terbaca
                // walau lapangan sedang bersih - redup saat nol, terang plus angka saat jalan.
                bool active = counts[i] > 0;
                if (active) _seenAilments.Add(i);
                else if (!_seenAilments.Contains(i)) continue;

                _ailmentStrip.Push(status.Icon, status.Color, active ? counts[i].ToString() : "",
                    Loc.F("hud.ailment.tip", StatusName(status), counts[i], status.Blurb),
                    dim: !active);
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
                _sb.Append(Loc.F("hud.pact.tip", PactName(p))).Append('\n');
                if (!string.IsNullOrEmpty(p.BoonText)) _sb.Append("+ ").Append(PactBoon(p)).Append('\n');
                if (!string.IsNullOrEmpty(p.BaneText)) _sb.Append("- ").Append(PactBane(p));

                // Tanpa angka: pakta tidak menghitung mundur, dan kotak angka kosong di sebelah
                // tiap ikon cuma melebarkan kolomnya ke dalam layar.
                _pactStrip.Push(p.Icon, p.Color, "", _sb.ToString());
            }

            _pactStrip.Apply();
        }

        void PushSlots(StatusStrip strip, PlayerCaster.BuffSlot[] slots, Vector2 origin,
            List<BuffDefinition> retain = null)
        {
            strip.Begin(origin);

            // Daftar menetap diisi dulu dari slot yang sedang hidup, supaya entri baru langsung
            // punya kursi tetap dan urutan strip tidak melompat-lompat antar frame.
            if (retain != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var def = slots[i].Def;
                    if (def != null && !retain.Contains(def)) retain.Add(def);
                }

                for (int r = 0; r < retain.Count; r++)
                {
                    var def = retain[r];

                    int slot = -1;
                    for (int i = 0; i < slots.Length; i++)
                        if (slots[i].Def == def) { slot = i; break; }

                    if (slot < 0)
                    {
                        // Menetap tapi sedang jeda: redup, tanpa angka. Terang lagi begitu jalan.
                        strip.Push(def.Icon, def.Color, "",
                            BuffName(def) + "\n" + _tooltips.DescribeMods(def), dim: true);
                        continue;
                    }

                    int stacks = Mathf.Max(1, slots[slot].Stacks);
                    string label = def.IsCharge ? stacks + "x" : slots[slot].Remaining.ToString("0.0");

                    strip.Push(def.Icon, def.Color, label,
                        BuffName(def) + (def.IsCharge ? "  -  " + Loc.F("hud.buff.charge", stacks, def.MaxStacks) : "")
                        + "  -  " + slots[slot].Remaining.ToString("0.0") + "s\n" +
                        _tooltips.DescribeMods(def));
                }

                strip.Apply();
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                var def = slots[i].Def;
                if (def == null) continue;

                // A charge's number is how many you are holding — that is the thing you are
                // watching. A plain buff's number is how long you have left.
                int stacks = Mathf.Max(1, slots[i].Stacks);
                string label = def.IsCharge ? stacks + "x" : slots[i].Remaining.ToString("0.0");

                strip.Push(def.Icon, def.Color, label,
                    BuffName(def) + (def.IsCharge ? "  -  " + Loc.F("hud.buff.charge", stacks, def.MaxStacks) : "")
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
            if (VitalHit(_hpHoverRects, _hpHover, mouse))
                return VitalsCard(Loc.T("vitals.hp"), Player.Hp, Player.MaxHp, Player.HpRegen);

            if (VitalHit(_manaHoverRects, _manaHover, mouse))
                return VitalsCard(Loc.T("vitals.mana"), Player.Mana, Player.MaxMana, Player.ManaRegen);

            return null;
        }

        /// <summary>
        /// Uji hover sebuah bola: SEMUA kotak di pohonnya dihitung, bukan cuma kotak hover
        /// yang ditunjuk rig — pemain menunjuk BOLA yang dilihat matanya, bukan kotak
        /// tersembunyi di baliknya. Dua pohon yang bertumpang tindih dimenangkan yang diuji
        /// lebih dulu (HP), dan itu jauh lebih baik daripada hover yang tidak menyala.
        /// </summary>
        static bool VitalHit(RectTransform[] rects, RectTransform single, Vector2 mouse)
        {
            if (rects != null && rects.Length > 0)
            {
                for (int i = 0; i < rects.Length; i++)
                {
                    var r = rects[i];
                    if (r != null && r.gameObject.activeInHierarchy && HoverHits(r, mouse))
                        return true;
                }

                return false;
            }

            return HoverHits(single, mouse);
        }

        /// <summary>
        /// Kanvas ini Overlay, jadi kameranya null — memberikan kamera arena di sini membuat
        /// setiap pengujian meleset sejauh perbedaan proyeksinya.
        /// </summary>
        // Mouse yang diterima SATUAN KANVAS (sudah dibagi UiScale), sementara
        // RectangleContainsScreenPoint minta PIKSEL layar mentah — dikali balik di sini.
        // Tanpa ini hover bola HP/mana cuma kebetulan benar saat jendela game persis
        // selebar referensi (skala 1), dan "hilang" begitu jendelanya lebih kecil.
        static bool HoverHits(RectTransform area, Vector2 mouse) =>
            area != null && area.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(
                area, mouse * GrimoireLayout.UiScale, null);

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
                _sb.Append(Loc.F("hud.regen", regen.ToString("0.#")));

            return _sb.ToString();
        }

        /// <summary>Description of whichever strip icon the cursor is over, or null.</summary>
        string StripTooltip(Vector2 mouse) =>
            _buffStrip.TooltipAt(mouse) ?? _debuffStrip.TooltipAt(mouse) ??
            _ailmentStrip.TooltipAt(mouse) ?? _pactStrip.TooltipAt(mouse);

        void BuildFloaters()
        {
            _floaters = new TextMeshProUGUI[FloatPoolSize];
            _floatLife = new float[FloatPoolSize];
            _floatWorld = new Vector3[FloatPoolSize];
            _floatMax = new float[FloatPoolSize];
            _floatScale = new float[FloatPoolSize];

            // Outline hitam — nama reaksi tampil BESAR di atas ledakan warna-warni miliknya
            // sendiri, dan huruf polos tenggelam persis di momen yang paling harus terbaca.
            // SATU material bersama untuk seluruh kolam, alasan yang sama dengan angka damage:
            // outline per label = satu material instance per label = batching pecah.
            Material floatInk = null;

            for (int i = 0; i < FloatPoolSize; i++)
            {
                _floaters[i] = MakeTmp($"Float_{i}", Vector2.zero, new Vector2(420, 60), 20,
                    Color.white, Vector2.zero, TextAlignmentOptions.Center);
                _floaters[i].text = "";

                // Font ANGKA (Cinzel), bukan font badan: floater sekelas dengan popup damage —
                // pengumuman di atas medan tempur, dan keduanya harus bicara dengan satu huruf
                // (permintaan pemilik project: "text popup dmg dan reaction diganti fontnya").
                if (_numberFont != null) _floaters[i].font = _numberFont;

                if (floatInk == null)
                {
                    floatInk = new Material(_floaters[i].fontSharedMaterial);
                    // FaceDilate menebalkan badan huruf Cinzel yang serif-nya tipis; outline
                    // pekat penuh. Angkanya senada dengan popup damage — dan sama-sama baru
                    // hidup setelah padding atlas Cinzel dinaikkan 5 -> 16 (lihat catatan
                    // panjang di DamagePopups).
                    floatInk.SetFloat(ShaderUtilities.ID_FaceDilate, 0.2f);
                    floatInk.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
                    floatInk.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
                    floatInk.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 1f));
                }

                _floaters[i].fontSharedMaterial = floatInk;
            }
        }

        /// <summary>
        /// Nama piece yang DIGAMBAR. Nilai di asetnya dipakai sebagai cadangan, jadi piece yang
        /// belum punya kunci tetap tampil apa adanya alih-alih jadi kunci mentah di tengah papan.
        ///
        /// Yang dibaca di sini SELALU nama tampilan. Tempat lain yang memakai
        /// <see cref="PieceDefinition.DisplayName"/> sebagai KUNCI - pencarian VFX hit, misalnya -
        /// tidak boleh ikut lewat sini; kunci yang berpindah bahasa berhenti cocok.
        /// </summary>
        static string PieceName(PieceDefinition def)
            => def == null ? "" : Loc.T("piece." + def.Id + ".name", def.DisplayName);

        static string StatusName(StatusDefinition def)
            => def == null ? "" : Loc.T("status." + def.Id + ".name", def.DisplayName);

        /// <summary>
        /// Nama PAKTA lewat tabel bahasa. Aset paktanya ditulis dalam Bahasa Indonesia
        /// ("SUMPAH SUNYI"), jadi tanpa ini nama Indonesia bocor ke UI bahasa apa pun —
        /// persis yang dilaporkan pemilik project.
        /// </summary>
        static string PactName(WorldModifierDefinition p)
            => p == null ? "" : Loc.T("pact." + p.Id + ".name", p.DisplayName);

        static string PactBoon(WorldModifierDefinition p)
            => p == null ? "" : Loc.T("pact." + p.Id + ".boon", p.BoonText);

        static string PactBane(WorldModifierDefinition p)
            => p == null ? "" : Loc.T("pact." + p.Id + ".bane", p.BaneText);

        /// <summary>
        /// Nama REAKSI lewat tabel bahasa. ReactionDefinition tidak punya field Id, jadi
        /// kuncinya diturunkan dari nama asetnya — stabil selama asetnya tidak di-rename.
        /// </summary>
        static string ReactionName(ReactionDefinition rx)
            => rx == null ? "" : Loc.T("reaction." + rx.name.ToLowerInvariant() + ".name", rx.DisplayName);

        static string BuffName(BuffDefinition def)
            => def == null ? "" : Loc.T("buff." + def.Id + ".name", def.DisplayName);

        int ValueOf(PieceDefinition def) => _balance.SellValueOf(def);

        // ---------- drop routing ----------

        /// <summary>Berapa drop beruntun terakhir yang BUKAN rune — bahan bakar jaring pity.</summary>
        int _dropsSinceRune;

        /// <summary>
        /// Satu-satunya pintu undian drop kill/wave. Rune sengaja langka (RuneShareOfDrops
        /// kecil), dan di sinilah janji "langka tapi tidak mustahil" ditepati: setelah
        /// RunePityDrops drop kering beruntun, undian berikutnya dipaksa rune. Dihitung dari
        /// DROP, bukan wave — laju drop berubah-ubah ikut kill, dan yang dirasakan pemain
        /// adalah barang yang jatuh, bukan nomor wave.
        /// </summary>
        PieceDefinition RollDrop(int starBonus)
        {
            bool pity = _balance.RunePityDrops > 0 && _dropsSinceRune >= _balance.RunePityDrops;
            float share = pity ? 1f : _balance.RuneShareOfDrops;

            var drop = _db.RandomDrop(share, _balance.DropStarWeights,
                _balance.DropStarMinWave, Enemies.Wave, starBonus);

            if (drop != null) _dropsSinceRune = drop.IsRune ? 0 : _dropsSinceRune + 1;
            return drop;
        }

        /// <summary>Runes go straight into the grimoire â€” they have no storage.</summary>
        bool AutoPlaceInGrimoire(PieceDefinition def)
        {
            for (int rot = 0; rot < 4; rot++)
            {
                for (int y = 0; y < Grimoire.Height; y++)
                {
                    for (int x = 0; x < Grimoire.Width; x++)
                    {
                        var seated = Book.Place(def, new Vector2Int(x, y), rot);
                        if (seated == null) continue;

                        // Pemasangan otomatis tetap pemasangan — tanpa sweep, rune yang
                        // menyelinap sendiri ke papan luput dari mata pemiliknya.
                        StartOutlineSweep(seated);
                        return true;
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
                var bonus = RollDrop(starBonus);
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
                if (count > 0) _sb.Append(Loc.F("hud.drops", count));
                if (_pendingGold > 0)
                {
                    if (_sb.Length > 0) _sb.Append("   ");
                    _sb.Append(Loc.F("hud.drops.sold", _pendingGold));
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
                    Loc.F("hud.full.sold", PieceName(def), ValueOf(def)), new Color(1f, 0.88f, 0.45f));
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
                Loc.F("hud.loose.sold", sold, value), new Color(1f, 0.88f, 0.45f));
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
        /// tepi panel sendiri dari <c>ScreenW</c> dengan mengasumsikan lebar panel 1180 dan
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

            var label = MakeTmp("KeluarLabel", Vector2.zero, new Vector2(300f, 46f), 18,
                new Color(1f, 0.85f, 0.8f), new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center);
            label.transform.SetParent(go.transform, false);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.text = Loc.T("hud.exit");

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

        /// <summary>
        /// Posisi mouse dalam SATUAN KANVAS — satu-satunya bentuk yang boleh dibandingkan
        /// dengan rect-rect di berkas ini. Piksel mentah dibagi skala kanvas; di QHD keduanya
        /// identik, di resolusi lain hit-test tetap menempel ke gambarnya.
        /// </summary>
        static Vector2 UiMouse => (Vector2)ProtoInput.MousePosition / Mathf.Max(0.0001f, UiScale);

        void Update()
        {
            // Dihitung dari rumus scaler (match tinggi penuh), bukan dibaca dari kanvas —
            // scaleFactor kanvas baru sah setelah layout pass, dan frame pembangunan UI
            // butuh angka ini SEBELUM itu.
            UiScale = Screen.height / UiRefHeight;

            if (_inputLock > 0f) _inputLock -= Time.unscaledDeltaTime;

            // !TutorialBlocking: selama tutorial, ESC ikut ditelan. Tanpa pagar ini ESC bisa
            // membuka setelan di atas dim yang beku — atau, pada tema tanpa prefab setelan,
            // MEMBUANG RUN ke menu — dari balik layar yang sedang bilang "klik untuk lanjut".
            // (Temuan verifikasi adversarial 2026-08-19.)
            if (ProtoInput.BackDown && _inputLock <= 0f && !TutorialBlocking)
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

            // Tutorial satu-kali (dim + sorotan). Selama aktif, SELURUH input permainan
            // berhenti — klik apa pun cuma melanjutkan langkah tutorialnya. Redraw dan kawan-
            // kawannya di bawah tetap jalan: papan yang disorot harus tetap tergambar.
            UpdateTutorial();

            if (TutorialBlocking)
            {
                if (ProtoInput.LeftClickDown) _tut.Advance();
            }
            else
            {
                bool consumed = HandleSpeed();
                if (!consumed) HandleInput();
            }

            if (_evolveTimer > 0f)
            {
                _evolveTimer -= Time.unscaledDeltaTime;
                if (_evolveTimer <= 0f) _evolveText.text = "";
            }

            if (_shopNag > 0f) _shopNag -= Time.unscaledDeltaTime;

            // Rune menyala selama ada yang duduk di papan. Dibaca dari Placed, bukan dari
            // Spells: yang terakhir cuma menghitung skill yang benar-benar bisa menembak, dan
            // rune dasar yang baru ditaruh — yang jelas-jelas terlihat di papan — tidak akan
            // menyalakan apa pun.
            if (_rune != null) _rune.SetLit(Book.Placed.Count > 0);

            Redraw();
            TickFloaters(Time.unscaledDeltaTime);
            _popups.Tick(Time.unscaledDeltaTime);
            if (_evoFx != null) _evoFx.Tick(Time.unscaledDeltaTime);
            TickReveals(Time.unscaledDeltaTime);
            TickWaveBanner(Time.unscaledDeltaTime);
            if (_enemyBars != null) _enemyBars.Tick();
            HandleBanner();
        }

        // ---------- tutorial satu-kali ----------

        TutorialOverlay _tut;

        bool TutorialBlocking => _tut != null && _tut.Active;

        /// <summary>
        /// Dua babak, masing-masing SEKALI SEUMUR INSTALL (PlayerPrefs, bukan per run):
        /// "intro" saat papan pertama kali dipandang (wave 0) — buku, tas, tombol LANJUT;
        /// "rest" saat papan kembali setelah wave pertama — mana rune, mana skill, cara evolusi.
        /// Pemicunya keadaan, bukan event: dicek tiap frame dan baru menyala saat papan
        /// benar-benar sedang bebas dipandang, jadi ia tidak pernah menimpa peta/toko/kejadian.
        /// </summary>
        void UpdateTutorial()
        {
            if (_tut == null) return;

            if (!Player.Alive)
            {
                if (_tut.Active) _tut.Hide();
                return;
            }

            if (_tut.Active)
            {
                _tut.Draw();
                return;
            }

            bool boardIdle = !Enemies.WaveActive && !_mapChoose && !_mapOpen && !_shopOpen
                && !_eventOpen && _coverT <= 0f && _held == null;
            if (!boardIdle) return;

            // Tanda "sudah pernah" DIBACA ULANG tiap frame idle, bukan di-cache di field sesi:
            // tombol reset di setelan (Codex & tutorial) menghapus tandanya, dan tutorial harus
            // langsung menyala lagi di papan yang sama supaya bisa diuji berulang-ulang —
            // menunggu run baru untuk tiap percobaan membunuh niat mengujinya. BeginOnce
            // menandai dirinya sendiri, jadi cabang ini tetap sekali-jalan sampai direset lagi.
            if (Enemies.Wave == 0)
            {
                if (TutorialOverlay.BeginOnce("intro")) _tut.Show(IntroSteps());
            }
            else if (TutorialOverlay.BeginOnce("rest"))
            {
                _tut.Show(RestSteps());
            }
        }

        TutorialOverlay.Step[] IntroSteps() => new[]
        {
            new TutorialOverlay.Step(
                () => Expand(GridRect(), new Vector4(12, 12, 12, 12)), "tut.intro.1"),
            new TutorialOverlay.Step(TutorialBagRect, "tut.intro.2"),
            new TutorialOverlay.Step(TutorialStartRect, "tut.intro.3"),
        };

        /// <summary>
        /// Sasaran langkah "tekan LANJUT". Tombol yang DIMATIKAN di prefab meninggalkan
        /// override 0x0 (RefreshHudSeats) — menyorot kotak nol berarti layar gelap penuh
        /// tanpa lubang; jatuhnya ke papan, dan kalimatnya masih terbaca masuk akal.
        /// </summary>
        Rect TutorialStartRect()
        {
            var r = StartButtonRect();
            return r.width < 1f || r.height < 1f
                ? Expand(GridRect(), new Vector4(12, 12, 12, 12))
                : r;
        }

        /// <summary>
        /// Kotak kanvas sebuah bagian HUD (bola HP/mana, plakat baris atas). Papan sebagai
        /// cadangan kalau bagiannya tidak ada — prefab HUD yang belum lengkap tidak boleh
        /// membuat tutorialnya menyorot kekosongan.
        /// </summary>
        Rect TutorialHudRect(RectTransform area)
        {
            if (area != null)
            {
                var r = CanvasRectOf(area);
                if (r.width > 1f && r.height > 1f)
                    return Expand(r, new Vector4(8, 8, 8, 8));
            }

            return Expand(GridRect(), new Vector4(12, 12, 12, 12));
        }

        /// <summary>Pelukan seluruh deret tombol kecepatan — gabungan kotak tombol yang ada,
        /// dari prefab maupun gambar-kode; rumus SpeedRect sebagai cadangan terakhir.</summary>
        Rect TutorialSpeedRect()
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            bool any = false;

            if (_speedButtons != null)
            {
                for (int i = 0; i < _speedButtons.Length; i++)
                {
                    if (_speedButtons[i] == null) continue;

                    var r = CanvasRectOf(_speedButtons[i].rectTransform);
                    if (r.width < 1f || r.height < 1f) continue;

                    min = Vector2.Min(min, r.min);
                    max = Vector2.Max(max, r.max);
                    any = true;
                }
            }

            if (any) return Expand(new Rect(min, max - min), new Vector4(8, 8, 8, 8));

            var first = SpeedRect(0, Speeds.Length);
            var last = SpeedRect(Speeds.Length - 1, Speeds.Length);
            float yMin = Mathf.Min(first.yMin, last.yMin);
            float yMax = Mathf.Max(first.yMax, last.yMax);
            return Expand(new Rect(first.xMin, yMin, last.xMax - first.xMin, yMax - yMin),
                new Vector4(8, 8, 8, 8));
        }

        /// <summary>Pelukan baris-baris panel spell yang sedang tampil — papan peringkat
        /// damage. Union kotak bg baris yang menyala; papan sebagai cadangan.</summary>
        Rect TutorialSpellsRect()
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            bool any = false;

            if (_spellBg != null)
            {
                for (int i = 0; i < _spellBg.Length; i++)
                {
                    var bg = _spellBg[i];
                    if (bg == null || !bg.enabled) continue;

                    var r = CanvasRectOf(bg.rectTransform);
                    if (r.width < 1f || r.height < 1f) continue;

                    min = Vector2.Min(min, r.min);
                    max = Vector2.Max(max, r.max);
                    any = true;
                }
            }

            if (any) return Expand(new Rect(min, max - min), new Vector4(8, 8, 8, 8));

            return Expand(GridRect(), new Vector4(12, 12, 12, 12));
        }

        /// <summary>
        /// Sorotan "barang tercecer": piece pertama di lantai kalau sudah ada. Drop bisa
        /// masih TERBANG ke pemain saat tutorial mulai (pickup dunia baru memanggil AddLoose
        /// begitu sampai), jadi cadangannya zona sebarnya sendiri — kotak tempat mereka
        /// AKAN mendarat.
        /// </summary>
        Rect TutorialLooseRect()
        {
            if (_loosePos.Count > 0)
            {
                var p = _loosePos[0];
                return new Rect(p.x - 80f, p.y - 80f, 160f, 160f);
            }

            float left = RightX() + 60f;
            float right = Mathf.Max(left + 160f, ScreenW - 80f);
            return new Rect(left, 70f, right - left, Mathf.Max(200f, ScreenH * 0.35f));
        }

        // Babak habis-wave memuat SEMUA materi lanjutan — termasuk HP/mana/speed/objektif
        // yang tadinya mau jadi babak sendiri di tengah combat. Keputusan pemilik project:
        // "taro tutorialnya semua abis wave aja biar gak bingung" — tidak ada tutorial yang
        // menimpa pertarungan yang sedang jalan.
        //
        // Sasaran bola NYAWA/MANA = FILL-nya (cairan bola itu sendiri), BUKAN kotak hover:
        // zona hover ditata tangan supaya gampang di-hover dan boleh lebih besar/geser dari
        // bolanya — sorotan yang memakainya "meleset" (laporan playtest + screenshot).
        TutorialOverlay.Step[] RestSteps() => new[]
        {
            new TutorialOverlay.Step(() => PlacedRect(skill: false), "tut.rest.1"),
            new TutorialOverlay.Step(() => PlacedRect(skill: true, passive: false), "tut.rest.2"),
            new TutorialOverlay.Step(() => PlacedRect(skill: true, passive: true), "tut.rest.sigil"),
            new TutorialOverlay.Step(
                () => Expand(GridRect(), new Vector4(12, 12, 12, 12)), "tut.rest.3"),
            new TutorialOverlay.Step(TutorialBagRect, "tut.rest.bag"),
            new TutorialOverlay.Step(TutorialLooseRect, "tut.rest.loose"),
            new TutorialOverlay.Step(
                () => TutorialHudRect(_hpFill != null ? _hpFill.rectTransform : _hpHover),
                "tut.rest.hp"),
            new TutorialOverlay.Step(
                () => TutorialHudRect(_manaFill != null ? _manaFill.rectTransform : _manaHover),
                "tut.rest.mana"),
            new TutorialOverlay.Step(TutorialSpeedRect, "tut.rest.speed"),
            new TutorialOverlay.Step(TutorialSpellsRect, "tut.rest.spells"),
            new TutorialOverlay.Step(
                () => TutorialHudRect(_hudPlaque != null ? _hudPlaque.rectTransform : null),
                "tut.rest.goal"),
        };

        static Rect TutorialBagRect()
        {
            float w = Backpack.Width * (BagCell + BagGap) - BagGap;
            float h = Backpack.Height * (BagCell + BagGap) - BagGap;
            return new Rect(GrimoireLayout.BagOrigin.x - 14f, GrimoireLayout.BagOrigin.y - 14f, w + 28f, h + 28f);
        }

        /// <summary>
        /// Kotak layar piece TERPASANG pertama di lapisan yang diminta — sasaran sorotan
        /// "ini rune / ini skill / ini sigil". <paramref name="passive"/> menyaring lapisan
        /// skill lebih jauh: false = skill penyerang, true = sigil (IsPassive), null = bodo
        /// amat. Starter selalu menaruh rune+skill di papan; sigil tidak dijamin (tergantung
        /// hero) — kalau tidak ketemu, sorotan jatuh ke seluruh papan dan kalimat sigilnya
        /// tetap terbaca sebagai definisi, bukan tudingan.
        /// </summary>
        Rect PlacedRect(bool skill, bool? passive = null)
        {
            for (int i = 0; i < Book.Placed.Count; i++)
            {
                var inst = Book.Placed[i];
                if (inst == null || inst.Def == null) continue;
                if ((inst.Def.Layer == Layer.Skill) != skill) continue;
                if (passive != null && inst.Def.IsPassive != passive.Value) continue;

                var shape = Shapes.Rotate(inst.Def.Cells, inst.Rot);
                var min = new Vector2(float.MaxValue, float.MaxValue);
                var max = new Vector2(float.MinValue, float.MinValue);

                for (int k = 0; k < shape.Length; k++)
                {
                    var at = CellAnchor(inst.Origin.x + shape[k].x, inst.Origin.y + shape[k].y);
                    min = Vector2.Min(min, at);
                    max = Vector2.Max(max, at + new Vector2(CellSize, CellSize));
                }

                return new Rect(min, max - min);
            }

            return Expand(GridRect(), new Vector4(12, 12, 12, 12));
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

            Vector2 mouse = UiMouse;

            // Panel singgah yang TERBUKA menang atas tombol HUD di belakangnya.
            //
            // Blok ini jalan SEBELUM HandleInput, jadi tanpa penjagaan ini setiap tombol HUD
            // yang kebetulan tertindih panel akan menelan kliknya tanpa suara — dan panel toko
            // sekarang kotak yang DITATA TANGAN di ShopPanel.prefab, artinya letaknya boleh
            // pindah ke mana saja termasuk ke atas deretan tombol kecepatan. Yang terbaca
            // pemain kalau itu terjadi bukan "kecepatan berubah", melainkan barang dagangan
            // yang tidak bisa ditarik.
            if ((_shopOpen || _eventOpen) && PanelRect().Contains(mouse)) return false;

            // Deret dari prefab mengurus kliknya sendiri lewat Button UGUI — kotak layar hasil
            // hitungan di bawah ini akan meleset begitu tombolnya dipindah tangan di prefab.
            if (!_speedFromPrefab)
            {
                for (int i = 0; i < Speeds.Length; i++)
                {
                    if (SpeedRect(i, Speeds.Length).Contains(mouse))
                    {
                        SetSpeed(i);
                        return true;
                    }
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

                // Dari prefab: rupa kedua keadaan milik anak Idle/Active — kode cuma menyalakan
                // salah satunya, tidak menyentuh sprite maupun warna sama sekali.
                if (_speedFromPrefab)
                {
                    if (_speedIdle[i] != null) _speedIdle[i].SetActive(!on);
                    if (_speedActive[i] != null) _speedActive[i].SetActive(on);
                    continue;
                }

                // Ber-kit: yang aktif menukar bingkainya ke versi MENYALA, bukan diwarnai —
                // menint art gelap dengan emas cuma membuatnya keruh. Tanpa kit: warna lama.
                var frame = _theme != null ? _theme.ButtonFrame : null;
                if (frame != null)
                {
                    var glow = _theme.ButtonGlow != null ? _theme.ButtonGlow : frame;
                    _speedButtons[i].sprite = on ? glow : frame;
                    _speedButtons[i].color = on ? Color.white : new Color(0.65f, 0.65f, 0.65f, 0.9f);
                    _speedLabels[i].color = on ? new Color(0.12f, 0.09f, 0.05f) : TextBone;
                }
                else
                {
                    _speedButtons[i].color = on
                        ? new Color(0.89f, 0.75f, 0.46f, 0.95f)
                        : PanelInk;
                    _speedLabels[i].color = on ? new Color(0.12f, 0.09f, 0.05f) : TextBone;
                }
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
            _startLabel.text = Loc.T(_run != null ? "hud.start.depart" : "hud.start.wave");

            // Panel singgah duduk di tengah layar — persis tempat banner ini. Keduanya menyala
            // bersamaan berarti judul hijau mendarat di atas dagangan toko, dan itulah yang
            // membuat toko terlihat berantakan padahal kartunya tertata rapi. Selama panelnya
            // terbuka, panel itu yang bicara.
            //
            // Disembunyikan lewat `enabled`, BUKAN dengan keluar lebih awal: sisa fungsi ini juga
            // yang mendengar SPACE untuk LANJUT, dan toko yang terbuka tidak boleh mematikannya.
            _bannerText.enabled = !(_shopOpen || _eventOpen);

            if (!Player.Alive)
            {
                // Layar GAME OVER yang bicara sekarang; banner di tengah arena cuma akan
                // bertumpuk dengan judulnya sendiri.
                _gridTitle.text = Loc.T("hud.grid.title");
                _bannerText.text = "";

                if (ProtoInput.RestartDown) LoadScene(GameSceneName);

                return;
            }

            if (!Enemies.WaveActive)
            {
                _gridTitle.text = Loc.T("hud.grid.open");
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
                        // "REST ISLE - TOKO" DICABUT. Pulaunya sendiri sudah panggung yang
                        // bercerita, dan teks di tengah layar cuma menutupi barang.
                        _bannerText.text = "";
                    else if (Enemies.Wave == 0)
                        _bannerText.text = Loc.T("hud.banner.build");
                    else
                        // "WAVE n BERES" DICABUT atas permintaan pemilik project. Lapangan yang
                        // sudah bersih dan tombol LANJUT yang sudah menyala sudah mengatakannya;
                        // kalimat di tengah arena cuma menutupi barang jatuh.
                        _bannerText.text = "";

                    if (Book.Spells.Count == 0)
                    {
                        _bannerText.color = new Color(1f, 0.75f, 0.4f);
                        _bannerText.text += Loc.T("hud.banner.needskill");
                    }
                    else if (ProtoInput.RestartDown && _inputLock <= 0f && !TutorialBlocking
                        && CanDepart())
                    {
                        DepartRun();
                    }

                    return;
                }

                if (Enemies.Wave == 0)
                    _bannerText.text = Loc.T("hud.banner.build");
                else
                    _bannerText.text = "";

                if (!showStart)
                {
                    _bannerText.color = new Color(1f, 0.75f, 0.4f);
                    _bannerText.text += Loc.T("hud.banner.needskill");
                }
                else if (ProtoInput.RestartDown && _inputLock <= 0f && !TutorialBlocking)
                {
                    StartNextWave();
                }

                return;
            }

            _gridTitle.text = Loc.T("hud.grid.locked");
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

                var dead = UiMouse;

                // IDUP LAGI — pakta kebangkitan dibelanjakan DI SINI, oleh keputusan pemain,
                // bukan otomatis saat HP habis. Revive otomatis yang tak terlihat itulah
                // keluhan "punya revive kok langsung game over".
                if (Player.CanRevive && GameOverReviveRect().Contains(dead))
                {
                    Player.ReviveNow();
                    return;
                }

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

            Vector2 mouse = UiMouse;

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
                        FinalizeShopPurchase();
                        DropRiders();
                        _held = null;
                        if (Sfx != null) Sfx.UiPlace();
                    }
                    else if (_bag.CanReplaceAt(_held, bagOrigin, _heldRot))
                    {
                        ScatterAll(_bag.ClearFootprint(_held, bagOrigin, _heldRot), mouse);

                        if (_bag.Place(_held, bagOrigin, _heldRot) != null)
                        {
                            FinalizeShopPurchase();
                            DropRiders();
                            _held = null;
                            if (Sfx != null) Sfx.UiPlace();
                        }
                    }

                    return;
                }

                var target = ScreenToCell(mouse);
                if (target.x >= 0)
                {
                    // Petak yang sama dengan hover & ghost — klik mendarat persis di sorotan.
                    var gridOrigin = SnapTarget(target, mouse);

                    var seated = Book.Place(_held, gridOrigin, _heldRot);
                    if (seated != null)
                    {
                        StartOutlineSweep(seated);
                        FinalizeShopPurchase();
                        LandRiders(gridOrigin, mouse);
                        _held = null;
                        if (Sfx != null) Sfx.UiPlace();
                    }
                    else if (Book.CanReplaceAt(_held, gridOrigin, _heldRot))
                    {
                        // Occupied â€” kick the old piece out and take its spot.
                        ScatterAll(Book.ClearFootprint(_held, gridOrigin, _heldRot), mouse);

                        seated = Book.Place(_held, gridOrigin, _heldRot);
                        if (seated != null)
                        {
                            StartOutlineSweep(seated);
                            FinalizeShopPurchase();
                            LandRiders(gridOrigin, mouse);
                            _held = null;
                            if (Sfx != null) Sfx.UiPlace();
                        }
                    }

                    return;
                }

                // Clicked empty space. Barang TOKO pulang ke slotnya — belum dibayar, jadi
                // "naruh sembarangan" bukan transaksi, cuma berubah pikiran. Barang milik
                // sendiri tetap jatuh di tempat seperti biasa.
                if (_heldShopSlot >= 0)
                {
                    CancelShopCarry();
                    return;
                }

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
                if (Sfx != null && Time.frameCount != _panelCloseFrame) Sfx.UiPick();
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
                    if (Sfx != null && Time.frameCount != _panelCloseFrame) Sfx.UiPick();
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
            if (Sfx != null && Time.frameCount != _panelCloseFrame) Sfx.UiPick();

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

                // Kotak klik = ukuran yang DIGAMBAR (lihat MapNodeSize), bukan angka tetap.
                //
                // Dan yang menang bukan yang PERTAMA ketemu di list, melainkan yang kliknya
                // paling DALAM: jaraknya dibagi radiusnya sendiri. Node peta ini ukurannya
                // berbeda empat kali lipat antar jenis, jadi mereka saling bertumpang tindih —
                // "yang pertama di urutan reachable" berarti node yang kepilih bukan node yang
                // ditunjuk, dan urutannya sendiri tidak berarti apa-apa buat pemain.
                RunNode best = null;
                float bestScore = 1f;

                for (int i = 0; i < reachable.Count; i++)
                {
                    var n = reachable[i];
                    Vector2 at = MapNodePos(n, panel, map.Floors, map.Lanes);

                    float radius = MapNodeSize(n, map.At == n.Index, true) * 0.5f;
                    if (radius <= 0.01f) continue;

                    float score = Vector2.Distance(mouse, at) / radius;
                    if (score > bestScore) continue;

                    bestScore = score;
                    best = n;
                }

                if (best != null)
                {
                    BeginMapTravel(best);
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

            if (!_shopOpen) return false;

            // KOTAK DAGANGAN MENANG atas piece tercecer, dan urutan inilah perbaikannya.
            //
            // Piece tercecer berserak di rentang ScatterPos — yang membentang dari sisi kanan
            // papan sampai tepi layar, jadi MELINTASI panel toko yang duduk di tengah. Kolom
            // kanan etalase (slot 2 & 5) persis di jalur itu. Selama pengecualian "piece
            // tercecer lolos" berdiri di depan, satu barang jatuhan yang kebetulan menutupi
            // slot membuat klik ke barang dagangan berbelok jadi klik ke barang jatuhan:
            // etalasenya terlihat, harganya terbaca, dan tarikannya tidak pernah sampai.
            //
            // Slot selalu di dalam panel, jadi mendahulukannya tidak merebut apa pun dari
            // pengecualian di bawah — yang tersisa untuk piece tercecer justru seluruh badan
            // panel selain enam kotak ini.
            int shopSlot = ShopSlotAt(mouse);
            if (shopSlot >= 0) return TakeFromShop(shopSlot);

            // Klik ke PIECE TERCECER selalu lolos, walau jatuhnya di dalam panel. Barang yang
            // barusan dibeli dilempar dekat slotnya — DI DALAM panel — dan tanpa baris ini guard
            // di bawah menelan kliknya: belanjaan tergeletak di depan mata dan tidak bisa
            // diambil, tanpa satu pun tanda kenapa.
            if (ScreenToLoose(mouse) >= 0) return false;

            // Klik di luar panel LOLOS ke lapangan, dan panelnya TETAP TERBUKA. Dulu klik
            // pertama di luar dipakai menutup toko — tapi tombol pembuka-tutupnya sudah dicabut,
            // jadi menutup di sini berarti toko yang tertutup tak sengaja tidak bisa dibuka lagi
            // sampai singgah berikutnya. Toko sekarang cuma tutup saat pemain berangkat.
            if (!PanelRect().Contains(mouse)) return false;

            if (RerollRect().Contains(mouse))
            {
                if (_gold >= _rerollCost)
                {
                    // Barang toko yang sedang dibawa dipulangkan dulu — reroll menukar SELURUH
                    // stok, dan barang yang belum dibayar ikut tertukar bersama slotnya.
                    if (_heldShopSlot >= 0) CancelShopCarry();

                    _gold -= _rerollCost;
                    _rerollCost += _balance.RerollCostIncrement;
                    RollShop();
                    if (Sfx != null) Sfx.UiReroll();
                }

                return true;
            }

            return true;
        }

        /// <summary>
        /// Slot toko di bawah kursor, atau -1.
        ///
        /// Slot yang SEDANG DIBAWA ikut dijawab walau isinya sudah kosong: kotak asal barang
        /// yang ada di tangan adalah tempat mengembalikannya, dan pemain yang berubah pikiran
        /// menaruhnya kembali ke sana lebih dulu sebelum ia terpikir melempar ke lantai.
        /// </summary>
        int ShopSlotAt(Vector2 mouse)
        {
            for (int i = 0; i < ShopSlots; i++)
            {
                if (_shop[i] == null && i != _heldShopSlot) continue;
                if (ShopSlotRect(i).Contains(mouse)) return i;
            }

            return -1;
        }

        /// <summary>
        /// Mengangkat barang dagangan ke tangan. Selalu mengembalikan true — kliknya jatuh di
        /// dalam etalase, dan tidak ada seorang pun di belakang etalase yang berhak menerimanya.
        /// </summary>
        bool TakeFromShop(int slot)
        {
            // Kotak asal barang yang sedang dibawa: mengkliknya = mengembalikannya. Sama
            // persis dengan menaruhnya sembarangan di lantai, cuma dengan sasaran yang jelas.
            if (slot == _heldShopSlot)
            {
                CancelShopCarry();
                if (Sfx != null) Sfx.UiClose();
                return true;
            }

            var def = _shop[slot];
            if (def == null) return true;

            int price = _balance.PriceOf(def);

            if (_gold < price)
            {
                // Penolakan yang BERSUARA dan TERLIHAT. Kotaknya memang sudah memerah sejak
                // digambar, tapi warna yang sudah ada di layar sebelum tangan bergerak bukan
                // jawaban atas tarikan — yang dibutuhkan adalah sesuatu yang berubah TEPAT
                // saat pemain mencoba.
                _shopNag = 1.6f;
                if (Sfx != null) Sfx.UiClick();
                return true;
            }

            // Barang DIBAWA dulu, bukan dibeli: uang baru berpindah saat ia benar-benar
            // terpasang di papan atau tas (FinalizeShopPurchase). Batal menaruh = pulang
            // ke slot tanpa transaksi. Dulu klik = bayar = dilempar ke lantai — salah
            // klik saja sudah jadi pembelian.
            if (_heldShopSlot >= 0) CancelShopCarry();
            if (_held != null) StashHeld();

            _held = def;
            _heldRot = 0;
            _heldShopSlot = slot;
            _shop[slot] = null;
            if (Sfx != null) Sfx.UiPick();
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

            // Barang toko tidak pernah menggeletak di lantai: ia belum dibayar, jadi ke mana
            // pun alur ini dipanggil (panel tutup, wave mulai), ia pulang ke slotnya.
            if (_heldShopSlot >= 0)
            {
                CancelShopCarry();
                return;
            }

            DropRiders();
            AddLoose(_held);
            _held = null;
        }

        /// <summary>Barang toko yang batal ditaruh pulang ke slotnya — tanpa transaksi.</summary>
        void CancelShopCarry()
        {
            if (_heldShopSlot < 0 || _held == null) return;

            _shop[_heldShopSlot] = _held;
            _held = null;
            _heldShopSlot = -1;
        }

        /// <summary>
        /// Uang berpindah DI SINI — saat barang toko benar-benar terpasang di papan atau tas.
        /// Dipanggil tepat SEBELUM genggaman dilepas, karena harganya dibaca dari _held.
        /// </summary>
        void FinalizeShopPurchase()
        {
            if (_heldShopSlot < 0 || _held == null) return;

            _gold -= _balance.PriceOf(_held);
            _heldShopSlot = -1;
            if (Sfx != null) Sfx.UiBuy();
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

            var drop = RollDrop(starBonus);
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

            // Perayaan dulu, pembukuan belakangan: banner tampil untuk SEMUA wave yang
            // beres — bukan cuma yang kebetulan menghasilkan evolusi (baris di bawah
            // keluar lebih awal saat tidak ada resep yang jadi).
            if (_waveBanner != null)
            {
                _waveBannerAge = 0f;
                _waveBanner.SetActive(true);
                _waveBanner.transform.SetAsLastSibling();
            }

            if (Sfx != null) Sfx.WaveClearChime();

            // Hasil yang tidak dapat tempat TIDAK membatalkan evolusinya — ia dikeluarkan ke sini.
            _spilled = 0;
            var evolutions = Book.ResolveEvolutions(SpillFromBoard, RevealEvolved);

            // The bag cooks too. Spare copies used to pile up in there with nowhere to go.
            evolutions.AddRange(_bag.ResolveEvolutions(_db, SpillFromBag, RevealEvolvedBag));
            for (int i = 0; i < Book.Placed.Count; i++) Discover(Book.Placed[i].Def);
            if (evolutions.Count == 0) return;

            _sb.Length = 0;
            _sb.Append(Loc.T("hud.evolve")).Append("   ");
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
            if (Sfx != null) Sfx.EvolveFanfare();

            // Kilat hijau sekejap: evolve harus TERLIHAT terjadi, bukan cuma tertulis.
            Flash(0.4f, new Color(0.6f, 1f, 0.7f));
            PushFloater(Player.transform.position + Vector3.up * 3f, Loc.T("hud.evolve"), new Color(0.55f, 1f, 0.7f));
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

            pos.x = Mathf.Clamp(pos.x, 60f, Mathf.Max(80f, ScreenW - 60f));
            pos.y = Mathf.Clamp(pos.y, 60f, Mathf.Max(80f, ScreenH - 90f));

            AddLoose(def, pos);
            PushFloater(Player.transform.position + Vector3.up * 2.8f,
                Loc.F("hud.bounced", PieceName(def)), new Color(1f, 0.72f, 0.35f));

            // Evolusi yang hasilnya kepental TETAP evolusi — dan pemain yang tidak melihat
            // ritualnya menyimpulkan "vfx-nya gak keluar" (laporan pemilik project; papan
            // penuh membuat hampir semua evolusi lanjutan lewat jalur ini). Ritualnya
            // dimainkan di tempat barangnya menggeletak, seukuran petak-petak barangnya.
            if (_evoFx != null)
            {
                _revealQueue.Add(new PendingReveal
                {
                    Inst = null,
                    Center = pos,
                    Size = Vector2.one * (CellSize * 1.9f),
                    Sprite = def.Art != null ? def.Art : def.Icon
                });
            }
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

            var from = UiMouse;

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
        void DrawEvoLink(Image line, Vector2 a, Vector2 b, Color color, float thickness)
        {
            var delta = b - a;

            line.enabled = true;
            line.color = color;
            line.rectTransform.anchoredPosition = (a + b) * 0.5f;

            // Kotak yang DILEBARKAN tegak lurus garis, cuma saat shader busur listrik terpasang.
            // Lihat EvoBoltHeightMul: itu ruang goyangnya, bukan tebal garisnya.
            line.rectTransform.sizeDelta = new Vector2(delta.magnitude,
                _evoBolt ? thickness * EvoBoltHeightMul : thickness);
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

        /// <summary>
        /// Same, for the backpack — different origin and a smaller cell. BagOrigin, BUKAN
        /// RightX()/BagY mentah: sejak kotak BagArea di prefab HUD berkuasa atas letak tas,
        /// rumus mentah menunjuk ke tempat tas yang LAMA — spill dan ritual evo tas mendarat
        /// di rumput kosong sementara tasnya sudah pindah.
        /// </summary>
        static Vector2 BagPoint(Vector2 cell)
        {
            float step = BagCell + BagGap;
            var origin = GrimoireLayout.BagOrigin;
            return new Vector2(origin.x + cell.x * step + BagCell * 0.5f,
                               origin.y + cell.y * step + BagCell * 0.5f);
        }

        // ---------- cahaya outline piece terpasang & ritual reveal evolusi ----------

        Shader _outlineShader;
        RectTransform _outlineLayer;
        readonly List<Image> _outlinePool = new List<Image>();
        int _outlineUsed;

        /// <summary>
        /// Warna cahaya sweep per KASTA bintang, indeks = bintang − 1 — permintaan pemilik
        /// project: "biar keliatan perbedaan kastanya". ★1 putih, ★2 biru, ★3 ungu, ★4 merah,
        /// ★5 emas. Semua dipilih terang karena blend-nya ADITIF: ungu gelap di atas kertas
        /// cuma jadi bayangan, bukan cahaya.
        /// </summary>
        static readonly Color[] StarInk =
        {
            new Color(1f, 1f, 1f),
            new Color(0.40f, 0.70f, 1f),
            new Color(0.80f, 0.50f, 1f),
            new Color(1f, 0.40f, 0.32f),
            new Color(1f, 0.84f, 0.40f)
        };

        /// <summary>
        /// Satu material per kasta, dibuat malas. Lima material — bukan satu per piece:
        /// segmen sekasta tetap satu batch, dan lima adalah harga penuh HANYA saat lima
        /// sweep beda kasta kebetulan hidup bersamaan.
        /// </summary>
        readonly Material[] _outlineMats = new Material[5];

        Material OutlineMatFor(int stars)
        {
            int i = Mathf.Clamp(stars, 1, 5) - 1;

            if (_outlineMats[i] == null && _outlineShader != null)
            {
                _outlineMats[i] = new Material(_outlineShader) { color = StarInk[i] };
            }

            return _outlineMats[i];
        }

        /// <summary>Petak milik piece yang SEDANG di-outline — diisi ulang per piece per frame.</summary>
        readonly HashSet<Vector2Int> _outlineCells = new HashSet<Vector2Int>();

        /// <summary>Tinggi kotak segmen. Jauh lebih tebal dari garisnya — shader butuh ruang
        /// untuk glow lembut di kedua sisi sumbu, persis alasan EvoBoltHeightMul.</summary>
        const float OutlineThick = 18f;

        /// <summary>
        /// Satu sweep = satu kali cahaya mengitari siluet sebuah piece, LALU HABIS —
        /// "waktu dipasang langsung muter-muter grid-nya, udah, abis" (pemilik project;
        /// versi pertama menyala terus dan itu salah baca permintaan). Age boleh lahir
        /// NEGATIF: sweep hasil evolusi menunggu ritual reveal-nya sampai ke pop dulu.
        /// Segs diisi sekali saat pertama tergambar — bentuk piece tidak berubah selama
        /// sweep, dan menelusuri keliling ulang tiap frame adalah kerja yang dibuang.
        /// </summary>
        class OutlineSweep
        {
            public RuneInstance Inst;
            public float Age;
            public List<OutlineSeg> Segs;
        }

        /// <summary>Satu tepi petak di keliling siluet, siap gambar: posisi, arah, dan
        /// jatah fasenya di sepanjang loop (fase = jarak tempuh keliling, 0..1).</summary>
        struct OutlineSeg
        {
            public Vector2 Mid;
            public float Angle;
            public float Phase;
            public float Span;
        }

        readonly List<OutlineSweep> _outlineSweeps = new List<OutlineSweep>();

        const float SweepIn = 0.12f;
        const float SweepLoop = 1.0f;
        /// <summary>"Muter-muter": satu putaran penuh plus setengah lagi, supaya terbaca
        /// sebagai cahaya yang MENGITARI, bukan kilat yang numpang lewat.</summary>
        const float SweepLoops = 1.5f;
        const float SweepFade = 0.35f;
        static float SweepDuration => SweepLoop * SweepLoops + SweepFade;

        EvoRevealFx _evoFx;

        /// <summary>
        /// Ritual evolusi yang MENUNGGU papan terlihat. Evolusi terjadi persis saat wave
        /// beres — frame berikutnya peta pemilih membentang satu layar dan menutup papan,
        /// jadi ritual yang dimainkan saat itu juga tidak pernah tertonton (laporan pemilik
        /// project: "evo belum kaya gitu" — efeknya jalan, penontonnya dihalangi).
        /// </summary>
        struct PendingReveal
        {
            public RuneInstance Inst;
            public Vector2 Center;
            public Vector2 Size;
            public Sprite Sprite;

            /// <summary>Terisi (Matched) saat geometrinya hasil PieceArt.Layout — overlay
            /// harus persis menimpa art papan, sudut dan semua, tanpa preserveAspect.</summary>
            public float Angle;
            public bool Matched;

            /// <summary>Ritual ikon PAKTA: posisinya dicari saat DILEPAS, dari slot strip
            /// pakta — saat diantre stripnya belum menggambar ikon barunya, jadi rect-nya
            /// belum ada. TickReveals berjalan SETELAH Redraw di frame yang sama.</summary>
            public bool AtPactStrip;
            public int PactSlot;
        }

        readonly List<PendingReveal> _revealQueue = new List<PendingReveal>();

        /// <summary>Jeda antar ritual saat antrean dilepas — evolusi berantai yang meledak
        /// serempak cuma jadi satu kilatan; berurutan, tiap perubahan wujud dapat panggungnya.</summary>
        float _revealGap;

        // ---------- banner wave beres ----------

        GameObject _waveBanner;
        RectTransform _waveBannerRt;
        Image _waveBannerGlow;
        float _waveBannerAge;

        /// <summary>
        /// Banner WAVE COMPLETE: lahir dengan letupan skala + kilau terang, lalu bernafas
        /// pelan selama pemain menata papan. Padam SENDIRI begitu langkah berikutnya
        /// benar-benar dimulai — klik LANJUT membuka peta, wave baru berjalan, atau panel
        /// singgah terbuka ("kalo udah klik continue baru beres" — pemilik project).
        /// Dicek dari KEADAAN, bukan dari klik: semua jalur lanjut lewat salah satu flag
        /// ini, jadi tidak ada tombol yang perlu diajari menyembunyikannya satu-satu.
        /// </summary>
        void TickWaveBanner(float dt)
        {
            if (_waveBanner == null || !_waveBanner.activeSelf) return;

            bool moved = Enemies.WaveActive || _mapChoose || _mapOpen || _shopOpen ||
                _eventOpen || _coverT > 0f || !Player.Alive;

            if (moved)
            {
                _waveBanner.SetActive(false);
                return;
            }

            _waveBannerAge += dt;

            // Letupan lahir: ease-out-back — mengembang lewat sedikit, lalu menetap.
            if (_waveBannerRt != null)
            {
                const float C1 = 1.70158f;
                const float C3 = C1 + 1f;
                float t = Mathf.Clamp01(_waveBannerAge / 0.45f);
                float u = t - 1f;
                float ease = 1f + C3 * u * u * u + C1 * u * u;
                _waveBannerRt.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, ease);
            }

            // Kilau: terang saat lahir, lalu bernafas — banner yang diam membatu terbaca
            // sebagai HUD, bukan sebagai perayaan.
            if (_waveBannerGlow != null)
            {
                var c = _waveBannerGlow.color;
                float flash = Mathf.Clamp01(1f - _waveBannerAge / 0.5f);
                c.a = 0.30f + 0.30f * flash + 0.12f * Mathf.Sin(_waveBannerAge * 3.2f);
                _waveBannerGlow.color = c;
            }
        }

        /// <summary>Mulai (atau ulang) sweep cahaya untuk satu piece yang baru dipasang.
        /// <paramref name="delay"/> menunda mulainya — dipakai hasil evolusi supaya sweep-nya
        /// jatuh tepat di pop ritual reveal, bukan bersamaan dengan fase putihnya.</summary>
        void StartOutlineSweep(RuneInstance inst, float delay = 0f)
        {
            if (inst == null || _outlineShader == null) return;

            for (int i = 0; i < _outlineSweeps.Count; i++)
            {
                if (_outlineSweeps[i].Inst != inst) continue;
                _outlineSweeps[i].Age = -delay;
                return;
            }

            _outlineSweeps.Add(new OutlineSweep { Inst = inst, Age = -delay });
        }

        /// <summary>
        /// Cahaya putih yang mengitari siluet piece SAAT DIPASANG — one-shot, bukan hiasan
        /// permanen. Segmen digambar hanya di tepi perimeter (tepi petak yang tetangganya
        /// bukan milik piece yang sama), jadi yang menyala adalah BENTUK piece-nya, bukan
        /// kotak-kotak petaknya satu-satu.
        ///
        /// Ditata ulang tiap frame lewat kolam, pola yang sama dengan lapisan art. Waktunya
        /// UNSCALED: memasang piece terjadi saat jeda antar wave, ketika dunia sering berhenti.
        /// </summary>
        void DrawRuneOutlines()
        {
            _outlineUsed = 0;

            if (_outlineShader != null && _outlineSweeps.Count > 0)
            {
                float dt = Time.unscaledDeltaTime;
                float step = CellSize + CellGap;

                for (int i = _outlineSweeps.Count - 1; i >= 0; i--)
                {
                    var sweep = _outlineSweeps[i];
                    sweep.Age += dt;

                    // Habis masa, atau piece-nya keburu diangkat/dimakan evolusi lanjutan —
                    // sweep mati diam-diam; cahaya di petak yang sudah kosong itu hantu.
                    if (sweep.Age >= SweepDuration || sweep.Inst == null ||
                        !Book.Placed.Contains(sweep.Inst))
                    {
                        _outlineSweeps.RemoveAt(i);
                        continue;
                    }

                    // Masih menunggu gilirannya (sweep hasil evolusi menunggu pop ritual).
                    if (sweep.Age < 0f) continue;

                    DrawOutlineSweep(sweep, step);
                }
            }

            for (int i = _outlineUsed; i < _outlinePool.Count; i++) _outlinePool[i].enabled = false;
        }

        void DrawOutlineSweep(OutlineSweep sweep, float step)
        {
            if (sweep.Segs == null) sweep.Segs = TraceOutline(sweep.Inst);
            if (sweep.Segs.Count == 0) return;

            // Warna kasta: cahaya ★4 merah harus terbaca beda dari ★1 putih dalam sekali
            // lirik — di situlah "wah, barang mahal" lahir tanpa membaca satu pun angka.
            var mat = OutlineMatFor(sweep.Inst.Def != null ? sweep.Inst.Def.Stars : 1);
            if (mat == null) return;

            // Amplop nyala-pudar: cepat menyala, mengitari, lalu habis — bagian "udah, abis".
            float alpha = Mathf.Clamp01(sweep.Age / SweepIn) *
                Mathf.Clamp01((SweepDuration - sweep.Age) / SweepFade) * 0.9f;

            // Posisi denyut di keliling (0..1), dikirim ke shader lewat kanal g tiap segmen —
            // shadernya sengaja tidak memegang jam sendiri, lihat catatan di UiRuneOutline.
            float pulsePos = Mathf.Repeat(sweep.Age / SweepLoop, 1f);

            for (int i = 0; i < sweep.Segs.Count; i++)
            {
                var seg = sweep.Segs[i];

                var img = TakeOutline();
                img.material = mat;

                var rt = img.rectTransform;
                rt.anchoredPosition = seg.Mid;
                rt.sizeDelta = new Vector2(step, OutlineThick);
                rt.localRotation = Quaternion.Euler(0f, 0f, seg.Angle);

                // Kontrak kanal warna milik shader UiRuneOutline: r = fase awal segmen,
                // g = posisi denyut, b = bentang fase, a = alpha amplop. BUKAN warna —
                // warna cahayanya milik material kasta di atas.
                img.color = new Color(seg.Phase, pulsePos, seg.Span, alpha);
            }
        }

        readonly List<(Vector2Int a, Vector2Int b)> _traceLoop =
            new List<(Vector2Int, Vector2Int)>();

        /// <summary>
        /// Menelusuri keliling siluet piece menjadi LOOP tepi yang BERURUTAN, lalu membagi
        /// fase menurut jarak tempuhnya. Versi pertama memberi fase dari SUDUT segmen
        /// terhadap pusat piece — dan hasilnya "patah-patah" (laporan pemilik project):
        /// di bentuk L, dua tepi yang berjauhan bisa berbagi sudut yang hampir sama dan
        /// menyala serempak, sementara urutan sudut melompat-lompat di sepanjang keliling.
        /// Fase jarak-tempuh membuat cahayanya MENJALAR dari tepi ke tepi tetangganya.
        ///
        /// Tepi diberi ARAH dengan interior selalu di kiri arah jalan — dua tepi yang
        /// bertemu di satu pojok selalu tersambung kepala-ke-ekor, jadi loop pasti menutup.
        /// Piece berlubang menghasilkan lebih dari satu loop; masing-masing berfase 0..1
        /// sendiri, artinya cahayanya mengitari lubangnya sendiri — dan itu benar.
        /// </summary>
        List<OutlineSeg> TraceOutline(RuneInstance inst)
        {
            _outlineCells.Clear();
            foreach (var c in inst.Cells()) _outlineCells.Add(c);

            var edges = new Dictionary<Vector2Int, List<Vector2Int>>();

            void Add(Vector2Int a, Vector2Int b)
            {
                if (!edges.TryGetValue(a, out var outs))
                {
                    outs = new List<Vector2Int>(2);
                    edges.Add(a, outs);
                }

                outs.Add(b);
            }

            foreach (var c in _outlineCells)
            {
                if (!_outlineCells.Contains(c + Vector2Int.up))
                    Add(new Vector2Int(c.x + 1, c.y + 1), new Vector2Int(c.x, c.y + 1));
                if (!_outlineCells.Contains(c + Vector2Int.down))
                    Add(new Vector2Int(c.x, c.y), new Vector2Int(c.x + 1, c.y));
                if (!_outlineCells.Contains(c + Vector2Int.right))
                    Add(new Vector2Int(c.x + 1, c.y), new Vector2Int(c.x + 1, c.y + 1));
                if (!_outlineCells.Contains(c + Vector2Int.left))
                    Add(new Vector2Int(c.x, c.y + 1), new Vector2Int(c.x, c.y));
            }

            var segs = new List<OutlineSeg>();

            while (edges.Count > 0)
            {
                Vector2Int start = default;
                foreach (var key in edges.Keys) { start = key; break; }

                _traceLoop.Clear();
                var at = start;

                do
                {
                    var outs = edges[at];
                    var to = outs[outs.Count - 1];
                    outs.RemoveAt(outs.Count - 1);
                    if (outs.Count == 0) edges.Remove(at);

                    _traceLoop.Add((at, to));
                    at = to;
                }
                while (at != start && edges.ContainsKey(at));

                int n = _traceLoop.Count;

                for (int i = 0; i < n; i++)
                {
                    var (a, b) = _traceLoop[i];
                    var pa = CornerPix(a);
                    var pb = CornerPix(b);
                    var d = pb - pa;

                    // Rotasi mengikuti ARAH JALAN loop, bukan sekadar mendatar/tegak: ujung
                    // uv.x=0 kotaknya selalu jatuh di pangkal tepi, dan itulah yang membuat
                    // interpolasi fase di shader (r + uv.x * b) berjalan ke arah yang benar.
                    segs.Add(new OutlineSeg
                    {
                        Mid = (pa + pb) * 0.5f,
                        Angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg,
                        Phase = (float)i / n,
                        Span = 1f / n
                    });
                }
            }

            return segs;
        }

        /// <summary>Pojok petak (x,y) dalam piksel kanvas — tepat di tengah celah antar
        /// petak, supaya outline dua petak setetangga bertemu tanpa celah.</summary>
        static Vector2 CornerPix(Vector2Int corner) =>
            GridPoint(new Vector2(corner.x - 0.5f, corner.y - 0.5f));

        Image TakeOutline()
        {
            while (_outlinePool.Count <= _outlineUsed)
            {
                var go = new GameObject($"Outline_{_outlinePool.Count}");
                go.transform.SetParent(_outlineLayer, false);

                var img = go.AddComponent<Image>();
                img.raycastTarget = false;

                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                _outlinePool.Add(img);
            }

            var image = _outlinePool[_outlineUsed++];
            image.enabled = true;
            return image;
        }

        /// <summary>
        /// Dipanggil model untuk tiap hasil evolusi yang berhasil DUDUK — MENGANTRIKAN ritual
        /// reveal (putih → warna tumbuh dari pusat → pop + cincin kejut) di footprint tempat
        /// ia mendarat. Diantrikan, bukan langsung dimainkan: evolusi terjadi saat wave beres,
        /// dan frame berikutnya peta pemilih menutup papan — ritual yang main saat itu tidak
        /// pernah tertonton. Hasil yang spill tidak lewat sini: barangnya menggeletak di luar
        /// papan, dan ritual di petak kosong cuma membingungkan.
        ///
        /// Footprint dihitung SEKARANG, bukan saat antrean dilepas: evolusi berantai bisa
        /// memakan piece ini lagi sebelum papan terlihat, dan Cells() dari instance yang
        /// sudah dicabut menghasilkan posisi hantu. Kalau ia dimakan, TickReveals memakai
        /// footprint terakhirnya — tempat perubahan wujudnya memang terjadi.
        /// </summary>
        void RevealEvolved(RuneInstance inst)
        {
            if (_evoFx == null || inst == null || inst.Def == null) return;

            float step = CellSize + CellGap;

            // Piece ber-ART memakai rumus penata yang SAMA dengan DrawPieceArt
            // (PieceArt.Layout): overlay ritual lahir persis seukuran, sepusat, dan
            // SESUDUT gambar yang akan ditinggalkannya. Versi kotak-pembatas membuat
            // gambarnya melompat saat overlay pudar ("gak ngikutin scale dari gambar
            // di grid" — pemilik project).
            if (!inst.Def.IsRune && inst.Def.Art != null)
            {
                Vector2 artCenter, artSize;
                float artAngle;
                var bottomLeft = new Vector2(GridX + inst.Origin.x * step,
                                             GridY + inst.Origin.y * step);

                if (PieceArt.Layout(inst.Def, bottomLeft, inst.Rot, CellSize, CellGap,
                    out artCenter, out artSize, out artAngle))
                {
                    _revealQueue.Add(new PendingReveal
                    {
                        Inst = inst,
                        Center = artCenter,
                        Size = artSize,
                        Sprite = inst.Def.Art,
                        Angle = artAngle,
                        Matched = true
                    });
                    return;
                }
            }

            var min = new Vector2Int(int.MaxValue, int.MaxValue);
            var max = new Vector2Int(int.MinValue, int.MinValue);

            foreach (var c in inst.Cells())
            {
                min = Vector2Int.Min(min, c);
                max = Vector2Int.Max(max, c);
            }

            if (min.x > max.x) return;

            var center = (GridPoint(new Vector2(min.x, min.y)) +
                          GridPoint(new Vector2(max.x, max.y))) * 0.5f;

            // Dilebihkan 15%: kilau yang pas-pasan di tepi footprint terbaca sebagai gambar
            // yang diganti, bukan sebagai energi yang meluap dari perubahan wujud.
            var size = new Vector2((max.x - min.x + 1) * step - CellGap,
                                   (max.y - min.y + 1) * step - CellGap) * 1.15f;

            _revealQueue.Add(new PendingReveal
            {
                Inst = inst,
                Center = center,
                Size = size,
                Sprite = inst.Def.Art != null ? inst.Def.Art : inst.Def.Icon
            });
        }

        /// <summary>
        /// Ritual untuk hasil evolusi yang duduk DI TAS — geometri tas, bukan papan.
        /// Tanpa ini separuh evolusi bisu: tas ikut memasak tiap wave, dan pemain yang
        /// evolusinya kebetulan terjadi di sana menyimpulkan "vfx-nya cuma sekali".
        /// Tidak diberi sweep outline — segmen outline digambar di ruang petak PAPAN.
        /// </summary>
        void RevealEvolvedBag(RuneInstance inst)
        {
            if (_evoFx == null || inst == null || inst.Def == null) return;

            var min = new Vector2Int(int.MaxValue, int.MaxValue);
            var max = new Vector2Int(int.MinValue, int.MinValue);

            foreach (var c in inst.Cells())
            {
                min = Vector2Int.Min(min, c);
                max = Vector2Int.Max(max, c);
            }

            if (min.x > max.x) return;

            float step = BagCell + BagGap;
            var center = (BagPoint(new Vector2(min.x, min.y)) +
                          BagPoint(new Vector2(max.x, max.y))) * 0.5f;

            var size = new Vector2((max.x - min.x + 1) * step - BagGap,
                                   (max.y - min.y + 1) * step - BagGap) * 1.15f;

            _revealQueue.Add(new PendingReveal
            {
                Inst = null,
                Center = center,
                Size = size,
                Sprite = inst.Def.Art != null ? inst.Def.Art : inst.Def.Icon
            });
        }

        /// <summary>
        /// Melepas antrean ritual SATU-SATU begitu papan benar-benar terlihat (tidak ada
        /// peta/toko/kejadian/tirai di atasnya). Jeda antar ritual memberi tiap perubahan
        /// wujud panggungnya sendiri; sweep outline hasilnya dijadwalkan tepat di pop.
        /// </summary>
        void TickReveals(float dt)
        {
            if (_revealGap > 0f) _revealGap -= dt;
            if (_revealQueue.Count == 0 || _evoFx == null) return;

            if (!Player.Alive)
            {
                // Ritual di bawah kerudung game over cuma cahaya yang menyala untuk mayat.
                _revealQueue.Clear();
                return;
            }

            bool boardVisible = !_mapChoose && !_mapOpen && !_shopOpen && !_eventOpen &&
                _coverT <= 0f;
            if (!boardVisible || _revealGap > 0f) return;

            var reveal = _revealQueue[0];
            _revealQueue.RemoveAt(0);

            if (reveal.AtPactStrip)
            {
                // Rect dicari SEKARANG — stripnya sudah menggambar ikon baru (Redraw jalan
                // lebih dulu di Update). Slot hilang (rect kosong) = ritual dilewati diam.
                var iconRect = _pactStrip != null
                    ? _pactStrip.IconRect(reveal.PactSlot)
                    : new Rect(0f, 0f, 0f, 0f);

                if (iconRect.width > 0f)
                    _evoFx.Play(iconRect.center, iconRect.size * 1.6f, reveal.Sprite);
            }
            else if (reveal.Matched)
                _evoFx.PlayMatched(reveal.Center, reveal.Size, reveal.Angle, reveal.Sprite);
            else
                _evoFx.Play(reveal.Center, reveal.Size, reveal.Sprite);

            StartOutlineSweep(reveal.Inst, EvoRevealFx.PopAt);
            _revealGap = 0.4f;
        }

        /// <summary>
        /// The grid cell the held piece would occupy right now, or null when nothing is held or
        /// the cursor is off the grid. Petaknya persis yang ditunjuk kursor (tanpa magnet);
        /// sah-tidaknya divalidasi pemakainya lewat CanPlace/CanReplaceAt.
        /// </summary>
        PieceDefinition ResolveGhost(out Vector2Int origin)
        {
            origin = default;
            if (_held == null || !Player.Alive || Enemies.WaveActive) return null;

            var mouse = UiMouse;
            var cell = ScreenToCell(mouse);
            if (cell.x < 0) return null;

            origin = SnapTarget(cell, mouse);
            return _held;
        }

        /// <summary>
        /// Petak asal untuk piece yang sedang dipegang — SATU pintu untuk hover, ghost, dan klik.
        ///
        /// TANPA MAGNET, atas perintah pemilik project ("waktu di-drop di grid itu, ya beneran
        /// drop di grid ITU — gak usah ada snap grid lagi"): petak yang ditunjuk kursor adalah
        /// petaknya, persis aturan tas — satu-satunya penempatan yang selama ini terasa jujur.
        /// Seluruh pencarian tetangga (SnapAssist/NearestSpot/SwapBias, Ronde 8-9) DICABUT.
        /// Menimpa piece lama tetap jalan: semua pemanggil memvalidasi CanPlace/CanReplaceAt
        /// di petak ini sebelum menempatkan, dan yang tergusur jatuh ke lantai seperti biasa.
        /// </summary>
        Vector2Int SnapTarget(Vector2Int cell, Vector2 mouse)
        {
            return cell - AnchorOffset(_held, _heldRot);
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
            BuildEventFromPrefab();

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
        /// <summary>
        /// Kembaran BuildShopFromPrefab untuk panel KEJADIAN: prefab EventRig menyerahkan
        /// kotak-kotaknya, dan null di slot mana pun jatuh ke rumus hitungan lama.
        /// </summary>
        void BuildEventFromPrefab()
        {
            EventPanelOverride = null;
            EventOptionAOverride = null;
            EventOptionBOverride = null;
            EventRefuseOverride = null;
            _eventRigRoot = null;

            if (_theme == null || _theme.EventPrefab == null) return;

            var go = Instantiate(_theme.EventPrefab, _canvas.transform, false);
            go.name = "EventAnchors";

            // Alasan yang sama dengan ShopAnchors di atas.
            if (_eventBg != null)
                go.transform.SetSiblingIndex(_eventBg.transform.GetSiblingIndex() + 1);

            var rig = go.GetComponent<EventRig>() ?? go.GetComponentInChildren<EventRig>(true);
            if (rig == null)
            {
                Debug.LogWarning("[GrimoireUI] EventPrefab tidak punya EventRig - tata letak " +
                                 "hitungan lama yang dipakai.");
                Destroy(go);
                return;
            }

            Canvas.ForceUpdateCanvases();

            if (rig.Panel != null) EventPanelOverride = CanvasRectOf(rig.Panel);
            if (rig.OptionA != null) EventOptionAOverride = CanvasRectOf(rig.OptionA);
            if (rig.OptionB != null) EventOptionBOverride = CanvasRectOf(rig.OptionB);
            if (rig.Refuse != null) EventRefuseOverride = CanvasRectOf(rig.Refuse);

            // ---- ADOPSI VISUAL, aturan yang sama dengan toko di atas ----
            if (rig.Panel != null)
            {
                var img = rig.Panel.GetComponent<Image>();
                if (img != null)
                {
                    _eventBg = img;
                    _eventBg.enabled = false;
                    _eventVisualsFromPrefab = true;
                }

                var title = FindTmpChild(rig.Panel, "Title");
                if (title != null) { _eventTitle = title; _eventTitle.enabled = false; }

                var body = FindTmpChild(rig.Panel, "Body");
                if (body != null) { _eventBody = body; _eventBody.enabled = false; }
            }

            if (_eventVisualsFromPrefab)
            {
                var imgA = rig.OptionA != null ? rig.OptionA.GetComponent<Image>() : null;
                if (imgA != null) { _eventABg = imgA; _eventABg.enabled = false; }
                var labelA = FindTmpChild(rig.OptionA, "Label");
                if (labelA != null) { _eventALabel = labelA; _eventALabel.enabled = false; }

                var imgB = rig.OptionB != null ? rig.OptionB.GetComponent<Image>() : null;
                if (imgB != null) { _eventBBg = imgB; _eventBBg.enabled = false; }
                var labelB = FindTmpChild(rig.OptionB, "Label");
                if (labelB != null) { _eventBLabel = labelB; _eventBLabel.enabled = false; }

                var imgC = rig.Refuse != null ? rig.Refuse.GetComponent<Image>() : null;
                if (imgC != null) { _eventCBg = imgC; _eventCBg.enabled = false; }
                var labelC = FindTmpChild(rig.Refuse, "Label");
                if (labelC != null) { _eventCLabel = labelC; _eventCLabel.enabled = false; }

                _eventCardsSkinned = imgA != null && imgA.sprite != null
                                  && imgB != null && imgB.sprite != null;
            }

            // Kotak dan visualnya sudah selesai dibaca; POHONNYA DIPADAMKAN sampai pemain
            // mendarat di pulau kejadian. Satu SetActive menutup seluruh isi prefab sekaligus —
            // termasuk dekor yang ditata tangan dan tidak dikenal daftar adopsi di atas.
            _eventRigRoot = go;
            go.SetActive(false);
        }

        /// <summary>
        /// TMP bernama <paramref name="childName"/> DI MANA PUN di bawah kotak rig, atau null.
        ///
        /// Anak langsung didahulukan; kalau tidak ada, dicari sampai ke dalam. Pencarian satu
        /// tingkat yang dulu dipakai di sini punya mode gagal yang diam-diam: begitu teksnya
        /// DIPINDAHKAN ke dalam bingkai atau header yang ditata tangan di prefab, ia bukan lagi
        /// anak langsung panel — hasilnya null, TMP prefab tidak pernah terisi teks, dan objek
        /// gambar-kode penggantinya tertinggal menyala di TENGAH layar karena penataan letaknya
        /// justru dilewati (prefab dianggap sudah mengurusnya sendiri).
        /// </summary>
        static TextMeshProUGUI FindTmpChild(Component root, string childName)
        {
            if (root == null) return null;

            var t = root.transform.Find(childName);
            if (t != null)
            {
                var direct = t.GetComponent<TextMeshProUGUI>();
                if (direct != null) return direct;
            }

            var all = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name == childName) return all[i];

            return null;
        }

        /// <summary>
        /// Kotak bernama <paramref name="childName"/> di mana pun di bawah <paramref name="root"/>,
        /// atau null. Aturan pencariannya sama dengan <see cref="FindTmpChild"/>: anak langsung
        /// dulu, baru menembus ke dalam.
        /// </summary>
        static RectTransform FindRectChild(Component root, string childName)
        {
            if (root == null) return null;

            var direct = root.transform.Find(childName) as RectTransform;
            if (direct != null) return direct;

            var all = root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name == childName) return all[i];

            return null;
        }

        void BuildShopFromPrefab()
        {
            ShopPanelOverride = null;
            ShopSlotsOverride = null;
            ShopRerollOverride = null;
            StartButtonOverride = null;
            ShopIconsOverride = null;
            _shopRigRoot = null;

            if (_theme == null || _theme.ShopPrefab == null) return;

            var go = Instantiate(_theme.ShopPrefab, _canvas.transform, false);
            go.name = "ShopAnchors";

            // DITARIK ke lapisan latar panel gambar-kode lama. Prefab ini lahir PALING AKHIR
            // di kanvas, jadi tanpa ini chip-nya menutupi kolam gambar piece yang lahir lebih
            // dulu - etalase tampil, barang dagangannya lenyap di belakang kartunya sendiri.
            if (_panelBg != null)
                go.transform.SetSiblingIndex(_panelBg.transform.GetSiblingIndex() + 1);

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

            // ---- ADOPSI VISUAL: prefab yang membawa Image/TMP-nya sendiri MENANG ----
            // Perintah pemilik project: "jangan hard code, buat semuanya prefab biar gw bisa
            // edit sendiri". Objek gambar-kode lama tetap lahir sebagai jaring untuk prefab
            // yang belum lengkap; yang teradopsi menggantikan referensinya, dan yang lama
            // tinggal diam dalam keadaan mati.
            if (rig.Panel != null)
            {
                var img = rig.Panel.GetComponent<Image>();
                if (img != null)
                {
                    _panelBg = img;
                    _panelBg.enabled = false;
                    _shopVisualsFromPrefab = true;
                }

                var title = FindTmpChild(rig.Panel, "Title");
                if (title != null)
                {
                    _panelTitle = title;
                    _panelTitleInk = title.color;
                    _panelTitle.enabled = false;
                }

            }

            if (_shopVisualsFromPrefab && rig.Slots != null)
            {
                for (int i = 0; i < rig.Slots.Length && i < ShopSlots; i++)
                {
                    if (rig.Slots[i] == null) continue;

                    var img = rig.Slots[i].GetComponent<Image>();
                    if (img != null)
                    {
                        _shopSlotBg[i] = img;
                        _shopSlotBg[i].enabled = false;
                        _shopSlotsSkinned = img.sprite != null;
                    }

                    var label = FindTmpChild(rig.Slots[i], "Label");
                    if (label != null)
                    {
                        _shopSlotText[i] = label;
                        _shopSlotText[i].enabled = false;
                    }
                }
            }

            if (_shopVisualsFromPrefab && rig.Reroll != null)
            {
                var img = rig.Reroll.GetComponent<Image>();
                if (img != null) { _rerollBg = img; _rerollBg.enabled = false; }

                var label = FindTmpChild(rig.Reroll, "Label");
                if (label != null) { _rerollLabel = label; _rerollLabel.enabled = false; }
            }

            // Rig HUD combat lebih tinggi pangkatnya untuk dua tombol ini: kalau kotaknya ada
            // di sana, rig toko tidak boleh menimpanya — dua sumber yang rebutan berakhir
            // dengan tombol yang meloncat tergantung siapa yang terakhir bicara.
            // Kursi tombol LANJUT dari rig TOKO DIBUANG: rumah tombol itu sekarang SATU —
            // pojok kanan-bawah (default StartButtonRect) atau kotak rig HUD — dan kursi
            // ketiga dari toko persis yang pernah menghidupkan kembali tombol tengah layar.
            bool hudOwnsShop = _hudRig != null && _hudRig.ShopToggle != null;
            if (!hudOwnsShop && rig.ShopToggle != null)
                ShopButtonOverride = CanvasRectOf(rig.ShopToggle);

            if (rig.Slots != null && rig.Slots.Length > 0)
            {
                var slots = new Rect[rig.Slots.Length];
                var icons = new Rect[rig.Slots.Length];
                bool any = false;
                bool anyIcon = false;

                for (int i = 0; i < rig.Slots.Length; i++)
                {
                    if (rig.Slots[i] == null) continue;
                    slots[i] = CanvasRectOf(rig.Slots[i]);
                    any = true;

                    // Anak "Icon" = jatah ruang GAMBAR di kartu itu. Kosong = pemusatan
                    // hitungan lama, jadi slot yang belum diberi kotak tidak berubah apa pun.
                    var icon = FindRectChild(rig.Slots[i], "Icon");
                    if (icon != null)
                    {
                        icons[i] = CanvasRectOf(icon);
                        anyIcon = true;
                    }
                }

                if (any) ShopSlotsOverride = slots;
                if (anyIcon) ShopIconsOverride = icons;
            }

            // Tombol TOKO: hit-test membaca
            // ShopButtonRect(), gambarnya lahir di BuildShop. Tanpa penyalinan ini, menggeser
            // kotaknya di prefab memindah kliknya saja dan tombolnya tertinggal di tempat lama.
            // Guard hudOwnsShop: alasan yang sama dengan hudOwnsStart di atas.
            if (!hudOwnsShop && ShopButtonOverride.HasValue && _shopBtnBg != null)
            {
                var r = ShopButtonOverride.Value;
                var centre = new Vector2(r.center.x - ScreenW * 0.5f,
                                         r.center.y - ScreenH * 0.5f);

                _shopBtnBg.rectTransform.anchoredPosition = centre;
                _shopBtnBg.rectTransform.sizeDelta = r.size;

                if (_shopBtnLabel != null)
                {
                    _shopBtnLabel.rectTransform.anchoredPosition = centre;
                    _shopBtnLabel.rectTransform.sizeDelta = r.size;
                }
            }

            // Dipadamkan sampai toko dibuka, alasan yang sama dengan panel kejadian. Kotak
            // StartButton & ShopToggle di prefab ini murni PENANDA LETAK — tidak membawa Image,
            // dan gambarnya lahir terpisah di BuildShop — jadi tombolnya tetap tampil selagi
            // pohon ini padam.
            _shopRigRoot = go;
            go.SetActive(false);
        }

        void OnRestEntered(RunNodeKind kind)
        {
            _shopOpen = false;
            _eventOpen = false;

            if (kind == RunNodeKind.Shop)
            {
                // Stok DIKOCOK tiap kali singgah — node toko yang isinya itu-itu saja bukan
                // hadiah, cuma etalase.
                RollShop();
                _shopOpen = true;
            }
            else if (kind == RunNodeKind.Event)
            {
                _eventOpen = true;
                _eventDone = false;

                // Diundi di sini, sekali. Yang sudah dipegang disaring di dalam RollPacts — tawaran
                // yang berisi pakta yang sudah dimiliki adalah pilihan yang tidak melakukan apa-apa,
                // dan pemain baru tahu setelah mengkliknya, lalu kehilangan seluruh kejadiannya.
                // Yang PERNAH TAMPIL juga disaring (_pactsOffered): satu pakta = satu kali muncul
                // per run, ditolak pun tidak antre lagi — keluhan "KEBANGKITAN nongol dua kali".
                _db.RollPacts(Player.Pacts, _pactOffer, _pactsOffered);

                for (int i = 0; i < _pactOffer.Length; i++)
                    if (_pactOffer[i] != null) _pactsOffered.Add(_pactOffer[i]);
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

            // Tinta di atas perkamen: judul DAN legenda dua-duanya DIBUANG atas permintaan
            // pemilik project — peta ber-icon besar sudah bercerita sendiri, dan baris teks
            // kecil di bawah cuma terbaca sebagai kotoran ("text gak jelas").
            Centre(_mapBg.rectTransform);

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
            _mapGlyphs = new TextMeshProUGUI[nodeCap];
            _mapIcons = new Image[nodeCap];
            _mapClearedMarks = new Image[nodeCap];

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

                _mapGlyphs[i] = MakeTmp("MapGlyph" + i, Vector2.zero, new Vector2(36f, 30f), 16,
                    Color.black, Vector2.zero, TextAlignmentOptions.Center);
                _mapGlyphs[i].transform.SetParent(_mapRoot, false);
                Centre(_mapGlyphs[i].rectTransform);
                _mapGlyphs[i].enabled = false;

                // Icon jenis node — gambar dari tema, dibuat SETELAH glyph supaya tergambar di
                // atas kotaknya sendiri. Glyph hurufnya tetap ada sebagai cadangan: tema tanpa
                // icon kembali ke huruf, aturan yang sama dengan semua sprite tema lainnya.
                _mapIcons[i] = MakeImage("MapIcon" + i, Vector2.zero, new Vector2(26f, 26f),
                    Color.black, Vector2.zero);
                _mapIcons[i].transform.SetParent(_mapRoot, false);
                Centre(_mapIcons[i].rectTransform);
                _mapIcons[i].preserveAspect = true;
                _mapIcons[i].enabled = false;

                // Tanda BERES - lahir paling akhir supaya tergambar DI ATAS icon nodenya.
                _mapClearedMarks[i] = MakeImage("MapCleared" + i, Vector2.zero, new Vector2(30f, 30f),
                    Color.white, Vector2.zero);
                _mapClearedMarks[i].transform.SetParent(_mapRoot, false);
                Centre(_mapClearedMarks[i].rectTransform);
                _mapClearedMarks[i].preserveAspect = true;
                _mapClearedMarks[i].raycastTarget = false;
                _mapClearedMarks[i].enabled = false;
            }

            // KARAKTER pemain di peta: HITAM pekat, mengikuti palet peta rujukannya —
            // semua MUSUH bertinta merah, jadi satu-satunya tinta hitam di perkamen ini
            // otomatis terbaca "itu gw" tanpa label; tulisan KAMU dibuang atas permintaan
            // yang sama. Gambarnya icon dari tema kalau ada; cadangannya bulatan `_circle`
            // yang sudah dibuat BuildSkillWidgets (jalan lebih dulu, lihat Init) — sprite
            // bulat bawaan Unity TIDAK bisa dipakai: GetBuiltinResource gagal saat runtime.
            var youIcon = _theme != null ? _theme.MapIconYou : null;

            _mapMark = MakeImage("MapMark", Vector2.zero, new Vector2(72f, 72f),
                new Color(0.07f, 0.05f, 0.04f, 1f), Vector2.zero);
            _mapMark.transform.SetParent(_mapRoot, false);
            Centre(_mapMark.rectTransform);
            _mapMark.sprite = youIcon != null ? youIcon : _circle;
            _mapMark.preserveAspect = true;
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

            // ---- kejadian ----
            _eventBg = MakeImage("EventBg", Vector2.zero, new Vector2(PanelW, PanelH),
                new Color(0.055f, 0.05f, 0.09f, 0.97f), Vector2.zero);
            // DialogBox dari kit Panels; kotak gelap polos hanya kalau art-nya belum dipasang.
            if (!Skin(_eventBg, _theme != null ? _theme.DialogPanel : null))
                Frame(_eventBg, 1.5f);
            _eventTitle = MakeTmp("EventTitle", Vector2.zero, new Vector2(500f, 30f), 22,
                TextGold, Vector2.zero, TextAlignmentOptions.Center);
            _eventBody = MakeTmp("EventBody", Vector2.zero, new Vector2(540f, 140f), 17,
                TextBone, Vector2.zero, TextAlignmentOptions.Center);
            _eventABg = MakeImage("EventA", Vector2.zero, Vector2.zero,
                new Color(0.2f, 0.4f, 0.25f, 0.95f), Vector2.zero);
            _eventBBg = MakeImage("EventB", Vector2.zero, Vector2.zero,
                new Color(0.4f, 0.24f, 0.45f, 0.95f), Vector2.zero);

            // Chip dari kit Panels untuk kedua kartu pakta. Flag-nya dibaca PaintPactCard:
            // sprite membawa gelapnya sendiri, jadi resep warnanya beda dengan kotak polos.
            _eventCardsSkinned = Skin(_eventABg, _theme != null ? _theme.CardChip : null)
                               & Skin(_eventBBg, _theme != null ? _theme.CardChip : null);
            if (!_eventCardsSkinned) { Frame(_eventABg); Frame(_eventBBg); }
            // Kartu pakta membawa tiga baris — nama, berkah, kutuk — jadi kotaknya lebih tinggi
            // dan hurufnya lebih kecil dari label tombol biasa. Dua baris pertama boleh dibaca
            // sekilas; baris kutuk justru yang harus dibaca pelan, dan itu tidak muat di 60 piksel.
            _eventALabel = MakeTmp("EventALabel", Vector2.zero, new Vector2(276f, 126f), 16,
                Color.white, Vector2.zero, TextAlignmentOptions.Center);
            _eventBLabel = MakeTmp("EventBLabel", Vector2.zero, new Vector2(276f, 126f), 16,
                Color.white, Vector2.zero, TextAlignmentOptions.Center);

            _eventCBg = MakeImage("EventC", Vector2.zero, Vector2.zero,
                PanelInk, Vector2.zero);
            if (!Skin(_eventCBg, _theme != null ? _theme.ButtonFrame : null))
                Frame(_eventCBg);
            _eventCLabel = MakeTmp("EventCLabel", Vector2.zero, new Vector2(240f, 30f), 16,
                TextDim, Vector2.zero, TextAlignmentOptions.Center);

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

        /// <summary>
        /// Menyalakan / mematikan tanda BERES sebuah node.
        ///
        /// Sprite-nya dari tema (MapIconCleared - "nanti gw tambahin image X"); selama slot itu
        /// kosong, placeholder X digenerate sekali dan dipakai terus. Placeholder di kode, bukan
        /// aset, supaya tandanya hidup hari ini juga dan tinggal ditukar dari Inspector nanti.
        /// </summary>
        void ShowClearedMark(int i, bool on, Vector2 pos, float nodeSize)
        {
            var mark = _mapClearedMarks != null && i < _mapClearedMarks.Length ? _mapClearedMarks[i] : null;
            if (mark == null) return;

            mark.enabled = on;
            if (!on) return;

            var sprite = _theme != null && _theme.MapIconCleared != null
                ? _theme.MapIconCleared
                : ClearedPlaceholder();

            mark.sprite = sprite;
            mark.rectTransform.anchoredPosition = pos;

            // Sedikit lebih kecil dari nodenya: tanda menumpang di atas icon, bukan menggantikan.
            float side = nodeSize * 0.72f;
            mark.rectTransform.sizeDelta = new Vector2(side, side);
            mark.rectTransform.localEulerAngles = Vector3.zero;

            // Tinta gelap pekat - "matikan warnanya": tandanya netral, bukan merah.
            mark.color = new Color(0.16f, 0.13f, 0.11f, 0.9f);
        }

        /// <summary>Placeholder X: dua garis diagonal di tekstur 64px, dibuat sekali.</summary>
        Sprite ClearedPlaceholder()
        {
            if (_clearedPlaceholder != null) return _clearedPlaceholder;

            const int size = 64;
            const float half = 5.5f;   // setengah tebal garis, piksel

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Jarak ke dua diagonal; di dalam salah satunya = bagian dari X.
                float d1 = Mathf.Abs(x - y) * 0.7071f;
                float d2 = Mathf.Abs(x + y - (size - 1)) * 0.7071f;
                float d = Mathf.Min(d1, d2);

                // Tepi dihaluskan 1,5 px supaya tidak bergerigi di peta.
                float a = Mathf.Clamp01((half - d) / 1.5f);

                // Ujung-ujung X dipangkas melingkar supaya tidak menyentuh sudut tekstur.
                float cx = x - (size - 1) * 0.5f, cy = y - (size - 1) * 0.5f;
                float r = Mathf.Sqrt(cx * cx + cy * cy) / ((size - 1) * 0.5f);
                a *= Mathf.Clamp01((1f - r) / 0.12f + 1f) * (r > 1f ? 0f : 1f);

                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            tex.name = "ClearedX";

            _clearedPlaceholder = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return _clearedPlaceholder;
        }

        /// <summary>Widget yang diposisikan lewat titik TENGAHNYA — pivot bawaan MakeImage ada
        /// di pojok, dan panel yang dihitung dari pojok selalu meleset separuh ukurannya.</summary>
        static void Centre(RectTransform rt) => rt.pivot = new Vector2(0.5f, 0.5f);

        static Rect EventOptionRect(int side)
        {
            // Kotak rig menang; rumus lama cuma untuk prefab yang belum lengkap.
            if (side == 0 && EventOptionAOverride.HasValue) return EventOptionAOverride.Value;
            if (side == 1 && EventOptionBOverride.HasValue) return EventOptionBOverride.Value;

            var panel = EventPanelRect();
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
            if (EventRefuseOverride.HasValue) return EventRefuseOverride.Value;

            var panel = EventPanelRect();
            return new Rect(panel.center.x - 118f, panel.yMin + 16f, 236f, 32f);
        }

        void DrawRunPanels(float dt)
        {
            bool hasRun = _run != null;

            UpdateMapTransition(dt);
            DrawMapOverlay();
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
                if (_mapGloom != null) _mapGloom.enabled = open;
            }

            if (!open)
            {
                _mapSig = -1;
                if (_mapMark != null) _mapMark.enabled = false;

                for (int i = 0; i < _mapNodes.Length; i++)
                {
                    if (_mapNodes[i] == null) continue;
                    _mapNodes[i].enabled = false;
                    _mapRings[i].enabled = false;
                    _mapGlyphs[i].enabled = false;
                    _mapIcons[i].enabled = false;

                    // Tanda X ikut jalur sembunyi yang sama. Tanpa baris ini ia tertinggal
                    // menyala di koordinat kanvas terakhirnya - dan di atas arena itu terbaca
                    // sebagai "ada X misterius di tanah", persis yang dilaporkan.
                    if (i < _mapClearedMarks.Length && _mapClearedMarks[i] != null)
                        _mapClearedMarks[i].enabled = false;
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
                    Vector2 now = UiMouse;
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
        /// KARAKTER pemain di peta: icon merah yang berdiri di node sekarang — atau di ruang
        /// tunggu bawah peta sebelum langkah pertama act — dan BERJALAN menyusuri jalur begitu
        /// node berikutnya dipilih. Tanpa label: icon yang jelas tidak butuh keterangan.
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

            // Ketidakrapian yang DIATUR, bukan diundi lebar-lebar. Versi lama menggeser
            // seluruh lantai secara acak sampai ±25% jarak lajur LALU menambah jitter ±17,5%
            // — worst case node melenceng ±42% jarak lajur, nyaris bertukar kolom, dan peta
            // terbaca "kacau" (laporan pemilik project). Aturan baru:
            //
            //   1. GRID dasar: lajur × colW, lantai × MapFloorGap — jarak antar titik tetap.
            //   2. OFFSET pola bata: lantai genap geser kiri, ganjil geser kanan, ±6% colW —
            //      cukup untuk mematahkan kolom lurus-tabel, tapi arahnya bisa ditebak mata.
            //   3. JITTER per node ±4,5% colW / ±8 px — aksen kecil ber-seed, peta tetap diam.
            //
            // Worst case ±10,5% jarak lajur. Ronde pertama pakai bata ±10% + jitter ±9%
            // (worst ±19%) dan masih "terkesan berantakan" kata pemilik project — kolomnya
            // harus KELIHATAN lurus dulu, baru goyangan halusnya terbaca sebagai gaya,
            // bukan sebagai kekacauan.
            float floorShift = (n.Floor % 2 == 0 ? -1f : 1f) * colW * 0.06f;

            uint h = (uint)((n.Index + 1) * 2654435761u);
            float jx = ((h & 0xFF) / 255f - 0.5f) * colW * 0.09f;
            float jy = (((h >> 8) & 0xFF) / 255f - 0.5f) * 16f;

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

        /// <summary>
        /// Warna jalur yang SEDANG DITAWARKAN.
        ///
        /// Dulu emas (1; 0,85; 0,3) — dan itu lahir waktu latar peta masih biru-gelap. Peta
        /// sekarang PERKAMEN KUNING, dan emas di atas kuning bukan sekadar kurang kontras:
        /// keduanya lebur jadi satu permukaan. Laporan pemilik project: <i>"jangan kuning,
        /// map gw kuning, itu jadi nge-blend dan gak keliat"</i>.
        ///
        /// Bacanya dari tema supaya bisa disetel mata tanpa menyentuh kode lagi — dan angka
        /// cadangan di sini bukan emas lagi, jadi tema yang lupa diisi pun tetap terbaca.
        /// </summary>
        Color OfferedPathInk =>
            _theme != null ? _theme.MapPathOfferedInk : new Color(0.78f, 0.09f, 0.07f, 1f);

        /// <summary>Warna jalur yang sudah dilalui. Alasan yang sama: hijau pucat hilang di perkamen.</summary>
        Color WalkedPathInk =>
            _theme != null ? _theme.MapPathWalkedInk : new Color(0.16f, 0.42f, 0.20f, 0.9f);


        /// <summary>Batas atas scroll: sisa tinggi act yang tidak muat di panel.
        /// Konstantanya = ruang tunggu bawah (310) + kepala boss (210 — node boss 4x dari
        /// dasar yang sudah digedekan, setengah badannya 150-an) + jendela atas (60).</summary>
        float MapScrollMax(RunMap map, Rect panel) =>
            Mathf.Max(0f, (map.Floors - 1) * MapFloorGap - (panel.height - 580f));

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


            int seg = 0;

            foreach (var n in map.Nodes)
            {
                Vector2 a = MapNodePos(n, panel, map.Floors, map.Lanes);

                foreach (int nextIndex in n.Next)
                {
                    Vector2 b = MapNodePos(map.Nodes[nextIndex], panel, map.Floors, map.Lanes);

                    bool walked = _run.Trail.Contains(n.Index) && _run.Trail.Contains(nextIndex);
                    bool offered = map.At == n.Index && reachable.Contains(map.Nodes[nextIndex]);

                    Color tone = walked ? WalkedPathInk
                        : offered ? OfferedPathInk
                        : _theme != null ? _theme.MapPathInk
                        : new Color(0.55f, 0.58f, 0.66f, 0.38f);

                    seg = DrawTrail(a, b, n.Index * 31 + nextIndex * 7, tone, seg);
                }
            }

            // Pemain adalah simpul pertama peta: sebelum langkah pertama act, jalur TAWARAN
            // terbentang dari ruang tunggunya ke SEMUA pilihan lantai pertama — persis
            // coretan pemilik project di screenshot-nya.
            if (map.At < 0)
            {
                Vector2 entry = MapEntryPos(panel);

                foreach (var n in map.Nodes)
                {
                    if (n.Floor != 0) continue;

                    seg = DrawTrail(entry, MapNodePos(n, panel, map.Floors, map.Lanes),
                        EntrySeed(n.Index), OfferedPathInk, seg);
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

                Sprite icon = live ? KindIcon(n.Kind) : null;

                // Dengan icon, kotak DAN cincinnya ikut hilang — "gak usah pake holder,
                // langsung warnai iconnya" kata pemilik project. Status yang dulu dibawa
                // cincin pindah ke warna dan ukuran iconnya sendiri; jalur emas dan hijau
                // di bawahnya tetap menunjukkan mana yang bisa dituju dan mana yang lewat.
                _mapNodes[i].enabled = show && icon == null;
                _mapRings[i].enabled = show && icon == null;
                _mapGlyphs[i].enabled = show && icon == null;
                _mapIcons[i].enabled = show && icon != null;
                if (_mapClearedMarks[i] != null && !show) _mapClearedMarks[i].enabled = false;
                if (!show) continue;

                bool now = map.At == n.Index;
                bool next = reachable.Contains(n);
                bool walked = _run.Trail.Contains(n.Index);

                // Jitter rotasi ala peta referensi — ber-seed, jadi diam di tempat.
                uint h = (uint)((n.Index + 1) * 1274126177u);
                float twist = (((h >> 8) & 0xFF) / 255f - 0.5f) * 14f;

                // Ukuran membawa DUA hal sekaligus, dan urutannya penting.
                //
                // Yang pertama status: yang sedang diinjak paling besar, yang bisa dituju sedang,
                // sisanya kecil. Itu menjawab "aku boleh ke mana".
                //
                // Yang kedua JENIS, dan ini yang baru. Peta yang semua nodenya sebesar satu sama
                // lain menuntut membaca hurufnya satu per satu untuk tahu ada apa di depan.
                // Ukuran menjawab itu dari jarak pandang: yang besar berarti besar taruhannya.
                // Dua kali dinaikkan dan dua kali masih "kekecilan" — sekarang seukuran node
                // peta Slay the Spire yang jadi rujukannya: icon adalah ISI petanya, bukan
                // hiasan di antara jalur. Jalurnya cuma pengantar antar icon.
                float size = MapNodeSize(n, now, next);

                var tone = RunDirector.KindColor(n.Kind);
                Color ring;

                if (now) ring = new Color(1f, 0.85f, 0.4f, 1f);
                // Putih lenyap di perkamen — cincin "berikutnya" harus jadi tinta, bukan sorotan.
                else if (next) ring = _theme != null ? _theme.MapRingInk : Color.white;
                else if (walked)
                {
                    // Sama dengan cabang icon: warna jenis DIMATIKAN untuk yang sudah beres.
                    ring = new Color(0.5f, 0.5f, 0.48f, 0.5f);
                    tone = new Color(0.45f, 0.44f, 0.42f, 0.75f);
                }
                else
                {
                    // Terkunci: DIPUCATKAN, bukan sekadar dipudarkan — warna jenisnya masih
                    // terbaca (buat merencanakan jalur), tapi jelas belum bisa diinjak.
                    tone = Color.Lerp(tone, new Color(0.3f, 0.32f, 0.38f), 0.55f);
                    tone.a = 0.55f;
                    ring = new Color(0f, 0f, 0f, 0.35f);
                }

                if (icon != null)
                {
                    // Icon-nya SENDIRI yang jadi node — tanpa kotak, tanpa cincin. Warnanya
                    // TINTA ala peta rujukan (Slay the Spire): musuh merah bata, elite merah
                    // bara, sisanya tinta gelap di keluarga warna jenisnya. Palet KindColor
                    // yang lama terang — dipakai langsung di icon, ia lenyap di perkamen.
                    var inkTone = KindInk(n.Kind);

                    // BERES = warnanya DIMATIKAN, bukan sekadar dipudarkan. Elite yang sudah
                    // ditumbangkan tidak boleh tetap menyala merah - merah artinya "bahaya di
                    // depan", dan node di belakangmu bukan bahaya lagi. Abu tinta perkamen,
                    // plus tanda X di atasnya (sprite tema, atau placeholder generatan).
                    if (walked && !now) inkTone = new Color(0.42f, 0.4f, 0.37f, 0.5f);
                    else if (!now && !next)
                    {
                        // Terkunci: dipucatkan ke arah abu perkamen — jenisnya masih kebaca
                        // buat merencanakan jalur, tapi jelas belum bisa diinjak.
                        inkTone = Color.Lerp(inkTone, new Color(0.5f, 0.46f, 0.4f), 0.45f);
                        inkTone.a = 0.62f;
                    }

                    _mapIcons[i].sprite = icon;
                    _mapIcons[i].rectTransform.anchoredPosition = pos;
                    _mapIcons[i].rectTransform.sizeDelta = new Vector2(size, size);
                    _mapIcons[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, twist);
                    _mapIcons[i].color = inkTone;

                    ShowClearedMark(i, walked && !now, pos, size);
                }
                else
                {
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

                    ShowClearedMark(i, walked && !now, pos, size);

                    // Hurufnya ikut membesar bersama kotaknya. Ukuran tetap membuat huruf di
                    // node boss yang empat kali lipat terlihat seperti tersasar di tengah kotak
                    // kosong — dan justru node itu yang paling perlu terbaca.
                    _mapGlyphs[i].fontSize = Mathf.Max(10, Mathf.RoundToInt(size * 0.46f));
                    _mapGlyphs[i].rectTransform.sizeDelta = new Vector2(size, size);
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
        /// membunuh, toko menghabiskan koin, kejadian cuma menawarkan pilihan. Yang paling
        /// banyak jumlahnya — pertarungan biasa — sengaja jadi yang paling kecil, karena
        /// node yang muncul di mana-mana tidak perlu meminta perhatian.
        /// </summary>
        /// <summary>
        /// Ukuran GAMBAR satu node peta, satuan kanvas. SATU sumber untuk DUA pemakai: yang
        /// menggambarnya dan yang menguji kliknya.
        ///
        /// Dulu keduanya punya angka sendiri, dan itu bug: gambar memakai
        /// <c>(104/88/76) × KindScale</c> — radius 44 px untuk Fight sampai 176 px untuk Boss —
        /// sementara hit-test memakai radius DATAR 34 px. Akibatnya dua arah sekaligus:
        /// pinggiran icon besar MATI kliknya (lebih dari separuh badan Elite, 96% badan Boss),
        /// dan icon besar menutupi titik tengah tetangganya sehingga klik di atas gambar A
        /// terbaca sebagai node B. Selama dua angka ini hidup terpisah, bug itu pasti balik.
        /// </summary>
        static float MapNodeSize(RunNode n, bool now, bool next)
        {
            float size = (now ? 104f : next ? 88f : 76f) * KindScale(n.Kind);

            // Jitter ber-seed, jadi diam di tempat. Boss dikecualikan: ia satu-satunya node
            // yang ukurannya BERARTI SESUATU secara mutlak, dan boss yang kebetulan diundi
            // kecil akan berhenti terbaca sebagai puncak act.
            if (n.Kind != RunNodeKind.Boss)
            {
                uint h = (uint)((n.Index + 1) * 1274126177u);
                size *= 1f + ((h & 0xFF) / 255f - 0.5f) * 0.24f;
            }

            return size;
        }

        static float KindScale(RunNodeKind kind)
        {
            // Selisihnya DILEBARKAN atas permintaan pemilik project: "ukurannya jangan sama".
            // Beda 10-20% tidak pernah terbaca dari jarak pandang peta — beda antar jenis
            // harus kelihatan tanpa membandingkan dua node berdampingan.
            switch (kind)
            {
                case RunNodeKind.Boss: return 4f;
                case RunNodeKind.Elite: return 1.7f;
                case RunNodeKind.Shop: return 1.5f;
                case RunNodeKind.Event: return 1.35f;
                default: return 1f;
            }
        }

        /// <summary>
        /// Warna TINTA icon per jenis — mengikuti peta rujukan (Slay the Spire): pertarungan
        /// merah bata, elite merah bara yang lebih menyala, boss merah paling pekat; toko,
        /// kejadian, dan slot tinta gelap di keluarga warnanya masing-masing. Pemain HITAM,
        /// satu-satunya tinta hitam pekat di peta. KindColor yang lama tetap dipakai kotak
        /// cadangan — palet terangnya memang untuk kotak berhuruf, bukan untuk icon telanjang.
        /// </summary>
        static Color KindInk(RunNodeKind kind)
        {
            switch (kind)
            {
                case RunNodeKind.Elite: return new Color(0.78f, 0.2f, 0.06f);
                case RunNodeKind.Shop: return new Color(0.52f, 0.36f, 0.05f);
                case RunNodeKind.Event: return new Color(0.38f, 0.24f, 0.5f);
                case RunNodeKind.Boss: return new Color(0.5f, 0.05f, 0.05f);
                default: return new Color(0.58f, 0.14f, 0.1f);
            }
        }

        /// <summary>
        /// Icon jenis node dari tema. Null = tema belum membawa gambarnya, dan node itu kembali
        /// ke huruf — aturan yang sama dengan seluruh sprite tema: art yang belum jadi tidak
        /// pernah memblokir petanya.
        /// </summary>
        Sprite KindIcon(RunNodeKind kind)
        {
            if (_theme == null) return null;

            switch (kind)
            {
                case RunNodeKind.Boss: return _theme.MapIconBoss;
                case RunNodeKind.Elite: return _theme.MapIconElite;
                case RunNodeKind.Shop: return _theme.MapIconShop;
                case RunNodeKind.Event: return _theme.MapIconEvent;
                default: return _theme.MapIconFight;
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

        /// <summary>
        /// Mengambil pakta yang ditawarkan. Berkah dan kutuknya masuk BERSAMAAN — tidak ada jalan
        /// mengambil separuhnya, dan itu seluruh isi mekanik ini.
        /// </summary>
        void TakePact(int slot)
        {
            var pact = slot >= 0 && slot < _pactOffer.Length ? _pactOffer[slot] : null;

            // Slot kosong tidak digambar lagi (DrawEvent menyembunyikan kartunya), jadi klik
            // di kotak hantunya tidak boleh diam-diam membayar koin — koin diambil lewat
            // tombol bawah yang memang kelihatan.
            if (pact == null) return;

            if (Player.Pacts == null || !Player.Pacts.Take(pact)) return;

            Announce(PactName(pact), pact.Color);

            // Letupan pakta, revisi KETIGA dan yang final dari pemilik project: "distorsinya
            // di tengah, GAK ADA icon, terus keluar sfx". Satu gelombang distorsi sekejap
            // membelah tengah layar plus fanfare — ikonnya tidak dimainkan di mana-mana;
            // ia cukup muncul di strip pakta, tempat tinggalnya. (Revisi 1: sigil besar di
            // tengah kamera — ditolak. Revisi 2: reveal ikon di kartu — juga ditolak.)
            if (_evoFx != null)
                _evoFx.PlayRingOnly(new Vector2(ScreenW * 0.5f, ScreenH * 0.5f), 1500f);

            // Ikon paktanya sendiri LAHIR dengan ritual kecil — putih dulu, lalu menampakkan
            // wujud — tepat di slotnya di strip pakta ("icon di sini gak putih dulu, gw kan
            // mau di putih dulu terus muncul" — pemilik project, menunjuk strip). Diantre,
            // bukan langsung: rect slotnya baru ada setelah strip menggambar ikon barunya.
            if (_evoFx != null && pact.Icon != null && Player.Pacts != null)
            {
                _revealQueue.Add(new PendingReveal
                {
                    Sprite = pact.Icon,
                    AtPactStrip = true,
                    PactSlot = Player.Pacts.Count - 1
                });
            }

            if (Sfx != null) Sfx.EvolveFanfare();

            // Pakta ukuran (ADDENDUM papan / DEEP POCKETS tas): berubah SEKARANG, di depan
            // mata — hadiah yang baru terasa wave depan bukan hadiah, cuma janji.
            if (pact.GridPlus > 0 || pact.BagPlus > 0) ApplyPactLayout();

            _pactOffer[0] = null;
            _pactOffer[1] = null;
            _eventDone = true;
            _eventOpen = false;
        }

        /// <summary>
        /// Menyamakan ukuran papan DAN tas dengan jatah pakta (Base + GridBonus/BagBonus),
        /// lalu mendudukkan ulang petaknya. Kotak GridArea milik prefab TIDAK berubah —
        /// sel papan mengecil mengisi kotak yang sama (Step membagi kotak dengan jumlah
        /// sel), tas mengikuti sel papan (BagCell => CellSize) sehingga kolom kanan
        /// bergeser nol piksel, dan tas yang melebar tumbuh ke kanan-atas dari pangkal
        /// yang sama: tidak ada yang bisa tumpang tindih dengan buku.
        /// </summary>
        void ApplyPactLayout()
        {
            var pacts = Player != null ? Player.Pacts : null;
            int gridBonus = pacts != null ? pacts.GridBonus : 0;
            int bagBonus = pacts != null ? pacts.BagBonus : 0;

            bool changed = false;

            int w = Grimoire.BaseWidth + gridBonus;
            int h = Grimoire.BaseHeight + gridBonus;
            if (w != Grimoire.Width || h != Grimoire.Height)
            {
                Grimoire.SetSize(w, h);
                ReseatBoardCells();
                changed = true;
            }

            int bw = Backpack.BaseWidth + bagBonus;
            int bh = Backpack.BaseHeight + bagBonus;
            if (bw != Backpack.Width || bh != Backpack.Height)
            {
                Backpack.SetSize(bw, bh);
                changed = true;
            }

            // Tas didudukkan ulang untuk KEDUA jalur: papan yang berubah menggeser ukuran
            // sel tas, tas yang berubah menggeser jumlah petaknya.
            if (changed) ReseatBag();
        }

        /// <summary>Menolak keduanya. Bayarannya koin — kecil, karena melewatkan harus terasa.</summary>
        void RefusePact()
        {
            _gold += _balance.EventGoldGift;
            Announce(Loc.F("event.gift", _balance.EventGoldGift), new Color(1f, 0.84f, 0.32f));

            _pactOffer[0] = null;
            _pactOffer[1] = null;
            _eventDone = true;
            _eventOpen = false;
        }

        void DrawEvent()
        {
            bool open = _eventOpen && _run != null;

            // SATU POHON, satu saklar. Ini yang menutup dekor tambahan di prefab; baris-baris
            // .enabled di bawah tinggal mengurus visual gambar-kode untuk tema TANPA prefab.
            // Ditaruh SEBELUM penjaga null di bawah: prefab yang panelnya belum diberi Image
            // tetap harus bisa dipadamkan.
            if (_eventRigRoot != null) _eventRigRoot.SetActive(open);

            if (_eventBg == null) return;

            // Kartu yang tidak punya pakta TIDAK digambar sama sekali — bukan kartu abu-abu
            // "tawaran koin" lama. Perintah pemilik project: kalau tidak ada buff yang bisa
            // dipakai, hadiahnya koin lewat SATU tombol, sisanya hilang.
            bool hasA = open && _pactOffer[0] != null;
            bool hasB = open && _pactOffer[1] != null;

            _eventBg.enabled = open;
            _eventTitle.enabled = open;
            _eventBody.enabled = open;
            _eventABg.enabled = hasA;
            _eventBBg.enabled = hasB;
            _eventALabel.enabled = hasA;
            _eventBLabel.enabled = hasB;
            _eventCBg.enabled = open;
            _eventCLabel.enabled = open;

            if (!open) return;

            var panel = EventPanelRect();
            if (!_eventVisualsFromPrefab)
                _eventBg.rectTransform.anchoredPosition = panel.center;

            if (!_eventVisualsFromPrefab)
            {
                _eventBg.rectTransform.sizeDelta = panel.size;
                _eventTitle.rectTransform.anchoredPosition = new Vector2(panel.center.x, panel.yMax - 28f);
                _eventBody.rectTransform.anchoredPosition = new Vector2(panel.center.x, panel.yMax - 92f);
            }

            _eventTitle.text = Loc.T("event.title");

            // Katalog kering = kalimat pamitan, bukan tawaran dagang yang tidak ada barangnya.
            _eventBody.text = Loc.T(hasA || hasB ? "event.body" : "event.empty");

            if (hasA) PaintPactCard(0, EventOptionRect(0), _eventABg, _eventALabel);
            if (hasB) PaintPactCard(1, EventOptionRect(1), _eventBBg, _eventBLabel);

            var c = EventRefuseRect();
            if (!_eventVisualsFromPrefab)
            {
                _eventCBg.rectTransform.anchoredPosition = c.center;
                _eventCBg.rectTransform.sizeDelta = c.size;
                _eventCLabel.rectTransform.anchoredPosition = c.center;
            }
            // Tanpa satu pun tawaran, "MENOLAK" bohong — tidak ada yang ditolak. Tombolnya
            // berganti kalimat jadi ambil koin, dan RefusePact yang sama tetap membayarnya.
            _eventCLabel.text = Loc.F(hasA || hasB ? "event.refuse" : "event.takegold",
                _balance.EventGoldGift);
        }

        /// <summary>
        /// Satu kartu pakta: nama, sisi untung, sisi rugi.
        ///
        /// Warnanya diambil dari paktanya sendiri dan DIGELAPKAN, bukan dipakai apa adanya — warna
        /// pakta dipilih supaya terbaca sebagai ikon 26 piksel di atas latar gelap, dan bidang
        /// seluas 292x132 dengan warna yang sama menelan tulisan putih di atasnya.
        /// </summary>
        void PaintPactCard(int slot, Rect area, Image bg, TextMeshProUGUI label)
        {
            if (!_eventVisualsFromPrefab)
            {
                bg.rectTransform.anchoredPosition = area.center;
                bg.rectTransform.sizeDelta = area.size;
                label.rectTransform.anchoredPosition = area.center;
            }

            var pact = slot < _pactOffer.Length ? _pactOffer[slot] : null;

            // Slot kosong tidak pernah sampai ke sini lagi — DrawEvent menyembunyikan kartunya
            // (aturan "hadiah koin lewat satu tombol"). Penjaga ini tinggal jaring.
            if (pact == null) return;

            var tone = pact.Color;

            // Tint pada Image itu PERKALIAN. Kotak polos butuh digelapkan sendiri (0,32x) supaya
            // teks putih terbaca; sprite Chip sudah membawa gelapnya, jadi cukup disemu ke arah
            // warna paktanya - mengalikan 0,32 ke sprite gelap menghasilkan kartu nyaris hitam.
            bg.color = _eventCardsSkinned
                ? Color.Lerp(Color.white, tone, 0.45f)
                : new Color(tone.r * 0.32f, tone.g * 0.32f, tone.b * 0.32f, 0.96f);

            _sb.Length = 0;
            _sb.Append(PactName(pact)).Append("\n\n");
            _sb.Append("+  ").Append(PactBoon(pact));

            // Pakta tanpa bane (ADDENDUM) tidak menggambar baris minus kosong — tanda "-" yang
            // tidak diikuti apa pun terbaca sebagai teks yang gagal dimuat.
            var bane = PactBane(pact);
            if (!string.IsNullOrEmpty(bane)) _sb.Append("\n\n").Append("-  ").Append(bane);

            label.text = _sb.ToString();
        }

        void Redraw()
        {
            RefreshHudSeats();
            BeginArtFrame();

            DrawGrid();
            DrawPlacedArt();
            DrawRuneOutlines();
            DrawEvoLines();
            DrawSkillWidgets(Time.deltaTime);
            DrawBackpack();
            DrawLoose();
            DrawSpells();
            DrawHud();
            DrawBuffs();
            DrawRunPanels(Time.deltaTime);
            UpdateTooltip();

            DrawHurtVeil();
            DrawFlash(Time.unscaledDeltaTime);

            // Paling akhir: kerudungnya harus menutupi SEMUA yang digambar di atas, termasuk
            // kartu hover dan panel yang kebetulan masih terbuka saat pemain mati.
            DrawGameOver();

            // Sesudah SEMUA penggambar (papan, lantai, toko) mengambil jatah art-nya.
            EndArtFrame();
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

            var hoverMouse = UiMouse;
            var hover = ScreenToCell(hoverMouse);
            if (hover.x < 0) return;

            // Petak yang sama dengan klik — dan MENIMPA dihitung sah: dulu preview-nya merah
            // padahal kliknya menimpa dengan sukses, jadi papan terasa melarang hal yang
            // sebenarnya ia izinkan.
            var origin = SnapTarget(hover, hoverMouse);
            bool valid = Book.CanPlace(_held, origin, _heldRot) ||
                         Book.CanReplaceAt(_held, origin, _heldRot);
            var shape = Shapes.Rotate(_held.Cells, _heldRot);

            // Yang boleh ditaruh memakai warnanya SENDIRI, bukan hijau. Hijau adalah warna
            // kelima di papan yang sudah penuh warna, dan ia menjawab pertanyaan yang sudah
            // dijawab bentuknya: benda yang muncul rapi di petaknya berarti muat.
            var tint = valid
                ? new Color(_held.Color.r, _held.Color.g, _held.Color.b, 0.9f)
                : InvalidCell;

            // Rune sudah digambar sebagai dirinya sendiri oleh lapisan tile - lihat
            // DrawHeldPreview - jadi menimpanya dengan kotak polos di sini akan mengembalikan
            // persis yang baru saja dihapus.
            bool tiledPreview = RuneTiles.IsRuneGlyph(_held.Icon);

            for (int i = 0; i < shape.Length; i++)
            {
                var c = origin + shape[i];
                if (!Grimoire.InBounds(c)) continue;

                int idx = c.y * Grimoire.Width + c.x;
                if (_held.Layer == Layer.Rune)
                {
                    if (tiledPreview) continue;

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

        // ------------------------------------------------------------------ art piece (dari SO)
        //
        // Pembaca setelan "Art di papan" milik PieceDefinition: Art + Offset/Ukuran/Putar/
        // DiBelakangPetak. HANYA membaca — menatanya tetap kerja tangan di jendela Bentuk
        // Grid / Inspector, dan matematikanya disalin SATU BANDING SATU dari editor itu
        // supaya pratinjau tidak pernah bohong.

        RectTransform _artBehindLayer;
        RectTransform _artFrontLayer;

        /// <summary>
        /// Lapisan art KETIGA, khusus piece yang menggeletak/dipajang/dibawa kursor. Terpisah
        /// karena petak tercecer dinaikkan ke atas panel saat dibangun — art di lapisan depan
        /// biasa tetap kalah tinggi dan gambarnya tenggelam di balik kotak petaknya sendiri.
        /// </summary>
        RectTransform _artLooseLayer;

        readonly List<Image> _artBehindPool = new List<Image>();
        readonly List<Image> _artFrontPool = new List<Image>();
        readonly List<Image> _artLoosePool = new List<Image>();
        int _artLooseUsed;
        int _artBehindUsed;
        int _artFrontUsed;

        RectTransform MakeArtLayer(string name)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        void BeginArtFrame()
        {
            _artBehindUsed = 0;
            _artFrontUsed = 0;
            _artLooseUsed = 0;
        }

        void EndArtFrame()
        {
            for (int i = _artBehindUsed; i < _artBehindPool.Count; i++)
                _artBehindPool[i].enabled = false;
            for (int i = _artFrontUsed; i < _artFrontPool.Count; i++)
                _artFrontPool[i].enabled = false;
            for (int i = _artLooseUsed; i < _artLoosePool.Count; i++)
                _artLoosePool[i].enabled = false;
        }

        Image TakeArt(bool behind, bool loose = false)
        {
            var layer = loose ? _artLooseLayer : behind ? _artBehindLayer : _artFrontLayer;
            var pool = loose ? _artLoosePool : behind ? _artBehindPool : _artFrontPool;
            int used = loose ? _artLooseUsed++ : behind ? _artBehindUsed++ : _artFrontUsed++;

            while (pool.Count <= used)
            {
                var go = new GameObject("PieceArt_" + pool.Count);
                go.transform.SetParent(layer, false);

                var img = go.AddComponent<Image>();
                img.raycastTarget = false;

                var rt = img.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                pool.Add(img);
            }

            var image = pool[used];
            image.enabled = true;
            return image;
        }

        /// <summary>
        /// Menggambar Art sebuah piece mengikuti setelan SO-nya — matematika yang SAMA
        /// dengan jendela Bentuk Grid: ukuran nol = kotak pembatas bentuk, offset dalam
        /// satuan petak dari pojok kiri-bawah, putaran positif searah jarum jam. Rotasi
        /// piece di papan ikut memutar art beserta offsetnya.
        /// </summary>
        /// <summary>
        /// Warna item yang BELUM TERPASANG di papan — isi tas dan piece yang menggeletak.
        ///
        /// Diredupkan DAN dipucatkan sedikit, bukan cuma diturunkan alpha-nya: alpha saja membuat
        /// ikon terlihat "setengah jadi" di atas latar gelap, sementara warna abu membuatnya
        /// terbaca sebagai barang yang MENUNGGU. Pemilik project: "bingung gw mana item kepasang
        /// mana engga" — begitu terpasang ia kembali ke warna aslinya, dan perbedaan itu yang
        /// menjawab pertanyaannya dalam sekali lihat, tanpa perlu hover satu-satu.
        /// </summary>
        static readonly Color StashedTint = new Color(0.52f, 0.52f, 0.58f, 1f);

        Image DrawPieceArt(PieceDefinition def, Vector2 shapeBottomLeft, int rot,
            float cellPx, float gapPx, float alpha, bool loose = false, bool dim = false)
        {
            // RUNE TIDAK DISENTUH — perintah pemilik project: rune sudah benar lewat
            // lapisan tile per-petak (RuneTiles). Art dari SO hanya untuk skill & segel.
            if (def == null || def.IsRune) return null;

            if (_artFrontLayer == null) return null;

            // Rumusnya pindah ke PieceArt.Layout — satu sumber yang juga dipakai preview
            // starter di menu, supaya art yang sama tidak pernah ditata dua cara berbeda.
            Vector2 center, size;
            float angle;
            if (!PieceArt.Layout(def, shapeBottomLeft, rot, cellPx, gapPx,
                out center, out size, out angle)) return null;

            var img = TakeArt(def.ArtBehindCells, loose);
            img.sprite = def.Art;
            var ink = dim ? StashedTint : Color.white;
            img.color = new Color(ink.r, ink.g, ink.b, alpha);

            // Pool-nya dipakai bergantian oleh gambar biasa, lapisan ISI jam cooldown, dan
            // ikon terpusat — status render di-reset di sini supaya bekas fillAmount/letupan/
            // preserveAspect frame lalu tidak menempel di piece lain yang kebagian Image sama.
            img.type = Image.Type.Simple;
            img.fillAmount = 1f;
            img.preserveAspect = false;

            var rt = img.rectTransform;
            rt.localScale = Vector3.one;
            rt.sizeDelta = size;
            rt.anchoredPosition = center;
            rt.localEulerAngles = new Vector3(0f, 0f, angle);
            return img;
        }

        /// <summary>
        /// Ikon piece dibentangkan di pusat footprint-nya — jalan visual untuk piece yang tidak
        /// punya Art papan (mis. segel). Aturan satu-glyph-per-piece yang sama dengan codex dan
        /// layar starter, supaya benda yang sama tampil serupa di mana pun.
        /// </summary>
        Image DrawPieceIcon(PieceDefinition def, Vector2 shapeBottomLeft, int rot,
            float cellPx, float gapPx, float alpha, bool loose = false, bool dim = false)
        {
            // Rune dan ikon glyph rune tidak lewat sini — mereka sudah digambar sebagai tile
            // per petak, dan ikon terpusat di atasnya jadi gambar dobel.
            if (def == null || def.IsRune || def.Icon == null) return null;
            if (RuneTiles.IsRuneGlyph(def.Icon)) return null;

            var shape = Shapes.Rotate(def.Cells, rot);
            int maxX = 0, maxY = 0;
            foreach (var c in shape)
            {
                if (c.x > maxX) maxX = c.x;
                if (c.y > maxY) maxY = c.y;
            }

            float w = (maxX + 1) * cellPx + maxX * gapPx;
            float h = (maxY + 1) * cellPx + maxY * gapPx;

            // Ikon besar di tengah HANYA untuk bentuk yang mengisi penuh kotak pembatasnya
            // (Dot, Line, Square). Bentuk bolong — diagonal, L — membuat ikon di tengah kotak
            // ngambang di antara sel dan menumpangi petak piece TETANGGA. Untuk mereka ikonnya
            // duduk di SEL JANGKAR (sel pertama bentuknya), dan sisa footprint dibaca dari
            // chip petaknya.
            bool solid = shape.Length == (maxX + 1) * (maxY + 1);

            float side;
            Vector2 at;

            if (solid)
            {
                side = Mathf.Min(w, h) * 0.92f;
                at = shapeBottomLeft + new Vector2(w * 0.5f, h * 0.5f);
            }
            else
            {
                float step = cellPx + gapPx;
                side = cellPx * 0.92f;
                at = shapeBottomLeft + new Vector2(
                    shape[0].x * step + cellPx * 0.5f,
                    shape[0].y * step + cellPx * 0.5f);
            }

            var img = TakeArt(false, loose);
            img.sprite = def.Icon;
            var ink = dim ? StashedTint : Color.white;
            img.color = new Color(ink.r, ink.g, ink.b, alpha);
            img.type = Image.Type.Simple;
            img.fillAmount = 1f;
            img.preserveAspect = true;

            var rt = img.rectTransform;
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(side, side);
            rt.anchoredPosition = at;
            rt.localEulerAngles = Vector3.zero;
            return img;
        }

        /// <summary>
        /// SATU pintu visual piece: Art papan kalau ada, ikon terpusat kalau tidak. Kotak warna
        /// primitif bukan lagi rupa piece di mana pun — perintah pemilik project.
        /// </summary>
        Image DrawPieceVisual(PieceDefinition def, Vector2 shapeBottomLeft, int rot,
            float cellPx, float gapPx, float alpha, bool loose = false, bool dim = false)
        {
            return def != null && def.Art != null
                ? DrawPieceArt(def, shapeBottomLeft, rot, cellPx, gapPx, alpha, loose, dim)
                : DrawPieceIcon(def, shapeBottomLeft, rot, cellPx, gapPx, alpha, loose, dim);
        }

        void DrawPlacedArt()
        {
            var spells = Book.Spells;

            for (int i = 0; i < Book.Placed.Count; i++)
            {
                var inst = Book.Placed[i];
                var def = inst.Def;
                if (def.IsRune) continue;
                if (def.Art == null && def.Icon == null) continue;

                var anchor = CellAnchor(inst.Origin.x, inst.Origin.y);

                // Skill ber-cooldown digambar DUA LAPIS dari art-nya sendiri — redup di bawah,
                // isi ber-fillAmount di atas — menggantikan cincin primitif yang dulu menumpang
                // di centroid. Isinya merangkak naik mengikuti cooldown, penuh berarti siap,
                // dan meletup membesar saat cast (nilai _pulse diisi OnSpellCast).
                int spellIdx = -1;
                for (int s = 0; s < spells.Count; s++)
                {
                    if (spells[s].Source == inst) { spellIdx = s; break; }
                }

                if (spellIdx >= 0)
                {
                    var sp = spells[spellIdx];
                    float progress = sp.Cooldown <= 0f
                        ? 1f
                        : 1f - Mathf.Clamp01(sp.Source.CdTimer / sp.Cooldown);

                    var dim = DrawPieceVisual(def, anchor, inst.Rot, CellSize, CellGap, 1f);
                    if (dim != null) dim.color = new Color(0.30f, 0.29f, 0.36f, 0.95f);

                    var fill = DrawPieceVisual(def, anchor, inst.Rot, CellSize, CellGap, 1f);
                    if (fill != null)
                    {
                        // MUTER, bukan naik dari bawah — permintaan pemilik project: isi yang
                        // menyapu melingkar searah jarum jam, seperti jam cooldown ARPG.
                        fill.type = Image.Type.Filled;
                        fill.fillMethod = Image.FillMethod.Radial360;
                        fill.fillOrigin = (int)Image.Origin360.Top;
                        fill.fillClockwise = true;
                        fill.fillAmount = progress;

                        // Bahasa warna cincin lama dipertahankan: BIRU berarti mananya yang
                        // kurang, bukan cooldown-nya yang belum pulih.
                        bool manaStarved = progress >= 1f && Player.Mana < def.ManaCost;
                        fill.color = manaStarved ? new Color(0.55f, 0.7f, 1f, 0.9f) : Color.white;

                        // 0,6, dulu 0,35 — letupannya harus KERASA, bukan sekadar kedip.
                        float pulse = spellIdx < _pulse.Length ? _pulse[spellIdx] : 0f;
                        fill.rectTransform.localScale = Vector3.one * (1f + pulse * 0.6f);
                    }
                }
                else
                {
                    DrawPieceVisual(def, anchor, inst.Rot, CellSize, CellGap, 1f);
                }

                // Petak primitif di bawah art: yang punya ART (menutup bentuk aslinya)
                // disembunyikan penuh; yang cuma punya IKON diredupkan, bukan dimatikan —
                // ikon jangkarnya cuma duduk di satu sel, dan footprint sisanya harus tetap
                // terbaca untuk penempatan & resep.
                float cellAlpha = def.ArtBehindCells ? 0.45f
                    : def.Art != null ? 0f
                    : 0.35f;

                foreach (var at in inst.Cells())
                {
                    if (!Grimoire.InBounds(at)) continue;

                    int idx = at.y * Grimoire.Width + at.x;
                    var target = _skillCells[idx];
                    var col = target.color;
                    col.a *= cellAlpha;
                    target.color = col;
                }
            }
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
                c => Book.BaseAt(c), true);
        }

        void DrawBagTiles()
        {
            // stashed: isi tas belum terpasang, rune-nya ikut aturan abu-abu.
            DrawTileLayer(ref _bagTiles, _bagCells, Backpack.Width, Backpack.Height,
                c => _bag.At(c), false, stashed: true);
        }

        /// <summary>
        /// Lapisan tile generik untuk grid petak mana pun (papan &amp; tas). Letak tiap tile
        /// diambil dari petak yang bersangkutan, bukan dihitung ulang dari rumus: begitu papan
        /// digeser atau petaknya diperbesar lewat prefab, tile-nya ikut tanpa diberi tahu.
        /// </summary>
        void DrawTileLayer(ref RuneTilePool pool, Image[] cells, int width, int height,
            System.Func<Vector2Int, RuneInstance> at, bool previewHeld, bool stashed = false)
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
                        tile.Cover(under.rectTransform, CellGap / Mathf.Max(1f, CellSize));
                        // stashed (tas) = belum terpasang: tint rune disemu ke abu, aturan
                        // yang sama dengan piece tercecer dan ikon skill di tas.
                        var tileTint = RuneTiles.AreaTint(inst.Def, k, inst.Def.Color);
                        if (stashed) tileTint = Color.Lerp(tileTint, new Color(0.45f, 0.45f, 0.5f), 0.55f);
                        tile.Bind(RuneTiles.BakedTileAt(inst.Def, k), RuneTiles.GlyphAt(inst.Def, k),
                            tileTint, alpha);
                    }
                }
            }

            if (previewHeld) DrawHeldPreview(pool, cells, width, height);

            pool.End();
        }

        /// <summary>
        /// Rune yang sedang dipegang, digambar SEBAGAI DIRINYA di petak yang akan ditempatinya:
        /// utuh kalau boleh, merah kalau tidak.
        ///
        /// Sebelumnya petaknya cuma diwarnai hijau atau merah, dan itu salah dua kali. Hijau
        /// mengajarkan bahasa yang tidak perlu ada - benda yang muncul rapi di tempatnya sudah
        /// mengatakan "boleh" tanpa satu warna pun - dan kotak polos itu membuat rune yang
        /// sedang dibawa BERUBAH WUJUD tepat pada saat pemain paling butuh melihat bentuknya,
        /// yaitu saat sedang mengukur muat atau tidak.
        /// </summary>
        void DrawHeldPreview(RuneTilePool pool, Image[] cells, int width, int height)
        {
            if (_held == null || !RuneTiles.IsRuneGlyph(_held.Icon)) return;

            var hover = ScreenToCell(UiMouse);
            if (hover.x < 0) return;

            var origin = hover - AnchorOffset(_held, _heldRot);
            bool blocked = !Book.CanPlace(_held, origin, _heldRot);
            var shape = Shapes.Rotate(_held.Cells, _heldRot);
            float bleed = CellGap / Mathf.Max(1f, CellSize);

            for (int k = 0; k < shape.Length; k++)
            {
                var c = origin + shape[k];
                if (c.x < 0 || c.y < 0 || c.x >= width || c.y >= height) continue;

                var tile = pool.Take();
                tile.Cover(cells[c.y * width + c.x].rectTransform, bleed);
                tile.Bind(RuneTiles.BakedTileAt(_held, k), RuneTiles.GlyphAt(_held, k),
                    RuneTiles.AreaTint(_held, k, _held.Color), 0.9f, blocked);
            }
        }

        void DrawBackpack()
        {
            var emptyColor = _held != null ? ShownBagCell : HiddenBagCell;

            // Sel terisi bukan lagi kotak warna piece: hitam nyaris transparan sebagai alas,
            // ikon piece-nya yang bicara (digambar di bawah). Sel kosong tetap kisi tas.
            var filledChip = new Color(0f, 0f, 0f, 0.18f);

            for (int y = 0; y < Backpack.Height; y++)
            {
                for (int x = 0; x < Backpack.Width; x++)
                {
                    int i = y * Backpack.Width + x;
                    var stored = _bag.At(new Vector2Int(x, y));
                    _bagCells[i].color = stored != null ? filledChip : emptyColor;
                }
            }

            DrawBagTiles();

            // Ikon per piece di tas — satu glyph di pusat footprint, aturan yang sama dengan
            // papan dan layar starter. Dulu tas cuma kotak warna, dan pemain tidak pernah bisa
            // tahu APA yang disimpannya tanpa hover satu-satu.
            for (int i = 0; i < _bag.Placed.Count; i++)
            {
                var inst = _bag.Placed[i];
                if (inst == null || inst.Def == null) continue;

                // Isi tas = belum terpasang, jadi diredupkan. Lihat StashedTint.
                DrawPieceVisual(inst.Def, BagAnchor(inst.Origin.x, inst.Origin.y), inst.Rot,
                    BagCell, BagGap, 1f, loose: true, dim: true);
            }

            if (_held == null) return;

            var hover = ScreenToBagCell(UiMouse);
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

            // Piece yang dibawa SELALU TERGAMBAR — dulu ia disembunyikan di atas papan dan tas
            // karena footprint-nya sudah digambar di sana, tapi footprint itu petak polos:
            // ikonnya lenyap justru di detik pemain sedang menimbang penempatan. Yang berubah
            // sekarang cuma LETAKNYA (lihat HeldDrawPos), bukan ada-tidaknya.
            if (_held != null)
            {
                cursor = DrawPiece(_held, _heldRot, HeldDrawPos(), cursor, 0.9f);
            }
            else _heldDrawLive = false;

            for (int i = cursor; i < _looseCells.Length; i++) _looseCells[i].enabled = false;

            if (_looseTiles != null) _looseTiles.End();
        }

        /// <summary>Titik gambar piece di tangan saat ini, dan kotak tempat ia menuju.</summary>
        Vector2 _heldDrawPos;

        /// <summary>Salah selama tangan kosong — supaya piece berikutnya lahir DI kursor, bukan
        /// meluncur dari tempat piece sebelumnya dilepas.</summary>
        bool _heldDrawLive;

        /// <summary>
        /// Waktu paruh magnet gambar, dalam detik. Ini yang membedakan "menempel" dari
        /// "teleportasi": pada nol gambarnya berpindah petak seketika dan tiap perpindahan
        /// terbaca sebagai kedutan, sementara terlalu besar membuat gambar tertinggal di
        /// belakang kursor dan itu terbaca sebagai lag. 0,045 dtk ≈ dua-tiga frame di 60 fps.
        /// </summary>
        const float HeldSnapTau = 0.045f;

        /// <summary>
        /// Di mana gambar piece yang sedang dibawa harus berada.
        ///
        /// DI ATAS PAPAN ia menempel ke petak yang benar-benar akan ditempatinya — bukan ke
        /// kursor. Inilah perbaikan atas "narik di grid gak sesmooth tas": selama gambarnya
        /// mengambang bebas sementara sorotan petak menempel ke kisi, mata melihat DUA benda
        /// yang bergerak dengan aturan berbeda, dan yang satu selalu terlihat meleset dari yang
        /// lain. Tas terasa jujur karena di sana tidak ada dua benda — cuma satu yang ikut
        /// kursor.
        ///
        /// Di luar papan ia kembali menunggangi kursor apa adanya: di lantai memang tidak ada
        /// kisi untuk ditempeli, dan menahannya di petak terakhir akan membuat piece yang
        /// ditarik keluar papan terlihat menyangkut.
        ///
        /// Perpindahannya DIHALUSKAN, bukan dipatok. Petak yang berganti seketika membuat
        /// gambar melompat satu petak penuh setiap kali kursor melewati garis papan — persis
        /// kedutan yang mau dihilangkan. Peredamnya kebal frame rate (eksponensial terhadap
        /// dt), jadi rasanya sama di 30 maupun 144 fps.
        /// </summary>
        Vector2 HeldDrawPos()
        {
            var mouse = UiMouse;
            var target = mouse;

            var cell = ScreenToCell(mouse);
            if (cell.x >= 0 && Player.Alive && !Enemies.WaveActive)
                target = GridDrawCentre(_held, _heldRot, SnapTarget(cell, mouse));

            if (!_heldDrawLive)
            {
                _heldDrawLive = true;
                _heldDrawPos = target;
                return _heldDrawPos;
            }

            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / HeldSnapTau);
            _heldDrawPos = Vector2.Lerp(_heldDrawPos, target, Mathf.Clamp01(k));
            return _heldDrawPos;
        }

        /// <summary>
        /// Pusat gambar piece kalau ia duduk di petak papan <paramref name="origin"/>.
        ///
        /// Sengaja diturunkan dari rumus <see cref="DrawPiece"/>, bukan dikira-kira: di sana
        /// gambarnya dipusatkan lalu petak ke-i digeser <c>shape[i] * step</c> dari sudut
        /// kiri-bawahnya. Menyamakan sudut itu dengan sudut petak papan memberi pusat ini.
        /// Kebetulan yang membuatnya rapi: <c>LooseCellSize</c>/<c>LooseCellGap</c> memang
        /// dipetakan ke <c>CellSize</c>/<c>CellGap</c>, jadi jarak antar petak gambar sudah
        /// sama dengan jarak antar petak papan — tidak ada penskalaan yang perlu dilakukan.
        /// </summary>
        static Vector2 GridDrawCentre(PieceDefinition def, int rot, Vector2Int origin)
        {
            float step = CellSize + CellGap;
            var size = PieceSize(Shapes.Rotate(def.Cells, rot));

            return new Vector2(GridX + origin.x * step + size.x * 0.5f,
                               GridY + origin.y * step + size.y * 0.5f);
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

            string kind = Loc.T(_held.Layer == Layer.Rune ? "held.kind.rune" : "held.kind.skill");
            // Nama & blurb piece lewat tabel bahasa - kunci piece.* sudah lengkap (272 kunci).
            _heldText.text = kind + " - " + PieceName(_held) + "   "
                           + Loc.T("piece." + _held.Id + ".blurb", _held.Blurb);
        }

        /// <summary>Detailed hover card + the ground ring showing the hovered skill's reach.</summary>
        void UpdateTooltip()
        {
            PieceDefinition hovered = null;
            CompiledSpell spell = null;
            string origin = "";

            // Strip icons win: they sit in the HUD corner, well away from the board, so a hit there
            // is unambiguous — and their whole reason to exist is answering "what is this".
            string strip = StripTooltip(UiMouse);
            string vitals = strip == null ? VitalsTooltip(UiMouse) : null;

            if (strip != null || vitals != null)
            {
                // Selama ALT ditahan, kartu resep yang sedang dibaca MENANG: kursor yang
                // kebetulan lewat di atas bola HP tidak boleh merampas panel yang dipatok.
                if (ProtoInput.AltHeld && _recipes.Visible)
                {
                    _tipBg.enabled = false;
                    _tipText.enabled = false;
                    return;
                }

                _recipes.Hide();

                // Dua jarak jatuh, dua alasan. Kartu BOLA HP/mana dijatuhkan jauh ke
                // bawah-kanan: bolanya besar, kartu yang lahir di kursor menutupi bola
                // sebelahnya. Kartu IKON STRIP (buff/kutukan/pakta) dulu ikut jarak itu dan
                // terasa lepas dari ikonnya ("hover buff jaraknya jauh" — pemilik project);
                // ikonnya kecil, cukup digeser sedikit supaya kursor tidak menutupi kartu.
                var drop = strip != null ? new Vector2(14f, -36f) : new Vector2(46f, -64f);
                ShowCard(strip ?? vitals, UiMouse + drop);
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
                var mouse = UiMouse;

                int looseIndex = ScreenToLoose(mouse);
                if (looseIndex >= 0)
                {
                    hovered = _loose[looseIndex];
                    origin = Loc.T("hud.origin.loose");
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
                            origin = Loc.T("hud.origin.bag");
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
                            origin = Loc.T(inst.Locked ? "hud.origin.locked" : "hud.origin.placed");
                            spell = FindSpell(inst);
                        }
                    }
                }
            }

            if (hovered == null)
            {
                // Kartu resep DIPATOK selama ALT ditahan: menarik mouse dari piece untuk
                // MEMBACA kartunya tidak boleh menutup kartu itu sendiri — dan ikon di
                // dalamnya boleh ditanya balik. Lepas ALT = tutup.
                if (ProtoInput.AltHeld && _recipes.Visible)
                {
                    InspectRecipeIcon();
                    Player.HideRange();
                    _lastHovered = null;
                    return;
                }

                _tipBg.enabled = false;
                _tipText.enabled = false;
                _recipes.Hide();
                Player.HideRange();
                _lastHovered = null;
                return;
            }

            // Berbunyi hanya saat SASARAN hover berganti. Tanpa gerbang ini ia berdesis
            // tiap frame selama kursor diam di atas piece yang sama.
            if (hovered != _lastHovered)
            {
                _lastHovered = hovered;
                if (Sfx != null) Sfx.UiHover();
            }

            // ALT swaps the stat card for the recipe card. They occupy the same corner of the
            // screen, so exactly one of them is ever up.
            if (ProtoInput.AltHeld)
            {
                // Kartu yang SUDAH terpampang TERKUNCI ke piece pertamanya: mouse yang
                // menyerempet item sebelah dalam perjalanan menuju kartu tidak boleh
                // mengganti bahan yang sedang diteliti. Ganti target = lepas ALT dulu.
                // Ikon DI DALAM kartu tetap boleh ditanya (InspectRecipeIcon).
                if (_recipes.Visible)
                {
                    InspectRecipeIcon();
                    return;
                }

                // Piece TANPA resep tidak punya apa pun untuk ditampilkan kartu ini — ALT-nya
                // jatuh kembali ke kartu keterangan biasa di bawah, bukan memunculkan kartu
                // kosong. Enam rune dasar persis kasus itu.
                if (_recipes.Show(hovered, UiMouse))
                {
                    _tipBg.enabled = false;
                    _tipText.enabled = false;
                    ShowHoverRange(hovered, spell);
                    return;
                }
            }

            _recipes.Hide();
            ShowCard(_tooltips.Build(hovered, spell, origin));
            ShowHoverRange(hovered, spell);
        }

        // 520 (dulu 380) atas permintaan pemilik project — dua kali dinilai kekecilan.
        const float TipWidthDefault = 520f;
        const float TipPadXDefault = 14f;

        /// <summary>
        /// Ikon DI DALAM kartu resep yang dipatok boleh ditanya balik: hover ikonnya
        /// memunculkan kartu keterangan item — "ini item apaan" — tanpa melepas patokan.
        /// </summary>
        void InspectRecipeIcon()
        {
            var inspect = _recipes.HoverPiece(UiMouse);

            if (inspect != null)
            {
                ShowCard(_tooltips.Build(inspect, null, Loc.T("hud.origin.recipe", "RESEP")));
                return;
            }

            _tipBg.enabled = false;
            _tipText.enabled = false;
        }
        const float TipPadYDefault = 12f;

        // Ukuran kartu hover: dibaca dari PREFAB kalau ada (lebar = lebar rect kartu, tepi =
        // jarak Body ke tepi kartu), kalau tidak jatuh ke angka lama. Kode tidak pernah menulis
        // lebar/tepi — yang ditulis cuma TINGGI (ikut panjang teks) dan POSISI (ikut kursor),
        // dan dua itu memang mustahil ditata tangan.
        float _tipWidth = TipWidthDefault;
        float _tipPadX = TipPadXDefault;
        float _tipPadY = TipPadYDefault;

        /// <summary>
        /// Kartu hover dari PREFAB. Lebar dan tepi dalamnya dibaca dari cetakan (kode tidak
        /// pernah menulisnya lagi); yang tetap ditulis kode cuma tinggi — ikut panjang teks —
        /// dan posisi, karena kartu ini mengikuti kursor dan itu mustahil ditata tangan.
        /// </summary>
        bool BuildTooltipFromPrefab()
        {
            var prefab = _theme != null ? _theme.TooltipPrefab : null;
            if (prefab == null) return false;

            var card = Instantiate(prefab, _canvas.transform, false);
            card.name = "TooltipCard";

            var bg = card.GetComponent<Image>();
            var bodyT = card.transform.Find("Body");
            var body = bodyT != null ? bodyT.GetComponent<TextMeshProUGUI>() : null;
            if (bg == null || body == null)
            {
                Debug.LogError("[GrimoireUI] TooltipPrefab butuh Image di root dan anak " +
                               "'Body' ber-TextMeshProUGUI.", card);
                Destroy(card);
                return false;
            }

            var cardRect = bg.rectTransform;
            var bodyRect = body.rectTransform;

            // Ukuran diambil SEBELUM pivot diseragamkan, selagi masih persis seperti di prefab.
            _tipWidth = Mathf.Max(40f, cardRect.rect.width);
            _tipPadX = Mathf.Max(0f, bodyRect.offsetMin.x);
            _tipPadY = Mathf.Max(0f, -bodyRect.offsetMax.y);
            if (_tipPadX < 0.5f && _tipPadY < 0.5f)
            {
                // Body dibentang penuh tanpa tepi — pakai tepi bawaan supaya teks tidak menempel
                // ke bingkai.
                _tipPadX = TipPadXDefault;
                _tipPadY = TipPadYDefault;
            }

            // Kode menaruh kartu lewat anchoredPosition kiri-atas; anchor/pivot diseragamkan
            // supaya angka itu berarti sama apa pun yang disetel di prefab.
            cardRect.anchorMin = cardRect.anchorMax = Vector2.zero;
            cardRect.pivot = new Vector2(0f, 1f);
            bodyRect.SetParent(_canvas.transform, false);
            bodyRect.anchorMin = bodyRect.anchorMax = Vector2.zero;
            bodyRect.pivot = new Vector2(0f, 1f);

            _tipBg = bg;
            _tipText = body;
            return true;
        }

        /// <summary>
        /// Menaruh kartu hover di sebelah kursor, dijepit supaya tidak pernah keluar layar.
        ///
        /// Tingginya DIHITUNG dari teksnya, tidak lagi dipatok 150 piksel. Kotak berukuran mati
        /// itu salah di dua arah sekaligus: segel dengan empat baris menyisakan sepertiga kotak
        /// kosong, sementara skill dengan sepuluh baris menulis keluar dari kotaknya sendiri —
        /// dan yang keluar itu justru baris terakhir, tempat blurb-nya berada.
        /// </summary>
        void ShowCard(string body, Vector2? at = null)
        {
            _tipText.text = body;

            var textRect = _tipText.rectTransform;
            float inner = _tipWidth - _tipPadX * 2f;

            // Lebar dikunci DULU: preferredHeight tanpa lebar yang pasti akan menjawab untuk
            // teks satu baris panjang, bukan untuk teks yang sudah dibungkus.
            // TMP: tinggi ditanya langsung untuk lebar tertentu — preferredHeight polos
            // menjawab untuk teks tanpa bungkus.
            float height = _tipText.GetPreferredValues(body, inner, 0f).y;
            textRect.sizeDelta = new Vector2(inner, height);

            float boxHeight = height + _tipPadY * 2f;
            _tipBg.rectTransform.sizeDelta = new Vector2(_tipWidth, boxHeight);

            var m = at ?? UiMouse;
            float x = Mathf.Min(m.x + 18f, ScreenW - _tipWidth - 8f);

            // Pivotnya kiri-ATAS, jadi y adalah tepi atas kartu: yang harus dijaga tetap di
            // dalam layar adalah DASARNYA, dan dasar itu bergantung tinggi kartunya.
            float y = Mathf.Clamp(m.y - 12f, boxHeight + 8f, ScreenH - 8f);

            _tipBg.rectTransform.anchoredPosition = new Vector2(x, y);
            textRect.anchoredPosition = new Vector2(x + _tipPadX, y - _tipPadY);
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

        /// <summary>
        /// Cincin cooldown primitif sudah pensiun: art piece-nya sendiri yang jadi jamnya,
        /// digambar DrawPlacedArt. Yang tersisa di sini cuma peluruhan letupan cast — nilainya
        /// dibaca DrawPlacedArt lewat indeks spell yang sama dengan yang diisi OnSpellCast.
        /// </summary>
        void DrawSkillWidgets(float dt)
        {
            for (int i = 0; i < _pulse.Length; i++)
                _pulse[i] = Mathf.MoveTowards(_pulse[i], 0f, dt * 3.5f);
        }

        int DrawPanels(int cursor)
        {
            bool eventOn = ShopEventActive;
            if (!eventOn) _shopOpen = false;

            // Alasan yang sama dengan panel kejadian: satu pohon, satu saklar.
            if (_shopRigRoot != null) _shopRigRoot.SetActive(_shopOpen);

            _panelBg.enabled = _shopOpen;
            _panelTitle.enabled = _shopOpen;
            _rerollBg.enabled = _shopOpen;
            _rerollLabel.enabled = _shopOpen;

            for (int i = 0; i < ShopSlots; i++)
            {
                _shopSlotBg[i].enabled = _shopOpen;
                _shopSlotText[i].enabled = _shopOpen;
            }

            if (!_shopOpen) return cursor;

            var panel = PanelRect();

            // Posisi & ukuran HANYA ditulis untuk visual gambar-kode. Yang datang dari prefab
            // sudah ditata tangan - menimpanya tiap frame persis "kenapa prefabku balik lagi"
            // yang berkali-kali dilaporkan.
            if (!_shopVisualsFromPrefab)
            {
                _panelBg.rectTransform.anchoredPosition = panel.center;
                _panelBg.rectTransform.sizeDelta = panel.size;

                _panelTitle.rectTransform.anchoredPosition = new Vector2(panel.xMin + 14f, panel.yMax - 8f);
                _panelTitle.rectTransform.pivot = new Vector2(0f, 1f);
            }

            // Judulnya yang menjawab tarikan yang ditolak — bukan pojok kiri layar. Baris ini
            // menempel di atas etalase, yaitu tempat mata pemain sudah berada saat ia mencoba.
            bool nag = _shopNag > 0f;
            _panelTitle.text = nag ? Loc.T("slot.nogold") : Loc.F("shop.title", _gold);
            _panelTitle.color = nag ? new Color(0.85f, 0.28f, 0.24f) : _panelTitleInk;

            for (int i = 0; i < ShopSlots; i++)
            {
                var rect = ShopSlotRect(i);
                if (!_shopVisualsFromPrefab)
                {
                    _shopSlotBg[i].rectTransform.anchoredPosition = new Vector2(rect.xMin, rect.yMin);
                    _shopSlotBg[i].rectTransform.sizeDelta = rect.size;
                    _shopSlotText[i].rectTransform.anchoredPosition = new Vector2(rect.xMin + 5f, rect.yMin + 6f);
                    _shopSlotText[i].rectTransform.sizeDelta = new Vector2(rect.width - 10f, 40f);
                }

                var def = _shop[i];
                if (def == null)
                {
                    _shopSlotBg[i].color = _shopSlotsSkinned
                        ? new Color(0.45f, 0.45f, 0.5f, 0.85f)
                        : new Color(0.1f, 0.1f, 0.12f, 0.7f);
                    _shopSlotText[i].text = Loc.T("shop.sold");
                    _shopSlotText[i].color = new Color(0.5f, 0.5f, 0.55f);
                    continue;
                }

                int price = _balance.PriceOf(def);
                bool afford = _gold >= price;

                // Sprite Chip membawa gelapnya sendiri - tint gelap lama membuatnya nyaris
                // hitam. Putih = terjangkau; semu merah pucat = koin kurang.
                _shopSlotBg[i].color = _shopSlotsSkinned
                    ? (afford ? Color.white : new Color(1f, 0.72f, 0.68f, 1f))
                    : (afford
                        ? new Color(0.15f, 0.16f, 0.22f, 0.95f)
                        : new Color(0.16f, 0.11f, 0.11f, 0.95f));

                _sb.Length = 0;
                _sb.Append(PieceName(def)).Append("  ").Append(Shapes.StarText(def.Stars)).Append('\n');
                _sb.Append(Loc.F("shop.price", price));
                _shopSlotText[i].text = _sb.ToString();
                _shopSlotText[i].color = afford ? Color.white : new Color(0.95f, 0.55f, 0.5f);

                var iconBox = ShopIconRect(i);
                if (iconBox.HasValue)
                {
                    // Kotak "Icon" yang ditata tangan MENANG: gambarnya dipaskan utuh ke dalamnya
                    // — petak DAN art — lalu dipusatkan di situ. Menggeser atau mengecilkan
                    // kotaknya di prefab memindahkan dan mengecilkan gambarnya, tanpa kode.
                    var box = iconBox.Value;

                    // Diukur ulang hanya saat isi slotnya BERGANTI: hasilnya tetap sama selama
                    // stoknya tidak dikocok, dan mengukur enam kali per frame cuma memberi makan
                    // GC lewat bentuk yang diputar dan art yang ditata ulang.
                    if (!ReferenceEquals(_shopBoundsFor[i], def))
                    {
                        PieceBounds(def, 0, out _shopBoundsSize[i], out _shopBoundsOffset[i]);
                        _shopBoundsFor[i] = def;
                    }

                    var bounds = _shopBoundsSize[i];

                    // Batas 1 = ukuran petak papan, DIPERTAHANKAN dari rumus lama. Tanpa ini,
                    // kotak yang lebih besar dari piece-nya akan menggelembungkan piece 1 petak
                    // sampai berkali-kali lipat: semua barang jadi tampak seukuran, bentuk
                    // footprint-nya tidak lagi terbaca dari etalase padahal itu informasi beli,
                    // dan garis tepi 2 px ikut membesar jadi kabur.
                    float fit = Mathf.Min(1f, box.width / bounds.x, box.height / bounds.y);
                    cursor = DrawPiece(def, 0, box.center - _shopBoundsOffset[i] * fit, cursor, 1f, fit);
                }
                else
                {
                    // Dipaskan ke kotaknya: 40 px bawah milik teks harga, sisanya milik piece.
                    var pieceSize = PieceSize(Shapes.Rotate(def.Cells, 0));
                    float fit = Mathf.Min(1f, (rect.width - 24f) / Mathf.Max(1f, pieceSize.x),
                        (rect.height - 60f) / Mathf.Max(1f, pieceSize.y));

                    cursor = DrawPiece(def, 0, new Vector2(rect.center.x, rect.center.y + 18f), cursor, 1f, fit);
                }
            }

            var reroll = RerollRect();
            if (!_shopVisualsFromPrefab)
            {
                _rerollBg.rectTransform.anchoredPosition = new Vector2(reroll.xMin, reroll.yMin);
                _rerollBg.rectTransform.sizeDelta = reroll.size;
            }
            _rerollBg.color = _gold >= _rerollCost
                ? new Color(0.28f, 0.36f, 0.18f, 0.95f)
                : new Color(0.3f, 0.15f, 0.13f, 0.95f);

            if (!_shopVisualsFromPrefab)
            {
                _rerollLabel.rectTransform.anchoredPosition = new Vector2(reroll.xMin, reroll.yMin + 8f);

                // Ukurannya ikut MASUK penjaga: di luar sini ia ditulis tiap frame, dan tinggi
                // label REROLL yang disetel tangan di prefab kembali ke 22 px sendiri.
                _rerollLabel.rectTransform.sizeDelta = new Vector2(reroll.width, 22f);
            }

            _rerollLabel.text = Loc.F("shop.reroll", _rerollCost);

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
        /// the board, and this runs every frame — <c>List.Sort</c> would allocate a comparer on a
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
            int shown = Mathf.Min(count, VisibleSpellRows);

            // Yang disembunyikan dihitung dari daftar PENUH, bukan dari kapasitas slot panel —
            // "+N" harus jujur soal berapa yang tidak kebagian baris.
            int hidden = Mathf.Max(0, spells.Count - shown);

            // Baris-baris dipaku dari judul ke bawah; yang mengikuti jumlah baris hanya
            // EKORNYA — baris "+N" dan angka total meter.
            if (shown != _spellRowsShown)
            {
                _spellRowsShown = shown;

                float tail = SpellPanelTop - 26f - shown * _spellRowPitch;
                _spellMore.rectTransform.anchoredPosition = new Vector2(SpellPanelRight - 8, tail);
            }

            _spellMore.enabled = hidden > 0;
            if (hidden > 0) _spellMore.text = Loc.F("hud.spells.more", hidden);

            for (int i = 0; i < MaxSpellRows; i++)
            {
                bool used = i < shown;
                _spellBg[i].enabled = used;
                _spellFill[i].enabled = used;
                _spellNotch[i].enabled = used;
                _spellText[i].enabled = used;
                if (!used) continue;

                var s = spells[_spellOrder[i]];
                float progress = s.Cooldown <= 0f ? 1f : 1f - Mathf.Clamp01(s.Source.CdTimer / s.Cooldown);
                _spellFill[i].fillAmount = progress;
                _spellFill[i].color = new Color(s.Source.Def.Color.r, s.Source.Def.Color.g,
                    s.Source.Def.Color.b, 0.35f);
                _spellNotch[i].color = new Color(s.Source.Def.Color.r, s.Source.Def.Color.g,
                    s.Source.Def.Color.b, 0.95f);

                _sb.Length = 0;
                // DisplayName mentah TETAP dipakai sebagai kunci meter di bawah - nama
                // terjemahan tidak boleh jadi kunci pencatatan damage, atau angkanya hilang
                // begitu bahasa diganti. Yang lewat Loc hanya yang DIGAMBAR.
                _sb.Append(i + 1).Append(". ").Append(PieceName(s.Source.Def));

                // Share of the run's damage, folded in from what used to be a separate meter panel.
                int share = _meter.ShareOf(s.Source.Def.DisplayName);
                if (share > 0) _sb.Append("  ").Append(share).Append('%');

                _sb.Append("   ").Append(BigNumber.Short(s.Damage)).Append(Loc.T("spell.dmg"));
                _sb.Append("   ").Append(BigNumber.Short(s.Cooldown <= 0f ? 0f : s.Damage / s.Cooldown))
                    .Append(Loc.T("spell.dps"));
                _sb.Append("   ").Append(s.Cooldown.ToString("0.00")).Append('s');
                _sb.Append("   ").Append(Mathf.RoundToInt(s.Source.Def.ManaCost)).Append(Loc.T("spell.mana"));

                if (s.DamageBonus > 0f) _sb.Append("  +").Append(Mathf.RoundToInt(s.DamageBonus * 100f)).Append("%D");
                if (s.CooldownBonus > 0f) _sb.Append("  -").Append(Mathf.RoundToInt(s.CooldownBonus * 100f)).Append("%CD");
                if (s.RadiusBonus > 0f) _sb.Append("  +").Append(Mathf.RoundToInt(s.RadiusBonus * 100f)).Append("%A");

                _spellText[i].text = _sb.ToString();
                _spellText[i].color = s.DamageBonus + s.CooldownBonus + s.RadiusBonus > 0f
                    ? new Color(1f, 0.92f, 0.55f)
                    : TextBone;
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
            _sb.Append(Loc.F("hud.line.wave", Enemies.Wave));

            // Cuma yang penting: STAGE, kills, koin - perintah pemilik project. "left x/y",
            // "enemies N", dan "FINISH THE REST" dicabut: jumlah musuh sudah kelihatan di
            // lapangan, dan baris yang isinya berubah-ubah itulah yang membuatnya tak bisa diam.
            _sb.Append(Loc.F("hud.line.kills", Enemies.Kills));
            _sb.Append(Loc.F("hud.line.gold", _gold));
            _hudText.text = _sb.ToString();

            // Plakat gambar-kode mengikuti panjang kalimat, bukan sebaliknya. Plakat dari PREFAB
            // tidak disentuh sama sekali: kalau pemiliknya sudah menyetel ukurannya dengan tangan,
            // menimpanya tiap frame bukan "menyesuaikan", itu membatalkan.
            if (!_hudPlaqueOwnsSize)
                _hudPlaque.rectTransform.sizeDelta = new Vector2(_hudText.preferredWidth + 24f, 36f);

            AnimateBars(Time.unscaledDeltaTime);

            if (_hpLabel != null)
            _hpLabel.text = Loc.F("hud.hp.label", Mathf.CeilToInt(Player.Hp), Mathf.RoundToInt(Player.MaxHp))
                            + (Player.HpRegen > 0f
                                ? Loc.F("hud.hp.regen", Player.HpRegen.ToString("0.0")) : "");

            if (_manaLabel != null)
            _manaLabel.text = Loc.F("hud.mana.label", Mathf.FloorToInt(Player.Mana), Mathf.RoundToInt(Player.MaxMana))
                              + Loc.F("hud.mana.regen", Player.ManaRegen.ToString("0.0"));

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

        /// <param name="scale">
        /// Pengali ukuran huruf. 1 = kabar biasa. Di atas 1 = PENGUMUMAN — dipakai reaksi:
        /// hurufnya membesar, umurnya lebih panjang, dan lahirnya menyentak seperti angka crit.
        /// Ukuran font disetel DI SINI, bukan per frame — mengubah fontSize membangun ulang
        /// mesh teksnya, dan itu hanya pantas dibayar sekali saat lahir.
        /// </param>
        void PushFloater(Vector3 world, string message, Color color, float scale = 1f)
        {
            for (int i = 0; i < FloatPoolSize; i++)
            {
                if (_floatLife[i] > 0f) continue;

                float life = scale > 1f ? 1.6f : 1.1f;
                _floatLife[i] = life;
                _floatMax[i] = life;
                _floatScale[i] = scale;
                _floatWorld[i] = world;
                _floaters[i].text = message;
                _floaters[i].color = color;
                _floaters[i].fontSize = Mathf.RoundToInt(20f * scale);
                _floaters[i].rectTransform.localScale = Vector3.one;
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

                var screen = _camera.WorldToScreenPoint(_floatWorld[i]) / Mathf.Max(0.0001f, UiScale);
                _floaters[i].rectTransform.anchoredPosition = new Vector2(screen.x, screen.y);

                // Sentakan lahir untuk pengumuman besar — lewat localScale yang gratis, bukan
                // fontSize yang membangun ulang mesh. Floater biasa tidak ikut menyentak:
                // "+2 mana" yang berjedar sama kerasnya dengan FIRESTORM menghapus jedarnya.
                if (_floatScale[i] > 1f)
                {
                    float age = _floatMax[i] - _floatLife[i];
                    float pop = 1f + 0.55f * Mathf.Max(0f, 1f - age / 0.18f);
                    _floaters[i].rectTransform.localScale = Vector3.one * pop;
                }

                var c = _floaters[i].color;
                c.a = Mathf.Clamp01(_floatLife[i]);
                _floaters[i].color = c;
            }
        }
    }
}
