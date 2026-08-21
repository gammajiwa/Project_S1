using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Satu ular. Kepalanya yang berpikir; badannya cuma menapaki jejak yang sudah dilalui kepala.
    ///
    /// Tiap ruas didaftarkan sebagai <see cref="EnemyManager.Enemy"/> biasa, dan itu keputusan
    /// terpenting di file ini. Boss dengan jalur damage sendiri berarti tiap skill di buku harus
    /// diajari cara mengenainya — dan yang terjadi selalu sama: satu atau dua skill terlupakan,
    /// lalu ada build yang secara diam-diam tidak bisa melukai boss sama sekali. Dengan ruasnya
    /// menjadi musuh biasa, semua yang sudah bisa mengenai musuh otomatis bisa mengenai boss.
    ///
    /// Yang membedakannya cuma satu: damage ke ruas mana pun masuk ke SATU kolam HP, dan ruasnya
    /// tidak pernah mati sendiri-sendiri.
    /// </summary>
    public class BossSnake
    {
        /// <summary>Jarak antar titik jejak. Lebih rapat = badan lebih halus, memori lebih boros.</summary>
        const float TrailStep = 0.28f;

        public BossDefinition Def { get; private set; }
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public bool Alive { get; private set; }

        public float HpFraction => MaxHp <= 0f ? 0f : Mathf.Clamp01(Hp / MaxHp);
        public Vector3 HeadPos => _head;

        /// <summary>
        /// Ke mana kepala sedang MENGHADAP.
        ///
        /// Harus dibuka keluar, dan sebabnya bug yang nyata: yang menggambar ruas menurunkan arah
        /// hadap tiap ruas dari selisih posisinya dengan ruas di depannya, dan untuk ruas nomor nol
        /// "ruas di depannya" adalah kepala itu sendiri. Selisihnya nyaris nol — jejak disisipkan
        /// di indeks 0 tiap kali kepala bergerak sejauh TrailStep — jadi penjaga anti-nol di sana
        /// menolak menghitung, dan kepala mempertahankan arah hadap lamanya SELAMANYA.
        ///
        /// Yang terlihat: seluruh badan meliuk mengikuti jalurnya dengan benar sementara kepalanya
        /// menatap utara dunia sepanjang pertarungan.
        /// </summary>
        public Vector3 Heading => _heading;

        readonly List<EnemyManager.Enemy> _segments = new List<EnemyManager.Enemy>();

        /// <summary>Jejak kepala, terbaru di indeks 0.</summary>
        readonly List<Vector3> _trail = new List<Vector3>(512);

        Vector3 _head;
        Vector3 _heading = Vector3.forward;
        float _orbitSign = 1f;
        float _wanderPhase;
        float _lungeTimer;
        float _lungeLeft;
        float _biteCooldown;

        public bool Lunging => _lungeLeft > 0f;

        // ---------- bersayap ----------

        float _breathTimer;
        float _breathLeft;
        Vector3 _laneAim;
        bool _lanePassed;
        float _laneLeft;
        float _sinceBreath;
        float _bank;

        /// <summary>
        /// Derajat kemiringan badan ke arah belokan, untuk yang bersayap.
        ///
        /// Inilah yang membedakan terbang dari meluncur di atas rel: benda yang berbelok datar
        /// terbaca sebagai karton yang diputar, yang memiringkan badannya ke dalam belokan
        /// terbaca sebagai sesuatu yang menahan beban di udara.
        /// </summary>
        public float BankDegrees => _bank;

        /// <summary>
        /// Sedang dalam gerakan menyembur — TERMASUK ancang-ancangnya, saat apinya belum melukai.
        ///
        /// Sengaja mencakup ancang-ancang, karena inilah yang menyalakan animasi dan VFX-nya.
        /// Pemain harus melihat api itu datang sebelum ia menyakiti; menyamakan "terlihat" dengan
        /// "melukai" menghapus satu-satunya jendela untuk menghindar.
        /// </summary>
        public bool Breathing => _breathLeft > 0f;

        /// <summary>
        /// Dipanggil tiap frame selama apinya menyala, dengan titik jatuhnya di tanah.
        /// Yang menggambar coretan gosong mendengarkan di sini.
        /// </summary>
        public System.Action<Vector3> OnScorch;

        /// <summary>
        /// Titik jatuh api di tanah: di depan naga, sepanjang arah terbangnya.
        ///
        /// Jarak depannya diikat ke FlyHeight — api dari mulut yang menunduk mendarat kira-kira
        /// sejauh ketinggiannya. Titik ini juga yang dipakai kerusakan DAN yang dibidik VFX-nya,
        /// jadi yang terlihat terbakar dan yang benar-benar melukai adalah tempat yang sama
        /// menurut konstruksi, bukan menurut dua rumus yang kebetulan mirip.
        /// </summary>
        public Vector3 GroundImpact
        {
            get
            {
                Vector3 at = _head + _heading * Mathf.Max(2f, Def.FlyHeight * 0.8f);
                at.y = 0f;
                return at;
            }
        }

        /// <summary>Apinya sudah benar-benar keluar dan melukai.</summary>
        public bool BreathHot => _breathLeft > 0f
            && _breathLeft <= Def.BreathDuration - Def.BreathWindup;

        /// <summary>
        /// Seberapa cepat badannya bergerak, 0..1 — atau <see cref="EnemyRenderer.AttackSpeed"/>
        /// saat menyembur. Inilah yang memilih klip mana yang diputar model panggangannya.
        /// </summary>
        public float Speed01 { get; private set; }

        /// <summary>Sudah berapa detik semburan ini berjalan, dihitung dari awal ancang-ancang.</summary>
        public float BreathElapsed => _breathLeft > 0f ? Def.BreathDuration - _breathLeft : 0f;

        /// <summary>
        /// Ujung mulut: tempat api keluar, dan tempat kerucutnya berpangkal.
        ///
        /// Offsetnya DIKALI HeadScale, karena BreathMuzzle disimpan sebagai pecahan dari tinggi
        /// naga. Tanpa perkalian itu, membesarkan naganya menggeser mulutnya ke dalam dadanya —
        /// dan apinya keluar dari perut.
        /// </summary>
        public Vector3 MuzzlePos =>
            _head + Quaternion.Euler(0f, Mathf.Atan2(_heading.x, _heading.z) * Mathf.Rad2Deg, 0f)
                  * (Def.BreathMuzzle * Def.HeadScale);

        // ---------- menyelam ----------

        float _diveTimer;
        float _surfaceLeft;
        float _spitTimer;
        int _breachesLeft;

        /// <summary>Sedang di atas tanah, artinya bisa dipukul dan sedang menyembur.</summary>
        public bool Surfaced => !Def.Burrows || _surfaceLeft > 0f;

        /// <summary>Dipanggil tiap kali ia menyemburkan racun. Argumennya arah tembak.</summary>
        public System.Action<Vector3, Vector3> OnSpit;

        /// <summary>
        /// Pengali agresi per-EKOR, dipasang setelah <see cref="Begin"/>. Membagi jeda terjangan,
        /// selaman, dan semburan — bukan menulis ke <see cref="Def"/>, karena Def itu ASET yang
        /// dipakai bersama: mengubahnya untuk satu boss mengubah semua boss, dan menetap sampai
        /// editor ditutup.
        /// </summary>
        public float Aggro = 1f;

        public void Begin(BossDefinition def, Vector3 at, float hp)
        {
            Def = def;
            MaxHp = Mathf.Max(1f, hp);
            Hp = MaxHp;
            Alive = true;

            _head = at;
            _heading = Vector3.forward;
            _orbitSign = Random.value < 0.5f ? -1f : 1f;
            _wanderPhase = Random.Range(0f, 100f);
            _lungeTimer = def.LungeInterval * 0.6f;
            _lungeLeft = 0f;
            _biteCooldown = 0f;

            _diveTimer = def.DiveInterval * 0.4f;
            _surfaceLeft = 0f;
            _spitTimer = 0f;
            _breachesLeft = Mathf.Max(1, def.BreachBurst);

            _breathTimer = def.BreathInterval * 0.5f;
            _breathLeft = 0f;
            _laneLeft = 0f;
            _lanePassed = true;
            _sinceBreath = 0f;
            Speed01 = 0f;

            // Naga datang dari ATAS, bukan merayap dari tepi lapangan. Melintas masuk dari langit
            // adalah kedatangannya; muncul di tanah lalu memanjat terbaca sebagai musuh biasa
            // yang kebetulan bisa terbang.
            //
            // KEDUANYA ditulis, dan itu bukan kelebihan. `_head` sudah disalin dari `at` di atas,
            // jadi menyentuh `at` saja cuma mengubah isi jejak dan meninggalkan kepalanya di
            // tanah; menyentuh `_head` saja meninggalkan jejaknya di tanah, dan ruas yang
            // membacanya akan menggantung di bawah naganya pada frame-frame pertama.
            if (def.Body == BossDefinition.BossBody.Winged)
            {
                at.y = def.FlyHeight;
                _head.y = def.FlyHeight;
            }

            // Kelabang mulai dari BAWAH tanah. Muncul di permukaan lalu langsung menyelam terbaca
            // sebagai bug; menyembur keluar dari tanah terbaca sebagai kedatangan.
            if (def.Burrows) at.y = -def.DiveDepth;

            // Jejaknya diisi penuh di tempat sejak awal. Tanpa itu seluruh badan menumpuk di satu
            // titik pada frame pertama lalu terurai sambil meregang — terbaca seperti glitch.
            _trail.Clear();
            for (int i = 0; i < 512; i++) _trail.Add(at);

            _segments.Clear();
        }

        public void End()
        {
            Alive = false;
            _segments.Clear();
            _trail.Clear();
        }

        /// <summary>Damage dari ruas mana pun berakhir di sini. Ruasnya sendiri tidak pernah mati.</summary>
        public void TakeDamage(float amount)
        {
            if (!Alive) return;

            Hp -= amount;
            if (Hp > 0f) return;

            Hp = 0f;
            Alive = false;
        }

        /// <summary>
        /// Berapa ruas yang seharusnya terlihat sekarang. Ini bar HP-nya, dan satu-satunya.
        /// </summary>
        public int WantedSegments()
        {
            // Bersayap: satu badan, selamanya.
            //
            // Badan yang memendek adalah bar HP milik yang BERUAS, dan naga tidak bisa
            // membayarnya — tidak ada ruas yang bisa dilepas tanpa memotong sayapnya. HP-nya
            // dibaca dari bar melayang yang sudah dipasang EnemyHpBars untuk semua boss.
            if (Def.Body == BossDefinition.BossBody.Winged) return 1;

            float t = HpFraction;
            return Mathf.Max(Def.MinSegments,
                Mathf.CeilToInt(Mathf.Lerp(Def.MinSegments, Def.MaxSegments, t)));
        }

        public void Tick(float dt, Vector3 target, System.Action<float> bite)
        {
            if (!Alive) return;

            if (Def.Body == BossDefinition.BossBody.Winged)
            {
                TickWinged(dt, target, bite);
                return;
            }

            Steer(dt, target);

            _head += _heading * (Lunging ? Def.LungeSpeed : Def.Speed) * dt;

            if (Def.Burrows) Burrow(dt, target);

            RecordTrail();

            if (_biteCooldown > 0f) _biteCooldown -= dt;

            // Menggigit hanya saat menerjang. Kepala yang melukai kapan pun ia kebetulan lewat
            // membuat seluruh terjangan jadi tidak berarti — pemain tidak punya apa pun untuk
            // dibaca, dan boss ini seluruhnya tentang membaca kapan terjangannya datang.
            bool striking = Def.Burrows ? Surfaced : Lunging;

            if (striking && _biteCooldown <= 0f)
            {
                Vector3 d = target - _head;
                d.y = 0f;

                if (d.sqrMagnitude <= Def.BiteRange * Def.BiteRange)
                {
                    bite?.Invoke(Def.BiteDamage);
                    _biteCooldown = 1.2f;
                }
            }
        }

        /// <summary>
        /// Siklus menyelam-menyembur. Yang diatur di sini HANYA ketinggian kepala.
        ///
        /// Badannya tidak disentuh sama sekali, dan itu bukan kebetulan: tiap ruas menapaki jejak
        /// kepala, dan jejak itu menyimpan ketinggian. Begitu kepalanya menukik lalu melengkung
        /// naik, seluruh badan mengikuti busur yang sama satu per satu — persis cacing yang
        /// menyembur keluar tanah. Menganimasikan badannya sendiri justru akan merusaknya.
        /// </summary>
        void Burrow(float dt, Vector3 target)
        {
            // MELOMPAT, bukan berjalan di permukaan.
            //
            // Kepalanya menyembul, melengkung, menukik masuk lagi — lalu mengulanginya dua-tiga
            // kali beruntun sebelum menghilang dalam-dalam. Persis lumba-lumba.
            //
            // Yang bikin ini bekerja adalah panjang badannya. Kepala menyelesaikan busurnya dalam
            // sedetik, tapi badannya butuh beberapa detik untuk menapaki jejak yang sama — jadi
            // saat kepalanya sudah masuk lagi, ruas-ruas tengahnya masih melengkung di udara.
            // Itulah siluet cacing pasir yang dicari, dan tidak satu ruas pun perlu dianimasikan.
            if (_surfaceLeft > 0f)
            {
                _surfaceLeft -= dt;

                float t = 1f - _surfaceLeft / Mathf.Max(0.05f, Def.ArcDuration);
                _head.y = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * Def.ArcHeight - 0.5f;

                Spit(dt, target);

                if (_surfaceLeft > 0f) return;

                // Lompatan habis. Masih ada sisa rentetan? Celup dangkal saja, bukan menyelam.
                _breachesLeft--;
                _diveTimer = (_breachesLeft > 0 ? Def.DipDuration : Def.DiveInterval) / Aggro;
                return;
            }

            _diveTimer -= dt;

            // Celupan di antara lompatan dangkal saja — dalamnya menyelam cuma untuk jeda panjang.
            // Kalau setiap celupan sedalam itu, rentetannya berhenti terbaca sebagai satu gerakan.
            float depth = _breachesLeft > 0 ? -1.6f : -Def.DiveDepth;
            _head.y = Mathf.Lerp(_head.y, depth, 8f * dt);

            if (_diveTimer > 0f) return;

            if (_breachesLeft <= 0) _breachesLeft = Mathf.Max(1, Def.BreachBurst);

            _surfaceLeft = Def.ArcDuration;
            _spitTimer = 0f;
        }

        void Spit(float dt, Vector3 target)
        {
            if (Def.SpitInterval <= 0f) return;

            _spitTimer -= dt;
            if (_spitTimer > 0f) return;

            _spitTimer = Def.SpitInterval / Aggro;

            Vector3 dir = target - _head;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;

            OnSpit?.Invoke(_head, dir.normalized);
        }

        /// <summary>
        /// Naga: terbang seperti burung pemangsa — LURUS, belok di ujung lintasan, lurus lagi.
        ///
        /// Tidak pernah berhenti, tidak pernah melambat, tidak pernah membidik di tengah
        /// lintasan. Permintaan pemilik project setelah melihat versi yang melayang sambil
        /// mengunci arah: itu terbaca sebagai drone yang mengambang, bukan naga. Yang sekarang:
        /// ia menyeberangi arena, MENYEMBUR SAMBIL LEWAT, kebablasan, memutar di kejauhan, lalu
        /// datang lagi untuk lintasan berikutnya.
        ///
        /// Tidak ada satu pun jejak yang direkam di sini — badannya satu keping, tidak ada yang
        /// perlu menapaki apa pun.
        /// </summary>
        void TickWinged(float dt, Vector3 target, System.Action<float> bite)
        {
            Breath(dt, target, bite);

            _laneLeft -= dt;
            if (_laneLeft <= 0f) NewLane(target);

            // Mengejar TITIK sasaran lintasan, bukan arah yang dikunci di awal lintasan.
            //
            // Versi arah-terkunci punya cacat geometri yang terukur: arah dihitung dari posisi
            // naga SEBELUM berbelok, dan belokan itu sendiri menggesernya lima sampai sepuluh
            // unit ke samping — jadi garis terbang finalnya PARALEL dengan garis yang menembus
            // pemain, bukan menembusnya. Lateral terkecil sepanjang 30 detik: 4,15, sementara
            // gerbang picunya 2,88. Nol semburan, selamanya.
            //
            // Mengejar titik membuat belokannya mengoreksi diri: seberapa pun ia tergeser saat
            // memutar, hidungnya berakhir mengarah ke titik itu. Begitu titiknya TERLEWATI,
            // arah dibekukan — sapuan api dan kebablasannya harus lurus, bukan berputar-putar
            // di sekitar titik yang sudah lewat.
            Vector3 want = _heading;

            if (!_lanePassed)
            {
                Vector3 toAim = _laneAim - _head;
                toAim.y = 0f;

                // "Terlewati" cuma sah kalau titiknya DEKAT. Sesaat setelah kebablasan, titik
                // bidik lintasan baru ada tiga puluh unit di BELAKANG — dot-nya negatif juga,
                // dan tanpa syarat jarak, lintasan baru dicap terlewati di frame pertamanya:
                // naganya tidak pernah berbelok balik sama sekali, cuma terbang lurus keluar
                // arena selamanya. Terukur: nol semburan di keempat pola gerak pemain.
                bool behind = Vector3.Dot(toAim, _heading) < 0f;

                if (toAim.sqrMagnitude < 4f || (behind && toAim.sqrMagnitude < 64f))
                    _lanePassed = true;
                else
                    want = toAim.normalized;
            }

            // Kemiringan dari selisih arah — bukan dari kecepatan belok sesaat, yang
            // berfluktuasi tiap frame dan membuat sayapnya bergetar kiri-kanan.
            float turn = Vector3.SignedAngle(_heading, want, Vector3.up);
            float wantBank = Mathf.Clamp(turn, -50f, 50f) * 0.8f;
            _bank = Mathf.MoveTowards(_bank, wantBank, 120f * dt);

            _heading = Vector3.RotateTowards(_heading, want,
                Def.TurnRate * Mathf.Deg2Rad * dt, 0f);
            _heading.y = 0f;
            _heading.Normalize();

            // Laju SELALU penuh. Versi sebelumnya melambat saat menyembur, dan gabungan
            // melambat + membidik itulah yang terbaca "aneh": naga yang mengerem di udara.
            _head += _heading * Def.Speed * dt;

            // Ketinggian dikejar TERPISAH dari gerak datarnya. Digabung, naganya akan menukik
            // tiap kali membelok — belokan memperlambat laju datar, dan ketinggian yang ikut
            // laju itu jadi ikut turun bersamanya.
            _head.y = Mathf.MoveTowards(_head.y, Def.FlyHeight, Def.ClimbRate * dt);

            // Selalu pita LARI (Fly Forward) — ia memang selalu melaju penuh. Menyembur menukar
            // klipnya ke Attack; kecepatan terbangnya sendiri tidak berubah sedikit pun.
            Speed01 = Breathing ? EnemyRenderer.AttackSpeed : 1f;
        }

        /// <summary>
        /// Lintasan berikutnya: garis lurus yang MENEMBUS posisi pemain (digeser sedikit acak),
        /// dengan panjang yang membawa naganya KEBABLASAN melewati pemain sebelum memutar.
        ///
        /// Kebablasannya bukan kelonggaran — itu bentuk terbangnya. Lintasan yang berakhir tepat
        /// di pemain membuat naganya berputar-putar di atas kepala; yang menembus jauh membuat
        /// pola serang-lewat-putar-balik, dan memberi pemain jeda yang bisa dibaca di antara
        /// dua lintasan.
        /// </summary>
        void NewLane(Vector3 target)
        {
            // Simpangan bidiknya KECIL — satu setengah unit. Terukur di simulasi dengan
            // simpangan empat: belasan semburan, nol damage, karena garis terbangnya sendiri
            // sudah meleset lebih jauh dari jari-jari apinya. Lintasan boleh bervariasi;
            // ancamannya tidak boleh ikut menguap.
            Vector3 aim = target + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
            Vector3 to = aim - _head;
            to.y = 0f;

            // Terlalu dekat untuk membentuk garis: lintasan ini jadi kebablasan murni — terbang
            // lurus menjauh dulu, lintasan BERIKUTNYA yang memutar kembali. Membalik arah tepat
            // di atas kepala pemain menghasilkan putaran di tempat — persis yang mau dibuang.
            if (to.sqrMagnitude < 16f)
            {
                _laneAim = _head + _heading * 20f;
                _lanePassed = true;
            }
            else
            {
                _laneAim = aim;
                _lanePassed = false;
            }

            // Sampai ke pemain, lalu terus sejauh StrafeWidth lagi sebelum memutar.
            float span = Mathf.Sqrt(Mathf.Max(16f, to.sqrMagnitude)) + Mathf.Max(6f, Def.StrafeWidth);
            _laneLeft = span / Mathf.Max(0.5f, Def.Speed);
        }

        /// <summary>
        /// Siklus semburan versi melintas: dipicu saat lintasan akan melewati pemain, membakar
        /// SAPUAN TANAH di depan naga selama menyala, dan tiap titik jatuhnya dilaporkan keluar
        /// untuk digambar sebagai coretan gosong.
        /// </summary>
        void Breath(float dt, Vector3 target, System.Action<float> bite)
        {
            if (Def.BreathInterval <= 0f) return;

            _sinceBreath += dt;

            if (_breathLeft > 0f)
            {
                _breathLeft -= dt;

                if (_breathLeft <= 0f)
                {
                    _breathLeft = 0f;
                    _breathTimer = Def.BreathInterval / Aggro;
                    return;
                }

                if (!BreathHot) return;

                // Sapuan tanah, bukan kerucut ke arah pemain. Apinya jatuh di depan naga dan
                // menyeret garis sepanjang lintasan terbangnya — pemain terbakar karena berdiri
                // di garis itu, dan jawabannya selalu sama dan selalu terbaca: keluar dari garis.
                Vector3 impact = GroundImpact;

                OnScorch?.Invoke(impact);

                Vector3 d = target - impact;
                d.y = 0f;

                if (d.sqrMagnitude > Def.BreathGroundRadius * Def.BreathGroundRadius) return;

                // Per DETIK, jadi dikali dt. Yang mengirim satu hantaman penuh tiap frame akan
                // membunuh pemain dalam sepersekian detik, dan seberapa cepat matinya jadi
                // bergantung pada seberapa kencang mesin yang memainkannya.
                bite?.Invoke(Def.BreathDamage * dt);
                return;
            }

            _breathTimer -= dt;
            if (_breathTimer > 0f) return;

            // Dipicu saat lintasan SEKARANG akan membawa apinya melewati pemain: pemain di
            // depan, dalam jangkauan picu, dan tidak jauh menyamping dari garis terbang.
            // Ancang-ancang berjalan selagi naga terus melaju, jadi begitu apinya menyala,
            // garis bakarnya mulai beberapa langkah sebelum pemain lalu menyeret melewatinya.
            Vector3 to = target - _head;
            to.y = 0f;

            float ahead = Vector3.Dot(to, _heading);
            Vector3 lateral = to - _heading * ahead;

            // Jendela picunya DIHITUNG dari geometri sapuan, bukan angka karangan.
            //
            // Api jatuh `lead` di depan naga, dan baru menyala setelah ancang-ancang — selama
            // itu naganya sudah maju sejauh windup dikali laju. Pemain yang lebih dekat dari
            // jumlah keduanya sudah TERLEWATI saat api pertama menyentuh tanah: sapuannya lahir
            // di depan pemain dan menjauh, tidak pernah menyentuhnya. Terukur sebelum dihitung
            // begini: separuh semburan meleset persis karena ini — 3,6 semburan cuma 27 damage
            // pada pemain yang DIAM.
            float lead = Mathf.Max(2f, Def.FlyHeight * 0.8f);
            float minAhead = lead + Def.Speed * Def.BreathWindup + 1f;
            float maxAhead = Mathf.Min(Def.BreathRange,
                minAhead + Def.Speed * (Def.BreathDuration - Def.BreathWindup) - 2f);

            if (ahead < minAhead || ahead > maxAhead) return;

            // Menyembur HANYA kalau garis terbang sekarang benar-benar akan menyapu pemain.
            // Sapuannya lurus tanpa koreksi, jadi menyamping saat picu = menyamping saat lewat;
            // gerbang yang lebih longgar dari jari-jari apinya cuma menghasilkan semburan yang
            // sudah pasti meleset sejak dipicu — terukur: belasan semburan, nol damage.
            //
            // Jendela menghindarnya tetap ada, dan justru jadi jujur: begitu api dipicu, pemain
            // punya ancang-ancang plus waktu tempuh sapuan untuk MINGGIR — bukan berharap
            // undian simpangan membuat apinya meleset sendiri.
            // Kelaparan: pemain yang terus berputar tidak pernah lolos gerbang ketat di atas,
            // dan naga yang TIDAK PERNAH menyembur berhenti terbaca sebagai naga. Setelah
            // sepuluh detik tanpa api, gerbangnya melebar — semburannya hampir pasti meleset
            // dari pemain yang segesit itu, dan memang boleh: apinya jadi tontonan dan coretan,
            // pemainnya sudah membayar dengan gerak kaki yang membuatnya meleset.
            // Saat kelaparan, gerbangnya DIBUANG, bukan dilebarkan: pemain yang berputar
            // delapan unit per detik menggeser enam belas unit selama satu ancang, jadi
            // pelebaran berapa pun tetap tidak pernah lolos. Meleset ya meleset — apinya
            // tetap keluar, coretannya tetap tergambar.
            if (_sinceBreath <= 10f && lateral.magnitude > Def.BreathGroundRadius * 0.9f) return;

            _breathLeft = Def.BreathDuration;
            _sinceBreath = 0f;
        }

        void Steer(float dt, Vector3 target)
        {
            _lungeTimer -= dt;

            if (_lungeLeft > 0f)
            {
                _lungeLeft -= dt;
            }
            else if (_lungeTimer <= 0f)
            {
                _lungeLeft = Def.LungeDuration;
                _lungeTimer = Def.LungeInterval / Aggro;
            }

            Vector3 toPlayer = target - _head;
            toPlayer.y = 0f;

            float distance = toPlayer.magnitude;
            if (distance < 0.01f) return;

            Vector3 want;

            if (_lungeLeft > 0f || (Def.Burrows && !Surfaced))
            {
                // Yang menyelam mengejar LURUS, tidak mengitari. Kepastiannya itulah ancamannya:
                // ia akan menyembur tepat di bawah kaki pemain, dan satu-satunya jawabannya adalah
                // pindah sebelum ia sampai.
                want = toPlayer / distance;
            }
            else
            {
                // Mengitari: gabungan menuju/menjauhi jari-jari orbit, ditambah komponen
                // menyamping. Yang menyamping itulah yang bikin dia MENGELILINGI alih-alih
                // sekadar mendekat lalu berhenti.
                Vector3 radial = toPlayer / distance;
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x) * _orbitSign;

                float error = (distance - Def.OrbitRadius) / Def.OrbitRadius;
                want = (tangent + radial * Mathf.Clamp(error, -1f, 1f)).normalized;

                // Meliuk tak menentu. Perlin, bukan acak per frame: yang acak menghasilkan getaran,
                // yang ini menghasilkan lintasan yang berkelok.
                _wanderPhase += dt * 0.6f;
                float noise = (Mathf.PerlinNoise(_wanderPhase, 0f) - 0.5f) * 2f;
                want = Quaternion.Euler(0f, noise * Def.Wander * 45f, 0f) * want;
            }

            // Belokannya dibatasi. Kepala yang bisa berbalik seketika akan melipat badannya ke
            // dalam dirinya sendiri, dan bentuk ularnya hilang.
            _heading = Vector3.RotateTowards(_heading, want, Def.TurnRate * Mathf.Deg2Rad * dt, 0f);
            _heading.y = 0f;
            _heading.Normalize();
        }

        /// <summary>
        /// Merekam jejak kepala pada jarak yang SELALU tepat <see cref="TrailStep"/>.
        ///
        /// Versi lama menyisipkan SATU titik tiap kali kepala sudah bergerak sejauh TrailStep —
        /// dan itu diam-diam salah begitu kepalanya bergerak lebih jauh dari itu dalam satu frame.
        /// Saat menerjang ia melaju 15 unit/detik, jadi di frame yang berat satu langkah bisa
        /// 0,8 unit sementara jejaknya cuma bertambah satu titik. <see cref="SegmentPoint"/>
        /// mencari ruas dengan MENGHITUNG INDEKS dan mengalikannya dengan TrailStep, jadi begitu
        /// jarak antar titik tidak lagi seragam, seluruh perhitungan itu berbohong.
        ///
        /// Yang terlihat pemain: badan ular MERENGGANG saat ia menerjang — persis di detik yang
        /// paling diperhatikan — lalu merapat lagi setelahnya. Terukur sebelum diperbaiki: jarak
        /// antar ruas terjauh 1,96 unit padahal ruasnya sendiri cuma 1,26 panjangnya.
        ///
        /// Sekarang titik-titik antaranya diisi. Jejaknya jadi punya jarak seragam menurut
        /// definisi, dan aritmetika indeks di SegmentPoint kembali benar tanpa disentuh.
        /// </summary>
        void RecordTrail()
        {
            if (_trail.Count == 0)
            {
                _trail.Insert(0, _head);
                return;
            }

            Vector3 last = _trail[0];
            float gap = Vector3.Distance(last, _head);
            if (gap < TrailStep) return;

            // Dibatasi supaya teleport (pindah act, boss dilahirkan ulang) tidak melahirkan
            // ribuan titik dalam satu frame. Lompatan sebesar itu memang bukan gerakan.
            int fill = Mathf.Min(Mathf.FloorToInt(gap / TrailStep), 64);

            for (int i = 1; i <= fill; i++)
            {
                _trail.Insert(0, Vector3.Lerp(last, _head, i * TrailStep / gap));
            }

            int needed = Mathf.CeilToInt(Def.MaxSegments * Def.Spacing / TrailStep) + 8;
            while (_trail.Count > needed) _trail.RemoveAt(_trail.Count - 1);
        }

        /// <summary>
        /// Posisi ruas ke-<paramref name="index"/> di belakang kepala, DIINTERPOLASI di antara dua
        /// titik jejak.
        ///
        /// Versi lama membulatkan ke titik jejak terdekat, dan pembulatan itu mengkuantisasi jarak
        /// antar ruas ke kelipatan <see cref="TrailStep"/> (0,28). Untuk boss ber-Spacing 0,6 itu
        /// berarti jarak nyatanya melompat-lompat antara 0,56 dan 0,84 — variasi 50%, dan yang
        /// 0,84 lebih panjang dari ruasnya sendiri. Terukur: badan `grub` masih berlubang bahkan
        /// setelah jejaknya dibuat berjarak seragam.
        /// </summary>
        public Vector3 SegmentPoint(int index)
        {
            // Bersayap: badannya ADA di kepala, bukan menapaki jejak di belakangnya. Membaca
            // jejak di sini membuat naganya tertinggal setengah langkah dari posisi yang dipakai
            // semburan dan bar HP — keduanya membaca HeadPos langsung, dan dua sumber kebenaran
            // yang meleset setengah langkah adalah api yang keluar dari udara kosong.
            if (Def.Body == BossDefinition.BossBody.Winged) return _head;

            if (_trail.Count == 0) return _head;

            float steps = index * Def.Spacing / TrailStep;

            int lo = Mathf.Clamp(Mathf.FloorToInt(steps), 0, _trail.Count - 1);
            int hi = Mathf.Clamp(lo + 1, 0, _trail.Count - 1);

            return Vector3.Lerp(_trail[lo], _trail[hi], steps - lo);
        }

        public List<EnemyManager.Enemy> Segments => _segments;
    }
}
