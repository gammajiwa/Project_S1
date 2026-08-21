using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Sample high-rarity runes. Their point is to prove one thing: a rune can carry player stats
    /// on top of its aura, and rarity gates it out of the drop pool.
    /// </summary>
    public static class RareRuneGenerator
    {
        const string Root = "Assets/GameData";
        const string PieceFolder = Root + "/Pieces";

        [MenuItem("Tools/Grimoire/Add Sample Rare Runes")]
        public static void Generate()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>($"{Root}/ContentDatabase.asset");
            if (database == null)
            {
                Debug.LogError("ContentDatabase.asset nggak ketemu.");
                return;
            }

            var made = new List<PieceDefinition>
            {
                Rune("runebenteng", "Rune Benteng", 2, ShapeKind.Square,
                    new Color(0.6f, 0.45f, 0.3f), AuraKind.None, 0f,
                    new[]
                    {
                        Mod(StatKind.MaxHp, 60f),
                        Mod(StatKind.Defense, 4f)
                    },
                    "2x2, bintang 2. +60 HP dan +4 pertahanan. Nggak ngasih buff ke skill di atasnya."),

                Rune("runearus", "Rune Arus", 2, ShapeKind.Line3,
                    new Color(0.35f, 0.65f, 0.95f), AuraKind.CooldownPct, 0.12f,
                    new[]
                    {
                        Mod(StatKind.ManaCostPct, 0.15f),
                        Mod(StatKind.MaxMana, 20f)
                    },
                    "3 panjang, bintang 2. Cooldown skill di atasnya -12%, plus mana skill -15% dan +20 mana."),

                Rune("runebadai", "Rune Badai", 3, ShapeKind.Big3,
                    new Color(0.95f, 0.85f, 0.35f), AuraKind.DamagePct, 0.15f,
                    new[]
                    {
                        Mod(StatKind.LightningDamagePct, 0.25f),
                        Mod(StatKind.AilmentPoints, 1f),
                        Mod(StatKind.CooldownPct, 0.08f)
                    },
                    "3x3, bintang 3. Damage petir +25%, tiap ailment nempel +1 poin, cooldown semua -8%.")
            };

            var pieces = new List<PieceDefinition>(database.Pieces);
            foreach (var piece in made)
            {
                if (!pieces.Contains(piece)) pieces.Add(piece);
            }

            database.EditorSet(pieces, new List<RecipeDefinition>(database.Recipes));
            EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[RareRuneGenerator] {made.Count} rune langka siap. Total piece: {pieces.Count}.");
            Selection.activeObject = database;
        }

        static StatModifier Mod(StatKind type, float value) => new StatModifier { Type = type, Value = value };

        static PieceDefinition Rune(string id, string label, int stars, ShapeKind shape, Color color,
            AuraKind aura, float auraValue, StatModifier[] stats, string blurb)
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
            asset.Layer = Layer.Rune;
            asset.Kind = CastKind.AuraOnly;
            asset.Element = Element.Arcane;
            asset.Shape = shape;
            asset.Color = color;
            asset.Aura = aura;
            asset.AuraValue = auraValue;
            asset.Stats = stats;
            asset.Blurb = blurb;

            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}
