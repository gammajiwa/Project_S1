using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Angka pertarungan yang DITAMPILKAN, bukan angka yang dihitung.
    ///
    /// Seluruh simulasi tetap berjalan pada skalanya sendiri — damage 34, HP 120, dan semua
    /// rumus keseimbangan yang sudah diadu tidak disentuh sama sekali. Yang dikalikan hanya
    /// yang sampai ke mata. Itu bukan kecurangan melainkan satu-satunya cara aman: menaikkan
    /// angka aslinya berarti mengganti setiap konstanta di <see cref="GameBalance"/>, setiap
    /// aset piece, dan setiap kurva musuh sekaligus — dan cukup satu yang terlewat untuk
    /// membuat wave 12 mustahil tanpa ada yang tahu kenapa.
    ///
    /// Kenapa dibesarkan sama sekali: "34" dan "3.400" membawa informasi yang persis sama,
    /// tapi tidak terbaca sama. Angka besar yang berubah cepat adalah setengah dari rasa
    /// pukulan di game seperti ini; angka dua digit membuat pukulan terbesar dalam satu run
    /// terlihat sama saja dengan pukulan pertama.
    ///
    /// Yang TIDAK ikut dibesarkan: mana dan koin. Keduanya dibaca sebagai hitungan — "berapa
    /// kali aku bisa cast", "berapa yang bisa kubeli" — dan membesarkannya cuma menambah nol
    /// tanpa menambah apa pun yang bisa dipakai memutuskan.
    /// </summary>
    public static class BigNumber
    {
        /// <summary>
        /// Pengali tampilan. Disimpan di sini, bukan di <see cref="GameBalance"/>, karena ia
        /// bukan knob keseimbangan: mengubahnya tidak mengubah satu pun hasil pertarungan.
        /// </summary>
        public static float Scale = 100f;

        /// <summary>
        /// Titik untuk ribuan, koma untuk pecahan — seluruh teks game berbahasa Indonesia, dan
        /// "3,400" di tengahnya terbaca sebagai tiga koma empat. Ditulis eksplisit, tidak
        /// mengandalkan culture mesin: build yang dijalankan di locale lain akan memformat
        /// angkanya dengan cara yang berbeda dari yang pernah dilihat pemiliknya.
        /// </summary>
        static readonly System.Globalization.NumberFormatInfo Id = new System.Globalization.NumberFormatInfo
        {
            NumberGroupSeparator = ".",
            NumberDecimalSeparator = ",",
            NumberGroupSizes = new[] { 3 }
        };

        /// <summary>
        /// Angka bulat dengan pemisah ribuan. Pecahan dibuang di atas skala — "3.412,7" tidak
        /// pernah dibaca sampai koma saat ia melayang di atas gerombolan selama 0,75 detik.
        /// </summary>
        public static string Damage(float raw)
        {
            float shown = raw * Scale;

            // Di bawah 10 pecahannya justru satu-satunya yang membedakan dua angka, jadi di
            // situ ia dipertahankan. Ini hanya terjadi kalau Scale dikecilkan mendekati 1.
            if (shown < 10f) return shown.ToString("0.#", Id);

            return Mathf.RoundToInt(shown).ToString("N0", Id);
        }

        /// <summary>Sama, untuk nilai yang menempati ruang sempit (baris panel, kartu hover).</summary>
        public static string Short(float raw)
        {
            float shown = raw * Scale;

            if (shown >= 1_000_000f) return (shown / 1_000_000f).ToString("0.#", Id) + "jt";
            if (shown >= 10_000f) return (shown / 1_000f).ToString("0.#", Id) + "rb";

            return shown < 10f ? shown.ToString("0.#", Id) : Mathf.RoundToInt(shown).ToString("N0", Id);
        }
    }
}
