using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Satu jenis prop bermesh — pohon, rumpun, atau batu — yang digambar lewat jalur instanced
    /// yang sama dengan prop primitif.
    ///
    /// Yang disimpan MESH DAN MATERIAL, bukan prefab-nya. Prefab ToonScapes membawa LODGroup dengan
    /// tiga sampai lima renderer; memasangnya sebagai GameObject berarti tiap rumpun rumput jadi
    /// satu objek dengan LODGroup sendiri yang dievaluasi tiap frame, dan seribu di antaranya
    /// menghapus seluruh alasan <see cref="PropBatch"/> ada. LOD-nya dipilih SEKALI di generator,
    /// lalu yang tersisa cuma satu mesh dan satu material — persis yang dibutuhkan
    /// <c>Graphics.RenderMeshInstanced</c>.
    ///
    /// Ukuran aslinya liar (pohon ToonScapes tingginya 9–28 unit, pohon di game ini 2,2–4), jadi
    /// skalanya TIDAK disimpan di sini. Ia dihitung dari ukuran target dibagi ukuran mesh-nya,
    /// supaya pohon ToonScapes dan pohon prosedural keluar setinggi persis sama dan perbandingannya
    /// jujur.
    /// </summary>
    [System.Serializable]
    public class MeshProp
    {
        [Tooltip("Nama aset sumbernya. Hanya untuk dibaca manusia di inspector.")]
        public string Name;

        public Mesh Mesh;

        /// <summary>
        /// Satu material per submesh, berurutan.
        ///
        /// Jamak, bukan tunggal, dan itu bukan kelebihan yang mungkin berguna nanti: pohon cemara
        /// di paket low-poly memisahkan batang dari daun sebagai dua submesh, dan bunga jadi tiga.
        /// Menggambar submesh nol saja menghasilkan pohon yang cuma batang — gagal tanpa error,
        /// dan terbaca sebagai hutan tiang.
        /// </summary>
        public Material[] Materials;

        [Tooltip("Peluang terpilih dibanding entri lain di daftar yang sama.")]
        [Min(0f)] public float Weight = 1f;

        [Tooltip("Pengali di atas ukuran target daftarnya. Semak perlu lebih besar dari rumput " +
                 "meski keduanya diambil dari daftar yang sama.")]
        [Min(0.01f)] public float SizeMultiplier = 1f;

        public bool Valid => Mesh != null && Materials != null && Materials.Length > 0 &&
                             Materials[0] != null;
    }
}
