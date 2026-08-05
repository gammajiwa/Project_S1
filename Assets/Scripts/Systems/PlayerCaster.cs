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

        readonly BuffSlot[] _buffs = new BuffSlot[BuffSlots];
        readonly float[] _buffStats = new float[(int)StatKind.Count];

        public BuffSlot[] Buffs => _buffs;

        /// <summary>Stat akhir: dari grimoire (permanen selama tersusun) + buff (sementara).</summary>
        public float Total(StatKind kind) => Book.Stat(kind) + _buffStats[(int)kind];

        public void ApplyBuff(BuffDefinition def)
        {
            if (def == null) return;

            int slot = -1;
            float shortest = float.MaxValue;
            int weakest = 0;

            for (int i = 0; i < BuffSlots; i++)
            {
                if (_buffs[i].Def == def) { slot = i; break; }
                if (_buffs[i].Def == null) { slot = i; break; }

                if (_buffs[i].Remaining >= shortest) continue;
                shortest = _buffs[i].Remaining;
                weakest = i;
            }

            if (slot < 0) slot = weakest;

            _buffs[slot].Def = def;
            _buffs[slot].Remaining = def.Duration;   // refresh, tidak menumpuk
            RecomputeBuffStats();
        }

        void TickBuffs(float dt)
        {
            bool changed = false;

            for (int i = 0; i < BuffSlots; i++)
            {
                if (_buffs[i].Def == null) continue;

                _buffs[i].Remaining -= dt;
                if (_buffs[i].Remaining > 0f) continue;

                _buffs[i].Def = null;
                changed = true;
            }

            if (changed) RecomputeBuffStats();
        }

        void RecomputeBuffStats()
        {
            System.Array.Clear(_buffStats, 0, _buffStats.Length);

            for (int i = 0; i < BuffSlots; i++)
            {
                var def = _buffs[i].Def;
                if (def == null || def.Mods == null) continue;

                for (int m = 0; m < def.Mods.Length; m++)
                {
                    var mod = def.Mods[m];
                    if (mod.Type == StatKind.None || mod.Type == StatKind.Count) continue;
                    _buffStats[(int)mod.Type] += mod.Value;
                }
            }
        }

        // Buff berubah saat wave berjalan, jadi ini dipakai SAAT CAST — bukan saat kompilasi grid.
        float BuffDamageMul => 1f + _buffStats[(int)StatKind.DamagePct];
        float BuffCooldownMul => Mathf.Clamp(1f - _buffStats[(int)StatKind.CooldownPct], 0.25f, 1f);
        float BuffAreaMul => 1f + _buffStats[(int)StatKind.AreaPct];
        float BuffRangeMul => 1f + _buffStats[(int)StatKind.RangePct];

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

        // A triggered cast can trigger again, but only a few links deep. Without this a chain
        // build locks the frame.
        const int MaxTriggerDepth = 3;
        int _triggerDepth;
        Transform _fxRoot;
        Material _fxMaterial;
        MaterialPropertyBlock _mpb;

        readonly List<Projectile> _projectiles = new List<Projectile>(64);
        readonly List<Flash> _flashes = new List<Flash>(32);
        readonly EnemyManager.Enemy[] _chainBuffer = new EnemyManager.Enemy[4];
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
            _rangeRing.sharedMaterial = _fxMaterial;
            _rangeRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _rangeRing.enabled = false;
        }

        public void Init(EnemyManager enemies, ContentDatabase database, GameBalance balance)
        {
            _enemies = enemies;
            _db = database;

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
            TickZones(dt);
            TickFlashes(dt);
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
                float cost = s.Source.Def.ManaCost * Book.ManaCostMultiplier;
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
                float triggerCost = def.ManaCost * Book.ManaCostMultiplier;
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
            Vector3 at = target.T.position;
            int points = def.AppliedPoints + Book.BonusAilmentPoints;

            switch (def.Kind)
            {
                case CastKind.Nova:
                    _enemies.DamageArea(at, spell.Radius, spell.Damage,
                        def.AppliedStatus, def.StatusDuration, points);
                    SpawnFlash(at, spell.Radius * 2f, 0.3f, def.Color);
                    return true;

                case CastKind.Chain:
                {
                    int count = _enemies.NearestMany(at, spell.Range, _chainBuffer);
                    if (count == 0) return false;

                    for (int i = 0; i < count && i < def.Hits; i++)
                    {
                        _enemies.Damage(_chainBuffer[i], spell.Damage * BuffDamageMul * RollCrit(),
                            def.AppliedStatus, def.StatusDuration, points, true, def.DisplayName);
                        SpawnFlash(_chainBuffer[i].T.position, 1.8f, 0.18f, def.Color);
                    }

                    return true;
                }

                case CastKind.Projectile:
                    _enemies.Damage(target, spell.Damage * BuffDamageMul * RollCrit(), def.AppliedStatus, def.StatusDuration, points, true, def.DisplayName);
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

                    Vector3 dir = target.T.position - transform.position;
                    dir.y = 0f;
                    FireProjectile(dir.normalized, spell.Damage * BuffDamageMul * RollCrit(), def, def.Color);
                    return true;
                }

                case CastKind.Nova:
                {
                    // Do not detonate into an empty field â€” that was burning mana for nothing.
                    if (_enemies.Nearest(transform.position, spell.Radius) == null) return false;

                    _enemies.DamageArea(transform.position, spell.Radius, spell.Damage,
                        def.AppliedStatus, def.StatusDuration, def.AppliedPoints + Book.BonusAilmentPoints);
                    SpawnFlash(transform.position, spell.Radius * 2f, 0.3f, def.Color);
                    return true;
                }

                case CastKind.Chain:
                {
                    int count = _enemies.NearestMany(transform.position, spell.Range, _chainBuffer);
                    if (count == 0) return false;

                    for (int i = 0; i < count && i < def.Hits; i++)
                    {
                        var e = _chainBuffer[i];
                        _enemies.Damage(e, spell.Damage, def.AppliedStatus, def.StatusDuration, def.AppliedPoints + Book.BonusAilmentPoints);
                        SpawnFlash(e.T.position, 1.8f, 0.18f, def.Color);
                    }

                    return true;
                }

                case CastKind.Heal:
                {
                    if (Hp >= MaxHp) return false;

                    Hp = Mathf.Min(MaxHp, Hp + spell.Damage);
                    SpawnFlash(transform.position, 3.2f, 0.3f, def.Color);
                    return true;
                }

                case CastKind.AreaAtTarget:
                {
                    // Land on the thickest part of the crowd, not on top of ourselves.
                    var cluster = _enemies.BestCluster(transform.position, spell.Range, spell.Radius);
                    if (cluster == null) return false;

                    Vector3 at = cluster.T.position;
                    _enemies.DamageArea(at, spell.Radius * BuffAreaMul,
                        spell.Damage * BuffDamageMul * RollCrit(),
                        def.AppliedStatus, def.StatusDuration, AilmentPoints(def), true, def.DisplayName);
                    SpawnFlash(at, spell.Radius * 2f, 0.3f, def.Color);
                    return true;
                }

                case CastKind.Line:
                {
                    var target = _enemies.Nearest(transform.position, spell.Range);
                    if (target == null) return false;

                    Vector3 dir = target.T.position - transform.position;
                    dir.y = 0f;

                    float halfWidth = Mathf.Max(0.4f, spell.Radius);
                    _enemies.DamageLine(transform.position, dir, spell.Range * BuffRangeMul, halfWidth,
                        spell.Damage * BuffDamageMul * RollCrit(),
                        def.AppliedStatus, def.StatusDuration, AilmentPoints(def), def.DisplayName);

                    SpawnBeam(transform.position, dir.normalized, spell.Range, halfWidth, def.Color);
                    return true;
                }

                case CastKind.Zone:
                {
                    var cluster = _enemies.BestCluster(transform.position, spell.Range, spell.Radius);
                    if (cluster == null) return false;

                    SpawnZone(cluster.T.position, spell, def);
                    return true;
                }
            }

            return false;
        }

        int AilmentPoints(PieceDefinition def) => def.AppliedPoints + Book.BonusAilmentPoints;

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
                    _enemies.Damage(hit, p.Damage, p.Status, p.StatusDuration, p.Points);
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

        /// <summary>A flat rectangle standing in for a sweep. Cheap, and it reads instantly.</summary>
        void SpawnBeam(Vector3 origin, Vector3 dir, float length, float halfWidth, Color color)
        {
            Vector3 mid = origin + dir * (length * 0.5f);
            SpawnFlash(mid, halfWidth * 2f + length * 0.35f, 0.18f, color);
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

        public void TakeDamage(float amount)
        {
            if (!Alive) return;

            // Defense is flat damage reduction, floored so it can never make you immortal.
            Hp -= Mathf.Max(amount * 0.1f, amount - Total(StatKind.Defense) * Time.deltaTime);
            if (Hp <= 0f)
            {
                Hp = 0f;
                Alive = false;
                if (_enemies != null) _enemies.Running = false;
            }
        }
    }
}
