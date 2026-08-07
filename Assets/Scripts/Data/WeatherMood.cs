using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Satu cuaca: sekumpulan VFX yang menyala bersama-sama.
    ///
    /// Dikelompokkan, bukan didaftar satu per satu, karena cuaca adalah PAKET. Hujan tanpa angin
    /// terbaca sebagai hujan di ruang tertutup; angin tanpa daun terbaca sebagai desis. Yang
    /// membuat sebuah cuaca terasa nyata adalah beberapa hal yang terjadi bersamaan, dan
    /// mengundinya satu per satu akan menghasilkan gabungan yang tidak pernah dimaksudkan siapa pun.
    /// </summary>
    [System.Serializable]
    public class WeatherMood
    {
        public string Name = "Cerah";

        [Tooltip("Boleh kosong — itulah cuaca cerah, dan ia harus ada di daftar supaya tidak " +
                 "setiap wave berisi sesuatu yang jatuh dari langit.\n\n" +
                 "Tiap efek punya SKALA, BUTIRAN, dan OFFSET-nya sendiri. Satu pengali untuk " +
                 "seluruh cuaca tidak pernah cukup: hujan dan angin-berdaun ada di paket yang sama " +
                 "tapi tetesnya perlu membesar sementara daunnya perlu mengecil, dan daun perlu " +
                 "lahir jauh di kiri sementara hujan harus tepat di atas kepala.")]
        public AmbientVfxEntry[] Effects;

        [Tooltip("Peluang terpilih dibanding cuaca lain di daftar yang sama.")]
        [Min(0f)] public float Weight = 1f;

        [Tooltip("Peredam gerak partikel. Di bawah 1 seluruh cuaca ini melambat — dipakai untuk " +
                 "membedakan gerimis dari hujan lebat tanpa menyiapkan dua set prefab.")]
        [Range(0.1f, 2f)] public float Speed = 1f;

        [Tooltip("Basah. Efek suasana yang bertanda HideInRain akan disembunyikan selama cuaca ini.")]
        public bool Wet;

        [Tooltip("Seberapa mendung. Meredupkan matahari, mendinginkan ambient, dan menebalkan " +
                 "bayangan awan — sekaligus, karena mendung bukan satu perubahan melainkan " +
                 "beberapa yang terjadi bersamaan. 0 = tidak berubah.")]
        [Range(0f, 1f)] public float Overcast;
    }
}
