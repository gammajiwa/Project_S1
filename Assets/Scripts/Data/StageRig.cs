using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Angka-angka <b>penataan panggung</b>: di mana kamera duduk, sebesar apa ia melihat, di mana
    /// dan sebesar apa pemain lahir, seberapa jauh lantai melebihi arena.
    ///
    /// Semuanya dulu ditulis langsung di <see cref="ProtoBootstrap"/>, dan itu jadi masalah karena
    /// scene permainan ini <b>dibangun runtime</b> — tidak ada kamera di scene yang bisa diseret,
    /// tidak ada pemain yang bisa dipilih, jadi satu-satunya cara menggeser sudut kamera adalah
    /// membuka berkas kode. Aset ini mengembalikan angka-angka itu ke Inspector, tempat mereka
    /// bisa diputar sambil melihat hasilnya.
    ///
    /// Yang TIDAK ada di sini adalah angka aturan main — nyawa, laju, damage, ukuran arena.
    /// Semua itu sudah milik <see cref="GameBalance"/>, dan menyalinnya ke sini berarti dua tempat
    /// menjawab satu pertanyaan. Batasnya: kalau mengubahnya mengubah cara bermain, ia balance;
    /// kalau cuma mengubah cara melihat, ia di sini.
    ///
    /// Boleh dibiarkan kosong di <see cref="ProtoBootstrap"/> — tanpa aset, nilai bawaan di kelas
    /// ini yang dipakai, dan nilai bawaan itu persis angka yang dulu tertulis di kode.
    /// </summary>
    [CreateAssetMenu(fileName = "StageRig", menuName = "Grimoire/Stage Rig")]
    public class StageRig : ScriptableObject
    {
        // ------------------------------------------------------------------ kamera

        [Header("Kamera")]
        [Tooltip("Letak kamera terhadap rig yang mengikuti pemain. Y = tinggi, Z negatif = mundur.")]
        public Vector3 CameraOffset = new Vector3(0f, 18.5f, -7.5f);

        [Tooltip("Kemiringan kamera dalam derajat. 90 = menunduk lurus ke bawah, 0 = mendatar.\n\n" +
                 "Angka ini juga ikut menghitung sejauh apa pemain boleh lahir dari tepi arena, " +
                 "jadi memiringkannya tanpa menguji titik lahir bisa membuat run dibuka dengan " +
                 "pemain menempel di pinggir layar.")]
        [Range(15f, 90f)] public float CameraPitch = 68f;

        [Tooltip("Ortografis = tanpa perspektif, jarak tidak mengecilkan benda.")]
        public bool CameraOrthographic = true;

        [Tooltip("Setengah tinggi pandangan kamera ortografis, dalam unit dunia. Besar = melihat " +
                 "lebih luas dan semuanya tampak lebih kecil.")]
        [Min(1f)] public float CameraSize = 11f;

        // ------------------------------------------------------------------ pemain

        [Header("Pemain")]
        [Tooltip("Tinggi titik lahir pemain. Pivot pemain ada di tengah dada, bukan di telapak " +
                 "kaki — itu sebabnya angkanya bukan nol.")]
        public float PlayerHeight = 0.9f;

        [Tooltip("Skala objek pemain, warisan dari kapsul yang dulu jadi badannya.\n\n" +
                 "Avatar di dalamnya DIKOMPENSASI balik dengan 1/skala supaya modelnya tampil " +
                 "pada ukuran aslinya, jadi mengubah angka ini tidak membesarkan avatar - ia " +
                 "membesarkan kotak tumbukan dan apa pun lain yang menempel pada pemain.")]
        [Min(0.05f)] public float PlayerScale = 0.9f;

        [Tooltip("Geser model avatar terhadap pivot pemain. Pivot pemain di dada, pivot model di " +
                 "telapak kaki; angka ini yang membatalkan selisihnya.")]
        public Vector3 AvatarOffset = new Vector3(0f, -1f, 0f);

        [Tooltip("Tinggi lampu yang dibawa pemain. Satu-satunya lampu yang menempel padanya, dan " +
                 "tanpa itu pemain lenyap total di malam hari jauh dari lampu arena.")]
        public float CarriedLightHeight = 2.2f;

        [Tooltip("Jarak minimum titik lahir pemain dari tepi arena.")]
        [Min(0f)] public float SpawnMargin = 9f;

        // ------------------------------------------------------------------ lantai

        [Header("Lantai")]
        [Tooltip("Seberapa jauh lantai melebihi arena. Untuk musuh yang lahir di LUAR arena - " +
                 "tanpa kelebihan ini mereka berjalan masuk sambil melayang di atas kekosongan.")]
        [Min(0f)] public float GroundMargin = 14f;

        // ------------------------------------------------------------------ bawaan

        static StageRig _fallback;

        /// <summary>
        /// Nilai bawaan, dipakai saat tidak ada aset yang dipasang. Dibuat di memori dan tidak
        /// pernah tersimpan ke disk — supaya "belum dipasang" berarti perilaku LAMA yang sudah
        /// teruji, bukan panggung yang runtuh.
        /// </summary>
        public static StageRig Fallback
        {
            get
            {
                if (_fallback == null)
                {
                    _fallback = CreateInstance<StageRig>();
                    _fallback.name = "StageRig (bawaan)";
                    _fallback.hideFlags = HideFlags.HideAndDontSave;
                }

                return _fallback;
            }
        }
    }
}
