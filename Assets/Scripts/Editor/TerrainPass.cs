using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Menaruh SATU Terrain kosong di scene game, siap dipahat dan dicat dengan tangan.
    ///
    /// Ini pembagian kerjanya: lantai disusun manusia, isinya disebar mesin. Lantai adalah satu
    /// permukaan besar yang setiap senti perseginya terlihat sekaligus, dan tidak ada derau
    /// terprogram yang menyamai orang yang bisa melihat hasilnya sambil mengecat. Pohon dan rumpun
    /// sebaliknya: ratusan benda kecil yang justru rusak kalau disusun tangan, karena mata manusia
    /// tidak bisa menaruh tiga ratus benda tanpa meninggalkan pola.
    ///
    /// Begitu Terrain ini ada di scene, <see cref="ProtoBootstrap"/> berhenti membangun lantainya
    /// sendiri dan <see cref="BiomeDresser"/> mengambil ketinggian tiap prop dari permukaan ini —
    /// jadi bukit yang dipahat langsung ditumbuhi pohon tanpa satu angka pun yang perlu disetel.
    ///
    /// Idempoten: dijalankan ulang tidak menimpa apa yang sudah dipahat.
    /// </summary>
    public static class TerrainPass
    {
        const string ScenePath = "Assets/Scenes/Proto.unity";
        const string Folder = "Assets/GameData/Terrain";
        const string DataPath = Folder + "/Arena_Terrain.asset";

        const string Nature = "Assets/Plugin/InnerverseInteractive/Ultimate Nature – Starter/";
        const string Painted = "Assets/Plugin/Handpainted_Grass_and_Ground_Textures/Textures/";

        /// <summary>Sisi lantai. Arena 80x60 plus margin kelahiran musuh di luar dindingnya.</summary>
        static readonly Vector2 Size = new Vector2(160f, 140f);

        /// <summary>
        /// Nilai kepadatan tertinggi yang ditulis cat pembuka. BiomeDresser menormalkan peta ini
        /// terhadap nilai TERTINGGI yang ditemukannya, jadi angka ini yang menjadi arti "penuh".
        /// </summary>
        const int DetailPeak = 8;

        /// <summary>
        /// Mengecat ulang lantai dari nol, MENGHAPUS apa pun yang sudah dicat di atasnya.
        ///
        /// Perintah terpisah dengan nama yang mengatakan apa yang dihapusnya. <c>Create Editable
        /// Terrain</c> sengaja tidak pernah menimpa catan — dan justru karena itu ia tidak bisa
        /// dipakai untuk berganti paket aset, karena daftar lapisannya ikut terkunci.
        /// </summary>
        [MenuItem("Tools/Grimoire/Reset Terrain Paint")]
        public static void Repaint()
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath);

            if (data == null)
            {
                Debug.LogError("[TerrainPass] belum ada terrain. Jalankan Create Editable Terrain.");
                return;
            }

            if (!EditorUtility.DisplayDialog("Reset cat lantai",
                    "Seluruh catan lapisan di " + DataPath + " akan diganti dengan cat pembuka. " +
                    "Bentuk pahatannya TIDAK berubah.\n\nLanjut?", "Cat ulang", "Batal"))
            {
                return;
            }

            data.terrainLayers = Layers();
            SeedPaint(data);

            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain != null) terrain.materialTemplate = null;

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            Debug.Log("[TerrainPass] lantai dicat ulang dengan " + data.terrainLayers.Length +
                      " lapisan low-poly.");
        }

        [MenuItem("Tools/Grimoire/Create Editable Terrain")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[TerrainPass] Tidak bisa jalan di play mode. Stop dulu.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/GameData", "Terrain");

            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath);
            bool fresh = data == null;

            if (fresh)
            {
                data = new TerrainData();

                // Urutannya WAJIB begini dan gagal tanpa error kalau dibalik: menyetel
                // heightmapResolution mengembalikan size ke bawaannya, dan menyetel
                // alphamapResolution membuang alphamap yang sudah ditulis.
                data.heightmapResolution = 257;
                data.size = new Vector3(Size.x, 30f, Size.y);
                data.alphamapResolution = 512;
                data.SetDetailResolution(512, 16);

                data.terrainLayers = Layers();

                AssetDatabase.CreateAsset(data, DataPath);
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
            var existing = Object.FindFirstObjectByType<Terrain>();

            if (existing == null)
            {
                var go = Terrain.CreateTerrainGameObject(data);
                go.name = "Ground";
                existing = go.GetComponent<Terrain>();
            }

            existing.terrainData = data;

            var collider = existing.GetComponent<TerrainCollider>();
            if (collider != null) collider.terrainData = data;

            // Daftar lapisan hanya disegarkan selagi belum ada yang mengecat. Setelah dicat, daftar
            // itu adalah arti dari catnya — mengubah urutannya berarti tanah yang sudah digambar
            // tiba-tiba jadi pasir, tanpa satu pun piksel alphamap yang berubah.
            if (Untouched(data))
            {
                data.terrainLayers = Layers();
                SeedPaint(data);
                EditorUtility.SetDirty(data);
            }

            // Dipusatkan di titik nol. Arena, kamera, dan seluruh penjepitan gerak dihitung dari
            // sana; terrain yang sudutnya di nol menaruh separuh lapangan di luar arena.
            existing.transform.position = new Vector3(-Size.x * 0.5f, 0f, -Size.y * 0.5f);

            existing.materialTemplate = TerrainMaterial();

            existing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            existing.basemapDistance = 1000f;
            existing.heightmapPixelError = 5f;

            // Rumput dikuas lewat alat Paint Details bawaan Unity, TAPI yang menggambarnya bukan
            // mesin detail Unity — melainkan BiomeDresser, lewat jalur instanced yang sama dengan
            // pohon dan batu.
            //
            // Bukan pilihan gaya: mesin detail Unity tidak menggambar apa pun di project ini.
            // Datanya tertulis dan terbaca kembali, prototipenya lolos Validate, shader detail URP
            // ada, jaraknya jauh di dalam jangkauan, dan layarnya tetap kosong — di kamera
            // ortografis maupun perspektif, dengan instancing maupun tanpa. Jadi peta kuasnya
            // dipakai sebagai DATA, dan penggambarannya dikerjakan sendiri.
            //
            // Jaraknya dinolkan supaya mesin itu tidak diam-diam mulai bekerja setelah update Unity
            // berikutnya lalu menggambar rumput KEDUA di atas rumput yang sudah ada.
            existing.detailObjectDistance = 0f;
            existing.detailObjectDensity = 1f;
            existing.drawInstanced = true;

            if (fresh || data.detailPrototypes.Length == 0) SeedDetails(data);

            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

            Debug.Log($"[TerrainPass] Terrain {(fresh ? "dibuat" : "dipakai ulang")} di {DataPath} " +
                      $"— {Size.x}x{Size.y}, {data.terrainLayers.Length} lapisan. " +
                      "Pahat dan cat sepuasnya; pohon & rumpun otomatis mengikuti permukaannya.\n" +
                      "PENTING: pemain dan musuh masih berjalan di bidang y=0. Pahat bukitnya di " +
                      "LUAR arena (|x| > 40, |z| > 30) sampai gerak mereka ikut membaca ketinggian.");
        }

        /// <summary>
        /// Benar kalau belum ada yang mengecat: lapisan pertama penuh di mana-mana, sisanya nol.
        ///
        /// Ini yang menjaga tombolnya boleh ditekan berkali-kali. Cat pembuka berguna sekali, saat
        /// lantainya masih kosong; menimpakannya di atas lantai yang sudah digarap adalah cara
        /// menghapus pekerjaan orang dengan perintah yang namanya terdengar seperti tidak merusak
        /// apa pun.
        /// </summary>
        static bool Untouched(TerrainData data)
        {
            if (data.terrainLayers.Length < 2) return false;

            int res = data.alphamapResolution;
            var map = data.GetAlphamaps(0, 0, res, res);

            for (int y = 0; y < res; y += 4)
            {
                for (int x = 0; x < res; x += 4)
                {
                    if (map[y, x, 0] < 0.999f) return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Cat pembuka: rumput sebagai dasar, tanah sebagai bercak besar, batu sebagai bercak kecil.
        ///
        /// Lantai satu lapisan yang diubin rata terbaca sebagai kain hijau, bukan sebagai tanah —
        /// polanya berulang dan mata menemukan pengulangannya dalam sedetik. Bercak berskala besar
        /// memecah pengulangan itu sekaligus memberi lapangan tanda tempat.
        ///
        /// Ambangnya dicari dari SEBARAN deraunya sendiri. Membandingkan Perlin langsung dengan
        /// angka tetap seperti 0,7 menghasilkan nol bercak, karena PerlinNoise berkerumun di
        /// sekitar 0,5 dan nyaris tidak pernah sejauh itu — dan gagalnya diam: lantainya tetap
        /// tergambar, cuma satu warna.
        /// </summary>
        static void SeedPaint(TerrainData data)
        {
            int res = data.alphamapResolution;
            int layers = data.terrainLayers.Length;
            var map = new float[res, res, layers];

            // Tiga derau dengan skala DAN geseran berbeda, jadi ketiga bercak tidak pernah jatuh
            // di tempat yang sama. Satu derau dipakai bersama menghasilkan tepi tanah yang selalu
            // dikelilingi cincin terang — pola yang langsung terbaca sebagai buatan.
            var dirt = Patches(res, data.size.x, 30f, 0.22f, 0f);
            var sun = Patches(res, data.size.x, 17f, 0.34f, 211f);
            var shade = Patches(res, data.size.x, 23f, 0.3f, 407f);

            // Urutan lapisan: 0 rumput dasar, 1 rumput terang, 2 rumput teduh, 3 tanah.
            int sunSlot = layers > 3 ? 1 : -1;
            int shadeSlot = layers > 3 ? 2 : -1;
            int dirtSlot = layers - 1;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    int i = y * res + x;

                    // Tanah MENANG di tempat mereka bertemu — bidang terbuka memakan rumput, bukan
                    // sebaliknya. Lalu terang menang atas teduh, supaya keduanya tidak saling
                    // meniadakan jadi abu-abu di perbatasannya.
                    float d = dirt[i];
                    float s = sunSlot >= 0 ? Mathf.Max(0f, sun[i] - d) : 0f;
                    float h = shadeSlot >= 0 ? Mathf.Max(0f, shade[i] - d - s) : 0f;

                    map[y, x, 0] = Mathf.Max(0f, 1f - d - s - h);
                    if (sunSlot >= 0) map[y, x, sunSlot] = s;
                    if (shadeSlot >= 0) map[y, x, shadeSlot] = h;
                    if (dirtSlot > 0) map[y, x, dirtSlot] = d;
                }
            }

            data.SetAlphamaps(0, 0, map);
        }

        static float[] Patches(int res, float world, float scale, float coverage, float offset)
        {
            var noise = new float[res * res];

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = offset + x / (float)res * world / Mathf.Max(1f, scale);
                    float v = offset + y / (float)res * world / Mathf.Max(1f, scale);

                    // Dua oktaf: yang besar menentukan letak bercaknya, yang halus merusak tepinya.
                    // Satu oktaf saja menghasilkan bercak berbentuk awan yang terlalu rapi.
                    noise[y * res + x] = Mathf.PerlinNoise(u, v) * 0.75f
                                         + Mathf.PerlinNoise(u * 3.1f, v * 3.1f) * 0.25f;
                }
            }

            var sorted = (float[])noise.Clone();
            System.Array.Sort(sorted);

            int cut = Mathf.Clamp(Mathf.RoundToInt((1f - coverage) * (sorted.Length - 1)),
                0, sorted.Length - 1);

            float threshold = sorted[cut];
            float band = Mathf.Max(0.004f, (sorted[sorted.Length - 1] - sorted[0]) * 0.14f);

            for (int i = 0; i < noise.Length; i++)
            {
                noise[i] = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(threshold - band, threshold + band, noise[i]));
            }

            return noise;
        }

        /// <summary>Material terrain URP. Wajib eksplisit — lihat <see cref="TerrainMaterial"/>.</summary>
        static Material TerrainMaterial()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;

            // Mengosongkan materialTemplate TIDAK jatuh ke material URP. Ia jatuh ke material terrain
            // bawaan Unity, yang shader-nya tidak dikenali URP — dan lantainya jadi MAGENTA.
            //
            // Membuat material sendiri dari Shader.Find("...Terrain/Lit") juga tidak cukup: material
            // terrain yang dibuat lewat kode lahir tanpa keyword dan nilai bawaan yang dibutuhkan
            // shader-nya, dan hasilnya lantai yang tidak tergambar sama sekali — gagal yang lebih
            // jahat dari magenta, karena tidak terlihat sebagai kesalahan.
            if (pipeline != null && pipeline.defaultTerrainMaterial != null)
                return pipeline.defaultTerrainMaterial;

            Debug.LogError("[TerrainPass] render pipeline tidak punya material terrain bawaan.");
            return null;
        }

        /// <summary>
        /// Mendaftarkan rumput sebagai detail yang bisa DIKUAS, lalu menaburkan lapisan pembuka
        /// tipis supaya lantainya tidak berangkat dari gundul.
        ///
        /// Kepadatannya mengikuti CAT LANTAINYA: rapat di bagian yang dicat rumput, nol di bagian
        /// yang dicat tanah. Rumput yang tumbuh di atas petak tanah gundul adalah hal pertama yang
        /// membocorkan bahwa lantainya dihasilkan mesin, bukan disusun orang.
        /// </summary>
        static void SeedDetails(TerrainData data)
        {
            var prefab = DetailPrefab(Nature + "Environment/Vegetation/Grass/Prefabs/UNS_Grass.prefab",
                "Detail_Grass", 1);

            if (prefab == null) return;

            data.detailPrototypes = new[]
            {
                new DetailPrototype
                {
                    prototype = prefab,
                    usePrototypeMesh = true,
                    useInstancing = true,
                    renderMode = DetailRenderMode.VertexLit,
                    minWidth = 0.5f, maxWidth = 0.9f,
                    minHeight = 0.5f, maxHeight = 1f,
                    noiseSeed = 7, noiseSpread = 0.35f,
                    healthyColor = Color.white, dryColor = Color.white,
                    alignToGround = 0.6f,
                    positionJitter = 0.8f
                }
            };

            int res = data.detailResolution;
            int alpha = data.alphamapResolution;
            var splat = data.GetAlphamaps(0, 0, alpha, alpha);
            var layer = new int[res, res];

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    int ax = Mathf.Clamp(x * alpha / res, 0, alpha - 1);
                    int ay = Mathf.Clamp(y * alpha / res, 0, alpha - 1);

                    // SEMUA lapisan kecuali yang terakhir adalah rumput; yang terakhir tanah.
                    // Menghitung lapisan nol saja membuat seluruh bercak rumput terang dan teduh
                    // keluar gundul — dan gundulnya justru di bagian yang paling terlihat.
                    float grass = 0f;
                    for (int i = 0; i < data.terrainLayers.Length - 1; i++) grass += splat[ay, ax, i];

                    if (grass < 0.5f) continue;

                    // Derau kasar supaya kepadatannya bergelombang. Kepadatan rata di seluruh
                    // lantai terbaca sebagai karpet, dan karpet bukan rumput.
                    float n = Mathf.PerlinNoise(x * 0.045f, y * 0.045f);

                    // Dasarnya 0,45, bukan nol: cat pembuka harus sudah terbaca sebagai padang
                    // rumput begitu dibuka. Yang dikerjakan derau cuma menipiskan sebagian,
                    // bukan menentukan ada-tidaknya.
                    layer[y, x] = Mathf.RoundToInt(
                        Mathf.Clamp01(0.45f + (n - 0.4f) * 1.6f) * grass * DetailPeak);
                }
            }

            data.SetDetailLayer(0, 0, 0, layer);
        }

        /// <summary>
        /// Prefab satu-mesh untuk dipakai sistem detail terrain.
        ///
        /// Prefab aslinya membawa LODGroup dengan empat renderer, dan sistem detail tidak menangani
        /// LODGroup — ia mengambil mesh yang ditemukannya, dan yang ditemukan bukan yang dimaksud.
        /// Jadi satu mesh dari satu tingkat LOD disalin ke prefab tersendiri.
        /// </summary>
        static GameObject DetailPrefab(string sourcePath, string name, int lod)
        {
            string path = Folder + "/" + name + ".prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);

            if (source == null)
            {
                Debug.LogError("[TerrainPass] prefab rumput tidak ketemu: " + sourcePath);
                return null;
            }

            Renderer picked = null;
            var group = source.GetComponentInChildren<LODGroup>(true);

            if (group != null)
            {
                var levels = group.GetLODs();
                foreach (var r in levels[Mathf.Clamp(lod, 0, levels.Length - 1)].renderers)
                {
                    if (r != null) { picked = r; break; }
                }
            }

            if (picked == null) picked = source.GetComponentInChildren<MeshRenderer>(true);

            var filter = picked != null ? picked.GetComponent<MeshFilter>() : null;

            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogError("[TerrainPass] " + source.name + " tidak punya mesh di LOD " + lod);
                return null;
            }

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = picked.sharedMaterials;

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            return prefab;
        }

        /// <summary>
        /// Tiga lapisan bercat tangan: rumput dasar, rumput kena matahari, tanah liat.
        ///
        /// Lapisan DIBUAT SENDIRI di GameData, bukan memakai .terrainlayer bawaan paketnya. Yang
        /// dibutuhkan cuma teksturnya; ukuran ubinnya harus disetel untuk kamera ini, dan menyetel
        /// aset milik paket berarti mengubah file pihak ketiga — perubahan yang baru ketahuan saat
        /// muncul di diff, dan hilang begitu paketnya diperbarui.
        /// </summary>
        static TerrainLayer[] Layers()
        {
            var made = new System.Collections.Generic.List<TerrainLayer>(4);

            // TIGA lapisan rumput, dan itu bukan pemborosan — itu obatnya.
            //
            // Satu tekstur rumput yang diubin di lantai seluas ini memperlihatkan pengulangannya
            // dalam sedetik: bercak terang yang sama muncul dalam kisi yang rapi, dan begitu mata
            // menemukan kisinya ia tidak bisa lagi tidak melihatnya. Paket ini menyediakan empat
            // PUTARAN tiap tekstur justru untuk itu.
            //
            // Ukuran ubinnya sengaja tidak berkelipatan (14 / 6,5 / 9,5): kalau berkelipatan,
            // pengulangan ketiganya bertemu di titik yang sama dan kisinya kembali — cuma lebih
            // besar dan lebih sulit ditebak asalnya.
            Add(made, Layer("Layer_Grass", Painted + "Grass/Grass_normal/Grass_normal_up.png", 14f));
            Add(made, Layer("Layer_Grass_Sun", Painted + "Grass/Grass_lighted/Grass_lighted_right.png", 6.5f));
            Add(made, Layer("Layer_Grass_Shade", Painted + "Grass/Grass_darked/Grass_darked_down.png", 9.5f));
            Add(made, Layer("Layer_Dirt", Painted + "Dirt/dirt_clay/dirt_clay_up.png", 5f));

            return made.ToArray();
        }

        static void Add(System.Collections.Generic.List<TerrainLayer> list, TerrainLayer layer)
        {
            if (layer != null) list.Add(layer);
        }

        static TerrainLayer Layer(string name, string texturePath, float tile)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

            if (texture == null)
            {
                Debug.LogError("[TerrainPass] tekstur tidak ketemu: " + texturePath);
                return null;
            }

            string path = Folder + "/" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);

            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }

            layer.diffuseTexture = texture;
            layer.tileSize = new Vector2(tile, tile);
            layer.tileOffset = Vector2.zero;

            // Matte mutlak. Lantai seluas ini yang punya kilau sedikit saja langsung berubah jadi
            // lantai plastik begitu matahari rendah menyorotinya.
            layer.smoothness = 0f;
            layer.metallic = 0f;
            layer.specular = Color.black;

            EditorUtility.SetDirty(layer);
            return layer;
        }
    }
}
