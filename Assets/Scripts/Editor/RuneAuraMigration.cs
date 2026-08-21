using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// AuraValue changed meaning: it used to be "per cell", now it is the rune's TOTAL split across
    /// its cells. Multiply the old numbers by the cell count so nothing changes in play, then give
    /// the elemental runes an element and a matching bonus.
    /// </summary>
    public static class RuneAuraMigration
    {
        [MenuItem("Tools/Grimoire/Migrate Rune Auras")]
        public static void Migrate()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/GameData/ContentDatabase.asset");
            if (database == null)
            {
                Debug.LogError("ContentDatabase.asset nggak ketemu.");
                return;
            }

            int rescaled = 0;

            foreach (var piece in database.Pieces)
            {
                if (piece == null || !piece.IsRune) continue;
                if (piece.Aura == AuraKind.None || piece.AuraValue <= 0f) continue;

                // Guard against running twice: a total above 1.0 has clearly been scaled already.
                if (piece.AuraValue >= 0.9f) continue;

                piece.AuraValue *= piece.Cells.Length;
                EditorUtility.SetDirty(piece);
                rescaled++;
            }

            Elemental(database, "emberrune", Element.Fire, 0.40f);
            Elemental(database, "runebadai", Element.Lightning, 0.45f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[RuneAuraMigration] {rescaled} rune diskalakan ke nilai total.");
            Selection.activeObject = database;
        }

        static void Elemental(ContentDatabase database, string id, Element element, float matchBonus)
        {
            var piece = database.ById(id);
            if (piece == null) return;

            piece.Element = element;
            piece.ElementMatchBonus = matchBonus;
            EditorUtility.SetDirty(piece);
        }
    }
}
