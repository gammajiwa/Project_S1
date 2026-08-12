using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Owns the menu's page switching and the one <see cref="GameSettings"/> instance the scene
    /// uses. Nothing here is static: the game scene loads its own copy from prefs.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        enum Page
        {
            Root,
            Starter,
            Codex,
            Settings
        }

        [Header("Scene")]
        [SerializeField] string _gameSceneName = "Proto";

        [Header("Layar muat")]
        [Tooltip("Sigil yang berputar di tengah tirai. Kosong = tirai polos tanpa sigil.")]
        [SerializeField] Sprite _loadingSigil;

        [SerializeField] Color _loadingInk = new Color(0.72f, 0.5f, 1f, 1f);

        [Header("Halaman")]
        [SerializeField] GameObject _rootPage;
        [SerializeField] GameObject _starterPage;
        [SerializeField] GameObject _codexPage;
        [SerializeField] GameObject _settingsPage;

        [Header("Tombol utama")]
        [SerializeField] Button _playButton;
        [SerializeField] Button _codexButton;
        [SerializeField] Button _settingsButton;
        [SerializeField] Button _quitButton;

        [Header("Tombol kembali")]
        [SerializeField] Button _starterBackButton;
        [SerializeField] Button _codexBackButton;
        [SerializeField] Button _settingsBackButton;

        [Header("Isi")]
        [SerializeField] CodexPanel _codexPanel;
        [SerializeField] SettingsPanel _settingsPanel;
        [SerializeField] TextMeshProUGUI _versionLabel;

        GameSettings _settings;
        Page _page = Page.Root;

        void Awake()
        {
            // A run that quit mid-wave can leave time scaled; the menu must never inherit that.
            Time.timeScale = 1f;

            // Matches the game scene: without it the drifting backdrop freezes whenever focus is lost.
            Application.runInBackground = true;

            _settings = GameSettings.Load();
            _settings.Apply();

            if (_settingsPanel != null) _settingsPanel.Init(_settings);

            Wire(_playButton, StartGame);
            Wire(_codexButton, () => Show(Page.Codex));
            Wire(_settingsButton, () => Show(Page.Settings));
            Wire(_quitButton, Quit);
            Wire(_starterBackButton, () => Show(Page.Root));
            Wire(_codexBackButton, () => Show(Page.Root));
            Wire(_settingsBackButton, () => Show(Page.Root));

            if (_versionLabel != null)
            {
                _versionLabel.text = "v" + Application.version;
            }

            Show(Page.Root);
        }

        void Update()
        {
            if (!ProtoInput.BackDown) return;

            if (_page == Page.Root) return;
            Show(Page.Root);
        }

        void Show(Page page)
        {
            _page = page;

            if (_rootPage != null) _rootPage.SetActive(page == Page.Root);
            if (_starterPage != null) _starterPage.SetActive(page == Page.Starter);
            if (_codexPage != null) _codexPage.SetActive(page == Page.Codex);
            if (_settingsPage != null) _settingsPage.SetActive(page == Page.Settings);
        }

        /// <summary>
        /// PLAY membuka layar pilih starter, bukan langsung memuat scene game — di situlah papan
        /// pembuka run ditentukan.
        ///
        /// Yang memuat scene-nya <see cref="StarterSelectPanel"/>, sesudah pilihannya disimpan.
        /// Halaman starter yang tidak terpasang jatuh balik ke perilaku lama, supaya scene menu
        /// yang belum dibangun ulang tidak berubah jadi tombol PLAY yang tidak melakukan apa-apa.
        /// </summary>
        void StartGame()
        {
            if (_starterPage != null)
            {
                Show(Page.Starter);
                return;
            }

            if (string.IsNullOrEmpty(_gameSceneName))
            {
                Debug.LogError("[MainMenuController] nama scene game belum diisi.", this);
                return;
            }

            LoadingScreen.Go(_gameSceneName, _loadingSigil, _loadingInk);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
