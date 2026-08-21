using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// The curse system's content: four things enemies can leave on you, and four ways to answer.
    ///
    /// Every curse is deliberately something you can already build against, because a debuff with
    /// no counter is just a tax. WEAKENED fights damage stacking, SLUGGISH fights cooldown
    /// stacking, LEADEN fights the movement build, DRAINED fights the mana build. Whatever a run
    /// leaned on, one of these hurts it specifically.
    ///
    /// Only cursed enemies carry them, and cursed enemies are bigger and tougher — so the answer is
    /// never only "resist it", it is also "kill that one first".
    ///
    /// Idempotent: matched by asset id.
    /// </summary>
    public static class CursePass
    {
        const string Root = "Assets/GameData";
        const string BuffFolder = Root + "/Buffs";
        const string PieceFolder = Root + "/Pieces";
        const string RecipeFolder = Root + "/Recipes";

        // Tanpa "&": di path MenuItem Unity, & adalah modifier Alt untuk shortcut, dan menu-nya
        // jadi tidak bisa dipanggil lewat nama.
        [MenuItem("Tools/Grimoire/Generate Curses and Counters")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[CursePass] ContentDatabase.asset tidak ketemu.");
                return;
            }

            var debuffs = new List<BuffDefinition>
            {
                Curse("kutuklemah", "WEAKENED", 5f, new Color(0.75f, 0.35f, 0.45f),
                    "Damage semua skill turun 30%.",
                    Mod(StatKind.DamagePct, -0.3f)),

                Curse("kutukberat", "SLUGGISH", 5f, new Color(0.55f, 0.4f, 0.7f),
                    "Cooldown semua skill naik 35%.",
                    Mod(StatKind.CooldownPct, -0.35f)),

                Curse("kutuktimah", "LEADEN", 4f, new Color(0.5f, 0.5f, 0.55f),
                    "Kecepatan menghindar turun drastis. Susah keluar dari kerumunan.",
                    Mod(StatKind.MoveSpeed, -1.5f)),

                Curse("kutukkering", "DRAINED", 6f, new Color(0.35f, 0.5f, 0.75f),
                    "Mana berhenti mengalir dan tiap cast jadi lebih mahal.",
                    Mod(StatKind.ManaRegen, -7f), Mod(StatKind.ManaCostPct, -0.3f))
            };

            db.EditorSetDebuffs(debuffs);

            var pieces = new List<PieceDefinition>(db.Pieces);
            var recipes = new List<RecipeDefinition>(db.Recipes);

            int added = AddCounters(db, pieces);
            db.EditorSet(pieces, recipes);

            int linked = AddCounterRecipes(db, recipes);
            db.EditorSet(pieces, recipes);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CursePass] {debuffs.Count} kutukan, {added} piece penangkal, " +
                      $"{linked} resep. Total piece: {pieces.Count}.");
            Selection.activeObject = db;
        }

        // ---------- counters ----------

        static int AddCounters(ContentDatabase db, List<PieceDefinition> pieces)
        {
            int before = pieces.Count;

            // Passive resistance: cheap, always on, never enough on its own.
            var ward = Sigil("segelpenangkal", "Ward Sigil", 1, ShapeKind.Line2,
                "Kutukan yang menempel padamu berakhir 20% lebih cepat.");
            Stats(ward, Mod(StatKind.DebuffResist, 0.2f));
            Add(pieces, ward);

            var purifier = Sigil("segelpemurni", "Purifier Sigil", 2, ShapeKind.Tee,
                "EVOLVED. Kutukan berakhir 40% lebih cepat, dan kamu pulih perlahan.");
            Stats(purifier, Mod(StatKind.DebuffResist, 0.4f), Mod(StatKind.HpRegen, 2f));
            Add(pieces, purifier);

            // Active removal: costs a slot and mana, but wipes everything at once.
            var light = Skill("cahayapembersih", "Cleansing Light", 1, ShapeKind.Corner,
                new Color(1f, 0.95f, 0.75f), cooldown: 8f, mana: 16f, heal: 0f,
                "Membuang SEMUA kutukan sekaligus. Diam saja kalau tidak ada yang perlu dibuang.");
            Add(pieces, light);

            var dawn = Skill("fajarpembersih", "Cleansing Dawn", 2, ShapeKind.SBend,
                new Color(1f, 0.88f, 0.55f), cooldown: 7f, mana: 24f, heal: 40f,
                "EVOLVED. Membuang semua kutukan dan menyembuhkan sekalian.");
            Add(pieces, dawn);

            return pieces.Count - before;
        }

        static int AddCounterRecipes(ContentDatabase db, List<RecipeDefinition> recipes)
        {
            int before = recipes.Count;

            R(recipes, db, "segelpemurni_a", "segelpemurni", "segelpenangkal", "segelpenangkal");
            R(recipes, db, "segelpemurni_b", "segelpemurni", "segelpenangkal", "segelsumur");
            R(recipes, db, "fajarpembersih_a", "fajarpembersih", "cahayapembersih", "minorheal");
            R(recipes, db, "fajarpembersih_b", "fajarpembersih", "cahayapembersih", "segelpenangkal");

            return recipes.Count - before;
        }

        // ---------- asset plumbing ----------

        static StatModifier Mod(StatKind kind, float value) =>
            new StatModifier { Type = kind, Value = value };

        static void Stats(PieceDefinition piece, params StatModifier[] mods)
        {
            piece.Stats = mods;
            EditorUtility.SetDirty(piece);
        }

        static void Add(List<PieceDefinition> pieces, PieceDefinition piece)
        {
            if (piece != null && !pieces.Contains(piece)) pieces.Add(piece);
        }

        static BuffDefinition Curse(string id, string name, float duration, Color color,
            string blurb, params StatModifier[] mods)
        {
            string path = $"{BuffFolder}/Buff_{id}.asset";
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
            asset.Mods = mods;
            asset.IsDebuff = true;
            asset.ResistShortensDuration = true;
            asset.Blurb = blurb;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        static PieceDefinition Sigil(string id, string name, int stars, ShapeKind shape, string blurb)
        {
            var asset = Load(id);

            asset.Id = id;
            asset.DisplayName = name;
            asset.Stars = stars;
            asset.Layer = Layer.Skill;
            asset.Kind = CastKind.Passive;      // invarian: segel WAJIB Passive
            asset.Element = Element.Arcane;
            asset.Shape = shape;
            asset.Color = new Color(0.85f, 0.86f, 0.7f);
            asset.Trigger = CastTrigger.Cooldown;
            asset.BaseDamage = 0f;
            asset.ManaCost = 0f;
            asset.Blurb = blurb;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        static PieceDefinition Skill(string id, string name, int stars, ShapeKind shape, Color color,
            float cooldown, float mana, float heal, string blurb)
        {
            var asset = Load(id);

            asset.Id = id;
            asset.DisplayName = name;
            asset.Stars = stars;
            asset.Layer = Layer.Skill;
            asset.Kind = CastKind.Cleanse;
            asset.Element = Element.Arcane;
            asset.Shape = shape;
            asset.Color = color;
            asset.Trigger = CastTrigger.Cooldown;

            // BaseDamage doubles as the heal on a cleanse, the same way it does on Heal.
            asset.BaseDamage = heal;
            asset.BaseCooldown = cooldown;
            asset.Radius = 0f;
            asset.Range = 0f;
            asset.Hits = 1;
            asset.ManaCost = mana;
            asset.AppliedStatus = null;
            asset.Blurb = blurb;

            EditorUtility.SetDirty(asset);
            return asset;
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

        static void R(List<RecipeDefinition> recipes, ContentDatabase db, string fileId,
            string resultId, params string[] ingredientIds)
        {
            var result = db.ById(resultId);
            if (result == null)
            {
                Debug.LogWarning($"[CursePass] hasil '{resultId}' tidak ada, resep dilewati.");
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
