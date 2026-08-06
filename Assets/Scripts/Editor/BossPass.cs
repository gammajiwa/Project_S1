using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Membuat aset boss ular dan menyambungkannya ke <see cref="ContentDatabase"/>.
    /// Idempoten: dicocokkan lewat path aset, dan menimpa nilainya, bukan membuat duplikat.
    /// </summary>
    public static class BossPass
    {
        const string Root = "Assets/GameData";
        const string Path = Root + "/Enemies/Boss_serpent.asset";

        [MenuItem("Tools/Grimoire/Generate Boss")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[BossPass] ContentDatabase.asset tidak ketemu.");
                return;
            }

            var boss = AssetDatabase.LoadAssetAtPath<BossDefinition>(Path);

            if (boss == null)
            {
                boss = ScriptableObject.CreateInstance<BossDefinition>();
                AssetDatabase.CreateAsset(boss, Path);
            }

            boss.Id = "serpent";
            boss.DisplayName = "The Coiled Dread";

            boss.MaxSegments = 24;
            boss.MinSegments = 4;
            boss.Spacing = 1.05f;
            boss.HeadScale = 2.6f;
            boss.TailScale = 0.85f;

            // Cukup tebal untuk sempat menunjukkan perilakunya. Boss yang mati dalam dua cast
            // adalah musuh besar, bukan boss — tidak ada yang sempat terbaca.
            boss.HpMultiplier = 90f;

            boss.OrbitRadius = 13f;
            boss.Speed = 6.5f;
            boss.LungeSpeed = 15f;
            boss.TurnRate = 150f;
            boss.Wander = 1.4f;

            boss.LungeInterval = 6f;
            boss.LungeDuration = 1.8f;

            // Satu hantaman keras, bukan gerusan per detik. Yang harus dipelajari pemain adalah
            // kapan terjangannya datang, dan itu cuma punya arti kalau kena telak.
            boss.BiteDamage = 26f;
            boss.BiteRange = 2.8f;
            boss.Curse = FindCurse(db, "leaden");

            boss.HeadColor = new Color(0.95f, 0.32f, 0.28f);
            boss.BodyColor = new Color(0.42f, 0.16f, 0.32f);

            EditorUtility.SetDirty(boss);

            db.EditorSetBoss(boss);
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BossPass] '{boss.DisplayName}' siap: {boss.MaxSegments} ruas, " +
                      $"HP x{boss.HpMultiplier}, gigit {boss.BiteDamage}. Tersambung ke database.");
            Selection.activeObject = boss;
        }

        /// <summary>
        /// Kutukan gigitannya. LEADEN dipilih karena memperlambat, dan itu justru yang paling
        /// menyakitkan dari makhluk yang seluruh ancamannya adalah menerjang.
        /// </summary>
        static BuffDefinition FindCurse(ContentDatabase db, string id)
        {
            for (int i = 0; i < db.Debuffs.Count; i++)
            {
                var curse = db.Debuffs[i];
                if (curse != null && curse.Id == id) return curse;
            }

            return db.Debuffs.Count > 0 ? db.Debuffs[0] : null;
        }
    }
}
