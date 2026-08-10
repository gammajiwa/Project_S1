using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Penanda letak layar pilih starter — <b>ditaruh di prefab, disetel tangan, dan kelihatan
    /// bentuknya selagi disetel</b>. Saudara kandung <see cref="ShopRig"/>,
    /// <see cref="GrimoireGridArea"/>, <see cref="VitalsRig"/>, dan <see cref="StatusStripRig"/>.
    ///
    /// Alasannya sama dengan mereka: selama tata letak halaman ini hidup sebagai koordinat di
    /// <c>MainMenuBuilder</c>, "posisinya jelek" tidak bisa dibetulkan dari editor — scene menu
    /// digenerate ulang dan editan tangan di scene selalu hilang. Prefab tidak ikut digenerate,
    /// jadi di sinilah tempat yang aman untuk menggeser apa pun.
    ///
    /// Isinya tetap digambar kode (petak, ikon piece, angka stat); yang dipindahkan ke sini
    /// KOTAK-KOTAKNYA dan label-labelnya.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Grimoire/Starter Rig")]
    public class StarterRig : MonoBehaviour
    {
        [Tooltip("Kotak tempat petak 7x7 digambar. Biasanya anak 'GridArea' di dalam prefab " +
                 "papan grimoire — petaknya akan mendarat persis di atas sampul bukunya.")]
        public RectTransform Board;

        [Header("Teks")]
        public TextMeshProUGUI NameLabel;
        public TextMeshProUGUI Blurb;
        public TextMeshProUGUI Stats;
        public TextMeshProUGUI PageLabel;

        [Tooltip("Slot gambar starter. Dimatikan sendiri selama starter-nya belum punya art.")]
        public Image Portrait;

        [Header("Tombol")]
        public Button Prev;
        public Button Next;
        public Button Play;
        public Button Back;

        [Header("Bantuan menata (editor saja)")]
        [Tooltip("Gambar kotak papan di Scene view. Tidak berpengaruh saat main.")]
        public bool ShowGuide = true;

        public Color GuideInk = new Color(1f, 0.72f, 0.35f, 1f);

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!ShowGuide || Board == null) return;

            var r = Board.rect;
            if (r.width < 1f || r.height < 1f) return;

            var old = Gizmos.matrix;
            Gizmos.matrix = Board.localToWorldMatrix;
            Gizmos.color = GuideInk;
            Gizmos.DrawWireCube(r.center, new Vector3(r.width, r.height, 0f));

            // Petaknya sendiri, dengan rumus yang SAMA dengan penggambarnya — kotak kosong tidak
            // memberi tahu apa pun soal seberapa besar satu sel nanti.
            float step = Mathf.Min((r.width + 4f) / Grimoire.Width, (r.height + 4f) / Grimoire.Height);
            float cell = Mathf.Max(1f, step - 4f);
            float padX = (r.width - (step * Grimoire.Width - 4f)) * 0.5f;
            float padY = (r.height - (step * Grimoire.Height - 4f)) * 0.5f;

            var faint = GuideInk;
            faint.a *= 0.4f;
            Gizmos.color = faint;

            for (int y = 0; y < Grimoire.Height; y++)
            {
                for (int x = 0; x < Grimoire.Width; x++)
                {
                    var c = new Vector3(r.xMin + padX + x * step + cell * 0.5f,
                                        r.yMin + padY + y * step + cell * 0.5f, 0f);
                    Gizmos.DrawWireCube(c, new Vector3(cell, cell, 0f));
                }
            }

            Gizmos.matrix = old;
        }
#endif
    }
}
