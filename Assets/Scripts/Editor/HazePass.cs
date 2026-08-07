using Haze.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Proto.EditorTools
{
    /// <summary>
    /// Memasang kabut volumetrik HAZE ke renderer yang benar-benar dipakai game, lalu menyetelnya.
    ///
    /// Yang dicari bukan kabut. Yang dicari adalah CAHAYA YANG PUNYA ISI: matahari rendah yang
    /// menyorot lapangan terbuka seharusnya terlihat menyeberangi udara di antara pohon, bukan cuma
    /// mendarat di tanah. Tanpa itu, lapangan sekosong ini selalu terbaca sebagai gambar datar —
    /// terang di mana kena, gelap di mana tidak, tanpa apa pun di antaranya.
    ///
    /// Idempoten: dijalankan ulang tidak menumpuk fitur kedua.
    /// </summary>
    public static class HazePass
    {
        const string Vendor = "Assets/Plugin/HAZE - Volumetric Fog & Lighting for URP/";
        const string VendorRenderer = Vendor + "Settings/HazeRenderer.asset";
        const string GameRenderer = "Assets/Settings/PC_Renderer.asset";
        const string Grade = "Assets/GameData/Look/PP_Sunny.asset";
        const string NightGrade = "Assets/GameData/Look/PP_Night.asset";

        [MenuItem("Tools/Grimoire/Install Volumetric Fog")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[HazePass] Tidak bisa jalan di play mode. Stop dulu.");
                return;
            }

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(GameRenderer);

            if (renderer == null)
            {
                Debug.LogError("[HazePass] renderer game tidak ketemu: " + GameRenderer);
                return;
            }

            // Forward+ WAJIB. Sebagian sumbangan cahaya HAZE cuma ada di jalur itu, dan di Forward
            // biasa ia gagal tanpa error — kabutnya tetap tergambar, cuma tidak pernah menerima
            // cahaya dari lampu selain matahari.
            if (renderer.renderingMode != RenderingMode.ForwardPlus)
            {
                Debug.LogWarning("[HazePass] renderer bukan Forward+. Kabutnya akan tergambar, " +
                                 "tapi lampu selain matahari tidak akan menyumbang warna.");
            }

            if (!Attach(renderer)) return;

            BuildSmokePocket();

            Configure(Grade, false);
            Configure(NightGrade, true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[HazePass] Kabut volumetrik terpasang di " + GameRenderer +
                      " dan disetel di " + Grade + ".");
        }

        /// <summary>
        /// Menyalin fitur HAZE dari renderer bawaan paketnya, BUKAN membuatnya dari nol.
        ///
        /// Fitur ini membawa compute shader, tekstur derau 3D, dan resolusi froxel di dalam
        /// dirinya. <c>CreateInstance</c> menghasilkan fitur dengan semua itu kosong — dan yang
        /// terjadi bukan kabut yang salah, melainkan pengecualian tiap frame begitu masuk play.
        /// </summary>
        static bool Attach(UniversalRendererData renderer)
        {
            foreach (var existing in renderer.rendererFeatures)
            {
                if (existing is HazeRendererFeature)
                {
                    Debug.Log("[HazePass] fitur HAZE sudah terpasang — dilewati.");
                    return true;
                }
            }

            var source = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(VendorRenderer);
            HazeRendererFeature template = null;

            if (source != null)
            {
                foreach (var feature in source.rendererFeatures)
                {
                    if (feature is HazeRendererFeature haze) { template = haze; break; }
                }
            }

            if (template == null)
            {
                Debug.LogError("[HazePass] fitur HAZE tidak ketemu di " + VendorRenderer);
                return false;
            }

            var copy = Object.Instantiate(template);
            copy.name = "HazeRendererFeature";

            AssetDatabase.AddObjectToAsset(copy, renderer);
            AssetDatabase.SaveAssets();

            // Daftar fitur dan PETA-nya harus tumbuh bersama. Peta itu menyimpan local file id tiap
            // fitur; kalau ia tertinggal, fiturnya hilang diam-diam saat aset dimuat ulang — bukan
            // saat ditulis. Jadi kesalahannya baru muncul setelah Unity dibuka ulang.
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(copy, out _, out long localId))
            {
                Debug.LogError("[HazePass] gagal membaca file id fitur yang baru ditambahkan.");
                return false;
            }

            var so = new SerializedObject(renderer);
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");

            // Dua tahap, dan urutannya wajib — mengubah ukuran array lalu langsung mengisinya dalam
            // sesi SerializedObject yang sama menyimpan ukurannya saja.
            int slot = list.arraySize;
            list.arraySize = slot + 1;
            map.arraySize = slot + 1;
            so.ApplyModifiedPropertiesWithoutUndo();

            so.Update();
            so.FindProperty("m_RendererFeatures").GetArrayElementAtIndex(slot).objectReferenceValue = copy;
            so.FindProperty("m_RendererFeatureMap").GetArrayElementAtIndex(slot).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(renderer);
            return true;
        }

        /// <summary>
        /// Menyetel kabutnya di profil warna scene game.
        ///
        /// Angkanya sengaja RENDAH. Kabut volumetrik yang terlihat sebagai kabut mengubah lapangan
        /// terbuka jadi pagi berkabut, dan itu bukan yang diminta — yang diminta cahaya yang
        /// membias dan menyebar. Jadi kepadatannya dipasang tepat di ambang terlihat, dan yang
        /// dibiarkan menonjol adalah hamburan mataharinya.
        /// </summary>
        /// <summary>
        /// Membangun prefab kantong udara bersih yang nanti menempel di pemain.
        ///
        /// Asapnya sendiri adalah kabut GLOBAL — froxel volumetrik sungguhan, bukan bidang datar.
        /// Bidang datar tidak akan pernah lolos sebagai asap: ia tidak punya tebal, cahaya tidak
        /// menembusnya, dan dari kamera menunduk ia selalu terbaca sebagai stiker yang menempel di
        /// tanah. Yang dikerjakan volume ini adalah kebalikannya — MENGURANGI kepadatan di sekitar
        /// pemain, jadi yang tersisa adalah kantong bersih yang ikut berjalan di tengah asap.
        ///
        /// Dibangun sebagai PREFAB di editor, bukan dirakit saat bermain. Seluruh setelan
        /// HazeDensityVolume disimpan di field privat tanpa setter, jadi merakitnya saat bermain
        /// menuntut refleksi — dan menuntut kode runtime kita ikut bergantung pada paket HAZE.
        /// Lewat prefab, runtime cuma meng-instantiate sebuah GameObject.
        /// </summary>
        static void BuildSmokePocket()
        {
            const string path = "Assets/GameData/Look/SmokePocket.prefab";

            var go = new GameObject("SmokePocket");
            var volume = go.AddComponent<HazeDensityVolume>();

            var so = new SerializedObject(volume);

            // Bola, bukan kubus: kantongnya tidak boleh punya sudut. Sudut yang menyeberangi layar
            // terbaca sebagai tepi kotak yang bergerak, dan tidak ada asap yang berbentuk kotak.
            so.FindProperty("_shape").enumValueIndex = (int)HazeDensityVolume.Shape.Sphere;
            so.FindProperty("_densityMode").enumValueIndex =
                (int)HazeDensityVolume.VolumeDensityMode.Subtractive;

            so.FindProperty("_density").floatValue = 40f;
            so.FindProperty("_noiseThreshold").floatValue = 0.35f;
            so.FindProperty("_priority").intValue = 10;

            so.ApplyModifiedPropertiesWithoutUndo();

            // Skalanya adalah JARI-JARI kantongnya. Cukup lebar untuk memuat layar di sekitar
            // pemain, cukup sempit untuk menyisakan asap di tepi pandangan.
            go.transform.localScale = Vector3.one * 26f;

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        static void Configure(string path, bool night)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);

            // Profil malam dibuat dengan MENYALIN profil siang, supaya seluruh gradasi warnanya
            // (tonemap, kontras, bloom) tetap satu keluarga. Yang membedakan malam dari siang
            // seharusnya cahayanya, bukan cara warnanya diproses.
            if (profile == null && night)
            {
                if (AssetDatabase.CopyAsset(Grade, NightGrade))
                    profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(NightGrade);
            }

            if (profile == null)
            {
                Debug.LogError("[HazePass] profil warna tidak ketemu: " + path +
                               " — jalankan Tools/Grimoire/Generate Biomes dulu.");
                return;
            }

            if (!profile.TryGet<HazeGlobalFogVolumeComponent>(out var fog))
            {
                fog = profile.Add<HazeGlobalFogVolumeComponent>(true);

                // WAJIB, dan tanpa ini seluruh pemasangannya sia-sia.
                //
                // VolumeProfile.Add cuma menyisipkan komponennya ke daftar DI MEMORI. Objeknya
                // sendiri tidak pernah menjadi bagian dari file profilnya, jadi ia hidup tepat
                // sampai domain reload berikutnya — masuk play mode, ubah satu skrip, buka ulang
                // Unity — lalu lenyap.
                //
                // Kegagalannya sempurna diam: memeriksanya sesaat setelah dipasang memperlihatkan
                // komponen yang lengkap dengan seluruh nilainya, dan yang hilang baru ketahuan
                // kalau ada yang memeriksa lagi SETELAH reload.
                AssetDatabase.AddObjectToAsset(fog, profile);
                fog.hideFlags = HideFlags.HideInHierarchy;
            }

            fog.active = true;

            // Skalanya PULUHAN, bukan pecahan. Percobaan pertama memasang 0,07 dengan asumsi
            // angkanya sekitar 1 — dan hasilnya selisih nyaris nol di layar. Demo paketnya sendiri
            // memakai 24,21; nilai di bawah satu praktis berarti mati.
            // Malam LEBIH PEKAT, bukan lebih gelap. Kabut yang cuma diredupkan menghasilkan malam
            // yang bersih dan kosong; yang dicari udara yang terlihat, supaya tiap lampu punya
            // kolam cahaya dan tiap bayangan punya tepi yang meluruh.
            // Malam JAUH LEBIH TIPIS, bukan lebih pekat — dan itu kebalikan dari dugaan awal.
            //
            // Percobaan pertama memakai 26 dengan alasan "udara malam lebih terlihat", lalu 8.
            // Dua-duanya menenggelamkan lapangan jadi susu biru: kabut menyala dari SETIAP sumber
            // cahaya di sekitarnya, dan malam justru punya lebih banyak sumber daripada siang —
            // sepuluh lampu arena plus lampu pemain. Yang di siang hari cuma disinari satu matahari,
            // di malam hari disinari dua belas benda sekaligus.
            // Siang diturunkan lagi ke 2. Yang membuat lapangan hutan terasa teduh sekarang bukan
            // kabut melainkan BAYANGAN AWAN yang melintas — dan bayangan yang bergerak memberi
            // keteduhan tanpa mengurangi kejernihan, sementara kabut mengambil dua-duanya.
            // Dikembalikan ke tipis. Sempat dinaikkan ke 22 supaya kejauhan jadi asap pekat, tapi
            // yang mengerjakan kegelapan jarak sekarang shader Gloom — dan kabut setebal itu di
            // atasnya cuma memutihkan apa yang seharusnya menggelap.
            // Siang DITIPISKAN ke 1,2. Kabut yang cukup tebal untuk terlihat sebagai kabut mengubah
            // lapangan terbuka jadi pagi berkabut, dan yang dicari bukan itu — yang dicari udara
            // yang punya isi supaya cahaya punya tempat menyebar. Begitu ia mulai terlihat sebagai
            // lapisan, ia sudah kelewatan.
            Set(fog.GlobalDensityMultiplier, night ? 3f : 0.55f);
            Set(fog.GlobalDensityThreshold, night ? 0.3f : 0.35f);

            // Warna kabut mengikuti ambient, bukan matahari: bagian yang TIDAK kena cahaya
            // seharusnya membiru, dan itu yang membuat udaranya terasa punya kedalaman.
            // Siang DITERANGKAN. Warna ini yang dipakai kabut untuk menyala sendiri di bagian
            // yang tidak kena matahari — kalau gelap, kabutnya berhenti jadi udara dan berubah jadi
            // lapisan abu-abu yang menutupi lapangan.
            Set(fog.AmbientColor, night
                ? new Color(0.07f, 0.1f, 0.2f)
                : new Color(0.5f, 0.6f, 0.7f));

            // Sumbangan cahaya utama jauh melebihi 1 dengan sengaja — di situlah sinarnya jadi
            // berkas. Bulan menyumbang jauh lebih sedikit daripada matahari.
            // Siang diturunkan dari (3,2; 2,9; 2,2). Nilai setinggi itu memang membuat berkas
            // cahaya, tapi di lapangan yang SELURUHNYA kena matahari tidak ada tempat yang tidak
            // menerimanya — jadi yang dihasilkan bukan berkas melainkan kabut susu merata.
            Set(fog.MainLightContribution, night
                ? new Color(0.5f, 0.62f, 1f)
                : new Color(1f, 0.95f, 0.8f));

            Set(fog.MainLightScattering, night ? 0.2f : 0.35f);

            // Kabut MENGENDAP di lantai, bukan memenuhi ruang. Kamera duduk 18,5 unit di atas;
            // kabut setinggi itu berarti melihat seluruh lapangan lewat susu.
            // TINGGI, dan ini yang membuat berkas cahaya mungkin sama sekali.
            //
            // Berkas adalah KOLOM — ia berdiri dari tajuk pohon sampai ke tanah. Kolom setinggi
            // tujuh unit tidak akan pernah terbaca sebagai berkas karena pohonnya sendiri setinggi
            // delapan; yang tergambar cuma kabut yang mengendap di kaki. Batas 22 unit membuat
            // seluruh tinggi pohon berada DI DALAM kabut, jadi bayangan tajuknya memotong kabut
            // sepanjang kolom — dan potongan itulah berkasnya.
            Set(fog.HeightFogFactor, 0.8f);
            Set(fog.MaxFogHeight, 22f);
            Set(fog.HeightFogSmoothness, 5f);
            Set(fog.CameraRelativeHeightFog, false);

            // INILAH berkas cahayanya. Parameter ini menaikkan kepadatan kabut di daerah yang TIDAK
            // terbayangi, jadi celah di antara bayangan tajuk terisi cahaya yang menempati ruang.
            //
            // Sempat dinolkan karena pada 0,9 ia memutihkan seluruh layar — tapi itu terjadi saat
            // kabutnya masih setinggi 7 unit DAN sumbangan mataharinya masih 3,2. Dua-duanya sudah
            // dibetulkan, jadi sekarang yang naik kepadatannya cuma kolom udara di atas lantai,
            // bukan lantainya sendiri. 0,45 — cukup untuk terlihat, jauh dari 0,9 yang membakar.
            Set(fog.GlobalMainLightDensityBoost, night ? 0.3f : 0.45f);

            // Sumbangan lampu selain matahari, dan angkanya SANGAT peka.
            //
            // Terukur: 40 membakar layar jadi putih pastel, 3,5 masih membakar, 0,15 baru benar.
            // Sebabnya angka ini dikalikan oleh SETIAP lampu yang menjangkau froxel yang sama — di
            // tengah arena ada sepuluh lampu yang jangkauannya saling bertindih, jadi yang terjadi
            // bukan penjumlahan sepuluh cahaya lemah melainkan perkalian yang meledak.
            //
            // Siang tidak punya satu pun lampu titik, jadi angkanya di sana tidak pernah terpakai —
            // dan justru karena itu ia tidak boleh dibiarkan besar: begitu ada yang menyalakan satu
            // lampu di siang hari, ia akan membakar layar dengan cara yang sama.
            Set(fog.AdditionalLightContribution, night ? 0.15f : 1f);

            // Malam dijatuhkan sedikit exposure-nya di sini, bukan lewat cahaya. Meredupkan lampu
            // menghapus kolam cahayanya sekaligus; meredupkan exposure menyisakannya.
            if (night && profile.TryGet<ColorAdjustments>(out var color))
            {
                color.postExposure.overrideState = true;
                color.postExposure.value = -0.55f;

                color.saturation.overrideState = true;
                color.saturation.value = -14f;
            }

            EditorUtility.SetDirty(profile);
        }

        static void Set<T>(VolumeParameter<T> parameter, T value)
        {
            if (parameter == null) return;

            parameter.overrideState = true;
            parameter.value = value;
        }
    }
}
