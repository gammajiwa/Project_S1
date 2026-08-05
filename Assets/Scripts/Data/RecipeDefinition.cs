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

            for (int i = 0; i < Ingredients.Length; i++)
            {
                if (Ingredients[i] == null) continue;
                if (Ingredients[i].Layer != Layer.Rune) continue;

                Debug.LogWarning($"[{name}] bahan '{Ingredients[i].name}' adalah RUNE. " +
                                 "Resep cuma boleh dari skill/segel.", this);
            }
        }
    }
}
