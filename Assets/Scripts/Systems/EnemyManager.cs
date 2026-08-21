using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Pooled swarm. No Rigidbody, no colliders, no per-enemy Update â€” one manager loop owns
    /// movement, ailment ticks and reactions so a few hundred enemies stay cheap without ECS.
    /// Ailments live in a fixed-size slot array: no List, no Dictionary, zero allocation.
    /// </summary>
    public class EnemyManager : MonoBehaviour
    {
        public const int StatusSlots = 4;

        /// <summary>Di bawah ketinggian ini, ruas boss dianggap terbenam: tak terlihat, tak terlukai.</summary>
        public const float BuriedDepth = -0.6f;

        /// <summary>Jeda minimum antar reaksi di satu musuh, biar tidak berkedip.</summary>
        public const float ReactionCooldown = 0.25f;

        public struct StatusSlot
        {
            public int Def;        // index into ContentDatabase.Statuses, -1 = empty
            public float Remaining;
            public int Points;
            public float TickTimer;

            // Titik tarik, dipakai kalau status ini punya PullStrength.
            public float PullX;
            public float PullZ;
        }

        public class Enemy
        {
            /// <summary>
            /// Plain data, not a Transform. Enemies have no GameObject at all — the swarm is drawn
            /// instanced from this array, which is what keeps a few hundred of them inside one draw
            /// call instead of a few hundred.
            /// </summary>
            public Vector3 Pos;

            /// <summary>Facing, in degrees. Meaningless on a capsule; the models will need it.</summary>
            public float Yaw;

            /// <summary>Random offset so the swarm does not breathe in unison.</summary>
            public float Phase;

            /// <summary>
            /// Which side of the caster this one is trying to get to, -1 to 1. Separation alone
            /// stops the pack stacking, but every enemy still walks at the same point, so the mass
            /// piles up behind a running player instead of closing around it. Giving each its own
            /// approach lane is what turns a chase into an encirclement.
            /// </summary>
            public float Flank;

            /// <summary>Index into the renderer's palette. Replaces the old per-enemy tint.</summary>
            public int Tint;

            /// <summary>
            /// Body size multiplier. The readable marker for "this one is different" — it survives
            /// being set on fire, which a colour marker does not, because colour already belongs to
            /// the ailment readout. A boss is this field turned up.
            /// </summary>
            public float Scale;

            /// <summary>What kind of enemy this is. Null falls back to plain chase-and-touch.</summary>
            public EnemyArchetype Kind;

            /// <summary>Counts down to the next shot. Only used by archetypes that shoot.</summary>
            public float AttackTimer;

            public float Hp;
            public float MaxHp;
            public float Speed;
            public bool Alive;

            /// <summary>
            /// <see cref="Time.unscaledTime"/> saat musuh ini terakhir kena. Dipakai bar HP di
            /// atas kepalanya untuk memutuskan muncul atau sembunyi.
            ///
            /// Disimpan sebagai WAKTU, bukan sebagai hitung mundur yang harus dikurangi tiap
            /// frame. Musuh tidak punya GameObject dan tidak semuanya di-update tiap frame, jadi
            /// hitung mundur akan macet di angka terakhirnya untuk yang sedang tidak diproses —
            /// dan barnya menggantung selamanya. Selisih terhadap jam tidak pernah macet.
            ///
            /// Tak berskala, karena barnya urusan mata: pada kecepatan 5x ia akan berkedip hilang
            /// sebelum sempat dibaca kalau ikut waktu permainan.
            /// </summary>
            public float HurtSeen;

            /// <summary>Reaksi tidak boleh meletus lagi sebelum ini nol. Mencegah kedip.</summary>
            public float ReactionLock;

            /// <summary>
            /// Ketinggian tempat musuh ini SEHARUSNYA berada. Disimpan terpisah karena Pos.y
            /// dipakai bergantian untuk melayang bawaan dan untuk terangkat sementara — tanpa
            /// nilai asli ini, sekali diangkat dia tidak pernah tahu harus turun ke mana.
            /// </summary>
            public float GroundY;

            /// <summary>
            /// Sisa detik musuh ini terangkat: tidak bisa jalan, tidak bisa menembak, tidak bisa
            /// menyentuh siapa pun. Ini alasan skill kontrol layak berdiri di sebelah skill damage.
            /// </summary>
            public float LiftTimer;

            /// <summary>Sisa dorongan dari ledakan, meluruh sendiri. Bukan fisika, cuma inersia.</summary>
            public Vector3 Knock;

            /// <summary>Hitung mundur ke serudukan berikutnya (arketipe ber-ChargeEvery).</summary>
            public float ChargeTimer;

            /// <summary>0 = jalan biasa, 1 = ancang-ancang (diam), 2 = melesat.</summary>
            public int ChargePhase;

            /// <summary>Sisa detik fase ancang-ancang / lesatan yang sedang berjalan.</summary>
            public float ChargeLeft;

            /// <summary>Arah lesatan, DIKUNCI saat ancang-ancang selesai — itulah yang membuat
            /// serudukan bisa dihindari dengan melangkah ke samping.</summary>
            public Vector3 ChargeDir;

            /// <summary>Sisa detik fase kabur-dan-pulih (arketipe ber-RegenBelow).</summary>
            public float RegenLeft;

            /// <summary>Sisa jatah kabur-dan-pulih untuk nyawa ini.</summary>
            public int RegenUsesLeft;

            /// <summary>
            /// Ular yang memiliki ruas ini, kalau ada. Terisi = damage masuk ke kolam HP boss,
            /// ruasnya tidak pernah mati sendiri, dan gerakannya disetir boss bukan oleh AI swarm.
            /// </summary>
            public BossSnake Boss;

            /// <summary>Urutan ruas dari kepala. 0 = kepala.</summary>
            public int BossIndex;

            public readonly StatusSlot[] Slots = NewSlots();

            static StatusSlot[] NewSlots()
            {
                var slots = new StatusSlot[StatusSlots];
                for (int i = 0; i < StatusSlots; i++) slots[i].Def = -1;
                return slots;
            }
        }

        public int Kills { get; private set; }
        public int AliveCount { get; private set; }
        public float Elapsed { get; private set; }
        public bool Running = true;

        public int Wave { get; private set; }
        public bool WaveActive { get; private set; }

        /// <summary>Enemies still to arrive. At zero the wave enters its closing phase.</summary>
        public int PendingSpawns { get; private set; }

        public int WaveTotal { get; private set; }

        /// <summary>
        /// Spawning is over and the wave is waiting for the field to clear.
        ///
        /// This is the whole reason the wave can end on "kill everything" without the dead tail it
        /// used to have: the moment it flips, the survivors charge. There is nothing left to chase
        /// across the map, because everything left is running at you.
        /// </summary>
        public bool Closing { get; private set; }

        public System.Action OnWaveCleared;
        public System.Action<Vector3> OnKill;

        /// <summary>Raised for each enemy removed by the end-of-wave sweep. Pays no reward.</summary>

        /// <summary>Raised with (position, reaction) so the UI can flash and shout its name.</summary>
        public System.Action<Vector3, ReactionDefinition> OnReaction;

        /// <summary>Raised after points land on an enemy, so trigger skills can check thresholds.</summary>
        public System.Action<Enemy, int, int> OnStatusApplied;

        /// <summary>Raised with (sumber, damage) untuk damage meter.</summary>
        public System.Action<string, float> OnDamage;

        /// <summary>
        /// Raised with (posisi, damage, HP maksimum musuh) untuk angka damage yang melayang.
        /// Max HP ikut dikirim karena besar-kecilnya angka di layar diukur dari porsi HP musuh,
        /// bukan dari angka mentahnya — 40 damage di wave 2 dan di wave 20 artinya beda jauh.
        /// </summary>
        /// <summary>
        /// (posisi, jumlah, maxHp, warna sumber). Warna diambil dari def skill/status yang
        /// melukai — popup damage tampil dengan warna skillnya ("biar warna warni",
        /// 2026-08-12), bukan satu gradasi seragam.
        /// </summary>
        public System.Action<Vector3, float, float, Color, bool> OnEnemyDamaged;

        /// <summary>
        /// HIT LANGSUNG saja — sengaja bukan <see cref="OnEnemyDamaged"/>, yang juga menyala
        /// untuk tiap tick DoT. VFX di tiap tick burn pada 500 musuh bukan umpan balik,
        /// itu kabut. (pos, nama sumber, tint skill, crit) — nama ikut supaya pendengar bisa
        /// mencari HitVfx milik skill-nya sendiri.
        /// </summary>
        public System.Action<Vector3, string, Color, bool> OnEnemyHit;

        /// <summary>
        /// Peta DisplayName -> warna, dibangun SEKALI dari database saat pertama dibutuhkan.
        /// Kunci pakai nama karena seluruh jalur damage sudah membawa sourceName — menjahit
        /// Color ke belasan call-site cuma menduplikasi informasi yang sudah lewat sini.
        /// </summary>
        Dictionary<string, Color> _sourceTints;

        static readonly Color PlainHitTint = new Color(1f, 0.96f, 0.86f);

        Color TintFor(string sourceName)
        {
            if (string.IsNullOrEmpty(sourceName)) return PlainHitTint;

            if (_sourceTints == null)
            {
                _sourceTints = new Dictionary<string, Color>(96);
                if (_db != null)
                {
                    for (int i = 0; i < _db.Pieces.Count; i++)
                    {
                        var p = _db.Pieces[i];
                        if (p != null && !string.IsNullOrEmpty(p.DisplayName))
                            _sourceTints[p.DisplayName] = p.Color;
                    }

                    for (int i = 0; i < _db.Statuses.Count; i++)
                    {
                        var s = _db.Statuses[i];
                        if (s != null && !string.IsNullOrEmpty(s.DisplayName) &&
                            !_sourceTints.ContainsKey(s.DisplayName))
                            _sourceTints[s.DisplayName] = s.Color;
                    }
                }
            }

            Color tint;
            return _sourceTints.TryGetValue(sourceName, out tint) ? tint : PlainHitTint;
        }

        public int Capacity => _balance != null ? _balance.MaxAliveEnemies : 200;

        /// <summary>How many living enemies currently carry each status, by database index.</summary>
        public int[] StatusCounts => _statusCounts;

        /// <summary>
        /// Seluruh slot kolam, hidup maupun mati. Baca-saja, dan pembacanya WAJIB memeriksa
        /// <see cref="Enemy.Alive"/> sendiri: slot dipakai ulang, jadi yang mati masih memegang
        /// posisi dan HP terakhirnya sampai ada yang lahir menempatinya.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<Enemy> All => _pool;

        readonly List<Enemy> _pool = new List<Enemy>(256);
        readonly Color _baseColor = new Color(0.55f, 0.5f, 0.62f);

        Transform _player;
        PlayerCaster _caster;
        GameBalance _balance;
        ContentDatabase _db;
        /// <summary>
        /// Satu per model yang dipakai di lapangan. Indeks 0 selalu model bawaan gerombolan.
        /// </summary>
        EnemyRenderer[] _renderers;

        readonly List<VatClipSet> _models = new List<VatClipSet>();
        readonly Dictionary<EnemyArchetype, int> _modelOf = new Dictionary<EnemyArchetype, int>();

        EnemyRenderer _shotRenderer;

        /// <summary>
        /// Penggambar per boss, dibuat saat pertama dibutuhkan. Entri yang bernilai null berarti
        /// def itu SUDAH diperiksa dan memang belum diberi mesh — disimpan justru supaya
        /// pemeriksaannya tidak diulang setiap frame untuk setiap ruas.
        /// </summary>
        readonly Dictionary<BossDefinition, BossVisual> _bossVisuals =
            new Dictionary<BossDefinition, BossVisual>();

        /// <summary>
        /// Instans VFX api yang sedang menyala, satu per naga.
        ///
        /// Dipegang di SINI, bukan di BossSnake, karena BossSnake sengaja tidak tahu apa-apa soal
        /// Unity di luar matematika posisinya — ia tidak memegang Transform, tidak memuat prefab,
        /// dan itu yang membuatnya bisa diuji tanpa scene. Yang tahu cara menyalakan sesuatu di
        /// dunia adalah yang memegang kolam VFX-nya.
        /// </summary>
        readonly Dictionary<BossSnake, Transform> _breathVfx = new Dictionary<BossSnake, Transform>();
        readonly Dictionary<EnemyArchetype, int> _shotTint = new Dictionary<EnemyArchetype, int>();
        readonly Dictionary<EnemyArchetype, int> _archetypeTint = new Dictionary<EnemyArchetype, int>();
        /// <summary>Saklar curang. Null di build normal, dan setiap pembacaan lewat propertinya.</summary>
        public DebugConfig Cheats;

        // ------------------------------------------------------------------
        //  modifier wave berikutnya — dipasang RunDirector (node peta: elite / boss puncak)
        // ------------------------------------------------------------------
        //
        // Pola yang sama dengan DebugConfig: dikalikan di titik pakai, GameBalance tidak pernah
        // ditimpa. Direset saat wave BERES, jadi elite tidak pernah menular ke wave sesudahnya.
        float _nextHpMul = 1f;
        float _nextCountMul = 1f;
        float _nextDamageMul = 1f;
        int _nextBossCount;
        float _nextBossHpMul = 1f;
        float _nextBossAggro = 1f;
        bool _nextBossAllKinds;
        int _nextBossAct = 1;

        /// <summary>
        /// Pakta run ini. Boleh null — dan setiap pembacaan lewat empat properti di bawah, bukan
        /// lewat field-nya langsung, supaya tidak ada satu pun jalur yang lupa menjaga null-nya.
        /// </summary>
        public WorldPacts Pacts;

        float PactHpMul => Pacts != null ? Pacts.EnemyHpMul : 1f;
        float PactSpeedMul => Pacts != null ? Pacts.EnemySpeedMul : 1f;
        float PactDamageMul => Pacts != null ? Pacts.EnemyDamageMul : 1f;
        float PactCountMul => Pacts != null ? Pacts.EnemyCountMul : 1f;

        /// <summary>Pengali wave BERIKUTNYA (node elite). Berlaku sekali, reset saat wave beres.</summary>
        public void SetNextWaveMods(float hpMul, float countMul, float damageMul)
        {
            _nextHpMul = Mathf.Max(0.1f, hpMul);
            _nextCountMul = Mathf.Max(0.1f, countMul);
            _nextDamageMul = Mathf.Max(0.1f, damageMul);
        }

        /// <summary>
        /// Boss pesanan untuk wave berikutnya. <paramref name="allKinds"/> = jenis bergiliran
        /// (puncak act: ular DAN kelabang, bukan dua ular); false = jenis acak (mini-boss elite).
        /// </summary>
        public void ForceBossNode(int count, float hpMul, float aggro, bool allKinds, int act = 1)
        {
            _nextBossCount = Mathf.Max(0, count);
            _nextBossHpMul = Mathf.Max(0.1f, hpMul);
            _nextBossAggro = Mathf.Max(0.25f, aggro);
            _nextBossAllKinds = allKinds;

            // Nomor act dititip bersama pesanannya: gerbang SummitMinAct (boss pamungkas)
            // dinilai saat boss-nya menetas, dan saat itu peta sudah tidak di tangan.
            _nextBossAct = Mathf.Max(1, act);
        }

        Vector3 _lastPlayerPos;
        float _spawnRate;
        float _spawnBudget;
        int[] _statusCounts = new int[0];

        // ---------- spatial hash ----------
        //
        // Only BestCluster needed this. Measured at 154 enemies it cost 1.95 ms per call and it is
        // called on EVERY area or zone cast, because it asked "how many neighbours" by walking the
        // whole swarm once per candidate — n squared. Four area skills firing in one frame ate most
        // of a 60 fps budget on their own.
        //
        // Nearest and CrowdPressure measured at 0.007 ms and are left alone. Cheap code does not
        // need a data structure wrapped around it.

        // Cell dipersempit dari 4 ke 2 karena hash sekarang juga melayani gaya pisah antar musuh,
        // yang radiusnya cuma ~1.3 — mencari tetangga sedekat itu di dalam kotak 4x4 berarti
        // memeriksa puluhan musuh yang jelas terlalu jauh.
        const int HashSide = 64;
        const float HashCell = 2f;
        const float HashExtent = HashSide * HashCell * 0.5f;

        readonly int[] _cellHead = NewHeads();
        readonly int[] _cellCount = new int[HashSide * HashSide];
        int[] _nextInCell = new int[0];

        /// <summary>
        /// Sel kosong ditandai -1, dan itu harus benar SEJAK LAHIR, bukan sejak
        /// <see cref="RebuildHash"/> pertama.
        ///
        /// Array int di C# lahir berisi nol, dan nol adalah indeks musuh yang sah. Jadi hash yang
        /// belum pernah dibangun mengaku setiap selnya berpenghuni musuh nomor 0, lalu rantainya
        /// dibaca lewat <c>_nextInCell</c> yang panjangnya masih nol — IndexOutOfRange tiap frame,
        /// di dalam Update, jadi seluruh gerakan swarm mati diam-diam tanpa satu pun musuh hilang.
        ///
        /// Selama ini tidak pernah meletus cuma karena wave selalu dimulai dari UI, beberapa frame
        /// setelah Update pertama. Begitu ada yang memanggil StartWave lebih awal, ia meletus.
        /// </summary>
        static int[] NewHeads()
        {
            var heads = new int[HashSide * HashSide];
            for (int i = 0; i < heads.Length; i++) heads[i] = -1;
            return heads;
        }

        /// <summary>
        /// Titik acuan hash, mengikuti pemain dan dikancing ke kelipatan sel.
        ///
        /// Hash-nya cuma seluas 128x128 unit, dan dulu ia terpaku di titik nol dunia. Selama arena
        /// masih berbatas itu tidak masalah. Begitu lapangannya tak terbatas, pemain yang berjalan
        /// ke x=300 membuat SELURUH swarm terjepit ke sel tepi: gaya pisah berhenti bekerja,
        /// BestCluster menunjuk ke tempat yang salah, dan tidak ada satu pun error yang muncul.
        ///
        /// Dikancing ke kelipatan sel, bukan mengikuti pemain mulus, supaya isi sel tidak bergeser
        /// setengah sel tiap frame dan tetangga tidak berkedip masuk-keluar.
        /// </summary>
        Vector3 _hashOrigin;

        int CellIndex(Vector3 p) => CellIndex(p.x, p.z);

        int CellIndex(float x, float z)
        {
            int cx = Mathf.Clamp((int)((x - _hashOrigin.x + HashExtent) / HashCell), 0, HashSide - 1);
            int cz = Mathf.Clamp((int)((z - _hashOrigin.z + HashExtent) / HashCell), 0, HashSide - 1);
            return cz * HashSide + cx;
        }

        /// <summary>
        /// Rebuilt once at the end of the movement pass, so it is at most one frame stale for
        /// anything that queries it from another component. An enemy moves about 0.03 units in a
        /// frame; nothing here can tell the difference.
        /// </summary>
        void RebuildHash()
        {
            // Dipindahkan ke sekitar pemain SEBELUM apa pun dimasukkan, jadi tiap frame hash-nya
            // memang meliputi tempat yang sedang dimainkan.
            if (_player != null)
            {
                _hashOrigin = new Vector3(
                    Mathf.Round(_player.position.x / HashCell) * HashCell, 0f,
                    Mathf.Round(_player.position.z / HashCell) * HashCell);
            }

            if (_nextInCell.Length < _pool.Count)
            {
                System.Array.Resize(ref _nextInCell, Mathf.Max(64, _pool.Count * 2));
            }

            for (int i = 0; i < _cellHead.Length; i++)
            {
                _cellHead[i] = -1;
                _cellCount[i] = 0;
            }

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                int cell = CellIndex(e.Pos);
                _nextInCell[i] = _cellHead[cell];
                _cellHead[cell] = i;
                _cellCount[cell]++;
            }
        }

        /// <summary>Most neighbours one enemy will push against. A pile-up must not cost O(n).</summary>
        const int MaxSeparationChecks = 14;

        /// <summary>
        /// How hard this enemy is being crowded, and from which side.
        ///
        /// This is the single fix for the conga line. Without it every enemy walks the identical
        /// path to the identical point and they stack into a queue behind the player; with it they
        /// cannot occupy the same ground, so a chasing pack spreads sideways and closes as a ring
        /// instead of a tail. The encirclement is emergent — nothing tells them to surround anyone.
        /// </summary>
        Vector3 Separation(Enemy self, Vector3 pos, float radius)
        {
            int cx = Mathf.Clamp((int)((pos.x - _hashOrigin.x + HashExtent) / HashCell), 0, HashSide - 1);
            int cz = Mathf.Clamp((int)((pos.z - _hashOrigin.z + HashExtent) / HashCell), 0, HashSide - 1);

            int x0 = Mathf.Max(0, cx - 1), x1 = Mathf.Min(HashSide - 1, cx + 1);
            int z0 = Mathf.Max(0, cz - 1), z1 = Mathf.Min(HashSide - 1, cz + 1);

            Vector3 push = Vector3.zero;
            float sqrRadius = radius * radius;
            int checks = 0;

            for (int gz = z0; gz <= z1; gz++)
            {
                for (int gx = x0; gx <= x1; gx++)
                {
                    for (int i = _cellHead[gz * HashSide + gx]; i >= 0; i = _nextInCell[i])
                    {
                        var other = _pool[i];
                        if (other == self || !other.Alive) continue;

                        Vector3 d = pos - other.Pos;
                        d.y = 0f;

                        float sqr = d.sqrMagnitude;
                        if (sqr > sqrRadius) continue;

                        // Exactly co-located: shove apart along a fixed axis rather than dividing
                        // by zero and freezing them together forever.
                        if (sqr < 0.0001f)
                        {
                            push.x += 0.5f;
                            continue;
                        }

                        float distance = Mathf.Sqrt(sqr);
                        push += d / distance * (1f - distance / radius);

                        if (++checks >= MaxSeparationChecks) return Clamped(push);
                    }
                }
            }

            return Clamped(push);
        }

        /// <summary>
        /// Capped at unit length. Raw, the sum of fourteen neighbours can reach a magnitude of ten
        /// or more, which swamps every other steering term it is added to — a shooter trying to
        /// hold its range would simply be shoved wherever the crowd was thinnest, including
        /// straight into melee.
        /// </summary>
        static Vector3 Clamped(Vector3 push) =>
            push.sqrMagnitude > 1f ? push.normalized : push;

        /// <summary>Any living enemy sitting in this cell and still inside the caster's reach.</summary>
        Enemy PickFromCell(int cell, Vector3 from, float sqrMaxDistance)
        {
            for (int i = _cellHead[cell]; i >= 0; i = _nextInCell[i])
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                Vector3 d = e.Pos - from;
                d.y = 0f;
                if (d.sqrMagnitude <= sqrMaxDistance) return e;
            }

            return null;
        }

        /// <summary>
        /// Animasi panggangan untuk gerombolan. Dipasang SEBELUM <see cref="Init"/>, seperti
        /// <see cref="Cheats"/> — renderernya dibangun di sana dan tidak pernah dibangun ulang.
        ///
        /// Null = kapsul seperti dulu. Itu bukan jalur usang melainkan jaring: panggangan yang
        /// gagal, hilang, atau belum dibuat harus menyisakan gerombolan yang tetap bisa
        /// dimainkan, bukan lapangan kosong tanpa satu pun musuh terlihat.
        /// </summary>
        public VatClipSet Vat;

        /// <summary>
        /// Gerombolan menjatuhkan bayangan. Dipasang sebelum <see cref="Init"/>.
        ///
        /// Tanpa bayangan, sosok di kamera yang menunduk kehilangan satu-satunya petunjuk bahwa
        /// ia MENYENTUH tanah — gerombolan terbaca melayang beberapa senti di atas rumput. Itu
        /// tidak terlihat waktu musuhnya kapsul, karena kapsul tidak pernah terbaca sebagai
        /// sesuatu yang berdiri.
        ///
        /// Harganya: pass bayangan menggambar seluruh gerombolan sekali lagi.
        /// </summary>
        public bool EnemyShadows = true;

        /// <summary>
        /// Tinggi musuh biasa dalam unit dunia. Model apa pun yang masuk diskalakan untuk
        /// MENYAMAI tinggi ini, bukan dibiarkan membawa tingginya sendiri — mengganti model
        /// dengan yang lebih jangkung tidak boleh diam-diam mengubah seberapa besar musuh terasa.
        ///
        /// Digandakan dari 1,1 (tinggi kapsul lama) jadi 2,2 atas permintaan pemilik project:
        /// kerangka setinggi kapsul terbaca kekecilan, karena kapsul mengisi seluruh siluetnya
        /// sementara sosok manusia menghabiskan sebagian tingginya untuk celah antar anggota
        /// badan. Dua benda setinggi sama tidak terbaca sebesar sama.
        ///
        /// Ini murni UKURAN GAMBAR. Jangkauan gigit dan tabrakan tidak diturunkan dari sini,
        /// jadi keduanya tetap seperti dulu.
        /// </summary>
        const float BodyHeight = 2.2f;

        /// <summary>
        /// Setengah lebar badan musuh berukuran normal, satuan dunia.
        ///
        /// Angka ini ada karena selama ini tidak ada. Setiap uji kena — peluru, sinar, ledakan —
        /// memperlakukan musuh sebagai TITIK tanpa ukuran, lalu memakai satu radius datar yang
        /// dikarang di tempat pemanggilan. Peluru memakai 0,75 untuk semuanya. Padahal musuh
        /// digambar sebesar <see cref="Enemy.Scale"/>: stalker 0,75 · grunt 1 · spitter 1,1 ·
        /// cursed 1,45 · dan kepala boss berkali-kali lipat itu. Akibatnya yang dilaporkan pemilik
        /// project: "musuh dekat tapi tidak kena damage" — badannya memenuhi layar, tapi yang
        /// diuji cuma satu titik di tengahnya.
        ///
        /// Dipilih 0,45 supaya musuh baku TIDAK berubah rasanya: peluru dulu memakai satu angka
        /// 0,75 yang mencampur ukuran peluru dan ukuran musuh jadi satu; sekarang keduanya dipisah
        /// jadi 0,3 (peluru) + 0,45 (badan grunt) = 0,75 yang sama persis. Yang berubah hanya
        /// musuh yang memang tidak seukuran grunt — dan itu justru yang dibetulkan.
        /// </summary>
        public const float BodyRadius = 0.45f;

        /// <summary>
        /// Radius kena satu musuh, ikut ukuran badan yang benar-benar digambar.
        /// SATU sumber untuk semua uji kena, supaya gambar dan tabrakan tidak bisa lagi berpisah.
        /// </summary>
        public static float HitRadius(Enemy e) =>
            e == null ? BodyRadius : BodyRadius * Mathf.Max(0.1f, e.Scale);

        /// <summary>
        /// Kecepatan yang dianggap lari penuh, dipakai memilih antara animasi jalan dan lari.
        /// </summary>
        const float RunSpeed = 3.2f;

        public void Init(Transform player, PlayerCaster caster, GameBalance balance, ContentDatabase database)
        {
            _player = player;
            _caster = caster;
            _balance = balance;
            _db = database;
            _statusCounts = new int[Mathf.Max(1, _db.Statuses.Count)];

            BuildSwarmRenderers();

            _shotRenderer = new EnemyRenderer(EnemyRenderer.BorrowPrimitiveMesh(PrimitiveType.Sphere),
                BuildShotPalette(), MaxShots, 0.3f, false);
        }

        /// <summary>
        /// Satu renderer per MODEL yang benar-benar dipakai — bukan per archetype, dan bukan satu
        /// untuk semuanya.
        ///
        /// Sebelum ini gerombolan memegang satu <see cref="VatClipSet"/>, jadi Grunt, Cursed,
        /// Stalker, dan Spitter semuanya tergambar sebagai kerangka yang sama. Modelnya sudah
        /// dipanggang sejak lama; yang belum ada cuma jalan buat memakainya.
        ///
        /// Dikelompokkan per model, bukan per archetype, karena dua archetype yang memakai model
        /// sama tidak punya alasan jadi dua panggilan gambar. Yang menentukan biaya jumlah MODEL
        /// yang berbeda di lapangan, dan itu selalu kecil.
        ///
        /// Model bawaan selalu jadi indeks 0 supaya archetype yang slotnya kosong — termasuk boss
        /// dan apa pun yang lahir tanpa archetype — punya tempat jatuh yang pasti.
        /// </summary>
        void BuildSwarmRenderers()
        {
            _models.Clear();
            _modelOf.Clear();

            _models.Add(Vat);

            for (int i = 0; i < _db.Archetypes.Count; i++)
            {
                var kind = _db.Archetypes[i];
                if (kind == null) continue;

                // Slot kosong ikut model bawaan. Itu perilaku sebelum slot ini ada, dan tetap
                // benar: archetype tanpa model sendiri harus tetap terlihat.
                if (kind.Vat == null) { _modelOf[kind] = 0; continue; }

                int found = _models.IndexOf(kind.Vat);
                if (found < 0) { _models.Add(kind.Vat); found = _models.Count - 1; }

                _modelOf[kind] = found;
            }

            _renderers = new EnemyRenderer[_models.Count];

            for (int i = 0; i < _models.Count; i++)
            {
                var vat = _models[i];
                bool baked = vat != null && vat.Mesh != null && vat.Positions != null;

                // Skala diturunkan dari tinggi model yang sebenarnya, bukan dikira-kira di
                // inspector — dan dihitung PER MODEL, karena tujuh model punya tujuh tinggi.
                // Satu angka untuk semuanya akan membuat yang jangkung menjulang dan yang pendek
                // tenggelam, padahal keduanya musuh biasa.
                //
                // Kapsul cadangan ikut digandakan. Mesh kapsul bawaan tingginya 2 unit, jadi
                // pengalinya setengah dari tinggi yang diminta.
                float body = baked && vat.Height > 0.01f
                    ? BodyHeight / vat.Height
                    : BodyHeight * 0.5f;

                // Bayangan cuma untuk model panggangan. Kapsul yang menjatuhkan bayangan kapsul
                // tidak menambah apa pun selain biaya — yang dibayar mahal itu justru siluetnya.
                _renderers[i] = new EnemyRenderer(
                    EnemyRenderer.BorrowPrimitiveMesh(PrimitiveType.Capsule),
                    BuildPalette(), Capacity, body, true, vat, EnemyShadows && baked,
                    null, vat != null ? vat.MeshRotation : Vector3.zero);
            }
        }

        /// <summary>
        /// Renderer milik archetype ini. Yang tidak dikenal — termasuk musuh tanpa archetype sama
        /// sekali — jatuh ke model bawaan, bukan ke pengecualian.
        /// </summary>
        int ModelOf(EnemyArchetype kind)
        {
            int index;
            return kind != null && _modelOf.TryGetValue(kind, out index) ? index : 0;
        }

        /// <summary>
        /// Penggambar milik boss ini, dibangun saat pertama dibutuhkan.
        ///
        /// Dibangun malas, bukan di <see cref="Bind"/>, karena boss yang tidak pernah muncul di
        /// satu run tidak boleh membayar tiga renderer beserta materialnya. Null yang dikembalikan
        /// ikut disimpan: itu jawaban "def ini memang tanpa mesh", bukan "belum sempat dicoba".
        /// </summary>
        BossVisual VisualFor(BossDefinition def)
        {
            if (def == null) return null;

            if (!_bossVisuals.TryGetValue(def, out var visual))
            {
                // Paletnya dibangun DULU, slot tintnya dibaca SESUDAH. BuildPalette sendiri yang
                // menetapkan _bossHeadTint dan _bossBodyTint, jadi membaca keduanya di baris yang
                // sama akan bergantung pada urutan evaluasi argumen — kebetulan benar di C#, tapi
                // tidak terbaca benar oleh siapa pun yang mengeditnya nanti.
                var palette = BuildPalette();

                visual = BossVisual.TryBuild(def, palette, Capacity, _bossHeadTint, _bossBodyTint);
                _bossVisuals[def] = visual;
            }

            return visual;
        }

        /// <summary>One shot colour per shooting archetype, so different shooters read apart.</summary>
        Color[] BuildShotPalette()
        {
            _shotTint.Clear();

            var colors = new List<Color> { new Color(0.7f, 1f, 0.5f) };

            for (int i = 0; i < _db.Archetypes.Count; i++)
            {
                var kind = _db.Archetypes[i];
                if (kind == null || !kind.Shoots) continue;

                _shotTint[kind] = colors.Count;
                colors.Add(kind.ShotColor);
            }

            return colors.ToArray();
        }

        /// <summary>
        /// Every look an enemy can have, flattened into a list the renderer can batch by.
        ///
        /// Slot 0 is plain. Then one entry per ailment, then the same ailments again washed toward
        /// white — that second half is the "two or more at once" tell, the warning that a reaction
        /// is one application away. Fifteen entries for seven ailments, so at most fifteen draw
        /// calls no matter how many enemies are alive.
        /// </summary>
        Color[] BuildPalette()
        {
            int statuses = _db.Statuses.Count;
            var palette = new List<Color>(1 + statuses * 2 + 8) { _baseColor };

            for (int i = 0; i < statuses; i++)
            {
                var status = _db.Statuses[i];
                palette.Add(status != null ? status.Color : _baseColor);
            }

            for (int i = 0; i < statuses; i++)
            {
                palette.Add(Color.Lerp(palette[1 + i], Color.white, 0.55f));
            }

            // Archetype colours go last, and are only reached when the enemy carries no ailment.
            _archetypeTint.Clear();

            for (int i = 0; i < _db.Archetypes.Count; i++)
            {
                var kind = _db.Archetypes[i];
                if (kind == null || !kind.UseTint) continue;

                _archetypeTint[kind] = palette.Count;
                palette.Add(kind.Tint);
            }

            // Dua slot terakhir milik boss: kepala dan badan. Kepala dibedakan warnanya karena
            // ITU satu-satunya bagian yang menggigit — pemain harus bisa menemukannya dalam
            // sekejap di antara dua puluh ruas yang bentuknya sama persis.
            _bossHeadTint = palette.Count;
            palette.Add(_db.Boss != null ? _db.Boss.HeadColor : new Color(0.85f, 0.25f, 0.35f));

            _bossBodyTint = palette.Count;
            palette.Add(_db.Boss != null ? _db.Boss.BodyColor : new Color(0.45f, 0.18f, 0.3f));

            return palette.ToArray();
        }

        int _bossHeadTint;
        int _bossBodyTint;

        /// <summary>
        /// Ular yang sedang hidup. Jamak, karena wave tinggi memunculkan lebih dari satu.
        ///
        /// Bertambahnya jumlah, bukan bertambahnya HP, yang membuat boss tetap terasa boss di
        /// wave 40: satu ular ber-HP sepuluh kali lipat cuma jadi tembok yang lebih lama dipukul,
        /// sementara tiga ular yang mengitari dari tiga arah adalah masalah yang benar-benar baru.
        /// </summary>
        readonly List<BossSnake> _bosses = new List<BossSnake>();

        public IReadOnlyList<BossSnake> Bosses => _bosses;

        /// <summary>Ular pertama yang masih hidup, untuk UI. Null kalau tidak ada.</summary>
        public BossSnake Boss
        {
            get
            {
                for (int i = 0; i < _bosses.Count; i++)
                {
                    if (_bosses[i].Alive && !_bosses[i].Def.Minion) return _bosses[i];
                }

                return null;
            }
        }

        public bool BossActive
        {
            get
            {
                for (int i = 0; i < _bosses.Count; i++)
                {
                    if (_bosses[i].Alive && !_bosses[i].Def.Minion) return true;
                }

                return false;
            }
        }

        public void StartWave(int wave)
        {
            Wave = wave;

            // A wave is a COUNT. A clock made the player watch a countdown instead of the field,
            // and "how many are left" is the only number they can actually act on. The rate below
            // still exists, but it only decides how fast that count arrives.
            WaveTotal = Mathf.Max(1, Mathf.RoundToInt(
                _balance.EnemyCountFor(wave) * (Cheats != null ? Cheats.EnemyCountScale : 1f)
                * _nextCountMul * PactCountMul));
            PendingSpawns = WaveTotal;
            Closing = false;

            _spawnRate = _balance.SpawnRateFor(wave);
            _spawnBudget = 0f;

            WaveActive = true;

            // A handful already on their way in, so the wave starts as an event rather than as a
            // quiet stretch of empty floor while the first walkers cross the map.
            int opener = Mathf.Min(_balance.WaveOpenerCount, PendingSpawns);
            for (int i = 0; i < opener; i++)
            {
                if (SpawnOne(_balance.WaveOpenerDistance)) PendingSpawns--;
            }

            if (_nextBossCount > 0) SpawnBossNode(wave);
            else if (_balance.BossEveryWaves > 0 && wave % _balance.BossEveryWaves == 0) SpawnBoss(wave);

            SpawnMinions(wave);

            OnWaveStarted?.Invoke(wave);
        }

        void Update()
        {
            if (!Running) return;

            float dt = Time.deltaTime;
            if (WaveActive) Elapsed += dt;

            if (WaveActive && !Closing && PendingSpawns <= 0) Closing = true;

            // Dijalankan SEBELUM TickEnemies supaya posisi ruas yang dipakai untuk damage dan
            // pengecekan jarak adalah posisi frame ini, bukan sisa frame kemarin.
            TickBoss(dt);

            TickSpawning(dt);
            TickEnemies(dt);

            if (!WaveActive || !Closing) return;

            // Wave tidak boleh dinyatakan beres selama ularnya masih hidup. Tanpa penjaga ini,
            // menghabiskan gerombolan biasa akan menutup wave sementara boss masih berkeliling —
            // dan papan terbuka kembali di tengah pertarungan yang belum selesai.
            if (AliveCount == 0 && !BossActive)
            {
                FinishWave();
                return;
            }

            // Wave berakhir HANYA kalau lapangannya benar-benar bersih. Tidak ada jam.
            //
            // Dulu di sini ada katup pengaman: setelah `ClosingTimeout` detik, sisa musuh dihapus
            // dan wave dinyatakan beres. Alasannya nyata — build tanpa damage sama sekali tidak
            // akan pernah bisa membersihkan lapangan — tapi harganya jauh lebih mahal dari
            // masalah yang dipecahkannya: pemain yang sedang menang tiba-tiba kehilangan sisa
            // musuh yang sudah susah payah dikejar, dan kemenangan itu berhenti terasa miliknya.
            //
            // Build tanpa damage sekarang memang menggantung, dan itu jawaban yang benar: buku
            // yang tidak bisa membunuh apa pun adalah build yang kalah, bukan keadaan yang perlu
            // diselamatkan diam-diam oleh jam.
        }

        // ---------- boss ----------

        /// <summary>Raised dengan nomor wave, tepat saat wave itu dibuka.</summary>
        public System.Action<int> OnWaveStarted;

        /// <summary>Raised sekali saat ular muncul, dan sekali saat mati.</summary>
        public System.Action<BossSnake> OnBossSpawned;

        public System.Action<Vector3> OnBossDied;

        /// <summary>
        /// Berapa ular yang datang di wave ini. Bertambah satu tiap empat kali kemunculan boss,
        /// jadi wave 5 dapat satu dan wave 40 dapat dua — naik pelan, karena tiap ular tambahan
        /// menggandakan tekanannya, bukan menambahnya.
        /// </summary>
        int BossCountFor(int wave)
        {
            int every = Mathf.Max(1, _balance.BossEveryWaves);
            return Mathf.Clamp(1 + wave / (every * 4), 1, 4);
        }

        /// <summary>Boss sungguhan: satu jenis acak dari yang bukan anak buah.</summary>
        BossDefinition RollBossKind()
        {
            var kinds = _db.BossKinds;
            BossDefinition picked = null;
            int seen = 0;

            for (int i = 0; i < kinds.Count; i++)
            {
                if (kinds[i] == null || kinds[i].Minion) continue;

                // Boss pamungkas tidak pernah ikut undian kelipatan-wave — ia hanya
                // menjaga puncak act yang memenuhi SummitMinAct-nya.
                if (kinds[i].SummitMinAct > 1) continue;

                // Reservoir sampling: satu lintasan, tanpa daftar sementara.
                seen++;
                if (Random.Range(0, seen) == 0) picked = kinds[i];
            }

            return picked;
        }

        /// <summary>
        /// Anak buah: kelabang kecil yang ikut wave biasa. Memakai seluruh sistem boss apa adanya,
        /// jadi mereka menyelam dan menyembur persis seperti yang besar — cuma lebih kecil, lebih
        /// rapuh, dan tidak diumumkan.
        /// </summary>
        void SpawnMinions(int wave)
        {
            var kinds = _db.BossKinds;

            for (int k = 0; k < kinds.Count; k++)
            {
                var def = kinds[k];
                if (def == null || !def.Minion || wave < def.MinionFromWave) continue;

                int count = def.MinionCount + Mathf.Max(0, wave - def.MinionFromWave) / 6;
                count = Mathf.Clamp(count, 1, 8);

                for (int i = 0; i < count; i++) Hatch(def, wave);
            }
        }

        void Hatch(BossDefinition def, int wave, float hpMul = 1f, float aggro = 1f)
        {
            var snake = new BossSnake();

            Vector3 at = SpawnPoint(1f);
            at.y = 0.9f;

            // Pakta ikut menebalkan BOSS, bukan cuma gerombolan. Kalau tidak, pakta "musuh lebih
            // tebal" justru membuat boss terasa lebih lemah relatif terhadap build yang sudah
            // dibayar untuk melawannya — dan puncak act adalah tempat harga sebuah pakta paling
            // harus terasa.
            snake.Begin(def, at, _balance.EnemyHpFor(wave) * def.HpMultiplier * hpMul * PactHpMul);
            snake.Aggro = Mathf.Max(0.25f, aggro);
            snake.OnSpit += (from, dir) => SpitShot(snake, from, dir, wave);

            // Tiap titik jatuh api meninggalkan noda. Penjaga jarak di dalam Drop yang
            // mengubah hujan panggilan per-frame ini jadi barisan noda berjarak rapi.
            snake.OnScorch += at =>
            {
                if (_scorch == null) _scorch = new ScorchMarks();
                _scorch.Drop(at);
            };

            _bosses.Add(snake);
            SyncBossSegments(snake);

            if (!def.Minion) OnBossSpawned?.Invoke(snake);
        }

        void SpawnBoss(int wave)
        {
            var def = RollBossKind();
            if (def == null) return;

            int count = BossCountFor(wave);

            for (int i = 0; i < count; i++)
            {
                Hatch(def, wave);
            }
        }

        /// <summary>
        /// Boss pesanan peta run — node puncak act atau elite ber-mini-boss. Jumlah, nyawa, dan
        /// agresinya dititip lewat <see cref="ForceBossNode"/>; jalur boss kelipatan-wave yang
        /// lama dilewati supaya pesanan tidak dobel.
        /// </summary>
        void SpawnBossNode(int wave)
        {
            var kinds = new List<BossDefinition>();
            var all = _db.BossKinds;

            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (def == null || def.Minion) continue;

                // Boss pamungkas (SummitMinAct > 1) tidak pernah jadi mini-boss elite
                // (jalur allKinds=false), dan di puncak act pun baru boleh sejak act-nya
                // tercapai — "jangan sampai gw ketemu naga lagi, dia boss taro di end"
                // (pemilik project, setelah bertemu naga di act awal).
                if (def.SummitMinAct > 1 &&
                    (!_nextBossAllKinds || _nextBossAct < def.SummitMinAct)) continue;

                kinds.Add(def);
            }

            if (kinds.Count == 0) return;

            // Pamungkas tampil PALING DEPAN di giliran jenis: di puncak yang memenuhi
            // syaratnya, kepala rombongan adalah naga — kepastian, bukan hasil undian.
            if (_nextBossAllKinds) kinds.Sort((a, b) => b.SummitMinAct.CompareTo(a.SummitMinAct));

            for (int i = 0; i < _nextBossCount; i++)
            {
                var def = _nextBossAllKinds
                    ? kinds[i % kinds.Count]
                    : kinds[Random.Range(0, kinds.Count)];

                Hatch(def, wave, _nextBossHpMul, _nextBossAggro);
            }

            // Tiap boss besar membawa grub pengawalnya sendiri, di atas jatah grub wave biasa.
            // Bar HP raksasa tidak memaksa pemain bergerak; gigitan-gigitan kecil dari bawah
            // tanah yang memaksa. Di-hatch polos — tanpa pengali nyawa/agresi node — karena
            // tugas mereka mengganggu, bukan menghadang.
            int grubs = Mathf.Max(0, _balance.BossNodeGrubsPerBoss) * _nextBossCount;
            if (grubs > 0)
            {
                var minions = new List<BossDefinition>();

                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null && all[i].Minion) minions.Add(all[i]);
                }

                for (int i = 0; minions.Count > 0 && i < grubs; i++)
                {
                    Hatch(minions[i % minions.Count], wave);
                }
            }
        }

        void TickBoss(float dt)
        {
            for (int i = _bosses.Count - 1; i >= 0; i--)
            {
                var snake = _bosses[i];

                if (!snake.Alive)
                {
                    // Apinya dimatikan SEBELUM ia dilepas dari daftar. Sesudahnya tidak ada lagi
                    // yang memanggil BreathVfx untuknya, dan instans yang tertinggal menyala akan
                    // menggantung di udara kosong sampai run berakhir.
                    BreathVfx(snake);

                    RetireBoss(snake);
                    _bosses.RemoveAt(i);
                    continue;
                }

                snake.Tick(dt, _player.position, damage =>
                {
                    _caster.TakeHit(damage);
                    if (snake.Def.Curse != null) _caster.ApplyDebuff(snake.Def.Curse);
                });

                BreathVfx(snake);
                SyncBossSegments(snake);
            }
        }

        /// <summary>
        /// Menyalakan, menggeser, dan mematikan api yang menempel di mulut naga.
        ///
        /// Instansnya DIIKUTKAN tiap frame alih-alih diparenting ke sesuatu, dan itu bukan pilihan
        /// bebas: boss di sini tidak punya Transform sama sekali — ia cuma posisi di dalam array —
        /// jadi tidak ada apa pun yang bisa dijadikan induk.
        ///
        /// Kolamnya DIPINJAM dari PlayerCaster. Kolam kedua khusus musuh berarti dua tumpukan
        /// prefab, dua Tick, dan dua tempat yang harus ingat mematikan instansnya saat run
        /// berakhir — dan yang ketiga itulah yang biasanya terlupakan.
        /// </summary>
        /// <summary>
        /// Titik moncong dan arah kepala naga PADA POSE YANG SEDANG TERGAMBAR, dibaca langsung
        /// dari tekstur panggangan.
        ///
        /// Naga VAT tidak punya tulang saat main — kerangkanya tinggal di editor, animasinya
        /// sudah pindah ke tekstur. Jadi "tempelkan api di tulang kepala" harus dijawab dengan
        /// membaca tekstur itu: kolom = vertex moncong, baris = frame yang sedang diputar, dengan
        /// interpolasi dan pembungkusan YANG SAMA dengan shadernya. Selisih rumus sekecil apa pun
        /// di sini terlihat sebagai api yang tertinggal dari kepala yang menyentak.
        ///
        /// Arahnya dari leher ke moncong — dua kumpulan vertex, bukan satu — supaya apinya ikut
        /// MENUNDUK saat kepalanya menukik di tengah klip serangan. Heading datar tidak tahu
        /// apa-apa soal itu.
        /// </summary>
        bool TryHeadPose(BossSnake boss, out Vector3 snoutWorld, out Vector3 dirWorld)
        {
            snoutWorld = default; dirWorld = default;

            var def = boss.Def;
            var vat = def.Vat;
            if (vat == null || vat.Positions == null || vat.Mesh == null) return false;
            if (!vat.Positions.isReadable) return false;

            int[] probe;
            if (!_headProbe.TryGetValue(def, out probe))
            {
                // Sekali per def: vertex diurutkan dari yang paling depan (+Z pose netral).
                // 12 terdepan = ujung moncong; peringkat 200..260 = tengah kepala/leher.
                var verts = vat.Mesh.vertices;
                var order = new int[verts.Length];
                for (int i = 0; i < order.Length; i++) order[i] = i;
                System.Array.Sort(order, (a, b) => verts[b].z.CompareTo(verts[a].z));

                int neckFrom = Mathf.Min(200, order.Length / 3);
                int neckTo = Mathf.Min(260, order.Length / 2);

                probe = new int[12 + (neckTo - neckFrom)];
                for (int i = 0; i < 12; i++) probe[i] = order[i];
                for (int i = neckFrom; i < neckTo; i++) probe[12 + i - neckFrom] = order[i];

                _headProbe[def] = probe;
            }

            // Klip yang sedang diputar: selama menyembur Speed01 dipatok ke AttackSpeed, jadi
            // shader memilih peran Attack — dan probe ini cuma dipanggil selama menyembur.
            VatClip clip;
            if (!vat.TryGet(VatRole.Attack, out clip) || clip.Rows <= 0) return false;

            // Rumus baris DISALIN dari ClipArgs + shader: progress = time/detik (fase boss = 0),
            // f = frac * Rows, frame kedua membungkus ke awal klip — bukan menyeberang ke klip
            // sebelah.
            float seconds = clip.Seconds > 0.01f ? clip.Seconds : 1f;
            float f = Mathf.Repeat(Time.time / seconds, 1f) * clip.Rows;
            int f0 = Mathf.FloorToInt(f);
            int f1 = f0 + 1 >= clip.Rows ? 0 : f0 + 1;
            float blend = f - f0;

            var tex = vat.Positions;
            Vector3 snout = Vector3.zero, neck = Vector3.zero;

            for (int i = 0; i < probe.Length; i++)
            {
                int v = probe[i];
                var a = tex.GetPixel(v, clip.FirstRow + f0);
                var b = tex.GetPixel(v, clip.FirstRow + f1);
                var pos = Vector3.LerpUnclamped(
                    new Vector3(a.r, a.g, a.b), new Vector3(b.r, b.g, b.b), blend);

                if (i < 12) snout += pos; else neck += pos;
            }

            snout /= 12f;
            neck /= probe.Length - 12;

            // Transform instance yang SAMA dengan Compose: yaw dunia, kemiringan, koreksi mesh,
            // skala HeadScale-per-tinggi. Ada yang beda di salah satunya = api melayang di
            // sebelah kepala, bukan di kepala.
            var seg = boss.Segments.Count > 0 ? boss.Segments[0] : null;
            float yaw = seg != null ? seg.Yaw
                : Mathf.Atan2(boss.Heading.x, boss.Heading.z) * Mathf.Rad2Deg;

            var facing = Quaternion.Euler(0f, yaw, 0f)
                       * Quaternion.Euler(0f, 0f, boss.BankDegrees)
                       * Quaternion.Euler(def.HeadMeshRotation);

            float unit = def.HeadScale / Mathf.Max(0.0001f, vat.Height);

            snoutWorld = boss.HeadPos + facing * (snout * unit);
            var neckWorld = boss.HeadPos + facing * (neck * unit);

            dirWorld = snoutWorld - neckWorld;
            if (dirWorld.sqrMagnitude < 0.0001f) dirWorld = boss.Heading;
            dirWorld.Normalize();

            return true;
        }

        readonly Dictionary<BossDefinition, int[]> _headProbe =
            new Dictionary<BossDefinition, int[]>();

        /// <summary>Coretan gosong yang ditinggalkan semburan naga. Dibangun malas.</summary>
        ScorchMarks _scorch;

        void BreathVfx(BossSnake boss)
        {
            var def = boss.Def;
            var pool = _caster != null ? _caster.Vfx : null;

            if (pool == null || def.BreathVfx == null) return;

            Transform inst;
            bool has = _breathVfx.TryGetValue(boss, out inst);

            // Jeda dihormati DI SINI, bukan di BossSnake: yang menunda cuma penampakannya.
            // Menunda keadaan Breathing-nya sendiri akan ikut menunda animasinya, dan yang
            // tertunda animasinya berhenti terbaca sebagai naga yang sedang menarik napas.
            bool showing = boss.Alive && boss.Breathing
                        && boss.BreathElapsed >= def.BreathVfxDelay;

            if (!showing)
            {
                if (!has) return;

                pool.Release(def.BreathVfx, inst);
                _breathVfx.Remove(boss);
                return;
            }

            // Ditempelkan ke KEPALA YANG SEDANG TERGAMBAR, bukan ke titik logika.
            //
            // MuzzlePos dihitung dari heading datar dan tidak tahu apa-apa soal animasi — kepala
            // yang menarik diri lalu menukik selama klip serangan meninggalkannya di tempat, dan
            // apinya menyala di udara kosong di sebelah kepala. Probe membaca pose dari tekstur
            // panggangan, jadi apinya ikut ke mana pun kepalanya pergi. MuzzlePos tinggal jadi
            // cadangan untuk keadaan yang tak bisa dibaca (tekstur tidak readable).
            Vector3 at; Vector3 aim;

            if (!TryHeadPose(boss, out at, out aim))
            {
                at = boss.MuzzlePos;
            }

            // Arah visualnya DITARIK ke titik jatuh di tanah — titik yang sama yang dipakai
            // kerusakan dan coretan gosong. Posisi mulut tetap dari probe kepala; tapi jet
            // yang tidak berujung di tempat tanah terbakar membuat pemain menghindari api
            // yang salah.
            aim = boss.GroundImpact - at;
            if (aim.sqrMagnitude < 0.0001f) aim = boss.Heading;
            aim.Normalize();

            var rot = Quaternion.LookRotation(aim, Vector3.up)
                    * Quaternion.Euler(def.BreathVfxRotation);

            if (!has || inst == null)
            {
                inst = pool.Attach(def.BreathVfx, at, rot, def.BreathVfxScale);
                _breathVfx[boss] = inst;
                return;
            }

            inst.SetPositionAndRotation(at, rot);
        }

        /// <summary>
        /// Menyamakan jumlah dan posisi ruas dengan keadaan ular sekarang.
        ///
        /// Panjangnya diturunkan dari HP, jadi memendeknya badan BUKAN efek terpisah yang perlu
        /// diurus — ia jatuh sendiri dari satu perhitungan, dan mustahil melenceng dari HP aslinya.
        /// </summary>
        void SyncBossSegments(BossSnake boss)
        {
            var segments = boss.Segments;
            int wanted = boss.WantedSegments();

            while (segments.Count > wanted)
            {
                int last = segments.Count - 1;
                var gone = segments[last];

                gone.Alive = false;
                gone.Boss = null;
                segments.RemoveAt(last);

                // Ruas yang lepas tetap memercik. Badan yang menyusut diam-diam tidak terbaca
                // sebagai kemajuan; percikannya yang memberi tahu pemain bahwa ia sedang menang.
                StartBurn(gone);
                OnKill?.Invoke(gone.Pos);
            }

            while (segments.Count < wanted)
            {
                var fresh = GetFree();
                if (fresh == null) break;

                if (_nextInCell.Length < _pool.Count)
                {
                    System.Array.Resize(ref _nextInCell, Mathf.Max(64, _pool.Count * 2));
                }

                for (int i = 0; i < StatusSlots; i++) fresh.Slots[i].Def = -1;

                fresh.Kind = null;
                fresh.Boss = boss;
                fresh.Alive = true;
                fresh.Hp = 1f;
                fresh.MaxHp = boss.MaxHp;
                fresh.Speed = 0f;
                fresh.Flank = 0f;
                fresh.LiftTimer = 0f;
                fresh.Knock = Vector3.zero;
                fresh.ReactionLock = 0f;
                fresh.Phase = segments.Count * 0.4f;

                segments.Add(fresh);
            }

            var def = boss.Def;

            for (int i = 0; i < segments.Count; i++)
            {
                var e = segments[i];

                e.BossIndex = i;
                e.Pos = boss.SegmentPoint(i);
                e.GroundY = e.Pos.y;

                // Meruncing ke belakang, jadi kepala dan ekor bisa dibedakan dari siluetnya saja.
                float t = segments.Count <= 1 ? 0f : i / (float)(segments.Count - 1);
                e.Scale = i == 0 ? def.HeadScale : Mathf.Lerp(def.HeadScale * 0.72f, def.TailScale, t);

                // Kepala memakai ARAH HADAP boss, bukan selisih posisi.
                //
                // Selisihnya di ruas nol adalah HeadPos dikurangi SegmentPoint(0), dan keduanya
                // titik yang sama: jejak disisipkan di indeks 0 setiap kepala bergerak sejauh
                // TrailStep. Jadi selisihnya nyaris nol, penjaga di bawah menolaknya, dan yaw
                // kepala tidak pernah ditulis sekali pun — kepalanya menatap utara dunia seumur
                // pertarungan sementara seluruh badannya meliuk dengan benar.
                Vector3 forward = i == 0 ? boss.Heading : boss.SegmentPoint(i - 1) - e.Pos;

                if (forward.sqrMagnitude > 0.0001f) e.Yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

                Paint(e);
            }
        }

        void RetireBoss(BossSnake boss)
        {
            var segments = boss.Segments;

            // Bangkai terbakar memakai model GEROMBOLAN, bukan model boss.
            //
            // BurningCorpse menyimpan `Kind`, dan ruas boss sengaja ber-Kind null supaya tidak
            // ikut jalur AI gerombolan — jadi bangkainya jatuh ke model bawaan. Untuk ruas ular
            // yang sebesar kepalan itu nyaris tidak terlihat; untuk naga setinggi delapan unit,
            // yang terlihat adalah naga yang mati lalu berubah jadi TENGKORAK di udara.
            //
            // Yang bersayap karena itu tidak meninggalkan bangkai sama sekali. Ia tidak
            // kehilangan apa pun: kematian boss sudah punya upacaranya sendiri — raungan,
            // fanfare, dan banner SLAIN — sementara percikan ruas itu milik badan yang memendek,
            // dan naga tidak punya ruas untuk dilepas.
            bool leavesCorpse = boss.Def.Body != BossDefinition.BossBody.Winged;

            for (int i = 0; i < segments.Count; i++)
            {
                segments[i].Alive = false;
                segments[i].Boss = null;
                if (leavesCorpse) StartBurn(segments[i]);
                OnKill?.Invoke(segments[i].Pos);
            }

            // Gerbang yang sama dengan OnBossSpawned di atas: grub minion bukan boss.
            // Tanpa ini tiap grub kecil yang mati membunyikan raungan, fanfare kemenangan,
            // dan banner SLAIN — tiga upacara untuk seekor belatung.
            if (!boss.Def.Minion) OnBossDied?.Invoke(boss.HeadPos);

            boss.End();
        }

        /// <summary>
        /// Mengosongkan lapangan tanpa menghitungnya sebagai kill. Buat ruang uji: menyusun ulang
        /// boneka tidak boleh mengotori Kills atau memicu drop.
        /// </summary>
        public void ClearField()
        {
            if (_scorch != null) _scorch.Clear();
            for (int i = 0; i < _bosses.Count; i++) _bosses[i].End();
            _bosses.Clear();

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                e.Alive = false;
                e.Boss = null;
                e.LiftTimer = 0f;
                e.Knock = Vector3.zero;
            }

            AliveCount = 0;
            RebuildHash();
        }

        /// <summary>
        /// Boneka latihan: berdiri diam di satu titik dan praktis tidak bisa mati.
        ///
        /// <c>Hp</c> sengaja jauh di atas <c>MaxHp</c>, dan itu bukan kelalaian. Besar-kecilnya
        /// angka damage di layar diukur dari porsi HP musuh, jadi boneka ber-HP sejuta akan membuat
        /// setiap pukulan tampil sebagai angka mungil yang tidak terbaca. Memisahkan keduanya
        /// memberi boneka yang tahan lama DAN angka yang tetap sebesar musuh sungguhan.
        /// </summary>
        /// <summary>
        /// Satu musuh dari arketipe yang DIPILIH, bukan diundi. Buat panggung sutradara.
        /// </summary>
        /// <returns>False kalau lapangan sudah penuh.</returns>
        public bool SpawnOfKind(EnemyArchetype kind) => SpawnOne(1f, kind);

        /// <summary>
        /// Satu boss dari jenis yang DIPILIH, langsung, tanpa menunggu wave.
        ///
        /// Jalur yang sudah ada tidak bisa dipakai untuk ini dan itu bukan kekurangan kecil:
        /// <c>SpawnBoss</c> mengundi lewat <c>RollBossKind</c>, dan <c>ForceBossNode</c> cuma
        /// menitipkan pesanan untuk wave BERIKUTNYA. Keduanya benar untuk permainan — yang tidak
        /// ada cuma cara meminta naga, sekarang, karena ada yang mau memotretnya.
        ///
        /// Melewati <see cref="Hatch"/> yang sama dengan boss sungguhan, jadi apa pun yang muncul
        /// di sini adalah boss yang sama persis dengan yang ditemui pemain — bukan tiruan yang
        /// diam-diam menyimpang begitu salah satunya diubah.
        /// </summary>
        /// <returns>Boss yang lahir, atau null kalau def-nya kosong.</returns>
        public BossSnake SpawnBossOfKind(BossDefinition def, float hpMul = 1f, float aggro = 1f)
        {
            if (def == null) return null;

            Hatch(def, Mathf.Max(1, Wave), hpMul, aggro);
            return _bosses.Count > 0 ? _bosses[_bosses.Count - 1] : null;
        }

        public void SpawnDummy(Vector3 at, float hp)
        {
            var e = GetFree();
            if (e == null) return;

            if (_nextInCell.Length < _pool.Count)
            {
                System.Array.Resize(ref _nextInCell, Mathf.Max(64, _pool.Count * 2));
            }

            e.Kind = null;
            e.Pos = at;
            e.GroundY = at.y;
            e.MaxHp = 500f;
            e.Hp = Mathf.Max(1f, hp);
            e.Speed = 0f;
            e.Scale = 1f;
            e.Flank = 0f;
            e.AttackTimer = 0f;
            e.LiftTimer = 0f;
            e.Knock = Vector3.zero;
            e.ReactionLock = 0f;
            e.Phase = Random.Range(0f, Mathf.PI * 2f);
            e.Yaw = 0f;

            for (int i = 0; i < StatusSlots; i++) e.Slots[i].Def = -1;

            e.Alive = true;
            Paint(e);
        }

        void FinishWave()
        {
            WaveActive = false;
            Closing = false;

            // Modifier peta habis masa berlakunya bersama wave-nya — elite tidak menular.
            _nextHpMul = 1f;
            _nextCountMul = 1f;
            _nextDamageMul = 1f;
            _nextBossCount = 0;
            _nextBossHpMul = 1f;
            _nextBossAggro = 1f;
            _nextBossAllKinds = false;
            _nextBossAct = 1;

            OnWaveCleared?.Invoke();
        }

        void TickSpawning(float dt)
        {
            if (!WaveActive || Closing) return;
            if (Cheats != null && Cheats.CheatFreezeSpawns) return;

            // Each wave climbs toward its own crescendo instead of running flat from the first
            // second, so the last stretch is the hardest part rather than the emptiest.
            float progress = WaveTotal <= 0 ? 1f : 1f - (float)PendingSpawns / WaveTotal;
            _spawnBudget += _spawnRate * _balance.RampAt(progress) * dt;

            // Banked spawns are capped: a long spell pinned at the alive cap must not save up a
            // tidal wave that lands all at once the moment a slot frees.
            if (_spawnBudget > 20f) _spawnBudget = 20f;

            while (_spawnBudget >= 1f && PendingSpawns > 0)
            {
                if (!SpawnOne()) break;

                _spawnBudget -= 1f;
                PendingSpawns--;
            }
        }

        /// <summary>
        /// Puts one enemy on the field. Every per-enemy stat is filled here and nowhere else, so a
        /// different kind of enemy — a boss, say — is a different fill of this one method.
        /// </summary>
        /// <returns>False when the alive cap is full and nothing could be spawned.</returns>
        /// <param name="forced">
        /// Arketipe yang dipaksa, menimpa undian. Null = diundi seperti biasa.
        ///
        /// Ada untuk panggung sutradara, tempat yang mau dilihat adalah SATU jenis musuh — dan
        /// undian per-wave membuat itu mustahil: meminta sepuluh ekor jenis tertentu berarti
        /// menekan tombol sampai undiannya kebetulan mengeluarkan yang dicari.
        /// </param>
        /// <param name="at">Titik lahir PAKSA (pecahan broodmother lahir di bangkai induknya,
        /// bukan di tepi layar). Null = kotak spawn biasa di luar pandangan.</param>
        bool SpawnOne(float distanceScale = 1f, EnemyArchetype forced = null, Vector3? at = null)
        {
            var e = GetFree();
            if (e == null) return false;

            var kind = forced != null ? forced : _db.RollArchetype(Wave);
            e.Kind = kind;

            e.Pos = at ?? SpawnPoint(distanceScale);

            // Tiga lapis pengali, dan ketiganya DIKALIKAN bukan saling menimpa: saklar curang,
            // modifier node peta (elite — sekali pakai), dan pakta (permanen seumur run). Elite
            // yang dijalani di bawah pakta "darah tebal" memang harus lebih tebal dari keduanya
            // sendiri-sendiri — itu seluruh gunanya menumpuk pakta.
            e.MaxHp = _balance.EnemyHpFor(Wave) * (Cheats != null ? Cheats.EnemyHpScale : 1f)
                      * _nextHpMul * PactHpMul;

            e.Speed = (Random.Range(_balance.EnemySpeedMin, _balance.EnemySpeedMax) +
                       Wave * _balance.EnemySpeedPerWave) * PactSpeedMul;
            e.Scale = 1f;
            e.AttackTimer = 0f;

            if (kind != null)
            {
                e.MaxHp *= kind.HpMultiplier;
                e.Speed *= kind.SpeedMultiplier;
                e.Scale = kind.Scale;
                e.Pos.y += kind.HoverHeight;

                // Staggered so a batch that spawns together does not volley in perfect unison.
                if (kind.Shoots) e.AttackTimer = Random.Range(0f, kind.AttackInterval);
            }

            // Kolom paralel hash harus tumbuh bersama pool, di sini dan bukan cuma di RebuildHash.
            // Musuh bisa lahir sebelum rebuild pertama, dan yang membaca rantai itu tidak punya
            // cara tahu bahwa larik indeksnya masih lebih pendek dari pool.
            if (_nextInCell.Length < _pool.Count)
            {
                System.Array.Resize(ref _nextInCell, Mathf.Max(64, _pool.Count * 2));
            }

            e.Hp = e.MaxHp;
            e.GroundY = e.Pos.y;
            e.LiftTimer = 0f;
            e.Knock = Vector3.zero;

            // Slot bekas penyeruduk tidak boleh mewariskan fase lesatannya ke penghuni baru.
            e.ChargePhase = 0;
            e.ChargeLeft = 0f;
            e.ChargeDir = Vector3.zero;
            e.ChargeTimer = kind != null && kind.ChargeEvery > 0f
                ? kind.ChargeEvery * Random.Range(0.6f, 1.3f)
                : 0f;

            // Jatah regen milik nyawa BARU — slot bekas kadal tidak mewariskan sisa kaburnya.
            e.RegenLeft = 0f;
            e.RegenUsesLeft = kind != null ? kind.RegenUses : 0;

            // Slot bekas musuh yang baru saja dipukul akan lahir sambil memamerkan bar HP penuh
            // kalau ini tidak dibersihkan.
            e.HurtSeen = -999f;

            // Slot pool dipakai ulang. Tanpa dibersihkan, musuh biasa yang kebetulan mendapat
            // slot bekas ruas boss akan mewarisi kepemilikannya: damage-nya mengalir ke kolam HP
            // ular yang sudah mati, dan musuh itu sendiri tidak pernah bisa dibunuh.
            e.Boss = null;
            e.BossIndex = 0;

            for (int i = 0; i < StatusSlots; i++) e.Slots[i].Def = -1;

            e.Phase = Random.Range(0f, Mathf.PI * 2f);
            e.Yaw = Random.Range(0f, 360f);

            // Signed, so half the wave peels left and half peels right and the two halves meet
            // on the far side of the caster.
            e.Flank = Random.Range(-1f, 1f);
            e.Alive = true;
            Paint(e);
            return true;
        }

        /// <summary>
        /// Titik acuan kotak spawn. Defaultnya pemain; begitu kamera bisa bergerak, ini harus
        /// diarahkan ke rig kamera.
        ///
        /// Bedanya bukan kosmetik. Kamera punya zona mati, jadi pemain boleh menyimpang jauh dari
        /// pusat layar sebelum kamera ikut — dan kotak yang mengikuti PEMAIN akan menaruh musuh di
        /// sisi yang sudah dijauhi pemain persis di dalam layar. Musuh menetas di depan mata.
        /// </summary>
        Transform _spawnAnchor;

        public void SetSpawnAnchor(Transform anchor) => _spawnAnchor = anchor;

        /// <summary>
        /// Walks a ray out from the caster in a random direction until it leaves the spawn box, and
        /// drops the enemy there.
        ///
        /// A ring of fixed radius does not work, because the region the camera shows is a wide
        /// rectangle, not a circle. On a ring, enemies arriving from the sides appear right at the
        /// screen edge in full view while enemies from above and below are still far off screen.
        /// Exiting through a box means every direction is off screen by the same margin.
        ///
        /// Kotaknya berpusat di ANCHOR, bukan di titik nol dunia. Dulu batasnya koordinat dunia
        /// absolut, yang diam-diam mengikat ukuran arena ke jarak tempuh musuh: memperbesar arena
        /// berarti musuh berjalan lebih lama, jadi arena tidak pernah bisa dibesarkan tanpa
        /// merusak tempo. Dilepas dari titik nol, keduanya jadi tidak saling menyandera.
        /// </summary>
        Vector3 SpawnPoint(float distanceScale = 1f)
        {
            Vector3 from = _player.position;
            Vector3 centre = _spawnAnchor != null ? _spawnAnchor.position : from;

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dx = Mathf.Cos(angle);
            float dz = Mathf.Sin(angle);

            float hx = _balance.SpawnBoundsX;
            float hz = _balance.SpawnBoundsZ;

            // Diukur relatif terhadap pusat kotak, lalu sinarnya tetap berangkat dari pemain.
            float sx = from.x - centre.x;
            float sz = from.z - centre.z;

            float tx = Mathf.Abs(dx) < 0.0001f
                ? float.MaxValue
                : ((dx > 0f ? hx : -hx) - sx) / dx;

            float tz = Mathf.Abs(dz) < 0.0001f
                ? float.MaxValue
                : ((dz > 0f ? hz : -hz) - sz) / dz;

            // Never behind us, and always at least a little way out even when the caster is already
            // pressed against the boundary.
            float t = Mathf.Max(4f, Mathf.Min(tx, tz)) * Mathf.Clamp(distanceScale, 0.2f, 1f);

            // Y = 0: musuh lahir DI TANAH. Angka lama 0,55 adalah setengah tinggi kapsul era
            // 1,1 unit — kapsul ber-origin tengah memang harus diangkat setengah badan supaya
            // pantatnya tidak tenggelam. Model panggangan ber-origin KAKI, jadi angka itu
            // membuat seluruh gerombolan melayang 0,55 unit — tak terlihat selama pass bayangan
            // mati, dan langsung ketahuan begitu bayangan menyala. Kapsul cadangan sekarang
            // mengangkat dirinya sendiri di LateUpdate, di tempat ia digambar.
            return new Vector3(from.x + dx * t, 0f, from.z + dz * t);
        }

        Enemy GetFree()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].Alive) return _pool[i];
            }

            if (_pool.Count >= Capacity) return null;

            var fresh = new Enemy { Alive = false };
            _pool.Add(fresh);
            return fresh;
        }

        void TickEnemies(float dt)
        {
            AliveCount = 0;
            for (int i = 0; i < _statusCounts.Length; i++) _statusCounts[i] = 0;

            Vector3 target = _player.position;

            // Measured here rather than asked of PlayerMotor: this only needs how the caster is
            // actually moving, and reading it from the transform keeps the two systems unaware
            // of each other.
            Vector3 playerVelocity = dt > 0.0001f ? (target - _lastPlayerPos) / dt : Vector3.zero;
            if (playerVelocity.sqrMagnitude > 400f) playerVelocity = Vector3.zero;   // teleport guard
            _lastPlayerPos = target;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                AliveCount++;
                if (e.ReactionLock > 0f) e.ReactionLock -= dt;

                // Survivors charge once nothing more is coming. The tail was never about having to
                // kill the last few — it was about walking to them.
                float speedMul = Closing ? _balance.ClosingSpeedMultiplier : 1f;
                bool repaint = false;

                for (int s = 0; s < StatusSlots; s++)
                {
                    int defIndex = e.Slots[s].Def;
                    if (defIndex < 0) continue;

                    var def = _db.Statuses[defIndex];
                    e.Slots[s].Remaining -= dt;

                    if (e.Slots[s].Remaining <= 0f)
                    {
                        e.Slots[s].Def = -1;
                        repaint = true;
                        continue;
                    }

                    _statusCounts[defIndex]++;
                    speedMul *= def.MoveSpeedMultiplier;

                    if (def.DamagePerTickPerPoint > 0f)
                    {
                        e.Slots[s].TickTimer -= dt;
                        if (e.Slots[s].TickTimer <= 0f)
                        {
                            e.Slots[s].TickTimer = def.TickInterval;
                            float dot = def.DamagePerTickPerPoint * e.Slots[s].Points;

                            // Sama seperti damage langsung: DoT di ruas boss masuk ke kolam
                            // bersama. Kalau tidak, membakar boss malah memutus badannya.
                            if (e.Boss != null) e.Boss.TakeDamage(dot);
                            else e.Hp -= dot;

                            OnDamage?.Invoke(def.DisplayName, dot);
                            // DoT memakai warna STATUS-nya — tik burn tampil merah api,
                            // racun hijau — bukan warna skill yang menempelkannya.
                            OnEnemyDamaged?.Invoke(e.Pos, dot, e.MaxHp, def.Color, false);
                        }
                    }

                    // Tarikan: geser musuh ke titik tempat ailment ini dipasang.
                    if (def.PullStrength > 0f)
                    {
                        Vector3 p = e.Pos;
                        float dx = e.Slots[s].PullX - p.x;
                        float dz = e.Slots[s].PullZ - p.z;
                        float distSqr = dx * dx + dz * dz;

                        if (distSqr > 0.09f)
                        {
                            float pullStep = def.PullStrength * dt / Mathf.Sqrt(distSqr);
                            p.x += dx * pullStep;
                            p.z += dz * pullStep;
                            e.Pos = p;
                        }
                    }
                }

                if (e.Hp <= 0f)
                {
                    Kill(e);
                    continue;
                }

                if (repaint) Paint(e);

                // Ruas boss disetir BossSnake, bukan AI swarm. Dilewati SETELAH ailment sempat
                // berdenyut — kalau dilewati lebih awal, membakar boss tidak melakukan apa pun —
                // tapi SEBELUM gaya pisah, jalur serong, lontaran dan damage sentuh, yang
                // semuanya akan langsung mencabik bentuk ularnya.
                if (e.Boss != null) continue;

                // Dorongan diproses lebih dulu dan berlaku untuk semua, termasuk yang terangkat.
                // Kalau tidak, musuh yang dilontarkan sambil diangkat akan menggantung di tempat
                // dan lontarannya tidak pernah terbaca.
                if (e.Knock.sqrMagnitude > 0.0004f)
                {
                    e.Pos += e.Knock * dt;

                    // Peluruhan eksponensial: keras di awal, berhenti mulus. Pengurangan linier
                    // membuat semua lontaran mendarat dengan rem mendadak yang sama.
                    e.Knock *= Mathf.Exp(-6f * dt);
                }
                else
                {
                    e.Knock = Vector3.zero;
                }

                // Terangkat = benar-benar keluar dari pertandingan sementara. Tidak melangkah,
                // tidak menembak, tidak menyentuh. Itu seluruh alasan skill kontrol ada.
                if (e.LiftTimer > 0f)
                {
                    e.LiftTimer -= dt;

                    // Naik cepat, turun pelan, jadi jatuhnya terbaca sebagai dijatuhkan bukan hilang.
                    float height = Mathf.Min(2.6f, e.LiftTimer * 4f);
                    e.Pos.y = e.GroundY + height;
                    e.Yaw += 420f * dt;

                    if (e.LiftTimer <= 0f) e.Pos.y = e.GroundY;
                    continue;
                }

                Vector3 pos = e.Pos;
                Vector3 delta = target - pos;
                delta.y = 0f;
                float sqr = delta.sqrMagnitude;

                Vector3 apart = Separation(e, pos, _balance.EnemySeparation) * _balance.SeparationWeight;
                float distance = Mathf.Sqrt(sqr);

                // ---- REGENERASI EKOR ----
                // Kabur MENJAUH sambil menumbuhkan lukanya — ekor kadal yang putus. Larinya
                // adalah aba-abanya: kejar dan habisi sebelum utuh, atau relakan ia kembali.
                if (e.RegenLeft > 0f && e.Kind != null)
                {
                    e.RegenLeft -= dt;
                    e.Hp = Mathf.Min(e.MaxHp, e.Hp + e.MaxHp * e.Kind.RegenFracPerSecond * dt);

                    // Utuh sebelum waktunya = tidak ada lagi yang ditumbuhkan, berhenti lari.
                    if (e.Hp >= e.MaxHp) e.RegenLeft = 0f;

                    // Pagar 26 m: tanpa pagar ia lari keluar dunia. Di luarnya ia berdiri
                    // memulihkan diri di tempat — tetap terkejar, tetap terlihat.
                    if (distance > 0.01f && distance < 26f)
                    {
                        Vector3 flee = delta / -distance + apart;
                        if (flee.sqrMagnitude > 0.0001f) flee.Normalize();

                        pos.x += flee.x * e.Speed * 1.35f * speedMul * dt;
                        pos.z += flee.z * e.Speed * 1.35f * speedMul * dt;
                        e.Pos = pos;
                        e.Yaw = Mathf.Atan2(flee.x, flee.z) * Mathf.Rad2Deg;
                    }

                    continue;
                }

                // ---- MENYERUDUK ----
                // Ancang-ancang yang BERHENTI TOTAL adalah aba-abanya — pemain diberi
                // ChargeWindup penuh untuk membaca garisnya. Arah lesatan dikunci saat
                // berangkat, jadi melangkah ke samping selalu jadi jawaban yang benar.
                if (e.Kind != null && e.Kind.ChargeEvery > 0f && e.Boss == null)
                {
                    if (e.ChargePhase == 1)
                    {
                        e.ChargeLeft -= dt;
                        e.Yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;

                        if (e.ChargeLeft <= 0f && distance > 0.01f)
                        {
                            e.ChargePhase = 2;
                            e.ChargeLeft = e.Kind.ChargeSeconds;
                            e.ChargeDir = delta / distance;
                        }

                        continue;
                    }

                    if (e.ChargePhase == 2)
                    {
                        e.ChargeLeft -= dt;

                        float rush = e.Speed * e.Kind.ChargeSpeedMul * speedMul * dt;
                        pos.x += e.ChargeDir.x * rush;
                        pos.z += e.ChargeDir.z * rush;
                        e.Pos = pos;
                        e.Yaw = Mathf.Atan2(e.ChargeDir.x, e.ChargeDir.z) * Mathf.Rad2Deg;

                        Vector3 ram = target - pos;
                        ram.y = 0f;

                        // Tabrakan = SATU pukulan, bukan tetesan per frame — serudukan yang
                        // kena harus terbaca sebagai kejadian, dan lesatannya berhenti di situ.
                        if (ram.sqrMagnitude < 1.1f)
                        {
                            _caster.TakeHit(e.Kind.AttackDamage * _balance.EnemyDamageScale(Wave)
                                * _nextDamageMul * PactDamageMul);
                            if (e.Kind.Curse != null) _caster.ApplyDebuff(e.Kind.Curse);
                            e.ChargeLeft = 0f;
                        }

                        if (e.ChargeLeft <= 0f)
                        {
                            e.ChargePhase = 0;
                            e.ChargeTimer = e.Kind.ChargeEvery * Random.Range(0.85f, 1.25f);
                        }

                        continue;
                    }

                    e.ChargeTimer -= dt;

                    // Terlalu dekat = serudukan tak terbaca; terlalu jauh = pemain keburu
                    // pergi dari garisnya. Di luar jendela, jam menunggu di nol.
                    if (e.ChargeTimer <= 0f && distance > 3f && distance < 16f)
                    {
                        e.ChargePhase = 1;
                        e.ChargeLeft = e.Kind.ChargeWindup;
                        continue;
                    }
                }

                // Shooters hold their distance and fire. Once the wave is closing they abandon the
                // range entirely and charge — otherwise a build with no long-range skill could
                // never finish a wave, and the safety timeout would fire every single round.
                float standOff = e.Kind != null && !Closing ? e.Kind.PreferredRange : 0f;

                if (e.Kind != null && e.Kind.Shoots)
                {
                    e.AttackTimer -= dt;
                    if (e.AttackTimer <= 0f && distance <= e.Kind.PreferredRange * 1.15f)
                    {
                        e.AttackTimer = e.Kind.AttackInterval;
                        FireShot(e, target);
                    }
                }

                if (standOff > 0f)
                {
                    TickStandOff(e, pos, delta, distance, standOff, apart, speedMul, dt);
                    continue;
                }

                if (sqr < 0.85f)
                {
                    _caster.TakeDamage(_balance.ContactDpsFor(Wave) * _nextDamageMul * PactDamageMul * dt);

                    // Refreshed on every frame of contact, so a curse lasts as long as you are in
                    // the crowd plus its duration. Getting out is the counter-play; resistance and
                    // cleansing shorten what is left once you do.
                    if (e.Kind != null && e.Kind.Curse != null) _caster.ApplyDebuff(e.Kind.Curse);

                    // Still shoved apart while in contact, so the pack forms a ring around the
                    // caster instead of every one of them stacking on the same square metre.
                    if (apart.sqrMagnitude > 0.0001f)
                    {
                        e.Pos = pos + apart.normalized * (e.Speed * speedMul * dt);
                    }

                    continue;
                }

                // Aim where the caster WILL be. Chasing where it is now means the pack can never
                // cut a corner, so a player that keeps walking simply tows it around forever.
                float lead = Mathf.Min(distance / Mathf.Max(0.1f, e.Speed), _balance.InterceptLead);
                Vector3 aimPoint = target + playerVelocity * lead;

                // Swing wide on the way in, then straighten up for the last few metres. Without the
                // fade they would circle forever instead of ever arriving.
                //
                // Dropped entirely once the wave is closing: swinging wide is what turns the last
                // handful of enemies into a chase across the map, which is the dead tail the whole
                // spawn window exists to avoid.
                bool flanks = e.Kind == null || e.Kind.Flanks;
                if (flanks && !Closing && distance > _balance.FlankFade)
                {
                    Vector3 tangent = new Vector3(-delta.z, 0f, delta.x) / distance;
                    aimPoint += tangent * (e.Flank * _balance.FlankWidth);
                }

                Vector3 aim = aimPoint - pos;
                aim.y = 0f;

                Vector3 heading = aim.sqrMagnitude > 0.0001f ? aim.normalized : delta / distance;
                heading += apart;

                if (heading.sqrMagnitude > 0.0001f) heading.Normalize();

                pos.x += heading.x * e.Speed * speedMul * dt;
                pos.z += heading.z * e.Speed * speedMul * dt;
                e.Pos = pos;

                // Facing does nothing on a capsule, but a walk cycle has to point somewhere.
                e.Yaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
            }

            RebuildHash();
            TickShots(dt, target);
        }

        /// <summary>
        /// A shooter's holding pattern: close to its preferred range, back off when crowded past it,
        /// and otherwise stand still and keep firing.
        ///
        /// The band matters. Without the back-off, a shooter pushed by separation drifts into melee
        /// and stops being a different threat; without the dead zone in between it jitters on the
        /// spot forever.
        /// </summary>
        void TickStandOff(Enemy e, Vector3 pos, Vector3 delta, float distance, float standOff,
            Vector3 apart, float speedMul, float dt)
        {
            Vector3 toPlayer = delta / distance;

            // Separation only nudges here. Holding the range is this archetype's whole identity, so
            // crowd pressure must not be allowed to outvote it.
            Vector3 heading = apart * 0.35f;

            if (distance > standOff) heading += toPlayer;
            else if (distance < standOff * 0.65f) heading -= toPlayer;

            if (heading.sqrMagnitude > 0.0001f)
            {
                heading.Normalize();
                pos.x += heading.x * e.Speed * speedMul * dt;
                pos.z += heading.z * e.Speed * speedMul * dt;
                e.Pos = pos;
            }

            // Always facing its target, even while strafing — it is aiming, not walking.
            e.Yaw = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
        }

        // ---------- enemy shots ----------

        public class Shot
        {
            public Vector3 Pos;
            public Vector3 Velocity;
            public float Life;
            public float Damage;
            public int Tint;

            /// <summary>Kutukan yang ditempel ke pemain saat kena. Null untuk peluru biasa.</summary>
            public BuffDefinition Curse;

            public bool Active;
        }

        readonly List<Shot> _shots = new List<Shot>(64);

        public int ShotCount { get; private set; }

        void FireShot(Enemy from, Vector3 target)
        {
            Vector3 aim = target - from.Pos;
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.0001f) return;

            Shot shot = null;
            for (int i = 0; i < _shots.Count; i++)
            {
                if (_shots[i].Active) continue;
                shot = _shots[i];
                break;
            }

            if (shot == null)
            {
                if (_shots.Count >= MaxShots) return;
                shot = new Shot();
                _shots.Add(shot);
            }

            float speed = Mathf.Max(1f, from.Kind.ShotSpeed);

            shot.Pos = from.Pos;
            shot.Velocity = aim.normalized * speed;
            shot.Damage = from.Kind.AttackDamage * _balance.EnemyDamageScale(Wave)
                          * _nextDamageMul * PactDamageMul;
            shot.Tint = _shotTint.TryGetValue(from.Kind, out int tint) ? tint : 0;

            // Jaring laba-laba dkk.: peluru boleh membawa kutukan — jalur yang sama dengan
            // ludah boss, cuma sumbernya arketipe.
            shot.Curse = from.Kind.ShotCurse;

            // Just past its own range, so a shot the player outran expires instead of chasing.
            shot.Life = from.Kind.PreferredRange * 1.5f / speed;
            shot.Active = true;
        }

        /// <summary>
        /// Semburan racun boss. Memakai kolam peluru yang sama dengan penembak biasa: pelurunya
        /// sudah punya tabrakan, penggambaran instanced dan umur sendiri, jadi menulis jalur baru
        /// cuma akan menduplikasi tiga hal yang sudah benar.
        /// </summary>
        void SpitShot(BossSnake boss, Vector3 from, Vector3 dir, int wave)
        {
            var def = boss.Def;

            Shot shot = null;
            for (int i = 0; i < _shots.Count; i++)
            {
                if (_shots[i].Active) continue;
                shot = _shots[i];
                break;
            }

            if (shot == null)
            {
                if (_shots.Count >= MaxShots) return;
                shot = new Shot();
                _shots.Add(shot);
            }

            float speed = Mathf.Max(1f, def.SpitSpeed);

            shot.Pos = from;
            shot.Velocity = dir * speed;
            // Ludah boss lewat pakta juga. Ia sempat terlewat sekali — dan yang terjadi adalah
            // pakta "musuh memukul lebih keras" yang diam saja terhadap satu-satunya lawan yang
            // pukulannya paling diperhatikan pemain.
            shot.Damage = def.SpitDamage * _balance.EnemyDamageScale(wave) * PactDamageMul;
            shot.Tint = 0;
            shot.Curse = def.SpitCurse;
            shot.Life = 2.4f;
            shot.Active = true;
        }

        const int MaxShots = 400;

        void TickShots(float dt, Vector3 target)
        {
            ShotCount = 0;

            for (int i = 0; i < _shots.Count; i++)
            {
                var shot = _shots[i];
                if (!shot.Active) continue;

                shot.Life -= dt;
                if (shot.Life <= 0f)
                {
                    shot.Active = false;
                    continue;
                }

                shot.Pos += shot.Velocity * dt;
                ShotCount++;

                Vector3 d = shot.Pos - target;
                d.y = 0f;
                if (d.sqrMagnitude > 0.55f) continue;

                // A burst, not a per-frame trickle: TakeDamage subtracts defence scaled by delta
                // time, which is right for contact and nearly nothing against a single hit.
                _caster.TakeHit(shot.Damage);

                // Racun kelabang menempel ke PEMAIN lewat kutukan, bukan lewat ailment musuh —
                // ailment tinggal di musuh, dan pemain punya jalur debuff-nya sendiri.
                if (shot.Curse != null) _caster.ApplyDebuff(shot.Curse);

                shot.Active = false;
            }
        }

        /// <summary>
        /// Hands the swarm to the renderer once per frame, after everything has settled. Enemies own
        /// no GameObject, so if this stops running they stop existing on screen.
        /// </summary>
        void LateUpdate()
        {
            if (_renderers == null || _renderers.Length == 0) return;

            for (int r = 0; r < _renderers.Length; r++) _renderers[r].Begin();

            foreach (var visual in _bossVisuals.Values) { if (visual != null) visual.Begin(); }

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                // Ruas boss yang sedang menyelam tidak digambar. Menggambarnya di bawah lantai
                // saja tidak cukup: lantai tidak menutupi apa pun dari kamera yang menunduk, dan
                // yang terlihat adalah kelabang yang berenang di dalam tanah kaca.
                if (e.Boss != null && e.Pos.y < BuriedDepth) continue;

                // Ruas boss punya penggambarnya sendiri, dan mesh-nya dipilih dari URUTAN ruas —
                // bukan dari archetype, karena ruas boss sengaja dibuat tanpa archetype (Kind null)
                // supaya ia tidak ikut jalur AI gerombolan. Tanpa cabang ini seluruh badan boss
                // jatuh ke model bawaan gerombolan dan tergambar sebagai barisan kapsul.
                if (e.Boss != null)
                {
                    var visual = VisualFor(e.Boss.Def);

                    if (visual != null)
                    {
                        // Tanpa pengangkatan kapsul: pivot ketiga mesh boss sudah di TENGAH keping,
                        // jadi menaikkannya setengah badan justru membuat ularnya melayang.
                        // Kecepatannya ikut dikirim, dan cuma yang bersayap memakainya: model
                        // ular tidak beranimasi sama sekali, jadi angka apa pun di sana tidak
                        // mengubah satu piksel pun.
                        visual.Add(e.BossIndex, e.Boss.Segments.Count,
                            e.Pos, e.Yaw, e.Phase, e.Tint, e.Scale, e.Boss.Speed01);

                        if (e.Boss.Def.Body == BossDefinition.BossBody.Winged)
                            visual.SetBank(e.Boss.BankDegrees);

                        continue;
                    }
                }

                // Kecepatannya ikut dikirim: itu yang memilih antara diam, jalan, dan lari di
                // animasi panggangan. Renderer kapsul mengabaikannya.
                int model = ModelOf(e.Kind);

                // Kapsul cadangan ber-origin TENGAH, model panggangan ber-origin KAKI. Posisi
                // gameplay sekarang selalu di tanah; yang butuh diangkat cuma kapsulnya, dan
                // pengangkatannya milik penggambaran — bukan milik posisi yang dipakai jarak
                // gigit dan AoE.
                var at = e.Pos;
                if (_models[model] == null) at.y += BodyHeight * 0.5f * e.Scale;

                _renderers[model].Add(at, e.Yaw, e.Phase, e.Tint, e.Scale,
                    Mathf.Clamp01(e.Speed / RunSpeed));
            }

            // Bangkai ikut di batch yang sama. deltaTime biasa, bukan unscaled: tombol kecepatan
            // 5x juga mempercepat kematian, dan bangkai yang terbakar dalam waktu nyata terlihat
            // bergerak lambat aneh di antara gerombolan yang dipercepat.
            DrawBurningCorpses(Time.deltaTime);
            if (_scorch != null) _scorch.Draw(Time.deltaTime);

            // Renderer yang tidak kebagian satu pun musuh keluar lebih awal tanpa menggambar,
            // jadi model yang belum muncul di wave ini tidak membayar apa-apa.
            for (int r = 0; r < _renderers.Length; r++) _renderers[r].Draw(Time.time);

            foreach (var visual in _bossVisuals.Values) { if (visual != null) visual.Draw(Time.time); }

            _shotRenderer.Begin();
            for (int i = 0; i < _shots.Count; i++)
            {
                var shot = _shots[i];
                if (!shot.Active) continue;

                _shotRenderer.Add(shot.Pos + Vector3.up * 0.2f, 0f, 0f, shot.Tint, 1f);
            }

            _shotRenderer.Draw(Time.time);
        }

        /// <summary>Draw calls the swarm cost this frame. Read by nothing but the profiler.</summary>
        /// <summary>
        /// Jumlah panggilan gambar seluruh gerombolan — DIJUMLAHKAN dari semua model.
        ///
        /// Melaporkan satu model saja akan membuat angka ini berbohong ke arah yang paling
        /// berbahaya: kelihatan murah justru saat lapangan sedang memakai empat model sekaligus.
        /// </summary>
        public int DrawBatches
        {
            get
            {
                if (_renderers == null) return 0;

                int total = 0;
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null) total += _renderers[i].Batches;
                }

                foreach (var visual in _bossVisuals.Values)
                {
                    if (visual != null) total += visual.Batches;
                }

                return total;
            }
        }

        // ---------- status ----------

        int SlotOf(Enemy e, int defIndex)
        {
            for (int i = 0; i < StatusSlots; i++)
            {
                if (e.Slots[i].Def == defIndex) return i;
            }

            return -1;
        }

        /// <summary>Empty slot, or the one expiring soonest when all four are taken.</summary>
        int FreeSlot(Enemy e)
        {
            int weakest = 0;
            float weakestRemaining = float.MaxValue;

            for (int i = 0; i < StatusSlots; i++)
            {
                if (e.Slots[i].Def < 0) return i;
                if (e.Slots[i].Remaining >= weakestRemaining) continue;

                weakestRemaining = e.Slots[i].Remaining;
                weakest = i;
            }

            return weakest;
        }

        public void ApplyStatus(Enemy e, StatusDefinition status, float duration, int points,
            bool allowReaction = true, Vector3? origin = null)
        {
            if (e == null || !e.Alive || status == null || duration <= 0f) return;

            int defIndex = _db.IndexOfStatus(status);
            if (defIndex < 0) return;

            int slot = SlotOf(e, defIndex);
            if (slot < 0)
            {
                slot = FreeSlot(e);
                e.Slots[slot].Def = defIndex;
                e.Slots[slot].Points = 0;
                e.Slots[slot].TickTimer = status.TickInterval;
            }

            if (status.PullStrength > 0f)
            {
                Vector3 from = origin ?? e.Pos;
                e.Slots[slot].PullX = from.x;
                e.Slots[slot].PullZ = from.z;
            }

            e.Slots[slot].Points = Mathf.Min(status.MaxPoints, e.Slots[slot].Points + Mathf.Max(1, points));
            e.Slots[slot].Remaining = status.RefreshOnReapply
                ? Mathf.Max(e.Slots[slot].Remaining, duration)
                : e.Slots[slot].Remaining + duration;

            Paint(e);

            OnStatusApplied?.Invoke(e, defIndex, e.Slots[slot].Points);

            if (allowReaction) CheckReactions(e);
        }

        /// <summary>Current points of a status on this enemy, 0 when absent.</summary>
        public int PointsOf(Enemy e, int statusIndex)
        {
            if (e == null || statusIndex < 0) return 0;

            int slot = SlotOf(e, statusIndex);
            return slot < 0 ? 0 : e.Slots[slot].Points;
        }

        /// <summary>Spends points from a status. Removes it entirely when nothing is left.</summary>
        public void ConsumePoints(Enemy e, int statusIndex, int amount)
        {
            if (e == null || statusIndex < 0 || amount <= 0) return;

            int slot = SlotOf(e, statusIndex);
            if (slot < 0) return;

            e.Slots[slot].Points -= amount;
            if (e.Slots[slot].Points <= 0) e.Slots[slot].Def = -1;

            Paint(e);
        }

        void CheckReactions(Enemy e)
        {
            if (e.ReactionLock > 0f) return;

            var reactions = _db.Reactions;

            for (int i = 0; i < reactions.Count; i++)
            {
                var rx = reactions[i];
                if (rx == null || !rx.IsValid) continue;

                int slotA = SlotOf(e, _db.IndexOfStatus(rx.A));
                int slotB = SlotOf(e, _db.IndexOfStatus(rx.B));
                if (slotA < 0 || slotB < 0) continue;
                if (e.Slots[slotA].Points < rx.MinPointsA) continue;
                if (e.Slots[slotB].Points < rx.MinPointsB) continue;

                Trigger(e, rx, slotA, slotB);
                return;
            }
        }

        void Trigger(Enemy e, ReactionDefinition rx, int slotA, int slotB)
        {
            int pointsA = e.Slots[slotA].Points;
            Vector3 at = e.Pos;

            if (rx.ConsumeA) e.Slots[slotA].Def = -1;
            if (rx.ConsumeB) e.Slots[slotB].Def = -1;

            // Flat damage alone goes stale: enemy HP climbs every wave while a burst never does,
            // so a share of max HP rides along to keep reactions relevant late.
            float damage = rx.BurstDamage + rx.BurstDamagePerPointA * pointsA
                           + e.MaxHp * rx.BurstPctOfMaxHp;

            e.ReactionLock = ReactionCooldown;
            OnReaction?.Invoke(at, rx);

            if (damage > 0f && rx.BurstRadius > 0f)
            {
                DamageArea(at, rx.BurstRadius, damage, null, 0f, 1, false, rx.DisplayName);
            }

            if (rx.ApplyStatus == null) return;

            if (rx.SpreadToNearby)
            {
                float sqrRadius = rx.BurstRadius * rx.BurstRadius;
                for (int i = 0; i < _pool.Count; i++)
                {
                    var other = _pool[i];
                    if (!other.Alive) continue;

                    Vector3 d = other.Pos - at;
                    d.y = 0f;
                    if (d.sqrMagnitude > sqrRadius) continue;

                    // allowReaction:false â€” a spread must never chain into another spread.
                    ApplyStatus(other, rx.ApplyStatus, rx.ApplyDuration, rx.ApplyPoints, false);
                }

                return;
            }

            ApplyStatus(e, rx.ApplyStatus, rx.ApplyDuration, rx.ApplyPoints, false);
        }

        float DamageTakenMultiplier(Enemy e)
        {
            float mul = 1f;

            for (int i = 0; i < StatusSlots; i++)
            {
                int defIndex = e.Slots[i].Def;
                if (defIndex < 0) continue;
                mul *= _db.Statuses[defIndex].DamageTakenMultiplier;
            }

            return mul;
        }

        /// <summary>
        /// Picks which palette slot this enemy draws with. Used to push a colour into a per-enemy
        /// MaterialPropertyBlock — which is exactly what stopped 200 enemies from ever batching.
        /// Now it just writes an int, and the renderer groups by it.
        /// </summary>
        void Paint(Enemy e)
        {
            int strongestIndex = -1;
            float strongest = 0f;
            int count = 0;

            for (int i = 0; i < StatusSlots; i++)
            {
                int defIndex = e.Slots[i].Def;
                if (defIndex < 0) continue;

                count++;
                if (e.Slots[i].Remaining <= strongest) continue;

                strongest = e.Slots[i].Remaining;
                strongestIndex = defIndex;
            }

            if (strongestIndex < 0)
            {
                // Ruas boss punya warnanya sendiri, dan kepala beda dari badan: kepala itu
                // satu-satunya bagian yang menggigit, jadi harus bisa ditemukan sekejap di antara
                // dua puluh ruas yang siluetnya sama persis.
                if (e.Boss != null)
                {
                    e.Tint = e.BossIndex == 0 ? _bossHeadTint : _bossBodyTint;
                    return;
                }

                // No ailment: fall back to whatever kind of enemy it is.
                e.Tint = e.Kind != null && _archetypeTint.TryGetValue(e.Kind, out int kindTint)
                    ? kindTint
                    : 0;
                return;
            }

            // Two or more ailments at once reads as near-white: the "about to react" tell.
            int statuses = _db.Statuses.Count;
            e.Tint = count >= 2 ? 1 + statuses + strongestIndex : 1 + strongestIndex;
        }

        void Kill(Enemy e)
        {
            e.Alive = false;
            Kills++;
            StartBurn(e);

            // PECAH SAAT MATI: broodmother menetaskan anak-anaknya DI BANGKAINYA — musuh
            // baru yang lahir di tepi layar tidak akan pernah terbaca sebagai "isi perutnya".
            // Ruas boss dikecualikan: kematian ruas bukan kematian makhluk.
            var split = e.Kind != null ? e.Kind.SplitInto : null;
            if (split != null && e.Kind.SplitCount > 0 && e.Boss == null)
            {
                float parentHover = e.Kind.HoverHeight;
                var basePos = new Vector3(e.Pos.x, e.GroundY - parentHover, e.Pos.z);

                for (int i = 0; i < e.Kind.SplitCount; i++)
                {
                    var jitter = new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f));
                    if (!SpawnOne(1f, split, basePos + jitter)) break;
                }
            }

            OnKill?.Invoke(e.Pos);
        }

        // ---------- mati terbakar ----------

        /// <summary>
        /// Bangkai yang sedang terbakar habis. Data GAMBAR saja — logika, tabrakan, dan hash
        /// spasial sudah melepasnya di detik ia mati; yang tersisa cuma beberapa frame di layar
        /// supaya "mati" terbaca sebagai peristiwa, bukan sebagai lenyap.
        /// </summary>
        struct BurningCorpse
        {
            public Vector3 Pos;
            public float Yaw;
            public float Phase;
            public int Tint;
            public float Scale;
            public float Speed01;
            public EnemyArchetype Kind;
            public float Age;
        }

        /// <summary>
        /// Ring buffer, bukan List yang tumbuh: wave besar membunuh puluhan per detik, dan yang
        /// paling tua boleh hilang lebih cepat — mata tidak menghitung bangkai, ia cuma butuh
        /// TIAP kematian sempat menyala sebentar.
        /// </summary>
        readonly BurningCorpse[] _corpses = new BurningCorpse[160];

        int _corpseHead;
        int _corpseCount;

        /// <summary>Lama satu bangkai menyala sebelum habis, detik.</summary>
        const float BurnSeconds = 0.55f;

        void StartBurn(Enemy e)
        {
            // Kecepatan DIBEKUKAN di nilai saat mati: animasinya terus berjalan selagi tubuhnya
            // digerogoti, jadi pelari mati sebagai pelari. Menukar ke pose diam membuat tiap
            // kematian diawali sentakan pose — persis yang membuat kematian instanced murahan.
            _corpses[(_corpseHead + _corpseCount) % _corpses.Length] = new BurningCorpse
            {
                Pos = e.Pos,
                Yaw = e.Yaw,
                Phase = e.Phase,
                Tint = e.Tint,
                Scale = e.Scale,
                Speed01 = Mathf.Clamp01(e.Speed / RunSpeed),
                Kind = e.Kind
            };

            if (_corpseCount < _corpses.Length) _corpseCount++;
            else _corpseHead = (_corpseHead + 1) % _corpses.Length;
        }

        /// <summary>
        /// Menggambar bangkai dan menggugurkan yang sudah habis. Dipanggil dari LateUpdate di
        /// antara musuh hidup dan Draw — bangkai memakai renderer & batch yang SAMA, jadi
        /// lima puluh kematian sedetik tidak menambah satu pun draw call.
        /// </summary>
        void DrawBurningCorpses(float dt)
        {
            int alive = 0;

            for (int i = 0; i < _corpseCount; i++)
            {
                int at = (_corpseHead + i) % _corpses.Length;
                var c = _corpses[at];

                c.Age += dt;
                if (c.Age >= BurnSeconds) continue;

                // Dipadatkan ke depan supaya yang gugur tidak meninggalkan lubang di ring.
                _corpses[(_corpseHead + alive) % _corpses.Length] = c;
                alive++;

                int model = ModelOf(c.Kind);

                // Pengangkatan kapsul yang sama dengan musuh hidup — bangkai kapsul yang tiba-tiba
                // amblas setengah badan saat mati akan terbaca sebagai jatuh ke tanah, bukan terbakar.
                var drawAt = c.Pos;
                if (_models[model] == null) drawAt.y += BodyHeight * 0.5f * c.Scale;

                _renderers[model].Add(drawAt, c.Yaw, c.Phase, c.Tint, c.Scale,
                    c.Speed01, c.Age / BurnSeconds);
            }

            _corpseCount = alive;
        }

        // ---------- damage API ----------

        /// <param name="crit">
        /// Apakah pukulan ini hasil lemparan crit yang berhasil. Parameter OPSIONAL, dan itu
        /// disengaja: jalur damage di game ini ada belasan, dan mewajibkan tiap pemanggil
        /// menyebutkan crit berarti belasan berkas ikut berubah untuk sesuatu yang cuma dibaca
        /// popup. Yang lupa menyebut dapat "bukan crit", dan itu jawaban yang benar untuk DoT,
        /// sentuhan musuh, reaksi, dan detonator.
        /// </param>
        public void Damage(Enemy e, float damage, StatusDefinition status, float duration,
            int points = 1, bool allowReaction = true, string sourceName = null,
            Vector3? origin = null, bool crit = false)
        {
            if (e == null || !e.Alive) return;

            // Yang sedang di bawah tanah kebal. Dicek di sini, bukan di tiap pemanggil, karena
            // SEMUA jalur damage -- area, rantai, peluru, DoT, detonator -- bermuara di fungsi ini.
            // Satu tempat berarti tidak ada skill yang bisa lupa.
            if (e.Boss != null && e.Pos.y < BuriedDepth) return;

            float dealt = damage * DamageTakenMultiplier(e);

            if (dealt > 0f)
            {
                OnDamage?.Invoke(sourceName ?? "?", dealt);

                // Dicatat di sini, di corong yang dilewati SEMUA jalur damage, bukan di tiap
                // pemanggil. Satu tempat berarti tidak ada skill yang bisa lupa memunculkan
                // bar HP korbannya.
                e.HurtSeen = Time.unscaledTime;

                var tint = TintFor(sourceName);
                OnEnemyDamaged?.Invoke(e.Pos, dealt, e.MaxHp, tint, crit);
                OnEnemyHit?.Invoke(e.Pos, sourceName, tint, crit);
            }

            if (status != null) ApplyStatus(e, status, duration, points, allowReaction, origin);
            else Paint(e);

            // Ruas boss tidak punya HP sendiri: apa pun yang mengenainya masuk ke satu kolam, dan
            // ruasnya baru hilang lewat badan yang memendek. Mengurangi HP ruas di sini berarti
            // ular bisa terputus di tengah, dan ekornya melayang lepas dari kepalanya.
            if (e.Boss != null)
            {
                e.Boss.TakeDamage(dealt);
                return;
            }

            e.Hp -= dealt;

            // ---- REGENERASI EKOR (pemicu) ----
            // Luka yang menembus ambang membuatnya kabur memulihkan diri. Dicek di corong
            // yang dilewati SEMUA jalur damage — sebab yang sama dengan HurtSeen di atas —
            // dan hanya saat masih hidup: regen bukan pengganti mati.
            if (e.Hp > 0f && e.Kind != null && e.Kind.RegenBelow > 0f && e.RegenUsesLeft > 0 &&
                e.RegenLeft <= 0f && e.Hp < e.MaxHp * e.Kind.RegenBelow)
            {
                e.RegenUsesLeft--;
                e.RegenLeft = e.Kind.RegenSeconds;

                // Serudukan yang sedang berjalan batal — kabur dan melesat tidak bisa barengan.
                e.ChargePhase = 0;
                e.ChargeLeft = 0f;
            }

            if (e.Hp <= 0f) Kill(e);
        }

        public void DamageArea(Vector3 center, float radius, float damage, StatusDefinition status,
            float duration, int points = 1, bool allowReaction = true, string sourceName = null,
            bool crit = false)
        {
            float sqrRadius = radius * radius;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                Vector3 d = e.Pos - center;
                d.y = 0f;
                if (d.sqrMagnitude <= sqrRadius) Damage(e, damage, status, duration, points, allowReaction, sourceName, center, crit);
            }
        }

        /// <summary>
        /// Melukai CINCIN, bukan cakram: yang lebih dekat dari <paramref name="inner"/> dilewati.
        ///
        /// Ada karena gelombang yang melebar harus menagih tiap musuh SEKALI, di detik tepinya
        /// menyapunya. Memakai <see cref="DamageArea"/> berulang kali dengan radius yang membesar
        /// akan menagih musuh di tengah setiap frame — sebuah skill "buka ruang" akan diam-diam
        /// jadi damage terbesar di buku, dan besarnya tergantung framerate.
        /// </summary>
        public void DamageRing(Vector3 center, float inner, float outer, float damage,
            StatusDefinition status, float duration, int points = 1, bool allowReaction = true,
            string sourceName = null, bool crit = false)
        {
            if (outer <= 0f) return;

            float sqrInner = Mathf.Max(0f, inner) * Mathf.Max(0f, inner);
            float sqrOuter = outer * outer;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                Vector3 d = e.Pos - center;
                d.y = 0f;

                float sqr = d.sqrMagnitude;
                if (sqr <= sqrInner || sqr > sqrOuter) continue;

                Damage(e, damage, status, duration, points, allowReaction, sourceName, center, crit);
            }
        }

        /// <summary>
        /// Melontar CINCIN. Pasangan <see cref="DamageRing"/>, dengan alasan yang sama: gelombang
        /// yang melebar harus mendorong tiap musuh sekali, di detik tepinya lewat. Memakai
        /// <see cref="Push"/> tiap frame akan menumpuk dorongan pada yang berdiri di tengah sampai
        /// mereka terlempar keluar layar.
        /// </summary>
        public void PushRing(Vector3 center, float inner, float outer, float force)
        {
            if (outer <= 0f) return;

            float sqrInner = Mathf.Max(0f, inner) * Mathf.Max(0f, inner);
            float sqrOuter = outer * outer;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                Vector3 d = e.Pos - center;
                d.y = 0f;

                float sqr = d.sqrMagnitude;
                if (sqr <= sqrInner || sqr > sqrOuter) continue;

                Vector3 away = sqr < 0.01f
                    ? new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f).normalized
                    : d / Mathf.Sqrt(sqr);

                e.Knock += away * force;
            }
        }

        /// <summary>
        /// Musuh pertama yang tersapu RUAS GARIS <paramref name="from"/> → <paramref name="to"/>,
        /// dengan ukuran badan masing-masing musuh, bukan satu angka datar.
        ///
        /// Ini menutup dua lubang sekaligus di jalur peluru, dan keduanya cuma kelihatan sebagai
        /// satu gejala: "sudah nembak tapi musuhnya tidak kena".
        ///
        /// <b>Satu — peluru melompati musuh.</b> Dulu peluru dipindah dulu sejauh satu langkah,
        /// baru ditanya "ada musuh di sekitar posisi barumu?". Itu uji TITIK di posisi akhir, jadi
        /// yang dilewati di tengah langkah tidak pernah ditanyakan sama sekali. Peluru melaju 16
        /// unit/detik: di 60 fps satu langkah 0,27 unit dan radius 0,75 masih menutupinya, tapi di
        /// 24 fps langkahnya 0,66 — dan satu frame tersendat saja peluru sudah menyeberangi musuh
        /// tanpa sekali pun diuji. Makin berat gamenya, makin sering tembakan "tembus". Menyapu
        /// ruasnya membuat kecepatan frame tidak lagi ikut menentukan siapa yang kena.
        ///
        /// <b>Dua — musuh yang menempel di pangkal.</b> Jaraknya diukur ke titik terdekat di ruas
        /// yang sudah DIJEPIT ke [0, panjang], jadi tutup pangkalnya ikut menguji. Musuh yang
        /// berdiri menempel di badan pemain kena di frame pertama; sebelumnya ia berada di
        /// belakang posisi peluru setelah langkah pertama dan lolos begitu saja.
        /// </summary>
        /// <param name="probeRadius">Ukuran benda yang menyapu — badan pelurunya sendiri.</param>
        public Enemy FirstAlongSegment(Vector3 from, Vector3 to, float probeRadius, Enemy skip)
        {
            Vector3 seg = to - from;
            seg.y = 0f;

            float len = seg.magnitude;
            Vector3 heading = len > 0.0001f ? seg / len : Vector3.zero;

            Enemy best = null;
            float bestAlong = float.MaxValue;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive || e == skip) continue;

                Vector3 d = e.Pos - from;
                d.y = 0f;

                // Dijepit ke ruasnya, jadi ini kapsul dan bukan sinar tak berujung: musuh di depan
                // ujung langkah belum kena, musuh di belakang pangkal juga tidak — tapi yang PERSIS
                // di pangkal kena, dan itulah musuh yang menempel di pemain.
                float along = len > 0.0001f ? Mathf.Clamp(Vector3.Dot(d, heading), 0f, len) : 0f;
                if (along >= bestAlong) continue;

                float reach = probeRadius + HitRadius(e);
                if ((d - heading * along).sqrMagnitude > reach * reach) continue;

                bestAlong = along;
                best = e;
            }

            return best;
        }

        /// <summary>
        /// Musuh PERTAMA yang tertusuk sinar dari <paramref name="from"/> ke arah
        /// <paramref name="dir"/>, dalam radius <paramref name="radius"/> dari garisnya.
        ///
        /// Satu lintasan linear, bukan sampling titik demi titik. Bedanya bukan gaya melainkan
        /// ongkos: sinar memantul dua puluh kali per cast, dan menanyakan "siapa yang terdekat"
        /// di dua puluh titik per ruas berarti dua puluh kali dua puluh kali seluruh gerombolan —
        /// di 500 musuh itu 200 ribu perbandingan untuk satu tembakan.
        /// </summary>
        /// <param name="hitDistance">Jarak sepanjang sinar ke musuh yang ketemu. Tidak berarti apa-apa kalau hasilnya null.</param>
        public Enemy FirstAlongRay(Vector3 from, Vector3 dir, float maxDistance, float radius,
            Enemy skip, out float hitDistance)
        {
            hitDistance = maxDistance;

            Vector3 heading = dir;
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.0001f) return null;
            heading.Normalize();

            Enemy best = null;
            float bestAlong = maxDistance;
            float sqrRadius = radius * radius;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive || e == skip) continue;

                Vector3 d = e.Pos - from;
                d.y = 0f;

                // Di belakang pangkalnya tidak dihitung: sinar yang menyambar ke belakang bukan
                // sinar, itu ledakan.
                float along = Vector3.Dot(d, heading);
                if (along <= 0f || along >= bestAlong) continue;

                // Jarak tegak lurus ke garisnya. Kuadrat, jadi tidak ada akar di dalam loop.
                float side = (d - heading * along).sqrMagnitude;
                if (side > sqrRadius) continue;

                bestAlong = along;
                best = e;
            }

            if (best != null) hitDistance = bestAlong;
            return best;
        }

        /// <summary>
        /// Melontarkan semua musuh di dalam radius MENJAUH dari titik pusat. Tidak melukai —
        /// yang dibeli pemain di sini adalah ruang, bukan kill.
        /// </summary>
        public void Push(Vector3 center, float radius, float force)
        {
            float sqrRadius = radius * radius;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                Vector3 d = e.Pos - center;
                d.y = 0f;

                float sqr = d.sqrMagnitude;
                if (sqr > sqrRadius) continue;

                // Yang berdiri persis di pusat tidak punya arah menjauh. Dilempar acak, bukan
                // dibiarkan diam — kalau tidak, justru musuh yang paling menempel yang selamat
                // dari skill yang seluruh gunanya melepaskan diri dari mereka.
                Vector3 away = sqr < 0.01f
                    ? new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f).normalized
                    : d / Mathf.Sqrt(sqr);

                // Yang dekat terlempar paling keras. Gelombangnya jadi terbaca sebagai berasal
                // dari satu titik, bukan sebagai kotak yang semuanya bergeser bersamaan.
                float falloff = 1f - Mathf.Sqrt(sqr) / radius;
                e.Knock += away * (force * Mathf.Max(0.25f, falloff));
            }
        }

        /// <summary>
        /// Mengangkat semua musuh di dalam radius. Selama terangkat mereka sepenuhnya lumpuh.
        /// Durasi di-REFRESH, bukan ditumpuk, supaya dua puting beliung yang tumpang tindih tidak
        /// mengunci lapangan selamanya.
        /// </summary>
        public void Lift(Vector3 center, float radius, float duration)
        {
            float sqrRadius = radius * radius;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                Vector3 d = e.Pos - center;
                d.y = 0f;
                if (d.sqrMagnitude > sqrRadius) continue;

                e.LiftTimer = Mathf.Max(e.LiftTimer, duration);
            }
        }

        /// <summary>
        /// Where to drop an area skill: the thickest part of the crowd within reach.
        ///
        /// It scores GRID CELLS, not enemies. The old version asked every enemy in range how many
        /// neighbours it had, which is n squared, and a spatial index does not rescue it — by the
        /// time a wave has converged, the whole swarm is standing inside one blast radius, so any
        /// "who is near me" query still touches everyone. Measured: 1.95 ms at 154 enemies, and
        /// wrapping a grid around the same question made it 4.85 ms at 200.
        ///
        /// Scoring cells instead costs the same whether ten enemies are alive or a thousand: the
        /// work is bounded by how many cells the caster can reach, and enemy count only ever shows
        /// up as an integer already counted during the movement pass.
        /// </summary>
        public Enemy BestCluster(Vector3 from, float maxDistance, float clusterRadius)
        {
            float sqrMax = maxDistance * maxDistance;

            // How many cells out to sum for one blast. Cells are 4 units, so a 6 unit radius reaches
            // roughly two of them in each direction.
            int block = Mathf.Clamp(Mathf.CeilToInt(clusterRadius / HashCell), 1, 4);

            int reach = Mathf.Clamp(Mathf.CeilToInt(maxDistance / HashCell), 1, HashSide);
            int originX = Mathf.Clamp((int)((from.x - _hashOrigin.x + HashExtent) / HashCell), 0, HashSide - 1);
            int originZ = Mathf.Clamp((int)((from.z - _hashOrigin.z + HashExtent) / HashCell), 0, HashSide - 1);

            int minX = Mathf.Max(0, originX - reach), maxX = Mathf.Min(HashSide - 1, originX + reach);
            int minZ = Mathf.Max(0, originZ - reach), maxZ = Mathf.Min(HashSide - 1, originZ + reach);

            int bestScore = 0;
            int bestCell = -1;

            for (int cz = minZ; cz <= maxZ; cz++)
            {
                for (int cx = minX; cx <= maxX; cx++)
                {
                    // Empty cells cannot be the centre of anything worth hitting.
                    if (_cellCount[cz * HashSide + cx] == 0) continue;

                    int score = 0;
                    int bz0 = Mathf.Max(0, cz - block), bz1 = Mathf.Min(HashSide - 1, cz + block);
                    int bx0 = Mathf.Max(0, cx - block), bx1 = Mathf.Min(HashSide - 1, cx + block);

                    for (int bz = bz0; bz <= bz1; bz++)
                    {
                        for (int bx = bx0; bx <= bx1; bx++) score += _cellCount[bz * HashSide + bx];
                    }

                    if (score <= bestScore) continue;

                    var candidate = PickFromCell(cz * HashSide + cx, from, sqrMax);
                    if (candidate == null) continue;

                    bestScore = score;
                    bestCell = cz * HashSide + cx;
                }
            }

            // Fall back to the closest enemy: better to fire at something than to hold the cast.
            return bestCell < 0 ? Nearest(from, maxDistance) : PickFromCell(bestCell, from, sqrMax);
        }

        /// <summary>Damages everything inside a rectangle running from origin along dir.</summary>
        public void DamageLine(Vector3 origin, Vector3 dir, float length, float halfWidth,
            float damage, StatusDefinition status, float duration, int points = 1,
            string sourceName = null, bool crit = false)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                Vector3 d = e.Pos - origin;
                d.y = 0f;

                float along = Vector3.Dot(d, dir);
                if (along < 0f || along > length) continue;

                float side = d.x * dir.z - d.z * dir.x;
                if (Mathf.Abs(side) > halfWidth) continue;

                Damage(e, damage, status, duration, points, true, sourceName, origin, crit);
            }
        }

        /// <summary>
        /// Which way to run. Every enemy inside <paramref name="radius"/> pushes the caster away,
        /// harder the closer it is; the sum is the escape direction.
        ///
        /// A crowd on one side gives a strong, obvious answer. A crowd on ALL sides cancels out and
        /// returns near zero — that is deliberate, and it is how being surrounded kills you.
        /// </summary>
        public Vector3 CrowdPressure(Vector3 at, float radius)
        {
            if (radius <= 0f) return Vector3.zero;

            Vector3 push = Vector3.zero;
            float sqrRadius = radius * radius;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                Vector3 d = at - e.Pos;
                d.y = 0f;

                float sqr = d.sqrMagnitude;
                if (sqr > sqrRadius || sqr < 0.0001f) continue;

                float distance = Mathf.Sqrt(sqr);
                push += d / distance * (1f - distance / radius);
            }

            return push.sqrMagnitude > 1f ? push.normalized : push;
        }

        /// <summary>
        /// Musuh terdekat yang TIDAK ADA di dalam <paramref name="skip"/>.
        ///
        /// Bedanya dengan <see cref="NearestExcluding"/> bukan kenyamanan melainkan benar-salah.
        /// Mengecualikan satu musuh cukup untuk peluru yang memantul SEKALI. Untuk sinar yang
        /// memantul delapan belas kali, itu menghasilkan ping-pong: dari A ia memilih B, dari B ia
        /// memilih A, dan seluruh pantulannya habis bolak-balik di sepasang musuh yang sama.
        /// Terukur sebelum diperbaiki — dua puluh titik lintasan, hanya DUA posisi berbeda.
        /// </summary>
        public Enemy NearestOutside(Vector3 from, float maxDistance, Enemy[] skip, int skipCount)
        {
            Enemy best = null;
            float bestSqr = maxDistance * maxDistance;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive || Contains(skip, skipCount, e)) continue;

                Vector3 d = e.Pos - from;
                d.y = 0f;

                float sqr = d.sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = e;
            }

            return best;
        }

        /// <summary>Versi <see cref="FirstAlongRay"/> yang mengecualikan sekumpulan musuh sekaligus.</summary>
        public Enemy FirstAlongRayOutside(Vector3 from, Vector3 dir, float maxDistance, float radius,
            Enemy[] skip, int skipCount, out float hitDistance)
        {
            hitDistance = maxDistance;

            Vector3 heading = dir;
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.0001f) return null;
            heading.Normalize();

            Enemy best = null;
            float bestAlong = maxDistance;
            float sqrRadius = radius * radius;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive || Contains(skip, skipCount, e)) continue;

                Vector3 d = e.Pos - from;
                d.y = 0f;

                float along = Vector3.Dot(d, heading);
                if (along <= 0f || along >= bestAlong) continue;

                float side = (d - heading * along).sqrMagnitude;
                if (side > sqrRadius) continue;

                bestAlong = along;
                best = e;
            }

            if (best != null) hitDistance = bestAlong;
            return best;
        }

        static bool Contains(Enemy[] list, int count, Enemy e)
        {
            for (int i = 0; i < count; i++)
            {
                if (list[i] == e) return true;
            }

            return false;
        }

        /// <summary>Nearest living enemy that is not <paramref name="skip"/>. For ricochets.</summary>
        public Enemy NearestExcluding(Vector3 from, float maxDistance, Enemy skip)
        {
            Enemy best = null;
            float bestSqr = maxDistance * maxDistance;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive || e == skip) continue;

                Vector3 d = e.Pos - from;
                d.y = 0f;

                float sqr = d.sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = e;
            }

            return best;
        }

        /// <summary>
        /// Blows up every enemy carrying <paramref name="statusIndex"/>, harder the more points it
        /// has stacked, then strips the status off.
        ///
        /// This is the payoff half of a two-skill combo: one skill spends its whole cast smearing
        /// a cheap ailment around, and this turns all of it into damage at once. It deliberately
        /// scales on POINTS rather than on enemy count, so stacking an ailment deep is worth more
        /// than spreading it thin.
        /// </summary>
        public int DetonateStatus(int statusIndex, float damagePerPoint, float splashRadius,
            int maxBlasts, string sourceName, System.Action<Vector3, int> onBlast,
            bool crit = false)
        {
            if (statusIndex < 0 || maxBlasts <= 0) return 0;

            int blasts = 0;

            // Snapshot first: detonating damages, damage can kill, and killing mutates the pool.
            int count = _pool.Count;

            for (int i = 0; i < count && blasts < maxBlasts; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                int slot = SlotOf(e, statusIndex);
                if (slot < 0) continue;

                int points = e.Slots[slot].Points;
                if (points <= 0) continue;

                // Consumed by the blast — a mark cannot be cashed in twice.
                e.Slots[slot].Def = -1;
                Paint(e);

                Vector3 at = e.Pos;
                blasts++;
                onBlast?.Invoke(at, points);

                DamageArea(at, splashRadius, damagePerPoint * points, null, 0f, 1, false, sourceName, crit);
            }

            return blasts;
        }

        public Enemy Nearest(Vector3 from, float maxDistance)
        {
            Enemy best = null;
            float bestSqr = maxDistance * maxDistance;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                Vector3 d = e.Pos - from;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = e;
            }

            return best;
        }

        /// <summary>
        /// Walks a chain the way a chain is supposed to walk: the first link is the nearest enemy
        /// to <paramref name="from"/>, and every link after that hops from the enemy just hit to the
        /// nearest one it has not touched yet, within <paramref name="jumpRange"/>.
        ///
        /// This replaces "grab the N nearest enemies to the caster", which never actually travelled
        /// anywhere — a chain skill could not reach past its own range no matter how tightly the
        /// swarm was packed, and there was nothing to draw a bolt between.
        /// </summary>
        /// <returns>How many links were made. Positions are in <paramref name="buffer"/> order.</returns>
        /// <param name="alreadyTaken">
        /// Entries at the front of <paramref name="buffer"/> that are already spoken for. Forked
        /// lightning passes the running total here, so a second branch cannot re-hit what the first
        /// one already struck — which is what makes the branches visibly spread apart instead of
        /// all crawling down the same line of enemies.
        /// </param>
        public int ChainFrom(Vector3 from, float firstRange, float jumpRange, Enemy[] buffer,
            int maxHits, int alreadyTaken = 0)
        {
            int found = alreadyTaken;
            int limit = Mathf.Min(alreadyTaken + maxHits, buffer.Length);

            Vector3 at = from;
            float reach = firstRange;

            while (found < limit)
            {
                Enemy best = null;
                float bestSqr = reach * reach;

                for (int i = 0; i < _pool.Count; i++)
                {
                    var e = _pool[i];
                    if (!e.Alive) continue;

                    bool taken = false;
                    for (int k = 0; k < found; k++)
                    {
                        if (buffer[k] != e) continue;
                        taken = true;
                        break;
                    }

                    if (taken) continue;

                    Vector3 d = e.Pos - at;
                    d.y = 0f;
                    float sqr = d.sqrMagnitude;
                    if (sqr >= bestSqr) continue;

                    bestSqr = sqr;
                    best = e;
                }

                if (best == null) break;

                buffer[found++] = best;
                at = best.Pos;
                reach = jumpRange;
            }

            return found;
        }

        /// <summary>Fills <paramref name="buffer"/> with the closest living enemies. Returns how many.</summary>
        public int NearestMany(Vector3 from, float maxDistance, Enemy[] buffer)
        {
            int found = 0;
            float sqrMax = maxDistance * maxDistance;

            for (int slot = 0; slot < buffer.Length; slot++)
            {
                Enemy best = null;
                float bestSqr = sqrMax;

                for (int i = 0; i < _pool.Count; i++)
                {
                    var e = _pool[i];
                    if (!e.Alive) continue;

                    bool taken = false;
                    for (int k = 0; k < found; k++)
                    {
                        if (buffer[k] != e) continue;
                        taken = true;
                        break;
                    }

                    if (taken) continue;

                    Vector3 d = e.Pos - from;
                    d.y = 0f;
                    float sqr = d.sqrMagnitude;
                    if (sqr >= bestSqr) continue;

                    bestSqr = sqr;
                    best = e;
                }

                if (best == null) break;
                buffer[found++] = best;
            }

            return found;
        }
    }
}
