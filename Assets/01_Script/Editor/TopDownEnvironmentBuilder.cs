using System.Collections.Generic;
using System.IO;
using ProjectS1.Gameplay;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ProjectS1.EditorTools
{
    /// <summary>
    /// One-click builder for the top-down sandbox environment.
    /// Generates the scene through the Editor API so Unity authors valid scene/asset YAML.
    /// Re-running rebuilds the scene from scratch; generated materials and textures are reused.
    /// </summary>
    public static class TopDownEnvironmentBuilder
    {
        private const string SceneFolder = "Assets/07_Scene";
        private const string ScenePath = SceneFolder + "/TopDown_Sandbox.unity";
        private const string NavMeshPath = SceneFolder + "/TopDown_Sandbox_NavMesh.asset";
        private const string MaterialFolder = "Assets/05_Material/Environment";
        private const string TextureFolder = "Assets/02_Art/Textures";
        private const string GridTexturePath = TextureFolder + "/T_GridCheck.png";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string VolumeProfilePath = "Assets/Settings/DefaultVolumeProfile.asset";

        private const float ArenaSize = 40f;
        private const float WallHeight = 3f;
        private const float WallThickness = 1f;

        [MenuItem("Tools/Project_S1/Build Top-Down Environment", priority = 0)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolder(SceneFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(TextureFolder);

            Texture2D grid = EnsureGridTexture();
            Material groundMat = EnsureMaterial("M_Ground", new Color(0.78f, 0.79f, 0.82f), grid, 20f, 0.15f);
            Material wallMat = EnsureMaterial("M_Wall", new Color(0.36f, 0.38f, 0.44f), null, 1f, 0.1f);
            Material obstacleMat = EnsureMaterial("M_Obstacle", new Color(0.55f, 0.42f, 0.30f), null, 1f, 0.2f);
            Material playerMat = EnsureMaterial("M_Player", new Color(0.24f, 0.62f, 0.90f), null, 1f, 0.35f);
            Material accentMat = EnsureMaterial("M_Accent", new Color(0.92f, 0.72f, 0.24f), null, 1f, 0.4f);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            GameObject envRoot = BuildEnvironment(groundMat, wallMat, obstacleMat);
            GameObject player = BuildPlayer(playerMat, accentMat);
            GameObject camera = BuildCamera(player.transform);
            LinkPlayerCamera(player, camera.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            BakeNavMesh(envRoot);

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"[TopDownEnvironmentBuilder] Built {ScenePath}. Press Play to drive the player with WASD / left stick.");
        }

        // ---------------------------------------------------------------- lighting

        private static void BuildLighting()
        {
            GameObject lightGO = new GameObject("Directional Light");
            Light light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.90f);
            light.intensity = 1.4f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            GameObject volumeGO = new GameObject("Global Volume");
            Volume volume = volumeGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile != null)
            {
                volume.sharedProfile = profile;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.58f, 0.65f);
            RenderSettings.ambientEquatorColor = new Color(0.40f, 0.41f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.22f, 0.24f);
        }

        // ------------------------------------------------------------- environment

        private static GameObject BuildEnvironment(Material groundMat, Material wallMat, Material obstacleMat)
        {
            GameObject root = new GameObject("Environment");

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localScale = new Vector3(ArenaSize / 10f, 1f, ArenaSize / 10f);
            ground.isStatic = true;
            ApplyMaterial(ground, groundMat);

            GameObject walls = new GameObject("Walls");
            walls.transform.SetParent(root.transform, false);

            float half = ArenaSize * 0.5f;
            float span = ArenaSize + WallThickness;
            float y = WallHeight * 0.5f;
            CreateBox(walls.transform, "Wall_North", new Vector3(0f, y, half), new Vector3(span, WallHeight, WallThickness), wallMat);
            CreateBox(walls.transform, "Wall_South", new Vector3(0f, y, -half), new Vector3(span, WallHeight, WallThickness), wallMat);
            CreateBox(walls.transform, "Wall_East", new Vector3(half, y, 0f), new Vector3(WallThickness, WallHeight, span), wallMat);
            CreateBox(walls.transform, "Wall_West", new Vector3(-half, y, 0f), new Vector3(WallThickness, WallHeight, span), wallMat);

            GameObject obstacles = new GameObject("Obstacles");
            obstacles.transform.SetParent(root.transform, false);

            // Corner pillars — readable landmarks for orientation.
            CreateBox(obstacles.transform, "Pillar_NE", new Vector3(9f, 2f, 9f), new Vector3(2f, 4f, 2f), obstacleMat);
            CreateBox(obstacles.transform, "Pillar_NW", new Vector3(-9f, 2f, 9f), new Vector3(2f, 4f, 2f), obstacleMat);
            CreateBox(obstacles.transform, "Pillar_SE", new Vector3(9f, 2f, -9f), new Vector3(2f, 4f, 2f), obstacleMat);
            CreateBox(obstacles.transform, "Pillar_SW", new Vector3(-9f, 2f, -9f), new Vector3(2f, 4f, 2f), obstacleMat);

            // Cover geometry — gives pathfinding and camera framing something to chew on.
            CreateBox(obstacles.transform, "Cover_West", new Vector3(-13f, 1f, 2f), new Vector3(6f, 2f, 1f), obstacleMat);
            CreateBox(obstacles.transform, "Cover_East", new Vector3(13f, 1f, -5f), new Vector3(1f, 2f, 8f), obstacleMat);
            CreateBox(obstacles.transform, "Cover_L_A", new Vector3(0f, 1f, 13f), new Vector3(8f, 2f, 1f), obstacleMat);
            CreateBox(obstacles.transform, "Cover_L_B", new Vector3(4f, 1f, 10.5f), new Vector3(1f, 2f, 6f), obstacleMat);

            // Crate stack.
            CreateBox(obstacles.transform, "Crate_A", new Vector3(6f, 0.75f, -3f), new Vector3(1.5f, 1.5f, 1.5f), obstacleMat);
            CreateBox(obstacles.transform, "Crate_B", new Vector3(7.6f, 0.75f, -2.2f), new Vector3(1.5f, 1.5f, 1.5f), obstacleMat);
            CreateBox(obstacles.transform, "Crate_C", new Vector3(6.8f, 2.25f, -2.6f), new Vector3(1.5f, 1.5f, 1.5f), obstacleMat);

            return root;
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.isStatic = true;
            ApplyMaterial(box, material);
            return box;
        }

        // ------------------------------------------------------------------ actors

        private static GameObject BuildPlayer(Material bodyMat, Material accentMat)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 1.1f, -12f);
            ApplyMaterial(player, bodyMat);

            // CharacterController replaces the primitive's collider.
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.4f;
            controller.center = Vector3.zero;
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.35f;
            controller.skinWidth = 0.02f;

            // Facing indicator — top-down needs a visible forward direction.
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "FacingIndicator";
            nose.transform.SetParent(player.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.35f, 0.55f);
            nose.transform.localScale = new Vector3(0.25f, 0.25f, 0.6f);
            Object.DestroyImmediate(nose.GetComponent<BoxCollider>());
            ApplyMaterial(nose, accentMat);

            PlayerInput input = player.AddComponent<PlayerInput>();
            ConfigurePlayerInput(input);

            player.AddComponent<TopDownPlayerController>();
            return player;
        }

        private static void ConfigurePlayerInput(PlayerInput input)
        {
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null)
            {
                Debug.LogWarning($"[TopDownEnvironmentBuilder] {InputActionsPath} not found — assign the actions asset on PlayerInput manually.");
                return;
            }

            SerializedObject so = new SerializedObject(input);
            SetProperty(so, "m_Actions", actions);
            SetProperty(so, "m_DefaultActionMap", "Player");
            SetProperty(so, "m_NotificationBehavior", (int)PlayerNotifications.SendMessages);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject BuildCamera(Transform target)
        {
            GameObject cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";

            Camera camera = cameraGO.AddComponent<Camera>();
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraGO.AddComponent<UniversalAdditionalCameraData>();
            cameraGO.AddComponent<AudioListener>();

            TopDownCameraRig rig = cameraGO.AddComponent<TopDownCameraRig>();
            rig.Target = target;
            rig.SnapToTarget();
            return cameraGO;
        }

        private static void LinkPlayerCamera(GameObject player, Transform cameraTransform)
        {
            TopDownPlayerController controller = player.GetComponent<TopDownPlayerController>();
            SerializedObject so = new SerializedObject(controller);
            SetProperty(so, "_cameraTransform", cameraTransform);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- navmesh

        private static void BakeNavMesh(GameObject envRoot)
        {
            try
            {
                NavMeshSurface surface = envRoot.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();

                if (surface.navMeshData != null)
                {
                    AssetDatabase.DeleteAsset(NavMeshPath);
                    AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TopDownEnvironmentBuilder] NavMesh bake skipped: {e.Message}. " +
                                 "Select Environment and press Bake on the NavMeshSurface component.");
            }
        }

        // ----------------------------------------------------------------- assets

        private static Texture2D EnsureGridTexture()
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
            if (existing != null)
            {
                return existing;
            }

            const int size = 256;
            const int half = size / 2;
            const int lineWidth = 2;

            Color light = new Color(0.86f, 0.87f, 0.89f);
            Color dark = new Color(0.76f, 0.77f, 0.80f);
            Color line = new Color(0.58f, 0.59f, 0.64f);

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool checker = (x < half) ^ (y < half);
                    Color color = checker ? light : dark;

                    bool onEdge = x < lineWidth || y < lineWidth || x >= size - lineWidth || y >= size - lineWidth;
                    bool onMidline = Mathf.Abs(x - half) < lineWidth || Mathf.Abs(y - half) < lineWidth;
                    if (onEdge || onMidline)
                    {
                        color = line;
                    }

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            File.WriteAllBytes(GridTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(GridTexturePath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(GridTexturePath) is TextureImporter importer)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.anisoLevel = 8;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
        }

        private static Material EnsureMaterial(string name, Color color, Texture2D baseMap, float tiling, float smoothness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else
            {
                material.color = color;
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
            else if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (baseMap != null && material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", baseMap);
                material.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyMaterial(GameObject target, Material material)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        // ------------------------------------------------------------------ utils

        private static void RegisterInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == ScenePath))
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void SetProperty(SerializedObject so, string propertyPath, Object value)
        {
            SerializedProperty property = so.FindProperty(propertyPath);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetProperty(SerializedObject so, string propertyPath, string value)
        {
            SerializedProperty property = so.FindProperty(propertyPath);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        private static void SetProperty(SerializedObject so, string propertyPath, int value)
        {
            SerializedProperty property = so.FindProperty(propertyPath);
            if (property != null)
            {
                property.intValue = value;
            }
        }
    }
}
