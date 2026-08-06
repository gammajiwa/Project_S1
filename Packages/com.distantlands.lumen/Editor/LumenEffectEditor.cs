// Distant Lands 2025
// Lumen: Stylized Light FX 2
// The contents of this file are protected under the Unity Asset Store EULA
//
// Provides the main custom inspector and editor logic for Lumen effect layers in the Unity Editor.

using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DistantLands.Lumen.EditorScripts
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(LumenEffectProfile))]
    public class LumenEffectEditor : Editor
    {
        VisualElement root;
        List<bool> foldoutStates;
        List<bool> layerFoldoutStates;
        LumenSettings global;
        LumenEffectProfile targetEffect;
        LumenEffectPlayer attachedPlayer;
        static int handleIndex = -1;
        bool refreshQueued;

        public override VisualElement CreateInspectorGUI()
        {
            global = LumenSettings.Global;
            handleIndex = -1;

            if (attachedPlayer)
                attachedPlayer.RedoEffect();

            root = new VisualElement();
            root.name = "LumenRoot";
            root.viewDataKey = "LumenInspectorRoot";

            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.distantlands.lumen/Editor/USS/Lumen.uss"
            ));

            VisualElement effectLayerSettings = new VisualElement();
            root.Add(effectLayerSettings);

            Label effectLayerLabel = new Label("Effect Layers");
            effectLayerLabel.AddToClassList("sectionLabel");
            effectLayerSettings.Add(effectLayerLabel);

            VisualElement effectLayersContainer = new VisualElement();
            effectLayerSettings.Add(effectLayersContainer);

            SerializedProperty layerArrayProp = serializedObject.FindProperty("layers");
            layerFoldoutStates = new List<bool>();
            for (int i = 0; i < layerArrayProp.arraySize; i++)
                layerFoldoutStates.Add(false);

            void DrawLayers()
            {
                effectLayersContainer.Clear();
                for (int i = 0; i < layerArrayProp.arraySize; i++)
                {
                    SerializedProperty layerProp = layerArrayProp.GetArrayElementAtIndex(i);

                    VisualElement layer = DrawLayer(i, layerProp, layerArrayProp, DrawLayers);
                    effectLayersContainer.Add(layer);
                }
            }

            DrawLayers();

            Button addButton = new Button();
            addButton.text = "Add Effect Layer";
            addButton.RegisterCallback<ClickEvent>(evt =>
            {
                GenericMenu menu = new GenericMenu();

                foreach (LumenEffectLayerPresets.LayerPreset preset in LumenEffectLayerPresets.layerPresets)
                {
                    menu.AddItem(new GUIContent(preset.name), false, () =>
                    {
                        targetEffect = (LumenEffectProfile)target;
                        targetEffect.layers.Add(preset.layer);
                        layerFoldoutStates.Add(true);
                        serializedObject.Update();
                        DrawLayers();
                        RefreshEffect();
                    });
                }
                menu.ShowAsContext();
            });
            effectLayerSettings.Add(addButton);

            return root;
        }

        public VisualElement DrawInlineEditor(LumenEffectPlayer player)
        {
            attachedPlayer = player;

            CreateInspectorGUI();

            return root;
        }

        public VisualElement DrawLayer(int index, SerializedProperty property, SerializedProperty array, Action action)
        {

            string type = GetLayerType(index);

            VisualElement container = new VisualElement();
            container.viewDataKey = $"layerFoldout_{property.propertyPath}";
            bool open = layerFoldoutStates[index];

            VisualElement foldout = new VisualElement();
            foldout.AddToClassList("layerFoldout");

            VisualElement foldoutProperties = new VisualElement();
            foldoutProperties.enabledSelf = property.FindPropertyRelative("active").boolValue;
            foldoutProperties.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

            Toggle activeToggle = new Toggle();
            activeToggle.SetValueWithoutNotify(property.FindPropertyRelative("active").boolValue);
            foldout.Add(activeToggle);
            activeToggle.AddToClassList("activeToggle");
            activeToggle.RegisterValueChangedCallback(evt =>
            {
                property.FindPropertyRelative("active").boolValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
                foldoutProperties.enabledSelf = evt.newValue;
                attachedPlayer?.RedoEffect();
            });

            VisualElement icon = new VisualElement();
            icon.name = "icon";
            foldout.Add(icon);

            Label label = new Label();
            foldout.Add(label);

            LumenSettings.LinkedEffectType effectType = LumenSettings.LinkedEffectType.DynamicRay;

            switch (type)
            {
                case "StaticRayLayer":
                    foldout.AddToClassList("staticRay");
                    effectType = LumenSettings.LinkedEffectType.StaticRay;
                    label.text = "Static Ray";
                    break;
                case "DynamicRayLayer":
                    foldout.AddToClassList("dynamicRay");
                    effectType = LumenSettings.LinkedEffectType.DynamicRay;
                    label.text = "Dynamic Ray";
                    break;
                case "LumenLightLayer":
                    foldout.AddToClassList("light");
                    effectType = LumenSettings.LinkedEffectType.Light;
                    label.text = "Light";
                    break;
                case "LumenFlareLayer":
                    foldout.AddToClassList("flare");
                    effectType = LumenSettings.LinkedEffectType.Flare;
                    label.text = "Flare";
                    break;
                case "EffectStackLayer":
                    foldout.AddToClassList("effectStack");
                    effectType = LumenSettings.LinkedEffectType.EffectStack;
                    label.text = "Effect Stack";
                    break;
            }

            string meshName = "";

            if (property.FindPropertyRelative("mesh").objectReferenceValue != null && type != "EffectStackLayer")
            {
                meshName = LumenSettings.Global.GetNameForMesh(property.FindPropertyRelative("mesh").objectReferenceValue as Mesh);
                label.text = meshName;
            }

            if (type == "EffectStackLayer" && property.FindPropertyRelative("profile").objectReferenceValue)
            {
                label.text = property.FindPropertyRelative("profile").objectReferenceValue.name;
            }

            VisualElement split = new VisualElement();
            split.name = "split";
            foldout.Add(split);


            VisualElement warning = new VisualElement();
            foldout.Add(warning);
            warning.AddToClassList("toolbarBtn");
            warning.name = "warning";
            if (type != "EffectStackLayer")
                warning.tooltip = "Please assign a mesh for this effect layer";
            else
                warning.tooltip = "Please assign an effect stack to this layer";
            warning.style.display = (property.FindPropertyRelative("mesh").objectReferenceValue == null && type != "EffectStackLayer") || (type == "EffectStackLayer" && property.FindPropertyRelative("profile").objectReferenceValue == null) ? DisplayStyle.Flex : DisplayStyle.None;

            VisualElement handles = new VisualElement();
            if (attachedPlayer != null)
            {
                handles.AddToClassList("toolbarBtn");
                handles.name = "handles";
                handles.RegisterCallback<ClickEvent>(evt =>
                {
                    if (handleIndex == index)
                    {
                        handleIndex = -1;
                        return;
                    }
                    handleIndex = index;
                });
                foldout.Add(handles);
            }

            void OpenDocumentation()
            {
                Application.OpenURL($"https://distant-lands.gitbook.io/lumen-stylized-light-fx/layers/{type.ToLower()}");
            }

            VisualElement help = new VisualElement();
            help.AddToClassList("toolbarBtn");
            help.name = "help";
            help.RegisterCallback<ClickEvent>(evt =>
            {
                OpenDocumentation();
            });
            foldout.Add(help);

            VisualElement context = new VisualElement();
            context.AddToClassList("toolbarBtn");
            context.name = "context";

            void MoveElement(int newPos)
            {
                array.MoveArrayElement(index, newPos);
                serializedObject.ApplyModifiedProperties();
                bool oldOpen = layerFoldoutStates[index];
                layerFoldoutStates[index] = layerFoldoutStates[newPos];
                layerFoldoutStates[newPos] = oldOpen;
                action.Invoke();
            }

            context.RegisterCallback<ClickEvent>(evt =>
            {
                GenericMenu ctxMenu = new GenericMenu();
                ctxMenu.AddItem(new GUIContent("Move To Top"), false, () => { MoveElement(0); });
                ctxMenu.AddItem(new GUIContent("Move Up"), false, () => { MoveElement(Mathf.Max(index - 1, 0)); });
                ctxMenu.AddItem(new GUIContent("Move Down"), false, () => { MoveElement(Mathf.Min(index + 1, array.arraySize - 1)); });
                ctxMenu.AddItem(new GUIContent("Move To Bottom"), false, () => { MoveElement(array.arraySize - 1); });
                ctxMenu.AddSeparator("");
                ctxMenu.AddItem(new GUIContent("Collapse All"), false, () =>
                {
                    for (int i = 0; i < layerFoldoutStates.Count; i++)
                        layerFoldoutStates[i] = false;

                    action.Invoke();
                });
                ctxMenu.AddItem(new GUIContent("Expand All"), false, () =>
                {
                    for (int i = 0; i < layerFoldoutStates.Count; i++)
                        layerFoldoutStates[i] = true;

                    action.Invoke();
                });
                ctxMenu.AddSeparator("");
                ctxMenu.AddItem(new GUIContent("Duplicate Layer"), false, () =>
                {
                    targetEffect = (LumenEffectProfile)target;
                    layerFoldoutStates.Insert(index, layerFoldoutStates[index]);
                    targetEffect.layers.Insert(index, targetEffect.layers[index].ShallowCopy());
                    serializedObject.Update();
                    attachedPlayer?.RedoEffect();
                    action.Invoke();
                });
                ctxMenu.AddItem(new GUIContent("Remove Layer"), false, () =>
                {
                    array.DeleteArrayElementAtIndex(index);
                    serializedObject.ApplyModifiedProperties();
                    layerFoldoutStates.RemoveAt(index);
                    attachedPlayer?.RedoEffect();
                    action.Invoke();
                });
                ctxMenu.AddItem(new GUIContent("Reset Layer"), false, () =>
                {

                    attachedPlayer.RedoEffect();
                    action.Invoke();
                });
                ctxMenu.AddSeparator("");
                ctxMenu.AddItem(new GUIContent("Documentation"), false, OpenDocumentation);
                ctxMenu.AddSeparator("");
                ctxMenu.AddItem(new GUIContent("Remove All"), false, () =>
                {
                    array.ClearArray();
                    serializedObject.ApplyModifiedProperties();
                    layerFoldoutStates.Clear();
                    attachedPlayer?.RedoEffect();
                    action.Invoke();
                });

                ctxMenu.ShowAsContext();
            });
            foldout.Add(context);

            foldout.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target != context && evt.target != handles && evt.target != help && evt.target != activeToggle)
                    open = !open;

                layerFoldoutStates[index] = open;

                foldoutProperties.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            });
            container.Add(foldout);

            container.Add(foldoutProperties);

            PropertyField position = new PropertyField();
            position.BindProperty(property.FindPropertyRelative("position"));
            position.RegisterValueChangeCallback(evt =>
            {
                RefreshEffect();
            });
            foldoutProperties.Add(position);
            PropertyField rotation = new PropertyField();
            rotation.BindProperty(property.FindPropertyRelative("rotation"));
            rotation.RegisterValueChangeCallback(evt =>
            {
                RefreshEffect();
            });
            foldoutProperties.Add(rotation);
            PropertyField scale = new PropertyField();
            scale.BindProperty(property.FindPropertyRelative("scale"));
            scale.RegisterValueChangeCallback(evt =>
            {
                RefreshEffect();
            });
            scale.AddToClassList("verticalBreak");
            foldoutProperties.Add(scale);

            PropertyField brightness = new PropertyField();
            brightness.BindProperty(property.FindPropertyRelative("brightness"));
            brightness.RegisterValueChangeCallback(evt =>
            {
                RefreshEffect();
            });
            foldoutProperties.Add(brightness);
            PropertyField color = new PropertyField();
            color.BindProperty(property.FindPropertyRelative("color"));
            color.RegisterValueChangeCallback(evt =>
            {
                RefreshEffect();
            });
            foldoutProperties.Add(color);

            if (type != "EffectStackLayer")
            {
                DropdownField dropdown = new DropdownField();
                dropdown.label = "Mesh";
                dropdown.choices = global.lumenMeshesForLayerType(effectType).Select(x => x.displayName).ToList();
                dropdown.choices.Add("Add New...");
                dropdown.value = meshName;
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue == "Add New...")
                    {
                        Selection.activeObject = global;
                        return;
                    }
                    property.FindPropertyRelative("mesh").objectReferenceValue = global.GetMeshByName(evt.newValue);
                    serializedObject.ApplyModifiedProperties();
                    warning.style.display = property.FindPropertyRelative("mesh").objectReferenceValue == null ? DisplayStyle.Flex : DisplayStyle.None;
                    label.text = evt.newValue;
                    attachedPlayer?.RedoEffect();
                });
                dropdown.AddToClassList("verticalBreak");
                foldoutProperties.Add(dropdown);
            }


            switch (type)
            {
                case "StaticRayLayer":
                    foldoutProperties.Add(CreatePropertySection(property, "sceneDepthFade", "sceneDepthFadeStart", "sceneDepthFadeEnd"));
                    foldoutProperties.Add(CreatePropertySection(property, "objectDistanceFade", "objectDistanceFadeStart", "objectDistanceFadeEnd"));
                    foldoutProperties.Add(CreatePropertySection(property, "cameraDepthFade", "cameraDepthFadeStart", "cameraDepthFadeEnd"));
                    foldoutProperties.Add(CreatePropertySection(property, "cameraDistanceFade", "cameraDistanceFadeStart", "cameraDistanceFadeEnd"));
                    foldoutProperties.Add(CreatePropertySection(property, "angleBasedFade", "angleFadeStart", "angleFadeEnd"));
                    foldoutProperties.Add(CreatePropertySection(property, "posterize", "posterizationSteps"));
                    foldoutProperties.Add(CreatePropertySection(property, "noiseVariation", "noiseVariationScale", "noiseVariationSpeed", "noiseVariationColor", "useUniformVariation"));
                    foldoutProperties.Add(CreatePropertySection(property, "sineVariation", "sineVariationOffset", "sineVariationSpeed", "sineVariationColor"));
                    foldoutProperties.Add(DrawRepeatingSection(property));
                    break;
                case "DynamicRayLayer":
                    VisualElement sunSettings = new VisualElement();
                    sunSettings.AddToClassList("verticalBreak");
                    foldoutProperties.Add(sunSettings);

                    PropertyField rayLength = GetPropertyField(property, "rayLength");
                    PropertyField bidirectional = GetPropertyField(property, "bidirectional");
                    PropertyField angleOpacityEffect = GetPropertyField(property, "angleOpacityEffect");
                    PropertyField angleRaylengthEffect = GetPropertyField(property, "angleRaylengthEffect");

                    sunSettings.Add(rayLength);
                    sunSettings.Add(bidirectional);
                    sunSettings.Add(angleOpacityEffect);
                    sunSettings.Add(angleRaylengthEffect);

                    foldoutProperties.Add(CreatePropertySection(property, "sceneDepthFade", "sceneDepthFadeStart", "sceneDepthFadeEnd"));
                    foldoutProperties.Add(CreatePropertySection(property, "objectDistanceFade", "objectDistanceFadeStart", "objectDistanceFadeEnd"));
                    foldoutProperties.Add(CreatePropertySection(property, "cameraDepthFade", "cameraDepthFadeStart", "cameraDepthFadeEnd"));
                    foldoutProperties.Add(CreatePropertySection(property, "cameraDistanceFade", "cameraDistanceFadeStart", "cameraDistanceFadeEnd"));
                    foldoutProperties.Add(CreatePropertySection(property, "angleBasedFade", "angleFadeStart", "angleFadeEnd"));
                    foldoutProperties.Add(CreatePropertySection(property, "posterize", "posterizationSteps"));
                    foldoutProperties.Add(CreatePropertySection(property, "noiseVariation", "noiseVariationScale", "noiseVariationSpeed", "noiseVariationColor", "useUniformVariation"));
                    foldoutProperties.Add(CreatePropertySection(property, "sineVariation", "sineVariationOffset", "sineVariationSpeed", "sineVariationColor"));
                    foldoutProperties.Add(DrawRepeatingSection(property));

                    break;
                case "LumenLightLayer":
                    VisualElement rangeSettings = new VisualElement();
                    rangeSettings.AddToClassList("verticalBreak");
                    foldoutProperties.Add(rangeSettings);

                    PropertyField range = GetPropertyField(property, "range");
                    PropertyField intensity = GetPropertyField(property, "intensity");
                    PropertyField smoothness = GetPropertyField(property, "smoothness");

                    rangeSettings.Add(range);
                    rangeSettings.Add(intensity);
                    rangeSettings.Add(smoothness);

                    foldoutProperties.Add(CreatePropertySection(property, "isSpotlight", "minSpotlightAngle", "maxSpotlightAngle"));
                    foldoutProperties.Add(CreatePropertySection(property, "normalFade", "normalFadeOffset", "normalFadeMultiplier"));
                    foldoutProperties.Add(CreatePropertySection(property, "posterize", "posterizationSteps"));
                    foldoutProperties.Add(CreatePropertySection(property, "noiseVariation", "noiseVariationScale", "noiseVariationSpeed", "noiseVariationColor"));
                    foldoutProperties.Add(CreatePropertySection(property, "sineVariation", "sineVariationOffset", "sineVariationSpeed", "sineVariationColor"));
                    foldoutProperties.Add(CreatePropertySection(property, "fluctuation", "fluctuationSpeed", "fluctuationScale", "fluctuationAmount"));
                    foldoutProperties.Add(DrawRepeatingSection(property));

                    break;
                case "LumenFlareLayer":
                    foldoutProperties.Add(CreatePropertySection(property, "sceneDepthFade", "sceneDepthFadeStart", "sceneDepthFadeEnd"));
                    foldoutProperties.Add(CreatePropertySection(property, "objectDistanceFade", "objectDistanceFadeStart", "objectDistanceFadeEnd"));
                    foldoutProperties.Add(CreatePropertySection(property, "cameraDepthFade", "cameraDepthFadeStart", "cameraDepthFadeEnd"));
                    foldoutProperties.Add(CreatePropertySection(property, "cameraDistanceFade", "cameraDistanceFadeStart", "cameraDistanceFadeEnd"));
                    foldoutProperties.Add(CreatePropertySection(property, "posterize", "posterizationSteps"));
                    foldoutProperties.Add(CreatePropertySection(property, "noiseVariation", "noiseVariationScale", "noiseVariationSpeed", "noiseVariationColor"));
                    foldoutProperties.Add(CreatePropertySection(property, "sineVariation", "sineVariationOffset", "sineVariationSpeed", "sineVariationColor"));
                    foldoutProperties.Add(DrawRepeatingSection(property));
                    break;
                case "EffectStackLayer":
                    PropertyField profile = new PropertyField();
                    profile.BindProperty(property.FindPropertyRelative("profile"));
                    profile.RegisterValueChangeCallback(evt =>
                    {
                        warning.style.display = (property.FindPropertyRelative("mesh").objectReferenceValue == null && type != "EffectStackLayer") || (type == "EffectStackLayer" && property.FindPropertyRelative("profile").objectReferenceValue == null) ? DisplayStyle.Flex : DisplayStyle.None;
                        label.text = property.FindPropertyRelative("profile").objectReferenceValue ? property.FindPropertyRelative("profile").objectReferenceValue.name : "Effect Stack";
                        attachedPlayer?.RedoEffect();
                    });
                    foldoutProperties.Add(profile);
                    break;
            }

            return container;
        }

        string GetLayerType(int index)
        {
            LumenEffectProfile lumenEffect = (target as LumenEffectProfile);
            return lumenEffect.layers[index]?.GetType().Name;
        }

        public static void DrawHandles(LumenEffectPlayer player)
        {
            LumenEffectProfile targetEffect = player.profile;

            if (player == null || targetEffect == null || handleIndex == -1 || handleIndex >= targetEffect.layers.Count)
                return;


            var tr = player.transform;

            // Transform layer's local position to world space
            Vector3 position = tr.TransformPoint(targetEffect.layers[handleIndex].position * player.scale);

            // Combine rotations correctly
            Quaternion rotation = Quaternion.Euler(tr.eulerAngles) * Quaternion.Euler(targetEffect.layers[handleIndex].rotation);

            // Keep local scale
            Vector3 scale = targetEffect.layers[handleIndex].scale;

            // Show transform handle
            Handles.TransformHandle(ref position, ref rotation, ref scale);

            // Convert world position back to local layer space
            if (player.scale == 0)
                targetEffect.layers[handleIndex].position = tr.InverseTransformPoint(position) * 0;
            else
                targetEffect.layers[handleIndex].position = tr.InverseTransformPoint(position) / player.scale;

            // Convert rotation back to local relative rotation
            targetEffect.layers[handleIndex].rotation = (Quaternion.Inverse(Quaternion.Euler(tr.eulerAngles)) * rotation).eulerAngles;

            // Store updated scale
            targetEffect.layers[handleIndex].scale = scale;

        }

        void RefreshEffect()
        {
            if (refreshQueued) return;
            refreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                refreshQueued = false;
                attachedPlayer?.RedoEffect();
            };
        }

        PropertyField GetPropertyField(SerializedProperty property, string relative, params VisualElement[] elements)
        {
            PropertyField propertyField = new PropertyField();
            propertyField.BindProperty(property.FindPropertyRelative(relative));
            propertyField.RegisterValueChangeCallback(evt =>
            {
                RefreshEffect();
                foreach (VisualElement element in elements)
                {
                    element.style.display = property.FindPropertyRelative(relative).boolValue ? DisplayStyle.Flex : DisplayStyle.None;
                }
            });
            return propertyField;
        }

        private VisualElement CreatePropertySection(SerializedProperty property, string toggleName, params string[] fieldNames)
        {
            VisualElement container = new VisualElement();
            container.AddToClassList("verticalBreak");

            var fields = fieldNames.Select(name => GetPropertyField(property, name)).ToArray();
            container.Add(GetPropertyField(property, toggleName, fields));
            foreach (var field in fields)
                container.Add(field);

            return container;
        }

        private VisualElement DrawRepeatingSection(SerializedProperty property)
        {
            VisualElement container = new VisualElement();
            container.AddToClassList("verticalBreak");

            PropertyField repeat = new PropertyField();
            repeat.BindProperty(property.FindPropertyRelative("repeat"));
            container.Add(repeat);

            VisualElement repeatSettings = new VisualElement();
            repeatSettings.style.display = property.FindPropertyRelative("repeat").boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            container.Add(repeatSettings);

            repeat.RegisterValueChangeCallback(evt =>
            {
                RefreshEffect();
                repeatSettings.style.display = property.FindPropertyRelative("repeat").boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            EventCallback<SerializedPropertyChangeEvent> UpdateEffect = evt =>
            {
                RefreshEffect();
            };

            PropertyField repeatCount = new PropertyField();
            repeatCount.BindProperty(property.FindPropertyRelative("repeatCount"));
            repeatCount.RegisterValueChangeCallback(UpdateEffect);
            repeatSettings.Add(repeatCount);

            PropertyField repeatDistribution = new PropertyField();
            repeatDistribution.BindProperty(property.FindPropertyRelative("repeatDistribution"));
            repeatSettings.Add(repeatDistribution);

            PropertyField repeatCurve = new PropertyField();
            repeatCurve.BindProperty(property.FindPropertyRelative("repeatCurve"));
            repeatCurve.RegisterValueChangeCallback(UpdateEffect);
            repeatSettings.Add(repeatCurve);

            repeatDistribution.RegisterValueChangeCallback(evt =>
            {
                RefreshEffect();
                repeatCurve.style.display = property.FindPropertyRelative("repeatDistribution").enumValueIndex == (int)LumenEffectLayer.RepeatDistributionStyle.Curve ? DisplayStyle.Flex : DisplayStyle.None;
            });

            PropertyField repeatPositionSpread = new PropertyField();
            repeatPositionSpread.BindProperty(property.FindPropertyRelative("repeatPositionSpread"));
            repeatPositionSpread.RegisterValueChangeCallback(UpdateEffect);
            repeatSettings.Add(repeatPositionSpread);

            PropertyField repeatRotationSpread = new PropertyField();
            repeatRotationSpread.BindProperty(property.FindPropertyRelative("repeatRotationSpread"));
            repeatRotationSpread.RegisterValueChangeCallback(UpdateEffect);
            repeatSettings.Add(repeatRotationSpread);

            PropertyField repeatScaleSpread = new PropertyField();
            repeatScaleSpread.BindProperty(property.FindPropertyRelative("repeatScaleSpread"));
            repeatScaleSpread.RegisterValueChangeCallback(UpdateEffect);
            repeatSettings.Add(repeatScaleSpread);

            PropertyField repeatBrightnessSpread = new PropertyField();
            repeatBrightnessSpread.BindProperty(property.FindPropertyRelative("repeatBrightnessSpread"));
            repeatBrightnessSpread.RegisterValueChangeCallback(UpdateEffect);
            repeatSettings.Add(repeatBrightnessSpread);

            PropertyField repeatColors = new PropertyField();
            repeatColors.BindProperty(property.FindPropertyRelative("repeatColors"));
            repeatColors.RegisterValueChangeCallback(UpdateEffect);
            repeatSettings.Add(repeatColors);

            PropertyField repeatSeed = new PropertyField();
            repeatSeed.BindProperty(property.FindPropertyRelative("repeatSeed"));
            repeatSeed.RegisterValueChangeCallback(UpdateEffect);
            repeatSettings.Add(repeatSeed);

            PropertyField repeatVariation = new PropertyField();
            repeatVariation.BindProperty(property.FindPropertyRelative("repeatVariation"));

            VisualElement variationContainer = new VisualElement();
            variationContainer.style.display = property.FindPropertyRelative("repeatVariation").boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            variationContainer.style.display = property.FindPropertyRelative("repeatVariation").boolValue ? DisplayStyle.Flex : DisplayStyle.None;

            repeatVariation.RegisterValueChangeCallback(evt =>
            {
                RefreshEffect();
                variationContainer.style.display = property.FindPropertyRelative("repeatVariation").boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            repeatSettings.Add(repeatVariation);
            repeatSettings.Add(variationContainer);

            PropertyField repeatPositionVariation = new PropertyField();
            repeatPositionVariation.BindProperty(property.FindPropertyRelative("repeatPositionVariation"));
            repeatPositionVariation.RegisterValueChangeCallback(UpdateEffect);
            variationContainer.Add(repeatPositionVariation);

            PropertyField repeatRotationVariation = new PropertyField();
            repeatRotationVariation.BindProperty(property.FindPropertyRelative("repeatRotationVariation"));
            repeatRotationVariation.RegisterValueChangeCallback(UpdateEffect);
            variationContainer.Add(repeatRotationVariation);

            PropertyField repeatScaleVariation = new PropertyField();
            repeatScaleVariation.BindProperty(property.FindPropertyRelative("repeatScaleVariation"));
            repeatScaleVariation.RegisterValueChangeCallback(UpdateEffect);
            variationContainer.Add(repeatScaleVariation);


            return container;
        }
    }
}
