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

            // Emberwright memakai angka bawaan seluruhnya. Ia acuan: starter yang setiap statnya
            // ditimpa tidak bisa dipakai membaca apakah angka di GameBalance sendiri sudah benar.
            hero.Accent = new Color(1f, 0.62f, 0.28f, 1f);
            EditorUtility.SetDirty(hero);

            var frost = BuildFrostwarden(db);
            var storm = BuildStormcaller(db);

            db.EditorSetHeroes(new List<HeroLoadout> { hero, frost, storm });
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (var h in new[] { hero, frost, storm })
            {
                Debug.Log($"[HeroPass] {h.DisplayName}: {Describe(h)}");
            }

            Selection.activeObject = hero;
        }

        /// <summary>
        /// Starter bertahan: nyawa besar, mana lambat.
        ///
        /// Angkanya PLACEHOLDER — dipilih supaya perbedaannya kelihatan waktu diadu, bukan supaya
        /// seimbang. Yang menyeimbangkan cukup mengubah asetnya; tidak ada satu pun angka di sini
        /// yang dibaca kode.
        /// </summary>
        static HeroLoadout BuildFrostwarden(ContentDatabase db)
        {
            var hero = Load("frostwarden");

            hero.Id = "frostwarden";
            hero.DisplayName = "Frostwarden";
            hero.Blurb = "Tahan pukul, lambat mengisi. Frost Shard dan Minor Heal berdiri " +
                         "menyilang di alas es — yang satu membunuh, yang satu menambal, dan " +
                         "mendempetkannya berarti memilih salah satunya untuk hilang.";

            hero.Accent = new Color(0.55f, 0.82f, 1f, 1f);

            hero.Placed = new[]
            {
                // Dua alas 2x2 yang tidak bersentuhan: (2,2) mengisi 2-3 x 2-3, (4,4) mengisi 4-5 x 4-5.
                Seat(db, "frostrune", 2, 2),
                Seat(db, "runebenteng", 4, 4),

                // Menyilang di alas es, sama seperti Emberwright — pertanyaannya sengaja sama,
                // jawabannya yang berbeda: melebur di sini berarti kehilangan satu-satunya
                // penyembuh, bukan sekadar menukar dua sumber damage jadi satu.
                Seat(db, "frostshard", 2, 2),
                Seat(db, "minorheal", 3, 3),

                Seat(db, "segelpenangkal", 4, 4),
                Seat(db, "segelmata", 5, 5)
            };

            hero.Loose = new PieceDefinition[0];

            hero.MaxHp = 140f;
            hero.MaxMana = 52f;
            hero.ManaRegen = 4f;
            hero.HpRegen = 3f;
            hero.MoveSpeed = 2.4f;

            EditorUtility.SetDirty(hero);
            return hero;
        }

        /// <summary>Starter agresif: cepat dan rapuh, dua skill berjauhan dengan ukuran berbeda.</summary>
        static HeroLoadout BuildStormcaller(ContentDatabase db)
        {
            var hero = Load("stormcaller");

            hero.Id = "stormcaller";
            hero.DisplayName = "Stormcaller";
            hero.Blurb = "Rapuh dan cepat. Sabetan Petir mengisi penuh alas garisnya, Fireball " +
                         "berdiri sendirian di seberang papan — dua skill yang tidak akan pernah " +
                         "bertemu kecuali kamu yang memindahkannya.";

            hero.Accent = new Color(0.86f, 0.72f, 1f, 1f);

            hero.Placed = new[]
            {
                // Alas garis 3 petak dan alas 2x2 di sudut berlawanan.
                Seat(db, "sparkrune", 1, 4),
                Seat(db, "runesiphon", 4, 1),

                // Sabetan Petir mengisi alas garisnya PAS. Itu pelajarannya sendiri: alas yang
                // terisi penuh tidak menyisakan tempat untuk apa pun berkembang di atasnya.
                Seat(db, "sabetanpetir", 1, 4),
                Seat(db, "fireball", 4, 1),

                Seat(db, "segelpenangkal", 4, 2),
                Seat(db, "segelmata", 5, 2)
            };

            hero.Loose = new PieceDefinition[0];

            hero.MaxHp = 78f;
            hero.MaxMana = 88f;
            hero.ManaRegen = 7f;
            hero.HpRegen = 1f;
            hero.MoveSpeed = 3.4f;

            EditorUtility.SetDirty(hero);
            return hero;
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

            // Sel dicatat bersama PEMILIKNYA. Tanpa itu, skill yang lebih dari satu petak selalu
            // dilaporkan bersentuhan — sel-selnya sendiri memang bertetangga, dan pemeriksa yang
            // cuma melihat koordinat tidak bisa membedakan "dua piece berdempetan" dari "satu
            // piece yang panjang". Cacat ini tidak pernah kelihatan selama semua skill pembuka
            // masih satu petak.
            //
            // Penandanya nomor SEAT, bukan aset piece-nya: starter yang membuka dengan dua
            // Fireball memegang aset yang sama dua kali, dan menandainya per aset akan
            // menganggap keduanya satu benda — persis kasus yang paling perlu diperiksa.
            var owner = new List<int>();

            for (int s = 0; s < hero.Placed.Length; s++)
            {
                var seat = hero.Placed[s];
                if (seat.Piece == null) continue;

                sb.Append(seat.Piece.DisplayName).Append('(').Append(seat.Piece.Cells.Length)
                    .Append(" petak) @").Append(seat.Origin).Append("  ");

                // Sigils never merge, so only casting skills matter for the touching check.
                if (seat.Piece.IsRune || seat.Piece.IsPassive) continue;

                foreach (var c in Shapes.Rotate(seat.Piece.Cells, seat.Rot))
                {
                    skillCells.Add(seat.Origin + c);
                    owner.Add(s);
                }
            }

            bool touching = false;
            for (int i = 0; i < skillCells.Count && !touching; i++)
            {
                for (int k = i + 1; k < skillCells.Count; k++)
                {
                    if (owner[i] == owner[k]) continue;

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
