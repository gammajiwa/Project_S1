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
            var byFloor = new List<RunNode>[floors];

            for (int f = 0; f < floors; f++)
            {
                byFloor[f] = new List<RunNode>();

                // Puncak selalu SATU node boss. Lantai PERTAMA 3–5: langkah pembuka adalah
                // pilihan terlebar di seluruh act (permintaan pemilik project — "jangan
                // terkesan cuma 3 arah"). Lantai lain 2–4: petanya harus terasa PENUH, dan
                // lajur yang dipakai berganti-ganti tiap lantai, jadi jalurnya menyerong
                // sendiri tanpa diatur.
                int count;

                if (f == floors - 1) count = 1;
                else if (f == 0) count = 3 + (dice.NextDouble() < 0.5 ? 1 : 0)
                                          + (dice.NextDouble() < 0.35 ? 1 : 0);
                else count = 2 + (dice.NextDouble() < 0.6 ? 1 : 0)
                             + (dice.NextDouble() < 0.35 ? 1 : 0);

                count = System.Math.Min(count, lanes);

                var open = new List<int>();
                for (int l = 0; l < lanes; l++) open.Add(l);

                // Boss duduk di lajur tengah; sisanya diundi dari lajur yang masih kosong.
                if (f == floors - 1)
                {
                    open.Clear();
                    open.Add(lanes / 2);
                }

                for (int i = 0; i < count; i++)
                {
                    int pick = open[dice.Next(open.Count)];
                    open.Remove(pick);

                    var node = new RunNode { Index = map.Nodes.Count, Floor = f, Lane = pick };
                    map.Nodes.Add(node);
                    byFloor[f].Add(node);
                }

                byFloor[f].Sort((a, b) => a.Lane.CompareTo(b.Lane));
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

            // ---- jenis node ----
            //
            // Lantai pertama selalu pertarungan: run harus dibuka dengan bermain, bukan dengan
            // belanja. Node istimewa tidak boleh berjenis sama dengan ORANG TUANYA — dua toko
            // beruntun di satu jalur berarti satu di antaranya bohong.
            foreach (var node in map.Nodes)
            {
                if (node.Floor == floors - 1) { node.Kind = RunNodeKind.Boss; continue; }
                if (node.Floor == 0) { node.Kind = RunNodeKind.Fight; continue; }

                double roll = dice.NextDouble();
                RunNodeKind kind;

                if (node.Floor >= eliteMinFloor && roll < eliteChance) kind = RunNodeKind.Elite;
                else if (roll < eliteChance + shopChance) kind = RunNodeKind.Shop;
                else if (roll < eliteChance + shopChance + eventChance) kind = RunNodeKind.Event;
                else kind = RunNodeKind.Fight;

                node.Kind = kind;
            }

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
            // karena semua jalur bermuara ke boss yang sama.
            foreach (var node in byFloor[floors - 2])
            {
                if (node.Kind == RunNodeKind.Elite) node.Kind = RunNodeKind.Fight;
            }

            return map;
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
