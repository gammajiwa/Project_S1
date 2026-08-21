using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Menyambungkan font cadangan untuk aksara yang tidak dimiliki font tampilan permainan.
    ///
    /// Font yang dipakai sekarang — Cinzel, Barlow, EB Garamond — <b>nol glyph CJK dan Hangul</b>,
    /// dan dua yang pertama bahkan nol Cyrillic. Itu terukur, bukan dugaan. Artinya begitu pemain
    /// memilih Rusia, Cina, Jepang, atau Korea, seluruh layar berubah jadi kotak kosong: bukan
    /// terjemahan yang jelek, tapi permainan yang tidak bisa dibaca sama sekali.
    ///
    /// TextMeshPro menjawab itu lewat <b>rantai fallback</b>: font utama dipakai selama glyphnya
    /// ada, dan yang tidak ada diambil dari font berikutnya di rantai. Jadi huruf Latin tetap
    /// memakai Cinzel yang jadi identitas permainan, dan hanya aksara yang tidak dimilikinya yang
    /// jatuh ke font cadangan. Tanpa rantai, satu-satunya alternatif adalah mengganti SELURUH
    /// font untuk semua bahasa — dan itu membuang tampang permainannya demi empat bahasa.
    ///
    /// <b>Cara pakai: taruh berkas fontnya di <c>Assets/Art/UI/Fonts/Fallback/</c>. Selesai.</b>
    /// Kelas ini mendengarkan impor aset, jadi tidak ada menu yang perlu ditekan — permintaan
    /// pemilik project, dan benar: langkah manual yang gampang dilupakan adalah bug yang menunggu.
    ///
    /// Atlasnya dibuat <b>Dynamic</b>, bukan dipanggang. Font CJK memuat puluhan ribu glyph, dan
    /// memanggang semuanya jadi tekstur statis menghasilkan aset ratusan megabyte yang 99%
    /// isinya tidak pernah muncul di layar. Dynamic merasterkan yang benar-benar dipakai saja.
    /// </summary>
    public class FontFallbackPass : AssetPostprocessor
    {
        public const string Folder = "Assets/Art/UI/Fonts/Fallback";

        /// <summary>Font tampilan yang rantai cadangannya diurus di sini.</summary>
        static readonly string[] DisplayFonts =
        {
            "Assets/Art/UI/Fonts/Cinzel SDF.asset",
            "Assets/Art/UI/Fonts/BarlowSemiCondensed-Regular SDF.asset",
            "Assets/Art/UI/Fonts/BarlowSemiCondensed-SemiBold SDF.asset",
            "Assets/Art/UI/Fonts/EBGaramond SDF.asset",
        };

        static void OnPostprocessAllAssets(string[] imported, string[] deleted,
            string[] moved, string[] movedFrom)
        {
            if (!Touches(imported) && !Touches(deleted) && !Touches(moved)) return;

            // Ditunda satu frame: impor font masih berjalan saat callback ini dipanggil, dan
            // membuat aset TMP di tengah impor adalah cara mendapatkan atlas kosong.
            EditorApplication.delayCall += () => Run(false);
        }

        static bool Touches(string[] paths)
        {
            if (paths == null) return false;

            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i] != null && paths[i].StartsWith(Folder) && IsFont(paths[i])) return true;
            }

            return false;
        }

        static bool IsFont(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".ttf" || ext == ".otf";
        }

        [MenuItem("Tools/Grimoire/Sambungkan Font Cadangan")]
        static void RunFromMenu() => Run(true);

        static void Run(bool loud)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                if (loud) Debug.LogWarning("[FontFallbackPass] folder " + Folder + " belum ada.");
                return;
            }

            var built = new List<TMP_FontAsset>();

            foreach (var guid in AssetDatabase.FindAssets("t:Font", new[] { Folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsFont(path)) continue;

                var asset = EnsureFontAsset(path);
                if (asset != null) built.Add(asset);
            }

            if (built.Count == 0)
            {
                if (loud)
                {
                    Debug.LogWarning("[FontFallbackPass] belum ada .ttf/.otf di " + Folder +
                                     ". Lihat README di folder itu untuk daftar berkas yang dibutuhkan.");
                }

                return;
            }

            int wired = 0;
            foreach (var path in DisplayFonts)
            {
                var display = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (display == null) continue;

                display.fallbackFontAssetTable = new List<TMP_FontAsset>(built);
                EditorUtility.SetDirty(display);
                wired++;
            }

            // Jaring terakhir: apa pun yang memakai font di luar daftar di atas tetap tertolong.
            if (TMP_Settings.instance != null)
            {
                var so = new SerializedObject(TMP_Settings.instance);
                var list = so.FindProperty("m_fallbackFontAssets");

                if (list != null)
                {
                    list.ClearArray();
                    for (int i = 0; i < built.Count; i++)
                    {
                        list.InsertArrayElementAtIndex(i);
                        list.GetArrayElementAtIndex(i).objectReferenceValue = built[i];
                    }

                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            AssetDatabase.SaveAssets();

            var report = new System.Text.StringBuilder();
            report.Append("[FontFallbackPass] ").Append(built.Count)
                  .Append(" font cadangan tersambung ke ").Append(wired)
                  .Append(" font tampilan + fallback global TMP:\n");

            foreach (var f in built) report.Append("  ").Append(f.name).Append('\n');

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Aset TMP untuk sebuah berkas font, dibuat kalau belum ada. Yang sudah ada TIDAK
        /// disentuh: pengaturannya bisa saja sudah diputar tangan, dan alat yang menimpanya tiap
        /// impor membuat setiap penyesuaian hilang tanpa jejak.
        /// </summary>
        static TMP_FontAsset EnsureFontAsset(string fontPath)
        {
            var assetPath = Path.ChangeExtension(fontPath, null) + " SDF.asset";

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null) return null;

            // Dynamic: glyph dirasterkan saat benar-benar dipakai. Satu-satunya pilihan yang
            // masuk akal untuk font CJK - memanggang seluruh glyphnya menghasilkan aset ratusan
            // megabyte yang isinya nyaris tidak pernah muncul.
            var created = TMP_FontAsset.CreateFontAsset(font, 90, 9,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);

            if (created == null) return null;

            created.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(created, assetPath);

            // Atlas dan material lahir sebagai objek lepas; tanpa dititipkan ke asetnya mereka
            // hilang saat Unity dimuat ulang, dan font assetnya bangun tanpa tekstur.
            if (created.atlasTextures != null)
            {
                foreach (var tex in created.atlasTextures)
                {
                    if (tex == null) continue;
                    tex.name = created.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(tex, created);
                }
            }

            if (created.material != null)
            {
                created.material.name = created.name + " Material";
                AssetDatabase.AddObjectToAsset(created.material, created);
            }

            AssetDatabase.SaveAssets();
            return created;
        }
    }
}
