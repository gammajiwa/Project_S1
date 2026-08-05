using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Proto
{
    /// <summary>
    /// Sets up the shared golden-hour look: the SceneLook asset, a post-processing profile, the
    /// URP asset flags those need, and the wiring into the game scene.
    ///
    /// Create-only on purpose — re-running never overwrites a look you have since tuned. Delete the
    /// asset if you want the defaults back.
    /// </summary>
    public static class LookBuilder
    {
        const string LegacyLookPath = "Assets/GameData/SceneLook.asset";
        const string GameLookPath = "Assets/GameData/SceneLook_Game.asset";
        const string MenuLookPath = "Assets/GameData/SceneLook_Menu.asset";
        const string ProfilePath = "Assets/GameData/Look/PP_GoldenHour.asset";
        const string GameScenePath = "Assets/Scenes/Proto.unity";

        /// <summary>
        /// The run needs a dark floor and the menu wants a bright one, and no single value serves
        /// both: enemies, spell VFX and the light-on-dark HUD all stop reading once the floor is
        /// bright. Sun, ambient, fog and grading stay shared, so it is still the same afternoon.
        /// </summary>
        static readonly Color GameGround = new Color(0.20f, 0.185f, 0.150f);

        [MenuItem("Tools/Grimoire/Build Scene Look")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[LookBuilder] stop Play mode dulu.");
                return;
            }

            MigrateLegacyLook();

            var gameLook = LoadOrCreateLook(GameLookPath, GameGround);
            var menuLook = LoadOrCreateLook(MenuLookPath, null);
            var profile = LoadOrCreateProfile();

            Attach(gameLook, profile);
            Attach(menuLook, profile);

            TuneRenderPipelineAssets();
            AssetDatabase.SaveAssets();

            WireGameScene(gameLook);

            Debug.Log("[LookBuilder] look siap. Game: " + GameLookPath + " (lantai gelap) - " +
                      "Menu: " + MenuLookPath + " (lantai terang). Post-processing dipakai bareng di " +
                      ProfilePath + ". Jalankan 'Build Main Menu' supaya scene menu ikut memakainya.");
        }

        /// <summary>The first version shipped one shared asset; keep whatever was tuned in it.</summary>
        static void MigrateLegacyLook()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneLook>(LegacyLookPath) == null) return;
            if (AssetDatabase.LoadAssetAtPath<SceneLook>(GameLookPath) != null) return;

            string error = AssetDatabase.MoveAsset(LegacyLookPath, GameLookPath);
            if (string.IsNullOrEmpty(error))
            {
                // Moving rather than recreating keeps the reference already wired into Proto.unity.
                Debug.Log("[LookBuilder] " + LegacyLookPath + " dipindah ke " + GameLookPath + ".");
            }
            else
            {
                Debug.LogWarning("[LookBuilder] gagal memindah look lama: " + error);
            }
        }

        static void Attach(SceneLook look, VolumeProfile profile)
        {
            if (look == null || look.PostProcess != null) return;

            look.PostProcess = profile;
            EditorUtility.SetDirty(look);
        }

        static SceneLook LoadOrCreateLook(string path, Color? groundOverride)
        {
            var existing = AssetDatabase.LoadAssetAtPath<SceneLook>(path);
            if (existing != null) return existing;

            // Field defaults already describe the golden hour; only the floor differs per scene.
            var look = ScriptableObject.CreateInstance<SceneLook>();
            if (groundOverride.HasValue) look.GroundColor = groundOverride.Value;

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateAsset(look, path);
            return look;
        }

        static VolumeProfile LoadOrCreateProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(ProfilePath).Replace('\\', '/'));

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            // Neutral rather than ACES: ACES crushes the warm highlights this look is built on.
            var tonemapping = AddComponent<Tonemapping>(profile);
            Override(tonemapping.mode, TonemappingMode.Neutral);

            var color = AddComponent<ColorAdjustments>(profile);
            Override(color.postExposure, 0.25f);

            // Kept low deliberately: a flat floor fills the frame, and contrast here eats it whole.
            Override(color.contrast, 5f);
            Override(color.saturation, 8f);

            var white = AddComponent<WhiteBalance>(profile);
            Override(white.temperature, 12f);
            Override(white.tint, 2f);

            // Cool shadows against a warm sun — this is what reads as late afternoon.
            var split = AddComponent<ShadowsMidtonesHighlights>(profile);
            Override(split.shadows, new Vector4(0.88f, 0.94f, 1.10f, 0f));
            Override(split.highlights, new Vector4(1.10f, 1.02f, 0.90f, 0f));

            var bloom = AddComponent<Bloom>(profile);
            Override(bloom.threshold, 1.1f);
            Override(bloom.intensity, 0.4f);
            Override(bloom.scatter, 0.65f);

            // Same reason as contrast: the vignette lands on the floor, not on empty background.
            var vignette = AddComponent<Vignette>(profile);
            Override(vignette.intensity, 0.16f);
            Override(vignette.smoothness, 0.5f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static T AddComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>();
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        static void Override<T>(VolumeParameter<T> parameter, T value)
        {
            parameter.overrideState = true;
            parameter.value = value;
        }

        /// <summary>
        /// HDR grading and MSAA are what separate "URP is on" from "URP looks like anything".
        /// Flat untextured polygons alias badly, and bloom needs somewhere above 1.0 to live.
        /// </summary>
        static void TuneRenderPipelineAssets()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null) continue;

                bool mobile = path.Contains("Mobile");

                var so = new SerializedObject(asset);
                SetInt(so, "m_ColorGradingMode", 1);
                SetInt(so, "m_MSAA", mobile ? 2 : 4);
                SetInt(so, "m_SupportsHDR", 1);
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(asset);
                Debug.Log("[LookBuilder] " + Path.GetFileName(path) + ": HDR grading + MSAA " +
                          (mobile ? "2x" : "4x"));
            }
        }

        static void SetInt(SerializedObject so, string field, int value)
        {
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning("[LookBuilder] field '" + field + "' tidak ada di URP asset ini.");
                return;
            }

            property.intValue = value;
        }

        /// <summary>Assigns the look into Proto.unity's bootstrap; nothing else in that scene is touched.</summary>
        static void WireGameScene(SceneLook look)
        {
            if (!File.Exists(GameScenePath))
            {
                Debug.LogWarning("[LookBuilder] " + GameScenePath + " tidak ketemu.");
                return;
            }

            var open = FindOpenScene(GameScenePath);
            bool alreadyOpen = open.IsValid();
            var scene = alreadyOpen
                ? open
                : EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

            var bootstrap = FindBootstrap(scene);
            if (bootstrap == null)
            {
                Debug.LogWarning("[LookBuilder] ProtoBootstrap tidak ketemu di " + GameScenePath + ".");
                if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            var so = new SerializedObject(bootstrap);
            var property = so.FindProperty("_look");
            if (property == null)
            {
                Debug.LogError("[LookBuilder] ProtoBootstrap tidak punya field '_look'.");
                if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            property.objectReferenceValue = look;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("[LookBuilder] gagal menyimpan " + GameScenePath + ".");
            }

            if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[LookBuilder] SceneLook dipasang ke ProtoBootstrap di " + GameScenePath + ".");
        }

        static ProtoBootstrap FindBootstrap(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<ProtoBootstrap>(true);
                if (found != null) return found;
            }

            return null;
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

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
