using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Sutradara run: peta act ala Slay the Spire, portal FISIK di lapangan, dan pulau rehat.
    ///
    /// Alurnya menggantikan tombol "MULAI WAVE": begitu lapangan bersih, dua-tiga portal menetas
    /// di dekat pemain — satu per cabang peta yang bisa diinjak. Pemain MENGKLIK portal, karakter
    /// berjalan sendiri ke sana, dan isi portalnya yang menentukan wave berikutnya: pertarungan,
    /// elite, boss puncak, atau pulau rehat (toko / kejadian / mesin slot).
    ///
    /// Portal dibuat dari primitif + lampu, bukan prefab paket — komposisi runtime di sini tidak
    /// bisa menunjuk aset tanpa pass editor, dan cincin yang berputar sudah cukup terbaca sebagai
    /// gerbang. VFX yang lebih heboh menyusul lewat pass, bukan lewat kode.
    ///
    /// Pulau rehat = KANTONG di scene yang sama, jauh di luar arena tapi masih di atas lantai.
    /// Scene terpisah butuh seluruh state run ikut pindah, dan persistensi run belum ada — begitu
    /// ada, pulau ini tinggal ditukar tanpa menyentuh alur peta.
    /// </summary>
    public class RunDirector : MonoBehaviour
    {
        // Di dalam jangkauan lantai (±70 / ±60) tapi jauh di luar elips arena (40 / 30) — hutan
        // rapat di luar dinding jadi latar pulaunya, dan petak hash yang sama berarti pulau yang
        // SAMA setiap kali singgah. Tempat rehat harus bisa dikenali.
        static readonly Vector3 IslandCentre = new Vector3(50f, 0f, 42f);

        EnemyManager _enemies;
        PlayerMotor _motor;
        ArenaCamera _camera;
        Camera _lens;
        GameBalance _balance;
        ContentDatabase _db;
        Transform _player;
        Transform _rig;
        Font _font;

        System.Random _dice;

        public RunMap Map { get; private set; }
        public int Act { get; private set; } = 1;

        /// <summary>Portal sedang terpampang, menunggu dipilih.</summary>
        public bool Choosing { get; private set; }

        /// <summary>Sedang di pulau rehat.</summary>
        public bool Resting { get; private set; }

        public RunNodeKind RestKind { get; private set; }

        /// <summary>Node yang sudah diinjak act ini — jejak untuk peta di UI.</summary>
        public readonly List<int> Trail = new List<int>();

        /// <summary>UI menumpang di sini: simpan tangan, jual serakan, reset cooldown.</summary>
        public System.Action OnEmbark;

        /// <summary>UI membuka panel toko / kejadian / slot begitu pemain mendarat di pulau.</summary>
        public System.Action<RunNodeKind> OnRestEntered;

        public System.Action<string, Color> OnAnnounce;

        /// <summary>Gerbang dari UI: boleh berangkat bertarung? (minimal satu skill terpasang).</summary>
        public System.Func<bool> CanEmbark;

        /// <summary>Benar selama sebuah panel UI terbuka — klik peta/toko tidak boleh tembus
        /// ke portal yang kebetulan berdiri di belakangnya.</summary>
        public System.Func<bool> ClickBlocked;

        class Portal
        {
            public RunNode Node;
            public Transform Root;
            public Transform Ring;
            public bool IsReturn;
        }

        readonly List<Portal> _portals = new List<Portal>();
        readonly List<GameObject> _islandProps = new List<GameObject>();
        Vector3 _returnPos;

        public void Init(EnemyManager enemies, PlayerMotor motor, ArenaCamera camera, Camera lens,
            GameBalance balance, ContentDatabase db, Transform player, Transform rig)
        {
            _enemies = enemies;
            _motor = motor;
            _camera = camera;
            _lens = lens;
            _balance = balance;
            _db = db;
            _player = player;
            _rig = rig;

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // System.Random milik sutradara, TERPISAH dari urutan acak gameplay: susunan peta dan
            // isi elite tidak boleh menggeser sebaran drop atau arah semburan skill.
            _dice = new System.Random(System.Environment.TickCount);

            Map = GenerateMap();

            _enemies.OnWaveCleared += OnWaveCleared;

            // Wave pertama pun lewat portal. Kecuali saklar curang sudah telanjur memulai wave —
            // saat itu portal menunggu giliran lewat OnWaveCleared seperti biasa.
            if (!_enemies.WaveActive) SpawnPortals();
        }

        RunMap GenerateMap()
        {
            return RunMap.Generate(Act, _balance.MapFloorsPerAct, _balance.MapLanes, _dice.Next(),
                _balance.MapEliteChance, _balance.MapShopChance, _balance.MapEventChance,
                _balance.MapGambleChance, _balance.MapEliteMinFloor);
        }

        void OnWaveCleared()
        {
            if (Resting) return;

            if (Map.AtBoss)
            {
                NextAct();
                return;
            }

            SpawnPortals();
        }

        void NextAct()
        {
            Act++;
            Map = GenerateMap();
            Trail.Clear();

            OnAnnounce?.Invoke("ACT " + Act, new Color(1f, 0.84f, 0.32f));
            SpawnPortals();
        }

        // ==================================================================
        //  portal
        // ==================================================================

        void SpawnPortals()
        {
            ClearPortals();

            var choices = Map.Reachable();
            if (choices.Count == 0) return;

            // Kipas menghadap UTARA layar (+Z), terurut lajur kiri-ke-kanan supaya susunan
            // portalnya menggemakan susunan peta — portal kiri memang cabang kiri.
            choices.Sort((a, b) => a.Lane.CompareTo(b.Lane));

            for (int i = 0; i < choices.Count; i++)
            {
                float angle = (90f + (i - (choices.Count - 1) * 0.5f) * -42f) * Mathf.Deg2Rad;
                Vector3 at = _player.position;
                at.y = 0f;
                at += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 6.5f;
                at = PullInsideArena(at);

                _portals.Add(BuildPortal(choices[i], at, false));
            }

            Choosing = true;
        }

        /// <summary>
        /// Portal yang menetas terlalu dekat dinding ditarik masuk. Pemain yang menutup wave di
        /// pojok arena tetap harus bisa melihat dan mencapai ketiga portalnya.
        /// </summary>
        Vector3 PullInsideArena(Vector3 at)
        {
            float nx = at.x / (_balance.ArenaHalfX - 3f);
            float nz = at.z / (_balance.ArenaHalfZ - 3f);
            float outside = nx * nx + nz * nz;

            if (outside <= 1f) return at;

            float scale = 1f / Mathf.Sqrt(outside);
            at.x *= scale;
            at.z *= scale;
            return at;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Portal BuildPortal(RunNode node, Vector3 at, bool isReturn)
        {
            var kind = isReturn ? RunNodeKind.Fight : node.Kind;

            var root = new GameObject("Portal " + (isReturn ? "LANJUT" : kind.ToString())).transform;
            root.SetParent(transform, false);
            root.position = at;

            Color tone = KindColor(kind);

            // Cincin bola kecil yang berputar. Bentuk paling murah yang tetap terbaca "gerbang" —
            // dan karena berputar, ia tidak pernah disangka prop hutan.
            var ring = new GameObject("Ring").transform;
            ring.SetParent(root, false);

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, tone);

            for (int i = 0; i < 8; i++)
            {
                var bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bead.name = "Bead";

                var collider = bead.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                float a = i / 8f * Mathf.PI * 2f;
                bead.transform.SetParent(ring, false);
                bead.transform.localPosition =
                    new Vector3(Mathf.Cos(a) * 1.15f, 1.1f + Mathf.Sin(a) * 1.15f, 0f);
                bead.transform.localScale = Vector3.one * 0.34f;

                var renderer = bead.GetComponent<Renderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.SetPropertyBlock(block);
            }

            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(root, false);
            glowGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);

            var glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = tone;
            glow.intensity = 5f;
            glow.range = 7f;
            glow.shadows = LightShadows.None;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root, false);
            labelGo.transform.localPosition = new Vector3(0f, 2.9f, 0f);

            // Dimiringkan sejajar kamera (menunduk 68°) supaya hurufnya menghadap lensa,
            // bukan rebah di tanah. Kamera tidak pernah berputar, jadi satu rotasi tetap cukup.
            labelGo.transform.rotation = Quaternion.Euler(68f, 0f, 0f);

            var label = labelGo.AddComponent<TextMesh>();
            label.text = isReturn ? "LANJUT" : KindLabel(kind);
            label.font = _font;
            label.fontSize = 44;
            label.characterSize = 0.055f;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = tone;

            var meshRenderer = labelGo.GetComponent<MeshRenderer>();
            if (meshRenderer != null && _font != null) meshRenderer.sharedMaterial = _font.material;

            return new Portal { Node = node, Root = root, Ring = ring, IsReturn = isReturn };
        }

        public static Color KindColor(RunNodeKind kind)
        {
            switch (kind)
            {
                case RunNodeKind.Elite: return new Color(1f, 0.42f, 0.25f);
                case RunNodeKind.Shop: return new Color(1f, 0.84f, 0.32f);
                case RunNodeKind.Event: return new Color(0.75f, 0.5f, 1f);
                case RunNodeKind.Gamble: return new Color(1f, 0.45f, 0.85f);
                case RunNodeKind.Boss: return new Color(1f, 0.2f, 0.2f);
                default: return new Color(0.45f, 0.9f, 1f);
            }
        }

        public static string KindLabel(RunNodeKind kind)
        {
            switch (kind)
            {
                case RunNodeKind.Elite: return "ELITE";
                case RunNodeKind.Shop: return "TOKO";
                case RunNodeKind.Event: return "???";
                case RunNodeKind.Gamble: return "SLOT";
                case RunNodeKind.Boss: return "BOSS";
                default: return "WAVE";
            }
        }

        void ClearPortals()
        {
            for (int i = 0; i < _portals.Count; i++)
            {
                if (_portals[i].Root != null) Destroy(_portals[i].Root.gameObject);
            }

            _portals.Clear();
            Choosing = false;
        }

        void Update()
        {
            // Cincin berputar pelan — satu-satunya animasi yang dibutuhkan gerbang untuk hidup.
            for (int i = 0; i < _portals.Count; i++)
            {
                if (_portals[i].Ring != null)
                    _portals[i].Ring.Rotate(0f, 0f, 55f * Time.deltaTime, Space.Self);
            }

            if (!Choosing || !ProtoInput.LeftClickDown) return;
            if (ClickBlocked != null && ClickBlocked()) return;

            // Klik dicocokkan di RUANG LAYAR, bukan lewat physics — project ini tidak punya satu
            // pun collider yang hidup, dan menambahkannya cuma untuk klik portal berarti membayar
            // simulasi fisika untuk satu raycast.
            Vector2 mouse = ProtoInput.MousePosition;

            for (int i = 0; i < _portals.Count; i++)
            {
                var portal = _portals[i];
                if (portal.Root == null) continue;

                Vector3 screen = _lens.WorldToScreenPoint(portal.Root.position + Vector3.up * 1.1f);
                if (screen.z < 0f) continue;

                if (Vector2.Distance(mouse, new Vector2(screen.x, screen.y)) > 52f) continue;

                Choose(portal);
                return;
            }
        }

        void Choose(Portal portal)
        {
            bool fights = portal.IsReturn == false &&
                          (portal.Node.Kind == RunNodeKind.Fight ||
                           portal.Node.Kind == RunNodeKind.Elite ||
                           portal.Node.Kind == RunNodeKind.Boss);

            if (fights && CanEmbark != null && !CanEmbark())
            {
                OnAnnounce?.Invoke("PASANG MINIMAL 1 SKILL DULU", new Color(1f, 0.75f, 0.4f));
                return;
            }

            Choosing = false;

            // Portal lain padam; yang dipilih tetap menyala sebagai tujuan berjalan.
            for (int i = 0; i < _portals.Count; i++)
            {
                if (_portals[i] != portal && _portals[i].Root != null)
                    Destroy(_portals[i].Root.gameObject);
            }

            _portals.Clear();
            _portals.Add(portal);

            _motor.WalkTo(portal.Root.position, () => Arrive(portal));
        }

        void Arrive(Portal portal)
        {
            var node = portal.Node;
            bool isReturn = portal.IsReturn;

            ClearPortals();

            if (isReturn)
            {
                LeaveRest();
                return;
            }

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
        }

        void Embark() => OnEmbark?.Invoke();

        /// <summary>
        /// Portal itu GERBANG, bukan pintu putar: keluar di titik acak arena, bukan meneruskan
        /// posisi lama. Tanpa ini tiap wave terasa seperti kode yang di-refresh — pemain masih
        /// berdiri di rumput yang sama, memandang sudut yang sama, ditemani kupu-kupu yang sama.
        /// </summary>
        void Relocate()
        {
            const float margin = 9f;

            float x = ((float)_dice.NextDouble() * 2f - 1f)
                      * Mathf.Max(0f, _balance.ArenaHalfX - margin);
            float z = ((float)_dice.NextDouble() * 2f - 1f)
                      * Mathf.Max(0f, _balance.ArenaHalfZ - margin);

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
            _returnPos = _player.position;

            _motor.SetRoam(true);
            _camera.Roam = true;

            _player.position = IslandCentre + new Vector3(0f, 0.9f, -2f);
            _camera.Teleport(IslandCentre);

            BuildIsland(kind);

            OnRestEntered?.Invoke(kind);
            OnAnnounce?.Invoke("PULAU REHAT — " + KindLabel(kind), KindColor(kind));
        }

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
            hostLabel.text = kind == RunNodeKind.Shop ? "PEDAGANG"
                : kind == RunNodeKind.Gamble ? "BANDAR" : "PERTAPA";
            hostLabel.font = _font;
            hostLabel.fontSize = 40;
            hostLabel.characterSize = 0.05f;
            hostLabel.anchor = TextAnchor.MiddleCenter;
            hostLabel.color = KindColor(kind);

            var hostText = hostLabelGo.GetComponent<MeshRenderer>();
            if (hostText != null && _font != null) hostText.sharedMaterial = _font.material;
            _islandProps.Add(hostLabelGo);

            // Portal pulang, sedikit menjauh dari perapian.
            _portals.Add(BuildPortal(null, IslandCentre + new Vector3(4.2f, 0f, -1.6f), true));
            Choosing = true;
        }

        void ClearIsland()
        {
            for (int i = 0; i < _islandProps.Count; i++)
            {
                if (_islandProps[i] != null) Destroy(_islandProps[i]);
            }

            _islandProps.Clear();
        }

        void LeaveRest()
        {
            ClearIsland();

            Resting = false;
            _motor.SetRoam(false);
            _camera.Roam = false;

            _player.position = new Vector3(_returnPos.x, 0.9f, _returnPos.z);
            _camera.Teleport(_returnPos);

            SpawnPortals();
        }
    }
}
