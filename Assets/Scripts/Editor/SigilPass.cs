using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Segel stat: empat sumbu yang bisa dinaikkan pemain, masing-masing sampai bintang tiga.
    ///
    /// Dua celah nyata ditutup di sini.
    ///
    /// <b>Pertama, tidak ada satu pun segel yang menaikkan damage.</b> Hanya rune yang bisa, dan
    /// rune tinggal di lapisan bawah — artinya "aku mau memukul lebih keras" bukan keputusan yang
    /// bisa dibuat pemain di lapisan skill sama sekali. Padahal itu keinginan paling dasar yang
    /// dibawa orang ke game seperti ini.
    ///
    /// <b>Kedua, segel berhenti di bintang dua</b> sementara skill sampai bintang lima. Jadi di
    /// endgame seluruh papan berisi skill raksasa dan segel yang nilainya sama dengan wave lima.
    ///
    /// Empat sumbu itu sengaja: NYAWA, MANA, TAHAN, SERANG. Empat cukup untuk membuat pilihan
    /// terasa berbeda, dan sedikit cukup supaya tiap satu tetap terbaca.
    /// </summary>
    public static class SigilPass
    {
        const string Root = "Assets/GameData";
        const string PieceFolder = Root + "/Pieces";
        const string RecipeFolder = Root + "/Recipes";

        [MenuItem("Tools/Grimoire/Generate Stat Sigils")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[SigilPass] ContentDatabase.asset tidak ketemu.");
                return;
            }

            var pieces = new List<PieceDefinition>(db.Pieces);
            var recipes = new List<RecipeDefinition>(db.Recipes);
            int before = pieces.Count;

            // ---- SERANG: sumbu yang sebelumnya tidak ada sama sekali ----
            Sigil(pieces, "segelpedang", "Whetstone", 1, ShapeKind.Line2,
                new Color(0.95f, 0.5f, 0.35f),
                "Semua skill memukul 12% lebih keras.",
                Mod(StatKind.DamagePct, 0.12f));

            Sigil(pieces, "segelamarah", "Wrath Sigil", 2, ShapeKind.Square,
                new Color(1f, 0.42f, 0.28f),
                "Semua skill memukul 30% lebih keras.",
                Mod(StatKind.DamagePct, 0.3f));

            Sigil(pieces, "segelmurka", "Sigil of Fury", 3, ShapeKind.Cross,
                new Color(1f, 0.32f, 0.24f),
                "+55% damage, dan crit menghantam jauh lebih dalam.",
                Mod(StatKind.DamagePct, 0.55f), Mod(StatKind.CritDamage, 0.5f));

            // ---- NYAWA ----
            Sigil(pieces, "segelbenteng", "Bulwark Sigil", 3, ShapeKind.Wedge,
                new Color(0.6f, 0.8f, 0.95f),
                "+120 HP maksimum dan pengurangan damage yang tebal.",
                Mod(StatKind.MaxHp, 120f), Mod(StatKind.Defense, 8f));

            // ---- MANA ----
            Sigil(pieces, "segeltelaga", "Deep Well", 2, ShapeKind.Ell,
                new Color(0.45f, 0.62f, 1f),
                "+55 mana maksimum. Papan yang penuh butuh kolam yang dalam, bukan cuma regen.",
                Mod(StatKind.MaxMana, 55f));

            Sigil(pieces, "segelsamudra", "Tidecaller", 3, ShapeKind.Cup,
                new Color(0.35f, 0.55f, 1f),
                "+90 mana maksimum, regen lebih deras, dan semua skill lebih murah.",
                Mod(StatKind.MaxMana, 90f), Mod(StatKind.ManaRegen, 6f),
                Mod(StatKind.ManaCostPct, 0.12f));

            // ---- TAHAN ----
            Sigil(pieces, "segelperisai", "Ironhide", 2, ShapeKind.Tee,
                new Color(0.72f, 0.76f, 0.8f),
                "Pengurangan damage yang nyata, dengan sedikit ongkos kecepatan.",
                Mod(StatKind.Defense, 6f), Mod(StatKind.MoveSpeed, -0.15f));

            db.EditorSet(pieces, recipes);

            int linked = Link(db, recipes);
            db.EditorSet(pieces, recipes);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SigilPass] {pieces.Count - before} segel baru, {linked} resep. " +
                      $"Total piece: {pieces.Count}.");
        }

        /// <summary>
        /// Tanpa resep, semua di atas bintang satu tidak bisa didapat: drop cuma mengeluarkan
        /// bintang satu dan toko tidak menyimpan semuanya.
        /// </summary>
        static int Link(ContentDatabase db, List<RecipeDefinition> recipes)
        {
            int before = recipes.Count;

            R(recipes, db, "segelamarah_a", "segelamarah", "segelpedang", "segelpedang");
            R(recipes, db, "segelamarah_b", "segelamarah", "segelpedang", "segelmata");
            R(recipes, db, "segelmurka_a", "segelmurka", "segelamarah", "segeltajam");
            R(recipes, db, "segelmurka_b", "segelmurka", "segelamarah", "segelamarah");

            R(recipes, db, "segeltelaga_a", "segeltelaga", "segelnadi", "segelnadi");
            R(recipes, db, "segeltelaga_b", "segeltelaga", "segelnadi", "segelbara");
            R(recipes, db, "segelsamudra_a", "segelsamudra", "segeltelaga", "segelarus");

            R(recipes, db, "segelperisai_a", "segelperisai", "tintamengalir", "tintamengalir");
            R(recipes, db, "segelbenteng_a", "segelbenteng", "segelagung", "segelperisai");
            R(recipes, db, "segelbenteng_b", "segelbenteng", "segelagung", "segelagung");

            return recipes.Count - before;
        }

        static void Sigil(List<PieceDefinition> pieces, string id, string name, int stars,
            ShapeKind shape, Color color, string blurb, params StatModifier[] stats)
        {
            string path = $"{PieceFolder}/Piece_{id}.asset";
            var a = AssetDatabase.LoadAssetAtPath<PieceDefinition>(path);

            if (a == null)
            {
                a = ScriptableObject.CreateInstance<PieceDefinition>();
                AssetDatabase.CreateAsset(a, path);
            }

            a.Id = id;
            a.DisplayName = name;
            a.Stars = stars;
            a.Layer = Layer.Skill;

            // Invarian: segel WAJIB Passive. Kind lain akan membuatnya ikut antre menembak, dan
            // sebuah segel yang "menembak" tanpa damage cuma memakan mana tiap cooldown.
            a.Kind = CastKind.Passive;

            a.Element = Element.Arcane;
            a.Shape = shape;
            a.Color = color;
            a.Stats = stats;
            a.Blurb = blurb;
            a.BaseDamage = 0f;
            a.ManaCost = 0f;
            a.BaseCooldown = 1f;

            EditorUtility.SetDirty(a);
            if (!pieces.Contains(a)) pieces.Add(a);
        }

        static StatModifier Mod(StatKind kind, float value) =>
            new StatModifier { Type = kind, Value = value };

        static void R(List<RecipeDefinition> recipes, ContentDatabase db, string fileId,
            string resultId, params string[] ingredientIds)
        {
            var result = db.ById(resultId);
            if (result == null)
            {
                Debug.LogWarning($"[SigilPass] hasil '{resultId}' tidak ada, resep dilewati.");
                return;
            }

            var ingredients = new PieceDefinition[ingredientIds.Length];

            for (int i = 0; i < ingredientIds.Length; i++)
            {
                ingredients[i] = db.ById(ingredientIds[i]);
                if (ingredients[i] != null) continue;

                // Resep dengan bahan kosong akan diam-diam cocok dengan apa pun.
                Debug.LogWarning($"[SigilPass] bahan '{ingredientIds[i]}' tidak ada, " +
                                 $"resep '{fileId}' dilewati.");
                return;
            }

            string path = $"{RecipeFolder}/Recipe_{fileId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RecipeDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Ingredients = ingredients;
            asset.Result = result;
            EditorUtility.SetDirty(asset);

            if (!recipes.Contains(asset)) recipes.Add(asset);
        }
    }
}
