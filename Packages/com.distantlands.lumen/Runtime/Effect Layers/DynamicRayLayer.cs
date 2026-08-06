// Distant Lands 2025
// Lumen: Stylized Light FX 2
// The contents of this file are protected under the Unity Asset Store EULA
//
// Implements a dynamic ray effect layer for animated light rays in the Lumen system.

using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Lumen
{
    /// <summary>
    /// Implements a dynamic ray effect layer for animated light rays in the Lumen system.
    /// </summary>
    [System.Serializable]
    public class DynamicRayLayer : StaticRayLayer
    {
        /// <summary>
        /// Maximum distance rays will travel from this effect layer.
        /// </summary>
        [Tooltip("The maximum distance each ray will travel.")]
        public float rayLength = 15;

        /// <summary>
        /// Whether rays are cast both forward and backward from the mesh surface.
        /// </summary>
        [Tooltip("Enable to cast rays in both directions from the surface.")]
        public bool bidirectional = false;

        /// <summary>
        /// Controls how much angle between the sun direction and the dynamic ray's forward vector affects opacity.
        /// </summary>
        [Tooltip("How strongly the angle between the sun direction and the dynamic ray's forward vector influences opacity.")]
        [Range(0, 1)]
        public float angleOpacityEffect = 0.5f;

        /// <summary>
        /// Controls how much angle between the sun direction and the dynamic ray's forward vector affects ray length.
        /// </summary>
        [Tooltip("How strongly the angle between the sun direction and the dynamic ray's forward vector influences ray length.")]
        [Range(0, 1)]
        public float angleRaylengthEffect = 0.5f;

        /// <summary>
        /// Automatically detects and assigns the primary directional light (sun).
        /// </summary>
        [Tooltip("Automatically assign the primary directional light as the sun.")]
        public bool autoAssignSun = true;

        /// <summary>
        /// Whether to use the custom Lumen sun script to drive direction and intensity.
        /// </summary>
        [Tooltip("Enable to use a custom script to control the Lumen sun behavior.")]
        public bool useLumenSunScript;

        /// <summary>
        /// The world-space direction of the simulated sun.
        /// </summary>
        [Tooltip("Direction vector representing the sun's position in world space.")]
        public Vector3 sunDirection = new Vector3(1, -1, 1);

        public override void DrawLayer(MaterialPropertyBlock propertyBlock, Renderer renderer)
        {

            SkinnedMeshRenderer skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
            propertyBlock.SetFloat(LumenShaderIDs.Style, 1);
            propertyBlock.SetFloat(LumenShaderIDs.Bidirectional, bidirectional ? 1f : 0f);
            propertyBlock.SetFloat(LumenShaderIDs.RayLength, rayLength);
            propertyBlock.SetFloat(LumenShaderIDs.AngleOpacityEffect, angleOpacityEffect);
            propertyBlock.SetFloat(LumenShaderIDs.AngleRaylengthEffect, angleRaylengthEffect);

            base.DrawLayer(propertyBlock, renderer);
        }

    }

}