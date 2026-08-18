using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Sutradara run: peta act ala Slay the Spire, dipilih LANGSUNG DI PETA, dan pulau rehat.
    ///
    /// Portal fisik pensiun. Alurnya sekarang: lapangan bersih → pemain menyusun grimoire →
    /// tombol LANJUT → kegelapan Gloom MERAPAT sampai menelan layar (nilai standar materialnya
    /// tidak disentuh — cuma dipinjam lewat MaterialPropertyBlock) → peta act terbuka dengan
    /// posisi pemain dilingkari → pemain mengklik node → penanda berjalan di peta → layar
    /// menggelap penuh → isi node dieksekusi dalam gelap (pindah tempat, ganti wajah arena) →
    /// kegelapan MEMBUKA kembali ke nilai standar di tempat yang baru.
    ///
    /// Yang dibeli dari transisi gelap: perpindahan tempat tidak pernah terlihat prosesnya.
    /// Teleport, ganti biome, dan penyebaran ulang suasana semuanya terjadi di balik tirai.
    ///
    /// Pulau rehat = KANTONG di scene yang sama, jauh di luar arena tapi masih di atas lantai.
    /// Scene terpisah butuh seluruh state run ikut pindah, dan persistensi run belum ada.
    /// Supaya tetap terasa TEMPAT LAIN, selama rehat wajah arena ditukar ke biome khusus
    /// (rest biome) — dan wajah wave berikutnya mengembalikannya lewat undian biasa.
    /// </summary>
    public class RunDirector : MonoBehaviour
    {
        // Di dalam jangkauan lantai (±70 / ±60) tapi jauh di luar elips arena (40 / 30) — hutan
        // rapat di luar dinding jadi latar pulaunya, dan petak hash yang sama berarti pulau yang
        // SAMA setiap kali singgah. Tempat rehat harus bisa dikenali.
        static readonly Vector3 IslandCentre = new Vector3(50f, 0f, 42f);

        enum Stage
        {
            /// <summary>Wave berjalan — sutradara diam.</summary>
            Fighting,

            /// <summary>Lapangan bersih / sedang rehat: grimoire terbuka, tombol LANJUT hidup.</summary>
            Ready,

            /// <summary>Gloom sedang merapat menuju gelap penuh.</summary>
            Closing,

            /// <summary>Gelap penuh, peta terpampang — menunggu PickNode dari UI.</summary>
            Choosing,

            /// <summary>Node sudah dieksekusi, gloom sedang membuka ke nilai standar.</summary>
            Opening
        }

        EnemyManager _enemies;
        PlayerMotor _motor;
        ArenaCamera _camera;
        Camera _lens;
        GameBalance _balance;
        ContentDatabase _db;
        Transform _player;
        Transform _rig;
        Font _font;

        /// <summary>Tema UI, disuntik composition root. Boleh null — font jatuh ke bawaan Unity.</summary>
        public UiTheme Theme;
        Gloom _gloom;
        BiomeDresser _dresser;
        BiomeDefinition _restBiome;

        System.Random _dice;

        Stage _stage = Stage.Fighting;
        float _fade;

        public RunMap Map { get; private set; }
        public int Act { get; private set; } = 1;

        /// <summary>Peta pemilih sedang terpampang, menunggu node dipilih.</summary>
        public bool Choosing => _stage == Stage.Choosing;

        /// <summary>
        /// Jenis node yang sedang dijalani. Yang menentukan hadiah saat wave beres — elite membayar
        /// lebih karena ia menuntut lebih, dan tanpa ini yang membayar tidak punya cara tahu.
        /// </summary>
        public RunNodeKind CurrentKind =>
            Map != null && Map.At >= 0 && Map.At < Map.Nodes.Count
                ? Map.Nodes[Map.At].Kind
                : RunNodeKind.Fight;

        /// <summary>Fase transisi gelap 0..1 — UI menumpangkan tirai hitamnya di sini supaya
        /// ujung transisi benar-benar HITAM TOTAL, bukan sekadar gloom yang sangat gelap.</summary>
        public float Fade => _fade;

        /// <summary>Tombol LANJUT boleh ditampilkan: lapangan bersih atau sedang rehat.</summary>
        public bool ReadyToDepart => _stage == Stage.Ready;

        /// <summary>Sedang di pulau rehat.</summary>
        public bool Resting { get; private set; }

        public RunNodeKind RestKind { get; private set; }

        /// <summary>Node yang sudah diinjak act ini — jejak untuk peta di UI.</summary>
        public readonly List<int> Trail = new List<int>();

        /// <summary>UI menumpang di sini: simpan tangan, jual serakan, reset cooldown.</summary>
        public System.Action OnEmbark;

        /// <summary>UI membuka panel toko / kejadian / slot begitu pemain mendarat di pulau.</summary>
        public System.Action<RunNodeKind> OnRestEntered;

        /// <summary>Layar sudah gelap penuh — UI membuka peta dalam mode MEMILIH.</summary>
        public System.Action OnMapChoose;

        public System.Action<string, Color> OnAnnounce;

        /// <summary>Gerbang dari UI: boleh berangkat? (minimal satu skill terpasang).</summary>
        public System.Func<bool> CanEmbark;

        readonly List<GameObject> _islandProps = new List<GameObject>();

        public void Init(EnemyManager enemies, PlayerMotor motor, ArenaCamera camera, Camera lens,
            GameBalance balance, ContentDatabase db, Transform player, Transform rig,
            Gloom gloom, BiomeDresser dresser, BiomeDefinition restBiome)
        {
            _enemies = enemies;
            _motor = motor;
            _camera = camera;
            _lens = lens;
            _balance = balance;
            _db = db;
            _player = player;
            _rig = rig;
            _gloom = gloom;
            _dresser = dresser;
            _restBiome = restBiome;

            // Font dari tema kalau ada, bawaan Unity kalau tidak. Field publik yang disuntik
            // composition root — bukan Resources.Load dan bukan singleton, dua-duanya dilarang
            // di kode runtime project ini.
            _font = Theme != null && Theme.UiFont != null
                ? Theme.UiFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // System.Random milik sutradara, TERPISAH dari urutan acak gameplay: susunan peta dan
            // isi elite tidak boleh menggeser sebaran drop atau arah semburan skill.
            _dice = new System.Random(System.Environment.TickCount);

            Map = GenerateMap();

            _enemies.OnWaveCleared += OnWaveCleared;

            // Wave pertama pun lewat peta. Kecuali saklar curang sudah telanjur memulai wave —
            // saat itu giliran menunggu lewat OnWaveCleared seperti biasa.
            if (!_enemies.WaveActive) _stage = Stage.Ready;
        }

        RunMap GenerateMap()
        {
            return RunMap.Generate(Act, _balance.MapFloorsPerAct, _balance.MapLanes, _dice.Next(),
                _balance.MapEliteChance, _balance.MapShopChance, _balance.MapEventChance,
                _balance.MapEliteMinFloor);
        }

        void OnWaveCleared()
        {
            if (Resting) return;

            if (Map.AtBoss)
            {
                Act++;
                Map = GenerateMap();
                Trail.Clear();
                OnAnnounce?.Invoke(Loc.F("announce.act", Act), new Color(1f, 0.84f, 0.32f));
            }

            // Bukan langsung ke peta: drop baru tumpah dan grimoire baru terbuka. Pemain
            // menyusun dulu — peta menunggu di belakang tombol LANJUT.
            _stage = Stage.Ready;
        }

        // ==================================================================
        //  transisi
        // ==================================================================

        /// <summary>
        /// Dipanggil UI dari tombol LANJUT. Menutup layar dengan gloom; begitu gelap penuh,
        /// <see cref="OnMapChoose"/> menyala dan peta mengambil alih.
        /// </summary>
        public void Depart()
        {
            if (_stage != Stage.Ready) return;

            if (CanEmbark != null && !CanEmbark())
            {
                OnAnnounce?.Invoke(Loc.T("announce.needskill"), new Color(1f, 0.75f, 0.4f));
                return;
            }

            _stage = Stage.Closing;
            _fade = _gloom != null && _gloom.CanShut ? 0f : 1f;
        }

        /// <summary>
        /// Membuka peta SEKETIKA, tanpa transisi menutup.
        ///
        /// Dipakai di awal run, dan bedanya dengan <see cref="Depart"/> bukan soal kecepatan
        /// melainkan soal apa yang ada di layar. Transisi gloom itu untuk BERANGKAT: pemain
        /// sedang berdiri di arena, dan kegelapan yang menelan layar adalah cara meninggalkannya.
        /// Di awal run tidak ada yang ditinggalkan — arenanya masih kosong, belum satu pun musuh
        /// lahir, dan menutupnya pelan-pelan berarti memaksa menatap lapangan kosong selama
        /// <c>MapFadeClose</c> detik sebelum layar yang sebenarnya diminta muncul.
        ///
        /// Layarnya ditinggalkan dalam keadaan GELAP PENUH, bukan dibiarkan bening: peta digambar
        /// di atasnya, dan arena yang mengintip dari belakang peta pemilih terbaca sebagai
        /// bocor. Tirainya dibuka nanti oleh <see cref="PickNode"/> lewat <c>Stage.Opening</c> —
        /// jalur yang sama persis dengan berangkat biasa.
        /// </summary>
        public void DepartNow()
        {
            if (_stage != Stage.Ready) return;

            if (CanEmbark != null && !CanEmbark())
            {
                OnAnnounce?.Invoke(Loc.T("announce.needskill"), new Color(1f, 0.75f, 0.4f));
                return;
            }

            _fade = 1f;
            if (_gloom != null) _gloom.Shut = 1f;

            _stage = Stage.Choosing;
            OnMapChoose?.Invoke();
        }

        void Update()
        {
            if (_stage == Stage.Closing)
            {
                _fade = Mathf.MoveTowards(_fade, 1f, Time.deltaTime / _balance.MapFadeClose);
                if (_gloom != null) _gloom.Shut = _fade;

                if (_fade >= 1f)
                {
                    _stage = Stage.Choosing;
                    OnMapChoose?.Invoke();
                }
            }
            else if (_stage == Stage.Opening)
            {
                _fade = Mathf.MoveTowards(_fade, 0f, Time.deltaTime / _balance.MapFadeOpen);
                if (_gloom != null) _gloom.Shut = _fade;

                if (_fade <= 0f) _stage = Resting ? Stage.Ready : Stage.Fighting;
            }
        }

        /// <summary>
        /// Dipanggil UI setelah penanda selesai berjalan dan tirai menutup: node dieksekusi
        /// SEKARANG, selagi layar masih gelap — teleport dan pergantian wajah tidak pernah
        /// terlihat prosesnya.
        /// </summary>
        public void PickNode(RunNode node)
        {
            if (_stage != Stage.Choosing || node == null) return;
            if (!Map.Reachable().Contains(node)) return;

            if (Resting) LeaveRest();

            Map.At = node.Index;
            Trail.Add(node.Index);

            switch (node.Kind)
            {
                case RunNodeKind.Fight:
                    Relocate();
                    Embark();
                    _enemies.StartWave(_enemies.Wave + 1);
                    break;

                case RunNodeKind.Elite:
                    Relocate();
                    Embark();

                    if (_dice.NextDouble() < _balance.EliteBossChance)
                    {
                        // Elite ber-mini-boss: boss menumpang wave yang sedikit dilonggarkan.
                        // Jumlah bossnya ikut act — makin dalam run, makin ramai puncaknya.
                        int minis = Mathf.Clamp(1 + (Act - 1), 1, 3);
                        _enemies.SetNextWaveMods(1.2f, 0.9f, 1.1f);
                        _enemies.ForceBossNode(minis, 1f, 1.15f, false);
                    }
                    else
                    {
                        _enemies.SetNextWaveMods(
                            _balance.EliteHpMul, _balance.EliteCountMul, _balance.EliteDamageMul);
                    }

                    _enemies.StartWave(_enemies.Wave + 1);
                    break;

                case RunNodeKind.Boss:
                    Relocate();
                    Embark();

                    // Puncak act: ular DAN kelabang sekaligus, tebal dan galak. Pengawalnya
                    // ditipiskan — yang diuji di sini mesin damage pemain, bukan kesabarannya.
                    int bosses = Mathf.Clamp(_balance.BossNodeCount + (Act - 1), 1, 4);
                    _enemies.SetNextWaveMods(1f, 0.55f, 1f);
                    _enemies.ForceBossNode(bosses, _balance.BossNodeHpMul,
                        _balance.BossNodeAggroMul, true);
                    _enemies.StartWave(_enemies.Wave + 1);
                    break;

                default:
                    EnterRest(node.Kind);
                    break;
            }

            _stage = Stage.Opening;
        }

        public static Color KindColor(RunNodeKind kind)
        {
            switch (kind)
            {
                case RunNodeKind.Elite: return new Color(1f, 0.42f, 0.25f);
                case RunNodeKind.Shop: return new Color(1f, 0.84f, 0.32f);
                case RunNodeKind.Event: return new Color(0.75f, 0.5f, 1f);
                case RunNodeKind.Boss: return new Color(1f, 0.2f, 0.2f);
                default: return new Color(0.45f, 0.9f, 1f);
            }
        }

        public static string KindLabel(RunNodeKind kind)
        {
            switch (kind)
            {
                case RunNodeKind.Elite: return Loc.T("map.kind.elite");
                case RunNodeKind.Shop: return Loc.T("map.kind.shop");
                case RunNodeKind.Event: return Loc.T("map.kind.event");
                case RunNodeKind.Boss: return Loc.T("map.kind.boss");
                default: return Loc.T("map.kind.fight");
            }
        }

        void Embark() => OnEmbark?.Invoke();

        /// <summary>
        /// Peta itu GERBANG, bukan pintu putar: keluar di titik acak arena, bukan meneruskan
        /// posisi lama. Tanpa ini tiap wave terasa seperti kode yang di-refresh — pemain masih
        /// berdiri di rumput yang sama, memandang sudut yang sama, ditemani kupu-kupu yang sama.
        /// </summary>
        void Relocate()
        {
            const float margin = 9f;

            // Dijepit juga oleh kotak-tengah kamera: titik yang lebih pinggir dari itu membuat
            // rig tertahan jepitan arena dan pemain lahir MENEPI di layar, bukan di tengah.
            float rx = Mathf.Min(Mathf.Max(0f, _balance.ArenaHalfX - margin), _camera.LimitX);
            float rz = Mathf.Min(Mathf.Max(0f, _balance.ArenaHalfZ - margin), _camera.LimitZ);

            float x = ((float)_dice.NextDouble() * 2f - 1f) * rx;
            float z = ((float)_dice.NextDouble() * 2f - 1f) * rz;

            _player.position = new Vector3(x, 0.9f, z);
            _camera.Teleport(new Vector3(x, 0f, z));
        }

        // ==================================================================
        //  pulau rehat
        // ==================================================================

        void EnterRest(RunNodeKind kind)
        {
            Resting = true;
            RestKind = kind;

            _motor.SetRoam(true);
            _camera.Roam = true;

            _player.position = IslandCentre + new Vector3(0f, 0.9f, -2f);
            _camera.Teleport(IslandCentre);

            // Wajah arena ditukar SELAGI GELAP — begitu tirai terbuka, tempatnya memang lain.
            // Wave berikutnya mengembalikannya sendiri lewat undian wajah di OnWaveStarted.
            if (_dresser != null && _restBiome != null) _dresser.ShowBiome(_restBiome);

            BuildIsland(kind);

            OnRestEntered?.Invoke(kind);

            // Banner "PULAU REHAT — X" DIBUANG atas permintaan pemilik project ("abis
            // ngambil event ada text gak jelas"): pulaunya sendiri sudah panggung yang
            // bercerita, dan teks di tengah layar cuma menutupi barangnya.
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void BuildIsland(RunNodeKind kind)
        {
            ClearIsland();

            // Penghuni pulau: satu sosok yang warnanya milik jenis nodenya. Belum bercerita —
            // tempat ceritanya sudah ada, isinya menyusul bersama sistem dialog.
            var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            host.name = "Penjaga Pulau";

            var hostCollider = host.GetComponent<Collider>();
            if (hostCollider != null) Destroy(hostCollider);

            host.transform.SetParent(transform, false);
            host.transform.position = IslandCentre + new Vector3(0f, 0.9f, 2.4f);
            host.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, KindColor(kind));
            host.GetComponent<Renderer>().SetPropertyBlock(block);
            _islandProps.Add(host);

            // Api unggun: satu lampu hangat rendah. Pulau rehat harus TERASA lebih hangat dari
            // arena, dan satu lampu jingga mengerjakan itu lebih cepat dari properti mana pun.
            var fireGo = new GameObject("Api Unggun");
            fireGo.transform.SetParent(transform, false);
            fireGo.transform.position = IslandCentre + new Vector3(-2.2f, 1.4f, 1.2f);

            var fire = fireGo.AddComponent<Light>();
            fire.type = LightType.Point;
            fire.color = new Color(1f, 0.62f, 0.28f);
            fire.intensity = 6f;
            fire.range = 12f;
            fire.shadows = LightShadows.None;
            _islandProps.Add(fireGo);

            var hostLabelGo = new GameObject("Nama Penjaga");
            hostLabelGo.transform.SetParent(transform, false);
            hostLabelGo.transform.position = IslandCentre + new Vector3(0f, 2.6f, 2.4f);
            hostLabelGo.transform.rotation = Quaternion.Euler(68f, 0f, 0f);

            var hostLabel = hostLabelGo.AddComponent<TextMesh>();
            hostLabel.text = Loc.T(kind == RunNodeKind.Shop ? "island.host.shop" : "island.host.hermit");
            hostLabel.font = _font;
            hostLabel.fontSize = 40;
            hostLabel.characterSize = 0.05f;
            hostLabel.anchor = TextAnchor.MiddleCenter;
            hostLabel.color = KindColor(kind);

            var hostText = hostLabelGo.GetComponent<MeshRenderer>();
            if (hostText != null && _font != null) hostText.sharedMaterial = _font.material;
            _islandProps.Add(hostLabelGo);
        }

        void ClearIsland()
        {
            for (int i = 0; i < _islandProps.Count; i++)
            {
                if (_islandProps[i] != null) Destroy(_islandProps[i]);
            }

            _islandProps.Clear();
        }

        /// <summary>
        /// Dipanggil dari PickNode SELAGI GELAP: pulau dibereskan tanpa pernah terlihat kosong.
        /// Posisi pemain tidak perlu dikembalikan — node tempur me-Relocate sendiri, dan node
        /// rehat beruntun menata pulau lagi lewat EnterRest.
        /// </summary>
        void LeaveRest()
        {
            ClearIsland();

            Resting = false;
            _motor.SetRoam(false);
            _camera.Roam = false;
        }
    }
}
