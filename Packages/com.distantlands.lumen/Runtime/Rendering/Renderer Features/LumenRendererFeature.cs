// Distant Lands 2025
// Lumen: Stylized Light FX 2
// The contents of this file are protected under the Unity Asset Store EULA
//
// Adds a custom renderer feature for integrating Lumen effects into the Unity rendering pipeline.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace DistantLands.Lumen
{
    public class LumenRendererFeature : ScriptableRendererFeature
    {
        class LumenRenderPass : ScriptableRenderPass
        {
            public void Setup()
            {
                ConfigureInput(ScriptableRenderPassInput.Normal);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {

            }
        }

        LumenRenderPass m_ScriptablePass;

        public override void Create()
        {
            m_ScriptablePass = new LumenRenderPass();
            m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            m_ScriptablePass.Setup();
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }
}