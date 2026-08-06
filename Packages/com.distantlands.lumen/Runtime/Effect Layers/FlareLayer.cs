// Distant Lands 2025
// Lumen: Stylized Light FX 2
// The contents of this file are protected under the Unity Asset Store EULA
//
// Implements a flare effect layer for lens flare and glow effects in the Lumen system.

using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Lumen
{
    /// <summary>
    /// Implements a flare effect layer for lens flare and glow effects in the Lumen system.
    /// </summary>
    [System.Serializable]
    public class LumenFlareLayer : LumenEffectLayer
    {
        public override Material EffectMaterial => (Material)Resources.Load("Materials/Lumen Flare");
        /// <summary>
        /// Enables fading based on scene depth.
        /// </summary>
        [Tooltip("Enable fading based on the distance from scene geometry behind the effect.")]
        public bool sceneDepthFade;

        /// <summary>
        /// The depth at which scene-based fading starts.
        /// </summary>
        [Tooltip("Scene depth distance where fading begins.")]
        public float sceneDepthFadeStart;

        /// <summary>
        /// The depth at which scene-based fading ends.
        /// </summary>
        [Tooltip("Scene depth distance where fading ends.")]
        public float sceneDepthFadeEnd;

        /// <summary>
        /// Enables fading based on the camera's depth buffer.
        /// </summary>
        [Tooltip("Enable fading based on how close objects are in the camera's depth buffer.")]
        public bool cameraDepthFade;

        /// <summary>
        /// The camera depth where fading starts.
        /// </summary>
        [Tooltip("Camera depth where fading begins.")]
        public float cameraDepthFadeStart;

        /// <summary>
        /// The camera depth where fading ends.
        /// </summary>
        [Tooltip("Camera depth where fading ends.")]
        public float cameraDepthFadeEnd;

        /// <summary>
        /// Enables fading based on distance from the camera.
        /// </summary>
        [Tooltip("Enable fading based on distance from the camera.")]
        public bool cameraDistanceFade;

        /// <summary>
        /// The distance from the camera where fading starts.
        /// </summary>
        [Tooltip("Camera distance where fading begins.")]
        public float cameraDistanceFadeStart;

        /// <summary>
        /// The distance from the camera where fading ends.
        /// </summary>
        [Tooltip("Camera distance where fading ends.")]
        public float cameraDistanceFadeEnd;

        /// <summary>
        /// Enables fading based on distance from the object itself.
        /// </summary>
        [Tooltip("Enable fading based on proximity to the object.")]
        public bool objectDistanceFade;

        /// <summary>
        /// The object-relative distance where fading starts.
        /// </summary>
        [Tooltip("Distance from the object where fading begins.")]
        public float objectDistanceFadeStart;

        /// <summary>
        /// The object-relative distance where fading ends.
        /// </summary>
        [Tooltip("Distance from the object where fading ends.")]
        public float objectDistanceFadeEnd;



        public override void DrawLayer(MaterialPropertyBlock propertyBlock, Renderer renderer)
        {
            propertyBlock.SetFloat(LumenShaderIDs.UseSceneDepthFade, sceneDepthFade == true ? 1 : 0);
            propertyBlock.SetFloat(LumenShaderIDs.DepthFadeStartDistance, sceneDepthFadeStart);
            propertyBlock.SetFloat(LumenShaderIDs.DepthFadeEndDistance, sceneDepthFadeEnd);
            propertyBlock.SetFloat(LumenShaderIDs.UseCameraDepthFade, cameraDepthFade == true ? 1 : 0);
            propertyBlock.SetFloat(LumenShaderIDs.CameraDepthFadeStart, cameraDepthFadeStart);
            propertyBlock.SetFloat(LumenShaderIDs.CameraDepthFadeEnd, cameraDepthFadeEnd);
            propertyBlock.SetFloat(LumenShaderIDs.UseCameraDistanceFade, cameraDistanceFade == true ? 1 : 0);
            propertyBlock.SetFloat(LumenShaderIDs.CameraDistanceFadeStart, cameraDistanceFadeStart);
            propertyBlock.SetFloat(LumenShaderIDs.CameraDistanceFadeEnd, cameraDistanceFadeEnd);
            propertyBlock.SetFloat(LumenShaderIDs.UseObjectDistanceFade, objectDistanceFade == true ? 1 : 0);
            propertyBlock.SetFloat(LumenShaderIDs.ObjectDistanceFadeStart, objectDistanceFadeStart);
            propertyBlock.SetFloat(LumenShaderIDs.ObjectDistanceFadeEnd, objectDistanceFadeEnd);
            propertyBlock.SetFloat(LumenShaderIDs.Posterize, posterize ? 1f : 0f);
            propertyBlock.SetInt(LumenShaderIDs.PosterizationSteps, posterizationSteps);
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