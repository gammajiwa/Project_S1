using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Creates the starter ailments and reactions, registers them in the database, and points every
    /// existing piece at its status. Safe to re-run â€” it updates assets in place.
    /// </summary>
    public static class AilmentGenerator
    {
        const string Root = "Assets/GameData";
        const string StatusFolder = Root + "/Statuses";
        const string ReactionFolder = Root + "/Reactions";

        [MenuItem("Tools/Grimoire/Generate Ailments")]
        public static void Generate()
        {
            EnsureFolder(Root, "Statuses");
            EnsureFolder(Root, "Reactions");

            var burn = Status("burn", "BURN", new Color(1f, 0.45f, 0.15f), 3, 0.5f, 3.5f, 1f, 1f,
                "Damage berjalan. Menumpuk sampai 3.");
            var chill = Status("chill", "CHILL", new Color(0.45f, 0.8f, 1f), 1, 0f, 0f, 0.45f, 1f,
                "Memperlambat 55%.");
            var shock = Status("shock", "SHOCK", new Color(0.95f, 0.92f, 0.4f), 1, 0f, 0f, 1f, 1.3f,
                "Menerima damage +30%.");
            var bleed = Status("bleed", "BLEED", new Color(0.85f, 0.2f, 0.25f), 5, 0.4f, 2.2f, 1f, 1f,
                "Damage berjalan yang menumpuk sampai 5.");
            var poison = Status("poison", "POISON", new Color(0.55f, 0.9f, 0.35f), 5, 0.6f, 2f, 0.85f, 1f,
                "Damage berjalan, menumpuk, dan bisa meledak menular.");

            var statuses = new List<StatusDefinition> { burn, chill, shock, bleed, poison };

            var reactions = new List<ReactionDefinition>
            {
                Reaction("pecah", "PECAH", burn, chill, true, true,
                    46f, 0f, 3.2f, null, 0, false, Color.white),

                Reaction("ledakracun", "LEDAK RACUN", poison, burn, true, false,
                    10f, 9f, 3.6f, poison, 1, true, new Color(0.6f, 1f, 0.4f)),

                Reaction("arusdarah", "ARUS DARAH", bleed, shock, true, false,
                    14f, 6f, 4f, shock, 1, true, new Color(1f, 0.9f, 0.4f)),

                Reaction("bekuretak", "BEKU RETAK", chill, bleed, true, false,
                    18f, 0f, 2.5f, bleed, 2, false, new Color(0.7f, 0.95f, 1f))
            };

            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>($"{Root}/ContentDatabase.asset");
            if (database == null)
            {
                Debug.LogError("ContentDatabase.asset nggak ketemu. Jalankan Generate Content Assets dulu.");
                return;
            }

            database.EditorSetAilments(statuses, reactions);
            EditorUtility.SetDirty(database);

            // Explicit map: the legacy enum field does not survive the rename reliably, and
            // guessing from Element would be wrong for hybrids like Steam Burst.
            var byId = new Dictionary<string, StatusDefinition>
            {
                { "fireball", burn },
                { "greaterfireball", burn },
                { "steamburst", burn },
                { "meteor", burn },
                { "novakiamat", burn },
                { "frostnova", chill },
                { "blizzard", chill },
                { "prismabeku", chill },
                { "arcbolt", shock }
            };

            int migrated = 0;
            foreach (var piece in database.Pieces)
            {
                if (piece == null) continue;
                if (!byId.TryGetValue(piece.Id, out var status)) continue;

                piece.AppliedStatus = status;
                if (piece.AppliedPoints < 1) piece.AppliedPoints = 1;
                if (piece.StatusDuration <= 0f) piece.StatusDuration = 4f;

                EditorUtility.SetDirty(piece);
                migrated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AilmentGenerator] {statuses.Count} status, {reactions.Count} reaksi. " +
                      $"{migrated} piece dimigrasi ke AppliedStatus.");
            Selection.activeObject = database;
        }

        static StatusDefinition Status(string id, string label, Color color, int maxPoints,
            float tickInterval, float dpsPerStack, float speedMul, float takenMul, string blurb)
        {
            var asset = LoadOrCreate<StatusDefinition>($"{StatusFolder}/Status_{id}.asset");
            asset.Id = id;
            asset.DisplayName = label;
            asset.Color = color;
            asset.MaxPoints = maxPoints;
            asset.RefreshOnReapply = true;
            asset.TickInterval = Mathf.Max(0.05f, tickInterval);
            asset.DamagePerTickPerPoint = dpsPerStack;
            asset.MoveSpeedMultiplier = speedMul;
            asset.DamageTakenMultiplier = takenMul;
            asset.Blurb = blurb;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static ReactionDefinition Reaction(string id, string label, StatusDefinition a, StatusDefinition b,
            bool consumeA, bool consumeB, float burst, float burstPerStack, float radius,
            StatusDefinition apply, int applyPoints, bool spread, Color flash)
        {
            var asset = LoadOrCreate<ReactionDefinition>($"{ReactionFolder}/Reaction_{id}.asset");
            asset.DisplayName = label;
            asset.A = a;
            asset.B = b;
            asset.MinPointsA = 1;
            asset.MinPointsB = 1;
            asset.ConsumeA = consumeA;
            asset.ConsumeB = consumeB;
            asset.BurstDamage = burst;
            asset.BurstDamagePerPointA = burstPerStack;
            asset.BurstRadius = radius;
            asset.ApplyStatus = apply;
            asset.ApplyDuration = 4f;
            asset.ApplyPoints = Mathf.Max(1, applyPoints);
            asset.SpreadToNearby = spread;
            asset.FlashColor = flash;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        static void EnsureFolder(string parent, string child)
        {
            if (AssetDatabase.IsValidFolder($"{parent}/{child}")) return;
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
