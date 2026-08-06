// Distant Lands 2025
// Lumen: Stylized Light FX 2
// The contents of this file are protected under the Unity Asset Store EULA
//
// Defines the base class for all Lumen effect layers, providing shared properties and methods for custom light FX in Unity.

using System;
using UnityEngine;

namespace DistantLands.Lumen
{
    /// <summary>
    /// Defines the base class for all Lumen effect layers, providing shared properties and methods for custom light FX in Unity.
    /// </summary>
    [Serializable]
    public abstract class LumenEffectLayer
    {
        /// <summary>
        /// Should this layer be rendered?
        /// </summary>
        [Tooltip("Should this layer be rendered?")]
        public bool active = true;
        /// <summary>
        /// The intensity of this effect layer.
        /// </summary>
        [Tooltip("The intensity of this effect layer")]
        [Range(0, 1)]
        public float brightness = 1;
        /// <summary>
        /// The position of this effect layer.
        /// </summary>
        [Tooltip("The position of this effect layer")]
        public Vector3 position = Vector3.zero;
        /// <summary>
        /// The rotation of this effect layer.
        /// </summary>
        [Tooltip("The rotation of this effect layer")]
        public Vector3 rotation = Vector3.zero;
        /// <summary>
        /// The scale of this effect layer.
        /// </summary>
        [Tooltip("The scale of this effect layer")]
        public Vector3 scale = Vector3.one;
        /// <summary>
        /// The color of this effect layer
        /// </summary>
        [Tooltip("The color of this effect layer")]
        [ColorUsage(true, true)]
        public Color color = Color.white;
        /// <summary>
        /// The mesh that this effect layer will use to display the Lumen effects
        /// </summary>
        [Tooltip("The mesh that this effect layer will use to display the Lumen effects")]
        public Mesh mesh;

        /// <summary>
        /// Applies a posterization effect to the effect layer.
        /// </summary>
        [Tooltip("Applies a posterization effect to the effect layer.")]
        public bool posterize;
        /// <summary>
        /// How many steps should be used in the posterization effect.
        /// </summary>
        [Tooltip("How many steps should be used in the posterization effect.")]
        public int posterizationSteps = 5;

        /// <summary>
        /// Applies a noise based variation to the effect layer. Uses a perlin function to interpolate between the main color and the Noise Variation Color.
        /// </summary>
        [Tooltip("Applies a noise based variation to the effect layer. Uses a perlin function to interpolate between the main color and the Noise Variation Color.")]
        public bool noiseVariation;
        /// <summary>
        /// The speed that the noise variation scrolls.
        /// </summary>
        [Tooltip("The speed that the noise variation scrolls.")]
        public float noiseVariationSpeed = 1;
        /// <summary>
        /// Determines the scale of the perlin noise.
        /// </summary>
        [Tooltip("Determines the scale of the perlin noise.")]
        public float noiseVariationScale = 10;
        /// <summary>
        /// Should the perlin noise be applied uniformly across the entire effect mesh? If set to false, the perlin noise will take the world-space position of each pixel into account when calculating. 
        /// </summary>
        [Tooltip("Should the perlin noise be applied uniformly across the entire effect mesh? If set to false, the perlin noise will take the world-space position of each pixel into account when calculating.")]
        public bool useUniformVariation = true;
        /// <summary>
        /// The color to interpolate to when applying the noise variation
        /// </summary>
        [Tooltip("The color to interpolate to when applying the noise variation")]
        [ColorUsage(false, true)]
        public Color noiseVariationColor = Color.gray;

        /// <summary>
        /// Interpolates to a variation color based on the effect of a sine wave.
        /// </summary>
        [Tooltip("Interpolates to a variation color based on the effect of a sine wave.")]
        public bool sineVariation;
        /// <summary>
        /// Determines the speed of the sine wave used for variation. Time between peaks is tau/x seconds.
        /// </summary>
        [Tooltip("Determines the speed of the sine wave used for variation. Time between peaks is tau/x seconds.")]
        public float sineVariationSpeed = 1;
        /// <summary>
        /// Offsets the sine function by x seconds.
        /// </summary>
        [Tooltip("Offsets the sine function by x seconds.")]
        public float sineVariationOffset = 0;
        /// <summary>
        /// The color to interpolate to when applying the sine variation.
        /// </summary>
        [Tooltip("The color to interpolate to when applying the sine variation.")]
        [ColorUsage(false, true)]
        public Color sineVariationColor = Color.gray;

        /// <summary>
        /// Should this effect layer be repeated?
        /// </summary>
        [Tooltip("Should this effect layer be repeated?")]
        public bool repeat = false;
        /// <summary>
        /// Sets the number of layer instances created by the repeat effect.
        /// </summary>
        [Tooltip("Sets the number of layer instances created by the repeat effect.")]
        public int repeatCount = 5;
        public enum RepeatDistributionStyle
        {
            Uniform,
            Curve,
            Random
        }
        /// <summary>
        /// Sets the distribution method for the repeat effect.
        /// </summary>
        [Tooltip("Sets the distribution method for the repeat effect.")]
        public RepeatDistributionStyle repeatDistribution = RepeatDistributionStyle.Uniform;
        /// <summary>
        /// Sets the position offset at the limit of the repeat effect. All repeated instances will be spread between the original transform and the spread value based on the repeat distribution style.
        /// </summary>
        [Tooltip("Sets the position offset at the limit of the repeat effect. All repeated instances will be spread between the original transform and the spread value based on the repeat distribution style.")]
        public Vector3 repeatPositionSpread;
        /// <summary>
        /// Sets the scale at the limit of the repeat effect. All repeated instances will be spread between the original transform and the spread value based on the repeat distribution style.
        /// </summary>
        [Tooltip("Sets the scale at the limit of the repeat effect. All repeated instances will be spread between the original transform and the spread value based on the repeat distribution style.")]
        public Vector3 repeatScaleSpread = Vector3.one;
        /// <summary>
        /// Sets the rotation offset at the limit of the repeat effect. All repeated instances will be spread between the original transform and the spread value based on the repeat distribution style.
        /// </summary>
        [Tooltip("Sets the rotation offset at the limit of the repeat effect. All repeated instances will be spread between the original transform and the spread value based on the repeat distribution style.")]
        public Vector3 repeatRotationSpread;
        /// <summary>
        /// Sets the brightness at the limit of the repeat effect. All repeated instances will be spread between the original brightness and the spread value based on the repeat distribution style.
        /// </summary>
        [Tooltip("Sets the brightness at the limit of the repeat effect. All repeated instances will be spread between the original brightness and the spread value based on the repeat distribution style.")]
        [Range(0, 1)]
        public float repeatBrightnessSpread;
        /// <summary>
        /// Specifies the gradient applied across all of the repeated instances.
        /// </summary>
        [Tooltip("Specifies the gradient applied across all of the repeated instances.")]
        public Gradient repeatColors;
        /// <summary>
        /// Sets the seed for the repeat effect. Only available when repeatDistribution is set to Random.
        /// </summary>
        [Tooltip("Sets the seed for the repeat effect.")]
        public int repeatSeed;

        /// <summary>
        /// Should the effect apply variation to each repeated instance?
        /// </summary>
        [Tooltip("Should the effect apply variation to each repeated instance?")]
        public bool repeatVariation = false;
        /// <summary>
        /// Adds a variation to the position of each repeat instance.
        /// </summary>
        [Tooltip("Adds a variation to the position of each repeat instance.")]
        public Vector3 repeatPositionVariation;
        /// <summary>
        /// Adds a variation to the rotation of each repeat instance.
        /// </summary>
        [Tooltip("Adds a variation to the rotation of each repeat instance.")]
        public Vector3 repeatRotationVariation;
        /// <summary>
        /// Adds a variation to the scale of each repeat instance.
        /// </summary>
        [Tooltip("Adds a variation to the scale of each repeat instance.")]
        [Range(0, 1)]
        public float repeatScaleVariation;


        /// <summary>
        /// Interpolates between the spread based on a prest curve.
        /// </summary>
        [Tooltip("Interpolates between the spread based on a prest curve.")]
        public AnimationCurve repeatCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));

        /// <summary>
        /// The material to apply when creating the effect. Generally found in the Resources/Materials folder.
        /// </summary>
        public virtual Material EffectMaterial => (Material)Resources.Load("Materials/Lumen Ray");

        /// <summary>
        /// Draws additional parameters to the property block and renderer when the layer is drawn.
        /// </summary>
        /// <param name="propertyBlock">The property block holding all material parameters.</param>
        /// <param name="renderer">The renderer that this effect is attached to.</param>
        public virtual void DrawLayer(MaterialPropertyBlock propertyBlock, Renderer renderer)
        {

        }

        /// <summary>
        /// Creates a new shallow copy of this effect layer with all parameters
        /// </summary>
        /// <returns></returns>
        public virtual LumenEffectLayer ShallowCopy()
        {
            return MemberwiseClone() as LumenEffectLayer;
        }

    }

}