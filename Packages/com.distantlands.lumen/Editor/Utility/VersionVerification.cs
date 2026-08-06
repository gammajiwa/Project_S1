using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Net.Http;

namespace DistantLands.Lumen.EditorScripts
{
    [InitializeOnLoad]
    public class VersionVerification
    {
        private static HttpClient httpClient = new HttpClient();

        private static bool _updateCheckPerformed
        {
            get { return SessionState.GetBool("LUMEN_UpdateChecked", false); }
            set { SessionState.SetBool("LUMEN_UpdateChecked", value); }
        }

        static VersionVerification()
        {
            EditorApplication.update += CheckForUpdatesAfterEditorUpdate;
        }

        private static async void CheckForUpdatesAfterEditorUpdate()
        {
            EditorApplication.update -= CheckForUpdatesAfterEditorUpdate;
            await CheckForUpdates((success, currentVersion, latestVersion) =>
            {
                if (success)
                {
                    AssetInformation.INSTALLED_VERSION = currentVersion;
                    AssetInformation.UAS_VERSION = latestVersion;
                }
            });
        }

        private static async Task CheckForUpdates(Action<bool, Version, Version> OnUpdatesChecked)
        {
            var latestVersionResult = await GetLatestLumenVersion();
            if (latestVersionResult == null)
            {
                OnUpdatesChecked?.Invoke(false, null, null);
                return;
            }

            Version currentVersion = null;
            Version latestVersion = null;
            try
            {
                currentVersion = new Version(UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().FirstOrDefault(x => x.name == AssetInformation.PACKAGE_ID).version);
                latestVersion = latestVersionResult;
            }
            catch
            {
                OnUpdatesChecked?.Invoke(false, null, null);
            }
            OnUpdatesChecked?.Invoke(true, currentVersion, latestVersion);
        }

        public class ASV
        {
            public string version;
            public string name;
            public string category;
            public int id;
            public string publisher;
        }

        public static async Task<Version> GetLatestLumenVersion()
        {
            try
            {
                var url = AssetInformation.VERSION_URL;
                var result = await httpClient.GetAsync(url);
                result.EnsureSuccessStatusCode();
                var resultStr = await result.Content.ReadAsStringAsync();
                ASV version = JsonUtility.FromJson<ASV>(resultStr);
                string[] numbers = version.version.Split('.');
                int major = int.TryParse(numbers[0], out major) ? major : 0;
                int minor = int.TryParse(numbers[1], out minor) ? minor : 0;
                int patch = int.TryParse(numbers[2], out patch) ? patch : 0;
                return new Version(major, minor, patch);
            }
            catch
            {
                return null;
            }
        }
    }
}
