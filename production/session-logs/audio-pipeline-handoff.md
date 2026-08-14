# Audio Pipeline — Log Handoff

> **Untuk siapa pun yang melanjutkan** (AI lain / sesi lain / manusia): baca file ini
> dari atas ke bawah, lalu baca `docs/audio/audio-index.md`. Keduanya cukup untuk
> melanjutkan tanpa bertanya. Update file ini setiap langkah selesai.

Terakhir diperbarui: 2026-08-14, sesi Claude (audio pipeline).

## ATURAN KERAS (pelanggaran = kerusakan nyata, sudah kejadian 3x)

1. **JANGAN PERNAH menjalankan `Tools/Grimoire/Build Main Menu`.** Meregenerasi
   `MainMenu.unity` dan menggilas VFX/penataan tangan di scene. Sudah 2x menghapus
   kerja user. Perubahan menu HANYA lewat kode komponen atau prefab.
2. **Jangan sentuh prefab tataan tangan**: `MenuBackdrop.prefab`, `VitalsPrefab`
   (bola HP/mana), `GrimoirePanelPrefab` (buku), `ShopRig`, `StarterPanel.prefab`.
3. **Ada sesi Claude paralel yang aktif ngedit & commit repo ini.** Sebelum edit
   file besar (`GrimoireUI.cs`, `ProtoBootstrap.cs`): re-read dulu. Sebelum
   restore/revert apa pun: `git status` + `git log` SEGAR, jangan percaya snapshot.
4. Bahasa: default game sekarang **English** (keputusan user — JANGAN diubah).
   Sistem lokalisasi `Loc.cs` + `Assets/Resources/Loc/*.txt`, 10 bahasa, key-based.
5. Commit hanya kalau user minta, TANPA baris atribusi Claude/Co-Authored-By.
   Format Conventional Commits. Jangan pernah commit file milik sesi paralel —
   stage per-path, cek diff dulu.

## YANG SUDAH SELESAI (verified, jangan diulang)

1. **337 file audio** disalin dari `D:\GameGamma\project_b\Assets` ke
   `Assets/Audio/` — checksum md5 cocok 2 arah, semua `.meta` (AudioImporter +
   setelan) ikut, Unity sudah import sebagai 337 AudioClip, 0 error.
   Struktur: `Music/` (7), `SFX/Element/<Elemen>/` (27), `SFX/Skill/` (57),
   `SFX/UI/` (3), `SFX/Card/` (4), `Library/{UniversalSoundFX,GameMakersKit,Feel,400Sounds}` (239).
2. **10 file Vefects Trails SENGAJA di-skip** — S1 sudah punya pack yang sama di
   `Assets/Art/VFX/Packs/Vefects/`, GUID identik → tabrakan kalau disalin.
3. **8.519 file (~2.0 GB)** SFX pack yang pernah dipangkas dari project_b
   (commit `d5f52879` di repo project_b) sudah dipulihkan via `git archive` ke
   **`SoundSource/`** (di LUAR Assets — sengaja, Unity tidak meng-import-nya).
   Sudah masuk `.gitignore`. Ini rak bahan: browse manual, salin satuan ke
   `Assets/Audio/` hanya yang dipakai.
4. **git-lfs AKTIF di repo ini** (`*.wav/*.ogg/*.mp3` → LFS, hook terpasang,
   remote github.com/gammajiwa/Project_S1). Komentar lama di `.gitignore` yang
   bilang "repo ini tidak pakai LFS" itu KELIRU — jangan dipercaya.
5. **Pemetaan klip → konten game sudah diverifikasi adversarial** (11 agent,
   semua path & nama dicek ke asset/kode nyata): lihat `docs/audio/audio-index.md`.
   Ringkasan angka: 7 musik ke state nyata, 25 elemen (tier LOW/MED/HIGH =
   bintang 1-2/3/4-5), 24 skill/event, 6 UI (dengan file:line), 19 kurasi library,
   plus daftar Celah (file tanpa padanan / butuh keputusan user).

## KEPUTUSAN USER YANG SUDAH DIAMBIL (jangan tanya ulang)

1. SFX 2 GB → `SoundSource/` di luar Assets (BUKAN import semua ke Unity).
2. Vefects Anime VFX (32 file suara di project Crimson) → AMBIL. **BELUM
   dikerjakan** — sumber: `D:\GameGamma\project-Project-Crimson-2025\Assets\Vefects\Anime VFX URP\Sounds\`,
   wajib cek tabrakan GUID dulu (pola sama seperti kasus Vefects Trails).
3. Audio di-wire lewat **ScriptableObject, BUKAN hardcode** (aturan project:
   gameplay values di SO). Harus "gurih" dan **anti tubruk-tubrukan** (mixing
   dipikirkan: limit voice, dedup, prioritas, ducking).
4. Bahasa English dibiarkan; fitur Language tetap ada.

## PR BESAR YANG SEDANG DIKERJAKAN: wiring audio (status per langkah)

> **UPDATE 2026-08-14 (lanjutan):** langkah a-e SELESAI & compile bersih.
> - `Data/AudioTheme.cs`, `Systems/MusicDirector.cs`, `Menu/MenuMusic.cs` dibuat;
>   `Systems/AudioDirector.cs` di-upgrade (PlayClip + prioritas voice + dedup per-klip
>   + resolusi tema + helper UiPick/UiPlace/UiBuy/UiReroll/UiClick/UiClose +
>   PlayCast/PlayReaction + fanfare Evolve/Victory/GameOver + poll volume 0.5s).
> - Wiring: ProtoBootstrap (theme via Resources, MusicDirector, CombatLoop dari detik
>   pertama, ShopLoop saat OnRestEntered — subscribe SETELAH AttachRun, cast per piece
>   via PlayCast, reaction via PlayReaction, VictoryFanfare saat boss mati, ui.Sfx),
>   MainMenuController (MenuMusic.Ensure + klik di Wire), GrimoireUI (field Sfx +
>   10 titik: pick x3, place x3, buy, reroll, close x2, gameover, evolve).
> - `Assets/Resources/AudioTheme.asset` TERISI via execute_code: 0 slot gagal,
>   Pieces=2 (absolutezero, tempest), Reactions=2 (badaiapi=FIRESTORM, pecah=SHATTER).
> - Bug yang sudah ketangkap & difix: stinger bisu (MakeSource menyetel volume=0,
>   PlayOneShot mengalikannya) — _stinger.volume=1 di Init.
> - HOVER SENGAJA BELUM di-wire (slot PieceHover terisi di aset, pemanggil belum ada)
>   — butuh debounce "hanya saat objek hover berubah" di UpdateTooltip; jangan
>   dipasang tanpa gerbang itu.
> - Review adversarial 3 lensa (wiring/mixing/regresi) sedang jalan; temuan
>   confirmed harus difix sebelum commit.

Arsitektur target (semua di `Assets/Scripts/`):

### a. `Data/AudioTheme.cs` — ScriptableObject [STATUS: BELUM]
Satu aset bahan bunyi, pola persis `UiTheme` (bisa dituker sambil play mode,
slot kosong = fallback aman):
- **Musik**: MenuLoop, CombatLoop, ShopLoop, LoseStinger, WinStinger (dipakai
  saat boss mati — bukan tiap wave clear, terlalu sering), EvolveStinger.
- **Inti** (menimpa 8 suara sintesis `AudioDirector.Sound`): array per slot
  (Cast[], Blast[], Hit[], Death[], Reaction[], Pickup[], BossRoar[], WaveStart[])
  — array untuk VARIASI (rotasi acak; pasangan GENERIC/GENERIC2 impact).
- **Per elemen**: cast ringan + cast berat per Element (Fire/Ice/Lightning/Arcane
  — HANYA 4 ini, lihat Enums.cs). Flag `heavy` sudah dihitung pemanggil di
  ProtoBootstrap (lihat bagian wiring).
- **Per piece**: `PieceSfx[] { PieceDefinition Piece; AudioClip Cast; }` —
  minimal Absolute Zero (`sfx_cat_absolutezero`) & Tempest Sigil (`sfx_cat_tempest`).
- **Per reaction**: `ReactionSfx[] { <tipe reaction> Reaction; AudioClip Clip; }`
  — FIRESTORM (`sfx_cat_firestorm` → aset `Reaction_badaiapi`), SHATTER
  (`sfx_cat_shatter` → `Reaction_pecah`). Cek tipe arg `enemies.OnReaction`.
- **UI**: PiecePick, PiecePlace, HoverPiece, RerollShuffle, Click, PanelClose
  (file per slot ada di audio-index bagian SFX Antarmuka, lengkap file:line).
- **Knob mixing**: SameClipWindow (~0.045s), DuckAmount/DuckSeconds, trim volume
  per keluarga.
Aset ditaruh `Assets/Resources/AudioTheme.asset` → dimuat `Resources.Load`
fallback (preseden: RuneTiles.cs memuat RuneTileSet begitu; larangan
Resources.Load di project ini untuk SISTEM, bukan aset data).

### b. `Systems/AudioDirector.cs` — upgrade [STATUS: BELUM]
Yang sudah ada & DIPERTAHANKAN: 16 voice round-robin, MinGap per Sound, pitch
random 0.94-1.06, sintesis prosedural sebagai fallback, `Overrides[]` lama.
Yang ditambah:
- `PlayClip(clip, vol, pitch, priority)` — jalur umum klip mana pun.
- **Dedup klip**: klip sama yang diminta < SameClipWindow detik → skip.
  (MinGap lama per-KATEGORI; dedup ini per-KLIP. Dua-duanya perlu.)
- **Prioritas voice steal**: voice nyimpen prioritas; kalau 16 penuh, curi
  prioritas terendah-tertua. BossRoar/stinger prioritas tertinggi = tak tercuri.
- `Play(Sound)` resolusi: theme array (pilih acak) → Overrides lama → sintesis.
- Panggil `MusicDirector.Duck()` saat memutar suara prioritas tinggi.

### c. `Systems/MusicDirector.cs` — baru [STATUS: BELUM]
- 2 AudioSource crossfade (~1.5s), loop; `SetLoop(clip)` idempoten.
- `PlayStinger(clip)` — source ketiga, sekali jalan, sambil duck loop.
- `Duck(amount, seconds)` — turunkan volume loop sementara, pulih halus.
- Pakai **unscaled time** (game punya speed 1x-5x, musik tidak boleh ikut cepat).
- Volume = `GameSettings.MusicVolume` (CEK nama field persisnya di
  `Menu/GameSettings.cs`) × trim theme. Dengarkan perubahan setelan (cek apakah
  GameSettings punya event; kalau tidak, poll tiap ~0.5s cukup).

### d. Wiring [STATUS: BELUM]
- `Composition/ProtoBootstrap.cs` (RE-READ DULU, sesi paralel aktif di file ini):
  - Muat AudioTheme → `audio` + `music` baru.
  - Handler `caster.OnCast` yang sudah ada (sekitar baris 225-236, ada flag
    `heavy`): resolusi per-piece → per-elemen(heavy?) → core. CEK apa yang
    tersedia di argumen callback (butuh `PieceDefinition`-nya).
  - `enemies.OnReaction` (baris ~219): klip per-reaction → fallback lama.
  - `OnBossSpawned`: + CreatureRoar1 kandidat; `OnBossDied`: WinStinger.
  - Game over (cari pemanggil `ShowGameOver`): music stop + LoseStinger.
  - Toko/rest (cari `OnRestEntered` / `RunNodeKind.Shop`): ShopLoop ↔ CombatLoop.
  - Evolve (GrimoireUI `ResolveEvolutions` ~2932, banner "EVOLVE!"): EvolveStinger.
- `View/GrimoireUI.cs` (RE-READ DULU): field publik `Sfx` diisi bootstrap; bunyi
  di titik yang SUDAH diverifikasi file:line di audio-index bagian SFX Antarmuka:
  pickup (3 jalur ~2646/2658/2673), place (~2592/2616), beli toko (~2842),
  reroll (~2827), hover piece (debounce: hanya saat objek hover BERUBAH),
  tutup panel (~2755/2817).
- `Menu/MainMenuController.cs`: spawn pemutar MenuLoop di Awake (komponen kecil
  baru, JANGAN edit scene/prefab menu).

### e. Isi `AudioTheme.asset` [STATUS: BELUM]
Lewat Unity MCP (`execute_code` / `manage_scriptable_object`), klip sesuai
`docs/audio/audio-index.md`. JANGAN tulis YAML aset dengan tangan.

### f. Compile + verifikasi [STATUS: BELUM]
`refresh_unity(compile)` → `read_console`. Lalu review adversarial diff-nya
(minimal: semua event yang di-subscribe beneran ada; tidak ada AudioSource
yang dibuat per-play; dedup tidak memakan suara penting; musik pakai unscaled).
Uji telinga = tugas user (QA loop visual/audio user pakai mata & kuping sendiri).

## UPDATE 2026-08-14 (sesi lanjutan): musik & selera pemilik project

**ATURAN MUSIK (keputusan user, final):**
1. **Musik/BGM project_b DILARANG total** — 7 file BGM-nya sudah dihapus dari
   Assets/Audio/Music (git rm). SFX project_b boleh.
2. Kandidat musik dari project internal lain (Crimson/Toge) juga DITOLAK.
3. Orkestra megah DITOLAK ("terlalu orkestra, ini game idle") — arah yang benar:
   **dark tapi chill** = dungeon synth / dark ambient.
4. Terpasang sekarang (semua dari OpenGameArt, lihat docs/audio/music-licenses.md):
   Menu=Nymph of the Forest (dungeon synth), Combat=Creepy Forest (eerie ambient),
   Shop=Cave Theme (ambient). Stinger MASIH KOSONG — cari yang chill juga.
5. SFX: "matching per elemen" (petir=petir) tapi TIDAK menggelegar — elemen
   dipindah ke satu keluarga EM_*, volume cast jadi knob SO
   (CastLightVolume 0.38 / CastHeavyVolume 0.6), MusicTrim naik ke 0.7,
   Death diganti satu splat (sfx_cat_death dinilai jelek).
6. FreePD.com TUTUP permanen. CC-BY-SA dihindari (share-alike).

**Review adversarial (6 agent) — 10 temuan confirmed, SEMUA SUDAH DIFIX:**
jalur tema PlayCast/PlayReaction kini menghormati MinGap (temuan tinggi);
crossfade anti-pop (fade dibalik, retarget-back tanpa restart); duck hold pakai
Max; MenuMusic tidak bunuh diri saat MenuLoop kosong; slider SFX/MUSIC live
(Save() per geser); klik tutup-panel tidak dobel bunyi dengan pungut
(_panelCloseFrame); place lewat replace di tas berbunyi; UiHover akhirnya
di-wire dengan debounce per-perubahan-sasaran.

## CATATAN TEKNIS KECIL

- `AudioDirector` dibuat DI KODE oleh ProtoBootstrap (bukan di scene) — makanya
  `Overrides` tidak pernah bisa diisi Inspector; itulah alasan butuh SO + Resources.
- Musik per halaman menu (StarterLoop dst.) = nice-to-have, JANGAN prioritas.
- `HIGH_Card_Game_Alert_End_Round_01.wav` dipetakan ke WaveStart (nama file
  bilang "End Round", slot kita "Wave START" — arah kebalik, sudah disadari, tak
  masalah secara bunyi).
- File-file di bagian "Celah/parkir" audio-index (sfx burung, stance, dll):
  JANGAN dipetakan tanpa keputusan user.
- PlayerPrefs bahasa: `opt.language` (nilai "en" tersimpan di
  HKCU\Software\Unity\UnityEditor\DefaultCompany\My project).

## YANG BELUM DISENTUH SAMA SEKALI (backlog di luar audio)

- Restore 2 partikel `L3_Sparks (1)/(2)` yang hilang dari menu (satu-satunya
  yang benar-benar hilang saat scene menu dibangun ulang ke English oleh commit
  `2fa9462`; sisanya cuma ganti nama). User bilang "stop" saat ini dibahas —
  TANYA DULU sebelum menyentuh.
- Vefects Anime VFX dari Crimson (keputusan user: ambil) — belum dieksekusi.
