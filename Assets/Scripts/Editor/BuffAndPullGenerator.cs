using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Closes the last design gap: reactions now hand the player a buff, SERET exists to bunch
    /// enemies up, and the new AoE skills finally appear in recipes.
    /// </summary>
    public static class BuffAndPullGenerator
    {
        const string Root = "Assets/GameData";
        const string BuffFolder = Root + "/Buffs";
        const string StatusFolder = Root + "/Statuses";
        const string RecipeFolder = Root + "/Recipes";
        const string PieceFolder = Root + "/Pieces";

        [MenuItem("Tools/Grimoire/Generate Buffs, Seret & Recipes")]
        public static void Generate()
        {
            EnsureFolder(Root, "Buffs");

            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>($"{Root}/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("ContentDatabase.asset nggak ketemu.");
                return;
            }

            // ---------- 1. buff pemain ----------
            var buffs = new List<BuffDefinition>
            {
                Buff("bara", "BARA", new Color(1f, 0.5f, 0.2f), 8f,
                    "Damage semua skill +25% selama 8 detik.",
                    Mod(StatKind.DamagePct, 0.25f)),

                Buff("aliran", "ALIRAN", new Color(0.6f, 0.85f, 1f), 6f,
                    "Cooldown berjalan 40% lebih cepat.",
                    Mod(StatKind.CooldownPct, 0.4f)),

                Buff("sumur", "SUMUR", new Color(0.4f, 0.6f, 1f), 8f,
                    "Regen mana +6 per detik.",
                    Mod(StatKind.ManaRegen, 6f)),

                Buff("fokus", "FOKUS", new Color(0.7f, 1f, 0.8f), 7f,
                    "Jangkauan dan radius +30%.",
                    Mod(StatKind.AreaPct, 0.3f), Mod(StatKind.RangePct, 0.3f)),

                Buff("perisai", "PERISAI", new Color(0.85f, 0.85f, 0.5f), 10f,
                    "Pertahanan +12 dan regen HP +2 per detik.",
                    Mod(StatKind.Defense, 12f), Mod(StatKind.HpRegen, 2f)),

                Buff("naluri", "NALURI", new Color(1f, 0.75f, 0.85f), 6f,
                    "Peluang crit +25%, damage crit +50%.",
                    Mod(StatKind.CritChance, 0.25f), Mod(StatKind.CritDamage, 0.5f))
            };

            // ---------- 2. SERET ----------
            var seret = AssetDatabase.LoadAssetAtPath<StatusDefinition>($"{StatusFolder}/Status_seret.asset");
            if (seret == null)
            {
                seret = ScriptableObject.CreateInstance<StatusDefinition>();
                AssetDatabase.CreateAsset(seret, $"{StatusFolder}/Status_seret.asset");
            }

            seret.Id = "seret";
            seret.DisplayName = "SERET";
            seret.Color = new Color(0.75f, 0.55f, 1f);
            seret.MaxPoints = 1;
            seret.RefreshOnReapply = true;
            seret.TickInterval = 0.5f;
            seret.DamagePerTickPerPoint = 0f;
            seret.MoveSpeedMultiplier = 0.8f;
            seret.DamageTakenMultiplier = 1f;
            seret.PullStrength = 3.5f;
            seret.Blurb = "Menarik musuh ke titik ledakan. Nggak ngasih damage — tugasnya mengumpulkan.";
            EditorUtility.SetDirty(seret);

            var statuses = new List<StatusDefinition>(db.Statuses);
            if (!statuses.Contains(seret)) statuses.Add(seret);

            // ---------- 3. skill sumber SERET ----------
            var pusaran = Skill(db, "pusaran", "Pusaran", 1, ShapeKind.Line2, CastKind.AreaAtTarget,
                Element.Arcane, new Color(0.7f, 0.5f, 0.95f),
                damage: 6f, cooldown: 3.2f, radius: 4f, range: 10f, mana: 10f,
                status: seret, statusDuration: 2.5f, points: 1,
                "2 panjang. Damage kecil, tapi menyeret satu gerombolan jadi satu titik. " +
                "Pasangan wajib semua skill AoE.");

            // ---------- 4. reaksi sekarang ngasih buff ----------
            GrantBuff(db, "PECAH", buffs[0]);       // BARA
            GrantBuff(db, "BEKU RETAK", buffs[1]);  // ALIRAN
            GrantBuff(db, "ARUS DARAH", buffs[2]);  // SUMUR
            GrantBuff(db, "LEDAK RACUN", buffs[5]); // NALURI

            db.EditorSetAilments(statuses, new List<ReactionDefinition>(db.Reactions), buffs);

            // ---------- 5. resep buat skill AoE ----------
            var recipes = new List<RecipeDefinition>(db.Recipes);

            AddRecipe(recipes, "badaisalju", db, "frostnova", "kubanganracun");
            AddRecipe(recipes, "meteor2", db, "hujanapi", "hujanapi", "prismabeku");
            AddRecipe(recipes, "novakiamat2", db, "sabetanpetir", "sabetanpetir", "arcbolt");
            AddRecipe(recipes, "steamburst2", db, "kubanganracun", "hujanapi");
            AddRecipe(recipes, "greaterfireball2", db, "hujanapi", "fireball");
            AddRecipe(recipes, "blizzard2", db, "belatiberputar", "frostnova");
            AddRecipe(recipes, "prismabeku2", db, "belatiberputar", "sabetanpetir", "segelsumur");

            var pieces = new List<PieceDefinition>(db.Pieces);
            if (!pieces.Contains(pusaran)) pieces.Add(pusaran);

            db.EditorSet(pieces, recipes);
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BuffAndPullGenerator] {buffs.Count} buff, SERET + Pusaran, " +
                      $"{recipes.Count} resep total, {pieces.Count} piece.");
            Selection.activeObject = db;
        }

        static void AddRecipe(List<RecipeDefinition> recipes, string fileId, ContentDatabase db,
            params string[] ingredientIds)
        {
            // Result id = the recipe file id with any trailing digit stripped.
            string resultId = fileId.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            var result = db.ById(resultId);
            if (result == null)
            {
                Debug.LogWarning($"Resep '{fileId}': hasil '{resultId}' nggak ketemu.");
                return;
            }

            var ingredients = new List<PieceDefinition>();
            foreach (var id in ingredientIds)
            {
                var piece = db.ById(id);
                if (piece == null)
                {
                    Debug.LogWarning($"Resep '{fileId}': bahan '{id}' nggak ketemu.");
                    return;
                }

                ingredients.Add(piece);
            }

            string path = $"{RecipeFolder}/Recipe_{fileId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RecipeDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Ingredients = ingredients.ToArray();
            asset.Result = result;
            EditorUtility.SetDirty(asset);

            if (!recipes.Contains(asset)) recipes.Add(asset);
        }

        static void GrantBuff(ContentDatabase db, string reactionName, BuffDefinition buff)
        {
            for (int i = 0; i < db.Reactions.Count; i++)
            {
                var rx = db.Reactions[i];
                if (rx == null || rx.DisplayName != reactionName) continue;

                rx.GrantBuff = buff;
                EditorUtility.SetDirty(rx);
                return;
            }
        }

        static StatModifier Mod(StatKind type, float value) => new StatModifier { Type = type, Value = value };

        static BuffDefinition Buff(string id, string label, Color color, float duration,
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
            asset.DisplayName = label;
            asset.Color = color;
            asset.Duration = duration;
            asset.Mods = mods;
            asset.Blurb = blurb;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static PieceDefinition Skill(ContentDatabase db, string id, string label, int stars, ShapeKind shape,
            CastKind kind, Element element, Color color, float damage, float cooldown, float radius,
            float range, float mana, StatusDefinition status, float statusDuration, int points, string blurb)
        {
            string path = $"{PieceFolder}/Piece_{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<PieceDefinition>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PieceDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Id = id;
            asset.DisplayName = label;
            asset.Stars = stars;
            asset.Layer = Layer.Skill;
            asset.Kind = kind;
            asset.Element = element;
            asset.Shape = shape;
            asset.Color = color;
            asset.Trigger = CastTrigger.Cooldown;
            asset.BaseDamage = damage;
            asset.BaseCooldown = cooldown;
            asset.Radius = radius;
            asset.Range = range;
            asset.Hits = 1;
            asset.ManaCost = mana;
            asset.AppliedStatus = status;
            asset.StatusDuration = statusDuration;
            asset.AppliedPoints = points;
            asset.Blurb = blurb;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void EnsureFolder(string parent, string child)
        {
            if (AssetDatabase.IsValidFolder($"{parent}/{child}")) return;
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
