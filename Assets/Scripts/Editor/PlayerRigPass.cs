using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Merakit avatar pemain dari paket <c>LizMage_Unity</c>, mengikuti resep BACA-DULU.txt
    /// milik paketnya baris demi baris: ekstrak tekstur & material, filter Point (atlasnya
    /// berpetak — filter halus membuat warna antar petak bocor), rig HUMANOID untuk kedua FBX,
    /// klip di-loop, Animator di ROOT hasil FBX, dan Cloth dipasang tangan di LizMage_Cape
    /// dengan kerah atas dijangkar.
    ///
    /// Kenapa Humanoid dan kenapa Animator wajib di root — pelajaran berdarah generasi
    /// pertama: klip Generic mencocokkan kurva lewat NAMA JALUR relatif terhadap transform
    /// Animator-nya. Satu tingkat pembungkus saja sudah membuat SEMUA kurva "(Missing!)" tanpa
    /// error, dan modelnya membeku diam. Humanoid mencocokkan lewat peta tulang, kebal susunan.
    ///
    /// Idempotent. Prefab hasil (<c>PlayerAvatar.prefab</c>) disimpan ke PATH LAMA supaya GUID
    /// dan referensi scene tetap hidup, dan TIDAK dirakit ulang kalau sudah sehat — kecuali
    /// terdeteksi dua tanda sakit: struktur pembungkus generasi pertama, atau root yang masih
    /// menunjuk FBX lama di luar folder LizMage_Unity.
    /// </summary>
    public static class PlayerRigPass
    {
        const string Folder = "Assets/Art/Characters/Player";
        const string Pack = Folder + "/LizMage_Unity";
        const string IdleFbx = Pack + "/LizMage_IDLE.fbx";
        const string RunFbx = Pack + "/LizMage_RUN.fbx";
        const string TexFolder = Pack + "/Textures";
        const string ControllerPath = Folder + "/PlayerAnim.controller";
        const string PrefabPath = Folder + "/PlayerAvatar.prefab";

        /// <summary>
        /// Tinggi badan yang dituju di dunia, meter. Kapsul lama 1,62; 1,65 lalu 2,10 dua-duanya
        /// masih dibilang KEKECILAN oleh pemilik project di layar sungguhan. Ukurannya memang
        /// harus dinilai dalam PIKSEL, bukan meter: kamera ortografis size 11 di layar 1080
        /// membuat 1 unit ≈ 49 px, dan proyeksi miring memendekkannya lagi. 3,0 unit ≈ 147 px —
        /// pemain akhirnya lebih tinggi dari Cursed (musuh terbesar, skala 1,45).
        /// </summary>
        const float TargetHeight = 3f;

        [MenuItem("Tools/Grimoire/Build Player Avatar")]
        public static void Run()
        {
            ExtractLook(IdleFbx);
            ExtractLook(RunFbx);
            PointFilterTextures();

            FixImporter(IdleFbx, "Idle");
            FixImporter(RunFbx, "Run");

            // Skala dihitung dari tinggi renderer TERUKUR sesudah impor — menebak "Mixamo
            // pasti sentimeter" adalah cara mendapat raksasa 100 meter diam-diam.
            float height = MeasuredHeight(IdleFbx);
            if (height > 0.01f)
            {
                float factor = TargetHeight / height;
                if (Mathf.Abs(1f - factor) > 0.02f)
                {
                    Rescale(IdleFbx, factor);
                    Rescale(RunFbx, factor);
                    height = MeasuredHeight(IdleFbx);
                }
            }

            var controller = BuildController();
            BuildPrefab(controller);

            Debug.Log($"[PlayerRigPass] beres. Tinggi avatar {height:0.00} unit, " +
                      $"controller {ControllerPath}, prefab {PrefabPath}.");
        }

        /// <summary>
        /// Langkah 1 BACA-DULU: Extract Textures + Extract Materials ke folder Textures.
        /// Material yang masih terkubur di dalam FBX tidak bisa diberi tekstur hasil ekstraksi
        /// secara permanen — Unity menulis ulang sub-aset tiap impor.
        /// </summary>
        static void ExtractLook(string fbxPath)
        {
            var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (imp == null) return;

            // Sekali saja. ExtractTextures memuntahkan salinan embedded bernama acak tiap kali
            // dipanggil (L1png.png, 537 KB, isi identik dengan atlas kiriman paket) — dijalankan
            // ulang tiap pass, folder Textures penuh kembar yang tidak dipakai siapa pun.
            bool alreadyExtracted =
                AssetDatabase.LoadAssetAtPath<Material>(TexFolder + "/LizMage_Mat.mat") != null;

            if (!alreadyExtracted) imp.ExtractTextures(TexFolder);

            bool extracted = false;
            foreach (var sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath))
            {
                var mat = sub as Material;
                if (mat == null) continue;

                string dst = $"{TexFolder}/{mat.name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(dst) != null)
                {
                    // Sudah pernah diekstrak (kemungkinan oleh FBX satunya — mereka berbagi
                    // material). Remap saja supaya FBX ini ikut memakai yang eksternal.
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(mat),
                        AssetDatabase.LoadAssetAtPath<Material>(dst));
                    extracted = true;
                    continue;
                }

                string err = AssetDatabase.ExtractAsset(mat, dst);
                if (!string.IsNullOrEmpty(err))
                {
                    Debug.LogWarning($"[PlayerRigPass] gagal ekstrak material {mat.name}: {err}");
                    continue;
                }

                extracted = true;
            }

            if (extracted)
            {
                AssetDatabase.WriteImportSettingsIfDirty(fbxPath);
                AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
            }

            FeedTextures();
        }

        /// <summary>
        /// FBX paket ini menunjuk teksturnya sebagai FILE LUAR (Textures/ ikut dikirim), jadi
        /// <c>ExtractTextures</c> tidak menemukan apa pun yang tertanam dan slot _BaseMap
        /// material hasil ekstraksi lahir KOSONG. Diisi tangan di sini: satu-satunya tekstur
        /// di folder itu memang atlas si kadal.
        /// </summary>
        static void FeedTextures()
        {
            // Atlas kiriman paket didahulukan — ExtractTextures bisa memuntahkan salinan
            // embedded bernama acak (L1png) yang isinya sama persis, dan dua nama untuk satu
            // gambar cuma menunggu salah pilih.
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(TexFolder + "/LizMage_Texture.png");

            if (atlas == null)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexFolder }))
                {
                    atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                    if (atlas != null) break;
                }
            }

            if (atlas == null) return;

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { TexFolder }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || !mat.HasProperty("_BaseMap")) continue;
                if (mat.GetTexture("_BaseMap") != null) continue;

                mat.SetTexture("_BaseMap", atlas);
                EditorUtility.SetDirty(mat);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Atlas berpetak wajib filter Point — kalau difilter halus, warna antar petak saling
        /// bocor dan wajahnya kotor (kata BACA-DULU, dan itu benar untuk semua atlas low-poly).
        /// </summary>
        static void PointFilterTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null || ti.filterMode == FilterMode.Point) continue;

                ti.filterMode = FilterMode.Point;
                ti.SaveAndReimport();
            }
        }

        static void FixImporter(string path, string clipName)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null)
            {
                Debug.LogError($"[PlayerRigPass] bukan model: {path}");
                return;
            }

            bool dirty = false;

            // Cloth menulis ulang vertex di CPU — tanpa Read/Write, AddComponent-nya saja
            // sudah melempar error.
            if (!imp.isReadable)
            {
                imp.isReadable = true;
                dirty = true;
            }

            // GENERIC, dan ini keputusan sadar melawan saran BACA-DULU: Humanoid dicoba dan
            // muscle-space manusianya MEREMUKKAN kadal berproporsi non-manusia ini jadi jarum
            // vertikal — preview klipnya pun ikut penyet, artinya avatarnya yang rusak, bukan
            // scene-nya. Masalah yang dulu memaksa Humanoid (kurva "(Missing!)") sudah mati
            // dari akarnya: model kini ROOT prefab dan Animator duduk persis di situ, jadi
            // pencocokan jalur Generic selalu ketemu. Generic = pose diputar persis seperti
            // di file, tanpa retarget. Harganya: animasi Mixamo baru tidak auto-retarget —
            // kalau suatu hari perlu, avatarnya dipetakan manual, bukan lewat auto T-pose.
            if (imp.animationType != ModelImporterAnimationType.Generic)
            {
                imp.animationType = ModelImporterAnimationType.Generic;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                dirty = true;
            }

            if (!imp.importAnimation)
            {
                imp.importAnimation = true;
                dirty = true;
            }

            // Rig Generic TIDAK punya konsep "root" bawaan — tanpa menunjuk simpulnya di sini,
            // seluruh setelan Bake Into Pose di bawah tidak melakukan apa pun (tercentang di
            // inspector, nol pengaruh). Panggul adalah simpul yang membawa perpindahan Mixamo.
            if (imp.motionNodeName != "mixamorig:Hips")
            {
                imp.motionNodeName = "mixamorig:Hips";
                dirty = true;
            }

            // Idle/Run dua-duanya gerak siklus — sekali main lalu beku adalah bug yang
            // kelihatan seperti "animasinya cuma jalan sekali".
            var defaults = imp.defaultClipAnimations;
            if (defaults.Length > 0)
            {
                var clips = imp.clipAnimations;
                bool needClip = clips.Length == 0 || clips[0].name != clipName ||
                                !clips[0].loopTime || clips[0].lockRootPositionXZ ||
                                !clips[0].lockRootHeightY || !clips[0].lockRootRotation;

                if (needClip)
                {
                    var clip = defaults[0];
                    clip.name = clipName;
                    clip.loopTime = true;

                    // Ini yang membetulkan sentakan balik saat Run berganti ke Idle, dan
                    // arahnya BERLAWANAN dengan tebakan pertama: "Bake Into Pose" berarti
                    // gerakan itu DIPERTAHANKAN di dalam pose. Dipasang untuk XZ, badannya
                    // justru merayap maju sepanjang klip lalu ditarik pulang oleh Idle.
                    //
                    // Yang benar untuk animasi di tempat:
                    // - XZ TIDAK dipanggang → perpindahannya diekstrak jadi root motion, lalu
                    //   dibuang karena applyRootMotion mati. Badan diam, kaki tetap melangkah.
                    // - Y dan rotasi DIPANGGANG → tidak ada root motion vertikal maupun putar,
                    //   jadi kaki tetap menapak dan model tidak berputar sendiri.
                    clip.lockRootPositionXZ = false;
                    clip.lockRootHeightY = true;
                    clip.keepOriginalPositionY = true;
                    clip.lockRootRotation = true;
                    clip.keepOriginalOrientation = true;

                    imp.clipAnimations = new[] { clip };
                    dirty = true;
                }
            }

            if (dirty) imp.SaveAndReimport();
        }

        static float MeasuredHeight(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) return 0f;

            var bounds = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                    continue;
                }

                bounds.Encapsulate(r.bounds);
            }

            return any ? bounds.size.y : 0f;
        }

        static void Rescale(string path, float factor)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) return;

            imp.globalScale *= factor;
            imp.SaveAndReimport();
        }

        static AnimatorController BuildController()
        {
            var idle = FindClip(IdleFbx, "Idle");
            var run = FindClip(RunFbx, "Run");

            var old = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (old != null) AssetDatabase.DeleteAsset(ControllerPath);

            var c = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            c.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var sm = c.layers[0].stateMachine;
            var idleState = sm.AddState("Idle");
            idleState.motion = idle;
            var runState = sm.AddState("Run");
            runState.motion = run;
            sm.defaultState = idleState;

            // Ambangnya sengaja renggang (0,9 vs 0,6): PlayerAvatar mengirim Speed sebagai
            // rasio terhadap ambang jalannya, dan dua ambang yang sama persis membuat animasi
            // bergonta-ganti tiap frame di kecepatan batas.
            var toRun = idleState.AddTransition(runState);
            toRun.hasExitTime = false;
            toRun.duration = 0.12f;
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.9f, "Speed");

            var toIdle = runState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.6f, "Speed");

            return c;
        }

        static AnimationClip FindClip(string path, string name)
        {
            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            {
                var clip = o as AnimationClip;
                if (clip != null && clip.name == name) return clip;
            }

            Debug.LogError($"[PlayerRigPass] klip '{name}' tidak ketemu di {path}");
            return null;
        }

        static void BuildPrefab(AnimatorController controller)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                // Dua tanda sakit yang memaksa rakit ulang: struktur pembungkus generasi
                // pertama (kurva Missing!), atau prefab yang masih lahir dari FBX lama di luar
                // paket LizMage_Unity. Selain itu prefabnya milik pemilik project.
                bool wrapped = existing.transform.Find("LizMage_IDLE") != null;

                var srcModel = PrefabUtility.GetCorrespondingObjectFromOriginalSource(existing);
                string srcPath = srcModel != null ? AssetDatabase.GetAssetPath(srcModel) : "";
                bool oldFbx = srcPath != IdleFbx;

                if (!wrapped && !oldFbx)
                {
                    // Sumbernya benar — jangan dirakit ulang (SaveAsPrefabAsset dengan root
                    // baru mengganti fileID root, dan referensi _playerAvatarPrefab di scene
                    // mati DIAM-DIAM; sudah kejadian sekali). Tapi setelan di DALAMNYA tetap
                    // disembuhkan di tempat.
                    Heal(controller);
                    return;
                }

                Debug.Log($"[PlayerRigPass] prefab lama (wrapped={wrapped}, sumberLama={oldFbx}) " +
                          "— dirakit ulang di path yang sama supaya referensi scene tetap hidup.");
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(IdleFbx);

            // Model-nya SENDIRI yang jadi root: Animator duduk di root hasil FBX persis pesan
            // BACA-DULU. Hasil simpannya prefab VARIANT — impor ulang model mengalir masuk.
            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            root.name = "PlayerAvatar";

            try
            {
                Outfit(root, controller);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            Debug.LogWarning("[PlayerRigPass] prefab dirakit ulang dari nol — fileID root " +
                             "berubah. Cek field _playerAvatarPrefab di scene Proto: kalau " +
                             "Missing, pasang ulang.");
        }

        /// <summary>
        /// Menyembuhkan prefab yang sudah ada TANPA mengganti identitas objeknya:
        /// LoadPrefabContents → betulkan → simpan balik. Identitas penting karena scene Proto
        /// memegang referensi ke root-nya.
        /// </summary>
        static void Heal(AnimatorController controller)
        {
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                Outfit(contents, controller);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            Debug.Log("[PlayerRigPass] prefab disembuhkan di tempat (root motion, cloth, controller).");
        }

        /// <summary>Semua setelan runtime avatar, dipakai jalur rakit-baru maupun jalur sembuh.</summary>
        static void Outfit(GameObject root, AnimatorController controller)
        {
            var anim = root.GetComponent<Animator>();
            if (anim == null) anim = root.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;

            // Root motion MATI — dua keluhan pemilik project lahir dari sini: klip Mixamo ikut
            // menyeret DAN memiringkan root tiap frame, jadi karakternya bergerak-gerak tidak
            // jelas melawan PlayerMotor, dan dari kamera atas badan yang miring terbaca GEPENG.
            // Posisi milik motor, pose milik klip; root motion tidak punya kursi di sini.
            anim.applyRootMotion = false;

            if (root.GetComponent<PlayerAvatar>() == null) root.AddComponent<PlayerAvatar>();

            DressCloth(root);
        }

        /// <summary>
        /// Langkah 5 BACA-DULU: Cloth di objek LizMage_Cape. Kerah atas dijangkar
        /// (maxDistance 0) — tanpa jangkar seluruh jubah jatuh ke lantai saat Play; sisanya
        /// makin longgar makin ke bawah supaya yang benar-benar berayun cuma ujungnya.
        /// Satu capsule collider kasar di badan menahan kain menembus dada saat berbalik.
        /// </summary>
        static void DressCloth(GameObject root)
        {
            SkinnedMeshRenderer cape = null;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name.Contains("Cape")) cape = r;
            }

            if (cape == null)
            {
                Debug.LogWarning("[PlayerRigPass] mesh Cape tidak ketemu — cloth dilewati.");
                return;
            }

            // Idempotent: pasang ulang dari nol tiap lewat sini, supaya jalur sembuh bisa
            // memperbaiki jangkar yang salah tanpa menumpuk komponen.
            var old = cape.GetComponent<Cloth>();
            if (old != null) Object.DestroyImmediate(old);

            var oldBody = root.transform.Find("ClothBody");
            if (oldBody != null) Object.DestroyImmediate(oldBody.gameObject);

            var cloth = cape.gameObject.AddComponent<Cloth>();

            // JANGKAR DIPETAKAN DARI PARTIKEL, DIUKUR DI RUANG DUNIA. Dua pelajaran mahal
            // tertanam di sini: (1) partikel cloth = vertex yang di-las (90), bukan vertex
            // mesh (112) — indeks mesh membuat jangkar nyasar dan jubah terbang liar;
            // (2) ruang LOKAL mesh ini tingginya cuma 6 MILIMETER dengan sumbu yang tidak
            // jelas (skala & rotasi hidup di node), jadi "atas" lokal itu derau — dua run
            // berturut menjangkar 20 lalu 4 partikel dari kode yang sama. Ruang dunia pada
            // bind pose stabil: jubah membentang ~0,9 unit dan kerahnya jelas di puncak.
            var particles = cloth.vertices;
            var coeffs = new ClothSkinningCoefficient[cloth.coefficients.Length];

            var heights = new float[particles.Length];
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < particles.Length; i++)
            {
                heights[i] = cape.transform.TransformPoint(particles[i]).y;
                minY = Mathf.Min(minY, heights[i]);
                maxY = Mathf.Max(maxY, heights[i]);
            }

            // SKALA ADALAH SEGALANYA DI SINI. FBX ini membawa skala 100 di node root-nya, dan
            // Cloth menghitung maxDistance / collisionSphereDistance / radius collider di ruang
            // LOKAL — jadi angka yang ditulis dalam meter diam-diam dikali 100. Batas ayun 0,14
            // jadi 14 METER (jubah membentang selebar layar) dan kapsul dada 0,13 jadi 13 meter
            // (menelan seluruh badan lalu menyodok jubah keluar ke depan). Semua angka di bawah
            // ini ditulis dalam METER DUNIA lalu dibagi skalanya.
            float scale = Mathf.Max(0.0001f, cape.transform.lossyScale.x);

            int pinned = 0;
            for (int i = 0; i < coeffs.Length; i++)
            {
                float h = i < heights.Length
                    ? Mathf.InverseLerp(maxY, minY, heights[i])
                    : 1f;

                // Kerah terjahit; ke bawah melonggar kuadratik. 14 cm di ujung — permintaannya
                // "tipis aja, tapi bergerak, jangan kaku": cukup untuk terbaca hidup, jauh dari
                // berkibar. Dibagi skala, jadi ini benar-benar 14 sentimeter di dunia.
                coeffs[i].maxDistance = h < 0.15f
                    ? 0f
                    : 0.18f * (TargetHeight / AuthoredHeight) * h * h / scale;

                // Jarak aman 2 cm terhadap collider tulang. Nol berarti kain boleh menempel
                // persis di kulit kapsul — dan di sanalah ia gampang terjepit lalu terlempar.
                coeffs[i].collisionSphereDistance = 0.02f / scale;
                if (coeffs[i].maxDistance <= 0f) pinned++;
            }

            cloth.coefficients = coeffs;

            // Kain ringan yang HIDUP, bukan kaku: redaman sedang (masih menghabisi osilasi
            // bolak-balik, tapi tidak membekukan gerak), tekuk lunak supaya jatuhnya luwes,
            // dan skala dunia kecil — pemain berbelok terus karena auto-dodge, dan angka besar
            // di sini yang dulu mencambuk jubah tiap frame.
            // Dilunakkan SEDIKIT atas permintaan pemilik project ("jangan terlalu kaku, tapi
            // dikit aja lunaknya"): redaman turun tipis supaya ayunan sempat terbaca sebelum
            // mati, tekuk lebih lunak supaya jatuhnya melipat alih-alih menekuk seperti karton.
            cloth.damping = 0.28f;
            cloth.stretchingStiffness = 0.85f;
            cloth.bendingStiffness = 0.08f;
            cloth.worldVelocityScale = 0.3f;
            cloth.worldAccelerationScale = 0.4f;
            cloth.friction = 0.4f;
            cloth.clothSolverFrequency = 240f;

            int cols = FitColliders(root, cloth);

            Debug.Log($"[PlayerRigPass] cloth: {coeffs.Length} partikel, {pinned} terjangkar di kerah, " +
                      $"{cols} collider tulang.");
        }

        /// <summary>
        /// Tulang yang harus menolak jubah, beserta jari-jari kapsulnya (meter, pada skala
        /// model). Tabel ini DIADOPSI dari <c>LizMage_Unity/Editor/LizMageClothSetup.cs</c>
        /// bawaan paket — pembuat modelnya yang paling tahu tulang mana yang menembus jubah.
        /// </summary>
        static readonly (string bone, float radius)[] ClothBones =
        {
            ("mixamorig:LeftArm", 0.055f),
            ("mixamorig:LeftForeArm", 0.045f),
            ("mixamorig:LeftHand", 0.045f),
            ("mixamorig:RightArm", 0.055f),
            ("mixamorig:RightForeArm", 0.045f),
            ("mixamorig:RightHand", 0.045f),
            ("mixamorig:Spine1", 0.130f),
            ("mixamorig:LeftUpLeg", 0.070f),
            ("mixamorig:RightUpLeg", 0.070f),
        };

        const string ClothColliderName = "ClothCollider";

        /// <summary>
        /// Tinggi karakter yang diasumsikan penulis tabel <see cref="ClothBones"/> — ukuran
        /// Mixamo standar. Semua angka meter di kain diskalakan dengan
        /// <c>TargetHeight / ini</c>, jadi membesarkan pemain tidak diam-diam membuat
        /// jubahnya kaku dan lengannya kekurusan.
        /// </summary>
        const float AuthoredHeight = 1.7f;

        /// <summary>
        /// Kapsul penolak per TULANG, bukan satu kapsul gendut di badan. Cloth Unity hanya
        /// mengenali collider yang terdaftar di daftarnya sendiri (dan hanya Capsule/Sphere),
        /// jadi memasang saja tidak cukup — harus didaftarkan.
        ///
        /// Versi lama memakai satu kapsul setinggi badan berjari-jari 0,26 m: lengan tidak
        /// punya penolak sama sekali (jubah tembus tangan), sementara silinder segemuk itu
        /// mendorong kain dari dalam — separuh dari rasa "brutal" yang dikeluhkan.
        /// </summary>
        static int FitColliders(GameObject root, Cloth cloth)
        {
            var bones = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                bones[t.name] = t;
            }

            // Sisa run sebelumnya dibersihkan dulu supaya pass tetap idempotent.
            var stale = new System.Collections.Generic.List<GameObject>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == ClothColliderName) stale.Add(t.gameObject);
            }

            foreach (var go in stale) Object.DestroyImmediate(go);

            var old = root.transform.Find("ClothBody");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var made = new System.Collections.Generic.List<CapsuleCollider>();

            foreach (var (boneName, radius) in ClothBones)
            {
                if (!bones.TryGetValue(boneName, out var bone) || bone == null) continue;

                var holder = new GameObject(ClothColliderName).transform;
                holder.SetParent(bone, false);

                var cap = holder.gameObject.AddComponent<CapsuleCollider>();

                // Jari-jari tabel ditulis dalam METER (0,055 m untuk lengan) sementara tulang
                // ini hidup di ruang berskala 100 — dipakai apa adanya, lengannya jadi kapsul
                // berjari-jari 5,5 meter. Dibagi lossyScale tulangnya sendiri.
                float boneScale = Mathf.Max(0.0001f, bone.lossyScale.x);

                // Panjang kapsul diambil dari jarak ke tulang anaknya (sudah dalam ruang lokal
                // yang sama, jadi TIDAK dibagi); tulang ujung (telapak) tidak punya anak yang
                // berarti, jadi dipakai panjang minimum.
                Transform child = bone.childCount > 0 ? bone.GetChild(0) : null;
                if (child != null && child.name == ClothColliderName)
                {
                    child = bone.childCount > 1 ? bone.GetChild(1) : null;
                }

                float localRadius = radius * (TargetHeight / AuthoredHeight) / boneScale;
                float length = child != null
                    ? Mathf.Max(child.localPosition.magnitude, localRadius * 2f)
                    : localRadius * 2f;

                cap.radius = localRadius;
                cap.height = length;
                cap.direction = LongestAxis(child != null ? child.localPosition : Vector3.up);
                cap.center = (child != null ? child.localPosition : Vector3.up * length) * 0.5f;

                made.Add(cap);
            }

            cloth.capsuleColliders = made.ToArray();
            return made.Count;
        }

        /// <summary>0 = X, 1 = Y, 2 = Z — sumbu yang paling searah dengan arah tulang.</summary>
        static int LongestAxis(Vector3 v)
        {
            var a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            if (a.x >= a.y && a.x >= a.z) return 0;
            return a.y >= a.z ? 1 : 2;
        }
    }
}
