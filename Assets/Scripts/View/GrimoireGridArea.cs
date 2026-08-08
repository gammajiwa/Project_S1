using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Penanda letak petak 7x7 di dalam prefab papan grimoire — <b>ditaruh di prefab, disetel
    /// tangan, dan kelihatan bentuknya selagi disetel</b>.
    ///
    /// Kenapa ada komponennya, bukan cukup RectTransform kosong bernama <c>GridArea</c>: kotak
    /// tanpa komponen tidak menggambar apa pun. Di jendela prefab ia benar-benar tak terlihat —
    /// yang menatanya harus menebak di mana petaknya akan jatuh, lalu masuk play mode untuk tahu
    /// hasilnya. Gizmo di bawah menggambar petaknya persis seperti yang akan dibangun kode, jadi
    /// menatanya cukup dengan Rect Tool sambil melihat.
    ///
    /// Yang mengatur letak & ukuran petak adalah <b>RectTransform komponen ini</b>: geser lewat
    /// Pos X / Pos Y, besarkan lewat Width / Height. Petaknya mengisi kotak itu.
    ///
    /// Boleh tidak ada. Prefab tanpa komponen ini (atau tanpa prefab sama sekali) kembali ke
    /// petak hitungan lama di pojok kiri-bawah layar — bukan error, bukan petak kosong.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Grimoire/Grimoire Grid Area")]
    public class GrimoireGridArea : MonoBehaviour
    {
        [Tooltip("Celah antar petak dalam piksel. Yang diregangkan mengikuti kotak ini adalah " +
                 "SELNYA; celah dibiarkan tetap, karena celah yang ikut membesar terbaca sebagai " +
                 "papan yang renggang, bukan papan yang lebih besar.")]
        [Min(0f)] public float Gap = 3f;

        [Header("Bantuan menata (editor saja)")]
        [Tooltip("Gambar kotak dan petak 7x7 di Scene view. Tidak berpengaruh apa pun saat main.")]
        public bool ShowGuide = true;

        [Tooltip("Warna garis bantunya.")]
        public Color GuideInk = new Color(1f, 0.82f, 0.35f, 1f);

        /// <summary>
        /// Sisi sel dan jarak antar pangkalnya untuk kotak ini — rumus yang SAMA dengan
        /// <see cref="GrimoireLayout"/>, supaya garis bantu tidak pernah membohongi hasilnya.
        /// </summary>
        public void Measure(out float step, out float cell)
        {
            var r = ((RectTransform)transform).rect;

            // Celah ditambahkan sebelum dibagi: petak 7x7 memakai tujuh sel tapi hanya enam
            // celah — yang ketujuh menggantung di ujung dan tidak pernah digambar.
            step = Mathf.Min((r.width + Gap) / Grimoire.Width,
                             (r.height + Gap) / Grimoire.Height);

            cell = Mathf.Max(1f, step - Gap);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!ShowGuide) return;

            var rt = (RectTransform)transform;
            var r = rt.rect;
            if (r.width < 1f || r.height < 1f) return;

            float step, cell;
            Measure(out step, out cell);

            var old = Gizmos.matrix;
            Gizmos.matrix = rt.localToWorldMatrix;

            // Tepi kotaknya sendiri: inilah yang digeser dan dibesarkan.
            Gizmos.color = GuideInk;
            Gizmos.DrawWireCube(r.center, new Vector3(r.width, r.height, 0f));

            // Petaknya, digambar dari rumus yang sama dengan yang membangunnya saat main. Kalau
            // kotaknya tidak sebangun, sisa ruang di sisi panjangnya akan langsung kelihatan di
            // sini — selnya dijaga persegi, tidak dipaksa gepeng mengikuti kotak.
            var faint = GuideInk;
            faint.a *= 0.4f;
            Gizmos.color = faint;

            for (int y = 0; y < Grimoire.Height; y++)
            {
                for (int x = 0; x < Grimoire.Width; x++)
                {
                    var centre = new Vector3(r.xMin + x * step + cell * 0.5f,
                                             r.yMin + y * step + cell * 0.5f, 0f);

                    Gizmos.DrawWireCube(centre, new Vector3(cell, cell, 0f));
                }
            }

            Gizmos.matrix = old;
        }
#endif
    }
}
