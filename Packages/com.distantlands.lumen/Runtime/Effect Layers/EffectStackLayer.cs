// Distant Lands 2025
// Lumen: Stylized Light FX 2
// The contents of this file are protected under the Unity Asset Store EULA
//
// Represents a Lumen effect layer that allows stacking multiple prebuilt Lumen effect profiles as a single layer for advanced light FX composition in Unity.

using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Lumen
{
    /// <summary>
    /// Represents a Lumen effect layer that allows stacking multiple prebuilt Lumen effect profiles as a single layer for advanced light FX composition in Unity.
    /// </summary>
    [System.Serializable]
    public class EffectStackLayer : LumenEffectLayer
    {
        /// <summary>
        /// Prebuilt Lumen effect stack saved as a profile.
        /// </summary>
        [Tooltip("Prebuilt Lumen effect stack saved as a profile.")]
        public LumenEffectProfile profile;

        public override Material EffectMaterial => (Material)Resources.Load("Materials/Lumen Light");

        public override void DrawLayer(MaterialPropertyBlock propertyBlock, Renderer renderer)
        {

        }

    }

}