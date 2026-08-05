using UnityEngine;
using UnityEngine.Rendering;

namespace Proto
{
    /// <summary>
    /// One shared description of how the world is lit, used by both the menu diorama and the run.
    /// Keeping it in an asset means the look can be retimed without touching either bootstrap.
    ///
    /// Note which colours get converted and which do not — see <see cref="RenderColor"/>. Sun and
    /// ambient are handed over raw on purpose.
    /// </summary>
    [CreateAssetMenu(fileName = "SceneLook", menuName = "Grimoire/Scene Look")]
    public class SceneLook : ScriptableObject
    {
        [Header("Matahari")]
        [Tooltip("Derajat di atas horizon. Makin kecil bayangan makin panjang, tapi lantai " +
                 "datar juga makin gelap — di bawah ~30 lantainya mulai kehilangan cahaya.")]
        [Range(5f, 80f)] public float SunPitch = 34f;

        [Range(-180f, 180f)] public float SunYaw = -35f;

        [ColorUsage(false)] public Color SunColor = new Color(1f, 0.851f, 0.627f);

        [Range(0f, 5f)] public float SunIntensity = 2.9f;

        [Range(0f, 1f)] public float ShadowStrength = 0.7f;

        [Header("Ambient (gradient tiga warna)")]
        [ColorUsage(false)] public Color SkyColor = new Color(0.40f, 0.46f, 0.58f);
        [ColorUsage(false)] public Color EquatorColor = new Color(0.62f, 0.54f, 0.44f);
        [ColorUsage(false)] public Color GroundBounce = new Color(0.36f, 0.29f, 0.23f);

        [Header("Kabut")]
        public bool FogEnabled = true;
        [ColorUsage(false)] public Color FogColor = new Color(0.30f, 0.24f, 0.22f);
        public float FogStart = 55f;
        public float FogEnd = 150f;

        [Header("Permukaan")]
        [Tooltip("Warna di luar tepi lantai. Nyaris tidak terlihat di kamera top-down.")]
        [ColorUsage(false)] public Color HorizonColor = new Color(0.22f, 0.20f, 0.23f);

        [Tooltip("Ingat: ini albedo sRGB, dan lantai datar cuma menerima sin(SunPitch) dari " +
                 "matahari. Angka yang terlihat 'sedang' di color picker akan tampil jauh lebih gelap.")]
        [ColorUsage(false)] public Color GroundColor = new Color(0.50f, 0.48f, 0.34f);

        [Tooltip("Benda tegak. Sengaja lebih terang dari lantai supaya siluetnya lepas.")]
        [ColorUsage(false)] public Color PropColor = new Color(0.74f, 0.70f, 0.62f);

        [ColorUsage(false)] public Color PlayerColor = new Color(0.95f, 0.85f, 0.5f);

        [Tooltip("0 = benar-benar matte. Permukaan datar yang mengkilap langsung terlihat plastik.")]
        [Range(0f, 1f)] public float SurfaceSmoothness = 0.05f;

        [Header("Post-processing")]
        public VolumeProfile PostProcess;

        /// <summary>Ambient and fog. Sun colours are handed to Unity raw — it linearises them.</summary>
        public void ApplyEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = SkyColor;
            RenderSettings.ambientEquatorColor = EquatorColor;
            RenderSettings.ambientGroundColor = GroundBounce;

            RenderSettings.fog = FogEnabled;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
        }

        public void ApplySun(Light light)
        {
            if (light == null) return;

            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(SunPitch, SunYaw, 0f);
            light.color = SunColor;
            light.intensity = SunIntensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = ShadowStrength;

            // A low sun grazing a big flat plane is the classic acne case.
            light.shadowBias = 0.08f;
            light.shadowNormalBias = 0.6f;
        }

        /// <summary>Builds a matte URP material. Colour is converted; see <see cref="RenderColor"/>.</summary>
        public Material CreateSurface(Color color, bool unlit = false)
        {
            var shader = Shader.Find(unlit
                ? "Universal Render Pipeline/Unlit"
                : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var material = new Material(shader);
            ApplySurface(material, color);
            return material;
        }

        public void ApplySurface(Material material, Color color)
        {
            if (material == null) return;

            int baseColor = Shader.PropertyToID("_BaseColor");
            if (material.HasProperty(baseColor)) material.SetColor(baseColor, RenderColor.Of(color));
            else material.color = RenderColor.Of(color);

            int smoothness = Shader.PropertyToID("_Smoothness");
            if (material.HasProperty(smoothness)) material.SetFloat(smoothness, SurfaceSmoothness);

            int metallic = Shader.PropertyToID("_Metallic");
            if (material.HasProperty(metallic)) material.SetFloat(metallic, 0f);
        }
    }
}
