using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Nyawa visual pemain: memilih antara dua animasi yang dimiliki model (Idle dan Run),
    /// dan menghadapkan badan ke arah jalan.
    ///
    /// Aturannya dari pemilik project: jalan = Run, diam = Idle, dan **mengecast memakai Idle**
    /// — model ini tidak punya animasi cast, dan pose tenang yang berdiri diam saat buku
    /// menembak terbaca sebagai "dia yang menembak", bukan sebagai animasi yang hilang.
    ///
    /// Kecepatan dibaca dari perpindahan transform, bukan dari <see cref="PlayerMotor"/>:
    /// pemain juga berpindah lewat Blink dan Relocate antar node, dan semua jalur itu lewat
    /// transform yang sama. Lompatan sesaat (teleport) dipotong pagar <see cref="TeleportCut"/>
    /// supaya tidak terbaca sebagai lari satu frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAvatar : MonoBehaviour
    {
        [Tooltip("Kecepatan (unit/detik) yang dianggap mulai berjalan.")]
        [Min(0.01f)] public float RunThreshold = 0.6f;

        [Tooltip("Berapa lama pose Idle ditahan tiap kali sebuah skill menembak.")]
        [Min(0f)] public float CastHold = 0.45f;

        [Tooltip("Kecepatan putar badan, derajat/detik.")]
        [Min(30f)] public float TurnSpeed = 720f;

        /// <summary>Perpindahan satu frame di atas ini dianggap teleport, bukan lari.</summary>
        const float TeleportCut = 3f;

        static readonly int SpeedId = Animator.StringToHash("Speed");

        /// <summary>
        /// Baru boleh turun ke Idle setelah selambat ini selama <see cref="StopDelay"/> penuh.
        /// Sengaja jauh di bawah <see cref="RunThreshold"/>: di kerumunan, arah kabur berbalik
        /// terus dan kecepatan motor melewati nol SESAAT di tiap baliknya.
        /// </summary>
        const float StopSpeed = 0.35f;

        /// <summary>Detik selambat <see cref="StopSpeed"/> sebelum Run dilepas.</summary>
        const float StopDelay = 0.3f;

        /// <summary>
        /// Ada musuh sedekat ini = pose lari TIDAK PERNAH dilepas, bergerak atau tidak.
        ///
        /// Ini penutup kasus "balik posisinya" yang sebenarnya: motor memang dirancang
        /// berhenti-jalan-berhenti di kerumunan (vektor kabur saling meniadakan saat terkepung —
        /// disengaja, itu bentuk kalahnya). Tiap berhenti sungguhan, pose lari yang condong ke
        /// depan dilepas ke pose diam yang tegak, dan badan terbaca mundur ~28 cm — berulang
        /// setiap beberapa detik di wave ramai. Ambang dan histeresis mana pun kalah melawan
        /// berhenti yang SUNGGUHAN; aturan genre-nya memang karakter terus bergerak selama
        /// dikepung, lari-di-tempat sekalipun.
        /// </summary>
        const float CombatRadius = 5f;

        Animator _anim;
        PlayerCaster _caster;
        EnemyManager _enemies;
        Vector3 _lastPos;
        float _castHold;
        float _smoothSpeed;

        /// <summary>Run itu LENGKET — state di kode, bukan sekadar ambang di controller.</summary>
        bool _running;

        float _slowTimer;

        public void Init(PlayerCaster caster, EnemyManager enemies)
        {
            _caster = caster;
            _enemies = enemies;
            _anim = GetComponentInChildren<Animator>();
            _lastPos = transform.position;

            // Menembak = menahan pose Idle sebentar. Dipasang sekali; caster hidup sepanjang
            // scene, jadi tidak ada jalur yang perlu melepasnya.
            if (_caster != null) _caster.OnCast += _ => _castHold = CastHold;
        }

        void Update()
        {
            if (_anim == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 delta = transform.position - _lastPos;
            _lastPos = transform.position;
            delta.y = 0f;

            float speed = delta.magnitude / dt;
            if (speed * dt > TeleportCut) speed = 0f;

            // Dihaluskan supaya satu frame tersendat (spike dt, tabrak dinding) tidak membuat
            // animasinya gagap bolak-balik di ambang.
            _smoothSpeed = Mathf.Lerp(_smoothSpeed, speed, 1f - Mathf.Exp(-10f * dt));

            _castHold -= dt;

            // RUN ITU LENGKET, dan inilah pemadam "balik posisinya" yang sesungguhnya (2026-08-10).
            //
            // Ambang sesaat tidak akan pernah benar di game ini: di kerumunan, arah kabur
            // berbalik terus-menerus, dan kecepatan motor MELEWATI NOL sesaat di tiap balik
            // arah (heading digeser MoveTowards). Setiap lintasan-nol itu menjatuhkan param di
            // bawah ambang → Idle menyala sepersekian detik → pose lari yang condong ke depan
            // disentak ke pose diam yang tegak → mata membacanya sebagai badan yang MENTAL BALIK.
            // Terekam di burst: Run(1,5) → Idle(1,3!) → Run(1,2) dalam setengah detik.
            //
            // Aturannya sekarang: masuk Run seketika begitu melaju; keluar Run hanya setelah
            // benar-benar lambat (StopSpeed) selama StopDelay penuh. Lintasan nol sesaat tidak
            // pernah cukup lama untuk lolos.
            // Dikepung = terus lari, titik. Lihat catatan CombatRadius.
            bool combat = _enemies != null && _enemies.WaveActive &&
                          _enemies.Nearest(transform.position, CombatRadius) != null;

            if (combat || _smoothSpeed >= RunThreshold)
            {
                _running = true;
                _slowTimer = 0f;
            }
            else if (_smoothSpeed < StopSpeed)
            {
                _slowTimer += dt;
                if (_slowTimer >= StopDelay) _running = false;
            }
            else
            {
                // Zona tengah: pertahankan state yang sedang berjalan, jangan hitung mundur.
                _slowTimer = 0f;
            }

            // Pose cast hanya menang saat benar-benar BERDIRI (bukan sekadar melambat) —
            // menembak sambil lari tetap animasi lari, keputusan pemilik project.
            float reported = _running
                ? Mathf.Max(1.2f, _smoothSpeed / Mathf.Max(0.01f, RunThreshold))
                : (_castHold > 0f ? 0f : Mathf.Min(0.5f, _smoothSpeed / Mathf.Max(0.01f, RunThreshold)));

            _anim.SetFloat(SpeedId, reported);

            // Badan menghadap arah jalan. Saat berhenti, hadap terakhir dipertahankan —
            // berputar balik ke "depan" tiap kali diam membuatnya seperti boneka yang di-reset.
            if (speed > 0.25f && delta.sqrMagnitude > 0.000001f)
            {
                var want = Quaternion.LookRotation(delta.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, TurnSpeed * dt);
            }
        }
    }
}
