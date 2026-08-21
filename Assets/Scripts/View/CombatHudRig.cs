using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// HUD combat sebagai BARANG BENERAN di prefab — Image ber-sprite dan teks TMP ber-font,
    /// kloningan satu-satu dari yang digambar kode. Kode tidak membuat tandingannya lagi:
    /// bagian yang terisi di sini DIPAKAI LANGSUNG (sprite, font, warna, letak, ukuran —
    /// semuanya milikmu di prefab), kode tinggal mengisi teks/nilai dan mendaftarkan kliknya.
    ///
    /// Dua keadaan yang ARTINYA BEDA:
    /// - Slot KOSONG (None) = prefab belum menyediakan bagian itu — kode membangun versi
    ///   gambar-kode di letak hitungan lama, supaya prefab boleh diisi sebagian.
    /// - Objek DINONAKTIFKAN (un-check di prefab) = bagian itu DIHAPUS dari HUD — kode
    ///   tidak menggambarnya, tidak menerima kliknya, dan TIDAK membangun penggantinya.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Grimoire/Combat HUD Rig")]
    public class CombatHudRig : MonoBehaviour
    {
        [Header("Baris info kiri-atas")]
        [Tooltip("Teks wave / sisa musuh / kill / gold. Isi teksnya dari kode + terjemahan; " +
                 "font, ukuran, warna, dan letak milikmu.")]
        public TextMeshProUGUI HudLine;

        [Tooltip("Plakat di belakang baris info. LEBARNYA tetap dipeluk kode mengikuti " +
                 "panjang kalimat; sprite, tinggi, dan letak milikmu.")]
        public Image HudPlaque;

        [Header("Tombol LANJUT / MULAI WAVE")]
        [Tooltip("Badan tombol yang muncul saat wave beres. Kliknya ikut ke mana pun " +
                 "kotak ini kamu pindahkan.")]
        public Image StartButton;

        public TextMeshProUGUI StartLabel;

        [Header("Tombol TOKO")]
        public Image ShopToggle;

        public TextMeshProUGUI ShopLabel;

        [Header("Bar boss")]
        public Image BossBar;

        [Tooltip("Isian nyawa boss — kode cuma menulis fillAmount.")]
        public Image BossFill;

        public TextMeshProUGUI BossLabel;

        [Header("Tas")]
        [Tooltip("Alas panel tas. SPRITE, warna, dan bahannya milikmu — tapi LETAK & UKURAN " +
                 "ditulis kode memeluk petak tas, karena posisi petak dihitung dari papan " +
                 "grimoire (GridOverride) dan alas tataan tangan pasti meleset begitu papan " +
                 "bergeser. Un-check objek ini = tas tanpa alas.")]
        public Image BagPanel;

        [Tooltip("KOTAK LETAK TAS — diisi = seluruh tas (petak, alas, dan area drag-nya) " +
                 "duduk di pojok kiri-bawah kotak ini, digeser di prefab langsung ikut. " +
                 "Kosong / di-un-check = rumus lama di kolom kanan papan. Ukuran petak tetap " +
                 "ikut papan; kotak ini cuma menentukan LETAK, bukan ukuran.")]
        public RectTransform BagArea;
    }
}
