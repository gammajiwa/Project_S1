using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Every pixel measurement and screen-space rect for the in-run HUD. Pure geometry: no canvas,
    /// no state, nothing to mock. Split out of GrimoireUI so the numbers live in one readable place
    /// instead of being scattered through two thousand lines of drawing code.
    ///
    /// GrimoireUI pulls these in with <c>using static</c>, so call sites read exactly as before.
    /// </summary>
    public static class GrimoireLayout
    {
        // ---------- grimoire grid ----------

        /// <summary>
        /// Kotak layar tempat petak 7x7 harus duduk, DIPAKSAKAN dari luar — diisi oleh anak
        /// bernama <c>GridArea</c> di dalam prefab papan grimoire.
        ///
        /// Null = hitung sendiri dari tepi layar, persis seperti sebelum ada prefab. Itu bukan
        /// jalur usang melainkan jaring: papan tanpa prefab, atau prefab yang lupa membawa
        /// <c>GridArea</c>, tetap dapat petak yang benar alih-alih petak seukuran nol.
        ///
        /// Static karena seluruh berkas ini static, dan ditulis ulang di SETIAP pembangunan UI
        /// (termasuk dikembalikan ke null) — play mode tanpa domain reload tidak boleh mewarisi
        /// kotak milik sesi sebelumnya.
        /// </summary>
        public static Rect? GridOverride;

        /// <summary>Celah antar petak yang dipaksakan prefab. Null = pakai <see cref="CellGapDefault"/>.</summary>
        public static float? GridGapOverride;

        // Ukuran bawaan, dipakai selama tidak ada prefab yang mengambil alih.
        public const int CellSizeDefault = 40;
        public const int CellGapDefault = 3;

        /// <summary>
        /// Jarak antar pangkal petak: sisi sel PLUS celahnya.
        ///
        /// Celah DITAMBAHKAN ke lebar kotak sebelum dibagi, dan itu bukan kelebihan satu piksel.
        /// Petak 7x7 memakai tujuh sel tapi hanya enam celah — yang ketujuh menggantung di ujung
        /// dan tidak pernah digambar. Membagi lebar mentah dengan tujuh membayar celah yang tidak
        /// ada, dan petaknya berhenti satu celah sebelum tepi kanan kotak yang digambar di prefab.
        /// </summary>
        static float Step => GridOverride.HasValue
            ? Mathf.Min((GridOverride.Value.width + CellGap) / Grimoire.Width,
                        (GridOverride.Value.height + CellGap) / Grimoire.Height)
            : CellSizeDefault + CellGapDefault;

        // Celahnya TIDAK ikut diregangkan mengikuti kotak — yang membesar adalah selnya. Celah
        // yang ikut membesar terbaca sebagai papan yang renggang, bukan papan yang lebih besar.
        // Yang boleh mengubahnya cuma angka yang disetel tangan di prefab.
        public static float CellGap => GridGapOverride ?? CellGapDefault;

        // Sel dijaga persegi lewat Mathf.Min di Step — kotak prefab yang tidak sebangun menyisakan
        // ruang di sisi panjangnya, dan itu jauh lebih baik daripada petak yang gepeng.
        public static float CellSize => Mathf.Max(1f, Step - CellGap);

        public const int Margin = 20;
        public const int SkillInset = 8;

        /// <summary>
        /// Dorongan tambahan untuk PAPAN GRIMOIRE saja, di atas <see cref="Margin"/>.
        ///
        /// Ada karena sampul buku yang membingkai papan punya PUNGGUNG di sisi kiri, dan
        /// punggung itu butuh ruang di luar petak. Menaikkan <see cref="Margin"/> tidak bisa
        /// dipakai: angka yang sama juga menjarak-tepikan bar HP, panel spell, dan tombol
        /// kecepatan di seberang layar — bingkai buku tidak ada urusannya dengan mereka.
        ///
        /// Nol = papan kembali menempel di pojok seperti sebelum ada bingkai.
        /// </summary>
        public const int GridInset = 56;

        public static float GridX => GridOverride?.xMin ?? Margin + GridInset;
        public static float GridY => GridOverride?.yMin ?? Margin + 8;

        // ---------- right-hand column ----------
        public const int BagCell = 34;
        public const int BagGap = 3;
        public const int BagY = 20;

        // Baris tombol tepat di atas tas 4x4, yang baris atasnya plus labelnya mencapai y=190.
        // Dulu bernama SellY karena kotak JUAL yang duduk di sini; kotaknya sudah dibuang —
        // menjual sekarang berarti meninggalkan barang di lantai, dan LANJUT yang menyapunya.
        public const int ButtonRowY = 196;

        // Wide enough for "1. Greater Fireball   34.0 dmg   32.4 dps   1.05s   17 mana".
        // Icon strips, stacked straight under the mana bar (which ends at -90).
        public const float StripIcon = 26f;
        public const float StripBuffY = -96f;
        public const float StripDebuffY = -128f;
        public const float StripAilmentY = -160f;

        /// <summary>
        /// Kolom PAKTA di tepi kanan, dan sengaja jauh di bawah tepi atas.
        ///
        /// Percobaan pertama menyejajarkannya dengan strip buff (−96) dan hasilnya terlihat di
        /// screenshot: bilah demo (pengubah waktu & cuaca) duduk di pojok kanan-atas sampai kira-
        /// kira −175, dan dua ikon pakta teratas tertimbun di belakangnya. Pakta permanen yang
        /// tidak terlihat sama saja dengan pakta yang tidak pernah diambil.
        ///
        /// Boleh ditimpa lewat kotak PactArea di StatusStripRig.
        /// </summary>
        public const float StripPactY = -210f;

        public const int SpellPanelW = 380;
        public const int CooldownDiameter = 26;

        public const int SpeedButtonW = 58;
        public const int SpeedButtonH = 34;

        public const int StartButtonW = 280;
        public const int StartButtonH = 56;

        // Loose pieces draw at the exact grid scale so nothing resizes when placed.
        public static float LooseCellSize => CellSize;
        public static float LooseCellGap => CellGap;

        // ---------- shop / recipe panel ----------
        public const int ShopSlotW = 196;
        public const int ShopSlotH = 148;
        public const int PanelW = 632;
        public const int PanelH = 372;

        // ---------- shapes ----------

        public static void ShapeBounds(Vector2Int[] shape, out int w, out int h)
        {
            int maxX = 0, maxY = 0;
            for (int i = 0; i < shape.Length; i++)
            {
                if (shape[i].x > maxX) maxX = shape[i].x;
                if (shape[i].y > maxY) maxY = shape[i].y;
            }

            w = maxX + 1;
            h = maxY + 1;
        }

        /// <summary>
        /// The cell inside a shape that sits under the cursor. Placement and the on-cursor preview
        /// both use this, otherwise the ghost and the real footprint drift apart.
        /// </summary>
        public static Vector2Int AnchorOffset(PieceDefinition def, int rot)
        {
            ShapeBounds(Shapes.Rotate(def.Cells, rot), out int w, out int h);
            return new Vector2Int((w - 1) / 2, (h - 1) / 2);
        }

        public static Vector2 PieceSize(Vector2Int[] shape)
        {
            ShapeBounds(shape, out int w, out int h);
            float step = LooseCellSize + LooseCellGap;
            return new Vector2(w * step - LooseCellGap, h * step - LooseCellGap);
        }

        // ---------- grid & bag ----------

        public static Vector2 CellAnchor(int x, int y) =>
            new Vector2(GridX + x * (CellSize + CellGap), GridY + y * (CellSize + CellGap));

        public static float GridTop() => GridY + Grimoire.Height * (CellSize + CellGap);

        /// <summary>
        /// Kotak yang PERSIS memeluk petak 7x7 — dari tepi kiri petak (0,0) sampai tepi kanan-atas
        /// petak terakhir, tanpa celah yang menggantung di ujung. Dipakai untuk mendudukkan alas
        /// dan bingkai; menghitungnya sendiri di pemakainya adalah cara termudah membuat bingkai
        /// meleset satu <c>CellGap</c> tanpa ada yang menyadarinya.
        /// </summary>
        public static Rect GridRect()
        {
            float w = Grimoire.Width * (CellSize + CellGap) - CellGap;
            float h = Grimoire.Height * (CellSize + CellGap) - CellGap;
            return new Rect(GridX, GridY, w, h);
        }

        /// <summary>Kotak yang sama, dilebarkan per sisi (x kiri, y bawah, z kanan, w atas).</summary>
        public static Rect Expand(Rect r, Vector4 pad) =>
            new Rect(r.xMin - pad.x, r.yMin - pad.y,
                     r.width + pad.x + pad.z, r.height + pad.y + pad.w);

        public static float RightX() => GridX + Grimoire.Width * (CellSize + CellGap) + 12;

        public static Vector2 BagAnchor(int x, int y) =>
            new Vector2(RightX() + x * (BagCell + BagGap), BagY + y * (BagCell + BagGap));

        public static Vector2Int ScreenToCell(Vector2 mouse)
        {
            float step = CellSize + CellGap;

            // Celah antar petak DIBAGI DUA, separuh ke petak kiri separuh ke kanan - bukan
            // ditolak sebagai "bukan petak mana pun".
            //
            // Menolaknya memang mencegah klik di seam nyasar ke tetangga, tapi ongkosnya jauh
            // lebih mahal daripada yang dibelinya: selama piece diseret, tiap kali kursor
            // melintasi satu garis papan ia dianggap KELUAR papan sesaat, jadi bayangan
            // penempatannya berkedip hilang lalu muncul lagi. Papan yang berkedip tiap beberapa
            // piksel gerakan mustahil terasa enak, dan itu yang membuat snap-nya terasa
            // tersendat dibandingkan tas di game lain yang menempel tanpa usaha.
            //
            // Setengah celah di tepi luar papan ikut kebagian, jadi petak paling pinggir tidak
            // lagi butuh ketelitian ekstra untuk dikenai.
            int x = Mathf.FloorToInt((mouse.x - GridX + CellGap * 0.5f) / step);
            int y = Mathf.FloorToInt((mouse.y - GridY + CellGap * 0.5f) / step);

            if (x < 0 || x >= Grimoire.Width || y < 0 || y >= Grimoire.Height) return new Vector2Int(-1, -1);

            return new Vector2Int(x, y);
        }

        public static Vector2Int ScreenToBagCell(Vector2 mouse)
        {
            float step = BagCell + BagGap;

            // Celahnya dibagi dua, alasan yang sama persis dengan papan di atas. Tas dan papan
            // dipakai dalam satu tarikan seret yang sama; yang satu menempel enak sementara yang
            // lain berkedip akan terasa seperti dua game berbeda.
            int x = Mathf.FloorToInt((mouse.x - RightX() + BagGap * 0.5f) / step);
            int y = Mathf.FloorToInt((mouse.y - BagY + BagGap * 0.5f) / step);

            if (x < 0 || x >= Backpack.Width || y < 0 || y >= Backpack.Height) return new Vector2Int(-1, -1);

            return new Vector2Int(x, y);
        }

        // ---------- buttons & panels ----------

        public static Rect SpeedRect(int index, int count)
        {
            float right = Screen.width - (Margin + (count - 1 - index) * (SpeedButtonW + 6));
            float top = Screen.height - Margin;
            return new Rect(right - SpeedButtonW, top - SpeedButtonH, SpeedButtonW, SpeedButtonH);
        }

        /// <summary>
        /// Tombol siang/malam, duduk tepat di bawah deretan tombol kecepatan.
        ///
        /// Lebarnya seluruh deret kecepatan, bukan satu tombol: ia bukan salah satu pilihan dalam
        /// deret itu, dan menyamakan lebarnya membuatnya terbaca sebagai "5x" yang kelima.
        /// </summary>
        public static Rect TimeButtonRect(int speedCount)
        {
            float width = speedCount * SpeedButtonW + (speedCount - 1) * 6;
            float right = Screen.width - Margin;
            float top = Screen.height - Margin - SpeedButtonH - 22;

            return new Rect(right - width, top - SpeedButtonH, width, SpeedButtonH);
        }

        // ---------- override dari prefab panel singgah (ShopRig) ----------
        //
        // Pola yang sama dengan GridOverride: null = rumus hitungan lama, terisi = kotak yang
        // ditata tangan di prefab. Hit-test dan penggambaran sama-sama membaca dari sini, jadi
        // keduanya mustahil berbeda pendapat soal di mana slotnya berada.

        public static Rect? ShopPanelOverride;
        public static Rect[] ShopSlotsOverride;
        public static Rect? ShopRerollOverride;
        public static Rect? StartButtonOverride;

        public static Rect StartButtonRect()
        {
            if (StartButtonOverride.HasValue) return StartButtonOverride.Value;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f + 120f;
            return new Rect(cx - StartButtonW * 0.5f, cy - StartButtonH * 0.5f, StartButtonW, StartButtonH);
        }

        public static Rect PanelRect() =>
            ShopPanelOverride
            ?? new Rect((Screen.width - PanelW) * 0.5f, (Screen.height - PanelH) * 0.5f, PanelW, PanelH);

        public static Rect ShopSlotRect(int i)
        {
            if (ShopSlotsOverride != null && i >= 0 && i < ShopSlotsOverride.Length)
                return ShopSlotsOverride[i];

            var panel = PanelRect();
            int col = i % 3;
            int row = i / 3;

            float x = panel.xMin + 12f + col * (ShopSlotW + 8f);
            float y = panel.yMax - 46f - (row + 1) * ShopSlotH - row * 8f;
            return new Rect(x, y, ShopSlotW, ShopSlotH);
        }

        public static Rect RerollRect()
        {
            if (ShopRerollOverride.HasValue) return ShopRerollOverride.Value;

            var panel = PanelRect();
            return new Rect(panel.center.x - 120f, panel.yMin + 12f, 240f, 34f);
        }

        // Satu-satunya tombol yang tersisa di kolom ini, dan ia cuma muncul saat toko buka.
        // Teman-temannya sudah pergi: label resep (bukan tombol), kotak JUAL (LANJUT yang
        // menjual sekarang), dan PETA (tombol M sudah melakukan hal yang sama).
        public static Rect ShopButtonRect() => new Rect(RightX(), ButtonRowY, 120, 34);

        // ---------- layar GAME OVER ----------
        //
        // Ditaruh di tengah dan BESAR: ini satu-satunya layar di mana pemain tidak sedang
        // melakukan apa-apa, jadi tidak ada yang perlu dihindari — dan tombol kecil di layar
        // kalah membuat pemain mengira run-nya menggantung, bukan berakhir.

        public const int OverButtonW = 340;
        public const int OverButtonH = 64;

        public static Rect GameOverMenuRect()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f - 60f;
            return new Rect(cx - OverButtonW * 0.5f, cy - OverButtonH * 0.5f, OverButtonW, OverButtonH);
        }

        /// <summary>Panel peta run saat MEMILIH: satu layar penuh, tegak ala Slay the Spire —
        /// lantai menumpuk dari bawah ke atas dengan jarak tetap, sisanya diintip lewat scroll.
        /// Saat memilih, peta bukan jendela di atas HUD; dialah layarnya.</summary>
        public static Rect MapPanelRect() => new Rect(0f, 0f, Screen.width, Screen.height);

        /// <summary>
        /// Panel peta saat MENGINTIP (M): kotak melayang di tengah, bukan satu layar penuh.
        ///
        /// Bedanya disengaja dan bukan sekadar hemat ruang — ukuran itu yang memberi tahu pemain
        /// apakah ia sedang mengambil keputusan atau cuma melirik. Layar penuh menuntut jawaban;
        /// kotak melayang boleh ditutup lagi tanpa memutuskan apa pun. Lapangan yang masih
        /// kelihatan di sekelilingnya bagian dari pesan itu.
        /// </summary>
        public static Rect MapPeekRect(Vector2 fraction)
        {
            float w = Screen.width * Mathf.Clamp(fraction.x, 0.3f, 0.95f);
            float h = Screen.height * Mathf.Clamp(fraction.y, 0.3f, 0.95f);

            // Dibulatkan ke piksel bulat: panel yang mendarat di setengah piksel membuat perkamen
            // dan bingkainya tersaring buram di tepi.
            w = Mathf.Round(w);
            h = Mathf.Round(h);

            return new Rect(Mathf.Round((Screen.width - w) * 0.5f),
                            Mathf.Round((Screen.height - h) * 0.5f), w, h);
        }

        // ---------- loose drops ----------

        /// <summary>Drops scatter anywhere on screen, clear of the left column and the HUD strip.</summary>
        public static Vector2 RandomScatterPos()
        {
            float left = RightX() + 60f;
            float right = Mathf.Max(left + 160f, Screen.width - 80f);
            float bottom = 70f;
            float top = Mathf.Max(bottom + 120f, Screen.height - 200f);

            return new Vector2(Random.Range(left, right), Random.Range(bottom, top));
        }

        public static Vector2 NearScatterPos(Vector2 near, int index)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f) + index * 1.1f;
            float dist = Random.Range(70f, 120f);

            var pos = near + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
            pos.x = Mathf.Clamp(pos.x, 70f, Mathf.Max(90f, Screen.width - 70f));
            pos.y = Mathf.Clamp(pos.y, 70f, Mathf.Max(90f, Screen.height - 190f));
            return pos;
        }
    }
}
