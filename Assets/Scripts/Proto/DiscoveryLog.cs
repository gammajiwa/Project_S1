using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// What the player has ever owned. The only thing that survives between runs — everything
    /// else is per-run state.
    /// </summary>
    public class DiscoveryLog
    {
        [System.Serializable]
        class SaveData
        {
            public List<string> Ids = new List<string>();
        }

        readonly HashSet<string> _ids = new HashSet<string>();

        static string FilePath => Path.Combine(Application.persistentDataPath, "codex.json");

        public int Count => _ids.Count;

        public bool Has(string id) => !string.IsNullOrEmpty(id) && _ids.Contains(id);

        /// <summary>Returns true only the first time this id is seen — good for a "NEW!" popup.</summary>
        public bool Discover(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (!_ids.Add(id)) return false;

            Save();
            return true;
        }

        /// <summary>Wipes every discovery. Writes an empty log rather than deleting the file —
        /// a missing file and an emptied one must not behave differently.</summary>
        public void Clear()
        {
            _ids.Clear();
            Save();
        }

        public static DiscoveryLog Load()
        {
            var log = new DiscoveryLog();

            try
            {
                if (!File.Exists(FilePath)) return log;

                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                if (data?.Ids == null) return log;

                for (int i = 0; i < data.Ids.Count; i++) log._ids.Add(data.Ids[i]);
            }
            catch (System.Exception e)
            {
                // A corrupt codex must never block play — start fresh and say so.
                Debug.LogWarning($"[DiscoveryLog] gagal baca codex, mulai kosong: {e.Message}");
            }

            return log;
        }

        public void Save()
        {
            try
            {
                var data = new SaveData();
                data.Ids.AddRange(_ids);
                File.WriteAllText(FilePath, JsonUtility.ToJson(data));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DiscoveryLog] gagal simpan codex: {e.Message}");
            }
        }
    }
}
