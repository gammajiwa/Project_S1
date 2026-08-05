using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Damage per source across a whole run. This is the only thing that can prove the design
    /// targets — "at wave 10 at least 25% of damage comes from reactions, and no single source
    /// goes past 40%" — so it deliberately holds raw totals rather than a smoothed readout.
    ///
    /// Plain C#: it takes numbers in and hands a string back. The HUD only owns the label.
    /// </summary>
    public class DamageMeter
    {
        readonly List<string> _names = new List<string>(16);
        readonly List<float> _values = new List<float>(16);
        readonly List<int> _ranked = new List<int>(8);
        readonly StringBuilder _sb = new StringBuilder(256);

        float _total;

        public float Total => _total;

        public void Record(string source, float amount)
        {
            if (string.IsNullOrEmpty(source) || amount <= 0f) return;

            _total += amount;

            for (int i = 0; i < _names.Count; i++)
            {
                if (_names[i] != source) continue;
                _values[i] += amount;
                return;
            }

            _names.Add(source);
            _values.Add(amount);
        }

        public void Reset()
        {
            _names.Clear();
            _values.Clear();
            _total = 0f;
        }

        /// <summary>Top sources as display text, or empty while nothing has been dealt yet.</summary>
        public string BuildSummary(int topCount)
        {
            if (_total <= 0f) return string.Empty;

            _sb.Length = 0;
            _sb.Append("DAMAGE  (total ").Append(Mathf.RoundToInt(_total)).Append(")\n");

            // Picked indices are tracked per call. The old version kept ranks between frames, so a
            // source that slipped down the order stayed filtered out and vanished from the list.
            _ranked.Clear();

            for (int rank = 0; rank < topCount; rank++)
            {
                int best = -1;
                float bestValue = 0f;

                for (int i = 0; i < _values.Count; i++)
                {
                    if (_values[i] <= bestValue) continue;
                    if (_ranked.Contains(i)) continue;

                    bestValue = _values[i];
                    best = i;
                }

                if (best < 0) break;

                _ranked.Add(best);
                int pct = Mathf.RoundToInt(_values[best] / _total * 100f);
                _sb.Append(_names[best]).Append("  ").Append(pct).Append("%\n");
            }

            return _sb.ToString();
        }
    }
}
