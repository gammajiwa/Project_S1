using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Starting loadouts. One hero for now; the second is another asset, not another code path.
    ///
    /// The opening board is this game's only tutorial, so it is authored rather than left to
    /// auto-placement. Hero 1 opens holding two identical 1-cell skills sitting APART, on two
    /// bases of different rarity. That layout asks the whole game's question in the first ten
    /// seconds: keep two skills firing, or push them together and end the wave with one that hits
    /// twice as hard. Placed side by side they would have merged on their own and the player would
    /// never have been asked.
    ///
    /// Idempotent: matched by asset id.
    /// </summary>
    public static class HeroPass
    {
        const string Root = "Assets/GameData";
        const string Folder = Root + "/Heroes";

        [MenuItem("Tools/Grimoire/Generate Heroes")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[HeroPass] ContentDatabase.asset tidak ketemu.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Root, "Heroes");

            var hero = Load("emberwright");

            hero.Id = "emberwright";
            hero.DisplayName = "Emberwright";
            hero.Blurb = "Dua alas, dua skill, dua segel. Fireball dan Frost Shard berdiri " +
                         "menyilang di alas api — dempetkan kalau mau Steam Burst bintang 2 yang " +
                         "mengisi alas itu utuh, atau biarkan keduanya tetap menembak.";

            hero.Placed = new[]
            {
                // Two bases. The 2x2 sits on the middle of the board; the 2-cell 2-star base is
                // parked well clear of it so what stands there is never dragged into a merge.
                Seat(db, "emberrune", 2, 2),
                Seat(db, "runeasah", 4, 4),

                // Two DIFFERENT skills, one cell each, placed DIAGONALLY on the fire base. Diagonal
                // is not touching, so they will not fuse on their own — sliding one across is the
                // player's move, and it is the first real decision of a run.
                Seat(db, "fireball", 2, 2),
                Seat(db, "frostshard", 3, 3),

                // Two sigils: the passive half of a loadout. They live on the other base so the
                // fire base can be freed completely when the two skills merge.
                Seat(db, "segelmata", 4, 4),
                Seat(db, "segelpenangkal", 5, 4)
            };

            hero.Loose = new PieceDefinition[0];
            EditorUtility.SetDirty(hero);

            db.EditorSetHeroes(new List<HeroLoadout> { hero });
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[HeroPass] {hero.DisplayName}: {Describe(hero)}");
            Selection.activeObject = hero;
        }

        static HeroLoadout.Seat Seat(ContentDatabase db, string id, int x, int y)
        {
            var piece = db.ById(id);
            if (piece == null) Debug.LogWarning($"[HeroPass] piece '{id}' tidak ada.");

            return new HeroLoadout.Seat
            {
                Piece = piece,
                Origin = new Vector2Int(x, y),
                Rot = 0
            };
        }

        static HeroLoadout Load(string id)
        {
            string path = $"{Folder}/Hero_{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<HeroLoadout>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HeroLoadout>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        /// <summary>Reports the opening board back, including whether the pair can merge on its own.</summary>
        static string Describe(HeroLoadout hero)
        {
            var sb = new System.Text.StringBuilder();
            var skillCells = new List<Vector2Int>();

            foreach (var seat in hero.Placed)
            {
                if (seat.Piece == null) continue;

                sb.Append(seat.Piece.DisplayName).Append('(').Append(seat.Piece.Cells.Length)
                    .Append(" petak) @").Append(seat.Origin).Append("  ");

                // Sigils never merge, so only casting skills matter for the touching check.
                if (seat.Piece.IsRune || seat.Piece.IsPassive) continue;

                foreach (var c in Shapes.Rotate(seat.Piece.Cells, seat.Rot))
                {
                    skillCells.Add(seat.Origin + c);
                }
            }

            bool touching = false;
            for (int i = 0; i < skillCells.Count && !touching; i++)
            {
                for (int k = i + 1; k < skillCells.Count; k++)
                {
                    int d = Mathf.Abs(skillCells[i].x - skillCells[k].x) +
                            Mathf.Abs(skillCells[i].y - skillCells[k].y);

                    if (d != 1) continue;
                    touching = true;
                    break;
                }
            }

            sb.Append("\nskill pembuka bersentuhan: ").Append(touching ? "YA - akan melebur sendiri, PERIKSA LAGI" : "tidak (benar)");
            return sb.ToString();
        }
    }
}
