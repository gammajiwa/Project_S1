using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Musik menu utama + bunyi klik tombolnya. Lahir dari kode lewat <see cref="Ensure"/> —
    /// SENGAJA bukan objek di scene: scene menu digenerate builder dan sudah dua kali
    /// menggilas penataan tangan; komponen runtime tidak pernah ikut tergilas.
    ///
    /// Scene menu tidak punya AudioListener (hanya kamera run yang membawanya), jadi
    /// komponen ini sekalian membawa telinganya sendiri — dengan penjaga: kalau suatu
    /// hari scene menu punya listener, di sini tidak menambah yang kedua.
    /// </summary>
    public class MenuMusic : MonoBehaviour
    {
        static MenuMusic _alive;

        AudioTheme _theme;
        AudioSource _loop;
        AudioSource _blip;
        float _pollAt;

        /// <summary>Aman dipanggil berkali-kali; menu yang dimuat ulang memakai yang lama.</summary>
        public static void Ensure()
        {
            if (_alive != null) return;

            var go = new GameObject("MenuMusic");
            _alive = go.AddComponent<MenuMusic>();
        }

        /// <summary>Klik tombol menu. Diam tanpa error kalau tema/klipnya belum ada.</summary>
        public static void Click()
        {
            if (_alive == null || _alive._theme == null || _alive._theme.Click == null) return;

            _alive._blip.PlayOneShot(_alive._theme.Click,
                0.7f * Mathf.Clamp01(GameSettings.Load().SfxVolume));
        }

        void Awake()
        {
            _theme = Resources.Load<AudioTheme>("AudioTheme");

            if (_theme == null)
            {
                // Tanpa tema, menu kembali sunyi seperti sebelumnya — bukan error.
                Destroy(gameObject);
                return;
            }

            if (FindFirstObjectByType<AudioListener>() == null)
            {
                gameObject.AddComponent<AudioListener>();
            }

            // Blip dibuat TANPA syarat: kontrak AudioTheme bilang slot mana pun boleh
            // kosong sendiri-sendiri — MenuLoop yang kosong tidak boleh ikut membunuh
            // bunyi klik, dua slot itu tidak saling kenal.
            _blip = gameObject.AddComponent<AudioSource>();
            _blip.playOnAwake = false;
            _blip.spatialBlend = 0f;

            if (_theme.MenuLoop != null)
            {
                _loop = gameObject.AddComponent<AudioSource>();
                _loop.playOnAwake = false;
                _loop.loop = true;
                _loop.spatialBlend = 0f;
                _loop.clip = _theme.MenuLoop;
                _loop.volume = Volume();
                _loop.Play();
            }
        }

        float Volume() => Mathf.Clamp01(GameSettings.Load().MusicVolume) * _theme.MusicTrim;

        void Update()
        {
            // Slider MUSIC di halaman setelan menu harus terasa saat digeser, tanpa event
            // dari GameSettings — dibaca ulang dua kali sedetik.
            if (Time.unscaledTime < _pollAt) return;

            _pollAt = Time.unscaledTime + 0.5f;
            if (_loop != null) _loop.volume = Volume();
        }

        void OnDestroy()
        {
            if (_alive == this) _alive = null;
        }
    }
}
