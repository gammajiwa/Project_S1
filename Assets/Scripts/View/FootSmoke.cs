using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Asap yang mengepul di kaki pemain, menebal saat ia bergerak.
    ///
    /// Permintaan pemilik project: <i>"kayak keluar asap kayak jin, warna hitam, ceritanya kan
    /// ini grim reaper, waktu jalan biar nggak polosan"</i>.
    ///
    /// Bukan sekadar efek yang dipasang lalu dibiarkan. Yang membuat asap terbaca sebagai
    /// KELUAR DARI benda yang bergerak — bukan sebagai kabut yang kebetulan ada di situ — adalah
    /// hubungannya dengan gerak: diam mengepul tipis, berjalan mengepul tebal, dan jeda di antara
    /// keduanya cukup panjang supaya perubahannya terbaca sebagai sebab-akibat, bukan sebagai
    /// saklar.
    ///
    /// Kecepatan diukur dari PERPINDAHAN POSISI, bukan dari komponen gerak apa pun. Pemain
    /// digerakkan <see cref="PlayerMotor"/>, tapi ia juga bisa dipindahkan Blink, seret Vortex,
    /// atau lontaran ForcePush — dan asap yang cuma tahu tentang motor akan diam persis di
    /// saat-saat paling dramatis.
    /// </summary>
    [AddComponentMenu("Grimoire/Foot Smoke")]
    public class FootSmoke : MonoBehaviour
    {
        [Tooltip("Laju keluar saat pemain diam. Nol = asapnya hilang sama sekali kalau berhenti, " +
                 "dan reaper yang berhenti mengepul berhenti terlihat hidup.")]
        [Min(0f)] public float IdleRate = 6f;

        [Tooltip("Laju keluar saat berlari penuh.")]
        [Min(0f)] public float MoveRate = 34f;

        [Tooltip("Kecepatan yang dihitung sebagai 'lari penuh', unit per detik.")]
        [Min(0.1f)] public float FullSpeed = 6f;

        [Tooltip("Seberapa cepat laju menyesuaikan. Kecil = malas, besar = menyentak. " +
                 "Asap punya massa; yang menyentak terbaca sebagai keran yang dibuka-tutup.")]
        [Min(0.1f)] public float Responsiveness = 3.5f;

        ParticleSystem[] _systems;
        float[] _baseRate;

        Vector3 _lastPos;
        float _speed;

        void Awake()
        {
            _systems = GetComponentsInChildren<ParticleSystem>(true);
            _baseRate = new float[_systems.Length];

            // Laju ASLI tiap sistem disimpan sebagai patokan proporsi. Efek pack sering punya
            // beberapa sistem dengan laju yang jauh berbeda — inti, kepulan, dan percikan — dan
            // menyeragamkan angkanya akan membuang keseimbangan yang sudah disetel pembuatnya.
            for (int i = 0; i < _systems.Length; i++)
            {
                _baseRate[i] = _systems[i].emission.rateOverTime.constant;
                if (_baseRate[i] <= 0.001f) _baseRate[i] = 1f;
            }

            _lastPos = transform.position;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 now = transform.position;
            Vector3 step = now - _lastPos;
            step.y = 0f;                       // naik-turun animasi bukan berjalan
            _lastPos = now;

            float instant = step.magnitude / dt;
            _speed = Mathf.Lerp(_speed, instant, 1f - Mathf.Exp(-Responsiveness * dt));

            float t = Mathf.Clamp01(_speed / FullSpeed);
            float wanted = Mathf.Lerp(IdleRate, MoveRate, t);

            for (int i = 0; i < _systems.Length; i++)
            {
                if (_systems[i] == null) continue;

                var emission = _systems[i].emission;
                var rate = emission.rateOverTime;

                // Proporsional terhadap laju aslinya, bukan diseragamkan.
                rate.constant = wanted * (_baseRate[i] / _baseRate[0]);
                emission.rateOverTime = rate;
            }
        }
    }
}
