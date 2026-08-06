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
                _diveTimer = _breachesLeft > 0 ? Def.DipDuration : Def.DiveInterval;
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

            _spitTimer = Def.SpitInterval;

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
                _lungeTimer = Def.LungeInterval;
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

        void RecordTrail()
        {
            if (_trail.Count > 0 && (_trail[0] - _head).sqrMagnitude < TrailStep * TrailStep) return;

            _trail.Insert(0, _head);

            int needed = Mathf.CeilToInt(Def.MaxSegments * Def.Spacing / TrailStep) + 8;
            while (_trail.Count > needed) _trail.RemoveAt(_trail.Count - 1);
        }

        /// <summary>Posisi ruas ke-<paramref name="index"/> di belakang kepala.</summary>
        public Vector3 SegmentPoint(int index)
        {
            if (_trail.Count == 0) return _head;

            int step = Mathf.RoundToInt(index * Def.Spacing / TrailStep);
            return _trail[Mathf.Clamp(step, 0, _trail.Count - 1)];
        }

        public List<EnemyManager.Enemy> Segments => _segments;
    }
}
