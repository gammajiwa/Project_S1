using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Proto
{
    /// <summary>
    /// Preset detail grafis — satu pilihan pemain yang memutar beberapa tuas sekaligus.
    ///
    /// Isi presetnya TIDAK dikarang: tiap tuas di sini dipilih karena sudah diukur di arena yang
    /// hidup, dan tuas yang terukur tidak berpengaruh sengaja TIDAK dipasang.
    ///
    /// Yang terukur di Proto (FrameTimingManager, GPU frame time):
    ///
    /// <list type="bullet">
    /// <item>3840x2160 -> 1920x1080 = <b>39,9 ms -> 14,5 ms</b>. Turun 25,4 ms, 63% frame.</item>
    /// <item>Bayangan dimatikan di 4K = 39,9 -> 34,3 ms. Turun 5,6 ms.</item>
    /// <item>Bayangan dimatikan di 1080p = 14,5 -> 14,2 ms. Turun 0,25 ms.</item>
    /// <item>MSAA 4x dimatikan = 14,5 -> 14,0 ms. Turun 0,44 ms.</item>
    /// </list>
    ///
    /// Tiga kesimpulan yang membentuk preset ini:
    ///
    /// <b>Satu — yang mahal itu PIKSEL, bukan segitiga.</b> Mematikan bayangan membuang 4,59 juta
    /// dari 6,29 juta segitiga yang dikirim (73%!) dan cuma menghemat 5,6 ms. Arena ini menggambar
    /// Bloom, kabut, tonemapping, color grading, vignette, dan satu salinan layar penuh — semuanya
    /// pass layar-penuh yang ongkosnya lurus dengan jumlah piksel. Karena itu tuas utama preset ini
    /// <see cref="UniversalRenderPipelineAsset.renderScale"/>, bukan jumlah objek.
    ///
    /// <b>Dua — bayangan mahal karena DISAMPLING, bukan karena digambar.</b> Buktinya penghematannya
    /// runtuh dari 5,6 ms jadi 0,25 ms begitu resolusinya turun. Jadi shadowDistance diturunkan
    /// bersama renderScale, bukan sebagai tuas yang berdiri sendiri.
    ///
    /// <b>Tiga — MSAA praktis gratis (0,44 ms), jadi TIDAK disentuh preset mana pun.</b> Mematikan
    /// sesuatu yang tidak menghemat apa-apa cuma memperjelek gambar tanpa imbalan.
    /// </summary>
    public static class GraphicsDetail
    {
        public const int Low = 0;
        public const int Medium = 1;
        public const int High = 2;
        public const int Ultra = 3;

        public const int Count = 4;

        /// <summary>Bawaan TINGGI, bukan sedang: itu tampilan yang selama ini dirancang.</summary>
        public const int Default = High;

        struct Preset
        {
            public float RenderScale;
            public float ShadowDistance;
            public int Cascades;
            public int ShadowmapResolution;
            public bool PostProcessing;
        }

        // ShadowDistance 0 = bayangan mati. supportsMainLightShadows TIDAK bisa ditulis runtime
        // (properti itu read-only), jadi jaraknya yang dinolkan — hasil akhirnya sama dan tidak
        // butuh mengedit aset pipeline lewat SerializedObject.
        static readonly Preset[] Presets =
        {
            new Preset { RenderScale = 0.60f, ShadowDistance = 0f,  Cascades = 1, ShadowmapResolution = 512,  PostProcessing = false },
            new Preset { RenderScale = 0.80f, ShadowDistance = 25f, Cascades = 2, ShadowmapResolution = 1024, PostProcessing = true },
            new Preset { RenderScale = 1.00f, ShadowDistance = 40f, Cascades = 4, ShadowmapResolution = 2048, PostProcessing = true },
            new Preset { RenderScale = 1.00f, ShadowDistance = 60f, Cascades = 4, ShadowmapResolution = 4096, PostProcessing = true },
        };

        static readonly string[] Keys =
        {
            "settings.detail.low", "settings.detail.medium", "settings.detail.high", "settings.detail.ultra"
        };

        public static string Label(int level) => Loc.T(Keys[Clamp(level)]);

        public static int Clamp(int level) => Mathf.Clamp(level, 0, Count - 1);

        static int _applied = -1;

        // ------------------------------------------------------------------ nilai pabrik
        //
        // Preset ini menulis ke UniversalRenderPipelineAsset, dan aset itu BUKAN objek scene:
        // perubahannya TIDAK dibatalkan saat keluar play mode, dan bisa ikut tersimpan ke file
        // aset kalau kebetulan ada yang menyentuhnya di Inspector setelahnya. Di build itu tidak
        // masalah — asetnya dimuat ulang bersih tiap kali game dijalankan — tapi di Editor itu
        // berarti mencoba detail RENDAH sekali bisa meninggalkan PC_RPAsset di renderScale 0,6
        // selamanya, dan tidak ada satu pun petunjuk kenapa.
        //
        // Jadi nilai aslinya direkam sekali sebelum sentuhan pertama, dan dikembalikan saat keluar
        // play mode. Ini bukan kerapihan, ini mencegah setelan project ikut berubah diam-diam.
        static bool _captured;
        static float _origRenderScale, _origShadowDistance;
        static int _origCascades, _origShadowmap;

        static void Capture(UniversalRenderPipelineAsset asset)
        {
            if (_captured || asset == null) return;

            _origRenderScale = asset.renderScale;
            _origShadowDistance = asset.shadowDistance;
            _origCascades = asset.shadowCascadeCount;
            _origShadowmap = asset.mainLightShadowmapResolution;
            _captured = true;
        }

        /// <summary>Mengembalikan aset pipeline ke nilai apa adanya sebelum preset pertama dipakai.</summary>
        public static void RestorePristine()
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!_captured || asset == null) return;

            asset.renderScale = _origRenderScale;
            asset.shadowDistance = _origShadowDistance;
            asset.shadowCascadeCount = _origCascades;
            asset.mainLightShadowmapResolution = _origShadowmap;
            _applied = -1;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void HookEditorRestore()
        {
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) RestorePristine();
            };
        }
#endif

        // ------------------------------------------------------------------ penerapan

        public static void Apply(int level)
        {
            level = Clamp(level);
            _applied = level;

            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != null)
            {
                Capture(asset);

                var p = Presets[level];
                asset.renderScale = p.RenderScale;
                asset.shadowDistance = p.ShadowDistance;
                asset.shadowCascadeCount = p.Cascades;
                asset.mainLightShadowmapResolution = p.ShadowmapResolution;
            }

            ApplyToCameras();

            // Kamera arena baru lahir tiap run dimulai, dan kamera itu belum pernah mendengar
            // preset ini. Tanpa langganan ini, mengubah detail di menu utama tidak berpengaruh
            // apa-apa begitu run dimulai — persis jenis setelan yang bikin pemain tidak percaya
            // lagi pada menu setelan.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToCameras();

        static void ApplyToCameras()
        {
            if (_applied < 0) return;

            bool post = Presets[_applied].PostProcessing;

            var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                var data = cameras[i].GetComponent<UniversalAdditionalCameraData>();
                if (data == null) continue;

                data.renderPostProcessing = post;
            }
        }
    }
}
