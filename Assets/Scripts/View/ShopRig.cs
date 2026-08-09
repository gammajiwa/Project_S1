using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Penanda letak panel singgah (toko / kejadian / slot) — <b>ditaruh di prefab, disetel
    /// tangan, dan kelihatan bentuknya selagi disetel</b>. Saudara kandung
    /// <see cref="GrimoireGridArea"/>, <see cref="VitalsRig"/>, dan <see cref="StatusStripRig"/>.
    ///
    /// Alasannya sama dengan ketiganya: tata letak panel dulu rumus piksel di
    /// <see cref="GrimoireLayout"/>, dan "berantakan" tidak bisa dibetulkan dari editor selama
    /// angkanya hidup di kode. Isinya tetap digambar kode — yang dipindahkan KOTAK-KOTAKNYA.
    ///
    /// Semua slot boleh kosong sendiri-sendiri; yang kosong kembali ke rumus hitungan lama.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Grimoire/Shop Rig")]
    public class ShopRig : MonoBehaviour
    {
        [Tooltip("Badan panel. Klik di luar kotak ini menutup toko; latar panel digambar " +
                 "seukuran kotak ini.")]
        public RectTransform Panel;

        [Tooltip("Slot dagangan, urut kiri-atas ke kanan-bawah. Boleh lebih sedikit dari enam — " +
                 "sisanya memakai rumus lama.")]
        public RectTransform[] Slots;

        [Tooltip("Tombol REROLL.")]
        public RectTransform Reroll;

        [Tooltip("Tempat tombol LANJUT / MULAI WAVE. Diminta pemilik project di pojok " +
                 "kanan-bawah — dan karena sekarang kotak, memindahkannya lagi tinggal geser, " +
                 "bukan tawar-menawar koordinat di kode.")]
        public RectTransform StartButton;

        [Header("Bantuan menata (editor saja)")]
        [Tooltip("Gambar semua kotak di Scene view. Tidak berpengaruh saat main.")]
        public bool ShowGuide = true;

        public Color GuideInk = new Color(1f, 0.72f, 0.35f, 1f);

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!ShowGuide) return;

            Draw(Panel, GuideInk);
            Draw(Reroll, GuideInk);
            Draw(StartButton, new Color(0.5f, 1f, 0.55f, GuideInk.a));

            if (Slots == null) return;
            var faint = GuideInk;
            faint.a *= 0.55f;
            for (int i = 0; i < Slots.Length; i++) Draw(Slots[i], faint);
        }

        static void Draw(RectTransform area, Color ink)
        {
            if (area == null) return;

            var r = area.rect;
            if (r.width < 1f || r.height < 1f) return;

            var old = Gizmos.matrix;
            Gizmos.matrix = area.localToWorldMatrix;
            Gizmos.color = ink;
            Gizmos.DrawWireCube(r.center, new Vector3(r.width, r.height, 0f));
            Gizmos.matrix = old;
        }
#endif
    }
}
