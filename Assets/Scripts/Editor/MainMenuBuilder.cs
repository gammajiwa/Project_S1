using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Generates Assets/Scenes/MainMenu.unity from scratch.
    ///
    /// The split that makes this safe to re-run: layout lives in the generated scene, look lives in
    /// MenuTheme. Swap placeholder art in the theme asset and rebuild — the art survives because it
    /// was never stored in the scene. Anything hand-edited *in the scene* is lost on rebuild; that
    /// is the price of being able to regenerate it.
    /// </summary>
    public static class MainMenuBuilder
    {
        const string ScenePath = "Assets/Scenes/MainMenu.unity";
        const string GameScenePath = "Assets/Scenes/Proto.unity";
        const string ThemePath = "Assets/GameData/MenuTheme.asset";
        const string UiThemePath = "Assets/GameData/UiTheme.asset";
        const string StarterPrefabPath = "Assets/Prefabs/UI/StarterPanel.prefab";
        const string MenuLookPath = "Assets/GameData/SceneLook_Menu.asset";
        const string MenuAssetDir = "Assets/GameData/Menu";

        const float RefWidth = 1920f;
        const float RefHeight = 1080f;

        static MenuTheme _theme;
        static SceneLook _look;

        [MenuItem("Tools/Grimoire/Build Main Menu")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[MainMenuBuilder] stop Play mode dulu.");
                return;
            }

            _theme = LoadOrCreateTheme();

            // The menu has its own look: its floor is a showcase, the run's floor has to stay dark.
            _look = AssetDatabase.LoadAssetAtPath<SceneLook>(MenuLookPath);
            if (_look == null)
            {
                Debug.LogError("[MainMenuBuilder] " + MenuLookPath + " tidak ketemu. " +
                               "Jalankan 'Tools/Grimoire/Build Scene Look' dulu.");
                return;
            }

            // Dipakai layar pilih starter buat menampilkan angka yang BENAR-BENAR berlaku: starter
            // yang membiarkan sebuah stat kosong tetap harus memperlihatkan nilainya, dan nilai itu
            // hidup di sini.
            var balance = FindAsset<GameBalance>();
            if (balance == null)
            {
                Debug.LogError("[MainMenuBuilder] GameBalance tidak ketemu di project.");
                return;
            }

            var database = FindAsset<ContentDatabase>();
            if (database == null)
            {
                Debug.LogError("[MainMenuBuilder] ContentDatabase tidak ketemu di project.");
                return;
            }

            // Unity refuses to save over a scene that is already open, and it also refuses to close
            // the last open scene — so when the target is open we empty it and rebuild in place.
            var target = FindOpenScene(ScenePath);
            bool builtInPlace = target.IsValid();

            var previous = SceneManager.GetActiveScene();
            var scene = builtInPlace
                ? target
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            SceneManager.SetActiveScene(scene);

            if (builtInPlace)
            {
                var stale = scene.GetRootGameObjects();
                for (int i = 0; i < stale.Length; i++) Object.DestroyImmediate(stale[i]);
            }

            var root = new GameObject("_Menu");
            var controller = root.AddComponent<MainMenuController>();

            BuildDiorama(root.transform);
            BuildEventSystem(root.transform);

            var canvas = BuildCanvas(root.transform);
            BuildBackdrop(canvas);

            var rootPage = BuildRootPage(canvas, out var play, out var codexBtn,
                out var settingsBtn, out var quit, out var version);

            var starterPage = BuildStarterPage(canvas, database, balance,
                out var starterPanel, out var starterBack);

            var codexPage = BuildCodexPage(canvas, database, out var codexPanel, out var codexBack);
            var settingsPage = BuildSettingsPage(canvas, out var settingsPanel, out var settingsBack);

            new Binder(controller)
                .SetString("_gameSceneName", "Proto")
                .Set("_rootPage", rootPage.gameObject)
                .Set("_starterPage", starterPage.gameObject)
                .Set("_starterBackButton", starterBack)
                .Set("_codexPage", codexPage.gameObject)
                .Set("_settingsPage", settingsPage.gameObject)
                .Set("_playButton", play)
                .Set("_codexButton", codexBtn)
                .Set("_settingsButton", settingsBtn)
                .Set("_quitButton", quit)
                .Set("_codexBackButton", codexBack)
                .Set("_settingsBackButton", settingsBack)
                .Set("_codexPanel", codexPanel)
                .Set("_settingsPanel", settingsPanel)
                .Set("_versionLabel", version)
                .Set("_loadingSigil", LoadingSigil())
                .Apply();

            // Halaman setelan DISIMPAN SEBAGAI PREFAB sebelum dimatikan — scene game membukanya
            // lewat ESC. Satu panel untuk dua scene; dua salinan akan saling menyimpang begitu
            // salah satunya diedit.
            SaveSettingsPrefab(settingsPage.gameObject);

            starterPage.gameObject.SetActive(false);
            codexPage.gameObject.SetActive(false);
            settingsPage.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("[MainMenuBuilder] gagal menyimpan " + ScenePath + ".");
                return;
            }

            if (!builtInPlace)
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }

            RegisterBuildSettings();

            AssetDatabase.SaveAssets();
            Debug.Log("[MainMenuBuilder] " + ScenePath + " dibangun ulang. " +
                      "Aset yang bisa diganti ada di " + ThemePath + ".");
        }

        // ---------- diorama ----------

        static void BuildDiorama(Transform parent)
        {
            _look.ApplyEnvironment();

            var rig = new GameObject("CameraRig");
            rig.transform.SetParent(parent, false);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 9f, -13f);
            camGo.transform.localRotation = Quaternion.Euler(26f, 0f, 0f);

            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 38f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Rendered(_look.HorizonColor);

            // Sama seperti scene game: tanpa ini console dibanjiri "no audio listeners" dan
            // berhenti berguna. Menu adalah layar pertama yang dilihat, jadi ia juga yang pertama
            // membanjirinya.
            camGo.AddComponent<AudioListener>();

            var urp = camGo.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;

            // Kamera yang bergerak terus, pelan, tanpa pernah berhenti — permintaan pemilik
            // project. Dua irama yang periodenya jauh berbeda: mengitari (50 detik) dan
            // mendekat-menjauh (83 detik). Karena keduanya tidak sebanding, bingkainya tidak
            // pernah mengulang komposisi yang sama persis dalam waktu yang masuk akal untuk
            // ditunggui orang.
            new Binder(camGo.AddComponent<MenuDiorama>())
                .SetFloat("_radius", 2.4f)
                .SetFloat("_depth", 1.1f)
                .SetFloat("_bob", 0.45f)
                .SetFloat("_speed", 0.02f)
                .SetFloat("_dolly", 3.2f)
                .SetFloat("_dollySpeed", 0.012f)
                .Apply();

            if (_look.PostProcess != null)
            {
                var volumeGo = new GameObject("Global Volume");
                volumeGo.transform.SetParent(parent, false);

                var volume = volumeGo.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = _look.PostProcess;
            }

            var sun = new GameObject("Sun");
            sun.transform.SetParent(parent, false);
            _look.ApplySun(sun.AddComponent<Light>());

            // TIDAK ada lantai lagi, dan itu bukan penghematan.
            //
            // Lantai dulu ada untuk satu alasan: menampung bayangan panjang dari kapsul dan tiga
            // kubus placeholder. Placeholder-nya sudah dibuang, dan yang tersisa dari lantai itu
            // cuma perannya sebagai PENGHALANG — ia mesh pejal di y = 0, dan lingkaran sihir yang
            // menghadap kamera menembusnya, jadi separuh bawah lingkarannya dipotong garis lurus.
            // Terlihat di foto: cincinnya tergigit persis di ketinggian lantai.
            //
            // Kedua bidang latar (kegelapan & bayangan awan) tidak butuh lantai — masing-masing
            // bidangnya sendiri, dan tepi terjauhnya jatuh di luar layar.
            BuildWorldBackdrop(parent);
        }

        /// <summary>
        /// Titik yang benar-benar ada di TENGAH LAYAR, bukan titik nol dunia.
        ///
        /// Kamera duduk di (0, 9, −13) menunduk 26°, jadi sinar tengah layarnya mendarat di tanah
        /// sekitar z = +5,5. Segala sesuatu yang dimaksudkan "di tengah" — lingkaran sihir, pusat
        /// pusaran gelap, sumber bara — harus dipusatkan ke situ. Dipusatkan ke titik nol, semuanya
        /// duduk di sepertiga bawah layar dan latar terbaca miring tanpa ada yang tahu kenapa.
        /// </summary>
        static readonly Vector3 ScreenHeart = new Vector3(0f, 0f, 5.5f);

        /// <summary>
        /// Titik yang jatuh TEPAT di tengah layar pada jarak tertentu dari kamera.
        ///
        /// <see cref="ScreenHeart"/> menjawab "di mana sinar tengah layar menyentuh TANAH".
        /// Yang ini menjawab pertanyaan berbeda: di mana menaruh benda MELAYANG supaya ia duduk
        /// di tengah bingkai. Kamera menunduk, jadi jawabannya bukan "di atas titik itu" —
        /// menaikkan sesuatu satu unit menggesernya dua unit ke atas layar.
        /// </summary>
        static Vector3 OnScreenAxis(float distance, float sideways = 0f)
        {
            var eye = new Vector3(0f, 9f, -13f);
            float pitch = 26f * Mathf.Deg2Rad;
            var forward = new Vector3(0f, -Mathf.Sin(pitch), Mathf.Cos(pitch));

            return eye + forward * distance + Vector3.right * sideways;
        }

        /// <summary>
        /// Lima lapis latar, dari yang paling jauh ke yang paling depan. Ini yang diminta pemilik
        /// project setelah menolak konsep diorama: <i>"bukan itu yg gw maksud tapi lebih ke latar
        /// di belakang, gw kan grimoire"</i> — yang menjual menunya harus LAYAR PENUH, bukan
        /// sebuah benda yang dipajang di sudut.
        ///
        /// Semua siklusnya di atas sepuluh detik. Latar menu dilihat berkali-kali dan lama; apa pun
        /// yang berulang dalam hitungan detik akan ketahuan berulang, dan begitu ketahuan ia
        /// berhenti jadi suasana dan berubah jadi animasi yang diputar ulang.
        /// </summary>
        const string BackdropPrefabPath = "Assets/Prefabs/UI/MenuBackdrop.prefab";

        /// <summary>
        /// Latar dibangun SEKALI, lalu hidup sebagai prefab.
        ///
        /// Ini bukan optimasi, ini perbaikan kerusakan. Scene menu digenerate ulang tiap kali
        /// tombol menunya disentuh, dan itu benar untuk tata letak — tapi latar ini ditata TANGAN
        /// oleh pemilik project: buku digeser, diputar, dibesarkan; lingkaran dalam dimiringkan;
        /// percikan digeser. Semua itu hilang pada rebuild berikutnya, dan sudah hilang sekali.
        ///
        /// Prefab tidak ikut digenerate. Sekali file ini ada, pass ini cuma memasangnya kembali
        /// dan tidak pernah menyentuh isinya lagi — persis pola yang sudah dipakai halaman setelan
        /// dan papan starter. Mau membangunnya ulang dari kode? Hapus prefabnya.
        /// </summary>
        static bool RestoreBackdrop(Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackdropPrefabPath);
            if (prefab == null) return false;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = "Backdrop";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            Debug.Log("[MainMenuBuilder] latar dipasang dari " + BackdropPrefabPath +
                      " — penataan tangan di prefab itu TIDAK disentuh. Hapus prefabnya kalau " +
                      "mau membangunnya ulang dari kode.");
            return true;
        }

        static void SaveBackdropPrefab(GameObject backdrop)
        {
            EnsureFolder("Assets/Prefabs/UI");
            PrefabUtility.SaveAsPrefabAssetAndConnect(backdrop, BackdropPrefabPath,
                InteractionMode.AutomatedAction);
        }

        static void BuildWorldBackdrop(Transform parent)
        {
            if (RestoreBackdrop(parent)) return;

            var root = new GameObject("Backdrop").transform;
            root.SetParent(parent, false);

            // --- L0: kegelapan yang mengepung, terang di tengah ---
            //
            // Shader yang sama dengan kegelapan peta, dan itu bukan penghematan: yang dicari sama
            // persis — garis batas yang BERGOLAK, bukan gradasi rapi. Gradasi rapi terbaca sebagai
            // vignette, dan vignette terbaca sebagai efek kamera, bukan sebagai tempat yang gelap.
            var gloom = NewLayerQuad(root, "L0_Gloom", "Grimoire/Gloom", 0.8f, 150f);
            if (gloom != null)
            {
                // Gelapnya JAUH lebih gelap dari warna dasar kamera, dan itu syaratnya: kegelapan
                // ini bekerja dengan mengurangi, jadi ia cuma terbaca kalau ada yang bisa
                // dikurangi. Percobaan pertama memakai nada yang hampir sama dengan latarnya —
                // secara teknis bekerja, secara mata tidak ada gradasi apa pun di layar.
                gloom.SetColor("_Color", Rendered(new Color(0.018f, 0.007f, 0.032f, 1f)));
                gloom.SetVector("_Center", new Vector4(ScreenHeart.x, ScreenHeart.z, 0f, 0f));
                // Jangkauannya diukur dari APA YANG TERLIHAT, bukan ditebak. Kamera menunduk 26°
                // dari ketinggian 9, jadi tepi bawah layar menatap tanah ~10 unit dari pusat dan
                // tepi atas ~50. Angka pertama (9/34) menaruh SELURUH layar di antara "mulai
                // gelap" dan "gelap penuh" — hasilnya latar hitam rata tanpa satu pun lapis lain
                // yang kelihatan.
                // Kolam terangnya DILEBARKAN jauh setelah pemilik project bilang "gelap banget
                // dan serasa kosong". Kegelapan yang mulai menutup 18 unit dari pusat berarti
                // dua pertiga layar sudah lewat separuh jalan menuju hitam sebelum ada apa pun
                // sempat digambar di situ — yang terbaca bukan suasana, melainkan ruang kosong.
                gloom.SetFloat("_Inner", 30f);
                gloom.SetFloat("_Outer", 76f);
                gloom.SetFloat("_Scale", 14f);
                gloom.SetFloat("_Churn", 0.05f);
                gloom.SetFloat("_Drift", 0.35f);
                gloom.SetFloat("_Wobble", 15f);

                // Halus, bukan raster titik. Titik komik adalah bahasa peta — di menu ia menarik
                // mata ke polanya sendiri, dan yang harus ditatap orang di sini lingkarannya.
                gloom.SetFloat("_Smooth", 1f);
                // Tidak pernah pekat penuh. Plafon 0,95 berarti tepi layar praktis hitam, dan
                // hitam pekat adalah yang membuat sudut-sudutnya terbaca sebagai lubang.
                gloom.SetFloat("_Ceiling", 0.6f);
                Seal(gloom);
            }

            // --- L0b: cahaya lebar yang MENGISI, bukan menghias ---
            //
            // Jawaban untuk "serasa kosong". Separuh kiri-bawah layar tidak punya satu pun benda
            // di dalamnya, dan menaruh benda di situ akan bersaing dengan kolom menu. Yang
            // dibutuhkan bukan benda melainkan NADA: satu lingkaran sihir raksasa, sangat besar
            // dan sangat pucat, dipasang serong di belakang segalanya sehingga tepinya lewat di
            // belakang teks tanpa pernah menarik mata ke dirinya sendiri.
            var wash = SpawnVfx(root, MagicCircle2Path, "L0b_Wash",
                OnScreenAxis(26f, -3f), 6.5f);

            if (wash != null)
            {
                Hide(wash, "Sides");
                Tint(wash, new Color(0.62f, 0.42f, 0.95f), 0.1f);
                Calm(wash, 0.2f);

            }

            // --- L1: lingkaran sihir raksasa, jiwa grimoire-nya ---
            // Digantung di SUMBU PANDANG, sedikit ke kanan supaya kolom menu di kiri tetap punya
            // ruang gelapnya sendiri.
            //
            // Merebahkannya ke tanah sudah dicoba dan terbukti mustahil: partikel lingkarannya
            // ber-render mode Billboard, jadi ia SELALU menghadap kamera berapa pun transformnya
            // diputar. Yang berubah cuma tempatnya, bukan hadapnya. Setelah tahu itu, menggantung
            // lingkarannya justru jawaban yang benar — sigil raksasa yang melayang di kegelapan,
            // bukan gambar di lantai yang kebetulan menghadap kamera.
            var circle = SpawnVfx(root, MagicCirclePath, "L1_MagicCircle",
                OnScreenAxis(19.6f, 4f), 2.4f);

            if (circle != null)
            {
                // Dinding cahaya tegaknya dibuang: ia milik portal yang berdiri, dan di sini cuma
                // jadi lembar cahaya yang menutupi separuh lingkaran yang mau dipamerkan.
                Hide(circle, "Sides");

                Tint(circle, new Color(0.70f, 0.45f, 1f), 0.34f);

                // Percikan bawaan lingkarannya ikut ditenangkan. Yang diminta pemilik project
                // "vfx nya jangan gerak2": yang bergerak boleh tetap ada, tapi ia harus HANYUT,
                // bukan menyambar. Sambaran menarik mata berkali-kali per detik, dan latar menu
                // dipandangi lama — apa pun yang menyambar di situ berubah jadi gangguan.
                Calm(circle, 0.25f);

                // Sumbunya Z LOKAL — normal lingkarannya sendiri, bukan Y dunia. Billboard yang
                // menghadap kamera diputar di sumbu Y akan berputar TANPA TERLIHAT BERPUTAR:
                // hadapnya dikunci kamera, jadi yang berubah cuma sesuatu yang tidak digambar.
                // TIDAK ada MenuSpin di sini lagi, dan itu perintah pemilik project:
                // "gw gak mau partikel yg udah gw seting gerak2 ... rotat rotat".
                //
                // Bukan sekadar selera. MenuSpin MENULIS ke transform, jadi tiap masuk play mode
                // rotasi yang disetel tangan ketimpa sudut sembarang — dan begitu scene atau
                // prefabnya tersimpan dalam keadaan itu, angka yang disetel orang hilang
                // permanen. Sempat terjadi: prefab latar pernah tersimpan dengan rot Z 35,17
                // dan 302,44, dua angka yang tidak pernah diketik siapa pun.
            }

            // --- L1b: lingkaran kedua, LEBIH KECIL, berputar berlawanan ---
            //
            // Dua cincin yang berputar berlawanan arah adalah cara termurah membuat sesuatu
            // terbaca sebagai MEKANISME dan bukan sebagai gambar yang diputar. Satu cincin
            // sendirian selalu bisa dibaca sebagai tekstur yang dianimasikan.
            var inner = SpawnVfx(root, MagicCircle2Path, "L1b_MagicCircleInner",
                OnScreenAxis(19.2f, 4f), 1.25f);

            if (inner != null)
            {
                inner.transform.localRotation = Quaternion.Euler(InnerCircleTilt);
                Hide(inner, "Sides");
                Tint(inner, new Color(0.55f, 0.62f, 1f), 0.26f);
                Calm(inner, 0.25f);

            }

            // --- L1c: BUKUNYA ---
            BuildBook(root, BookPlace);

            // --- L2: bara/rune ungu yang naik pelan ---
            //
            // Skala 1, bukan 4,5. Kotak sebarannya sudah 30x10x30 dari sananya — persis seukuran
            // pandangan. Membesarkannya membesarkan BUTIRNYA juga, dan bara selebar satu unit
            // berhenti terbaca sebagai bara; yang tampil kepingan terang yang berhamburan.
            var embers = SpawnVfx(root, EmbersPath, "L2_Embers",
                ScreenHeart + new Vector3(0f, 0f, -2f), 1f);

            if (embers != null)
            {
                Tint(embers, new Color(0.78f, 0.42f, 1f), 0.55f);
                Calm(embers, 0.3f);
            }

            // --- L2b: lapis bara KEDUA, jauh di belakang dan lebih redup ---
            //
            // Ini yang membuat latarnya punya JARAK. Satu lapis bara, seberapa pun banyaknya,
            // tetap satu bidang; dua lapis yang berbeda kedalaman bergeser dengan laju berbeda
            // saat kamera bergerak, dan pergeseran itulah yang dibaca sebagai ruang.
            var deepEmbers = SpawnVfx(root, EmbersFarPath, "L2b_EmbersFar",
                ScreenHeart + new Vector3(0f, 1f, 16f), 1.6f);

            if (deepEmbers != null)
            {
                Tint(deepEmbers, new Color(0.52f, 0.35f, 0.95f), 0.35f);
                Slow(deepEmbers, 0.6f);
                Calm(deepEmbers, 0.25f);
            }

            // Kabut rendah DICABUT atas perintah pemilik project ("yg asap itu cabut aja").
            // Niatnya memberi buku sesuatu untuk berdiri di atasnya; yang tampil kepulan abu
            // yang menutupi bukunya sendiri. Kekosongan di bawah dijawab lapis cahaya lebar
            // (L0b) dan latar yang lebih terang, bukan dengan asap.

            // --- L3: percikan biru, JARANG ---
            //
            // Jarang itu keputusan, bukan kompromi performa. Percikan yang terus-menerus jadi
            // tekstur; yang muncul sesekali membuat orang menoleh — dan menoleh ke latar menu
            // adalah persis yang diminta.
            // 3,78 di sumbu X, bukan 1 — pemilik project menggesernya sendiri di scene.
            var sparks = SpawnVfx(root, SparksPath, "L3_Sparks",
                OnScreenAxis(17f, 3.78f), 7f);

            if (sparks != null)
            {
                Slow(sparks, 0.3f);
                Calm(sparks, 0.35f);
            }

            // --- L4: bayangan awan yang menyapu semuanya ---
            //
            // Di atas semua lapis lain, dan itu yang memberinya kedalaman: bayangan yang melintas
            // DI DEPAN lingkaran sihir menaruh lingkaran itu di bawah sesuatu. Bayangan yang cuma
            // menggelapkan lantai tidak menaruhnya di bawah apa pun.
            var clouds = NewLayerQuad(root, "L4_CloudShadows", "Grimoire/CloudShadows", 5f, 150f);
            if (clouds != null)
            {
                // Naik dari nyaris hitam ke ungu redup. Bayangan yang lebih gelap dari latarnya
                // tidak menambah kedalaman di latar yang sudah gelap — ia cuma melubanginya.
                clouds.SetColor("_Color", Rendered(new Color(0.11f, 0.06f, 0.19f, 1f)));
                clouds.SetFloat("_Scale", 42f);
                clouds.SetFloat("_Coverage", 0.42f);
                clouds.SetFloat("_Softness", 0.4f);
                clouds.SetVector("_Direction", new Vector4(1f, 0.3f, 0f, 0f));

                // 0,7 unit/detik pada gumpalan selebar 42 unit = satu lintasan penuh sekitar satu
                // menit. Itu batas bawah yang masih terbaca bergerak oleh mata.
                clouds.SetFloat("_Speed", 0.7f);
                clouds.SetFloat("_Evolve", 0.012f);
                Seal(clouds);
            }

            // Disimpan SEKARANG, sebelum ada yang sempat menatanya lagi. Mulai detik ini latar
            // menu punya satu tempat tinggal yang tidak digenerate ulang.
            SaveBackdropPrefab(root.gameObject);
        }

        /// <summary>
        /// Menyimpan penyetelan yang barusan ditulis ke sebuah material ASET.
        ///
        /// <c>SetFloat</c>/<c>SetColor</c> pada material yang sudah jadi aset TIDAK menandainya
        /// kotor, dan <c>SaveAssets</c> berikutnya melewatinya begitu saja — nilai yang ditulis
        /// hidup di memori sampai domain reload lalu hilang tanpa jejak. Ini sudah memakan satu
        /// putaran penuh: seluruh lapis latar dibangun dengan angka yang benar di kode dan
        /// tampil dengan angka bawaan shader di layar.
        /// </summary>
        static void Seal(Material material)
        {
            if (material != null) EditorUtility.SetDirty(material);
        }

        const string MagicCirclePath =
            "Assets/Art/VFX/Packs/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle.prefab";

        const string EmbersPath =
            "Assets/Plugin/Lana Studio/Environment VFX pack/Prefabs/Embers/Embers_calm.prefab";

        const string SparksPath =
            "Assets/Art/VFX/Packs/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks flashing blue.prefab";

        const string MagicCircle2Path =
            "Assets/Art/VFX/Packs/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle 2.prefab";

        const string EmbersFarPath =
            "Assets/Plugin/Lana Studio/Environment VFX pack/Prefabs/Embers/Embers_average.prefab";

        const string MistPath =
            "Assets/Art/VFX/Packs/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Smoke ground.prefab";

        /// <summary>
        /// Sampul grimoire yang SUDAH dipakai UI dalam permainan, dipinjam ke latar menu.
        ///
        /// Bukan model baru dan bukan kubus placeholder. Kubus akan langsung kena penolakan yang
        /// sama dengan primitif FX ("gw gak mau lihat kalo masih ada"), dan buku yang dipahat
        /// sendiri adalah pekerjaan sehari untuk sesuatu yang dilihat dari satu sudut saja.
        /// Gambar yang sama yang jadi alas papan grimoire justru menjahit menu ke permainannya.
        /// </summary>
        const string BookArtPath = "Assets/Art/UI/Frames/grimoireUI.png";

        /// <summary>
        /// Sigil yang berputar di layar muat. SALINAN milik kita, bukan tekstur pack-nya
        /// langsung: memakai yang di pack berarti mengubah tipe importnya jadi Sprite, dan
        /// tekstur itu masih dipakai material partikel yang tidak minta diubah apa-apa.
        /// </summary>
        static Sprite LoadingSigil() =>
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sigil_Loading.png");

        const string BookModelPath = "Assets/Art/Props/Grimoire/SM_Grimoire.fbx";
        const string BookTextureDir = "Assets/Art/Props/Grimoire/Textures";

        /// <summary>Lebar buku di dunia, dalam unit. Model apa pun dinormalkan ke angka ini.</summary>
        /// <summary>
        /// Lebar buku di dunia, dalam unit. Model apa pun dinormalkan ke angka ini.
        ///
        /// 8,0 bukan angka desain melainkan angka PENGAMATAN: pemilik project menata sendiri
        /// bukunya di scene sampai skala 44,2, dan itu setara 8 unit pada model ini. Nilainya
        /// dipindahkan ke sini karena scene menu digenerate ulang — yang ditata tangan di scene
        /// hilang pada rebuild berikutnya, dan yang di sini bertahan.
        /// </summary>
        const float BookWidth = 9.51f;

        /// <summary>Letak & putaran buku, DISALIN dari penataan tangan pemilik project.</summary>
        static readonly Vector3 BookPlace = new Vector3(4.36f, 0.90f, 4.01f);

        static readonly Vector3 BookFacing = new Vector3(354.90f, 242.10f, 348.61f);

        /// <summary>Kemiringan lingkaran dalam, juga dari penataan tangan.</summary>
        static readonly Vector3 InnerCircleTilt = new Vector3(300.70f, 0f, 0f);

        /// <summary>
        /// Grimoire SUNGGUHAN kalau modelnya ada di project.
        ///
        /// Mengembalikan false kalau tidak ada, dan pemanggilnya jatuh ke dua bidang bergambar —
        /// menu tidak boleh kehilangan bukunya cuma karena satu aset belum diimpor.
        ///
        /// Skalanya DIUKUR, bukan ditebak: skala ekspor FBX bervariasi ratusan kali lipat antar
        /// pipeline, dan angka tetap di kode berarti buku yang menghilang jadi setitik atau
        /// menelan seluruh layar tanpa ada yang tahu kenapa.
        /// </summary>
        static bool BuildBookModel(Transform parent, Vector3 centre)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BookModelPath);
            if (prefab == null) return false;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            go.name = "L1c_Book";
            go.transform.localPosition = centre;
            go.transform.localRotation = Quaternion.Euler(BookFacing);
            go.transform.localScale = Vector3.one;

            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Object.DestroyImmediate(go);
                return false;
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float widest = Mathf.Max(bounds.size.x, bounds.size.z);
            if (widest > 0.0001f) go.transform.localScale = Vector3.one * (BookWidth / widest);

            var material = BookMaterial();
            if (material != null)
            {
                foreach (var r in renderers)
                {
                    r.sharedMaterial = material;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }

            return true;
        }

        /// <summary>
        /// Material PBR buku dari peta yang ikut asetnya.
        ///
        /// Emissive DINAIKKAN jauh di atas satu. Latar menu ini malam pekat dengan matahari 0,95;
        /// material Lit yang benar secara fisika akan tampil hampir hitam di situ, dan yang
        /// membuat grimoire terbaca sebagai benda bertenaga justru pendarnya, bukan albedonya.
        /// </summary>
        static Material BookMaterial()
        {
            string path = MenuAssetDir + "/M_MenuGrimoire.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return null;

            var material = new Material(lit);

            Set(material, "_BaseMap", "Grimoire_albedo.jpeg");
            Set(material, "_BumpMap", "Grimoire_normal.png");
            Set(material, "_MetallicGlossMap", "Grimoire_metallic.jpeg");
            Set(material, "_OcclusionMap", "Grimoire_AO.jpeg");

            var glow = Load("Grimoire_emissive.jpeg");
            if (glow != null)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetTexture("_EmissionMap", glow);
                material.SetColor("_EmissionColor", new Color(0.55f, 0.42f, 0.85f));
            }

            material.SetFloat("_Smoothness", 0.35f);
            material.SetFloat("_Metallic", 0.1f);

            EnsureFolder(MenuAssetDir);
            AssetDatabase.CreateAsset(material, path);
            Seal(material);
            return material;

            void Set(Material m, string slot, string file)
            {
                var tex = Load(file);
                if (tex != null && m.HasProperty(slot)) m.SetTexture(slot, tex);
                if (slot == "_BumpMap" && tex != null) m.EnableKeyword("_NORMALMAP");
            }

            Texture2D Load(string file) =>
                AssetDatabase.LoadAssetAtPath<Texture2D>(BookTextureDir + "/" + file);
        }

        /// <summary>
        /// Satu bidang datar seukuran pandangan, memakai salah satu shader latar yang sudah ada.
        ///
        /// Materialnya disimpan sebagai ASET di folder menu, bukan dibuat runtime: material
        /// sementara tampil magenta begitu scene-nya dibuka di luar play mode, dan yang mau
        /// dinilai mata justru tampilannya. Aset yang sudah ada dipakai apa adanya — penyetelan
        /// tangan bertahan melewati rebuild, dan rebuild menu ini sering.
        /// </summary>
        static Material NewLayerQuad(Transform parent, string name, string shaderName,
            float height, float span)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning("[MainMenuBuilder] shader " + shaderName + " tidak ketemu — " +
                                 "lapis " + name + " dilewati.");
                return null;
            }

            string path = MenuAssetDir + "/M_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader);
                EnsureFolder(MenuAssetDir);
                AssetDatabase.CreateAsset(material, path);
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localPosition = new Vector3(ScreenHeart.x, height, ScreenHeart.z);
            go.transform.localScale = Vector3.one * span;
            Object.DestroyImmediate(go.GetComponent<Collider>());

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // SELALU dikembalikan, jadi pemanggilnya selalu menulis ulang nilainya.
            //
            // Versi sebelumnya hanya mengembalikan material yang baru dibuat, dengan alasan
            // menghormati penyetelan tangan. Niatnya benar, akibatnya tidak: menghapus file
            // .mat-nya dari luar Unity TIDAK membuatnya hilang selama scene menu masih membuka
            // renderer yang menunjuknya — Unity menuliskannya kembali saat refresh, lengkap
            // dengan nilai bawaan shader. Hasilnya jalur "baru dibuat" tidak pernah terpakai
            // lagi setelah build pertama, dan tiga putaran penuh dihabiskan menyetel angka di
            // kode yang tidak pernah sampai ke layar.
            //
            // Lapis latar ini digenerate seperti seluruh sisa scene menu. Tempat menyetelnya
            // adalah kode ini, sama seperti tata letak tombolnya.
            return material;
        }

        /// <summary>
        /// Grimoire terbuka yang tergeletak di dalam lingkaran sihir.
        ///
        /// Dua bidang datar, bukan model: halaman kanan memakai gambarnya apa adanya, halaman
        /// kiri memakai gambar yang sama dengan skala X NEGATIF. Jilid kulit yang di gambarnya
        /// ada di tepi kiri jadi bertemu di TENGAH — dan pertemuan dua jilid itulah yang dibaca
        /// mata sebagai punggung buku yang terbuka. Tanpa pencerminan, dua jilid duduk di tepi
        /// luar dan yang tampil dua papan terpisah.
        /// </summary>
        static void BuildBook(Transform parent, Vector3 centre)
        {
            if (BuildBookModel(parent, centre)) return;

            var art = AssetDatabase.LoadAssetAtPath<Texture2D>(BookArtPath);
            if (art == null)
            {
                Debug.LogWarning("[MainMenuBuilder] gambar buku tidak ketemu: " + BookArtPath);
                return;
            }

            // Sprites/Default, bukan URP/Unlit: dia sudah alpha-blend, sudah Cull Off, dan sudah
            // terbukti di project ini (BoltPool memakainya). URP/Unlit harus dibujuk jadi
            // transparan lewat lima properti plus satu keyword, dan satu saja yang terlewat
            // membuat tepi PNG-nya tampil sebagai kotak hitam.
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[MainMenuBuilder] shader Sprites/Default tidak ketemu — buku dilewati.");
                return;
            }

            string path = MenuAssetDir + "/M_MenuBook.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader);
                material.SetTexture("_MainTex", art);

                // Digelapkan dan didinginkan. Perkamen yang dipakai UI dibuat untuk latar panel
                // yang terang; ditaruh apa adanya di malam ungu ia jadi satu-satunya benda hangat
                // di layar dan langsung terbaca sebagai tempelan.
                material.SetColor("_Color", new Color(0.62f, 0.55f, 0.68f, 1f));

                EnsureFolder(MenuAssetDir);
                AssetDatabase.CreateAsset(material, path);
                Seal(material);
            }

            var book = new GameObject("L1c_Book").transform;
            book.SetParent(parent, false);
            book.localPosition = centre;

            // Dimiringkan menyerong, mengikuti arah yang ditunjuk pemilik project di atas
            // fotonya — buku yang sejajar tepi layar terbaca sebagai ikon, yang menyerong
            // terbaca sebagai benda yang tergeletak di situ.
            book.localRotation = Quaternion.Euler(0f, 24f, 0f);

            NewPage(book, "PageRight", material, 1f);
            NewPage(book, "PageLeft", material, -1f);
        }

        static void NewPage(Transform parent, string name, Material material, float side)
        {
            const float pageWidth = 3.1f;
            const float pageDepth = 4f;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            Object.DestroyImmediate(go.GetComponent<Collider>());

            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            go.transform.localPosition = new Vector3(side * pageWidth * 0.5f, 0f, 0f);
            go.transform.localScale = new Vector3(side * pageWidth, pageDepth, 1f);

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Menaruh satu prefab VFX pack ke scene menu. Null kalau path-nya meleset.</summary>
        static GameObject SpawnVfx(Transform parent, string path, string name,
            Vector3 position, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("[MainMenuBuilder] prefab latar tidak ketemu: " + path);
                return null;
            }

            // Instance BIASA, bukan tautan prefab: scene ini digenerate ulang terus, dan tautan
            // ke prefab pack pihak ketiga berarti tiap update pack diam-diam menata ulang menu.
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            go.name = name;
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * scale;

            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);

            // Cahaya yang ikut menumpang di prefab pack bisa jauh lebih terang daripada matahari
            // menu, dan satu titik terang di tengah layar mengubah seluruh nada gambar.
            foreach (var light in go.GetComponentsInChildren<Light>(true)) light.intensity *= 0.25f;

            return go;
        }

        /// <summary>
        /// Mewarnai ulang sebuah efek pack dan menurunkan kepekatannya.
        ///
        /// Mode gradien dilewati dengan sengaja, bukan karena lupa: menulis ulang gradien berarti
        /// menebak niat pembuat efeknya di tiap titik kunci, dan tebakan itu lebih sering merusak
        /// daripada menolong. Yang dilewati dicatat ke console supaya ketahuan kalau ternyata
        /// justru itu yang bikin warnanya tidak berubah.
        /// </summary>
        static void Tint(GameObject go, Color tint, float alpha)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var start = main.startColor;

                switch (start.mode)
                {
                    case ParticleSystemGradientMode.Color:
                        start.color = Blend(start.color, tint, alpha);
                        break;

                    case ParticleSystemGradientMode.TwoColors:
                        start.colorMin = Blend(start.colorMin, tint, alpha);
                        start.colorMax = Blend(start.colorMax, tint, alpha);
                        break;

                    case ParticleSystemGradientMode.Gradient:
                        start.gradient = Blend(start.gradient, tint, alpha);
                        break;

                    case ParticleSystemGradientMode.TwoGradients:
                        start.gradientMin = Blend(start.gradientMin, tint, alpha);
                        start.gradientMax = Blend(start.gradientMax, tint, alpha);
                        break;
                }

                main.startColor = start;
            }
        }

        static Color Blend(Color original, Color tint, float alpha) =>
            new Color(tint.r, tint.g, tint.b, original.a * alpha);

        /// <summary>
        /// Gradien diwarnai per titik kunci: warna diganti seluruhnya, kepekatan DIKALI.
        ///
        /// Mengalikan warna juga akan menghapus kurva alfa yang bikin efeknya hidup — yang mau
        /// diganti nadanya, bukan waktunya. Titik kunci alfa dibiarkan di tempat semula dan
        /// hanya diturunkan tingginya, jadi efek yang menyala lalu meredup tetap menyala lalu
        /// meredup, cuma lebih pelan.
        /// </summary>
        static Gradient Blend(Gradient original, Color tint, float alpha)
        {
            if (original == null) return null;

            var colours = new GradientColorKey[original.colorKeys.Length];
            for (int i = 0; i < colours.Length; i++)
            {
                colours[i] = new GradientColorKey(tint, original.colorKeys[i].time);
            }

            var alphas = new GradientAlphaKey[original.alphaKeys.Length];
            for (int i = 0; i < alphas.Length; i++)
            {
                alphas[i] = new GradientAlphaKey(original.alphaKeys[i].alpha * alpha,
                    original.alphaKeys[i].time);
            }

            var blended = new Gradient { mode = original.mode };
            blended.SetKeys(colours, alphas);
            return blended;
        }

        /// <summary>Mematikan satu anak sebuah efek pack berdasarkan namanya. Diam kalau tidak ada.</summary>
        static void Hide(GameObject root, string childName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == childName) t.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Menenangkan GERAK sebuah efek tanpa mengurangi jumlahnya.
        ///
        /// Ini bukan <see cref="Slow"/>. Slow membuat efeknya lebih JARANG, dan itu justru yang
        /// tidak boleh terjadi di sini — pemilik project bilang latarnya "serasa kosong", jadi
        /// membuang partikel memperburuknya. Yang dikeluhkan geraknya: kecepatan awal dan derau
        /// turbulensi yang membuat tiap butir menyambar ke arah acak. Dua-duanya dikecilkan,
        /// jumlah dan umurnya dibiarkan utuh — hasilnya bidang bara yang sama padatnya, tapi
        /// HANYUT alih-alih berkelebat.
        /// </summary>
        static void Calm(GameObject go, float motionScale)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;

                var speed = main.startSpeed;
                speed.constantMin *= motionScale;
                speed.constantMax *= motionScale;
                main.startSpeed = speed;

                // Gravitasi ikut dikecilkan: bara yang melambat tapi tetap ditarik penuh ke bawah
                // berhenti melayang dan mulai jatuh, dan yang jatuh terbaca sebagai puing.
                var gravity = main.gravityModifier;
                gravity.constantMin *= motionScale;
                gravity.constantMax *= motionScale;
                main.gravityModifier = gravity;

                var noise = ps.noise;
                if (noise.enabled)
                {
                    var strength = noise.strength;
                    strength.constantMin *= motionScale;
                    strength.constantMax *= motionScale;
                    noise.strength = strength;
                    noise.frequency *= motionScale;
                }

                var velocity = ps.velocityOverLifetime;
                if (velocity.enabled) velocity.speedModifier = motionScale;
            }
        }

        /// <summary>
        /// Menjarangkan sebuah efek.
        ///
        /// Laju DAN letusan, keduanya. Banyak efek pack tidak memakai <c>rateOverTime</c> sama
        /// sekali — "Sparks flashing blue" laju-nya nol dan seluruh partikelnya lahir dari
        /// burst — jadi mengalikan laju saja adalah operasi yang sukses tanpa mengubah apa pun.
        /// </summary>
        static void Slow(GameObject go, float scale)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var emission = ps.emission;

                var rate = emission.rateOverTime;
                rate.constantMin *= scale;
                rate.constantMax *= scale;
                emission.rateOverTime = rate;

                var bursts = new ParticleSystem.Burst[emission.burstCount];
                emission.GetBursts(bursts);

                for (int i = 0; i < bursts.Length; i++)
                {
                    var count = bursts[i].count;
                    count.constantMin = Mathf.Max(1f, count.constantMin * scale);
                    count.constantMax = Mathf.Max(1f, count.constantMax * scale);
                    bursts[i].count = count;

                    // Jeda antar letusan DIPANJANGKAN, bukan dipendekkan: yang diminta "jarang",
                    // dan letusan kecil yang tetap sesering dulu cuma jadi kerlip terus-menerus.
                    bursts[i].repeatInterval = Mathf.Max(bursts[i].repeatInterval, 0.01f) / scale;
                }

                emission.SetBursts(bursts);
            }
        }

        static void BuildEventSystem(Transform parent)
        {
            var go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            go.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
            // Active Input Handling is set to the new package, so the legacy module would be inert.
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // ---------- canvas ----------

        static RectTransform BuildCanvas(Transform parent)
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }

        static void BuildBackdrop(RectTransform canvas)
        {
            if (_theme.BackdropArt != null)
            {
                var art = NewImage("BackdropArt", canvas, Color.white, _theme.BackdropArt);
                art.type = Image.Type.Simple;
                art.preserveAspect = true;
                Stretch(art.rectTransform);
            }

            // Kerudung SEPARUH LAYAR, bukan seluruhnya.
            //
            // Yang lama membentangkan satu bidang gelap 55% di atas segalanya. Itu benar selama
            // yang ada di belakangnya cuma diorama placeholder — dan langsung jadi salah begitu
            // latar belakangnya sendiri yang jadi jualan: menggelapkannya rata berarti membangun
            // lima lapis lalu menutupinya dengan satu.
            //
            // Yang dibutuhkan teks cuma kontras DI BAWAH KOLOM MENU, dan kolom itu ada di kiri.
            // Jadi gelapnya ditaruh di kiri saja, meluruh ke bening sebelum sampai tengah, dan
            // separuh kanan layar — tempat lingkaran sihirnya duduk — dibiarkan utuh.
            var gradient = LeftScrimSprite();
            if (gradient == null) return;

            var scrim = NewImage("BackdropScrim", canvas, Color.white, gradient);
            scrim.type = Image.Type.Simple;

            var rect = scrim.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0.62f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Pita gradasi 64x1: pekat di kiri, bening di kanan. Disimpan sebagai PNG di folder menu
        /// supaya bisa dibuka dan diganti tangan seperti aset lain — dan supaya tidak ada tekstur
        /// yatim yang lahir tiap kali scene-nya dibangun ulang.
        ///
        /// Create-only: file yang sudah ada dipakai apa adanya.
        /// </summary>
        static Sprite LeftScrimSprite()
        {
            const string path = MenuAssetDir + "/T_LeftScrim.png";

            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            const int width = 64;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false);
            var veil = _theme.BackdropVeil;

            for (int x = 0; x < width; x++)
            {
                // Kuadrat, bukan lurus. Peluruhan lurus menyisakan kabut tipis sepanjang separuh
                // layar yang masih cukup untuk memucatkan lingkaran sihirnya; yang kuadrat sudah
                // hampir bening di sepertiga pertama.
                float t = x / (width - 1f);
                float fade = (1f - t) * (1f - t);
                texture.SetPixel(x, 0, new Color(veil.r, veil.g, veil.b, veil.a * fade));
            }

            texture.Apply();

            EnsureFolder(MenuAssetDir);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;

            // WAJIB disebut sendiri. Di project 3D, menyetel textureType ke Sprite TIDAK ikut
            // menyetel mode ini — asetnya terimpor sebagai tekstur tanpa Sprite di dalamnya, dan
            // Image yang spritenya null menggambar KOTAK PUTIH PENUH. Persis itu yang terjadi
            // percobaan pertama: separuh kiri layar jadi putih pekat.
            importer.spriteImportMode = SpriteImportMode.Single;

            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
            {
                Debug.LogWarning("[MainMenuBuilder] " + path + " gagal jadi Sprite — " +
                                 "scrim dilewati supaya tidak jadi kotak putih.");
            }

            return sprite;
        }

        // ---------- root page ----------

        static RectTransform BuildRootPage(RectTransform canvas, out Button play, out Button codex,
            out Button settings, out Button quit, out TextMeshProUGUI version)
        {
            var page = NewRect("RootPage", canvas);
            Stretch(page);

            // Judulnya hidup di MenuTheme, bukan di sini: nama game diganti dengan mengedit satu
            // field asset, dan jarak antar hurufnya diatur TMP — bukan dengan menyelipkan spasi ke
            // dalam string, yang membuat teksnya tidak bisa dicari dan pecah saat namanya berubah.
            var title = NewText("Title", page, _theme.GameTitle,
                _theme.TitleSize, _theme.TextIdle, TextAlignmentOptions.Left, true);
            title.characterSpacing = _theme.TitleTracking;
            Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(180f, 232f), new Vector2(1400f, 120f));

            // Tanpa tagline — permintaan pemilik project: "cukup nama aja". Garisnya tetap, karena
            // dialah yang mengikat judul ke daftar menu; tanpa itu keduanya mengambang sendiri.
            var rule = NewImage("Rule", page, _theme.PanelLine);
            Place(rule.rectTransform, new Vector2(0f, 0.5f), new Vector2(186f, 168f), new Vector2(420f, 2f));

            var list = NewRect("MenuList", page);
            Place(list, new Vector2(0f, 0.5f), new Vector2(180f, 118f), new Vector2(520f, 10f));
            list.pivot = new Vector2(0f, 1f);

            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = _theme.MenuSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            play = NewMenuLine(list, "Play", "MULAI");
            codex = NewMenuLine(list, "Codex", "CODEX");
            settings = NewMenuLine(list, "Settings", "SETELAN");
            quit = NewMenuLine(list, "Quit", "KELUAR");

            version = NewText("Version", page, "v0.0", _theme.SmallSize,
                _theme.TextMuted, TextAlignmentOptions.Right);
            Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 34f), new Vector2(300f, 24f));

            return page;
        }

        // ---------- codex page ----------

        static RectTransform BuildCodexPage(RectTransform canvas, ContentDatabase database,
            out CodexPanel panelComponent, out Button back)
        {
            var page = NewRect("CodexPage", canvas);
            Stretch(page);

            var panel = NewPanel(page, "CodexPanel", new Vector2(1420f, 840f));
            panelComponent = panel.gameObject.AddComponent<CodexPanel>();

            var heading = NewText("Heading", panel, "CODEX", _theme.HeadingSize,
                _theme.TextIdle, TextAlignmentOptions.Left, true);
            Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -40f), new Vector2(600f, 52f));

            var counter = NewText("Counter", panel, "0 / 0 KETEMU", _theme.BodySize,
                _theme.Accent, TextAlignmentOptions.Right);
            Place(counter.rectTransform, new Vector2(1f, 1f), new Vector2(-48f, -50f), new Vector2(500f, 30f));

            var hint = NewText("EmptyHint", panel,
                "Belum ada yang ketemu. Ambil barang di dalam run buat ngisi ini.",
                _theme.BodySize, _theme.TextMuted, TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 40f));

            var content = BuildScrollArea(panel, out var viewport);
            var headerTemplate = BuildCodexHeaderTemplate(content);
            var sectionTemplate = BuildCodexSectionTemplate(content);
            var template = BuildCodexEntryTemplate(content);

            new Binder(panelComponent)
                .Set("_database", database)
                .Set("_counter", counter)
                .Set("_emptyHint", hint)
                .Set("_content", content)
                .Set("_entryTemplate", template)
                .Set("_headerTemplate", headerTemplate)
                .Set("_sectionTemplate", sectionTemplate)
                .Apply();

            back = NewMenuLine(panel, "Back", "KEMBALI");
            var backRect = (RectTransform)back.transform;
            Place(backRect, new Vector2(0f, 0f), new Vector2(48f, 26f), new Vector2(360f, 46f));

            // The scroll area must not swallow the row the back button sits on.
            Place(viewport, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(1320f, 620f));

            return page;
        }

        // ---------- starter page ----------

        /// <summary>
        /// Layar pilih starter. Yang besar di tengah adalah PAPAN PEMBUKANYA, bukan portrait:
        /// susunan petak itu satu-satunya bagian kartu yang benar-benar memberi tahu apa yang
        /// akan dimainkan, dan ia digambar sendiri oleh panelnya dari aset — jadi di sini cuma
        /// disediakan kotaknya.
        ///
        /// <b>Begitu <c>StarterPanel.prefab</c> ada, prefab itu yang memegang tata letaknya</b> dan
        /// fungsi ini berhenti menata apa pun — ia cuma menyambungkan data ke label-label yang
        /// ditunjuk <see cref="StarterRig"/>. Itu satu-satunya cara "geser posisinya" bisa
        /// bertahan: scene menu digenerate ulang setiap kali, prefab tidak.
        /// </summary>
        static RectTransform BuildStarterPage(RectTransform canvas, ContentDatabase database,
            GameBalance balance, out StarterSelectPanel panelComponent, out Button back)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StarterPrefabPath);
            if (existing != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(existing, canvas);
                instance.name = "StarterPage";

                var prefabRig = instance.GetComponentInChildren<StarterRig>(true);
                panelComponent = instance.GetComponentInChildren<StarterSelectPanel>(true);

                if (prefabRig == null || panelComponent == null)
                {
                    Debug.LogError("[MainMenuBuilder] " + StarterPrefabPath + " tidak membawa " +
                                   "StarterRig atau StarterSelectPanel. Hapus prefabnya kalau mau " +
                                   "halaman bawaan dibangun ulang dari nol.");
                    back = null;
                    return (RectTransform)instance.transform;
                }

                BindStarter(panelComponent, database, balance, prefabRig);
                back = prefabRig.Back;
                return (RectTransform)instance.transform;
            }

            var page = NewRect("StarterPage", canvas);
            Stretch(page);

            var panel = NewPanel(page, "StarterPanel", new Vector2(1420f, 840f));
            panelComponent = panel.gameObject.AddComponent<StarterSelectPanel>();
            var rig = panel.gameObject.AddComponent<StarterRig>();

            var heading = NewText("Heading", panel, "PILIH GRIMOIRE", _theme.HeadingSize,
                _theme.TextIdle, TextAlignmentOptions.Left, true);
            Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -40f), new Vector2(700f, 52f));

            var page01 = NewText("PageLabel", panel, "1 / 1", _theme.BodySize,
                _theme.Accent, TextAlignmentOptions.Right);
            Place(page01.rectTransform, new Vector2(1f, 1f), new Vector2(-48f, -50f), new Vector2(300f, 30f));

            // Papannya BUKAN kotak kosong lagi: prefab papan grimoire yang dipakai di dalam run
            // ditaruh apa adanya di sini, dan petak 7x7 digambar di dalam `GridArea` miliknya.
            // Sampul, mata, dan rune-nya ikut — layar pilih starter memperlihatkan benda yang
            // sama persis dengan yang akan dipegang pemain, bukan gambar penggantinya.
            var board = BuildStarterBoard(panel);

            // Nama dan blurb DULU saling menimpa: keduanya dipasang dengan pivot tengah, dan kotak
            // blurb setinggi 220 yang dipusatkan di y=96 naik sampai y=206 — melewati baris nama di
            // 126..174. Teksnya rata-atas, jadi kalimat pertama mendarat persis di atas namanya.
            // Sekarang nama dipatok di 200 dan blurb digantung dari 160 KE BAWAH (pivot atas), jadi
            // panjang blurb tidak pernah lagi bisa merambat naik.
            var name = NewText("Name", panel, "Nama Starter", _theme.HeadingSize,
                _theme.Accent, TextAlignmentOptions.Left, true);
            Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(48f, 200f), new Vector2(420f, 54f));

            var blurb = NewText("Blurb", panel, "", _theme.BodySize,
                Color.Lerp(_theme.TextMuted, _theme.TextIdle, 0.55f), TextAlignmentOptions.TopLeft);
            Place(blurb.rectTransform, new Vector2(0f, 0.5f), new Vector2(48f, 160f), new Vector2(420f, 280f));
            blurb.rectTransform.pivot = new Vector2(0f, 1f);
            blurb.textWrappingMode = TextWrappingModes.Normal;

            // Blurb adalah satu-satunya paragraf di seluruh menu; baris rapat yang enak untuk label
            // satu baris justru bikin paragraf jadi blok abu-abu.
            blurb.lineSpacing = 14f;

            var stats = NewText("Stats", panel, "", _theme.BodySize,
                _theme.TextIdle, TextAlignmentOptions.Center);
            Place(stats.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(1200f, 30f));

            var portrait = NewImage("Portrait", panel, Color.white);
            Place(portrait.rectTransform, new Vector2(1f, 0.5f), new Vector2(-48f, 0f), new Vector2(380f, 480f));
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;

            // Panahnya mengapit papan, bukan berdiri di tepi panel: yang sedang diganti isi
            // papannya, dan tombol yang jauh dari benda yang berubah terbaca sebagai ganti halaman.
            var prev = NewMenuLine(panel, "Prev", "<");
            Place((RectTransform)prev.transform, new Vector2(0.5f, 0.5f), new Vector2(-300f, 22f),
                new Vector2(90f, 90f));

            var next = NewMenuLine(panel, "Next", ">");
            Place((RectTransform)next.transform, new Vector2(0.5f, 0.5f), new Vector2(300f, 22f),
                new Vector2(90f, 90f));

            var play = NewMenuLine(panel, "Launch", "MULAI RUN");
            Place((RectTransform)play.transform, new Vector2(1f, 0f), new Vector2(-48f, 26f),
                new Vector2(360f, 46f));

            back = NewMenuLine(panel, "Back", "KEMBALI");
            Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(48f, 26f),
                new Vector2(360f, 46f));

            rig.Board = board;
            rig.NameLabel = name;
            rig.Blurb = blurb;
            rig.Stats = stats;
            rig.PageLabel = page01;
            rig.Portrait = portrait;
            rig.Prev = prev;
            rig.Next = next;
            rig.Play = play;
            rig.Back = back;

            BindStarter(panelComponent, database, balance, rig);

            SaveStarterPrefab(page.gameObject);
            return page;
        }

        /// <summary>
        /// Papan preview: prefab papan grimoire yang dipakai di dalam run, ditaruh apa adanya.
        /// Petaknya digambar di dalam <c>GridArea</c> milik prefab itu — kotak yang sama yang
        /// menentukan letak petak saat bermain, jadi preview dan papan sungguhan mustahil
        /// berbeda ukuran sel.
        ///
        /// Tanpa prefab (tema kosong), kembali ke kotak kosong 470x470 seperti sebelumnya.
        /// </summary>
        static RectTransform BuildStarterBoard(RectTransform panel)
        {
            var uiTheme = AssetDatabase.LoadAssetAtPath<UiTheme>(UiThemePath);

            if (uiTheme != null && uiTheme.GrimoirePanelPrefab != null)
            {
                var art = (GameObject)PrefabUtility.InstantiatePrefab(uiTheme.GrimoirePanelPrefab, panel);
                art.name = "BoardArt";

                // Prefabnya dirancang duduk di pojok kiri-bawah layar saat bermain. Di sini ia
                // dipindah ke tengah panel; susunan ISI-nya tidak disentuh, cuma titik tumpunya.
                var artRect = (RectTransform)art.transform;
                artRect.anchorMin = artRect.anchorMax = artRect.pivot = new Vector2(0.5f, 0.5f);
                artRect.anchoredPosition = new Vector2(0f, 10f);

                var area = FindGridArea(art.transform);
                if (area != null) return area;

                Debug.LogWarning("[MainMenuBuilder] prefab papan grimoire tidak membawa GridArea — " +
                                 "petak starter jatuh ke kotak kosong bawaan.");
            }

            var board = NewRect("Board", panel);
            Place(board, new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(470f, 470f));
            return board;
        }

        /// <summary>Kotak petak di dalam prefab papan: lewat komponennya dulu, lalu lewat nama.</summary>
        static RectTransform FindGridArea(Transform root)
        {
            var marker = root.GetComponentInChildren<GrimoireGridArea>(true);
            if (marker != null) return (RectTransform)marker.transform;

            var all = root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == "GridArea") return all[i];
            }

            return null;
        }

        /// <summary>
        /// Menyambungkan data + widget ke panelnya. Dipakai dua kali: sekali untuk halaman bawaan
        /// yang baru dibangun, dan sekali lagi tiap rebuild untuk halaman yang datang dari prefab.
        /// </summary>
        static void BindStarter(StarterSelectPanel panel, ContentDatabase database,
            GameBalance balance, StarterRig rig)
        {
            new Binder(panel)
                .Set("_database", database)
                .Set("_balance", balance)
                .SetString("_gameSceneName", "Proto")
                .Set("_nameLabel", rig.NameLabel)
                .Set("_blurbLabel", rig.Blurb)
                .Set("_statsLabel", rig.Stats)
                .Set("_pageLabel", rig.PageLabel)
                .Set("_portrait", rig.Portrait)
                .Set("_board", rig.Board)
                .Set("_prevButton", rig.Prev)
                .Set("_nextButton", rig.Next)
                .Set("_playButton", rig.Play)
                .Apply();
        }

        /// <summary>
        /// Menyimpan halaman starter sebagai prefab SEKALI. Sesudah file ini ada, builder tidak
        /// pernah menulisnya lagi — di situlah tata letak halaman ini boleh digeser tangan tanpa
        /// hilang tiap rebuild.
        /// </summary>
        static void SaveStarterPrefab(GameObject page)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(page, StarterPrefabPath);

            if (prefab == null)
            {
                Debug.LogError("[MainMenuBuilder] gagal menyimpan " + StarterPrefabPath + ".");
                return;
            }

            Debug.Log("[MainMenuBuilder] " + StarterPrefabPath + " dibuat. Mulai sekarang tata " +
                      "letak layar pilih starter diatur DI PREFAB ITU — rebuild menu tidak akan " +
                      "menimpanya lagi. Hapus prefabnya kalau mau kembali ke tata letak bawaan.");
        }

        static RectTransform BuildScrollArea(RectTransform panel, out RectTransform viewport)
        {
            var scrollGo = NewRect("Scroll", panel);
            viewport = scrollGo;

            var scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var viewGo = NewRect("Viewport", scrollGo);
            Stretch(viewGo);
            var mask = viewGo.gameObject.AddComponent<Image>();
            mask.color = new Color(0f, 0f, 0f, 0.001f);
            viewGo.gameObject.AddComponent<RectMask2D>();

            var content = NewRect("Content", viewGo);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);

            // Tumpukan vertikal, bukan grid rata: isinya sekarang berseksi — judul, lalu grid
            // kartu, lalu judul berikutnya. Grid kartunya milik tiap seksi (lihat
            // BuildCodexSectionTemplate); tinggi tiap anak dibaca dari preferredHeight-nya,
            // itulah sebabnya childControlHeight menyala.
            var stack = content.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 10f;
            stack.padding = new RectOffset(8, 8, 4, 16);
            stack.childAlignment = TextAnchor.UpperLeft;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewGo;
            scroll.content = content;

            return content;
        }

        /// <summary>
        /// Judul satu seksi codex: nama rak di kiri (emas), hitungan ketemu di kanan, garis
        /// tipis di dasar. Teksnya sengaja bernama "Title" dan "Count" — CodexPanel mengisinya
        /// lewat nama, supaya cetakan ini tidak butuh komponen rig sendiri.
        /// </summary>
        static RectTransform BuildCodexHeaderTemplate(RectTransform content)
        {
            var header = NewRect("HeaderTemplate", content);

            var height = header.gameObject.AddComponent<LayoutElement>();
            height.minHeight = 48f;
            height.preferredHeight = 48f;

            var title = NewText("Title", header, "SEKSI", _theme.BodySize + 6,
                _theme.Accent, TextAlignmentOptions.BottomLeft, true);
            title.rectTransform.anchorMin = new Vector2(0f, 0f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(6f, 9f);
            title.rectTransform.offsetMax = new Vector2(-280f, 0f);

            var count = NewText("Count", header, "0 / 0 KETEMU", _theme.SmallSize,
                _theme.TextMuted, TextAlignmentOptions.BottomRight);
            count.rectTransform.anchorMin = new Vector2(1f, 0f);
            count.rectTransform.anchorMax = new Vector2(1f, 1f);
            count.rectTransform.offsetMin = new Vector2(-266f, 11f);
            count.rectTransform.offsetMax = new Vector2(-6f, 0f);

            var rule = NewImage("Rule", header, _theme.PanelLine);
            rule.rectTransform.anchorMin = new Vector2(0f, 0f);
            rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.sizeDelta = new Vector2(0f, 1f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, 2f);

            header.gameObject.SetActive(false);
            return header;
        }

        /// <summary>
        /// Badan satu seksi codex: grid tempat kartu-kartu rak itu berbaris. Tanpa
        /// ContentSizeFitter — tingginya dilaporkan GridLayoutGroup sebagai preferredHeight
        /// dan dibaca tumpukan vertikal di atasnya.
        /// </summary>
        static RectTransform BuildCodexSectionTemplate(RectTransform content)
        {
            var section = NewRect("SectionTemplate", content);

            var grid = section.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(305f, 132f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(0, 0, 0, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            section.gameObject.SetActive(false);
            return section;
        }

        static CodexEntry BuildCodexEntryTemplate(RectTransform content)
        {
            var entry = NewRect("EntryTemplate", content);
            var component = entry.gameObject.AddComponent<CodexEntry>();

            var background = NewImage("Background", entry, _theme.SlotKnown, _theme.PanelSprite);
            Stretch(background.rectTransform);

            // Kartu sekarang 305x132 (lihat BuildCodexSectionTemplate) — siluet bentuknya ikut
            // membesar; 52 piksel yang lama dipilih waktu kartunya masih 250 dan lima kolom.
            var shape = NewRect("Shape", entry);
            Place(shape, new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(64f, 64f));
            shape.pivot = new Vector2(0f, 1f);

            var grid = shape.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(19f, 19f);
            grid.spacing = new Vector2(2f, 2f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = CodexEntry.ShapeGrid;

            var cells = new Object[CodexEntry.ShapeGrid * CodexEntry.ShapeGrid];
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = NewImage("Cell_" + i, shape, Color.white);
                cell.enabled = false;
                cells[i] = cell;
            }

            var label = NewText("Name", entry, "Nama", _theme.BodySize,
                _theme.TextIdle, TextAlignmentOptions.TopLeft);
            Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(94f, -18f), new Vector2(196f, 60f));
            label.rectTransform.pivot = new Vector2(0f, 1f);

            var meta = NewText("Meta", entry, "*", _theme.SmallSize,
                _theme.TextMuted, TextAlignmentOptions.BottomLeft);
            Place(meta.rectTransform, new Vector2(0f, 0f), new Vector2(16f, 12f), new Vector2(273f, 22f));
            meta.rectTransform.pivot = new Vector2(0f, 0f);

            new Binder(component)
                .Set("_background", background)
                .Set("_name", label)
                .Set("_meta", meta)
                .SetArray("_shapeCells", cells)
                .SetColor("_knownFill", _theme.SlotKnown)
                .SetColor("_unknownFill", _theme.SlotUnknown)
                .SetColor("_knownText", _theme.TextIdle)
                .SetColor("_unknownText", _theme.TextMuted)
                .SetColor("_unknownCell", _theme.SilhouetteCell)
                .Apply();

            entry.gameObject.SetActive(false);
            return component;
        }

        // ---------- settings page ----------

        static RectTransform BuildSettingsPage(RectTransform canvas, out SettingsPanel panelComponent,
            out Button back)
        {
            var page = NewRect("SettingsPage", canvas);
            Stretch(page);

            // Dulu satu gulungan setinggi 1060: empat seksi berbaris ke bawah, dan tiap baris
            // baru mendorong seksi di bawahnya sampai baris DATA menabrak tombol KEMBALI di
            // dasar panel. Sekarang empat sub-halaman yang bergantian — yang tumbuh cuma satu
            // halaman, dan panelnya boleh menyusut lagi ke ukuran yang tidak menelan layar.
            var panel = NewPanel(page, "SettingsPanel", new Vector2(1180f, 720f));
            panelComponent = panel.gameObject.AddComponent<SettingsPanel>();

            var heading = NewText("Heading", panel, "SETELAN", _theme.HeadingSize,
                _theme.TextIdle, TextAlignmentOptions.Left, true);
            Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -40f), new Vector2(600f, 52f));

            // Rail tab di kiri, badan halaman di kanan garis. Semuanya dipatok dari sudut
            // kiri-atas panel, jadi menambah baris di satu halaman tidak menggeser halaman lain.
            const float BodyLeft = 372f;
            const float BodyTop = -112f;
            var bodySize = new Vector2(1180f - BodyLeft - 48f, 452f);

            var divider = NewImage("TabRule", panel, _theme.PanelLine);
            Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(332f, BodyTop),
                new Vector2(1f, bodySize.y));

            var layar = NewTabBody(panel, "Layar", BodyLeft, BodyTop, bodySize);
            float y = 0f;
            var fullscreen = NewStepper(layar, "Mode layar", ref y);
            var resolution = NewStepper(layar, "Resolusi", ref y);
            var vsync = NewStepper(layar, "VSync", ref y);
            var frameCap = NewStepper(layar, "Batas FPS", ref y);

            // Toggle untuk yang mahal digambar. Mengubahnya baru terasa di run BERIKUTNYA -
            // scene game membacanya saat lahir; catatan di bawah panel yang bilang begitu.
            var performa = NewTabBody(panel, "Performa", BodyLeft, BodyTop, bodySize);
            y = 0f;
            var damageText = NewStepper(performa, "Teks damage", ref y);
            var enemyShadows = NewStepper(performa, "Bayangan musuh", ref y);
            var weatherVfx = NewStepper(performa, "VFX cuaca", ref y);

            var suara = NewTabBody(panel, "Suara", BodyLeft, BodyTop, bodySize);
            y = 0f;
            var master = NewSliderRow(suara, "Master", ref y);
            var sfx = NewSliderRow(suara, "Efek suara", ref y);
            var music = NewSliderRow(suara, "Musik", ref y);

            var data = NewTabBody(panel, "Data", BodyLeft, BodyTop, bodySize);
            y = 0f;
            var reset = NewResetRow(data, ref y);

            var tabs = panel.gameObject.AddComponent<SettingsTabs>();
            var tabLines = new Object[4];
            NewTabLine(panel, tabLines, 0, "LAYAR", BodyTop);
            NewTabLine(panel, tabLines, 1, "PERFORMA", BodyTop);
            NewTabLine(panel, tabLines, 2, "SUARA", BodyTop);
            NewTabLine(panel, tabLines, 3, "DATA", BodyTop);

            new Binder(tabs)
                .SetArray("_lines", tabLines)
                .SetArray("_pages", new Object[]
                {
                    layar.gameObject, performa.gameObject, suara.gameObject, data.gameObject
                })
                .Apply();

            // Opposite the back button: the rows above run all the way down to it.
            var note = NewText("Note", panel, "", _theme.SmallSize,
                _theme.TextMuted, TextAlignmentOptions.Right);
            Place(note.rectTransform, new Vector2(1f, 0f), new Vector2(-48f, 84f), new Vector2(760f, 24f));

            new Binder(panelComponent)
                .Set("_fullscreenPrev", fullscreen.Prev)
                .Set("_fullscreenNext", fullscreen.Next)
                .Set("_fullscreenValue", fullscreen.Value)
                .Set("_resolutionPrev", resolution.Prev)
                .Set("_resolutionNext", resolution.Next)
                .Set("_resolutionValue", resolution.Value)
                .Set("_vsyncPrev", vsync.Prev)
                .Set("_vsyncNext", vsync.Next)
                .Set("_vsyncValue", vsync.Value)
                .Set("_frameCapPrev", frameCap.Prev)
                .Set("_frameCapNext", frameCap.Next)
                .Set("_frameCapValue", frameCap.Value)
                .Set("_damageTextPrev", damageText.Prev)
                .Set("_damageTextNext", damageText.Next)
                .Set("_damageTextValue", damageText.Value)
                .Set("_enemyShadowsPrev", enemyShadows.Prev)
                .Set("_enemyShadowsNext", enemyShadows.Next)
                .Set("_enemyShadowsValue", enemyShadows.Value)
                .Set("_weatherVfxPrev", weatherVfx.Prev)
                .Set("_weatherVfxNext", weatherVfx.Next)
                .Set("_weatherVfxValue", weatherVfx.Value)
                .Set("_masterSlider", master.Slider)
                .Set("_masterValue", master.Value)
                .Set("_sfxSlider", sfx.Slider)
                .Set("_sfxValue", sfx.Value)
                .Set("_musicSlider", music.Slider)
                .Set("_musicValue", music.Value)
                .Set("_resetCodex", reset.Button)
                .Set("_resetLabel", reset.Label)
                .Set("_resetHint", reset.Hint)
                .Set("_note", note)
                .SetColor("_dangerColor", new Color(0.85f, 0.35f, 0.3f, 1f))
                .SetColor("_mutedColor", _theme.TextMuted)
                .Apply();

            back = NewMenuLine(panel, "Back", "KEMBALI");
            Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(48f, 26f),
                new Vector2(360f, 46f));

            return page;
        }

        /// <summary>
        /// Menyimpan halaman setelan sebagai prefab dan menautkannya ke <see cref="UiTheme"/>,
        /// supaya scene game bisa membukanya tanpa tahu-menahu soal builder ini.
        /// </summary>
        static void SaveSettingsPrefab(GameObject page)
        {
            const string Path = "Assets/Prefabs/UI/SettingsPage.prefab";

            var prefab = PrefabUtility.SaveAsPrefabAsset(page, Path);

            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(UiThemePath);
            if (theme == null || prefab == null) return;

            var so = new SerializedObject(theme);
            so.FindProperty("SettingsPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
        }

        struct Stepper
        {
            public Button Prev;
            public Button Next;
            public TextMeshProUGUI Value;
        }

        struct SliderRow
        {
            public Slider Slider;
            public TextMeshProUGUI Value;
        }

        struct ResetRow
        {
            public Button Button;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Hint;
        }

        /// <summary>
        /// Badan satu sub-halaman setelan. Keempatnya menumpuk persis di kotak yang sama; yang
        /// menentukan mana yang terlihat adalah <see cref="SettingsTabs"/>.
        /// </summary>
        static RectTransform NewTabBody(RectTransform panel, string name, float left, float top,
            Vector2 size)
        {
            var body = NewRect("Tab_" + name, panel);
            Place(body, new Vector2(0f, 1f), new Vector2(left, top), size);
            return body;
        }

        static void NewTabLine(RectTransform panel, Object[] into, int index, string label, float top)
        {
            var button = NewMenuLine(panel, "Tab" + index, label);

            Place((RectTransform)button.transform, new Vector2(0f, 1f),
                new Vector2(48f, top - index * 56f), new Vector2(268f, 52f));

            into[index] = button.GetComponent<MenuLine>();
        }

        static RectTransform NewRow(RectTransform host, string name, string label, ref float y)
        {
            var row = NewRect("Row_" + name, host);
            Place(row, new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(host.rect.width, 48f));

            var text = NewText("Label", row, label, _theme.BodySize,
                _theme.TextIdle, TextAlignmentOptions.Left);
            Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(320f, 30f));

            y -= 58f;
            return row;
        }

        /// <summary>
        /// Kontrol baris dipatok dari tepi KANAN barisnya, bukan dari koordinat tetap. Angka
        /// 340/740 yang dulu di-hardcode benar hanya selama barisnya selebar panel penuh; badan
        /// sub-halaman lebih sempit, dan di sana keduanya menggantung keluar baris.
        /// </summary>
        static Stepper NewStepper(RectTransform host, string label, ref float y)
        {
            var row = NewRow(host, label, label, ref y);

            const float Arrow = 44f;
            const float Value = 240f;
            float x0 = row.rect.width - (Arrow * 2f + Value + 24f) - 8f;

            var stepper = new Stepper
            {
                Prev = NewSmallButton(row, "Prev", "<", x0, Arrow),
                Next = NewSmallButton(row, "Next", ">", x0 + Arrow + Value + 24f, Arrow)
            };

            stepper.Value = NewText("Value", row, "-", _theme.BodySize,
                _theme.TextIdle, TextAlignmentOptions.Center);
            Place(stepper.Value.rectTransform, new Vector2(0f, 0.5f), new Vector2(x0 + Arrow + 12f, 0f),
                new Vector2(Value, 30f));

            return stepper;
        }

        static SliderRow NewSliderRow(RectTransform host, string label, ref float y)
        {
            var row = NewRow(host, label, label, ref y);

            const float Track = 260f;
            const float Value = 70f;
            float x0 = row.rect.width - (Track + Value + 20f) - 8f;

            var sliderGo = DefaultControls.CreateSlider(UiResources());
            sliderGo.name = "Slider";
            sliderGo.transform.SetParent(row, false);

            var rect = (RectTransform)sliderGo.transform;
            Place(rect, new Vector2(0f, 0.5f), new Vector2(x0, 0f), new Vector2(Track, 20f));

            var slider = sliderGo.GetComponent<Slider>();
            StyleSlider(slider);

            var value = NewText("Value", row, "100%", _theme.BodySize,
                _theme.TextMuted, TextAlignmentOptions.Right);
            Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(x0 + Track + 20f, 0f),
                new Vector2(Value, 30f));

            return new SliderRow { Slider = slider, Value = value };
        }

        static ResetRow NewResetRow(RectTransform host, ref float y)
        {
            var row = NewRow(host, "Reset", "Codex", ref y);

            const float Wide = 260f;
            float x0 = row.rect.width - Wide - 8f;

            var button = NewSmallButton(row, "Reset", "KOSONGKAN CODEX", x0, Wide);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.color = _theme.TextMuted;

            // Petunjuknya turun ke bawah label barisnya. Di kolom kanan yang sekarang lebih
            // sempit ia akan berdesakan dengan tombolnya sendiri.
            var hint = NewText("Hint", row, "butuh dua klik", _theme.SmallSize,
                _theme.TextMuted, TextAlignmentOptions.Left);
            Place(hint.rectTransform, new Vector2(0f, 0.5f), new Vector2(6f, -20f), new Vector2(400f, 20f));

            return new ResetRow { Button = button, Label = label, Hint = hint };
        }

        // ---------- widgets ----------

        static Button NewMenuLine(RectTransform parent, string name, string label)
        {
            var line = NewRect("MenuLine_" + name, parent);

            var element = line.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 52f;
            element.preferredHeight = 52f;

            // The click target has to be a graphic, but the look has no box — so it stays invisible.
            var hit = NewImage("Hit", line, new Color(1f, 1f, 1f, 0f));
            hit.raycastTarget = true;
            Stretch(hit.rectTransform);

            var marker = NewImage("Marker", line, _theme.Accent, _theme.MarkerSprite);
            Place(marker.rectTransform, new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(14f, 3f));

            var text = NewText("Label", line, label, _theme.MenuItemSize,
                _theme.TextIdle, TextAlignmentOptions.Left);
            Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(460f, 44f));

            var button = line.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            var menuLine = line.gameObject.AddComponent<MenuLine>();
            new Binder(menuLine)
                .Set("_label", text)
                .Set("_marker", marker.rectTransform)
                .SetColor("_idle", _theme.TextIdle)
                .SetColor("_highlight", _theme.Accent)
                .SetFloat("_slide", _theme.HoverSlide)
                .Apply();

            return button;
        }

        static Button NewSmallButton(RectTransform parent, string name, string label, float x,
            float width = 48f)
        {
            var go = NewRect("Btn_" + name, parent);
            Place(go, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(width, 40f));

            var image = go.gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.sprite = _theme.ButtonSprite;
            if (_theme.ButtonSprite != null) image.type = Image.Type.Sliced;

            var button = go.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            var accent = Rendered(_theme.Accent);

            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.05f);
            colors.highlightedColor = new Color(accent.r, accent.g, accent.b, 0.30f);
            colors.pressedColor = new Color(accent.r, accent.g, accent.b, 0.50f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.02f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var text = NewText("Label", go, label, _theme.BodySize,
                _theme.TextIdle, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);

            return button;
        }

        static RectTransform NewPanel(RectTransform parent, string name, Vector2 size)
        {
            var frame = NewImage(name + "Frame", parent, _theme.PanelLine, _theme.PanelSprite);
            Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);

            var fill = NewImage(name, frame.rectTransform, _theme.PanelFill, _theme.PanelSprite);
            Stretch(fill.rectTransform);
            fill.rectTransform.offsetMin = new Vector2(2f, 2f);
            fill.rectTransform.offsetMax = new Vector2(-2f, -2f);

            // Panels eat clicks so a stray press never reaches whatever sits behind them.
            fill.raycastTarget = true;

            return fill.rectTransform;
        }

        static void StyleSlider(Slider slider)
        {
            var background = slider.transform.Find("Background")?.GetComponent<Image>();
            if (background != null) background.color = new Color(1f, 1f, 1f, 0.08f);

            var fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
            if (fill != null) fill.color = Rendered(_theme.Accent);

            var handle = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
            if (handle != null) handle.color = Rendered(_theme.TextIdle);
        }

        static DefaultControls.Resources UiResources()
        {
            return new DefaultControls.Resources
            {
                standard = Builtin("UI/Skin/UISprite.psd"),
                background = Builtin("UI/Skin/Background.psd"),
                inputField = Builtin("UI/Skin/InputFieldBackground.psd"),
                knob = Builtin("UI/Skin/Knob.psd"),
                checkmark = Builtin("UI/Skin/Checkmark.psd"),
                dropdown = Builtin("UI/Skin/DropdownArrow.psd"),
                mask = Builtin("UI/Skin/UIMask.psd")
            };
        }

        static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        /// <summary>
        /// Colours written to materials and cameras from script are handed to the renderer raw, so
        /// in a linear project an authored sRGB value renders washed out. UI graphics convert on
        /// their own — this is only for the 3D side.
        /// </summary>
        static Color Rendered(Color color) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;

        // ---------- primitives ----------

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image NewImage(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var rect = NewRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Rendered(color);
            image.raycastTarget = false;

            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        static TextMeshProUGUI NewText(string name, Transform parent, string text, float size,
            Color color, TextAlignmentOptions align, bool title = false)
        {
            var rect = NewRect(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();

            tmp.font = title ? _theme.ResolveTitleFont() : _theme.ResolveBodyFont();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = Rendered(color);
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;

            return tmp;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        // ---------- assets ----------

        static MenuTheme LoadOrCreateTheme()
        {
            var existing = AssetDatabase.LoadAssetAtPath<MenuTheme>(ThemePath);
            if (existing != null) return existing;

            var theme = ScriptableObject.CreateInstance<MenuTheme>();
            EnsureFolder(Path.GetDirectoryName(ThemePath).Replace('\\', '/'));
            AssetDatabase.CreateAsset(theme, ThemePath);
            Debug.Log("[MainMenuBuilder] MenuTheme baru dibikin di " + ThemePath +
                      ". Ganti aset placeholder di sini.");

            return theme;
        }

        static Material LoadOrCreateMaterial(string name, Color color)
        {
            string path = MenuAssetDir + "/" + name + ".mat";

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // Shader choice is structural, not art: the first placeholders were Unlit and would
                // ignore the sun completely. Only repaint when we actually had to switch it.
                if (existing.shader != lit)
                {
                    existing.shader = lit;
                    _look.ApplySurface(existing, color);
                    EditorUtility.SetDirty(existing);
                }

                return existing;
            }

            var material = new Material(lit);
            _look.ApplySurface(material, color);

            EnsureFolder(MenuAssetDir);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static T FindAsset<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids.Length == 0) return null;

            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        static Scene FindOpenScene(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.path == path) return scene;
            }

            return default;
        }

        /// <summary>
        /// MENAMBAHKAN menu & scene game ke Build Settings — tidak pernah menulis ulang daftarnya.
        ///
        /// Versi lama mengganti seluruh daftar dengan {menu, game}, dan itu bug yang baru
        /// kelihatan begitu ada scene ketiga: tiap rebuild menu menyapu scene ruangan
        /// toko/kejadian/slot dari daftar, dan RoomLoader menolaknya saat run — panel singgah
        /// jatuh balik ke atas arena tanpa error, cuma "kok nggak pindah". Daftar Build Settings
        /// itu milik BERSAMA; builder boleh menjamin barisnya sendiri ada, bukan menghapus
        /// baris orang lain.
        /// </summary>
        static void RegisterBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            // Menu selalu indeks 0 — scene pertama adalah yang dimuat build saat boot.
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));

            if (!File.Exists(GameScenePath))
            {
                Debug.LogWarning("[MainMenuBuilder] " + GameScenePath +
                                 " tidak ketemu — MULAI bakal gagal load scene.");
            }
            else if (!scenes.Exists(s => s.path == GameScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(GameScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Writes private [SerializeField] references without making them public.</summary>
        class Binder
        {
            readonly SerializedObject _target;

            public Binder(Object target) => _target = new SerializedObject(target);

            public Binder Set(string field, Object value)
            {
                var property = Find(field);
                if (property != null) property.objectReferenceValue = value;
                return this;
            }

            public Binder SetArray(string field, IReadOnlyList<Object> values)
            {
                var property = Find(field);
                if (property == null) return this;

                property.arraySize = values.Count;
                for (int i = 0; i < values.Count; i++)
                {
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }

                return this;
            }

            /// <summary>Converted like every other colour here: these fields are pushed straight
            /// onto Graphic.color / TMP.color at runtime, which is the same raw path.</summary>
            public Binder SetColor(string field, Color value)
            {
                var property = Find(field);
                if (property != null) property.colorValue = Rendered(value);
                return this;
            }

            public Binder SetFloat(string field, float value)
            {
                var property = Find(field);
                if (property != null) property.floatValue = value;
                return this;
            }

            public Binder SetVector3(string field, Vector3 value)
            {
                var property = Find(field);
                if (property != null) property.vector3Value = value;
                return this;
            }

            public Binder SetString(string field, string value)
            {
                var property = Find(field);
                if (property != null) property.stringValue = value;
                return this;
            }

            public void Apply() => _target.ApplyModifiedPropertiesWithoutUndo();

            SerializedProperty Find(string field)
            {
                var property = _target.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError("[MainMenuBuilder] field '" + field + "' tidak ada di " +
                                   _target.targetObject.GetType().Name);
                }

                return property;
            }
        }
    }
}
