using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Proto
{
    /// <summary>
    /// Menggambar benda-benda yang TIDAK PERNAH BERGERAK, dan menyusun matriksnya sekali saja.
    ///
    /// <see cref="EnemyRenderer"/> menyusun ulang seluruh matriks tiap frame, dan untuk swarm itu
    /// memang wajib — mereka berjalan. Pohon dan rumput tidak. Memakai jalur yang sama untuk
    /// keduanya berarti membayar lima ribu perkalian matriks per frame demi hasil yang identik
    /// dengan frame sebelumnya, dan itu terbaca sebagai fps yang jatuh tanpa ada yang terjadi
    /// di layar.
    ///
    /// Di sini matriksnya dipanggang sekali dan cuma dibangun ulang saat isi petaknya berubah —
    /// yaitu saat pemain menyeberang batas petak, bukan tiap frame.
    /// </summary>
    public class PropBatch
    {
        /// <summary>Hard ceiling of Graphics.RenderMeshInstanced. Bigger buckets are split.</summary>
        const int MaxPerDraw = 1023;

        readonly Mesh _mesh;

        // Tidak readonly: keduanya diisi lewat Allocate, dan readonly di C# hanya boleh ditulis di
        // badan konstruktornya sendiri — bukan di metode yang dipanggil dari sana.
        RenderParams[] _params;
        List<Matrix4x4>[] _building;
        Matrix4x4[][] _baked;

        /// <summary>Submesh yang digambar tiap material. Nol untuk semua di mode palet warna.</summary>
        int[] _submesh;

        /// <summary>
        /// Benar kalau seluruh material menggambar KUMPULAN MATRIKS YANG SAMA, masing-masing pada
        /// submesh-nya sendiri — itulah satu benda yang tersusun dari beberapa bahan. Salah kalau
        /// tiap material punya kumpulan matriksnya sendiri, yaitu satu bentuk yang diulang dalam
        /// beberapa warna.
        /// </summary>
        bool _shared;

        public int Batches { get; private set; }
        public int Count { get; private set; }

        /// <summary>Bentuk primitif yang diwarnai dari palet: satu bucket per warna.</summary>
        public PropBatch(Mesh mesh, Color[] palette, bool castShadows = false)
        {
            _mesh = mesh;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var materials = new Material[palette.Length];

            for (int i = 0; i < palette.Length; i++)
            {
                var material = new Material(shader) { enableInstancing = true };
                material.SetColor("_BaseColor", palette[i]);
                material.SetColor("_Color", palette[i]);

                // Matte, dan ini WAJIB disetel eksplisit. URP/Lit lahir dengan smoothness 0,5,
                // jadi tiap material yang dibuat lewat kode tanpa menyentuhnya akan mengkilap
                // seperti plastik — dan sorotan spekular di atas ribuan rumpun rumput adalah
                // hal pertama yang menghancurkan kesan bergaya ilustrasi.
                material.SetFloat("_Smoothness", 0f);
                material.SetFloat("_Glossiness", 0f);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_SpecularHighlights", 0f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");

                materials[i] = material;
            }

            Allocate(materials.Length, false);
            Setup(materials, castShadows);
        }

        /// <summary>
        /// Mesh siap pakai dengan materialnya sendiri — pohon dan rumpun dari paket aset.
        ///
        /// Satu material per submesh, berurutan. Ini yang membuat satu pohon tetap satu benda:
        /// batang dan daunnya digambar dari kumpulan matriks yang sama, jadi mustahil salah satunya
        /// muncul tanpa yang lain.
        ///
        /// Materialnya DISALIN, tidak dipakai langsung. <c>RenderMeshInstanced</c> menuntut
        /// <c>enableInstancing</c> menyala, dan menyalakannya pada aset .mat asli akan menandai
        /// file itu kotor lalu ikut tersimpan — perubahan diam-diam pada paket pihak ketiga yang
        /// baru ketahuan saat file-nya muncul di diff.
        /// </summary>
        public PropBatch(Mesh mesh, Material[] sources, bool castShadows = false)
        {
            _mesh = mesh;

            // Dijepit ke jumlah submesh yang benar-benar ada. Material berlebih menggambar submesh
            // yang tidak ada, dan itu melempar tiap frame alih-alih sekali saat disiapkan.
            int count = Mathf.Clamp(sources.Length, 1, Mathf.Max(1, mesh.subMeshCount));

            Allocate(count, true);

            var copies = new Material[count];

            for (int i = 0; i < count; i++)
            {
                var source = sources[i] != null ? sources[i] : sources[0];
                copies[i] = new Material(source) { enableInstancing = true };
                _submesh[i] = i;
            }

            Setup(copies, castShadows);
        }

        void Allocate(int count, bool shared)
        {
            _params = new RenderParams[count];
            _building = new List<Matrix4x4>[count];
            _baked = new Matrix4x4[count][];
            _submesh = new int[count];
            _shared = shared;
        }

        void Setup(Material[] materials, bool castShadows)
        {
            for (int i = 0; i < materials.Length; i++)
            {
                _params[i] = new RenderParams(materials[i])
                {
                    // Bayangan rumput MATI. Tiap rumpun yang melempar bayangan berarti satu lagi
                    // objek yang harus digambar ulang ke peta bayangan tiap frame, dan ribuan
                    // rumpun membuat pass bayangan lebih mahal dari seluruh sisa layarnya.
                    // Bayangan panjang dari matahari rendah tetap ada — dari pemain dan musuh,
                    // yang jumlahnya ratusan, bukan ribuan. Pohon boleh minta pengecualian:
                    // jumlahnya ratusan, bukan ribuan, dan bayangannyalah yang memisahkan pohon
                    // dari lantai.
                    shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,

                    // Sengaja besar sekali: lapangannya tak berujung, dan kotak sebesar arena akan
                    // membuat seluruh batch hilang begitu pemain berjalan cukup jauh.
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 100000f)
                };

                _building[i] = new List<Matrix4x4>(256);
                _baked[i] = new Matrix4x4[0];
            }
        }

        public void BeginRebuild()
        {
            for (int i = 0; i < _building.Length; i++) _building[i].Clear();
        }

        public void Add(int tint, Matrix4x4 matrix)
        {
            // Satu benda dari beberapa bahan hanya punya SATU daftar matriks. Menaruhnya di daftar
            // per material berarti tiap submesh digambar di tempat yang berbeda.
            if (_shared || tint < 0 || tint >= _building.Length) tint = 0;

            _building[tint].Add(matrix);
        }

        public void EndRebuild()
        {
            Count = 0;

            for (int i = 0; i < _building.Length; i++)
            {
                int needed = _building[i].Count;
                Count += needed;

                // Array-nya tidak pernah menyusut. Jumlah props per petak praktis tetap, jadi
                // setelah beberapa penyeberangan pertama tidak ada alokasi sama sekali.
                if (_baked[i].Length < needed) _baked[i] = new Matrix4x4[Mathf.Max(64, needed * 2)];

                _building[i].CopyTo(0, _baked[i], 0, needed);
            }
        }

        public void Draw()
        {
            Batches = 0;

            for (int i = 0; i < _params.Length; i++)
            {
                int bucket = _shared ? 0 : i;

                int remaining = _building[bucket].Count;
                int start = 0;

                while (remaining > 0)
                {
                    int chunk = Mathf.Min(remaining, MaxPerDraw);

                    Graphics.RenderMeshInstanced(_params[i], _mesh, _submesh[i],
                        _baked[bucket], chunk, start);

                    Batches++;

                    start += chunk;
                    remaining -= chunk;
                }
            }
        }
    }
}
