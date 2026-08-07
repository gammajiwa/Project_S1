namespace Proto
{
    /// <summary>
    /// Undian deterministik per wave: wave yang sama selalu menghasilkan angka yang sama,
    /// di setiap run.
    ///
    /// Yang TIDAK boleh dipakai untuk ini adalah <c>new System.Random(wave * K + C)</c>, dan itu
    /// pelajaran yang dibayar mahal: seed yang berjajar rapat menghasilkan sample PERTAMA yang
    /// berjajar rapat juga — konstruktor System.Random cuma mengaduk seed-nya secara linear.
    /// Terukur: undian malam 50% dengan cara itu menghasilkan wave 1–29 malam SEMUA, lalu
    /// 30–40 siang semua. Bukan meleset sedikit — polanya balok, dan mata menangkap balok.
    ///
    /// Hash avalanche di bawah mengubah satu bit masukan jadi separuh bit keluaran, jadi wave
    /// yang bersebelahan mendarat di ujung-ujung rentang yang tidak berhubungan.
    /// </summary>
    public static class WaveHash
    {
        /// <summary>Angka 0..1 untuk wave ini. Salt memisahkan undian yang berbeda —
        /// biome dan cuaca tidak boleh sinkron, "tiap malam pasti hujan" itu pola.</summary>
        public static float Roll01(int wave, int salt)
        {
            unchecked
            {
                uint h = (uint)(wave * 374761393) + (uint)(salt * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;

                // 24 bit teratas cukup — float cuma punya 24 bit mantissa, dan membaginya
                // dengan 2^24 menjamin hasilnya tidak pernah tepat 1.
                return (h >> 8) / 16777216f;
            }
        }
    }
}
