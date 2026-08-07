using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Kegelapan yang MEMBUKA saat pemain mendekat dan MENUTUP lagi saat ditinggalkan.
    ///
    /// Anchornya dunia, bukan layar. Itu beda yang menentukan: vignette menggelapkan sudut monitor
    /// dan tidak tahu apa pun tentang tempat, jadi berjalan ke mana pun yang gelap tetap sudut yang
    /// sama. Di sini yang gelap adalah TEMPAT — satu petak tanah tetap gelap sampai pemain
    /// benar-benar mendatanginya, dan menggelap lagi begitu ditinggal.
    ///
    /// Yang membuatnya berhenti terbaca sebagai vignette bukan bentuk dasarnya, melainkan
    /// <b>deraunya menggoyang GARIS BATAS, bukan kepekatannya</b>. Mengalikan kepekatan dengan derau
    /// menghasilkan lingkaran sempurna yang isinya belang; menggeser ambang jaraknya membuat garis
    /// batasnya sendiri berkelok masuk-keluar sejauh belasan unit. Deraunya sendiri dibaca dari
    /// koordinat dunia, jadi lekuk yang sama menempel di petak tanah yang sama.
    ///
    /// Satu bidang, bukan bertumpuk. Percobaan bertumpuk dibuat untuk mengejar kesan volume lewat
    /// paralaks, dan itu tidak pernah berhasil — bidang datar tidak punya tebal, dan menumpuknya
    /// cuma membuat bidang datar yang digambar beberapa kali. Volume sungguhan dikerjakan kabut
    /// froxel, bukan di sini.
    /// </summary>
    public class Gloom : MonoBehaviour
    {
        static readonly int Tint = Shader.PropertyToID("_Color");
        static readonly int Centre = Shader.PropertyToID("_Center");
        static readonly int Inner = Shader.PropertyToID("_Inner");
        static readonly int Outer = Shader.PropertyToID("_Outer");
        static readonly int Scale = Shader.PropertyToID("_Scale");
        static readonly int Churn = Shader.PropertyToID("_Churn");
        static readonly int Drift = Shader.PropertyToID("_Drift");
        static readonly int Wobble = Shader.PropertyToID("_Wobble");
        static readonly int DotSize = Shader.PropertyToID("_DotSize");
        static readonly int Smooth = Shader.PropertyToID("_Smooth");
        static readonly int Ceiling = Shader.PropertyToID("_Ceiling");

        Transform _follow;
        Transform _player;
        Transform _plane;
        Material _material;
        float _span;
        bool _centreOnCamera = true;

        public void Init(Transform follow, Transform player, float span)
        {
            _follow = follow;
            _player = player;

            // Dilebihkan dari lebar layar. Bidangnya melayang sedikit di atas tanah, dan di kamera
            // menunduk ketinggian itu menggesernya beberapa piksel — bidang yang pas-pasan akan
            // menyisakan garis terang di tepi bawah layar.
            _span = span * 1.5f;
        }

        public void Apply(BiomeDefinition biome)
        {
            if (_plane != null) Destroy(_plane.gameObject);
            _plane = null;
            _material = null;

            if (biome == null || _follow == null || biome.GloomLayers == 0) return;

            var shader = Shader.Find("Grimoire/Gloom");

            if (shader == null)
            {
                Debug.LogError("[Gloom] shader Grimoire/Gloom tidak ketemu.");
                return;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Gloom";

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * _span;

            // Cukup tinggi untuk menutupi rumput dan batu, cukup rendah untuk membiarkan puncak
            // pohon menembusnya — dan batang yang keluar dari kegelapan itulah yang membuatnya
            // terbaca punya permukaan alih-alih sekadar warna yang ditumpuk.
            go.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            _material = new Material(shader) { name = "Gloom (Grimoire)" };

            _material.SetColor(Tint, biome.GloomColor);
            _material.SetFloat(Inner, biome.GloomStart);
            _material.SetFloat(Outer, biome.GloomEnd);
            _material.SetFloat(Scale, biome.GloomSize);
            _material.SetFloat(Churn, biome.GloomChurn);
            _material.SetFloat(Drift, biome.GloomDrift);
            _material.SetFloat(Wobble, biome.GloomWobble);
            _material.SetFloat(DotSize, biome.GloomDotSize);
            _material.SetFloat(Smooth, biome.GloomSmooth);
            _material.SetFloat(Ceiling, biome.GloomCeiling);

            _centreOnCamera = biome.GloomFollowsCamera;

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _plane = go.transform;
        }

        void LateUpdate()
        {
            if (_follow == null || _material == null) return;

            Vector3 at = _follow.position;
            transform.position = new Vector3(at.x, 0f, at.z);

            // Pusat terangnya adalah APA YANG SEDANG DISOROT KAMERA, bukan pemain.
            //
            // Terlihat mirip karena kamera memang mengikuti pemain, tapi kamera punya zona mati:
            // pemain boleh menyimpang beberapa unit dari pusat layar tanpa kamera bergerak. Kalau
            // pusatnya pemain, lingkaran terang ikut menyimpang bersamanya dan tepi layar yang
            // berlawanan diam-diam menggelap — yang terbaca sebagai kegelapan yang menjalar masuk
            // tiap kali pemain bergerak, bukan sebagai tempat yang tetap.
            Vector3 heart = _centreOnCamera || _player == null ? at : _player.position;
            _material.SetVector(Centre, new Vector4(heart.x, heart.z, 0f, 0f));
        }
    }
}
