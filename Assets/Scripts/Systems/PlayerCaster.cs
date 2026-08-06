using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// The stationary caster. Reads the compiled loadout only â€” it never looks at the grid.
    /// Owns the tiny FX pools so the prototype stays in one place.
    /// </summary>
    public class PlayerCaster : MonoBehaviour
    {
        class Projectile
        {
            public Transform T;
            public Vector3 Dir;
            public float Life;
            public float Damage;
            public StatusDefinition Status;
            public float StatusDuration;
            public int Points;

            /// <summary>Carried so the damage meter can name the skill that fired it.</summary>
            public string SourceName;

            public bool Active;
        }

        class Flash
        {
            public Transform T;
            public float Life;
            public float MaxLife;
            public float TargetScale;
            public bool Active;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public Grimoire Book { get; private set; }

        // Base stats, filled from GameBalance. Sigils placed in the grimoire raise these.
        public float BaseMaxHp = 100f;
        public float BaseMaxMana = 60f;
        public float BaseManaRegen = 5f;
        public float BaseHpRegen = 0f;

        public float Hp = 100f;
        public float Mana = 60f;
        public bool Alive = true;

        public float MaxHp => BaseMaxHp + Total(StatKind.MaxHp);
        public float MaxMana => BaseMaxMana + Total(StatKind.MaxMana);
        public float ManaRegen => BaseManaRegen + Total(StatKind.ManaRegen);
        public float HpRegen => BaseHpRegen + Total(StatKind.HpRegen);

        // ---------- buff pemain ----------

        public const int BuffSlots = 6;

        public struct BuffSlot
        {
            public BuffDefinition Def;
            public float Remaining;
        }

        /// <summary>
        /// Debuffs get their own four slots rather than sharing the buff array.
        ///
        /// Sharing looked cheaper and is a trap: while surrounded you are being cursed constantly,
        /// so a shared array would evict the reaction buffs exactly when the wave is at its worst.
        /// The whole game is built on reaction -> buff -> bigger hit, and it must not switch itself
        /// off under pressure.
        /// </summary>
        public const int DebuffSlots = 4;

        readonly BuffSlot[] _buffs = new BuffSlot[BuffSlots];
        readonly BuffSlot[] _debuffs = new BuffSlot[DebuffSlots];
        readonly float[] _buffStats = new float[(int)StatKind.Count];
        readonly float[] _debuffStats = new float[(int)StatKind.Count];

        public BuffSlot[] Buffs => _buffs;
        public BuffSlot[] Debuffs => _debuffs;

        /// <summary>Stat akhir: grimoire (permanen selama tersusun) + buff + debuff (sementara).</summary>
        public float Total(StatKind kind) =>
            Book.Stat(kind) + _buffStats[(int)kind] + _debuffStats[(int)kind];

        public void ApplyBuff(BuffDefinition def)
        {
            if (def == null) return;

            if (def.IsDebuff) Land(def, _debuffs, DebuffSlots, DebuffDuration(def));
            else Land(def, _buffs, BuffSlots, def.Duration);

            Recompute();
        }

        /// <summary>Curses land through here so resistance is applied in exactly one place.</summary>
        public void ApplyDebuff(BuffDefinition def)
        {
            if (def == null || !def.IsDebuff) return;

            Land(def, _debuffs, DebuffSlots, DebuffDuration(def));
            Recompute();
        }

        float DebuffDuration(BuffDefinition def)
        {
            if (!def.ResistShortensDuration) return def.Duration;

            // Floored, never zeroed: full immunity would make the counters mandatory rather than
            // a choice, and a debuff you cannot feel is content that may as well not exist.
            float resist = Mathf.Clamp(Total(StatKind.DebuffResist), 0f, 0.8f);
            return def.Duration * (1f - resist);
        }

        static void Land(BuffDefinition def, BuffSlot[] slots, int count, float duration)
        {
            int chosen = -1;
            float shortest = float.MaxValue;
            int weakest = 0;

            for (int i = 0; i < count; i++)
            {
                if (slots[i].Def == def) { chosen = i; break; }
                if (slots[i].Def == null) { chosen = i; break; }

                if (slots[i].Remaining >= shortest) continue;
                shortest = slots[i].Remaining;
                weakest = i;
            }

            if (chosen < 0) chosen = weakest;

            slots[chosen].Def = def;
            slots[chosen].Remaining = duration;   // refresh, tidak menumpuk
        }

        /// <summary>Drops every curse. The whole point of <see cref="CastKind.Cleanse"/>.</summary>
        public bool ClearDebuffs()
        {
            bool any = false;

            for (int i = 0; i < DebuffSlots; i++)
            {
                if (_debuffs[i].Def == null) continue;
                _debuffs[i].Def = null;
                any = true;
            }

            if (any) Recompute();
            return any;
        }

        void TickBuffs(float dt)
        {
            bool changed = Expire(_buffs, BuffSlots, dt) | Expire(_debuffs, DebuffSlots, dt);
            if (changed) Recompute();
        }

        static bool Expire(BuffSlot[] slots, int count, float dt)
        {
            bool changed = false;

            for (int i = 0; i < count; i++)
            {
                if (slots[i].Def == null) continue;

                slots[i].Remaining -= dt;
                if (slots[i].Remaining > 0f) continue;

                slots[i].Def = null;
                changed = true;
            }

            return changed;
        }

        void Recompute()
        {
            Sum(_buffs, BuffSlots, _buffStats);
            Sum(_debuffs, DebuffSlots, _debuffStats);
        }

        static void Sum(BuffSlot[] slots, int count, float[] into)
        {
            System.Array.Clear(into, 0, into.Length);

            for (int i = 0; i < count; i++)
            {
                var def = slots[i].Def;
                if (def == null || def.Mods == null) continue;

                for (int m = 0; m < def.Mods.Length; m++)
                {
                    var mod = def.Mods[m];
                    if (mod.Type == StatKind.None || mod.Type == StatKind.Count) continue;
                    into[(int)mod.Type] += mod.Value;
                }
            }
        }

        // Buff berubah saat wave berjalan, jadi ini dipakai SAAT CAST — bukan saat kompilasi grid.
        //
        // Batas atasnya 2, bukan 1. Dulu 1, dan itu berarti nilai NEGATIF tidak berpengaruh sama
        // sekali: sebuah debuff "cast jadi lambat" akan tampil di HUD, lalu tidak melakukan apa pun.
        float BuffDamageMul => Mathf.Max(0.05f, 1f + _buffStats[(int)StatKind.DamagePct]
                                                  + _debuffStats[(int)StatKind.DamagePct]);
        float BuffCooldownMul => Mathf.Clamp(1f - _buffStats[(int)StatKind.CooldownPct]
                                                - _debuffStats[(int)StatKind.CooldownPct], 0.25f, 2f);
        float BuffAreaMul => Mathf.Max(0.2f, 1f + _buffStats[(int)StatKind.AreaPct]
                                                + _debuffStats[(int)StatKind.AreaPct]);
        float BuffRangeMul => Mathf.Max(0.2f, 1f + _buffStats[(int)StatKind.RangePct]
                                                 + _debuffStats[(int)StatKind.RangePct]);

        /// <summary>Mana cost from the temporary layer. The grid's own share lives on Grimoire.</summary>
        float BuffManaCostMul => Mathf.Clamp(1f - _buffStats[(int)StatKind.ManaCostPct]
                                                - _debuffStats[(int)StatKind.ManaCostPct], 0.2f, 2f);

        /// <summary>Satu lemparan crit. Dipanggil sekali per cast, bukan per musuh.</summary>
        float RollCrit()
        {
            float chance = Total(StatKind.CritChance);
            if (chance <= 0f || Random.value > chance) return 1f;

            return 1.5f + Total(StatKind.CritDamage);
        }

        /// <summary>Raised with the rune instance that just fired, so the UI can pulse it.</summary>
        public System.Action<RuneInstance> OnCast;

        EnemyManager _enemies;
        ContentDatabase _db;
        GameBalance _balance;

        // A triggered cast can trigger again, but only a few links deep. Without this a chain
        // build locks the frame.
        const int MaxTriggerDepth = 3;
        int _triggerDepth;
        Transform _fxRoot;
        Material _fxMaterial;
        MaterialPropertyBlock _mpb;

        readonly List<Projectile> _projectiles = new List<Projectile>(64);
        readonly List<Flash> _flashes = new List<Flash>(32);

        // Twelve, not four. The old buffer was shorter than the longest chain skill in the book
        // (Frost Prism hits five), so two of its links were silently thrown away every cast.
        readonly EnemyManager.Enemy[] _chainBuffer = new EnemyManager.Enemy[12];

        BoltPool _bolts;
        LineRenderer _rangeRing;

        /// <summary>Draws a ground ring showing a skill's reach while you hover it.</summary>
        public void ShowRange(float radius, Color color)
        {
            if (_rangeRing == null) return;

            if (radius <= 0.05f)
            {
                _rangeRing.enabled = false;
                return;
            }

            const int segments = 48;
            _rangeRing.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                _rangeRing.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.06f, Mathf.Sin(a) * radius));
            }

            _rangeRing.startColor = _rangeRing.endColor = new Color(color.r, color.g, color.b, 0.9f);
            _rangeRing.enabled = true;
        }

        public void HideRange()
        {
            if (_rangeRing != null) _rangeRing.enabled = false;
        }

        void BuildRangeRing()
        {
            var go = new GameObject("RangeRing");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, -0.85f, 0f);

            _rangeRing = go.AddComponent<LineRenderer>();
            _rangeRing.useWorldSpace = false;
            _rangeRing.loop = true;
            _rangeRing.widthMultiplier = 0.12f;

            // The bolt material, not the FX one: URP/Unlit ignores LineRenderer vertex colours, so
            // on the shared FX material this ring drew white whatever colour it was handed.
            _rangeRing.sharedMaterial = _bolts.Material;
            _rangeRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _rangeRing.enabled = false;
        }

        public void Init(EnemyManager enemies, ContentDatabase database, GameBalance balance)
        {
            _enemies = enemies;
            _db = database;
            _balance = balance;

            Book = new Grimoire(database);

            BaseMaxHp = balance.BaseMaxHp;
            BaseMaxMana = balance.BaseMaxMana;
            BaseManaRegen = balance.BaseManaRegen;
            BaseHpRegen = balance.BaseHpRegen;
            Hp = BaseMaxHp;
            Mana = BaseMaxMana;
            _mpb = new MaterialPropertyBlock();

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            _fxMaterial = new Material(shader) { enableInstancing = true };

            _fxRoot = new GameObject("FX").transform;
            _fxRoot.SetParent(transform.parent, false);

            _bolts = new BoltPool(_fxRoot);

            _enemies.OnReaction += OnReactionFired;
            _enemies.OnStatusApplied += OnEnemyStatusApplied;

            BuildRangeRing();
        }

        void OnReactionFired(Vector3 pos, ReactionDefinition rx)
        {
            SpawnFlash(pos, rx.BurstRadius * 2f, 0.35f, rx.FlashColor);

            // Inilah rantai intinya: reaksi -> buff -> skill berikutnya lebih kuat.
            if (rx.GrantBuff != null) ApplyBuff(rx.GrantBuff);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (Alive)
            {
                Mana = Mathf.Min(MaxMana, Mana + ManaRegen * dt);
                if (HpRegen > 0f) Hp = Mathf.Min(MaxHp, Hp + HpRegen * dt);
            }

            // Spells only fire while a wave is running â€” between waves the book goes quiet.
            if (Alive && _enemies != null && _enemies.WaveActive) TickSpells(dt);

            TickBuffs(dt);
            TickProjectiles(dt);
            TickDescents(dt);
            TickZones(dt);
            TickFlashes(dt);
            _bolts.Tick(dt);
        }

        /// <summary>Called when a wave starts so every spell opens on a ready cooldown.</summary>
        public void ResetCooldowns()
        {
            Mana = MaxMana;
            var spells = Book.Spells;
            for (int i = 0; i < spells.Count; i++) spells[i].Source.CdTimer = 0f;
        }

        void TickSpells(float dt)
        {
            var spells = Book.Spells;
            for (int i = 0; i < spells.Count; i++)
            {
                var s = spells[i];
                s.Source.CdTimer -= dt;

                // Trigger skills never fire on their own — they wait for an ailment threshold.
                if (s.Source.Def.Trigger != CastTrigger.Cooldown) continue;
                if (s.Source.CdTimer > 0f) continue;

                // Cooldown is ready but the book is dry â€” hold the cast until mana comes back.
                float cost = s.Source.Def.ManaCost * Book.ManaCostMultiplier * BuffManaCostMul;
                if (Mana < cost) continue;

                // No valid target: stay ready and idle. The cooldown must not keep spinning.
                if (!Cast(s)) continue;

                Mana -= cost;
                s.Source.CdTimer = s.Cooldown * BuffCooldownMul;
                OnCast?.Invoke(s.Source);
            }
        }

        /// <summary>
        /// An ailment just landed. Every trigger skill watching that ailment checks whether the
        /// target crossed its threshold, and detonates on that enemy if so.
        /// </summary>
        void OnEnemyStatusApplied(EnemyManager.Enemy enemy, int statusIndex, int points)
        {
            if (!Alive || _db == null || _triggerDepth >= MaxTriggerDepth) return;

            var spells = Book.Spells;

            for (int i = 0; i < spells.Count; i++)
            {
                var s = spells[i];
                var def = s.Source.Def;

                if (def.Trigger != CastTrigger.StatusThreshold) continue;
                if (def.TriggerStatus == null) continue;
                if (_db.IndexOfStatus(def.TriggerStatus) != statusIndex) continue;
                if (points < def.TriggerPoints) continue;
                if (s.Source.CdTimer > 0f) continue;
                float triggerCost = def.ManaCost * Book.ManaCostMultiplier * BuffManaCostMul;
                if (Mana < triggerCost) continue;

                _triggerDepth++;
                bool fired = CastAt(s, enemy);
                _triggerDepth--;

                if (!fired) continue;

                if (def.ConsumeTriggerPoints) _enemies.ConsumePoints(enemy, statusIndex, def.TriggerPoints);

                Mana -= triggerCost;
                s.Source.CdTimer = s.Cooldown * BuffCooldownMul;
                OnCast?.Invoke(s.Source);
            }
        }

        /// <summary>Same effects as a normal cast, but centred on the enemy that crossed the threshold.</summary>
        bool CastAt(CompiledSpell spell, EnemyManager.Enemy target)
        {
            if (target == null || !target.Alive) return false;

            var def = spell.Source.Def;
            Vector3 at = target.Pos;
            int points = def.AppliedPoints + Book.BonusAilmentPoints;

            switch (def.Kind)
            {
                case CastKind.Nova:
                {
                    float radius = spell.Radius * BuffAreaMul;
                    _enemies.DamageArea(at, radius, spell.Damage * BuffDamageMul * RollCrit(),
                        def.AppliedStatus, def.StatusDuration, points, true, def.DisplayName);
                    SpawnFlash(at, radius * 2f, 0.3f, def.Color);
                    return true;
                }

                case CastKind.Chain:
                    return CastChain(spell, def, at, spell.Damage * BuffDamageMul * RollCrit());

                case CastKind.Projectile:
                    _enemies.Damage(target, spell.Damage * BuffDamageMul * RollCrit(),
                        def.AppliedStatus, def.StatusDuration, points, true, def.DisplayName);
                    SpawnFlash(at, 1.8f, 0.2f, def.Color);
                    return true;

                case CastKind.Heal:
                    if (Hp >= MaxHp) return false;
                    Hp = Mathf.Min(MaxHp, Hp + spell.Damage);
                    SpawnFlash(transform.position, 3.2f, 0.3f, def.Color);
                    return true;
            }

            return false;
        }

        /// <summary>Returns false when the spell had no valid target and did not actually fire.</summary>
        bool Cast(CompiledSpell spell)
        {
            var def = spell.Source.Def;

            switch (def.Kind)
            {
                case CastKind.Projectile:
                {
                    var target = _enemies.Nearest(transform.position, spell.Range);
                    if (target == null) return false;

                    Vector3 dir = target.Pos - transform.position;
                    dir.y = 0f;
                    FireProjectile(dir.normalized, spell.Damage * BuffDamageMul * RollCrit(), def, def.Color);
                    return true;
                }

                case CastKind.Nova:
                {
                    float radius = spell.Radius * BuffAreaMul;

                    // Do not detonate into an empty field â€” that was burning mana for nothing.
                    if (_enemies.Nearest(transform.position, radius) == null) return false;

                    // Buffs and crit apply here exactly like they do to every other cast. They did
                    // not before, which quietly excluded the heaviest skills in the book — every
                    // Nova, up to Doom Nova — from the reaction -> buff -> bigger hit loop.
                    _enemies.DamageArea(transform.position, radius,
                        spell.Damage * BuffDamageMul * RollCrit(),
                        def.AppliedStatus, def.StatusDuration, AilmentPoints(def), true, def.DisplayName);

                    SpawnFlash(transform.position, radius * 2f, 0.3f, def.Color);
                    return true;
                }

                case CastKind.Chain:
                    return CastChain(spell, def, transform.position, spell.Damage * BuffDamageMul * RollCrit());

                case CastKind.Heal:
                {
                    if (Hp >= MaxHp) return false;

                    Hp = Mathf.Min(MaxHp, Hp + spell.Damage);
                    SpawnFlash(transform.position, 3.2f, 0.3f, def.Color);
                    return true;
                }

                case CastKind.Cleanse:
                {
                    // Holds its cooldown and its mana when there is nothing to cleanse, the same way
                    // Heal refuses to fire at full health. A cleanser that burns itself on an empty
                    // debuff bar is never up when a curse actually lands.
                    if (!ClearDebuffs()) return false;

                    if (spell.Damage > 0f) Hp = Mathf.Min(MaxHp, Hp + spell.Damage);
                    SpawnFlash(transform.position, 4f, 0.35f, def.Color);
                    return true;
                }

                case CastKind.AreaAtTarget:
                {
                    // Land on the thickest part of the crowd, not on top of ourselves.
                    var cluster = _enemies.BestCluster(transform.position, spell.Range, spell.Radius);
                    if (cluster == null) return false;

                    // Crit is rolled here, at cast, and carried down with the shot — rolling it on
                    // impact would break the one-roll-per-cast rule the whole game is tuned around.
                    LaunchDescent(cluster.Pos, spell, def,
                        spell.Damage * BuffDamageMul * RollCrit(), spell.Radius * BuffAreaMul);
                    return true;
                }

                case CastKind.Line:
                {
                    var target = _enemies.Nearest(transform.position, spell.Range);
                    if (target == null) return false;

                    Vector3 dir = target.Pos - transform.position;
                    dir.y = 0f;

                    float halfWidth = Mathf.Max(0.4f, spell.Radius);
                    float length = spell.Range * BuffRangeMul;

                    _enemies.DamageLine(transform.position, dir, length, halfWidth,
                        spell.Damage * BuffDamageMul * RollCrit(),
                        def.AppliedStatus, def.StatusDuration, AilmentPoints(def), def.DisplayName);

                    _bolts.Beam(transform.position, transform.position + dir.normalized * length,
                        def.Color, halfWidth * 1.6f);
                    return true;
                }

                case CastKind.Zone:
                {
                    var cluster = _enemies.BestCluster(transform.position, spell.Range, spell.Radius);
                    if (cluster == null) return false;

                    // Zones fall out of the sky too — the pool forms where the glob lands.
                    LaunchDescent(cluster.Pos, spell, def, 0f, 0f);
                    return true;
                }
            }

            return false;
        }

        int AilmentPoints(PieceDefinition def) => def.AppliedPoints + Book.BonusAilmentPoints;

        // ---------- chain lightning ----------

        /// <summary>
        /// Hops enemy to enemy and draws a bolt along every hop. Shared by the cooldown cast and the
        /// ailment-triggered one so both look and reach the same.
        /// </summary>
        bool CastChain(CompiledSpell spell, PieceDefinition def, Vector3 origin, float damage)
        {
            int count = _enemies.ChainFrom(origin, spell.Range, _balance.ChainJumpRange,
                _chainBuffer, def.Hits);

            if (count == 0) return false;

            Vector3 from = origin;
            int points = AilmentPoints(def);

            for (int i = 0; i < count; i++)
            {
                var target = _chainBuffer[i];
                Vector3 to = target.Pos;

                _bolts.Arc(from, to, def.Color);
                SpawnFlash(to, 1.6f, 0.15f, def.Color);

                _enemies.Damage(target, damage, def.AppliedStatus, def.StatusDuration,
                    points, true, def.DisplayName, from);

                from = to;
            }

            return true;
        }

        // ---------- sky-falling casts ----------

        /// <summary>
        /// A shot that falls onto a ground point and only pays out when it lands. Area skills and
        /// zones both use it, which is why the payload is split into a damage half and a zone half.
        /// </summary>
        class Descent
        {
            public Transform T;
            public TrailRenderer Trail;
            public Renderer R;
            public Vector3 Target;

            public CompiledSpell Spell;
            public PieceDefinition Def;
            public float Damage;
            public float Radius;
            public bool IsZone;

            public bool Active;
        }

        readonly List<Descent> _descents = new List<Descent>(8);

        void LaunchDescent(Vector3 at, CompiledSpell spell, PieceDefinition def,
            float damage, float radius)
        {
            Descent d = null;
            for (int i = 0; i < _descents.Count; i++)
            {
                if (_descents[i].Active) continue;
                d = _descents[i];
                break;
            }

            if (d == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Descent";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);

                go.transform.SetParent(_fxRoot, false);
                go.transform.localScale = Vector3.one * 0.85f;

                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = _fxMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = _bolts.Material;
                trail.time = 0.22f;
                trail.widthMultiplier = 0.7f;
                trail.numCapVertices = 2;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;

                d = new Descent { T = go.transform, Trail = trail, R = r };
                _descents.Add(d);
            }

            d.Target = new Vector3(at.x, 0.35f, at.z);
            d.Spell = spell;
            d.Def = def;
            d.Damage = damage;
            d.Radius = radius;
            d.IsZone = def.Kind == CastKind.Zone;
            d.Active = true;

            // A slight slant reads as "thrown from somewhere" rather than spawned directly overhead.
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float lean = _balance.SkyFallHeight * 0.22f;
            d.T.position = d.Target + Vector3.up * _balance.SkyFallHeight +
                           new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * lean;

            _mpb.SetColor(BaseColorId, def.Color);
            d.R.SetPropertyBlock(_mpb);

            d.Trail.startColor = new Color(def.Color.r, def.Color.g, def.Color.b, 0.9f);
            d.Trail.endColor = new Color(def.Color.r, def.Color.g, def.Color.b, 0f);

            // Pooled trails remember where they were last used — without this the shot arrives
            // dragging a streak from the previous impact clean across the map.
            d.T.gameObject.SetActive(true);
            d.Trail.Clear();
        }

        void TickDescents(float dt)
        {
            float step = _balance.SkyFallSpeed * dt;

            for (int i = 0; i < _descents.Count; i++)
            {
                var d = _descents[i];
                if (!d.Active) continue;

                Vector3 delta = d.Target - d.T.position;
                if (delta.sqrMagnitude <= step * step)
                {
                    Impact(d);
                    continue;
                }

                d.T.position += delta.normalized * step;
            }
        }

        void Impact(Descent d)
        {
            d.Active = false;
            d.T.position = d.Target;
            d.T.gameObject.SetActive(false);

            if (d.IsZone)
            {
                SpawnZone(d.Target, d.Spell, d.Def);
                return;
            }

            _enemies.DamageArea(d.Target, d.Radius, d.Damage, d.Def.AppliedStatus,
                d.Def.StatusDuration, AilmentPoints(d.Def), true, d.Def.DisplayName);

            SpawnFlash(d.Target, d.Radius * 2f, 0.3f, d.Def.Color);
        }

        /// <summary>A pop where an enemy died. Wired from the composition root, not from combat.</summary>
        public void DeathBurst(Vector3 at)
        {
            SpawnFlash(at, 2.2f, 0.18f, new Color(0.95f, 0.72f, 0.55f));
        }

        void FireProjectile(Vector3 dir, float damage, PieceDefinition source, Color color)
        {
            Projectile p = null;
            for (int i = 0; i < _projectiles.Count; i++)
            {
                if (!_projectiles[i].Active)
                {
                    p = _projectiles[i];
                    break;
                }
            }

            if (p == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Projectile";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.SetParent(_fxRoot, false);
                go.transform.localScale = Vector3.one * 0.45f;

                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = _fxMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                p = new Projectile { T = go.transform };
                _projectiles.Add(p);
            }

            _mpb.SetColor(BaseColorId, color);
            p.T.GetComponent<Renderer>().SetPropertyBlock(_mpb);

            p.T.position = transform.position;
            p.T.gameObject.SetActive(true);
            p.Dir = dir;
            p.Life = 2.2f;
            p.Damage = damage;
            p.Status = source.AppliedStatus;
            p.StatusDuration = source.StatusDuration;
            p.Points = source.AppliedPoints + Book.BonusAilmentPoints;
            p.SourceName = source.DisplayName;
            p.Active = true;
        }

        void TickProjectiles(float dt)
        {
            const float speed = 16f;

            for (int i = 0; i < _projectiles.Count; i++)
            {
                var p = _projectiles[i];
                if (!p.Active) continue;

                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    Retire(p);
                    continue;
                }

                p.T.position += p.Dir * (speed * dt);

                var hit = _enemies.Nearest(p.T.position, 0.75f);
                if (hit != null)
                {
                    _enemies.Damage(hit, p.Damage, p.Status, p.StatusDuration, p.Points, true, p.SourceName);
                    SpawnFlash(p.T.position, 1.4f, 0.15f, Color.white);
                    Retire(p);
                }
            }
        }

        void Retire(Projectile p)
        {
            p.Active = false;
            p.T.gameObject.SetActive(false);
        }

        /// <summary>Ceiling on the flash pool. Every kill pops one, so at 150 enemies a wave this
        /// would otherwise grow without bound and never shrink.</summary>
        const int MaxFlashes = 96;

        void SpawnFlash(Vector3 pos, float size, float life, Color color)
        {
            Flash f = null;
            for (int i = 0; i < _flashes.Count; i++)
            {
                if (!_flashes[i].Active)
                {
                    f = _flashes[i];
                    break;
                }
            }

            if (f == null && _flashes.Count >= MaxFlashes)
            {
                // Saturated: recycle whatever is closest to finishing.
                f = _flashes[0];
                for (int i = 1; i < _flashes.Count; i++)
                {
                    if (_flashes[i].Life < f.Life) f = _flashes[i];
                }
            }

            if (f == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Flash";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.SetParent(_fxRoot, false);

                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = _fxMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                f = new Flash { T = go.transform };
                _flashes.Add(f);
            }

            _mpb.SetColor(BaseColorId, color);
            f.T.GetComponent<Renderer>().SetPropertyBlock(_mpb);

            f.T.position = new Vector3(pos.x, 0.35f, pos.z);
            f.T.localScale = Vector3.one * 0.2f;
            f.T.gameObject.SetActive(true);
            f.Life = life;
            f.MaxLife = life;
            f.TargetScale = size;
            f.Active = true;
        }

        // ---------- zones ----------

        class Zone
        {
            public Transform T;
            public Vector3 Pos;
            public float Radius;
            public float Remaining;
            public float TickTimer;
            public float TickInterval;
            public float Damage;
            public StatusDefinition Status;
            public float StatusDuration;
            public int Points;
            public string SourceName;
            public bool Active;
        }

        readonly List<Zone> _zones = new List<Zone>(8);

        void SpawnZone(Vector3 at, CompiledSpell spell, PieceDefinition def)
        {
            Zone z = null;
            for (int i = 0; i < _zones.Count; i++)
            {
                if (_zones[i].Active) continue;
                z = _zones[i];
                break;
            }

            if (z == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "Zone";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);

                go.transform.SetParent(_fxRoot, false);
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = _fxMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                z = new Zone { T = go.transform };
                _zones.Add(z);
            }

            _mpb.SetColor(BaseColorId, def.Color);
            z.T.GetComponent<Renderer>().SetPropertyBlock(_mpb);

            z.Pos = new Vector3(at.x, 0.06f, at.z);
            z.Radius = spell.Radius * BuffAreaMul;
            z.Remaining = Mathf.Max(0.5f, def.ZoneDuration);
            z.TickInterval = Mathf.Max(0.1f, def.ZoneTickInterval);
            z.TickTimer = 0f;
            z.Damage = spell.Damage * BuffDamageMul;
            z.Status = def.AppliedStatus;
            z.StatusDuration = def.StatusDuration;
            z.Points = AilmentPoints(def);
            z.SourceName = def.DisplayName;
            z.Active = true;

            z.T.position = z.Pos;
            z.T.localScale = new Vector3(z.Radius * 2f, 0.03f, z.Radius * 2f);
            z.T.gameObject.SetActive(true);
        }

        void TickZones(float dt)
        {
            for (int i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                if (!z.Active) continue;

                z.Remaining -= dt;
                if (z.Remaining <= 0f)
                {
                    z.Active = false;
                    z.T.gameObject.SetActive(false);
                    continue;
                }

                z.TickTimer -= dt;
                if (z.TickTimer > 0f) continue;

                z.TickTimer = z.TickInterval;
                _enemies.DamageArea(z.Pos, z.Radius, z.Damage, z.Status, z.StatusDuration, z.Points, true, z.SourceName);
            }
        }

        void TickFlashes(float dt)
        {
            for (int i = 0; i < _flashes.Count; i++)
            {
                var f = _flashes[i];
                if (!f.Active) continue;

                f.Life -= dt;
                if (f.Life <= 0f)
                {
                    f.Active = false;
                    f.T.gameObject.SetActive(false);
                    continue;
                }

                float t = 1f - f.Life / f.MaxLife;
                float scale = Mathf.Lerp(0.2f, f.TargetScale, t);
                f.T.localScale = new Vector3(scale, 0.25f, scale);
            }
        }

        /// <summary>Sustained contact damage, already scaled by delta time by the caller.</summary>
        public void TakeDamage(float amount)
        {
            if (!Alive) return;

            // Defense is flat damage reduction, floored so it can never make you immortal.
            Drain(Mathf.Max(amount * 0.1f, amount - Total(StatKind.Defense) * Time.deltaTime));
        }

        /// <summary>
        /// A single hit, like an enemy shot. Needs its own curve: <see cref="TakeDamage"/> subtracts
        /// defence scaled by delta time, which is correct for a per-frame trickle and comes out to
        /// almost nothing against one burst — a fully armoured caster would take shots at full price.
        /// </summary>
        public void TakeHit(float amount)
        {
            if (!Alive || amount <= 0f) return;

            float defense = Mathf.Max(0f, Total(StatKind.Defense));
            float reduction = Mathf.Clamp(defense / (defense + 20f), 0f, 0.75f);
            Drain(amount * (1f - reduction));
        }

        void Drain(float amount)
        {
            Hp -= amount;
            if (Hp > 0f) return;

            Hp = 0f;
            Alive = false;
            if (_enemies != null) _enemies.Running = false;
        }
    }
}
