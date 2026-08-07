using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Dua lapis cahaya di atas lantai: bayangan awan yang mengembara, dan berkas cahaya yang
    /// menembus dari arah matahari.
    ///
    /// Keduanya satu bidang besar yang mengikuti kamera, memakai shader
    /// <c>Grimoire/CloudShadows</c>. Deraunya dihitung DI DALAM shader dari posisi dunia fragmennya,
    /// dan waktunya diambil dari <c>_Time</c> milik shader sendiri.
    ///
    /// Itu dua hal yang dulu dikerjakan di sini dan dua-duanya salah:
    ///
    /// 1. Polanya dulu tekstur Perlin yang UV-nya digeser tiap frame agar "terkunci ke dunia".
    ///    Penguncian itu menuntut ukuran ubin dan pembagi pergeseran selalu sejalan — dan begitu
    ///    tidak, polanya menggeser sendiri setiap pemain berjalan. Membaca posisi dunia langsung
    ///    menghapus seluruh kelas kesalahan itu: tidak ada UV yang dikunci, jadi tidak ada yang
    ///    bisa salah dikunci.
    ///
    /// 2. Pergeserannya ditulis ke <c>_BaseMap</c>, nama milik URP, sementara shader yang dipakai
    ///    Sprites/Default yang teksturnya bernama <c>_MainTex</c>. Perintahnya mendarat di properti
    ///    yang tidak ada dan awannya TIDAK PERNAH bergerak. Sekarang tidak ada satu pun properti
    ///    yang perlu disentuh per frame, jadi tidak ada yang bisa meleset diam-diam.
    ///
    /// Volumetrik sungguhan tetap tidak dipakai. Di kamera ortografis yang menunduk, berkas cahaya
    /// sebetulnya cuma terlihat sebagai pola terang DI LANTAI — dan pola di lantai harganya satu
    /// bidang, bukan satu render pass.
    /// </summary>
    public class Atmosphere : MonoBehaviour
    {
        static readonly int Tint = Shader.PropertyToID("_Color");
        static readonly int Scale = Shader.PropertyToID("_Scale");
        static readonly int Coverage = Shader.PropertyToID("_Coverage");
        static readonly int Softness = Shader.PropertyToID("_Softness");
        static readonly int Direction = Shader.PropertyToID("_Direction");
        static readonly int Speed = Shader.PropertyToID("_Speed");
        static readonly int Evolve = Shader.PropertyToID("_Evolve");
        static readonly int Warp = Shader.PropertyToID("_Warp");
        static readonly int Stretch = Shader.PropertyToID("_Stretch");
        static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlend = Shader.PropertyToID("_DstBlend");

        Transform _follow;
        Transform _clouds;
        Transform _rays;
        float _span;

        public void Init(Transform follow, float span)
        {
            _follow = follow;
            _span = span;
        }

        /// <summary>
        /// Membangun ulang kedua lapis untuk wajah arena yang sekarang.
        ///
        /// Wajib bisa dipanggil BERKALI-KALI: versi pertama membangunnya sekali dari wajah pertama,
        /// dan begitu siang/malam jadi dua aset, malam mewarisi awan siang lengkap dengan arah angin
        /// yang mengikuti matahari yang sudah tidak ada.
        /// </summary>
        public void Apply(BiomeDefinition biome)
        {
            if (biome == null || _follow == null) return;

            if (_clouds != null) Destroy(_clouds.gameObject);
            if (_rays != null) Destroy(_rays.gameObject);

            var shader = Shader.Find("Grimoire/CloudShadows");

            if (shader == null)
            {
                Debug.LogError("[Atmosphere] shader Grimoire/CloudShadows tidak ketemu.");
                _clouds = null;
                _rays = null;
                return;
            }

            // Arah angin diacak TIAP SESI, dan itu memang yang diminta: dua run berturut-turut
            // tidak boleh punya cuaca yang persis sama. Dipakai System.Random, bukan
            // UnityEngine.Random — yang terakhir itu urutan acak milik gameplay (jenis musuh,
            // sebaran drop, arah semburan skill), dan mengambil satu angka dari sana menggeser
            // seluruh sisanya.
            var dice = new System.Random(System.Environment.TickCount);
            float heading = (float)dice.NextDouble() * Mathf.PI * 2f;
            var wind = new Vector2(Mathf.Sin(heading), Mathf.Cos(heading));

            // CloudSpeed dipakai APA ADANYA, dalam unit dunia per detik.
            //
            // Dulu ia dikalikan CloudSize di sini, dan pengali tersembunyi itulah yang membuat
            // angkanya salah dibaca: 0,06 terlihat masuk akal untuk "pelan", padahal hasilnya
            // 2 unit/detik — dan gumpalan selebar 34 unit yang bergerak 2 unit/detik butuh
            // tujuh belas detik untuk bergeser selebar dirinya. Terukur beranimasi, tetapi tidak
            // ada mata yang akan menyebutnya bergerak.
            _clouds = BuildLayer(shader, "CloudShadows", 0.04f, biome.CloudColor,
                // Evolve 0,015 — hampir nol, dan itu memang yang benar untuk awan.
                //
                // Sebelumnya 0,35, dan hasilnya bentuknya berubah lebih cepat daripada ia bergeser:
                // yang terlihat bukan awan yang melintas melainkan noda yang mendidih di tempat.
                // Awan sungguhan memang berubah bentuk, tapi jauh lebih lambat daripada ia hanyut —
                // dan mata membaca GERAK dari bentuk yang tetap sambil berpindah.
                biome.CloudSize, biome.CloudCoverage, 0.18f,
                wind, biome.CloudSpeed, 0.015f, 0.45f, Vector2.one, false);

            // Berkas cahaya DICOPOT dari sini, dan itu koreksi atas kesalahan yang mendasar.
            //
            // Versi lama menggambarnya sebagai bidang datar di atas tanah — pola terang bergaris
            // yang diregangkan searah matahari. Trik itu punya batas yang tidak bisa dilewati:
            // bidang tidak punya TINGGI. Berkas cahaya yang meyakinkan adalah kolom yang berdiri
            // dari tajuk pohon sampai ke tanah, dan tidak ada bidang setipis apa pun yang bisa
            // berpura-pura punya tinggi di kamera yang menunduk.
            //
            // Penggantinya berkas volumetrik sungguhan: kabut froxel HAZE yang kepadatannya
            // dinaikkan di daerah tak terbayangi, sehingga celah di antara bayangan pohon terisi
            // cahaya yang benar-benar menempati ruang. Disetel di HazePass, bukan di sini.
            if (biome.RayColor.a > 0.001f)
            {
                _rays = BuildLayer(shader, "GodRays", 0.06f, biome.RayColor,
                    biome.RaySize, biome.RayCoverage, 0.3f,
                    wind, biome.CloudSpeed * 0.35f, 0.12f, 0.15f,
                    new Vector2(0.28f, 2.4f), true);

                _rays.localRotation = Quaternion.Euler(90f, biome.SunYaw, 0f);
            }
        }

        Transform BuildLayer(Shader shader, string label, float height, Color tint,
            float size, float coverage, float softness, Vector2 wind, float speed,
            float evolve, float warp, Vector2 stretch, bool additive)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = label;

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * _span;
            go.transform.localPosition = new Vector3(0f, height, 0f);

            var material = new Material(shader) { name = label + " (Grimoire)" };

            material.SetColor(Tint, tint);
            material.SetFloat(Scale, Mathf.Max(1f, size));
            material.SetFloat(Coverage, coverage);
            material.SetFloat(Softness, softness);
            material.SetVector(Direction, new Vector4(wind.x, wind.y, 0f, 0f));
            material.SetFloat(Speed, speed);
            material.SetFloat(Evolve, evolve);
            material.SetFloat(Warp, warp);
            material.SetVector(Stretch, new Vector4(stretch.x, stretch.y, 0f, 0f));

            // Berkas cahaya MENAMBAH, bayangan awan MENGURANGI.
            material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat(DstBlend, (float)(additive
                ? UnityEngine.Rendering.BlendMode.One
                : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));

            material.renderQueue = 3000 + (additive ? 1 : 0);

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go.transform;
        }

        void LateUpdate()
        {
            if (_follow == null || _clouds == null) return;

            // Yang diikuti cuma POSISI bidangnya. Polanya tidak ikut — ia dihitung dari koordinat
            // dunia di dalam shader, jadi ia sudah diam di tanah tanpa dibantu apa pun dari sini.
            Vector3 at = _follow.position;

            _clouds.position = new Vector3(at.x, _clouds.localPosition.y, at.z);

            if (_rays != null) _rays.position = new Vector3(at.x, _rays.localPosition.y, at.z);
        }
    }
}
