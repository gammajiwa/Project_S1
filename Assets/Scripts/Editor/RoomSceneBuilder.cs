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

            Dress(root.transform, room);

            var camGo = new GameObject("Kamera Ruangan");
            camGo.transform.SetParent(root.transform, false);
            // TOP-DOWN, sama seperti arena — permintaan pemilik project: "semua kalo bisa itu
            // top-down".
            //
            // Yang lama duduk di ketinggian mata (pitch 14 derajat), dan itu bahasa kamera yang
            // BERBEDA dari seluruh sisa permainan. Pemain yang baru saja menatap arena dari atas
            // lalu dilempar ke ruangan yang dilihat dari samping membaca perpindahan itu sebagai
            // pindah game, bukan sebagai pindah tempat.
            //
            // 62 derajat, bukan 68 seperti arena: ruangan ini punya dinding, dan sudut arena yang
            // penuh membuat dinding jadi pita tipis di tepi layar. Enam derajat lebih landai
            // menyisakan cukup dinding untuk memberi ruangan itu batas.
            camGo.transform.localPosition = new Vector3(0f, 12.5f, -6.6f);
            camGo.transform.localRotation = Quaternion.Euler(62f, 0f, 0f);

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

        /// <summary>
        /// Isi ruangan. Primitif, dan itu disengaja: yang dinilai di sini TATA LETAK dan
        /// siluetnya dari atas, bukan kualitas modelnya. Begitu bentuknya benar, tiap kubus
        /// bisa ditukar model sungguhan tanpa satu baris pun berubah — namanya sudah dipisah
        /// per benda supaya bisa dicari di Hierarchy.
        ///
        /// Semua diletakkan dengan asumsi kamera menatap dari atas pada 62 derajat: benda tinggi
        /// menutupi apa pun di belakangnya, jadi yang penting duduk di DEPAN, dan yang tinggi
        /// didorong ke belakang.
        /// </summary>
        static void Dress(Transform root, Room room)
        {
            var ink = room.Wall * 0.55f;
            var wood = new Color(0.28f, 0.18f, 0.11f);
            var stone = new Color(0.3f, 0.29f, 0.32f);

            switch (room.Scene)
            {
                case RoomLoader.ShopScene:
                {
                    // Meja MELINTANG, pedagang di belakangnya. Ini yang membuat ruangan terbaca
                    // sebagai toko dalam satu pandang: ada batas, dan ada yang berdiri di
                    // seberang batas itu.
                    Prop(root, "Meja", PrimitiveType.Cube, new Vector3(0f, 0.9f, 0.5f),
                        new Vector3(11f, 1.8f, 2.2f), wood);

                    Prop(root, "Pedagang_Badan", PrimitiveType.Capsule, new Vector3(0f, 1.7f, 3.4f),
                        new Vector3(1.5f, 1.7f, 1.5f), ink);
                    Prop(root, "Pedagang_Kepala", PrimitiveType.Sphere, new Vector3(0f, 3.5f, 3.4f),
                        Vector3.one * 1.1f, room.Light * 0.8f);

                    // Peti di sisi, bukan di tengah: yang di tengah menutupi dagangan yang
                    // digambar UI tepat di atas mejanya.
                    Prop(root, "Peti_Kiri", PrimitiveType.Cube, new Vector3(-6.6f, 0.7f, 2.4f),
                        new Vector3(1.8f, 1.4f, 1.8f), wood * 0.8f);
                    Prop(root, "Peti_Kanan", PrimitiveType.Cube, new Vector3(6.6f, 0.7f, 2.4f),
                        new Vector3(1.8f, 1.4f, 1.8f), wood * 0.8f);
                    Prop(root, "Peti_Tumpuk", PrimitiveType.Cube, new Vector3(6.6f, 1.9f, 2.4f),
                        new Vector3(1.3f, 1f, 1.3f), wood * 0.7f);
                    break;
                }

                case RoomLoader.EventScene:
                {
                    // BUKU, bukan orang. Pakta adalah tawaran dari sesuatu yang tidak berwajah;
                    // NPC yang punya wajah membuat pemain mencari motifnya, dan motif yang tidak
                    // pernah dijelaskan terbaca sebagai cerita yang bolong. Buku tidak menawar,
                    // ia cuma terbuka.
                    Prop(root, "Altar", PrimitiveType.Cube, new Vector3(0f, 0.55f, 1.5f),
                        new Vector3(5f, 1.1f, 3.4f), stone);
                    Prop(root, "Altar_Kaki", PrimitiveType.Cube, new Vector3(0f, 0.2f, 1.5f),
                        new Vector3(6f, 0.4f, 4.2f), stone * 0.8f);

                    // Dua halaman miring saling menyandar — itu siluet buku terbuka dari atas.
                    var kiri = Prop(root, "Halaman_Kiri", PrimitiveType.Cube,
                        new Vector3(-1.05f, 1.35f, 1.5f), new Vector3(2.2f, 0.12f, 2.8f),
                        new Color(0.86f, 0.8f, 0.66f));
                    kiri.transform.localRotation = Quaternion.Euler(0f, 0f, 9f);

                    var kanan = Prop(root, "Halaman_Kanan", PrimitiveType.Cube,
                        new Vector3(1.05f, 1.35f, 1.5f), new Vector3(2.2f, 0.12f, 2.8f),
                        new Color(0.86f, 0.8f, 0.66f));
                    kanan.transform.localRotation = Quaternion.Euler(0f, 0f, -9f);

                    Prop(root, "Punggung", PrimitiveType.Cube, new Vector3(0f, 1.28f, 1.5f),
                        new Vector3(0.5f, 0.2f, 2.9f), new Color(0.35f, 0.1f, 0.14f));

                    // Cahaya keluar DARI halamannya. Itu satu-satunya sumber terang di ruangan,
                    // jadi mata jatuh ke buku sebelum sempat menyapu sisanya.
                    var glowGo = new GameObject("Pendar_Halaman");
                    glowGo.transform.SetParent(root, false);
                    glowGo.transform.localPosition = new Vector3(0f, 2.2f, 1.5f);
                    var glow = glowGo.AddComponent<Light>();
                    glow.type = LightType.Point;
                    glow.color = new Color(0.72f, 0.5f, 1f);
                    glow.intensity = 6f;
                    glow.range = 12f;
                    glow.shadows = LightShadows.None;
                    break;
                }

                case RoomLoader.SlotScene:
                {
                    Prop(root, "Mesin_Badan", PrimitiveType.Cube, new Vector3(0f, 2f, 2f),
                        new Vector3(7f, 4f, 2.4f), stone);
                    Prop(root, "Mesin_Alas", PrimitiveType.Cube, new Vector3(0f, 0.4f, 2f),
                        new Vector3(8f, 0.8f, 3.2f), stone * 0.75f);

                    // Tiga gulungan, direbahkan supaya sumbunya mendatar — dari atas yang terbaca
                    // tiga silinder sejajar, dan itu bahasa mesin slot di mana pun.
                    for (int i = 0; i < 3; i++)
                    {
                        var reel = Prop(root, "Gulungan_" + i, PrimitiveType.Cylinder,
                            new Vector3((i - 1) * 2.1f, 2.6f, 0.9f), new Vector3(1.6f, 0.7f, 1.6f),
                            new Color(0.55f, 0.42f, 0.2f));
                        reel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }

                    Prop(root, "Tuas_Batang", PrimitiveType.Cube, new Vector3(4.4f, 2.6f, 1.2f),
                        new Vector3(0.25f, 2.4f, 0.25f), new Color(0.4f, 0.38f, 0.42f));
                    Prop(root, "Tuas_Kenop", PrimitiveType.Sphere, new Vector3(4.4f, 4f, 1.2f),
                        Vector3.one * 0.8f, new Color(0.75f, 0.15f, 0.18f));
                    break;
                }
            }
        }

        static GameObject Prop(Transform root, string name, PrimitiveType shape,
            Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;

            // Collider dicabut: tidak ada yang berjalan di ruangan ini, dan collider yang
            // tertinggal ikut disapu fisika tiap frame untuk benda yang tidak pernah ditabrak.
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            Paint(go, color);
            return go;
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
