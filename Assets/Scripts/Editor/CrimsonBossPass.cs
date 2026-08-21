using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Membuat varian merah menyala dari boss ular, dan menyambungkannya ke
    /// <see cref="ContentDatabase"/>.
    ///
    /// Seluruh angka tuningnya DISALIN dari Boss_serpent, bukan diketik ulang. Boss varian yang
    /// menyimpan salinan angkanya sendiri akan diam-diam menyimpang begitu ularnya di-tuning: dua
    /// boss dengan nama berbeda tapi perilaku yang seharusnya sama, dan tidak ada satu pun tempat
    /// yang mengatakan mana yang benar. Yang benar-benar milik varian ini cuma identitas dan
    /// tampilannya — dan itulah satu-satunya yang ditimpa di bawah.
    ///
    /// Idempoten: dicocokkan lewat path aset, ditimpa, bukan diduplikasi.
    /// </summary>
    public static class CrimsonBossPass
    {
        const string Root   = "Assets/GameData";
        const string Db     = Root + "/ContentDatabase.asset";
        const string Source = Root + "/Enemies/Boss_serpent.asset";
        const string Path   = Root + "/Enemies/Boss_serpent_crimson.asset";

        const string Folder = "Assets/Art/Characters/Bosses/SnakeBoss";

        [MenuItem("Tools/Grimoire/Buat Varian Boss Crimson")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Db);
            if (db == null)
            {
                Debug.LogError("[Crimson] ContentDatabase.asset tidak ketemu.");
                return;
            }

            var source = AssetDatabase.LoadAssetAtPath<BossDefinition>(Source);
            if (source == null)
            {
                Debug.LogError($"[Crimson] {Source} tidak ketemu — jalankan Generate Boss dulu.");
                return;
            }

            var boss = AssetDatabase.LoadAssetAtPath<BossDefinition>(Path);
            bool fresh = boss == null;

            if (fresh)
            {
                boss = ScriptableObject.CreateInstance<BossDefinition>();
                AssetDatabase.CreateAsset(boss, Path);
            }

            // CopySerialized ikut menyalin m_Name, jadi namanya harus dikembalikan sesudahnya —
            // kalau tidak, asetnya muncul sebagai "Boss_serpent" kedua di inspector sementara
            // nama filenya tetap crimson, dan yang mana yang mana jadi tebak-tebakan.
            EditorUtility.CopySerialized(source, boss);
            boss.name = "Boss_serpent_crimson";

            boss.Id = "serpentcrimson";
            boss.DisplayName = "The Ember Coil";

            // Niat disimpan sebagai NAMA BERKAS, bukan cuma sebagai referensi mesh.
            //
            // BossModelPass menimpa HeadMesh/BodyMesh setiap kali dijalankan; yang bertahan dari
            // klik menu berikutnya hanyalah nama berkasnya. Referensinya tetap dipasang di bawah
            // supaya varian ini langsung terlihat benar tanpa harus menjalankan pass yang lain.
            boss.HeadMeshFile = "SK_Snake_Head_Horned";
            boss.BodyMeshFile = "SK_Snake_Segment_Spiked";
            boss.TailMeshFile = "";                  // ekornya sama; kosong = pakai bawaan
            boss.BoneSkinFile = "";                  // isi "SnakeBoss_Albedo_Charred" untuk versi bara

            var head = MeshAt($"{Folder}/{boss.HeadMeshFile}.fbx");
            var body = MeshAt($"{Folder}/{boss.BodyMeshFile}.fbx");

            if (head != null) { boss.HeadMesh = head; boss.HeadMeshScale = UnitScale(head); }
            if (body != null) { boss.BodyMesh = body; boss.BodyMeshScale = UnitScale(body); }

            if (head == null || body == null)
            {
                Debug.LogWarning($"[Crimson] mesh varian belum terimpor (head={head != null} " +
                                 $"body={body != null}). Nama berkasnya sudah tersimpan — jalankan " +
                                 $"Tools/Grimoire/Pasang Model Boss setelah Unity selesai impor.");
            }

            // Merah, lalu pendar HDR di atasnya.
            //
            // Ambang bloom terendah di look profile ada di 0,8 dan tertinggi 1,25. Nilai di bawah
            // itu tidak menyala sama sekali — jadi angkanya sengaja jauh di atas satu, bukan
            // "merah terang". Kepala dibuat lebih menyala daripada badan dengan sengaja: kepala
            // satu-satunya bagian yang menggigit, dan pemain harus menemukannya dalam sekejap.
            // Merahnya sengaja TIDAK pekat.
            //
            // _BaseColor mengalikan tekstur tulang. Merah pekat (1, 0.18, 0.12) akan menekan hijau
            // dan biru hampir ke nol, dan yang tersisa bukan tulang merah melainkan siluet merah
            // rata — grain tulangnya hilang seluruhnya. Itu persis kesalahan yang membuat versi
            // putihnya tampak seperti plastik.
            //
            // Yang membuatnya MENYALA adalah emisi di bawah, bukan tint ini. Tugas tint cuma
            // memberi warna dasar; tugas emisi memberi baranya.
            boss.HeadColor = new Color(1.00f, 0.40f, 0.32f);
            boss.BodyColor = new Color(0.70f, 0.26f, 0.22f);

            boss.HeadEmission = new Color(2.60f, 0.34f, 0.16f);
            boss.BodyEmission = new Color(1.45f, 0.16f, 0.09f);

            EditorUtility.SetDirty(boss);

            db.EditorAddBoss(boss);
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Crimson] '{boss.DisplayName}' {(fresh ? "dibuat" : "diperbarui")}: " +
                      $"{boss.MaxSegments} ruas, kepala bertanduk, ruas berduri, " +
                      $"pendar {boss.HeadEmission}. Tersambung ke database.");

            Selection.activeObject = boss;
        }

        static float UnitScale(Mesh mesh) => 1f / Mathf.Max(0.0001f, mesh.bounds.size.z);

        static Mesh MeshAt(string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is Mesh m) return m;
            }

            return null;
        }
    }
}
