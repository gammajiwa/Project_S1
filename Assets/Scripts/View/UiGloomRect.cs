using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Kegelapan bergolak yang menggerogoti tepi sebuah kotak UI — <b>aura</b>.
    ///
    /// Memakai shader <c>Grimoire/GloomEdge</c> yang sudah dipakai peta, karena persoalannya
    /// sama persis: derau yang menggoyang GARIS BATAS, bukan kepekatan, dihitung dalam piksel
    /// supaya kotak besar dan kotak kecil terlihat berasal dari satu bahan. Yang belum ada cuma
    /// cara memakainya di luar peta — <c>_RectSize</c> wajib diisi dari C#, dan tanpa itu
    /// shadernya memakai 1920x1080 bawaan lalu menggambar pita gelap dengan ukuran yang salah.
    ///
    /// Ditaruh di objek Image kosong (tanpa sprite) yang lebih BESAR dari benda yang mau
    /// diberi aura, dan digambar SEBELUM bendanya — pita gelapnya lalu jatuh di luar benda
    /// itu alih-alih menggelapkan tepinya sendiri.
    ///
    /// Materialnya dibuat runtime per komponen, jadi dua aura dengan setelan berbeda tidak
    /// saling menimpa, dan tidak ada aset material yang perlu diurus.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("Grimoire/UI Gloom Rect")]
    public class UiGloomRect : MonoBehaviour
    {
        [Header("Warna")]
        public Color Tint = new Color(0.02f, 0.01f, 0.03f, 1f);

        [Tooltip("Kepekatan maksimum di tepi.")]
        [Range(0f, 1f)] public float Ceiling = 0.85f;

        [Header("Jangkauan dari tepi (piksel)")]
        [Tooltip("Seberapa jauh ke dalam kegelapannya mulai. Dijepit ke seperempat sisi " +
                 "terpendek di kode — tanpa itu kotak sempit bisa tertutup gelap seluruhnya.")]
        [Min(0f)] public float Inset = 120f;

        [Tooltip("Ketakberaturan garis batasnya, dalam piksel. Nol = bingkai gradien rapi, " +
                 "dan rapi itu yang membuatnya langsung terbaca sebagai vignette.")]
        [Min(0f)] public float Wobble = 70f;

        [Header("Bentuk & gerak")]
        [Min(1f)] public float Scale = 180f;

        [Tooltip("Kecepatan garis batasnya bergolak di tempat.")]
        [Min(0f)] public float Churn = 0.3f;

        [Tooltip("Kecepatan noda hanyut melintasi kotak, dalam piksel per detik.")]
        [Min(0f)] public float Drift = 36f;

        [Header("Tepi robek")]
        [Min(0f)] public float TearDepth = 40f;
        [Min(1f)] public float TearScale = 60f;
        [Range(0f, 1f)] public float TearFray = 0.45f;
        [Range(0.5f, 8f)] public float TearSoft = 1.5f;
        [Range(0f, 3f)] public float TearWarp = 0.8f;
        [Min(0f)] public float TearDrift = 5f;

        Image _image;
        Material _material;
        Vector2 _lastSize = new Vector2(-1f, -1f);

        void OnEnable()
        {
            _image = GetComponent<Image>();
            Build();
        }

        void OnDisable()
        {
            if (_material == null) return;

            if (Application.isPlaying) Destroy(_material);
            else DestroyImmediate(_material);

            _material = null;
        }

        void Build()
        {
            var shader = Shader.Find("Grimoire/GloomEdge");

            if (shader == null)
            {
                // Diam saja, bukan mati. Panel tanpa aura masih panel; mematikan seluruh UI
                // gara-gara hiasan adalah pertukaran yang salah.
                Debug.LogWarning("[UiGloomRect] shader Grimoire/GloomEdge tidak ketemu — " +
                                 "aura dilewati.");
                return;
            }

            _material = new Material(shader) { name = "GloomEdge (" + name + ")", hideFlags = HideFlags.DontSave };
            _material.SetFloat("_PaperMode", 0f);

            _image.material = _material;
            _lastSize = new Vector2(-1f, -1f);
            Push();
        }

        void Update()
        {
            if (_material == null) return;
            Push();
        }

        void Push()
        {
            _material.SetColor("_Color", Tint);
            _material.SetFloat("_Ceiling", Ceiling);
            _material.SetFloat("_Wobble", Wobble);
            _material.SetFloat("_Scale", Scale);
            _material.SetFloat("_Churn", Churn);
            _material.SetFloat("_Drift", Drift);
            _material.SetFloat("_TearScale", TearScale);
            _material.SetFloat("_TearFray", TearFray);
            _material.SetFloat("_TearSoft", TearSoft);
            _material.SetFloat("_TearWarp", TearWarp);
            _material.SetFloat("_TearDrift", TearDrift);

            var size = ((RectTransform)transform).rect.size;
            if (size == _lastSize) return;
            _lastSize = size;

            _material.SetVector("_RectSize", new Vector4(size.x, size.y, 0f, 0f));

            // Dijepit ke seperempat sisi terpendek: kalau tidak, kotak sempit membuat kedua
            // sisinya bertemu di tengah dan seluruh kotak tertutup gelap.
            float inset = Mathf.Min(Inset, Mathf.Min(size.x, size.y) * 0.25f);
            _material.SetFloat("_Inset", inset);
            _material.SetFloat("_TearDepth", TearDepth);
        }
    }
}
