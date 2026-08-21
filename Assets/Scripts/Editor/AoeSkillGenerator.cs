using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Area skills. Nova already existed but always detonated on the player, which is the worst
    /// place for it — these three land where the crowd actually is.
    /// </summary>
    public static class AoeSkillGenerator
    {
        const string Root = "Assets/GameData";
        const string PieceFolder = Root + "/Pieces";

        [MenuItem("Tools/Grimoire/Add AoE Skills")]
        public static void Generate()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>($"{Root}/ContentDatabase.asset");
            if (database == null)
            {
                Debug.LogError("ContentDatabase.asset nggak ketemu.");
                return;
            }

            var burn = database.StatusById("burn");
            var chill = database.StatusById("chill");
            var shock = database.StatusById("shock");
            var poison = database.StatusById("poison");
            var bleed = database.StatusById("bleed");

            var made = new List<PieceDefinition>
            {
                Skill("hujanapi", "Hujan Api", 1, ShapeKind.Square, CastKind.AreaAtTarget, Element.Fire,
                    new Color(1f, 0.55f, 0.2f),
                    damage: 16f, cooldown: 2.4f, radius: 3.4f, range: 11f, mana: 12f,
                    status: burn, statusDuration: 4f, points: 1,
                    "2x2. Jatuh di gerombolan paling padat, bukan di kamu. Nempel BURN."),

                Skill("sabetanpetir", "Sabetan Petir", 1, ShapeKind.Line3, CastKind.Line, Element.Lightning,
                    new Color(0.95f, 0.9f, 0.4f),
                    damage: 14f, cooldown: 2f, radius: 1.1f, range: 10f, mana: 11f,
                    status: shock, statusDuration: 3f, points: 1,
                    "3 panjang. Menyapu garis lurus ke arah musuh terdekat, kena semua yang dilewati."),

                Skill("kubanganracun", "Kubangan Racun", 1, ShapeKind.Corner, CastKind.Zone, Element.Arcane,
                    new Color(0.5f, 0.85f, 0.35f),
                    damage: 5f, cooldown: 5f, radius: 3f, range: 9f, mana: 14f,
                    status: poison, statusDuration: 5f, points: 2,
                    "Bentuk L. Ninggalin kubangan racun 5 detik yang berdenyut terus. Sumber POISON."),

                Skill("badaisalju", "Badai Salju", 2, ShapeKind.Line3, CastKind.Zone, Element.Ice,
                    new Color(0.6f, 0.9f, 1f),
                    damage: 7f, cooldown: 6f, radius: 4.2f, range: 10f, mana: 20f,
                    status: chill, statusDuration: 3f, points: 1,
                    "EVOLVED. Kubangan es lebar yang bikin semua di dalamnya melambat terus-menerus."),

                Skill("belatiberputar", "Belati Berputar", 1, ShapeKind.Line2, CastKind.Nova, Element.Arcane,
                    new Color(0.9f, 0.3f, 0.35f),
                    damage: 11f, cooldown: 1.6f, radius: 3.2f, range: 3.2f, mana: 8f,
                    status: bleed, statusDuration: 4f, points: 2,
                    "2 panjang. Berputar di sekelilingmu. Sumber BLEED, dan penjaga jarak dekat.")
            };

            // Zone skills need their own timing fields.
            Zone(made[2], 5f, 0.5f);
            Zone(made[3], 6f, 0.6f);

            var pieces = new List<PieceDefinition>(database.Pieces);
            foreach (var piece in made)
            {
                if (!pieces.Contains(piece)) pieces.Add(piece);
            }

            database.EditorSet(pieces, new List<RecipeDefinition>(database.Recipes));
            EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AoeSkillGenerator] {made.Count} skill AoE siap. Total piece: {pieces.Count}.");
            Selection.activeObject = database;
        }

        static void Zone(PieceDefinition piece, float duration, float tick)
        {
            piece.ZoneDuration = duration;
            piece.ZoneTickInterval = tick;
            EditorUtility.SetDirty(piece);
        }

        static PieceDefinition Skill(string id, string label, int stars, ShapeKind shape, CastKind kind,
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
    }
}
