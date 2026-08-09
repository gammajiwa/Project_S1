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

            var title = NewText("Title", page, "G R I M O I R E   H A V E N",
                _theme.TitleSize, _theme.TextIdle, TextAlignmentOptions.Left, true);
            Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(180f, 250f), new Vector2(1400f, 120f));

            var tagline = NewText("Tagline", page, "penyihir yang menulis, bukan yang bertarung",
                _theme.TaglineSize, _theme.TextMuted, TextAlignmentOptions.Left);
            Place(tagline.rectTransform, new Vector2(0f, 0.5f), new Vector2(186f, 180f), new Vector2(900f, 30f));

            var rule = NewImage("Rule", page, _theme.PanelLine);
            Place(rule.rectTransform, new Vector2(0f, 0.5f), new Vector2(186f, 150f), new Vector2(420f, 2f));

            var list = NewRect("MenuList", page);
            Place(list, new Vector2(0f, 0.5f), new Vector2(180f, 90f), new Vector2(520f, 10f));
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
        /// </summary>
        static RectTransform BuildStarterPage(RectTransform canvas, ContentDatabase database,
            GameBalance balance, out StarterSelectPanel panelComponent, out Button back)
        {
            var page = NewRect("StarterPage", canvas);
            Stretch(page);

            var panel = NewPanel(page, "StarterPanel", new Vector2(1420f, 840f));
            panelComponent = panel.gameObject.AddComponent<StarterSelectPanel>();

            var heading = NewText("Heading", panel, "PILIH GRIMOIRE", _theme.HeadingSize,
                _theme.TextIdle, TextAlignmentOptions.Left, true);
            Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -40f), new Vector2(700f, 52f));

            var page01 = NewText("PageLabel", panel, "1 / 1", _theme.BodySize,
                _theme.Accent, TextAlignmentOptions.Right);
            Place(page01.rectTransform, new Vector2(1f, 1f), new Vector2(-48f, -50f), new Vector2(300f, 30f));

            // Papan di tengah, persegi. Sisi kiri untuk namanya, sisi kanan untuk portrait —
            // papannya sendiri tidak boleh ikut melar mengikuti sisa ruang, karena petak yang
            // gepeng membuat bentuk piece berbohong.
            var board = NewRect("Board", panel);
            Place(board, new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(470f, 470f));

            var name = NewText("Name", panel, "Nama Starter", _theme.HeadingSize,
                _theme.Accent, TextAlignmentOptions.Left, true);
            Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(48f, 150f), new Vector2(420f, 48f));

            var blurb = NewText("Blurb", panel, "", _theme.BodySize,
                _theme.TextMuted, TextAlignmentOptions.TopLeft);
            Place(blurb.rectTransform, new Vector2(0f, 0.5f), new Vector2(48f, 96f), new Vector2(420f, 220f));
            blurb.textWrappingMode = TextWrappingModes.Normal;

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

            new Binder(panelComponent)
                .Set("_database", database)
                .Set("_balance", balance)
                .SetString("_gameSceneName", "Proto")
                .Set("_nameLabel", name)
                .Set("_blurbLabel", blurb)
                .Set("_statsLabel", stats)
                .Set("_pageLabel", page01)
                .Set("_portrait", portrait)
                .Set("_board", board)
                .Set("_prevButton", prev)
                .Set("_nextButton", next)
                .Set("_playButton", play)
                .Apply();

            return page;
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

            var panel = NewPanel(page, "SettingsPanel", new Vector2(1180f, 840f));
            panelComponent = panel.gameObject.AddComponent<SettingsPanel>();

            var heading = NewText("Heading", panel, "SETELAN", _theme.HeadingSize,
                _theme.TextIdle, TextAlignmentOptions.Left, true);
            Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -40f), new Vector2(600f, 52f));

            float y = -130f;

            NewSection(panel, "LAYAR", ref y);
            var fullscreen = NewStepper(panel, "Mode layar", ref y);
            var resolution = NewStepper(panel, "Resolusi", ref y);
            var vsync = NewStepper(panel, "VSync", ref y);
            var frameCap = NewStepper(panel, "Batas FPS", ref y);

            y -= 18f;
            NewSection(panel, "SUARA", ref y);
            var master = NewSliderRow(panel, "Master", ref y);
            var sfx = NewSliderRow(panel, "Efek suara", ref y);
            var music = NewSliderRow(panel, "Musik", ref y);

            y -= 18f;
            NewSection(panel, "DATA", ref y);
            var reset = NewResetRow(panel, ref y);

            // Opposite the back button: the rows above run all the way down to it.
            var note = NewText("Note", panel, "", _theme.SmallSize,
                _theme.TextMuted, TextAlignmentOptions.Right);
            Place(note.rectTransform, new Vector2(1f, 0f), new Vector2(-48f, 38f), new Vector2(760f, 24f));

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

        static void NewSection(RectTransform panel, string title, ref float y)
        {
            var label = NewText("Section_" + title, panel, title, _theme.SmallSize,
                _theme.Accent, TextAlignmentOptions.Left);
            Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(48f, y), new Vector2(400f, 22f));
            y -= 40f;
        }

        static RectTransform NewRow(RectTransform panel, string name, string label, ref float y)
        {
            var row = NewRect("Row_" + name, panel);
            Place(row, new Vector2(0f, 1f), new Vector2(48f, y), new Vector2(1080f, 48f));
            row.pivot = new Vector2(0f, 1f);

            var text = NewText("Label", row, label, _theme.BodySize,
                _theme.TextIdle, TextAlignmentOptions.Left);
            Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(320f, 30f));

            y -= 58f;
            return row;
        }

        static Stepper NewStepper(RectTransform panel, string label, ref float y)
        {
            var row = NewRow(panel, label, label, ref y);

            var stepper = new Stepper
            {
                Prev = NewSmallButton(row, "Prev", "<", 340f),
                Next = NewSmallButton(row, "Next", ">", 740f)
            };

            stepper.Value = NewText("Value", row, "-", _theme.BodySize,
                _theme.TextIdle, TextAlignmentOptions.Center);
            Place(stepper.Value.rectTransform, new Vector2(0f, 0.5f), new Vector2(396f, 0f),
                new Vector2(336f, 30f));

            return stepper;
        }

        static SliderRow NewSliderRow(RectTransform panel, string label, ref float y)
        {
            var row = NewRow(panel, label, label, ref y);

            var sliderGo = DefaultControls.CreateSlider(UiResources());
            sliderGo.name = "Slider";
            sliderGo.transform.SetParent(row, false);

            var rect = (RectTransform)sliderGo.transform;
            Place(rect, new Vector2(0f, 0.5f), new Vector2(340f, 0f), new Vector2(400f, 20f));

            var slider = sliderGo.GetComponent<Slider>();
            StyleSlider(slider);

            var value = NewText("Value", row, "100%", _theme.BodySize,
                _theme.TextMuted, TextAlignmentOptions.Right);
            Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(760f, 0f), new Vector2(120f, 30f));

            return new SliderRow { Slider = slider, Value = value };
        }

        static ResetRow NewResetRow(RectTransform panel, ref float y)
        {
            var row = NewRow(panel, "Reset", "Codex", ref y);

            var button = NewSmallButton(row, "Reset", "KOSONGKAN CODEX", 340f, 320f);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.color = _theme.TextMuted;

            var hint = NewText("Hint", row, "butuh dua klik", _theme.SmallSize,
                _theme.TextMuted, TextAlignmentOptions.Left);
            Place(hint.rectTransform, new Vector2(0f, 0.5f), new Vector2(676f, 0f), new Vector2(400f, 24f));

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

        static void RegisterBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            if (File.Exists(GameScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(GameScenePath, true));
            }
            else
            {
                Debug.LogWarning("[MainMenuBuilder] " + GameScenePath +
                                 " tidak ketemu — MULAI bakal gagal load scene.");
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
