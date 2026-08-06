using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Turns the top tier from "bigger circle" into things that behave differently.
    ///
    /// Everything used to aim: at the nearest enemy, at the densest pack, along a line toward
    /// someone. That makes every high-rarity skill the same verb with a larger number, and it is
    /// why a 5-star felt like a 3-star with better stats. Three behaviours fix that, and none of
    /// them are cosmetic:
    ///
    /// - <b>Radial</b> aims at nobody. It waits for anything to come within reach and then covers
    ///   every direction at once, so standing in the middle of a ring is suddenly the strong play
    ///   rather than the losing one.
    /// - <b>Forks</b> send several chains at the same instant, each forbidden from touching what
    ///   another already struck — so lightning genuinely spreads across a crowd instead of
    ///   threading one line through it.
    /// - <b>Drift</b> makes a pool wander. A stationary zone is read once and then ignored; one
    ///   that moves has to be re-read, and it can chase a pack the caster is running from.
    ///
    /// Idempotent: matched by asset id.
    /// </summary>
    public static class SpectaclePass
    {
        const string Root = "Assets/GameData";
        const string PieceFolder = Root + "/Pieces";

        [MenuItem("Tools/Grimoire/Make Top Tier Spectacular")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[SpectaclePass] ContentDatabase.asset tidak ketemu.");
                return;
            }

            int changed = 0;

            // ---- Stormbreaker: eight branches at once, four jumps each. 32 enemies per cast. ----
            changed += Retune(db, "stormbreaker", p =>
            {
                p.Kind = CastKind.Chain;
                p.Forks = 8;
                p.Hits = 4;
                p.Range = 13f;
                p.BaseDamage = 150f;
                p.BaseCooldown = 4.2f;
                p.Blurb = "EVOLVED. Delapan cabang petir berangkat sekaligus, tiap cabang melompat " +
                          "empat kali, dan tidak ada dua cabang yang menyambar musuh yang sama.";
            });

            // ---- Singularity: the pool hunts. ----
            changed += Retune(db, "singularity", p =>
            {
                p.ZoneDrift = 3.4f;
                p.ZoneDuration = 7f;
                p.Blurb = "EVOLVED. Lubang yang MENGEMBARA — dia menyeret gerombolan ke dalam " +
                          "dirinya sambil berjalan sendiri melintasi lapangan.";
            });

            // ---- Cataclysm keeps falling from the sky, but wider and heavier. ----
            changed += Retune(db, "cataclysm", p =>
            {
                p.Radius = 12f;
                p.Blurb = "EVOLVED. Langit terbuka. Tidak ada yang selamat dua kali di dalam kawah.";
            });

            // ---- Absolute Zero becomes the untargeted one: a storm of shards, every direction. ----
            changed += Retune(db, "absolutezero", p =>
            {
                p.Kind = CastKind.Radial;
                p.Hits = 16;
                p.Range = 13f;
                p.Radius = 0.9f;
                p.BaseDamage = 95f;
                p.BaseCooldown = 2.6f;
                p.ManaCost = 70f;
                p.Blurb = "EVOLVED. Tidak membidik siapa pun. Enam belas pecahan es menyembur ke " +
                          "SEGALA ARAH, dan polanya berputar acak tiap tembakan.";
            });

            // ---- A 4-star radial, so the behaviour is met before the very top. ----
            changed += Retune(db, "thundercrown", p =>
            {
                p.Kind = CastKind.Chain;
                p.Forks = 4;
                p.Hits = 3;
                p.Range = 11f;
                p.BaseDamage = 110f;
                p.Blurb = "EVOLVED. Empat cabang, tiga lompatan masing-masing. Menyapu gerombolan " +
                          "melebar, bukan menembus satu barisan.";
            });

            // ---- Ion Storm: a mid-tier wandering pool, so drift is met early too. ----
            changed += Retune(db, "ionstorm", p =>
            {
                p.ZoneDrift = 2.6f;
                p.Blurb = "EVOLVED. Petak bermuatan yang berjalan pelan sendiri, terus menembak " +
                          "lama setelah kamu melemparkannya.";
            });

            // ---- Whirling Blade: the 1-star taste of untargeted, so the idea is taught early. ----
            changed += Retune(db, "belatiberputar", p =>
            {
                p.Kind = CastKind.Radial;
                p.Hits = 5;
                p.Range = 5.5f;
                p.Radius = 0.6f;
                p.BaseDamage = 9f;
                p.BaseCooldown = 1.6f;
                p.ManaCost = 10f;
                p.Blurb = "Melempar lima belati ke segala arah sekaligus. Tidak membidik — dia " +
                          "cuma menunggu ada yang cukup dekat. Sumber BLEED.";
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SpectaclePass] {changed} skill dirombak perilakunya.\n{Report(db)}");
            Selection.activeObject = db;
        }

        static int Retune(ContentDatabase db, string id, System.Action<PieceDefinition> edit)
        {
            var piece = db.ById(id);
            if (piece == null)
            {
                Debug.LogWarning($"[SpectaclePass] piece '{id}' tidak ada.");
                return 0;
            }

            edit(piece);
            EditorUtility.SetDirty(piece);
            return 1;
        }

        static string Report(ContentDatabase db)
        {
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < db.Pieces.Count; i++)
            {
                var p = db.Pieces[i];
                if (p == null || p.IsRune || p.IsPassive) continue;
                if (p.Kind != CastKind.Radial && p.Forks <= 1 && p.ZoneDrift <= 0f) continue;

                sb.Append("  ").Append(p.DisplayName).Append(" [").Append(p.Kind).Append(']');
                if (p.Kind == CastKind.Radial) sb.Append(" arah ").Append(p.Hits);
                if (p.Forks > 1) sb.Append(" cabang ").Append(p.Forks).Append(" x ").Append(p.Hits)
                    .Append(" lompatan = ").Append(p.Forks * p.Hits).Append(" musuh");
                if (p.ZoneDrift > 0f) sb.Append(" mengembara ").Append(p.ZoneDrift).Append("/dtk");
                sb.Append('\n');
            }

            return sb.ToString();
        }
    }
}
