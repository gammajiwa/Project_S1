using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Memanggang animasi skinned mesh jadi TEKSTUR, supaya gerombolan musuh bisa bergerak
    /// tanpa satu pun Animator.
    ///
    /// Musuh di game ini tidak punya GameObject — seluruh gerombolan keluar sebagai beberapa
    /// panggilan instanced (lihat <see cref="EnemyRenderer"/>). Instancing menutup pintu untuk
    /// Animator dan SkinnedMeshRenderer sekaligus, karena keduanya menuntut satu objek per
    /// musuh. Memanggang memindahkan seluruh animasinya ke dalam tekstur yang bisa dibaca
    /// ribuan instance sekaligus dari satu material.
    ///
    /// Cara pakai: pilih prefab (atau FBX) berisi SkinnedMeshRenderer di Project window, lalu
    /// <b>Tools/Grimoire/Bake Enemy VAT</b>. Klip dicari otomatis di folder yang sama.
    /// </summary>
    public static class VatBaker
    {
        /// <summary>
        /// Frame per detik panggangan. Tiga puluh sudah di atas ambang mata untuk musuh
        /// setinggi belasan piksel di layar, dan menaikkannya melipatgandakan ukuran tekstur
        /// tanpa satu pun frame yang benar-benar terlihat baru.
        /// </summary>
        const int Fps = 30;

        [MenuItem("Tools/Grimoire/Bake Enemy VAT")]
        static void BakeSelection()
        {
            var source = Selection.activeObject as GameObject;

            if (source == null)
            {
                EditorUtility.DisplayDialog("Bake VAT",
                    "Pilih dulu prefab atau FBX yang punya SkinnedMeshRenderer di Project window.",
                    "Oke");
                return;
            }

            Bake(source);
        }

        [MenuItem("Tools/Grimoire/Bake Enemy VAT", true)]
        static bool BakeSelectionValid() => Selection.activeObject is GameObject;

        public static VatClipSet Bake(GameObject source)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string folder = System.IO.Path.GetDirectoryName(sourcePath).Replace('\\', '/');

            var clips = CollectClips(folder);

            // Paket aset hampir selalu memisah prefab dan animasinya ke folder bersaudara
            // (Prefab/ di sebelah FBX/). Mencari di folder prefabnya saja akan selalu pulang
            // dengan tangan kosong, jadi kalau kosong kita naik satu tingkat — dan hasil
            // panggangannya ikut ditulis di sana, bukan bersarang di dalam Prefab/.
            if (clips.Count == 0)
            {
                string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');

                if (!string.IsNullOrEmpty(parent) && parent.StartsWith("Assets"))
                {
                    var wider = CollectClips(parent);

                    if (wider.Count > 0)
                    {
                        clips = wider;
                        folder = parent;
                    }
                }
            }

            if (clips.Count == 0)
            {
                Debug.LogError("[VatBaker] tidak ada AnimationClip di " + folder +
                               " maupun folder induknya — panggangan dibatalkan.");
                return null;
            }

            // Dikerjakan di atas SALINAN di scene, bukan di asetnya. SampleAnimation menulis
            // langsung ke transform yang diberikan, dan menulis ke prefab aset akan mengubah
            // pose tersimpannya secara permanen.
            var rig = Object.Instantiate(source);
            rig.hideFlags = HideFlags.HideAndDontSave;

            var smr = rig.GetComponentInChildren<SkinnedMeshRenderer>(true);

            if (smr == null)
            {
                Object.DestroyImmediate(rig);
                Debug.LogError("[VatBaker] " + source.name + " tidak punya SkinnedMeshRenderer.");
                return null;
            }

            // Animator dimatikan: kalau ia hidup, ia menimpa pose yang barusan disampling
            // sebelum BakeMesh sempat membacanya, dan seluruh tekstur keluar berisi pose diam.
            foreach (var a in rig.GetComponentsInChildren<Animator>(true)) a.enabled = false;

            var result = BakeRig(source, rig, smr, clips, folder);
            Object.DestroyImmediate(rig);
            return result;
        }

        static VatClipSet BakeRig(GameObject source, GameObject rig, SkinnedMeshRenderer smr,
            List<AnimationClip> clips, string folder)
        {
            var probe = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            smr.BakeMesh(probe, true);

            int verts = probe.vertexCount;

            // Berapa baris yang dibutuhkan seluruh klip, dihitung dulu supaya teksturnya
            // dialokasikan sekali.
            var plan = new List<VatClip>(clips.Count);
            int rows = 0;

            for (int i = 0; i < clips.Count; i++)
            {
                int frames = Mathf.Max(2, Mathf.RoundToInt(clips[i].length * Fps));

                plan.Add(new VatClip
                {
                    Role = RoleOf(clips[i].name),
                    SourceName = clips[i].name,
                    FirstRow = rows,
                    Rows = frames,
                    Seconds = clips[i].length,
                });

                rows += frames;
            }

            // RGBAHalf, dan posisinya disimpan APA ADANYA tanpa dinormalkan ke kotak pembatas.
            // Half punya presisi sekitar seperseribu unit pada jangkauan beberapa unit — jauh
            // di bawah satu piksel di layar — dan menyimpan mentah menghapus seluruh kelas bug
            // "membongkarnya dengan kotak pembatas yang salah".
            var tex = new Texture2D(verts, rows, TextureFormat.RGBAHalf, false, true)
            {
                name = source.name + "_vat",
                filterMode = FilterMode.Point,   // tiap texel mendatar = vertex LAIN
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
            };

            var pixels = new Color[verts * rows];
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var buffer = new List<Vector3>(verts);

            for (int c = 0; c < clips.Count; c++)
            {
                var clip = clips[c];
                var slot = plan[c];

                for (int f = 0; f < slot.Rows; f++)
                {
                    // Dibagi Rows, bukan (Rows - 1): frame terakhir tidak boleh menduplikat
                    // frame pertama, kalau tidak animasi berputarnya tersendat satu frame
                    // tiap kali mengulang.
                    float t = clip.length * (f / (float)slot.Rows);

                    clip.SampleAnimation(rig, t);
                    smr.BakeMesh(probe, true);
                    probe.GetVertices(buffer);

                    int row = (slot.FirstRow + f) * verts;

                    for (int v = 0; v < verts; v++)
                    {
                        var p = buffer[v];
                        pixels[row + v] = new Color(p.x, p.y, p.z, 1f);
                        min = Vector3.Min(min, p);
                        max = Vector3.Max(max, p);
                    }
                }

                EditorUtility.DisplayProgressBar("Bake VAT", clip.name, (c + 1f) / clips.Count);
            }

            EditorUtility.ClearProgressBar();

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            var mesh = BuildStaticMesh(probe, verts, min, max, source.name);

            Object.DestroyImmediate(probe);

            return WriteAssets(source, folder, tex, mesh, smr.sharedMaterial, plan, verts, rows,
                min, max);
        }

        /// <summary>
        /// Mesh yang benar-benar digambar: pose netral, ditambah satu koordinat UV yang memberi
        /// tahu shader vertex ini ada di kolom mana pada tekstur.
        /// </summary>
        static Mesh BuildStaticMesh(Mesh probe, int verts, Vector3 min, Vector3 max, string label)
        {
            var mesh = Object.Instantiate(probe);
            mesh.name = label + "_vatMesh";

            var lookup = new List<Vector2>(verts);

            // Setengah texel: sampling tepat di batas texel dengan filter Point bisa jatuh ke
            // kolom sebelah karena pembulatan, dan kolom sebelah itu vertex yang sama sekali
            // lain — hasilnya satu vertex nyasar yang menarik segitiga melintasi model.
            for (int v = 0; v < verts; v++) lookup.Add(new Vector2((v + 0.5f) / verts, 0f));

            mesh.SetUVs(2, lookup);

            // Kotak pembatas dipaksa selebar SELURUH gerakan. Bounds pose netral akan membuat
            // Unity membuang musuh dari layar tepat saat ia mengayunkan tangan keluar kotak.
            mesh.bounds = new Bounds((min + max) * 0.5f, max - min);

            // Tulang dan bobotnya dibuang: mesh ini tidak akan pernah di-skin lagi, dan
            // membawanya berarti membayar memori untuk data yang tidak dibaca siapa pun.
            mesh.boneWeights = new BoneWeight[0];
            mesh.bindposes = new Matrix4x4[0];

            return mesh;
        }

        static VatClipSet WriteAssets(GameObject source, string folder, Texture2D tex, Mesh mesh,
            Material sourceMaterial, List<VatClip> plan, int verts, int rows,
            Vector3 min, Vector3 max)
        {
            string dir = folder + "/VAT";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(folder, "VAT");

            string setPath = dir + "/" + source.name + "_vat.asset";

            var set = AssetDatabase.LoadAssetAtPath<VatClipSet>(setPath);
            bool fresh = set == null;
            if (fresh) set = ScriptableObject.CreateInstance<VatClipSet>();

            set.Mesh = mesh;
            set.Positions = tex;
            set.SourceMaterial = sourceMaterial;
            set.VertexCount = verts;
            set.TotalRows = rows;
            set.BoundsMin = min;
            set.BoundsMax = max;
            set.Height = max.y - min.y;
            set.Clips = plan.ToArray();

            if (fresh) AssetDatabase.CreateAsset(set, setPath);

            // Tekstur dan mesh dititipkan DI DALAM aset yang sama, bukan jadi file terpisah:
            // ketiganya cuma berarti kalau bersama, dan memisahkannya membuka jalan bagi
            // tekstur yang tertinggal saat mesh dipanggang ulang.
            foreach (var old in AssetDatabase.LoadAllAssetsAtPath(setPath))
            {
                if (old == set) continue;
                Object.DestroyImmediate(old, true);
            }

            AssetDatabase.AddObjectToAsset(tex, set);
            AssetDatabase.AddObjectToAsset(mesh, set);

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(setPath);

            var report = new System.Text.StringBuilder();
            report.Append("[VatBaker] ").Append(source.name).Append(": ")
                  .Append(verts).Append(" vertex x ").Append(rows).Append(" frame = ")
                  .Append((verts * rows * 8 / 1024f / 1024f).ToString("F2")).Append(" MB\n");

            for (int i = 0; i < plan.Count; i++)
                report.Append("   \"").Append(plan[i].SourceName).Append("\" -> ")
                      .Append(plan[i].Role).Append("  baris ").Append(plan[i].FirstRow)
                      .Append("..").Append(plan[i].FirstRow + plan[i].Rows - 1).Append('\n');

            Debug.Log(report.ToString(), set);
            return set;
        }

        static List<AnimationClip> CollectClips(string folder)
        {
            var found = new List<AnimationClip>();
            var seen = new HashSet<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var clip = o as AnimationClip;
                    if (clip == null) continue;

                    // Klip pratinjau yang dibuat editor ikut terjaring FindAssets dan isinya
                    // bukan animasi sungguhan.
                    if (clip.name.StartsWith("__preview")) continue;
                    if (!seen.Add(clip.name)) continue;

                    found.Add(clip);
                }
            }

            found.Sort((a, b) => RoleOf(a.name).CompareTo(RoleOf(b.name)));
            return found;
        }

        /// <summary>
        /// Menebak peran dari nama klip.
        ///
        /// Ini satu-satunya tempat nama buatan orang lain bertemu kosakata kita. Aset yang
        /// dibeli menamai animasinya sesuka pembuatnya — <c>IDLE</c>, <c>Anim_Idle_01</c>,
        /// <c>Armature|walk</c>, <c>sprint_fwd</c> — dan tebakannya sengaja longgar. Yang salah
        /// tebak tinggal dibetulkan tangan di <see cref="VatClipSet.Clips"/> setelah memanggang;
        /// yang penting tidak ada klip yang hilang diam-diam.
        /// </summary>
        static VatRole RoleOf(string name)
        {
            string n = name.ToLowerInvariant();

            // Lari diperiksa DULU: "run" juga muncul di dalam kata lain, tapi klip yang
            // namanya mengandung sprint/run praktis selalu klip lari.
            if (n.Contains("run") || n.Contains("sprint") || n.Contains("jog")) return VatRole.Run;
            if (n.Contains("walk") || n.Contains("move")) return VatRole.Walk;

            // Sisanya jatuh ke diam, termasuk nama yang tidak dikenali sama sekali. Diam adalah
            // tebakan paling aman: musuh yang berdiri diam terlihat salah, musuh yang berlari
            // di tempat terlihat rusak.
            return VatRole.Idle;
        }
    }
}
