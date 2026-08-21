using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Skill pairs that only pay off together, and reactions that answer more than damage.
    ///
    /// The book already had one combo axis — two ailments on one enemy make a reaction — but every
    /// skill was still self-sufficient. Nothing said "this piece is weak on its own and enormous
    /// beside that one", which is the exact shape that makes a build feel discovered rather than
    /// picked.
    ///
    /// Three kinds of pairing get authored here:
    ///
    /// 1. <b>Marker + detonator.</b> Plague Brand smears POISON for almost no damage. Sunder cashes
    ///    every stack of it in at once. Neither is worth a slot alone.
    /// 2. <b>Ricochet.</b> Untargeted sprays that bounce enemy to enemy, so packing enemies
    ///    together turns a weak spray into a saw.
    /// 3. <b>Reactions that pay.</b> Some now strip a curse off the caster or hand mana back, so an
    ///    ailment build answers the enemy's debuffs instead of buying a separate answer.
    ///
    /// Idempotent: matched by asset id.
    /// </summary>
    public static class ComboPass
    {
        const string Root = "Assets/GameData";
        const string PieceFolder = Root + "/Pieces";
        const string RecipeFolder = Root + "/Recipes";

        [MenuItem("Tools/Grimoire/Generate Combos")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[ComboPass] ContentDatabase.asset tidak ketemu.");
                return;
            }

            var pieces = new List<PieceDefinition>(db.Pieces);
            int added = AddCombos(db, pieces);
            db.EditorSet(pieces, new List<RecipeDefinition>(db.Recipes));

            var recipes = new List<RecipeDefinition>(db.Recipes);
            int linked = AddRecipes(db, recipes);
            db.EditorSet(pieces, recipes);

            int reactions = UpgradeReactions(db);
            int bounced = AddBounces(db);
            int charged = AddCharges(db, pieces);
            db.EditorSet(pieces, recipes);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ComboPass] {added} piece kombo, {linked} resep, {reactions} reaksi " +
                      $"diperkaya, {bounced} skill memantul, {charged} piece charge. " +
                      $"Total piece: {pieces.Count}.");
            Selection.activeObject = db;
        }

        // ---------- marker / detonator pairs ----------

        static int AddCombos(ContentDatabase db, List<PieceDefinition> pieces)
        {
            int before = pieces.Count;

            var poison = db.StatusById("poison");
            var bleed = db.StatusById("bleed");
            var burn = db.StatusById("burn");

            // The marker: almost no damage, huge spread. Useless alone, on purpose.
            var brand = Skill(db, "plaguebrand", "Plague Brand", 1, ShapeKind.Line2,
                CastKind.AreaAtTarget, Element.Arcane, new Color(0.55f, 0.85f, 0.35f),
                damage: 2f, cooldown: 2.4f, radius: 5.5f, range: 11f, mana: 10f,
                status: poison, duration: 6f, points: 3,
                "Nyaris tidak melukai. Ia MENANDAI: POISON 3 poin ke gerombolan lebar. " +
                "Nilainya baru muncul kalau ada yang menagih tanda itu.");
            Add(pieces, brand);

            // The detonator: nothing without a marker, enormous with one.
            var sunder = Detonator(db, "sunder", "Sunder", 2, ShapeKind.Tee,
                new Color(0.7f, 1f, 0.4f), poison,
                damagePerPoint: 26f, cooldown: 3.2f, radius: 3.4f, mana: 22f,
                "EVOLVED. Meledakkan SETIAP musuh ber-POISON sebesar poin yang menumpuk padanya, " +
                "lalu mencabut racunnya. Diam saja kalau tidak ada yang ditandai.");
            Add(pieces, sunder);

            var reckoning = Detonator(db, "reckoning", "Reckoning", 4, ShapeKind.Zed,
                new Color(1f, 0.5f, 0.25f), burn,
                damagePerPoint: 95f, cooldown: 4.4f, radius: 6f, mana: 54f,
                "EVOLVED. Setiap musuh yang terbakar meledak sebesar tumpukan BURN-nya. " +
                "Semakin dalam kamu membakar, semakin besar tagihannya.");
            Add(pieces, reckoning);

            var rupture = Detonator(db, "rupture", "Rupture", 3, ShapeKind.Cup,
                new Color(0.9f, 0.35f, 0.4f), bleed,
                damagePerPoint: 52f, cooldown: 3.6f, radius: 4.6f, mana: 34f,
                "EVOLVED. Semua luka terbuka pecah sekaligus.");
            Add(pieces, rupture);

            return pieces.Count - before;
        }

        static int AddRecipes(ContentDatabase db, List<RecipeDefinition> recipes)
        {
            int before = recipes.Count;

            R(recipes, db, "plaguebrand_a", "plaguebrand", "kubanganracun", "pusaran");
            R(recipes, db, "sunder_a", "sunder", "plaguebrand", "kubanganracun");
            R(recipes, db, "sunder_b", "sunder", "plaguebrand", "plaguebrand");
            R(recipes, db, "rupture_a", "rupture", "sunder", "belatiberputar");
            R(recipes, db, "reckoning_a", "reckoning", "rupture", "firestormcore");
            R(recipes, db, "reckoning_b", "reckoning", "rupture", "meteor");

            R(recipes, db, "segeldaya_a", "segeldaya", "segelamuk", "segelmata");
            R(recipes, db, "lepasamuk_a", "lepasamuk", "segelamuk", "hujanapi");
            R(recipes, db, "lepasamuk_b", "lepasamuk", "segelamuk", "emberburst");

            return recipes.Count - before;
        }

        // ---------- ricochet ----------

        /// <summary>
        /// Bouncing turns "spray everywhere" into something that reads the board: a shot into an
        /// empty quarter dies, a shot into a pack saws through it. That is the difference between
        /// firing in all directions and firing in all directions being interesting.
        /// </summary>
        static int AddBounces(ContentDatabase db)
        {
            int n = 0;

            // Bouncing starts at 2 stars on purpose. Throughput is fixed per tier, so every extra
            // hit a piece gets divides its printed damage — and a 1-star spray with three bounces
            // solved to THREE damage a hit. Correct by the model, and unreadable on a card: a
            // player sees "3 dmg" and bins the piece before the bounces ever pay it back.
            n += Bounce(db, "belatiberputar", 0, 0f);
            n += Bounce(db, "sparkbolt", 0, 0f);

            n += Bounce(db, "glacialspike", 3, 6.5f);
            n += Bounce(db, "absolutezero", 4, 8f);

            return n;
        }

        static int Bounce(ContentDatabase db, string id, int bounces, float range)
        {
            var piece = db.ById(id);
            if (piece == null) return 0;

            piece.Bounces = bounces;
            piece.BounceRange = range;
            EditorUtility.SetDirty(piece);
            return 1;
        }

        // ---------- charges ----------

        /// <summary>
        /// The collect-then-spend loop. A charge is a buff that stacks, generated by killing, and
        /// eaten whole by one big skill that hits harder for every stack it swallows.
        ///
        /// The generator is a SIGIL: it costs board space and produces nothing on its own, so a
        /// charge build pays for the loop up front. The spender REFUSES to fire at zero charges,
        /// which is what makes holding them a decision instead of an accident.
        /// </summary>
        static int AddCharges(ContentDatabase db, List<PieceDefinition> pieces)
        {
            var buffs = new List<BuffDefinition>(db.Buffs);

            var frenzy = Charge("frenzy", "FRENZY", 8f, 6, new Color(1f, 0.55f, 0.25f),
                "Satu tumpukan tiap musuh mati. Tiap tumpukan: -6% cooldown, +6% damage.",
                Mod(StatKind.CooldownPct, 0.06f), Mod(StatKind.DamagePct, 0.06f));

            var power = Charge("power", "POWER", 9f, 5, new Color(0.75f, 0.6f, 1f),
                "Satu tumpukan tiap musuh mati. Tiap tumpukan: +7% peluang crit, +12% damage crit.",
                Mod(StatKind.CritChance, 0.07f), Mod(StatKind.CritDamage, 0.12f));

            if (!buffs.Contains(frenzy)) buffs.Add(frenzy);
            if (!buffs.Contains(power)) buffs.Add(power);
            db.EditorSetAilments(new List<StatusDefinition>(db.Statuses),
                new List<ReactionDefinition>(db.Reactions), buffs);

            int n = 0;

            // Generators — sigils, so they cost space and give nothing directly.
            var frenzySigil = Sigil(db, "segelamuk", "Frenzy Sigil", 1, ShapeKind.Line2,
                "Tiap musuh mati memberimu satu tumpukan FRENZY (maks 6).");
            frenzySigil.GrantOnKill = frenzy;
            EditorUtility.SetDirty(frenzySigil);
            Add(pieces, frenzySigil);
            n++;

            var powerSigil = Sigil(db, "segeldaya", "Power Sigil", 2, ShapeKind.SBend,
                "EVOLVED. Tiap musuh mati memberimu satu tumpukan POWER (maks 5).");
            powerSigil.GrantOnKill = power;
            EditorUtility.SetDirty(powerSigil);
            Add(pieces, powerSigil);
            n++;

            // The spender. Holds its cast entirely until there is something to spend.
            var release = Load("lepasamuk");
            release.Id = "lepasamuk";
            release.DisplayName = "Frenzy Release";
            release.Stars = 3;
            release.Layer = Layer.Skill;
            release.Kind = CastKind.AreaAtTarget;
            release.Element = Element.Fire;
            release.Shape = ShapeKind.Wedge;
            release.Color = new Color(1f, 0.45f, 0.2f);
            release.Trigger = CastTrigger.Cooldown;
            release.BaseDamage = 46f;
            release.BaseCooldown = 3.4f;
            release.Radius = 5.5f;
            release.Range = 11f;
            release.Hits = 1;
            release.ManaCost = 30f;
            release.AppliedStatus = db.StatusById("burn");
            release.StatusDuration = 5f;
            release.AppliedPoints = 2;
            release.ConsumesCharge = frenzy;
            release.DamagePerCharge = 0.55f;
            release.Blurb = "EVOLVED. MENGHABISKAN semua tumpukan FRENZY: +55% damage per tumpukan. " +
                            "Tidak akan menembak sama sekali kalau kamu belum mengumpulkan apa pun.";
            EditorUtility.SetDirty(release);
            Add(pieces, release);
            n++;

            return n;
        }

        static BuffDefinition Charge(string id, string name, float duration, int maxStacks,
            Color color, string blurb, params StatModifier[] mods)
        {
            string path = $"{Root}/Buffs/Buff_{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<BuffDefinition>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BuffDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Id = id;
            asset.DisplayName = name;
            asset.Color = color;
            asset.Duration = duration;
            asset.MaxStacks = maxStacks;
            asset.Mods = mods;
            asset.IsDebuff = false;
            asset.Blurb = blurb;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        static StatModifier Mod(StatKind kind, float value) =>
            new StatModifier { Type = kind, Value = value };

        static PieceDefinition Sigil(ContentDatabase db, string id, string name, int stars,
            ShapeKind shape, string blurb)
        {
            var a = Load(id);

            a.Id = id;
            a.DisplayName = name;
            a.Stars = stars;
            a.Layer = Layer.Skill;
            a.Kind = CastKind.Passive;      // invarian: segel WAJIB Passive
            a.Element = Element.Arcane;
            a.Shape = shape;
            a.Color = new Color(0.95f, 0.72f, 0.45f);
            a.Trigger = CastTrigger.Cooldown;
            a.BaseDamage = 0f;
            a.ManaCost = 0f;
            a.Stats = new StatModifier[0];
            a.Blurb = blurb;

            EditorUtility.SetDirty(a);
            return a;
        }

        // ---------- reactions that do more than damage ----------

        static int UpgradeReactions(ContentDatabase db)
        {
            int n = 0;

            // Ailment builds now answer the enemy's curses instead of paying separately for a
            // cleanser: the reaction you were already chasing strips one off.
            n += Enrich(db, "SHATTER", cleanses: true, refund: 0f);
            n += Enrich(db, "STATIC FREEZE", cleanses: true, refund: 0f);

            // And two that pay their own way, so a combo build can sustain casts it could not
            // otherwise afford.
            n += Enrich(db, "TOXIC BURST", cleanses: false, refund: 8f);
            n += Enrich(db, "BLOOD SURGE", cleanses: false, refund: 6f);
            n += Enrich(db, "FIRESTORM", cleanses: false, refund: 10f);

            return n;
        }

        static int Enrich(ContentDatabase db, string displayName, bool cleanses, float refund)
        {
            for (int i = 0; i < db.Reactions.Count; i++)
            {
                var rx = db.Reactions[i];
                if (rx == null || rx.DisplayName != displayName) continue;

                rx.CleansesOneDebuff = cleanses;
                rx.RefundMana = refund;
                EditorUtility.SetDirty(rx);
                return 1;
            }

            Debug.LogWarning($"[ComboPass] reaksi '{displayName}' tidak ketemu.");
            return 0;
        }

        // ---------- asset plumbing ----------

        static void Add(List<PieceDefinition> pieces, PieceDefinition piece)
        {
            if (piece != null && !pieces.Contains(piece)) pieces.Add(piece);
        }

        static PieceDefinition Load(string id)
        {
            string path = $"{PieceFolder}/Piece_{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<PieceDefinition>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PieceDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        static PieceDefinition Skill(ContentDatabase db, string id, string name, int stars,
            ShapeKind shape, CastKind kind, Element element, Color color, float damage,
            float cooldown, float radius, float range, float mana, StatusDefinition status,
            float duration, int points, string blurb)
        {
            var a = Load(id);

            a.Id = id;
            a.DisplayName = name;
            a.Stars = stars;
            a.Layer = Layer.Skill;
            a.Kind = kind;
            a.Element = element;
            a.Shape = shape;
            a.Color = color;
            a.Trigger = CastTrigger.Cooldown;
            a.BaseDamage = damage;
            a.BaseCooldown = cooldown;
            a.Radius = radius;
            a.Range = range;
            a.Hits = 1;
            a.Forks = 1;
            a.ManaCost = mana;
            a.AppliedStatus = status;
            a.StatusDuration = duration;
            a.AppliedPoints = points;
            a.Blurb = blurb;

            EditorUtility.SetDirty(a);
            return a;
        }

        static PieceDefinition Detonator(ContentDatabase db, string id, string name, int stars,
            ShapeKind shape, Color color, StatusDefinition eats, float damagePerPoint,
            float cooldown, float radius, float mana, string blurb)
        {
            var a = Load(id);

            a.Id = id;
            a.DisplayName = name;
            a.Stars = stars;
            a.Layer = Layer.Skill;
            a.Kind = CastKind.Detonate;
            a.Element = Element.Arcane;
            a.Shape = shape;
            a.Color = color;
            a.Trigger = CastTrigger.Cooldown;

            // BaseDamage is per POINT here, not per hit — stacking the mark deep is the whole play.
            a.BaseDamage = damagePerPoint;
            a.BaseCooldown = cooldown;
            a.Radius = radius;
            a.Range = 0f;
            a.Hits = 1;
            a.ManaCost = mana;

            // TriggerStatus doubles as "what this eats".
            a.TriggerStatus = eats;
            a.AppliedStatus = null;
            a.Blurb = blurb;

            EditorUtility.SetDirty(a);
            return a;
        }

        static void R(List<RecipeDefinition> recipes, ContentDatabase db, string fileId,
            string resultId, params string[] ingredientIds)
        {
            var result = db.ById(resultId);
            if (result == null)
            {
                Debug.LogWarning($"[ComboPass] hasil '{resultId}' tidak ada, resep dilewati.");
                return;
            }

            string path = $"{RecipeFolder}/Recipe_{fileId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RecipeDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var ingredients = new PieceDefinition[ingredientIds.Length];
            for (int i = 0; i < ingredientIds.Length; i++) ingredients[i] = db.ById(ingredientIds[i]);

            asset.Ingredients = ingredients;
            asset.Result = result;
            EditorUtility.SetDirty(asset);

            if (!recipes.Contains(asset)) recipes.Add(asset);
        }
    }
}
