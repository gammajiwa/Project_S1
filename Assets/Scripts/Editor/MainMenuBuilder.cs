using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Generates Assets/Scenes/MainMenu.unity from scratch.
    ///
    /// The split that makes this safe to re-run: layout lives in the generated scene, look lives in
    /// MenuTheme. Swap placeholder art in the theme asset and rebuild — the art survives because it
    /// was never stored in the scene. Anything hand-edited *in the scene* is lost on rebuild; that
    /// is the price of being able to regenerate it.
    /// </summary>
    public static class MainMenuBuilder
    {
        const string ScenePath = "Assets/Scenes/MainMenu.unity";
        const string GameScenePath = "Assets/Scenes/Proto.unity";
        const string ThemePath = "Assets/GameData/MenuTheme.asset";
        const string UiThemePath = "Assets/GameData/UiTheme.asset";
        const string StarterPrefabPath = "Assets/Art/UI/Prefabs/StarterPanel.prefab";
        const string MenuLookPath = "Assets/GameData/SceneLook_Menu.asset";
        const string MenuAssetDir = "Assets/GameData/Menu";

        const float RefWidth = 1920f;
        const float RefHeight = 1080f;

        static MenuTheme _theme;
        static SceneLook _look;

        [MenuItem("Tools/Grimoire/Build Main Menu")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[MainMenuBuilder] stop Play mode dulu.");
                return;
            }

            _theme = LoadOrCreateTheme();

            // The menu has its own look: its floor is a showcase, the run's floor has to stay dark.
            _look = AssetDatabase.LoadAssetAtPath<SceneLook>(MenuLookPath);
            if (_look == null)
            {
                Debug.LogError("[MainMenuBuilder] " + MenuLookPath + " tidak ketemu. " +
                               "Jalankan 'Tools/Grimoire/Build Scene Look' dulu.");
                return;
            }

            // Dipakai layar pilih starter buat menampilkan angka yang BENAR-BENAR berlaku: starter
            // yang membiarkan sebuah stat kosong tetap harus memperlihatkan nilainya, dan nilai itu
            // hidup di sini.
            var balance = FindAsset<GameBalance>();
            if (balance == null)
            {
                Debug.LogError("[MainMenuBuilder] GameBalance tidak ketemu di project.");
                return;
            }

            var database = FindAsset<ContentDatabase>();
            if (database == null)
            {
                Debug.LogError("[MainMenuBuilder] ContentDatabase tidak ketemu di project.");
                return;
            }

            // Unity refuses to save over a scene that is already open, and it also refuses to close
            // the last open scene — so when the target is open we empty it and rebuild in place.
            var target = FindOpenScene(ScenePath);
            bool builtInPlace = target.IsValid();

            var previous = SceneManager.GetActiveScene();
            var scene = builtInPlace
                ? target
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            SceneManager.SetActiveScene(scene);

            if (builtInPlace)
            {
                var stale = scene.GetRootGameObjects();
                for (int i = 0; i < stale.Length; i++) Object.DestroyImmediate(stale[i]);
            }

            var root = new GameObject("_Menu");
            var controller = root.AddComponent<MainMenuController>();

            BuildDiorama(root.transform);
            BuildEventSystem(root.transform);

            var canvas = BuildCanvas(root.transform);
            BuildBackdrop(canvas);

            var rootPage = BuildRootPage(canvas, out var play, out var codexBtn,
                out var settingsBtn, out var quit, out var version);

            var starterPage = BuildStarterPage(canvas, database, balance,
                out var starterPanel, out var starterBack);

            var codexPage = BuildCodexPage(canvas, database, out var codexPanel, out var codexBack);
            var settingsPage = BuildSettingsPage(canvas, out var settingsPanel, out var settingsBack);

            new Binder(controller)
                .SetString("_gameSceneName", "Proto")
                .Set("_rootPage", rootPage.gameObject)
                .Set("_starterPage", starterPage.gameObject)
                .Set("_starterBackButton", starterBack)
                .Set("_codexPage", codexPage.gameObject)
                .Set("_settingsPage", settingsPage.gameObject)
                .Set("_playButton", play)
                .Set("_codexButton", codexBtn)
                .Set("_settingsButton", settingsBtn)
                .Set("_quitButton", quit)
                .Set("_codexBackButton", codexBack)
                .Set("_settingsBackButton", settingsBack)
                .Set("_codexPanel", codexPanel)
                .Set("_settingsPanel", settingsPanel)
                .Set("_versionLabel", version)
                .Apply();

            // Halaman setelan DISIMPAN SEBAGAI PREFAB sebelum dimatikan — scene game membukanya
            // lewat ESC. Satu panel untuk dua scene; dua salinan akan saling menyimpang begitu
            // salah satunya diedit.
            SaveSettingsPrefab(settingsPage.gameObject);

            starterPage.gameObject.SetActive(false);
            codexPage.gameObject.SetActive(false);
            settingsPage.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("[MainMenuBuilder] gagal menyimpan " + ScenePath + ".");
                return;
            }

            if (!builtInPlace)
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }

            RegisterBuildSettings();

            AssetDatabase.SaveAssets();
            Debug.Log("[MainMenuBuilder] " + ScenePath + " dibangun ulang. " +
                      "Aset yang bisa diganti ada di " + ThemePath + ".");
        }

        // ---------- diorama ----------

        static void BuildDiorama(Transform parent)
        {
            _look.ApplyEnvironment();

            var rig = new GameObject("CameraRig");
            rig.transform.SetParent(parent, false);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 9f, -13f);
            camGo.transform.localRotation = Quaternion.Euler(26f, 0f, 0f);

            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 38f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Rendered(_look.HorizonColor);

            // Sama seperti scene game: tanpa ini console dibanjiri "no audio listeners" dan
            // berhenti berguna. Menu adalah layar pertama yang dilihat, jadi ia juga yang pertama
            // membanjirinya.
            camGo.AddComponent<AudioListener>();

            var urp = camGo.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;

            camGo.AddComponent<MenuDiorama>();

            if (_look.PostProcess != null)
            {
                var volumeGo = new GameObject("Global Volume");
                volumeGo.transform.SetParent(parent, false);

                var volume = volumeGo.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = _look.PostProcess;
            }

            var sun = new GameObject("Sun");
            sun.transform.SetParent(parent, false);
            _look.ApplySun(sun.AddComponent<Light>());

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.localScale = new Vector3(5f, 1f, 5f);
            Object.DestroyImmediate(ground.GetComponent<Collider>());

            var groundRenderer = ground.GetComponent<Renderer>();
            groundRenderer.sharedMaterial = LoadOrCreateMaterial("M_MenuGround", _look.GroundColor);
            groundRenderer.receiveShadows = true;
            groundRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var figure = NewProp(parent, PrimitiveType.Capsule, "Figure",
                new Vector3(2.6f, 0.9f, 0f), new Vector3(0.9f, 0.9f, 0.9f),
                LoadOrCreateMaterial("M_MenuFigure", _look.PlayerColor));
            figure.transform.localRotation = Quaternion.Euler(0f, -20f, 0f);

            // Placeholder blocks purely so the long shadows have something to be cast by —
            // a lone capsule on an empty plane shows none of what the lighting is doing.
            var propMaterial = LoadOrCreateMaterial("M_MenuProp", _look.PropColor);
            NewProp(parent, PrimitiveType.Cube, "Prop_A", new Vector3(-2.2f, 0.8f, 1.4f),
                new Vector3(1.6f, 1.6f, 1.6f), propMaterial);
            NewProp(parent, PrimitiveType.Cube, "Prop_B", new Vector3(-4.4f, 0.5f, -0.6f),
                new Vector3(1f, 1f, 1f), propMaterial);
            NewProp(parent, PrimitiveType.Cube, "Prop_C", new Vector3(5.4f, 1.2f, 2.2f),
                new Vector3(2.4f, 2.4f, 2.4f), propMaterial);
        }

        static GameObject NewProp(Transform parent, PrimitiveType type, string name,
            Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        static void BuildEventSystem(Transform parent)
        {
            var go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            go.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
            // Active Input Handling is set to the new package, so the legacy module would be inert.
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // ---------- canvas ----------

        static RectTransform BuildCanvas(Transform parent)
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }

        static void BuildBackdrop(RectTransform canvas)
        {
            if (_theme.BackdropArt != null)
            {
                var art = NewImage("BackdropArt", canvas, Color.white, _theme.BackdropArt);
                art.type = Image.Type.Simple;
                art.preserveAspect = true;
                Stretch(art.rectTransform);
            }

            var veil = NewImage("BackdropVeil", canvas, _theme.BackdropVeil);
            Stretch(veil.rectTransform);
        }

        // ---------- root page ----------

        static RectTransform BuildRootPage(RectTransform canvas, out Button play, out Button codex,
            out Button settings, out Button quit, out TextMeshProUGUI version)
        {
            var page = NewRect("RootPage", canvas);
            Stretch(page);

            // Judulnya hidup di MenuTheme, bukan di sini: nama game diganti dengan mengedit satu
            // field asset, dan jarak antar hurufnya diatur TMP — bukan dengan menyelipkan spasi ke
            // dalam string, yang membuat teksnya tidak bisa dicari dan pecah saat namanya berubah.
            var title = NewText("Title", page, _theme.GameTitle,
                _theme.TitleSize, _theme.TextIdle, TextAlignmentOptions.Left, true);
            title.characterSpacing = _theme.TitleTracking;
            Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(180f, 232f), new Vector2(1400f, 120f));

            // Tanpa tagline — permintaan pemilik project: "cukup nama aja". Garisnya tetap, karena
            // dialah yang mengikat judul ke daftar menu; tanpa itu keduanya mengambang sendiri.
            var rule = NewImage("Rule", page, _theme.PanelLine);
            Place(rule.rectTransform, new Vector2(0f, 0.5f), new Vector2(186f, 168f), new Vector2(420f, 2f));

            var list = NewRect("MenuList", page);
            Place(list, new Vector2(0f, 0.5f), new Vector2(180f, 118f), new Vector2(520f, 10f));
            list.pivot = new Vector2(0f, 1f);

            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = _theme.MenuSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            play = NewMenuLine(list, "Play", "MULAI");
            codex = NewMenuLine(list, "Codex", "CODEX");
            settings = NewMenuLine(list, "Settings", "SETELAN");
            quit = NewMenuLine(list, "Quit", "KELUAR");

            version = NewText("Version", page, "v0.0", _theme.SmallSize,
                _theme.TextMuted, TextAlignmentOptions.Right);
            Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 34f), new Vector2(300f, 24f));

            return page;
        }

        // ---------- codex page ----------

        static RectTransform BuildCodexPage(RectTransform canvas, ContentDatabase database,
            out CodexPanel panelComponent, out Button back)
        {
            var page = NewRect("CodexPage", canvas);
            Stretch(page);

            var panel = NewPanel(page, "CodexPanel", new Vector2(1420f, 840f));
            panelComponent = panel.gameObject.AddComponent<CodexPanel>();

            var heading = NewText("Heading", panel, "CODEX", _theme.HeadingSize,
                _theme.TextIdle, TextAlignmentOptions.Left, true);
            Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -40f), new Vector2(600f, 52f));

            var counter = NewText("Counter", panel, "0 / 0 KETEMU", _theme.BodySize,
                _theme.Accent, TextAlignmentOptions.Right);
            Place(counter.rectTransform, new Vector2(1f, 1f), new Vector2(-48f, -50f), new Vector2(500f, 30f));

            var hint = NewText("EmptyHint", panel,
                "Belum ada yang ketemu. Ambil barang di dalam run buat ngisi ini.",
                _theme.BodySize, _theme.TextMuted, TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 40f));

            var content = BuildScrollArea(panel, out var viewport);
            var template = BuildCodexEntryTemplate(content);

            new Binder(panelComponent)
                .Set("_database", database)
                .Set("_counter", counter)
                .Set("_emptyHint", hint)
                .Set("_content", content)
                .Set("_entryTemplate", template)
                .Apply();

            back = NewMenuLine(panel, "Back", "KEMBALI");
            var backRect = (RectTransform)back.transform;
            Place(backRect, new Vector2(0f, 0f), new Vector2(48f, 26f), new Vector2(360f, 46f));

            // The scroll area must not swallow the row the back button sits on.
            Place(viewport, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(1320f, 620f));

            return page;
        }

        // ---------- starter page ----------

        /// <summary>
        /// Layar pilih starter. Yang besar di tengah adalah PAPAN PEMBUKANYA, bukan portrait:
        /// susunan petak itu satu-satunya bagian kartu yang benar-benar memberi tahu apa yang
        /// akan dimainkan, dan ia digambar sendiri oleh panelnya dari aset — jadi di sini cuma
        /// disediakan kotaknya.
        ///
        /// <b>Begitu <c>StarterPanel.prefab</c> ada, prefab itu yang memegang tata letaknya</b> dan
        /// fungsi ini berhenti menata apa pun — ia cuma menyambungkan data ke label-label yang
        /// ditunjuk <see cref="StarterRig"/>. Itu satu-satunya cara "geser posisinya" bisa
        /// bertahan: scene menu digenerate ulang setiap kali, prefab tidak.
        /// </summary>
        static RectTransform BuildStarterPage(RectTransform canvas, ContentDatabase database,
            GameBalance balance, out StarterSelectPanel panelComponent, out Button back)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StarterPrefabPath);
            if (existing != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(existing, canvas);
                instance.name = "StarterPage";

                var prefabRig = instance.GetComponentInChildren<StarterRig>(true);
                panelComponent = instance.GetComponentInChildren<StarterSelectPanel>(true);

                if (prefabRig == null || panelComponent == null)
                {
                    Debug.LogError("[MainMenuBuilder] " + StarterPrefabPath + " tidak membawa " +
                                   "StarterRig atau StarterSelectPanel. Hapus prefabnya kalau mau " +
                                   "halaman bawaan dibangun ulang dari nol.");
                    back = null;
                    return (RectTransform)instance.transform;
                }

                BindStarter(panelComponent, database, balance, prefabRig);
                back = prefabRig.Back;
                return (RectTransform)instance.transform;
            }

            var page = NewRect("StarterPage", canvas);
            Stretch(page);

            var panel = NewPanel(page, "StarterPanel", new Vector2(1420f, 840f));
            panelComponent = panel.gameObject.AddComponent<StarterSelectPanel>();
            var rig = panel.gameObject.AddComponent<StarterRig>();

            var heading = NewText("Heading", panel, "PILIH GRIMOIRE", _theme.HeadingSize,
                _theme.TextIdle, TextAlignmentOptions.Left, true);
            Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -40f), new Vector2(700f, 52f));

            var page01 = NewText("PageLabel", panel, "1 / 1", _theme.BodySize,
                _theme.Accent, TextAlignmentOptions.Right);
            Place(page01.rectTransform, new Vector2(1f, 1f), new Vector2(-48f, -50f), new Vector2(300f, 30f));

            // Papannya BUKAN kotak kosong lagi: prefab papan grimoire yang dipakai di dalam run
            // ditaruh apa adanya di sini, dan petak 7x7 digambar di dalam `GridArea` miliknya.
            // Sampul, mata, dan rune-nya ikut — layar pilih starter memperlihatkan benda yang
            // sama persis dengan yang akan dipegang pemain, bukan gambar penggantinya.
            var board = BuildStarterBoard(panel);

            // Nama dan blurb DULU saling menimpa: keduanya dipasang dengan pivot tengah, dan kotak
            // blurb setinggi 220 yang dipusatkan di y=96 naik sampai y=206 — melewati baris nama di
            // 126..174. Teksnya rata-atas, jadi kalimat pertama mendarat persis di atas namanya.
            // Sekarang nama dipatok di 200 dan blurb digantung dari 160 KE BAWAH (pivot atas), jadi
            // panjang blurb tidak pernah lagi bisa merambat naik.
            var name = NewText("Name", panel, "Nama Starter", _theme.HeadingSize,
                _theme.Accent, TextAlignmentOptions.Left, true);
            Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(48f, 200f), new Vector2(420f, 54f));

            var blurb = NewText("Blurb", panel, "", _theme.BodySize,
                Color.Lerp(_theme.TextMuted, _theme.TextIdle, 0.55f), TextAlignmentOptions.TopLeft);
            Place(blurb.rectTransform, new Vector2(0f, 0.5f), new Vector2(48f, 160f), new Vector2(420f, 280f));
            blurb.rectTransform.pivot = new Vector2(0f, 1f);
            blurb.textWrappingMode = TextWrappingModes.Normal;

            // Blurb adalah satu-satunya paragraf di seluruh menu; baris rapat yang enak untuk label
            // satu baris justru bikin paragraf jadi blok abu-abu.
            blurb.lineSpacing = 14f;

            var stats = NewText("Stats", panel, "", _theme.BodySize,
                _theme.TextIdle, TextAlignmentOptions.Center);
            Place(stats.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(1200f, 30f));

            var portrait = NewImage("Portrait", panel, Color.white);
            Place(portrait.rectTransform, new Vector2(1f, 0.5f), new Vector2(-48f, 0f), new Vector2(380f, 480f));
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;

            // Panahnya mengapit papan, bukan berdiri di tepi panel: yang sedang diganti isi
            // papannya, dan tombol yang jauh dari benda yang berubah terbaca sebagai ganti halaman.
            var prev = NewMenuLine(panel, "Prev", "<");
            Place((RectTransform)prev.transform, new Vector2(0.5f, 0.5f), new Vector2(-300f, 22f),
                new Vector2(90f, 90f));

            var next = NewMenuLine(panel, "Next", ">");
            Place((RectTransform)next.transform, new Vector2(0.5f, 0.5f), new Vector2(300f, 22f),
                new Vector2(90f, 90f));

            var play = NewMenuLine(panel, "Launch", "MULAI RUN");
            Place((RectTransform)play.transform, new Vector2(1f, 0f), new Vector2(-48f, 26f),
                new Vector2(360f, 46f));

            back = NewMenuLine(panel, "Back", "KEMBALI");
            Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(48f, 26f),
                new Vector2(360f, 46f));

            rig.Board = board;
            rig.NameLabel = name;
            rig.Blurb = blurb;
            rig.Stats = stats;
            rig.PageLabel = page01;
            rig.Portrait = portrait;
            rig.Prev = prev;
            rig.Next = next;
            rig.Play = play;
            rig.Back = back;

            BindStarter(panelComponent, database, balance, rig);

            SaveStarterPrefab(page.gameObject);
            return page;
        }

        /// <summary>
        /// Papan preview: prefab papan grimoire yang dipakai di dalam run, ditaruh apa adanya.
        /// Petaknya digambar di dalam <c>GridArea</c> milik prefab itu — kotak yang sama yang
        /// menentukan letak petak saat bermain, jadi preview dan papan sungguhan mustahil
        /// berbeda ukuran sel.
        ///
        /// Tanpa prefab (tema kosong), kembali ke kotak kosong 470x470 seperti sebelumnya.
        /// </summary>
        static RectTransform BuildStarterBoard(RectTransform panel)
        {
            var uiTheme = AssetDatabase.LoadAssetAtPath<UiTheme>(UiThemePath);

            if (uiTheme != null && uiTheme.GrimoirePanelPrefab != null)
            {
                var art = (GameObject)PrefabUtility.InstantiatePrefab(uiTheme.GrimoirePanelPrefab, panel);
                art.name = "BoardArt";

                // Prefabnya dirancang duduk di pojok kiri-bawah layar saat bermain. Di sini ia
                // dipindah ke tengah panel; susunan ISI-nya tidak disentuh, cuma titik tumpunya.
                var artRect = (RectTransform)art.transform;
                artRect.anchorMin = artRect.anchorMax = artRect.pivot = new Vector2(0.5f, 0.5f);
                artRect.anchoredPosition = new Vector2(0f, 10f);

                var area = FindGridArea(art.transform);
                if (area != null) return area;

                Debug.LogWarning("[MainMenuBuilder] prefab papan grimoire tidak membawa GridArea — " +
                                 "petak starter jatuh ke kotak kosong bawaan.");
            }

            var board = NewRect("Board", panel);
            Place(board, new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(470f, 470f));
            return board;
        }

        /// <summary>Kotak petak di dalam prefab papan: lewat komponennya dulu, lalu lewat nama.</summary>
        static RectTransform FindGridArea(Transform root)
        {
            var marker = root.GetComponentInChildren<GrimoireGridArea>(true);
            if (marker != null) return (RectTransform)marker.transform;

            var all = root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == "GridArea") return all[i];
            }

            return null;
        }

        /// <summary>
        /// Menyambungkan data + widget ke panelnya. Dipakai dua kali: sekali untuk halaman bawaan
        /// yang baru dibangun, dan sekali lagi tiap rebuild untuk halaman yang datang dari prefab.
        /// </summary>
        static void BindStarter(StarterSelectPanel panel, ContentDatabase database,
            GameBalance balance, StarterRig rig)
        {
            new Binder(panel)
                .Set("_database", database)
                .Set("_balance", balance)
                .SetString("_gameSceneName", "Proto")
                .Set("_nameLabel", rig.NameLabel)
                .Set("_blurbLabel", rig.Blurb)
                .Set("_statsLabel", rig.Stats)
                .Set("_pageLabel", rig.PageLabel)
                .Set("_portrait", rig.Portrait)
                .Set("_board", rig.Board)
                .Set("_prevButton", rig.Prev)
                .Set("_nextButton", rig.Next)
                .Set("_playButton", rig.Play)
                .Apply();
        }

        /// <summary>
        /// Menyimpan halaman starter sebagai prefab SEKALI. Sesudah file ini ada, builder tidak
        /// pernah menulisnya lagi — di situlah tata letak halaman ini boleh digeser tangan tanpa
        /// hilang tiap rebuild.
        /// </summary>
        static void SaveStarterPrefab(GameObject page)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(page, StarterPrefabPath);

            if (prefab == null)
            {
                Debug.LogError("[MainMenuBuilder] gagal menyimpan " + StarterPrefabPath + ".");
                return;
            }

            Debug.Log("[MainMenuBuilder] " + StarterPrefabPath + " dibuat. Mulai sekarang tata " +
                      "letak layar pilih starter diatur DI PREFAB ITU — rebuild menu tidak akan " +
                      "menimpanya lagi. Hapus prefabnya kalau mau kembali ke tata letak bawaan.");
        }

        static RectTransform BuildScrollArea(RectTransform panel, out RectTransform viewport)
        {
            var scrollGo = NewRect("Scroll", panel);
            viewport = scrollGo;

            var scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var viewGo = NewRect("Viewport", scrollGo);
            Stretch(viewGo);
            var mask = viewGo.gameObject.AddComponent<Image>();
            mask.color = new Color(0f, 0f, 0f, 0.001f);
            viewGo.gameObject.AddComponent<RectMask2D>();

            var content = NewRect("Content", viewGo);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(250f, 118f);
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(4, 4, 4, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewGo;
            scroll.content = content;

            return content;
        }

        static CodexEntry BuildCodexEntryTemplate(RectTransform content)
        {
            var entry = NewRect("EntryTemplate", content);
            var component = entry.gameObject.AddComponent<CodexEntry>();

            var background = NewImage("Background", entry, _theme.SlotKnown, _theme.PanelSprite);
            Stretch(background.rectTransform);

            var shape = NewRect("Shape", entry);
            Place(shape, new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(52f, 52f));
            shape.pivot = new Vector2(0f, 1f);

            var grid = shape.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(15f, 15f);
            grid.spacing = new Vector2(2f, 2f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = CodexEntry.ShapeGrid;

            var cells = new Object[CodexEntry.ShapeGrid * CodexEntry.ShapeGrid];
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = NewImage("Cell_" + i, shape, Color.white);
                cell.enabled = false;
                cells[i] = cell;
            }

            var label = NewText("Name", entry, "Nama", _theme.BodySize,
                _theme.TextIdle, TextAlignmentOptions.TopLeft);
            Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(80f, -16f), new Vector2(156f, 52f));
            label.rectTransform.pivot = new Vector2(0f, 1f);

            var meta = NewText("Meta", entry, "*", _theme.SmallSize,
                _theme.TextMuted, TextAlignmentOptions.BottomLeft);
            Place(meta.rectTransform, new Vector2(0f, 0f), new Vector2(14f, 12f), new Vector2(220f, 22f));
            meta.rectTransform.pivot = new Vector2(0f, 0f);

            new Binder(component)
                .Set("_background", background)
                .Set("_name", label)
                .Set("_meta", meta)
                .SetArray("_shapeCells", cells)
                .SetColor("_knownFill", _theme.SlotKnown)
                .SetColor("_unknownFill", _theme.SlotUnknown)
                .SetColor("_knownText", _theme.TextIdle)
                .SetColor("_unknownText", _theme.TextMuted)
                .SetColor("_unknownCell", _theme.SilhouetteCell)
                .Apply();

            entry.gameObject.SetActive(false);
            return component;
        }

        // ---------- settings page ----------

        static RectTransform BuildSettingsPage(RectTransform canvas, out SettingsPanel panelComponent,
            out Button back)
        {
            var page = NewRect("SettingsPage", canvas);
            Stretch(page);

            // Dulu satu gulungan setinggi 1060: empat seksi berbaris ke bawah, dan tiap baris
            // baru mendorong seksi di bawahnya sampai baris DATA menabrak tombol KEMBALI di
            // dasar panel. Sekarang empat sub-halaman yang bergantian — yang tumbuh cuma satu
            // halaman, dan panelnya boleh menyusut lagi ke ukuran yang tidak menelan layar.
            var panel = NewPanel(page, "SettingsPanel", new Vector2(1180f, 720f));
            panelComponent = panel.gameObject.AddComponent<SettingsPanel>();

            var heading = NewText("Heading", panel, "SETELAN", _theme.HeadingSize,
                _theme.TextIdle, TextAlignmentOptions.Left, true);
            Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -40f), new Vector2(600f, 52f));

            // Rail tab di kiri, badan halaman di kanan garis. Semuanya dipatok dari sudut
            // kiri-atas panel, jadi menambah baris di satu halaman tidak menggeser halaman lain.
            const float BodyLeft = 372f;
            const float BodyTop = -112f;
            var bodySize = new Vector2(1180f - BodyLeft - 48f, 452f);

            var divider = NewImage("TabRule", panel, _theme.PanelLine);
            Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(332f, BodyTop),
                new Vector2(1f, bodySize.y));

            var layar = NewTabBody(panel, "Layar", BodyLeft, BodyTop, bodySize);
            float y = 0f;
            var fullscreen = NewStepper(layar, "Mode layar", ref y);
            var resolution = NewStepper(layar, "Resolusi", ref y);
            var vsync = NewStepper(layar, "VSync", ref y);
            var frameCap = NewStepper(layar, "Batas FPS", ref y);

            // Toggle untuk yang mahal digambar. Mengubahnya baru terasa di run BERIKUTNYA -
            // scene game membacanya saat lahir; catatan di bawah panel yang bilang begitu.
            var performa = NewTabBody(panel, "Performa", BodyLeft, BodyTop, bodySize);
            y = 0f;
            var damageText = NewStepper(performa, "Teks damage", ref y);
            var enemyShadows = NewStepper(performa, "Bayangan musuh", ref y);
            var weatherVfx = NewStepper(performa, "VFX cuaca", ref y);

            var suara = NewTabBody(panel, "Suara", BodyLeft, BodyTop, bodySize);
            y = 0f;
            var master = NewSliderRow(suara, "Master", ref y);
            var sfx = NewSliderRow(suara, "Efek suara", ref y);
            var music = NewSliderRow(suara, "Musik", ref y);

            var data = NewTabBody(panel, "Data", BodyLeft, BodyTop, bodySize);
            y = 0f;
            var reset = NewResetRow(data, ref y);

            var tabs = panel.gameObject.AddComponent<SettingsTabs>();
            var tabLines = new Object[4];
            NewTabLine(panel, tabLines, 0, "LAYAR", BodyTop);
            NewTabLine(panel, tabLines, 1, "PERFORMA", BodyTop);
            NewTabLine(panel, tabLines, 2, "SUARA", BodyTop);
            NewTabLine(panel, tabLines, 3, "DATA", BodyTop);

            new Binder(tabs)
                .SetArray("_lines", tabLines)
                .SetArray("_pages", new Object[]
                {
                    layar.gameObject, performa.gameObject, suara.gameObject, data.gameObject
                })
                .Apply();

            // Opposite the back button: the rows above run all the way down to it.
            var note = NewText("Note", panel, "", _theme.SmallSize,
                _theme.TextMuted, TextAlignmentOptions.Right);
            Place(note.rectTransform, new Vector2(1f, 0f), new Vector2(-48f, 84f), new Vector2(760f, 24f));

            new Binder(panelComponent)
                .Set("_fullscreenPrev", fullscreen.Prev)
                .Set("_fullscreenNext", fullscreen.Next)
                .Set("_fullscreenValue", fullscreen.Value)
                .Set("_resolutionPrev", resolution.Prev)
                .Set("_resolutionNext", resolution.Next)
                .Set("_resolutionValue", resolution.Value)
                .Set("_vsyncPrev", vsync.Prev)
                .Set("_vsyncNext", vsync.Next)
                .Set("_vsyncValue", vsync.Value)
                .Set("_frameCapPrev", frameCap.Prev)
                .Set("_frameCapNext", frameCap.Next)
                .Set("_frameCapValue", frameCap.Value)
                .Set("_damageTextPrev", damageText.Prev)
                .Set("_damageTextNext", damageText.Next)
                .Set("_damageTextValue", damageText.Value)
                .Set("_enemyShadowsPrev", enemyShadows.Prev)
                .Set("_enemyShadowsNext", enemyShadows.Next)
                .Set("_enemyShadowsValue", enemyShadows.Value)
                .Set("_weatherVfxPrev", weatherVfx.Prev)
                .Set("_weatherVfxNext", weatherVfx.Next)
                .Set("_weatherVfxValue", weatherVfx.Value)
                .Set("_masterSlider", master.Slider)
                .Set("_masterValue", master.Value)
                .Set("_sfxSlider", sfx.Slider)
                .Set("_sfxValue", sfx.Value)
                .Set("_musicSlider", music.Slider)
                .Set("_musicValue", music.Value)
                .Set("_resetCodex", reset.Button)
                .Set("_resetLabel", reset.Label)
                .Set("_resetHint", reset.Hint)
                .Set("_note", note)
                .SetColor("_dangerColor", new Color(0.85f, 0.35f, 0.3f, 1f))
                .SetColor("_mutedColor", _theme.TextMuted)
                .Apply();

            back = NewMenuLine(panel, "Back", "KEMBALI");
            Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(48f, 26f),
                new Vector2(360f, 46f));

            return page;
        }

        /// <summary>
        /// Menyimpan halaman setelan sebagai prefab dan menautkannya ke <see cref="UiTheme"/>,
        /// supaya scene game bisa membukanya tanpa tahu-menahu soal builder ini.
        /// </summary>
        static void SaveSettingsPrefab(GameObject page)
        {
            const string Path = "Assets/Art/UI/Prefabs/SettingsPage.prefab";

            var prefab = PrefabUtility.SaveAsPrefabAsset(page, Path);

            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(UiThemePath);
            if (theme == null || prefab == null) return;

            var so = new SerializedObject(theme);
            so.FindProperty("SettingsPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
        }

        struct Stepper
        {
            public Button Prev;
            public Button Next;
            public TextMeshProUGUI Value;
        }

        struct SliderRow
        {
            public Slider Slider;
            public TextMeshProUGUI Value;
        }

        struct ResetRow
        {
            public Button Button;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Hint;
        }

        /// <summary>
        /// Badan satu sub-halaman setelan. Keempatnya menumpuk persis di kotak yang sama; yang
        /// menentukan mana yang terlihat adalah <see cref="SettingsTabs"/>.
        /// </summary>
        static RectTransform NewTabBody(RectTransform panel, string name, float left, float top,
            Vector2 size)
        {
            var body = NewRect("Tab_" + name, panel);
            Place(body, new Vector2(0f, 1f), new Vector2(left, top), size);
            return body;
        }

        static void NewTabLine(RectTransform panel, Object[] into, int index, string label, float top)
        {
            var button = NewMenuLine(panel, "Tab" + index, label);

            Place((RectTransform)button.transform, new Vector2(0f, 1f),
                new Vector2(48f, top - index * 56f), new Vector2(268f, 52f));

            into[index] = button.GetComponent<MenuLine>();
        }

        static RectTransform NewRow(RectTransform host, string name, string label, ref float y)
        {
            var row = NewRect("Row_" + name, host);
            Place(row, new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(host.rect.width, 48f));

            var text = NewText("Label", row, label, _theme.BodySize,
                _theme.TextIdle, TextAlignmentOptions.Left);
            Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(320f, 30f));

            y -= 58f;
            return row;
        }

        /// <summary>
        /// Kontrol baris dipatok dari tepi KANAN barisnya, bukan dari koordinat tetap. Angka
        /// 340/740 yang dulu di-hardcode benar hanya selama barisnya selebar panel penuh; badan
        /// sub-halaman lebih sempit, dan di sana keduanya menggantung keluar baris.
        /// </summary>
        static Stepper NewStepper(RectTransform host, string label, ref float y)
        {
            var row = NewRow(host, label, label, ref y);

            const float Arrow = 44f;
            const float Value = 240f;
            float x0 = row.rect.width - (Arrow * 2f + Value + 24f) - 8f;

            var stepper = new Stepper
            {
                Prev = NewSmallButton(row, "Prev", "<", x0, Arrow),
                Next = NewSmallButton(row, "Next", ">", x0 + Arrow + Value + 24f, Arrow)
            };

            stepper.Value = NewText("Value", row, "-", _theme.BodySize,
                _theme.TextIdle, TextAlignmentOptions.Center);
            Place(stepper.Value.rectTransform, new Vector2(0f, 0.5f), new Vector2(x0 + Arrow + 12f, 0f),
                new Vector2(Value, 30f));

            return stepper;
        }

        static SliderRow NewSliderRow(RectTransform host, string label, ref float y)
        {
            var row = NewRow(host, label, label, ref y);

            const float Track = 260f;
            const float Value = 70f;
            float x0 = row.rect.width - (Track + Value + 20f) - 8f;

            var sliderGo = DefaultControls.CreateSlider(UiResources());
            sliderGo.name = "Slider";
            sliderGo.transform.SetParent(row, false);

            var rect = (RectTransform)sliderGo.transform;
            Place(rect, new Vector2(0f, 0.5f), new Vector2(x0, 0f), new Vector2(Track, 20f));

            var slider = sliderGo.GetComponent<Slider>();
            StyleSlider(slider);

            var value = NewText("Value", row, "100%", _theme.BodySize,
                _theme.TextMuted, TextAlignmentOptions.Right);
            Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(x0 + Track + 20f, 0f),
                new Vector2(Value, 30f));

            return new SliderRow { Slider = slider, Value = value };
        }

        static ResetRow NewResetRow(RectTransform host, ref float y)
        {
            var row = NewRow(host, "Reset", "Codex", ref y);

            const float Wide = 260f;
            float x0 = row.rect.width - Wide - 8f;

            var button = NewSmallButton(row, "Reset", "KOSONGKAN CODEX", x0, Wide);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.color = _theme.TextMuted;

            // Petunjuknya turun ke bawah label barisnya. Di kolom kanan yang sekarang lebih
            // sempit ia akan berdesakan dengan tombolnya sendiri.
            var hint = NewText("Hint", row, "butuh dua klik", _theme.SmallSize,
                _theme.TextMuted, TextAlignmentOptions.Left);
            Place(hint.rectTransform, new Vector2(0f, 0.5f), new Vector2(6f, -20f), new Vector2(400f, 20f));

            return new ResetRow { Button = button, Label = label, Hint = hint };
        }

        // ---------- widgets ----------

        static Button NewMenuLine(RectTransform parent, string name, string label)
        {
            var line = NewRect("MenuLine_" + name, parent);

            var element = line.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 52f;
            element.preferredHeight = 52f;

            // The click target has to be a graphic, but the look has no box — so it stays invisible.
            var hit = NewImage("Hit", line, new Color(1f, 1f, 1f, 0f));
            hit.raycastTarget = true;
            Stretch(hit.rectTransform);

            var marker = NewImage("Marker", line, _theme.Accent, _theme.MarkerSprite);
            Place(marker.rectTransform, new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(14f, 3f));

            var text = NewText("Label", line, label, _theme.MenuItemSize,
                _theme.TextIdle, TextAlignmentOptions.Left);
            Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(460f, 44f));

            var button = line.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            var menuLine = line.gameObject.AddComponent<MenuLine>();
            new Binder(menuLine)
                .Set("_label", text)
                .Set("_marker", marker.rectTransform)
                .SetColor("_idle", _theme.TextIdle)
                .SetColor("_highlight", _theme.Accent)
                .SetFloat("_slide", _theme.HoverSlide)
                .Apply();

            return button;
        }

        static Button NewSmallButton(RectTransform parent, string name, string label, float x,
            float width = 48f)
        {
            var go = NewRect("Btn_" + name, parent);
            Place(go, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(width, 40f));

            var image = go.gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.sprite = _theme.ButtonSprite;
            if (_theme.ButtonSprite != null) image.type = Image.Type.Sliced;

            var button = go.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            var accent = Rendered(_theme.Accent);

            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.05f);
            colors.highlightedColor = new Color(accent.r, accent.g, accent.b, 0.30f);
            colors.pressedColor = new Color(accent.r, accent.g, accent.b, 0.50f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.02f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var text = NewText("Label", go, label, _theme.BodySize,
                _theme.TextIdle, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);

            return button;
        }

        static RectTransform NewPanel(RectTransform parent, string name, Vector2 size)
        {
            var frame = NewImage(name + "Frame", parent, _theme.PanelLine, _theme.PanelSprite);
            Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);

            var fill = NewImage(name, frame.rectTransform, _theme.PanelFill, _theme.PanelSprite);
            Stretch(fill.rectTransform);
            fill.rectTransform.offsetMin = new Vector2(2f, 2f);
            fill.rectTransform.offsetMax = new Vector2(-2f, -2f);

            // Panels eat clicks so a stray press never reaches whatever sits behind them.
            fill.raycastTarget = true;

            return fill.rectTransform;
        }

        static void StyleSlider(Slider slider)
        {
            var background = slider.transform.Find("Background")?.GetComponent<Image>();
            if (background != null) background.color = new Color(1f, 1f, 1f, 0.08f);

            var fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
            if (fill != null) fill.color = Rendered(_theme.Accent);

            var handle = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
            if (handle != null) handle.color = Rendered(_theme.TextIdle);
        }

        static DefaultControls.Resources UiResources()
        {
            return new DefaultControls.Resources
            {
                standard = Builtin("UI/Skin/UISprite.psd"),
                background = Builtin("UI/Skin/Background.psd"),
                inputField = Builtin("UI/Skin/InputFieldBackground.psd"),
                knob = Builtin("UI/Skin/Knob.psd"),
                checkmark = Builtin("UI/Skin/Checkmark.psd"),
                dropdown = Builtin("UI/Skin/DropdownArrow.psd"),
                mask = Builtin("UI/Skin/UIMask.psd")
            };
        }

        static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        /// <summary>
        /// Colours written to materials and cameras from script are handed to the renderer raw, so
        /// in a linear project an authored sRGB value renders washed out. UI graphics convert on
        /// their own — this is only for the 3D side.
        /// </summary>
        static Color Rendered(Color color) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;

        // ---------- primitives ----------

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image NewImage(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var rect = NewRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Rendered(color);
            image.raycastTarget = false;

            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        static TextMeshProUGUI NewText(string name, Transform parent, string text, float size,
            Color color, TextAlignmentOptions align, bool title = false)
        {
            var rect = NewRect(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();

            tmp.font = title ? _theme.ResolveTitleFont() : _theme.ResolveBodyFont();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = Rendered(color);
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;

            return tmp;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        // ---------- assets ----------

        static MenuTheme LoadOrCreateTheme()
        {
            var existing = AssetDatabase.LoadAssetAtPath<MenuTheme>(ThemePath);
            if (existing != null) return existing;

            var theme = ScriptableObject.CreateInstance<MenuTheme>();
            EnsureFolder(Path.GetDirectoryName(ThemePath).Replace('\\', '/'));
            AssetDatabase.CreateAsset(theme, ThemePath);
            Debug.Log("[MainMenuBuilder] MenuTheme baru dibikin di " + ThemePath +
                      ". Ganti aset placeholder di sini.");

            return theme;
        }

        static Material LoadOrCreateMaterial(string name, Color color)
        {
            string path = MenuAssetDir + "/" + name + ".mat";

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // Shader choice is structural, not art: the first placeholders were Unlit and would
                // ignore the sun completely. Only repaint when we actually had to switch it.
                if (existing.shader != lit)
                {
                    existing.shader = lit;
                    _look.ApplySurface(existing, color);
                    EditorUtility.SetDirty(existing);
                }

                return existing;
            }

            var material = new Material(lit);
            _look.ApplySurface(material, color);

            EnsureFolder(MenuAssetDir);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static T FindAsset<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids.Length == 0) return null;

            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        static Scene FindOpenScene(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.path == path) return scene;
            }

            return default;
        }

        /// <summary>
        /// MENAMBAHKAN menu & scene game ke Build Settings — tidak pernah menulis ulang daftarnya.
        ///
        /// Versi lama mengganti seluruh daftar dengan {menu, game}, dan itu bug yang baru
        /// kelihatan begitu ada scene ketiga: tiap rebuild menu menyapu scene ruangan
        /// toko/kejadian/slot dari daftar, dan RoomLoader menolaknya saat run — panel singgah
        /// jatuh balik ke atas arena tanpa error, cuma "kok nggak pindah". Daftar Build Settings
        /// itu milik BERSAMA; builder boleh menjamin barisnya sendiri ada, bukan menghapus
        /// baris orang lain.
        /// </summary>
        static void RegisterBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            // Menu selalu indeks 0 — scene pertama adalah yang dimuat build saat boot.
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));

            if (!File.Exists(GameScenePath))
            {
                Debug.LogWarning("[MainMenuBuilder] " + GameScenePath +
                                 " tidak ketemu — MULAI bakal gagal load scene.");
            }
            else if (!scenes.Exists(s => s.path == GameScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(GameScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Writes private [SerializeField] references without making them public.</summary>
        class Binder
        {
            readonly SerializedObject _target;

            public Binder(Object target) => _target = new SerializedObject(target);

            public Binder Set(string field, Object value)
            {
                var property = Find(field);
                if (property != null) property.objectReferenceValue = value;
                return this;
            }

            public Binder SetArray(string field, IReadOnlyList<Object> values)
            {
                var property = Find(field);
                if (property == null) return this;

                property.arraySize = values.Count;
                for (int i = 0; i < values.Count; i++)
                {
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }

                return this;
            }

            /// <summary>Converted like every other colour here: these fields are pushed straight
            /// onto Graphic.color / TMP.color at runtime, which is the same raw path.</summary>
            public Binder SetColor(string field, Color value)
            {
                var property = Find(field);
                if (property != null) property.colorValue = Rendered(value);
                return this;
            }

            public Binder SetFloat(string field, float value)
            {
                var property = Find(field);
                if (property != null) property.floatValue = value;
                return this;
            }

            public Binder SetString(string field, string value)
            {
                var property = Find(field);
                if (property != null) property.stringValue = value;
                return this;
            }

            public void Apply() => _target.ApplyModifiedPropertiesWithoutUndo();

            SerializedProperty Find(string field)
            {
                var property = _target.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError("[MainMenuBuilder] field '" + field + "' tidak ada di " +
                                   _target.targetObject.GetType().Name);
                }

                return property;
            }
        }
    }
}
