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
        readonly RenderParams[] _params;
        readonly List<Matrix4x4>[] _building;
        readonly Matrix4x4[][] _baked;

        public int Batches { get; private set; }
        public int Count { get; private set; }

        public PropBatch(Mesh mesh, Color[] palette)
        {
            _mesh = mesh;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            _params = new RenderParams[palette.Length];
            _building = new List<Matrix4x4>[palette.Length];
            _baked = new Matrix4x4[palette.Length][];

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

                _params[i] = new RenderParams(material)
                {
                    // Bayangan MATI. Tiap rumpun rumput yang melempar bayangan berarti satu lagi
                    // objek yang harus digambar ulang ke peta bayangan tiap frame, dan ribuan
                    // rumpun membuat pass bayangan lebih mahal dari seluruh sisa layarnya.
                    // Bayangan panjang dari matahari rendah tetap ada — dari pemain dan musuh,
                    // yang jumlahnya ratusan, bukan ribuan.
                    shadowCastingMode = ShadowCastingMode.Off,
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
            if (tint < 0 || tint >= _building.Length) tint = 0;
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

            for (int i = 0; i < _building.Length; i++)
            {
                int remaining = _building[i].Count;
                int start = 0;

                while (remaining > 0)
                {
                    int chunk = Mathf.Min(remaining, MaxPerDraw);
                    Graphics.RenderMeshInstanced(_params[i], _mesh, 0, _baked[i], chunk, start);
                    Batches++;

                    start += chunk;
                    remaining -= chunk;
                }
            }
        }
    }
}
