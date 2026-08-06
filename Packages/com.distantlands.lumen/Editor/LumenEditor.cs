// Distant Lands 2025
// Lumen: Stylized Light FX 2
// The contents of this file are protected under the Unity Asset Store EULA
//
// Provides the main custom inspector and editor logic for the Lumen effect player in the Unity Editor.

using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System.Linq;

namespace DistantLands.Lumen.EditorScripts
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(LumenEffectPlayer))]
    public class LumenEffectPlayerEditor : Editor
    {
        VisualElement root;
        public LumenEffectPlayer effectPlayer;

        [MenuItem("GameObject/Effects/Empty Lumen Player", false, 100)]
        public static void CreateLumenEffectPlayer(MenuCommand menuCommand)
        {
            GameObject go = new();
            go.name = "Lumen Effect Player";

            // Ensure it gets parented to the context object if it's selected
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

            LumenEffectPlayer player = go.AddComponent<LumenEffectPlayer>();
            if (Selection.activeObject is LumenEffectProfile)
            {
                player.profile = (LumenEffectProfile)Selection.activeObject;
                go.name = player.profile.name;
            }

            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
        }

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();
            effectPlayer = target as LumenEffectPlayer;

            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.distantlands.lumen/Editor/USS/Lumen.uss"
            ));

            VisualElement banner = new VisualElement();
            banner.AddToClassList("banner");

            root.Add(banner);

            VisualElement localSettings = new VisualElement();
            root.Add(localSettings);

            Label localSettingsLabel = new Label("Local Settings");
            localSettingsLabel.AddToClassList("sectionLabel");
            localSettings.Add(localSettingsLabel);

            PropertyField scale = new PropertyField();
            scale.BindProperty(serializedObject.FindProperty("scale"));
            scale.RegisterValueChangeCallback(evt =>
            {
                if (effectPlayer != null) 
                    effectPlayer.RedoEffect();
            });
            localSettings.Add(scale);

            PropertyField brightness = new PropertyField();
            brightness.BindProperty(serializedObject.FindProperty("brightness"));
            brightness.RegisterValueChangeCallback(evt =>
            {
                if (effectPlayer != null) 
                    effectPlayer.RedoEffect();
            });
            localSettings.Add(brightness);

            PropertyField color = new PropertyField();
            color.BindProperty(serializedObject.FindProperty("color"));
            color.RegisterValueChangeCallback(evt =>
            {
                if (effectPlayer != null) 
                    effectPlayer.RedoEffect();
            });
            color.AddToClassList("verticalBreak");
            localSettings.Add(color);

            VisualElement lightLayerSettingsContainer = new VisualElement();
            lightLayerSettingsContainer.style.display = effectPlayer.profile && effectPlayer.profile.layers.Any(x => x is LumenLightLayer) ? DisplayStyle.Flex : DisplayStyle.None;
            lightLayerSettingsContainer.AddToClassList("verticalBreak");
            localSettings.Add(lightLayerSettingsContainer);

            if (effectPlayer.profile && effectPlayer.profile.layers.Any(x => x is LumenLightLayer))
            {
                PropertyField range = new PropertyField();
                range.BindProperty(serializedObject.FindProperty("range"));
                lightLayerSettingsContainer.Add(range);
            }

            VisualElement dynamicRaySettingsContainer = new VisualElement();
            dynamicRaySettingsContainer.style.display = effectPlayer.profile && effectPlayer.profile.layers.Any(x => x is DynamicRayLayer) ? DisplayStyle.Flex : DisplayStyle.None;
            dynamicRaySettingsContainer.AddToClassList("verticalBreak");
            localSettings.Add(dynamicRaySettingsContainer);


            if (effectPlayer.profile && effectPlayer.profile.layers.Any(x => x is DynamicRayLayer))
            {
                PropertyField autoAssignSun = new PropertyField();
                autoAssignSun.BindProperty(serializedObject.FindProperty("autoAssignSun"));
                VisualElement dontAssignSunFoldout = new VisualElement();
                PropertyField useLumenSunScript = new PropertyField();
                useLumenSunScript.BindProperty(serializedObject.FindProperty("useLumenSunScript"));
                PropertyField localSunDirection = new PropertyField();
                localSunDirection.BindProperty(serializedObject.FindProperty("localSunDirection"));

                autoAssignSun.RegisterValueChangeCallback(evt =>
                {
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();

                    if (effectPlayer != null)
                        effectPlayer.RedoEffect();

                    dontAssignSunFoldout.style.display = !effectPlayer.autoAssignSun ? DisplayStyle.Flex : DisplayStyle.None;
                });

                useLumenSunScript.RegisterValueChangeCallback(evt =>
                {
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();

                    if (effectPlayer != null) 
                        effectPlayer.RedoEffect();

                    localSunDirection.style.display = !effectPlayer.useLumenSunScript ? DisplayStyle.Flex : DisplayStyle.None;
                });

                localSunDirection.RegisterValueChangeCallback(evt =>
                {
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();

                    if (effectPlayer != null)
                        effectPlayer.RedoEffect();
                });


                dynamicRaySettingsContainer.Add(autoAssignSun);
                dontAssignSunFoldout.Add(useLumenSunScript);
                dontAssignSunFoldout.Add(localSunDirection);
                dynamicRaySettingsContainer.Add(dontAssignSunFoldout);
            }



            PropertyField updateFrequency = new PropertyField();
            updateFrequency.BindProperty(serializedObject.FindProperty("updateFrequency"));
            updateFrequency.AddToClassList("verticalBreak");
            localSettings.Add(updateFrequency);



            PropertyField initializationBehavior = new PropertyField();
            initializationBehavior.BindProperty(serializedObject.FindProperty("initializationBehavior"));
            localSettings.Add(initializationBehavior);

            PropertyField deinitializationBehavior = new PropertyField();
            deinitializationBehavior.BindProperty(serializedObject.FindProperty("deinitializationBehavior"));
            localSettings.Add(deinitializationBehavior);


            PropertyField fadingTime = new PropertyField();
            fadingTime.BindProperty(serializedObject.FindProperty("fadingTime"));
            void RedrawFadeSettings()
            {
                serializedObject.ApplyModifiedProperties();
                bool useFade = !(effectPlayer.deinitializationBehavior == LumenEffectPlayer.DeinitializationBehavior.Immediate) || !(effectPlayer.initializationBehavior == LumenEffectPlayer.InitializationBehavior.Immediate || effectPlayer.initializationBehavior == LumenEffectPlayer.InitializationBehavior.SkipInitialization);
                fadingTime.style.display = useFade ? DisplayStyle.Flex : DisplayStyle.None;
            }
            RedrawFadeSettings();
            initializationBehavior.RegisterValueChangeCallback(evt => RedrawFadeSettings());
            deinitializationBehavior.RegisterValueChangeCallback(evt => RedrawFadeSettings());
            localSettings.Add(fadingTime);

            VisualElement profileContainer = new VisualElement();
            profileContainer.AddToClassList("effectSelector");
            root.Add(profileContainer);

            VisualElement icon = new VisualElement();
            icon.AddToClassList("icon");
            profileContainer.Add(icon);

            VisualElement profileSettings = new VisualElement();
            ObjectField profileProperty = new ObjectField();
            profileProperty.objectType = typeof(LumenEffectProfile);
            profileProperty.BindProperty(serializedObject.FindProperty("profile"));

            void DrawProfileEditor()
            {
                profileSettings.Clear();

                if (effectPlayer.profile == null) return;

                profileSettings.Add(((LumenEffectEditor)CreateEditor(effectPlayer.profile)).DrawInlineEditor(effectPlayer));
            }

            profileProperty.RegisterValueChangedCallback(evt => DrawProfileEditor());
            profileContainer.Add(profileProperty);

            root.Add(profileSettings);

            return root;
        }

        void OnSceneGUI()
        {
            if (!effectPlayer || effectPlayer.profile == null) return;

            LumenEffectEditor.DrawHandles(effectPlayer);
        }

    }
}
