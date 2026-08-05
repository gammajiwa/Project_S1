using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// The codex, now living in the main menu instead of mid-run. It reads the discovery log
    /// straight off disk every time it opens, so a run that just ended is already reflected.
    /// </summary>
    public class CodexPanel : MonoBehaviour
    {
        [SerializeField] ContentDatabase _database;
        [SerializeField] TextMeshProUGUI _counter;
        [SerializeField] TextMeshProUGUI _emptyHint;
        [SerializeField] RectTransform _content;

        [Tooltip("Anak yang dinonaktifkan, dipakai sebagai cetakan tiap slot.")]
        [SerializeField] CodexEntry _entryTemplate;

        readonly List<PieceDefinition> _order = new List<PieceDefinition>();
        readonly List<CodexEntry> _entries = new List<CodexEntry>();

        void OnEnable() => Refresh();

        public void Refresh()
        {
            if (_database == null || _content == null || _entryTemplate == null)
            {
                Debug.LogError("[CodexPanel] referensi belum lengkap di Inspector.", this);
                return;
            }

            BuildOrder();

            var log = DiscoveryLog.Load();

            for (int i = 0; i < _order.Count; i++)
            {
                EntryAt(i).Bind(_order[i], log.Has(_order[i].Id));
            }

            // Spare entries stay alive but hidden — the list only ever grows when content is added.
            for (int i = _order.Count; i < _entries.Count; i++)
            {
                _entries[i].gameObject.SetActive(false);
            }

            if (_counter != null)
            {
                _counter.text = log.Count + " / " + _order.Count + " KETEMU";
            }

            if (_emptyHint != null)
            {
                _emptyHint.gameObject.SetActive(log.Count == 0);
            }
        }

        void BuildOrder()
        {
            _order.Clear();

            for (int i = 0; i < _database.Pieces.Count; i++)
            {
                if (_database.Pieces[i] != null) _order.Add(_database.Pieces[i]);
            }

            // Stable order: runes first, then rarity, then name. Scroll position must not shuffle.
            _order.Sort((a, b) =>
            {
                if (a.IsRune != b.IsRune) return a.IsRune ? -1 : 1;
                if (a.Stars != b.Stars) return a.Stars.CompareTo(b.Stars);
                return string.CompareOrdinal(a.DisplayName, b.DisplayName);
            });
        }

        CodexEntry EntryAt(int index)
        {
            while (_entries.Count <= index)
            {
                var entry = Instantiate(_entryTemplate, _content);
                entry.name = "CodexEntry_" + _entries.Count;
                _entries.Add(entry);
            }

            return _entries[index];
        }
    }
}
