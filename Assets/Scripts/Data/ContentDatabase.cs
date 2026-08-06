using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// The single door to all content. Nothing else is allowed to load assets by path or
    /// search the project â€” systems receive this and ask it.
    /// </summary>
    [CreateAssetMenu(fileName = "ContentDatabase", menuName = "Grimoire/Content Database")]
    public class ContentDatabase : ScriptableObject
    {
        [SerializeField] List<PieceDefinition> _pieces = new List<PieceDefinition>();
        [SerializeField] List<RecipeDefinition> _recipes = new List<RecipeDefinition>();
        [SerializeField] List<StatusDefinition> _statuses = new List<StatusDefinition>();
        [SerializeField] List<ReactionDefinition> _reactions = new List<ReactionDefinition>();
        [SerializeField] List<BuffDefinition> _buffs = new List<BuffDefinition>();

        [Tooltip("Kutukan yang ditempelkan musuh. Tipenya sama dengan buff, cuma nilainya negatif.")]
        [SerializeField] List<BuffDefinition> _debuffs = new List<BuffDefinition>();

        [SerializeField] List<EnemyArchetype> _archetypes = new List<EnemyArchetype>();
        [SerializeField] List<HeroLoadout> _heroes = new List<HeroLoadout>();

        readonly Dictionary<string, PieceDefinition> _byId = new Dictionary<string, PieceDefinition>();
        readonly List<PieceDefinition> _runes = new List<PieceDefinition>();
        readonly List<PieceDefinition> _droppableSkills = new List<PieceDefinition>();
        readonly List<PieceDefinition> _starTwo = new List<PieceDefinition>();
        bool _indexed;

        public IReadOnlyList<PieceDefinition> Pieces => _pieces;
        public IReadOnlyList<RecipeDefinition> Recipes => _recipes;
        public IReadOnlyList<StatusDefinition> Statuses => _statuses;
        public IReadOnlyList<ReactionDefinition> Reactions => _reactions;
        public IReadOnlyList<BuffDefinition> Buffs => _buffs;
        public IReadOnlyList<BuffDefinition> Debuffs => _debuffs;

        public IReadOnlyList<EnemyArchetype> Archetypes => _archetypes;
        public IReadOnlyList<HeroLoadout> Heroes => _heroes;

        /// <summary>The hero a run starts as. Null only when none have been authored.</summary>
        public HeroLoadout DefaultHero => _heroes.Count > 0 ? _heroes[0] : null;

        /// <summary>One curse for an enemy to carry, or null when none are authored yet.</summary>
        public BuffDefinition RandomDebuff()
        {
            if (_debuffs.Count == 0) return null;
            return _debuffs[Random.Range(0, _debuffs.Count)];
        }

        /// <summary>
        /// Weighted draw across every archetype unlocked by this wave. Returns null only when no
        /// archetypes are authored at all — the swarm then falls back to its plain defaults.
        /// </summary>
        public EnemyArchetype RollArchetype(int wave)
        {
            float total = 0f;
            for (int i = 0; i < _archetypes.Count; i++)
            {
                if (_archetypes[i] != null) total += _archetypes[i].WeightAt(wave);
            }

            if (total <= 0f) return null;

            float pick = Random.value * total;
            for (int i = 0; i < _archetypes.Count; i++)
            {
                var kind = _archetypes[i];
                if (kind == null) continue;

                pick -= kind.WeightAt(wave);
                if (pick <= 0f) return kind;
            }

            return _archetypes[_archetypes.Count - 1];
        }

        /// <summary>Slot index used by the runtime status array. -1 when the status is unknown.</summary>
        public int IndexOfStatus(StatusDefinition status)
        {
            if (status == null) return -1;
            for (int i = 0; i < _statuses.Count; i++)
            {
                if (_statuses[i] == status) return i;
            }

            return -1;
        }

        public StatusDefinition StatusById(string id)
        {
            for (int i = 0; i < _statuses.Count; i++)
            {
                if (_statuses[i] != null && _statuses[i].Id == id) return _statuses[i];
            }

            return null;
        }

        void OnEnable() => _indexed = false;

        void Index()
        {
            if (_indexed) return;

            _byId.Clear();
            _runes.Clear();
            _droppableSkills.Clear();
            _starTwo.Clear();

            for (int i = 0; i < _pieces.Count; i++)
            {
                var p = _pieces[i];
                if (p == null || string.IsNullOrEmpty(p.Id)) continue;

                _byId[p.Id] = p;

                // Rarity gates dropping for runes exactly like it does for skills.
                if (p.IsRune)
                {
                    if (p.CanDrop) _runes.Add(p);
                    else if (p.Stars == 2) _starTwo.Add(p);
                }
                else if (p.CanDrop) _droppableSkills.Add(p);
                else if (p.Stars == 2) _starTwo.Add(p);
            }

            _indexed = true;
        }

        public PieceDefinition ById(string id)
        {
            Index();
            return _byId.TryGetValue(id, out var piece) ? piece : null;
        }

        /// <summary>Kill / wave reward. Only 1-star pieces ever drop.</summary>
        public PieceDefinition RandomDrop(float runeShare)
        {
            Index();

            bool wantRune = Random.value < runeShare;
            var pool = wantRune ? _runes : _droppableSkills;
            if (pool.Count == 0) pool = _runes.Count > 0 ? _runes : _droppableSkills;
            if (pool.Count == 0) return null;

            return pool[Random.Range(0, pool.Count)];
        }

        /// <summary>One shop slot: mostly 1-star, with a small window for a 2-star hit.</summary>
        public PieceDefinition ShopRoll(float highChance)
        {
            Index();

            if (Random.value < highChance && _starTwo.Count > 0)
            {
                return _starTwo[Random.Range(0, _starTwo.Count)];
            }

            if (_runes.Count + _droppableSkills.Count == 0) return null;

            int index = Random.Range(0, _runes.Count + _droppableSkills.Count);
            return index < _runes.Count ? _runes[index] : _droppableSkills[index - _runes.Count];
        }

#if UNITY_EDITOR
        public void EditorSet(List<PieceDefinition> pieces, List<RecipeDefinition> recipes)
        {
            _pieces = pieces;
            _recipes = recipes;
            _indexed = false;
        }

        public void EditorSetAilments(List<StatusDefinition> statuses, List<ReactionDefinition> reactions,
            List<BuffDefinition> buffs = null)
        {
            _statuses = statuses;
            _reactions = reactions;
            if (buffs != null) _buffs = buffs;
            _indexed = false;
        }

        public void EditorSetDebuffs(List<BuffDefinition> debuffs)
        {
            _debuffs = debuffs;
            _indexed = false;
        }

        public void EditorSetArchetypes(List<EnemyArchetype> archetypes)
        {
            _archetypes = archetypes;
            _indexed = false;
        }

        public void EditorSetHeroes(List<HeroLoadout> heroes)
        {
            _heroes = heroes;
            _indexed = false;
        }
#endif

        void OnValidate()
        {
            _indexed = false;

            var seen = new HashSet<string>();

            for (int i = 0; i < _pieces.Count; i++)
            {
                var p = _pieces[i];
                if (p == null)
                {
                    Debug.LogError($"[{name}] ada slot piece kosong di index {i}.", this);
                    continue;
                }

                if (string.IsNullOrEmpty(p.Id))
                {
                    Debug.LogError($"[{name}] '{p.name}' belum punya Id.", p);
                    continue;
                }

                if (!seen.Add(p.Id)) Debug.LogError($"[{name}] Id ganda: '{p.Id}'.", p);
            }

            for (int i = 0; i < _recipes.Count; i++)
            {
                var r = _recipes[i];
                if (r == null)
                {
                    Debug.LogError($"[{name}] ada slot resep kosong di index {i}.", this);
                    continue;
                }

                if (!r.IsValid) Debug.LogError($"[{name}] resep '{r.name}' belum lengkap.", r);
            }
        }
    }
}
