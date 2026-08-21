using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Nyawa visual pemain: memilih antara dua animasi yang dimiliki model (Idle dan Run),
    /// dan menghadapkan badan ke arah jalan.
    ///
    /// SENGAJA POLOS. Versi 2026-08-10 sempat penuh penyangga (state Run lengket, paksa-lari
    /// selama dikepung, histeresis berlapis) karena klip lari saat itu membawa perpindahan dan
    /// tiap pergantian pose terbaca sebagai badan yang mental balik. Pemilik project kemudian
    /// mengekspor ulang animasinya DIAM DI TEMPAT dari sumbernya — akar masalahnya mati, dan
    /// seluruh penyangga itu dicabut atas perintahnya. Kalau gejala "mental balik" muncul lagi,
    /// periksa KLIPNYA dulu, jangan pasang ulang penyangga di sini.
    ///
    /// Aturan yang tersisa cuma dua, dua-duanya keputusan pemilik project:
    /// - jalan = Run, diam = Idle;
    /// - menembak memakai pose Idle HANYA saat berdiri — sambil lari nge-skill tetap lari.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAvatar : MonoBehaviour
    {
        [Tooltip("Kecepatan (unit/detik) yang dianggap mulai berjalan.")]
        [Min(0.01f)] public float RunThreshold = 0.6f;

        [Tooltip("Berapa lama pose Idle ditahan tiap kali sebuah skill menembak (saat berdiri).")]
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

            // Pose cast hanya menang saat pemain BERDIRI — menembak sambil lari tetap lari.
            bool moving = _smoothSpeed >= RunThreshold;
            float reported = !moving && _castHold > 0f
                ? 0f
                : _smoothSpeed / Mathf.Max(0.01f, RunThreshold);

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
