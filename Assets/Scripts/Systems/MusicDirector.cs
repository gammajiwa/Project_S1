using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Musik dalam run: satu loop yang berganti lewat silih (crossfade), stinger sekali
    /// bunyi di atasnya, dan ducking — musik menunduk sejenak saat ada yang lebih penting
    /// berbunyi, lalu bangkit pelan.
    ///
    /// Seluruh geraknya memakai waktu UNSCALED: game punya kecepatan 1x-5x dan jeda
    /// build-phase menghentikan Time.timeScale — musik tidak boleh ikut ngebut ataupun beku.
    ///
    /// Klipnya dari <see cref="AudioTheme"/>; tema kosong berarti komponen ini diam
    /// seumur hidup tanpa satu pun error, kontrak yang sama dengan UiTheme.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        AudioTheme _theme;

        AudioSource _from;
        AudioSource _to;
        AudioSource _stinger;

        float _fade = 1f;
        float _duck = 1f;
        float _duckHeldUntil;

        float _userVolume = 0.7f;
        float _pollAt;

        public void Init(AudioTheme theme, float userVolume)
        {
            _theme = theme;
            _userVolume = Mathf.Clamp01(userVolume);

            _from = MakeSource("MusicA");
            _to = MakeSource("MusicB");
            _stinger = MakeSource("MusicStinger");
            _stinger.loop = false;

            // PlayOneShot MENGALIKAN volume source-nya. Nol milik crossfade membuat semua
            // stinger bisu total — kesalahan yang tidak berbunyi, harfiah.
            _stinger.volume = 1f;
        }

        AudioSource MakeSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        /// <summary>
        /// Ganti loop. Idempoten: meminta loop yang sedang berbunyi tidak melakukan apa-apa,
        /// jadi pemanggil boleh meneriakkannya tiap wave tanpa musiknya tersendat.
        /// </summary>
        public void SetLoop(AudioClip clip)
        {
            if (_to == null || clip == null) return;
            if (_to.clip == clip && _to.isPlaying) return;

            // Yang sedang menuju penuh jadi sumber silih berikutnya.
            (_from, _to) = (_to, _from);

            _to.clip = clip;
            _to.Play();
            _fade = 0f;
        }

        /// <summary>Hentikan loop — dipakai game over, supaya stinger kalah berbunyi sendirian.</summary>
        public void StopLoop()
        {
            if (_to == null) return;

            // Silih menuju sumber yang diam: mekanisme fade yang sama, tujuan kosong.
            (_from, _to) = (_to, _from);
            _to.clip = null;
            _to.Stop();
            _fade = 0f;
        }

        public void PlayStinger(AudioClip clip)
        {
            if (_stinger == null || clip == null || _theme == null) return;

            _stinger.PlayOneShot(clip, _userVolume * _theme.StingerTrim);
            Duck(_theme.DuckAmount, Mathf.Max(_theme.DuckSeconds, clip.length * 0.6f));
        }

        /// <summary>Turunkan volume loop sementara. Turunnya seketika, pulihnya pelan.</summary>
        public void Duck(float amount, float seconds)
        {
            _duck = Mathf.Min(_duck, 1f - Mathf.Clamp01(amount));
            _duckHeldUntil = Time.unscaledTime + Mathf.Max(0.1f, seconds);
        }

        public void SetUserVolume(float volume) => _userVolume = Mathf.Clamp01(volume);

        void Update()
        {
            if (_theme == null || _to == null) return;

            float dt = Time.unscaledDeltaTime;

            if (_fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + dt / Mathf.Max(0.1f, _theme.CrossfadeSeconds));
                if (_fade >= 1f && _from.isPlaying) _from.Stop();
            }

            if (_duck < 1f && Time.unscaledTime > _duckHeldUntil)
            {
                _duck = Mathf.Min(1f, _duck + dt / 1.4f);
            }

            float bed = _userVolume * _theme.MusicTrim * _duck;
            _to.volume = bed * _fade;
            _from.volume = bed * (1f - _fade);

            // Slider MUSIC di setelan boleh digeser kapan pun, termasuk lewat ESC di tengah
            // run. Tidak ada event dari GameSettings, jadi dibaca ulang dua kali sedetik —
            // cukup rapat untuk terasa langsung, cukup renggang untuk tidak terasa di CPU.
            if (Time.unscaledTime >= _pollAt)
            {
                _pollAt = Time.unscaledTime + 0.5f;
                _userVolume = Mathf.Clamp01(GameSettings.Load().MusicVolume);
            }
        }
    }
}
