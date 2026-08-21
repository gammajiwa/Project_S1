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
            if (swarmPalette == null || swarmPalette.Length == 0) return null;

            // Syarat "tiga mesh" itu milik badan BERUAS. Menerapkannya ke naga akan menolak boss
            // yang model lengkapnya justru ada — cuma tersimpan di slot yang lain.
            if (def.Body == BossDefinition.BossBody.Winged)
            {
                if (def.Vat == null || def.Vat.Mesh == null) return null;
            }
            else if (def.HeadMesh == null || def.BodyMesh == null || def.TailMesh == null)
            {
                return null;
            }

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

            // Bersayap: SATU renderer, bukan tiga.
            //
            // Yang tiga itu syarat badan beruas — kepala, ruas yang diulang, penutup ekor. Naga
            // tidak punya ruas untuk diulang, jadi dua renderer sisanya akan berdiri kosong
            // seumur pertarungan sambil tetap membayar material dan buffer instansnya sendiri.
            //
            // Yang dipakai slot KEPALA, dan itu bukan pilihan bebas: Add() menyalurkan indeks 0
            // ke kepala, dan naga selalu satu ruas dengan indeks 0.
            if (def.Body == BossDefinition.BossBody.Winged)
            {
                var vat = def.Vat;

                // Skala mesh dibuat satu-per-tinggi-model, jadi HeadScale di aset langsung
                // berarti TINGGI NAGA DALAM UNIT DUNIA. Tanpa pembagian itu angkanya bergantung
                // pada seberapa besar model kebetulan diekspor — dan aset ini datang pada skala
                // seribu lima ratus kali, jadi angka yang wajar di inspector akan meleset jauh.
                float unit = 1f / Mathf.Max(0.0001f, vat.Height);

                // animate: true, dan itu saklar yang menentukan apakah shader membaca tekstur
                // panggangannya sama sekali. Yang beruas memakai false karena bob squash-stretch
                // di sana adalah animasi palsu; di sini animasinya nyata.
                _head = new EnemyRenderer(vat.Mesh, palette, capacity, unit, true, vat, true,
                    vat.SourceMaterial != null ? vat.SourceMaterial.GetTexture("_BaseMap") : null,
                    def.HeadMeshRotation, def.HeadEmission);

                _spin = 0f;
                return;
            }

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

            _spin = def.SpinDegreesPerSecond;
        }

        /// <summary>Derajat per detik badan berguling. Kepala mengambil nilai yang sama, negatif.</summary>
        readonly float _spin;

        /// <summary>
        /// Kemiringan badan bersayap, derajat. Menumpang slot roll yang sama dengan gulingan
        /// cacing — untuk yang bersayap _spin selalu nol, jadi keduanya tidak pernah berebut.
        /// </summary>
        public void SetBank(float degrees) => _head.SetRoll(degrees);

        public void Begin()
        {
            _head.Begin();
            if (_body != null) _body.Begin();
            if (_tail != null) _tail.Begin();
        }

        /// <summary>
        /// Menitipkan satu ruas. Indeks 0 kepala, indeks terakhir ekor, sisanya badan — urutan yang
        /// sama yang dipakai <see cref="BossSnake.SegmentPoint"/>, jadi tidak ada kesepakatan kedua
        /// yang bisa melenceng dari yang pertama.
        ///
        /// Ular sependek satu ruas menggambar kepala saja, dan itu benar: yang tersisa menjelang
        /// mati memang kepalanya.
        /// </summary>
        /// <param name="speed01">
        /// Yang memilih klip panggangan: diam, jalan, lari, atau — di atas
        /// <see cref="EnemyRenderer.AttackSpeed"/> — menyerang. Nol untuk yang beruas, yang
        /// meshnya memang tidak beranimasi.
        /// </param>
        public void Add(int index, int count, Vector3 at, float yaw, float phase, int tint,
            float scale, float speed01 = 0f)
        {
            var target = index <= 0 ? _head
                       : index >= count - 1 ? _tail
                       : _body;

            if (target == null) return;

            target.Add(at, yaw, phase, tint, scale, speed01);
        }

        public void Draw(float time)
        {
            if (Mathf.Abs(_spin) > 0.0001f)
            {
                // Berlawanan, dan sengaja BUKAN sekadar dua kecepatan berbeda. Yang dibaca mata
                // sebagai mengebor adalah PERLAWANAN — dua bagian yang berputar ke arah yang
                // sama, secepat apa pun, cuma terbaca sebagai satu benda yang berguling.
                float roll = time * _spin;
                _head.SetRoll(-roll);
                if (_body != null) _body.SetRoll(roll);
                if (_tail != null) _tail.SetRoll(roll);
            }

            _head.Draw(time);
            if (_body != null) _body.Draw(time);
            if (_tail != null) _tail.Draw(time);
        }

        public int Batches => _head.Batches
                            + (_body != null ? _body.Batches : 0)
                            + (_tail != null ? _tail.Batches : 0);
    }
}
