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
    /// <c>Assets/Art/VFX/Core/&lt;Nama&gt;/</c> — isinya SAMA dengan primitif lama, jadi
    /// memasangnya tidak mengubah apa pun sampai isinya benar-benar diganti.
    /// </summary>
    [CreateAssetMenu(fileName = "FxLibrary", menuName = "Grimoire/FX Library")]
    public class FxLibrary : ScriptableObject
    {
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

        [Header("Dunia")]
        [Tooltip("Barang jatuh yang bisa dipungut.")]
        public GameObject DropPickup;

        [Tooltip("Penjaga di pulau rehat: pedagang, bandar, pertapa.")]
        public GameObject IslandGuardian;
    }
}
