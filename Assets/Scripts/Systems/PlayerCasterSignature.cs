using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// The casts that are not "damage in a shape": escape, defence, control, and the few attacks
    /// with a signature timing of their own.
    ///
    /// They live in their own partial because they earn their keep differently. Every skill in the
    /// main file answers "how much, how wide, how often" — these answer "what does the player DO
    /// when the plan has already failed". A book with nine ways to deal damage and no way to get
    /// out of a crowd only has one decision in it, and the player makes it once at wave one.
    /// </summary>
    public partial class PlayerCaster
    {
        // ---------- perisai ----------

        /// <summary>Sisa serapan. Dimakan lebih dulu oleh <c>Drain</c>, sebelum HP.</summary>
        public float Shield { get; private set; }

        public float ShieldRemaining => _shieldTimer;

        float _shieldTimer;

        /// <summary>
        /// Gelembung perisai yang menempel di pemain SELAMA serapannya hidup — satu-satunya efek
        /// skill yang umurnya diikat ke state, bukan ke cast. Perisai yang habis dimakan damage
        /// harus kehilangan gelembungnya di frame yang sama, atau pemain membaca perlindungan
        /// yang sudah tidak ada.
        /// </summary>
        Transform _wardVfx;

        GameObject _wardVfxSrc;

        // ---------- pecahan mengambang ----------

        class Orb
        {
            public Transform T;
            public float Angle;

            /// <summary>Sudah lepas dari kepala dan sedang meluncur.</summary>
            public bool Flying;

            public Vector3 Dir;
            public float Speed;
            public float Life;
            public float Damage;
            public StatusDefinition Status;
            public float StatusDuration;
            public int Points;
            public string SourceName;
            public float Trigger;
            public bool Active;

            /// <summary>Efek partikel yang menjadi badan pecahan, dilepas ke kolam saat pensiun.</summary>
            public Transform Vfx;

            public GameObject VfxSrc;
        }

        // ---------- hantaman beraba-aba ----------

        class Strike
        {
            public Transform Ring;
            public Vector3 Target;
            public float Remaining;
            public float Radius;
            public float Damage;
            public StatusDefinition Status;
            public float StatusDuration;
            public int Points;
            public string SourceName;
            public Color Tint;
            public bool Active;

            /// <summary>Prefab semburan detik hantaman — dibawa karena Strike tidak menyimpan Def.</summary>
            public GameObject VfxPrefab;

            public float VfxScale;
        }

        // ---------- bola menggelinding ----------

        class Boulder
        {
            public Transform T;
            public Vector3 Dir;
            public float Speed;
            public float Radius;
            public float Travelled;
            public float MaxTravel;
            public float Damage;
            public StatusDefinition Status;
            public float StatusDuration;
            public int Points;
            public string SourceName;

            /// <summary>Efek yang menggelinding bersama bola. Mengikuti posisi, bukan putarannya.</summary>
            public Transform Vfx;

            public GameObject VfxSrc;

            /// <summary>Yang sudah dilindas, supaya satu bola tidak menagih musuh yang sama berkali-kali.</summary>
            public readonly List<EnemyManager.Enemy> Hit = new List<EnemyManager.Enemy>(64);

            public bool Active;
        }

        // ---------- puting beliung ----------

        class Twister
        {
            public Transform T;
            public Vector3 Pos;
            public float Radius;
            public float Remaining;
            public float TickTimer;
            public float Damage;
            public float Pull;
            public float LiftDuration;
            public StatusDefinition Status;
            public float StatusDuration;
            public int Points;
            public string SourceName;
            public float Heading;
            public float Drift;
            public bool Active;

            /// <summary>
            /// Efek partikel milik skill, kalau ada. Disimpan bersama PREFAB SUMBERNYA karena
            /// puting beliung diambil dari pool: yang barusan milik Tornado bisa dipakai lagi
            /// oleh Maelstrom, dan tanpa membandingkan sumbernya efek lama ikut terbawa.
            ///
            /// Digantung terpisah dari <see cref="T"/> — badan primitifnya diberi skala tidak
            /// seragam (lebar × 2,4 tinggi), dan anak apa pun ikut terpelintir bersamanya.
            /// </summary>
            public Transform Vfx;

            public GameObject VfxSource;
        }

        readonly List<Orb> _orbs = new List<Orb>();
        readonly List<Strike> _strikes = new List<Strike>();
        readonly List<Boulder> _boulders = new List<Boulder>();
        readonly List<Twister> _twisters = new List<Twister>();

        const float TwisterTick = 0.25f;

        // =====================================================================================
        //  cast
        // =====================================================================================

        /// <summary>
        /// Menyiapkan muatan sebelum dibutuhkan. Pecahannya mengambang dan menunggu; yang dibeli
        /// pemain di sini adalah waktu, bukan damage — dayanya sudah dibayar sebelum gerombolan
        /// datang, bukan saat sudah kewalahan.
        /// </summary>
        bool CastOrbit(CompiledSpell spell, PieceDefinition def)
        {
            int wanted = Mathf.Max(1, def.Hits + BonusHits);

            // Menolak menambah kalau langit-langitnya sudah penuh. Tanpa ini dia terus membakar mana
            // untuk pecahan yang tidak pernah terpakai selama lapangan lengang.
            int held = 0;
            for (int i = 0; i < _orbs.Count; i++)
            {
                if (_orbs[i].Active && !_orbs[i].Flying) held++;
            }

            if (held >= wanted * 2) return false;

            float damage = spell.Damage * BuffDamageMul * RollCrit();
            float baseAngle = Random.Range(0f, Mathf.PI * 2f);

            for (int i = 0; i < wanted; i++)
            {
                var o = TakeOrb();

                o.Angle = baseAngle + i * (Mathf.PI * 2f / wanted);
                o.Flying = false;
                o.Speed = Mathf.Max(4f, def.TravelSpeed);
                o.Life = 0f;
                o.Damage = damage;
                o.Status = def.AppliedStatus;
                o.StatusDuration = def.StatusDuration;
                o.Points = AilmentPoints(def);
                o.SourceName = def.DisplayName;
                o.Trigger = Mathf.Max(2f, spell.Range * BuffRangeMul);
                o.Active = true;

                Paint(o.T, def.Color);
                o.T.localScale = Vector3.one * 0.34f;
                o.T.gameObject.SetActive(true);

                // Badan pecahan diganti efek partikel; bolanya menciut jadi inti. Aturannya sama
                // dengan peluru — slotnya dipakai ulang lintas skill, jadi efek selalu lepas-pasang.
                if (o.Vfx != null)
                {
                    _vfx.Release(o.VfxSrc, o.Vfx);
                    o.Vfx = null;
                    o.VfxSrc = null;
                }

                if (def.CastVfx != null)
                {
                    o.Vfx = _vfx.Attach(def.CastVfx, o.T.position, Quaternion.identity,
                        def.CastVfxScale * 0.6f);
                    o.VfxSrc = def.CastVfx;
                }

                ShowBody(o.T, BodyVisible(def));
            }

            return true;
        }

        /// <summary>
        /// Lompat menjauh dari titik terpadat. Sengaja MENOLAK menyala saat lapangan lengang, jadi
        /// cooldown-nya masih utuh justru pada detik pemain benar-benar terkepung — itu satu-satunya
        /// detik skill ini ada gunanya.
        /// </summary>
        bool CastBlink(CompiledSpell spell, PieceDefinition def)
        {
            Vector3 away = _enemies.CrowdPressure(transform.position, spell.Range);
            if (away.sqrMagnitude < 0.0001f) return false;

            Vector3 from = transform.position;
            Vector3 to = from + away.normalized * Mathf.Max(3f, spell.Range * BuffRangeMul);
            to.y = from.y;

            // Jepitan ELIPS — geometri yang SAMA dengan PlayerMotor.Clamp. Dulu kotak
            // ±ArenaHalf, dan itu bug yang ditemukan audit 2026-08-10: blink ke arah sudut
            // mendarat di luar elips arena, lalu pada frame yang sama motor menyentaknya
            // radial balik ke tepi — pemain terlihat maju lalu DITARIK MUNDUR 2-5 unit,
            // sementara flash dan beam menandai titik yang tidak pernah dihuni.
            float nx = to.x / _balance.ArenaHalfX;
            float nz = to.z / _balance.ArenaHalfZ;
            float outside = nx * nx + nz * nz;

            if (outside > 1f)
            {
                float shrink = 1f / Mathf.Sqrt(outside);
                to.x *= shrink;
                to.z *= shrink;
            }

            transform.position = to;

            // Ditandai di kedua ujung. Tanpa jejak keberangkatan, teleport terbaca sebagai patah
            // frame, bukan sebagai perpindahan.
            SpawnFlash(from, 3.4f, 0.28f, def.Color);
            SpawnFlash(to, 3.4f, 0.28f, def.Color);
            _bolts.Beam(from + Vector3.up * 0.6f, to + Vector3.up * 0.6f, def.Color, 0.5f);
            _vfx.Burst(def.CastVfx, from, def.CastVfxScale);
            _vfx.Burst(def.CastVfx, to, def.CastVfxScale);

            if (def.GrantOnCast != null) ApplyBuff(def.GrantOnCast);
            return true;
        }

        /// <summary>Serapan berjangka waktu. Menolak menumpuk di atas perisai yang masih tebal.</summary>
        bool CastWard(CompiledSpell spell, PieceDefinition def)
        {
            float amount = spell.Damage * BuffDamageMul;
            if (Shield >= amount * 0.9f) return false;

            Shield = amount;
            _shieldTimer = Mathf.Max(1f, def.ZoneDuration);

            SpawnFlash(transform.position, 4.2f, 0.35f, def.Color);

            // Ward lain boleh menimpa: gelembung lama dilepas dulu supaya dua kubah tidak
            // bertumpuk di badan yang sama.
            if (_wardVfx != null && _wardVfxSrc != def.CastVfx)
            {
                _vfx.Release(_wardVfxSrc, _wardVfx);
                _wardVfx = null;
                _wardVfxSrc = null;
            }

            if (def.CastVfx != null && _wardVfx == null)
            {
                _wardVfx = _vfx.Attach(def.CastVfx, transform.position, Quaternion.identity,
                    def.CastVfxScale);
                _wardVfxSrc = def.CastVfx;
            }

            if (def.GrantOnCast != null) ApplyBuff(def.GrantOnCast);
            return true;
        }

        /// <summary>
        /// Satu kind untuk semua penguat diri. Bedanya Haste dan Fortify dan Amuk cuma aset buff
        /// yang ditunjuk, bukan cabang kode — kalau tiap penguat punya kind sendiri, menambah
        /// penguat berarti menyentuh dispatcher lagi, selamanya.
        /// </summary>
        bool CastSurge(PieceDefinition def)
        {
            if (def.GrantOnCast == null) return false;

            // Menolak menyegarkan buff yang baru saja dipasang. Tanpa ini dia menembak tiap kali
            // cooldown habis dan membakar mana untuk durasi yang toh sudah dimiliki pemain.
            if (!def.GrantOnCast.IsCharge && RemainingOf(def.GrantOnCast) > def.GrantOnCast.Duration * 0.4f)
            {
                return false;
            }

            ApplyBuff(def.GrantOnCast);
            SpawnFlash(transform.position, 3.6f, 0.3f, def.Color);
            _vfx.Burst(def.CastVfx, transform.position, def.CastVfxScale);
            return true;
        }

        /// <summary>Isi ulang mana. Menahan diri selama masih di atas separuh.</summary>
        bool CastRestore(CompiledSpell spell, PieceDefinition def)
        {
            if (Mana >= MaxMana * 0.6f) return false;

            // Nol berarti penuh. Nilai apa pun di atas nol dibaca sebagai jumlah tetap, jadi satu
            // kind ini melayani "isi penuh" dan "teguk kecil" tanpa cabang tambahan.
            Mana = spell.Damage <= 0f ? MaxMana : Mathf.Min(MaxMana, Mana + spell.Damage);

            SpawnFlash(transform.position, 3.8f, 0.32f, def.Color);
            _vfx.Burst(def.CastVfx, transform.position, def.CastVfxScale);
            if (def.GrantOnCast != null) ApplyBuff(def.GrantOnCast);
            return true;
        }

        /// <summary>
        /// Menandai tanah dulu, menghantam belakangan. Aba-abanya bukan hiasan: itu satu-satunya
        /// cast yang bisa MELESET, dan itulah harga yang dibayar untuk pukulan sebesar ini.
        /// </summary>
        bool CastSunStrike(CompiledSpell spell, PieceDefinition def)
        {
            var cluster = _enemies.BestCluster(transform.position, spell.Range, spell.Radius);
            if (cluster == null) return false;

            var s = TakeStrike();

            s.Target = cluster.Pos;
            s.Target.y = 0.06f;
            s.Radius = spell.Radius * BuffAreaMul;
            s.Remaining = Mathf.Max(0.15f, def.TelegraphDelay);
            s.Damage = spell.Damage * BuffDamageMul * RollCrit();
            s.Status = def.AppliedStatus;
            s.StatusDuration = def.StatusDuration;
            s.Points = AilmentPoints(def);
            s.SourceName = def.DisplayName;
            s.Tint = def.Color;
            s.VfxPrefab = def.CastVfx;
            s.VfxScale = VfxScale(def, s.Radius);
            s.Active = true;

            Paint(s.Ring, def.Color);
            s.Ring.position = s.Target;
            s.Ring.localScale = new Vector3(s.Radius * 2f, 0.02f, s.Radius * 2f);
            s.Ring.gameObject.SetActive(true);
            return true;
        }

        /// <summary>
        /// Menggelinding menembus gerombolan. Beda dari Line bukan pada bentuknya tapi pada
        /// waktunya: dia butuh sedetik untuk sampai, dan gerombolan sempat bergerak selama itu.
        /// </summary>
        bool CastRollingBall(CompiledSpell spell, PieceDefinition def)
        {
            var target = _enemies.BestCluster(transform.position, spell.Range, spell.Radius) ??
                         _enemies.Nearest(transform.position, spell.Range);

            if (target == null) return false;

            Vector3 dir = target.Pos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;

            var b = TakeBoulder();

            b.Dir = dir.normalized;
            b.Speed = Mathf.Max(3f, def.TravelSpeed);
            b.Radius = Mathf.Max(0.8f, spell.Radius * BuffAreaMul);
            b.Travelled = 0f;
            b.MaxTravel = spell.Range * BuffRangeMul;
            b.Damage = spell.Damage * BuffDamageMul * RollCrit();
            b.Status = def.AppliedStatus;
            b.StatusDuration = def.StatusDuration;
            b.Points = AilmentPoints(def);
            b.SourceName = def.DisplayName;
            b.Hit.Clear();
            b.Active = true;

            Paint(b.T, def.Color);
            b.T.position = transform.position + Vector3.up * (b.Radius * 0.5f);
            b.T.localScale = Vector3.one * (b.Radius * 1.4f);
            b.T.gameObject.SetActive(true);

            // Bola api menggantikan batu primitif. Batunya menciut jadi inti — dia yang melindas,
            // efek cuma menumpang di posisinya.
            if (b.Vfx != null)
            {
                _vfx.Release(b.VfxSrc, b.Vfx);
                b.Vfx = null;
                b.VfxSrc = null;
            }

            if (def.CastVfx != null)
            {
                b.Vfx = _vfx.Attach(def.CastVfx, b.T.position,
                    Quaternion.LookRotation(b.Dir), def.CastVfxScale * Mathf.Max(0.6f, b.Radius / 1.4f));
                b.VfxSrc = def.CastVfx;
            }

            ShowBody(b.T, BodyVisible(def));
            return true;
        }

        /// <summary>
        /// Menyeret, mengangkat, lalu menggerus. Nilainya bukan di angka damage-nya — musuh yang
        /// terangkat tidak berjalan dan tidak menembak, jadi ini satu-satunya skill yang benar-benar
        /// MEMBELI WAKTU buat pemain.
        /// </summary>
        bool CastVortex(CompiledSpell spell, PieceDefinition def)
        {
            var cluster = _enemies.BestCluster(transform.position, spell.Range, spell.Radius);
            if (cluster == null) return false;

            var t = TakeTwister();

            t.Pos = cluster.Pos;
            t.Pos.y = 0f;
            t.Radius = Mathf.Max(1.5f, spell.Radius * BuffAreaMul);
            t.Remaining = Mathf.Max(1f, def.ZoneDuration);
            t.TickTimer = 0f;
            t.Damage = spell.Damage * BuffDamageMul;
            t.Pull = Mathf.Max(1f, def.PushForce);
            t.LiftDuration = Mathf.Max(0.2f, def.LiftDuration);
            t.Status = def.AppliedStatus;
            t.StatusDuration = def.StatusDuration;
            t.Points = AilmentPoints(def);
            t.SourceName = def.DisplayName;
            t.Drift = def.ZoneDrift;
            t.Heading = Random.Range(0f, Mathf.PI * 2f);
            t.Active = true;

            // TABUNGNYA DIMATIKAN kalau skill ini punya efeknya sendiri.
            //
            // Dulu cuma ditipiskan jadi 22% — dan itu justru yang diprotes: tabung silinder
            // tembus pandang setinggi 2,4 unit MASIH TERLIHAT membungkus setiap tornado, dan
            // tidak ada partikel sebagus apa pun yang bisa menang melawan bentuk primitif yang
            // menyelimutinya. Tornado adalah contoh yang disebut namanya.
            var tone = def.Color;
            tone.a = 1f;

            Paint(t.T, tone);
            t.T.position = t.Pos + Vector3.up * 1.2f;
            t.T.localScale = new Vector3(t.Radius * 1.1f, 2.4f, t.Radius * 1.1f);
            t.T.gameObject.SetActive(true);
            ShowBody(t.T, BodyVisible(def));

            AttachTwisterVfx(t, def);
            return true;
        }

        /// <summary>
        /// Memasang (atau menukar) efek partikel milik skill pada satu puting beliung. Dipanggil
        /// TIAP cast — termasuk yang mengambil badan dari pool, karena pemilik sebelumnya bisa
        /// skill lain.
        /// </summary>
        void AttachTwisterVfx(Twister t, PieceDefinition def)
        {
            if (t.VfxSource != def.CastVfx)
            {
                if (t.Vfx != null) Destroy(t.Vfx.gameObject);
                t.Vfx = null;
                t.VfxSource = def.CastVfx;

                if (def.CastVfx != null)
                {
                    var go = Instantiate(def.CastVfx, _fxRoot);
                    go.name = "CastVfx " + def.DisplayName;
                    t.Vfx = go.transform;
                }
            }

            if (t.Vfx == null) return;

            // Radius 3 unit = skala 1, aturan yang sama dengan kubangan Zone.
            t.Vfx.localScale = Vector3.one * (def.CastVfxScale * Mathf.Max(0.35f, t.Radius / 3f));
            t.Vfx.position = t.Pos;
            t.Vfx.gameObject.SetActive(true);
        }

        /// <summary>Membuka ruang. Damage-nya ada tapi bukan itu yang dibeli.</summary>
        bool CastForcePush(CompiledSpell spell, PieceDefinition def)
        {
            float radius = spell.Radius * BuffAreaMul;
            if (_enemies.Nearest(transform.position, radius) == null) return false;

            _enemies.Push(transform.position, radius, def.PushForce);

            if (spell.Damage > 0f)
            {
                _enemies.DamageArea(transform.position, radius,
                    spell.Damage * BuffDamageMul * RollCrit(),
                    def.AppliedStatus, def.StatusDuration, AilmentPoints(def), true, def.DisplayName);
            }

            SpawnFlash(transform.position, radius * 2.2f, 0.28f, def.Color);
            _vfx.Burst(def.CastVfx, transform.position, VfxScale(def, radius));
            if (def.GrantOnCast != null) ApplyBuff(def.GrantOnCast);
            return true;
        }

        // =====================================================================================
        //  tick
        // =====================================================================================

        void TickSignature(float dt)
        {
            TickShield(dt);
            TickOrbs(dt);
            TickStrikes(dt);
            TickBoulders(dt);
            TickTwisters(dt);
        }

        void TickShield(float dt)
        {
            // Gelembungnya dievaluasi SEBELUM guard: perisai bisa habis dimakan damage (bukan cuma
            // waktu), dan jalur itu tidak lewat sini — satu-satunya tempat yang pasti berjalan
            // tiap frame adalah tick ini.
            if (Shield <= 0f)
            {
                if (_wardVfx != null)
                {
                    _vfx.Release(_wardVfxSrc, _wardVfx);
                    _wardVfx = null;
                    _wardVfxSrc = null;
                }

                return;
            }

            if (_wardVfx != null) _wardVfx.position = transform.position;

            _shieldTimer -= dt;
            if (_shieldTimer > 0f) return;

            Shield = 0f;
            _shieldTimer = 0f;
        }

        void TickOrbs(float dt)
        {
            Vector3 head = transform.position + Vector3.up * 1.9f;

            for (int i = 0; i < _orbs.Count; i++)
            {
                var o = _orbs[i];
                if (!o.Active) continue;

                if (!o.Flying)
                {
                    // Menempel ke pemain, bukan ke titik dunia: kalau pemain melompat pakai Blink,
                    // muatannya ikut. Muatan yang tertinggal di seberang peta tidak ada gunanya.
                    o.Angle += 2.2f * dt;
                    o.T.position = head + new Vector3(Mathf.Cos(o.Angle), 0f, Mathf.Sin(o.Angle)) * 0.85f;
                    if (o.Vfx != null) o.Vfx.position = o.T.position;

                    var prey = _enemies.Nearest(transform.position, o.Trigger);
                    if (prey == null) continue;

                    Vector3 dir = prey.Pos - o.T.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 0.0001f) continue;

                    o.Dir = dir.normalized;
                    o.Flying = true;
                    o.Life = 2.4f;
                    continue;
                }

                o.Life -= dt;
                if (o.Life <= 0f)
                {
                    Retire(o);
                    continue;
                }

                o.T.position += o.Dir * (o.Speed * dt);
                if (o.Vfx != null) o.Vfx.position = o.T.position;

                var hit = _enemies.NearestExcluding(o.T.position, 0.8f, null);
                if (hit == null) continue;

                _enemies.Damage(hit, o.Damage, o.Status, o.StatusDuration, o.Points, true, o.SourceName);
                SpawnFlash(o.T.position, 1.6f, 0.16f, Color.white);
                Retire(o);
            }
        }

        void TickStrikes(float dt)
        {
            for (int i = 0; i < _strikes.Count; i++)
            {
                var s = _strikes[i];
                if (!s.Active) continue;

                s.Remaining -= dt;

                if (s.Remaining > 0f)
                {
                    // Cincinnya mengetat menuju detik hantaman. Aba-aba yang diam tidak memberi tahu
                    // pemain BERAPA LAMA lagi, dan itu separuh gunanya.
                    float tighten = Mathf.Clamp01(s.Remaining * 2f);
                    float scale = s.Radius * 2f * Mathf.Lerp(1f, 1.45f, tighten);
                    s.Ring.localScale = new Vector3(scale, 0.02f, scale);
                    continue;
                }

                _enemies.DamageArea(s.Target, s.Radius, s.Damage,
                    s.Status, s.StatusDuration, s.Points, true, s.SourceName);

                _bolts.Beam(s.Target + Vector3.up * 14f, s.Target, s.Tint, s.Radius * 0.7f);
                SpawnFlash(s.Target, s.Radius * 2.4f, 0.35f, s.Tint);
                _vfx.Burst(s.VfxPrefab, s.Target, s.VfxScale);

                s.Active = false;
                s.Ring.gameObject.SetActive(false);
            }
        }

        void TickBoulders(float dt)
        {
            for (int i = 0; i < _boulders.Count; i++)
            {
                var b = _boulders[i];
                if (!b.Active) continue;

                float step = b.Speed * dt;
                b.Travelled += step;

                if (b.Travelled >= b.MaxTravel)
                {
                    b.Active = false;
                    b.T.gameObject.SetActive(false);
                    SpawnFlash(b.T.position, b.Radius * 2.6f, 0.3f, Color.white);

                    if (b.Vfx != null)
                    {
                        _vfx.Release(b.VfxSrc, b.Vfx);
                        b.Vfx = null;
                        b.VfxSrc = null;
                    }

                    continue;
                }

                b.T.position += b.Dir * step;
                b.T.Rotate(Vector3.Cross(Vector3.up, b.Dir), step * 220f, Space.World);
                if (b.Vfx != null) b.Vfx.position = b.T.position;

                Sweep(b);
            }
        }

        /// <summary>
        /// Melindas siapa pun yang belum pernah dilindas bola ini. Daftar korbannya per-bola dan
        /// bukan per-frame: tanpa itu bola yang lambat menagih musuh yang sama tiap frame, dan
        /// damage aslinya jadi bergantung pada framerate.
        /// </summary>
        void Sweep(Boulder b)
        {
            var victim = _enemies.NearestExcluding(b.T.position, b.Radius, null);
            if (victim == null) return;

            for (int i = 0; i < b.Hit.Count; i++)
            {
                if (b.Hit[i] == victim) return;
            }

            b.Hit.Add(victim);
            _enemies.Damage(victim, b.Damage, b.Status, b.StatusDuration, b.Points, true, b.SourceName);
        }

        void TickTwisters(float dt)
        {
            for (int i = 0; i < _twisters.Count; i++)
            {
                var t = _twisters[i];
                if (!t.Active) continue;

                t.Remaining -= dt;
                if (t.Remaining <= 0f)
                {
                    t.Active = false;
                    t.T.gameObject.SetActive(false);

                    // Dimatikan, bukan dihancurkan: cast berikutnya dari skill yang sama
                    // memakainya lagi tanpa membayar Instantiate.
                    if (t.Vfx != null) t.Vfx.gameObject.SetActive(false);
                    continue;
                }

                if (t.Drift > 0f)
                {
                    t.Heading += Random.Range(-1.4f, 1.4f) * dt;
                    t.Pos.x += Mathf.Cos(t.Heading) * t.Drift * dt;
                    t.Pos.z += Mathf.Sin(t.Heading) * t.Drift * dt;
                }

                t.T.position = t.Pos + Vector3.up * 1.2f;
                t.T.Rotate(Vector3.up, 900f * dt, Space.World);

                // Efeknya ikut berjalan. Kalau tidak, puting beliungnya berdiri diam sementara
                // seretan dan angkatannya berjalan pergi — dan yang terbaca dua hal berbeda.
                if (t.Vfx != null) t.Vfx.position = t.Pos;

                t.TickTimer -= dt;
                if (t.TickTimer > 0f) continue;

                t.TickTimer = TwisterTick;

                // Gaya negatif = seretan. Push sudah tahu cara memberi arah menjauh dari satu titik,
                // dan membaliknya jauh lebih jujur daripada menyalin seluruh loop itu lagi.
                _enemies.Push(t.Pos, t.Radius, -t.Pull);

                // Yang DILEWATI ikut terangkat, bukan cuma yang persis di titik matanya —
                // permintaan pemilik project, dan memang itu yang membuatnya terbaca sebagai
                // puting beliung alih-alih sebagai lingkaran damage yang kebetulan berjalan.
                //
                // Tetap TIDAK seluruh radius: bagian luar hanya menyeret. Kalau semuanya ikut
                // terangkat, satu cast mematikan seisi layar dan tidak ada lagi yang dilawan.
                _enemies.Lift(t.Pos, t.Radius * 0.7f, t.LiftDuration);

                _enemies.DamageArea(t.Pos, t.Radius, t.Damage,
                    t.Status, t.StatusDuration, t.Points, true, t.SourceName);
            }
        }

        // =====================================================================================
        //  kolam objek
        // =====================================================================================

        void Retire(Orb o)
        {
            o.Active = false;
            o.Flying = false;
            o.T.gameObject.SetActive(false);

            if (o.Vfx != null)
            {
                _vfx.Release(o.VfxSrc, o.Vfx);
                o.Vfx = null;
                o.VfxSrc = null;
            }
        }

        void Paint(Transform t, Color color)
        {
            _mpb.SetColor(BaseColorId, color);
            t.GetComponent<Renderer>().SetPropertyBlock(_mpb);
        }

        Orb TakeOrb()
        {
            for (int i = 0; i < _orbs.Count; i++)
            {
                if (!_orbs[i].Active) return _orbs[i];
            }

            var fresh = new Orb
            {
                T = Sleeping(NewFx(Fx != null ? Fx.Orb : null, PrimitiveType.Sphere, "Orb", true))
            };

            _orbs.Add(fresh);
            return fresh;
        }

        Strike TakeStrike()
        {
            for (int i = 0; i < _strikes.Count; i++)
            {
                if (!_strikes[i].Active) return _strikes[i];
            }

            // Telegraf memakai material penanda AOE, bukan unlit pekat: isi tipis supaya musuh
            // yang sedang diincar tetap terlihat, tepi tegas karena tepilah aba-abanya.
            var fresh = new Strike
            {
                Ring = Sleeping(NewFx(Fx != null ? Fx.StrikeRing : null,
                    PrimitiveType.Cylinder, "StrikeRing", false))
            };

            _strikes.Add(fresh);
            return fresh;
        }

        Boulder TakeBoulder()
        {
            for (int i = 0; i < _boulders.Count; i++)
            {
                if (!_boulders[i].Active) return _boulders[i];
            }

            var fresh = new Boulder
            {
                T = Sleeping(NewFx(Fx != null ? Fx.Boulder : null, PrimitiveType.Sphere, "Boulder", true))
            };

            _boulders.Add(fresh);
            return fresh;
        }

        Twister TakeTwister()
        {
            for (int i = 0; i < _twisters.Count; i++)
            {
                if (!_twisters[i].Active) return _twisters[i];
            }

            var fresh = new Twister
            {
                T = Sleeping(NewFx(Fx != null ? Fx.Twister : null, PrimitiveType.Cylinder, "Twister", true))
            };

            _twisters.Add(fresh);
            return fresh;
        }

        /// <summary>
        /// Lahir dalam keadaan mati. Keempat benda di atas dinyalakan oleh cast yang memakainya,
        /// dan yang lahir menyala akan berdiri diam di titik nol sampai cast pertama datang.
        /// </summary>
        static Transform Sleeping(Transform t)
        {
            t.gameObject.SetActive(false);
            return t;
        }

        /// <summary>Sisa detik sebuah buff, nol kalau tidak sedang aktif.</summary>
        float RemainingOf(BuffDefinition def)
        {
            var slots = def.IsDebuff ? _debuffs : _buffs;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Def == def) return slots[i].Remaining;
            }

            return 0f;
        }
    }
}
