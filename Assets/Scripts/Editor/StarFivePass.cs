using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Two jobs that have to happen together, because doing either alone leaves the game worse off.
    ///
    /// 1. <b>Mana and cooldown pass.</b> Base mana regen used to be high enough that no build ever
    ///    ran dry, which meant the mana number on every skill was decoration. Regen came down in
    ///    GameBalance; this is the other half — the heavy end of the book now costs what it is worth,
    ///    so running four nukes at once is a real decision instead of the obvious one.
    ///
    /// 2. <b>A 5-star tier.</b> The evolution tree topped out at Doom Nova, so nothing on the board
    ///    was worth chasing once you had it. These three never drop and never appear in the shop —
    ///    the only way to hold one is to build it.
    ///
    /// Idempotent: everything is matched by asset id, never by display name, so running it twice
    /// changes nothing and running it after a rename still finds its targets.
    /// </summary>
    public static class StarFivePass
    {
        const string Root = "Assets/GameData";
        const string PieceFolder = Root + "/Pieces";
        const string RecipeFolder = Root + "/Recipes";

        [MenuItem("Tools/Grimoire/Rebalance + Star 5 Tier")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[StarFivePass] ContentDatabase.asset tidak ketemu.");
                return;
            }

            int tuned = ApplyTuning(db);
            int reshaped = MakeMeteorFall(db);

            var pieces = new List<PieceDefinition>(db.Pieces);
            var recipes = new List<RecipeDefinition>(db.Recipes);

            int added = AddStarFive(db, pieces);
            db.EditorSet(pieces, recipes);

            int linked = AddStarFiveRecipes(db, recipes);
            db.EditorSet(pieces, recipes);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StarFivePass] {tuned} skill di-tuning, {reshaped} diubah jadi jatuh dari langit, " +
                      $"{added} piece bintang 5, {linked} resep. Total piece: {pieces.Count}.");
            Selection.activeObject = db;
        }

        // ---------- mana & cooldown ----------

        struct Tuning
        {
            public string Id;
            public float Damage;
            public float Cooldown;
            public float Mana;
        }

        /// <summary>
        /// Target: mana per second climbs with rarity. A 1-star skill sits near 5/s, a 2-star near
        /// 9/s, a 3-star near 13/s and the 4-star near 14/s — against a base regen of 10, that is
        /// what forces a build to pick a spine instead of stacking every nuke it owns.
        /// </summary>
        static readonly Tuning[] Table =
        {
            // 1-star — kept affordable; these have to carry the opening waves on their own.
            new Tuning { Id = "fireball",        Damage = 14f,  Cooldown = 1.10f, Mana = 6f },
            new Tuning { Id = "belatiberputar",  Damage = 11f,  Cooldown = 1.50f, Mana = 9f },
            new Tuning { Id = "frostnova",       Damage = 11f,  Cooldown = 3.00f, Mana = 13f },
            new Tuning { Id = "arcbolt",         Damage = 10f,  Cooldown = 1.90f, Mana = 12f },
            new Tuning { Id = "hujanapi",        Damage = 16f,  Cooldown = 2.40f, Mana = 13f },
            new Tuning { Id = "sabetanpetir",    Damage = 14f,  Cooldown = 1.90f, Mana = 12f },
            new Tuning { Id = "kubanganracun",   Damage = 5f,   Cooldown = 5.00f, Mana = 15f },

            // 2-star
            new Tuning { Id = "greaterfireball", Damage = 34f,  Cooldown = 1.05f, Mana = 17f },
            new Tuning { Id = "steamburst",      Damage = 18f,  Cooldown = 2.50f, Mana = 21f },
            new Tuning { Id = "blizzard",        Damage = 21f,  Cooldown = 3.00f, Mana = 26f },
            new Tuning { Id = "badaisalju",      Damage = 7f,   Cooldown = 6.00f, Mana = 23f },
            new Tuning { Id = "greaterheal",     Damage = 45f,  Cooldown = 10.0f, Mana = 34f },

            // 3-star
            new Tuning { Id = "meteor",          Damage = 70f,  Cooldown = 3.20f, Mana = 34f },
            new Tuning { Id = "prismabeku",      Damage = 34f,  Cooldown = 2.40f, Mana = 32f },

            // 4-star. Base damage is untouched on purpose: Nova casts only just started receiving
            // player damage buffs and crit at all, so this skill got much stronger without the
            // number moving. Raising it on top of that would have doubled the change.
            new Tuning { Id = "novakiamat",      Damage = 110f, Cooldown = 4.20f, Mana = 58f }
        };

        static int ApplyTuning(ContentDatabase db)
        {
            int n = 0;

            for (int i = 0; i < Table.Length; i++)
            {
                var piece = db.ById(Table[i].Id);
                if (piece == null)
                {
                    Debug.LogWarning($"[StarFivePass] piece '{Table[i].Id}' tidak ada, dilewati.");
                    continue;
                }

                piece.BaseDamage = Table[i].Damage;
                piece.BaseCooldown = Table[i].Cooldown;
                piece.ManaCost = Table[i].Mana;
                EditorUtility.SetDirty(piece);
                n++;
            }

            return n;
        }

        /// <summary>
        /// Meteor was a Nova, which means it detonated on the caster's own feet. A meteor that lands
        /// on you is not a meteor. It drops on the thickest part of the crowd now, which is also the
        /// only way it gets a falling shot to draw.
        /// </summary>
        static int MakeMeteorFall(ContentDatabase db)
        {
            var meteor = db.ById("meteor");
            if (meteor == null) return 0;

            meteor.Kind = CastKind.AreaAtTarget;
            meteor.Range = 11f;
            meteor.Radius = 5.5f;
            meteor.Blurb = "2 long. Falls out of the sky onto the densest pack. Applies BURN.";
            EditorUtility.SetDirty(meteor);
            return 1;
        }

        // ---------- 5-star tier ----------

        static int AddStarFive(ContentDatabase db, List<PieceDefinition> pieces)
        {
            var burn = db.StatusById("burn");
            var chill = db.StatusById("chill");
            var drag = db.StatusById("seret");

            var made = new List<PieceDefinition>
            {
                Skill("cataclysm", "Cataclysm", ShapeKind.Square, CastKind.AreaAtTarget, Element.Fire,
                    new Color(1f, 0.35f, 0.1f),
                    damage: 260f, cooldown: 5.5f, radius: 11f, range: 12f, mana: 82f,
                    status: burn, statusDuration: 5f, points: 3,
                    "2x2. The sky opens. Nothing inside the crater survives twice. Applies BURN."),

                Skill("absolutezero", "Absolute Zero", ShapeKind.Line3, CastKind.AreaAtTarget, Element.Ice,
                    new Color(0.72f, 0.95f, 1f),
                    damage: 165f, cooldown: 3.4f, radius: 7.5f, range: 13f, mana: 66f,
                    status: chill, statusDuration: 5f, points: 3,
                    "3 long. Reaches further than anything else in the book. Applies CHILL."),

                Skill("singularity", "Singularity", ShapeKind.Square, CastKind.Zone, Element.Arcane,
                    new Color(0.62f, 0.35f, 0.95f),
                    damage: 34f, cooldown: 7f, radius: 6f, range: 12f, mana: 74f,
                    status: drag, statusDuration: 2f, points: 2,
                    "2x2. Drags the swarm into itself and grinds for six seconds. Applies DRAG.")
            };

            // The zone half of Singularity: how long it grinds and how often.
            made[2].ZoneDuration = 6f;
            made[2].ZoneTickInterval = 0.4f;
            EditorUtility.SetDirty(made[2]);

            int added = 0;
            for (int i = 0; i < made.Count; i++)
            {
                if (pieces.Contains(made[i])) continue;
                pieces.Add(made[i]);
                added++;
            }

            return added;
        }

        static int AddStarFiveRecipes(ContentDatabase db, List<RecipeDefinition> recipes)
        {
            int before = recipes.Count;

            // Two routes into the tier so a run is not funnelled down one branch: the fire route
            // wants a second Meteor, the arcane route pays with two cheap 1-star utility pieces.
            Add(recipes, db, "cataclysm", "cataclysm", "novakiamat", "meteor");
            Add(recipes, db, "singularity", "singularity", "novakiamat", "pusaran", "kubanganracun");
            Add(recipes, db, "absolutezero", "absolutezero", "prismabeku", "blizzard", "badaisalju");

            return recipes.Count - before;
        }

        // ---------- asset plumbing ----------

        static PieceDefinition Skill(string id, string label, ShapeKind shape, CastKind kind,
            Element element, Color color, float damage, float cooldown, float radius, float range,
            float mana, StatusDefinition status, float statusDuration, int points, string blurb)
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

            // 5 stars is what keeps these out of every drop table and every shop roll: the database
            // only ever offers 1-star pieces, with a narrow window for 2-star.
            asset.Stars = 5;

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
