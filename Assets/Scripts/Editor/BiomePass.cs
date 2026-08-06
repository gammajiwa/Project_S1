using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Membuat SATU biome hutan dan menyambungkannya ke `_Bootstrap` di scene game.
    ///
    /// Satu, bukan empat. Empat wajah arena yang bergantian terdengar seperti kedalaman, dan yang
    /// dihasilkan justru sebaliknya: tidak ada satu pun yang sempat dikenali, dan tidak ada satu
    /// pun yang bisa dipoles sampai benar-benar bagus. Satu tempat yang digarap sampai selesai
    /// mengalahkan empat tempat yang setengah jadi.
    ///
    /// Idempoten — dicocokkan lewat path aset.
    /// </summary>
    public static class BiomePass
    {
        const string Root = "Assets/GameData";
        const string Folder = Root + "/Biomes";
        const string ScenePath = "Assets/Scenes/Proto.unity";

        [MenuItem("Tools/Grimoire/Generate Biomes")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Root, "Biomes");

            string path = Folder + "/Biome_forest.asset";
            var forest = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(path);

            if (forest == null)
            {
                forest = ScriptableObject.CreateInstance<BiomeDefinition>();
                AssetDatabase.CreateAsset(forest, path);
            }

            forest.Id = "forest";
            forest.DisplayName = "Verdant Hollow";

            forest.GroundColor = new Color(0.13f, 0.19f, 0.12f);
            forest.HorizonColor = new Color(0.07f, 0.11f, 0.09f);

            // Matahari rendah dan hangat, menembus dari samping. Sudut inilah yang membuat batang
            // pohon melempar bayangan panjang melintasi lapangan — itu separuh dari kesan hutan,
            // dan hilang total begitu mataharinya dinaikkan ke atas kepala.
            forest.SunColor = new Color(1f, 0.94f, 0.74f);
            forest.SunPitch = 26f;
            forest.SunYaw = 42f;
            forest.SunIntensity = 1.3f;
            forest.AmbientColor = new Color(0.18f, 0.23f, 0.19f);

            // Jarang DAN pendek. Pohon setinggi 8 unit di kamera ortografis menutupi seperempat
            // layar sendirian, dan yang tertutup selalu gerombolan — satu-satunya hal yang harus
            // dibaca pemain. Yang dicari adalah padang terbuka dengan pohon sebagai penanda jarak,
            // bukan rimba yang menghalangi.
            forest.TreeCount = 55;
            forest.TrunkHeightRange = new Vector2(2.2f, 4f);
            forest.TrunkWidthRange = new Vector2(0.18f, 0.34f);
            forest.CanopyWidthRatio = new Vector2(0.5f, 0.8f);
            forest.CanopyFlatten = 0.6f;

            forest.TrunkColors = new[]
            {
                new Color(0.19f, 0.14f, 0.11f),
                new Color(0.24f, 0.17f, 0.12f),
                new Color(0.15f, 0.12f, 0.1f)
            };

            // Empat hijau, bukan satu. Kanopi sewarna semua terbaca sebagai satu benda raksasa;
            // variasinya yang memisahkan pohon dari pohon di kejauhan.
            forest.CanopyColors = new[]
            {
                new Color(0.15f, 0.31f, 0.16f),
                new Color(0.21f, 0.41f, 0.19f),
                new Color(0.12f, 0.25f, 0.15f),
                new Color(0.27f, 0.45f, 0.21f)
            };

            // Rumput dipangkas keras. Instancing memang murah di GPU, tapi tiap rumpun tetap satu
            // matriks di CPU dan satu bayangan yang dihitung — seribu empat ratus rumpun di
            // dua puluh lima petak adalah puluhan ribu, dan di situlah fps-nya jatuh.
            // Yang dibutuhkan cuma cukup untuk memecah lantai polos, bukan menutupinya.
            forest.ScatterCount = 260;
            forest.ScatterShape = PrimitiveType.Cube;
            forest.ScatterScaleRange = new Vector2(0.12f, 0.3f);
            forest.ScatterFlatten = 2.4f;

            forest.ScatterColors = new[]
            {
                new Color(0.19f, 0.33f, 0.15f),
                new Color(0.25f, 0.4f, 0.17f),
                new Color(0.15f, 0.27f, 0.14f),
                new Color(0.3f, 0.42f, 0.19f)
            };

            // Lebih lebar dari radius biasanya: pemain memulai di sini, dan wave pertama tidak
            // boleh dibuka dengan pandangan yang terhalang batang pohon.
            forest.ClearingRadius = 10f;

            EditorUtility.SetDirty(forest);
            AssetDatabase.SaveAssets();

            // TIDAK ada Refresh() di sini. Refresh menjadwalkan impor ulang, dan selama impor itu
            // berjalan LoadAssetAtPath mengembalikan null untuk aset yang jelas-jelas ada di disk —
            // yang tersimpan ke scene lalu jadi slot kosong, tanpa satu pun error.
            Attach(new[] { path });

            Debug.Log($"[BiomePass] '{forest.DisplayName}' siap: {forest.TreeCount} pohon, " +
                      $"{forest.ScatterCount} semak. Tersambung ke _Bootstrap.");
        }

        static void Attach(string[] paths)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
            var boot = Object.FindFirstObjectByType<ProtoBootstrap>();

            if (boot == null)
            {
                Debug.LogWarning("[BiomePass] _Bootstrap tidak ketemu di " + ScenePath +
                                 " — aset biome sudah dibuat, tapi belum tersambung.");
                return;
            }

            var so = new SerializedObject(boot);

            // Dua tahap, dan urutannya wajib. Mengubah ukuran array lalu langsung mengisi elemennya
            // dalam sesi SerializedObject yang sama akan menyimpan ukurannya saja: referensi
            // objeknya hilang tanpa satu pun error, dan yang tersimpan adalah slot kosong.
            so.FindProperty("_biomes").arraySize = paths.Length;
            so.ApplyModifiedPropertiesWithoutUndo();

            so.Update();
            var list = so.FindProperty("_biomes");

            for (int i = 0; i < paths.Length; i++)
            {
                var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(paths[i]);

                if (biome == null)
                {
                    Debug.LogError("[BiomePass] gagal memuat " + paths[i] +
                                   " — slot biome ini akan kosong. Jalankan ulang pass-nya.");
                    continue;
                }

                list.GetArrayElementAtIndex(i).objectReferenceValue = biome;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(boot);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
    }
}
