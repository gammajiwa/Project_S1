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
            //
            // Laju NOL berarti sistem itu bukan pengepul: dia sub-emitter (gelembung yang
            // meletus saat mati) dan hanya boleh menyala lewat induknya. Versi lama menclamp
            // 0 -> 1 lalu ikut memodulasi — sub-emitter-nya bocor jadi keran kedua yang
            // mengepul dari titik yang salah.
            for (int i = 0; i < _systems.Length; i++)
                _baseRate[i] = _systems[i].emission.rateOverTime.constant;

            _lastPos = transform.position;
        }

        /// <summary>
        /// Menyalakan pengepulnya sendiri. Efek ini diauthor dengan <b>Play On Awake mati di
        /// SEMUA sistemnya</b> — termasuk dua pengepul aslinya — jadi tanpa ini tidak ada satu
        /// pun yang pernah mulai, dan menyetel <c>rateOverTime</c> ke sistem yang berhenti tidak
        /// mengeluarkan satu partikel pun: keran diputar di pipa yang belum dibuka.
        ///
        /// Dinyalakan dari sini, bukan dengan mencentang balik Play On Awake di prefab, karena
        /// centang itu berlaku untuk sub-emitter juga — dan sub-emitter yang berjalan sendiri
        /// mengepul dari titiknya sendiri alih-alih menunggu partikel induknya mati. Aturan
        /// "laju nol = sub-emitter" sudah dipakai keran di bawah; di sini ia dipakai lagi.
        ///
        /// <c>Play(false)</c>, bukan <c>Play()</c>: yang terakhir ikut menyalakan anak-anaknya
        /// dan mengembalikan kebocoran yang sama lewat pintu belakang.
        /// </summary>
        void OnEnable()
        {
            if (_systems == null) return;

            for (int i = 0; i < _systems.Length; i++)
            {
                if (_systems[i] == null) continue;
                if (_baseRate[i] <= 0.001f) continue;   // sub-emitter: induknya yang menyalakan
                if (!_systems[i].isPlaying) _systems[i].Play(false);
            }
        }

        /// <summary>Laju asli pengepul pertama yang benar-benar mengepul. Tak ada = 1.</summary>
        float Reference()
        {
            for (int i = 0; i < _baseRate.Length; i++)
            {
                if (_baseRate[i] > 0.001f) return _baseRate[i];
            }

            return 1f;
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
            // Patokannya pengepul PERTAMA yang lajunya bukan nol, bukan `_baseRate[0]` mentah:
            // kalau sistem paling atas kebetulan sub-emitter, membaginya dengan 0,001 melipatkan
            // laju semua yang lain seribu kali.
            float reference = Reference();

            for (int i = 0; i < _systems.Length; i++)
            {
                if (_systems[i] == null) continue;
                if (_baseRate[i] <= 0.001f) continue;  // sub-emitter: bukan urusan keran ini

                var emission = _systems[i].emission;
                var rate = emission.rateOverTime;

                // Dipaksa jadi satu angka. Pengepul utamanya diauthor sebagai DUA konstanta
                // (2..5), dan menulis `constant` ke kurva semacam itu cuma mengganti batas
                // ATASNYA — batas bawahnya tetap 2, jadi laju yang diminta 30 keluar sebagai
                // undian 2..30. Yang sedang dikendalikan di sini adalah lajunya sendiri; ia
                // harus berarti persis angka itu.
                rate.mode = ParticleSystemCurveMode.Constant;

                // Proporsional terhadap laju aslinya, bukan diseragamkan.
                rate.constant = wanted * (_baseRate[i] / reference);
                emission.rateOverTime = rate;
            }
        }
    }
}
