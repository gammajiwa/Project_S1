using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Menjadikan sepuluh benda FX yang dulu lahir dari <c>CreatePrimitive</c> sebagai PREFAB
    /// di folder masing-masing, lalu mendaftarkannya ke <see cref="FxLibrary"/>.
    ///
    /// Permintaan pemilik project: "setiap object, mau itu AOE, skill, meteor, dll — buat jadi
    /// prefab, folderin yang rapi, nanti gw QA." Selama benda-benda ini lahir dari kode, tidak
    /// ada yang bisa dibuka atau diseret; satu-satunya cara mengubahnya menyunting C#.
    ///
    /// Prefab bawaannya SAMA PERSIS dengan primitif lama (mesh, material, skala) — memasangnya
    /// tidak boleh mengubah apa pun sampai isinya benar-benar diganti tangan. Idempotent:
    /// prefab yang sudah ada tidak pernah dibangun ulang.
    /// </summary>
    public static class CoreFxPass
    {
        const string Root = "Assets/Art/VFX/Core";
        const string LibraryPath = "Assets/GameData/FxLibrary.asset";

        /// <summary>(nama slot di FxLibrary, nama folder, bentuk, skala, material AOE?).</summary>
        static readonly (string field, string folder, PrimitiveType shape, float scale, bool aoe)[] Items =
        {
            ("ProjectileCore",  "Inti Peluru",        PrimitiveType.Sphere,   0.45f, false),
            ("ImpactFlash",     "Kilatan Tumbukan",   PrimitiveType.Sphere,   0.2f,  false),
            ("Descent",         "Meteor Jatuh",       PrimitiveType.Sphere,   0.85f, false),
            ("ZoneDisc",        "Cakram Kubangan",    PrimitiveType.Cylinder, 1f,    true),
            ("StrikeRing",      "Telegraf Hantaman",  PrimitiveType.Cylinder, 1f,    true),
            ("Orb",             "Pecahan Mengambang", PrimitiveType.Sphere,   0.34f, false),
            ("Boulder",         "Bola Menggelinding", PrimitiveType.Sphere,   1f,    false),
            ("Twister",         "Puting Beliung",     PrimitiveType.Cylinder, 1f,    false),
            ("DropPickup",      "Barang Jatuh",       PrimitiveType.Cube,     0.42f, false),
            ("IslandGuardian",  "Penjaga Pulau",      PrimitiveType.Capsule,  1f,    false),
        };

        [MenuItem("Tools/Grimoire/Build Core FX")]
        public static void Run()
        {
            var library = AssetDatabase.LoadAssetAtPath<FxLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<FxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/Art/VFX", "Core");

            int built = 0, wired = 0, kept = 0;
            var problems = new List<string>();
            var so = new SerializedObject(library);

            foreach (var (field, folder, shape, scale, aoe) in Items)
            {
                string path = $"{Root}/{folder}/Vfx_{Slug(folder)}.prefab";

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    prefab = Build(path, folder, shape, scale, aoe, problems);
                    if (prefab == null) continue;
                    built++;
                }

                var prop = so.FindProperty(field);
                if (prop == null)
                {
                    problems.Add($"slot tidak ada di FxLibrary: {field}");
                    continue;
                }

                if (prop.objectReferenceValue == prefab)
                {
                    kept++;
                    continue;
                }

                prop.objectReferenceValue = prefab;
                wired++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CoreFxPass] prefab dibangun {built}, slot disambung {wired}, " +
                      $"sudah benar {kept}, masalah {problems.Count}." +
                      (problems.Count > 0 ? "\n - " + string.Join("\n - ", problems) : "") +
                      $"\nAset: {LibraryPath} — pasang ke _Bootstrap kalau belum.");
        }

        static GameObject Build(string path, string folder, PrimitiveType shape, float scale,
            bool aoe, List<string> problems)
        {
            string dir = $"{Root}/{folder}";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Root, folder);

            var go = GameObject.CreatePrimitive(shape);

            try
            {
                go.name = "Vfx_" + Slug(folder);
                go.transform.localScale = Vector3.one * scale;

                // Collider dicabut: benda-benda ini tidak pernah ditabrak, dan collider yang
                // ikut terbawa ke dalam prefab akan menyapu fisika tiap kali salah satunya lahir.
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);

                var r = go.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;

                // Material DISIMPAN sebagai aset di folder yang sama, bukan dibuat runtime:
                // prefab yang menunjuk material sementara akan tampil magenta begitu dibuka
                // di luar play mode, dan yang mau di-QA justru tampilannya.
                var mat = MaterialFor(dir, aoe, problems);
                if (mat != null) r.sharedMaterial = mat;

                return PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static Material MaterialFor(string dir, bool aoe, List<string> problems)
        {
            string name = aoe ? "AoeMarker" : "FxUnlit";
            string path = $"{dir}/{name}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = aoe
                ? Shader.Find("Grimoire/AoeRing")
                : Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
            {
                problems.Add($"shader untuk {name} tidak ketemu");
                return null;
            }

            var mat = new Material(shader) { enableInstancing = !aoe };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static string Slug(string folder) => folder.Replace(" ", "");
    }
}
