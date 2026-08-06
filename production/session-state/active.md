# Session State

<!-- STATUS -->
Epic: Grimoire Haven — arah bullet-haven
Feature: Auto-move, pengepungan musuh, wave berbasis jam, rendering instanced, konten 70 piece
Task: Semua terpasang & terverifikasi programatik — belum dinilai dengan tangan
<!-- /STATUS -->

## Baca ini dulu

**[docs/AI-HANDOFF.md](../../docs/AI-HANDOFF.md)** — dokumen serah-terima lengkap:
peta file, invarian yang dipegang kode, jebakan yang sudah memakan waktu, dan
daftar "jangan lakukan". Semua konteks yang dibutuhkan ada di sana.

Dokumen itu **baru saja ditulis ulang** (2026-08-06). Versi sebelumnya sudah
salah, bukan sekadar kurang lengkap.

## Yang berubah sesi ini

Satu sesi panjang, arahnya dari pemilik project: **rusuh, meledak-ledak,
dikeroyok banyak orang** — rasa Vampire Survivors dengan fase menyusun tetap ada.

**Rasa & tampilan**
- Angka damage melayang; hit yang berdekatan digabung jadi satu angka besar
- Proyektil: skill AoE/Zone jatuh dari langit, rantai petir melompat musuh ke
  musuh, sinar garis pakai LineRenderer, musuh meledak saat mati
- Meteor diubah dari Nova (meledak di kaki pemain) jadi jatuh ke gerombolan
- Panel skill diurut damage terbesar di atas, dengan peringkat + dps
- Garis evolusi menghubungkan bahan (dulu cuma kotak menyala)
- Kartu resep ALT jadi panel berikon: hasil kiri, formula kanan, punya = nyala

**Gameplay**
- Pemain bergerak sendiri menghindar; `MoveSpeed` jadi stat yang bisa dibangun
- Musuh mengepung: gaya pisah + membidik posisi depan + jalur serong per musuh
- Wave selesai karena **jam habis**, bukan lapangan bersih; spawn mengalir terus
- Drop dari kill ditahan sampai wave beres
- Bentuk piece terikat rarity: ★1 = 2–3 petak sampai ★5 = 8–9 petak

**Konten**
- 29 → **70 piece**, 20 → **71 resep**, piramida ★1 32 / ★2 20 / ★3 10 / ★4 4 / ★5 4
- Tier ★5 dibuat dari nol; keempat elemen kini punya jalur sampai puncak
- 70 ikon PNG placeholder — ganti art = timpa filenya

**Performa (diukur, bukan ditebak)**
- `BestCluster` 1,95 ms → **0,002 ms** dan datar terhadap jumlah musuh
- 200 draw call → **1** (rendering instanced, musuh tanpa GameObject)
- Cap musuh 200 → **500**; wave 20 jalan 59 fps dengan 397 musuh hidup

**Bug lama yang ketemu & dibenerin**
- Nova tidak pernah kena buff & crit — mengeluarkan skill terberat dari loop inti
- `PendingSpawns` berkurang walau spawn gagal → musuh hilang diam-diam di cap
- Buffer rantai 4 padahal Frost Prism 5 hit
- 41% damage tercatat sebagai `?` (Fireball & Frost Nova tanpa nama sumber)
- Rune ★3 tidak bisa didapat dari mana pun → sekarang lewat peleburan segel
- `SeatEvolved` cuma mencoba petak bekas bahan → resep lengkap bisa gagal senyap

## Berikutnya

1. **Main dan nilai rasanya.** Semua verifikasi sesi ini programatik. Kurva wave,
   tekanan grid, lompatan bintang, dan rasa auto-move belum pernah dinilai manusia
2. Varian musuh + **boss** — `SpawnOne()` sudah disiapkan jadi satu-satunya
   tempat stat per musuh diisi
3. Animasi baked (VAT) — jahitannya sudah ada di `EnemyRenderer.Compose()`
4. Optimasi FX `PlayerCaster` — hambatan berikutnya begitu VFX masuk
5. Sistem audio
6. Refactor `GrimoireUI.cs` (~1940 baris)

## Knob kalau ada yang meleset

| Gejala | Knob di `GameBalance.asset` |
|---|---|
| musuh kurang banyak | `SpawnRateBase`, `SpawnRatePerWave`, `SpawnRateGrowth` |
| wave kepanjangan/kependekan | `WaveDurationBase`, `WaveDurationPerWave` |
| masih kerasa diseret, bukan dikepung | `FlankWidth` naikkan |
| musuh muter-muter nggak nyampe | `FlankFade` naikkan |
| kepung terlalu mematikan | `EnemyContactDps` (menumpuk per musuh — naik dikit efeknya besar) |
| papan kesempitan | `Grimoire.Width/Height` (const, hardcode 7) |
| mana nyekik | `BaseManaRegen` |
