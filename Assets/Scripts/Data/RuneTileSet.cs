using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Enam belas petak rune yang <b>sudah jadi</b>: bingkai, latar, dan glyphnya digambar
    /// menyatu dalam satu gambar per petak.
    ///
    /// Ini jalur alternatif, bukan pengganti. Kalau terisi, papan menggambar gambar ini apa
    /// adanya dan berhenti menyusun petaknya sendiri dari pelat warna + glyph. Kalau
    /// <b>dikosongkan</b>, semuanya kembali ke cara lama tanpa satu baris kode pun diubah — itu
    /// syarat yang diminta pemilik project waktu memasukkan art ini: "ini percobaan, kalau jelek
    /// gw balikin lagi ke yang tadi".
    ///
    /// Urutan slotnya WAJIB sama dengan urutan sheet di <see cref="RuneTiles"/>: petak ke-0
    /// milik rune yang ikonnya <c>Rune_S1_1</c>, dan seterusnya sampai <c>Rune_S5_1</c>. Aturan
    /// "petak ke-k memakai gambar ke (ikon + k)" dihitung dari indeks yang sama, jadi urutan yang
    /// tertukar di sini menukar gambar di seluruh papan sekaligus.
    /// </summary>
    [CreateAssetMenu(fileName = "RuneTileSet", menuName = "Grimoire/Rune Tile Set")]
    public class RuneTileSet : ScriptableObject
    {
        [Tooltip("16 gambar petak, urut: S1_1..S1_6, S2_1..S2_4, S3_1, S3_2, S4_1..S4_3, S5_1.\n\n" +
                 "Kosongkan seluruhnya untuk kembali ke petak yang disusun kode (pelat warna + " +
                 "glyph terpisah). Slot yang kosong sendirian juga jatuh balik, satu petak saja.")]
        public Sprite[] Tiles = new Sprite[16];

        /// <summary>Gambar petak ke-<paramref name="index"/>, atau null kalau slotnya kosong.</summary>
        public Sprite At(int index)
        {
            if (Tiles == null || Tiles.Length == 0) return null;

            int n = Tiles.Length;
            return Tiles[((index % n) + n) % n];
        }
    }
}
