using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Efek partikel untuk sembilan REAKSI — separuh damage sebuah build ailment lahir dari
    /// sini, dan sampai sekarang semuanya cuma bola primitif yang mengembang.
    ///
    /// Pola persis <see cref="VfxPass"/>: satu folder + satu prefab wrapper per reaksi di
    /// <c>Art/VFX/Reactions/&lt;Nama&gt;/</c>, aset reaksi menunjuk wrapper, dan wrapper yang
    /// sudah ada TIDAK PERNAH dibangun ulang — menukar efek jelek = buka prefabnya, hapus
    /// anaknya, seret prefab paket lain ke dalamnya.
    ///
    /// Pemilihannya mengikuti BAHAN reaksinya, bukan warnanya: yang lahir dari es memakai
    /// keluarga es, dari racun memakai racun, dari darah memakai cipratan. Pemain harus bisa
    /// menebak apa yang barusan meledak tanpa membaca teks.
    /// </summary>
    public static class ReactionVfxPass
    {
        const string ReactionFolder = "Assets/GameData/Reactions";
        const string VfxRoot = "Assets/Art/VFX/Reactions";

        const string Ga = "Assets/Art/VFX/Packs/GabrielAguiarProductions/UniqueMagicAbilitiesVol_2/Prefabs/";
        const string Cfxr = "Assets/Art/VFX/Packs/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/";

        /// <summary>(nama file aset, prefab paket default, VfxScale).</summary>
        static readonly (string asset, string path, float scale)[] Map =
        {
            // BLEED + SHOCK — darah menyembur.
            ("Reaction_arusdarah", Cfxr + "Liquids/CFXR2 Blood Shape Splash.prefab", 0.9f),

            // DRAG + BURN — badai api. Radius terbesar di daftar (4,2), jadi yang paling ramai.
            ("Reaction_badaiapi", Cfxr + "Explosions/CFXR3 Fire Explosion B.prefab", 1f),

            // BLEED + BURN — luka yang terbakar: percikan api kecil, bukan ledakan.
            ("Reaction_bakarluka", Cfxr + "Fire/CFXR3 Hit Fire B (Air).prefab", 0.9f),

            // CHILL + BLEED — es retak, serpihan jatuh.
            ("Reaction_bekuretak", Cfxr + "Ice/CFXR3 Ice Debris Hit (Lit).prefab", 0.9f),

            // CHILL + SHOCK — yang dipilih listriknya, karena esnya sudah diceritakan warna kilat.
            ("Reaction_bekustatis", Cfxr + "Electric/CFXR3 Hit Electric B (Air).prefab", 0.9f),

            // POISON + BURN — letusan racun.
            ("Reaction_ledakracun", Ga + "vfx_ImpactAoE04_Poison.prefab", 0.7f),

            // BLEED + POISON — nanah: awan busuk bertengkorak, bukan letusan bersih.
            ("Reaction_nanah", Cfxr + "Eerie/CFXR2 Poison Cloud + Skulls.prefab", 0.8f),

            // BURN + CHILL — PECAH. Hantaman es di tanah, paling keras bunyinya secara visual.
            ("Reaction_pecah", Cfxr + "Ice/CFXR3 Hit Ice A (Ground).prefab", 1f),

            // DRAG + CHILL — satu-satunya reaksi yang menyeret, jadi ia dapat bentuk berputar.
            ("Reaction_pusaranbeku", Ga + "vfx_ImpactAoE02_Ice.prefab", 0.8f),
        };

        [MenuItem("Tools/Grimoire/Assign Reaction VFX")]
        public static void Run()
        {
            int built = 0, repointed = 0, kept = 0;
            var problems = new List<string>();

            foreach (var (assetName, path, scale) in Map)
            {
                var rx = AssetDatabase.LoadAssetAtPath<ReactionDefinition>(
                    $"{ReactionFolder}/{assetName}.asset");
                if (rx == null)
                {
                    problems.Add($"reaksi hilang: {assetName}");
                    continue;
                }

                string folder = Sanitize(rx.DisplayName);
                string wrapperPath = $"{VfxRoot}/{folder}/Vfx_{folder}.prefab";

                var wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(wrapperPath);
                if (wrapper == null)
                {
                    wrapper = BuildWrapper(wrapperPath, folder, path, problems);
                    if (wrapper == null) continue;
                    built++;
                }

                if (rx.Vfx == wrapper && Mathf.Approximately(rx.VfxScale, scale))
                {
                    kept++;
                    continue;
                }

                rx.Vfx = wrapper;
                rx.VfxScale = scale;
                EditorUtility.SetDirty(rx);
                repointed++;
            }

            AssetDatabase.SaveAssets();
            Audit(problems);

            Debug.Log($"[ReactionVfxPass] wrapper dibangun {built}, pointer dibetulkan {repointed}, " +
                      $"sudah benar {kept}, masalah {problems.Count}." +
                      (problems.Count > 0 ? "\n - " + string.Join("\n - ", problems) : ""));
        }

        static GameObject BuildWrapper(string wrapperPath, string folder, string packPath,
            List<string> problems)
        {
            var pack = AssetDatabase.LoadAssetAtPath<GameObject>(packPath);
            if (pack == null)
            {
                problems.Add($"prefab paket hilang: {packPath}");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(VfxRoot))
            {
                AssetDatabase.CreateFolder("Assets/Art/VFX", "Reactions");
            }

            string dir = $"{VfxRoot}/{folder}";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(VfxRoot, folder);

            var root = new GameObject("Vfx_" + folder);
            try
            {
                // InstantiatePrefab, bukan Instantiate: hanya yang pertama menjaga tautan
                // nested-prefab, dan tautan itulah yang membuat isinya bisa diganti seret-lepas.
                var child = (GameObject)PrefabUtility.InstantiatePrefab(pack);
                child.transform.SetParent(root.transform, false);

                return PrefabUtility.SaveAsPrefabAsset(root, wrapperPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static string Sanitize(string name)
        {
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(System.Array.IndexOf(System.IO.Path.GetInvalidFileNameChars(), c) >= 0
                    ? '_' : c);
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Reaksi yang masih polos, dan efek yang LOOP — reaksi itu letusan sekejap, dan prefab
        /// loop di sini akan menyala selamanya di titik ledakan.
        /// </summary>
        static void Audit(List<string> problems)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ReactionDefinition", new[] { ReactionFolder }))
            {
                var rx = AssetDatabase.LoadAssetAtPath<ReactionDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (rx == null) continue;

                if (rx.Vfx == null)
                {
                    problems.Add($"masih polos: {rx.name} ({rx.DisplayName})");
                    continue;
                }

                foreach (var ps in rx.Vfx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (!ps.main.loop) continue;
                    problems.Add($"efek LOOP di reaksi sekejap: {rx.name} <- {rx.Vfx.name}");
                    break;
                }
            }
        }
    }
}
