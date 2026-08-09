using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Starter yang dipilih pemain di menu, dibawa menyeberang ke scene game.
    ///
    /// Disimpan di <see cref="PlayerPrefs"/>, bukan di field static. Static kelihatan lebih
    /// bersih dan salah di sini karena dua hal: scene game bisa dimuat ulang sendiri (mati,
    /// ulangi run) tanpa pernah lewat menu lagi, dan editor membuang seluruh domain saat
    /// script direkompilasi. Keduanya menghapus static diam-diam, dan yang tersisa adalah run
    /// yang tiba-tiba memakai starter pertama — tanpa error, tanpa petunjuk.
    ///
    /// Yang disimpan ID, bukan rujukan aset: rujukan tidak bisa menyeberangi scene, dan ID
    /// yang asetnya sudah dihapus masih bisa ditolak dengan jujur di <see cref="Resolve"/>.
    /// </summary>
    public static class HeroChoice
    {
        const string Key = "grimoire.starter";

        /// <summary>ID starter terakhir yang dipilih, atau string kosong kalau belum pernah.</summary>
        public static string Id
        {
            get { return PlayerPrefs.GetString(Key, string.Empty); }
            set
            {
                PlayerPrefs.SetString(Key, value ?? string.Empty);

                // Ditulis SEKARANG, bukan saat aplikasi ditutup: pemain yang memilih starter lalu
                // gamenya crash di wave pertama harus tetap menemukan pilihannya waktu membuka
                // lagi — dan crash tidak pernah memanggil OnApplicationQuit.
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Starter pilihan pemain dari <paramref name="db"/>, atau starter pertama kalau
        /// pilihannya belum ada / asetnya sudah tidak ada lagi.
        ///
        /// Jatuh balik ke yang pertama, bukan ke null: run tanpa loadout sama sekali membuka
        /// papan kosong, dan papan kosong tidak bisa dimainkan.
        /// </summary>
        public static HeroLoadout Resolve(ContentDatabase db)
        {
            if (db == null) return null;

            string id = Id;
            if (string.IsNullOrEmpty(id)) return db.DefaultHero;

            var heroes = db.Heroes;
            for (int i = 0; i < heroes.Count; i++)
            {
                if (heroes[i] != null && heroes[i].Id == id) return heroes[i];
            }

            Debug.LogWarning("[HeroChoice] starter '" + id + "' tidak ada di ContentDatabase — " +
                             "dipakai yang pertama.");

            return db.DefaultHero;
        }
    }
}
