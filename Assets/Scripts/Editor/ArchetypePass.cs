using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// The four kinds of enemy, and when each one starts showing up.
    ///
    /// Each asks the build a different question, and that is the point — one enemy type only ever
    /// asks "is your damage high enough".
    ///
    /// - GRUNT walks at you. The baseline everything else is measured against.
    /// - CURSED is slow and tough and leaves something on you. Answer: kill it first, or resist.
    /// - STALKER flies, is fast and fragile, and dives straight instead of circling. Answer: any
    ///   area damage at all — but it arrives before your slow skills come off cooldown.
    /// - SPITTER stops out of reach and shoots. Answer: REACH. It is the one enemy a short-range
    ///   build simply cannot touch while the wave is live, which is what finally makes Range and
    ///   the Nadir Rune worth a slot.
    ///
    /// Spitter is deliberately unable to stall the wave forever: once spawning stops, every
    /// archetype abandons its stand-off and charges. See EnemyManager.Closing.
    ///
    /// Idempotent: matched by asset id.
    /// </summary>
    public static class ArchetypePass
    {
        const string Root = "Assets/GameData";
        const string Folder = Root + "/Enemies";

        [MenuItem("Tools/Grimoire/Generate Enemy Archetypes")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[ArchetypePass] ContentDatabase.asset tidak ketemu.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Root, "Enemies");

            var grunt = Make("grunt", "Grunt", fromWave: 1, weight: 10f, weightPerWave: 0f);
            grunt.Blurb = "Berjalan lurus ke arahmu. Tidak ada yang istimewa, dan jumlahnya banyak.";
            EditorUtility.SetDirty(grunt);

            var cursed = Make("cursed", "Cursed", fromWave: 3, weight: 1.2f, weightPerWave: 0.25f);
            cursed.HpMultiplier = 1.6f;
            cursed.SpeedMultiplier = 0.85f;
            cursed.Scale = 1.45f;
            cursed.UseTint = true;
            cursed.Tint = new Color(0.5f, 0.32f, 0.5f);
            cursed.Curse = FirstDebuff(db);
            cursed.Blurb = "Besar, lambat, tebal. Menempelkan kutukan tiap kali menyentuhmu.";
            EditorUtility.SetDirty(cursed);

            var stalker = Make("stalker", "Stalker", fromWave: 5, weight: 1.5f, weightPerWave: 0.35f);
            stalker.HpMultiplier = 0.5f;
            stalker.SpeedMultiplier = 1.55f;
            stalker.Scale = 0.75f;
            stalker.HoverHeight = 2.3f;

            // Dives instead of circling: a flyer that swings wide is just a slow grunt.
            stalker.Flanks = false;

            stalker.UseTint = true;
            stalker.Tint = new Color(0.55f, 0.78f, 0.95f);
            stalker.Blurb = "Terbang, cepat, tipis. Menukik lurus — tidak ikut memutar mengepung.";
            EditorUtility.SetDirty(stalker);

            var spitter = Make("spitter", "Spitter", fromWave: 7, weight: 1f, weightPerWave: 0.3f);
            spitter.HpMultiplier = 1.15f;
            spitter.SpeedMultiplier = 0.9f;
            spitter.Scale = 1.1f;
            spitter.PreferredRange = 11f;
            spitter.AttackInterval = 2.2f;
            spitter.AttackDamage = 9f;
            spitter.ShotSpeed = 12f;
            spitter.ShotColor = new Color(0.65f, 1f, 0.45f);
            spitter.UseTint = true;
            spitter.Tint = new Color(0.45f, 0.62f, 0.35f);
            spitter.Blurb = "Berhenti di luar jangkauan dan meludah. Butuh skill ber-JANGKAUAN " +
                            "panjang untuk menyentuhnya sebelum wave menutup.";
            EditorUtility.SetDirty(spitter);

            var list = new List<EnemyArchetype> { grunt, cursed, stalker, spitter };
            db.EditorSetArchetypes(list);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ArchetypePass] {list.Count} arketipe.\n{Report(list)}");
            Selection.activeObject = db;
        }

        static BuffDefinition FirstDebuff(ContentDatabase db) =>
            db.Debuffs.Count > 0 ? db.Debuffs[0] : null;

        static EnemyArchetype Make(string id, string name, int fromWave, float weight,
            float weightPerWave)
        {
            string path = $"{Folder}/Enemy_{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<EnemyArchetype>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<EnemyArchetype>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Id = id;
            asset.DisplayName = name;
            asset.FromWave = fromWave;
            asset.Weight = weight;
            asset.WeightPerWave = weightPerWave;
            asset.WeightMax = 20f;

            // Reset to plain first, so a re-run cannot leave a stale field from an older shape.
            asset.HpMultiplier = 1f;
            asset.SpeedMultiplier = 1f;
            asset.Scale = 1f;
            asset.HoverHeight = 0f;
            asset.UseTint = false;
            asset.Tint = Color.white;
            asset.PreferredRange = 0f;
            asset.Flanks = true;
            asset.AttackInterval = 0f;
            asset.AttackDamage = 8f;
            asset.ShotSpeed = 11f;
            asset.Curse = null;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>Campuran musuh per wave, supaya kurvanya bisa dibaca balik, bukan ditebak.</summary>
        static string Report(List<EnemyArchetype> list)
        {
            var sb = new System.Text.StringBuilder("wave | ");
            foreach (var k in list) sb.Append(k.DisplayName).Append(' ');
            sb.Append('\n');

            foreach (int w in new[] { 1, 3, 5, 7, 10, 15, 20 })
            {
                float total = 0f;
                foreach (var k in list) total += k.WeightAt(w);

                sb.Append(" ").Append(w).Append("   | ");
                foreach (var k in list)
                {
                    sb.Append(Mathf.RoundToInt(100f * k.WeightAt(w) / Mathf.Max(0.01f, total)))
                        .Append("% ");
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }
    }
}
