using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Dua lapis cahaya di atas lantai: bayangan awan yang mengembara, dan berkas cahaya yang
    /// menembus dari arah matahari.
    ///
    /// Keduanya memakai trik yang sama — satu bidang besar mengikuti kamera, dengan tekstur derau
    /// yang digulung. Yang membuatnya bekerja bukan teksturnya, tapi <b>UV-nya dikunci ke
    /// koordinat DUNIA</b>: bidangnya ikut kamera, tapi polanya tidak. Tanpa itu bayangan awannya
    /// menempel di layar dan ikut ke mana pun pemain berjalan, yang langsung terbaca sebagai kotor
    /// di lensa, bukan sebagai awan di langit.
    ///
    /// Volumetrik sungguhan tidak dipakai dengan sengaja. Di kamera ortografis yang menunduk,
    /// berkas cahaya sebetulnya cuma terlihat sebagai pola terang DI LANTAI — dan pola di lantai
    /// harganya satu bidang, bukan satu render pass.
    /// </summary>
    public class Atmosphere : MonoBehaviour
    {
        const int NoiseSize = 256;

        Transform _follow;
        Transform _clouds;
        Transform _rays;
        Material _cloudMaterial;
        Material _rayMaterial;

        Vector2 _cloudDrift;
        Vector2 _rayDrift;

        float _cloudScale;
        float _rayScale;

        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public void Init(Transform follow, BiomeDefinition biome, float sunYaw, float span)
        {
            _follow = follow;

            // Digulung ke arah matahari, jadi awan dan berkasnya bergerak searah — dua arah yang
            // berbeda terbaca sebagai dua sistem cuaca yang tidak saling kenal.
            float rad = sunYaw * Mathf.Deg2Rad;
            var wind = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

            _cloudDrift = wind * biome.CloudSpeed;
            _rayDrift = wind * (biome.CloudSpeed * 0.35f);

            _cloudScale = 1f / Mathf.Max(1f, biome.CloudSize);
            _rayScale = 1f / Mathf.Max(1f, biome.RaySize);

            _clouds = BuildLayer("CloudShadows", span, 0.04f, out _cloudMaterial,
                Tileable(NoiseSize, 3.5f, 3.5f, biome.CloudCoverage, false),
                biome.CloudColor, false);

            // Berkasnya dimiringkan mengikuti arah matahari, dan itu satu-satunya hal yang membuat
            // ia terbaca sebagai cahaya yang MENEMBUS sesuatu alih-alih sebagai garis dekoratif.
            _rays = BuildLayer("GodRays", span, 0.06f, out _rayMaterial,
                Tileable(NoiseSize, 0.6f, 9f, biome.RayCoverage, true),
                biome.RayColor, true);

            _rays.localRotation = Quaternion.Euler(90f, sunYaw, 0f);
        }

        Transform BuildLayer(string label, float span, float height, out Material material,
            Texture2D texture, Color tint, bool additive)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = label;

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * span;

            // Sprites/Default, BUKAN URP/Unlit.
            //
            // URP/Unlit lahir sebagai opaque, dan menyalakan transparansinya lewat kode berarti
            // menyetel _Surface, _Blend, _SrcBlend, _DstBlend, _ZWrite, render queue DAN kata kunci
            // shader-nya — semuanya harus benar bersamaan, dan kalau satu meleset materialnya tetap
            // opaque tanpa satu pun keluhan. Hasilnya bidang putih pekat menutupi lantai, atau
            // tidak terlihat sama sekali.
            //
            // Sprites/Default sudah transparan sejak lahir, menghormati alpha tekstur DAN warna
            // tint, dan sudah terbukti jalan di project ini (BoltPool memakainya untuk alasan
            // serupa). Kurang ideal, tapi benar — dan benar mengalahkan ideal di sini.
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

            material = new Material(shader);
            material.mainTexture = texture;
            material.SetTexture(BaseMap, texture);
            material.color = tint;
            material.SetColor(BaseColor, tint);

            // Berkas cahaya MENAMBAH, bayangan awan MENGURANGI. Blend-nya diubah langsung karena
            // Sprites/Default memang mengeksposnya.
            if (additive)
            {
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            }

            material.renderQueue = 3000 + (additive ? 1 : 0);

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _ = height;
            go.transform.localPosition = new Vector3(0f, height, 0f);
            return go.transform;
        }

        void LateUpdate()
        {
            if (_follow == null) return;

            Vector3 at = _follow.position;

            _clouds.position = new Vector3(at.x, _clouds.localPosition.y, at.z);
            _rays.position = new Vector3(at.x, _rays.localPosition.y, at.z);

            // Inti seluruh efeknya: bidangnya ikut kamera, tapi UV-nya digeser balik sebanyak
            // posisi dunia. Polanya jadi diam di dunia, dan yang bergerak cuma waktu.
            _cloudMaterial.SetTextureOffset(BaseMap,
                new Vector2(at.x, at.z) * _cloudScale + _cloudDrift * Time.time);

            _rayMaterial.SetTextureOffset(BaseMap,
                new Vector2(at.x, at.z) * _rayScale + _rayDrift * Time.time);
        }

        /// <summary>
        /// Derau Perlin yang BISA DIUBIN. Perlin bawaan Unity tidak berulang, dan tekstur yang
        /// tidak berulang akan memperlihatkan jahitan lurus melintasi lapangan setiap kali ia
        /// mengulang — satu-satunya hal yang paling cepat merusak ilusi awan.
        ///
        /// Triknya: tiap piksel adalah campuran empat sampel dari sudut domain yang berseberangan,
        /// ditimbang jaraknya ke tepi. Tepi kiri jadi identik dengan tepi kanan secara matematis.
        /// </summary>
        static Texture2D Tileable(int size, float frequencyX, float frequencyY, float coverage,
            bool sharpen)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;

                    float value = Blend(u, v, frequencyX, frequencyY);

                    // Dua oktaf. Satu oktaf terbaca sebagai gumpalan lembut tanpa bentuk; yang
                    // kedua memberinya tepi yang bisa dikenali sebagai awan.
                    value = value * 0.7f + Blend(u, v, frequencyX * 2.7f, frequencyY * 2.7f) * 0.3f;

                    // Coverage menggeser ambangnya: rendah = langit cerah dengan sedikit awan,
                    // tinggi = mendung.
                    value = Mathf.InverseLerp(1f - coverage, 1f, value);
                    if (sharpen) value *= value;

                    byte alpha = (byte)(Mathf.Clamp01(value) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        static float Blend(float u, float v, float fx, float fy)
        {
            float a = Mathf.PerlinNoise(u * fx, v * fy);
            float b = Mathf.PerlinNoise((u - 1f) * fx, v * fy);
            float c = Mathf.PerlinNoise(u * fx, (v - 1f) * fy);
            float d = Mathf.PerlinNoise((u - 1f) * fx, (v - 1f) * fy);

            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
    }
}
