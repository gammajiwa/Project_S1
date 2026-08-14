using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Binds the settings widgets to a <see cref="GameSettings"/> handed in by the menu controller.
    /// Discrete options use steppers rather than dropdowns so every row reads the same way.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Layar penuh")]
        [Header("Bahasa")]
        [SerializeField] Button _languagePrev;
        [SerializeField] Button _languageNext;
        [SerializeField] TextMeshProUGUI _languageValue;

        [SerializeField] Button _fullscreenPrev;
        [SerializeField] Button _fullscreenNext;
        [SerializeField] TextMeshProUGUI _fullscreenValue;

        [Header("Resolusi")]
        [SerializeField] Button _resolutionPrev;
        [SerializeField] Button _resolutionNext;
        [SerializeField] TextMeshProUGUI _resolutionValue;

        [Header("VSync")]
        [SerializeField] Button _vsyncPrev;
        [SerializeField] Button _vsyncNext;
        [SerializeField] TextMeshProUGUI _vsyncValue;

        [Header("Batas FPS")]
        [SerializeField] Button _frameCapPrev;
        [SerializeField] Button _frameCapNext;
        [SerializeField] TextMeshProUGUI _frameCapValue;

        [Header("Suara")]
        [SerializeField] Slider _masterSlider;
        [SerializeField] TextMeshProUGUI _masterValue;
        [SerializeField] Slider _sfxSlider;
        [SerializeField] TextMeshProUGUI _sfxValue;
        [SerializeField] Slider _musicSlider;
        [SerializeField] TextMeshProUGUI _musicValue;

        // Tiga baris performa. Stepper dua tombol seperti baris lain — bukan checkbox — supaya
        // seluruh panel terbaca dengan satu kebiasaan.
        [Header("Teks damage")]
        [SerializeField] Button _damageTextPrev;
        [SerializeField] Button _damageTextNext;
        [SerializeField] TextMeshProUGUI _damageTextValue;

        [Header("Bayangan musuh")]
        [SerializeField] Button _enemyShadowsPrev;
        [SerializeField] Button _enemyShadowsNext;
        [SerializeField] TextMeshProUGUI _enemyShadowsValue;

        [Header("VFX cuaca")]
        [SerializeField] Button _weatherVfxPrev;
        [SerializeField] Button _weatherVfxNext;
        [SerializeField] TextMeshProUGUI _weatherVfxValue;

        [Header("Data")]
        [SerializeField] Button _resetCodex;
        [SerializeField] TextMeshProUGUI _resetLabel;
        [SerializeField] TextMeshProUGUI _resetHint;

        [Header("Catatan")]
        [SerializeField] TextMeshProUGUI _note;

        [SerializeField] Color _dangerColor = new Color(0.85f, 0.35f, 0.3f, 1f);
        [SerializeField] Color _mutedColor = new Color(0.55f, 0.545f, 0.58f, 1f);

        const float ResetArmSeconds = 4f;

        GameSettings _settings;
        List<Vector2Int> _resolutions;
        int _resolutionIndex;
        int _frameCapIndex;
        float _resetArmedUntil;

        public void Init(GameSettings settings)
        {
            _settings = settings;

            _resolutions = _settings.AvailableResolutions();
            _resolutionIndex = Mathf.Max(0,
                _resolutions.IndexOf(new Vector2Int(_settings.Width, _settings.Height)));

            _frameCapIndex = System.Array.IndexOf(GameSettings.FrameCapChoices, _settings.FrameCap);
            if (_frameCapIndex < 0) _frameCapIndex = 0;

            Wire(_languagePrev, () => StepLanguage(-1));
            Wire(_languageNext, () => StepLanguage(1));
            Wire(_fullscreenPrev, () => ToggleFullscreen());
            Wire(_fullscreenNext, () => ToggleFullscreen());
            Wire(_resolutionPrev, () => StepResolution(-1));
            Wire(_resolutionNext, () => StepResolution(1));
            Wire(_vsyncPrev, () => ToggleVSync());
            Wire(_vsyncNext, () => ToggleVSync());
            Wire(_frameCapPrev, () => StepFrameCap(-1));
            Wire(_frameCapNext, () => StepFrameCap(1));
            Wire(_damageTextPrev, () => ToggleDamageText());
            Wire(_damageTextNext, () => ToggleDamageText());
            Wire(_enemyShadowsPrev, () => ToggleEnemyShadows());
            Wire(_enemyShadowsNext, () => ToggleEnemyShadows());
            Wire(_weatherVfxPrev, () => ToggleWeatherVfx());
            Wire(_weatherVfxNext, () => ToggleWeatherVfx());
            Wire(_resetCodex, ResetCodex);

            BindSlider(_masterSlider, _settings.MasterVolume, value =>
            {
                _settings.MasterVolume = value;
                AudioListener.volume = value;
                SetPercent(_masterValue, value);
            });

            BindSlider(_sfxSlider, _settings.SfxVolume, value =>
            {
                _settings.SfxVolume = value;
                SetPercent(_sfxValue, value);
            });

            BindSlider(_musicSlider, _settings.MusicVolume, value =>
            {
                _settings.MusicVolume = value;
                SetPercent(_musicValue, value);
            });

            if (_note != null)
            {
                _note.text = (Application.isEditor
                    ? "Resolusi & layar penuh cuma berlaku di build, bukan di Editor."
                    : "Batas FPS diabaikan selama VSync hidup.")
                    + "   Baris PERFORMA berlaku mulai run berikutnya.";
            }

            Redraw();
        }

        /// <summary>
        /// Bahasa berikutnya di daftar, melingkar. Berlaku SEKETIKA - bukan "berlaku setelah
        /// restart" - karena orang yang sedang mencari bahasanya sendiri di menu asing perlu
        /// tahu ia sudah menemukannya, dan satu-satunya bukti yang bisa dibacanya adalah
        /// layarnya berubah.
        /// </summary>
        void StepLanguage(int direction)
        {
            var all = Loc.All;

            int at = System.Array.IndexOf(all, Loc.Current);
            if (at < 0) at = 0;

            Loc.Use(all[((at + direction) % all.Length + all.Length) % all.Length]);
            Redraw();
        }

        void OnDisable()
        {
            // Sliders fire every frame while dragged; writing prefs once on the way out is enough.
            _settings?.Save();
            DisarmReset();
        }

        void Update()
        {
            if (_resetArmedUntil <= 0f) return;
            if (Time.unscaledTime < _resetArmedUntil) return;

            DisarmReset();
        }

        // ---------- rows ----------

        void ToggleFullscreen()
        {
            _settings.Fullscreen = !_settings.Fullscreen;
            ApplyAndRedraw();
        }

        void ToggleVSync()
        {
            _settings.VSync = !_settings.VSync;
            ApplyAndRedraw();
        }

        // Ketiganya dibaca scene game saat lahir, bukan dipantau — jadi mengubahnya di sini
        // baru terasa di run BERIKUTNYA. Catatan di bawah panel yang memberi tahu itu.

        void ToggleDamageText()
        {
            _settings.DamageText = !_settings.DamageText;
            ApplyAndRedraw();
        }

        void ToggleEnemyShadows()
        {
            _settings.EnemyShadows = !_settings.EnemyShadows;
            ApplyAndRedraw();
        }

        void ToggleWeatherVfx()
        {
            _settings.WeatherVfx = !_settings.WeatherVfx;
            ApplyAndRedraw();
        }

        void StepResolution(int direction)
        {
            if (_resolutions.Count == 0) return;

            _resolutionIndex = Wrap(_resolutionIndex + direction, _resolutions.Count);
            _settings.Width = _resolutions[_resolutionIndex].x;
            _settings.Height = _resolutions[_resolutionIndex].y;
            ApplyAndRedraw();
        }

        void StepFrameCap(int direction)
        {
            _frameCapIndex = Wrap(_frameCapIndex + direction, GameSettings.FrameCapChoices.Length);
            _settings.FrameCap = GameSettings.FrameCapChoices[_frameCapIndex];
            ApplyAndRedraw();
        }

        void ResetCodex()
        {
            if (_resetArmedUntil <= 0f)
            {
                // Wiping the only persistent data in the game deserves a second press.
                _resetArmedUntil = Time.unscaledTime + ResetArmSeconds;
                if (_resetLabel != null)
                {
                    _resetLabel.text = "KLIK LAGI KALAU YAKIN";
                    _resetLabel.color = _dangerColor;
                }

                return;
            }

            var log = DiscoveryLog.Load();
            log.Clear();

            DisarmReset();
            if (_resetHint != null) _resetHint.text = "Codex dikosongkan.";
        }

        void DisarmReset()
        {
            _resetArmedUntil = 0f;

            if (_resetLabel != null)
            {
                _resetLabel.text = "KOSONGKAN CODEX";
                _resetLabel.color = _mutedColor;
            }
        }

        // ---------- drawing ----------

        void ApplyAndRedraw()
        {
            _settings.Apply();
            _settings.Save();
            Redraw();
        }

        void Redraw()
        {
            // Nama bahasa DALAM bahasanya sendiri. Daftar yang ditulis dalam bahasa yang sedang
            // aktif tidak berguna untuk orang yang tidak bisa membacanya - dan orang itu persis
            // yang sedang mencari daftar ini.
            if (_languageValue != null) _languageValue.text = Loc.NativeNameOf(Loc.Current);

            SetText(_fullscreenValue, _settings.Fullscreen ? "LAYAR PENUH" : "JENDELA");
            SetText(_vsyncValue, _settings.VSync ? "HIDUP" : "MATI");
            SetText(_frameCapValue, GameSettings.FrameCapLabel(_settings.FrameCap));

            SetText(_resolutionValue, _resolutions != null && _resolutions.Count > 0
                ? GameSettings.ResolutionLabel(_resolutions[_resolutionIndex])
                : "-");

            SetPercent(_masterValue, _settings.MasterVolume);
            SetPercent(_sfxValue, _settings.SfxVolume);
            SetPercent(_musicValue, _settings.MusicVolume);

            SetText(_damageTextValue, _settings.DamageText ? "HIDUP" : "MATI");
            SetText(_enemyShadowsValue, _settings.EnemyShadows ? "HIDUP" : "MATI");
            SetText(_weatherVfxValue, _settings.WeatherVfx ? "HIDUP" : "MATI");

            // The cap row is dead weight while vSync owns the frame rate — say so instead of lying.
            if (_frameCapValue != null)
            {
                _frameCapValue.alpha = _settings.VSync ? 0.4f : 1f;
            }
        }

        // ---------- helpers ----------

        static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        static void BindSlider(Slider slider, float value, UnityEngine.Events.UnityAction<float> action)
        {
            if (slider == null) return;

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(action);
        }

        static void SetText(TextMeshProUGUI label, string value)
        {
            if (label != null) label.text = value;
        }

        static void SetPercent(TextMeshProUGUI label, float value)
        {
            if (label != null) label.text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }

        static int Wrap(int index, int count) => count <= 0 ? 0 : ((index % count) + count) % count;
    }
}
