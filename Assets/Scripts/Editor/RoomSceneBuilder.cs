using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Proto.EditorTools
{
    /// <summary>
    /// Membangun tiga scene ruangan singgah: toko, kejadian, dan slot.
    ///
    /// Isinya sengaja SEDIKIT — lantai, dinding belakang, cahaya, dan kamera. Panel toko/kejadian/
    /// slot tetap milik <see cref="GrimoireUI"/> dan digambar di kanvas yang sama seperti dulu;
    /// yang dikerjakan scene ini cuma menyediakan LATAR supaya panel itu tidak lagi mengambang di
    /// atas rumput yang barusan berdarah.
    ///
    /// Pembagian kerjanya disengaja: kode membuat ruangannya ADA dan bisa ditukar, menghiasnya
    /// pekerjaan tangan di editor. Scene yang dibangun ulang lewat menu ini akan menimpa hiasan
    /// itu, jadi begitu sebuah ruangan mulai didandani, berhenti menjalankan menu ini untuknya.
    /// </summary>
    public static class RoomSceneBuilder
    {
        const string Folder = "Assets/Scenes";

        struct Room
        {
            public string Scene;
            public string Title;
            public Color Floor;
            public Color Wall;
            public Color Light;
        }

        static readonly Room[] Rooms =
        {
            // Warnanya diambil dari warna node peta masing-masing, supaya ruangan yang dimasuki
            // terbaca sebagai tempat yang barusan diklik - bukan sebagai ruangan acak.
            new Room { Scene = RoomLoader.ShopScene,  Title = "Toko",
                       Floor = new Color(0.24f, 0.20f, 0.15f), Wall = new Color(0.16f, 0.13f, 0.10f),
                       Light = new Color(1f, 0.86f, 0.62f) },

            new Room { Scene = RoomLoader.EventScene, Title = "Kejadian",
                       Floor = new Color(0.18f, 0.17f, 0.24f), Wall = new Color(0.12f, 0.11f, 0.17f),
                       Light = new Color(0.72f, 0.76f, 1f) },

            new Room { Scene = RoomLoader.SlotScene,  Title = "Slot",
                       Floor = new Color(0.22f, 0.16f, 0.24f), Wall = new Color(0.15f, 0.10f, 0.17f),
                       Light = new Color(0.92f, 0.68f, 1f) }
        };

        [MenuItem("Tools/Grimoire/Build Room Scenes")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[RoomSceneBuilder] stop Play mode dulu.");
                return;
            }

            var previous = SceneManager.GetActiveScene().path;

            for (int i = 0; i < Rooms.Length; i++) BuildOne(Rooms[i]);

            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);

            RegisterInBuildSettings();

            Debug.Log("[RoomSceneBuilder] tiga ruangan dibangun dan didaftarkan di Build Settings. " +
                      "Hias sesukamu di editor - tapi jangan jalankan menu ini lagi untuk ruangan " +
                      "yang sudah dihias, karena ia menimpa isinya.");
        }

        static void BuildOne(Room room)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // SATU root saja. RoomLoader menyala-matikan seluruh root scene ini, dan root kedua
            // yang lupa dimatikan akan tetap tergambar di belakang ruangan lain.
            var root = new GameObject("Room_" + room.Title);

            // JAUH dari arena. Scene additive berbagi satu ruang dunia, dan ruangan yang dibangun
            // di titik nol berdiri TEPAT di tengah arena — kamera ruangan menatap hutan, cahaya
            // ruangan menyiram pertempuran. Tiap ruangan dapat kavlingnya sendiri supaya cahaya
            // ruangan sebelah juga tidak bocor.
            root.transform.position = new Vector3(3000f, 0f, RoomOffset(room.Scene));

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Lantai";
            floor.transform.SetParent(root.transform, false);
            floor.transform.localScale = new Vector3(4f, 1f, 4f);
            Paint(floor, room.Floor);

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Dinding";
            wall.transform.SetParent(root.transform, false);
            wall.transform.localPosition = new Vector3(0f, 6f, 14f);
            wall.transform.localScale = new Vector3(40f, 14f, 1f);
            Paint(wall, room.Wall);

            var sun = new GameObject("Cahaya");
            sun.transform.SetParent(root.transform, false);
            sun.transform.localRotation = Quaternion.Euler(38f, -160f, 0f);

            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = room.Light;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;

            var camGo = new GameObject("Kamera Ruangan");
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 4.2f, -9.5f);
            camGo.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);

            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 42f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = room.Wall * 0.5f;

            // TIDAK diberi AudioListener. Telinga scene tetap satu, menumpang di kamera arena -
            // telinga kedua membuat Unity mengeluh dan mencampur suara dua kali.
            camGo.AddComponent<UniversalAdditionalCameraData>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, Folder + "/" + room.Scene + ".unity");
        }

        static float RoomOffset(string scene)
        {
            switch (scene)
            {
                case RoomLoader.EventScene: return 400f;
                case RoomLoader.SlotScene: return 800f;
                default: return 0f;
            }
        }

        static void Paint(GameObject go, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", 0.05f);
            go.GetComponent<Renderer>().sharedMaterial = mat;

            // Collider dibuang: tidak ada yang berjalan di ruangan ini, dan collider yang tersisa
            // cuma menunggu ada raycast UI yang tersangkut di sana nanti.
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        /// <summary>
        /// Menambahkan ketiganya ke Build Settings kalau belum ada.
        ///
        /// Wajib: <c>LoadSceneAsync</c> menolak scene yang tidak terdaftar, dan penolakannya
        /// berupa peringatan - bukan error - jadi ruangan yang lupa didaftarkan akan hilang diam
        /// tanpa ada yang tahu kenapa.
        /// </summary>
        static void RegisterInBuildSettings()
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);

            for (int i = 0; i < Rooms.Length; i++)
            {
                string path = Folder + "/" + Rooms[i].Scene + ".unity";
                if (list.Exists(s => s.path == path)) continue;

                list.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
