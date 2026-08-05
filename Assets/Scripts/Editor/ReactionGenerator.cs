using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Fills in the reaction table. Before this only 4 of 15 status pairs reacted, SERET could be
    /// applied but was never an ingredient in anything, and two buffs had no source at all — so the
    /// "debuff -> reaction -> buff -> stronger skill" loop only closed in a few places.
    ///
    /// Idempotent: re-running updates the same assets instead of adding duplicates.
    /// </summary>
    public static class ReactionGenerator
    {
        const string Root = "Assets/GameData";
        const string StatusFolder = Root + "/Statuses";
        const string ReactionFolder = Root + "/Reactions";

        [MenuItem("Tools/Grimoire/Generate Reactions & Stun")]
        public static void Generate()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[ReactionGenerator] ContentDatabase.asset tidak ketemu.");
                return;
            }

            var statuses = new List<StatusDefinition>(db.Statuses);

            var burn = Find(statuses, "burn");
            var chill = Find(statuses, "chill");
            var shock = Find(statuses, "shock");
            var bleed = Find(statuses, "bleed");
            var poison = Find(statuses, "poison");
            var seret = Find(statuses, "seret");

            if (burn == null || chill == null || shock == null || bleed == null || poison == null || seret == null)
            {
                Debug.LogError("[ReactionGenerator] status dasar belum lengkap. " +
                               "Jalankan 'Generate Ailments' dan 'Generate Buffs, Seret & Recipes' dulu.");
                return;
            }

            // STUN is just a total move-speed stop. Enemies only hurt by touching, so freezing
            // movement is a real disable without needing a new mechanic in the tick loop.
            var stun = Upsert(statuses, "stun");
            stun.DisplayName = "STUN";
            stun.Color = new Color(1f, 0.95f, 0.6f);
            stun.MaxPoints = 1;
            stun.RefreshOnReapply = true;
            stun.TickInterval = 0f;
            stun.DamagePerTickPerPoint = 0f;
            stun.MoveSpeedMultiplier = 0f;
            stun.DamageTakenMultiplier = 1.15f;
            stun.PullStrength = 0f;
            stun.Blurb = "Berhenti total. Sebentar saja, tapi cukup untuk menumpuk ailment lain.";
            EditorUtility.SetDirty(stun);

            var buffs = new List<BuffDefinition>(db.Buffs);
            var aliran = FindBuff(buffs, "aliran");
            var fokus = FindBuff(buffs, "fokus");
            var perisai = FindBuff(buffs, "perisai");

            var reactions = new List<ReactionDefinition>(db.Reactions);

            // Order is priority: one application fires at most one reaction, first match wins.
            // SERET pairs go first — gathering the crowd is what makes every other reaction matter.
            Upsert(reactions, "badaiapi", "BADAI API", seret, burn,
                consumeA: true, consumeB: false, burst: 26f, perPointA: 0f, radius: 4.2f,
                apply: burn, applyPoints: 2, spread: true, grant: null,
                flash: new Color(1f, 0.55f, 0.2f));

            Upsert(reactions, "pusaranbeku", "PUSARAN BEKU", seret, chill,
                consumeA: true, consumeB: false, burst: 22f, perPointA: 0f, radius: 4.2f,
                apply: chill, applyPoints: 1, spread: true, grant: fokus,
                flash: new Color(0.6f, 0.9f, 1f));

            Upsert(reactions, "bekustatis", "BEKU STATIS", chill, shock,
                consumeA: true, consumeB: true, burst: 30f, perPointA: 0f, radius: 3f,
                apply: stun, applyPoints: 1, spread: false, grant: aliran,
                flash: new Color(0.75f, 0.85f, 1f));

            Upsert(reactions, "bakarluka", "BAKAR LUKA", bleed, burn,
                consumeA: true, consumeB: false, burst: 14f, perPointA: 9f, radius: 2.6f,
                apply: null, applyPoints: 1, spread: false, grant: perisai,
                flash: new Color(1f, 0.4f, 0.35f));

            Upsert(reactions, "nanah", "NANAH", bleed, poison,
                consumeA: true, consumeB: true, burst: 18f, perPointA: 7f, radius: 2.8f,
                apply: null, applyPoints: 1, spread: false, grant: null,
                flash: new Color(0.7f, 0.9f, 0.4f));

            db.EditorSetAilments(statuses, reactions, buffs);
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ReactionGenerator] {statuses.Count} status (STUN baru), " +
                      $"{reactions.Count} reaksi, {buffs.Count} buff. " +
                      "SERET akhirnya jadi bahan reaksi.");
            Selection.activeObject = db;
        }

        static StatusDefinition Find(List<StatusDefinition> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].Id == id) return list[i];
            }

            return null;
        }

        static BuffDefinition FindBuff(List<BuffDefinition> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].Id == id) return list[i];
            }

            return null;
        }

        static StatusDefinition Upsert(List<StatusDefinition> list, string id)
        {
            var existing = Find(list, id);
            if (existing != null) return existing;

            string path = $"{StatusFolder}/Status_{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<StatusDefinition>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<StatusDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Id = id;
            list.Add(asset);
            return asset;
        }

        static void Upsert(List<ReactionDefinition> list, string id, string label,
            StatusDefinition a, StatusDefinition b, bool consumeA, bool consumeB,
            float burst, float perPointA, float radius,
            StatusDefinition apply, int applyPoints, bool spread, BuffDefinition grant, Color flash)
        {
            string path = $"{ReactionFolder}/Reaction_{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ReactionDefinition>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ReactionDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.DisplayName = label;
            asset.A = a;
            asset.B = b;
            asset.MinPointsA = 1;
            asset.MinPointsB = 1;
            asset.ConsumeA = consumeA;
            asset.ConsumeB = consumeB;
            asset.BurstDamage = burst;
            asset.BurstDamagePerPointA = perPointA;
            asset.BurstRadius = radius;
            asset.ApplyStatus = apply;
            asset.ApplyDuration = apply == null ? 0f : 2.5f;
            asset.ApplyPoints = Mathf.Max(1, applyPoints);
            asset.SpreadToNearby = spread;
            asset.GrantBuff = grant;
            asset.FlashColor = flash;
            EditorUtility.SetDirty(asset);

            if (!list.Contains(asset)) list.Add(asset);
        }
    }
}
