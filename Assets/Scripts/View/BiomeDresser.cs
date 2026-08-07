using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Hutan tak berujung, dibangkitkan per PETAK di sekitar kamera.
    ///
    /// Isi tiap petak diturunkan dari koordinat petaknya sendiri lewat hash, bukan dari daftar
    /// yang disimpan. Artinya petak yang sama selalu menghasilkan pohon yang sama persis, berapa
    /// kali pun ia keluar-masuk jangkauan — pemain bisa berjalan sejauh apa pun lalu kembali, dan
    /// hutannya masih hutan yang sama.
    ///
    /// <b>Matriksnya dipanggang sekali, bukan tiap frame.</b> Pohon tidak berjalan; menyusun ulang
    /// ribuan matriks tiap frame demi hasil yang identik dengan frame sebelumnya adalah cara
    /// termudah menjatuhkan fps tanpa ada apa pun yang berubah di layar. Pembangunan ulang hanya
    /// terjadi saat pemain MENYEBERANG batas petak.
    /// </summary>
    public class BiomeDresser : MonoBehaviour
    {
        /// <summary>Sisi satu petak dalam unit dunia.</summary>
        const float ChunkSize = 24f;

        /// <summary>Berapa petak ke tiap arah yang dijaga tetap hidup di sekitar kamera.</summary>
        const int ChunkRadius = 2;

        struct Piece
        {
            public Matrix4x4 Matrix;

            /// <summary>Indeks warna untuk prop primitif, indeks jenis untuk prop bermesh.</summary>
            public int Tint;
        }

        class Chunk
        {
            public Piece[] Trunks;
            public Piece[] Canopies;
            public Piece[] Grass;
            public Piece[] MeshTrees;
            public Piece[] MeshScatter;
            public Piece[] MeshGrass;
            public int Seen;
        }

        GameBalance _balance;
        BiomeDefinition[] _biomes;
        SceneLook _look;
        Light _sun;
        Camera _camera;
        Renderer _ground;
        Transform _follow;

        /// <summary>
        /// Lantai yang sebenarnya, kalau ada. Ini yang menentukan ketinggian tiap prop, jadi
        /// lantai yang dipahat tangan langsung diikuti tanpa satu pun angka yang perlu disetel.
        /// </summary>
        Terrain _terrain;

        PropBatch _trunks;
        PropBatch _canopies;
        PropBatch _grass;

        // Satu batch per JENIS mesh, karena satu panggilan instanced hanya bisa menggambar satu
        // mesh. Itulah yang membatasi berapa banyak jenis yang layak dipakai: tiap jenis tambahan
        // adalah satu draw call tetap, berapa pun sedikitnya yang muncul di layar.
        MeshProp[] _meshTrees = System.Array.Empty<MeshProp>();
        PropBatch[] _meshTreeBatches = System.Array.Empty<PropBatch>();
        float[] _meshTreeWeights = System.Array.Empty<float>();

        MeshProp[] _meshScatter = System.Array.Empty<MeshProp>();
        PropBatch[] _meshScatterBatches = System.Array.Empty<PropBatch>();
        float[] _meshScatterWeights = System.Array.Empty<float>();

        MeshProp[] _meshGrass = System.Array.Empty<MeshProp>();
        PropBatch[] _meshGrassBatches = System.Array.Empty<PropBatch>();
        float[] _meshGrassWeights = System.Array.Empty<float>();

        /// <summary>
        /// Salinan peta detail terrain — apa yang dikuas orang lewat alat Paint Details.
        ///
        /// Disalin sekali, bukan dibaca per prop. <c>GetDetailLayer</c> mengalokasi array baru tiap
        /// panggilan, dan memanggilnya sekali per rumpun berarti ribuan alokasi tiap kali pemain
        /// menyeberang batas petak.
        /// </summary>
        int[,] _paint;
        int _paintWidth;
        int _paintHeight;
        float _paintPeak;

        readonly Dictionary<Vector2Int, Chunk> _chunks = new Dictionary<Vector2Int, Chunk>();
        readonly List<Vector2Int> _expired = new List<Vector2Int>();

        BiomeDefinition _current;
        int _frame;
        Vector2Int _centre = new Vector2Int(int.MinValue, int.MinValue);

        int _treesPerChunk;
        int _grassPerChunk;

        UnityEngine.Rendering.Volume _volume;

        /// <summary>
        /// Pergantian otomatis tiap beberapa wave. MATI, dan itu keputusan lama yang masih berlaku:
        /// wajah arena yang berganti sendiri tidak pernah sempat dikenali. Siang dan malam sekarang
        /// dua aset, tapi yang menggantinya tombol — bukan hitungan wave.
        /// </summary>
        public bool AutoCycle;

        public BiomeDefinition Current => _current;
        public int LoadedChunks => _chunks.Count;
        public int Faces => _biomes != null ? _biomes.Length : 0;

        public int FaceIndex
        {
            get
            {
                if (_biomes == null) return 0;

                for (int i = 0; i < _biomes.Length; i++)
                {
                    if (_biomes[i] == _current) return i;
                }

                return 0;
            }
        }

        /// <summary>Mengganti wajah arena sekarang juga. Nama wajahnya dikembalikan.</summary>
        public string Show(int index)
        {
            if (_biomes == null || _biomes.Length == 0) return "";

            var next = _biomes[((index % _biomes.Length) + _biomes.Length) % _biomes.Length];
            if (next != null && next != _current) Apply(next);

            return _current != null ? _current.DisplayName : "";
        }

        public int Batches =>
            (_trunks != null ? _trunks.Batches : 0) +
            (_canopies != null ? _canopies.Batches : 0) +
            (_grass != null ? _grass.Batches : 0) +
            Sum(_meshTreeBatches, true) +
            Sum(_meshScatterBatches, true) +
            Sum(_meshGrassBatches, true);

        public int PropCount =>
            (_trunks != null ? _trunks.Count : 0) +
            (_canopies != null ? _canopies.Count : 0) +
            (_grass != null ? _grass.Count : 0) +
            Sum(_meshTreeBatches, false) +
            Sum(_meshScatterBatches, false) +
            Sum(_meshGrassBatches, false);

        static int Sum(PropBatch[] batches, bool drawCalls)
        {
            int total = 0;

            for (int i = 0; i < batches.Length; i++)
            {
                if (batches[i] == null) continue;
                total += drawCalls ? batches[i].Batches : batches[i].Count;
            }

            return total;
        }

        ArenaLights _lights;
        Light _playerLight;
        Atmosphere _sky;
        Gloom _gloom;
        Weather _weather;
        Transform _player;
        GameObject _smoke;

        /// <summary>
        /// Segala sesuatu yang ikut berganti bersama wajah arena dititipkan ke sini. Semuanya boleh
        /// null — arena tanpa lampu atau tanpa awan tetap jalan.
        /// </summary>
        public void Attach(ArenaLights lights, Light playerLight, Atmosphere sky, Gloom gloom,
            Weather weather, Transform player)
        {
            _lights = lights;
            _playerLight = playerLight;
            _sky = sky;
            _gloom = gloom;
            _weather = weather;
            _player = player;

            if (_current != null) Relight(_current);
        }

        public void Init(GameBalance balance, BiomeDefinition[] biomes, Light sun, Camera camera,
            Renderer ground, SceneLook look, Transform follow)
        {
            _balance = balance;
            _biomes = biomes;
            _sun = sun;
            _camera = camera;
            _ground = ground;
            _look = look;
            _follow = follow;
            _terrain = Terrain.activeTerrain;
            _volume = FindFirstObjectByType<UnityEngine.Rendering.Volume>();

            if (_biomes == null || _biomes.Length == 0) return;

            Apply(_biomes[0]);
        }

        /// <summary>Mati kecuali <see cref="AutoCycle"/> dinyalakan — lihat catatannya di sana.</summary>
        public void OnWaveStarted(int wave)
        {
            // Cuaca diundi ulang tiap wave TERLEPAS dari AutoCycle. Yang dimatikan AutoCycle adalah
            // pergantian wajah arena — siang jadi malam sendiri — bukan cuaca. Wajah yang berganti
            // sendiri tidak pernah sempat dikenali; cuaca yang berganti justru sebaliknya.
            if (_weather != null) _weather.Roll(wave);

            if (!AutoCycle) return;
            if (_biomes == null || _biomes.Length <= 1) return;

            int every = Mathf.Max(1, _balance.BiomeEveryWaves);
            int index = Mathf.Max(0, wave - 1) / every % _biomes.Length;

            if (_biomes[index] == _current) return;

            Apply(_biomes[index]);
        }

        void Apply(BiomeDefinition biome)
        {
            if (biome == null) return;

            _current = biome;

            if (_ground != null) DressGround(biome);

            // Gradasi warna dan kabut ikut berganti bersama wajahnya. Malam yang memakai gradasi
            // siang tetap terbaca sebagai siang yang diredupkan, bukan sebagai malam.
            if (_volume != null && biome.PostProcess != null) _volume.sharedProfile = biome.PostProcess;

            // Langit sungguhan kalau ada. Warna rata di belakang lantai memaksa kabut mengerjakan
            // seluruh pembingkaian sendirian; skybox memberi kejauhan sesuatu untuk memudar ke
            // arahnya, dan itulah yang membuat lapangan terbaca punya udara di atasnya.
            if (biome.Skybox != null)
            {
                RenderSettings.skybox = biome.Skybox;

                if (_camera != null) _camera.clearFlags = CameraClearFlags.Skybox;
            }
            else if (_camera != null)
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = RenderColor.Of(biome.HorizonColor);
            }

            if (_sun != null)
            {
                // MENTAH, bukan lewat RenderColor. Unity sudah melinearkan warna lampu sendiri
                // (lightsUseLinearIntensity), jadi mengubahnya di sini berarti melinearkan dua kali:
                // matahari (1; 0,9; 0,68) sampai ke shader sebagai (1; 0,79; 0,41) — jauh lebih
                // jingga dari yang ditulis. Selama warnanya putih kesalahan ini tidak pernah
                // terlihat, karena putih tetap putih setelah dikonversi berapa kali pun.
                _sun.color = biome.SunColor;
                _sun.intensity = biome.SunIntensity;
                _sun.transform.rotation = Quaternion.Euler(biome.SunPitch, biome.SunYaw, 0f);
            }

            // Trilight, bukan ambientLight datar. SceneLook menyalakan mode ini, dan mengisi
            // ambientLight di mode Trilight tidak melakukan apa pun sama sekali.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = biome.AmbientSky;
            RenderSettings.ambientEquatorColor = biome.AmbientEquator;
            RenderSettings.ambientGroundColor = biome.AmbientGround;

            RenderSettings.fog = biome.FogEnabled;
            RenderSettings.fogMode = FogMode.Linear;
            // Mentah, sama seperti SceneLook.ApplyEnvironment. Dua tempat yang menulis field yang
            // sama dengan aturan konversi berbeda berarti kabutnya berubah warna tergantung siapa
            // yang menulis terakhir.
            RenderSettings.fogColor = biome.FogColor;
            RenderSettings.fogStartDistance = biome.FogStart;
            RenderSettings.fogEndDistance = biome.FogEnd;

            float referenceArea = Mathf.Max(1f, (_balance.ArenaHalfX * 2f) * (_balance.ArenaHalfZ * 2f));
            float chunkArea = ChunkSize * ChunkSize;

            // Kerapatan di aset masih ditulis untuk arena berbatas. Diubah jadi per-petak di sini,
            // supaya angka di aset tetap terbaca sebagai "seberapa rapat", bukan "berapa banyak".
            _treesPerChunk = Mathf.Max(0, Mathf.RoundToInt(biome.TreeCount * chunkArea / referenceArea));
            _grassPerChunk = Mathf.Max(0, Mathf.RoundToInt(biome.ScatterCount * chunkArea / referenceArea));

            _trunks = new PropBatch(EnemyRenderer.BorrowPrimitiveMesh(PrimitiveType.Cylinder),
                Safe(biome.TrunkColors, new Color(0.2f, 0.15f, 0.11f)), biome.TreeShadows);

            _canopies = new PropBatch(EnemyRenderer.BorrowPrimitiveMesh(PrimitiveType.Sphere),
                Safe(biome.CanopyColors, new Color(0.18f, 0.34f, 0.18f)), biome.TreeShadows);

            _grass = new PropBatch(EnemyRenderer.BorrowPrimitiveMesh(biome.ScatterShape),
                Safe(biome.ScatterColors, new Color(0.17f, 0.26f, 0.15f)));

            Collect(biome.MeshTrees, biome.TreeShadows,
                out _meshTrees, out _meshTreeBatches, out _meshTreeWeights);

            Collect(biome.MeshScatter, false,
                out _meshScatter, out _meshScatterBatches, out _meshScatterWeights);

            Collect(biome.MeshGrass, false,
                out _meshGrass, out _meshGrassBatches, out _meshGrassWeights);

            CachePaint();

            Relight(biome);

            _chunks.Clear();
            _centre = new Vector2Int(int.MinValue, int.MinValue);
        }

        void Relight(BiomeDefinition biome)
        {
            if (_lights != null) _lights.Apply(biome);
            if (_sky != null) _sky.Apply(biome);
            if (_gloom != null) _gloom.Apply(biome);
            if (_weather != null) _weather.Apply(biome);

            // Kantong udara bersih di sekitar pemain. Dibuat ulang tiap pergantian wajah karena
            // ukurannya boleh berbeda antara siang dan malam.
            if (_smoke != null) Destroy(_smoke);
            _smoke = null;

            if (biome.SmokePocket != null && _player != null)
            {
                _smoke = Instantiate(biome.SmokePocket, _player);
                _smoke.transform.localPosition = Vector3.zero;
                _smoke.transform.localRotation = Quaternion.identity;
            }

            if (_playerLight == null) return;

            _playerLight.color = biome.PlayerLightColor;
            _playerLight.intensity = biome.PlayerLightIntensity;
            _playerLight.range = biome.PlayerLightRange;

            // Dimatikan, bukan disetel nol. Lampu berintensitas nol tetap ikut dihitung tiap frame
            // oleh perayapan cahaya, dan di siang hari tidak ada satu pun yang membutuhkannya.
            _playerLight.enabled = biome.PlayerLightIntensity > 0f;

            var local = _playerLight.transform.localPosition;
            _playerLight.transform.localPosition = new Vector3(local.x, biome.PlayerLightHeight, local.z);
        }

        /// <summary>
        /// Menyaring entri yang tidak lengkap, lalu membangun satu batch per jenis.
        ///
        /// Entri kosong disaring di SINI, bukan di titik pemakaian. Daftarnya diisi generator dari
        /// path aset, dan satu path yang salah ketik menghasilkan entri tanpa mesh — kalau itu lolos
        /// sampai ke pengundian, hasilnya bukan error melainkan sebagian pohon yang diam-diam tidak
        /// muncul, yang terbaca sebagai hutan lebih jarang alih-alih sebagai kesalahan.
        /// </summary>
        static void Collect(MeshProp[] source, bool castShadows,
            out MeshProp[] props, out PropBatch[] batches, out float[] cumulative)
        {
            var kept = new List<MeshProp>();

            if (source != null)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] != null && source[i].Valid && source[i].Weight > 0f)
                        kept.Add(source[i]);
                }
            }

            props = kept.ToArray();
            batches = new PropBatch[props.Length];
            cumulative = new float[props.Length];

            float running = 0f;

            for (int i = 0; i < props.Length; i++)
            {
                batches[i] = new PropBatch(props[i].Mesh, props[i].Materials, castShadows);
                running += props[i].Weight;
                cumulative[i] = running;
            }

            // Dinormalkan supaya pengundiannya cukup satu Random.value di rentang 0–1, bukan
            // Random.Range yang bergantung pada total bobot — total itu berubah tiap kali daftarnya
            // disunting, dan bersamanya berubah pula seluruh hutan yang sudah disetujui.
            if (running <= 0f) return;

            for (int i = 0; i < cumulative.Length; i++) cumulative[i] /= running;
        }

        /// <summary>
        /// Menyalin SELURUH lapisan detail terrain jadi satu peta kepadatan.
        ///
        /// Seluruh lapisan dijumlahkan, bukan lapisan nol saja: yang ditanya peta ini adalah "di
        /// sini dikuas rumput atau tidak", dan orang yang mengecat tidak punya alasan menghafal
        /// prototipe mana yang kebetulan terpasang di slot nol.
        /// </summary>
        void CachePaint()
        {
            _paint = null;
            _paintPeak = 0f;

            if (_terrain == null || _terrain.terrainData == null) return;

            var data = _terrain.terrainData;
            int layers = data.detailPrototypes.Length;

            if (layers == 0) return;

            _paintWidth = data.detailWidth;
            _paintHeight = data.detailHeight;

            if (_paintWidth <= 0 || _paintHeight <= 0) return;

            _paint = new int[_paintHeight, _paintWidth];

            for (int layer = 0; layer < layers; layer++)
            {
                var slice = data.GetDetailLayer(0, 0, _paintWidth, _paintHeight, layer);

                for (int y = 0; y < _paintHeight; y++)
                {
                    for (int x = 0; x < _paintWidth; x++)
                    {
                        int total = _paint[y, x] + slice[y, x];
                        _paint[y, x] = total;

                        if (total > _paintPeak) _paintPeak = total;
                    }
                }
            }

            // Puncaknya diambil dari cat yang ADA, bukan dari batas maksimum sistemnya. Orang yang
            // menguas dengan kekuatan setengah di seluruh lantai bermaksud "rumput di mana-mana",
            // bukan "rumput setengah di mana-mana" — dan menormalkan ke batas sistem membuat
            // seluruh lapangan keluar separuh gundul tanpa sebab yang terlihat.
            if (_paintPeak <= 0f) _paint = null;
        }

        /// <summary>Kepadatan cat 0..1 di satu titik dunia. Satu kalau tidak ada peta sama sekali.</summary>
        float Painted(float x, float z)
        {
            if (_paint == null || _terrain == null) return 1f;

            var origin = _terrain.transform.position;
            var size = _terrain.terrainData.size;

            int px = Mathf.FloorToInt((x - origin.x) / Mathf.Max(0.001f, size.x) * _paintWidth);
            int pz = Mathf.FloorToInt((z - origin.z) / Mathf.Max(0.001f, size.z) * _paintHeight);

            if (px < 0 || pz < 0 || px >= _paintWidth || pz >= _paintHeight) return 0f;

            return _paint[pz, px] / _paintPeak;
        }

        static int Pick(float[] cumulative, float roll)
        {
            for (int i = 0; i < cumulative.Length; i++)
            {
                if (roll <= cumulative[i]) return i;
            }

            return cumulative.Length - 1;
        }

        /// <summary>
        /// Memberi lantai tekstur rumput yang dibangkitkan.
        ///
        /// Bidang satu warna tidak akan pernah terbaca sebagai rumput, seberapa pun tepat
        /// warnanya — yang hilang bukan warnanya, tapi variasi rapatnya. Mata membaca rumput dari
        /// bintik-bintik kecil yang tak beraturan, bukan dari hijau.
        ///
        /// Dua skala digabung dengan sengaja: bercak besar memberi lapangan bentuk yang bisa
        /// dipakai sebagai penanda tempat, bintik halus mengisi ruang di antaranya. Satu skala
        /// saja terbaca sebagai kain, bukan tanah.
        /// </summary>
        void DressGround(BiomeDefinition biome)
        {
            var material = _look.CreateSurface(biome.GroundColor);
            material.SetTexture(BaseMap, GrassTexture(biome));

            // Satu ubin kira-kira 6 unit dunia. Lebih besar dan bintiknya jadi bercak; lebih kecil
            // dan polanya berubah jadi desis yang berkedip saat kamera bergerak.
            float span = _ground.transform.localScale.x * 10f;
            material.SetTextureScale(BaseMap, Vector2.one * Mathf.Max(1f, span / 6f));

            _ground.sharedMaterial = material;
        }

        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        static Texture2D GrassTexture(BiomeDefinition biome)
        {
            const int size = 256;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };

            var pixels = new Color32[size * size];

            // Dibangun sebagai PENGALI di sekitar 1, bukan sebagai warna. Warna aslinya tetap
            // datang dari GroundColor lewat _BaseColor, jadi mengganti warna biome tidak menuntut
            // membangkitkan ulang teksturnya.
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;

                    float patches = Tile(u, v, 3f, 3f);
                    float blades = Tile(u, v, 26f, 26f);
                    float speckle = Tile(u, v, 64f, 64f);

                    float value = 0.78f + patches * 0.3f + (blades - 0.5f) * 0.34f
                                  + (speckle - 0.5f) * 0.16f;

                    value = Mathf.Clamp01(value);

                    // Hijau digeser sedikit lebih kuat dari merah dan biru, jadi bagian yang lebih
                    // terang condong ke hijau muda alih-alih sekadar lebih putih.
                    byte r = (byte)(Mathf.Clamp01(value * 0.92f) * 255f);
                    byte g = (byte)(Mathf.Clamp01(value * 1.05f) * 255f);
                    byte b = (byte)(Mathf.Clamp01(value * 0.85f) * 255f);

                    pixels[y * size + x] = new Color32(r, g, b, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Perlin yang bisa diubin. Tanpa ini jahitannya terlihat sebagai garis lurus.</summary>
        static float Tile(float u, float v, float fx, float fy)
        {
            float a = Mathf.PerlinNoise(u * fx, v * fy);
            float b = Mathf.PerlinNoise((u - 1f) * fx, v * fy);
            float c = Mathf.PerlinNoise(u * fx, (v - 1f) * fy);
            float d = Mathf.PerlinNoise((u - 1f) * fx, (v - 1f) * fy);

            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        // =====================================================================================
        //  petak
        // =====================================================================================

        /// <summary>
        /// Isi satu petak, ditentukan sepenuhnya oleh koordinat petaknya.
        ///
        /// <c>Random.state</c> DIPULIHKAN setelahnya. Tanpa itu, membangkitkan satu petak akan
        /// menggeser seluruh keacakan game — jenis musuh, sebaran drop, arah semburan skill — dan
        /// semuanya jadi bergantung pada ke mana pemain kebetulan berjalan.
        /// </summary>
        Chunk Generate(Vector2Int coord, BiomeDefinition biome)
        {
            var state = Random.state;
            Random.InitState(coord.x * 73856093 ^ coord.y * 19349663);

            var trunks = new List<Piece>(_treesPerChunk * 2);
            var canopies = new List<Piece>(_treesPerChunk * 2);
            var grass = new List<Piece>(_grassPerChunk);
            var meshTrees = new List<Piece>(_treesPerChunk * 2);
            var meshScatter = new List<Piece>(_grassPerChunk);
            var meshGrass = new List<Piece>(biome.GrassPerChunk);

            int trunkColors = Safe(biome.TrunkColors, Color.gray).Length;
            int canopyColors = Safe(biome.CanopyColors, Color.green).Length;
            int grassColors = Safe(biome.ScatterColors, Color.green).Length;

            float baseX = coord.x * ChunkSize;
            float baseZ = coord.y * ChunkSize;
            float clearSqr = biome.ClearingRadius * biome.ClearingRadius;

            // Dicoba tiga kali lipat, lalu disaring. Yang di LUAR arena selalu lolos; yang di
            // dalam cuma sepertiganya. Hasilnya rimbun di luar dan lapang di dalam — dan itulah
            // yang membuat dinding arena terbaca sebagai batas pohon, bukan sebagai batas tak
            // kasat mata tempat kontrol tiba-tiba macet.
            for (int i = 0; i < _treesPerChunk * 3; i++)
            {
                float x = baseX + Random.Range(0f, ChunkSize);
                float z = baseZ + Random.Range(0f, ChunkSize);

                float height = Random.Range(biome.TrunkHeightRange.x, biome.TrunkHeightRange.y);
                float width = Random.Range(biome.TrunkWidthRange.x, biome.TrunkWidthRange.y);
                float canopy = height * Random.Range(biome.CanopyWidthRatio.x, biome.CanopyWidthRatio.y);
                float yaw = Random.Range(0f, 360f);
                int trunkTint = Random.Range(0, trunkColors);
                int canopyTint = Random.Range(0, canopyColors);
                float roll = Random.value;

                // Diundi di sini, sebelum penyaringan apa pun, bukan di dalam cabangnya. Nilai acak
                // yang diambil hanya kadang-kadang menggeser seluruh urutan acak petak ini, dan
                // petak yang sama berhenti menghasilkan hutan yang sama.
                float mix = Random.value;
                float species = Random.value;

                // Halaman kosong cuma berlaku di sekitar titik nol — di situlah run dimulai, dan
                // wave pertama tidak boleh dibuka dengan pandangan terhalang batang pohon.
                // Nilai acaknya tetap diambil di atas supaya urutan acak petak ini tidak bergeser.
                if (x * x + z * z < clearSqr) continue;

                float nx = x / Mathf.Max(0.01f, _balance.ArenaHalfX);
                float nz = z / Mathf.Max(0.01f, _balance.ArenaHalfZ);

                if (nx * nx + nz * nz <= 1f && roll > 0.34f) continue;

                // Pohon bermesh MENGGANTIKAN slotnya, bukan menambah slot baru. Tingginya pun
                // diambil dari undian yang sama, jadi keduanya berdiri sama tinggi dan yang
                // dibandingkan benar-benar bentuknya.
                float floor = GroundY(x, z);

                if (_meshTrees.Length > 0 && mix < biome.MeshTreeShare)
                {
                    meshTrees.Add(Place(_meshTrees, _meshTreeWeights, species, height, true,
                        x, floor, z, Quaternion.Euler(0f, yaw, 0f)));

                    continue;
                }

                // Silinder bawaan Unity tingginya 2 unit, jadi skala Y-nya setengah tinggi asli.
                trunks.Add(new Piece
                {
                    Matrix = Matrix4x4.TRS(new Vector3(x, floor + height * 0.5f, z),
                        Quaternion.Euler(0f, yaw, 0f), new Vector3(width, height * 0.5f, width)),
                    Tint = trunkTint
                });

                // Tajuk duduk sedikit di bawah puncak batangnya. Persis di puncak menyisakan celah
                // yang terbaca sebagai bola melayang, bukan sebagai pohon.
                canopies.Add(new Piece
                {
                    Matrix = Matrix4x4.TRS(new Vector3(x, floor + height * 0.92f, z),
                        Quaternion.Euler(0f, yaw, 0f),
                        new Vector3(canopy, canopy * biome.CanopyFlatten, canopy)),
                    Tint = canopyTint
                });
            }

            for (int i = 0; i < _grassPerChunk; i++)
            {
                float x = baseX + Random.Range(0f, ChunkSize);
                float z = baseZ + Random.Range(0f, ChunkSize);
                float size = Random.Range(biome.ScatterScaleRange.x, biome.ScatterScaleRange.y);
                float yaw = Random.Range(0f, 360f);
                int tint = Random.Range(0, grassColors);
                float mix = Random.value;
                float species = Random.value;
                float width = Random.Range(biome.MeshScatterWidth.x, biome.MeshScatterWidth.y);

                float floor = GroundY(x, z);

                if (_meshScatter.Length > 0 && mix < biome.MeshScatterShare)
                {
                    // Rumput MENGIKUTI kemiringan, pohon tidak. Rumpun yang tetap tegak di lereng
                    // berdiri dengan pangkalnya menggantung di udara di sisi menurun; pohon yang
                    // dimiringkan mengikuti lereng terbaca sebagai pohon yang mau tumbang.
                    meshScatter.Add(Place(_meshScatter, _meshScatterWeights, species, width, false,
                        x, floor, z, GroundTilt(x, z) * Quaternion.Euler(0f, yaw, 0f)));

                    continue;
                }

                grass.Add(new Piece
                {
                    Matrix = Matrix4x4.TRS(
                        new Vector3(x, floor + size * biome.ScatterFlatten * 0.5f, z),
                        Quaternion.Euler(0f, yaw, 0f),
                        new Vector3(size, size * biome.ScatterFlatten, size)),
                    Tint = tint
                });
            }

            // Rumput, dan cuma di tempat yang DIKUAS.
            //
            // Tiap calon diundi lalu diterima dengan peluang sebesar kepadatan cat di titiknya.
            // Itulah yang membuat kuas berarti: tempat yang dikuas penuh dapat semuanya, yang
            // dikuas tipis dapat sebagian, yang tidak disentuh tetap kosong — dan pinggirannya
            // meluruh sendiri alih-alih berhenti di garis lurus.
            for (int i = 0; i < biome.GrassPerChunk; i++)
            {
                float x = baseX + Random.Range(0f, ChunkSize);
                float z = baseZ + Random.Range(0f, ChunkSize);
                float yaw = Random.Range(0f, 360f);
                float species = Random.value;
                float width = Random.Range(biome.GrassWidth.x, biome.GrassWidth.y);
                float accept = Random.value;

                if (_meshGrass.Length == 0) continue;
                if (accept > Painted(x, z)) continue;
                if (x * x + z * z < clearSqr) continue;

                meshGrass.Add(Place(_meshGrass, _meshGrassWeights, species, width, false,
                    x, GroundY(x, z), z, GroundTilt(x, z) * Quaternion.Euler(0f, yaw, 0f)));
            }

            Random.state = state;

            return new Chunk
            {
                Trunks = trunks.ToArray(),
                Canopies = canopies.ToArray(),
                Grass = grass.ToArray(),
                MeshTrees = meshTrees.ToArray(),
                MeshScatter = meshScatter.ToArray(),
                MeshGrass = meshGrass.ToArray()
            };
        }

        /// <summary>
        /// Mendudukkan satu prop bermesh di tanah pada ukuran jadi yang diminta.
        ///
        /// Skalanya diturunkan dari UKURAN MESH-nya sendiri, bukan dari angka yang disimpan. Mesh
        /// dari paket aset datang dengan tinggi 9 sampai 28 unit dan lebar 0,9 sampai 3,4 — pengali
        /// yang pas untuk satu di antaranya salah total untuk yang lain, dan menyetelnya satu per
        /// satu berarti tiap penambahan jenis baru menuntut penyetelan ulang.
        ///
        /// Titik nol mesh-nya juga tidak bisa dipercaya duduk di pangkal: sebagian mengambang di
        /// atas nol, sebagian tenggelam di bawahnya. <c>bounds.min.y</c> yang menentukan, bukan
        /// asumsi.
        /// </summary>
        static Piece Place(MeshProp[] props, float[] weights, float species, float target,
            bool byHeight, float x, float floor, float z, Quaternion rotation)
        {
            int kind = Pick(weights, species);

            var bounds = props[kind].Mesh.bounds;
            float native = byHeight
                ? bounds.size.y
                : Mathf.Max(bounds.size.x, bounds.size.z);

            float scale = target / Mathf.Max(0.001f, native) * props[kind].SizeMultiplier;

            return new Piece
            {
                Matrix = Matrix4x4.TRS(new Vector3(x, floor - bounds.min.y * scale, z),
                    rotation, Vector3.one * scale),
                Tint = kind
            };
        }

        /// <summary>
        /// Ketinggian lantai di satu titik. Nol kalau tidak ada terrain — dan nol memang jawaban
        /// yang benar untuk bidang datar.
        /// </summary>
        float GroundY(float x, float z)
        {
            if (_terrain == null) return 0f;

            // SampleHeight mengembalikan tinggi RELATIF terhadap posisi terrain-nya, bukan tinggi
            // dunia. Terrain yang digeser ke atas tanpa penambahan ini menanam seluruh hutannya
            // di bawah tanah.
            return _terrain.SampleHeight(new Vector3(x, 0f, z)) + _terrain.transform.position.y;
        }

        /// <summary>Kemiringan lantai di satu titik, sebagai rotasi dari tegak lurus.</summary>
        Quaternion GroundTilt(float x, float z)
        {
            if (_terrain == null) return Quaternion.identity;

            var data = _terrain.terrainData;
            var origin = _terrain.transform.position;

            float u = Mathf.Clamp01((x - origin.x) / Mathf.Max(0.001f, data.size.x));
            float v = Mathf.Clamp01((z - origin.z) / Mathf.Max(0.001f, data.size.z));

            return Quaternion.FromToRotation(Vector3.up, data.GetInterpolatedNormal(u, v));
        }

        static Color[] Safe(Color[] colors, Color fallback) =>
            colors != null && colors.Length > 0 ? colors : new[] { fallback };

        // =====================================================================================
        //  gambar
        // =====================================================================================

        void LateUpdate()
        {
            if (_current == null || _follow == null || _grass == null) return;

            Vector3 at = _follow.position;
            var centre = new Vector2Int(
                Mathf.FloorToInt(at.x / ChunkSize), Mathf.FloorToInt(at.z / ChunkSize));

            // Inilah seluruh optimasinya: selama pemain masih di petak yang sama, tidak ada satu
            // pun matriks yang disusun ulang — cuma tiga panggilan gambar dari buffer yang sudah
            // ada. Menyeberang batas petak baru memicu pembangunan ulang.
            if (centre != _centre)
            {
                _centre = centre;
                Rebuild(centre);
            }

            _trunks.Draw();
            _canopies.Draw();
            _grass.Draw();

            for (int i = 0; i < _meshTreeBatches.Length; i++) _meshTreeBatches[i].Draw();
            for (int i = 0; i < _meshScatterBatches.Length; i++) _meshScatterBatches[i].Draw();
            for (int i = 0; i < _meshGrassBatches.Length; i++) _meshGrassBatches[i].Draw();
        }

        void Rebuild(Vector2Int centre)
        {
            _frame++;

            _trunks.BeginRebuild();
            _canopies.BeginRebuild();
            _grass.BeginRebuild();

            for (int i = 0; i < _meshTreeBatches.Length; i++) _meshTreeBatches[i].BeginRebuild();
            for (int i = 0; i < _meshScatterBatches.Length; i++) _meshScatterBatches[i].BeginRebuild();
            for (int i = 0; i < _meshGrassBatches.Length; i++) _meshGrassBatches[i].BeginRebuild();

            for (int z = centre.y - ChunkRadius; z <= centre.y + ChunkRadius; z++)
            {
                for (int x = centre.x - ChunkRadius; x <= centre.x + ChunkRadius; x++)
                {
                    var coord = new Vector2Int(x, z);

                    if (!_chunks.TryGetValue(coord, out var chunk))
                    {
                        chunk = Generate(coord, _current);
                        _chunks[coord] = chunk;
                    }

                    chunk.Seen = _frame;

                    Feed(_trunks, chunk.Trunks);
                    Feed(_canopies, chunk.Canopies);
                    Feed(_grass, chunk.Grass);

                    Scatter(_meshTreeBatches, chunk.MeshTrees);
                    Scatter(_meshScatterBatches, chunk.MeshScatter);
                    Scatter(_meshGrassBatches, chunk.MeshGrass);
                }
            }

            _trunks.EndRebuild();
            _canopies.EndRebuild();
            _grass.EndRebuild();

            for (int i = 0; i < _meshTreeBatches.Length; i++) _meshTreeBatches[i].EndRebuild();
            for (int i = 0; i < _meshScatterBatches.Length; i++) _meshScatterBatches[i].EndRebuild();
            for (int i = 0; i < _meshGrassBatches.Length; i++) _meshGrassBatches[i].EndRebuild();

            Evict();
        }

        static void Feed(PropBatch batch, Piece[] pieces)
        {
            for (int i = 0; i < pieces.Length; i++) batch.Add(pieces[i].Tint, pieces[i].Matrix);
        }

        /// <summary>
        /// Prop bermesh dipisah ke batch MILIKNYA SENDIRI, karena satu panggilan instanced hanya
        /// menggambar satu mesh. <c>Tint</c> di sini berarti jenis, bukan warna.
        ///
        /// Petak yang dibangkitkan sebelum daftarnya berubah bisa membawa indeks yang sekarang di
        /// luar jangkauan. Itu dijaga di sini — di luar jangkauan berarti prop-nya dilewati, bukan
        /// pengecualian yang mematikan seluruh pembangunan petak dan menghapus hutannya.
        /// </summary>
        static void Scatter(PropBatch[] batches, Piece[] pieces)
        {
            if (pieces == null) return;

            for (int i = 0; i < pieces.Length; i++)
            {
                int kind = pieces[i].Tint;
                if (kind < 0 || kind >= batches.Length) continue;

                batches[kind].Add(0, pieces[i].Matrix);
            }
        }

        /// <summary>
        /// Membuang petak yang tidak tersentuh pembangunan terakhir. Inilah yang membuat biaya
        /// memorinya ditentukan luas layar, bukan seberapa jauh pemain sudah berjalan.
        /// </summary>
        void Evict()
        {
            _expired.Clear();

            foreach (var pair in _chunks)
            {
                if (pair.Value.Seen != _frame) _expired.Add(pair.Key);
            }

            for (int i = 0; i < _expired.Count; i++) _chunks.Remove(_expired[i]);
        }
    }
}
