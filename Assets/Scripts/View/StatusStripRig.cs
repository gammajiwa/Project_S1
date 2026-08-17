using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Penanda letak tiga strip ikon — buff, kutukan, dan tally ailment — <b>ditaruh di prefab,
    /// disetel tangan, dan kelihatan bentuknya selagi disetel</b>.
    ///
    /// Alasannya sama persis dengan papan grimoire dan bar vitals: sebelum ini letak ketiga strip
    /// itu tiga angka tetap di <see cref="GrimoireLayout"/> (−96, −128, −160), dan angka tetap
    /// dipilih waktu bar HP masih kotak setinggi 18 piksel. Begitu bar itu jadi BOLA setinggi
    /// hampir 190 piksel, ketiga strip mendarat di atas bolanya — dan tidak ada cara membetulkan
    /// itu dari editor, karena angkanya hidup di kode.
    ///
    /// Yang mengatur letak tiap strip adalah <b>RectTransform kotak yang ditunjuk di bawah</b>:
    /// yang dipakai pojok KIRI-ATAS-nya, karena strip tumbuh ke kanan dari sana. Lebar dan tinggi
    /// kotak tidak memotong apa pun — ia cuma pegangan buat mata dan Rect Tool.
    ///
    /// Ketiganya boleh kosong sendiri-sendiri. Yang kosong kembali ke tempat lamanya di
    /// <see cref="GrimoireLayout"/>, jadi prefab yang cuma mau memindahkan satu strip tidak
    /// dipaksa ikut menata dua sisanya.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Grimoire/Status Strip Rig")]
    public class StatusStripRig : MonoBehaviour
    {
        [Tooltip("Kotak untuk strip BUFF — apa yang sedang membantumu. Kosong = tempat lama " +
                 "(y −96 dari atas layar).")]
        public RectTransform BuffArea;

        [Tooltip("Kotak untuk strip KUTUKAN — apa yang sedang menyakitimu. Kosong = y −128.")]
        public RectTransform DebuffArea;

        [Tooltip("Kotak untuk TALLY AILMENT — berapa musuh sedang terbakar, beku, dan seterusnya. " +
                 "Kosong = y −160.\n\n" +
                 "Dua baris keterangan (piece di tangan & janji evolusi) mengikuti kotak ini, " +
                 "jadi memindahkannya memindahkan mereka juga — kalau tidak, mereka tertinggal " +
                 "menggantung di tempat strip yang sudah pindah.")]
        public RectTransform AilmentArea;

        [Tooltip("Kotak untuk strip PAKTA — berkah & kutuk permanen yang kamu pilih sendiri di " +
                 "node kejadian. Kosong = kolom TEGAK di tepi kanan layar.\n\n" +
                 "Strip ini satu-satunya yang tumbuh KE BAWAH, jadi yang dipakai dari kotak ini " +
                 "tetap pojok kiri-atasnya, dan ikon berikutnya turun dari sana.")]
        public RectTransform PactArea;

        [Header("Bantuan menata (editor saja)")]
        [Tooltip("Gambar kotak ketiga strip di Scene view. RectTransform kosong tidak menggambar " +
                 "apa pun — tanpa garis bantu ini, yang menatanya harus masuk play mode dulu " +
                 "untuk tahu di mana stripnya jatuh. Tidak berpengaruh saat main.")]
        public bool ShowGuide = true;

        [Tooltip("Warna garis bantunya.")]
        public Color GuideInk = new Color(0.55f, 0.85f, 1f, 1f);

        /// <summary>
        /// Pojok kiri-atas kotak <paramref name="area"/> dalam koordinat kanvas yang dipakai
        /// strip: x dari tepi kiri, y MENURUN dari tepi atas.
        ///
        /// Diambil dari sudut dunia, bukan dari <c>anchoredPosition</c>, supaya anchor dan pivot
        /// apa pun yang dipilih di prefab tetap menghasilkan jawaban yang sama.
        /// </summary>
        public static bool TryOrigin(RectTransform area, out Vector2 origin)
        {
            origin = Vector2.zero;
            if (area == null) return false;

            var corners = new Vector3[4];
            area.GetWorldCorners(corners);

            // corners[1] = kiri-atas, dalam PIKSEL LAYAR — kanvasnya ber-CanvasScaler, jadi
            // dibagi skala dulu supaya jadi satuan kanvas yang dipakai strip. Y dihitung
            // turun dari tepi atas, makanya dikurangi tinggi kanvas.
            float scale = Mathf.Max(0.0001f, GrimoireLayout.UiScale);
            origin = new Vector2(corners[1].x / scale, corners[1].y / scale - GrimoireLayout.ScreenH);
            return true;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!ShowGuide) return;

            DrawArea(BuffArea, GuideInk);
            DrawArea(DebuffArea, new Color(1f, 0.5f, 0.5f, GuideInk.a));
            DrawArea(AilmentArea, new Color(0.85f, 0.88f, 0.95f, GuideInk.a));
            DrawArea(PactArea, new Color(1f, 0.8f, 0.35f, GuideInk.a));
        }

        static void DrawArea(RectTransform area, Color ink)
        {
            if (area == null) return;

            var r = area.rect;
            if (r.width < 1f || r.height < 1f) return;

            var old = Gizmos.matrix;
            Gizmos.matrix = area.localToWorldMatrix;

            Gizmos.color = ink;
            Gizmos.DrawWireCube(r.center, new Vector3(r.width, r.height, 0f));

            // Titik pangkalnya ditandai terpisah: yang dipakai kode cuma pojok kiri-atas, dan
            // kotak yang terlihat rapi di tengah layar tetap menaruh stripnya dari pojok itu.
            Gizmos.DrawWireSphere(new Vector3(r.xMin, r.yMax, 0f), 4f);

            Gizmos.matrix = old;
        }
#endif
    }
}
