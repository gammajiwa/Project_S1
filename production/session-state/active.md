# Session State

<!-- STATUS -->
Epic: Grimoire Haven — arah bullet-haven
Feature: Peta run STS (visual baru), relokasi antar wave, kunang-kunang, god ray
Task: Terverifikasi lewat screenshot — tinggal dinilai main tangan
<!-- /STATUS -->

## Baca ini dulu

**[docs/AI-HANDOFF.md](../../docs/AI-HANDOFF.md)** — peta file, 24 invarian,
23 jebakan, daftar "jangan lakukan", dan urutan menjalankan editor tool dari nol.
Ditulis ulang 2026-08-06; versi sebelumnya sudah salah, bukan sekadar kurang.

> **Catatan alur kerja:** pemilik project memakai dua AI bergantian. Semua progres
> harus mendarat di dua file ini (`AI-HANDOFF.md` + `active.md`) supaya sesi
> berikutnya — siapa pun yang menjalankannya — bisa nyambung tanpa membaca ulang
> seluruh codebase. Update di akhir tiap potongan kerja, bukan di akhir sesi.

## Kondisi sekarang

74 piece · 75 resep · 7 status · 9 reaksi · 6 buff · **4 kutukan** ·
**4 arketipe musuh** · 74 ikon placeholder.

Wave 15 diuji: **500 musuh hidup, 5 draw call, 59 fps.**

## Yang beres di sesi ini

**Rasa & tampilan** — angka damage melayang yang menggabung hit berdekatan;
skill AoE/Zone jatuh dari langit; rantai petir melompat musuh ke musuh; sinar
garis LineRenderer; musuh meledak saat mati; panel skill urut damage; garis
evolusi menghubungkan bahan; kartu resep ALT berikon.

**Gameplay** — pemain bergerak sendiri menghindar (`MoveSpeed` jadi stat);
musuh mengepung (gaya pisah + bidik posisi depan + jalur serong); bentuk piece
terikat rarity; drop ditahan sampai wave beres.

**Wave** — jam mengatur **spawn**, bukan wave. Wave tetap selesai dengan
menghabiskan lapangan; begitu jendela spawn tutup, sisa musuh ngebut 1,9× dan
berhenti memutar. Itu membunuh ekor mati tanpa menghapus musuh.

**Varian musuh** — Grunt / Cursed / Stalker (terbang) / Spitter (penembak).
Peluru musuh punya pool + renderer instanced sendiri.

**Kutukan** — 4 efek negatif dari musuh, 4 piece penangkal, slot terpisah dari buff.

**Performa** — `BestCluster` 1,95 ms → 0,002 ms dan datar; 200 draw call → 1–5;
cap musuh 200 → 500.

## Bug yang ketemu & dibenerin

- Nova tidak pernah kena buff & crit — mengeluarkan skill terberat dari loop inti
- `PendingSpawns` berkurang walau spawn gagal → musuh hilang senyap di cap
- Buffer rantai 4 padahal Frost Prism 5 hit
- 41% damage tercatat `?` (Fireball & Frost Nova tanpa nama sumber)
- Rune ★3 tidak bisa didapat dari mana pun → lewat peleburan segel
- `SeatEvolved` cuma mencoba petak bekas bahan → resep lengkap bisa gagal senyap
- `BuffCooldownMul` & mana-cost multiplier dibatasi di 1 → nilai negatif (debuff)
  tidak berpengaruh sama sekali
- `Separation()` mengembalikan magnitude mentah → menelan kemudi lain, Spitter
  terdorong masuk melee
- `TakeDamage` mengurangi Defense × deltaTime → hampir nol terhadap tembakan sesaat

## Strip ikon & kabel resep — SUDAH BERES

**Tiga strip ikon** menggantikan tiga baris teks, ditumpuk di bawah bar mana
(`View/StatusStrip.cs`): buff (y −96), kutukan (−128), dan tally ailment di
seluruh swarm (−160). Tiap entri = ikon + angka, hover memunculkan efeknya.
Teks salah bentuk untuk ini: dia reflow tiap ada yang masuk/keluar, jadi tidak
ada yang diam cukup lama untuk dikenali. Ikon menahan posisi.

Ikon status & buff digenerate sebagai **glyph pip ala dadu** (1–6 titik) —
warna saja tidak cukup memisahkan tujuh ailment di ukuran 26 piksel, dan tanpa
font tidak ada cara menaruh huruf di tekstur.

**Kabel resep saat piece diangkat** (`Grimoire.FindPartners`): begitu piece
diangkat, kabel langsung terbentang dari KURSOR ke tiap calon pasangan di papan.
Emas kalau menaruhnya di sebelah situ langsung menyelesaikan resep (bahan 2),
biru kalau resepnya masih butuh bahan ketiga.

## Putaran feedback playtest (2026-08-06)

Pemilik project main dan bilang "lumayan seru". Yang diperbaiki setelahnya:

**Aturan warna garis diperbaiki.** Sebelumnya kabel saat mengangkat piece bisa
emas. Salah. Aturannya sekarang tunggal dan tegas: **emas = janji bahwa grup ini
AKAN berevolusi saat wave berakhir.** Apa pun yang masih di tangan belum
menjanjikan apa-apa, jadi selalu biru — termasuk grup yang cuma lengkap karena
piece di kursor ikut dihitung (`EvoPreview.NeedsHeldPiece`).

**Panel damage digabung ke panel spell.** Dulu dua blok terpisah. Sekarang tiap
baris spell membawa persentase kontribusinya, diurut dari **damage yang sudah
benar-benar dihasilkan** (bukan angka di kartu). Satu baris kecil di bawah judul
membawa total + sumber non-skill (reaksi & ailment), karena mereka rutin
menyumbang sepertiga damage dan tidak punya baris sendiri.

**Tuning:**

| Keluhan | Perubahan |
|---|---|
| sekali drop langsung OP | `KillDropChance` 0.12→0.035, `MaxDropsPerWave` 8→3, `WaveClearDrops` 3→1 |
| musuh awal terlalu banyak | `SpawnRateBase` 0.8→0.5, `SpawnRatePerWave` 0.28→0.22 (wave 1: 39→26 musuh) |
| spawn jauh, nunggu dulu | kotak spawn 24×15→21×13, **plus `WaveOpenerCount` 6 musuh langsung ditaruh di 60% jarak.** Tunggu awal wave 19 detik → **8 detik** |
| musuh kadang tiba-tiba ngebut | `ClosingSpeedMultiplier` 1.9→**1**. Sprint akhir wave dimatikan |
| speed musuh terlalu cepat/bervariasi | `EnemySpeedMin/Max` 1.5–2.1 → **1.35–1.6**, per-wave 0.04→0.025 |

> **Risiko yang diambil sadar:** mematikan sprint akhir wave bisa menghidupkan
> lagi ekor mati. Dua hal lain menahannya — spawn lebih dekat, dan berhentinya
> gerakan memutar saat `Closing`. Kalau ekornya balik terasa lama, knob-nya
> `ClosingSpeedMultiplier`.

## Putaran feedback playtest #2

**BUG: emas tapi tidak berevolusi.** Preview hanya menanyakan "bahan lengkap dan
bersentuhan?" — tidak pernah "hasilnya muat didudukkan?". Papan penuh → `Merge`
gagal, bahan dikembalikan, dan garis emas bertahan melewati wave tanpa penjelasan.
Sekarang `CouldSeat()` (murni, tidak memindahkan apa pun) ikut menentukan warna.
Diuji: tas penuh 25 piece, dua Fireball bersentuhan → **biru**, dan memang tidak
berevolusi. Preview dan hasil cocok.

**Mesin resep dijadikan satu** (`Model/RecipeResolver.cs`). Papan dan tas
menanyakan pertanyaan yang sama persis dan cuma beda aturan penempatan (skill di
papan butuh rune di bawah tiap petak; di tas tidak), jadi hanya aturan itu yang
disuntikkan. Disatukan karena preview dan merge **wajib sepakat** — mereka sempat
berbeda, dan gejalanya persis bug emas di atas.

**Tas: 5×4 → 5×5, dan bisa berevolusi di dalamnya.** Dulu murni penyimpanan, jadi
jalan buntu: salinan menumpuk tanpa bisa jadi apa-apa. `SellY` digeser 190→232
karena lima baris tas mencapai y=205.

**Kontrol dipisah.** `R` = putar, **klik kanan = kunci** (dulu berbagi tombol dan
artinya berubah tergantung tangan penuh atau tidak). Kunci juga berlaku di tas.
Piece terkunci tidak terlihat oleh resep mana pun: tidak dimakan, dan tidak ada
garis yang digambar ke arahnya.

**Footprint: `FootprintPass` jadi satu-satunya sumber kebenaran** untuk semua 74
piece. Dulu tiga pass menetapkan bentuk sendiri-sendiri dan tangganya berlubang.
Sekarang ada override bernama + aturan per-bintang untuk sisanya.

Bintang 1 sebelumnya 14 piece di 3 petak. Sekarang:

| Petak | Jumlah (★1) |
|---|---|
| 1 | 6 |
| 2 | 16 |
| 3 | 6 |

Semua segel ★1 turun ke 2 petak — segel itu pengisi celah, bukan pesaing papan.

## Hero & loadout awal

**`HeroLoadout` (SO baru).** "Hero" jadi konsep berbasis data, bukan empat id
hardcode di UI — hero kedua nanti = aset lain, bukan cabang kode lain. Posisi
tiap piece ditulis eksplisit di aset, bukan auto-place, karena **jarak antar dua
skill pembuka ITU keputusan desainnya**.

**Hero 1 — Emberwright.** Enam piece: **2 rune, 2 skill, 2 segel** — tiga kategori
piece yang ada di game ini, satu pasang masing-masing.

| Kategori | Isi |
|---|---|
| Rune (alas) | Ember Rune ★1 2×2 @(2,2) · Whetstone Rune ★2 2 petak @(4,4) |
| Skill | Fireball @(2,2) · Frost Shard @(3,3) — **menyilang, tidak bersentuhan** |
| Segel (item pasif) | Keen Sigil @(4,4) · Ward Sigil @(5,4) — +20% crit, +20% tahan kutukan |

Kedua skill 1 petak dan **berbeda**, dipasang diagonal. Diagonal bukan bersentuhan,
jadi mereka tidak melebur sendiri — menggesernya adalah langkah pemain, dan itu
keputusan nyata pertama sebuah run.

**Resep pembuka baru: `fireball + frostshard -> Steam Burst`.** Dibutuhkan karena
tidak ada satu pun pasangan skill 1-petak BERBEDA yang punya jalur lebur; satu-
satunya pembuka yang bisa melebur adalah dua piece yang sama, dan itu pilihan
malas. Api + es = uap, jadi temanya juga jalan.

Kedua segel duduk di alas yang lain supaya alas api bisa dikosongkan utuh saat
kedua skill melebur. Diuji: geser Frost Shard → grup **EMAS** → akhir wave
`Fireball + Frost Shard -> Steam Burst`, Steam Burst mengisi Ember Rune persis,
**kedua segel tetap utuh**.

**Greater Fireball diubah jadi 2×2 (dulu Huruf T).** Ini load-bearing, bukan
kosmetik: Huruf T butuh 3 petak melintang sedangkan alas pembukanya cuma 2×2,
jadi upgrade yang dijanjikan hero **mustahil terjadi** — resepnya lengkap dan
hasilnya tidak pernah bisa didudukkan. Ketahuan justru oleh `CouldSeat()` yang
baru dipasang: garisnya biru, bukan emas palsu.

Diuji penuh: awal 2 spell & 0 grup → didempetkan → grup **EMAS** → akhir wave
`Fireball + Fireball -> Greater Fireball`, papan jadi 1 spell dengan Greater
Fireball mengisi Ember Rune persis.

**Tas 5×5 → 4×4** (`SellY` balik ke 196).

## Putaran feedback #4

**Wave kembali berbasis JUMLAH, bukan jam.** `SpawnWindow*` dibuang, `EnemyCountFor`
kembali. Laju spawn tetap ada tapi cuma mengatur kecepatan datang. HUD: `sisa
N/total`. Alasannya: jam membuat pemain menatap hitung mundur, sementara "sisa
berapa" adalah satu-satunya angka yang bisa ditindaklanjuti.

| Wave | Jumlah | HP | ~durasi |
|---|---|---|---|
| 1 | 19 | 23 | 24s |
| 5 ⚡ | 67 | 56 | 27s |
| 10 ⚡ | 147 | 109 | 30s |
| 15 ⚡ | 265 | 186 | 33s |
| 20 ⚡ | 438 | 299 | 37s |

**Rune membawa penumpangnya.** Mengangkat sebuah rune ikut mengangkat skill yang
berdiri **seluruhnya** di atasnya (`Grimoire.LiftRiders` / `SeatRiders`). Skill yang
menjembatani dua rune tidak ikut — ia bukan milik salah satunya, jadi tetap jatuh
ke lantai seperti sebelumnya. Diuji: Ember Rune membawa Fireball + Frost Shard;
Fire Rain yang melintang antara Ember dan Void Rune lepas.

> Penumpang **dilepas** kalau rune diputar, dijual, atau dimasukkan ke tas.
> Offset penumpang ditulis di kerangka rune yang belum diputar, dan menurunkannya
> lewat rotasi itu tipe bug yang kelihatan benar sampai alas 3-petak diputar dua kali.

**Tes sapu bersih ★5** — build maksimum yang mungkin (4 rune ★3 + 4 skill ★5):

| Skill | Damage | Radius |
|---|---|---|
| Cataclysm | **799** | 13,2 |
| Absolute Zero | 660 | 9,0 |
| Stormbreaker | 300 ×12 lompatan | 28,8 |
| Singularity | 60/tick | 13,6 |

Cataclysm sekali tembak membunuh sampai **wave 33** (di wave 20 masih 2,7× lebih
dari cukup). Di wave 15 lapangan cuma menahan ~12 musuh hidup dari 265 yang datang
— mereka mati begitu sampai. Jadi ya, harapan "sekali nge-skill kesapu semua"
tercapai, dan ada batasnya di sekitar wave 33.

> **Catatan:** tidak ada segel ★5 maupun rune ★5. Segel berhenti di ★2, rune di ★3.
> Tes ini memakai yang tertinggi yang benar-benar ada.

## Putaran feedback #5 — skill jadi punya PERILAKU, bukan cuma angka

Sebelumnya semua skill membidik: ke yang terdekat, ke gerombolan terpadat, atau
menyusuri garis ke arah seseorang. Itu membuat setiap skill rarity tinggi menjadi
**kata kerja yang sama dengan angka lebih besar**, dan itulah kenapa ★5 terasa
seperti ★3 berstat bagus. Tiga perilaku baru, tidak satu pun kosmetik:

**`CastKind.Radial`** — tidak membidik siapa pun. Menunggu ada yang masuk
jangkauan lalu menyembur ke SEGALA ARAH sekaligus, dengan sudut awal diacak tiap
tembakan. Berdiri di tengah cincin jadi permainan yang kuat, bukan yang kalah.
Diuji: 16 peluru aktif, sudutnya `61° 84° 106° 129° 151° 174° -164° …` — merata.

**`PieceDefinition.Forks`** — beberapa rantai berangkat pada saat yang sama, dan
tiap cabang **dilarang menyentuh apa yang sudah disambar cabang lain**
(`ChainFrom(..., alreadyTaken)`). Larangan itulah yang membuat mereka melebar,
bukan sekadar menusuk satu barisan.

**`PieceDefinition.ZoneDrift`** — kubangan mengembara. Arahnya *dibelokkan* sedikit
tiap frame, bukan diacak ulang, jadi jalurnya melengkung seperti cuaca bukan
berkedut seperti derau. Kubangan diam dibaca sekali lalu diabaikan; yang bergerak
harus dibaca terus, dan bisa mengejar gerombolan yang sedang kamu hindari.
Diuji: Singularity bergerak dari `(-4.9, -8.6)` ke `(-4.8, -7.9)`.

| Skill | Perilaku baru |
|---|---|
| Stormbreaker ★5 | **8 cabang × 4 lompatan** = 32 musuh sekali cast |
| Absolute Zero ★5 | **Radial 16 arah**, tidak membidik |
| Singularity ★5 | kubangan **mengembara** 3,4/dtk |
| Thunder Crown ★4 | 4 cabang × 3 lompatan |
| Ion Storm ★3 | kubangan mengembara 2,6/dtk |
| Whirling Blade ★1 | **Radial 5 arah** — idenya diajarkan sejak awal |

**Bentuk baru yang menyulitkan** (`Zed` 7, `Aitch` 7, `Ess` 6, `Fork` 6), semua
tetap dalam kotak 3×3. Bukan sekadar besar — mereka **mengurung petak mati**: H
menyisakan dua petak tunggal yang cuma bisa diisi piece 1-petak, Z menyisakan dua
coakan. Dipasang di band ★4, jadi tier itu sekarang: `Ess / Fork / Zed / Aitch /
Hook`.

## Putaran feedback #6 — kombo antar skill (arah PoE)

**81 piece, 82 resep.** Tiga sumbu kombo baru, semuanya berbasis aset:

**1. Penanda + peledak (`CastKind.Detonate`).** Satu skill menghabiskan seluruh
cast-nya mengoles ailment murah; skill lain menagih semuanya sekaligus, sebesar
POIN yang menumpuk di tiap musuh, lalu mencabut ailment-nya.

| Piece | Peran |
|---|---|
| Plague Brand ★1 | damage 2 (!), POISON 3 poin, radius 5,5 — **sia-sia sendirian, itu disengaja** |
| Sunder ★2 | meledakkan semua ber-POISON, 26/poin |
| Rupture ★3 | meledakkan semua ber-BLEED, 52/poin |
| Reckoning ★4 | meledakkan semua ber-BURN, 95/poin |

Peledak **menahan cooldown DAN mana kalau tidak ada yang bertanda** — peledak yang
menembak ke lapangan kosong tidak akan pernah siap saat lapangannya akhirnya
bertanda, dan itu seluruh isi kombonya.

**2. Charge ala PoE (`BuffDefinition.MaxStacks` + `GrantOnKill` + `ConsumesCharge`).**
Kumpulkan sambil membunuh, belanjakan sekaligus.

- **FRENZY** (maks 6): −6% cooldown, +6% damage **per tumpukan**
- **POWER** (maks 5): +7% crit, +12% damage crit per tumpukan
- Generatornya **segel** — makan petak, tidak menghasilkan apa pun sendiri
- **Frenzy Release ★3** menghabiskan semua tumpukan: **+55% damage per tumpukan**,
  dan **tidak menembak sama sekali** kalau belum ada yang dikumpulkan

`Grimoire.KillGrants` dikompilasi sekali saat grid berubah — handler kill jalan
ratusan kali per detik di wave tinggi dan tidak boleh menyusuri papan.

**3. Peluru memantul (`Bounces`).** "Nembak segala arah" terlalu biasa; memantul
membuatnya membaca lapangan — tembakan ke petak kosong mati, tembakan ke gerombolan
menggergaji. Whirling Blade 3 pantulan, Absolute Zero 4, Spark Bolt 2, Glacial
Spike 3. Musuh yang baru disambar dikecualikan supaya sepasang musuh tidak
memantul-mantulkan satu peluru di tempat.

**4. Reaksi yang membayar lebih dari damage.** `CleansesOneDebuff` (mencabut kutukan
dengan sisa TERPANJANG — yang pendek toh mau habis sendiri) dan `RefundMana`.
SHATTER & STATIC FREEZE mencabut kutukan; TOXIC BURST/BLOOD SURGE/FIRESTORM
mengembalikan mana. Build ailment jadi **jawaban** atas kutukan musuh, bukan cabang
terpisah yang harus dibayar sendiri.

Diuji di wave 10: FRENZY menumpuk penuh **6/6** dari kill, Sunder 51% damage total,
Frenzy Release 45%, Plague Brand **1%** — persis bentuk yang dimau: penanda hampir
tidak melukai, peledaknya yang menagih.

## BERIKUTNYA

1. **Main dan nilai rasanya** — masih belum pernah dinilai tangan manusia
2. **Boss** — tinggal satu aset `EnemyArchetype` lagi dengan angka besar
3. **Animasi baked (VAT)** — jahitannya di `EnemyRenderer.Compose()`
4. **Optimasi FX `PlayerCaster`** — Projectile/Flash/Descent/Zone masih GameObject
   satu-satu; ini hambatan berikutnya begitu VFX masuk
5. **Sistem audio**
6. **Refactor `GrimoireUI.cs`** (~2000 baris)

## Knob kalau ada yang meleset

| Gejala | Knob di `GameBalance.asset` |
|---|---|
| musuh kurang banyak | `SpawnRateBase`, `SpawnRatePerWave`, `SpawnRateGrowth` |
| wave kepanjangan/kependekan | `SpawnWindowBase`, `SpawnWindowPerWave` |
| ekor akhir wave masih lama | `ClosingSpeedMultiplier` |
| kerasa diseret, bukan dikepung | `FlankWidth` naikkan, atau `SeparationWeight` |
| musuh saling tembus | `EnemySeparation` |
| kepung terlalu mematikan | `EnemyContactDps` (menumpuk per musuh) |
| Spitter terlalu jauh/dekat | `PreferredRange` di `Assets/GameData/Enemies/Enemy_spitter.asset` |
| campuran jenis musuh | `Weight` / `WeightPerWave` / `FromWave` di tiap aset arketipe |
| papan kesempitan | `Grimoire.Width/Height` (const, hardcode 7) |

---

## Potongan kerja: skill non-serangan, kamera, drop nyata (2026-08-06, lanjutan)

Detail teknis lengkap ada di **[docs/AI-HANDOFF.md §13](../../docs/AI-HANDOFF.md)**.
Ringkasnya:

### Selesai & terverifikasi

- **9 `CastKind` baru** — `Orbit`, `Blink`, `Ward`, `Surge`, `Restore`,
  `SunStrike`, `RollingBall`, `Vortex`, `ForcePush`. Ditambahkan **di belakang**
  enum (menyisipkan di tengah akan menggeser tiap skill di bawahnya jadi kind
  yang salah, tanpa error).
  Implementasi: `Systems/PlayerCasterSignature.cs` (`partial class PlayerCaster`).
- **19 piece + 16 resep + 4 buff** lewat `Editor/SignaturePass.cs`.
  Konten sekarang **100 piece, 101 resep**, semua punya ikon.
- **Musuh bisa diangkat & dilontarkan** — `EnemyManager.Push()` (gaya negatif =
  seretan) dan `EnemyManager.Lift()`. Yang terangkat **benar-benar lumpuh**.
  Terukur: 14 musuh melayang di 2,60 unit, HP pemain tidak turun sama sekali.
- **Solver keseimbangan** — damage & mana dipecahkan dari throughput target per
  bintang, bukan diketik tangan. Sebaran dalam tier turun dari **×6 → ×1,5–2,4**;
  tangga antar tier jadi **×2,5–3,1** konsisten (sebelumnya ★3→★4 cuma ×1,5).
- **Bug nyata ketemu & diperbaiki:** biaya mana lahir dari `mana/detik × cooldown`,
  dan itu meledak di cooldown panjang — Maelstrom keluar di **180 sementara mana
  dasar cuma 120**, jadi ia tidak akan pernah bisa dinyalakan seumur hidup run.
  Gagalnya senyap: piece-nya terlihat sehat di papan dan tidak pernah menembak.
  Sekarang biaya dibatasi 75% mana dasar.
- **Drop jadi benda nyata** (`View/DropPickups.cs`) — dilempar dari posisi pemain,
  memantul, magnetnya menjemput dalam 7 unit, dan ada batas 6 detik yang
  menyerahkannya begitu saja supaya drop tidak pernah hilang.
- **Pemain tidak lagi ditarik balik ke tengah** saat lapangan sepi
  (`PlayerMotor`). Ini keluhan "kalo end gak harus balik ke tengah".

### Kamera dead-zone — diputuskan & diterapkan

`View/ArenaCamera.cs` dipasang di GameObject **"Camera Rig"** yang terpisah dari
`CameraShake`. Itu wajib: `CameraShake` menulis `localPosition` kamera dan
mengingat titik asalnya di `Awake`, jadi kalau keduanya menulis transform yang
sama, guncangan menarik kamera balik tiap frame dan pengikutan mati tanpa error.

Saat pertama dipasang jarak geraknya **nol** — arena 16×9 sementara layar
menutupi 19,6×11,9. User memilih membesarkan arena:

| | Sebelum | Sesudah |
|---|---|---|
| `ArenaHalfX / Z` | 16 / 9 | **22 / 17** |
| `SpawnBoundsX / Z` | 21 / 13 | **24 / 19** |
| `WaveOpenerCount / Distance` | 6 / 0,6 | **9 / 0,5** |
| Sisa gerak kamera | 0,0 / 0,0 | **2,4 / 5,1** |

Terukur setelah perubahan: rig bergeser sampai batas `(2,44 / 5,14)` lalu
berhenti; rombongan pembuka 9 musuh di jarak 9,6–12,9 (±7,6 detik untuk tiba);
pengepungan tetap **8/8 sektor**, terpadat 22%; 60 fps.

Kamera **tidak** kembali ke tengah setelah pemain masuk lagi ke zona mati — itu
memang cara kerja dead zone.

### Catatan urutan editor tool

`ComboPass` dan `SignaturePass` menulis damage mentah; `BalanceTunePass`
menghitungnya ulang. **Solver selalu dijalankan terakhir.**

### Jebakan baru yang memakan waktu di sesi ini

`System.Threading.Thread.Sleep` di dalam `execute_code` **membekukan main thread
Unity** — tidak ada frame yang jalan selama sampling, jadi semua pencacah selalu
nol dan kelihatan seperti fitur yang tidak berfungsi. Sampling harus lewat
panggilan `execute_code` terpisah, bukan loop dengan sleep.

---

## Tes build ★5 penuh (2026-08-06, lanjutan)

Detail lengkap: **[docs/AI-HANDOFF.md §14](../../docs/AI-HANDOFF.md)**.

Papan cuma muat **4 dari 6** skill ★5 (49 petak vs footprint 8–9) — rarity yang
memakan ruang papan bekerja sesuai desain.

**Bug ketemu:** anggaran mana di `BalanceTunePass` dihitung **per skill**, bukan
per papan. Empat skill ★5 butuh **96,2 mana/dtk** melawan regen **10** — build
terbaik di game menembak di **10% laju nominalnya** dan mati di wave 20, padahal
damage-nya cukup untuk menyapu.

Diisolasi dengan menjalankan wave 20 dua kali dan **hanya mengubah regen mana**:
mana normal → mati detik ~20 dengan 145 kills; mana tak terbatas → HP stabil 91
dengan 499 kills.

**Perbaikan (pilihan user: longgar, target ~70%):**
`TargetManaPerSecond` dibagi konstanta baru `SkillsOnAFullBoard = 5`, dan
`BaseManaRegen` 10 → **13**. Build ★5 yang sama sekarang butuh 20,9 mana/dtk
melawan 13 → **62% laju nominal**; mana masih turun sampai 8–10 saat ramai.

**Kurva endgame setelah perbaikan** (mana apa adanya, tidak dicurangi):

| Wave | HP musuh | Hasil |
|---|---|---|
| 20 | 299 | HP pemain **100 utuh**, 413 kills |
| 35 | 1 002 | HP 100, musuh ditahan 36–73 |
| 45 | 2 046 | **mati** di ~7 detik, 1 288 kills |

Dindingnya jatuh persis di tempat yang bisa dibaca dari angka: Solar Flare polos
1 528, jadi wave 45 adalah wave pertama yang **tidak bisa di-one-shot tanpa crit**.
60 fps di semua tes, termasuk 177 musuh hidup.

---

## Kamera roaming + DebugConfig + Playground (2026-08-06, lanjutan)

Detail lengkap: **[docs/AI-HANDOFF.md §15](../../docs/AI-HANDOFF.md)**.

### Kamera akhirnya benar-benar jalan

Akarnya bukan di kameranya. `SpawnPoint()` memakai `SpawnBoundsX/Z` sebagai
**koordinat dunia absolut**, yang diam-diam mengikat ukuran arena ke jarak tempuh
musuh — arena tidak pernah bisa dibesarkan tanpa merusak tempo, dan tanpa arena
besar kamera tidak punya ruang bergerak.

Sekarang kotak spawn berpusat di **rig kamera** (`SetSpawnAnchor`), bukan titik nol
dan bukan pemain.

| | Sebelum | Sesudah |
|---|---|---|
| Arena | 22 / 17 | **40 / 30** |
| Kotak spawn | 24 / 19 (dunia) | **24 / 15,5 (relatif rig)** |
| Jarak jalan kamera | 2,4 / 5,1 | **20,4 / 18,1** |

Kotak spawn **wajib lebih besar dari layar** (19,6 × 11,9), kalau tidak potongan
opener jatuh di dalam layar — sempat 11 dari 26 musuh menetas di depan mata.
Sesudah diperbaiki: **0 dari 9**.

### Bug spatial hash yang sudah lama tidur

`_cellHead` lahir berisi nol, dan nol adalah indeks musuh yang sah — jadi hash yang
belum dibangun mengaku tiap selnya berpenghuni musuh #0, lalu `_nextInCell`
sepanjang nol dibaca. `IndexOutOfRangeException` tiap frame **di dalam Update**:
500 musuh hidup, semuanya membeku di titik spawn, `AliveCount` terkunci di 1.

Tidak pernah meletus karena wave selalu dimulai dari UI beberapa frame setelah
Update pertama. `DebugConfig.StartAtWave` memanggilnya di `Awake` dan langsung
membongkarnya.

### DebugConfig

`Assets/GameData/DebugConfig.asset`, tersambung ke `_Bootstrap`. Gerbang induk
`Enabled` **default MATI**, dan `ProtoBootstrap` menyalakan warning selama hidup.
Isi: invuln, mana tak terbatas, tanpa cooldown, pengali damage/HP/jumlah musuh,
mulai dari wave berapa, bekukan spawn, timpa loadout, sembunyikan UI, time scale.

### Scene Playground

`Assets/Scenes/Playground.unity` — digenerate lewat **Tools/Grimoire/Build
Playground Scene**, terdaftar di Build Settings paling akhir dan **dimatikan**.
Daftar 74 skill, boneka diam dalam 4 formasi, damage terukur per sumber.
Terukur: Cataclysm pada 40 boneka = 522 dps + 329 dps BURN, 58 fps.

## Boss ular + biome (2026-08-06, lanjutan)

Detail lengkap: **[docs/AI-HANDOFF.md §16](../../docs/AI-HANDOFF.md)**.

### Boss ular — selesai

Kepala + badan + ekor, mengitari pemain, menerjang tiap 6 detik, dan **badannya
memendek seiring HP turun**. Panjang badan diturunkan dari `HpFraction`, jadi itu
BUKAN efek terpisah — ia satu-satunya bar HP yang dimiliki boss ini, dan mustahil
melenceng dari HP aslinya.

Tiap ruas didaftarkan sebagai `Enemy` biasa di pool. Itu keputusan terpenting:
boss dengan jalur damage sendiri berarti tiap skill harus diajari cara mengenainya,
dan selalu ada satu-dua yang terlupakan sampai ada build yang diam-diam tidak bisa
melukai boss sama sekali.

Tiga tempat wajib dirutekan ke kolam HP bersama, dua di antaranya gampang terlewat:
`Damage()`, **DoT di `TickEnemies`** (tanpa ini membakar boss malah MEMUTUS
badannya), dan reset `e.Boss = null` di `SpawnOne` (slot pool dipakai ulang —
tanpa reset, musuh biasa mewarisi kepemilikan ruas boss dan jadi tidak bisa dibunuh).

Terukur: wave 10, HP 9778, 24 ruas → 15 ruas saat HP 5040; orbit 13,8 (target 13);
59 fps. Wave 11 tidak memunculkan boss, wave 20 memunculkannya. Setelah mati: 0 ruas
nyasar. Aset lewat **Tools/Grimoire/Generate Boss**, muncul tiap 10 wave.

### Biome — selesai

Empat wajah arena bergantian tiap 5 wave: Ashen Flats → Frostbound Waste →
Emberfall → The Hollow. Masing-masing punya warna tanah/langit, sudut & warna
matahari, dan props sendiri.

Props lewat `EnemyRenderer` yang sama dengan swarm: **260 props = 3 draw call**.
Sengaja tanpa collider — yang dibeli titik acuan, bukan rintangan.

Terukur bersamaan: biome The Hollow + boss 24 ruas + 165 musuh biasa + 260 props =
**60 fps**. Aset lewat **Tools/Grimoire/Generate Biomes**.

### Dua jebakan editor baru

1. **`SerializedObject` array: resize → Apply → `Update()` → baru isi elemennya.**
   Satu tahap menyimpan ukurannya saja; referensi objeknya hilang tanpa error.
2. **Jangan `AssetDatabase.Refresh()` tepat sebelum `LoadAssetAtPath`.** Selama impor
   berjalan, LoadAssetAtPath mengembalikan **null** untuk aset yang ada di disk.

Keduanya gagal dengan cara yang sama jahatnya: sukses tanpa error, hasil kosong.

## Hutan, kamera, audio, bar boss, wave tanpa timer (2026-08-06, lanjutan)

Detail lengkap: **[docs/AI-HANDOFF.md §17](../../docs/AI-HANDOFF.md)**.

### Biome dipangkas jadi SATU hutan

Empat biome bergantian dibatalkan atas permintaan user. Sekarang cuma
**`Biome_forest.asset` — "Verdant Hollow"**. Mekanisme pergantian tetap ada tapi
tidak melakukan apa pun kalau biome-nya cuma satu.

Pohon = batang (silinder) + tajuk (bola) yang duduk di `height * 0,92`. Satu bentuk
saja tidak pernah terbaca sebagai pohon.

Versi pertama terlalu padat; dikoreksi user → **85 pohon** (dari 300) dan
**1 400 rumpun rumput tegak** (dari 380 semak bulat). Hutan rapat menutupi gerombolan
dan menghapus satu-satunya hal yang harus dibaca pemain: musuh datang dari arah mana.

`EnemyRenderer.Add` dapat varian skala `Vector3` — batang itu tinggi-kurus, tajuk
lebar-pipih, mustahil dari satu angka skala. Terukur: **11 draw call**, 59 fps.

### Zona mati kamera jadi knob

`GameBalance.CameraDeadZone` 0,5 → **0,22** (9,8×5,9 unit → **4,3×2,6**).
Terverifikasi kamera menyusul lalu berhenti begitu pemain kembali di dalam kotak.

### Audio — dari nol

Sebelumnya **tidak ada satu pun `AudioSource` di seluruh project**. Klipnya
**dibangkitkan**, bukan diimpor: 8 suara dari 3 generator (`Tone`, `Noise`, `Chime`),
16 voice bergilir, 2D, dengan jeda minimum per jenis suara supaya 20 kematian
bersamaan tidak menumpuk jadi satu bunyi memekakkan.

Ganti dengan file asli lewat array `Overrides` — tidak ada pemanggil yang berubah.

### Bar HP boss

Di atas layar, lebar penuh, nama + persen. `OnBossSpawned`/`OnBossDied` akhirnya punya
pelanggan: banner lewat `GrimoireUI.Announce()` dan raungan audio.
`PlayerCaster.OnHurt` ditambahkan, sengaja **tidak menyala untuk damage yang tertahan
perisai**.

### Timer akhir wave DICOPOT

Wave berakhir hanya kalau lapangan bersih. `ClosingTimeout`, `OnSweep`, dan
`_closingElapsed` dibuang. Terukur: wave 3 masih jalan di t=28,4 dengan 5 musuh sisa
(dulu sudah disapu di t≈25), beres di t=36,7 dengan `AliveCount 0`.

## Lapangan tak berujung (2026-08-06, lanjutan)

Detail lengkap: **[docs/AI-HANDOFF.md §18](../../docs/AI-HANDOFF.md)**.

Batas arena **dicopot sepenuhnya**. Hutannya dibangkitkan per petak 24 unit, radius
2 petak → selalu 25 petak hidup berapa pun jauhnya pemain berjalan.

Isi tiap petak diturunkan dari **hash koordinat petaknya sendiri**, jadi petak yang
sama selalu menghasilkan pohon yang sama persis. Terverifikasi: petak (27,−16) dibuang
lalu dimuat ulang → tiga batang pertama identik sampai desimal terakhir.

**Empat hal terpaku ke titik nol dan harus ikut dilepas**, dan yang pertama adalah
jebakan sesungguhnya karena gagal tanpa error:

1. **Spatial hash** — cuma 128×128 unit. Pemain di x=300 membuat seluruh swarm
   terjepit ke sel tepi: gaya pisah mati, `BestCluster` salah tunjuk, tanpa satu pun
   error. Punya **tiga** pembaca yang semuanya harus dirutekan ulang.
2. `PlayerMotor.Clamp()`
3. `ArenaCamera` `_limitX/_limitZ`
4. `EnemyRenderer.worldBounds` (400) — seluruh batch hilang dari layar begitu pemain
   lewat ~200 unit

`Random.state` wajib dipulihkan setelah membangkitkan petak, kalau tidak satu petak
baru menggeser seluruh keacakan game.

Terukur di (594; −443): lantai, hash origin, dan hutan semuanya ikut; 25 petak tetap;
pengepungan 8/8; 59 fps. Beban gabungan di (200; 150): 141 musuh + boss 24 ruas =
15 draw call props, 56 fps.

### Koreksi setelah dicoba user

**Kamera merayap sendiri = BUG.** `ArenaCamera` menghitung sasaran dari
`transform.position` yang sedang di-SmoothDamp menuju sasaran itu sendiri — umpan balik
yang tidak pernah mengendap. Diperbaiki dengan `_focus` sebagai field terpisah.
Terverifikasi mengendap tepat di (0,6978; 0,3899) dan identik di dua sampel terpisah.

**Pohon dikecilkan**: tinggi 4–8,5 → **2,2–4**, jumlah 85 → **55**. Pohon setinggi 8 unit
di kamera ortografis menutupi seperempat layar, dan yang tertutup selalu gerombolan.

**FPS drop — dua sebab, yang kedua jauh lebih besar:**
1. Rumput 1 400 → **260** (4 700 props → **1 117**)
2. **Matriks props disusun ulang tiap frame.** `View/PropBatch.cs` memanggangnya sekali
   dan hanya membangun ulang saat pemain menyeberang batas petak. Bayangan props juga
   dimatikan.

Terukur: wave 20, 158 musuh + boss 24 ruas + 1 117 props = **11 draw call, 59–61 fps**.

### Batas arena dipasang lagi (koreksi user)

Lapangan tak berujung **dibalik**. Alasan user lebih tajam dari analisis awal: seluruh
perilaku `PlayerMotor` adalah menjauh dari kerumunan, jadi tanpa dinding menjauh SELALU
berhasil — pemain jalan lurus selamanya, gerombolan mengekor tanpa menyusul, dan wave
tidak pernah selesai karena tidak ada yang mati.

Pelajarannya: **lapangan tak berbatas mematikan game yang gerak pemainnya otomatis dan
defensif.** Dinding itu bagian dari permainannya, bukan pembatas teknis.

Dikembalikan: `PlayerMotor.Clamp()`, `ArenaCamera._limit*` (dijepit pada `_focus`, bukan
posisi kamera), lantai bidang tetap. `InfiniteGround.cs` dihapus.

Tetap dipertahankan karena semuanya perbaikan sejati: hutan berbasis petak, `PropBatch`
(matriks dipanggang), spatial hash yang mengikuti pemain, `worldBounds` besar.

Hutan sekarang membingkai dinding — rimbun di luar arena, lapang di dalam.

Terukur: pemain dilempar ke (500; 400) → ditarik balik ke (27,4; 21,9); rig berhenti tepat
di batas. Wave 4 loadout default: 49 kill, beres di t=59,5 karena lapangan bersih, HP utuh,
1 661 props / 11 draw call / 59 fps.

## Tampilan, segel stat, kecepatan musuh, boss kelabang (2026-08-06, lanjutan)

Detail lengkap: **[docs/AI-HANDOFF.md §19](../../docs/AI-HANDOFF.md)**.

### Tampilan — dua kali salah sebelum benar

Target Cult of the Lamb: **lapangan terang, bingkai gelap**. Percobaan 1 terang merata,
percobaan 2 gelap merata; dua-duanya salah membaca DI MANA gelapnya berada. Yang
mengerjakan pembingkaian adalah **kabut** (21→44, dihitung dari geometri kamera), bukan
warna tanah.

**Tiga bug tampilan ketemu:**
1. `RenderSettings.ambientLight` **diabaikan** di mode Trilight — seluruh pengaturan
   ambient biome tidak pernah berpengaruh.
2. Props & musuh **mengkilap seperti plastik**: material instanced tidak pernah lewat
   `ApplySurface`, jadi memakai smoothness bawaan URP 0,5.
3. Bidang satu warna **tidak akan pernah** terbaca sebagai rumput — yang hilang variasi
   rapatnya. Tekstur rumput sekarang dibangkitkan dari tiga skala derau.

Ditambah `View/Atmosphere.cs` (bayangan awan + berkas cahaya, UV dikunci ke koordinat
dunia) dan `View/ArenaLights.cs` (lampu titik lembut yang mengembara).

### Musuh tidak pernah bisa menyentuh pemain — aritmetika

Pemain **3,2** dan kabur otomatis; Grunt **1,6**; Stalker baru wave 5. Wave 1–4 mustahil
menyentuh pemain. Buktinya sudah ada di log sesi sebelumnya dan terlewat.

Diperbaiki tiga sisi: musuh **2,0–2,4**, pemain **2,8**, `DangerRadius` **3,5**.
Stalker kini **3,7 — lebih cepat dari pemain**. Damage musuh juga **menskala per wave**.

### Segel stat

Tidak ada satu pun segel yang menaikkan damage (hanya rune), dan segel berhenti di ★2.
Sekarang empat sumbu sampai ★3: SERANG / NYAWA / MANA / TAHAN. **107 piece, 111 resep.**

### Boss jamak + kelabang + anak buah

`Boss` tunggal → daftar. Wave 20 = 2 ekor, wave 40 = 3. Bertambahnya JUMLAH, bukan HP.

**Kelabang penyelam** nyaris tanpa kode baru: jejak kepala menyimpan ketinggian, jadi
badannya mengikuti busur sendiri. Ritmenya lumba-lumba — 3 lompatan beruntun (busur 1,1
dtk) lalu menghilang 5,5 dtk. Terukur profil badan
`2.8 5.0 2.9 | -0.3 | 2.8 5.0 2.7 | -6.0` — dua gundukan bersamaan, ekor terkubur.

Terbenam = **kebal & tak terlihat**, dicek di satu tempat supaya tidak ada skill yang lupa.

**Coilspawn** = kelabang yang sama, angka jauh lebih kecil, flag `Minion` — ikut wave
biasa dari wave 6, tanpa pengumuman dan tanpa bar HP.

### Jebakan pengukuran yang terulang

`execute_code` menahan main thread Unity. Membaca `Time.smoothDeltaTime` di dalam
rentetan panggilan menghasilkan angka jauh lebih buruk dari kenyataan — sempat
melaporkan "20 fps" dan "171 ms di lapangan kosong", yang mustahil. Ukur fps di
panggilan yang tidak melakukan apa pun selain membacanya.

## Berikutnya (belum dikerjakan)

- **Main dan nilai rasanya** — semua verifikasi masih programatik
- Animasi baked (VAT) untuk swarm dan boss
- Optimasi FX `PlayerCaster` (Projectile/Flash/Descent/Zone masih GameObject satu-satu)
- Save/persistensi run
- Navigasi keyboard/gamepad di menu
- Teks UI in-game masih Indonesia sementara konten sudah Inggris
- **Nol tes** — `coding-standards.md` menyebutnya BLOCKING untuk story logic
- `design/gdd/content-plan.md` usang (target 23 skill, isi 100 piece)

## Ambience & undian per-wave, drop berbintang, evo emas dikunci (2026-08-07)

Umpan balik pemilik project disikat satu-satu. Semua terverifikasi programatik di play mode.

### Siang/malam & cuaca — akar masalahnya dua bug diam

1. **Aset malam adalah salinan mentah siang.** `BiomePass.BuildNight` memanggil
   `CopySerialized(day, night)` LALU mengecek "daftar VFX masih kosong?" — sudah terisi
   salinan siang, jadi guard tidak pernah menyala: kunang-kunang & embers malam tidak pernah
   terpasang, kupu-kupu siang terbang di malam. Fix: daftar malam diselamatkan sebelum
   CopySerialized, dikembalikan sesudahnya.
2. **`new System.Random(wave * K + C)` menghasilkan undian MENGEBLOK.** Seed berjajar rapat
   -> sample pertama berjajar rapat (konstruktornya cuma mengaduk linear). Terukur: undian
   malam 50% menghasilkan wave 1-29 malam SEMUA. Pengganti: `Model/WaveHash.cs` (hash
   avalanche), dipakai cuaca DAN biome dengan salt beda. Sesudahnya: malam 22/40, blok
   terpanjang 9 dari 200 wave.

Sebaran sekarang (permintaan user): **cerah 60% / berangin 20% / basah 20%**, siang/malam
**50:50 per wave** (knob `GameBalance.NightChance`), diundi deterministik per nomor wave.
Mendung dipertebal: `OvercastSun` 0,85 -> 0,72, `OvercastCloud` -> 0,8, Hujan 0,6 / Badai 0,85.

### VFX suasana — siapa muncul kapan

- **`AmbientVfxEntry.OnlyClear`** (flag baru): kupu-kupu, debu cahaya, berkas matahari cuma
  muncul saat CERAH (bukan sekadar tidak hujan). "Cerah" DITURUNKAN (tidak basah && tanpa
  efek langit), bukan flag di mood — flag terpisah pasti lupa dicentang suatu hari.
- **Godray**: dulu `Light/Sunlight` nyangkut di paket HUJAN — satu titik debu nempel pusat
  kamera ("godray kecil ngumpul di tengah"). Sekarang: Sunlight = debu ikut-kamera satu
  lapis (CoverageOnly, skala 0,75) + `TSI_Sun_Shaft_01A` (ToonScapes, berkas beneran) 2
  kantong dunia ~18 unit. Siang cerah saja.
- **Burung lahir DI LUAR layar, dipaksa kode**: `Weather` menghitung setengah diagonal layar
  dari kameranya (+7 margin) dan memakai `max(MinDistance, offscreen)` — angka aset tidak
  bisa lagi menaruh burung di dalam pandangan. Burung kedua (`Birds_spin`) ditambah.
- **Malam**: kunang-kunang (`Butterflies_*_fog`) 4+3 kantong + `Embers_calm` 3 kantong.
- **Leak dibereskan**: efek ikut-kamera dulu ditanam langsung di transform Weather dan tidak
  pernah dibersihkan saat wajah berganti — tiap pergantian siang/malam menumpuk satu set
  daun. Sekarang di simpul `Ikut Kamera` yang ikut di-Clear, plus daftar `_carriedFx` supaya
  debu ikut aturan cerah (dulu kantong doang yang dicek).

### FPS "30-60"

Akarnya bukan beban render: **`GameSettings.FrameCap` bawaan = 60** (dan pref lama menyimpan
60). Cap 60 + frame meleset = kejeblos ke 30-an. Bawaan jadi TANPA BATAS, pref `opt.framecap`
lama dihapus sekali. Terukur editor: 71 musuh + hujan = ~15 ms (67 fps) unlocked; HAZE cuma
0,5 ms. Menu options tetap bisa nge-cap.

### Drop berbintang

`ContentDatabase.RandomDrop` sekarang mengundi bintang dari `GameBalance.DropStarWeights`
(82/12/4,5/1,5/0). **Bintang 5 nol = aturan desain: hanya lahir dari resep.** Pool per
bintang; bintang kosong turun setingkat, bukan gagal senyap. Terukur 4000 undian:
82,6/11,7/4,3/1,5/0,0%.

### Evo emas dikunci (bug "kuning kok jadi biru")

Akar: di `Grimoire.FindPendingGroups`, grup GHOST (piece di tangan) dicari SEBELUM grup
yang sudah duduk — mengangkat kembaran bahan membajak anggota pasangan emas, garis pindah
ke kursor dan jadi biru. Fix: **grup emas (lengkap + hasil muat duduk) dikunci paling dulu,
ghost cuma boleh pakai sisa; hanya lock yang membubarkan grup emas.** Diulang per resep
sampai kering (dua pasang = dua janji). Diverifikasi: pasangan emas tetap emas saat kembaran
dipegang, garis bidik ghost tetap muncul untuk piece bebas. Tas tidak kena (Preview-nya
tanpa ghost).

### Spawn player acak

`ProtoBootstrap.BuildPlayer`: titik lahir diacak dalam arena (margin 9 dari dinding),
System.Random (bukan stream gameplay). Rig kamera ikut pindah SEBELUM ArenaCamera dipasang
(fokus direkam di Awake — tanpa ini run dibuka panning panjang dari titik nol).

### Peta run ala STS — SELESAI & terverifikasi (2026-08-07, lanjutan)

Detail teknis: **docs/AI-HANDOFF.md §21**. Ringkas:

- **`Model/RunMap.cs`** — graf act 15 lantai × 3 lajur, boss di puncak; teruji 200 peta,
  0 node yatim / 0 buntu. **`Systems/RunDirector.cs`** — portal FISIK menetas setelah wave
  bersih, diklik -> karakter JALAN sendiri ke portal -> isi node dieksekusi.
- Node: Fight / **Elite** (x2,2 HP x1,25 jumlah x1,3 gigit, ATAU 40% mini-boss yang ikut
  act) / **Boss puncak** (ular DAN kelabang sekaligus, x2,5 HP, aggro 1,6, pengawal
  ditipiskan) / Toko / Kejadian / Slot. Semua angka di `GameBalance` (header Peta Run).
- **Pulau rehat** = kantong di (50, 42) — scene sama, hutan hash yang sama tiap singgah;
  PEDAGANG/BANDAR/PERTAPA + api unggun + portal LANJUT. Scene terpisah menunggu
  persistensi run.
- **Peta diintip** lewat M / tombol PETA: read-only, node berikut berdenyut, jejak emas,
  `@` = posisi sekarang. Memilih tetap lewat portal di lapangan.
- Toko pindah ke node peta (stok dikocok per singgah); tombol MULAI WAVE pensiun; boss
  kelipatan-10 lama otomatis kalah oleh boss pesanan node.
- Terverifikasi play mode: klik->jalan->wave->bersih->portal lagi; pulau bolak-balik utuh;
  elite x1,25 pas; boss node HP persis rumus; act berikutnya regenerasi peta.

### Putaran feedback peta & suasana (2026-08-07, lanjutan) — SELESAI

Diverifikasi pakai SCREENSHOT (pertama kalinya sesi AI menilai tampilan pakai mata).
Detail: AI-HANDOFF.md §22.

- **Portal = pindah tempat**: `Relocate()` acak posisi tiap node tempur +
  `Weather.Rescatter()` tiap wave — kupu-kupu tidak menunggu di titik lama lagi.
- **Peta ditulis ulang** meniru `project_b RoguelikeMapUI`: kiri->kanan, bezier
  putus-putus ber-seed, node jitter + ring status, boss besar, latar solid.
- **Malam**: kupu-kupu fog dicopot; kunang-kunang BANGKITAN (`Fireflies.prefab`,
  kedip via gradasi alpha) + bara. Yang menyala malam cuma kunang-kunang.
- **God ray DIBANGUN SENDIRI** (`GodRay.prefab`, bangkitan pass): mesh shaft paket
  memipih jadi genangan di kamera 68 derajat — stretch pun tak menolong. Tiga pita
  menghadap kamera, 38-46 unit (layar 22), gradien nol di 85% badan: masuk dari luar
  layar, sumber tak pernah terlihat, semi. Siang emas, malam biru bulan. `CullSheets`
  mematikan lembaran beam UFO di prefab debu Sunlight/Moonlight — akar asli
  "godray ngumpul di tengah".

### Berikutnya

- **Main dan nilai rasanya** — alur portal + pulau belum pernah dinilai tangan manusia
- VFX slot "dopamin ala Vampire Survivors" (panel masih teks) + cerita penjaga pulau
- Skill 3 + fragment 3 jalur (merah/biru/kuning) + skill tree bertingkat — belum disentuh
- Event baru satu jenis; tambah variasi + formula hadiah slot yang lebih kaya
