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
            public int Tint;
        }

        class Chunk
        {
            public Piece[] Trunks;
            public Piece[] Canopies;
            public Piece[] Grass;
            public int Seen;
        }

        GameBalance _balance;
        BiomeDefinition[] _biomes;
        SceneLook _look;
        Light _sun;
        Camera _camera;
        Renderer _ground;
        Transform _follow;

        PropBatch _trunks;
        PropBatch _canopies;
        PropBatch _grass;

        readonly Dictionary<Vector2Int, Chunk> _chunks = new Dictionary<Vector2Int, Chunk>();
        readonly List<Vector2Int> _expired = new List<Vector2Int>();

        BiomeDefinition _current;
        int _frame;
        Vector2Int _centre = new Vector2Int(int.MinValue, int.MinValue);

        int _treesPerChunk;
        int _grassPerChunk;

        public BiomeDefinition Current => _current;
        public int LoadedChunks => _chunks.Count;

        public int Batches =>
            (_trunks != null ? _trunks.Batches : 0) +
            (_canopies != null ? _canopies.Batches : 0) +
            (_grass != null ? _grass.Batches : 0);

        public int PropCount =>
            (_trunks != null ? _trunks.Count : 0) +
            (_canopies != null ? _canopies.Count : 0) +
            (_grass != null ? _grass.Count : 0);

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

            if (_biomes == null || _biomes.Length == 0) return;

            Apply(_biomes[0]);
        }

        /// <summary>Dengan satu biome ini tidak melakukan apa pun, dan memang itu yang diinginkan.</summary>
        public void OnWaveStarted(int wave)
        {
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

            if (_ground != null) _ground.sharedMaterial = _look.CreateSurface(biome.GroundColor);
            if (_camera != null) _camera.backgroundColor = RenderColor.Of(biome.HorizonColor);

            if (_sun != null)
            {
                _sun.color = RenderColor.Of(biome.SunColor);
                _sun.intensity = biome.SunIntensity;
                _sun.transform.rotation = Quaternion.Euler(biome.SunPitch, biome.SunYaw, 0f);
            }

            RenderSettings.ambientLight = RenderColor.Of(biome.AmbientColor);

            float referenceArea = Mathf.Max(1f, (_balance.ArenaHalfX * 2f) * (_balance.ArenaHalfZ * 2f));
            float chunkArea = ChunkSize * ChunkSize;

            // Kerapatan di aset masih ditulis untuk arena berbatas. Diubah jadi per-petak di sini,
            // supaya angka di aset tetap terbaca sebagai "seberapa rapat", bukan "berapa banyak".
            _treesPerChunk = Mathf.Max(0, Mathf.RoundToInt(biome.TreeCount * chunkArea / referenceArea));
            _grassPerChunk = Mathf.Max(0, Mathf.RoundToInt(biome.ScatterCount * chunkArea / referenceArea));

            _trunks = new PropBatch(EnemyRenderer.BorrowPrimitiveMesh(PrimitiveType.Cylinder),
                Safe(biome.TrunkColors, new Color(0.2f, 0.15f, 0.11f)));

            _canopies = new PropBatch(EnemyRenderer.BorrowPrimitiveMesh(PrimitiveType.Sphere),
                Safe(biome.CanopyColors, new Color(0.18f, 0.34f, 0.18f)));

            _grass = new PropBatch(EnemyRenderer.BorrowPrimitiveMesh(biome.ScatterShape),
                Safe(biome.ScatterColors, new Color(0.17f, 0.26f, 0.15f)));

            _chunks.Clear();
            _centre = new Vector2Int(int.MinValue, int.MinValue);
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

                // Halaman kosong cuma berlaku di sekitar titik nol — di situlah run dimulai, dan
                // wave pertama tidak boleh dibuka dengan pandangan terhalang batang pohon.
                // Nilai acaknya tetap diambil di atas supaya urutan acak petak ini tidak bergeser.
                if (x * x + z * z < clearSqr) continue;

                float nx = x / Mathf.Max(0.01f, _balance.ArenaHalfX);
                float nz = z / Mathf.Max(0.01f, _balance.ArenaHalfZ);

                if (nx * nx + nz * nz <= 1f && roll > 0.34f) continue;

                // Silinder bawaan Unity tingginya 2 unit, jadi skala Y-nya setengah tinggi asli.
                trunks.Add(new Piece
                {
                    Matrix = Matrix4x4.TRS(new Vector3(x, height * 0.5f, z),
                        Quaternion.Euler(0f, yaw, 0f), new Vector3(width, height * 0.5f, width)),
                    Tint = trunkTint
                });

                // Tajuk duduk sedikit di bawah puncak batangnya. Persis di puncak menyisakan celah
                // yang terbaca sebagai bola melayang, bukan sebagai pohon.
                canopies.Add(new Piece
                {
                    Matrix = Matrix4x4.TRS(new Vector3(x, height * 0.92f, z),
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

                grass.Add(new Piece
                {
                    Matrix = Matrix4x4.TRS(new Vector3(x, size * biome.ScatterFlatten * 0.5f, z),
                        Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                        new Vector3(size, size * biome.ScatterFlatten, size)),
                    Tint = Random.Range(0, grassColors)
                });
            }

            Random.state = state;

            return new Chunk
            {
                Trunks = trunks.ToArray(),
                Canopies = canopies.ToArray(),
                Grass = grass.ToArray()
            };
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
        }

        void Rebuild(Vector2Int centre)
        {
            _frame++;

            _trunks.BeginRebuild();
            _canopies.BeginRebuild();
            _grass.BeginRebuild();

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
                }
            }

            _trunks.EndRebuild();
            _canopies.EndRebuild();
            _grass.EndRebuild();

            Evict();
        }

        static void Feed(PropBatch batch, Piece[] pieces)
        {
            for (int i = 0; i < pieces.Length; i++) batch.Add(pieces[i].Tint, pieces[i].Matrix);
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
