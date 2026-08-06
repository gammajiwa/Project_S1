using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Suara. Sebelum ini tidak ada satu pun <c>AudioSource</c> di seluruh project, dan slider
    /// volume di menu menyimpan angka yang tidak menggerakkan apa pun.
    ///
    /// Klipnya DIBANGKITKAN, bukan diimpor — filosofi yang sama dengan ikon placeholder. Menunggu
    /// aset audio berarti game penuh ledakan ini tetap senyap sampai entah kapan, dan kesenyapan
    /// itu adalah hal pertama yang terdengar di rekaman untuk client. Sintesisnya kasar dan memang
    /// seharusnya begitu: yang dibeli di sini adalah UMPAN BALIK, bukan kualitas produksi.
    ///
    /// Menggantinya nanti cukup mengisi array <see cref="Overrides"/> — tidak ada pemanggil yang
    /// perlu berubah.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public enum Sound
        {
            Cast,
            Blast,
            Hit,
            Death,
            Reaction,
            Pickup,
            BossRoar,
            WaveStart
        }

        const int SoundCount = 8;
        const int Voices = 16;
        const int SampleRate = 44100;

        [Tooltip("Isi slot mana pun untuk menimpa suara bangkitan dengan file asli. " +
                 "Kosong = pakai yang disintesis.")]
        public AudioClip[] Overrides = new AudioClip[SoundCount];

        readonly AudioClip[] _clips = new AudioClip[SoundCount];
        readonly AudioSource[] _voices = new AudioSource[Voices];

        int _cursor;
        float _volume = 1f;

        /// <summary>
        /// Dua suara identik dalam frame yang sama saling menumpuk jadi satu bunyi dua kali lebih
        /// keras, dan dua puluh musuh yang mati bersamaan jadi ledakan yang memekakkan. Jeda
        /// minimum per jenis suara adalah rem termurah untuk itu.
        /// </summary>
        readonly float[] _lastPlayed = new float[SoundCount];

        static readonly float[] MinGap =
        {
            0.04f,  // Cast
            0.06f,  // Blast
            0.05f,  // Hit
            0.05f,  // Death
            0.12f,  // Reaction
            0.05f,  // Pickup
            0f,     // BossRoar
            0f      // WaveStart
        };

        public void Init(float volume)
        {
            _volume = Mathf.Clamp01(volume);

            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(transform, false);

                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;

                // 2D: pemain diam di tengah layar dan seluruh aksi terjadi di sekitarnya. Suara
                // berposisi cuma akan memindahkan ledakan ke satu telinga tanpa memberi informasi.
                source.spatialBlend = 0f;
                _voices[i] = source;
            }

            for (int i = 0; i < SoundCount; i++) _clips[i] = Synthesise((Sound)i);
        }

        public void SetVolume(float volume) => _volume = Mathf.Clamp01(volume);

        public void Play(Sound sound, float volumeScale = 1f, float pitch = 1f)
        {
            int index = (int)sound;
            if (_volume <= 0.001f) return;
            if (Time.unscaledTime - _lastPlayed[index] < MinGap[index]) return;

            var clip = Overrides != null && index < Overrides.Length && Overrides[index] != null
                ? Overrides[index]
                : _clips[index];

            if (clip == null) return;

            _lastPlayed[index] = Time.unscaledTime;

            var voice = _voices[_cursor];
            _cursor = (_cursor + 1) % Voices;

            voice.clip = clip;

            // Nada diacak sedikit tiap kali. Suara yang persis sama berulang-ulang berhenti
            // terdengar sebagai kejadian dan mulai terdengar sebagai kerusakan.
            voice.pitch = pitch * Random.Range(0.94f, 1.06f);
            voice.volume = Mathf.Clamp01(volumeScale) * _volume;
            voice.Play();
        }

        // =====================================================================================
        //  sintesis
        // =====================================================================================

        static AudioClip Synthesise(Sound sound)
        {
            switch (sound)
            {
                // Naik cepat lalu dipotong: terbaca sebagai "sesuatu berangkat".
                case Sound.Cast: return Tone("sfx_cast", 0.18f, 420f, 900f, 0.35f, 0f);

                // Derau dengan peluruhan tajam. Ini bunyi ledakan yang paling murah dan paling jujur.
                case Sound.Blast: return Noise("sfx_blast", 0.42f, 0.8f, 1f);

                case Sound.Hit: return Noise("sfx_hit", 0.09f, 0.35f, 1f);

                // Lebih rendah dan lebih pendek dari Blast, supaya kematian satu musuh tidak
                // terdengar sebesar skill yang membunuhnya.
                case Sound.Death: return Noise("sfx_death", 0.16f, 0.45f, 0.5f);

                // Dua nada bersamaan = dentang. Ini satu-satunya suara "hadiah" di daftar, jadi
                // ia harus terdengar beda jenis, bukan cuma beda tinggi.
                case Sound.Reaction: return Chime("sfx_reaction", 0.5f, 780f, 1170f);

                case Sound.Pickup: return Tone("sfx_pickup", 0.22f, 620f, 1240f, 0.5f, 0f);

                // Turun, panjang, dan kasar. Turun karena naik terdengar seperti hadiah.
                case Sound.BossRoar: return Tone("sfx_boss", 1.6f, 220f, 70f, 0.9f, 0.35f);

                case Sound.WaveStart: return Chime("sfx_wave", 0.6f, 330f, 495f);

                default: return null;
            }
        }

        /// <summary>Gelombang gigi gergaji dengan sapuan nada. <paramref name="grit"/> mencampur derau.</summary>
        static AudioClip Tone(string name, float seconds, float fromHz, float toHz,
            float decay, float grit)
        {
            int count = Mathf.CeilToInt(SampleRate * seconds);
            var data = new float[count];
            float phase = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float hz = Mathf.Lerp(fromHz, toHz, t);

                phase += hz / SampleRate;
                if (phase > 1f) phase -= 1f;

                // Gigi gergaji, bukan sinus: sinus murni terdengar seperti nada uji, bukan efek.
                float wave = phase * 2f - 1f;
                if (grit > 0f) wave = Mathf.Lerp(wave, Random.Range(-1f, 1f), grit);

                data[i] = wave * Mathf.Pow(1f - t, decay * 6f) * 0.35f;
            }

            return Bake(name, data);
        }

        static AudioClip Noise(string name, float seconds, float decay, float lowness)
        {
            int count = Mathf.CeilToInt(SampleRate * seconds);
            var data = new float[count];
            float smoothed = 0f;

            // Rata-rata bergerak = tapis rendah murahan. Tanpa itu semuanya terdengar seperti
            // desis, dan ledakan besar tidak bisa dibedakan dari ledakan kecil.
            float blend = Mathf.Clamp01(1f - lowness * 0.85f);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                smoothed = Mathf.Lerp(smoothed, Random.Range(-1f, 1f), Mathf.Max(0.02f, blend));
                data[i] = smoothed * Mathf.Pow(1f - t, decay * 6f) * 0.45f;
            }

            return Bake(name, data);
        }

        static AudioClip Chime(string name, float seconds, float lowHz, float highHz)
        {
            int count = Mathf.CeilToInt(SampleRate * seconds);
            var data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float a = Mathf.Sin(2f * Mathf.PI * lowHz * i / SampleRate);
                float b = Mathf.Sin(2f * Mathf.PI * highHz * i / SampleRate) * 0.6f;

                data[i] = (a + b) * Mathf.Pow(1f - t, 2.5f) * 0.3f;
            }

            return Bake(name, data);
        }

        static AudioClip Bake(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
