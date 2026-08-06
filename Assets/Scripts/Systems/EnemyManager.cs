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
        public System.Action<Vector3, float, float> OnEnemyDamaged;

        public int Capacity => _balance != null ? _balance.MaxAliveEnemies : 200;

        /// <summary>How many living enemies currently carry each status, by database index.</summary>
        public int[] StatusCounts => _statusCounts;

        readonly List<Enemy> _pool = new List<Enemy>(256);
        readonly Color _baseColor = new Color(0.55f, 0.5f, 0.62f);

        Transform _player;
        PlayerCaster _caster;
        GameBalance _balance;
        ContentDatabase _db;
        EnemyRenderer _renderer;
        EnemyRenderer _shotRenderer;
        readonly Dictionary<EnemyArchetype, int> _shotTint = new Dictionary<EnemyArchetype, int>();
        readonly Dictionary<EnemyArchetype, int> _archetypeTint = new Dictionary<EnemyArchetype, int>();
        /// <summary>Saklar curang. Null di build normal, dan setiap pembacaan lewat propertinya.</summary>
        public DebugConfig Cheats;

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

        public void Init(Transform player, PlayerCaster caster, GameBalance balance, ContentDatabase database)
        {
            _player = player;
            _caster = caster;
            _balance = balance;
            _db = database;
            _statusCounts = new int[Mathf.Max(1, _db.Statuses.Count)];

            _renderer = new EnemyRenderer(EnemyRenderer.BorrowPrimitiveMesh(PrimitiveType.Capsule),
                BuildPalette(), Capacity, 0.55f, true);

            _shotRenderer = new EnemyRenderer(EnemyRenderer.BorrowPrimitiveMesh(PrimitiveType.Sphere),
                BuildShotPalette(), MaxShots, 0.3f, false);
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

        /// <summary>Ular yang sedang hidup, atau null. Satu saja — dua boss bukan boss.</summary>
        public BossSnake Boss { get; private set; }

        public bool BossActive => Boss != null && Boss.Alive;

        public void StartWave(int wave)
        {
            Wave = wave;

            // A wave is a COUNT. A clock made the player watch a countdown instead of the field,
            // and "how many are left" is the only number they can actually act on. The rate below
            // still exists, but it only decides how fast that count arrives.
            WaveTotal = Mathf.Max(1, Mathf.RoundToInt(
                _balance.EnemyCountFor(wave) * (Cheats != null ? Cheats.EnemyCountScale : 1f)));
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

            if (_balance.BossEveryWaves > 0 && wave % _balance.BossEveryWaves == 0) SpawnBoss(wave);

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

        void SpawnBoss(int wave)
        {
            var def = _db.Boss;
            if (def == null) return;

            Boss = new BossSnake();

            // Muncul di luar layar, arah acak, sama seperti musuh biasa — tapi lebih jauh, supaya
            // seluruh badannya sempat terurai sebelum terlihat.
            Vector3 at = SpawnPoint(1f);
            at.y = 0.9f;

            Boss.Begin(def, at, _balance.EnemyHpFor(wave) * def.HpMultiplier);
            SyncBossSegments();

            OnBossSpawned?.Invoke(Boss);
        }

        void TickBoss(float dt)
        {
            if (Boss == null) return;

            if (!Boss.Alive)
            {
                RetireBoss();
                return;
            }

            Boss.Tick(dt, _player.position, damage =>
            {
                _caster.TakeHit(damage);
                if (Boss.Def.Curse != null) _caster.ApplyDebuff(Boss.Def.Curse);
            });

            SyncBossSegments();
        }

        /// <summary>
        /// Menyamakan jumlah dan posisi ruas dengan keadaan ular sekarang.
        ///
        /// Panjangnya diturunkan dari HP, jadi memendeknya badan BUKAN efek terpisah yang perlu
        /// diurus — ia jatuh sendiri dari satu perhitungan, dan mustahil melenceng dari HP aslinya.
        /// </summary>
        void SyncBossSegments()
        {
            var segments = Boss.Segments;
            int wanted = Boss.WantedSegments();

            while (segments.Count > wanted)
            {
                int last = segments.Count - 1;
                var gone = segments[last];

                gone.Alive = false;
                gone.Boss = null;
                segments.RemoveAt(last);

                // Ruas yang lepas tetap memercik. Badan yang menyusut diam-diam tidak terbaca
                // sebagai kemajuan; percikannya yang memberi tahu pemain bahwa ia sedang menang.
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
                fresh.Boss = Boss;
                fresh.Alive = true;
                fresh.Hp = 1f;
                fresh.MaxHp = Boss.MaxHp;
                fresh.Speed = 0f;
                fresh.Flank = 0f;
                fresh.LiftTimer = 0f;
                fresh.Knock = Vector3.zero;
                fresh.ReactionLock = 0f;
                fresh.Phase = segments.Count * 0.4f;

                segments.Add(fresh);
            }

            var def = Boss.Def;

            for (int i = 0; i < segments.Count; i++)
            {
                var e = segments[i];

                e.BossIndex = i;
                e.Pos = Boss.SegmentPoint(i);
                e.GroundY = e.Pos.y;

                // Meruncing ke belakang, jadi kepala dan ekor bisa dibedakan dari siluetnya saja.
                float t = segments.Count <= 1 ? 0f : i / (float)(segments.Count - 1);
                e.Scale = i == 0 ? def.HeadScale : Mathf.Lerp(def.HeadScale * 0.72f, def.TailScale, t);

                Vector3 ahead = i == 0 ? Boss.HeadPos : Boss.SegmentPoint(i - 1);
                Vector3 forward = ahead - e.Pos;
                if (forward.sqrMagnitude > 0.0001f) e.Yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

                Paint(e);
            }
        }

        void RetireBoss()
        {
            var segments = Boss.Segments;

            for (int i = 0; i < segments.Count; i++)
            {
                segments[i].Alive = false;
                segments[i].Boss = null;
                OnKill?.Invoke(segments[i].Pos);
            }

            OnBossDied?.Invoke(Boss.HeadPos);
            Boss.End();
            Boss = null;
        }

        /// <summary>
        /// Mengosongkan lapangan tanpa menghitungnya sebagai kill. Buat ruang uji: menyusun ulang
        /// boneka tidak boleh mengotori Kills atau memicu drop.
        /// </summary>
        public void ClearField()
        {
            if (Boss != null)
            {
                Boss.End();
                Boss = null;
            }

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
        bool SpawnOne(float distanceScale = 1f)
        {
            var e = GetFree();
            if (e == null) return false;

            var kind = _db.RollArchetype(Wave);
            e.Kind = kind;

            e.Pos = SpawnPoint(distanceScale);
            e.MaxHp = _balance.EnemyHpFor(Wave) * (Cheats != null ? Cheats.EnemyHpScale : 1f);
            e.Speed = Random.Range(_balance.EnemySpeedMin, _balance.EnemySpeedMax) +
                      Wave * _balance.EnemySpeedPerWave;
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

            return new Vector3(from.x + dx * t, 0.55f, from.z + dz * t);
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
                            OnEnemyDamaged?.Invoke(e.Pos, dot, e.MaxHp);
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
                    _caster.TakeDamage(_balance.EnemyContactDps * dt);

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
            shot.Damage = from.Kind.AttackDamage;
            shot.Tint = _shotTint.TryGetValue(from.Kind, out int tint) ? tint : 0;

            // Just past its own range, so a shot the player outran expires instead of chasing.
            shot.Life = from.Kind.PreferredRange * 1.5f / speed;
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
                shot.Active = false;
            }
        }

        /// <summary>
        /// Hands the swarm to the renderer once per frame, after everything has settled. Enemies own
        /// no GameObject, so if this stops running they stop existing on screen.
        /// </summary>
        void LateUpdate()
        {
            if (_renderer == null) return;

            _renderer.Begin();

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                _renderer.Add(e.Pos, e.Yaw, e.Phase, e.Tint, e.Scale);
            }

            _renderer.Draw(Time.time);

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
        public int DrawBatches => _renderer != null ? _renderer.Batches : 0;

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
            OnKill?.Invoke(e.Pos);
        }

        // ---------- damage API ----------

        public void Damage(Enemy e, float damage, StatusDefinition status, float duration,
            int points = 1, bool allowReaction = true, string sourceName = null, Vector3? origin = null)
        {
            if (e == null || !e.Alive) return;

            float dealt = damage * DamageTakenMultiplier(e);

            if (dealt > 0f)
            {
                OnDamage?.Invoke(sourceName ?? "?", dealt);
                OnEnemyDamaged?.Invoke(e.Pos, dealt, e.MaxHp);
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
            if (e.Hp <= 0f) Kill(e);
        }

        public void DamageArea(Vector3 center, float radius, float damage, StatusDefinition status,
            float duration, int points = 1, bool allowReaction = true, string sourceName = null)
        {
            float sqrRadius = radius * radius;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                Vector3 d = e.Pos - center;
                d.y = 0f;
                if (d.sqrMagnitude <= sqrRadius) Damage(e, damage, status, duration, points, allowReaction, sourceName, center);
            }
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
            float damage, StatusDefinition status, float duration, int points = 1, string sourceName = null)
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

                Damage(e, damage, status, duration, points, true, sourceName, origin);
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
            int maxBlasts, string sourceName, System.Action<Vector3, int> onBlast)
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

                DamageArea(at, splashRadius, damagePerPoint * points, null, 0f, 1, false, sourceName);
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
