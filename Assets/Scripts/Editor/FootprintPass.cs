using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Makes rarity cost board space.
    ///
    /// Before this, Doom Nova (4 stars) took three cells and Meteor (3 stars) took two — the same
    /// footprint as a starting Fireball. Rarity bought raw power and nothing else, so the only
    /// question a strong piece asked was "do I own it", never "what do I give up for it". With a
    /// 7x7 board and every skill costing two or three cells, there was nothing to give up.
    ///
    /// The ladder: 1 star takes 2-3 cells, 2 stars take 4, 3 stars take 5, 4 stars takes 7, and
    /// 5 stars take 8-9. The shapes get deliberately awkward on the way up — a Ring wastes its own
    /// centre, a Cup leaves a notch nothing else can reach — so the cost is not just area but the
    /// dead space each piece strands around it.
    ///
    /// Runes are left alone on purpose. They are the platform every skill has to stand on; making
    /// both layers awkward at the same time multiplies the difficulty instead of adding to it. They
    /// already scale anyway — the two 3x3 bases are the widest footprints in the game.
    ///
    /// Idempotent: matched by asset id.
    /// </summary>
    public static class FootprintPass
    {
        const string Root = "Assets/GameData";

        struct Footprint
        {
            public string Id;
            public ShapeKind Shape;
            public string Blurb;
        }

        /// <summary>
        /// Blurbs no longer describe the footprint. The tooltip reads the shape straight off the
        /// piece now, so prose like "3 long" could only ever drift out of date — and already had.
        /// </summary>
        static readonly Footprint[] Table =
        {
            // ---- 1 star: 2-3 cells. Cheap enough to still carpet the early board. ----
            new Footprint { Id = "fireball",       Shape = ShapeKind.Dot,
                Blurb = "Fast single bolt. One cell — it fits wherever nothing else will." },
            new Footprint { Id = "belatiberputar", Shape = ShapeKind.Line2,
                Blurb = "Spins around you. Close-range guard, and the source of BLEED." },
            new Footprint { Id = "minorheal",      Shape = ShapeKind.Dot,
                Blurb = "A small top-up on a long cooldown. One cell." },
            new Footprint { Id = "frostnova",      Shape = ShapeKind.Corner,
                Blurb = "Freezing burst centred on you. Applies CHILL." },
            new Footprint { Id = "arcbolt",        Shape = ShapeKind.Corner,
                Blurb = "Leaps from enemy to enemy. Applies SHOCK." },
            new Footprint { Id = "hujanapi",       Shape = ShapeKind.Line2,
                Blurb = "Rains fire onto the densest pack. Applies BURN." },
            new Footprint { Id = "sabetanpetir",   Shape = ShapeKind.Line3,
                Blurb = "Sweeps a line of enemies. Applies SHOCK." },
            new Footprint { Id = "kubanganracun",  Shape = ShapeKind.Corner,
                Blurb = "Leaves a lingering pool. Applies POISON." },
            new Footprint { Id = "pusaran",        Shape = ShapeKind.Line3,
                Blurb = "Drags enemies together. Applies DRAG - the setup for mass reactions." },

            // ---- 2 stars: 4 cells, all bent. ----
            // 2x2, and that is load-bearing: it is the hero's opening upgrade, and it has to fit
            // exactly on the 2x2 base the run starts with. A T-shape needs 3 cells across, so the
            // promised upgrade was quietly impossible — the recipe completed and could never seat.
            new Footprint { Id = "greaterfireball", Shape = ShapeKind.Square,
                Blurb = "EVOLVED. Far heavier than the bolt it came from, and no slower. " +
                        "Fills a 2x2 base exactly." },
            new Footprint { Id = "blizzard",        Shape = ShapeKind.SBend,
                Blurb = "EVOLVED. Wide freezing blast centred on you. Applies CHILL." },
            // 2x2 for the same reason Greater Fireball is: it is an opening upgrade, so it has to
            // land on the 2x2 base a run starts with. An L needs three cells of reach and would
            // have made the hero's advertised first choice impossible to actually take.
            new Footprint { Id = "steamburst",      Shape = ShapeKind.Square,
                Blurb = "EVOLVED. Scalding burst - fire and ice meeting at once. Fills a 2x2 base." },
            new Footprint { Id = "badaisalju",      Shape = ShapeKind.Square,
                Blurb = "EVOLVED. A freezing storm that stays on the ground. Applies CHILL." },
            new Footprint { Id = "greaterheal",     Shape = ShapeKind.SBend,
                Blurb = "EVOLVED. A real heal, but you wait a long time for it." },

            // ---- 3 stars: 5 cells. ----
            new Footprint { Id = "meteor",     Shape = ShapeKind.Cross,
                Blurb = "EVOLVED. Falls out of the sky onto the densest pack. Applies BURN." },
            new Footprint { Id = "prismabeku", Shape = ShapeKind.Cup,
                Blurb = "EVOLVED. Shattering ice, chaining through five enemies. Applies CHILL." },

            // ---- 4 stars: 7 cells. ----
            new Footprint { Id = "novakiamat", Shape = ShapeKind.Hook,
                Blurb = "EVOLVED. The heaviest blast in the book. Applies BURN." },

            // ---- 5 stars: 8-9 cells. A fifth of the board, each. ----
            new Footprint { Id = "cataclysm",    Shape = ShapeKind.Big3,
                Blurb = "EVOLVED. The sky opens. Nothing inside the crater survives. Applies BURN." },
            new Footprint { Id = "absolutezero", Shape = ShapeKind.Ring,
                Blurb = "EVOLVED. Reaches further than anything else in the book. Applies CHILL." },
            new Footprint { Id = "singularity",  Shape = ShapeKind.Chunk,
                Blurb = "EVOLVED. Drags the swarm into itself and grinds. Applies DRAG." }
        };

        // Dua daftar paksa lama — SingleCell dan TwoCell — dicabut.
        //
        // Keduanya ada untuk menambal tangga lama: ukuran terkecilnya dua petak, jadi papan tidak
        // punya pengisi celah sama sekali dan beberapa piece harus dipaksa turun ke satu petak.
        // Tangga baru mulai DARI satu petak, jadi tambalannya tidak punya pekerjaan lagi — dan
        // membiarkannya berarti sepuluh piece dipaksa dua petak melawan tangga yang baru saja
        // memutuskan mereka satu.

        /// <summary>
        /// Tangga JUMLAH PETAK, bukan tangga bentuk.
        ///
        /// Permintaan pemilik project, apa adanya: bintang 1 itu satu petak dan sesekali dua,
        /// bintang 2 itu dua dan sesekali tiga, seterusnya — "gw mau bentuknya bisa beda-beda,
        /// gak harus ada rule". Jadi yang dipatok di sini cuma UKURANNYA; bentuk mana yang dipakai
        /// diundi dari semua bentuk seukuran itu.
        ///
        /// Jauh lebih kecil dari tangga lama (2-3 / 4 / 5 / 7 / 8-9), dan itu sekaligus menghapus
        /// seluruh kelas "bentuk yang mustahil didudukkan": dulu Greater Fireball berbentuk huruf T
        /// butuh tiga petak melintang sementara alas pembukanya cuma 2x2, jadi upgrade yang
        /// dijanjikan hero tidak pernah bisa terjadi. Piece dua petak muat di mana pun.
        /// </summary>
        static readonly (int common, int rare)[] CellsByStar =
        {
            (1, 1),   // tidak dipakai — bintang dihitung dari 1
            (1, 2),
            (2, 3),
            (3, 4),
            (4, 7),   // bintang 4-5 tidak memakai tabel ini; lihat CellsByPower
            (5, 9)
        };

        /// <summary>
        /// Persen piece bintang 1-3 yang naik satu ukuran. Dipilih per piece dari hash id-nya,
        /// jadi menjalankan ulang pass ini selalu menghasilkan papan yang sama.
        ///
        /// Ditulis sebagai persen, bukan "satu dari lima". Bentuk `hash % 5 == 0` terlihat setara
        /// dan TIDAK: hash id yang pendek tidak tersebar rata di lima sisa bagi, dan yang terukur
        /// 30% — setengah lebih banyak dari yang dimaksud. Sisa bagi seratus jauh lebih rata.
        /// </summary>
        const int RarePercent = 15;

        [MenuItem("Tools/Grimoire/Footprint by Rarity")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[FootprintPass] ContentDatabase.asset tidak ketemu.");
                return;
            }

            int changed = 0, hand = 0;

            // Tabel bernama sekarang cuma membawa BLURB-nya. Bentuknya tidak lagi dipatok di sana:
            // tiap patokan itu lahir dari tangga lama yang besar (supaya muat di alasnya), dan
            // tangga baru sudah jauh lebih kecil dari semua kasus yang dulu bermasalah.
            for (int i = 0; i < Table.Length; i++)
            {
                var piece = db.ById(Table[i].Id);
                if (piece == null)
                {
                    Debug.LogWarning($"[FootprintPass] piece '{Table[i].Id}' tidak ada, dilewati.");
                    continue;
                }

                // Piece yang sudah berubah jadi segel tidak boleh menyandang keterangan skill lagi.
                if (piece.IsPassive) continue;

                piece.Blurb = Table[i].Blurb;
                EditorUtility.SetDirty(piece);
            }

            // Bentuk dikelompokkan sekali menurut jumlah petaknya, lalu diundi dari kelompok itu.
            var byCells = GroupShapes();

            // Skor ke-OP-an untuk bintang 4-5, dihitung untuk SELURUH piece dulu supaya tiap
            // skill bisa diperingkat terhadap sesama tingkatnya, bukan terhadap angka mutlak
            // yang tidak berarti apa-apa sendirian.
            var power = ScorePower(db);
            var starters = StarterPieces(db);

            for (int i = 0; i < db.Pieces.Count; i++)
            {
                var piece = db.Pieces[i];
                if (piece == null || piece.IsRune) continue;

                // Bentuk gambaran tangan TIDAK PERNAH disentuh. Itu kontrak Editor Bentuk Grid:
                // yang digambar tangan adalah keputusan, dan generator yang membatalkan keputusan
                // membuat siapa pun berhenti memercayai kedua alat itu sekaligus.
                if (piece.HasCustomShape)
                {
                    hand++;
                    continue;
                }

                // Bentuk bernama tetap dihormati BLURB-nya, tapi ukurannya ikut tangga baru:
                // seluruh alasan bentuk-bentuk itu dipatok tangan adalah supaya muat di alasnya,
                // dan tangga baru sudah jauh lebih kecil dari yang dulu bermasalah.
                int cells = CellsFor(piece, power, starters);

                var options = byCells.TryGetValue(cells, out var list) ? list : byCells[1];

                // Diundi dari hash id, bukan acak: menjalankan ulang pass ini harus menghasilkan
                // papan yang sama persis, atau setiap resep dan setiap starter harus diuji ulang.
                piece.Shape = options[Hash(piece.Id, 7) % options.Count];

                EditorUtility.SetDirty(piece);
                changed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FootprintPass] {changed} piece diubah bentuknya" +
                      (hand > 0 ? $", {hand} dilewati karena digambar tangan" : "") +
                      $".\n{Report(db)}");
            Selection.activeObject = db;
        }

        /// <summary>
        /// Id tiap piece yang dipakai susunan papan awal hero mana pun.
        ///
        /// Dibaca lewat SerializedObject, bukan lewat bidangnya langsung: susunan itu berupa array
        /// struct bersarang, dan membacanya lewat refleksi biasa berarti pass ini pecah diam-diam
        /// setiap kali struct-nya diganti nama.
        /// </summary>
        static HashSet<string> StarterPieces(ContentDatabase db)
        {
            var ids = new HashSet<string>();

            for (int h = 0; h < db.Heroes.Count; h++)
            {
                var hero = db.Heroes[h];
                if (hero == null) continue;

                var so = new SerializedObject(hero);

                foreach (string field in new[] { "Placed", "Loose" })
                {
                    var array = so.FindProperty(field);
                    if (array == null || !array.isArray) continue;

                    for (int i = 0; i < array.arraySize; i++)
                    {
                        var slot = array.GetArrayElementAtIndex(i);
                        var piece = slot.FindPropertyRelative("Piece");

                        // Daftar `Loose` menyimpan piece-nya langsung, bukan di dalam struct.
                        var value = (piece ?? slot).objectReferenceValue as PieceDefinition;
                        if (value != null) ids.Add(value.Id);
                    }
                }
            }

            return ids;
        }

        /// <summary>Semua ShapeKind dikelompokkan menurut berapa petak yang dimakannya.</summary>
        static Dictionary<int, List<ShapeKind>> GroupShapes()
        {
            var map = new Dictionary<int, List<ShapeKind>>();

            foreach (ShapeKind kind in System.Enum.GetValues(typeof(ShapeKind)))
            {
                int n = Shapes.Of(kind).Length;
                if (!map.TryGetValue(n, out var list)) map[n] = list = new List<ShapeKind>();
                list.Add(kind);
            }

            return map;
        }

        /// <summary>
        /// Berapa petak yang dimakan sebuah piece.
        ///
        /// Bintang 1-3 memakai tangga tetap dengan sesekali naik satu. Bintang 4-5 TIDAK: pemilik
        /// project memintanya "gak karuan bentuknya dan jumlahnya tergantung seberapa OP mereka",
        /// jadi ukurannya diperingkat dari skor kekuatan, bukan dipatok.
        /// </summary>
        static int CellsFor(PieceDefinition piece, Dictionary<string, float> power,
            HashSet<string> starters)
        {
            int star = Mathf.Clamp(piece.Stars, 1, 5);

            if (star <= 3)
            {
                var (common, rare) = CellsByStar[star];

                // Piece PEMBUKA tidak pernah dapat ukuran yang lebih besar.
                //
                // Susunan papan awal tiap hero ditulis tangan petak demi petak, dan satu piece
                // yang membesar satu petak akan mencaplok petak tetangganya. Terukur saat aturan
                // ini belum ada: Fireball naik dari 1 ke 2 petak dan Keen Sigil milik Stormcaller
                // GAGAL DUDUK — run itu dibuka dengan satu piece yang dijanjikan hilang, tanpa
                // satu pun error.
                if (starters.Contains(piece.Id)) return common;

                return Hash(piece.Id, 31) % 100 < RarePercent ? rare : common;
            }

            // Bintang 4 memakan 4-7 petak, bintang 5 memakan 5-9 — dan di mana persisnya
            // ditentukan peringkat skornya di antara sesama tingkatnya.
            int low = star == 4 ? 4 : 5;
            int high = star == 4 ? 7 : 9;

            float rank = power.TryGetValue(piece.Id, out float r) ? r : 0.5f;
            return Mathf.Clamp(low + Mathf.RoundToInt(rank * (high - low)), low, high);
        }

        /// <summary>
        /// Skor ke-OP-an tiap skill bintang 4-5, dinormalkan jadi 0..1 di dalam tingkatnya.
        ///
        /// Yang diukur BUKAN damage: solver keseimbangan sudah menyamakan throughput seluruh
        /// anggota satu tingkat, jadi damage tidak membedakan apa pun. Yang tersisa sebagai
        /// kekuatan nyata adalah dua hal yang tidak pernah dinormalkan siapa pun — berapa musuh
        /// yang tersentuh sekali cast, dan sejauh mana ia menjangkau papan.
        /// </summary>
        static Dictionary<string, float> ScorePower(ContentDatabase db)
        {
            var raw = new Dictionary<string, float>();
            var byStar = new Dictionary<int, List<float>>();

            for (int i = 0; i < db.Pieces.Count; i++)
            {
                var p = db.Pieces[i];
                if (p == null || p.IsRune || p.IsPassive) continue;
                if (p.Stars < 4) continue;

                float targets = Mathf.Max(0f, BalanceTunePass.ExpectedTargets(p));
                float reach = Mathf.Max(1f, Mathf.Max(p.Radius, p.Range));

                float score = targets * reach;
                raw[p.Id] = score;

                if (!byStar.TryGetValue(p.Stars, out var list)) byStar[p.Stars] = list = new List<float>();
                list.Add(score);
            }

            foreach (var list in byStar.Values) list.Sort();

            var rank = new Dictionary<string, float>();

            for (int i = 0; i < db.Pieces.Count; i++)
            {
                var p = db.Pieces[i];
                if (p == null || !raw.TryGetValue(p.Id, out float score)) continue;

                var list = byStar[p.Stars];

                // Peringkat, bukan nilai mentah: skor sebuah skill zone yang menagih dua puluh
                // denyut berbeda dua ORDE dari skill peluru, dan menskalakan nilai mentah akan
                // membuat seluruh tingkat menempel di satu ujung.
                int below = 0;
                for (int j = 0; j < list.Count; j++)
                {
                    if (list[j] < score) below++;
                }

                rank[p.Id] = list.Count <= 1 ? 0.5f : below / (float)(list.Count - 1);
            }

            return rank;
        }

        /// <summary>Hash id yang STABIL antar sesi. <c>string.GetHashCode</c> tidak dijamin begitu.</summary>
        static int Hash(string id, int salt)
        {
            unchecked
            {
                int h = salt;
                for (int i = 0; i < id.Length; i++) h = h * 31 + id[i];
                return h & 0x7fffffff;
            }
        }

        /// <summary>Cells per star, so the ladder can be read back instead of trusted.</summary>
        static string Report(ContentDatabase db)
        {
            var byStar = new SortedDictionary<int, List<string>>();

            for (int i = 0; i < db.Pieces.Count; i++)
            {
                var piece = db.Pieces[i];
                if (piece == null || piece.IsRune) continue;

                if (!byStar.ContainsKey(piece.Stars)) byStar[piece.Stars] = new List<string>();
                byStar[piece.Stars].Add($"{piece.DisplayName} {Shapes.NameOf(piece.Shape)}" +
                                        $"({piece.Cells.Length})");
            }

            var sb = new System.Text.StringBuilder();
            foreach (var pair in byStar)
            {
                sb.Append(pair.Key).Append(" bintang: ").Append(string.Join("  ", pair.Value.ToArray()))
                    .Append('\n');
            }

            return sb.ToString();
        }
    }
}
