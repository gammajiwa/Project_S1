using System.Collections.Generic;

namespace Proto
{
    public enum RunNodeKind
    {
        Fight,
        Elite,
        Shop,
        Event,
        Boss
    }

    /// <summary>Satu titik di peta run. Lantai = urutan wave; lajur = posisi mendatar di layar.</summary>
    public class RunNode
    {
        public int Index;
        public int Floor;
        public int Lane;
        public RunNodeKind Kind;

        /// <summary>Indeks node lantai berikutnya yang tersambung dari sini.</summary>
        public readonly List<int> Next = new List<int>();
    }

    /// <summary>
    /// Peta run ala Slay the Spire: lantai-lantai bercabang dari bawah ke atas, boss di puncak.
    /// Pemain tidak lagi disodori "wave berikutnya" — ia MEMILIH portal, dan pilihan itulah
    /// yang menentukan isi wave: pertarungan biasa, elite, toko, kejadian, atau boss.
    ///
    /// Murni model — tidak tahu apa-apa soal UI maupun EnemyManager. Yang menghubungkannya ke
    /// keduanya adalah pemanggilnya, supaya peta bisa diuji tanpa satu pun scene.
    ///
    /// Di-generate dari SEED yang diberikan pemanggil: satu run satu peta. Di dalam satu
    /// generate, System.Random tunggal dipakai BERURUTAN — jebakan seed-berjajar (lihat
    /// WaveHash) hanya berlaku kalau membuat Random baru per undian, bukan di sini.
    /// </summary>
    public class RunMap
    {
        public readonly List<RunNode> Nodes = new List<RunNode>();
        public int Floors { get; private set; }
        public int Lanes { get; private set; }
        public int Act { get; private set; }

        /// <summary>Node yang sedang diduduki. −1 = belum melangkah (awal act).</summary>
        public int At = -1;

        /// <summary>
        /// Bobot jenis node, dinormalkan oleh generator. Elite baru boleh muncul dari
        /// <paramref name="eliteMinFloor"/> — elite di lantai satu adalah tembok, bukan pilihan.
        /// </summary>
        public static RunMap Generate(int act, int floors, int lanes, int seed,
            float eliteChance, float shopChance, float eventChance,
            int eliteMinFloor)
        {
            var map = new RunMap { Floors = floors, Lanes = lanes, Act = act };
            var dice = new System.Random(seed);

            // ---- tata letak: JALUR-JALUR BERKELANA, ala peta Slay the Spire ----
            //
            // Bukan "berapa node per lantai" — melainkan beberapa JALUR yang masing-masing
            // berjalan dari lantai dasar ke puncak, tiap lantai bergeser paling jauh satu
            // lajur. Node hanya lahir di petak yang DILEWATI jalur; dua jalur yang lewat
            // petak sama MELEBUR (itulah simpangnya) lalu boleh berpisah lagi. Hasilnya
            // kepang longgar bercelah — punya pola, tapi tidak pernah kotak penuh.
            //
            // Sejarahnya dua ekstrem: undian lajur bebas = "berantakan posisinya" (garis
            // silang panjang ke mana-mana), lalu jendela-menempel = "terlalu rapih bahkan
            // kaya jahitan" (pemilik project, menunjuk peta StS sebagai acuan). Jalur
            // berkelana adalah cara StS sendiri berdiri di antara keduanya.
            var byFloor = new List<RunNode>[floors];
            for (int f = 0; f < floors; f++) byFloor[f] = new List<RunNode>();

            var grid = new RunNode[floors, lanes];
            var walked = new HashSet<int>();
            var starts = new List<int>();
            int pathCount = System.Math.Max(4, lanes);

            for (int p = 0; p < pathCount; p++)
            {
                // Tiga jalur pertama wajib berangkat dari lajur BERBEDA: langkah pembuka
                // harus pilihan lebar ("jangan terkesan cuma 3 arah").
                int lane;
                do { lane = dice.Next(lanes); }
                while (p < 3 && starts.Contains(lane));
                starts.Add(lane);

                var node = TouchNode(map, byFloor, grid, 0, lane);

                for (int f = 0; f < floors - 2; f++)
                {
                    int target = lane + dice.Next(3) - 1;
                    if (target < 0) target = 0;
                    if (target > lanes - 1) target = lanes - 1;

                    // Anti-silang: tapak (L -> L+1) dilarang kalau sudah ada (L+1 -> L)
                    // di sela lantai yang sama — dua tapak menyilang membentuk X yang
                    // tidak terbaca sebagai jalan. Mengalah = jalan lurus, selalu aman.
                    if (target == lane + 1 && walked.Contains(EdgeKey(f, lane + 1, lane)))
                        target = lane;
                    else if (target == lane - 1 && walked.Contains(EdgeKey(f, lane - 1, lane)))
                        target = lane;

                    var next = TouchNode(map, byFloor, grid, f + 1, target);
                    if (!node.Next.Contains(next.Index)) node.Next.Add(next.Index);
                    walked.Add(EdgeKey(f, lane, target));

                    lane = target;
                    node = next;
                }

                // Semua jalur bermuara ke SATU boss di lajur tengah puncak — corong
                // penutup act, sama seperti acuannya.
                var boss = TouchNode(map, byFloor, grid, floors - 1, lanes / 2);
                if (!node.Next.Contains(boss.Index)) node.Next.Add(boss.Index);
            }

            // Sambungan antar lantai sudah lahir BERSAMA jalurnya — tiap node pasti punya
            // jalan keluar (jalurnya sendiri berlanjut) dan jalan masuk (tapak yang
            // melahirkannya), jadi node yatim mustahil secara konstruksi.

            // ---- jenis node: KUOTA per pita lantai, bukan dadu per node ----
            //
            // Dadu independen per node menghasilkan paceklik dan gerombolan ("kaya bener
            // random gak jelas distribusinya" — pemilik project): sepuluh lantai tanpa toko
            // di satu peta, dua toko bersebelahan di peta lain. Sekarang peluangnya dibaca
            // sebagai TAKARAN: lantai 1..floors-2 dipotong jadi pita ~6 lantai, tiap pita
            // dapat jatah pasti (peluang x jumlah node pita, pecahannya diundi sekali),
            // lalu jatah disebar dengan aturan SATU istimewa per lantai. Kepadatan jadi
            // rata di sepanjang act; letak persisnya tetap milik seed. Lantai pertama
            // selalu pertarungan: run dibuka dengan bermain, bukan belanja.
            foreach (var node in map.Nodes)
            {
                node.Kind = node.Floor == floors - 1 ? RunNodeKind.Boss : RunNodeKind.Fight;
            }

            const int Band = 6;

            for (int bandStart = 1; bandStart < floors - 1; bandStart += Band)
            {
                int bandEnd = System.Math.Min(bandStart + Band - 1, floors - 2);

                int nodeCount = 0;
                for (int f = bandStart; f <= bandEnd; f++) nodeCount += byFloor[f].Count;

                SpreadKind(byFloor, dice, RunNodeKind.Elite,
                    Quota(dice, eliteChance * nodeCount), bandStart, bandEnd, eliteMinFloor, floors);
                SpreadKind(byFloor, dice, RunNodeKind.Shop,
                    Quota(dice, shopChance * nodeCount), bandStart, bandEnd, 0, floors);
                SpreadKind(byFloor, dice, RunNodeKind.Event,
                    Quota(dice, eventChance * nodeCount), bandStart, bandEnd, 0, floors);
            }

            // Ritual belanja pra-boss: minimal SATU toko di enam lantai terakhir sebelum
            // boss. Kuota pita bisa saja menumpuk tokonya di awal, dan act panjang tanpa
            // tempat membelanjakan tabungan menutup dengan rasa tertipu.
            bool lateShop = false;
            int lateFrom = System.Math.Max(1, floors - 7);

            for (int f = lateFrom; f <= floors - 2 && !lateShop; f++)
            {
                foreach (var node in byFloor[f])
                {
                    if (node.Kind == RunNodeKind.Shop) { lateShop = true; break; }
                }
            }

            if (!lateShop)
                SpreadKind(byFloor, dice, RunNodeKind.Shop, 1, lateFrom, floors - 2, 0, floors);

            // Dua istimewa sejenis beruntun di SATU JALUR tetap dilarang — kuota menjaga
            // takaran, pagar ini menjaga rasa pilihan.
            foreach (var node in map.Nodes)
            {
                foreach (int next in node.Next)
                {
                    var child = map.Nodes[next];

                    if (child.Kind != RunNodeKind.Fight && child.Kind != RunNodeKind.Boss &&
                        child.Kind == node.Kind)
                    {
                        child.Kind = RunNodeKind.Fight;
                    }
                }
            }

            // Lantai TERAKHIR sebelum boss dibersihkan dari elite — dua wave keras beruntun
            // menutup act dengan tembok ganda, dan pemain tidak punya cara menghindarinya
            // karena semua jalur bermuara ke boss yang sama. (Penempatan sudah melewatinya;
            // sapuan ini tinggal pagar ganda.)
            foreach (var node in byFloor[floors - 2])
            {
                if (node.Kind == RunNodeKind.Elite) node.Kind = RunNodeKind.Fight;
            }

            return map;
        }

        /// <summary>Takaran pecahan diundi SEKALI: 1,6 berarti pasti 1, kadang-kadang 2.</summary>
        static int Quota(System.Random dice, double expected)
        {
            int whole = (int)expected;
            return whole + (dice.NextDouble() < expected - whole ? 1 : 0);
        }

        /// <summary>
        /// Menyebar sejumlah node berjenis <paramref name="kind"/> ke pita lantai
        /// [from..to]: satu istimewa per lantai, hanya menimpa Fight, menghormati lantai
        /// minimum (gerbang elite), dan elite tidak pernah duduk di lantai pra-boss.
        /// Kehabisan tempat = sisa jatah hangus — takaran boleh meleset satu, bentuk peta
        /// tidak boleh dipaksa.
        /// </summary>
        static void SpreadKind(List<RunNode>[] byFloor, System.Random dice, RunNodeKind kind,
            int quota, int from, int to, int minFloor, int floors)
        {
            for (int q = 0; q < quota; q++)
            {
                for (int attempt = 0; attempt < 24; attempt++)
                {
                    int f = from + dice.Next(to - from + 1);
                    if (f < minFloor || f > floors - 2) continue;
                    if (kind == RunNodeKind.Elite && f == floors - 2) continue;

                    bool floorTaken = false;
                    foreach (var n in byFloor[f])
                    {
                        if (n.Kind != RunNodeKind.Fight) { floorTaken = true; break; }
                    }
                    if (floorTaken) continue;

                    var node = byFloor[f][dice.Next(byFloor[f].Count)];
                    node.Kind = kind;
                    break;
                }
            }
        }

        /// <summary>Node di petak (lantai, lajur) — lahir saat tapak pertama menyentuhnya.</summary>
        static RunNode TouchNode(RunMap map, List<RunNode>[] byFloor, RunNode[,] grid,
            int floor, int lane)
        {
            var node = grid[floor, lane];
            if (node != null) return node;

            node = new RunNode { Index = map.Nodes.Count, Floor = floor, Lane = lane };
            map.Nodes.Add(node);
            byFloor[floor].Add(node);
            grid[floor, lane] = node;
            return node;
        }

        /// <summary>Kunci tapak (lantai, dari, ke) untuk larangan silang. Lajur &lt; 10.</summary>
        static int EdgeKey(int floor, int from, int to) => (floor * 100 + from) * 10 + to;

        /// <summary>Node yang boleh diinjak sekarang. Sebelum melangkah: seluruh lantai dasar.</summary>
        public List<RunNode> Reachable()
        {
            var reachable = new List<RunNode>();

            if (At < 0)
            {
                foreach (var node in Nodes)
                {
                    if (node.Floor == 0) reachable.Add(node);
                }

                return reachable;
            }

            foreach (int next in Nodes[At].Next) reachable.Add(Nodes[next]);
            return reachable;
        }

        /// <summary>Lantai yang sedang dipijak. −1 sebelum langkah pertama.</summary>
        public int CurrentFloor => At < 0 ? -1 : Nodes[At].Floor;

        /// <summary>Act selesai saat boss di puncak sudah diinjak (dan wave-nya dibersihkan).</summary>
        public bool AtBoss => At >= 0 && Nodes[At].Kind == RunNodeKind.Boss;
    }
}
