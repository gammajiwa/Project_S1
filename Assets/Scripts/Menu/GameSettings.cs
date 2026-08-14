using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Player-facing options, backed by PlayerPrefs. Plain C# and instanced on purpose — the
    /// bootstrap owns one and hands it out, so this stays out of the no-singleton rule.
    /// </summary>
    public class GameSettings
    {
        /// <summary>Sentinel for "no cap" — <see cref="Application.targetFrameRate"/> uses -1 for that.</summary>
        public const int FrameCapUnlimited = 0;

        public static readonly int[] FrameCapChoices = { 30, 60, 120, 144, 240, FrameCapUnlimited };

        const string KeyFullscreen = "opt.fullscreen";
        const string KeyWidth = "opt.width";
        const string KeyHeight = "opt.height";
        const string KeyVSync = "opt.vsync";
        const string KeyFrameCap = "opt.framecap";
        const string KeyMaster = "opt.vol.master";
        const string KeySfx = "opt.vol.sfx";
        const string KeyMusic = "opt.vol.music";
        const string KeyDamageText = "opt.fx.damagetext";
        const string KeyEnemyShadows = "opt.fx.enemyshadows";
        const string KeyWeatherVfx = "opt.fx.weather";

        public bool Fullscreen = true;
        public int Width;
        public int Height;
        public bool VSync = true;

        // TANPA BATAS, dan itu koreksi. Cap 60 sebagai bawaan membuat game yang sanggup lebih
        // terkunci di 60 — dan tiap frame yang meleset jatuh terasa ke 30-an. Yang mau membatasi
        // fps tetap bisa lewat menu; bawaan tidak boleh jadi pagar.
        public int FrameCap = FrameCapUnlimited;

        public float MasterVolume = 1f;

        /// <summary>Stored only — nothing reads it yet because the game has no audio system.</summary>
        public float SfxVolume = 1f;

        /// <summary>Stored only — see <see cref="SfxVolume"/>.</summary>
        public float MusicVolume = 0.7f;

        // ------------------------------------------------------------------ performa
        //
        // Toggle untuk yang mahal digambar dan tidak mengubah aturan main sedikit pun. Semuanya
        // nyala secara bawaan — mematikan adalah keputusan pemilik mesin kentang, bukan bawaan
        // yang menghukum semua orang. Yang gameplay (jumlah musuh, drop) TIDAK boleh pindah ke
        // sini: setting performa yang mengubah aturan main adalah cheat yang menyamar.

        [Tooltip("Angka damage melayang.")]
        public bool DamageText = true;

        /// <summary>
        /// Bayangan gerombolan. Satu-satunya petunjuk kaki menyentuh tanah di kamera menunduk,
        /// tapi juga pass yang menggambar seluruh gerombolan SEKALI LAGI.
        /// </summary>
        public bool EnemyShadows = true;

        /// <summary>Hujan, angin, kunang-kunang, dan kawan-kawan cuacanya.</summary>
        public bool WeatherVfx = true;

        public static GameSettings Load()
        {
            var s = new GameSettings
            {
                Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1,
                Width = PlayerPrefs.GetInt(KeyWidth, Screen.width),
                Height = PlayerPrefs.GetInt(KeyHeight, Screen.height),
                VSync = PlayerPrefs.GetInt(KeyVSync, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1,
                FrameCap = PlayerPrefs.GetInt(KeyFrameCap, FrameCapUnlimited),
                MasterVolume = PlayerPrefs.GetFloat(KeyMaster, 1f),
                SfxVolume = PlayerPrefs.GetFloat(KeySfx, 1f),
                MusicVolume = PlayerPrefs.GetFloat(KeyMusic, 0.7f),
                DamageText = PlayerPrefs.GetInt(KeyDamageText, 1) == 1,
                EnemyShadows = PlayerPrefs.GetInt(KeyEnemyShadows, 1) == 1,
                WeatherVfx = PlayerPrefs.GetInt(KeyWeatherVfx, 1) == 1
            };

            return s;
        }

        public void Save()
        {
            PlayerPrefs.SetInt(KeyFullscreen, Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(KeyWidth, Width);
            PlayerPrefs.SetInt(KeyHeight, Height);
            PlayerPrefs.SetInt(KeyVSync, VSync ? 1 : 0);
            PlayerPrefs.SetInt(KeyFrameCap, FrameCap);
            PlayerPrefs.SetFloat(KeyMaster, MasterVolume);
            PlayerPrefs.SetFloat(KeySfx, SfxVolume);
            PlayerPrefs.SetFloat(KeyMusic, MusicVolume);
            PlayerPrefs.SetInt(KeyDamageText, DamageText ? 1 : 0);
            PlayerPrefs.SetInt(KeyEnemyShadows, EnemyShadows ? 1 : 0);
            PlayerPrefs.SetInt(KeyWeatherVfx, WeatherVfx ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Apply()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;

            // A frame cap does nothing while vSync drives presentation, so don't pretend it applies.
            Application.targetFrameRate = VSync || FrameCap <= 0 ? -1 : FrameCap;

            AudioListener.volume = Mathf.Clamp01(MasterVolume);

#if !UNITY_EDITOR
            // In the Editor this only fights the Game view's own resolution dropdown.
            var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (Width > 0 && Height > 0) Screen.SetResolution(Width, Height, mode);
            else Screen.fullScreenMode = mode;
#endif
        }

        /// <summary>Distinct display sizes, smallest first, with the current one guaranteed present.</summary>
        public List<Vector2Int> AvailableResolutions()
        {
            var list = new List<Vector2Int>();
            var native = Screen.resolutions;

            for (int i = 0; i < native.Length; i++)
            {
                var entry = new Vector2Int(native[i].width, native[i].height);
                if (!list.Contains(entry)) list.Add(entry);
            }

            if (list.Count == 0)
            {
                // Some editor and headless setups report nothing — a fixed ladder beats an empty row.
                list.Add(new Vector2Int(1280, 720));
                list.Add(new Vector2Int(1600, 900));
                list.Add(new Vector2Int(1920, 1080));
                list.Add(new Vector2Int(2560, 1440));
            }

            if (Width > 0 && Height > 0)
            {
                var current = new Vector2Int(Width, Height);
                if (!list.Contains(current)) list.Add(current);
            }

            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }

        public static string FrameCapLabel(int cap)
            => cap <= 0 ? Loc.T("settings.framecap.unlimited") : Loc.F("settings.framecap.value", cap);

        public static string ResolutionLabel(Vector2Int size)
            => Loc.F("settings.resolution.value", size.x, size.y);
    }
}
