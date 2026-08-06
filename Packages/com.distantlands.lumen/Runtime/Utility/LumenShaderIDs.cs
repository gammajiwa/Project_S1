// Distant Lands 2025
// Lumen: Stylized Light FX 2
// The contents of this file are protected under the Unity Asset Store EULA
//
// <summary>
// Provides shader property IDs for efficient access to Lumen shader variables.
// </summary>
// <remarks>
// This static class caches shader property IDs for all Lumen-related shader variables, improving performance by avoiding repeated string lookups.
// </remarks>
//
// <copyright>
// Copyright (c) Distant Lands. All rights reserved. Licensed under Unity Asset Store EULA.
// </copyright>

using UnityEngine;

namespace DistantLands.Lumen
{
    /// <summary>
    /// Provides cached shader property IDs for all Lumen-related shader variables.
    /// Use these properties to avoid repeated string lookups when setting shader values.
    /// </summary>
    public static class LumenShaderIDs
    {
        /// <summary>
        /// Shader property ID for the _Color property.
        /// </summary>
        static int _ColorID;
        /// <summary>
        /// Gets the shader property ID for _Color.
        /// </summary>
        public static int Color
        {
            get
            {
                if (_ColorID == 0)
                    _ColorID = Shader.PropertyToID("_Color");
                return _ColorID;
            }
        }
        /// <summary>
        /// Shader property ID for the _CameraDistanceFadeStart property.
        /// </summary>
        static int _CameraDistanceFadeStartID;
        /// <summary>
        /// Gets the shader property ID for _CameraDistanceFadeStart.
        /// </summary>
        public static int CameraDistanceFadeStart
        {
            get
            {
                if (_CameraDistanceFadeStartID == 0)
                    _CameraDistanceFadeStartID = Shader.PropertyToID("_CameraDistanceFadeStart");
                return _CameraDistanceFadeStartID;
            }
        }
        /// <summary>
        /// Shader property ID for the _CameraDistanceFadeEnd property.
        /// </summary>
        static int _CameraDistanceFadeEndID;
        /// <summary>
        /// Gets the shader property ID for _CameraDistanceFadeEnd.
        /// </summary>
        public static int CameraDistanceFadeEnd
        {
            get
            {
                if (_CameraDistanceFadeEndID == 0)
                    _CameraDistanceFadeEndID = Shader.PropertyToID("_CameraDistanceFadeEnd");
                return _CameraDistanceFadeEndID;
            }
        }
        /// <summary>
        /// Shader property ID for the _ObjectDistanceFadeStart property.
        /// </summary>
        static int _ObjectDistanceFadeStartID;
        /// <summary>
        /// Gets the shader property ID for _ObjectDistanceFadeStart.
        /// </summary>
        public static int ObjectDistanceFadeStart
        {
            get
            {
                if (_ObjectDistanceFadeStartID == 0)
                    _ObjectDistanceFadeStartID = Shader.PropertyToID("_ObjectDistanceFadeStart");
                return _ObjectDistanceFadeStartID;
            }
        }
        /// <summary>
        /// Shader property ID for the _ObjectDistanceFadeEnd property.
        /// </summary>
        static int _ObjectDistanceFadeEndID;
        /// <summary>
        /// Gets the shader property ID for _ObjectDistanceFadeEnd.
        /// </summary>
        public static int ObjectDistanceFadeEnd
        {
            get
            {
                if (_ObjectDistanceFadeEndID == 0)
                    _ObjectDistanceFadeEndID = Shader.PropertyToID("_ObjectDistanceFadeEnd");
                return _ObjectDistanceFadeEndID;
            }
        }
        /// <summary>
        /// Shader property ID for the _UseObjectDistanceFade property.
        /// </summary>
        static int _UseObjectDistanceFadeID;
        /// <summary>
        /// Gets the shader property ID for _UseObjectDistanceFade.
        /// </summary>
        public static int UseObjectDistanceFade
        {
            get
            {
                if (_UseObjectDistanceFadeID == 0)
                    _UseObjectDistanceFadeID = Shader.PropertyToID("_UseObjectDistanceFade");
                return _UseObjectDistanceFadeID;
            }
        }
        /// <summary>
        /// Shader property ID for the _SineVariationSpeed property.
        /// </summary>
        static int _SineVariationSpeedID;
        /// <summary>
        /// Gets the shader property ID for _SineVariationSpeed.
        /// </summary>
        public static int SineVariationSpeed
        {
            get
            {
                if (_SineVariationSpeedID == 0)
                    _SineVariationSpeedID = Shader.PropertyToID("_SineVariationSpeed");
                return _SineVariationSpeedID;
            }
        }
        /// <summary>
        /// Shader property ID for the _UseSineVariation property.
        /// </summary>
        static int _UseSineVariationID;
        /// <summary>
        /// Gets the shader property ID for _UseSineVariation.
        /// </summary>
        public static int UseSineVariation
        {
            get
            {
                if (_UseSineVariationID == 0)
                    _UseSineVariationID = Shader.PropertyToID("_UseSineVariation");
                return _UseSineVariationID;
            }
        }
        /// <summary>
        /// Shader property ID for the _SineVariationColor property.
        /// </summary>
        static int _SineVariationColorID;
        /// <summary>
        /// Gets the shader property ID for _SineVariationColor.
        /// </summary>
        public static int SineVariationColor
        {
            get
            {
                if (_SineVariationColorID == 0)
                    _SineVariationColorID = Shader.PropertyToID("_SineVariationColor");
                return _SineVariationColorID;
            }
        }
        /// <summary>
        /// Shader property ID for the _SineOffset property.
        /// </summary>
        static int _SineOffsetID;
        /// <summary>
        /// Gets the shader property ID for _SineOffset.
        /// </summary>
        public static int SineOffset
        {
            get
            {
                if (_SineOffsetID == 0)
                    _SineOffsetID = Shader.PropertyToID("_SineOffset");
                return _SineOffsetID;
            }
        }
        /// <summary>
        /// Shader property ID for the _Style property.
        /// </summary>
        static int _StyleID;
        /// <summary>
        /// Gets the shader property ID for _Style.
        /// </summary>
        public static int Style
        {
            get
            {
                if (_StyleID == 0)
                    _StyleID = Shader.PropertyToID("_Style");
                return _StyleID;
            }
        }
        /// <summary>
        /// Shader property ID for the _Brightness property.
        /// </summary>
        static int _BrightnessID;
        /// <summary>
        /// Gets the shader property ID for _Brightness.
        /// </summary>
        public static int Brightness
        {
            get
            {
                if (_BrightnessID == 0)
                    _BrightnessID = Shader.PropertyToID("_Brightness");
                return _BrightnessID;
            }
        }
        /// <summary>
        /// Shader property ID for the _Bidirectional property.
        /// </summary>
        static int _BidirectionalID;
        /// <summary>
        /// Gets the shader property ID for _Bidirectional.
        /// </summary>
        public static int Bidirectional
        {
            get
            {
                if (_BidirectionalID == 0)
                    _BidirectionalID = Shader.PropertyToID("_Bidirectional");
                return _BidirectionalID;
            }
        }
        /// <summary>
        /// Shader property ID for the _AngleOpacityEffect property.
        /// </summary>
        static int _AngleOpacityEffectID;
        /// <summary>
        /// Gets the shader property ID for _AngleOpacityEffect.
        /// </summary>
        public static int AngleOpacityEffect
        {
            get
            {
                if (_AngleOpacityEffectID == 0)
                    _AngleOpacityEffectID = Shader.PropertyToID("_AngleOpacityEffect");
                return _AngleOpacityEffectID;
            }
        }
        /// <summary>
        /// Shader property ID for the _AngleRaylengthEffect property.
        /// </summary>
        static int _AngleRaylengthEffectID;
        /// <summary>
        /// Gets the shader property ID for _AngleRaylengthEffect.
        /// </summary>
        public static int AngleRaylengthEffect
        {
            get
            {
                if (_AngleRaylengthEffectID == 0)
                    _AngleRaylengthEffectID = Shader.PropertyToID("_AngleRaylengthEffect");
                return _AngleRaylengthEffectID;
            }
        }
        /// <summary>
        /// Shader property ID for the _RayLength property.
        /// </summary>
        static int _RayLengthID;
        /// <summary>
        /// Gets the shader property ID for _RayLength.
        /// </summary>
        public static int RayLength
        {
            get
            {
                if (_RayLengthID == 0)
                    _RayLengthID = Shader.PropertyToID("_RayLength");
                return _RayLengthID;
            }
        }
        /// <summary>
        /// Shader property ID for the _AutoAssignSun property.
        /// </summary>
        static int _AutoAssignSunID;
        /// <summary>
        /// Gets the shader property ID for _AutoAssignSun.
        /// </summary>
        public static int AutoAssignSun
        {
            get
            {
                if (_AutoAssignSunID == 0)
                    _AutoAssignSunID = Shader.PropertyToID("_AutoAssignSun");
                return _AutoAssignSunID;
            }
        }
        /// <summary>
        /// Shader property ID for the _UseLumenSunScript property.
        /// </summary>
        static int _UseLumenSunScriptID;
        /// <summary>
        /// Gets the shader property ID for _UseLumenSunScript.
        /// </summary>
        public static int UseLumenSunScript
        {
            get
            {
                if (_UseLumenSunScriptID == 0)
                    _UseLumenSunScriptID = Shader.PropertyToID("_UseLumenSunScript");
                return _UseLumenSunScriptID;
            }
        }
        /// <summary>
        /// Shader property ID for the _SunDirection property.
        /// </summary>
        static int _SunDirectionID;
        /// <summary>
        /// Gets the shader property ID for _SunDirection.
        /// </summary>
        public static int SunDirection
        {
            get
            {
                if (_SunDirectionID == 0)
                    _SunDirectionID = Shader.PropertyToID("_SunDirection");
                return _SunDirectionID;
            }
        }
        /// <summary>
        /// Shader property ID for the _UseCameraDepthFade property.
        /// </summary>
        static int _UseCameraDepthFadeID;
        /// <summary>
        /// Gets the shader property ID for _UseCameraDepthFade.
        /// </summary>
        public static int UseCameraDepthFade
        {
            get
            {
                if (_UseCameraDepthFadeID == 0)
                    _UseCameraDepthFadeID = Shader.PropertyToID("_UseCameraDepthFade");
                return _UseCameraDepthFadeID;
            }
        }
        /// <summary>
        /// Shader property ID for the _UseCameraDistanceFade property.
        /// </summary>
        static int _UseCameraDistanceFadeID;
        /// <summary>
        /// Gets the shader property ID for _UseCameraDistanceFade.
        /// </summary>
        public static int UseCameraDistanceFade
        {
            get
            {
                if (_UseCameraDistanceFadeID == 0)
                    _UseCameraDistanceFadeID = Shader.PropertyToID("_UseCameraDistanceFade");
                return _UseCameraDistanceFadeID;
            }
        }
        /// <summary>
        /// Shader property ID for the _CameraDepthFadeStart property.
        /// </summary>
        static int _CameraDepthFadeStartID;
        /// <summary>
        /// Gets the shader property ID for _CameraDepthFadeStart.
        /// </summary>
        public static int CameraDepthFadeStart
        {
            get
            {
                if (_CameraDepthFadeStartID == 0)
                    _CameraDepthFadeStartID = Shader.PropertyToID("_CameraDepthFadeStart");
                return _CameraDepthFadeStartID;
            }
        }
        /// <summary>
        /// Shader property ID for the _CameraDepthFadeEnd property.
        /// </summary>
        static int _CameraDepthFadeEndID;
        /// <summary>
        /// Gets the shader property ID for _CameraDepthFadeEnd.
        /// </summary>
        public static int CameraDepthFadeEnd
        {
            get
            {
                if (_CameraDepthFadeEndID == 0)
                    _CameraDepthFadeEndID = Shader.PropertyToID("_CameraDepthFadeEnd");
                return _CameraDepthFadeEndID;
            }
        }
        /// <summary>
        /// Shader property ID for the _UseSceneDepthFade property.
        /// </summary>
        static int _UseSceneDepthFadeID;
        /// <summary>
        /// Gets the shader property ID for _UseSceneDepthFade.
        /// </summary>
        public static int UseSceneDepthFade
        {
            get
            {
                if (_UseSceneDepthFadeID == 0)
                    _UseSceneDepthFadeID = Shader.PropertyToID("_UseSceneDepthFade");
                return _UseSceneDepthFadeID;
            }
        }
        /// <summary>
        /// Shader property ID for the _DepthFadeStartDistance property.
        /// </summary>
        static int _DepthFadeStartDistanceID;
        /// <summary>
        /// Gets the shader property ID for _DepthFadeStartDistance.
        /// </summary>
        public static int DepthFadeStartDistance
        {
            get
            {
                if (_DepthFadeStartDistanceID == 0)
                    _DepthFadeStartDistanceID = Shader.PropertyToID("_DepthFadeStartDistance");
                return _DepthFadeStartDistanceID;
            }
        }
        /// <summary>
        /// Shader property ID for the _DepthFadeEndDistance property.
        /// </summary>
        static int _DepthFadeEndDistanceID;
        /// <summary>
        /// Gets the shader property ID for _DepthFadeEndDistance.
        /// </summary>
        public static int DepthFadeEndDistance
        {
            get
            {
                if (_DepthFadeEndDistanceID == 0)
                    _DepthFadeEndDistanceID = Shader.PropertyToID("_DepthFadeEndDistance");
                return _DepthFadeEndDistanceID;
            }
        }
        /// <summary>
        /// Shader property ID for the _UseAngleBasedFade property.
        /// </summary>
        static int _UseAngleBasedFadeID;
        /// <summary>
        /// Gets the shader property ID for _UseAngleBasedFade.
        /// </summary>
        public static int UseAngleBasedFade
        {
            get
            {
                if (_UseAngleBasedFadeID == 0)
                    _UseAngleBasedFadeID = Shader.PropertyToID("_UseAngleBasedFade");
                return _UseAngleBasedFadeID;
            }
        }
        /// <summary>
        /// Shader property ID for the _AngleFadeEnd property.
        /// </summary>
        static int _AngleFadeID;
        /// <summary>
        /// Gets the shader property ID for _AngleFadeEnd.
        /// </summary>
        public static int AngleFadeEnd
        {
            get
            {
                if (_AngleFadeID == 0)
                    _AngleFadeID = Shader.PropertyToID("_AngleFadeEnd");
                return _AngleFadeID;
            }
        }
        /// <summary>
        /// Shader property ID for the _AngleFadeStart property.
        /// </summary>
        static int _AngleFadeStartID;
        /// <summary>
        /// Gets the shader property ID for _AngleFadeStart.
        /// </summary>
        public static int AngleFadeStart
        {
            get
            {
                if (_AngleFadeStartID == 0)
                    _AngleFadeStartID = Shader.PropertyToID("_AngleFadeStart");
                return _AngleFadeStartID;
            }
        }
        /// <summary>
        /// Shader property ID for the _NoiseVariationSpeed property.
        /// </summary>
        static int _NoiseVariationSpeedID;
        /// <summary>
        /// Gets the shader property ID for _NoiseVariationSpeed.
        /// </summary>
        public static int NoiseVariationSpeed
        {
            get
            {
                if (_NoiseVariationSpeedID == 0)
                    _NoiseVariationSpeedID = Shader.PropertyToID("_NoiseVariationSpeed");
                return _NoiseVariationSpeedID;
            }
        }
        /// <summary>
        /// Shader property ID for the _NoiseVariationScale property.
        /// </summary>
        static int _NoiseVariationScaleID;
        /// <summary>
        /// Gets the shader property ID for _NoiseVariationScale.
        /// </summary>
        public static int NoiseVariationScale
        {
            get
            {
                if (_NoiseVariationScaleID == 0)
                    _NoiseVariationScaleID = Shader.PropertyToID("_NoiseVariationScale");
                return _NoiseVariationScaleID;
            }
        }
        /// <summary>
        /// Shader property ID for the _UseUniformVariation property.
        /// </summary>
        static int _UseUniformVariationID;
        /// <summary>
        /// Gets the shader property ID for _UseUniformVariation.
        /// </summary>
        public static int UseUniformVariation
        {
            get
            {
                if (_UseUniformVariationID == 0)
                    _UseUniformVariationID = Shader.PropertyToID("_UseUniformVariation");
                return _UseUniformVariationID;
            }
        }
        /// <summary>
        /// Shader property ID for the _UseNoiseVariation property.
        /// </summary>
        static int _UseNoiseVariationID;
        /// <summary>
        /// Gets the shader property ID for _UseNoiseVariation.
        /// </summary>
        public static int UseNoiseVariation
        {
            get
            {
                if (_UseNoiseVariationID == 0)
                    _UseNoiseVariationID = Shader.PropertyToID("_UseNoiseVariation");
                return _UseNoiseVariationID;
            }
        }
        /// <summary>
        /// Shader property ID for the _NoiseVariationColor property.
        /// </summary>
        static int _NoiseVariationColorID;
        /// <summary>
        /// Gets the shader property ID for _NoiseVariationColor.
        /// </summary>
        public static int NoiseVariationColor
        {
            get
            {
                if (_NoiseVariationColorID == 0)
                    _NoiseVariationColorID = Shader.PropertyToID("_NoiseVariationColor");
                return _NoiseVariationColorID;
            }
        }
        /// <summary>
        /// Shader property ID for the _Range property.
        /// </summary>
        static int _RangeID;
        /// <summary>
        /// Gets the shader property ID for _Range.
        /// </summary>
        public static int Range
        {
            get
            {
                if (_RangeID == 0)
                    _RangeID = Shader.PropertyToID("_Range");
                return _RangeID;
            }
        }
        /// <summary>
        /// Shader property ID for the _Intensity property.
        /// </summary>
        static int _IntensityID;
        /// <summary>
        /// Gets the shader property ID for _Intensity.
        /// </summary>
        public static int Intensity
        {
            get
            {
                if (_IntensityID == 0)
                    _IntensityID = Shader.PropertyToID("_Intensity");
                return _IntensityID;
            }
        }
        /// <summary>
        /// Shader property ID for the _Smoothness property.
        /// </summary>
        static int _SmoothnessID;
        /// <summary>
        /// Gets the shader property ID for _Smoothness.
        /// </summary>
        public static int Smoothness
        {
            get
            {
                if (_SmoothnessID == 0)
                    _SmoothnessID = Shader.PropertyToID("_Smoothness");
                return _SmoothnessID;
            }
        }
        /// <summary>
        /// Shader property ID for the _IsSpotlight property.
        /// </summary>
        static int _IsSpotlightID;
        /// <summary>
        /// Gets the shader property ID for _IsSpotlight.
        /// </summary>
        public static int IsSpotlight
        {
            get
            {
                if (_IsSpotlightID == 0)
                    _IsSpotlightID = Shader.PropertyToID("_IsSpotlight");
                return _IsSpotlightID;
            }
        }
        /// <summary>
        /// Shader property ID for the _MinSpotlightAngle property.
        /// </summary>
        static int _MinSpotlightAngleID;
        /// <summary>
        /// Gets the shader property ID for _MinSpotlightAngle.
        /// </summary>
        public static int MinSpotlightAngle
        {
            get
            {
                if (_MinSpotlightAngleID == 0)
                    _MinSpotlightAngleID = Shader.PropertyToID("_MinSpotlightAngle");
                return _MinSpotlightAngleID;
            }
        }
        /// <summary>
        /// Shader property ID for the _MaxSpotlightAngle property.
        /// </summary>
        static int _MaxSpotlightAngleID;
        /// <summary>
        /// Gets the shader property ID for _MaxSpotlightAngle.
        /// </summary>
        public static int MaxSpotlightAngle
        {
            get
            {
                if (_MaxSpotlightAngleID == 0)
                    _MaxSpotlightAngleID = Shader.PropertyToID("_MaxSpotlightAngle");
                return _MaxSpotlightAngleID;
            }
        }
        /// <summary>
        /// Shader property ID for the _NormalFade property.
        /// </summary>
        static int _NormalFadeID;
        /// <summary>
        /// Gets the shader property ID for _NormalFade.
        /// </summary>
        public static int NormalFade
        {
            get
            {
                if (_NormalFadeID == 0)
                    _NormalFadeID = Shader.PropertyToID("_NormalFade");
                return _NormalFadeID;
            }
        }
        /// <summary>
        /// Shader property ID for the _NormalFadeOffset property.
        /// </summary>
        static int _NormalFadeOffsetID;
        /// <summary>
        /// Gets the shader property ID for _NormalFadeOffset.
        /// </summary>
        public static int NormalFadeOffset
        {
            get
            {
                if (_NormalFadeOffsetID == 0)
                    _NormalFadeOffsetID = Shader.PropertyToID("_NormalFadeOffset");
                return _NormalFadeOffsetID;
            }
        }
        /// <summary>
        /// Shader property ID for the _NormalFadeMultiplier property.
        /// </summary>
        static int _NormalFadeMultiplierID;
        /// <summary>
        /// Gets the shader property ID for _NormalFadeMultiplier.
        /// </summary>
        public static int NormalFadeMultiplier
        {
            get
            {
                if (_NormalFadeMultiplierID == 0)
                    _NormalFadeMultiplierID = Shader.PropertyToID("_NormalFadeMultiplier");
                return _NormalFadeMultiplierID;
            }
        }
        /// <summary>
        /// Shader property ID for the _Posterize property.
        /// </summary>
        static int _PosterizeID;
        /// <summary>
        /// Gets the shader property ID for _Posterize.
        /// </summary>
        public static int Posterize
        {
            get
            {
                if (_PosterizeID == 0)
                    _PosterizeID = Shader.PropertyToID("_Posterize");
                return _PosterizeID;
            }
        }
        /// <summary>
        /// Shader property ID for the _PosterizationSteps property.
        /// </summary>
        static int _PosterizationStepsID;
        /// <summary>
        /// Gets the shader property ID for _PosterizationSteps.
        /// </summary>
        public static int PosterizationSteps
        {
            get
            {
                if (_PosterizationStepsID == 0)
                    _PosterizationStepsID = Shader.PropertyToID("_PosterizationSteps");
                return _PosterizationStepsID;
            }
        }
        
        /// <summary>
        /// Shader property ID for the _Fluctuation property.
        /// </summary>
        static int _Fluctuation;
        /// <summary>
        /// Gets the shader property ID for _Fluctuation.
        /// </summary>
        public static int Fluctuation
        {
            get
            {
                if (_Fluctuation == 0)
                    _Fluctuation = Shader.PropertyToID("_Fluctuation");
                return _Fluctuation;
            }
        }
        /// <summary>
        /// Shader property ID for the _FluctuationSpeed property.
        /// </summary>
        static int _FluctuationSpeed;
        /// <summary>
        /// Gets the shader property ID for _FluctuationSpeed.
        /// </summary>
        public static int FluctuationSpeed
        {
            get
            {
                if (_FluctuationSpeed == 0)
                    _FluctuationSpeed = Shader.PropertyToID("_FluctuationSpeed");
                return _FluctuationSpeed;
            }
        }
        /// <summary>
        /// Shader property ID for the _FluctuationScale property.
        /// </summary>
        static int _FluctuationScale;
        /// <summary>
        /// Gets the shader property ID for _FluctuationScale.
        /// </summary>
        public static int FluctuationScale
        {
            get
            {
                if (_FluctuationScale == 0)
                    _FluctuationScale = Shader.PropertyToID("_FluctuationScale");
                return _FluctuationScale;
            }
        }
        /// <summary>
        /// Shader property ID for the _FluctuationAmount property.
        /// </summary>
        static int _FluctuationAmount;
        /// <summary>
        /// Gets the shader property ID for _FluctuationAmount.
        /// </summary>
        public static int FluctuationAmount
        {
            get
            {
                if (_FluctuationAmount == 0)
                    _FluctuationAmount = Shader.PropertyToID("_FluctuationAmount");
                return _FluctuationAmount;
            }
        }


    }
}