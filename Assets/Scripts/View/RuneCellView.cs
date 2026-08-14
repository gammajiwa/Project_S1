using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Satu petak rune di layar: bingkai ornamen, latar gelap, dan SATU glyph di dalamnya.
    ///
    /// Bagian-bagiannya boleh disambungkan di prefab, dan kalau tidak disambungkan komponen ini
    /// mencarinya sendiri lewat nama anak. Itu bukan kemalasan: <c>RuneCell.prefab</c> sudah
    /// ditata tangan sebelum komponen ini ada, dan memaksa tiap prefab dibuka ulang cuma untuk
    /// menyeret dua referensi adalah cara memastikan perbaikannya tidak pernah dipakai.
    /// </summary>
    [AddComponentMenu("Grimoire/Rune Cell View")]
    [RequireComponent(typeof(RectTransform))]
    public class RuneCellView : MonoBehaviour
    {
        [Tooltip("Sapuan warna PALING BELAKANG, yang melebar sampai menutup celah antar petak " +
                 "sehingga seluruh footprint rune terbaca sebagai satu bidang. Kosong = anak " +
                 "bernama \"Area\", dibuat kalau belum ada.")]
        [SerializeField] Image _area;

        [Tooltip("Kotak warna di belakang glyph. Kosong = dicari dari anak bernama \"BG\".")]
        [SerializeField] Image _plate;

        [Tooltip("Bingkai ornamen. Kosong = anak bernama \"Frame\". Lihat ShowBorder.")]
        [SerializeField] Image _border;

        [Tooltip("Gambar rune di dalamnya. Kosong = anak bernama \"Glyph\", dibuat kalau belum ada.")]
        [SerializeField] Image _glyph;

        [Header("Tampang")]
        [Tooltip("Sepekat apa kotak warnanya. Petak rune duduk di atas HALAMAN PERKAMEN yang " +
                 "terang, dan kotak pekat di atasnya terbaca sebagai lubang hitam, bukan sebagai " +
                 "petak. Yang harus menang di petak ini adalah runenya.")]
        [Range(0f, 1f)] public float PlateAlpha = 0.22f;

        [Tooltip("Bingkai ornamennya dipakai atau tidak. Mati secara bawaan: ornamen yang " +
                 "digambar untuk petak 128 piksel jadi bubur begitu dikecilkan ke ukuran petak " +
                 "papan, dan bubur di tepi petak cuma mengaburkan runenya.")]
        public bool ShowBorder;

        [Tooltip("Seberapa jauh glyph masuk ke dalam petak, sebagai pecahan sisinya. Kecil = " +
                 "runenya besar dan tegas.")]
        [Range(0f, 0.4f)] public float GlyphInset = 0.06f;

        [Tooltip("Sepekat apa sapuan area di belakang seluruh footprint. Harus KECIL: gunanya " +
                 "cuma mengikat petak-petak yang terpisah celah supaya terbaca sebagai satu " +
                 "rune. Begitu ia cukup pekat untuk diperhatikan, ia berhenti jadi pengikat dan " +
                 "mulai bersaing dengan runenya sendiri.")]
        [Range(0f, 1f)] public float AreaAlpha = 0.16f;

        [Tooltip("Nada petak saat penempatannya DITOLAK. Yang boleh ditaruh digambar apa adanya - " +
                 "warna kedua untuk 'boleh' cuma menambah bahasa yang tidak perlu dipelajari, " +
                 "karena bentuk yang sudah duduk di tempatnya sudah mengatakannya.")]
        public Color BlockedTint = new Color(1f, 0.25f, 0.25f, 1f);

        [Tooltip("Geser tambahan glyph SESUDAH dipusatkan, dalam pecahan sisi petak. +y naik. " +
                 "Pemusatannya sendiri otomatis per gambar; yang ini murni selera.")]
        public Vector2 GlyphNudge = new Vector2(0f, 0.03f);

        RectTransform _rect;
        Sprite _placed;
        bool _ready;

        void Awake() => Ensure();

        void Ensure()
        {
            if (_ready) return;
            _ready = true;

            _rect = (RectTransform)transform;

            // Petak ini sering menumpang di induk ber-GridLayoutGroup (kotak siluet codex). Tanpa
            // ini ia ikut ditata sebagai sel tambahan: dipaksa seukuran petak layout lalu dibuang
            // ke ujung barisan, dan letaknya yang sudah dihitung dengan benar dibuang diam-diam.
            var ignore = GetComponent<LayoutElement>();
            if (ignore == null) ignore = gameObject.AddComponent<LayoutElement>();
            ignore.ignoreLayout = true;

            if (_area == null) _area = Child("Area");
            if (_area == null)
            {
                var go = new GameObject("Area", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                _area = go.AddComponent<Image>();
            }

            // Paling depan di daftar anak = digambar paling BELAKANG. Sapuan area yang menimpa
            // runenya sendiri adalah kebalikan dari gunanya.
            _area.transform.SetAsFirstSibling();
            _area.raycastTarget = false;

            if (_plate == null) _plate = Child("BG");
            if (_border == null) _border = Child("Frame");
            if (_glyph == null) _glyph = Child("Glyph");

            if (_glyph == null)
            {
                var go = new GameObject("Glyph", typeof(RectTransform));
                go.transform.SetParent(transform, false);

                _glyph = go.AddComponent<Image>();
                _glyph.preserveAspect = true;
            }

            // Terakhir di antara saudaranya = digambar paling atas. Glyph yang tertutup kotak
            // warnanya sendiri adalah keluhan yang sama dengan yang sedang diperbaiki.
            _glyph.transform.SetAsLastSibling();

            if (_plate != null) _plate.raycastTarget = false;
            if (_border != null) _border.raycastTarget = false;
            _glyph.raycastTarget = false;
        }

        /// <summary>
        /// Menempatkan glyph supaya TINTANYA yang duduk di tengah petak, bukan kanvas gambarnya.
        ///
        /// Runenya digambar tidak terpusat di PNG-nya sendiri, dan preserveAspect memusatkan
        /// KOTAK gambarnya - jadi yang terlihat mendarat melenceng, beda-beda per rune. Koreksinya
        /// dituang ke ANCHOR, bukan ke posisi dalam piksel, supaya ikut benar di petak 15 piksel
        /// codex maupun petak papan yang berkali-kali lebih besar.
        /// </summary>
        /// <summary>
        /// Petak yang sudah jadi MENGISI PENUH selnya, tanpa jaga aspek dan tanpa koreksi
        /// pemusatan.
        ///
        /// Dua-duanya sengaja. Gambar-gambar itu digambar sebagai petak, jadi tepinya memang
        /// tepi petak - memberinya jarak akan menyisakan celah di antara petak yang seharusnya
        /// bersambung. Dan aspek tiap potongan atlasnya sedikit berbeda (180x160, 186x150, ...),
        /// jadi menjaga aspek justru membuat petak-petak di papan tampil beda-beda ukuran.
        /// </summary>
        void PlaceBaked()
        {
            _glyph.preserveAspect = false;

            var rt = _glyph.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void PlaceGlyph(Sprite glyph)
        {
            _glyph.preserveAspect = true;

            float span = 1f - 2f * GlyphInset;

            // Sisi mana yang dipakai preserveAspect untuk memuat gambarnya. Gambar yang lebih
            // tinggi daripada lebar menyusut mendatar, dan koreksi mendatarnya ikut menyusut.
            float fitX = 1f, fitY = 1f;
            if (glyph != null && glyph.rect.width > 0.001f && glyph.rect.height > 0.001f)
            {
                float aspect = glyph.rect.width / glyph.rect.height;
                if (aspect >= 1f) fitY = 1f / aspect;
                else fitX = aspect;
            }

            var ink = RuneTiles.InkOffset(glyph);
            float dx = -ink.x * span * fitX + GlyphNudge.x;
            float dy = -ink.y * span * fitY + GlyphNudge.y;

            // Ditambatkan ke SUDUT petak, bukan diberi jarak dalam piksel: petak yang sama
            // digambar 15 piksel di codex dan puluhan kali lebih besar di papan, dan jarak dalam
            // piksel yang benar di salah satunya pasti salah di yang lain.
            var rt = _glyph.rectTransform;
            rt.anchorMin = new Vector2(GlyphInset + dx, GlyphInset + dy);
            rt.anchorMax = new Vector2(1f - GlyphInset + dx, 1f - GlyphInset + dy);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        Image Child(string name)
        {
            var found = transform.Find(name);
            return found != null ? found.GetComponent<Image>() : null;
        }

        /// <summary>
        /// Menempatkan petak ini persis menutupi <paramref name="cell"/>, apa pun induk keduanya.
        ///
        /// Lewat posisi DUNIA, bukan dengan menyalin anchor dan anchoredPosition: sel yang ditata
        /// GridLayoutGroup baru punya anchoredPosition yang benar setelah layout dihitung, dan
        /// membacanya sebelum itu memberi angka dari ronde sebelumnya.
        /// </summary>
        /// <param name="bleed">
        /// Seberapa jauh sapuan area melebar keluar petak, sebagai pecahan sisi petak. Diisi
        /// dengan <c>celah / sisi petak</c>: dengan angka itu sapuan tiap petak persis
        /// BERSENTUHAN dengan tetangganya, tidak kurang dan tidak lebih.
        ///
        /// Keduanya penting. Kurang sedikit menyisakan garis latar di antara petak, dan
        /// footprint-nya kembali terbaca sebagai ubin lepas. Lebih sedikit membuat sapuan
        /// bertindih, dan dua lapis warna tembus pandang yang bertindih menggelap di
        /// sambungannya — persis kisi yang justru sedang dihapus.
        /// </param>
        public void Cover(RectTransform cell, float bleed = 0f)
        {
            if (cell == null) return;
            Ensure();

            if (_area != null)
            {
                float k = Mathf.Max(0f, bleed) * 0.5f;

                var art = _area.rectTransform;
                art.anchorMin = new Vector2(-k, -k);
                art.anchorMax = new Vector2(1f + k, 1f + k);
                art.offsetMin = Vector2.zero;
                art.offsetMax = Vector2.zero;
            }

            _rect.localRotation = Quaternion.identity;
            _rect.localScale = Vector3.one;
            _rect.pivot = new Vector2(0.5f, 0.5f);

            var parent = _rect.parent as RectTransform;
            float px = parent != null ? Mathf.Max(0.0001f, Mathf.Abs(parent.lossyScale.x)) : 1f;
            float py = parent != null ? Mathf.Max(0.0001f, Mathf.Abs(parent.lossyScale.y)) : 1f;

            var size = cell.rect.size;
            _rect.sizeDelta = new Vector2(
                size.x * Mathf.Abs(cell.lossyScale.x) / px,
                size.y * Mathf.Abs(cell.lossyScale.y) / py);

            // Ukuran dulu, letak belakangan: mengubah sizeDelta menahan anchoredPosition, jadi
            // menyetelnya sesudah posisi akan menggeser petaknya lagi.
            _rect.position = cell.TransformPoint(cell.rect.center);
        }

        /// <summary>
        /// Isi petak ini: gambar runenya, warna petaknya, dan sepekat apa keseluruhannya.
        ///
        /// Yang harus menang di sini adalah RUNENYA. Warna cuma memberi tahu piece mana yang
        /// menempati petak itu, jadi ia dibiarkan tembus pandang; runenya digambar penuh, tanpa
        /// diwarnai ulang, supaya glow yang sudah ada di gambarnya tetap glow.
        /// </summary>
        public void Bind(Sprite baked, Sprite glyph, Color tint, float alpha, bool blocked = false)
        {
            Ensure();

            if (blocked) tint = BlockedTint;

            // Petak yang sudah jadi membawa bingkai dan latarnya sendiri di dalam gambarnya.
            // Menyalakan pelat warna di bawahnya cuma menaruh kotak berwarna di belakang gambar
            // yang latarnya sudah pekat - tidak terlihat, dan satu draw call per petak percuma.
            bool hasBaked = baked != null;

            if (_area != null)
            {
                float wash = RuneTiles.AreaAlpha(AreaAlpha);
                _area.enabled = wash > 0.001f;
                _area.color = new Color(tint.r, tint.g, tint.b, wash * alpha);
            }

            if (_plate != null)
            {
                _plate.enabled = !hasBaked;
                if (!hasBaked) _plate.color = new Color(tint.r, tint.g, tint.b, PlateAlpha * alpha);
            }

            if (_border != null)
            {
                _border.enabled = !hasBaked && ShowBorder;
                if (_border.enabled) _border.color = new Color(tint.r, tint.g, tint.b, alpha);
            }

            if (_glyph != null)
            {
                var wanted = hasBaked ? baked : glyph;
                _glyph.enabled = wanted != null;

                // Ditata ulang HANYA saat gambarnya berganti. Menyentuh anchor tiap frame
                // menandai layout kotor untuk tiap petak di papan, tiap frame.
                if (_placed != wanted)
                {
                    _glyph.sprite = wanted;
                    _placed = wanted;

                    if (hasBaked) PlaceBaked();
                    else PlaceGlyph(wanted);
                }

                // Putih penuh, kecuali saat ditolak. Mengalikannya dengan warna piece akan
                // meredupkan gambar yang memang sudah digambar menyala - itu kebalikan dari
                // gunanya - tapi merah "tidak boleh" harus menang atas apa pun di petak itu.
                _glyph.color = blocked
                    ? new Color(BlockedTint.r, BlockedTint.g, BlockedTint.b, alpha)
                    : new Color(1f, 1f, 1f, alpha);
            }
        }
    }
}
