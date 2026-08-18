using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Penanda letak panel KEJADIAN — saudara kembar <see cref="ShopRig"/>, lahir belakangan
    /// karena kejadian selama ini satu-satunya panel singgah yang seluruh tata letaknya masih
    /// rumus piksel di kode. "Berantakan" pada panel ini tidak bisa dibetulkan dari editor
    /// selama kotak-kotaknya tidak punya wujud yang bisa digeser.
    ///
    /// Aturannya sama persis dengan saudaranya: isinya tetap digambar kode — yang dipindahkan
    /// KOTAK-KOTAKNYA. Slot yang kosong jatuh kembali ke rumus hitungan lama, jadi prefab yang
    /// belum lengkap tidak pernah mematahkan panelnya.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Grimoire/Event Rig")]
    public class EventRig : MonoBehaviour
    {
        [Tooltip("Badan panel kejadian. Latar digambar seukuran kotak ini.")]
        public RectTransform Panel;

        [Tooltip("Kartu pakta KIRI.")]
        public RectTransform OptionA;

        [Tooltip("Kartu pakta KANAN.")]
        public RectTransform OptionB;

        [Tooltip("Tombol MENOLAK di bawah kedua kartu.")]
        public RectTransform Refuse;

        [Header("Bantuan menata (editor saja)")]
        [Tooltip("Gambar semua kotak di Scene view. Tidak berpengaruh saat main.")]
        public bool ShowGuide = true;

        public Color GuideInk = new Color(0.62f, 0.78f, 1f, 1f);

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!ShowGuide) return;

            Draw(Panel, GuideInk);
            Draw(OptionA, GuideInk);
            Draw(OptionB, GuideInk);
            Draw(Refuse, new Color(1f, 0.85f, 0.4f, GuideInk.a));
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
