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
                 "setiap wave berisi sesuatu yang jatuh dari langit.")]
        public GameObject[] Effects;

        [Tooltip("Peluang terpilih dibanding cuaca lain di daftar yang sama.")]
        [Min(0f)] public float Weight = 1f;

        [Tooltip("Peredam gerak partikel. Di bawah 1 seluruh cuaca ini melambat — dipakai untuk " +
                 "membedakan gerimis dari hujan lebat tanpa menyiapkan dua set prefab.")]
        [Range(0.1f, 2f)] public float Speed = 1f;

        [Tooltip("Pengali CAKUPAN — luas petak tempat efeknya turun, BUKAN ukuran butirannya.\n\n" +
                 "Hujan harus menutupi seluruh layar, tapi tetesnya harus tetap sebesar tetes. " +
                 "Prefab-nya disetel untuk kamera setinggi badan; di kamera yang menunduk dari 18 " +
                 "unit ia cuma menetes di satu petak selebar beberapa unit.")]
        [Min(0.1f)] public float Scale = 1f;

        [Tooltip("Basah. Efek suasana yang bertanda HideInRain akan disembunyikan selama cuaca ini.")]
        public bool Wet;

        [Tooltip("Seberapa mendung. Meredupkan matahari, mendinginkan ambient, dan menebalkan " +
                 "bayangan awan — sekaligus, karena mendung bukan satu perubahan melainkan " +
                 "beberapa yang terjadi bersamaan. 0 = tidak berubah.")]
        [Range(0f, 1f)] public float Overcast;
    }
}
