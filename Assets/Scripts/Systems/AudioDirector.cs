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

        [Tooltip("Bahan bunyi dari aset (Resources/AudioTheme). Kosong = jalur lama: " +
                 "Overrides, lalu sintesis. Diisi composition root, bukan Inspector — " +
                 "komponen ini lahir dari kode.")]
        public AudioTheme Theme;

        /// <summary>Untuk ducking: suara besar menyuruh musik menunduk. Boleh null.</summary>
        public MusicDirector Music;

        readonly AudioClip[] _clips = new AudioClip[SoundCount];
        readonly AudioSource[] _voices = new AudioSource[Voices];

        // Prioritas & umur tiap voice. Dipakai saat enam belas voice penuh: yang dicuri
        // prioritas terendah paling tua, dan permintaan tidak pernah mencuri ke atas.
        readonly int[] _voicePriority = new int[Voices];
        readonly float[] _voiceStarted = new float[Voices];

        // Jendela anti-dobel per KLIP — MinGap di bawah per KATEGORI; dua-duanya perlu.
        // Dua puluh musuh yang mati di frame yang sama memutar SATU splat, bukan dinding
        // suara dua puluh lapis.
        readonly System.Collections.Generic.Dictionary<AudioClip, float> _clipStarted =
            new System.Collections.Generic.Dictionary<AudioClip, float>(64);

        float _volume = 1f;
        float _pollAt;

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
            if (Time.unscaledTime - _lastPlayed[index] < MinGap[index]) return;

            var clip = Resolve(sound);
            if (clip == null) return;

            _lastPlayed[index] = Time.unscaledTime;
            PlayClip(clip, volumeScale, pitch, PriorityOf(sound));
        }

        /// <summary>Klip sebuah slot inti: tema (variasi diundi) -> Overrides -> sintesis.</summary>
        AudioClip Resolve(Sound sound)
        {
            int index = (int)sound;

            var pool = Theme != null ? ThemePool(sound) : null;
            if (pool != null && pool.Length > 0)
            {
                var pick = pool[Random.Range(0, pool.Length)];
                if (pick != null) return pick;
            }

            if (Overrides != null && index < Overrides.Length && Overrides[index] != null)
                return Overrides[index];

            return _clips[index];
        }

        AudioClip[] ThemePool(Sound sound)
        {
            switch (sound)
            {
                case Sound.Cast: return Theme.Cast;
                case Sound.Blast: return Theme.Blast;
                case Sound.Hit: return Theme.Hit;
                case Sound.Death: return Theme.Death;
                case Sound.Reaction: return Theme.Reaction;
                case Sound.Pickup: return Theme.Pickup;
                case Sound.BossRoar: return Theme.BossRoar;
                case Sound.WaveStart: return Theme.WaveStart;
                default: return null;
            }
        }

        /// <summary>
        /// Boss dan penanda ritme wave tidak boleh tercuri; reaksi mengalahkan bunyi rutin;
        /// antarmuka (prioritas 0, lewat jalur PlayClip) paling rela mengalah.
        /// </summary>
        static int PriorityOf(Sound sound)
        {
            switch (sound)
            {
                case Sound.BossRoar:
                case Sound.WaveStart: return 3;
                case Sound.Reaction: return 2;
                default: return 1;
            }
        }

        /// <summary>
        /// Jalur umum semua klip. Empat rem menyala di sini sekaligus: volume master,
        /// jendela anti-dobel per klip, pencurian voice berprioritas, dan acak nada.
        /// </summary>
        public void PlayClip(AudioClip clip, float volumeScale = 1f, float pitch = 1f,
            int priority = 1)
        {
            if (clip == null || _volume <= 0.001f) return;

            float window = Theme != null ? Theme.SameClipWindow : 0.045f;
            if (_clipStarted.TryGetValue(clip, out float started) &&
                Time.unscaledTime - started < window) return;

            var voice = TakeVoice(priority);
            if (voice == null) return;

            _clipStarted[clip] = Time.unscaledTime;

            voice.clip = clip;

            // Nada diacak sedikit tiap kali. Suara yang persis sama berulang-ulang berhenti
            // terdengar sebagai kejadian dan mulai terdengar sebagai kerusakan.
            voice.pitch = pitch * Random.Range(0.94f, 1.06f);
            voice.volume = Mathf.Clamp01(volumeScale) * _volume;
            voice.Play();

            // Suara sebesar ini pantas didengar sendirian — musiknya menunduk dulu.
            if (priority >= 3 && Music != null && Theme != null)
                Music.Duck(Theme.DuckAmount, Theme.DuckSeconds);
        }

        AudioSource TakeVoice(int priority)
        {
            int steal = -1;

            for (int i = 0; i < Voices; i++)
            {
                if (!_voices[i].isPlaying)
                {
                    _voicePriority[i] = priority;
                    _voiceStarted[i] = Time.unscaledTime;
                    return _voices[i];
                }

                if (steal < 0 ||
                    _voicePriority[i] < _voicePriority[steal] ||
                    (_voicePriority[i] == _voicePriority[steal] &&
                     _voiceStarted[i] < _voiceStarted[steal]))
                {
                    steal = i;
                }
            }

            // Semua sibuk dan yang paling lemah pun lebih penting dari permintaan ini.
            // Permintaan kecil yang datang saat enam belas suara penting berbunyi memang
            // layak hilang — itu bukan kegagalan, itu mixing.
            if (steal < 0 || _voicePriority[steal] > priority) return null;

            _voicePriority[steal] = priority;
            _voiceStarted[steal] = Time.unscaledTime;
            return _voices[steal];
        }

        // =====================================================================================
        //  jalur bertema — yang paling spesifik menang
        // =====================================================================================

        /// <summary>Cast sebuah piece: per-piece -> per-elemen -> slot inti -> sintesis.</summary>
        public void PlayCast(PieceDefinition def, bool heavy)
        {
            var clip = Theme != null ? Theme.CastClipFor(def, heavy) : null;

            if (clip != null)
            {
                // Rem per-kategori TETAP berlaku di jalur tema — tanpa ini, sepuluh skill
                // yang cooldown-nya serempak menembakkan sepuluh klip berbeda di frame yang
                // sama, dan dedup per-klip tidak menahan apa pun.
                int gate = (int)(heavy ? Sound.Blast : Sound.Cast);
                if (Time.unscaledTime - _lastPlayed[gate] < MinGap[gate]) return;
                _lastPlayed[gate] = Time.unscaledTime;

                // Volume dari tema, bukan angka mati — keseimbangan lawan musiknya disetel
                // telinga pemilik project di Inspector, sambil play mode jalan.
                PlayClip(clip, heavy ? Theme.CastHeavyVolume : Theme.CastLightVolume,
                    heavy ? 0.95f : 1.05f);
                return;
            }

            // Jalur lama apa adanya, termasuk nadanya: berat lebih rendah, ringan lebih
            // tinggi — papan yang penuh tetap terbaca lewat telinga saja.
            Play(heavy ? Sound.Blast : Sound.Cast, heavy ? 0.85f : 0.5f, heavy ? 0.9f : 1.15f);
        }

        public void PlayReaction(ReactionDefinition reaction)
        {
            var clip = Theme != null ? Theme.ReactionClipFor(reaction) : null;

            if (clip == null)
            {
                Play(Sound.Reaction);
                return;
            }

            // Rem kategori yang sama dengan jalur sintesis. Tanpa stempel ini, gerombolan
            // yang meledak berantai menembakkan puluhan reaction per detik dan satu-satunya
            // rem tersisa cuma jendela 45 ms per-klip.
            int gate = (int)Sound.Reaction;
            if (Time.unscaledTime - _lastPlayed[gate] < MinGap[gate]) return;
            _lastPlayed[gate] = Time.unscaledTime;

            PlayClip(clip, 0.9f, 1f, 2);
        }

        // ---- antarmuka: prioritas 0, paling rela mengalah saat layar sedang ramai ----

        public void UiPick() => PlayClip(Theme != null ? Theme.PiecePick : null, 0.8f, 1f, 0);
        public void UiPlace() => PlayClip(Theme != null ? Theme.PiecePlace : null, 0.85f, 1f, 0);
        public void UiHover() => PlayClip(Theme != null ? Theme.PieceHover : null, 0.45f, 1f, 0);
        public void UiBuy() => PlayClip(Theme != null ? Theme.Buy : null, 0.85f, 1f, 0);
        public void UiReroll() => PlayClip(Theme != null ? Theme.Reroll : null, 0.85f, 1f, 0);
        public void UiClick() => PlayClip(Theme != null ? Theme.Click : null, 0.7f, 1f, 0);
        public void UiClose() => PlayClip(Theme != null ? Theme.PanelClose : null, 0.7f, 1f, 0);

        // ---- fanfare: kejadian yang layak menyentuh musiknya ----

        public void EvolveFanfare()
        {
            if (Music != null && Theme != null && Theme.EvolveStinger != null)
                Music.PlayStinger(Theme.EvolveStinger);
            else Play(Sound.Reaction);
        }

        /// <summary>Boss tumbang. Fanfare penuh sengaja disimpan untuk ini — bukan tiap wave.</summary>
        public void VictoryFanfare()
        {
            if (Music != null && Theme != null) Music.PlayStinger(Theme.WinStinger);
        }

        public void GameOverFanfare()
        {
            if (Music == null || Theme == null) return;

            Music.StopLoop();
            Music.PlayStinger(Theme.LoseStinger);
        }

        void Update()
        {
            // Slider SFX boleh digeser lewat ESC di tengah run; GameSettings tidak punya
            // event, jadi dibaca ulang dua kali sedetik.
            if (Time.unscaledTime < _pollAt) return;

            _pollAt = Time.unscaledTime + 0.5f;
            _volume = Mathf.Clamp01(GameSettings.Load().SfxVolume);
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
