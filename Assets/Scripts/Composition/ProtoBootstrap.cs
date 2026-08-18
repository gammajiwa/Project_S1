using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Proto
{
    /// <summary>
    /// Builds the whole prototype scene at runtime so there is nothing to wire by hand.
    /// Drop this on one empty GameObject and press Play.
    /// </summary>
    public class ProtoBootstrap : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] ContentDatabase _database;
        [SerializeField] GameBalance _balance;

        [Tooltip("Angka penataan panggung: letak & sudut kamera, tinggi/skala titik lahir " +
                 "pemain, kelebihan lantai. Kosong = nilai bawaan, yang persis angka lama di kode.")]
        [SerializeField] StageRig _rig_setup;

        /// <summary>Penataan yang BERLAKU. Tidak pernah null - lihat <see cref="StageRig.Fallback"/>.</summary>
        StageRig Stage => _rig_setup != null ? _rig_setup : StageRig.Fallback;

        [Header("Tampilan")]
        [Tooltip("Dipakai bareng scene menu, supaya dua-duanya tidak pernah beda cahaya.")]
        [SerializeField] SceneLook _look;

        [Tooltip("Wajah arena, dipakai bergantian tiap beberapa wave. Kosong = lantai polos.")]
        [SerializeField] BiomeDefinition[] _biomes;

        [Tooltip("Wajah KHUSUS pulau rehat (toko/kejadian/slot). Selama singgah, arena memakai " +
                 "wajah ini supaya tempat rehat terasa tempat lain — wave berikutnya " +
                 "mengembalikan wajah biasa lewat undian. Kosong = pulau memakai wajah wave.")]
        [SerializeField] BiomeDefinition _restBiome;

        [Tooltip("Kertas, bingkai, dan warna tinta UI. Kosong = UI kembali jadi kotak warna " +
                 "datar seperti sebelum art masuk — sengaja tidak wajib, supaya art yang belum " +
                 "jadi tidak pernah memblokir tes gameplay.")]
        [SerializeField] UiTheme _uiTheme;

        [Tooltip("Animasi gerombolan yang sudah dipanggang jadi tekstur (Tools/Grimoire/Bake " +
                 "Enemy VAT). Kosong = musuh kembali jadi kapsul — bukan jalur usang, tapi " +
                 "jaring: panggangan yang hilang harus menyisakan gerombolan yang tetap bisa " +
                 "dimainkan, bukan lapangan tanpa musuh terlihat.")]
        [SerializeField] VatClipSet _enemyVat;

        [Tooltip("Avatar pemain (model + animator + cloth). Kosong = kapsul primitif lama.")]
        [SerializeField] GameObject _playerAvatarPrefab;

        [Tooltip("Prefab pengganti benda FX yang dulu primitif. Slot kosong = primitif lama.")]
        [SerializeField] FxLibrary _fx;

        [Header("Debug")]
        [Tooltip("Saklar curang buat rekaman & tes. Boleh dikosongkan — dan aset ini pun tidak " +
                 "berefek apa pun sampai gerbang 'Enabled' di dalamnya dinyalakan.")]
        [SerializeField] DebugConfig _cheats;

        void Awake()
        {
            if (_database == null || _balance == null || _look == null)
            {
                Debug.LogError("[ProtoBootstrap] ContentDatabase / GameBalance / SceneLook " +
                               "belum diisi di Inspector.", this);
                enabled = false;
                return;
            }

            Application.runInBackground = true;

            // Options are owned by the menu, but a run started from it must inherit them.
            var settings = GameSettings.Load();
            settings.Apply();

            _look.ApplyEnvironment();

            var cam = BuildCamera();
            BuildVolume();
            var sun = BuildLight();
            var ground = BuildGround();

            var playerGo = BuildPlayer(cam);

            var managerGo = new GameObject("EnemyManager");
            managerGo.transform.SetParent(transform, false);
            var enemies = managerGo.AddComponent<EnemyManager>();

            var caster = playerGo.AddComponent<PlayerCaster>();
            var motor = playerGo.AddComponent<PlayerMotor>();

            // Avatar dinyawakan SETELAH caster ada — dialah yang memberi tahu kapan pose
            // Idle harus ditahan (menembak memakai Idle saat berdiri).
            var avatar = playerGo.GetComponentInChildren<PlayerAvatar>();
            if (avatar != null) avatar.Init(caster);

            // Pemain ikut hangus saat mati, seperti gerombolan yang dibunuhnya. Dipasang di sini
            // dan bukan di dalam PlayerCaster: yang dipegangnya renderer, bukan angka.
            playerGo.AddComponent<PlayerBurnout>().Init(caster);

            // Dipasang SEBELUM Init: keduanya membaca saklar ini selama penyiapan, dan memasangnya
            // belakangan berarti wave pertama sudah lewat sebelum curangnya berlaku.
            enemies.Cheats = _cheats;
            caster.Cheats = _cheats;

            // Satu wadah pakta, DIPAKAI BERSAMA. Sengaja bukan dua salinan: separuh isi sebuah
            // pakta menggeser stat pemain dan separuhnya lagi menggeser gerombolan, dan dua salinan
            // yang menyimpang satu sama lain akan menghasilkan pakta yang berkahnya berlaku tapi
            // kutuknya tidak — bug yang tidak akan pernah dilaporkan siapa pun.
            var pacts = new WorldPacts();
            enemies.Pacts = pacts;
            caster.Pacts = pacts;

            // Sebelum Init: kolam FX dibangun di dalamnya, dan library yang dipasang belakangan
            // berarti benda-benda yang sudah terlanjur lahir tetap primitif seumur run.
            caster.Fx = _fx;

            // Dipakai HANYA oleh sinar memantul, untuk tahu di mana tepi layar berada. Boleh
            // kosong — tanpa kamera, sinarnya memantul di kotak seluas jangkauannya sendiri.
            caster.Lens = cam;

            // Alasan yang sama: renderernya dibangun di dalam Init dan tidak pernah dibangun
            // ulang, jadi panggangan yang dipasang belakangan tidak akan pernah terpakai.
            enemies.Vat = _enemyVat;

            // Toggle performa dari menu. AND, bukan timpa: bayangan cuma menyala kalau pemilik
            // project TIDAK mematikannya di inspector DAN pemainnya tidak mematikannya di menu.
            enemies.EnemyShadows &= settings.EnemyShadows;

            enemies.Init(playerGo.transform, caster, _balance, _database);
            caster.Init(enemies, _database, _balance);
            motor.Init(enemies, caster, _balance);

            if (_cheats != null && _cheats.Enabled)
            {
                Debug.LogWarning("[ProtoBootstrap] DebugConfig AKTIF — build ini curang. " +
                                 "Matikan 'Enabled' sebelum dikirim ke siapa pun.", _cheats);
            }

            var shake = cam.gameObject.AddComponent<CameraShake>();

            var arenaCam = _rig.gameObject.AddComponent<ArenaCamera>();
            arenaCam.Init(playerGo.transform, cam, _balance);

            // Pengisi data pudar tembus pandang pohon (shader Grimoire/PropSeeThrough) —
            // duduk di kamera karena posisi layar pemain milik kamera, bukan milik pemain.
            // Jari-jari, kekuatan, dan keburaman pusatnya bisa disetel di Inspector SELAGI
            // main: komponennya ada di GameObject kamera, bernama sama dengan kelasnya.
            var seeThrough = cam.gameObject.AddComponent<SeeThroughFeeder>();
            seeThrough.Init(playerGo.transform, cam);

            // Musuh lahir relatif terhadap apa yang TERLIHAT, bukan terhadap pemain. Kamera punya
            // zona mati, jadi pemain boleh menyimpang jauh dari pusat layar — kotak yang mengikuti
            // pemain akan menetaskan musuh di dalam layar, di sisi yang barusan ditinggalkan.
            enemies.SetSpawnAnchor(_rig);

            // Lantai TIDAK ikut kamera. Arenanya berbatas lagi, dan tepi lantai itulah yang
            // membuat dindingnya terlihat — tanpa itu pemain menabrak batas tak kasat mata dan
            // yang terbaca cuma kontrol yang macet.
            BiomeDresser dresser = null;
            Gloom gloom = null;

            if (_biomes != null && _biomes.Length > 0)
            {
                dresser = new GameObject("Biome").AddComponent<BiomeDresser>();
                dresser.transform.SetParent(transform, false);
                dresser.Init(_balance, _biomes, sun, cam, ground, _look, _rig);

                enemies.OnWaveStarted += dresser.OnWaveStarted;

                // Bayangan awan dan berkas cahaya. Dipasang di bawah rig supaya ikut kamera, tapi
                // polanya dikunci ke koordinat dunia — kalau tidak, awannya menempel di layar.
                var sky = new GameObject("Atmosphere").AddComponent<Atmosphere>();
                sky.transform.SetParent(transform, false);

                // Rentangnya diambil dari yang benar-benar terlihat, bukan dari arena: bidangnya
                // ikut kamera, jadi yang perlu tertutup cuma seluas layar plus sedikit margin.
                float span = cam.orthographicSize * cam.aspect * 2.6f;
                sky.Init(_rig, span);

                var lamps = new GameObject("ArenaLights").AddComponent<ArenaLights>();
                lamps.transform.SetParent(transform, false);
                lamps.Init(_balance, _biomes[0]);

                // Satu-satunya lampu yang boleh menempel ke pemain. Di malam hari pemain bisa
                // berjalan jauh dari setiap lampu arena, dan tanpa ini ia menghilang sepenuhnya —
                // bukan jadi siluet, tapi jadi tidak ada.
                var carried = new GameObject("PlayerLight");
                carried.transform.SetParent(playerGo.transform, false);
                carried.transform.localPosition = new Vector3(0f, Stage.CarriedLightHeight, 0f);

                var glow = carried.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.shadows = LightShadows.None;
                glow.enabled = false;

                gloom = new GameObject("Gloom").AddComponent<Gloom>();
                gloom.transform.SetParent(transform, false);
                gloom.Init(_rig, playerGo.transform, span);

                var weather = new GameObject("Weather").AddComponent<Weather>();
                weather.VfxEnabled = settings.WeatherVfx;
                weather.transform.SetParent(transform, false);
                weather.Init(_rig, sun, sky, glow, cam);

                dresser.Attach(lamps, glow, sky, gloom, weather, playerGo.transform);

                // Bilah demo: tombol ganti wajah & cuaca. Dibangun DI SINI karena hanya di sini
                // dresser dan weather dua-duanya sudah ada dan sudah tersambung.
                //
                // Saklarnya pindah ke DebugConfig (dulu bool terpisah di komponen ini): ikut
                // gerbang induk 'Enabled', jadi build yang dikirim tidak mungkin membawanya
                // cuma karena satu centang scene lupa dimatikan.
                if (_cheats != null && _cheats.DemoBarVisible)
                {
                    var bar = new GameObject("DemoBar").AddComponent<DemoBar>();
                    bar.transform.SetParent(transform, false);
                    bar.Theme = _uiTheme;
                    bar.Init(dresser, weather, _biomes.Length);
                }
            }

            // Bahan bunyi dari aset; kosong = jalur lama (sintesis) tetap jalan utuh.
            // Resources.Load untuk ASET DATA — preseden RuneTileSet; larangannya untuk sistem.
            var audioTheme = Resources.Load<AudioTheme>("AudioTheme");

            var music = new GameObject("Music").AddComponent<MusicDirector>();
            music.transform.SetParent(transform, false);
            music.Init(audioTheme, GameSettings.Load().MusicVolume);

            var audio = new GameObject("Audio").AddComponent<AudioDirector>();
            audio.transform.SetParent(transform, false);
            audio.Theme = audioTheme;
            audio.Music = music;
            audio.Init(GameSettings.Load().SfxVolume);

            // Musik pertarungan menyala dari detik pertama run — build phase juga bagian
            // dari lapangan, dan keheningan sebelum wave pertama terbaca sebagai bug.
            if (audioTheme != null) music.SetLoop(audioTheme.CombatLoop);

            // Burst radius drives the kick, so a screen-clearing reaction outweighs a small one.
            enemies.OnReaction += (at, reaction) =>
            {
                shake.Add(0.16f + reaction.BurstRadius * 0.045f);

                // FIRESTORM berbunyi sebagai badai api, bukan sebagai dentang generik —
                // klip per-reaction dari tema, dentang lama sebagai jaringnya.
                audio.PlayReaction(reaction);
            };

            // Single-target casts stay quiet; only the ones that cover ground get a nudge.
            caster.OnCast += inst =>
            {
                bool heavy = IsHeavy(inst.Def.Kind);
                if (heavy) shake.Add(0.07f);

                // Resolusi paling-spesifik-menang: klip milik PIECE ini -> klip elemennya
                // (berat/ringan) -> slot inti -> sintesis. Absolute Zero tidak boleh
                // berbunyi sama dengan Fireball.
                audio.PlayCast(inst.Def, heavy);
            };

            // Pantulan berbunyi per pantulan — dedup & prioritas di dalam yang menahan
            // hujannya saat dua puluh peluru memantul bersamaan.
            caster.OnBounce += _ => audio.PlayBounce();

            enemies.OnKill += _ => audio.Play(AudioDirector.Sound.Death, 0.35f);
            enemies.OnWaveStarted += _ => audio.Play(AudioDirector.Sound.WaveStart, 0.8f);

            // Wave baru = kembali ke musik pertarungan (idempoten — kalau sudah, diam).
            if (audioTheme != null)
                enemies.OnWaveStarted += _ => music.SetLoop(audioTheme.CombatLoop);

            // Satu-satunya suara yang tidak pernah diredam dan tidak pernah dijeda: kedatangan
            // boss harus terdengar bahkan di tengah tiga ratus musuh yang meledak.
            enemies.OnBossSpawned += _ => audio.Play(AudioDirector.Sound.BossRoar, 1f);
            enemies.OnBossDied += _ => audio.Play(AudioDirector.Sound.BossRoar, 1f, 0.6f);

            // Fanfare kemenangan disimpan untuk BOSS tumbang — bukan tiap wave beres;
            // wave beres terjadi tiap dua menit dan hadiah sesering itu berhenti terasa hadiah.
            enemies.OnBossDied += _ => audio.VictoryFanfare();

            // Enemies used to just wink out of existence. A pop on death is what makes clearing a
            // pack read as an event instead of a disappearance.
            enemies.OnKill += caster.DeathBurst;

            // Charge generation lives on the kill, so "collect them by fighting" is literal.
            enemies.OnKill += caster.OnEnemyKilled;

            // EventSystem untuk halaman setelan (ESC). Seluruh UI permainan memakai hit-test
            // sendiri lewat ProtoInput, jadi scene ini tidak pernah membutuhkannya — sampai
            // panel setelan (Button & Slider UGUI sungguhan) ikut menumpang di sini.
            var eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(transform, false);
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif

            var uiGo = new GameObject("GrimoireUI");
            uiGo.transform.SetParent(transform, false);
            var ui = uiGo.AddComponent<GrimoireUI>();
            ui.Init(caster, enemies, cam, _database, _balance, dresser, _uiTheme);

            // UI bersuara lewat satu pegangan ini: ambil/taruh piece, beli, reroll, evolve,
            // game over. Null aman — UI yang bisu bukan UI yang rusak.
            ui.Sfx = audio;

            // Sutradara run: peta act, portal antar wave, pulau rehat. Dipasang SETELAH UI —
            // ia mengecek WaveActive saat lahir, dan saklar curang OpeningWave hidup di ui.Init.
            var run = new GameObject("RunDirector").AddComponent<RunDirector>();
            run.transform.SetParent(transform, false);
            run.Theme = _uiTheme;
            run.Init(enemies, motor, arenaCam, cam, _balance, _database, playerGo.transform, _rig,
                gloom, dresser, _restBiome);
            // Ruangan singgah dipramuat SEKARANG, di awal run - bukan saat nodenya diinjak.
            // Memuat scene tepat saat pemain menekan node berarti membayar jeda di detik yang
            // paling tidak sabar; ketiganya kecil, dan memori yang dibayar untuk itu murah.
            var rooms = new GameObject("Rooms").AddComponent<RoomLoader>();
            rooms.transform.SetParent(transform, false);
            rooms.Init(cam);
            ui.AttachRooms(rooms);

            ui.AttachRun(run);

            // DI BAWAH AttachRun dengan sengaja: di dalamnya GrimoireUI MENGISI (bukan
            // menambah) run.OnRestEntered — subscribe sebelum itu berarti tersapu diam-diam.
            if (audioTheme != null)
                run.OnRestEntered += _ => music.SetLoop(audioTheme.ShopLoop);

            // Diumumkan lewat banner yang sama dengan reaksi, bukan lewat widget baru: pemain
            // sudah tahu harus melihat ke mana saat sesuatu penting terjadi.
            enemies.OnBossSpawned += boss =>
                ui.Announce(boss.Def.DisplayName.ToUpperInvariant(), new Color(1f, 0.35f, 0.3f));

            enemies.OnBossDied += at =>
                ui.Announce("SLAIN", new Color(1f, 0.85f, 0.4f), at);

            caster.OnHurt += () => audio.Play(AudioDirector.Sound.Hit, 0.6f, 0.8f);
        }

        Transform _rig;

        static bool IsHeavy(CastKind kind) =>
            kind == CastKind.Nova || kind == CastKind.AreaAtTarget || kind == CastKind.Line ||
            kind == CastKind.SunStrike || kind == CastKind.ForcePush || kind == CastKind.RollingBall;

        Camera BuildCamera()
        {
            // Rig terpisah dari kamera: rig yang mengikuti pemain, kamera yang berguncang. Kalau
            // keduanya menulis transform yang sama, CameraShake akan menarik balik ke titik asalnya
            // tiap frame dan pengikutannya mati diam-diam.
            _rig = new GameObject("Camera Rig").transform;
            _rig.SetParent(transform, false);

            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(_rig, false);
            go.transform.localPosition = Stage.CameraOffset;
            go.transform.localRotation = Quaternion.Euler(Stage.CameraPitch, 0f, 0f);

            var cam = go.AddComponent<Camera>();
            cam.orthographic = Stage.CameraOrthographic;
            cam.orthographicSize = Stage.CameraSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderColor.Of(_look.HorizonColor);

            // Telinga scene. Tanpa ini Unity memperingatkan "no audio listeners" tiap kali ada
            // yang berbunyi, dan peringatan itu membanjiri console sampai kesalahan sungguhan
            // tenggelam di dalamnya — biaya sebenarnya bukan bisunya, tapi hilangnya console
            // sebagai alat.
            //
            // Menumpang di kamera, bukan di pemain: suara dicampur dari sudut pandang PENONTON,
            // dan kamera menunduk dari jauh. Ditaruh di pemain, seluruh gerombolan yang mengepung
            // akan terdengar tepat di telinga.
            go.AddComponent<AudioListener>();

            var urp = go.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            return cam;
        }

        void BuildVolume()
        {
            if (_look.PostProcess == null) return;

            var go = new GameObject("Global Volume");
            go.transform.SetParent(transform, false);

            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = _look.PostProcess;
        }

        Light BuildLight()
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);

            var sun = go.AddComponent<Light>();
            _look.ApplySun(sun);
            return sun;
        }

        Renderer BuildGround()
        {
            // Terrain yang SUDAH ADA di scene selalu menang, dan tidak disentuh sedikit pun.
            //
            // Lantai yang disusun tangan adalah lantai yang sudah punya pemilik: teksturnya
            // dicat, tingginya dipahat, lapisannya dipilih. Membangun lantai sendiri di
            // sampingnya menghasilkan dua lantai yang saling menutupi, dan yang menang adalah
            // yang kebetulan lebih tinggi — kegagalan yang terlihat seperti pilihan.
            var authored = Terrain.activeTerrain;

            if (authored != null)
            {
                // null, dan itu bukan kelalaian: BiomeDresser hanya mengecat lantai kalau ada
                // Renderer yang dititipkan padanya.
                return null;
            }

            var biome = _biomes != null && _biomes.Length > 0 ? _biomes[0] : null;

            if (biome != null && biome.GroundMaterial != null &&
                biome.GroundLayers != null && biome.GroundLayers.Length > 0)
            {
                BuildTerrain(biome);
                return null;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground";
            go.transform.SetParent(transform, false);

            // Diukur dari arena. Plane bawaan Unity 10 unit, jadi skalanya seperlimanya.
            // Marginnya untuk musuh yang lahir di luar arena — tanpa itu mereka berjalan masuk
            // sambil melayang di atas kekosongan.
            float margin = Stage.GroundMargin;
            go.transform.localScale = new Vector3(
                (_balance.ArenaHalfX + margin) * 0.2f, 1f, (_balance.ArenaHalfZ + margin) * 0.2f);

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _look.CreateSurface(_look.GroundColor);

            // The floor is what the long shadows fall on — that is most of the look.
            r.receiveShadows = true;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return r;
        }

        /// <summary>
        /// Lantai sebagai Terrain sungguhan, DATAR.
        ///
        /// Datar itu keputusan, bukan keterbatasan: kamera ortografis menunduk 68 derajat, dan
        /// bukit apa pun langsung menyembunyikan apa yang ada di baliknya — di game yang seluruh
        /// isinya membaca dari arah mana gerombolan datang, punggung bukit adalah tempat musuh
        /// menghilang. Yang diambil dari terrain bukan reliefnya, melainkan shader dan teksturnya.
        ///
        /// Urutan penyiapan TerrainData penting dan gagal tanpa error kalau dibalik:
        /// <c>heightmapResolution</c> menyetel ulang <c>size</c>, dan <c>alphamapResolution</c>
        /// membuang alphamap yang sudah ditulis.
        /// </summary>
        void BuildTerrain(BiomeDefinition biome)
        {
            var data = new TerrainData();

            // 33 sudah lebih dari cukup untuk bidang yang seluruh titiknya bernilai nol. Resolusi
            // heightmap di lantai datar tidak membeli apa pun selain memori.
            data.heightmapResolution = 33;
            data.size = new Vector3(biome.GroundSize.x, 1f, biome.GroundSize.y);

            data.alphamapResolution = 256;
            data.terrainLayers = biome.GroundLayers;

            PaintGround(data, biome);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Ground";
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(
                -biome.GroundSize.x * 0.5f, 0f, -biome.GroundSize.y * 0.5f);

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = biome.GroundMaterial;

            // Lantai adalah tempat bayangan panjang jatuh, dan itu sebagian besar tampilannya.
            terrain.shadowCastingMode = ShadowCastingMode.Off;

            // Basemap adalah versi murah yang dipakai di kejauhan, dan di sanalah teksturnya
            // berubah jadi bubur. Jaraknya dilewatkan seluruh lapangan supaya tidak pernah aktif.
            terrain.basemapDistance = 1000f;
            terrain.heightmapPixelError = 5f;
        }

        /// <summary>
        /// Mencat lapisan kedua sebagai bercak lewat derau.
        ///
        /// Satu lapisan yang diubin rata di lantai seluas ini terbaca sebagai kain, bukan tanah —
        /// polanya berulang dan mata menemukan pengulangannya dalam sedetik. Bercak berskala besar
        /// memecah pengulangan itu sekaligus memberi lapangan tanda tempat yang bisa dipakai
        /// mengukur bahwa kita sudah berpindah.
        /// </summary>
        static void PaintGround(TerrainData data, BiomeDefinition biome)
        {
            int size = data.alphamapResolution;
            int layers = data.terrainLayers.Length;
            var map = new float[size, size, layers];

            // Dua lapisan pertama saja. Lapisan ketiga dan seterusnya sengaja dibiarkan nol —
            // dipakai kalau nanti ada yang mengecat pasir atau batu di tempat tertentu.
            if (layers < 2 || biome.GroundPatchCoverage <= 0f)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++) map[y, x, 0] = 1f;
                }

                data.SetAlphamaps(0, 0, map);
                return;
            }

            float scale = Mathf.Max(1f, biome.GroundPatchSize);
            float world = Mathf.Max(1f, biome.GroundSize.x);

            var noise = new float[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size * world / scale;
                    float v = y / (float)size * world / scale;

                    // Dua oktaf: yang besar menentukan letak bercaknya, yang halus merusak tepinya.
                    // Satu oktaf saja menghasilkan bercak berbentuk awan yang terlalu rapi untuk
                    // dibaca sebagai tanah terbuka.
                    noise[y * size + x] = Mathf.PerlinNoise(u, v) * 0.75f
                                          + Mathf.PerlinNoise(u * 3.1f, v * 3.1f) * 0.25f;
                }
            }

            // Ambangnya dicari dari SEBARAN deraunya sendiri, bukan dipatok ke angka tetap.
            //
            // Percobaan pertama membandingkan derau dengan (1 − cakupan) langsung, dan hasilnya
            // nol bercak: PerlinNoise berkerumun di sekitar 0,5 dan nyaris tidak pernah menyentuh
            // 0,72, jadi ambang berapa pun di atas situ tidak pernah terlewati. Kegagalannya diam —
            // lantainya tetap tergambar, cuma satu warna, dan tidak ada apa pun yang memberi tahu
            // bahwa lapisan keduanya tidak pernah terpakai.
            var sorted = (float[])noise.Clone();
            System.Array.Sort(sorted);

            int cut = Mathf.Clamp(
                Mathf.RoundToInt((1f - biome.GroundPatchCoverage) * (sorted.Length - 1)),
                0, sorted.Length - 1);

            float threshold = sorted[cut];

            // Lebar peralihan diambil dari sebarannya juga, supaya tepi bercaknya selalu selembut
            // ini berapa pun cakupannya diubah.
            float band = Mathf.Max(0.004f,
                (sorted[sorted.Length - 1] - sorted[0]) * 0.12f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float patch = Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(threshold - band, threshold + band, noise[y * size + x]));

                    map[y, x, 0] = 1f - patch;
                    map[y, x, 1] = patch;
                }
            }

            data.SetAlphamaps(0, 0, map);
        }

        GameObject BuildPlayer(Camera cam)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player";
            go.transform.SetParent(transform, false);

            // Titik mulai DIACAK, bukan selalu pusat arena. System.Random, bukan
            // UnityEngine.Random — yang terakhir milik gameplay, dan mengambil satu angka darinya
            // menggeser seluruh urutan acak run. Margin menjauhkan titik lahir dari dinding:
            // lahir menempel dinding berarti membuka wave dengan separuh arah kabur yang buntu.
            //
            // Dijepit juga oleh kotak-tengah kamera (rumus yang sama dengan ArenaCamera.Init):
            // titik yang lebih pinggir membuat rig tertahan jepitan arena, dan pemain muncul
            // MENEPI di layar sejak frame pertama run.
            var dice = new System.Random(System.Environment.TickCount);
            float margin = Stage.SpawnMargin;

            float halfWidth = cam.orthographicSize * cam.aspect;
            float halfDepth = cam.orthographicSize /
                              Mathf.Sin(Mathf.Max(15f, cam.transform.eulerAngles.x) * Mathf.Deg2Rad);

            float rx = Mathf.Min(Mathf.Max(0f, _balance.ArenaHalfX - margin),
                Mathf.Max(0f, _balance.ArenaHalfX - halfWidth));
            float rz = Mathf.Min(Mathf.Max(0f, _balance.ArenaHalfZ - margin),
                Mathf.Max(0f, _balance.ArenaHalfZ - halfDepth));

            float x = ((float)dice.NextDouble() * 2f - 1f) * rx;
            float z = ((float)dice.NextDouble() * 2f - 1f) * rz;

            go.transform.position = new Vector3(x, Stage.PlayerHeight, z);

            // Rig kamera menyusul ke titik lahir SEKARANG, sebelum ArenaCamera dipasang —
            // komponen itu merekam fokusnya di Awake, dan rig yang masih di titik nol membuka
            // run dengan panning panjang dari tengah arena ke pemain.
            if (_rig != null) _rig.position = new Vector3(x, 0f, z);

            go.transform.localScale = Vector3.one * Stage.PlayerScale;

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var r = go.GetComponent<Renderer>();

            if (_playerAvatarPrefab != null)
            {
                // Badan sungguhan menggantikan kapsul: mesh kapsulnya dicabut, tapi GameObject
                // dan posisinya tetap — seluruh sistem lain memegang transform INI, bukan
                // modelnya. Pivot pemain ada di tengah dada (y 0,9 dunia), sedangkan pivot
                // model di telapak kaki; offset lokalnya membatalkan itu. Skala parent 0,9
                // warisan kapsul dikompensasi supaya avatar tampil pada ukuran aslinya.
                Destroy(r);
                Destroy(go.GetComponent<MeshFilter>());

                var avatar = Instantiate(_playerAvatarPrefab, go.transform);
                avatar.transform.localPosition = Stage.AvatarOffset;
                avatar.transform.localScale = Vector3.one / Mathf.Max(0.05f, Stage.PlayerScale);
            }
            else
            {
                r.sharedMaterial = _look.CreateSurface(_look.PlayerColor);

                // Enemy shadows stay off for the 200-enemy budget; the player is a single caster.
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            return go;
        }
    }
}
