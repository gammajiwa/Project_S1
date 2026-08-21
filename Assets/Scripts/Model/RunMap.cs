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

            // ---- tata letak: berapa node per lantai, di lajur mana ----
            //
            // Lajur TIDAK lagi diundi bebas dari seluruh papan. Undian bebas menaruh node di
            // lajur 0 dan 4 sekaligus lalu mengosongkan tengahnya — node loncat-loncat, dan
            // sambungan |Δ|<=1 gagal sampai jaring Nearest menggambar garis silang panjang:
            // persis "berantakan posisinya" yang dilaporkan. Sekarang tiap lantai adalah
            // JENDELA MENEMPEL (node bersebelahan) yang wajib menyinggung jendela lantai
            // sebelumnya — jalur jadi anyaman rapat yang menyerong pelan, dan Nearest turun
            // pangkat jadi jaring pengaman langka.
            var byFloor = new List<RunNode>[floors];
            int prevStart = lanes / 2, prevEnd = lanes / 2;

            for (int f = 0; f < floors; f++)
            {
                byFloor[f] = new List<RunNode>();

                // Puncak selalu SATU node boss di tengah. Lantai PERTAMA 3–5: langkah
                // pembuka adalah pilihan terlebar di seluruh act ("jangan terkesan cuma
                // 3 arah"). Lantai lain 2–4: petanya harus terasa penuh.
                int count;

                if (f == floors - 1) count = 1;
                else if (f == 0) count = 3 + (dice.NextDouble() < 0.5 ? 1 : 0)
                                          + (dice.NextDouble() < 0.35 ? 1 : 0);
                else count = 2 + (dice.NextDouble() < 0.6 ? 1 : 0)
                             + (dice.NextDouble() < 0.35 ? 1 : 0);

                count = System.Math.Min(count, lanes);

                int start;

                if (f == floors - 1)
                {
                    count = 1;
                    start = lanes / 2;
                }
                else
                {
                    // Lebar lantai berubah paling banyak ±1 dari lantai sebelumnya — lompatan
                    // 2 lajur -> 4 lajur membuat jendela mustahil saling menutupi.
                    if (f > 0)
                    {
                        int prevLen = prevEnd - prevStart + 1;
                        count = System.Math.Max(prevLen - 1, System.Math.Min(prevLen + 1, count));
                        count = System.Math.Min(count, lanes);
                    }

                    // Jendela wajib SALING MENUTUPI dua arah: tiap node atas punya orang tua
                    // |Δ|<=1, tiap node bawah punya jalan keluar |Δ|<=1. Menyinggung saja
                    // tidak cukup — node di tepi jauh jendela tetap minta garis silang
                    // panjang dari jaring Nearest, dan garis itulah "berantakan"-nya.
                    int lo = System.Math.Max(0,
                        System.Math.Max(prevStart - 1, prevEnd - count));
                    int hi = System.Math.Min(lanes - count,
                        System.Math.Min(prevStart + 1, prevEnd + 2 - count));

                    // Jaring: kalau syarat ketat mustahil (tepi papan), mundur ke aturan
                    // menyinggung — Nearest yang menambal sisanya.
                    if (hi < lo)
                    {
                        lo = System.Math.Max(0, prevStart - count + 1);
                        hi = System.Math.Min(lanes - count, prevEnd);
                        if (hi < lo) hi = lo;
                    }

                    // Lantai terakhir sebelum boss digiring menyinggung lajur tengah,
                    // supaya muara ke boss tidak butuh garis silang jauh.
                    if (f == floors - 2)
                    {
                        lo = System.Math.Max(lo, System.Math.Max(0, lanes / 2 - count + 1));
                        hi = System.Math.Min(hi, System.Math.Min(lanes - count, lanes / 2));
                        if (hi < lo) { lo = System.Math.Max(0, lanes / 2 - count + 1); hi = lo; }
                    }

                    start = lo + dice.Next(hi - lo + 1);
                }

                for (int i = 0; i < count; i++)
                {
                    var node = new RunNode { Index = map.Nodes.Count, Floor = f, Lane = start + i };
                    map.Nodes.Add(node);
                    byFloor[f].Add(node);
                }

                prevStart = start;
                prevEnd = start + count - 1;
            }

            // ---- sambungan antar lantai ----
            //
            // Aturannya dua arah dan dua-duanya wajib: tiap node punya JALAN KELUAR (lajur
            // bersebelahan), dan tiap node lantai atas punya JALAN MASUK. Tanpa yang kedua,
            // generator sesekali melahirkan node yatim yang tergambar di peta tapi tidak akan
            // pernah bisa diinjak — terlihat seperti konten, padahal cuma hiasan.
            for (int f = 0; f < floors - 1; f++)
            {
                var here = byFloor[f];
                var above = byFloor[f + 1];

                foreach (var node in here)
                {
                    foreach (var target in above)
                    {
                        if (System.Math.Abs(target.Lane - node.Lane) <= 1) node.Next.Add(target.Index);
                    }

                    if (node.Next.Count == 0) node.Next.Add(Nearest(above, node.Lane).Index);
                }

                foreach (var target in above)
                {
                    bool reachable = false;

                    foreach (var node in here)
                    {
                        if (node.Next.Contains(target.Index)) { reachable = true; break; }
                    }

                    if (!reachable) Nearest(here, target.Lane).Next.Add(target.Index);
                }
            }

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

        static RunNode Nearest(List<RunNode> pool, int lane)
        {
            RunNode best = pool[0];
            int gap = System.Math.Abs(best.Lane - lane);

            for (int i = 1; i < pool.Count; i++)
            {
                int d = System.Math.Abs(pool[i].Lane - lane);
                if (d < gap) { best = pool[i]; gap = d; }
            }

            return best;
        }

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
