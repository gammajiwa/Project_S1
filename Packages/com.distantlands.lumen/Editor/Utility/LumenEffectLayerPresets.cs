// Distant Lands 2025
// Lumen: Stylized Light FX 2
// The contents of this file are protected under the Unity Asset Store EULA
//
// Provides a set of presets for different Lumen effect layers for quick configuration in the Unity Editor.

using System.Collections.Generic;

namespace DistantLands.Lumen.EditorScripts
{
    public static class LumenEffectLayerPresets
    {
        public struct LayerPreset
        {
            public string name;
            public LumenEffectLayer layer;
        }
        public static List<LayerPreset> layerPresets
        {
            get
            {
                List<LayerPreset> presets = new List<LayerPreset>()
                {
                    new LayerPreset() { name = "Flare", layer = new LumenFlareLayer() { }},
                    new LayerPreset() { name = "Static Ray", layer = new StaticRayLayer() { }},
                    new LayerPreset() { name = "Dynamic Ray", layer = new DynamicRayLayer() { }},
                    new LayerPreset() { name = "Light", layer = new LumenLightLayer() { }},
                    new LayerPreset() { name = "Effect Stack", layer = new EffectStackLayer() { }},
                };

                return presets;
            }
        }

    }
}