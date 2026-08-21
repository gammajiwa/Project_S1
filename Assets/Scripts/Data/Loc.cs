using System;
using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>Bahasa yang dikirim bersama permainan. Kodenya dipakai sebagai nama berkas tabel.</summary>
    public enum Language
    {
        English,
        Indonesian,
        French,
        German,
        Spanish,
        PortugueseBrazil,
        Russian,
        ChineseSimplified,
        Japanese,
        Korean
    }

    /// <summary>
    /// Lokalisasi buatan sendiri, sengaja TANPA package Localization milik Unity.
    ///
    /// Permintaan pemilik project, dan pilihan yang masuk akal untuk permainan seukuran ini:
    /// package itu membawa Addressables, tabel biner, dan satu lapisan aset yang harus dibuka di
    /// Editor untuk mengubah satu kata. Yang dibutuhkan di sini cuma "kunci -> kalimat", dan
    /// berkas teks biasa membuatnya bisa disunting penerjemah yang tidak punya Unity, bisa
    /// di-diff di git, dan bisa ditambal tanpa build ulang.
    ///
    /// <b>Bentuk berkasnya</b> — <c>Assets/Resources/Loc/&lt;kode&gt;.txt</c>, satu entri per baris:
    /// <code>
    /// # baris yang diawali pagar adalah komentar
    /// menu.play=PLAY
    /// hud.wave=WAVE {0}
    /// tip.rune=Runes are the floor.\nSkills stand on them.
    /// </code>
    /// Pemisahnya tanda sama dengan PERTAMA, jadi nilainya boleh mengandung tanda sama dengan.
    /// <c>\n</c> jadi ganti baris.
    ///
    /// <b>Rantai jatuh baliknya</b> bahasa terpilih -> Inggris -> teks cadangan dari pemanggil ->
    /// kuncinya sendiri. Yang terakhir itu disengaja: kunci yang bocor ke layar jelek dilihat,
    /// dan justru itu gunanya. Kalimat kosong akan lolos ke rilis tanpa ada yang sadar.
    /// </summary>
    public static class Loc
    {
        const string Folder = "Loc/";
        const string PrefKey = "opt.language";

        static readonly Dictionary<string, string> Table = new Dictionary<string, string>(512);
        static readonly Dictionary<string, string> Fallback = new Dictionary<string, string>(512);

        static bool _ready;

        /// <summary>Bahasa yang sedang dipakai. Ubah lewat <see cref="Use"/>.</summary>
        public static Language Current { get; private set; } = Language.English;

        /// <summary>
        /// Dipanggil setiap bahasa berganti. Layar yang menulis teksnya sekali saat dibangun
        /// WAJIB mendengarkan ini; yang menulis ulang tiap frame tidak perlu.
        /// </summary>
        public static event Action Changed;

        // ---------------------------------------------------------------- kode & nama

        /// <summary>Kode berkas tabel. Bukan nama yang dilihat pemain.</summary>
        public static string CodeOf(Language lang)
        {
            switch (lang)
            {
                case Language.Indonesian: return "id";
                case Language.French: return "fr";
                case Language.German: return "de";
                case Language.Spanish: return "es";
                case Language.PortugueseBrazil: return "pt-BR";
                case Language.Russian: return "ru";
                case Language.ChineseSimplified: return "zh-Hans";
                case Language.Japanese: return "ja";
                case Language.Korean: return "ko";
                default: return "en";
            }
        }

        /// <summary>
        /// Nama bahasa DALAM bahasanya sendiri. Daftar bahasa yang ditulis dalam bahasa yang
        /// sedang aktif tidak berguna untuk orang yang tidak bisa membacanya — dan orang itu
        /// persis yang sedang mencari daftar ini.
        /// </summary>
        public static string NativeNameOf(Language lang)
        {
            switch (lang)
            {
                case Language.Indonesian: return "Bahasa Indonesia";
                case Language.French: return "Français";
                case Language.German: return "Deutsch";
                case Language.Spanish: return "Español";
                case Language.PortugueseBrazil: return "Português (Brasil)";
                case Language.Russian: return "Русский";
                case Language.ChineseSimplified: return "简体中文";
                case Language.Japanese: return "日本語";
                case Language.Korean: return "한국어";
                default: return "English";
            }
        }

        /// <summary>
        /// Nama bahasa yang AMAN DIGAMBAR font permainan sekarang.
        ///
        /// <see cref="NativeNameOf"/> benar secara prinsip - daftar bahasa yang ditulis dalam
        /// bahasa yang sedang aktif tidak berguna untuk orang yang tidak bisa membacanya - tapi
        /// prinsip itu berhenti berlaku begitu fontnya tidak punya aksaranya: yang muncul bukan
        /// nama asing, melainkan tiga kotak kosong, dan kotak kosong tidak bisa dibaca siapa pun.
        ///
        /// Jadi yang beraksara Latin memakai nama aslinya, dan empat sisanya memakai nama Inggris
        /// sampai font CJK/Cyrillic-nya ada. Begitu font itu masuk, cukup empat baris di bawah
        /// yang berubah.
        /// </summary>
        public static string MenuNameOf(Language lang)
        {
            switch (lang)
            {
                case Language.Russian: return "Russian";
                case Language.ChineseSimplified: return "Chinese (Simplified)";
                case Language.Japanese: return "Japanese";
                case Language.Korean: return "Korean";
                default: return NativeNameOf(lang);
            }
        }

        public static Language[] All => (Language[])Enum.GetValues(typeof(Language));

        // ---------------------------------------------------------------- memuat

        /// <summary>
        /// Bahasa sistem pemain, dipetakan ke yang paling dekat. Dipakai HANYA saat belum pernah
        /// ada pilihan tersimpan: menebak sekali itu ramah, menebak ulang tiap kali membuka game
        /// berarti membatalkan pilihan orang.
        /// </summary>
        public static Language Detect()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Indonesian: return Language.Indonesian;
                case SystemLanguage.French: return Language.French;
                case SystemLanguage.German: return Language.German;
                case SystemLanguage.Spanish: return Language.Spanish;
                case SystemLanguage.Portuguese: return Language.PortugueseBrazil;
                case SystemLanguage.Russian: return Language.Russian;
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional: return Language.ChineseSimplified;
                case SystemLanguage.Japanese: return Language.Japanese;
                case SystemLanguage.Korean: return Language.Korean;
                default: return Language.English;
            }
        }

        /// <summary>
        /// Menyiapkan tabel. Aman dipanggil berkali-kali dan dari mana saja — tiap pembaca teks
        /// memanggilnya lebih dulu, jadi urutan bangun scene tidak pernah bisa membuat satu layar
        /// terlanjur menggambar sebelum tabelnya ada.
        /// </summary>
        public static void EnsureReady()
        {
            if (_ready) return;

            var saved = PlayerPrefs.GetString(PrefKey, null);

            // Bawaannya INGGRIS, bukan bahasa sistem pemain. Keputusan pemilik project, dan
            // pilihan yang bisa dipertahankan: Inggris satu-satunya tabel yang dijamin lengkap,
            // sedangkan sembilan lainnya menyusul. Membuka permainan langsung dalam bahasa yang
            // separuh barisnya masih Inggris terbaca sebagai terjemahan yang RUSAK — jauh lebih
            // buruk daripada terjemahan yang belum ada.
            //
            // <see cref="Detect"/> tetap ada dan tetap benar; ia menunggu sampai tabelnya layak
            // dipercaya, dan saat itu tiba cukup satu baris di sini yang berubah.
            var lang = Language.English;

            if (!string.IsNullOrEmpty(saved))
            {
                foreach (var candidate in All)
                {
                    if (CodeOf(candidate) == saved) { lang = candidate; break; }
                }
            }

            _ready = true;
            Apply(lang);
        }

        /// <summary>Ganti bahasa dan simpan pilihannya.</summary>
        public static void Use(Language lang)
        {
            _ready = true;

            if (Current == lang && Table.Count > 0) return;

            Apply(lang);

            PlayerPrefs.SetString(PrefKey, CodeOf(lang));
            PlayerPrefs.Save();
        }

        static void Apply(Language lang)
        {
            Current = lang;

            // Inggris dimuat SELALU, bahkan saat ia bukan bahasa terpilih: ia jaring pengaman
            // untuk kunci yang belum diterjemahkan, dan tabel terjemahan selalu tertinggal dari
            // tabel sumbernya. Tanpa jaring ini tiap kunci baru muncul sebagai kunci mentah di
            // sembilan bahasa sekaligus sampai kesembilannya diperbarui.
            Read(Fallback, "en");

            if (CodeOf(lang) == "en") CopyInto(Table, Fallback);
            else Read(Table, CodeOf(lang));

            Changed?.Invoke();
        }

        static void CopyInto(Dictionary<string, string> target, Dictionary<string, string> source)
        {
            target.Clear();
            foreach (var pair in source) target[pair.Key] = pair.Value;
        }

        static void Read(Dictionary<string, string> target, string code)
        {
            target.Clear();

            var asset = Resources.Load<TextAsset>(Folder + code);
            if (asset == null)
            {
                Debug.LogWarning("[Loc] tabel '" + code + "' tidak ada di Resources/" + Folder +
                                 " — teksnya jatuh balik ke Inggris.");
                return;
            }

            var lines = asset.text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim('\r', ' ', '\t');
                if (line.Length == 0 || line[0] == '#') continue;

                int split = line.IndexOf('=');
                if (split <= 0) continue;

                var key = line.Substring(0, split).Trim();
                var value = line.Substring(split + 1).Replace("\\n", "\n");

                // Entri kembar: yang BELAKANGAN menang, karena itu yang akan dilihat orang saat
                // menyunting berkasnya dari atas ke bawah.
                target[key] = value;
            }
        }

        // ---------------------------------------------------------------- membaca

        /// <summary>Kalimat untuk sebuah kunci. Tidak pernah null.</summary>
        public static string T(string key) => T(key, null);

        /// <summary>
        /// Kalimat untuk sebuah kunci, dengan teks cadangan kalau belum ada di tabel mana pun.
        /// Dipakai untuk isi yang sudah punya teks di asetnya sendiri: nama piece, blurb, dan
        /// kawan-kawan. Aset yang belum punya kunci tetap tampil apa adanya, bukan jadi kunci
        /// mentah di tengah papan.
        /// </summary>
        public static string T(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback ?? string.Empty;

            EnsureReady();

            if (Table.TryGetValue(key, out var hit) && hit.Length > 0) return hit;
            if (Fallback.TryGetValue(key, out var english) && english.Length > 0) return english;

            return string.IsNullOrEmpty(fallback) ? key : fallback;
        }

        /// <summary>Kalimat bersisipan, urutan sisipannya ditentukan tabel lewat {0}, {1}, ...</summary>
        public static string F(string key, params object[] args)
        {
            var form = T(key);

            try { return string.Format(form, args); }
            catch (FormatException)
            {
                // Terjemahan yang salah menulis sisipannya tidak boleh menjatuhkan permainan.
                // Kalimat mentahnya jelek dibaca, dan itu jauh lebih baik daripada layar hitam.
                Debug.LogWarning("[Loc] sisipan rusak di kunci '" + key + "': " + form);
                return form;
            }
        }

        public static bool Has(string key)
        {
            EnsureReady();
            return Table.ContainsKey(key) || Fallback.ContainsKey(key);
        }

        /// <summary>Semua kunci yang dikenal tabel Inggris. Dipakai alat editor untuk mengaudit.</summary>
        public static IEnumerable<string> EnglishKeys
        {
            get
            {
                EnsureReady();
                return Fallback.Keys;
            }
        }
    }
}
