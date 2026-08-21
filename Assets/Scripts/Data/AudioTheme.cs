using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Satu tempat untuk semua BAHAN bunyi: musik, sfx inti, sfx per elemen, per piece,
    /// per reaction, dan bunyi antarmuka. Saudara kandung <see cref="UiTheme"/>, dengan
    /// kontrak yang sama: slot mana pun boleh kosong, dan kosong tidak pernah error —
    /// pemakainya jatuh ke lapisan di bawahnya (sintesis prosedural di AudioDirector),
    /// supaya aset yang belum ada tidak pernah memblokir tes gameplay.
    ///
    /// Kenapa aset dan bukan konstanta: bunyi dinilai dengan telinga, dan telinga butuh
    /// mendengar sambil play mode jalan. Aset bisa dituker slotnya tanpa recompile.
    ///
    /// Dimuat lewat <c>Resources/AudioTheme</c> oleh composition root — pola yang sama
    /// dengan RuneTileSet di RuneTiles.cs. Larangan Resources.Load project ini berlaku
    /// untuk SISTEM (lihat RunDirector.cs:133), bukan untuk aset data.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioTheme", menuName = "Grimoire/Audio Theme")]
    public class AudioTheme : ScriptableObject
    {
        [System.Serializable]
        public struct PieceSfx
        {
            [Tooltip("Piece yang punya suara khas sendiri.")]
            public PieceDefinition Piece;

            [Tooltip("Diputar saat piece ini cast. Mengalahkan suara elemen & inti.")]
            public AudioClip Cast;
        }

        [System.Serializable]
        public struct ReactionSfx
        {
            public ReactionDefinition Reaction;
            public AudioClip Clip;
        }

        [System.Serializable]
        public struct ElementSfx
        {
            [Tooltip("Cast biasa (projectile, chain, dsb).")]
            public AudioClip Light;

            [Tooltip("Cast berat: Nova, AreaAtTarget, Line, SunStrike, ForcePush, RollingBall.")]
            public AudioClip Heavy;
        }

        // ------------------------------------------------------------------ musik

        [Header("Musik (loop)")]
        public AudioClip MenuLoop;
        public AudioClip CombatLoop;

        [Tooltip("Selama singgah di pulau rehat: toko dan kejadian.")]
        public AudioClip ShopLoop;

        [Header("Musik (stinger — sekali bunyi di atas loop)")]
        [Tooltip("Boss mati. BUKAN tiap wave beres — wave beres terjadi tiap dua menit dan " +
                 "fanfare penuh sesering itu berhenti terasa sebagai hadiah.")]
        public AudioClip WinStinger;

        [Tooltip("Game over. Loop-nya berhenti dulu, baru ini berbunyi di keheningan.")]
        public AudioClip LoseStinger;

        [Tooltip("Dua piece melebur (banner EVOLVE!).")]
        public AudioClip EvolveStinger;

        [Tooltip("Bunyi WAVE BERES — sting kemenangan kecil, sekelas di bawah WinStinger " +
                 "milik boss. Kosong = arpeggio lonceng sintesis (cadangan lama).")]
        public AudioClip WaveClear;

        [Tooltip("Lapisan KILAU di atas WaveClear — dimainkan bersamaan, lebih pelan. " +
                 "Satu klip saja jarang terdengar 'menang'; pengumuman + gemerlap itulah " +
                 "resepnya (pola yang sama dengan JackpotBlast). Kosong = tanpa lapisan.")]
        public AudioClip WaveClearSparkle;

        [Header("Volume musik")]
        [Tooltip("Pengali di bawah slider MUSIC milik pemain. Musik itu alas, bukan lawan " +
                 "bicara — di 1.0 ia bertarung dengan SFX memperebutkan telinga.")]
        [Range(0f, 1f)] public float MusicTrim = 0.55f;

        [Range(0f, 1f)] public float StingerTrim = 0.8f;

        [Header("Volume cast (di bawah slider SFX pemain)")]
        [Tooltip("Cast biasa. Sengaja RENDAH: di game idle skill menembak sendiri terus-menerus, " +
                 "dan hujan cast yang keras menelan musik dalam semenit. Petir boleh berbunyi " +
                 "petir — tapi tidak boleh menggelegar di atas segalanya.")]
        [Range(0f, 1f)] public float CastLightVolume = 0.38f;

        [Range(0f, 1f)] public float CastHeavyVolume = 0.6f;

        [Tooltip("Lama silih ganti antar loop, detik. Pakai waktu UNSCALED — game punya " +
                 "kecepatan 1x-5x dan musik tidak boleh ikut ngebut.")]
        [Range(0.1f, 6f)] public float CrossfadeSeconds = 1.6f;

        // ------------------------------------------------------------------ sfx inti

        [Header("SFX inti — menimpa sintesis AudioDirector. Array = variasi, diundi tiap bunyi")]
        public AudioClip[] Cast;
        public AudioClip[] Blast;
        public AudioClip[] Hit;
        public AudioClip[] Death;
        public AudioClip[] Reaction;
        public AudioClip[] Pickup;
        public AudioClip[] BossRoar;
        public AudioClip[] WaveStart;

        // ------------------------------------------------------------------ per elemen

        [Header("Cast per elemen — mengalahkan SFX inti, kalah dari per-piece")]
        public ElementSfx Fire;
        public ElementSfx Ice;
        public ElementSfx Lightning;
        public ElementSfx Arcane;

        // ------------------------------------------------------------------ khusus

        [Header("Pantulan peluru")]
        [Tooltip("\"Tuk\" tiap peluru memantul ke sasaran berikutnya — dirotasi acak. " +
                 "Harus TERDENGAR: pantulan adalah stat yang dibeli pemain.")]
        public AudioClip[] Bounce;

        [Header("Per piece (paling spesifik, menang atas semuanya)")]
        public PieceSfx[] Pieces;

        [Header("Per reaction")]
        public ReactionSfx[] Reactions;

        // ------------------------------------------------------------------ antarmuka

        [Header("Antarmuka")]
        [Tooltip("Mengambil piece ke tangan (dari papan, tas, atau lantai).")]
        public AudioClip PiecePick;

        [Tooltip("Menaruh piece (ke papan atau tas), dan barang belanjaan mendarat.")]
        public AudioClip PiecePlace;

        [Tooltip("Kursor pindah ke piece lain. Pemanggil WAJIB menggerbangi 'hanya saat " +
                 "objek hover berubah' — tanpa gerbang itu ini berdesis tiap frame.")]
        public AudioClip PieceHover;

        public AudioClip Buy;
        public AudioClip Reroll;
        public AudioClip Click;
        public AudioClip PanelClose;

        // ------------------------------------------------------------------ hadiah

        [Header("Hadiah")]
        [Tooltip("Lapisan gemerincing TIPIS di atas PiecePlace — kepuasan kecil tiap menaruh, " +
                 "itu yang membuat menata papan terasa seperti memasukkan koin ke celengan.")]
        public AudioClip PlaceSweetener;

        [Tooltip("Ckring pendek — dirotasi acak untuk hujan koin kemenangan.")]
        public AudioClip[] CoinTick;

        [Tooltip("Tumpahan koin sekaligus, menang besar.")]
        public AudioClip CoinShower;

        [Tooltip("Lonceng heboh saat jackpot piece.")]
        public AudioClip Jackpot;

        [Tooltip("Kilau evolve — berbunyi bersama stinger musiknya (atau sendirian).")]
        public AudioClip EvolveBurst;

        // ------------------------------------------------------------------ mixing

        [Header("Mixing — rem anti tubruk-tubrukan")]
        [Tooltip("Klip yang SAMA tidak boleh mulai dua kali dalam jendela ini (detik). " +
                 "Dua puluh musuh mati di frame yang sama = SATU bunyi, bukan dinding suara. " +
                 "Ini per-klip; jeda per-kategori (MinGap) di AudioDirector tetap berlaku.")]
        [Range(0.01f, 0.25f)] public float SameClipWindow = 0.045f;

        [Tooltip("Seberapa dalam musik menunduk saat stinger/boss berbunyi. 0.45 = " +
                 "volume musik tinggal 55%.")]
        [Range(0f, 1f)] public float DuckAmount = 0.45f;

        [Range(0.1f, 3f)] public float DuckSeconds = 0.9f;

        // ------------------------------------------------------------------ resolusi

        /// <summary>
        /// Suara cast sebuah piece: per-piece dulu, elemen belakangan. Null = tidak ada
        /// yang spesifik; pemanggil jatuh ke suara inti lalu sintesis.
        /// </summary>
        public AudioClip CastClipFor(PieceDefinition def, bool heavy)
        {
            if (def == null) return null;

            if (Pieces != null)
            {
                for (int i = 0; i < Pieces.Length; i++)
                {
                    if (Pieces[i].Piece == def && Pieces[i].Cast != null) return Pieces[i].Cast;
                }
            }

            var set = SetFor(def.Element);

            // Berat tanpa klip berat memakai klip ringan — lebih baik bunyi yang kurang
            // gagah daripada skill besar yang bisu.
            return heavy && set.Heavy != null ? set.Heavy : set.Light;
        }

        public AudioClip ReactionClipFor(ReactionDefinition reaction)
        {
            if (reaction == null || Reactions == null) return null;

            for (int i = 0; i < Reactions.Length; i++)
            {
                if (Reactions[i].Reaction == reaction) return Reactions[i].Clip;
            }

            return null;
        }

        ElementSfx SetFor(Element element)
        {
            switch (element)
            {
                case Element.Fire: return Fire;
                case Element.Ice: return Ice;
                case Element.Lightning: return Lightning;
                default: return Arcane;
            }
        }
    }
}
