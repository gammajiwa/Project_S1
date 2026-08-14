using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Memindahkan isi aset ke bahasa Inggris, dan menyelamatkan teks Indonesia yang sekarang ada
    /// di sana ke <c>id.txt</c>.
    ///
    /// Dijalankan SEKALI. Setelah asetnya berbahasa Inggris, alat ini tidak punya pekerjaan lagi.
    ///
    /// Kenapa asetnya yang harus jadi Inggris, bukan sekadar menaruh Inggris di tabel: nilai
    /// <c>DisplayName</c> dipakai sebagai KUNCI di beberapa tempat — <c>PlayerCaster</c> mencari
    /// VFX hit lewat nama piece — jadi ia harus stabil dan tidak boleh ikut berganti mengikuti
    /// bahasa layar. Yang berpindah bahasa cuma yang DIGAMBAR, dan itu terjadi di tabel.
    ///
    /// Menulis lewat SerializedObject, bukan menyunting YAML-nya langsung: Unity melipat kalimat
    /// panjang jadi beberapa baris dan mengutipnya kalau perlu, dan menebak aturan itu dengan
    /// regex adalah cara memecahkan 136 aset sekaligus.
    /// </summary>
    public static class LocEnglishMigration
    {
        const string InputPath = "Assets/GameData/_loc_migration.txt";
        const string IndonesianTable = "Assets/Resources/Loc/id.txt";

        [MenuItem("Tools/Grimoire/Pindahkan Isi ke Bahasa Inggris (sekali jalan)")]
        public static void Run()
        {
            if (!File.Exists(InputPath))
            {
                Debug.LogError("[LocEnglishMigration] tidak ada " + InputPath +
                               " — berkas itu berisi terjemahan Inggrisnya, satu 'kunci=teks' per baris.");
                return;
            }

            var english = new Dictionary<string, string>(160);
            foreach (var raw in File.ReadAllLines(InputPath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                int split = line.IndexOf('=');
                if (split <= 0) continue;

                english[line.Substring(0, split).Trim()] =
                    line.Substring(split + 1).Replace("\\n", "\n");
            }

            var pieces = Index<PieceDefinition>(d => d.Id);
            var heroes = Index<HeroLoadout>(h => h.Id);
            var statuses = Index<StatusDefinition>(s => s.Id);

            var rescued = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
            int changed = 0, missing = 0;

            foreach (var pair in english)
            {
                if (!Split(pair.Key, out var kind, out var id, out var field))
                {
                    Debug.LogWarning("[LocEnglishMigration] kunci tidak dikenal: " + pair.Key);
                    continue;
                }

                Object asset = kind == "piece" ? Get(pieces, id)
                             : kind == "hero" ? Get(heroes, id)
                             : Get(statuses, id);

                if (asset == null)
                {
                    Debug.LogWarning("[LocEnglishMigration] aset tidak ketemu untuk " + pair.Key);
                    missing++;
                    continue;
                }

                var so = new SerializedObject(asset);
                var prop = so.FindProperty(field == "name" ? "DisplayName" : "Blurb");

                if (prop == null)
                {
                    Debug.LogWarning("[LocEnglishMigration] field tidak ada untuk " + pair.Key);
                    missing++;
                    continue;
                }

                var old = prop.stringValue;

                // Teks lama diselamatkan APA ADANYA, termasuk kalau kebetulan sudah sama dengan
                // yang baru. id.txt yang lengkap lebih berguna daripada id.txt yang cuma memuat
                // baris-baris yang kebetulan berubah.
                if (!string.IsNullOrEmpty(old)) rescued[pair.Key] = old;

                if (old == pair.Value) continue;

                prop.stringValue = pair.Value;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                changed++;
            }

            AssetDatabase.SaveAssets();

            int written = MergeIndonesian(rescued);

            AssetDatabase.Refresh();

            Debug.Log("[LocEnglishMigration] " + changed + " field aset jadi Inggris, "
                      + written + " baris masuk ke id.txt"
                      + (missing > 0 ? ", " + missing + " kunci gagal (lihat peringatan di atas)" : "")
                      + ".\nJalankan Tools/Grimoire/Susun Tabel Bahasa untuk menyegarkan en.txt.");
        }

        /// <summary>
        /// Menulis teks Indonesia ke tabelnya sebagai terjemahan SUNGGUHAN — tanpa tanda TODO,
        /// karena baris bertanda itu dilewati pembacanya dan teks ini justru yang paling benar
        /// dari semua yang ada.
        /// </summary>
        static int MergeIndonesian(SortedDictionary<string, string> rescued)
        {
            var table = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
            var head = new List<string>();

            if (File.Exists(IndonesianTable))
            {
                foreach (var raw in File.ReadAllLines(IndonesianTable))
                {
                    var line = raw.Trim();

                    if (line.StartsWith("# TODO "))
                    {
                        // Baris TODO diurai juga: kalau kuncinya termasuk yang diselamatkan, ia
                        // akan ditimpa jadi terjemahan betulan di bawah.
                        line = line.Substring(7).Trim();
                    }
                    else if (line.Length == 0 || line[0] == '#')
                    {
                        if (table.Count == 0) head.Add(raw);
                        continue;
                    }

                    int split = line.IndexOf('=');
                    if (split <= 0) continue;

                    var key = line.Substring(0, split).Trim();
                    if (!table.ContainsKey(key)) table[key] = line.Substring(split + 1);
                }
            }

            foreach (var pair in rescued) table[pair.Key] = pair.Value.Replace("\n", "\\n");

            var body = new StringBuilder(24576);
            foreach (var line in head) body.Append(line).Append('\n');
            if (head.Count == 0) body.Append("# id  —  Bahasa Indonesia\n");

            string section = null;
            foreach (var pair in table)
            {
                var group = pair.Key.Split('.')[0];
                if (group != section)
                {
                    section = group;
                    body.Append("\n# ---------------- ").Append(section).Append(" ----------------\n");
                }

                body.Append(pair.Key).Append('=').Append(pair.Value).Append('\n');
            }

            File.WriteAllText(IndonesianTable, body.ToString(), new UTF8Encoding(false));
            return rescued.Count;
        }

        // ------------------------------------------------------------------ bantu

        static Dictionary<string, Object> Index<T>(System.Func<T, string> id) where T : Object
        {
            var map = new Dictionary<string, Object>(200);

            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;

                var key = id(asset);
                if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key)) map[key] = asset;
            }

            return map;
        }

        static Object Get(Dictionary<string, Object> map, string id)
            => map.TryGetValue(id, out var hit) ? hit : null;

        static bool Split(string key, out string kind, out string id, out string field)
        {
            kind = id = field = null;

            int first = key.IndexOf('.');
            int last = key.LastIndexOf('.');
            if (first <= 0 || last <= first) return false;

            kind = key.Substring(0, first);
            id = key.Substring(first + 1, last - first - 1);
            field = key.Substring(last + 1);

            return kind == "piece" || kind == "hero" || kind == "status";
        }
    }
}
