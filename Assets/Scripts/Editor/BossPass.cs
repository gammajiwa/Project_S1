using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Membuat aset boss ular dan menyambungkannya ke <see cref="ContentDatabase"/>.
    /// Idempoten: dicocokkan lewat path aset, dan menimpa nilainya, bukan membuat duplikat.
    /// </summary>
    public static class BossPass
    {
        const string Root = "Assets/GameData";
        const string Path = Root + "/Enemies/Boss_serpent.asset";

        [MenuItem("Tools/Grimoire/Generate Boss")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[BossPass] ContentDatabase.asset tidak ketemu.");
                return;
            }

            var boss = AssetDatabase.LoadAssetAtPath<BossDefinition>(Path);

            if (boss == null)
            {
                boss = ScriptableObject.CreateInstance<BossDefinition>();
                AssetDatabase.CreateAsset(boss, Path);
            }

            boss.Id = "serpent";
            boss.DisplayName = "The Coiled Dread";

            boss.MaxSegments = 24;
            boss.MinSegments = 4;
            boss.Spacing = 1.05f;
            boss.HeadScale = 2.6f;
            boss.TailScale = 0.85f;

            // Cukup tebal untuk sempat menunjukkan perilakunya. Boss yang mati dalam dua cast
            // adalah musuh besar, bukan boss — tidak ada yang sempat terbaca.
            boss.HpMultiplier = 90f;

            boss.OrbitRadius = 13f;
            boss.Speed = 6.5f;
            boss.LungeSpeed = 15f;
            boss.TurnRate = 150f;
            boss.Wander = 1.4f;

            boss.LungeInterval = 6f;
            boss.LungeDuration = 1.8f;

            // Satu hantaman keras, bukan gerusan per detik. Yang harus dipelajari pemain adalah
            // kapan terjangannya datang, dan itu cuma punya arti kalau kena telak.
            boss.BiteDamage = 26f;
            boss.BiteRange = 2.8f;
            boss.Curse = FindCurse(db, "leaden");

            // Putih, dan itu keputusan — bukan "belum diwarnai".
            //
            // Kedua angka ini dikalikan ke _BaseColor, DI ATAS tekstur tulang. Merah-ungu di sini
            // ditulis waktu boss masih kapsul polos, ketika warna memang seluruh penampilannya.
            // Begitu ada tekstur di bawahnya, mengalikannya dengan merah pekat tidak menghasilkan
            // "tulang bernuansa merah" — ia menghapus teksturnya dan menyisakan plastik.
            //
            // Kepala dibuat putih penuh dan badan sedikit lebih redup: pemain tetap harus bisa
            // menemukan kepala dalam sekejap, dan sekarang bentuknya sendiri (tengkorak vs ruas)
            // sudah melakukan sebagian besar pekerjaan itu — jadi bedanya cukup terang-redup,
            // tidak perlu beda warna.
            //
            // Yang MENYALA merah adalah varian crimson, dan itu memang inti bedanya.
            boss.HeadColor = Color.white;
            boss.BodyColor = new Color(0.78f, 0.76f, 0.72f);

            boss.HeadEmission = Color.black;
            boss.BodyEmission = Color.black;

            EditorUtility.SetDirty(boss);

            db.EditorAddBoss(boss);
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Centipede(db);
            Grub(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BossPass] '{boss.DisplayName}' siap: {boss.MaxSegments} ruas, " +
                      $"HP x{boss.HpMultiplier}, gigit {boss.BiteDamage}. Tersambung ke database.");
            Selection.activeObject = boss;
        }

        /// <summary>
        /// Kelabang raksasa yang menyelam ke tanah lalu menyembur keluar.
        ///
        /// Perhatikan berapa sedikit yang berbeda dari ular: badannya menapaki jejak kepala, dan
        /// jejak itu menyimpan ketinggian — jadi begitu kepalanya menukik dan melengkung naik,
        /// seluruh badan mengikuti busur itu sendiri. Tidak ada animasi badan yang ditulis.
        /// </summary>
        static void Centipede(ContentDatabase db)
        {
            var a = Load("Boss_centipede");

            a.Id = "centipede";
            a.DisplayName = "The Gorging Coil";
            a.Burrows = true;

            // Lebih panjang dari ular: ia harus terbaca sebagai satu barisan yang tak
            // habis-habis saat menyembur keluar tanah. Jaraknya disetel di bawah, sesudah
            // model cacingnya dipasang — lihat komentar di sana.
            a.MaxSegments = 30;
            a.MinSegments = 5;
            a.HeadScale = 2.9f;
            a.TailScale = 0.7f;

            a.HpMultiplier = 110f;

            // Tidak mengitari — ia MENGEJAR di bawah tanah lalu menyembur tepat di kaki pemain.
            a.Speed = 7.5f;
            a.LungeSpeed = 7.5f;
            a.TurnRate = 110f;
            a.Wander = 0.6f;
            a.OrbitRadius = 4f;

            // Melompat tiga kali beruntun, lalu hilang lima setengah detik. Busurnya cuma 1,1
            // detik: kepala yang bertahan lama di atas tanah tidak terbaca sebagai melompat.
            a.ArcDuration = 1.1f;
            a.ArcHeight = 5.5f;
            a.BreachBurst = 3;
            a.DipDuration = 0.4f;
            a.DiveInterval = 5.5f;
            a.DiveDepth = 6f;

            // Melintasi pemain ITU serangannya. Tidak ada terjangan terpisah.
            a.BiteDamage = 30f;
            a.BiteRange = 3.2f;
            a.LungeInterval = 999f;
            a.Curse = FindCurse(db, "weakened");

            a.SpitInterval = 0.7f;
            a.SpitDamage = 15f;
            a.SpitSpeed = 12f;
            a.SpitCurse = FindCurse(db, "drained");

            // Badan HITAM, kepala MERAH — kepalanya satu-satunya yang menggigit, dan di badan
            // sepanjang tiga puluh ruas yang bentuknya sama, warna itulah yang memberi tahu
            // pemain ujung mana yang berbahaya.
            //
            // Hitamnya 0,16 dan bukan 0. Angka ini dikalikan ke tekstur, jadi nol berarti
            // seluruh grain chitin-nya hilang dan yang tersisa siluet hitam rata — kelabangnya
            // berhenti terlihat seperti benda dan mulai terlihat seperti lubang di layar.
            a.HeadColor = new Color(1f, 0.22f, 0.18f);
            a.BodyColor = new Color(0.16f, 0.16f, 0.19f);

            a.HeadEmission = Color.black;
            a.BodyEmission = Color.black;

            // Model CACING, disimpan sebagai NAMA BERKAS supaya bertahan dari BossModelPass
            // berikutnya — pass itu menimpa referensi mesh semua boss dengan model bawaan.
            //
            // Perintah pemilik project: "yg GEDE pake CACING (worm), yg KECIL pake KELABANG
            // (centipede)". Pemetaan ini sempat dipasang tangan di asetnya lalu terhapus begitu
            // pass ini dijalankan lagi — sekarang ia hidup di sini, jadi ia bertahan.
            a.HeadMeshFile = "SK_Worm_Head";
            a.BodyMeshFile = "SK_Worm_Segment";
            a.TailMeshFile = "SK_Worm_Tail";
            a.BoneSkinFile = "SnakeBoss_Albedo_Flesh";

            // Kepala cacing TIDAK butuh 180 derajat tambahan seperti kepala ular. Angka lama
            // diwarisi membabi buta dari aset SnakeBoss, dan hasilnya dilaporkan pemilik
            // project: "kepalanya si cacing kebalik".
            a.HeadMeshRotation = new Vector3(0f, 0f, 90f);
            a.BodyMeshRotation = new Vector3(0f, 0f, 90f);
            a.TailMeshRotation = new Vector3(0f, 0f, 90f);

            // Mengebor: badan berguling satu arah, kepala berlawanan.
            a.SpinDegreesPerSecond = 110f;

            // Ruasnya DIRENGGANGKAN. Laporan pemilik project: "badan sama kepalanya terlalu
            // nempel, kasih jarak di antara badan-badannya".
            //
            // 0,85 itu angka warisan dari waktu boss ini masih memakai model kelabang yang jauh
            // lebih kecil. Kepalanya sekarang 2,9 — paling besar di antara ketiga boss — sementara
            // jaraknya paling rapat, dan ruas yang lebih besar dari jaraknya sendiri saling
            // menembus sampai seluruh badannya terbaca sebagai satu batang pejal.
            //
            // Pembandingnya ular: kepala 2,6 dengan jarak 1,05. Diskalakan ke kepala 2,9 itu
            // 1,17 — dan dilebihkan sedikit ke 1,35 karena yang diminta memang JARAK yang
            // terlihat, bukan sekadar berhenti saling menembus.
            a.Spacing = 1.35f;

            EditorUtility.SetDirty(a);
            db.EditorAddBoss(a);
        }

        /// <summary>Versi kecilnya, ikut wave biasa. Sistem yang sama, angka yang jauh lebih kecil.</summary>
        static void Grub(ContentDatabase db)
        {
            var a = Load("Boss_grub");

            a.Id = "grub";
            a.DisplayName = "Coilspawn";
            a.Burrows = true;
            a.Minion = true;
            a.MinionFromWave = 6;
            a.MinionCount = 2;

            a.MaxSegments = 7;
            a.MinSegments = 3;
            a.Spacing = 0.6f;
            a.HeadScale = 1.15f;
            a.TailScale = 0.45f;

            // Setara beberapa musuh biasa, bukan boss. Ia mengganggu, bukan menghadang —
            // tapi di 5 ia mati sebelum sempat menyengat, dan gangguannya tinggal visual.
            a.HpMultiplier = 8f;

            a.Speed = 6.5f;
            a.LungeSpeed = 6.5f;
            a.TurnRate = 140f;
            a.Wander = 0.9f;
            a.OrbitRadius = 3f;

            a.ArcDuration = 0.8f;
            a.ArcHeight = 2.6f;
            a.BreachBurst = 2;
            a.DipDuration = 0.3f;
            a.DiveInterval = 3.6f;
            a.DiveDepth = 3.5f;

            a.BiteDamage = 12f;
            a.BiteRange = 1.7f;
            a.LungeInterval = 999f;
            a.Curse = null;

            a.SpitInterval = 1.4f;
            a.SpitDamage = 8f;
            a.SpitSpeed = 10f;
            a.SpitCurse = null;

            // Warnanya datang dari TEKSTUR, bukan dari tint — jadi tint-nya dibiarkan mendekati
            // putih. Cacing ini memakai kulit daging yang memang sudah merah muda, dan mengalikan
            // merah muda dengan merah muda lagi cuma menjenuhkan warnanya sampai grain-nya hilang.
            a.HeadColor = new Color(1f, 0.97f, 0.97f);
            a.BodyColor = new Color(0.88f, 0.84f, 0.85f);

            a.HeadEmission = Color.black;
            a.BodyEmission = Color.black;

            // Anak buah memakai KELABANG — yang kecil, sesuai perintah pemilik project.
            a.HeadMeshFile = "SK_Centipede_Head";
            a.BodyMeshFile = "SK_Centipede_Segment";
            a.TailMeshFile = "SK_Centipede_Tail";
            a.BoneSkinFile = "SnakeBoss_Albedo_Flesh";

            // Ditulis eksplisit, bukan dibiarkan kosong: sejak boss boleh punya mesh sendiri,
            // BossModelPass TIDAK lagi menimpa rotasi boss ber-mesh sendiri. Yang dibiarkan
            // kosong akan tampil BERDIRI — dan diamnya jauh lebih membingungkan daripada
            // terbalik. Angka kelabang belum pernah dinilai mata; ini nilai ular, dan kalau
            // salah, yang diubah tiga angka di sini.
            a.HeadMeshRotation = new Vector3(0f, 180f, 90f);
            a.BodyMeshRotation = new Vector3(0f, 0f, 90f);
            a.TailMeshRotation = new Vector3(0f, 0f, 90f);

            // Ditulis NOL secara eksplisit, bukan dibiarkan. Aset ini pernah memegang model
            // cacing pada putaran sebelumnya dan ikut menerima nilai gulingnya; field yang
            // dibiarkan tidak disentuh akan menyimpan nilai itu selamanya, dan kelabang kecil
            // akan mengebor tanpa ada satu baris pun di kode yang menyuruhnya.
            a.SpinDegreesPerSecond = 0f;

            EditorUtility.SetDirty(a);
            db.EditorAddBoss(a);
        }

        static BossDefinition Load(string fileName)
        {
            string path = Root + "/Enemies/" + fileName + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<BossDefinition>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BossDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        /// <summary>
        /// Kutukan gigitannya. LEADEN dipilih karena memperlambat, dan itu justru yang paling
        /// menyakitkan dari makhluk yang seluruh ancamannya adalah menerjang.
        /// </summary>
        static BuffDefinition FindCurse(ContentDatabase db, string id)
        {
            for (int i = 0; i < db.Debuffs.Count; i++)
            {
                var curse = db.Debuffs[i];
                if (curse != null && curse.Id == id) return curse;
            }

            return db.Debuffs.Count > 0 ? db.Debuffs[0] : null;
        }
    }
}
