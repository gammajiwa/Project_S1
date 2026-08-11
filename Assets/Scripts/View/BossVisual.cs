using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Penggambar boss ular: tiga mesh — kepala, ruas badan, ruas ekor — masing-masing dengan
    /// renderer sendiri.
    ///
    /// TIGA renderer, bukan satu, karena <see cref="EnemyRenderer"/> memegang tepat satu mesh.
    /// Itu bukan kelalaian melainkan syarat <c>Graphics.RenderMeshInstanced</c>, dan melonggarkannya
    /// berarti membongkar seluruh jalur batching gerombolan demi satu musuh. Tiga renderer justru
    /// murah: berapa pun ruas yang hidup, dan berapa pun ular yang mengitari sekaligus, harganya
    /// tetap tiga batch — karena semua ruas badan memakai mesh yang sama persis.
    ///
    /// Dipegang PER <see cref="BossDefinition"/>, jadi kelabang bisa memakai model yang sama sekali
    /// berbeda tanpa satu baris pun kode baru — cukup mengisi tiga slot mesh di asetnya.
    /// </summary>
    public class BossVisual
    {
        readonly EnemyRenderer _head;
        readonly EnemyRenderer _body;
        readonly EnemyRenderer _tail;

        /// <summary>
        /// Null kalau def ini belum diberi mesh. Null adalah jawaban yang SAH, bukan kegagalan:
        /// pemanggilnya memakainya untuk kembali menggambar kapsul seperti sebelum ada model.
        /// </summary>
        public static BossVisual TryBuild(BossDefinition def, Color[] swarmPalette, int capacity,
            int headTint, int bodyTint)
        {
            if (def == null) return null;
            if (def.HeadMesh == null || def.BodyMesh == null || def.TailMesh == null) return null;
            if (swarmPalette == null || swarmPalette.Length == 0) return null;

            return new BossVisual(def, swarmPalette, capacity, headTint, bodyTint);
        }

        BossVisual(BossDefinition def, Color[] swarmPalette, int capacity, int headTint, int bodyTint)
        {
            // Panjang palet WAJIB sama dengan palet gerombolan.
            //
            // Enemy.Tint adalah indeks ke palet itu, dipasang Paint() tanpa tahu siapa yang akan
            // menggambar ruasnya. Palet yang lebih pendek berarti ruas boss yang kebetulan terbakar
            // mengindeks keluar array — dan matinya bukan saat boss muncul, melainkan saat seseorang
            // pertama kali membakarnya, yang bisa berjam-jam kemudian.
            var palette = (Color[])swarmPalette.Clone();

            // Slot 0 diputihkan supaya tekstur tulangnya tampil apa adanya; slot 0 milik gerombolan
            // adalah warna daging musuh, dan mengalikannya ke tekstur tulang membuat kerangkanya
            // tampak dicelup. Slot ailment sengaja DIBIARKAN: boss yang terbakar tetap harus
            // terbaca merah seperti musuh lain.
            palette[0] = Color.white;

            // Warna boss ditimpa dengan warna DEF INI.
            //
            // BuildPalette mengisi kedua slot itu dari `_db.Boss` — boss PERTAMA yang masih hidup,
            // satu-satunya. Selama cuma ada satu boss ber-model, itu benar secara kebetulan. Begitu
            // ada varian kedua, keduanya mewarisi warna boss pertama dan varian yang seharusnya
            // merah tampil dengan warna saudaranya. Di sini BossVisual sudah per-def, jadi inilah
            // tempat paling awal yang benar-benar tahu warna siapa yang seharusnya dipakai.
            if (headTint >= 0 && headTint < palette.Length) palette[headTint] = def.HeadColor;
            if (bodyTint >= 0 && bodyTint < palette.Length) palette[bodyTint] = def.BodyColor;

            float headScale = Mathf.Max(0.0001f, def.HeadMeshScale);
            float bodyScale = Mathf.Max(0.0001f, def.BodyMeshScale);

            // animate: false. Bob squash-stretch milik gerombolan adalah animasi palsu untuk kapsul;
            // dipasang pada tulang, ia terbaca sebagai kerangka yang bernapas.
            //
            // Kapasitas ketiganya dibuat selapang badan, bukan dipas-paskan: satu EnemyManager bisa
            // memegang beberapa ular sekaligus dan semuanya menumpang renderer yang sama, jadi
            // "kepala cuma satu" hanya benar untuk satu ular.
            // Koreksi orientasi diambil PER MESH. Satu koreksi untuk ketiganya terlihat lebih
            // rapi dan tidak cukup: di aset SnakeBoss, kepala dan ekor diekspor menghadap arah
            // yang berlawanan, jadi apa pun yang membetulkan salah satunya membalik yang lain.
            _head = new EnemyRenderer(def.HeadMesh, palette, capacity, headScale,
                false, null, true, def.BoneSkin, def.HeadMeshRotation, def.HeadEmission);

            _body = new EnemyRenderer(def.BodyMesh, palette, capacity, bodyScale,
                false, null, true, def.BoneSkin, def.BodyMeshRotation, def.BodyEmission);

            // Ekor ikut pendar BADAN, bukan pendar kepala. Ia ujung dari barisan yang sama, dan
            // ekor yang menyala berbeda dari ruas di depannya terbaca sebagai potongan yang salah
            // tersambung — bukan sebagai ujung.
            _tail = new EnemyRenderer(def.TailMesh, palette, capacity, bodyScale,
                false, null, true, def.BoneSkin, def.TailMeshRotation, def.BodyEmission);
        }

        public void Begin()
        {
            _head.Begin();
            _body.Begin();
            _tail.Begin();
        }

        /// <summary>
        /// Menitipkan satu ruas. Indeks 0 kepala, indeks terakhir ekor, sisanya badan — urutan yang
        /// sama yang dipakai <see cref="BossSnake.SegmentPoint"/>, jadi tidak ada kesepakatan kedua
        /// yang bisa melenceng dari yang pertama.
        ///
        /// Ular sependek satu ruas menggambar kepala saja, dan itu benar: yang tersisa menjelang
        /// mati memang kepalanya.
        /// </summary>
        public void Add(int index, int count, Vector3 at, float yaw, float phase, int tint, float scale)
        {
            var target = index <= 0 ? _head
                       : index >= count - 1 ? _tail
                       : _body;

            target.Add(at, yaw, phase, tint, scale, 0f);
        }

        public void Draw(float time)
        {
            _head.Draw(time);
            _body.Draw(time);
            _tail.Draw(time);
        }

        public int Batches => _head.Batches + _body.Batches + _tail.Batches;
    }
}
