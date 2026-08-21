using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Membangkitkan dua TEMPAT baru dari hutan yang sudah ada: MUSIM DINGIN dan RIMBA TERNODA.
    ///
    /// Tidak ada satu pun yang digambar dari nol. Paket tekstur yang sudah dibeli ternyata
    /// menyimpan seluruh musimnya — salju tiga gelap-terang, rumput ternoda, tanah terkorupsi —
    /// dan empat wajah hutan (siang/malam/senja/tengah-malam) sudah menyimpan seluruh penyetelan
    /// cahaya yang susah payah dicari. Pass ini cuma MENURUNKAN: tiap wajah baru lahir dari
    /// CopySerialized wajah hutan yang sejam dengannya, lalu ditimpa di tempat yang memang beda —
    /// lapisan tanah, palet pohon, dan cuacanya.
    ///
    /// Polanya pola <see cref="BiomePass"/>, termasuk penyelamatan daftar cuaca sebelum
    /// CopySerialized — pelajaran kunang-kunang yang hilang berlaku di sini juga.
    ///
    /// Idempoten: aset yang sudah ada dipakai ulang, penyetelan tangan pada daftar cuaca dan
    /// suasana tidak ditimpa ulang.
    /// </summary>
    public static class WinterBiomePass
    {
        const string BiomeFolder = "Assets/GameData/Biomes";
        const string TerrainFolder = "Assets/GameData/Terrain";
        const string WinterLook = "Assets/GameData/Look/Winter";
        const string BlightLook = "Assets/GameData/Look/Blight";

        const string Painted = "Assets/Plugin/Handpainted_Grass_and_Ground_Textures/Textures/";
        const string Vfxs = "Assets/Plugin/Lana Studio/Environment VFX pack/Prefabs/";

        /// <summary>Urutan wajah = urutan undian waktu-hari: siang, malam, senja, tengah malam.</summary>
        static readonly string[] ForestFaces =
        {
            "Biome_forest", "Biome_forest_night", "Biome_forest_sore", "Biome_forest_midnight",
        };

        [MenuItem("Tools/Grimoire/Generate Winter + Blight Biomes")]
        public static void Run()
        {
            EnsureFolder("Assets/GameData/Look", "Winter");
            EnsureFolder("Assets/GameData/Look", "Blight");

            // ---- lapisan tanah ------------------------------------------------------------
            //
            // Jumlah dan URUTANNYA harus persis empat milik hutan (dasar, bercak terang, bercak
            // gelap, jalur tanah). Splatmap menyimpan bobot per-INDEKS: lapisan kelima tidak akan
            // pernah tercat, dan urutan yang tertukar membuat jalur tanah dicat salju terang.
            //
            // Ubinnya mewarisi trik anti-kisi hutan: tiga ukuran yang tidak berkelipatan.
            var snowLayers = new[]
            {
                Layer("Layer_Snow", Painted + "Snow/snow_normal.png", 14f),
                Layer("Layer_Snow_Sun", Painted + "Snow/snow_normal.png", 6.5f),
                Layer("Layer_Snow_Shade", Painted + "Snow/snow_dark.png", 9.5f),
                Layer("Layer_Snow_Path", Painted + "Snow/snow_super_dark.png", 5f),
            };

            var blightLayers = new[]
            {
                Layer("Layer_Blight", Painted + "Grass/Grass_corrupted/Grass_corrupted_up.png", 14f),
                Layer("Layer_Blight_Sun", Painted + "Grass/Grass_overcorrupted/Grass_overcorrupted_right.png", 6.5f),
                Layer("Layer_Blight_Shade", Painted + "Grass/Grass_darked/Grass_darked_down.png", 9.5f),
                Layer("Layer_Blight_Path", Painted + "Dirt/dirt_corrupted/dirt_corrupted_up.png", 5f),
            };

            // ---- wajah-wajah --------------------------------------------------------------

            var winterFaces = new BiomeDefinition[ForestFaces.Length];
            var blightFaces = new BiomeDefinition[ForestFaces.Length];

            for (int i = 0; i < ForestFaces.Length; i++)
            {
                var source = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(
                    BiomeFolder + "/" + ForestFaces[i] + ".asset");

                if (source == null)
                {
                    Debug.LogError("[WinterBiomePass] wajah hutan tidak ketemu: " + ForestFaces[i] +
                                   " — jalankan Generate Biomes dulu.");
                    return;
                }

                winterFaces[i] = DeriveFace(source, "winter", i, snowLayers, WinterLook,
                    new Color(0.88f, 0.93f, 1f), 0.5f, WinterPalette, WinterLight, SnowMoods());

                blightFaces[i] = DeriveFace(source, "blight", i, blightLayers, BlightLook,
                    new Color(0.72f, 0.42f, 0.8f), 0.38f, BlightPalette, BlightLight, null);
            }

            // ---- tempat -------------------------------------------------------------------

            var forestPlace = Place("Place_forest", "Verdant Hollow", LoadFaces(ForestFaces));
            var winterPlace = Place("Place_winter", "Frozen Hollow", winterFaces);
            var blightPlace = Place("Place_blight", "Blighted Hollow", blightFaces);

            AssetDatabase.SaveAssets();

            WirePlaces(new[] { forestPlace, winterPlace, blightPlace });

            Debug.Log("[WinterBiomePass] 8 wajah + 3 tempat siap. Act 1 hutan, act 2 salju, " +
                      "act 3 ternoda, act 4 membungkus kembali.");
        }

        // =====================================================================================
        //  penurunan satu wajah
        // =====================================================================================

        delegate void PaletteFn(BiomeDefinition face);
        delegate void LightFn(BiomeDefinition face, int index);

        static BiomeDefinition DeriveFace(BiomeDefinition source, string id, int index,
            TerrainLayer[] layers, string lookFolder, Color tintTarget, float tintAmount,
            PaletteFn palette, LightFn light, WeatherMood[] moods)
        {
            string suffix = index == 0 ? "" : index == 1 ? "_night" : index == 2 ? "_sore" : "_midnight";
            string path = BiomeFolder + "/Biome_" + id + suffix + ".asset";

            var face = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(path);

            if (face == null)
            {
                face = ScriptableObject.CreateInstance<BiomeDefinition>();
                AssetDatabase.CreateAsset(face, path);
            }

            // Daftar cuaca & suasana yang SUDAH disetel tangan diselamatkan sebelum CopySerialized
            // menimpa semuanya — pelajaran yang sama yang menghilangkan kunang-kunang malam.
            var keptVfx = face.AmbientVfx;
            var keptMoods = face.WeatherMoods;
            bool hadMoods = keptMoods != null && keptMoods.Length > 0;
            bool hadVfx = keptVfx != null && keptVfx.Length > 0;

            EditorUtility.CopySerialized(source, face);
            face.name = System.IO.Path.GetFileNameWithoutExtension(path);

            face.Id = id + suffix;
            face.DisplayName = index == 0 ? id.ToUpperInvariant()
                : source.DisplayName;   // MALAM/SENJA/TENGAH MALAM sudah benar dari sumbernya

            face.GroundLayers = layers;

            // Suasana hutan (kupu-kupu, kunang-kunang) tidak ikut: salju dan noda punya udara
            // yang berbeda, dan kupu-kupu di badai salju terbaca sebagai bug, bukan puitis.
            face.AmbientVfx = hadVfx ? keptVfx : new AmbientVfxEntry[0];

            face.WeatherMoods = hadMoods ? keptMoods
                : moods != null ? moods
                : face.WeatherMoods;   // blight: warisan hujan hutan dari CopySerialized, dan cocok

            palette(face);
            light(face, index);

            // Pohon dan semak memakai MESH yang sama dengan hutan — yang berganti materialnya,
            // dikloning dengan semu warna musim. Mesh yang sama berarti siluet lapangan tidak
            // berubah antar act, dan yang dibaca pemain tetap terbaca.
            face.MeshTrees = Reskin(source.MeshTrees, lookFolder, tintTarget, tintAmount, id);
            face.MeshScatter = Reskin(source.MeshScatter, lookFolder, tintTarget, tintAmount, id);
            face.MeshGrass = Reskin(source.MeshGrass, lookFolder, tintTarget, tintAmount, id);

            EditorUtility.SetDirty(face);
            return face;
        }

        // =====================================================================================
        //  palet per tempat
        // =====================================================================================

        static void WinterPalette(BiomeDefinition face)
        {
            face.GroundColor = new Color(0.72f, 0.78f, 0.85f);

            face.TrunkColors = new[]
            {
                new Color(0.14f, 0.11f, 0.1f),
                new Color(0.19f, 0.15f, 0.13f),
            };

            // Cemara berselimut salju: hijau yang nyaris kalah oleh putihnya.
            face.CanopyColors = new[]
            {
                new Color(0.55f, 0.66f, 0.6f),
                new Color(0.68f, 0.78f, 0.72f),
                new Color(0.8f, 0.86f, 0.84f),
                new Color(0.45f, 0.58f, 0.5f),
            };

            face.ScatterColors = new[]
            {
                new Color(0.75f, 0.8f, 0.85f),
                new Color(0.62f, 0.68f, 0.73f),
                new Color(0.5f, 0.55f, 0.6f),
            };
        }

        static void BlightPalette(BiomeDefinition face)
        {
            face.GroundColor = new Color(0.24f, 0.16f, 0.26f);

            face.TrunkColors = new[]
            {
                new Color(0.12f, 0.09f, 0.14f),
                new Color(0.17f, 0.12f, 0.18f),
            };

            face.CanopyColors = new[]
            {
                new Color(0.4f, 0.16f, 0.38f),
                new Color(0.5f, 0.2f, 0.42f),
                new Color(0.32f, 0.12f, 0.3f),
                new Color(0.58f, 0.24f, 0.46f),
            };

            face.ScatterColors = new[]
            {
                new Color(0.3f, 0.18f, 0.32f),
                new Color(0.38f, 0.22f, 0.36f),
                new Color(0.24f, 0.2f, 0.26f),
            };
        }

        // =====================================================================================
        //  cahaya per wajah
        // =====================================================================================

        static void WinterLight(BiomeDefinition face, int index)
        {
            switch (index)
            {
                case 0:   // siang: putih menyilaukan, salju memantulkan segalanya
                    face.SunColor = new Color(1f, 0.96f, 0.9f);
                    face.SunIntensity = 1.2f;
                    face.SunPitch = 36f;
                    face.AmbientSky = new Color(0.66f, 0.74f, 0.86f);
                    face.AmbientEquator = new Color(0.58f, 0.63f, 0.72f);
                    face.AmbientGround = new Color(0.52f, 0.57f, 0.66f);
                    face.FogColor = new Color(0.82f, 0.87f, 0.95f);
                    face.FogStart = 45f; face.FogEnd = 135f;
                    face.HorizonColor = new Color(0.6f, 0.7f, 0.82f);
                    break;

                case 1:   // malam: salju memantulkan bulan — lebih terang dari malam hutan
                    face.AmbientSky = new Color(0.22f, 0.3f, 0.55f);
                    face.AmbientEquator = new Color(0.16f, 0.22f, 0.42f);
                    face.AmbientGround = new Color(0.12f, 0.16f, 0.28f);
                    face.FogColor = new Color(0.14f, 0.2f, 0.38f);
                    face.FogStart = 25f; face.FogEnd = 115f;
                    break;

                case 2:   // senja: emas-jambu di atas putih
                    face.SunColor = new Color(1f, 0.62f, 0.42f);
                    face.SunIntensity = 1f;
                    face.SunPitch = 14f;
                    face.AmbientSky = new Color(0.5f, 0.44f, 0.56f);
                    face.AmbientEquator = new Color(0.46f, 0.36f, 0.46f);
                    face.AmbientGround = new Color(0.4f, 0.34f, 0.42f);
                    face.FogColor = new Color(0.66f, 0.5f, 0.55f);
                    face.FogStart = 35f; face.FogEnd = 120f;
                    break;

                default:  // tengah malam: biru terdalam, masih lebih terang dari hutan
                    face.AmbientSky = new Color(0.14f, 0.19f, 0.38f);
                    face.AmbientEquator = new Color(0.1f, 0.14f, 0.28f);
                    face.AmbientGround = new Color(0.06f, 0.09f, 0.18f);
                    face.FogColor = new Color(0.09f, 0.13f, 0.26f);
                    face.FogStart = 20f; face.FogEnd = 100f;
                    break;
            }
        }

        static void BlightLight(BiomeDefinition face, int index)
        {
            switch (index)
            {
                case 0:
                    face.SunColor = new Color(0.95f, 0.7f, 0.8f);
                    face.SunIntensity = 1.05f;
                    face.SunPitch = 30f;
                    face.AmbientSky = new Color(0.45f, 0.32f, 0.5f);
                    face.AmbientEquator = new Color(0.4f, 0.28f, 0.42f);
                    face.AmbientGround = new Color(0.28f, 0.2f, 0.3f);
                    face.FogColor = new Color(0.5f, 0.35f, 0.5f);
                    face.FogStart = 35f; face.FogEnd = 120f;
                    face.HorizonColor = new Color(0.3f, 0.2f, 0.32f);

                    // Cubemap siang hutan di atas rimba ungu adalah satu-satunya benda yang masih
                    // hutan — alasan yang sama yang mencopot skybox dari malam.
                    face.Skybox = null;
                    break;

                case 1:
                    face.SunColor = new Color(0.7f, 0.5f, 0.9f);
                    face.SunIntensity = 0.6f;
                    face.AmbientSky = new Color(0.2f, 0.12f, 0.3f);
                    face.AmbientEquator = new Color(0.15f, 0.09f, 0.24f);
                    face.AmbientGround = new Color(0.08f, 0.05f, 0.14f);
                    face.FogColor = new Color(0.16f, 0.09f, 0.22f);
                    face.FogStart = 22f; face.FogEnd = 100f;
                    break;

                case 2:
                    face.SunColor = new Color(1f, 0.5f, 0.4f);
                    face.SunIntensity = 0.95f;
                    face.SunPitch = 14f;
                    face.AmbientSky = new Color(0.42f, 0.26f, 0.38f);
                    face.AmbientEquator = new Color(0.36f, 0.22f, 0.34f);
                    face.AmbientGround = new Color(0.26f, 0.16f, 0.26f);
                    face.FogColor = new Color(0.45f, 0.26f, 0.36f);
                    face.FogStart = 30f; face.FogEnd = 115f;
                    face.Skybox = null;
                    break;

                default:
                    face.AmbientSky = new Color(0.13f, 0.08f, 0.22f);
                    face.AmbientEquator = new Color(0.09f, 0.06f, 0.17f);
                    face.AmbientGround = new Color(0.05f, 0.03f, 0.1f);
                    face.FogColor = new Color(0.1f, 0.06f, 0.16f);
                    face.FogStart = 18f; face.FogEnd = 95f;
                    break;
            }
        }

        // =====================================================================================
        //  cuaca salju
        // =====================================================================================

        static WeatherMood[] SnowMoods()
        {
            return new[]
            {
                new WeatherMood { Name = "Cerah", Weight = 1.2f },

                new WeatherMood
                {
                    Name = "Salju tipis", Weight = 1f, Speed = 0.9f, Overcast = 0.15f,
                    Effects = new[] { Fx("Snow/Snow_calm", 2.4f, 1.1f) },
                },

                new WeatherMood
                {
                    Name = "Salju", Weight = 0.8f, Overcast = 0.35f,
                    Effects = new[] { Fx("Snow/Snow_average", 2.6f, 1.1f) },
                },

                // Badai memakai DUA sistem: butiran yang jatuh dan tirai yang menyapu menyamping.
                // Salju lebat tanpa arah angin cuma hujan yang putih.
                new WeatherMood
                {
                    Name = "Badai salju", Weight = 0.45f, Speed = 1.1f, Overcast = 0.6f,
                    Effects = new[]
                    {
                        Fx("Snow/Snow_heavy", 3f, 1.2f),
                        Fx("Snow/Snowstorm_linear", 2.6f, 1f),
                    },
                },
            };
        }

        static AmbientVfxEntry Fx(string name, float scale, float grain)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Vfxs + name + ".prefab");
            if (prefab == null) Debug.LogError("[WinterBiomePass] VFX tidak ketemu: " + name);

            return new AmbientVfxEntry
            {
                Prefab = prefab,
                Scale = scale,
                Grain = grain,
                CoverageOnly = true,
            };
        }

        // =====================================================================================
        //  material musim
        // =====================================================================================

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>
        /// Salinan daftar prop dengan material yang dicelup musim. Mesh tidak disentuh.
        ///
        /// Material klonnya ASET yang dipakai ulang antar wajah dan antar regenerasi — empat
        /// wajah musim dingin memakai SATU set material salju, bukan empat, karena batching
        /// instanced menghitung per material dan empat salinan identik berarti empat kali batch
        /// untuk gambar yang sama persis.
        /// </summary>
        static MeshProp[] Reskin(MeshProp[] source, string folder, Color target, float amount,
            string suffix)
        {
            if (source == null || source.Length == 0) return source;

            var result = new MeshProp[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                var s = source[i];

                if (s == null || !s.Valid) { result[i] = s; continue; }

                var mats = new Material[s.Materials.Length];

                for (int m = 0; m < mats.Length; m++)
                {
                    mats[m] = SeasonMaterial(s.Materials[m], folder, target, amount, suffix);
                }

                result[i] = new MeshProp
                {
                    Name = s.Name + " (" + suffix + ")",
                    Mesh = s.Mesh,
                    Materials = mats,
                    Weight = s.Weight,
                    SizeMultiplier = s.SizeMultiplier,
                };
            }

            return result;
        }

        static Material SeasonMaterial(Material source, string folder, Color target, float amount,
            string suffix)
        {
            if (source == null) return null;

            string path = folder + "/" + source.name + "_" + suffix + ".mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var clone = new Material(source) { name = System.IO.Path.GetFileNameWithoutExtension(path) };

            int id = clone.HasProperty(BaseColorId) ? BaseColorId
                   : clone.HasProperty(ColorId) ? ColorId : -1;

            if (id != -1)
            {
                clone.SetColor(id, Color.Lerp(clone.GetColor(id), target, amount));
            }

            AssetDatabase.CreateAsset(clone, path);
            return clone;
        }

        // =====================================================================================
        //  tempat & wiring
        // =====================================================================================

        static BiomeDefinition[] LoadFaces(string[] names)
        {
            var faces = new BiomeDefinition[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                faces[i] = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(
                    BiomeFolder + "/" + names[i] + ".asset");
            }

            return faces;
        }

        static BiomePlace Place(string assetName, string display, BiomeDefinition[] faces)
        {
            string path = BiomeFolder + "/" + assetName + ".asset";

            var place = AssetDatabase.LoadAssetAtPath<BiomePlace>(path);

            if (place == null)
            {
                place = ScriptableObject.CreateInstance<BiomePlace>();
                AssetDatabase.CreateAsset(place, path);
            }

            place.name = assetName;
            place.Id = assetName.ToLowerInvariant().Replace("place_", "");
            place.DisplayName = display;
            place.Faces = faces;

            EditorUtility.SetDirty(place);
            return place;
        }

        /// <summary>
        /// Memasang daftar tempat ke ProtoBootstrap di KEDUA scene yang memakainya. Scene yang
        /// sedang terbuka dikembalikan sesudahnya — pass yang meninggalkan editor di scene lain
        /// terbaca sebagai editor yang rusak, bukan sebagai pass yang selesai.
        /// </summary>
        static void WirePlaces(BiomePlace[] places)
        {
            string[] scenes = { "Assets/Scenes/Proto.unity", "Assets/Scenes/Stage.unity" };
            string restore = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

            foreach (var scenePath in scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) continue;

                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath,
                    UnityEditor.SceneManagement.OpenSceneMode.Single);

                var boot = Object.FindFirstObjectByType<ProtoBootstrap>();

                if (boot == null)
                {
                    Debug.LogWarning("[WinterBiomePass] " + scenePath + " tanpa ProtoBootstrap — dilewati.");
                    continue;
                }

                var so = new SerializedObject(boot);
                var list = so.FindProperty("_places");
                list.arraySize = places.Length;

                for (int i = 0; i < places.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).objectReferenceValue = places[i];
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

                Debug.Log("[WinterBiomePass] _places terpasang di " + scenePath);
            }

            if (!string.IsNullOrEmpty(restore) && restore != UnityEngine.SceneManagement.SceneManager.GetActiveScene().path)
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(restore,
                    UnityEditor.SceneManagement.OpenSceneMode.Single);
            }
        }

        // =====================================================================================
        //  kecil-kecil
        // =====================================================================================

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                AssetDatabase.CreateFolder(parent, child);
        }

        static TerrainLayer Layer(string name, string texturePath, float tile)
        {
            string path = TerrainFolder + "/" + name + ".terrainlayer";

            var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (existing != null) return existing;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

            if (texture == null)
            {
                Debug.LogError("[WinterBiomePass] tekstur tidak ketemu: " + texturePath);
                return null;
            }

            var layer = new TerrainLayer
            {
                name = name,
                diffuseTexture = texture,
                tileSize = Vector2.one * tile,
                tileOffset = Vector2.zero,
            };

            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }
    }
}
