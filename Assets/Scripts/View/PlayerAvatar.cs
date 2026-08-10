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

        Animator _anim;
        PlayerCaster _caster;
        Vector3 _lastPos;
        float _castHold;
        float _smoothSpeed;

        public void Init(PlayerCaster caster)
        {
            _caster = caster;
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
            float reported = _castHold > 0f ? 0f : _smoothSpeed / Mathf.Max(0.01f, RunThreshold);

            _anim.SetFloat(SpeedId, reported);

            // Badan menghadap arah jalan. Saat berhenti (atau sedang menahan pose cast),
            // hadap terakhir dipertahankan — berputar balik ke "depan" tiap kali diam justru
            // membuatnya seperti boneka yang di-reset.
            if (_castHold <= 0f && speed > 0.25f && delta.sqrMagnitude > 0.000001f)
            {
                var want = Quaternion.LookRotation(delta.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, TurnSpeed * dt);
            }
        }
    }
}
