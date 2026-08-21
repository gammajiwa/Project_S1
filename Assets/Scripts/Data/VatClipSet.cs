using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Satu peran animasi yang dikenali gameplay, beserta letaknya di dalam tekstur panggangan.
    ///
    /// Peran, bukan nama klip. Aset yang dibeli menamai animasinya sesuka pembuatnya —
    /// <c>IDLE</c>, <c>Anim_Idle_01</c>, <c>Armature|walk</c> — dan kode tidak boleh peduli.
    /// Yang dipetakan saat memanggang adalah nama apa pun itu ke salah satu peran di sini,
    /// dan sejak titik itu seluruh sisa sistem cuma bicara soal peran.
    /// </summary>
    public enum VatRole
    {
        Idle = 0,
        Walk = 1,
        Run = 2,

        /// <summary>
        /// Memukul atau menembak. Satu-satunya peran yang TIDAK berputar: ia punya awal dan
        /// akhir, dan yang memainkannya harus tahu kapan selesai.
        /// </summary>
        Attack = 3,
    }

    [System.Serializable]
    public struct VatClip
    {
        public VatRole Role;

        [Tooltip("Nama klip aslinya. Disimpan supaya masih bisa ditelusuri balik ke asetnya " +
                 "waktu ada yang salah — tidak dipakai saat main.")]
        public string SourceName;

        [Tooltip("Baris pertama klip ini di dalam tekstur.")]
        public int FirstRow;

        [Tooltip("Jumlah baris (frame) yang dipakai klip ini.")]
        public int Rows;

        [Tooltip("Durasi asli klip dalam detik. Dipakai supaya kecepatan putarnya tetap benar " +
                 "berapa pun frame yang dipanggang.")]
        public float Seconds;
    }

    /// <summary>
    /// Hasil panggangan satu karakter: mesh statis, tekstur posisi per-frame, dan daftar peran.
    ///
    /// <b>Kenapa dipanggang.</b> Musuh di game ini tidak punya GameObject — seluruh gerombolan
    /// keluar sebagai beberapa panggilan instanced. Instancing menutup pintu untuk Animator dan
    /// SkinnedMeshRenderer sekaligus: keduanya menuntut satu objek per musuh. Memanggang
    /// memindahkan animasinya ke dalam TEKSTUR, dan tekstur bisa dibaca oleh ribuan instance
    /// dari satu material yang sama.
    ///
    /// Yang disimpan cuma POSISI vertex, bukan normalnya. Normal ikut dipanggang berarti dua
    /// kali ukuran tekstur untuk keuntungan yang nyaris tidak terlihat di kamera yang menunduk
    /// dari 18 unit — musuhnya setinggi belasan piksel di layar. Kalau nanti ada yang berdiri
    /// dekat kamera, di situlah normal mulai layak dibayar.
    /// </summary>
    [CreateAssetMenu(fileName = "VatClipSet", menuName = "Grimoire/VAT Clip Set")]
    public class VatClipSet : ScriptableObject
    {
        [Tooltip("Mesh statis hasil panggangan. Vertexnya digeser shader, jadi bentuk yang " +
                 "tersimpan di sini cuma pose netral.")]
        public Mesh Mesh;

        [Tooltip("Tekstur posisi: satu texel per vertex mendatar, satu baris per frame.")]
        public Texture2D Positions;

        [Tooltip("Material asli dari asetnya. Dipakai sebagai sumber tekstur warna saat " +
                 "material VAT dibuat.")]
        public Material SourceMaterial;

        [Tooltip("Noise untuk efek mati terbakar — piksel tubuh digerogoti mengikuti nilai " +
                 "tekstur ini, tepinya membara. Dibagikan dari paket Sprite Shaders Ultimate " +
                 "(SSU_Noise_1K), bukan disalin.\n\n" +
                 "Boleh kosong: terbakarnya jadi serempak sekujur badan alih-alih menggerogoti — " +
                 "jelek, tapi tidak menghentikan apa pun.")]
        public Texture2D BurnNoise;

        [Tooltip("Berapa vertex yang dipanggang. Sama dengan lebar tekstur.")]
        public int VertexCount;

        [Tooltip("Total baris di tekstur.")]
        public int TotalRows;

        [Tooltip("Kotak pembatas gerakan terjauh, dipakai shader untuk membongkar posisi " +
                 "kembali dari nilai 0..1 yang tersimpan di tekstur.")]
        public Vector3 BoundsMin;

        public Vector3 BoundsMax;

        [Tooltip("Tinggi model dalam unit dunia pada skala aslinya. Dipakai untuk mendudukkan " +
                 "musuh dengan tinggi yang benar tanpa harus dikira-kira di inspector.")]
        public float Height = 1f;

        public VatClip[] Clips;

        /// <summary>
        /// Klip untuk sebuah peran. Mengembalikan klip pertama kalau perannya tidak ada —
        /// musuh yang berjalan tanpa animasi jalan lebih baik daripada musuh yang membeku.
        /// </summary>
        public bool TryGet(VatRole role, out VatClip clip)
        {
            if (Clips != null)
            {
                for (int i = 0; i < Clips.Length; i++)
                {
                    if (Clips[i].Role != role) continue;
                    clip = Clips[i];
                    return true;
                }
            }

            if (Clips != null && Clips.Length > 0)
            {
                clip = Clips[0];
                return true;
            }

            clip = default;
            return false;
        }
    }
}
