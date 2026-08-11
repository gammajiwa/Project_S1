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
            float t = HpFraction;
            return Mathf.Max(Def.MinSegments,
                Mathf.CeilToInt(Mathf.Lerp(Def.MinSegments, Def.MaxSegments, t)));
        }

        public void Tick(float dt, Vector3 target, System.Action<float> bite)
        {
            if (!Alive) return;

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
            if (_trail.Count == 0) return _head;

            float steps = index * Def.Spacing / TrailStep;

            int lo = Mathf.Clamp(Mathf.FloorToInt(steps), 0, _trail.Count - 1);
            int hi = Mathf.Clamp(lo + 1, 0, _trail.Count - 1);

            return Vector3.Lerp(_trail[lo], _trail[hi], steps - lo);
        }

        public List<EnemyManager.Enemy> Segments => _segments;
    }
}
