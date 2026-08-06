using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Lumen
{
    public class LumenManager : MonoBehaviour
    {
        private List<LumenEffectPlayer> players = new List<LumenEffectPlayer>(100);
        private HashSet<LumenEffectPlayer> playersSet = new HashSet<LumenEffectPlayer>(100);

        private static LumenManager _instance;

        public static LumenManager Instance
        {
            get
            {
                if (!_instance)
                    _instance = FindFirstObjectByType<LumenManager>();

                if (!_instance)
                {
                    _instance = new GameObject("Lumen Manager").AddComponent<LumenManager>();
                    _instance.gameObject.hideFlags = HideFlags.HideAndDontSave;
                }

                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
                _instance = this;
            else if (_instance != this)
            {
                Debug.LogWarning($"Multiple instances of {nameof(LumenManager)} detected. This should not happen, destroying this new instance");
                if (Application.isPlaying)
                    Destroy(gameObject);
                else
                    DestroyImmediate(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
        }

        public void AddEffectPlayer(LumenEffectPlayer effectPlayer)
        {
            if (effectPlayer != null && playersSet.Add(effectPlayer))
                players.Add(effectPlayer);
        }

        public void RemoveEffectPlayer(LumenEffectPlayer effectPlayer)
        {
            if (effectPlayer == null || !playersSet.Remove(effectPlayer))
                return;

            int idxToRemove = players.IndexOf(effectPlayer);
            if (idxToRemove < 0)
                return;

            players[idxToRemove] = players[^1];
            players.RemoveAt(players.Count - 1);
        }

        void LateUpdate()
        {
            for (int i = players.Count - 1; i >= 0; i--)
            {
                LumenEffectPlayer player = players[i];

                if (player == null)
                {
                    playersSet.Remove(player);
                    players[i] = players[^1];
                    players.RemoveAt(players.Count - 1);
                    continue;
                }

                if (!player.isActiveAndEnabled)
                    continue;

                bool isFading = player.brightnessFade.isActive || player.scaleFade.isActive || player.colorFade.isActive;
                if (player.updateFrequency == LumenEffectPlayer.UpdateFrequency.Always || isFading)
                {
                    player.RedoEffect(fullRedraw: false);
                }
            }
        }
    }
}