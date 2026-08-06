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
            // Dijaga di DEPAN, bukan dibiarkan meletus di tengah. OpenScene melempar di play mode,
            // dan saat itu aset biome-nya sudah terlanjur ditulis — jadi asetnya baru sementara
            // scene-nya masih menunjuk yang lama, dan tidak ada tanda apa pun bahwa keduanya
            // sekarang tidak cocok.
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[BiomePass] Tidak bisa jalan di play mode. Stop dulu, baru ulangi.");
                return;
            }

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

            // LAPANGAN TERANG, DIBINGKAI KEGELAPAN. Ini yang sebenarnya terjadi di Cult of the
            // Lamb, dan dua percobaan sebelumnya salah membacanya dari dua arah berlawanan.
            //
            // Lantainya di sana justru PUCAT — hijau sage terang. Yang gelap adalah tepi layar dan
            // kejauhannya. Jadi "gelap tapi bersinar" bukan lapangan gelap: itu lapangan terang
            // yang dikelilingi gelap, dan kontras antara keduanya yang bikin terbaca berkilau.
            //
            // Yang mengerjakan pembingkaian itu KABUT dan VIGNETTE, bukan warna tanahnya.
            forest.GroundColor = new Color(0.5f, 0.58f, 0.42f);
            forest.HorizonColor = new Color(0.05f, 0.07f, 0.08f);

            forest.SunColor = new Color(1f, 0.93f, 0.76f);
            forest.SunPitch = 26f;
            forest.SunYaw = 42f;
            forest.SunIntensity = 2.4f;

            // Cukup terang supaya yang teduh tetap terbaca, dan condong dingin supaya bayangannya
            // berwarna biru alih-alih abu-abu mati.
            forest.AmbientSky = new Color(0.44f, 0.5f, 0.54f);
            forest.AmbientEquator = new Color(0.4f, 0.43f, 0.36f);
            forest.AmbientGround = new Color(0.26f, 0.3f, 0.22f);

            // Kabut RAPAT dan DEKAT, dan ini bagian yang paling menentukan.
            //
            // Kamera duduk 18,5 unit di atas dan menunduk, jadi lantai di BAWAH layar berjarak
            // ~19 unit sementara yang di ATAS layar berjarak ~27-36. Kabut yang mulai di 21 dan
            // penuh di 44 karena itu cuma menyentuh bagian atas layar: sekitar pemain tetap
            // terang, kejauhan meluruh jadi hitam. Persis pembingkaian di referensinya.
            forest.FogEnabled = true;
            forest.FogColor = new Color(0.05f, 0.08f, 0.09f);
            forest.FogStart = 21f;
            forest.FogEnd = 44f;

            // Awan lebih tipis: lantainya sekarang terang, dan bayangan yang terlalu pekat di atas
            // lantai terang terbaca sebagai noda, bukan sebagai awan lewat.
            forest.CloudColor = new Color(0.06f, 0.1f, 0.09f, 0.34f);
            forest.CloudSize = 46f;
            forest.CloudCoverage = 0.5f;
            forest.CloudSpeed = 0.012f;

            // Berkas cahaya dinaikkan — di referensinya inilah yang paling terlihat, melintang
            // lebar di lantai. Lebih lebar dan lebih terang dari percobaan sebelumnya.
            forest.RayColor = new Color(1f, 0.94f, 0.7f, 0.4f);
            forest.RaySize = 34f;
            forest.RayCoverage = 0.45f;

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
                new Color(0.32f, 0.26f, 0.22f),
                new Color(0.24f, 0.2f, 0.18f),
                new Color(0.4f, 0.32f, 0.25f)
            };

            // Hijau SEDANG, bukan gelap dan bukan neon. Di referensinya dedaunan duduk cuma
            // sedikit lebih gelap dari rumputnya — yang memisahkan pohon dari lantai adalah
            // bayangannya, bukan warnanya. Satu aksen amber untuk titik hangat.
            forest.CanopyColors = new[]
            {
                new Color(0.26f, 0.42f, 0.24f),
                new Color(0.33f, 0.5f, 0.27f),
                new Color(0.19f, 0.32f, 0.22f),
                new Color(0.55f, 0.42f, 0.18f)
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
                new Color(0.3f, 0.46f, 0.26f),
                new Color(0.38f, 0.54f, 0.3f),
                new Color(0.23f, 0.36f, 0.24f),
                new Color(0.48f, 0.5f, 0.28f)
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
