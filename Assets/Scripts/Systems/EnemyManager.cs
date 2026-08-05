using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Pooled swarm. No Rigidbody, no colliders, no per-enemy Update â€” one manager loop owns
    /// movement, ailment ticks and reactions so a few hundred enemies stay cheap without ECS.
    /// Ailments live in a fixed-size slot array: no List, no Dictionary, zero allocation.
    /// </summary>
    public class EnemyManager : MonoBehaviour
    {
        public const int StatusSlots = 4;

        /// <summary>Jeda minimum antar reaksi di satu musuh, biar tidak berkedip.</summary>
        public const float ReactionCooldown = 0.25f;

        public struct StatusSlot
        {
            public int Def;        // index into ContentDatabase.Statuses, -1 = empty
            public float Remaining;
            public int Points;
            public float TickTimer;

            // Titik tarik, dipakai kalau status ini punya PullStrength.
            public float PullX;
            public float PullZ;
        }

        public class Enemy
        {
            public Transform T;
            public Renderer R;
            public float Hp;
            public float MaxHp;
            public float Speed;
            public bool Alive;

            /// <summary>Reaksi tidak boleh meletus lagi sebelum ini nol. Mencegah kedip.</summary>
            public float ReactionLock;

            public readonly StatusSlot[] Slots = NewSlots();

            static StatusSlot[] NewSlots()
            {
                var slots = new StatusSlot[StatusSlots];
                for (int i = 0; i < StatusSlots; i++) slots[i].Def = -1;
                return slots;
            }
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public int Kills { get; private set; }
        public int AliveCount { get; private set; }
        public float Elapsed { get; private set; }
        public bool Running = true;

        public int Wave { get; private set; }
        public bool WaveActive { get; private set; }
        public int PendingSpawns { get; private set; }

        public System.Action OnWaveCleared;
        public System.Action<Vector3> OnKill;

        /// <summary>Raised with (position, reaction) so the UI can flash and shout its name.</summary>
        public System.Action<Vector3, ReactionDefinition> OnReaction;

        /// <summary>Raised after points land on an enemy, so trigger skills can check thresholds.</summary>
        public System.Action<Enemy, int, int> OnStatusApplied;

        /// <summary>Raised with (sumber, damage) untuk damage meter.</summary>
        public System.Action<string, float> OnDamage;

        public int Capacity => _balance != null ? _balance.MaxAliveEnemies : 200;

        /// <summary>How many living enemies currently carry each status, by database index.</summary>
        public int[] StatusCounts => _statusCounts;

        readonly List<Enemy> _pool = new List<Enemy>(256);
        readonly Color _baseColor = new Color(0.55f, 0.5f, 0.62f);

        Transform _player;
        PlayerCaster _caster;
        GameBalance _balance;
        ContentDatabase _db;
        Material _material;
        MaterialPropertyBlock _mpb;
        Transform _root;
        float _spawnTimer;
        int[] _statusCounts = new int[0];

        public void Init(Transform player, PlayerCaster caster, GameBalance balance, ContentDatabase database)
        {
            _player = player;
            _caster = caster;
            _balance = balance;
            _db = database;
            _mpb = new MaterialPropertyBlock();
            _statusCounts = new int[Mathf.Max(1, _db.Statuses.Count)];

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            _material = new Material(shader) { enableInstancing = true };

            _root = new GameObject("Enemies").transform;
            _root.SetParent(transform, false);
        }

        public void StartWave(int wave)
        {
            Wave = wave;
            PendingSpawns = _balance.EnemiesBase + wave * _balance.EnemiesPerWave;
            _spawnTimer = 0.25f;
            WaveActive = true;
        }

        void Update()
        {
            if (!Running) return;

            float dt = Time.deltaTime;
            if (WaveActive) Elapsed += dt;

            TickSpawning(dt);
            TickEnemies(dt);

            if (WaveActive && PendingSpawns <= 0 && AliveCount == 0)
            {
                WaveActive = false;
                OnWaveCleared?.Invoke();
            }
        }

        void TickSpawning(float dt)
        {
            if (!WaveActive || PendingSpawns <= 0) return;

            _spawnTimer -= dt;
            if (_spawnTimer > 0f) return;

            _spawnTimer = Mathf.Max(_balance.SpawnIntervalMin,
                _balance.SpawnIntervalBase - Wave * _balance.SpawnIntervalPerWave);
            SpawnOne();
            PendingSpawns--;
        }

        void SpawnOne()
        {
            var e = GetFree();
            if (e == null) return;

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(15f, 18f);

            e.T.position = new Vector3(Mathf.Cos(angle) * dist, 0.55f, Mathf.Sin(angle) * dist);
            e.MaxHp = _balance.EnemyHpBase + Wave * _balance.EnemyHpPerWave;
            e.Hp = e.MaxHp;
            e.Speed = Random.Range(_balance.EnemySpeedMin, _balance.EnemySpeedMax) +
                      Wave * _balance.EnemySpeedPerWave;

            for (int i = 0; i < StatusSlots; i++) e.Slots[i].Def = -1;

            e.Alive = true;
            e.T.gameObject.SetActive(true);
            Paint(e);
        }

        Enemy GetFree()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].Alive) return _pool[i];
            }

            if (_pool.Count >= Capacity) return null;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Enemy";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            go.transform.SetParent(_root, false);
            go.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            var e = new Enemy { T = go.transform, R = r, Alive = false };
            go.SetActive(false);
            _pool.Add(e);
            return e;
        }

        void TickEnemies(float dt)
        {
            AliveCount = 0;
            for (int i = 0; i < _statusCounts.Length; i++) _statusCounts[i] = 0;

            Vector3 target = _player.position;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                AliveCount++;
                if (e.ReactionLock > 0f) e.ReactionLock -= dt;

                float speedMul = 1f;
                bool repaint = false;

                for (int s = 0; s < StatusSlots; s++)
                {
                    int defIndex = e.Slots[s].Def;
                    if (defIndex < 0) continue;

                    var def = _db.Statuses[defIndex];
                    e.Slots[s].Remaining -= dt;

                    if (e.Slots[s].Remaining <= 0f)
                    {
                        e.Slots[s].Def = -1;
                        repaint = true;
                        continue;
                    }

                    _statusCounts[defIndex]++;
                    speedMul *= def.MoveSpeedMultiplier;

                    if (def.DamagePerTickPerPoint > 0f)
                    {
                        e.Slots[s].TickTimer -= dt;
                        if (e.Slots[s].TickTimer <= 0f)
                        {
                            e.Slots[s].TickTimer = def.TickInterval;
                            float dot = def.DamagePerTickPerPoint * e.Slots[s].Points;
                            e.Hp -= dot;
                            OnDamage?.Invoke(def.DisplayName, dot);
                        }
                    }

                    // Tarikan: geser musuh ke titik tempat ailment ini dipasang.
                    if (def.PullStrength > 0f)
                    {
                        Vector3 p = e.T.position;
                        float dx = e.Slots[s].PullX - p.x;
                        float dz = e.Slots[s].PullZ - p.z;
                        float distSqr = dx * dx + dz * dz;

                        if (distSqr > 0.09f)
                        {
                            float pullStep = def.PullStrength * dt / Mathf.Sqrt(distSqr);
                            p.x += dx * pullStep;
                            p.z += dz * pullStep;
                            e.T.position = p;
                        }
                    }
                }

                if (e.Hp <= 0f)
                {
                    Kill(e);
                    continue;
                }

                if (repaint) Paint(e);

                Vector3 pos = e.T.position;
                Vector3 delta = target - pos;
                delta.y = 0f;
                float sqr = delta.sqrMagnitude;

                if (sqr < 0.85f)
                {
                    _caster.TakeDamage(_balance.EnemyContactDps * dt);
                    continue;
                }

                float inv = 1f / Mathf.Sqrt(sqr);
                pos.x += delta.x * inv * e.Speed * speedMul * dt;
                pos.z += delta.z * inv * e.Speed * speedMul * dt;
                e.T.position = pos;
            }
        }

        // ---------- status ----------

        int SlotOf(Enemy e, int defIndex)
        {
            for (int i = 0; i < StatusSlots; i++)
            {
                if (e.Slots[i].Def == defIndex) return i;
            }

            return -1;
        }

        /// <summary>Empty slot, or the one expiring soonest when all four are taken.</summary>
        int FreeSlot(Enemy e)
        {
            int weakest = 0;
            float weakestRemaining = float.MaxValue;

            for (int i = 0; i < StatusSlots; i++)
            {
                if (e.Slots[i].Def < 0) return i;
                if (e.Slots[i].Remaining >= weakestRemaining) continue;

                weakestRemaining = e.Slots[i].Remaining;
                weakest = i;
            }

            return weakest;
        }

        public void ApplyStatus(Enemy e, StatusDefinition status, float duration, int points,
            bool allowReaction = true, Vector3? origin = null)
        {
            if (e == null || !e.Alive || status == null || duration <= 0f) return;

            int defIndex = _db.IndexOfStatus(status);
            if (defIndex < 0) return;

            int slot = SlotOf(e, defIndex);
            if (slot < 0)
            {
                slot = FreeSlot(e);
                e.Slots[slot].Def = defIndex;
                e.Slots[slot].Points = 0;
                e.Slots[slot].TickTimer = status.TickInterval;
            }

            if (status.PullStrength > 0f)
            {
                Vector3 from = origin ?? e.T.position;
                e.Slots[slot].PullX = from.x;
                e.Slots[slot].PullZ = from.z;
            }

            e.Slots[slot].Points = Mathf.Min(status.MaxPoints, e.Slots[slot].Points + Mathf.Max(1, points));
            e.Slots[slot].Remaining = status.RefreshOnReapply
                ? Mathf.Max(e.Slots[slot].Remaining, duration)
                : e.Slots[slot].Remaining + duration;

            Paint(e);

            OnStatusApplied?.Invoke(e, defIndex, e.Slots[slot].Points);

            if (allowReaction) CheckReactions(e);
        }

        /// <summary>Current points of a status on this enemy, 0 when absent.</summary>
        public int PointsOf(Enemy e, int statusIndex)
        {
            if (e == null || statusIndex < 0) return 0;

            int slot = SlotOf(e, statusIndex);
            return slot < 0 ? 0 : e.Slots[slot].Points;
        }

        /// <summary>Spends points from a status. Removes it entirely when nothing is left.</summary>
        public void ConsumePoints(Enemy e, int statusIndex, int amount)
        {
            if (e == null || statusIndex < 0 || amount <= 0) return;

            int slot = SlotOf(e, statusIndex);
            if (slot < 0) return;

            e.Slots[slot].Points -= amount;
            if (e.Slots[slot].Points <= 0) e.Slots[slot].Def = -1;

            Paint(e);
        }

        void CheckReactions(Enemy e)
        {
            if (e.ReactionLock > 0f) return;

            var reactions = _db.Reactions;

            for (int i = 0; i < reactions.Count; i++)
            {
                var rx = reactions[i];
                if (rx == null || !rx.IsValid) continue;

                int slotA = SlotOf(e, _db.IndexOfStatus(rx.A));
                int slotB = SlotOf(e, _db.IndexOfStatus(rx.B));
                if (slotA < 0 || slotB < 0) continue;
                if (e.Slots[slotA].Points < rx.MinPointsA) continue;
                if (e.Slots[slotB].Points < rx.MinPointsB) continue;

                Trigger(e, rx, slotA, slotB);
                return;
            }
        }

        void Trigger(Enemy e, ReactionDefinition rx, int slotA, int slotB)
        {
            int pointsA = e.Slots[slotA].Points;
            Vector3 at = e.T.position;

            if (rx.ConsumeA) e.Slots[slotA].Def = -1;
            if (rx.ConsumeB) e.Slots[slotB].Def = -1;

            float damage = rx.BurstDamage + rx.BurstDamagePerPointA * pointsA;

            e.ReactionLock = ReactionCooldown;
            OnReaction?.Invoke(at, rx);

            if (damage > 0f && rx.BurstRadius > 0f)
            {
                DamageArea(at, rx.BurstRadius, damage, null, 0f, 1, false, rx.DisplayName);
            }

            if (rx.ApplyStatus == null) return;

            if (rx.SpreadToNearby)
            {
                float sqrRadius = rx.BurstRadius * rx.BurstRadius;
                for (int i = 0; i < _pool.Count; i++)
                {
                    var other = _pool[i];
                    if (!other.Alive) continue;

                    Vector3 d = other.T.position - at;
                    d.y = 0f;
                    if (d.sqrMagnitude > sqrRadius) continue;

                    // allowReaction:false â€” a spread must never chain into another spread.
                    ApplyStatus(other, rx.ApplyStatus, rx.ApplyDuration, rx.ApplyPoints, false);
                }

                return;
            }

            ApplyStatus(e, rx.ApplyStatus, rx.ApplyDuration, rx.ApplyPoints, false);
        }

        float DamageTakenMultiplier(Enemy e)
        {
            float mul = 1f;

            for (int i = 0; i < StatusSlots; i++)
            {
                int defIndex = e.Slots[i].Def;
                if (defIndex < 0) continue;
                mul *= _db.Statuses[defIndex].DamageTakenMultiplier;
            }

            return mul;
        }

        void Paint(Enemy e)
        {
            Color c = _baseColor;
            float strongest = 0f;
            int count = 0;

            for (int i = 0; i < StatusSlots; i++)
            {
                int defIndex = e.Slots[i].Def;
                if (defIndex < 0) continue;

                count++;
                if (e.Slots[i].Remaining <= strongest) continue;

                strongest = e.Slots[i].Remaining;
                c = _db.Statuses[defIndex].Color;
            }

            // Two or more ailments at once reads as near-white: the "about to react" tell.
            if (count >= 2) c = Color.Lerp(c, Color.white, 0.55f);

            _mpb.SetColor(BaseColorId, c);
            e.R.SetPropertyBlock(_mpb);
        }

        void Kill(Enemy e)
        {
            e.Alive = false;
            Vector3 at = e.T.position;
            e.T.gameObject.SetActive(false);
            Kills++;
            OnKill?.Invoke(at);
        }

        // ---------- damage API ----------

        public void Damage(Enemy e, float damage, StatusDefinition status, float duration,
            int points = 1, bool allowReaction = true, string sourceName = null, Vector3? origin = null)
        {
            if (e == null || !e.Alive) return;

            float dealt = damage * DamageTakenMultiplier(e);
            e.Hp -= dealt;
            if (dealt > 0f) OnDamage?.Invoke(sourceName ?? "?", dealt);

            if (status != null) ApplyStatus(e, status, duration, points, allowReaction, origin);
            else Paint(e);

            if (e.Hp <= 0f) Kill(e);
        }

        public void DamageArea(Vector3 center, float radius, float damage, StatusDefinition status,
            float duration, int points = 1, bool allowReaction = true, string sourceName = null)
        {
            float sqrRadius = radius * radius;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                Vector3 d = e.T.position - center;
                d.y = 0f;
                if (d.sqrMagnitude <= sqrRadius) Damage(e, damage, status, duration, points, allowReaction, sourceName, center);
            }
        }

        /// <summary>
        /// The enemy with the most neighbours inside <paramref name="clusterRadius"/> — i.e. the
        /// best place to drop an area skill. Only ever called on cast, never per frame.
        /// </summary>
        public Enemy BestCluster(Vector3 from, float maxDistance, float clusterRadius)
        {
            Enemy best = null;
            int bestCount = -1;

            float sqrMax = maxDistance * maxDistance;
            float sqrCluster = clusterRadius * clusterRadius;

            for (int i = 0; i < _pool.Count; i++)
            {
                var candidate = _pool[i];
                if (!candidate.Alive) continue;

                Vector3 toCandidate = candidate.T.position - from;
                toCandidate.y = 0f;
                if (toCandidate.sqrMagnitude > sqrMax) continue;

                int count = 0;
                for (int k = 0; k < _pool.Count; k++)
                {
                    var other = _pool[k];
                    if (!other.Alive) continue;

                    Vector3 d = other.T.position - candidate.T.position;
                    d.y = 0f;
                    if (d.sqrMagnitude <= sqrCluster) count++;
                }

                if (count <= bestCount) continue;

                bestCount = count;
                best = candidate;
            }

            return best;
        }

        /// <summary>Damages everything inside a rectangle running from origin along dir.</summary>
        public void DamageLine(Vector3 origin, Vector3 dir, float length, float halfWidth,
            float damage, StatusDefinition status, float duration, int points = 1, string sourceName = null)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                Vector3 d = e.T.position - origin;
                d.y = 0f;

                float along = Vector3.Dot(d, dir);
                if (along < 0f || along > length) continue;

                float side = d.x * dir.z - d.z * dir.x;
                if (Mathf.Abs(side) > halfWidth) continue;

                Damage(e, damage, status, duration, points, true, sourceName, origin);
            }
        }

        public Enemy Nearest(Vector3 from, float maxDistance)
        {
            Enemy best = null;
            float bestSqr = maxDistance * maxDistance;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.Alive) continue;

                Vector3 d = e.T.position - from;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = e;
            }

            return best;
        }

        /// <summary>Fills <paramref name="buffer"/> with the closest living enemies. Returns how many.</summary>
        public int NearestMany(Vector3 from, float maxDistance, Enemy[] buffer)
        {
            int found = 0;
            float sqrMax = maxDistance * maxDistance;

            for (int slot = 0; slot < buffer.Length; slot++)
            {
                Enemy best = null;
                float bestSqr = sqrMax;

                for (int i = 0; i < _pool.Count; i++)
                {
                    var e = _pool[i];
                    if (!e.Alive) continue;

                    bool taken = false;
                    for (int k = 0; k < found; k++)
                    {
                        if (buffer[k] != e) continue;
                        taken = true;
                        break;
                    }

                    if (taken) continue;

                    Vector3 d = e.T.position - from;
                    d.y = 0f;
                    float sqr = d.sqrMagnitude;
                    if (sqr >= bestSqr) continue;

                    bestSqr = sqr;
                    best = e;
                }

                if (best == null) break;
                buffer[found++] = best;
            }

            return found;
        }
    }
}
