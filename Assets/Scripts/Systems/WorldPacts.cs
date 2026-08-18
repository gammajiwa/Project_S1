using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Pakta yang sedang berlaku di run ini, sudah dijumlahkan sekali.
    ///
    /// Bukan MonoBehaviour: ia tidak punya frame, tidak punya posisi, dan tidak menggambar apa pun.
    /// Dibuat di akar komposisi lalu diserahkan ke <see cref="PlayerCaster"/> dan
    /// <see cref="EnemyManager"/> — pola yang sama dengan <see cref="GameBalance"/> dan
    /// <see cref="DebugConfig"/>, dan alasannya sama: yang membutuhkannya menerimanya, bukan
    /// mencarinya sendiri lewat singleton.
    ///
    /// Semuanya dihitung ulang HANYA saat pakta baru diambil. Nilainya dibaca di jalur terpanas
    /// yang ada — tiap cast, tiap kelahiran musuh, tiap frame regen — jadi menjumlahkan daftar di
    /// titik baca akan membayar biaya yang sama berkali-kali per frame untuk data yang berubah
    /// beberapa kali per RUN.
    /// </summary>
    public class WorldPacts
    {
        readonly List<WorldModifierDefinition> _taken = new List<WorldModifierDefinition>(8);
        readonly float[] _stats = new float[(int)StatKind.Count];

        public IReadOnlyList<WorldModifierDefinition> Taken => _taken;

        public int Count => _taken.Count;

        // Semuanya PERKALIAN berantai: dua pakta yang masing-masing menaikkan nyawa musuh 30%
        // menghasilkan 1,69x, bukan 1,6x. Itu memang yang diinginkan — pakta menumpuk sepanjang
        // run, dan penjumlahan akan membuat pakta kelima nyaris tidak terasa.
        public float EnemyHpMul { get; private set; } = 1f;
        public float EnemySpeedMul { get; private set; } = 1f;
        public float EnemyDamageMul { get; private set; } = 1f;
        public float EnemyCountMul { get; private set; } = 1f;
        public float ManaRegenMul { get; private set; } = 1f;
        public float HpRegenMul { get; private set; } = 1f;

        public float ManaPerKill { get; private set; }
        public float HpPerKill { get; private set; }

        /// <summary>Peluang satu cast menembak dua kali. Digabung sebagai peluang GAGAL semua.</summary>
        public float EchoChance { get; private set; }

        /// <summary>Porsi HP saat bangkit. Nol berarti tidak ada pakta kebangkitan yang diambil.</summary>
        public float ReviveAt { get; private set; }

        /// <summary>
        /// Petak papan tambahan per sisi, dijumlah dari semua pakta ber-GridPlus. Pemakainya
        /// (GrimoireUI) yang mengeksekusi lewat Grimoire.SetSize — data di sini, aksi di sana.
        /// </summary>
        public int GridBonus { get; private set; }

        /// <summary>Petak tas tambahan per sisi (BagPlus). Aturan yang sama dengan GridBonus.</summary>
        public int BagBonus { get; private set; }

        /// <summary>
        /// Kebangkitan sudah dipakai. Disimpan di sini, bukan di pemain: aset paktanya dipakai
        /// bersama seluruh run yang pernah dimainkan, dan menulis "sudah dipakai" ke dalam aset
        /// berarti run berikutnya lahir tanpa jatah kebangkitannya.
        /// </summary>
        public bool ReviveSpent { get; private set; }

        /// <summary>Dinaikkan tiap ada pakta masuk. UI menumpang di sini untuk tahu kapan menggambar ulang.</summary>
        public int Version { get; private set; }

        public bool Has(WorldModifierDefinition def) => def != null && _taken.Contains(def);

        public float Stat(StatKind kind) => _stats[(int)kind];

        /// <summary>Mengambil sebuah pakta. Menolak yang sudah dimiliki — pakta tidak menumpuk pada dirinya sendiri.</summary>
        public bool Take(WorldModifierDefinition def)
        {
            if (def == null || _taken.Contains(def)) return false;

            _taken.Add(def);
            Recompute();
            return true;
        }

        /// <summary>Dipanggil saat run baru dimulai. Pakta tidak pernah menyeberang antar run.</summary>
        public void Clear()
        {
            _taken.Clear();
            ReviveSpent = false;
            Recompute();
        }

        /// <summary>
        /// Membelanjakan jatah kebangkitan. Mengembalikan porsi HP yang harus dikembalikan, atau
        /// nol kalau tidak ada jatah — pemanggil memakai nol itu sebagai "ya, kamu memang mati".
        /// </summary>
        public float SpendRevive()
        {
            if (ReviveSpent || ReviveAt <= 0f) return 0f;

            ReviveSpent = true;
            return ReviveAt;
        }

        void Recompute()
        {
            System.Array.Clear(_stats, 0, _stats.Length);

            EnemyHpMul = 1f;
            EnemySpeedMul = 1f;
            EnemyDamageMul = 1f;
            EnemyCountMul = 1f;
            ManaRegenMul = 1f;
            HpRegenMul = 1f;
            ManaPerKill = 0f;
            HpPerKill = 0f;
            ReviveAt = 0f;
            GridBonus = 0;
            BagBonus = 0;

            // Peluang gema digabung sebagai peluang TIDAK ADA yang menggema, lalu dibalik. Menjumlah
            // langsung akan menembus 100% di pakta ketiga, dan "peluang" yang selalu terjadi bukan
            // peluang — sekaligus membuat pakta gema keempat tidak bernilai apa pun.
            float noEcho = 1f;

            for (int i = 0; i < _taken.Count; i++)
            {
                var p = _taken[i];
                if (p == null) continue;

                Add(p.Boon);
                Add(p.Bane);

                EnemyHpMul *= Mathf.Max(0.1f, p.EnemyHpMul);
                EnemySpeedMul *= Mathf.Max(0.1f, p.EnemySpeedMul);
                EnemyDamageMul *= Mathf.Max(0.1f, p.EnemyDamageMul);
                EnemyCountMul *= Mathf.Max(0.1f, p.EnemyCountMul);
                ManaRegenMul *= Mathf.Max(0f, p.ManaRegenMul);
                HpRegenMul *= Mathf.Max(0f, p.HpRegenMul);

                ManaPerKill += p.ManaPerKill;
                HpPerKill += p.HpPerKill;

                noEcho *= 1f - Mathf.Clamp01(p.EchoChance);

                // Yang paling murah hati menang, bukan yang terakhir diambil. Dua pakta kebangkitan
                // tetap satu kebangkitan — jatahnya dipegang di sini, bukan di paktanya.
                if (p.ReviveAt > ReviveAt) ReviveAt = p.ReviveAt;

                GridBonus += Mathf.Max(0, p.GridPlus);
                BagBonus += Mathf.Max(0, p.BagPlus);
            }

            EchoChance = 1f - noEcho;
            Version++;
        }

        void Add(StatModifier[] mods)
        {
            if (mods == null) return;

            for (int i = 0; i < mods.Length; i++)
            {
                var mod = mods[i];
                if (mod.Type == StatKind.None || mod.Type == StatKind.Count) continue;

                _stats[(int)mod.Type] += mod.Value;
            }
        }
    }
}
