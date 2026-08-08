# Daftar aset — apa yang dibutuhkan, di mana ditaruh

Ditulis 2026-08-09 dari **kode yang benar-benar jalan**, bukan daftar generik.
Tiap baris menyebutkan satu hal: apakah asetnya bisa langsung dipakai begitu
di-drop (**drop-in**), atau masih butuh kode dulu (**butuh hook**).

Aturan letaknya cuma dua:

- **`Assets/GameData/Icons`** — PNG yang SUDAH dirujuk aset piece. Ganti art =
  **timpa file PNG-nya**, jangan pindah, jangan rename (rujukan lewat GUID file).
- **`Assets/Art` & `Assets/Audio`** — semua aset BARU (skin UI, peta, model,
  font, audio) DAN prefab pihak ketiga yang sudah kita ADOPSI.

## VFX yang sudah diadopsi (`Assets/Art/VFX/Prefabs/`)

23 prefab dipindahkan keluar dari paket Lana Studio, dipisah per PEKERJAAN —
bukan per paket asalnya:

| Folder | Isi | Dipakai di |
|---|---|---|
| `Light/` | Sunlight, Moonlight | god ray siang/malam — **disetel tangan pemilik project** |
| `Ambient/` | Birds ×2, Butterflies ×2, Embers ×2, MagicField ×2 | `BiomeDefinition.AmbientVfx` |
| `Weather/` | Leaves ×4, Wind_heavy, Rain ×3 | `WeatherMood.Effects` |
| `Skill/` | Tornado_sand, Tornado_snow, Orb_lightning, Rockfall, SpeedBoost_front | `PieceDefinition.ZoneVfx` |

**Kenapa dipindah, bukan dibiarkan di paketnya:** `Sunlight`/`Moonlight` sudah
disetel tangan. Kalau paket Lana di-reimport/di-update, prefab aslinya menimpa
setelan itu tanpa peringatan. `AssetDatabase.MoveAsset` mempertahankan GUID, jadi
semua rujukan aset biome tetap utuh. Sisa paket TIDAK disentuh.

**Masih nganggur di paket** (~33 prefab): Snow ×5, Sandstorm ×2, Bubbles ×3,
Butterflies warna lain, MagicField orange/rainbow, Orb sand/snow, SpeedBoost_aside.
Kandidat kalau nanti bikin biome salju/rawa.

---

## P0 — paling kerasa duluan

| # | Aset | Folder | Spek | Status |
|---|---|---|---|---|
| 1 | **Font in-game** | `Art/UI/Fonts` | TTF/OTF + TMP asset. Latin + angka; butuh tebal untuk angka damage | **butuh hook** — seluruh UI in-game masih `LegacyRuntime.ttf` (Arial bawaan). Menu sudah TMP |
| 2 | **Frame panel 9-slice** | `Art/UI/Panels` | 3 varian: papan grimoire, panel modal (toko/kejadian/slot), tooltip. 9-slice, border ~16–24 px | **butuh hook** — panel sekarang kotak warna polos |
| 3 | **Tombol 9-slice** | `Art/UI/Buttons` | idle / hover / pressed. Dipakai LANJUT, JUAL, PETA, TOKO, RESEP, 1x–5x | **butuh hook** |
| 4 | **Petak grid & tas** | `Art/UI/Frames` | 1 tile petak kosong + 1 tile tas (~64 px, boleh 9-slice) | **butuh hook** |
| 5 | **Bingkai rarity ★1–★5** | `Art/UI/Frames` | 5 border yang menempel di ikon piece 64 px | **butuh hook** |
| 6 | **Ikon node peta** | `Art/Icons/Map` | 6 buah, 64×64 PNG alpha: WAVE / ELITE / TOKO / ??? / SLOT / BOSS | **butuh hook** — sekarang cuma huruf W/E/T/?/S/B |
| 7 | **Token karakter di peta** | `Art/Map` | 64×64, siluet/potret hero. Ini "KAMU" yang jalan di peta | **butuh hook** (1 baris — sekarang lingkaran kuning generatif) |
| 8 | **Latar peta** | `Art/Map` | 1 gambar full-screen (perkamen/kain). 1920×1080, boleh tileable | **butuh hook** — sekarang warna solid `#0B0E1A` |

## P1 — bikin naik kelas

| # | Aset | Folder | Spek | Status |
|---|---|---|---|---|
| 9 | **Ikon piece final** | timpa `GameData/Icons` (master di `Art/Icons/Pieces`) | **64×64 PNG, alpha**, 107 piece. Nama file = nama yang sudah ada, JANGAN diubah | **drop-in** ✅ |
| 10 | **Ikon status/buff/kutukan** | `Art/Icons/Status` → timpa `GameData/Icons` | 64×64 (tampil 26 px, harus terbaca kecil). 7 ailment + 16 buff/kutukan | **drop-in** ✅ |
| 11 | **Ikon stat** | `Art/Icons/Stats` | 4: SERANG / NYAWA / MANA / TAHAN. 64×64 | **butuh hook** |
| 12 | **SFX** | `Audio/SFX` | 8 slot persis: `Cast`, `Blast`, `Hit`, `Death`, `Reaction`, `Pickup`, `BossRoar`, `WaveStart`. WAV mono 44,1 kHz | **drop-in** ✅ — isi array `Overrides` di AudioDirector; yang kosong tetap pakai bunyi sintetis |
| 13 | **Musik** | `Audio/Music` | 3 track loop: arena, pulau/suaka, boss. OGG, 60–120 dtk loop mulus | **butuh hook** — belum ada musik sama sekali |
| 14 | **Bar HP/mana** | `Art/UI/Bars` | fill + frame, plus bar HP boss (lebih besar) | **butuh hook** |
| 15 | **Model pemain** | `Art/Characters/Player` | **kapsul primitif sekarang.** Low-poly, ~2 unit tinggi, 1 material | **butuh hook** |
| 16 | **NPC pulau** | `Art/Characters/NPC` | 3 sosok: PEDAGANG, BANDAR, PERTAPA (kapsul sekarang) | **butuh hook** |

## P2 — nanti, tapi catat sekarang

| # | Aset | Folder | Spek | Catatan penting |
|---|---|---|---|---|
| 17 | **Model musuh** | `Art/Characters/Enemies` | 4 arketipe: Grunt, Cursed, Stalker (terbang), Spitter | ⚠️ **Musuh TIDAK punya GameObject** (invarian #2). Digambar `RenderMeshInstanced` → **haram SkinnedMeshRenderer/Animator**. Animasi harus **baked (VAT)**. Mesh statik low-poly aman |
| 18 | **Model boss** | `Art/Characters/Bosses` | 3: serpent, centipede, grub. Boss berupa RUAS berulang — cukup 1 mesh kepala + 1 mesh ruas per boss | idem, tanpa Animator |
| 19 | **Props pulau** | `Art/Props` | api unggun, gerobak pedagang, meja judi, obor | Suaka sekarang cuma lampu + kapsul |
| 20 | **VFX slot "dopamin"** | `Art/VFX/Prefabs/Skill` | gulungan + ledakan hadiah ala Vampire Survivors | Sudah dijanjikan sejak §21, panel slot masih teks |
| 23 | **VFX skill non-Zone** | `Art/VFX/Prefabs/Skill` | Projectile, Nova, Chain, Line masih primitif | **butuh hook** — slot prefab baru ada untuk `Zone` (`ZoneVfx`); pola yang sama tinggal ditiru |
| 21 | **Kursor** | `Art/UI/Cursor` | normal + "tangan memegang piece" | — |
| 22 | **Ambience** | `Audio/Ambience` | hutan siang, malam, hujan, angin | Cocok dikaitkan ke cuaca yang sudah ada |

---

## Aturan teknis yang mengikat

1. **Ikon piece: timpa, jangan pindah.** Generator `Tools/Grimoire/Generate
   Placeholder Icons` bersifat **create-only** — art buatanmu aman, tidak akan
   ditimpa. Tapi `Regenerate Placeholder Icons (TIMPA art)` **merusak** — hanya
   dipakai kalau bentuk piece berubah total.
2. **PNG UI**: import setting `Sprite (2D and UI)`, `Mesh Type: Full Rect`,
   `Compression: None` untuk ikon kecil (kompresi merusak alpha di 64 px).
3. **9-slice** wajib diatur `border` di Inspector — kalau tidak, framenya melar.
4. **Tidak ada Animator untuk musuh/boss.** Batas keras dari arsitektur renderer.
5. **Aset pihak ketiga tetap di `Assets/Plugin`** (3,9 GB di sana sekarang).
   Jangan menyalin isinya ke `Art/` — bikin dobel di build.
6. **`Assets/Screenshots` sudah di-gitignore** — itu keluaran alat, bukan sumber.

## Yang masih menunggu keputusan

- **Gaya UI**: sekarang seluruhnya kotak warna datar. Begitu frame/tombol masuk,
  gw perlu bikin satu `UiTheme` ScriptableObject (mirip `MenuTheme` yang sudah
  ada untuk menu) supaya art bisa dipasang tanpa menyentuh kode tiap kali.
  **Ini pekerjaan sekali, dan paling murah dilakukan SEBELUM art masuk banyak.**
- **Resolusi ikon**: 64×64 mengikuti yang sudah ada. Kalau art-nya mau 128,
  bilang dulu — generator dan layout ikut disetel.
