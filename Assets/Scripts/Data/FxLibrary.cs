using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Semua benda FX yang dulu lahir sebagai <c>GameObject.CreatePrimitive</c> — bola, silinder,
    /// kubus — dikumpulkan jadi slot prefab di satu aset.
    ///
    /// Alasannya bukan kerapian kode: pemilik project ingin **meng-QA tiap objek satu per satu**
    /// dan menukar yang jelek sendiri. Selama benda-benda ini lahir dari kode, tidak ada yang
    /// bisa dibuka, dilihat, atau diseret — satu-satunya cara mengubahnya adalah menyunting C#.
    ///
    /// Tiap slot boleh KOSONG: yang kosong jatuh kembali ke primitif lama persis seperti dulu.
    /// Itu penting supaya aset yang belum diisi tidak pernah membuat skill jadi tak terlihat.
    ///
    /// Prefab bawaannya dibangkitkan <c>Tools/Grimoire/Build Core FX</c> ke
    /// <c>Assets/Prefabs/FX_Core/&lt;Nama&gt;/</c> — isinya SAMA dengan primitif lama, jadi
    /// memasangnya tidak mengubah apa pun sampai isinya benar-benar diganti.
    /// </summary>
    [CreateAssetMenu(fileName = "FxLibrary", menuName = "Grimoire/FX Library")]
    public class FxLibrary : ScriptableObject
    {
        [Tooltip("Tampilkan BADAN benda FX walau skillnya sudah punya efek partikel sendiri.\n\n" +
                 "MATI secara bawaan, dan itu permintaan langsung pemilik project: \"buang semua " +
                 "primitif object, gw gak mau lihat kalo masih ada\". Selama isi prefab di " +
                 "slot-slot bawah masih bentuk primitif bawaan (kubus, bola, silinder), badan itu " +
                 "cuma menempel di atas efek dan membuat seluruh layar terbaca sebagai placeholder.\n\n" +
                 "NYALAKAN begitu slot-slotnya sudah diisi model sungguhan — bola api yang " +
                 "digambar tangan memang harus terlihat, bukan disembunyikan.\n\n" +
                 "Benda yang TIDAK punya efek partikel tetap digambar apa pun nilai ini: tidak " +
                 "menggambar apa-apa lebih buruk daripada menggambar kotak.")]
        public bool ShowBodiesWithVfx;

        [Header("Cahaya skill")]
        [Tooltip("Terangnya cahaya yang dipancarkan tiap kilatan skill. 0 = mati total.\n\n" +
                 "Ada karena efek partikel menggambar dirinya sendiri terang tapi tidak " +
                 "MENYINARI apa pun — ledakan yang tidak menerangi tanah di bawahnya terbaca " +
                 "sebagai stiker yang ditempel di layar, dan itu paling kentara di biome malam.\n\n" +
                 "Jumlah lampunya dibatasi keras di kolam (sepuluh) berapa pun angka ini, jadi " +
                 "menaikkannya menambah TERANG, bukan menambah beban.")]
        [Range(0f, 3f)] public float SkillLightBrightness = 1f;

        [Tooltip("Pengali jangkauan cahaya. Dipisah dari terangnya: redup-tapi-luas dan " +
                 "terang-tapi-sempit adalah dua rasa yang berbeda.")]
        [Range(0.1f, 3f)] public float SkillLightReach = 1f;

        [Header("Peluru & tumbukan")]
        [Tooltip("Inti peluru. Menciut sendiri jadi titik kalau skillnya punya CastVfx — " +
                 "yang diuji tabrakannya benda ini, efeknya cuma menumpang di posisinya.")]
        public GameObject ProjectileCore;

        [Tooltip("Kilatan yang muncul di tiap tumbukan, kematian, dan reaksi. Benda paling " +
                 "sering terlihat di seluruh game — belasan per detik saat wave ramai.")]
        public GameObject ImpactFlash;

        [Header("Jatuh dari langit")]
        [Tooltip("Bola yang meluncur turun sebelum skill AoE/Zone mendarat, beserta ekornya.")]
        public GameObject Descent;

        [Header("Penanda tanah")]
        [Tooltip("Cakram kubangan Zone. Memakai shader Grimoire/AoeRing.")]
        public GameObject ZoneDisc;

        [Tooltip("Cincin aba-aba SunStrike, mengetat menuju detik hantaman.")]
        public GameObject StrikeRing;

        [Header("Benda bergerak")]
        [Tooltip("Pecahan yang mengambang di atas kepala (CastKind.Orbit).")]
        public GameObject Orb;

        [Tooltip("Bola yang menggelinding menembus gerombolan (CastKind.RollingBall).")]
        public GameObject Boulder;

        [Tooltip("Badan puting beliung (CastKind.Vortex). Menandai jangkauan seret.")]
        public GameObject Twister;

        [Header("Perilaku gelombang kedua")]
        [Tooltip("Satu bilah yang mengitari badan pemain (CastKind.Orbital). Satu skill memakai " +
                 "beberapa sekaligus, jadi prefabnya harus MURAH — ada sampai delapan di layar " +
                 "sepanjang durasi, bukan sekejap seperti kilatan.")]
        public GameObject Blade;

        [Tooltip("Bumerang yang dilempar dan kembali (CastKind.Boomerang).")]
        public GameObject Boomerang;

        [Tooltip("Badan menara yang ditanam dan menembak sendiri (CastKind.Turret).")]
        public GameObject TurretBody;

        [Tooltip("Cincin gelombang yang melebar (CastKind.Shockwave). Memakai shader " +
                 "Grimoire/AoeRing seperti penanda tanah lain — yang harus terbaca TEPINYA, " +
                 "karena cuma tepi itu yang melukai.")]
        public GameObject ShockRing;

        [Tooltip("Rudal pengejar (CastKind.Seeker).")]
        public GameObject Missile;

        [Tooltip("Penanda tanah jangkauan Orbital — cakram tipis di kaki pemain. Memakai shader " +
                 "Grimoire/AoeRing seperti penanda tanah lain.\n\n" +
                 "Ini yang menjawab \"kok areanya besar tapi tidak melukai apa-apa\": efek " +
                 "partikel sebuah Orbital hampir selalu jauh lebih lebar dari radius damage-nya, " +
                 "dan tanpa cakram ini tidak ada apa pun di layar yang menyebut batas sebenarnya.")]
        public GameObject OrbitRing;

        [Header("Dunia")]
        [Tooltip("Barang jatuh yang bisa dipungut.")]
        public GameObject DropPickup;

        [Tooltip("Penjaga di pulau rehat: pedagang, bandar, pertapa.")]
        public GameObject IslandGuardian;
    }
}
