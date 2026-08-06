// Distant Lands 2025
// Lumen: Stylized Light FX 2
// The contents of this file are protected under the Unity Asset Store EULA
//
// Controls the runtime behavior of Lumen effects in a Unity scene, including playback and updates.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

using Random = UnityEngine.Random;

namespace DistantLands.Lumen
{
    /// <summary>
    /// Controls the runtime behavior of Lumen effects in a Unity scene, including playback and updates.
    /// </summary>
    [ExecuteAlways]
    public class LumenEffectPlayer : MonoBehaviour
    {
        /// <summary>
        /// Create a new instance of a Lumen Effect Player for a given effect.
        /// </summary>
        /// <param name="effect">The effect that is played.</param>
        /// <returns>The new Lumen Effect Player.</returns>
        public static LumenEffectPlayer CreatePlayer(LumenEffectProfile effect)
        {
            return CreatePlayer(effect, Vector3.zero);
        }

        /// <summary>
        /// Create a new instance of a Lumen Effect Player at a specific position.
        /// </summary>
        /// <param name="effect">The effect that is played.</param>
        /// <param name="pos">The location to create the effect.</param>
        /// <returns>The new Lumen Effect Player.</returns>
        public static LumenEffectPlayer CreatePlayer(LumenEffectProfile effect, Vector3 pos)
        {
            LumenEffectPlayer player = new GameObject().AddComponent<LumenEffectPlayer>();
            player.transform.position = pos;
            player.gameObject.name = $"{effect.name} Player";
            player.profile = effect;
            return player;
        }

        /// <summary>
        /// Determine the frequency that a particular effect receives updates
        /// Always - updates every frame in the update loop
        /// On Changes - only update when a change is detected in the editor
        /// Via Scripting - only update when the RedoEffect method is called
        /// </summary>
        public enum UpdateFrequency
        {
            Always,
            OnChanges,
            ViaScripting
        }

        /// <summary>
        /// How frequently should the effect update?
        /// </summary>
        [Tooltip("How frequently should the effect update?")]
        public UpdateFrequency updateFrequency = UpdateFrequency.OnChanges;

        /// <summary>
        /// Applies a uniform scale modifier on all effects in the stack
        /// </summary>
        [Tooltip("Applies a uniform scale modifier on all effects in the stack")]
        public float scale = 1;

        /// <summary>
        /// Applies a uniform brightness modifier on all effects in the stack
        /// </summary>
        [Tooltip("Applies a uniform brightness modifier on all effects in the stack")]
        public float brightness = 1;

        /// <summary>
        /// Applies a uniform color modifier on all effects in the stack
        /// </summary>
        [Tooltip("Applies a uniform color modifier on all effects in the stack")]
        [ColorUsage(true, true)]
        public Color color = Color.white;

        internal LumenFloatFade brightnessFade;
        internal LumenFloatFade scaleFade;
        internal LumenColorFade colorFade;
        internal float controlledBrightness = 1;
        internal float controlledScale = 1;
        internal Color controlledColor = Color.white;

        /// <summary>
        /// Should the layers be displayed in the hierarchy? 
        /// </summary>
        public bool displayLayersInHierarchy = false;




        /// <summary>
        /// Applies a uniform range multiplier on all light layer effects in the stack
        /// </summary>
        [Tooltip("Applies a uniform range multiplier on all light layer effects in the stack")]
        public float range = 1;




        /// <summary>
        /// Controls the current sun direction for this effect. Only applies if the effect is set to not use the main directional light or the Lumen light direction component.
        /// </summary>
        [Tooltip("Controls the current sun direction for this effect.")]
        public Vector3 localSunDirection;

        /// <summary>
        /// Should the Lumen effect automatically assign the sun from the brightest directional light in the scene?
        /// </summary>
        [Tooltip("Should the Lumen effect automatically assign the sun from the brightest directional light in the scene?")]
        public bool autoAssignSun = true;

        /// <summary>
        /// Should the Lumen effect automatically assign the sun from the LumenSun.cs script?
        /// </summary>
        [Tooltip("Should the Lumen effect automatically assign the sun from the LumenSun.cs script?")]
        public bool useLumenSunScript = true;




        /// <summary>
        /// The profile used for the effect
        /// </summary>
        [Tooltip("The profile used for the effect")]
        public LumenEffectProfile profile;

        public enum InitializationBehavior
        {
            Immediate,
            FadeToTargetBrightness,
            FadeToTargetScale,
            FadeToTargetColor,
            SkipInitialization
        }

        /// <summary>
        /// Determines the time it takes to transition or complete a fade
        /// </summary>
        [Tooltip("Determines the time it takes to transition or complete a fade")]
        public float fadingTime = 1;

        /// <summary>
        /// Determines the behavior of the Lumen Effect Player when the effect is initialized
        /// </summary>
        [Tooltip("Determines the behavior of the Lumen Effect Player when the effect is initialized")]
        public InitializationBehavior initializationBehavior = InitializationBehavior.Immediate;

        public enum DeinitializationBehavior
        {
            Immediate,
            FadeBrightnessToZero,
            FadeScaleToZero,
            FadeColorToBlack
        }

        /// <summary>
        /// Determines the behavior of the Lumen Effect Player when the effect is deinitialized or destroyed.
        /// </summary>
        [Tooltip("Determines the behavior of the Lumen Effect Player when the effect is deinitialized or destroyed")]
        public DeinitializationBehavior deinitializationBehavior = DeinitializationBehavior.Immediate;

        public struct InstantiatedLumenLayer
        {
            public Transform transform;
            public Renderer renderer;
            public MeshFilter filter;

        }

        /// <summary>
        /// Manages the references for the currently instantiated effect layers
        /// </summary>
        public List<InstantiatedLumenLayer> instantiatedLumenLayers = new List<InstantiatedLumenLayer>();

        private MaterialPropertyBlock sharedBlock;
        private int totalLayerCount;

        bool isQuitting = false;

        private int entityId;
        private int entityIdHash;
        private LumenManager effectManager;


        private void Awake()
        {
            entityId = gameObject.GetInstanceID();
            entityIdHash = entityId.GetHashCode();
        }

        private void HandleStartQuit() => isQuitting = true;

        /// <summary>
        /// This function is called when the object becomes enabled and active.
        /// </summary>
        void OnEnable()
        {
            effectManager = LumenManager.Instance;

            if (sharedBlock == null)
                sharedBlock = new MaterialPropertyBlock();
                
            ClearEffect();

            if (Application.isPlaying)
            {
                switch (initializationBehavior)
                {
                    case InitializationBehavior.Immediate:
                        RedoEffect();
                        break;
                    case InitializationBehavior.FadeToTargetBrightness:
                        RedoEffect();
                        controlledBrightness = 0;
                        FadeBrightness(1, fadingTime);
                        break;
                    case InitializationBehavior.FadeToTargetScale:
                        RedoEffect();
                        controlledScale = 0;
                        FadeScale(1, fadingTime);
                        break;
                    case InitializationBehavior.FadeToTargetColor:
                        RedoEffect();
                        controlledColor = Color.black;
                        FadeColor(Color.white, fadingTime);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                RedoEffect();
            }
            LumenUtility.OnRedoEntireEffect += RedoEffect;
            Application.quitting += HandleStartQuit;

            isQuitting = false;

            effectManager.AddEffectPlayer(this);
        }

        /// <summary>
        /// This function is called when the behaviour becomes disabled or inactive.
        /// </summary>
        void OnDisable()
        {
            if (Application.isPlaying)
            {
                switch (deinitializationBehavior)
                {
                    case DeinitializationBehavior.Immediate:
                        ClearEffect();
                        effectManager.RemoveEffectPlayer(this);
                        break;
                    case DeinitializationBehavior.FadeBrightnessToZero:
                        DestroyEffect(fadingTime);
                        FadeBrightness(0, fadingTime);
                        break;
                    case DeinitializationBehavior.FadeScaleToZero:
                        DestroyEffect(fadingTime);
                        FadeScale(0, fadingTime);
                        break;
                    case DeinitializationBehavior.FadeColorToBlack:
                        DestroyEffect(fadingTime);
                        FadeColor(Color.black, fadingTime);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                ClearEffect();
                effectManager.RemoveEffectPlayer(this);
            }
            LumenUtility.OnRedoEntireEffect -= RedoEffect;
            Application.quitting -= HandleStartQuit;
        }

        /// <summary>
        /// Fades the effect linearly to a set brightness
        /// </summary>
        /// <param name="targetBrightness">The new target brightness</param>
        /// <param name="time">The amount of time that the fade takes to fully complete.</param>
        public void FadeBrightness(float targetBrightness, float time)
        {
            brightnessFade.Start(controlledBrightness, targetBrightness, time);
        }

        /// <summary>
        /// Fades the effect linearly to a set scale
        /// </summary>
        /// <param name="targetScale">The new target scale</param>
        /// <param name="time">The amount of time that the fade takes to fully complete.</param>
        public void FadeScale(float targetScale, float time)
        {
            scaleFade.Start(controlledScale, targetScale, time);
        }

        /// <summary>
        /// Fades the effect linearly to a set color
        /// </summary>
        /// <param name="targetColor">The new target color</param>
        /// <param name="time">The amount of time that the fade takes to fully complete.</param>
        public void FadeColor(Color targetColor, float time)
        {
            colorFade.Start(controlledColor, targetColor, time);
        }

        [ContextMenu("Refresh Effect")]
        void RefreshEffect()
        {
            ClearEffect();
            RedoEffect();
        }
        [ContextMenu("Toggle Display")]
        void ToggleDisplay()
        {
            displayLayersInHierarchy = !displayLayersInHierarchy;
            ClearEffect();
            RedoEffect();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RedoEffect()
        {
            RedoEffect(true);
        }

        /// <summary>
        /// Full redraw. Updates meshes, materials, and hierarchy flags.
        /// </summary>
        public void RedoEffect(bool fullRedraw = true)
        {
            if (!gameObject || !gameObject.scene.IsValid() || profile == null || profile.layers.Count == 0) return;

            if (brightnessFade.isActive) controlledBrightness = brightnessFade.Evaluate();
            if (scaleFade.isActive) controlledScale = scaleFade.Evaluate();
            if (colorFade.isActive) controlledColor = colorFade.Evaluate();

            totalLayerCount = GetTotalLayerCount(profile);
            UpdateMeshPool();
            int currentMeshIndex = 0;

            foreach (LumenEffectLayer layer in profile.layers)
            {
                if (layer == null) continue;
                if (fullRedraw)
                    DrawInstancedMeshHeavy(layer, Vector3.zero, Vector3.zero, Vector3.one, Color.white, 1, ref currentMeshIndex);
                else
                    DrawInstancedMeshLight(layer, Vector3.zero, Vector3.zero, Vector3.one, Color.white, 1, ref currentMeshIndex);
            }
        }

        void DrawInstancedMeshHeavy(LumenEffectLayer layer, Vector3 posOffset, Vector3 rotOffset, Vector3 scaleOffset, Color colorOffset, float brightnessOffset, ref int currentMeshIndex)
        {
            if (!layer.active) return;

            if (layer is EffectStackLayer effectStack)
            {
                if (!effectStack.profile || effectStack.profile == profile) return;
                foreach (LumenEffectLayer childLayer in effectStack.profile.layers)
                    DrawInstancedMeshHeavy(childLayer, layer.position * scale, layer.rotation, layer.scale * scale, layer.color, layer.brightness, ref currentMeshIndex);
                return;
            }

            if (layer.mesh == null) return;

            int loopCount = (layer.repeat && layer.repeatCount > 0) ? (int)Mathf.Max(layer.repeatCount, 1) : 1;
            Random.State savedState = Random.state;
            Random.InitState(layer.repeatSeed + entityIdHash);

            for (int i = 0; i < loopCount; i++)
            {
                InstantiatedLumenLayer currentLayer = instantiatedLumenLayers[currentMeshIndex];
                GameObject go = currentLayer.transform.gameObject;
                MeshRenderer renderer = (MeshRenderer)currentLayer.renderer;

                currentLayer.filter.mesh = layer.mesh;
                renderer.sharedMaterial = layer.EffectMaterial;
                go.layer = gameObject.layer;
                go.hideFlags = displayLayersInHierarchy ? HideFlags.DontSaveInEditor : (HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy);

                if (layer is DynamicRayLayer dynamicRay)
                    renderer.bounds = new Bounds(Vector3.zero, Vector3.one * dynamicRay.rayLength);

                UpdateLayerProperties(layer, currentLayer, i, loopCount, posOffset, rotOffset, scaleOffset, colorOffset, brightnessOffset);
                currentMeshIndex++;
            }

            Random.state = savedState;
        }

        void DrawInstancedMeshLight(LumenEffectLayer layer, Vector3 posOffset, Vector3 rotOffset, Vector3 scaleOffset, Color colorOffset, float brightnessOffset, ref int currentMeshIndex)
        {
            if (!layer.active) return;

            if (layer is EffectStackLayer effectStack)
            {
                if (!effectStack.profile || effectStack.profile == profile) return;
                foreach (LumenEffectLayer childLayer in effectStack.profile.layers)
                    DrawInstancedMeshLight(childLayer, layer.position * scale, layer.rotation, layer.scale * scale, layer.color, layer.brightness, ref currentMeshIndex);
                return;
            }

            if (layer.mesh == null) return;

            int loopCount = (layer.repeat && layer.repeatCount > 0) ? (int)Mathf.Max(layer.repeatCount, 1) : 1;
            Random.State savedState = Random.state;
            Random.InitState(layer.repeatSeed + entityIdHash);

            for (int i = 0; i < loopCount; i++)
            {
                UpdateLayerProperties(layer, instantiatedLumenLayers[currentMeshIndex], i, loopCount, posOffset, rotOffset, scaleOffset, colorOffset, brightnessOffset);
                currentMeshIndex++;
            }

            Random.state = savedState;
        }

        void UpdateLayerProperties(LumenEffectLayer layer, InstantiatedLumenLayer currentMesh, int index, int totalIndices, Vector3 posOffset, Vector3 rotOffset, Vector3 scaleOffset, Color colorOffset, float brightnessOffset)
        {
            Transform t = currentMesh.transform;
            float completion = (float)index / Mathf.Max(totalIndices - 1, 1);

            if (layer.repeat && layer.repeatCount > 0)
            {
                if (layer.repeatDistribution == LumenEffectLayer.RepeatDistributionStyle.Random) completion = Random.value;
                else if (layer.repeatDistribution == LumenEffectLayer.RepeatDistributionStyle.Curve) completion = layer.repeatCurve.Evaluate(completion);

                t.SetLocalPositionAndRotation(layer.position * scale + layer.repeatPositionSpread * completion, Quaternion.Euler(layer.rotation + layer.repeatRotationSpread * completion));
                t.localScale = scale * Vector3.Lerp(layer.scale, Vector3.Scale(layer.scale, layer.repeatScaleSpread), completion);

                if (layer.repeatVariation)
                {
                    var localPositionOffest = new Vector3((Random.value - 0.5f) * 2 * layer.repeatPositionVariation.x, (Random.value - 0.5f) * 2 * layer.repeatPositionVariation.y, (Random.value - 0.5f) * 2 * layer.repeatPositionVariation.z);
                    var localEulerAnglesOffset = new Vector3((Random.value - 0.5f) * 2 * layer.repeatRotationVariation.x, (Random.value - 0.5f) * 2 * layer.repeatRotationVariation.y, (Random.value - 0.5f) * 2 * layer.repeatRotationVariation.z);
                    t.SetLocalPositionAndRotation(t.localPosition + localPositionOffest, Quaternion.Euler(t.localEulerAngles + localEulerAnglesOffset));
                    t.localScale *= 1f - Random.value * layer.repeatScaleVariation;
                }
            }
            else
            {
                t.SetLocalPositionAndRotation((layer.position + posOffset) * scale, Quaternion.Euler(layer.rotation + rotOffset));
                t.localScale = Vector3.Scale(layer.scale, scaleOffset) * scale;
            }

            sharedBlock.Clear();

            if (layer is DynamicRayLayer)
            {
                sharedBlock.SetFloat(LumenShaderIDs.AutoAssignSun, autoAssignSun ? 1f : 0f);
                sharedBlock.SetFloat(LumenShaderIDs.UseLumenSunScript, useLumenSunScript ? 1f : 0f);
                sharedBlock.SetVector(LumenShaderIDs.SunDirection, localSunDirection);
            }
            else if (layer is LumenLightLayer lightLayer)
            {
                sharedBlock.SetFloat(LumenShaderIDs.Range, range * lightLayer.range);
                t.localScale = Vector3.one * range * lightLayer.range * 2;
            }

            float repeatBright = layer.repeat ? Mathf.Lerp(1, (1 - layer.repeatBrightnessSpread), completion) : 1;
            Color repeatCol = layer.repeat ? layer.repeatColors.Evaluate(completion) : Color.white;

            sharedBlock.SetColor(LumenShaderIDs.Color, color * controlledColor * layer.color * colorOffset * repeatCol);
            sharedBlock.SetFloat(LumenShaderIDs.Brightness, brightness * controlledBrightness * layer.brightness * brightnessOffset * repeatBright);

            layer.DrawLayer(sharedBlock, currentMesh.renderer);
            currentMesh.renderer.SetPropertyBlock(sharedBlock);
        }

        /// <summary>
        /// Counts the total number of meshes needed to draw the effect (including repeated iterations and effect stacks)
        /// </summary>
        public int GetTotalLayerCount(LumenEffectProfile lProfile)
        {
            int currentLayerCount = 0;

            foreach (LumenEffectLayer layer in lProfile.layers)
            {
                if (!layer.active) continue;

                if (layer is EffectStackLayer effectStack)
                {
                    if (effectStack.profile && effectStack.profile != lProfile && effectStack.profile != profile)
                        currentLayerCount += GetTotalLayerCount(effectStack.profile);

                    continue;
                }

                if (layer.mesh == null) continue;

                if (layer.repeat && layer.repeatCount > 0)
                {
                    currentLayerCount += layer.repeatCount;
                    continue;
                }
                currentLayerCount++;
            }

            return currentLayerCount;
        }

        /// <summary>
        /// Updates the pool of meshes used in the effect. Can be heavy or create garbage if there are large changes happening often so use sparingly!
        /// </summary>
        public void UpdateMeshPool()
        {
            while (instantiatedLumenLayers.Count > totalLayerCount)
            {
                if (Application.isPlaying)
                    Destroy(instantiatedLumenLayers[^1].transform.gameObject);
                else
                    DestroyImmediate(instantiatedLumenLayers[^1].transform.gameObject);

                instantiatedLumenLayers.RemoveAt(instantiatedLumenLayers.Count - 1);
            }
            while (instantiatedLumenLayers.Count < totalLayerCount)
            {
                GameObject obj = new GameObject("Layer");

                obj.layer = gameObject.layer;
                if (displayLayersInHierarchy)
                    obj.hideFlags = HideFlags.DontSaveInEditor;
                else
                    obj.hideFlags = HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy;

                var objTransform = obj.transform;
                objTransform.parent = transform;
                objTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                objTransform.localScale = Vector3.one;
                MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
                MeshFilter meshFilter = obj.AddComponent<MeshFilter>();

                instantiatedLumenLayers.Add(new InstantiatedLumenLayer() { filter = meshFilter, renderer = renderer, transform = obj.transform });
            }
        }

        /// <summary>
        /// Clears the effect stack
        /// </summary>
        public void ClearEffect()
        {
            if (!gameObject.scene.IsValid() || gameObject.scene.name == null)
            {
                return;
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                {
                    if (transform.GetChild(i) != null)
                        Destroy(transform.GetChild(i).gameObject);
                }
                else
                {
                    if (transform.GetChild(i) != null)
                        DestroyImmediate(transform.GetChild(i).gameObject);
                }
            }

            instantiatedLumenLayers.Clear();
        }

        /// <summary>
        /// Clears the effect stack
        /// </summary>
        /// <param name="delay">Will clear the effect after a set delay (in seconds)</param>
        public void DestroyEffect(float delay)
        {
            if (!isQuitting && gameObject.activeInHierarchy)
                StartCoroutine(DelayClear(delay));
        }

        IEnumerator DelayClear(float delay)
        {
            yield return new WaitForSeconds(delay);
            ClearEffect();
            effectManager.RemoveEffectPlayer(this);
        }

    }

    public struct LumenFloatFade
    {
        public float start;
        public float target;
        public float time;
        public float elapsed;
        public bool isActive;

        public void Start(float startValue, float targetValue, float duration)
        {
            start = startValue;
            target = targetValue;
            time = duration;
            elapsed = 0;
            isActive = duration > 0;
            if (!isActive) start = target;
        }

        public float Evaluate()
        {
            if (!isActive) return target;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / time);
            if (t >= 1) isActive = false;
            return Mathf.Lerp(start, target, t);
        }
    }

    public struct LumenColorFade
    {
        public Color start;
        public Color target;
        public float time;
        public float elapsed;
        public bool isActive;

        public void Start(Color startValue, Color targetValue, float duration)
        {
            start = startValue;
            target = targetValue;
            time = duration;
            elapsed = 0;
            isActive = duration > 0;
            if (!isActive) start = target;
        }

        public Color Evaluate()
        {
            if (!isActive) return target;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / time);
            if (t >= 1) isActive = false;
            return Color.Lerp(start, target, t);
        }
    }
}
