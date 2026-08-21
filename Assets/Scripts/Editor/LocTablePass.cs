using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Menyusun tabel bahasa di <c>Assets/Resources/Loc</c>.
    ///
    /// Dua sumber, dan keduanya diperlakukan berbeda:
    ///
    /// <b>Kunci ISI</b> (<c>piece.*</c>, <c>hero.*</c>, <c>status.*</c>) selalu ditulis ulang dari
    /// asetnya. Asetnya adalah sumber kebenaran untuk bahasa Inggris, jadi menyuntingnya di tabel
    /// cuma akan tertimpa saat alat ini jalan lagi — dan tertimpa diam-diam adalah cara paling
    /// halus membuat orang berhenti percaya pada alatnya.
    ///
    /// <b>Kunci UI</b> ditulis tangan di <c>en.txt</c> dan TIDAK PERNAH disentuh alat ini.
    ///
    /// Untuk sembilan bahasa lain, alat ini cuma MELENGKAPI: entri yang sudah diterjemahkan
    /// dibiarkan apa adanya, kunci yang belum ada ditambahkan dengan teks Inggrisnya dan ditandai
    /// TODO. Jadi berkasnya selalu utuh, permainannya selalu terbaca, dan penerjemah selalu tahu
    /// persis mana yang masih giliran dia.
    /// </summary>
    public static class LocTablePass
    {
        const string Folder = "Assets/Resources/Loc";

        [MenuItem("Tools/Grimoire/Susun Tabel Bahasa")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");

                AssetDatabase.CreateFolder("Assets/Resources", "Loc");
            }

            var content = CollectContent(out int pieces, out int heroes, out int statuses);

            // --- Inggris: kunci UI dipertahankan, kunci isi ditulis ulang ---
            var english = ReadTable("en");
            foreach (var pair in content) english[pair.Key] = pair.Value;
            WriteTable("en", english, null);

            // --- sisanya: dilengkapi, tidak ditimpa ---
            var report = new StringBuilder();
            report.Append("en  ").Append(english.Count).Append(" kunci  (isi: ")
                  .Append(pieces).Append(" piece, ").Append(heroes).Append(" hero, ")
                  .Append(statuses).Append(" status)\n");

            foreach (var lang in Loc.All)
            {
                var code = Loc.CodeOf(lang);
                if (code == "en") continue;

                var table = ReadTable(code);

                int already = 0;
                foreach (var key in english.Keys)
                {
                    if (table.ContainsKey(key) && table[key].Length > 0) already++;
                }

                WriteTable(code, table, english);

                report.Append(code.PadRight(8)).Append(english.Count).Append(" kunci  ")
                      .Append(already).Append(" diterjemahkan, ")
                      .Append(english.Count - already).Append(" masih TODO\n");
            }

            AssetDatabase.Refresh();
            Debug.Log("[LocTablePass] tabel bahasa tersusun di " + Folder + ":\n" + report);
        }

        // ------------------------------------------------------------------ isi

        static SortedDictionary<string, string> CollectContent(out int pieces, out int heroes,
            out int statuses)
        {
            var map = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
            pieces = heroes = statuses = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:PieceDefinition"))
            {
                var def = AssetDatabase.LoadAssetAtPath<PieceDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (def == null || string.IsNullOrEmpty(def.Id)) continue;

                Put(map, "piece." + def.Id + ".name", def.DisplayName);
                Put(map, "piece." + def.Id + ".blurb", def.Blurb);
                pieces++;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:HeroLoadout"))
            {
                var hero = AssetDatabase.LoadAssetAtPath<HeroLoadout>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (hero == null || string.IsNullOrEmpty(hero.Id)) continue;

                Put(map, "hero." + hero.Id + ".name", hero.DisplayName);
                Put(map, "hero." + hero.Id + ".blurb", hero.Blurb);
                heroes++;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:StatusDefinition"))
            {
                var status = AssetDatabase.LoadAssetAtPath<StatusDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (status == null || string.IsNullOrEmpty(status.Id)) continue;

                Put(map, "status." + status.Id + ".name", status.DisplayName);
                statuses++;
            }

            return map;
        }

        static void Put(SortedDictionary<string, string> map, string key, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            map[key] = value;
        }

        // ------------------------------------------------------------------ berkas

        static SortedDictionary<string, string> ReadTable(string code)
        {
            var map = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
            var path = Folder + "/" + code + ".txt";

            if (!File.Exists(path)) return map;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                int split = line.IndexOf('=');
                if (split <= 0) continue;

                map[line.Substring(0, split).Trim()] = line.Substring(split + 1);
            }

            return map;
        }

        /// <summary>
        /// <paramref name="source"/> null = berkas ini SUMBERNYA (Inggris). Terisi = berkas
        /// terjemahan: tiap kunci sumber wajib muncul, yang belum diterjemahkan memakai teks
        /// Inggrisnya dan diberi tanda.
        /// </summary>
        static void WriteTable(string code, SortedDictionary<string, string> table,
            SortedDictionary<string, string> source)
        {
            var body = new StringBuilder(16384);

            body.Append("# ").Append(code).Append("  —  ").Append(Loc.NativeNameOf(LangOf(code)))
                .Append('\n');
            body.Append("# Digenerate & dilengkapi oleh Tools/Grimoire/Susun Tabel Bahasa.\n");

            if (source != null)
            {
                body.Append("# Baris bertanda TODO masih memakai teks Inggris. Terjemahkan lalu\n");
                body.Append("# HAPUS tanda TODO-nya, supaya laporan berikutnya menghitungnya.\n");
            }
            else
            {
                body.Append("# Kunci piece./hero./status. ditulis ulang dari aset - jangan disunting\n");
                body.Append("# di sini. Kunci UI ditulis tangan dan tidak pernah disentuh alat ini.\n");
            }

            body.Append("# \\n = ganti baris. Pemisahnya tanda sama dengan PERTAMA.\n");

            var keys = source ?? table;
            string section = null;

            foreach (var key in keys.Keys)
            {
                var group = SectionOf(key);
                if (group != section)
                {
                    section = group;
                    body.Append("\n# ---------------- ").Append(section).Append(" ----------------\n");
                }

                bool translated = table.TryGetValue(key, out var value) && value.Length > 0;
                if (!translated && source != null) value = source[key];
                if (!translated && source == null) value = table.TryGetValue(key, out var own) ? own : "";

                if (source != null && !translated) body.Append("# TODO ");

                body.Append(key).Append('=').Append(value.Replace("\n", "\\n")).Append('\n');
            }

            File.WriteAllText(Folder + "/" + code + ".txt", body.ToString(), new UTF8Encoding(false));
        }

        static string SectionOf(string key)
        {
            int dot = key.IndexOf('.');
            return dot <= 0 ? "lain" : key.Substring(0, dot);
        }

        static Language LangOf(string code)
        {
            foreach (var lang in Loc.All)
            {
                if (Loc.CodeOf(lang) == code) return lang;
            }

            return Language.English;
        }
    }
}
