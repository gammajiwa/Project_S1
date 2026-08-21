using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Moves all player-facing content names to English, scales reactions so they stay relevant at
    /// high waves, and gives every orphan piece a recipe.
    ///
    /// English is the default language: there is no localisation layer yet, and mixing two languages
    /// in one item list made the content look half-missing rather than half-translated.
    ///
    /// Idempotent — matches on asset id, not on the old display name.
    /// </summary>
    public static class EnglishNamingPass
    {
        const string Root = "Assets/GameData";
        const string RecipeFolder = Root + "/Recipes";

        [MenuItem("Tools/Grimoire/English Naming + Balance Pass")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[EnglishNamingPass] ContentDatabase.asset tidak ketemu.");
                return;
            }

            int renamed = RenamePieces(db) + RenameStatuses(db) + RenameReactions(db) + RenameBuffs(db);
            int scaled = ScaleReactions(db);
            int added = AddOrphanRecipes(db);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EnglishNamingPass] {renamed} nama diinggriskan, {scaled} reaksi diskalakan, " +
                      $"{added} resep baru.");
            Selection.activeObject = db;
        }

        // ---------- naming ----------

        static readonly Dictionary<string, string[]> PieceNames = new Dictionary<string, string[]>
        {
            // id -> { display name, blurb }
            { "hujanapi",        new[] { "Fire Rain",      "2x2. Rains fire over a wide patch. Applies BURN." } },
            { "kubanganracun",   new[] { "Poison Pool",    "L-shape. Leaves a lingering pool. Applies POISON." } },
            { "sabetanpetir",    new[] { "Lightning Slash", "3 long. Sweeps a line of enemies. Applies SHOCK." } },
            { "belatiberputar",  new[] { "Whirling Blade", "2 long. Fast spinning strike. Applies BLEED." } },
            { "pusaran",         new[] { "Vortex",         "3 long. Drags enemies together. Applies DRAG — the setup for mass reactions." } },
            { "badaisalju",      new[] { "Snowstorm",      "3 long. Wide freezing storm. Applies CHILL." } },
            { "prismabeku",      new[] { "Frost Prism",    "3 long. Shattering ice burst. Applies CHILL." } },
            { "novakiamat",      new[] { "Doom Nova",      "3 long. The heaviest blast in the book. Applies BURN." } },
            { "segelvitalitas",  new[] { "Vitality Sigil", "2 long. +30 max HP. Passive - never fires." } },
            { "segelsumur",      new[] { "Wellspring Sigil", "2 long. +2.5 HP per second. Passive - never fires." } },
            { "segelbara",       new[] { "Ember Sigil",    "2 long. +3 mana per second. Passive - never fires." } },
            { "segelnadi",       new[] { "Pulse Sigil",    "2 long. +25 max mana. Passive - never fires." } },
            { "tintamengalir",   new[] { "Flowing Ink",    "2 long. +2.5 defence. Passive - never fires." } },
            { "runebenteng",     new[] { "Bastion Rune",   "2x2 base. +60 max HP and +4 defence." } },
            { "runearus",        new[] { "Current Rune",   "3 long base. Cuts cooldowns and eases mana cost." } },
            { "runebadai",       new[] { "Storm Rune",     "3x3 base. Huge damage aura, spread across nine cells." } }
        };

        static readonly Dictionary<string, string> StatusNames = new Dictionary<string, string>
        {
            { "seret", "DRAG" }
        };

        static readonly Dictionary<string, string> ReactionNames = new Dictionary<string, string>
        {
            { "PECAH", "SHATTER" },
            { "LEDAK RACUN", "TOXIC BURST" },
            { "ARUS DARAH", "BLOOD SURGE" },
            { "BEKU RETAK", "FROST CRACK" },
            { "BADAI API", "FIRESTORM" },
            { "PUSARAN BEKU", "FROZEN VORTEX" },
            { "BEKU STATIS", "STATIC FREEZE" },
            { "BAKAR LUKA", "SEARING WOUND" },
            { "NANAH", "FESTER" }
        };

        static readonly Dictionary<string, string> BuffNames = new Dictionary<string, string>
        {
            { "bara", "EMBER" },
            { "aliran", "FLOW" },
            { "sumur", "WELLSPRING" },
            { "fokus", "FOCUS" },
            { "perisai", "AEGIS" },
            { "naluri", "INSTINCT" }
        };

        static int RenamePieces(ContentDatabase db)
        {
            int n = 0;
            for (int i = 0; i < db.Pieces.Count; i++)
            {
                var piece = db.Pieces[i];
                if (piece == null || !PieceNames.TryGetValue(piece.Id, out var text)) continue;

                piece.DisplayName = text[0];
                piece.Blurb = text[1];
                EditorUtility.SetDirty(piece);
                n++;
            }

            return n;
        }

        static int RenameStatuses(ContentDatabase db)
        {
            int n = 0;
            for (int i = 0; i < db.Statuses.Count; i++)
            {
                var status = db.Statuses[i];
                if (status == null || !StatusNames.TryGetValue(status.Id, out string name)) continue;

                status.DisplayName = name;
                EditorUtility.SetDirty(status);
                n++;
            }

            return n;
        }

        static int RenameReactions(ContentDatabase db)
        {
            int n = 0;
            for (int i = 0; i < db.Reactions.Count; i++)
            {
                var reaction = db.Reactions[i];
                if (reaction == null || !ReactionNames.TryGetValue(reaction.DisplayName, out string name)) continue;

                reaction.DisplayName = name;
                EditorUtility.SetDirty(reaction);
                n++;
            }

            return n;
        }

        static int RenameBuffs(ContentDatabase db)
        {
            int n = 0;
            for (int i = 0; i < db.Buffs.Count; i++)
            {
                var buff = db.Buffs[i];
                if (buff == null || !BuffNames.TryGetValue(buff.Id, out string name)) continue;

                buff.DisplayName = name;
                EditorUtility.SetDirty(buff);
                n++;
            }

            return n;
        }

        // ---------- reaction scaling ----------

        /// <summary>
        /// Enemy HP climbs every wave; a flat burst does not. Without a share of max HP, reactions
        /// are decoration by wave 10 — and the GDD target of "25% of damage from reactions" is
        /// unreachable. Crowd reactions get the larger share because hitting many is their job.
        /// </summary>
        static readonly Dictionary<string, float> BurstShare = new Dictionary<string, float>
        {
            { "SHATTER", 0.22f },
            { "TOXIC BURST", 0.15f },
            { "BLOOD SURGE", 0.15f },
            { "FROST CRACK", 0.14f },
            { "FIRESTORM", 0.20f },
            { "FROZEN VORTEX", 0.18f },
            { "STATIC FREEZE", 0.18f },
            { "SEARING WOUND", 0.16f },
            { "FESTER", 0.16f }
        };

        static int ScaleReactions(ContentDatabase db)
        {
            int n = 0;
            for (int i = 0; i < db.Reactions.Count; i++)
            {
                var reaction = db.Reactions[i];
                if (reaction == null || !BurstShare.TryGetValue(reaction.DisplayName, out float share)) continue;

                reaction.BurstPctOfMaxHp = share;
                EditorUtility.SetDirty(reaction);
                n++;
            }

            return n;
        }

        // ---------- orphan recipes ----------

        /// <summary>
        /// content-plan.md: every 1-star skill must appear in at least two recipes, and at least
        /// three recipes must take a sigil. Six pieces broke that — Vortex worst of all, since it is
        /// the only source of DRAG and had no recipe at either end.
        /// </summary>
        static int AddOrphanRecipes(ContentDatabase db)
        {
            var recipes = new List<RecipeDefinition>(db.Recipes);
            int before = recipes.Count;

            Add(recipes, db, "vortex", "pusaran", "arcbolt", "belatiberputar");
            Add(recipes, db, "snowstorm2", "badaisalju", "pusaran", "frostnova");
            Add(recipes, db, "greaterheal2", "greaterheal", "segelvitalitas", "minorheal");
            Add(recipes, db, "greaterfireball3", "greaterfireball", "segelbara", "fireball");
            Add(recipes, db, "blizzard3", "blizzard", "segelnadi", "frostnova");
            Add(recipes, db, "steamburst3", "steamburst", "tintamengalir", "belatiberputar");

            db.EditorSet(new List<PieceDefinition>(db.Pieces), recipes);
            return recipes.Count - before;
        }

        static void Add(List<RecipeDefinition> recipes, ContentDatabase db, string fileId,
            string resultId, params string[] ingredientIds)
        {
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
            asset.Result = db.ById(resultId);
            EditorUtility.SetDirty(asset);

            if (!recipes.Contains(asset)) recipes.Add(asset);
        }
    }
}
