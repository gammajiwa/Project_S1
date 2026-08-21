using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 2–3 pieces placed adjacent AND in one straight line merge into the result at wave end.
    /// Ingredients are direct references, not ids, so a rename can never break a recipe.
    /// </summary>
    [CreateAssetMenu(fileName = "Recipe_", menuName = "Grimoire/Recipe")]
    public class RecipeDefinition : ScriptableObject
    {
        [Tooltip("2 atau 3 bahan. Urutan tidak berpengaruh.")]
        public PieceDefinition[] Ingredients;

        public PieceDefinition Result;

        public bool IsValid =>
            Result != null && Ingredients != null && Ingredients.Length >= 2 && Ingredients.Length <= 3;

        void OnValidate()
        {
            if (Ingredients == null) return;

            int cells = 0;

            for (int i = 0; i < Ingredients.Length; i++)
            {
                var ingredient = Ingredients[i];
                if (ingredient == null) continue;

                cells += ingredient.Cells.Length;

                if (ingredient.Layer == Layer.Rune)
                {
                    Debug.LogWarning($"[{name}] bahan '{ingredient.name}' adalah RUNE. " +
                                     "Resep cuma boleh dari skill/segel.", this);
                }

            }

            // Ingredients only need to touch each other, so the real ceiling is the whole board.
            int board = Grimoire.Width * Grimoire.Height;
            if (cells > board)
            {
                Debug.LogError($"[{name}] butuh {cells} petak, tapi papan cuma {board}. " +
                               "Resep ini MUSTAHIL dievolusikan.", this);
            }
        }
    }
}
