using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// The stationary caster. Reads the compiled loadout only â€” it never looks at the grid.
    /// Owns the tiny FX pools so the prototype stays in one place.
    /// </summary>
    public partial class PlayerCaster : MonoBehaviour
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

            /// <summary>Ricochets left before it dies on contact.</summary>
            public int Bounces;

            public float BounceRange;
            public Color Tint;

            /// <summary>Who it just hit, so it cannot immediately bounce back into them.</summary>
            public EnemyManager.Enemy LastHit;

            public bool Active;

            /// <summary>
            /// Efek partikel yang menjadi badan peluru ini, kalau skillnya punya. Dilepas kembali
            /// ke kolam saat peluru pensiun — slot peluru dipakai ulang lintas skill, jadi efeknya
            /// tidak boleh menetap di slotnya.
            /// </summary>
            public Transform Vfx;

            public GameObject VfxSrc;
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

        /// <summary>
        /// Kecepatan jalan dasar, dipegang di sini bukan dibaca langsung dari
        /// <see cref="GameBalance"/> oleh yang menggerakkan pemain.
        ///
        /// Perlu pindah begitu starter boleh menimpanya: <see cref="GameBalance"/> itu aset yang
        /// dipakai bersama, dan menimpa angkanya saat run berarti menulis permanen ke file di
        /// disk — starter cepat yang dimainkan sekali akan mempercepat setiap run sesudahnya,
        /// termasuk yang memilih starter lain.
        /// </summary>
        public float BaseMoveSpeed = 2.8f;
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

            /// <summary>How many charges are held. Always 1 for a plain buff.</summary>
            public int Stacks;
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

            // Landing on itself adds a charge; landing on anything else starts from one.
            slots[chosen].Stacks = slots[chosen].Def == def
                ? Mathf.Min(def.MaxStacks, slots[chosen].Stacks + 1)
                : 1;

            slots[chosen].Def = def;
            slots[chosen].Remaining = duration;   // refresh
        }

        /// <summary>
        /// Strips the curse with the LONGEST left on it. Longest rather than shortest on purpose:
        /// the short ones are about to fall off anyway, so taking those would make the reward
        /// technically real and practically nothing.
        /// </summary>
        public bool ClearOldestDebuff()
        {
            int worst = -1;
            float longest = 0f;

            for (int i = 0; i < DebuffSlots; i++)
            {
                if (_debuffs[i].Def == null || _debuffs[i].Remaining <= longest) continue;

                longest = _debuffs[i].Remaining;
                worst = i;
            }

            if (worst < 0) return false;

            _debuffs[worst].Def = null;
            Recompute();
            return true;
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

                // Every charge counts. A three-stack Frenzy is three times the buff, which is what
                // makes spending time collecting them worth anything.
                int stacks = Mathf.Max(1, slots[i].Stacks);

                for (int m = 0; m < def.Mods.Length; m++)
                {
                    var mod = def.Mods[m];
                    if (mod.Type == StatKind.None || mod.Type == StatKind.Count) continue;
                    into[(int)mod.Type] += mod.Value * stacks;
                }
            }
        }

        /// <summary>Charges held of a given buff, zero when it is not up.</summary>
        public int StacksOf(BuffDefinition def)
        {
            if (def == null) return 0;

            for (int i = 0; i < BuffSlots; i++)
            {
                if (_buffs[i].Def == def) return Mathf.Max(1, _buffs[i].Stacks);
            }

            return 0;
        }

        /// <summary>Spends every charge of a buff and reports how many were taken.</summary>
        public int SpendCharges(BuffDefinition def)
        {
            if (def == null) return 0;

            for (int i = 0; i < BuffSlots; i++)
            {
                if (_buffs[i].Def != def) continue;

                int held = Mathf.Max(1, _buffs[i].Stacks);
                _buffs[i].Def = null;
                _buffs[i].Stacks = 0;
                Recompute();
                return held;
            }

            return 0;
        }

        /// <summary>
        /// A kill happened. Pieces on the board that generate charges do it here.
        ///
        /// Wired from the composition root rather than reached for, and cheap enough to run on
        /// every death: the grant list is compiled once when the grid changes, and a charge that is
        /// already at maximum costs one comparison.
        /// </summary>
        public void OnEnemyKilled(Vector3 at)
        {
            if (Book == null) return;

            var grants = Book.KillGrants;
            for (int i = 0; i < grants.Count; i++) ApplyBuff(grants[i]);

            // Panen: skill besar yang membawa efek ini mengubah pembantaian jadi bahan bakar.
            // Nilainya sudah teragregasi saat kompilasi grid, jadi di sini tinggal dua clamp —
            // dan hanya selagi hidup: mayat tidak ikut memanen kill dari zone yang masih menyala.
            if (!Alive) return;

            if (Book.KillRestoreMana > 0f) Mana = Mathf.Min(MaxMana, Mana + Book.KillRestoreMana);
            if (Book.KillRestoreHp > 0f) Hp = Mathf.Min(MaxHp, Hp + Book.KillRestoreHp);
        }

        // Buff berubah saat wave berjalan, jadi ini dipakai SAAT CAST — bukan saat kompilasi grid.
        //
        // Batas atasnya 2, bukan 1. Dulu 1, dan itu berarti nilai NEGATIF tidak berpengaruh sama
        // sekali: sebuah debuff "cast jadi lambat" akan tampil di HUD, lalu tidak melakukan apa pun.
        /// <summary>Set for the duration of one cast when that cast just ate a pile of charges.</summary>
        float _chargeBonus;

        /// <summary>Saklar curang. Null di build normal, dan setiap pembacaan lewat propertinya.</summary>
        public DebugConfig Cheats;

        /// <summary>
        /// Prefab pengganti untuk benda FX yang dulu primitif. Null = primitif lama, jadi
        /// aset yang belum diisi tidak pernah membuat sebuah skill jadi tak terlihat.
        /// </summary>
        public FxLibrary Fx;

        /// <summary>
        /// Melahirkan satu benda FX: dari prefab kalau slotnya terisi, dari primitif kalau tidak.
        ///
        /// Collider selalu dicabut dan bayangan selalu dimatikan — benda-benda ini muncul
        /// puluhan sekaligus, dan satu saja yang lupa dimatikan bayangannya membuat wave ramai
        /// membayar shadow pass untuk kilatan seumur 0,15 detik.
        /// </summary>
        Transform NewFx(GameObject prefab, PrimitiveType fallback, string label, bool paintable)
        {
            GameObject go;

            if (prefab != null)
            {
                go = Instantiate(prefab, _fxRoot);
            }
            else
            {
                go = GameObject.CreatePrimitive(fallback);
                go.transform.SetParent(_fxRoot, false);

                var r = go.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = paintable ? _fxMaterial : _aoeMaterial;
            }

            go.name = label;

            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            return go.transform;
        }

        /// <summary>Khusus ruang uji: buku tetap menembak walau tidak ada wave yang berjalan.</summary>
        public bool CastWithoutWave;

        float BuffDamageMul => (Cheats != null ? Cheats.DamageScale : 1f) *
                               Mathf.Max(0.05f, 1f + _buffStats[(int)StatKind.DamagePct]
                                                  + _debuffStats[(int)StatKind.DamagePct]
                                                  + _chargeBonus);
        float BuffCooldownMul => Cheats != null && Cheats.CheatNoCooldowns
            ? 0.01f
            : Mathf.Clamp(1f - _buffStats[(int)StatKind.CooldownPct]
                             - _debuffStats[(int)StatKind.CooldownPct], 0.25f, 2f);
        float BuffAreaMul => Mathf.Max(0.2f, 1f + _buffStats[(int)StatKind.AreaPct]
                                                + _debuffStats[(int)StatKind.AreaPct]);
        float BuffRangeMul => Mathf.Max(0.2f, 1f + _buffStats[(int)StatKind.RangePct]
                                                 + _debuffStats[(int)StatKind.RangePct]);

        /// <summary>Mana cost from the temporary layer. The grid's own share lives on Grimoire.</summary>
        /// <summary>
        /// Pengali harga mana seluruh skill, dari <see cref="GameBalance"/>.
        ///
        /// Dibaca lewat properti, bukan disalin ke field saat <c>Init</c>: ini knob balancing yang
        /// memang diputar sambil play mode berjalan, dan salinan akan membekukannya sampai run
        /// berikutnya. Beda dengan <see cref="BaseMoveSpeed"/> — yang itu DITIMPA starter, jadi ia
        /// justru tidak boleh membaca balik ke aset.
        /// </summary>
        float ManaCostScale => _balance != null ? Mathf.Max(0f, _balance.ManaCostScale) : 1f;

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

        /// <summary>
        /// Material penanda AOE: isi semi transparan, tepi tegas (shader <c>Grimoire/AoeRing</c>).
        /// Cakram pekat menutupi persis apa yang sedang ditandainya; yang ini membiarkan musuh
        /// dan efek terlihat, sementara TEPI — informasi jangkauan yang sebenarnya — tetap tebal.
        /// </summary>
        Material _aoeMaterial;

        MaterialPropertyBlock _mpb;

        readonly List<Projectile> _projectiles = new List<Projectile>(64);
        readonly List<Flash> _flashes = new List<Flash>(32);

        // Twelve, not four. The old buffer was shorter than the longest chain skill in the book
        // (Frost Prism hits five), so two of its links were silently thrown away every cast.
        // Sized for the worst forked cast in the book: branches share one buffer so no two of them
        // can strike the same enemy, which means it has to hold forks x hits at once.
        readonly EnemyManager.Enemy[] _chainBuffer = new EnemyManager.Enemy[64];

        BoltPool _bolts;
        SkillVfxPool _vfx;
        LineRenderer _rangeRing;

        /// <summary>
        /// Skala instans VFX sebuah skill: radius 3 unit = skala 1, aturan yang sama dengan
        /// kubangan Zone, supaya efek yang area-nya di-buff ikut terlihat membesar.
        /// </summary>
        float VfxScale(PieceDefinition def, float radius) =>
            def.CastVfxScale * Mathf.Max(0.35f, radius / 3f);

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
            BaseMoveSpeed = balance.BaseMoveSpeed;
            Hp = BaseMaxHp;
            Mana = BaseMaxMana;
            _mpb = new MaterialPropertyBlock();

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            _fxMaterial = new Material(shader) { enableInstancing = true };

            var aoeShader = Shader.Find("Grimoire/AoeRing");
            _aoeMaterial = aoeShader != null ? new Material(aoeShader) : _fxMaterial;

            _fxRoot = new GameObject("FX").transform;
            _fxRoot.SetParent(transform.parent, false);

            _bolts = new BoltPool(_fxRoot);
            _vfx = new SkillVfxPool(_fxRoot);

            _enemies.OnReaction += OnReactionFired;
            _enemies.OnStatusApplied += OnEnemyStatusApplied;

            BuildRangeRing();
        }

        /// <summary>
        /// Menimpa angka dasar dengan milik starter, lalu mengisi HP & mana sampai penuh.
        ///
        /// Dipanggil SESUDAH papan pembuka tersusun: "penuh" di sini memakai maksimum yang sudah
        /// termasuk bonus rune, dan rune baru menyumbang setelah ia duduk di papan.
        ///
        /// Starter yang tidak menyebutkan satu stat pun (semuanya −1) melewati langkah ini tanpa
        /// mengubah apa-apa selain mengisi ulang — persis perilaku sebelum starter bisa dipilih.
        /// </summary>
        public void ApplyLoadoutStats(HeroLoadout hero)
        {
            if (hero != null && _balance != null)
            {
                BaseMaxHp = HeroLoadout.Pick(hero.MaxHp, _balance.BaseMaxHp);
                BaseMaxMana = HeroLoadout.Pick(hero.MaxMana, _balance.BaseMaxMana);
                BaseManaRegen = HeroLoadout.Pick(hero.ManaRegen, _balance.BaseManaRegen);
                BaseHpRegen = HeroLoadout.Pick(hero.HpRegen, _balance.BaseHpRegen);
                BaseMoveSpeed = HeroLoadout.Pick(hero.MoveSpeed, _balance.BaseMoveSpeed);
            }

            // Lewat properti, bukan lewat Base-nya: yang ini yang menghitung bonus rune.
            Hp = MaxHp;
            Mana = MaxMana;
        }

        void OnReactionFired(Vector3 pos, ReactionDefinition rx)
        {
            // Bola primitifnya ditipiskan kalau reaksi ini punya efeknya sendiri: tugasnya
            // berubah dari MENJADI ledakan menjadi sekadar kilatan warna yang menamai reaksinya.
            // Dibiarkan pekat, ia menelan efek yang barusan disembur di titik yang sama.
            var tint = rx.FlashColor;
            if (rx.Vfx != null) tint.a *= 0.35f;

            SpawnFlash(pos, rx.BurstRadius * (rx.Vfx != null ? 1.1f : 2f), 0.35f, tint);

            // Radius 3 unit = skala 1, aturan yang sama dengan skill — reaksi berdiameter 8
            // tidak boleh terlihat sama besar dengan yang berdiameter 5.
            _vfx.Burst(rx.Vfx, pos, rx.VfxScale * Mathf.Max(0.35f, rx.BurstRadius / 3f));

            // Inilah rantai intinya: reaksi -> buff -> skill berikutnya lebih kuat.
            if (rx.GrantBuff != null) ApplyBuff(rx.GrantBuff);

            // Reactions that answer the enemy's curses, and reactions that pay for themselves.
            if (rx.CleansesOneDebuff) ClearOldestDebuff();
            if (rx.RefundMana > 0f) Mana = Mathf.Min(MaxMana, Mana + rx.RefundMana);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (Alive)
            {
                Mana = Cheats != null && Cheats.CheatInfiniteMana
                    ? MaxMana
                    : Mathf.Min(MaxMana, Mana + ManaRegen * dt);

                if (HpRegen > 0f) Hp = Mathf.Min(MaxHp, Hp + HpRegen * dt);
            }

            // Spells only fire while a wave is running â€” between waves the book goes quiet.
            //
            // Ruang uji tidak punya wave sama sekali, dan menyalakan wave palsu di sana berarti
            // gerombolan sungguhan ikut berdatangan menimpa boneka yang sedang diamati.
            if (Alive && _enemies != null && (_enemies.WaveActive || CastWithoutWave)) TickSpells(dt);

            TickBuffs(dt);
            TickProjectiles(dt);
            TickDescents(dt);
            TickZones(dt);
            TickSignature(dt);
            TickFlashes(dt);
            _bolts.Tick(dt);
            _vfx.Tick(dt);
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
                float cost = s.Source.Def.ManaCost * ManaCostScale
                             * Book.ManaCostMultiplier * BuffManaCostMul;
                if (Mana < cost) continue;

                // Charge spenders wait for something to spend. Firing at zero charges would make
                // collecting them pointless — the whole loop is "hold it until it is worth it".
                var eats = s.Source.Def.ConsumesCharge;
                if (eats != null && StacksOf(eats) <= 0) continue;

                _chargeBonus = eats == null ? 0f : SpendCharges(eats) * s.Source.Def.DamagePerCharge;

                // No valid target: stay ready and idle. The cooldown must not keep spinning.
                if (!Cast(s))
                {
                    _chargeBonus = 0f;
                    continue;
                }

                _chargeBonus = 0f;

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
                float triggerCost = def.ManaCost * ManaCostScale
                                    * Book.ManaCostMultiplier * BuffManaCostMul;
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
                    _vfx.Burst(def.CastVfx, at, VfxScale(def, radius));
                    return true;
                }

                case CastKind.Chain:
                    return CastChain(spell, def, at, spell.Damage * BuffDamageMul * RollCrit());

                case CastKind.Projectile:
                    _enemies.Damage(target, spell.Damage * BuffDamageMul * RollCrit(),
                        def.AppliedStatus, def.StatusDuration, points, true, def.DisplayName);
                    SpawnFlash(at, 1.8f, 0.2f, def.Color);
                    _vfx.Burst(def.CastVfx, at, def.CastVfxScale);
                    return true;

                case CastKind.Heal:
                    if (Hp >= MaxHp) return false;
                    Hp = Mathf.Min(MaxHp, Hp + spell.Damage);
                    SpawnFlash(transform.position, 3.2f, 0.3f, def.Color);
                    _vfx.Burst(def.CastVfx, transform.position, def.CastVfxScale);
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
                    _vfx.Burst(def.CastVfx, transform.position, VfxScale(def, radius));
                    return true;
                }

                case CastKind.Chain:
                    return CastChain(spell, def, transform.position, spell.Damage * BuffDamageMul * RollCrit());

                case CastKind.Radial:
                    return CastRadial(spell, def);

                case CastKind.Detonate:
                    return CastDetonate(spell, def);

                case CastKind.Heal:
                {
                    if (Hp >= MaxHp) return false;

                    Hp = Mathf.Min(MaxHp, Hp + spell.Damage);
                    SpawnFlash(transform.position, 3.2f, 0.3f, def.Color);
                    _vfx.Burst(def.CastVfx, transform.position, def.CastVfxScale);
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
                    _vfx.Burst(def.CastVfx, transform.position, def.CastVfxScale);
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

                    // Di 30% panjang garis, menghadap arah sapuan: lidah api terbaca keluar dari
                    // pemain, dan efek ledakan tidak menutupi pemainnya sendiri. Umurnya dipaksa
                    // pendek karena sebagian prefab garis (semburan api) adalah loop.
                    if (def.CastVfx != null && dir.sqrMagnitude > 0.0001f)
                    {
                        Vector3 dirN = dir.normalized;
                        _vfx.Burst(def.CastVfx, transform.position + dirN * (length * 0.3f),
                            def.CastVfxScale * Mathf.Max(0.6f, length / 6f),
                            Quaternion.LookRotation(dirN), 0.8f);
                    }

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

                case CastKind.Orbit: return CastOrbit(spell, def);
                case CastKind.Blink: return CastBlink(spell, def);
                case CastKind.Ward: return CastWard(spell, def);
                case CastKind.Surge: return CastSurge(def);
                case CastKind.Restore: return CastRestore(spell, def);
                case CastKind.SunStrike: return CastSunStrike(spell, def);
                case CastKind.RollingBall: return CastRollingBall(spell, def);
                case CastKind.Vortex: return CastVortex(spell, def);
                case CastKind.ForcePush: return CastForcePush(spell, def);
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
            int points = AilmentPoints(def);
            int forks = Mathf.Max(1, def.Forks);
            int taken = 0;

            for (int fork = 0; fork < forks; fork++)
            {
                // Every branch leaves the caster, but none may touch anyone an earlier branch
                // already struck — that exclusion is what makes them visibly fan outward instead
                // of all crawling down the same line of enemies.
                int end = _enemies.ChainFrom(origin, spell.Range, _balance.ChainJumpRange,
                    _chainBuffer, def.Hits, taken);

                if (end == taken) break;

                Vector3 from = origin;

                for (int i = taken; i < end; i++)
                {
                    var target = _chainBuffer[i];
                    Vector3 to = target.Pos;

                    _bolts.Arc(from, to, def.Color);
                    SpawnFlash(to, 1.6f, 0.15f, def.Color);

                    // Hanya sasaran PERTAMA tiap cabang: Stormbreaker menyambar 32 musuh sekali
                    // cast, dan 32 semburan adalah cara tercepat menjenuhkan kolam sekaligus
                    // menutupi lapangan. Delapan pangkal cabang sudah menceritakan bentuknya;
                    // sisanya tetap dapat kilat dan flash.
                    if (i == taken) _vfx.Burst(def.CastVfx, to, def.CastVfxScale);

                    _enemies.Damage(target, damage, def.AppliedStatus, def.StatusDuration,
                        points, true, def.DisplayName, from);

                    from = to;
                }

                taken = end;
            }

            return taken > 0;
        }

        /// <summary>
        /// Sprays projectiles outward in a ring. The only cast that aims at nobody — it waits for
        /// something to come within reach and then covers every direction at once, so where you are
        /// standing matters more than what is nearest.
        /// </summary>
        bool CastRadial(CompiledSpell spell, PieceDefinition def)
        {
            // Still refuses to fire into an empty field: untargeted is not the same as wasteful.
            if (_enemies.Nearest(transform.position, spell.Range) == null) return false;

            int arms = Mathf.Max(2, def.Hits);
            float damage = spell.Damage * BuffDamageMul * RollCrit();

            // Rolled per cast, so two shots never lay down the same pattern.
            float spin = Random.Range(0f, Mathf.PI * 2f);

            for (int i = 0; i < arms; i++)
            {
                float angle = spin + i * (Mathf.PI * 2f / arms);
                FireProjectile(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)),
                    damage, def, def.Color);
            }

            SpawnFlash(transform.position, 2.4f, 0.2f, def.Color);
            return true;
        }

        /// <summary>
        /// Cashes in an ailment: everything carrying it detonates for what it has stacked.
        ///
        /// Holds its cooldown and its mana when nothing is marked, the same way Heal refuses at
        /// full health — a detonator that fires into an unmarked field is never up when the field
        /// finally IS marked, and that is the entire combo.
        /// </summary>
        bool CastDetonate(CompiledSpell spell, PieceDefinition def)
        {
            if (def.TriggerStatus == null || _db == null) return false;

            int statusIndex = _db.IndexOfStatus(def.TriggerStatus);
            if (statusIndex < 0) return false;

            float perPoint = spell.Damage * BuffDamageMul * RollCrit();
            float radius = spell.Radius * BuffAreaMul;

            int blasts = _enemies.DetonateStatus(statusIndex, perPoint, radius,
                def.MaxDetonations, def.DisplayName,
                (at, points) =>
                {
                    // Bigger stack, bigger bang — the flash has to say so.
                    SpawnFlash(at, radius * (1f + points * 0.12f), 0.28f, def.Color);
                    _vfx.Burst(def.CastVfx, at, def.CastVfxScale * (1f + points * 0.05f));
                });

            return blasts > 0;
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
                var t = NewFx(Fx != null ? Fx.Descent : null, PrimitiveType.Sphere, "Descent", true);
                t.localScale = Vector3.one * 0.85f;

                var r = t.GetComponentInChildren<Renderer>();

                // Ekornya menempel di prefab kalau prefabnya sudah membawa satu; kalau tidak,
                // dipasang di sini. Tanpa ekor, tembakan dari langit terbaca menetas di tanah.
                var trail = t.GetComponentInChildren<TrailRenderer>();
                if (trail == null)
                {
                    trail = t.gameObject.AddComponent<TrailRenderer>();
                    trail.sharedMaterial = _bolts.Material;
                    trail.time = 0.22f;
                    trail.widthMultiplier = 0.7f;
                    trail.numCapVertices = 2;
                    trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    trail.receiveShadows = false;
                }

                d = new Descent { T = t, Trail = trail, R = r };
                _descents.Add(d);
            }

            d.Target = new Vector3(at.x, 0.35f, at.z);
            d.Spell = spell;
            d.Def = def;
            d.Damage = damage;
            d.Radius = radius;
            d.IsZone = def.Kind == CastKind.Zone;
            d.Active = true;

            // Bola jatuhnya menciut jadi inti kalau skillnya punya efek sendiri: yang bercerita
            // saat mendarat adalah efek itu, dan bola abu seukuran kepala yang turun mendahuluinya
            // cuma menandai bahwa ada yang belum diganti. Ekor jejaknya TETAP — itu yang membuat
            // tembakan terbaca datang dari langit, bukan menetas di tanah.
            d.T.localScale = Vector3.one * (def.CastVfx != null ? 0.3f : 0.85f);

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
            _vfx.Burst(d.Def.CastVfx, d.Target, VfxScale(d.Def, d.Radius));
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
                var t = NewFx(Fx != null ? Fx.ProjectileCore : null, PrimitiveType.Sphere,
                    "Projectile", true);
                t.localScale = Vector3.one * 0.45f;

                p = new Projectile { T = t };
                _projectiles.Add(p);
            }

            _mpb.SetColor(BaseColorId, color);
            p.T.GetComponent<Renderer>().SetPropertyBlock(_mpb);

            p.T.position = transform.position;

            // Prefab efek menjadi badan peluru; bola primitifnya menciut jadi inti nyaris tak
            // terlihat. Tetap ada karena dialah yang dites tabrakannya — efek cuma bajunya.
            if (source.CastVfx != null)
            {
                p.Vfx = _vfx.Attach(source.CastVfx, p.T.position,
                    Quaternion.LookRotation(dir), source.CastVfxScale);
                p.VfxSrc = source.CastVfx;
                p.T.localScale = Vector3.one * 0.12f;
            }
            else
            {
                p.T.localScale = Vector3.one * 0.45f;
            }

            p.T.gameObject.SetActive(true);
            p.Dir = dir;
            p.Life = 2.2f;
            p.Damage = damage;
            p.Status = source.AppliedStatus;
            p.StatusDuration = source.StatusDuration;
            p.Points = source.AppliedPoints + Book.BonusAilmentPoints;
            p.SourceName = source.DisplayName;
            p.Bounces = source.Bounces;
            p.BounceRange = source.BounceRange;
            p.Tint = color;
            p.LastHit = null;
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
                if (p.Vfx != null) p.Vfx.position = p.T.position;

                var hit = _enemies.NearestExcluding(p.T.position, 0.75f, p.LastHit);
                if (hit == null) continue;

                _enemies.Damage(hit, p.Damage, p.Status, p.StatusDuration, p.Points, true, p.SourceName);

                // Kilatan tumbukan: putih besar kalau peluru ini polos, dan kecil berwarna
                // kalau ia sudah membawa efeknya sendiri. Bola putih di tiap tumbukan adalah
                // primitif yang paling sering terlihat di layar — ada belasan per detik.
                if (p.Vfx != null) SpawnFlash(p.T.position, 0.7f, 0.1f, p.Tint);
                else SpawnFlash(p.T.position, 1.4f, 0.15f, Color.white);

                if (p.Bounces <= 0)
                {
                    Retire(p);
                    continue;
                }

                // Ricochet. Excluding the enemy it just struck is what stops a pair of them
                // batting one projectile back and forth on the spot.
                var next = _enemies.NearestExcluding(p.T.position, p.BounceRange, hit);
                if (next == null)
                {
                    Retire(p);
                    continue;
                }

                Vector3 toNext = next.Pos - p.T.position;
                toNext.y = 0f;

                _bolts.Arc(p.T.position, next.Pos, p.Tint, 0.1f, 0.12f);

                p.Dir = toNext.normalized;
                if (p.Vfx != null && p.Dir.sqrMagnitude > 0.0001f)
                {
                    p.Vfx.rotation = Quaternion.LookRotation(p.Dir);
                }
                p.LastHit = hit;
                p.Bounces--;
                p.Life = Mathf.Max(p.Life, 0.9f);
            }
        }

        void Retire(Projectile p)
        {
            p.Active = false;
            p.T.gameObject.SetActive(false);

            // Selalu dilepas, bukan disimpan di slot: slot peluru berikutnya bisa milik skill
            // lain, dan kolamnya yang membuat lepas-pasang ini murah.
            if (p.Vfx != null)
            {
                _vfx.Release(p.VfxSrc, p.Vfx);
                p.Vfx = null;
                p.VfxSrc = null;
            }
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
                f = new Flash
                {
                    T = NewFx(Fx != null ? Fx.ImpactFlash : null, PrimitiveType.Sphere, "Flash", true)
                };

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

            /// <summary>Units per second it wanders. Zero keeps it planted.</summary>
            public float Drift;

            /// <summary>Current heading, turned a little every frame so the path curves.</summary>
            public float Heading;

            public bool Active;

            /// <summary>
            /// Efek partikel milik skill ini, kalau ada. Disimpan bersama PREFAB SUMBERNYA karena
            /// zone-nya dipakai ulang dari pool: kubangan yang barusan milik Tornado bisa dipakai
            /// lagi oleh skill lain, dan tanpa membandingkan sumbernya efek lama akan ikut terbawa.
            ///
            /// Digantung di root FX, BUKAN di cakramnya — cakram itu dipipihkan
            /// (skala Y 0,03) untuk jadi genangan, dan apa pun yang jadi anaknya ikut gepeng.
            /// </summary>
            public Transform Vfx;

            public GameObject VfxSource;
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
                z = new Zone
                {
                    T = NewFx(Fx != null ? Fx.ZoneDisc : null, PrimitiveType.Cylinder, "Zone", false)
                };

                _zones.Add(z);
            }

            // Kepekatan bukan lagi urusan kode ini — shader AoeRing yang memegangnya: isi tipis,
            // tepi tegas. Yang dikirim dari sini tinggal WARNANYA.
            var tone = def.Color;
            tone.a = 1f;

            _mpb.SetColor(BaseColorId, tone);
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
            z.Drift = def.ZoneDrift;
            z.Heading = Random.Range(0f, Mathf.PI * 2f);
            z.Active = true;

            z.T.position = z.Pos;
            z.T.localScale = new Vector3(z.Radius * 2f, 0.03f, z.Radius * 2f);
            z.T.gameObject.SetActive(true);

            AttachCastVfx(z, def);
        }

        /// <summary>
        /// Memasang (atau menukar) efek partikel milik skill di sebuah kubangan. Dipanggil tiap
        /// kubangan lahir — termasuk yang diambil dari pool, karena pemilik sebelumnya bisa
        /// skill lain.
        /// </summary>
        void AttachCastVfx(Zone z, PieceDefinition def)
        {
            if (z.VfxSource != def.CastVfx)
            {
                if (z.Vfx != null) Destroy(z.Vfx.gameObject);
                z.Vfx = null;
                z.VfxSource = def.CastVfx;

                if (def.CastVfx != null)
                {
                    var go = Instantiate(def.CastVfx, _fxRoot);
                    go.name = "CastVfx " + def.DisplayName;
                    z.Vfx = go.transform;
                }
            }

            if (z.Vfx == null) return;

            // Radius 3 unit = skala 1. Efek yang ukurannya tidak ikut jangkauan membuat skill
            // yang sudah dibuffed area-nya terlihat persis sama dengan yang belum.
            float scale = def.CastVfxScale * Mathf.Max(0.35f, z.Radius / 3f);

            z.Vfx.localScale = Vector3.one * scale;
            z.Vfx.position = z.Pos;
            z.Vfx.gameObject.SetActive(true);
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

                    // Dimatikan, bukan dihancurkan: kubangan berikutnya dari skill yang sama
                    // memakainya lagi tanpa membayar Instantiate.
                    if (z.Vfx != null) z.Vfx.gameObject.SetActive(false);
                    continue;
                }

                // A wandering pool has to be re-read every few seconds instead of understood once.
                // The heading turns rather than being re-rolled, so it curves like weather rather
                // than twitching like noise.
                if (z.Drift > 0f)
                {
                    z.Heading += Random.Range(-1.6f, 1.6f) * dt;

                    z.Pos.x += Mathf.Cos(z.Heading) * z.Drift * dt;
                    z.Pos.z += Mathf.Sin(z.Heading) * z.Drift * dt;
                    z.T.position = z.Pos;

                    // Efeknya ikut mengembara. Kalau tidak, tornado berdiri diam sementara
                    // kubangan damage-nya berjalan pergi — dan yang terbaca adalah dua hal
                    // berbeda, bukan satu.
                    if (z.Vfx != null) z.Vfx.position = z.Pos;
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

        /// <summary>Raised saat damage benar-benar menembus. Tidak menyala untuk yang tertahan perisai.</summary>
        public System.Action OnHurt;

        void Drain(float amount)
        {
            if (Cheats != null && Cheats.CheatInvulnerable) return;

            // Perisai dimakan lebih dulu, dan sisa yang tembus tetap diteruskan ke HP dalam pukulan
            // yang sama. Kalau perisai menelan seluruh pukulan hanya karena dia sempat aktif, satu
            // Ward tipis bisa memblokir hantaman terbesar di layar secara gratis.
            if (Shield > 0f)
            {
                float absorbed = Mathf.Min(Shield, amount);
                Shield -= absorbed;
                amount -= absorbed;
                if (amount <= 0f) return;
            }

            Hp -= amount;
            OnHurt?.Invoke();

            if (Hp > 0f) return;

            Hp = 0f;
            Alive = false;
            if (_enemies != null) _enemies.Running = false;
        }
    }
}
