using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Gelombang kedua perilaku cast: bilah yang mengitari badan, bumerang, sinar memantul, menara,
    /// gelombang melebar, rudal pengejar, sinar pengunci, dan hujan hantaman.
    ///
    /// Dipisah dari <c>PlayerCasterSignature</c> karena keduanya menjawab pertanyaan berbeda. Yang
    /// di sana menjawab "apa yang pemain lakukan setelah rencananya gagal" — kabur, bertahan,
    /// mengendalikan. Yang di sini menjawab keluhan yang lain: seluruh buku memakai KATA KERJA yang
    /// sama, dan bintang lima cuma bintang tiga dengan angka lebih besar. Tiap kind di file ini
    /// sengaja mengubah di mana damage-nya lahir, kapan ia tiba, atau siapa yang menentukan
    /// arahnya — bukan seberapa besar angkanya.
    ///
    /// Aturan yang dipegang semuanya, sama dengan file lain:
    /// crit dilempar SEKALI per cast (bukan per musuh), <c>BuffDamageMul</c>/<c>BuffAreaMul</c>/
    /// <c>BuffRangeMul</c> dikalikan di titik pakai, dan tiap benda diambil dari kolam — tidak ada
    /// satu pun <c>Instantiate</c> di dalam loop per frame.
    /// </summary>
    public partial class PlayerCaster
    {
        // =====================================================================================
        //  bentuk data
        // =====================================================================================

        /// <summary>Bilah yang mengitari badan pemain. Satu Ring memegang beberapa bilah sekaligus.</summary>
        class Ring
        {
            public readonly List<Transform> Blades = new List<Transform>(8);
            public PieceDefinition Def;
            public float Angle;
            public float Spin;
            public float Radius;
            public float Remaining;
            public float TickTimer;
            public float TickInterval;
            public float Damage;
            public float Push;
            public StatusDefinition Status;
            public float StatusDuration;
            public int Points;
            public string SourceName;

            /// <summary>
            /// Efek per BILAH — satu salinan wrapper skill di tiap bilah, digeret TickRings
            /// bersama bilahnya. Dulu satu aura di badan, dan itu gagal dua kali: auranya
            /// sekali-main jadi mati duluan sementara cincinnya masih hidup, dan begitu
            /// BodyVisible menyembunyikan bilah primitif, yang tersisa dari skill ini cuma
            /// cakram penanda di tanah. Yang berputar sekarang efeknya sendiri.
            ///
            /// Aturannya sama dengan Boomerang/Seeker: wrapper-nya HARUS efek yang diam di
            /// tempat (orb, bola api loop) — efek yang jatuh sendiri akan melawan geretan ini.
            /// </summary>
            public readonly List<Transform> BladeVfx = new List<Transform>(8);

            public GameObject BladeVfxSrc;

            /// <summary>Cakram penanda jangkauan, di kaki pemain. Radius DAMAGE, bukan radius efek.</summary>
            public Transform Ring_;

            public bool Active;
        }

        /// <summary>Bumerang: satu benda dengan dua kaki — pergi lurus, pulang mengejar pemain.</summary>
        class Wing
        {
            public Transform T;
            public Vector3 Dir;
            public float Travelled;
            public float MaxTravel;
            public float Speed;
            public float Radius;
            public float Damage;
            public StatusDefinition Status;
            public float StatusDuration;
            public int Points;
            public string SourceName;

            /// <summary>Undian crit dilempar saat cast dan MENUMPANG sampai pukulannya mendarat.</summary>
            public bool Crit;

            /// <summary>Sedang pulang. Kaki pulangnya membidik posisi pemain SEKARANG, bukan tempat ia dilempar.</summary>
            public bool Returning;

            /// <summary>
            /// Korban di kaki yang sedang berjalan. DIKOSONGKAN saat berbalik — itu yang membuat
            /// bumerang menagih dua kali di jalur yang sama, dan itulah seluruh alasan ia ada.
            /// </summary>
            public readonly List<EnemyManager.Enemy> Hit = new List<EnemyManager.Enemy>(32);

            public Transform Vfx;
            public GameObject VfxSrc;
            public bool Active;
        }

        /// <summary>
        /// Coretan sinar: SATU LineRenderer untuk seluruh lintasan pantul, bukan satu per ruas.
        ///
        /// Sinar 20 pantulan yang menyewa 20 bolt akan menguras <see cref="BoltPool"/> (batasnya 48)
        /// dalam satu tembakan, dan petir rantai yang berbagi kolam itu akan berkedip mati. Satu
        /// renderer berisi 21 titik menggambar coretan yang sama persis, memudar sebagai satu benda,
        /// dan ongkosnya satu.
        /// </summary>
        class Scribble
        {
            public LineRenderer Line;
            public float Life;
            public float MaxLife;
            public Color Tint;
            public bool Active;
        }

        /// <summary>Menara yang menembak sendiri dari tempat ia ditanam.</summary>
        class Totem
        {
            public Transform T;
            public PieceDefinition Def;
            public Vector3 Pos;
            public float Remaining;
            public float FireTimer;
            public float Interval;
            public float Range;
            public float Damage;
            public int Shots;
            public Transform Vfx;
            public GameObject VfxSrc;
            public bool Active;
        }

        /// <summary>Cincin yang melebar. Hanya TEPINYA yang melukai.</summary>
        class Swell
        {
            public Transform T;
            public Vector3 Centre;
            public float Radius;
            public float LastRadius;
            public float MaxRadius;
            public float Speed;
            public float Damage;
            public float Push;
            public StatusDefinition Status;
            public float StatusDuration;
            public int Points;
            public string SourceName;

            /// <summary>Undian crit dilempar saat cast dan MENUMPANG sampai pukulannya mendarat.</summary>
            public bool Crit;
            public bool Active;
        }

        /// <summary>Rudal yang melengkung mengejar satu musuh.</summary>
        class Missile
        {
            public Transform T;
            public Vector3 Dir;
            public EnemyManager.Enemy Target;
            public float Speed;
            public float Turn;
            public float Life;
            public float Damage;
            public StatusDefinition Status;
            public float StatusDuration;
            public int Points;
            public string SourceName;

            /// <summary>Undian crit dilempar saat cast dan MENUMPANG sampai pukulannya mendarat.</summary>
            public bool Crit;
            public Transform Vfx;
            public GameObject VfxSrc;
            public bool Active;
        }

        /// <summary>Sinar yang mengunci satu musuh dan terus membakar selama tersambung.</summary>
        class Leash
        {
            public LineRenderer Line;
            public EnemyManager.Enemy Target;
            public float Remaining;
            public float TickTimer;
            public float TickInterval;
            public float Damage;
            public float Range;
            public float Pull;
            public StatusDefinition Status;
            public float StatusDuration;
            public int Points;
            public string SourceName;
            public Color Tint;
            public float Width;

            /// <summary>Prefab semburan di ujung sinar. Dibawa karena Leash tidak menyimpan Def.</summary>
            public GameObject VfxPrefab;

            public float VfxScale;
            public bool Active;
        }

        readonly List<Ring> _rings = new List<Ring>(4);
        readonly List<Wing> _wings = new List<Wing>(8);
        readonly List<Scribble> _scribbles = new List<Scribble>(8);
        readonly List<Totem> _totems = new List<Totem>(6);
        readonly List<Swell> _swells = new List<Swell>(8);
        readonly List<Missile> _missiles = new List<Missile>(24);
        readonly List<Leash> _leashes = new List<Leash>(4);

        /// <summary>
        /// Titik pantul maksimum dalam satu tembakan sinar.
        ///
        /// Bukan batas desain melainkan batas keselamatan: <see cref="PieceDefinition.Bounces"/>
        /// datang dari aset DAN dari segel yang menambahnya, jadi tidak ada satu pun angka di file
        /// aset yang bisa menjamin lintasannya berhenti. Ini yang menjamin.
        /// </summary>
        const int MaxRicochetPoints = 40;

        readonly Vector3[] _ricochetPath = new Vector3[MaxRicochetPoints];

        // =====================================================================================
        //  cast
        // =====================================================================================

        /// <summary>
        /// Bilah yang mengitari badan. Satu-satunya skill yang jangkauannya adalah TUBUH pemain,
        /// jadi ia membayar pemain karena didekati — kebalikan dari seluruh isi buku.
        ///
        /// Menolak menyala kalau bilahnya masih berputar kencang, dengan alasan yang sama persis
        /// dengan <c>CastSurge</c>: tanpa itu ia menembak tiap cooldown habis dan membakar mana
        /// untuk durasi yang toh sudah dimiliki pemain.
        /// </summary>
        bool CastOrbital(CompiledSpell spell, PieceDefinition def)
        {
            float life = Mathf.Max(1f, def.ZoneDuration);

            for (int i = 0; i < _rings.Count; i++)
            {
                if (_rings[i].Active && _rings[i].Def == def && _rings[i].Remaining > life * 0.35f)
                {
                    return false;
                }
            }

            var ring = TakeRing();
            // Jepitan atasnya tetap 8: tiap bilah itu satu Transform yang di-tick tiap frame, dan
            // segel yang menumpuk tanpa plafon akan melahirkannya tanpa batas.
            int blades = Mathf.Clamp(def.Hits + BonusHits, 1, 8);

            ring.Def = def;
            ring.Radius = Mathf.Max(1.2f, spell.Radius * BuffAreaMul);
            ring.Remaining = life;
            ring.TickInterval = Mathf.Max(0.1f, def.ZoneTickInterval);
            ring.TickTimer = 0f;

            // Damage TIDAK dikalikan crit. Bilahnya menagih berkali-kali selama hidupnya, dan satu
            // lemparan crit yang berlaku untuk seluruh durasi berarti separuh cast-nya bernilai dua
            // kali lipat cast lain — aturan "satu lemparan per cast" jadi bohong justru di skill
            // yang paling sering menagih. Sama dengan Zone dan Vortex.
            ring.Damage = spell.Damage * BuffDamageMul;
            ring.Push = def.PushForce;
            ring.Status = def.AppliedStatus;
            ring.StatusDuration = def.StatusDuration;
            ring.Points = AilmentPoints(def);
            ring.SourceName = def.DisplayName;

            // Kecepatan putar diturunkan dari kecepatan jelajah dibagi radius, bukan diketik
            // sendiri: bilah di lingkaran besar dan lingkaran kecil harus terasa sama cepat DI
            // UJUNGNYA, dan sudut per detik yang sama membuat yang kecil terlihat panik.
            ring.Spin = Mathf.Max(1.2f, def.TravelSpeed / ring.Radius);
            ring.Angle = Random.Range(0f, Mathf.PI * 2f);
            ring.Active = true;

            EnsureBlades(ring, blades, def);

            // Penanda jangkauan. Digambar dari radius DAMAGE, tidak pernah dari skala efeknya —
            // itu seluruh gunanya: efek partikel sebuah Orbital hampir selalu jauh lebih lebar
            // dari yang benar-benar dilukai, dan tanpa cakram ini pemain membaca janji area yang
            // tidak akan pernah ditepati.
            if (ring.Ring_ == null)
            {
                ring.Ring_ = Sleeping(NewFx(Fx != null ? Fx.OrbitRing : null,
                    PrimitiveType.Cylinder, "OrbitRing", false));
            }

            var mark = def.Color;
            mark.a = 1f;
            Paint(ring.Ring_, mark);

            ring.Ring_.localScale = new Vector3(ring.Radius * 2f, 0.02f, ring.Radius * 2f);
            ring.Ring_.gameObject.SetActive(true);

            SpawnFlash(transform.position, ring.Radius * 1.6f, 0.22f, def.Color);
            return true;
        }

        /// <summary>
        /// Melepas semua efek bilah sebuah ring kembali ke pool. Dipanggil saat ring mati DAN
        /// saat ring dipakai ulang oleh skill lain — wrapper skill lama di bilah skill baru
        /// berarti Blade Dance yang tampil dengan bola api milik Ring of Ruin.
        /// </summary>
        void ReleaseBladeVfx(Ring ring)
        {
            for (int i = 0; i < ring.BladeVfx.Count; i++)
            {
                if (ring.BladeVfx[i] != null) _vfx.Release(ring.BladeVfxSrc, ring.BladeVfx[i]);
            }

            ring.BladeVfx.Clear();
            ring.BladeVfxSrc = null;
        }

        /// <summary>
        /// Menyiapkan sejumlah bilah untuk satu ring. Bilah dipakai ulang lintas cast — yang
        /// berubah cuma warnanya dan berapa yang dinyalakan.
        /// </summary>
        void EnsureBlades(Ring ring, int wanted, PieceDefinition def)
        {
            while (ring.Blades.Count < wanted)
            {
                var t = NewFx(Fx != null ? Fx.Blade : null, PrimitiveType.Cube, "Blade", true);
                t.localScale = new Vector3(0.75f, 0.14f, 0.22f);
                ring.Blades.Add(t);
            }

            // Bilah kubus dimatikan kalau slot Fx.Blade masih kosong DAN skillnya sudah punya
            // efek sendiri: delapan kubus putih mengitari badan pemain adalah bentuk placeholder
            // paling mencolok di seluruh layar. Begitu slotnya diisi bilah sungguhan, ia tampil.
            bool showBlades = BodyVisible(def);

            for (int i = 0; i < ring.Blades.Count; i++)
            {
                bool used = i < wanted;
                ring.Blades[i].gameObject.SetActive(used);

                if (!used) continue;

                Paint(ring.Blades[i], def.Color);
                ShowBody(ring.Blades[i], showBlades);
            }

            // Efek per bilah. Dilepas dulu kalau ring ini bekas skill lain atau jumlah bilahnya
            // berubah — menambal selisihnya saja berarti menyimpan campuran dua skill.
            if (ring.BladeVfxSrc != def.CastVfx || ring.BladeVfx.Count != wanted)
            {
                ReleaseBladeVfx(ring);
            }

            if (def.CastVfx != null && ring.BladeVfx.Count == 0)
            {
                for (int i = 0; i < wanted; i++)
                {
                    ring.BladeVfx.Add(_vfx.Attach(def.CastVfx, ring.Blades[i].position,
                        Quaternion.identity, def.CastVfxScale));
                }

                ring.BladeVfxSrc = def.CastVfx;
            }

            ring.TickInterval = Mathf.Max(0.1f, def.ZoneTickInterval);
        }

        /// <summary>
        /// Bumerang. Bedanya dari peluru bukan bentuknya melainkan siapa yang menentukan hasilnya:
        /// kaki pulangnya membidik posisi pemain SEKARANG, jadi gerombolan yang sedang mengejar
        /// berjalan masuk sendiri ke jalur pulang. Yang lari, tidak.
        /// </summary>
        bool CastBoomerang(CompiledSpell spell, PieceDefinition def)
        {
            var target = _enemies.Nearest(transform.position, spell.Range * BuffRangeMul);
            if (target == null) return false;

            Vector3 dir = target.Pos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;

            var w = TakeWing();

            w.Dir = dir.normalized;
            w.Travelled = 0f;
            w.MaxTravel = Mathf.Max(3f, spell.Range * BuffRangeMul);
            w.Speed = Mathf.Max(6f, def.TravelSpeed);
            w.Radius = Mathf.Max(0.7f, spell.Radius * BuffAreaMul);
            w.Damage = CritHit(spell, out bool wingCrit);
            w.Crit = wingCrit;
            w.Status = def.AppliedStatus;
            w.StatusDuration = def.StatusDuration;
            w.Points = AilmentPoints(def);
            w.SourceName = def.DisplayName;
            w.Returning = false;
            w.Hit.Clear();
            w.Active = true;

            Paint(w.T, def.Color);
            w.T.position = transform.position;
            w.T.localScale = new Vector3(w.Radius * 1.1f, 0.16f, w.Radius * 0.3f);
            w.T.gameObject.SetActive(true);

            Attach(ref w.Vfx, ref w.VfxSrc, def, w.T.position,
                Quaternion.LookRotation(w.Dir), def.CastVfxScale);

            ShowBody(w.T, BodyVisible(def));
            return true;
        }

        /// <summary>
        /// Sinar memantul. Seluruh lintasannya dihitung SEKARANG, bukan disimulasikan bergerak —
        /// laser memang seketika, dan menganimasikan perjalanannya cuma menunda damage yang sudah
        /// pasti mendarat sambil menambah satu benda yang harus di-tick.
        ///
        /// Urutan pencarian tiap ruas: musuh yang tertusuk sinar dulu; kalau kosong, tepi layar.
        /// Itu yang membuatnya tetap mencorat-coret di lapangan sepi — sinar yang mati begitu
        /// kehabisan musuh cuma jadi Line yang mahal.
        /// </summary>
        bool CastRicochet(CompiledSpell spell, PieceDefinition def)
        {
            float reach = Mathf.Max(4f, spell.Range * BuffRangeMul);

            var first = _enemies.Nearest(transform.position, reach);
            if (first == null) return false;

            Vector3 dir = first.Pos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            dir.Normalize();

            float damage = CritHit(spell, out bool crit);
            float beamRadius = Mathf.Max(0.45f, spell.Radius);
            float bounceReach = Mathf.Max(reach, def.BounceRange);

            ScreenBox(out Vector3 boxMin, out Vector3 boxMax, reach);

            Vector3 at = transform.position;
            at.y = 0.6f;

            _ricochetPath[0] = at;
            int points = 1;

            // Segmen = pantulan + 1. Yang dibatasi jumlah TITIK, bukan jumlah pantulan: segel
            // penambah pantulan boleh menumpuk tanpa plafon, dan array-nya tidak boleh ikut.
            int segments = Mathf.Clamp(def.Bounces + BonusBounces + 1, 1, MaxRicochetPoints - 1);

            // Setiap musuh yang sudah disambar cast INI, bukan cuma yang barusan.
            //
            // Mengecualikan satu saja sudah cukup untuk peluru yang memantul sekali, dan itu
            // aturan yang diwarisi dari sana — lalu terbukti salah di sini. Terukur: dua puluh
            // titik lintasan yang isinya hanya DUA posisi berbeda, karena sinarnya memilih A dari
            // B lalu B dari A sampai pantulannya habis. Yang tergambar bukan coretan melainkan
            // satu ruas pendek yang ditumpuk delapan belas kali.
            //
            // Efek sampingnya justru yang diinginkan: begitu musuh segar habis, sinarnya
            // TERPAKSA memantul di tepi layar — dan pantulan tepi itulah yang mencorat-coret.
            int struck = 0;

            for (int s = 0; s < segments; s++)
            {
                var hit = _enemies.FirstAlongRayOutside(at, dir, reach, beamRadius,
                    _chainBuffer, struck, out float distance);

                if (hit != null)
                {
                    // Dicatat SEBELUM dipakai mencari sasaran berikutnya, dan sebelum ia bisa mati
                    // oleh damage di bawah — slot pool musuh yang mati dipakai ulang, dan yang
                    // dicatat harus tetap menghalangi sinar ini sampai cast-nya selesai.
                    if (struck < _chainBuffer.Length) _chainBuffer[struck] = hit;
                    at = hit.Pos;
                    at.y = 0.6f;
                    _ricochetPath[points++] = at;

                    _enemies.Damage(hit, damage, def.AppliedStatus, def.StatusDuration,
                        AilmentPoints(def), true, def.DisplayName, null, crit);

                    SpawnFlash(hit.Pos, 1.5f, 0.12f, def.Color);

                    // Semburan cuma di beberapa titik pertama. Sinar OP memantul delapan belas
                    // kali, dan delapan belas semburan sekaligus menjenuhkan kolam VFX dalam satu
                    // tembakan — yang tersisa cuma kabut yang menutupi coretannya sendiri, dan
                    // coretan itulah seluruh isi skill ini.
                    if (struck < 6) _vfx.Burst(def.CastVfx, hit.Pos, def.CastVfxScale);

                    struck++;

                    // Membelok ke musuh berikutnya yang BELUM PERNAH disambar cast ini — sinar yang
                    // memantul ke musuh terbaca sebagai memburu; yang memantul acak terbaca rusak.
                    var next = _enemies.NearestOutside(at, bounceReach, _chainBuffer, struck);

                    if (next != null)
                    {
                        Vector3 toNext = next.Pos - at;
                        toNext.y = 0f;
                        if (toNext.sqrMagnitude > 0.0001f) dir = toNext.normalized;
                    }
                    else
                    {
                        dir = BounceInBox(ref at, dir, boxMin, boxMax, distance: 0f);
                    }

                    continue;
                }

                // Tidak ada yang tertusuk: berjalan sampai tepi layar, lalu memantul di sana.
                //
                // Daftar korban TIDAK dikosongkan di sini. Membiarkan sinar menyambar ulang musuh
                // yang sama setelah kembali dari tepi terdengar murah hati dan sebenarnya membunuh
                // skill ini: gerombolan terpadat selalu ada di satu tempat, jadi sinarnya akan
                // pulang ke situ terus dan berhenti menyeberangi layar.
                dir = BounceInBox(ref at, dir, boxMin, boxMax, reach);
                _ricochetPath[points++] = at;
            }

            DrawScribble(points, def.Color, beamRadius * 1.4f);

            // Dianggap gagal kalau tidak satu pun musuh kena: cooldown dan mana harus utuh saat
            // sinarnya cuma memantul di layar kosong.
            return struck > 0;
        }

        /// <summary>
        /// Menanam menara. Satu-satunya sumber damage yang tidak berangkat dari badan pemain —
        /// itu yang membuat "taruh di tengah gerombolan lalu lari" jadi permainan yang sah.
        /// </summary>
        bool CastTurret(CompiledSpell spell, PieceDefinition def)
        {
            var cluster = _enemies.BestCluster(transform.position, spell.Range * BuffRangeMul, spell.Radius) ??
                          _enemies.Nearest(transform.position, spell.Range * BuffRangeMul);

            if (cluster == null) return false;

            var t = TakeTotem();

            t.Def = def;
            t.Pos = new Vector3(cluster.Pos.x, 0f, cluster.Pos.z);
            t.Remaining = Mathf.Max(1.5f, def.ZoneDuration);
            t.Interval = Mathf.Max(0.15f, def.ZoneTickInterval);

            // Menembak SEKETIKA setelah ditanam. Menara yang menunggu satu interval dulu terasa
            // seperti gagal ditanam, dan di skill yang seluruh nilainya soal penempatan, detik
            // pertama itu yang paling dilihat pemain.
            t.FireTimer = 0f;

            // Jangkauan tembak menara diambil dari radius, bukan dari Range: Range sudah dipakai
            // untuk "sejauh mana boleh DITANAM", dan memakai satu angka untuk dua jarak berarti
            // menara yang bisa ditanam jauh otomatis juga menembak sejauh itu.
            t.Range = Mathf.Max(3f, spell.Radius * BuffRangeMul);
            t.Damage = spell.Damage * BuffDamageMul;
            t.Shots = Mathf.Clamp(def.Hits + BonusHits, 1, 6);
            t.Active = true;

            Paint(t.T, def.Color);
            t.T.position = t.Pos + Vector3.up * 0.8f;
            t.T.localScale = new Vector3(0.5f, 0.8f, 0.5f);
            t.T.gameObject.SetActive(true);

            Attach(ref t.Vfx, ref t.VfxSrc, def, t.Pos, Quaternion.identity, def.CastVfxScale);
            ShowBody(t.T, BodyVisible(def));

            SpawnFlash(t.Pos, 2.6f, 0.25f, def.Color);
            return true;
        }

        /// <summary>
        /// Gelombang yang melebar dari pemain, dan cuma TEPINYA yang melukai.
        ///
        /// Bedanya dari Nova ada di siapa yang kena: Nova mengisi lingkaran seketika, jadi apa pun
        /// di luar radiusnya selamat selamanya. Yang ini sampai ke tepi belakangan, jadi ia
        /// mengenai musuh yang saat cast masih di luar jangkauan mana pun di buku.
        /// </summary>
        bool CastShockwave(CompiledSpell spell, PieceDefinition def)
        {
            float max = Mathf.Max(2f, spell.Radius * BuffAreaMul);
            if (_enemies.Nearest(transform.position, max) == null) return false;

            var w = TakeSwell();

            w.Centre = new Vector3(transform.position.x, 0.06f, transform.position.z);
            w.Radius = 0.4f;
            w.LastRadius = 0f;
            w.MaxRadius = max;
            w.Speed = Mathf.Max(4f, def.TravelSpeed);
            w.Damage = CritHit(spell, out bool swellCrit);
            w.Crit = swellCrit;
            w.Push = def.PushForce;
            w.Status = def.AppliedStatus;
            w.StatusDuration = def.StatusDuration;
            w.Points = AilmentPoints(def);
            w.SourceName = def.DisplayName;
            w.Active = true;

            var tone = def.Color;
            tone.a = 1f;
            Paint(w.T, tone);

            w.T.position = w.Centre;
            w.T.localScale = new Vector3(w.Radius * 2f, 0.03f, w.Radius * 2f);
            w.T.gameObject.SetActive(true);

            _vfx.Burst(def.CastVfx, w.Centre, VfxScale(def, max));
            return true;
        }

        /// <summary>
        /// Rudal pengejar. Sasarannya dipilih lewat <c>ChainFrom</c> — bukan karena ini rantai,
        /// tapi karena itu satu-satunya cara di codebase ini meminta N musuh yang BERBEDA. Tanpa
        /// itu lima rudal semuanya mengejar musuh terdekat yang sama, dan empat di antaranya
        /// meledak di mayat.
        /// </summary>
        bool CastSeeker(CompiledSpell spell, PieceDefinition def)
        {
            int wanted = Mathf.Clamp(def.Hits + BonusHits, 1, 12);
            float reach = Mathf.Max(4f, spell.Range * BuffRangeMul);

            int found = _enemies.ChainFrom(transform.position, reach, _balance.ChainJumpRange,
                _chainBuffer, wanted);

            if (found <= 0) return false;

            float damage = CritHit(spell, out bool crit);
            float spread = Random.Range(0f, Mathf.PI * 2f);

            for (int i = 0; i < found; i++)
            {
                var m = TakeMissile();

                m.Target = _chainBuffer[i];

                // Berangkat menyamping, bukan lurus ke sasaran. Rudal yang langsung menghadap
                // targetnya bergerak persis seperti peluru biasa — lengkungannya, yang jadi
                // seluruh ciri khasnya, tidak pernah terlihat.
                float angle = spread + i * (Mathf.PI * 2f / found);
                m.Dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                m.Speed = Mathf.Max(6f, def.TravelSpeed);
                m.Turn = 5.5f;
                m.Life = 3.2f;
                m.Damage = damage;
                m.Status = def.AppliedStatus;
                m.StatusDuration = def.StatusDuration;
                m.Points = AilmentPoints(def);
                m.SourceName = def.DisplayName;
                m.Crit = crit;
                m.Active = true;

                Paint(m.T, def.Color);
                m.T.position = transform.position;
                m.T.localScale = Vector3.one * 0.32f;
                m.T.gameObject.SetActive(true);

                Attach(ref m.Vfx, ref m.VfxSrc, def, m.T.position,
                    Quaternion.LookRotation(m.Dir), def.CastVfxScale * 0.7f);

                ShowBody(m.T, BodyVisible(def));
            }

            return true;
        }

        /// <summary>
        /// Sinar pengunci: damage yang BERJALAN, bukan meletus. Satu-satunya jawaban di buku untuk
        /// satu musuh bernyawa tebal, sementara semua yang lain dituning untuk gerombolan.
        ///
        /// Menolak menambah sinar kedua dari skill yang sama — dua sinar yang mengunci musuh yang
        /// sama tidak terlihat sebagai apa pun, dan biayanya dua kali.
        /// </summary>
        bool CastTether(CompiledSpell spell, PieceDefinition def)
        {
            float reach = Mathf.Max(4f, spell.Range * BuffRangeMul);

            var prey = _enemies.Nearest(transform.position, reach);
            if (prey == null) return false;

            for (int i = 0; i < _leashes.Count; i++)
            {
                if (_leashes[i].Active && _leashes[i].SourceName == def.DisplayName) return false;
            }

            var l = TakeLeash();

            l.Target = prey;
            l.Remaining = Mathf.Max(1f, def.ZoneDuration);
            l.TickInterval = Mathf.Max(0.1f, def.ZoneTickInterval);
            l.TickTimer = 0f;
            l.Damage = spell.Damage * BuffDamageMul;
            l.Range = reach;
            l.Pull = def.PushForce;
            l.Status = def.AppliedStatus;
            l.StatusDuration = def.StatusDuration;
            l.Points = AilmentPoints(def);
            l.SourceName = def.DisplayName;
            l.Tint = def.Color;
            l.Width = Mathf.Max(0.12f, spell.Radius * 0.5f);
            l.VfxPrefab = def.CastVfx;
            l.VfxScale = def.CastVfxScale * 0.7f;
            l.Active = true;

            l.Line.enabled = true;
            l.Line.widthMultiplier = l.Width;
            l.Line.startColor = l.Line.endColor = def.Color;
            return true;
        }

        /// <summary>
        /// Hujan hantaman beraba-aba. Memakai kolam <c>Strike</c> yang sama dengan SunStrike —
        /// bentuk, cincin, dan aturan mendaratnya memang identik; yang berbeda cuma bahwa di sini
        /// ada beberapa, berurutan, dan berpencar.
        ///
        /// Itulah bedanya sebagai permainan: SunStrike menanyakan "sempat menyingkir tidak?" satu
        /// kali, yang ini menanyakannya berkali-kali di tempat berbeda — jadi menghindarinya adalah
        /// gerakan, bukan satu langkah.
        /// </summary>
        bool CastBarrage(CompiledSpell spell, PieceDefinition def)
        {
            var cluster = _enemies.BestCluster(transform.position, spell.Range * BuffRangeMul, spell.Radius);
            if (cluster == null) return false;

            int shots = Mathf.Clamp(def.Hits + BonusHits, 2, 12);
            float damage = CritHit(spell, out bool crit);
            float radius = Mathf.Max(0.8f, spell.Radius * BuffAreaMul);
            float gap = Mathf.Max(0.08f, def.ZoneTickInterval);

            // Sebarannya diukur dari radius hantamannya, bukan dari Range: memakai Range membuat
            // hujan berjangkauan jauh tersebar sampai tidak ada dua lingkaran yang bersentuhan,
            // dan yang tersisa cuma beberapa tembakan meleset yang kebetulan berbarengan.
            float scatter = radius * 2.2f;

            for (int i = 0; i < shots; i++)
            {
                var s = TakeStrike();

                float angle = Random.Range(0f, Mathf.PI * 2f);
                float away = Mathf.Sqrt(Random.value) * scatter;

                s.Target = new Vector3(
                    cluster.Pos.x + Mathf.Cos(angle) * away,
                    0.06f,
                    cluster.Pos.z + Mathf.Sin(angle) * away);

                s.Radius = radius;

                // Jendela aba-aba SAMA untuk semua tembakan; yang berbeda cuma kapan ia dibuka.
                // Kalau Total ikut menampung jeda antrean, tembakan kedua belas menghabiskan
                // detik demi detik mengisi dirinya pelan-pelan, dan hitungan mundur yang lajunya
                // berbeda-beda per lingkaran tidak bisa dibaca sebagai satu bahasa.
                s.Total = Mathf.Max(0.15f, def.TelegraphDelay);
                s.Remaining = s.Total + i * gap;
                s.Damage = damage;
                s.Crit = crit;
                s.Status = def.AppliedStatus;
                s.StatusDuration = def.StatusDuration;
                s.Points = AilmentPoints(def);
                s.SourceName = def.DisplayName;
                s.Tint = def.Color;
                s.VfxPrefab = def.CastVfx;
                s.VfxScale = VfxScale(def, radius);
                s.Active = true;

                PaintStrike(s, 0f);
                s.Ring.position = s.Target;
                s.Ring.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
                s.Ring.gameObject.SetActive(true);
            }

            return true;
        }

        // =====================================================================================
        //  tick
        // =====================================================================================

        void TickBehaviour(float dt)
        {
            TickRings(dt);
            TickWings(dt);
            TickScribbles(dt);
            TickTotems(dt);
            TickSwells(dt);
            TickMissiles(dt);
            TickLeashes(dt);
        }

        void TickRings(float dt)
        {
            Vector3 body = transform.position;

            for (int i = 0; i < _rings.Count; i++)
            {
                var r = _rings[i];
                if (!r.Active) continue;

                r.Remaining -= dt;

                if (r.Remaining <= 0f)
                {
                    r.Active = false;
                    for (int b = 0; b < r.Blades.Count; b++) r.Blades[b].gameObject.SetActive(false);
                    if (r.Ring_ != null) r.Ring_.gameObject.SetActive(false);

                    ReleaseBladeVfx(r);
                    continue;
                }

                r.Angle += r.Spin * dt;

                if (r.Ring_ != null) r.Ring_.position = new Vector3(body.x, 0.06f, body.z);

                int used = 0;
                for (int b = 0; b < r.Blades.Count; b++)
                {
                    if (!r.Blades[b].gameObject.activeSelf) continue;
                    used++;
                }

                int slot = 0;
                for (int b = 0; b < r.Blades.Count; b++)
                {
                    var blade = r.Blades[b];
                    if (!blade.gameObject.activeSelf) continue;

                    float a = r.Angle + slot * (Mathf.PI * 2f / Mathf.Max(1, used));
                    var offset = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));

                    // Menempel ke PEMAIN, bukan ke titik dunia. Kalau pemain berkedip menyeberang
                    // lapangan, bilahnya ikut — bilah yang tertinggal di seberang peta cuma
                    // menggambarkan skill yang mati diam-diam.
                    blade.position = body + offset * r.Radius + Vector3.up * 0.7f;
                    blade.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, offset));

                    // Efeknya digeret persis ke posisi bilah — bukan di-parent, karena instance
                    // pool dipakai bergantian dan parent yang menghilang membawa efeknya ikut mati.
                    if (slot < r.BladeVfx.Count && r.BladeVfx[slot] != null)
                    {
                        r.BladeVfx[slot].position = blade.position;
                    }

                    slot++;
                }

                r.TickTimer -= dt;
                if (r.TickTimer > 0f) continue;

                r.TickTimer = r.TickInterval;

                _enemies.DamageArea(body, r.Radius, r.Damage, r.Status, r.StatusDuration,
                    r.Points, true, r.SourceName);

                if (r.Push > 0f) _enemies.Push(body, r.Radius, r.Push);
            }
        }

        void TickWings(float dt)
        {
            for (int i = 0; i < _wings.Count; i++)
            {
                var w = _wings[i];
                if (!w.Active) continue;

                float step = w.Speed * dt;

                if (!w.Returning)
                {
                    w.Travelled += step;
                    w.T.position += w.Dir * step;

                    if (w.Travelled >= w.MaxTravel)
                    {
                        w.Returning = true;

                        // Daftar korban dikosongkan di titik balik, bukan disimpan. Itu yang
                        // membuat kaki pulang menagih lagi di jalur yang sama.
                        w.Hit.Clear();
                    }
                }
                else
                {
                    Vector3 home = transform.position - w.T.position;
                    home.y = 0f;

                    if (home.sqrMagnitude <= step * step + 0.6f)
                    {
                        RetireWing(w);
                        continue;
                    }

                    w.Dir = home.normalized;
                    w.T.position += w.Dir * step;
                }

                w.T.Rotate(Vector3.up, 900f * dt, Space.World);
                if (w.Vfx != null) w.Vfx.position = w.T.position;

                SweepWing(w);
            }
        }

        /// <summary>Melindas siapa pun yang belum kena di KAKI ini. Aturannya sama dengan bola menggelinding.</summary>
        void SweepWing(Wing w)
        {
            var victim = _enemies.NearestExcluding(w.T.position, w.Radius, null);
            if (victim == null) return;

            for (int i = 0; i < w.Hit.Count; i++)
            {
                if (w.Hit[i] == victim) return;
            }

            w.Hit.Add(victim);
            _enemies.Damage(victim, w.Damage, w.Status, w.StatusDuration, w.Points, true, w.SourceName);
            SpawnFlash(w.T.position, 1.3f, 0.12f, Color.white);
        }

        void RetireWing(Wing w)
        {
            w.Active = false;
            w.T.gameObject.SetActive(false);

            if (w.Vfx == null) return;

            _vfx.Release(w.VfxSrc, w.Vfx);
            w.Vfx = null;
            w.VfxSrc = null;
        }

        void TickScribbles(float dt)
        {
            for (int i = 0; i < _scribbles.Count; i++)
            {
                var s = _scribbles[i];
                if (!s.Active) continue;

                s.Life -= dt;

                if (s.Life <= 0f)
                {
                    s.Active = false;
                    s.Line.enabled = false;
                    continue;
                }

                var faded = s.Tint;
                faded.a = s.Tint.a * Mathf.Clamp01(s.Life / s.MaxLife);
                s.Line.startColor = s.Line.endColor = faded;
            }
        }

        void TickTotems(float dt)
        {
            for (int i = 0; i < _totems.Count; i++)
            {
                var t = _totems[i];
                if (!t.Active) continue;

                t.Remaining -= dt;

                if (t.Remaining <= 0f)
                {
                    t.Active = false;
                    t.T.gameObject.SetActive(false);

                    if (t.Vfx != null) t.Vfx.gameObject.SetActive(false);
                    continue;
                }

                t.FireTimer -= dt;
                if (t.FireTimer > 0f) continue;

                var prey = _enemies.Nearest(t.Pos, t.Range);

                // Tidak ada sasaran: pemicunya TIDAK di-reset penuh, cuma dicoba lagi sebentar
                // lagi. Menara yang mengulang seluruh intervalnya tiap kali gagal akan menembak
                // jauh lebih jarang dari yang tertulis begitu gerombolan mulai berpencar.
                if (prey == null)
                {
                    t.FireTimer = 0.15f;
                    continue;
                }

                t.FireTimer = t.Interval;

                for (int s = 0; s < t.Shots; s++)
                {
                    var mark = s == 0 ? prey : _enemies.NearestExcluding(t.Pos, t.Range, prey);
                    if (mark == null) mark = prey;

                    Vector3 dir = mark.Pos - t.Pos;
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 0.0001f) continue;

                    FireProjectileFrom(t.Pos + Vector3.up * 0.9f, dir.normalized,
                        t.Damage, t.Def, t.Def.Color);
                }
            }
        }

        void TickSwells(float dt)
        {
            for (int i = 0; i < _swells.Count; i++)
            {
                var w = _swells[i];
                if (!w.Active) continue;

                w.LastRadius = w.Radius;
                w.Radius += w.Speed * dt;

                // Cincinnya menagih SEKALI per musuh karena yang ditanyakan cuma pita antara radius
                // frame lalu dan radius sekarang. Musuh yang sudah dilewati tepinya ada di dalam
                // lingkaran, dan lingkaran dalam tidak pernah ditanya lagi.
                _enemies.DamageRing(w.Centre, w.LastRadius, w.Radius, w.Damage,
                    w.Status, w.StatusDuration, w.Points, true, w.SourceName, w.Crit);

                if (w.Push > 0f) _enemies.PushRing(w.Centre, w.LastRadius, w.Radius, w.Push);

                w.T.localScale = new Vector3(w.Radius * 2f, 0.03f, w.Radius * 2f);

                if (w.Radius < w.MaxRadius) continue;

                w.Active = false;
                w.T.gameObject.SetActive(false);
            }
        }

        void TickMissiles(float dt)
        {
            for (int i = 0; i < _missiles.Count; i++)
            {
                var m = _missiles[i];
                if (!m.Active) continue;

                m.Life -= dt;
                if (m.Life <= 0f)
                {
                    RetireMissile(m);
                    continue;
                }

                // Sasaran mati: mencari yang lain, bukan terbang lurus sampai umurnya habis.
                if (m.Target == null || !m.Target.Alive)
                {
                    m.Target = _enemies.Nearest(m.T.position, 12f);
                }

                if (m.Target != null)
                {
                    Vector3 want = m.Target.Pos - m.T.position;
                    want.y = 0f;

                    if (want.sqrMagnitude > 0.0001f)
                    {
                        // Membelok terbatas, bukan langsung menghadap. Belokan terbatas itulah
                        // yang membuat lintasannya melengkung — tanpa batas, rudal ini cuma peluru
                        // yang kebetulan tahu ke mana harus pergi.
                        m.Dir = Vector3.RotateTowards(m.Dir, want.normalized, m.Turn * dt, 0f);
                    }
                }

                m.T.position += m.Dir * (m.Speed * dt);

                if (m.Vfx != null)
                {
                    m.Vfx.position = m.T.position;
                    if (m.Dir.sqrMagnitude > 0.0001f) m.Vfx.rotation = Quaternion.LookRotation(m.Dir);
                }

                var hit = _enemies.NearestExcluding(m.T.position, 0.8f, null);
                if (hit == null) continue;

                _enemies.Damage(hit, m.Damage, m.Status, m.StatusDuration, m.Points, true,
                    m.SourceName, null, m.Crit);
                SpawnFlash(m.T.position, 1.6f, 0.16f, Color.white);
                RetireMissile(m);
            }
        }

        void RetireMissile(Missile m)
        {
            m.Active = false;
            m.Target = null;
            m.T.gameObject.SetActive(false);

            if (m.Vfx == null) return;

            _vfx.Release(m.VfxSrc, m.Vfx);
            m.Vfx = null;
            m.VfxSrc = null;
        }

        void TickLeashes(float dt)
        {
            Vector3 body = transform.position + Vector3.up * 0.9f;

            for (int i = 0; i < _leashes.Count; i++)
            {
                var l = _leashes[i];
                if (!l.Active) continue;

                l.Remaining -= dt;
                if (l.Remaining <= 0f)
                {
                    RetireLeash(l);
                    continue;
                }

                // Mengunci ulang saat sasarannya mati atau kabur keluar jangkauan. Sinar yang mati
                // bersama korban pertamanya akan padam di detik pertama tiap cast — justru saat
                // gerombolannya paling tipis dan musuhnya paling cepat mati.
                if (l.Target == null || !l.Target.Alive ||
                    (l.Target.Pos - transform.position).sqrMagnitude > l.Range * l.Range)
                {
                    l.Target = _enemies.Nearest(transform.position, l.Range);
                }

                if (l.Target == null)
                {
                    l.Line.enabled = false;
                    continue;
                }

                Vector3 head = l.Target.Pos + Vector3.up * 0.5f;

                l.Line.enabled = true;
                l.Line.positionCount = 2;
                l.Line.SetPosition(0, body);
                l.Line.SetPosition(1, head);

                l.TickTimer -= dt;
                if (l.TickTimer > 0f) continue;

                l.TickTimer = l.TickInterval;

                _enemies.Damage(l.Target, l.Damage, l.Status, l.StatusDuration, l.Points, true,
                    l.SourceName, transform.position);

                // Semburan di UJUNG sinar tiap denyut. Garis polos tanpa apa pun di ujungnya
                // terbaca sebagai penanda seleksi, bukan sebagai sesuatu yang sedang menggerus.
                _vfx.Burst(l.VfxPrefab, head, l.VfxScale);

                // Seretan negatif: Push sudah tahu memberi arah MENJAUH dari satu titik, jadi
                // membaliknya lebih jujur daripada menyalin seluruh loop itu lagi. Radius kecil
                // supaya yang tertarik cuma yang sedang diikat, bukan seisi lapangan.
                if (l.Pull > 0f) _enemies.Push(l.Target.Pos, 0.9f, -l.Pull);
            }
        }

        void RetireLeash(Leash l)
        {
            l.Active = false;
            l.Target = null;
            l.Line.enabled = false;
        }

        // =====================================================================================
        //  perkakas
        // =====================================================================================

        // Satu pengambil per kolam, bukan satu pengambil generik.
        //
        // Yang generik menuntut sebuah lambda pembuat di TIAP pemanggilan, dan lambda yang menyentuh
        // `this` (semuanya menyentuh: NewFx dan Sleeping milik instance) dialokasikan ulang tiap
        // cast. Enam skill di papan yang menembak beberapa kali per detik membayar sampah itu terus
        // menerus, dan sampah kecil yang lahir per frame persis bentuk yang menyebabkan patah GC.

        Ring TakeRing()
        {
            for (int i = 0; i < _rings.Count; i++)
            {
                if (!_rings[i].Active) return _rings[i];
            }

            var fresh = new Ring();
            _rings.Add(fresh);
            return fresh;
        }

        Wing TakeWing()
        {
            for (int i = 0; i < _wings.Count; i++)
            {
                if (!_wings[i].Active) return _wings[i];
            }

            var fresh = new Wing
            {
                T = Sleeping(NewFx(Fx != null ? Fx.Boomerang : null, PrimitiveType.Cube, "Boomerang", true))
            };

            _wings.Add(fresh);
            return fresh;
        }

        Totem TakeTotem()
        {
            for (int i = 0; i < _totems.Count; i++)
            {
                if (!_totems[i].Active) return _totems[i];
            }

            var fresh = new Totem
            {
                T = Sleeping(NewFx(Fx != null ? Fx.TurretBody : null, PrimitiveType.Cylinder, "Turret", true))
            };

            _totems.Add(fresh);
            return fresh;
        }

        Swell TakeSwell()
        {
            for (int i = 0; i < _swells.Count; i++)
            {
                if (!_swells[i].Active) return _swells[i];
            }

            // Material penanda AOE (paintable: false), bukan unlit pekat: cakram pekat seukuran
            // gelombang menutupi persis apa yang sedang disapunya.
            var fresh = new Swell
            {
                T = Sleeping(NewFx(Fx != null ? Fx.ShockRing : null, PrimitiveType.Cylinder, "Shockwave", false))
            };

            _swells.Add(fresh);
            return fresh;
        }

        Missile TakeMissile()
        {
            for (int i = 0; i < _missiles.Count; i++)
            {
                if (!_missiles[i].Active) return _missiles[i];
            }

            var fresh = new Missile
            {
                T = Sleeping(NewFx(Fx != null ? Fx.Missile : null, PrimitiveType.Sphere, "Missile", true))
            };

            _missiles.Add(fresh);
            return fresh;
        }

        Leash TakeLeash()
        {
            for (int i = 0; i < _leashes.Count; i++)
            {
                if (!_leashes[i].Active) return _leashes[i];
            }

            var fresh = new Leash { Line = NewLine("Tether") };
            _leashes.Add(fresh);
            return fresh;
        }

        Scribble TakeScribble()
        {
            for (int i = 0; i < _scribbles.Count; i++)
            {
                if (!_scribbles[i].Active) return _scribbles[i];
            }

            var fresh = new Scribble { Line = NewLine("Scribble") };
            _scribbles.Add(fresh);
            return fresh;
        }

        /// <summary>
        /// LineRenderer milik sendiri, bukan pinjaman dari <see cref="BoltPool"/>.
        ///
        /// Material bolt, bukan material FX: URP/Unlit mengabaikan warna vertex LineRenderer, jadi
        /// di material FX garis ini akan selalu putih apa pun warna skillnya — dan tidak akan bisa
        /// dipudarkan sama sekali.
        /// </summary>
        LineRenderer NewLine(string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(_fxRoot, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = _bolts.Material;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        /// <summary>
        /// Memasang efek partikel sebagai BADAN sebuah benda bergerak.
        ///
        /// Selalu dilepas lebih dulu: slot benda dipakai ulang lintas skill, dan efek milik skill
        /// sebelumnya yang menempel di slot itu adalah bug yang terlihat seperti pilihan seni.
        /// </summary>
        /// <returns>True kalau efeknya benar-benar terpasang — pemanggil memakai ini untuk
        /// menciutkan primitifnya jadi inti. Skill tanpa <c>CastVfx</c> harus tetap punya badan.</returns>
        bool Attach(ref Transform vfx, ref GameObject src, PieceDefinition def, Vector3 at,
            Quaternion rotation, float scale)
        {
            if (vfx != null)
            {
                _vfx.Release(src, vfx);
                vfx = null;
                src = null;
            }

            if (def.CastVfx == null) return false;

            vfx = _vfx.Attach(def.CastVfx, at, rotation, scale);
            src = def.CastVfx;
            return true;
        }

        /// <summary>Menggambar seluruh lintasan pantul sebagai satu garis yang memudar.</summary>
        void DrawScribble(int points, Color color, float width)
        {
            if (points < 2) return;

            var s = TakeScribble();

            s.Line.positionCount = points;
            for (int i = 0; i < points; i++) s.Line.SetPosition(i, _ricochetPath[i]);

            s.Line.widthMultiplier = width;
            s.Tint = color;

            // Umurnya panjang dengan sengaja. Coretan yang hilang dalam 0,2 detik tidak pernah
            // sempat dibaca sebagai coretan — yang terlihat cuma kedipan, dan seluruh janji skill
            // ini ada pada bentuk yang tertinggal di layar.
            s.MaxLife = 0.55f;
            s.Life = s.MaxLife;
            s.Active = true;

            s.Line.enabled = true;
            s.Line.startColor = s.Line.endColor = color;
        }

        /// <summary>
        /// Kotak tempat sinar memantul: apa yang sedang TERLIHAT.
        ///
        /// Diambil dari empat sudut viewport yang ditembakkan ke bidang lantai, bukan dari rumus
        /// setengah-lebar × aspek. Kameranya ortografis dan MENUNDUK, jadi jejak layarnya di lantai
        /// direntangkan oleh sudut tunduknya — rumus yang mengabaikan itu meleset makin jauh makin
        /// dalam sudutnya, dan melesetnya tidak kelihatan sampai sinarnya memantul di udara kosong
        /// jauh di bawah tepi layar.
        /// </summary>
        void ScreenBox(out Vector3 min, out Vector3 max, float fallbackReach)
        {
            Vector3 here = transform.position;

            if (Lens != null && Lens.orthographic)
            {
                bool ok = true;
                float minX = float.MaxValue, maxX = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;

                for (int i = 0; i < 4 && ok; i++)
                {
                    var corner = new Vector3(i == 1 || i == 3 ? 1f : 0f, i >= 2 ? 1f : 0f, 0f);
                    ok = GroundPoint(Lens.ViewportPointToRay(corner), out Vector3 p);

                    if (!ok) break;

                    if (p.x < minX) minX = p.x;
                    if (p.x > maxX) maxX = p.x;
                    if (p.z < minZ) minZ = p.z;
                    if (p.z > maxZ) maxZ = p.z;
                }

                if (ok)
                {
                    // Sedikit dikerut ke dalam: pantulan tepat di garis tepi terpotong separuh
                    // oleh layar, dan yang terbaca adalah sinar yang menghilang, bukan memantul.
                    const float inset = 0.6f;

                    min = new Vector3(minX + inset, 0f, minZ + inset);
                    max = new Vector3(maxX - inset, 0f, maxZ - inset);
                    return;
                }
            }

            min = new Vector3(here.x - fallbackReach, 0f, here.z - fallbackReach);
            max = new Vector3(here.x + fallbackReach, 0f, here.z + fallbackReach);
        }

        static bool GroundPoint(Ray ray, out Vector3 at)
        {
            at = ray.origin;

            if (Mathf.Abs(ray.direction.y) < 0.001f) return false;

            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) return false;

            at = ray.origin + ray.direction * t;
            return true;
        }

        /// <summary>
        /// Memajukan <paramref name="at"/> sampai menyentuh dinding kotak lalu mengembalikan arah
        /// pantulnya. <paramref name="distance"/> nol berarti "pantulkan dari tempat ini juga" —
        /// dipakai saat sinar sudah berada di seorang musuh dan tidak punya musuh berikutnya.
        /// </summary>
        static Vector3 BounceInBox(ref Vector3 at, Vector3 dir, Vector3 min, Vector3 max, float distance)
        {
            // Titik di luar kotak ditarik masuk dulu. Bisa terjadi kalau pemain berdiri di tepi
            // layar: tanpa ini seluruh sisa lintasan dihitung di luar kotak dan tidak pernah
            // memantul lagi — sinarnya kabur lurus keluar layar.
            at.x = Mathf.Clamp(at.x, min.x, max.x);
            at.z = Mathf.Clamp(at.z, min.z, max.z);

            float tx = dir.x > 0.0001f ? (max.x - at.x) / dir.x
                : dir.x < -0.0001f ? (min.x - at.x) / dir.x
                : float.MaxValue;

            float tz = dir.z > 0.0001f ? (max.z - at.z) / dir.z
                : dir.z < -0.0001f ? (min.z - at.z) / dir.z
                : float.MaxValue;

            float wall = Mathf.Min(tx, tz);
            if (wall >= float.MaxValue) return dir;

            float travel = distance > 0f ? Mathf.Min(distance, wall) : wall;

            at += dir * travel;
            at.y = 0.6f;

            // Belum sampai dinding: berhenti di situ tanpa memantul. Ruas berikutnya melanjutkan
            // dari sini, dan itu yang membuat sinar berjangkauan pendek tetap bisa menyeberang
            // layar lewat beberapa ruas.
            if (travel < wall - 0.001f) return dir;

            return tx <= tz
                ? new Vector3(-dir.x, 0f, dir.z)
                : new Vector3(dir.x, 0f, -dir.z);
        }
    }
}
