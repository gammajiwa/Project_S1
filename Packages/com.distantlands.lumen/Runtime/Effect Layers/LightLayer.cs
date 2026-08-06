// Distant Lands 2025
// Lumen: Stylized Light FX 2
// The contents of this file are protected under the Unity Asset Store EULA
//
// Implements a fake light effect layer for lighting up the environment in the Lumen system.

using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Lumen
{
    /// <summary>
    /// Implements a fake light effect layer for lighting up the environment in the Lumen system.
    /// </summary>
    [System.Serializable]
    public class LumenLightLayer : LumenEffectLayer
    {
        /// <summary>
        /// The effective range of the light.
        /// </summary>
        [Tooltip("The effective range of the light.")]
        public float range = 10;

        /// <summary>
        /// The intensity of the light effect.
        /// </summary>
        [Tooltip("The intensity of the light effect.")]
        public float intensity = 1;

        /// <summary>
        /// The softness of the light's edges.
        /// </summary>
        [Tooltip("The softness of the light's edges.")]
        [Range(0, 5)]
        public float smoothness = 1;

        /// <summary>
        /// Whether this light behaves as a spotlight.
        /// </summary>
        [Tooltip("Enable to make this light behave like a spotlight.")]
        public bool isSpotlight;

        /// <summary>
        /// The minimum angle for the spotlight cone.
        /// </summary>
        [Tooltip("Minimum angle for the spotlight cone.")]
        public float minSpotlightAngle = 30;

        /// <summary>
        /// The maximum angle for the spotlight cone.
        /// </summary>
        [Tooltip("Maximum angle for the spotlight cone.")]
        public float maxSpotlightAngle = 35;

        /// <summary>
        /// Enables fading based on surface normals.
        /// </summary>
        [Tooltip("Enable fading based on how the surface normal aligns with the view direction.")]
        public bool normalFade;

        /// <summary>
        /// Offset added to the normal fade calculation.
        /// </summary>
        [Tooltip("Offset applied to the normal fade value.")]
        public float normalFadeOffset;

        /// <summary>
        /// Multiplier applied to the normal fade intensity.
        /// </summary>
        [Tooltip("Multiplier that scales the effect of normal fading.")]
        public float normalFadeMultiplier = 1;

        /// <summary>
        /// Enables light intensity fluctuation.
        /// </summary>
        [Tooltip("Enable fluctuating light intensity for a more dynamic effect.")]
        public bool fluctuation;

        /// <summary>
        /// Speed of the fluctuation animation.
        /// </summary>
        [Tooltip("Speed of light intensity fluctuation.")]
        public float fluctuationSpeed = 1;

        /// <summary>
        /// Scale of the fluctuation over time.
        /// </summary>
        [Tooltip("Scale of the fluctuation effect over time.")]
        public float fluctuationScale = 1;

        /// <summary>
        /// Maximum deviation in intensity due to fluctuation.
        /// </summary>
        [Tooltip("Maximum change in intensity caused by fluctuation.")]
        public float fluctuationAmount = 0.5f;


        public override Material EffectMaterial => (Material)Resources.Load("Materials/Lumen Light");

        public override void DrawLayer(MaterialPropertyBlock propertyBlock, Renderer renderer)
        {
            propertyBlock.SetFloat(LumenShaderIDs.Intensity, intensity);
            propertyBlock.SetFloat(LumenShaderIDs.Smoothness, smoothness);

            propertyBlock.SetFloat(LumenShaderIDs.IsSpotlight, isSpotlight ? 1f : 0f);
            propertyBlock.SetFloat(LumenShaderIDs.MinSpotlightAngle, minSpotlightAngle);
            propertyBlock.SetFloat(LumenShaderIDs.MaxSpotlightAngle, maxSpotlightAngle);

            propertyBlock.SetFloat(LumenShaderIDs.NormalFade, normalFade ? 1f : 0f);
            propertyBlock.SetFloat(LumenShaderIDs.NormalFadeOffset, normalFadeOffset);
            propertyBlock.SetFloat(LumenShaderIDs.NormalFadeMultiplier, normalFadeMultiplier);

            propertyBlock.SetFloat(LumenShaderIDs.Posterize, posterize ? 1f : 0f);
            propertyBlock.SetInt(LumenShaderIDs.PosterizationSteps, posterizationSteps);

            propertyBlock.SetFloat(LumenShaderIDs.Fluctuation, fluctuation ? 1f : 0f);
            propertyBlock.SetFloat(LumenShaderIDs.FluctuationSpeed, fluctuationSpeed);
            propertyBlock.SetFloat(LumenShaderIDs.FluctuationScale, fluctuationScale);
            propertyBlock.SetFloat(LumenShaderIDs.FluctuationAmount, fluctuationAmount);

            propertyBlock.SetFloat(LumenShaderIDs.UseNoiseVariation, noiseVariation == true ? 1 : 0);
            propertyBlock.SetFloat(LumenShaderIDs.NoiseVariationScale, noiseVariationScale);
            propertyBlock.SetFloat(LumenShaderIDs.NoiseVariationSpeed, noiseVariationSpeed);
            propertyBlock.SetColor(LumenShaderIDs.NoiseVariationColor, noiseVariationColor);
            propertyBlock.SetFloat(LumenShaderIDs.UseUniformVariation, useUniformVariation == true ? 1 : 0);
            propertyBlock.SetFloat(LumenShaderIDs.UseSineVariation, sineVariation == true ? 1 : 0);
            propertyBlock.SetFloat(LumenShaderIDs.SineOffset, sineVariationOffset);
            propertyBlock.SetFloat(LumenShaderIDs.SineVariationSpeed, sineVariationSpeed);
            propertyBlock.SetColor(LumenShaderIDs.SineVariationColor, sineVariationColor);
        }

    }

}