using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Meja sutradara: panel curang yang MENUMPANG di atas game yang sudah jadi.
    ///
    /// <b>Ia tidak membangun apa pun.</b> Scene yang memuatnya adalah salinan
    /// <c>Proto.unity</c> — arena yang sudah didandani, biome, cuaca, cahaya, atmosfer,
    /// HUD, sutradara run, semuanya lahir dari <see cref="ProtoBootstrap"/> persis seperti
    /// di permainan sungguhan. Yang ditambahkan di sini cuma satu kanvas dan beberapa tombol.
    ///
    /// Percobaan pertama justru membangun arenanya sendiri dari nol — lantai polos dan satu
    /// kapsul — dan itu salah untuk dua hal sekaligus. Untuk trailer ia tidak berguna karena
    /// yang direkam bukan game ini. Untuk memeriksa boss ia MENIPU: naga yang terlihat benar
    /// di atas lantai kosong belum tentu terbaca di antara pepohonan, kabut, dan bloom yang
    /// sesungguhnya ada di sana.
    ///
    /// Jadi yang benar adalah menumpang. Apa yang terlihat di panggung ini adalah apa yang
    /// dilihat pemain, karena memang scene yang sama.
    ///
    /// <b>Berjalan di <see cref="Start"/>, bukan Awake.</b> ProtoBootstrap membangun seluruh
    /// dunianya di Awake; mencari EnemyManager dari Awake berarti berlomba dengan yang
    /// membuatnya, dan siapa yang menang bergantung pada urutan yang tidak dijamin Unity.
    /// </summary>
    public class StageDirector : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.92f, 0.94f, 1f);
        static readonly Color Dim = new Color(0.62f, 0.66f, 0.76f);
        static readonly Color PanelInk = new Color(0.05f, 0.06f, 0.09f, 0.92f);

        ContentDatabase _db;
        EnemyManager _enemies;
        PlayerCaster _caster;
        ArenaCamera _arenaCam;
        RunDirector _run;
        bool _entered;
        Camera _lens;
        Canvas _canvas;
        Transform _player;

        TextMeshProUGUI _readout;
        TextMeshProUGUI _hint;
        RectTransform _leftPanel;
        RectTransform _rightPanel;
        RectTransform _bar;
        RectTransform _mini;
        TextMeshProUGUI _barPanel;
        TextMeshProUGUI _barHud;

        readonly List<Canvas> _gameCanvases = new List<Canvas>();
        readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(512);

        struct Spawnable
        {
            public string Label;
            public BossDefinition Boss;
            public EnemyArchetype Kind;
            public bool Dummy;
        }

        readonly List<Spawnable> _catalogue = new List<Spawnable>();

        // ---------- kamera sutradara ----------

        bool _freeCam;
        Vector3 _focus;
        float _camYaw;
        float _camPitch = 55f;
        float _camSize = 14f;

        /// <summary>0 = pemain, 1 = boss, 2 = diam di tempat.</summary>
        int _follow;
        static readonly string[] FollowNames = { "pemain", "boss", "diam" };

        // Keadaan asli kamera game, disimpan supaya kamera sutradara bisa DIMATIKAN lagi.
        // Tanpa ini, sekali menyalakan mode bebas berarti kamera permainan tidak pernah bisa
        // kembali — dan membandingkan bidikan sutradara dengan bidikan sungguhan jadi mustahil.
        Vector3 _camHomePos;
        Quaternion _camHomeRot;
        float _camHomeSize;

        bool _uiHidden;
        bool _hudHidden;
        float _timeScale = 1f;
        int _spawnBatch = 1;

        void Start()
        {
            _enemies = FindFirstObjectByType<EnemyManager>();
            _caster = FindFirstObjectByType<PlayerCaster>();
            _arenaCam = FindFirstObjectByType<ArenaCamera>();
            _run = FindFirstObjectByType<RunDirector>();
            _lens = Camera.main;

            if (_enemies == null || _caster == null || _lens == null)
            {
                Debug.LogError("[Sutradara] Scene game belum terbangun. Panggung ini harus " +
                               "ditaruh di SALINAN Proto.unity, bukan di scene kosong.", this);
                enabled = false;
                return;
            }

            _player = _caster.transform;
            _db = FindDatabase();

            if (_db == null)
            {
                Debug.LogError("[Sutradara] ContentDatabase tidak ketemu.", this);
                enabled = false;
                return;
            }

            TakeOverCheats();

            _camHomePos = _lens.transform.position;
            _camHomeRot = _lens.transform.rotation;
            _camHomeSize = _lens.orthographicSize;
            _camSize = _camHomeSize;
            _camPitch = _lens.transform.eulerAngles.x;
            _camYaw = _lens.transform.eulerAngles.y;

            BuildCatalogue();
            BuildUi();
            CollectGameCanvases();
        }

        void OnDestroy()
        {
            // Jam global dikembalikan. Meninggalkannya di 0,2 berarti scene BERIKUTNYA yang
            // dibuka jalan gerak lambat tanpa sebab yang kelihatan, dan yang mencarinya tidak
            // akan pernah menduga penyebabnya ada di scene yang sudah ditutup.
            Time.timeScale = 1f;
        }

        ContentDatabase FindDatabase()
        {
            // Diambil dari EnemyManager lewat refleksi, bukan diserahkan ulang lewat inspector.
            //
            // Menyerahkannya ulang berarti panggung ini punya rujukan KEDUA ke database, dan dua
            // rujukan bisa menunjuk aset yang berbeda tanpa ada yang sadar — panelnya akan
            // menawarkan boss yang tidak ada di undian permainan, atau melewatkan yang ada.
            var f = typeof(EnemyManager).GetField("_db",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return f != null ? f.GetValue(_enemies) as ContentDatabase : null;
        }

        /// <summary>
        /// Menukar aset curang dengan salinan yang hidup di memori saja.
        ///
        /// Tanpa ini, tiap geseran slider di panggung menulis PERMANEN ke DebugConfig.asset —
        /// dan satu centang yang lupa dimatikan ikut terbawa ke build sebagai game yang tidak
        /// bisa kalah, persis yang diperingatkan aset itu sendiri.
        ///
        /// Ditukar di dua tempat karena memang disimpan di dua tempat: EnemyManager memakainya
        /// untuk musuh, PlayerCaster untuk pemain. Menukar salah satunya saja menghasilkan
        /// panggung yang separuh kerannya tidak berfungsi.
        /// </summary>
        void TakeOverCheats()
        {
            var source = _enemies.Cheats != null ? _enemies.Cheats : _caster.Cheats;

            var copy = source != null
                ? Instantiate(source)
                : ScriptableObject.CreateInstance<DebugConfig>();

            copy.name = "DebugConfig (panggung, tidak disimpan)";
            copy.Enabled = true;
            copy.Invulnerable = true;    // sutradara tidak sedang bermain
            copy.InfiniteMana = true;
            copy.ShowDemoBar = false;
            copy.HideUI = false;

            _enemies.Cheats = copy;
            _caster.Cheats = copy;
        }

        DebugConfig Cheats => _enemies.Cheats;

        /// <summary>
        /// Kanvas milik PERMAINAN, dicatat supaya HUD bisa disembunyikan terpisah dari panel
        /// sutradara. Rekaman gameplay bersih butuh HUD ikut hilang; memeriksa boss justru
        /// butuh HUD tetap ada.
        /// </summary>
        void CollectGameCanvases()
        {
            _gameCanvases.Clear();

            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c == _canvas) continue;
                if (c.transform.IsChildOf(transform)) continue;
                _gameCanvases.Add(c);
            }
        }

        // =============================================================================
        //  katalog
        // =============================================================================

        void BuildCatalogue()
        {
            _catalogue.Clear();

            var bosses = _db.BossKinds;

            for (int i = 0; i < bosses.Count; i++)
            {
                var b = bosses[i];
                if (b == null) continue;

                string tag = b.Minion ? "anak buah"
                    : b.Body == BossDefinition.BossBody.Winged ? "bersayap" : "beruas";

                _catalogue.Add(new Spawnable { Label = "BOSS  " + b.DisplayName + "  <" + tag + ">", Boss = b });
            }

            var kinds = _db.Archetypes;

            for (int i = 0; i < kinds.Count; i++)
            {
                var k = kinds[i];
                if (k == null) continue;

                _catalogue.Add(new Spawnable
                {
                    Label = "musuh  " + (string.IsNullOrEmpty(k.DisplayName) ? k.name : k.DisplayName),
                    Kind = k,
                });
            }

            _catalogue.Add(new Spawnable { Label = "boneka diam (tidak melawan)", Dummy = true });
        }

        void Spawn(Spawnable what)
        {
            for (int i = 0; i < _spawnBatch; i++)
            {
                if (what.Boss != null) _enemies.SpawnBossOfKind(what.Boss);
                else if (what.Kind != null) _enemies.SpawnOfKind(what.Kind);
                else if (what.Dummy)
                {
                    float a = (i / (float)Mathf.Max(1, _spawnBatch)) * Mathf.PI * 2f;
                    Vector3 at = _player.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 7f;
                    at.y = 0.9f;
                    _enemies.SpawnDummy(at, 100000f);
                }
            }

            // Boss yang baru lahir langsung jadi sasaran kamera bebas. Yang memencet tombol naga
            // hampir selalu memencetnya karena ingin MELIHAT naga.
            if (what.Boss != null && _freeCam) _follow = 1;
        }

        // =============================================================================
        //  antarmuka
        // =============================================================================

        void BuildUi()
        {
            var go = new GameObject("StageCanvas");
            go.transform.SetParent(transform, false);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Di ATAS HUD permainan. Panel yang tenggelam di belakang kotak mantra tidak bisa
            // diklik, dan yang mencarinya akan mengira panggungnya rusak.
            _canvas.sortingOrder = 500;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560f, 1440f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();

            BuildSpawnList();
            BuildKnobs();
            BuildToggleBar();

            _readout = Label(_canvas.transform, "", 24, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(496f, -186f), new Vector2(1500f, -76f));

            _hint = Label(_canvas.transform, "", 18, TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(496f, 8f), new Vector2(1700f, 150f));
            _hint.color = Dim;
        }

        void BuildSpawnList()
        {
            _leftPanel = Panel(_canvas.transform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(8f, 8f), new Vector2(480f, -8f), PanelInk);

            Label(_leftPanel, "MUNCULKAN", 26, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -30f), new Vector2(-12f, -6f));

            var scroll = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect),
                typeof(Image), typeof(Mask));
            scroll.transform.SetParent(_leftPanel, false);
            var srt = (RectTransform)scroll.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(6f, 222f); srt.offsetMax = new Vector2(-6f, -48f);
            scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            var content = new GameObject("Content", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(scroll.transform, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = Vector2.zero;

            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 2f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scroll.GetComponent<ScrollRect>();
            sr.content = crt;
            sr.horizontal = false;
            sr.scrollSensitivity = 28f;
            sr.viewport = srt;

            for (int i = 0; i < _catalogue.Count; i++)
            {
                var what = _catalogue[i];
                bool isBoss = what.Boss != null;

                MakeButton(content.transform, what.Label, 42f,
                    isBoss ? new Color(0.30f, 0.12f, 0.15f, 0.95f) : new Color(0.12f, 0.14f, 0.19f, 0.95f),
                    () => Spawn(what));
            }

            MakeButton(_leftPanel, "MASUK ARENA (lompati peta)", 46f,
                new Color(0.13f, 0.18f, 0.14f, 0.95f), () => { _entered = false; EnterArena(); },
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 166f), new Vector2(-6f, 212f));

            MakeButton(_leftPanel, "KOSONGKAN LAPANGAN   (C)", 46f,
                new Color(0.10f, 0.12f, 0.16f, 0.95f), () => _enemies.ClearField(),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 114f), new Vector2(-6f, 160f));

            MakeButton(_leftPanel, "WAVE BERIKUTNYA", 46f,
                new Color(0.10f, 0.12f, 0.16f, 0.95f),
                () => _enemies.StartWave(_enemies.Wave + 1),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 62f), new Vector2(-6f, 108f));

            MakeButton(_leftPanel, "ULANGI WAVE INI", 46f,
                new Color(0.10f, 0.12f, 0.16f, 0.95f),
                () => { _enemies.ClearField(); _enemies.StartWave(Mathf.Max(1, _enemies.Wave)); },
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 10f), new Vector2(-6f, 56f));
        }

        void BuildKnobs()
        {
            _rightPanel = Panel(_canvas.transform, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-580f, 8f), new Vector2(-8f, -8f), PanelInk);

            Label(_rightPanel, "KERAN", 26, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -30f), new Vector2(-12f, -6f));

            float y = -40f;

            Slider(_rightPanel, "kecepatan waktu", ref y, 0.05f, 3f, _timeScale,
                v => { _timeScale = v; Time.timeScale = v; }, x => x.ToString("F2") + "x");

            Slider(_rightPanel, "berapa ekor per klik", ref y, 1f, 20f, _spawnBatch,
                v => _spawnBatch = Mathf.RoundToInt(v), x => Mathf.RoundToInt(x) + " ekor");

            Slider(_rightPanel, "HP musuh", ref y, 0.05f, 10f, Cheats.EnemyHpMultiplier,
                v => Cheats.EnemyHpMultiplier = v, x => x.ToString("F2") + "x");

            Slider(_rightPanel, "jumlah musuh per wave", ref y, 0.1f, 5f, Cheats.EnemyCountMultiplier,
                v => Cheats.EnemyCountMultiplier = v, x => x.ToString("F2") + "x");

            Slider(_rightPanel, "damage pemain", ref y, 0.1f, 50f, Cheats.DamageMultiplier,
                v => Cheats.DamageMultiplier = v, x => x.ToString("F1") + "x");

            y -= 8f;
            Label(_rightPanel, "KAMERA SUTRADARA", 22, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, y - 20f), new Vector2(-12f, y));
            y -= 26f;

            Slider(_rightPanel, "sudut tunduk", ref y, 8f, 89f, _camPitch,
                v => _camPitch = v, x => Mathf.RoundToInt(x) + "°");

            Slider(_rightPanel, "putar", ref y, -180f, 180f, _camYaw,
                v => _camYaw = v, x => Mathf.RoundToInt(x) + "°");

            Slider(_rightPanel, "zoom", ref y, 3f, 45f, _camSize,
                v => _camSize = v, x => x.ToString("F1"));

            y -= 6f;

            Toggle(_rightPanel, "AMBIL ALIH KAMERA  (V)", ref y, false, SetFreeCam);
            Toggle(_rightPanel, "pemain kebal", ref y, Cheats.Invulnerable, v => Cheats.Invulnerable = v);
            Toggle(_rightPanel, "mana tak terbatas", ref y, Cheats.InfiniteMana, v => Cheats.InfiniteMana = v);
            Toggle(_rightPanel, "tanpa cooldown", ref y, Cheats.NoCooldowns, v => Cheats.NoCooldowns = v);
            Toggle(_rightPanel, "musuh berhenti berdatangan", ref y, Cheats.FreezeSpawns,
                v => Cheats.FreezeSpawns = v);
            Toggle(_rightPanel, "sembunyikan HUD game  (J)", ref y, false, HideHud);
            Toggle(_rightPanel, "bayangan musuh", ref y, _enemies.EnemyShadows,
                v => _enemies.EnemyShadows = v);
        }

        // =============================================================================
        //  jalan
        // =============================================================================

        void Update()
        {
            // Lompati peta, sekali saja, begitu sutradara run siap menerima pilihan.
            //
            // Peta adalah bagian permainan yang sah, tapi bukan yang dicari di sini: panggung
            // dibuka untuk MELIHAT sesuatu di arena, dan memaksa yang membukanya memilih node
            // dulu tiap kali adalah satu layar yang selalu harus dilewati sebelum kerja dimulai.
            //
            // Dikerjakan di Update, bukan Start, karena RunDirector belum tentu sudah sampai di
            // tahap memilih pada frame pertama — memanggilnya terlalu awal ditolak diam-diam,
            // dan panggungnya berhenti di peta tanpa satu pun pesan yang menjelaskan kenapa.
            if (!_entered && _run != null && _run.Choosing) EnterArena();

            Hotkeys();
            if (_freeCam) DriveCamera(Time.unscaledDeltaTime);
            if (!_uiHidden) Redraw();
        }

        void Hotkeys()
        {
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k == null) return;

            if (k.hKey.wasPressedThisFrame) SetClean(!_uiHidden);
            if (k.jKey.wasPressedThisFrame) HideHud(!_hudHidden);
            if (k.vKey.wasPressedThisFrame) SetFreeCam(!_freeCam);

            if (k.spaceKey.wasPressedThisFrame)
                Time.timeScale = Time.timeScale > 0.0001f ? 0f : _timeScale;

            if (k.leftBracketKey.wasPressedThisFrame) SetSpeed(_timeScale * 0.5f);
            if (k.rightBracketKey.wasPressedThisFrame) SetSpeed(_timeScale * 2f);

            if (k.f1Key.wasPressedThisFrame) SetSpeed(0.15f);
            if (k.f2Key.wasPressedThisFrame) SetSpeed(0.35f);
            if (k.f3Key.wasPressedThisFrame) SetSpeed(1f);
            if (k.f4Key.wasPressedThisFrame) SetSpeed(2f);

            if (k.tabKey.wasPressedThisFrame) _follow = (_follow + 1) % 3;
            if (k.cKey.wasPressedThisFrame) _enemies.ClearField();
#endif
        }

        /// <summary>
        /// Memilih node TEMPUR terdekat di peta, jadi panggung langsung berdiri di arena.
        ///
        /// Yang dicari node <c>Fight</c> lebih dulu, bukan sembarang yang bisa dijangkau: node
        /// rehat dan toko juga "bisa dijangkau", dan mendarat di sana berarti panggung terbuka
        /// di ruangan tanpa satu pun musuh — tempat yang paling tidak berguna untuk memeriksa boss.
        /// </summary>
        void EnterArena()
        {
            if (_run == null || !_run.Choosing) return;

            var reachable = _run.Map != null ? _run.Map.Reachable() : null;
            if (reachable == null || reachable.Count == 0) return;

            RunNode pick = null;

            for (int i = 0; i < reachable.Count; i++)
            {
                if (reachable[i].Kind != RunNodeKind.Fight) continue;
                pick = reachable[i];
                break;
            }

            if (pick == null) pick = reachable[0];

            // Dijalankan lewat JALUR UI, bukan langsung ke RunDirector.
            //
            // `PickNode` sendiri memang bekerja — node berpindah, wave mulai — tapi peta yang
            // menutupinya TIDAK ikut tertutup: yang menutupnya adalah rangkaian di GrimoireUI
            // (penanda berjalan, tirai turun, baru node dieksekusi dalam gelap), dan rangkaian
            // itu yang memanggil PickNode di ujungnya. Memanggilnya dari samping berarti
            // separuh transisi dilewati, dan panggungnya berdiri di arena di balik peta yang
            // masih terbentang — terlihat seperti hang, padahal jalan.
            //
            // Jadi yang dipanggil pemicunya, dan sisanya dibiarkan berjalan sendiri. Sekalian
            // dapat fade-nya, yang memang lebih enak dilihat untuk rekaman daripada potong keras.
            var ui = FindFirstObjectByType<GrimoireUI>();

            var begin = ui != null
                ? typeof(GrimoireUI).GetMethod("BeginMapTravel",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                : null;

            if (begin != null)
            {
                begin.Invoke(ui, new object[] { pick });
                _entered = true;
                return;
            }

            // Jatuh balik kalau rangkaian UI-nya berubah nama: masuk arena tetap lebih berguna
            // daripada berhenti di peta, meski petanya harus ditutup tangan lewat M.
            Debug.LogWarning("[Sutradara] BeginMapTravel tidak ketemu — masuk arena tanpa " +
                             "transisi, petanya mungkin perlu ditutup manual (M).", this);

            _run.PickNode(pick);
            _entered = true;
        }

        void SetSpeed(float value)
        {
            _timeScale = Mathf.Clamp(value, 0.05f, 4f);
            Time.timeScale = _timeScale;
        }

        /// <summary>
        /// Dua tombol kecil di atas-tengah: nyalakan/matikan panel sutradara dan HUD permainan.
        ///
        /// Ada karena tombol pintas saja TIDAK CUKUP begitu semuanya tersembunyi. Layar yang
        /// kosong tidak memberi tahu siapa pun bahwa H mengembalikannya — yang menemukannya
        /// dalam keadaan itu wajar menyimpulkan panggungnya rusak, bukan sedang disembunyikan.
        ///
        /// Bilah ini sengaja TIDAK ikut disembunyikan tombol PANEL; yang menghapusnya cuma H,
        /// yang memang berarti "bersihkan layar untuk merekam".
        /// </summary>
        void BuildToggleBar()
        {
            _bar = Panel(_canvas.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-450f, -66f), new Vector2(450f, -8f), PanelInk);

            var one = MakeButton(_bar, "", 50f, new Color(0.16f, 0.18f, 0.25f, 0.95f),
                () => ShowPanels(_uiHidden),
                new Vector2(0f, 0f), new Vector2(0.34f, 1f), new Vector2(6f, 6f), new Vector2(-3f, -6f));
            _barPanel = one.GetComponentInChildren<TextMeshProUGUI>();
            _barPanel.alignment = TextAlignmentOptions.Center;

            var two = MakeButton(_bar, "", 50f, new Color(0.16f, 0.18f, 0.25f, 0.95f),
                () => HideHud(!_hudHidden),
                new Vector2(0.34f, 0f), new Vector2(0.66f, 1f), new Vector2(3f, 6f), new Vector2(-3f, -6f));
            _barHud = two.GetComponentInChildren<TextMeshProUGUI>();
            _barHud.alignment = TextAlignmentOptions.Center;

            // Tombol REKAM: sembunyikan SEMUANYA lewat klik — permintaan pemilik project, yang
            // membuka-tutup panel sambil merekam dan tidak mau menyentuh keyboard di tengah
            // rekaman. Pasangannya tombol mini di pojok (dibuat di bawah), karena layar yang
            // sudah kosong tidak menyisakan apa pun untuk diklik kembali.
            var three = MakeButton(_bar, "REKAM: BERSIHKAN LAYAR", 50f,
                new Color(0.30f, 0.14f, 0.16f, 0.95f), () => SetClean(true),
                new Vector2(0.66f, 0f), new Vector2(1f, 1f), new Vector2(3f, 6f), new Vector2(-6f, -6f));
            var threeLabel = three.GetComponentInChildren<TextMeshProUGUI>();
            threeLabel.alignment = TextAlignmentOptions.Center;

            // Tombol mini pemulih: kotak kecil redup di pojok kiri-atas, satu-satunya yang
            // tersisa saat layar dibersihkan. Sengaja pudar — ia akan ikut terekam, jadi ia
            // harus cukup terlihat untuk diklik dan cukup redup untuk diabaikan penonton.
            var mini = MakeButton(_canvas.transform, "≡", 40f, new Color(0.1f, 0.11f, 0.15f, 0.22f),
                () => SetClean(false),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -48f), new Vector2(52f, -8f));
            var miniLabel = mini.GetComponentInChildren<TextMeshProUGUI>();
            miniLabel.alignment = TextAlignmentOptions.Center;
            miniLabel.color = new Color(1f, 1f, 1f, 0.45f);
            _mini = (RectTransform)mini.transform;
            _mini.gameObject.SetActive(false);

            RefreshBar();
        }

        void RefreshBar()
        {
            if (_barPanel != null) _barPanel.text = _uiHidden ? "PANEL: MATI" : "PANEL: NYALA";
            if (_barHud != null) _barHud.text = _hudHidden ? "HUD: MATI" : "HUD: NYALA";
        }

        /// <summary>Bersih total untuk merekam: panel, bilah togel, dan HUD sekaligus.</summary>
        void SetClean(bool clean)
        {
            ShowPanels(!clean);
            HideHud(clean);
            if (_bar != null) _bar.gameObject.SetActive(!clean);
            if (_mini != null) _mini.gameObject.SetActive(clean);
        }

        void ShowPanels(bool show)
        {
            _uiHidden = !show;
            _leftPanel.gameObject.SetActive(show);
            _rightPanel.gameObject.SetActive(show);
            _readout.gameObject.SetActive(show);
            _hint.gameObject.SetActive(show);
            RefreshBar();
        }

        void HideHud(bool hide)
        {
            _hudHidden = hide;

            for (int i = 0; i < _gameCanvases.Count; i++)
            {
                if (_gameCanvases[i] != null) _gameCanvases[i].enabled = !hide;
            }

            RefreshBar();
        }

        /// <summary>
        /// Menyalakan/mematikan kamera sutradara.
        ///
        /// <see cref="ArenaCamera"/> dimatikan, bukan ditimpa tiap frame: keduanya menulis ke
        /// transform yang sama, dan dua penulis dalam satu frame menghasilkan kamera yang
        /// bergetar di antara dua jawaban — terlihat seperti bug rendering, padahal rebutan.
        /// </summary>
        void SetFreeCam(bool on)
        {
            _freeCam = on;

            if (_arenaCam != null) _arenaCam.enabled = !on;

            if (on)
            {
                _focus = _player != null ? _player.position : Vector3.zero;
                return;
            }

            // Dikembalikan persis ke keadaan yang direkam saat Start. Membiarkannya di posisi
            // sutradara berarti ArenaCamera harus menyeretnya pulang sambil terlihat melayang.
            _lens.transform.SetPositionAndRotation(_camHomePos, _camHomeRot);
            _lens.orthographicSize = _camHomeSize;
        }

        void DriveCamera(float dt)
        {
            // Dipaksa mati TIAP FRAME, bukan cuma saat tombolnya ditekan. Terukur di panggung:
            // transisi RunDirector menghidupkan ArenaCamera kembali di tengah sesi, dan dua
            // penulis di satu transform berarti kamera game yang menang — panggung menunjuk
            // boss sementara layarnya menonton pemain, tanpa satu pun pesan error.
            if (_arenaCam != null && _arenaCam.enabled) _arenaCam.enabled = false;

            Vector3 want = _focus;

            if (_follow == 0 && _player != null) want = _player.position;
            else if (_follow == 1)
            {
                var boss = _enemies.Boss;
                if (boss != null && boss.Alive) want = boss.HeadPos;
            }

            // Dikejar halus, tidak dipatok: kamera yang menempel persis pada boss yang meliuk
            // membuat seluruh latar bergetar, dan rekaman yang bergetar tidak bisa dipakai.
            _focus = Vector3.Lerp(_focus, want, 1f - Mathf.Exp(-3.5f * dt));

            float scroll = ProtoInput.ScrollY;
            if (Mathf.Abs(scroll) > 0.01f) _camSize = Mathf.Clamp(_camSize - scroll * 1.4f, 3f, 45f);

            if (ProtoInput.RightHeld)
            {
                var d = ProtoInput.MouseDelta;
                _camYaw += d.x * 0.25f;
                _camPitch = Mathf.Clamp(_camPitch - d.y * 0.15f, 8f, 89f);
            }

            var rot = Quaternion.Euler(_camPitch, _camYaw, 0f);
            _lens.orthographicSize = _camSize;
            _lens.transform.SetPositionAndRotation(_focus + rot * new Vector3(0f, 0f, -60f), rot);
        }

        void Redraw()
        {
            _sb.Clear();
            _sb.Append("wave ").Append(_enemies.Wave)
               .Append("   musuh hidup ").Append(_enemies.AliveCount).AppendLine();

            var boss = _enemies.Boss;

            if (boss != null && boss.Alive)
            {
                _sb.Append(boss.Def.DisplayName).Append("  ")
                   .Append(Mathf.RoundToInt(boss.HpFraction * 100f)).Append('%');

                if (boss.Def.Body == BossDefinition.BossBody.Winged)
                {
                    _sb.Append("  ").Append(boss.Breathing
                        ? (boss.BreathHot ? "MENYEMBUR" : "ancang-ancang") : "terbang");
                    _sb.Append("  y=").Append(boss.HeadPos.y.ToString("F1"));
                }

                _sb.AppendLine();
            }
            else _sb.AppendLine("tidak ada boss");

            _sb.Append("kamera: ").Append(_freeCam ? "SUTRADARA -> " + FollowNames[_follow] : "permainan")
               .Append("   waktu ").Append(Time.timeScale.ToString("F2")).Append('x');

            _readout.SetText(_sb);

            _hint.text =
                "H bersih total (panel + bilah + HUD)   J HUD game   V ambil alih kamera\n" +
                "SPASI jeda   [ ] kecepatan   F1-F4 preset kecepatan   TAB sasaran kamera   C kosongkan\n" +
                "saat kamera diambil alih: drag KANAN memutar, roda mouse zoom\n" +
                "curang di panggung ini TIDAK tersimpan ke DebugConfig.asset";
        }

        // =============================================================================
        //  widget
        // =============================================================================

        static RectTransform Panel(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;

            go.GetComponent<Image>().color = color;
            return rt;
        }

        static TextMeshProUGUI Label(Transform parent, string content, int size,
            TextAlignmentOptions anchor, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;

            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Ink;
            t.text = content;
            t.raycastTarget = false;
            return t;
        }

        static Button MakeButton(Transform parent, string text, float height, Color color,
            UnityEngine.Events.UnityAction onClick,
            Vector2? anchorMin = null, Vector2? anchorMax = null,
            Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;

            if (anchorMin.HasValue)
            {
                rt.anchorMin = anchorMin.Value; rt.anchorMax = anchorMax.Value;
                rt.offsetMin = offsetMin.Value; rt.offsetMax = offsetMax.Value;
            }
            else
            {
                var le = go.AddComponent<LayoutElement>();
                le.minHeight = height;
                le.preferredHeight = height;
            }

            go.GetComponent<Image>().color = color;

            var label = Label(go.transform, text, 21, TextAlignmentOptions.Left,
                Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-6f, 0f));
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;

            go.GetComponent<Button>().onClick.AddListener(onClick);
            return go.GetComponent<Button>();
        }

        void Slider(RectTransform parent, string name, ref float y, float min, float max,
            float value, System.Action<float> onChange, System.Func<float, string> format)
        {
            const float Row = 64f;

            var holder = new GameObject("Row", typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            var hrt = (RectTransform)holder.transform;
            hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(1f, 1f);
            hrt.offsetMin = new Vector2(12f, y - Row); hrt.offsetMax = new Vector2(-12f, y);

            Label(holder.transform, name, 19, TextAlignmentOptions.TopLeft,
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-150f, 0f));

            var readout = Label(holder.transform, format(value), 19, TextAlignmentOptions.TopRight,
                Vector2.zero, Vector2.one, new Vector2(-150f, 0f), Vector2.zero);
            readout.color = Dim;

            var bar = new GameObject("Slider", typeof(RectTransform), typeof(UnityEngine.UI.Slider));
            bar.transform.SetParent(holder.transform, false);
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 0f);
            brt.offsetMin = new Vector2(0f, 2f); brt.offsetMax = new Vector2(0f, 24f);

            var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(bar.transform, false);
            var trt = (RectTransform)track.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            track.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 1f);

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(bar.transform, false);
            var fart = (RectTransform)fillArea.transform;
            fart.anchorMin = Vector2.zero; fart.anchorMax = Vector2.one;
            fart.offsetMin = Vector2.zero; fart.offsetMax = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var frt = (RectTransform)fill.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.55f, 0.42f, 0.85f, 1f);

            var s = bar.GetComponent<UnityEngine.UI.Slider>();
            s.fillRect = frt;
            s.targetGraphic = track.GetComponent<Image>();
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = min;
            s.maxValue = max;
            s.SetValueWithoutNotify(value);
            s.onValueChanged.AddListener(v => { onChange(v); readout.text = format(v); });

            y -= Row + 4f;
        }

        void Toggle(RectTransform parent, string name, ref float y, bool value,
            System.Action<bool> onChange)
        {
            const float Row = 40f;

            var go = new GameObject("Toggle", typeof(RectTransform), typeof(Image),
                typeof(UnityEngine.UI.Toggle));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(12f, y - Row); rt.offsetMax = new Vector2(-12f, y);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(go.transform, false);
            var boxRt = (RectTransform)box.transform;
            boxRt.anchorMin = new Vector2(0f, 0.5f); boxRt.anchorMax = new Vector2(0f, 0.5f);
            boxRt.sizeDelta = new Vector2(22f, 22f);
            boxRt.anchoredPosition = new Vector2(8f, 0f);
            box.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 1f);

            var tick = new GameObject("Tick", typeof(RectTransform), typeof(Image));
            tick.transform.SetParent(box.transform, false);
            var tickRt = (RectTransform)tick.transform;
            tickRt.anchorMin = Vector2.zero; tickRt.anchorMax = Vector2.one;
            tickRt.offsetMin = new Vector2(3f, 3f); tickRt.offsetMax = new Vector2(-3f, -3f);
            tick.GetComponent<Image>().color = new Color(0.62f, 0.5f, 0.92f, 1f);

            Label(go.transform, name, 19, TextAlignmentOptions.Left,
                Vector2.zero, Vector2.one, new Vector2(38f, 0f), Vector2.zero);

            var t = go.GetComponent<UnityEngine.UI.Toggle>();
            t.targetGraphic = go.GetComponent<Image>();
            t.graphic = tick.GetComponent<Image>();
            t.SetIsOnWithoutNotify(value);
            tick.GetComponent<Image>().enabled = value;
            t.onValueChanged.AddListener(v => onChange(v));

            y -= Row + 2f;
        }
    }
}
