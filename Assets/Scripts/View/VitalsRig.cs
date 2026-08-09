using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Bar HP dan mana yang bentuknya ditentukan PREFAB, bukan kode.
    ///
    /// Kode di sini menyentuh tepat tiga hal: <c>fillAmount</c>, warna kilat saat kena pukul,
    /// dan isi teksnya. Semua yang lain — letak, ukuran, sprite, arah isian, apakah ia bar
    /// mendatar atau bola yang terisi memutar — milik prefab.
    ///
    /// Batas itu disengaja dan pernah dibayar mahal di papan grimoire: begitu kode ikut
    /// menyetel ukuran atau arah isian, setiap perubahan di prefab tertimpa diam-diam saat
    /// run, dan yang mengubahnya tidak akan pernah tahu kenapa hasilnya tidak berubah.
    ///
    /// Slot mana pun boleh kosong. Bar tanpa label tetap bar; bola mana tanpa bingkai tetap
    /// bola mana.
    /// </summary>
    [AddComponentMenu("Grimoire/Vitals Rig")]
    public class VitalsRig : MonoBehaviour
    {
        [Header("Nyawa")]
        [Tooltip("Gambar yang terisi mengikuti HP. Wajib bertipe Filled di Inspector — arah dan " +
                 "metode isiannya (mendatar, menegak, memutar) dibaca dari sana, TIDAK disetel " +
                 "kode. Bola bisa dibuat Radial 360; bar biasa Horizontal.")]
        public Image HpFill;

        [Tooltip("Serpihan yang tertinggal di belakang HP dan menyusul perlahan — itu yang " +
                 "membuat besarnya satu pukulan terbaca setelah pukulannya lewat. Boleh kosong.")]
        public Image HpChip;

        [Tooltip("Tulisan angka HP. Boleh kosong kalau bolanya sudah bercerita sendiri.")]
        public Text HpLabel;

        [Header("Mana")]
        public Image ManaFill;

        public Text ManaLabel;

        /// <summary>
        /// Semua yang benar-benar dibutuhkan sudah ada. Bar tanpa mana masih separuh berguna,
        /// jadi yang dituntut cuma HP-nya.
        /// </summary>
        public bool Usable => HpFill != null;
    }
}
