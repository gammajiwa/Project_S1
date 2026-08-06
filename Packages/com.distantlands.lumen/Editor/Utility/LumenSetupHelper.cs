using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace DistantLands.Lumen.EditorScripts
{
    public class LumenSetupHelper : EditorWindow
    {
        private const string LumenSetupShownKey = "DistantLands.Lumen.SetupHelper.Shown";
        private VisualElement checksRoot;

        [MenuItem("Window/Lumen Setup")]
        public static void ShowWindow()
        {
            GetWindow<LumenSetupHelper>("Lumen Setup");
        }

        [InitializeOnLoadMethod]
        private static void ShowOnFirstInstall()
        {
            if (!EditorPrefs.GetBool(LumenSetupShownKey, false))
            {
                ShowWindow();
                EditorPrefs.SetBool(LumenSetupShownKey, true);
            }
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.distantlands.lumen/Editor/USS/Lumen.uss"
            ));

            root.Clear();

            VisualElement banner = new VisualElement();
            banner.AddToClassList("banner");
            root.Add(banner);

            ScrollView scrollView = new ScrollView();
            scrollView.AddToClassList("checksContainer");
            root.Add(scrollView);

            var label = new Label("Thank you for installing Lumen");
            label.AddToClassList("sectionLabel");
            scrollView.Add(label);

            var description = new Label("This window will help you set up Lumen in your project. Please follow the checks below to ensure everything is configured correctly.");
            description.AddToClassList("description");
            scrollView.Add(description);


            var setupLabel = new Label("Project Setup");
            setupLabel.AddToClassList("sectionLabel");
            scrollView.Add(setupLabel);
            checksRoot = new VisualElement();
            scrollView.Add(checksRoot);
            DrawChecks();

            VisualElement links = new VisualElement();
            links.AddToClassList("externalLinks");
            root.Add(links);

            Button documentationButton = new Button(() => Application.OpenURL("https://distant-lands.gitbook.io/lumen-stylized-light-fx")) { tooltip = "Documentation" };
            documentationButton.AddToClassList("documentation");
            links.Add(documentationButton);

            Button discordButton = new Button(() => Application.OpenURL("https://discord.gg/HWGU4hj")) { tooltip = "Discord" };
            discordButton.AddToClassList("discord");
            links.Add(discordButton);

            Button emailButton = new Button(() => Application.OpenURL("mailto:contact@distantlands.space")) { tooltip = "Email" };
            emailButton.AddToClassList("email");
            links.Add(emailButton);

            Button websiteButton = new Button(() => Application.OpenURL("https://distantlands.space")) { tooltip = "Website" };
            websiteButton.AddToClassList("website");
            links.Add(websiteButton);

            Button githubButton = new Button(() => Application.OpenURL("https://github.com/DistantLandsProductions/com.distantlands.lumen")) { tooltip = "GitHub" };
            githubButton.AddToClassList("github");
            links.Add(githubButton);

        }

        private void DrawChecks()
        {
            checksRoot.Clear();

            string version = File.ReadAllLines("ProjectSettings/ProjectVersion.txt")[0].Split(':')[1].Trim();
            bool unity6 = version.StartsWith("6");
            AddCheck("Unity version is 6 or higher", unity6);

            bool optimizeMeshOff = !PlayerSettings.stripUnusedMeshComponents;
            AddCheck("Optimize mesh data is off", optimizeMeshOff, () => { PlayerSettings.stripUnusedMeshComponents = false; DrawChecks(); });

            bool checkDepthTexture = true;
            bool checkDownsampling = true;
            bool checkHDR = true;
            bool checkOpaqueTexture = true;


            for (int i = 0; i < GraphicsSettings.allConfiguredRenderPipelines.Length; i++)
            {
                if (GraphicsSettings.allConfiguredRenderPipelines[i].GetType() != typeof(UniversalRenderPipelineAsset)) continue;

                UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.allConfiguredRenderPipelines[i];

                if (pipeline.supportsCameraDepthTexture == false)
                {
                    checkDepthTexture = false;
                }
                if (pipeline.opaqueDownsampling != Downsampling.None)
                {
                    checkDownsampling = false;
                }
                if (pipeline.supportsHDR == false)
                {
                    checkHDR = false;
                }
                if (pipeline.supportsCameraOpaqueTexture == false)
                {
                    checkOpaqueTexture = false;
                }
            }


            AddCheck("Depth texture is on", checkDepthTexture, () =>
            {
                for (int i = 0; i < GraphicsSettings.allConfiguredRenderPipelines.Length; i++)
                {
                    if (GraphicsSettings.allConfiguredRenderPipelines[i].GetType() != typeof(UniversalRenderPipelineAsset)) continue;
                    UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.allConfiguredRenderPipelines[i];
                    pipeline.supportsCameraDepthTexture = true;
                    EditorUtility.SetDirty(pipeline);
                }
                DrawChecks();
            });

            AddCheck("Opaque texture is on", checkOpaqueTexture, () =>
            {
                for (int i = 0; i < GraphicsSettings.allConfiguredRenderPipelines.Length; i++)
                {
                    if (GraphicsSettings.allConfiguredRenderPipelines[i].GetType() != typeof(UniversalRenderPipelineAsset)) continue;
                    UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.allConfiguredRenderPipelines[i];
                    pipeline.supportsCameraOpaqueTexture = true;
                    EditorUtility.SetDirty(pipeline);
                }
                DrawChecks();
            });

            AddCheck("Opaque downsampling is set to none", checkDownsampling, () =>
            {
                for (int i = 0; i < GraphicsSettings.allConfiguredRenderPipelines.Length; i++)
                {
                    if (GraphicsSettings.allConfiguredRenderPipelines[i].GetType() != typeof(UniversalRenderPipelineAsset))
                        continue;

                    UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.allConfiguredRenderPipelines[i];
                    Type t = typeof(UniversalRenderPipelineAsset);
                    FieldInfo fieldInfo = t.GetField("m_OpaqueDownsampling", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (fieldInfo != null)
                    {
                        fieldInfo.SetValue(pipeline, Downsampling.None);
                    }
                    else
                    {
                        Debug.LogError("Field 'm_OpaqueDownsampling' not found!");
                    }
                    EditorUtility.SetDirty(pipeline);
                }
                DrawChecks();
            });

            AddCheck("HDR is supported", checkHDR, () =>
            {
                for (int i = 0; i < GraphicsSettings.allConfiguredRenderPipelines.Length; i++)
                {
                    if (GraphicsSettings.allConfiguredRenderPipelines[i].GetType() != typeof(UniversalRenderPipelineAsset)) continue;
                    UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.allConfiguredRenderPipelines[i];
                    pipeline.supportsHDR = true;
                    EditorUtility.SetDirty(pipeline);
                }
                DrawChecks();
            });

            string[] shaderPaths = new[] {
                "Packages/com.distantlands.lumen/Runtime/Rendering/Shaders/Lumen Fake Light.shader",
                "Packages/com.distantlands.lumen/Runtime/Rendering/Shaders/Lumen Flare.shader",
                "Packages/com.distantlands.lumen/Runtime/Rendering/Shaders/Lumen Light Ray.shader"
            };
            bool allShadersOk = shaderPaths.All(path =>
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                return shader != null && !ShaderUtil.ShaderHasError(shader);
            });
            AddCheck("Shaders compile without errors", allShadersOk);

            bool colorSpaceLinear = PlayerSettings.colorSpace == ColorSpace.Linear;
            AddCheck("Color space is set to linear", colorSpaceLinear, () => { PlayerSettings.colorSpace = ColorSpace.Linear; DrawChecks(); });
        }

        private void AddCheck(string label, bool passed, System.Action fixAction)
        {
            var row = new VisualElement();
            row.AddToClassList("check-row");
            row.AddToClassList(passed ? "passed" : "failed");

            var icon = new VisualElement();
            icon.AddToClassList("icon");

            row.Add(icon);
            row.Add(new Label(label));
            var btn = new Button(() => fixAction.Invoke()) { text = "Fix" };
            btn.style.display = DisplayStyle.None;
            btn.style.marginLeft = 8;
            row.Add(btn);
            if (!passed && fixAction != null)
            {
                btn.style.display = DisplayStyle.Flex;
            }
            checksRoot.Add(row);
        }
        private void AddCheck(string label, bool passed)
        {
            var row = new VisualElement();
            row.AddToClassList("check-row");
            row.AddToClassList(passed ? "passed" : "failed");

            var icon = new VisualElement();
            icon.AddToClassList("icon");

            row.Add(icon);
            row.Add(new Label(label));
            checksRoot.Add(row);
        }
    }
}
