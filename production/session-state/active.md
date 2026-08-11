# Session State

<!-- STATUS -->
Epic: Grimoire Haven — arah bullet-haven
Feature: VFX per-skill bisa ditukar tangan + avatar LizMage hidup
Task: 74 wrapper VFX, ring AOE baru, player beranimasi+cloth; menunggu penilaian mata
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

### SELESAI (commit e6ec21d..e748be8): gelombang perbaikan live-QA

Semua yang tadi antre sudah terkompilasi, terverifikasi, dan masuk: fix drag belanjaan
toko; Build Settings merge (ruangan tidak tersapu lagi - TERBUKTI termuat 4 scene + latar
toko tampil menggantikan arena); papan starter terpusat (3 kartu lolos cek bersentuhan);
Necromancer bob napas; ShopRig + ShopPanel.prefab (panel/slot/reroll/LANJUT ditata tangan,
LANJUT default pojok kanan bawah); ruangan dipindah ke kavling 3000 supaya tidak menumpuk
dengan arena.

### ARSIP investigasi (menunggu kompilasi - sudah tidak berlaku)

Empat edit sudah di disk, belum terkompilasi/terverifikasi/ter-commit:

1. **Fix drag belanjaan toko** (`GrimoireUI.HandlePanelClick`): klik ke piece tercecer
   sekarang LOLOS dari guard panel toko. Akarnya: belanjaan dilempar DI DALAM PanelRect,
   dan guard menelan semua klik di area itu — belanjaan tergeletak tak bisa diambil.
2. **MainMenuBuilder.RegisterBuildSettings jadi MERGE, bukan timpa.** Versi lama mengganti
   seluruh daftar dengan {menu, game} — tiap rebuild menu MENYAPU tiga scene ruangan, dan
   itulah kenapa "masuk toko gak pindah scene": Room_* hilang dari Build Settings, RoomLoader
   menolak diam-diam. Scene sudah kudaftarkan ulang manual; snapshot play lama tetap tanpa
   ruangan — baru termuat di sesi play BERIKUTNYA.
3. **Tata letak papan starter dipusatkan** (`HeroPass`): Frostwarden dua alas cermin
   kiri-kanan mengapit tengah; Stormcaller garis atas + kotak bawah-tengah (tetap
   berjauhan — identitasnya — tapi tidak lari ke pojok). SETELAH kompilasi jalankan
   `Tools/Grimoire/Generate Heroes` lalu cek carousel.
4. **Necromancer dapat bob napas** (`EnemyRenderer`): VAT satu-pose (TotalRows <= 1) tidak
   lagi mematikan bob — paket Feyloom TERBUKTI tanpa satu pun AnimationClip (folder nol
   klip, VAT 1 baris), jadi patung diam adalah satu-satunya alternatifnya.

### INVESTIGASI BERJALAN

- **Evo dicuri resep lain + lock tidak menolong.** Resolver & preview SAMA-SAMA
  menghormati Locked (RecipeResolver 117, Grimoire 550/767/850) dan tidak ada yang
  menghapus Locked. Dua mekanisme yang bisa menjelaskan: (a) urutan db.Recipes — resep
  lain yang lebih dulu MENCURI bahan bersama; (b) CouldSeat gagal karena papan makin
  penuh. ATURAN DARI PEMILIK: "kalo gw lock (penyusupnya) harusnya balik ke yang bisa
  evo". BUTUH dari user: nama 3 piece yang terlibat saat kejadian.
- **Teks reaction "tidak pernah muncul".** Sistem TERBUKTI hidup: injeksi burn+chill 10
  poin memicu reaksi, musuh mati oleh burst, dua floater sempat hidup. Kemungkinan akar:
  (a) build user saat itu tidak punya pasangan elemen (Frost Shard + Minor Heal saja);
  (b) EnemyHpBase 15 membuat musuh awal mati SEBELUM ailment menumpuk ke ambang — efek
  samping balancing, kandidat fix: turunkan ambang reaksi / naikkan poin ailment per hit.
- **Asap mata "hilang".** Data bilang hidup: EyeSmoke aktif, alpha 1, GloomEdge terpasang,
  UiGloomRect enabled. Shader edit _PaperScroll TIDAK menyentuh jalur non-paper. Belum
  terlihat mata — butuh tangkapan layar area buku saat run berjalan.

### DIMINTA USER, BELUM DIKERJAKAN — UI toko dirombak

- Panel toko jadi PREFAB yang ditata tangan (pola GridArea/VitalsRig/StatusStripRig):
  kotak per slot, reroll, judul — "buatin prefabsnya sekalian", layout sekarang berantakan.
- Tombol LANJUT dipindah ke pojok kanan bawah (hati-hati: panel spell menempati area itu).
- VFX evolusi (sekarang "muter doang") — minta efek; kandidat: prefab SSU/Lana + Flame SSU.

### DIMINTA USER, BELUM DIKERJAKAN — perombakan hover

Permintaan tepatnya (2026-08-09): "rapihkan text hover, terlalu berantakan, apalagi hover
skill banyak info yang gak butuh" + "hover evo dibuat FULL GAMBAR: [] = [] + [] + [] —
kalau satu item bisa jadi 5 item baru, ada 5 baris resep ke bawah; bahan yang SUDAH
dimiliki dikasih CEKLIS di bawah gambarnya dan gambarnya TERANG, yang belum punya GELAP;
belum ada art, pakai placeholder dulu."

File yang terlibat: `View/TooltipBuilder.cs` (kartu stat — pangkas), `View/RecipePanel.cs`
(kartu ALT — dirombak jadi baris ikon), `GrimoireUI.UpdateTooltip` (routing). Ikon piece
sudah ada (placeholder pip/kotak warna) — pakai `PieceDefinition.Icon`. "Punya" = hitung
dari papan + tas + tercecer (`OwnedCount` sudah ada, dipakai RecipePanel sekarang).

**Lanjutan permintaan pemilik project (2026-08-10):**

- Hasil yang kepental mendarat **di sebelah bahannya** (terukur: bahan di petak x=3,4,5 y=4 →
  spill di `(4,4)`), bukan di titik tetap. Jalur "masuk tas dulu" dicabut — maunya memang
  kepental keluar.
- **Hadiah wave berhenti nyebar**: `DropPickups.Toss` tidak lagi mengundi arah & jarak.
  Sudut emas × urutan, kecepatan `1,5 + 0,14×urutan`. Lima hadiah mendarat 0,90–1,24 unit dari
  pemain (dulu 1,07–1,93 acak), jarak antar-hadiah terdekat 0,94 unit — rapat, tidak menumpuk.
- **Bug: barang tercecer tidak terjual kalau node berikutnya bukan wave.** `SellLoose()` cuma
  dipanggil `StartNextWave()`, sementara node toko/kejadian/slot berangkat lewat `Depart()`.
  Sekarang kedua jalur LANJUT memanggil `DepartRun()` = `StashHeld` + `SellLoose` + `Depart`.

**HUD dipangkas lagi + layar GAME OVER (2026-08-10):**

- **Kotak JUAL dan tombol PETA dibuang.** JUAL jadi mubazir sejak LANJUT menyapu lantai;
  PETA sudah punya kembarannya di tombol M. `SellRect`/`MapButtonRect`/`SellHeld`/`DrawSellBox`
  ikut hilang, `SellY` berganti nama jadi `ButtonRowY`, dan TOKO (satu-satunya yang tersisa di
  kolom itu) naik menempati barisnya.
- **Layar GAME OVER.** Dulu kematian cuma mengganti teks banner ("SPACE buat ulang - ESC buat
  balik ke menu") sementara arena tetap hidup di belakangnya. Sekarang: kerudung gelap penuh
  layar, judul, baris "run berakhir di wave N - X koin", tombol **KE MENU UTAMA** dan **ULANG
  RUN**. Peta/toko/kejadian/slot yang masih terbuka ikut ditutup — peta pemilih yang tertinggal
  menyala menuntut jawaban untuk perjalanan yang sudah tidak ada. Diverifikasi lewat screenshot
  (`Assets/Screenshots/game_over3.png`).

**Temuan menunggu keputusan — resep yang menghasilkan RUNE:**

**10 dari 121 resep** hasilnya rune, semua bahannya segel. Pemilik project menyatakan resep
seharusnya cuma untuk skill & item. Konsekuensi kalau dicabut (terukur, 8000 undian drop
@wave20): rune ★2/★3/★4 tetap bisa jatuh sendiri (244/83/35 dari 8000), tapi **`Keystone Rune`
★5 = 0** — ia satu-satunya rune ★5 dan aturannya "★5 hanya lahir dari resep". Jadi mencabut
kesepuluhnya menghapus Keystone dari permainan kecuali diberi jalur lain.

Daftar: Current, Bastion, Siphon, Whetstone (★2) · Storm, Nadir (★3) · Beacon, Echo, River (★4)
· Keystone (★5).

**Belum dikerjakan (diminta 2026-08-10, belum tersentuh):**

- **Pemain mati belum terbakar** seperti musuh. Musuh memakai jalur VAT instanced
  (`_VatClip.w` + `BurnNoise`); pemain cuma kapsul `CreatePrimitive` bermaterial bawaan, jadi
  butuh shader dissolve tersendiri — bukan sekadar menyalin materialnya.
- **Damage popup "big number"** — angka damage dibuat besar supaya terasa jedar-jeder. Cara
  paling aman: faktor TAMPILAN di `GameBalance` (mis. ×100) yang dipakai popup, panel spell,
  dan tooltip sekaligus, sehingga balance internal tidak tersentuh sama sekali.

**Angka besar & pemain terbakar (2026-08-10):**

- **`Data/BigNumber.cs`** — pengali TAMPILAN (`Scale = 100`), bukan knob keseimbangan.
  Simulasi tetap berjalan di angka aslinya; yang dikalikan cuma yang sampai ke mata, jadi
  tidak satu pun konstanta `GameBalance` / aset piece / kurva musuh perlu disentuh. Dipakai
  popup damage, panel spell, dan kartu hover. Format Indonesia eksplisit (titik ribuan, koma
  desimal) — bukan culture mesin, supaya build di locale lain tidak memformat berbeda.
  Terukur: `34 -> 3.400`, `128,4 -> 12.840` (panel: `12,8rb`), `5400 -> 540.000` (`540rb`).
- **Popup lebih "jedar"**: rentang ukuran font 16–34 → **20–54** (dengan angka empat digit,
  ukuran jadi satu-satunya pembeda gigitan kecil dari pukulan mematikan), sentakan lahir
  0,45 → **0,8** dan mengempis lebih lambat (5 → 4).
- **Pemain ikut hangus saat mati** — `Shaders/BurnAway.shader` + `View/PlayerBurnout.cs`.
  Musuh terbakar lewat jalur VAT instanced yang terikat animasi terpanggang; pemain cuma
  kapsul ber-MeshRenderer, jadi ia dapat shader dissolve sendiri. Noise DIHITUNG (value-noise
  tiga oktaf), tanpa aset tekstur baru. Menjalar dari BAWAH ke atas — benda yang hilang dari
  ubun-ubun terbaca sebagai tenggelam, bukan terbakar. Verifikasi: shader `isSupported=True`,
  material terpasang di renderer pemain, `_Burn` beranimasi 0→1 lalu renderer dimatikan.
  **Belum dinilai dengan mata** — kamera arena menimpa posisi tiap frame sehingga sulit
  dipotret dari luar; pemilik project akan melihatnya saat mati sungguhan.

**Resep tidak lagi menghasilkan rune (2026-08-10, keputusan pemilik project):**

Aturannya sekarang: **evolusi untuk item & skill; rune DIDAPAT (drop) dan DIBELI (toko).**

- **10 resep dicabut** dari `ContentDatabase._recipes` (121 -> 111): `Recipe_runearus_a`,
  `runebenteng_a`, `runesiphon_a`, `runeasah_a`, `runebadai_a`, `runenadir_a`, `runemercu_a`,
  `runegema_a`, `runesungai_a`, `runeinti_a`. **File `.asset`-nya masih di disk** (yatim, tidak
  terdaftar, tidak pernah diperiksa resolver) — sengaja tidak dihapus supaya bisa dibalik;
  bilang kalau mau dihapus permanen.
- **Rune ★5 dibuka jalannya.** Tanpa resep, `Keystone Rune` mustahil didapat. `DropStarWeights[4]`
  0 -> **0,35** dan `DropStarMinWave[4]` 99 -> **14**. Supaya aturan lama "skill ★5 hanya lahir
  dari resep" tetap utuh, `ContentDatabase.RandomOfStar` memaksa bintang 5 mengambil dari pool
  RUNE saja.
- Terukur, 40.000 undian @wave20: rune ★1=8.096 ★2=1.227 ★3=438 ★4=147 **★5=141**
  (Keystone), **skill ★5 = 0**. Di wave 10: bintang 5 = **0** (belum buka).
- Toko (20.000 lemparan): rune ★1=1.944, ★2=583, ★3+ = 0 — `CanDrop` adalah properti
  `Stars <= 1`, jadi toko memang lapak barang dasar; rune langka datang dari lantai.

### Berikutnya

- VFX slot "dopamin ala Vampire Survivors" (panel masih teks) + cerita penjaga pulau
- Skill 3 + fragment 3 jalur (merah/biru/kuning) + skill tree bertingkat — belum disentuh
- Event baru satu jenis; tambah variasi + formula hadiah slot yang lebih kaya

## Portal DIHAPUS: peta pemilih fullscreen + transisi Gloom + pulau Suaka (2026-08-09)

Detail teknis: **docs/AI-HANDOFF.md §24**. Tiga permintaan user, semuanya masuk:

- **Portal fisik pensiun.** Wave bersih → susun grimoire → tombol **LANJUT (SPACE)**
  → Gloom (VFX kegelapan buatan sendiri) MERAPAT sampai menelan layar — nilai standar
  materialnya TIDAK disentuh, transisi cuma meminjam lewat MaterialPropertyBlock —
  → peta terbuka, posisi dilingkari → klik node → penanda emas BERJALAN di jalur →
  tirai hitam → node dieksekusi DALAM GELAP (teleport + ganti wajah tak terlihat) →
  Gloom membuka kembali ke nilai standar di tempat baru.
- **Feedback putaran 1** ("kurang gelap; peta harus nutup semua layar"): tirai hitam
  UI menumpang 45% terakhir fade — ujungnya HITAM TOTAL, HUD pun tertelan; peta jadi
  SATU LAYAR PENUH.
- **Feedback putaran 2**: peta ditegakkan **ala STS bawah→atas**, jarak antar lantai
  TETAP 110 px (tidak dipadatkan), **scroll roda mouse** buat ngintip ke atas +
  auto-scroll menjemput posisi & penanda.
- **Pulau rehat = tempat lain betulan**: wajah arena ditukar ke `Biome_sanctum.asset`
  (Suaka — ungu berkabut, siluet, kunang-kunang saja) selama singgah; node tempur
  berikutnya memulihkan wajah lewat undian biasa. Aset ini MILIK USER — tidak ada
  pass yang meregenerasinya.
- M / tombol PETA tetap intip read-only. `MapFadeClose/Open`, `MapMarkerTravel` di
  GameBalance.
- Diverifikasi: programatik + screenshot + **user main sendiri beberapa wave di sesi
  ini** (wave 4, 130 kill, muka senja, lancar).

### Feedback putaran 3 (SS user vs peta referensi) — SELESAI

- **Scroll "mati" = BUG beneran**: Input System baru memberi scroll ±1/gerigi,
  kode menebak ±120 → gerakan <1 px. `ProtoInput.ScrollY` kini ternormalisasi ke
  gerigi; 1 gerigi = 90 px. Plus **drag-pan** (`LeftHeld` baru): klik di luar node
  = pegangan, peta nempel di kursor — di mode memilih maupun intip.
- **Karakter pemain tampil di peta**: token kuning (warna kapsul pemain) + KAMU,
  berdiri di ruang tunggu bawah sebelum langkah pertama, berjalan saat memilih.
- **"Terlalu rapi berjejer"**: geser acak per lantai ±40 px + jitter node ±28 px
  (ber-seed) — garis selalu menyerong; `MapLanes` 3→4 (aset) + generator 2–4
  node/lantai — penuh pilihan ala referensi. Verifikasi screenshot: cocok.

### Feedback putaran 4 (coretan user) — SELESAI

- **Pemain = simpul pertama peta**: jalur EMAS dari token KAMU (bulat, sprite
  buatan 32 px) ke SEMUA node lantai pertama; travel pembuka menyusuri jalur itu.
- **Pilihan awal 3–5 node** (bukan "3 arah"), `MapLanes` 4→5, lantai lain 2–4.
- **Boss dikunci mati di tengah** — tanpa geser/jitter di lantai puncak.
- **Jalur dikalemkan** — satu arah lengkung tipis per ruas (0.06, dulu 0.3 dua
  arah), nyaris lurus ala STS rujukan.
- **BUG spawn "gak di tengah kamera"**: titik lahir/Relocate melebihi jepitan
  kamera → rig tertahan, pemain menepi. Kini dijepit `ArenaCamera.LimitX/LimitZ`.
- Verifikasi screenshot: garis emas + 4 pilihan ✔, boss center di puncak ✔.

### Feedback putaran 5 — SELESAI

- Jarak ruang tunggu → lantai pertama dijauhkan (240 px) — langkah pembuka
  terbaca perjalanan, bukan lompatan.
- **MapFloorsPerAct 15 → 20** (aset): jalur act ≈ **14–15 wave** tanpa
  toko/event/slot (dulu ≈ 11). Alasan user: building grimoire butuh waktu.

### Feedback putaran 6 — SELESAI

Peta **melebar**: pita node bukan lagi 320 px tetap melainkan `lebar layar × 0,34`
(cap 700) — dulu seluruh act menggumpal di tengah, dua pertiga monitor kosong.
Geser-lantai & jitter ikut diukur dari jarak antar lajur, jadi kemiringannya tidak
hilang; hasil akhir dijepit tepi panel supaya lajur terluar tidak terpotong.

### Feedback putaran 7 — SELESAI

- **Peta**: pita dipersempit (`lebar × 0,26`), bawah dikasih napas (310 px),
  jarak antar lantai **110 → 170** ("bantet"), satu gerigi roda = satu lantai.
- **Wave per act 12 → 27,7** (`MapFloorsPerAct` 34, rehat diturunkan). Terukur
  400 simulasi: min 21, maks 34, rehat 6,3.

### Aset — folder & VFX (2026-08-09)

Detail: **docs/ASSET-LIST.md** (daftar + spek + prioritas) dan **AI-HANDOFF §25**.

- `Assets/Art/{UI,Icons,Map,VFX,Characters,Props}` + `Assets/Audio/*` dibuat,
  tiap daun ber-`.gitkeep`.
- **23 prefab VFX diadopsi** keluar dari paket Lana ke
  `Art/VFX/Prefabs/{Light,Ambient,Weather,Skill}`. Alasan utama: Sunlight/Moonlight
  setelan tangan bisa ketimpa kalau paketnya di-reimport.
- **Slot VFX skill baru**: `PieceDefinition.ZoneVfx` + `ZoneVfxScale`.
  Terpasang di 4 skill (Storm Cell←Tornado, Snowstorm, Ion Storm, Ashfall).
  Terverifikasi play mode.
- **Jebakan ketemu**: `WeatherMood.Effects` mengabaikan `RepeatEvery` — efek cuaca
  selalu permanen. Tornado sempat salah pasang di sana, sudah dicopot.

## VFX skill nyantol di piece yang SALAH — dibenerin (2026-08-09)

Rename `ZoneVfx` -> `CastVfx` (biar slotnya kepakai `Kind = Vortex`, bukan cuma `Zone`)
selesai di kode tapi **aset belum ikut** — 4 `.asset` masih nulis `ZoneVfx:`, dan Unity
DIAM saja saat nama tak dikenal. Ion Storm & Ashfall tinggal selangkah dari kosong permanen.

- `[FormerlySerializedAs("ZoneVfx")]` dipasang sebagai jaring; semua aset di disk sekarang
  sudah `CastVfx:`. **Catatan penting:** domain reload TIDAK membaca ulang YAML, jadi aset
  yang terlanjur dimuat dengan script baru tetap null sampai di-set ulang lewat kode.
- Prefab tornado ternyata dipasang di **Storm Cell (★1)** & **Snowstorm (★2)** — dua-duanya
  `Kind = Zone`, kubangan DIAM. Dipindah ke skill Vortex asli:
  **Tornado_snow -> Maelstrom ★5**, **Tornado_sand -> Tornado ★4**. Whirlwind ★3 masih kosong.
- Perilaku "jalan-jalan + kena musuh terbang" sudah ada di kode dari awal
  (`ZoneDrift` + `Lift(radius x 0,7)`, naik dari 0,45) — yang salah cuma pemasangannya.

## Art UI masuk: perkamen peta, gloom tepi, papan grimoire (2026-08-09)

- **`UiTheme` SO baru** (`Assets/GameData/UiTheme.asset`) — kertas, bingkai, warna tinta,
  knob gloom. Boleh null: tanpa tema, UI balik jadi kotak warna datar. Dipasang lewat
  `ProtoBootstrap._uiTheme`.
- **Shader baru `Grimoire/GloomEdge`** (`Assets/Shaders/GloomEdge.shader`) — saudara kanvas
  dari `Grimoire/Gloom`. Shader lama TIDAK bisa dipakai di UI (`positionWS` +
  `UniversalForward`). Yang dipinjam cuma keputusannya: **derau menggoyang GARIS BATAS,
  bukan kepekatan**. Jarak dihitung dalam PIKSEL (`_RectSize` dari C#) supaya peta besar &
  peta kecil terlihat sebahan.
  - `_PaperMode = 1` -> gambar spritenya sendiri + **SOBEK** tepinya.
    **Putaran 1 salah**: dibuat memudar halus (gradien puluhan piksel). User menolak —
    yang diminta **compang-camping**. Bedanya bukan seberapa jauh alpha turun melainkan
    seberapa CEPAT: gradien lebar selalu terbaca sebagai kabut di depan kertas dan bentuk
    perseginya masih kelihatan; potongan yang hampir tegak memindahkan seluruh
    ketakberaturan ke GARIS potongnya, dan garis berkelok itu yang dibaca sebagai sobek.
    Knob: `TearDepth/TearScale/TearFray/TearSoft` di UiTheme.
  - **Lapisan gloom WAJIB memakai potongan sobek yang sama.** Sempat tidak, dan hasilnya
    kotak gelap membayang mengelilingi kertas yang sudah tercabik — bentuk yang justru
    sedang dihapus. `_TearDepth` dikirim ke KEDUA material dengan nilai identik.
  - `TearDepth` peta kecil TIDAK diperkecil sepenuhnya mengikuti panel: ukuran robekan
    sifat KERTASNYA, bukan sifat panelnya.
  - `_TexAspect` -> ISI-lalu-POTONG di UV, bukan melar. Mask tidak dipakai karena tepi yang
    dibaca shader jadi tepi GAMBAR (di luar layar) dan pita larutnya ikut hilang ke sana.
- **Peta punya DUA ukuran sekarang**: `MapPanelRect()` (memilih, satu layar penuh) dan
  `MapPeekRect()` (intip lewat M, kotak melayang `PeekScreenFraction`). Dipilih `MapView()`.
  Ukuran panel ikut ditandatangani di `_mapSig` — tanpa itu ganti mode meninggalkan tata
  letak lama.
- **Perkamen memaksa tinta gelap**: judul, legenda, jalur, cincin, label KAMU, judul
  GRIMOIRE — semua warna lama dipilih untuk latar gelap dan LENYAP di kertas terang.
- **`GridInset = 56`** (baru): papan grimoire digeser sendiri, TIDAK lewat `Margin` —
  angka yang sama menjarak-tepikan bar HP & panel spell di seberang layar.
- **`GrimoirePanel.prefab`** (`Assets/Art/UI/Prefabs/`) — atas permintaan user. Kode HANYA
  menempelkan + menaruh di pojok kiri-bawah petak; ukuran/anchor/anak TIDAK disentuh.
  Menyetel `sizeDelta` dari kode akan menimpa editan prefab diam-diam saat run.
- **Ditolak user**: `gridgrimoireUI.png` sebagai alas petak (jadi bingkai bersarang).
  "Grid" yang dimaksud sejak awal = petak 7x7-nya sendiri; warnanya dipertebal jadi tinta.

## Empat keluhan dari satu screenshot — SELESAI & terverifikasi (2026-08-09)

Detail teknis: **docs/AI-HANDOFF.md §27**. Semuanya bug diam — tidak satu pun melempar
error yang menyebut dirinya.

- **Magenta menutupi setengah layar di toko** = `MagicField_pink.prefab` anak `fog`
  materialnya KOSONG; renderer bermaterial null di URP digambar warna error, dan kabut
  selebar 19,6 unit itu menutupi layar. Suaka memanggilnya `Chance 1, Count 2` — persis
  dua yang muncul. Dipasangi `Additive_soft`, ditiru dari `MagicField_blue` yang sehat.
  **Empat prefab lain punya lubang sama**; `SpeedBoost_front` satu-satunya yang berbahaya
  (renderer hidup) dan sudah diisi.
- **LANJUT tak bisa dipencet di toko**: `HandlePanelClick` jalan lebih dulu dan menelan
  semua klik di dalam `PanelRect()` — yang TUMPANG TINDIH dengan tombolnya. Guard ditaruh
  sesudah modal sungguhan (peta/kejadian/slot), sebelum blok toko.
- **Gloom peta beku**: `Tune()` tidak pernah mengirim `_Churn`/`_Drift`, jadi jatuh ke
  bawaan shader = 0,029 gumpalan/detik. Knob baru di `UiTheme`, disamakan dengan gloom
  arena → terukur 0,200.
- **Petak grimoire kini diatur dari prefab**: anak `GridArea` menentukan letak & ukuran
  petak 7x7. `GridOverride` null = perilaku lama persis. Terukur `(76, 28, 298, 298)`,
  `CellSize` 40 — papan tidak bergeser sedikit pun.
- **Kunang-kunang** dibangun ulang dari anak `sparks` milik `MagicField_blue` (permintaan
  user: "pakai aset yang ada"), bukan partikel bangkitan lagi.

Diverifikasi: kompilasi bersih (0 error/warning), play mode 0 renderer bermasalah,
`HandlePanelClick(tengah tombol)` = False sementara klik badan panel tetap True,
dan dua screenshot.

### Putaran lanjutan hari yang sama — SELESAI

- **Tepi peta MASIH beku** sesudah perbaikan gloom, dan itu benar: `_Churn`/`_Drift` cuma
  menggerakkan NODA. Garis SOBEKNYA dihitung dari `tp = pixel / _TearScale` — tanpa suku
  waktu sama sekali — dan garis potong itulah yang dibaca mata sebagai "tepi peta".
  Sekarang `tp` digeser medan warp gloom (`TearWarp`) + hanyut pelan (`TearDrift`);
  warpnya DIPINJAM dari gloom, bukan disampel ulang. Diverifikasi dua tangkapan berjarak
  23 detik: node & posisi peta identik, sudut kiri-atas bergeser 450 → 478 px.
- **`GrimoireGridArea`** komponen baru. RectTransform kosong tidak menggambar apa pun, jadi
  `GridArea` tak terlihat di jendela prefab dan user menyimpulkan gridnya belum disetel.
  Komponennya menggambar gizmo kotak + petak 7x7 pakai rumus yang SAMA dengan pembangunnya,
  plus knob `Gap`. Terpasang di prefab; terukur `GridGapOverride = 3`, `GridRect()` 298x298.
- **"Skill di-lock tapi nggak evolusi" BUKAN bug** — lock memang membuat piece tak terlihat
  oleh resep (`RecipeResolver.cs:115`). Labelnya yang menyesatkan, diperjelas jadi
  `KEPASANG - TERKUNCI, nggak ikut evolusi`. **Aturan mainnya belum diubah** — menunggu
  keputusan pemilik project.

## Mata di sampul grimoire, pendar UI, aura buku (2026-08-09)

Detail teknis: **docs/AI-HANDOFF.md §28**. Art matanya milik pemilik project; yang dibuat
di sini nyawanya.

- **`GrimoireEye`** — mengikuti kursor HANYA kalau kursor masuk `WakeRadius` (300 px, tepi
  lembut 120). Di luar itu **celingukan**: pindah pandangan tiap 0,9–2,6 detik, 30% ke
  tengah, 22% menunduk, sisanya bebas. Dicampur lewat `interest`, bukan ditukar mendadak.
  Mengikuti = halus, celingukan = MENYENTAK. Kedip 0,13 s lewat squash Y. Jepitan ELIPS
  (per-sumbu bikin juling di diagonal). `unscaledDeltaTime` — ada tombol kecepatan 5x.
- **`Grimoire/UiGlow`** — bloom URP tidak akan pernah mengenai kanvas Overlay, jadi
  pendarnya digambar sendiri: gradien radial additive, tanpa sprite. `EyeGlow` sengaja
  SIBLING dari mata, bukan anaknya — sebagai anak ia terpotong Mask.
- **`UiGloomRect`** — aura hitam memakai ulang shader `GloomEdge`. Sibling index 0, 40 px
  lebih besar per sisi dari sampul. Putaran 1 (70 px, ceiling 0,85) jadi noda hitam besar.
- **Judul papan**: `ShowTitle` dimatikan (mata menindihnya, tempatnya buat ornamen) +
  `TitleArea` buat memindahkannya. **Catatan:** baris itu juga penanda "TERKUNCI - wave
  lagi jalan"; sekarang penanda itu tidak terlihat di mana pun.

Diverifikasi: kompilasi bersih; celingukan terbukti (pupil berpindah antar sampling);
kursor 317 px di luar radius 300 → pupil balik ke tengah; screenshot aura + pendar.
**Belum diverifikasi mata-ke-mata:** pupil mengunci arah kursor saat kursor betul-betul
dekat — paling gampang dirasakan pemilik project sambil main.

### Belum dikerjakan

- `SpriteMask` bawaan di objek `eye` tidak berfungsi di Canvas (itu komponen SpriteRenderer
  2D). Yang bekerja `Mask` UGUI. Aman dihapus, dibiarkan karena itu punya user.
- Ornamen pengganti judul GRIMOIRE belum ada.

## Musuh terpanggang (VAT), bar vitals, strip, dan layar pilih starter (2026-08-09)

Detail teknis: **docs/AI-HANDOFF.md §29–§31**.

**Musuh terpanggang (§29).** Animasi musuh pindah ke TEKSTUR — nol `Animator`, batching utuh.
25 musuh = 3 draw call; wave 15 = 500 musuh, 5 draw call, 59 fps. Tujuh musuh dipanggang,
aset musuh 463 → 97 MB. Jebakan termahalnya `AnimationRootFor`: prefab yang menyelipkan satu
tingkat pembungkus membuat `SampleAnimation` **gagal tanpa error**, dan keenam monster keluar
beku — ketahuan lewat pengukuran, bukan lewat error.

**Bar vitals & strip (§30).**
- `VitalsRig`: kode cuma menyentuh `fillAmount`, warna kilat, dan teks. Sisanya milik prefab.
- **"Mana nggak pernah turun" ternyata BUKAN bug UI** — `fillAmount` mana terbukti mengikuti
  nilai mana sampai 7 desimal. Yang salah `BaseManaRegen` **13**/detik terhadap skill seharga
  1–10. Dikembalikan ke **5**.
- **Hover bola HP & mana** memunculkan angka (`87 / 120`, persen, regen) — bola tidak punya
  tempat menempelkan angka tanpa mengotori artnya.
- **Tiga strip ikon turun** ke −190/−222/−254 lewat prefab `StatusStrips.prefab` — tempat
  lamanya dipilih waktu bar HP masih kotak 18 px, dan bola berbingkai 190 px menelan ketiganya.

**Layar pilih starter (§31).** Menu → PLAY → pilih starter → MULAI RUN → **peta langsung
terbuka** (`GameBalance.MapOpensRun`). Tiga starter: `emberwright` (acuan, semua −1),
`frostwarden` (tebal, lambat), `stormcaller` (rapuh, cepat). Semuanya dari SO — piece,
koordinat petak, stat awal, nama, blurb, warna aksen.

> **Angka Frostwarden & Stormcaller PLACEHOLDER.** Dipilih supaya perbedaannya kelihatan
> waktu diadu, bukan supaya seimbang. Itu pekerjaan pemilik project.

### SELESAI: toko/kejadian/slot jadi scene sendiri (2026-08-09, commit aea1559)

`RoomLoader` pramuat `Room_Shop`/`Room_Event`/`Room_Slot` additive di awal run, nyala-mati
root per node, kamera arena mati hanya kalau ruangan membawa kamera hidup. Panel UI-nya
TIDAK pindah — yang berganti latarnya. `RoomSceneBuilder` membangun ruangan polos; menghias
= pekerjaan tangan di editor, dan menu buildernya MENIMPA jadi jangan dijalankan ulang untuk
ruangan yang sudah dihias.

### SELESAI: sustain + panen + ekspansi rune (fable, commit cda9cb5) & mati terbakar (ecbae3f)

Konten: 7 segel sustain (semua lebih pelit per-petak dari yang lama - findability naik
lewat jumlah, bukan angka), panen kill-restore di 7 skill *3+ (agregat, terverifikasi
8 kill x 0.5 = +4.0), 4 rune baru termasuk runeinti *5 Dot (HANYA resep 3 segel *3).
Pass `Tools/Grimoire/Generate Sustain & Runes`, idempotent terbukti.

Mati terbakar: teknik SSU diport ke Grimoire/EnemyVat (instanced, _VatClip.w), bangkai
ring buffer 160 di EnemyManager, bayangan ikut tergerogoti. SEKALIAN ketemu: pass
ShadowCaster VAT tidak pernah terkompilasi sejak lahir (ApplyShadowBias float3 ->
positionCS float4). Sudah dibetulkan - bayangan VAT MENYALA PERTAMA KALINYA, harga
bayangan sebenarnya belum pernah terukur; kalau fps turun, knob pertama EnemyShadows.

BELUM: pemain mati belum terbakar (masih perlu material SSU di kapsul pemain); efek
visual burn belum dinilai mata pemilik project (baru screenshot slow-mo); Flame SSU
buat VFX skill belum disentuh.

Permintaan user (2026-08-09): item ManaRegen/HpRegen diperbanyak (sekarang cuma 4/2 dari
107 piece); skill ★3+ dapat efek "membunuh memulihkan mana/HP"; rune diperbanyak — ★5
bentuk Dot efek raksasa (lewat resep, ★5 tidak pernah jatuh), ≥2 rune ★4, rune 5-sel
(WAJIB muat 3x3 — batas keras codex/tas/pool). Output: `SustainPass.cs` + edit runtime
minimal. Belum diverifikasi/di-commit — cek hasil agent dulu.

### Arsip: rencana scene ruangan (sudah dieksekusi)

Permintaan tepatnya: *"si shop si event si slot itu taro di scene berbeda tapi buat dia
multiple scene biar gak loading lama"*, dan saat ditanya bentuknya user memilih
**pramuat semua di awal run, tinggal nyala-mati** (bukan load-saat-dibutuhkan).

Sekarang ketiganya cuma panel UGUI yang menumpang di scene `Proto`, digambar
`GrimoireUI` di atas arena (`_shopOpen` / `_eventOpen` / `_gambleOpen`).

Yang dibutuhkan, urutan yang disarankan:

1. `RoomLoader` — `LoadSceneAsync(..., Additive)` ketiganya sekali saat run mulai, lalu
   root-nya di-`SetActive` nyala-mati. Nol loading saat masuk, sesuai pilihan user.
2. Kamera: tiap room punya kameranya sendiri; kamera arena dimatikan saat room aktif.
   **Hati-hati** — `ArenaCamera` menjepit posisi pemain, dan `Gloom` mengikuti kamera.
3. Routing input: `HandlePanelClick` sekarang menelan klik di dalam `PanelRect()`. Begitu
   toko pindah ke room, guard itu harus ikut pindah atau ia akan menelan klik arena.
4. `RunDirector.OnRestEntered` yang memicu masuk room; keluar room balik ke `Stage.Ready`.
5. Tiga scene ditambahkan ke Build Settings.

## Bersih-bersih UI (2026-08-10)

Detail teknis: **docs/AI-HANDOFF.md §32**. Dasarnya lima screenshot bertanda dari pemilik
project; sesi yang mengerjakannya mati kena listrik padam sebelum menyentuh kode, jadi
semuanya digarap dari nol di sini.

- **"Gak bisa ke main menu" ternyata bukan soal tombol.** `GrimoireUI.BuildCanvas()` tidak
  pernah memasang `GraphicRaycaster` — **seluruh** tombol UGUI di dalam run mati diam-diam,
  bukan cuma satu. Terbukti lewat `EventSystem.RaycastAll`: sesudahnya tombol KELUAR KE MENU
  dan KEMBALI dua-duanya tertembus. Tombolnya juga berhenti menghitung posisi dari
  `Screen.width` dan menempel ke rect panel.
- **Setelan jadi empat sub-halaman** (LAYAR / PERFORMA / SUARA / DATA) lewat `SettingsTabs`.
  Panel 1180×1060 → **1180×720**.
- **Nama game: `GRIMOIRE MASTER`** (diputuskan pemilik project 2026-08-10). Hidup di
  `MenuTheme.GameTitle` + `TitleTracking`; ganti = edit satu field lalu rebuild menu.
  **Tagline dibuang.**
- **Deskripsi starter kebaca** — nama dan blurb dulu saling menimpa karena keduanya
  berpivot tengah.
- **Layar starter pakai prefab papan grimoire** (sampul + rune + mata), bukan kotak abu
  polos; petaknya digambar di `GridArea` milik prefab itu. Tata letak halamannya pindah ke
  **`Assets/Art/UI/Prefabs/StarterPanel.prefab`** + komponen `StarterRig` — builder membuatnya
  sekali lalu berhenti menimpanya, jadi geseran tangan bertahan melewati rebuild (diuji:
  geser 60 px → rebuild → masih di tempat). Scene memegang instance-nya, jadi edit prefab
  langsung kelihatan tanpa rebuild.
- **HUD dibersihkan** dari teks tutorial yang menetap (hint klik, "wave lagi jalan",
  "ALT + hover = lihat resep", "TAS (skill doang…)", ekor judul SPELL AKTIF, baris kedua
  banner). PETA naik ke petak yang ditinggalkan label resep.
- **Toko**: banner tengah layar dimatikan selama panel singgah terbuka; tinggi panel prefab
  372 → 420 supaya REROLL berhenti menembus slot baris kedua (terukur nol bentrok);
  `_panelBg` sekarang ikut ukuran kotak prefab, bukan cuma posisinya.

Diverifikasi: kompilasi bersih (0 error/warning), `Build Main Menu` sukses, screenshot menu
utama / halaman starter / setelan tab LAYAR & DATA, raycast tombol, dan perhitungan bentrok
slot toko. **Belum dilihat mata pemilik project.**

> **Konsekuensi yang disadari:** penanda "grimoire terkunci selama wave" sekarang tidak
> muncul di mana pun. Kalau itu terasa hilang, gantinya harus penanda visual di papan.

## Kartu hover & "kok nggak evolusi" (2026-08-10)

**Kartu hover dirapikan.** Tiga penyebabnya sekaligus: kotaknya dipatok **360×150** apa pun
isinya, teksnya `HorizontalWrapMode.Overflow` (jadi blurb berjalan keluar kotak dan dibaca di
atas rumput), dan semua baris berbagi ukuran + warna yang sama. Sekarang tingginya dihitung
dari `preferredHeight` (terukur: segel 380×161, skill 380×189), teks dibungkus, dan isinya
dibagi tiga lapis — kepala (nama besar, jenis·bentuk·petak, status), angka, lalu kaki
(harga & blurb, kecil dan redup di balik garis).

**"Evo gak jalan, garisnya biru padahal bahannya lengkap".** Akarnya: hasil resep sering
lebih besar dari bahannya — `Frenzy Sigil` (2 petak) + `Keen Sigil` (1 petak) = 3 petak
dibebaskan, tapi `Power Sigil` berbentuk **SBend 4 petak**, dan tiap petaknya wajib beralas
rune. Aturan lama **membatalkan merge** kalau hasilnya tidak dapat tempat: bahan dikembalikan,
garis tetap biru, wave lewat tanpa apa-apa.

Audit seluruh resep: **20 dari 121** hasilnya lebih besar dari total bahan, 7 di antaranya
selisih ≥2 petak (mis. `Fireball ×2` 2 petak → `Greater Fireball` Square 4 petak).

**Keputusan pemilik project (2026-08-10): evolusi TIDAK PERNAH dibatalkan.** Aturan lama
terbalik arah — papan penuh justru saat pemain paling butuh menukar piece kecil jadi besar,
dan membatalkannya mengunci pemain di sana. Sekarang bahannya tetap dimakan; hasil yang tidak
muat **keluar dari papan** dan dipasang ulang sendiri oleh pemain. Urutan mendaratnya: TAS
dulu (aman melewati pergantian wave), lantai belakangan — barang tercecer ikut terjual saat
wave berikutnya mulai, dan ★4 hasil peleburan tidak pantas hilang karena pemain berkedip.
Rune tidak bisa masuk tas, jadi langsung ke lantai dekat papan.

Warna garis ikut berubah maknanya: **emas = berevolusi & mendarat di papan**, **jingga =
berevolusi tapi hasilnya keluar**, biru tetap "kurang bahan". Jingga setebal emas — sama-sama
janji yang ditepati, cuma beda tempat mendarat.

Diverifikasi lewat dua papan uji terisolasi:

| Papan | preview | hasil |
|---|---|---|
| 3 petak rune sebaris (mustahil muat) | `complete=True spillsOut=True` | `Keen + Frenzy -> Power Sigil (keluar - pasang ulang)`, bahan habis, `Power Sigil` dikeluarkan |
| 4x4 petak rune (lapang) | `complete=True spillsOut=False` | `Keen + Frenzy -> Power Sigil`, duduk di `(0,0)`, tidak ada yang keluar |


## VFX untuk SEMUA skill — 74/74 terpasang & terverifikasi (2026-08-10)

Detail teknis: **docs/AI-HANDOFF.md §34**. Permintaan: "gak ada lagi placeholder skill".

- **Dua pack di-clone dari project_b** (GUID utuh): `Art/VFX/Packs/GabrielAguiarProductions`
  (AOE elemental: MeteorRain/ArrowRain/ImpactAoE/BuffAoE/SingleComet, 255 MB) dan
  `Art/VFX/Packs/JMO Assets/Cartoon FX Remaster` (CFXR: projectile loop, hit, explosion,
  barrier, 134 MB). Demo scene & Kino Bloom sengaja ditinggal. Cartoon Coffee & 118 sprite
  (1,4 GB, sprite 2D) tidak diambil.
- **`View/SkillVfxPool.cs`** — kolam per-prefab untuk semua kind; instans baru dipreteli
  (Light, AudioSource, script `CFXR_*` — yang terakhir menghancurkan dirinya sendiri selesai
  main, musuh pooling). Burst dua tahap mati (stop emit → 0,7 dtk → nonaktif).
- **Semua CastKind dapat titik pasang**: Projectile/Radial/Orbit/RollingBall = prefab jadi
  badan yang ikut terbang (primitif menciut jadi inti); Nova/Heal/Cleanse/Surge/Restore/
  ForcePush/Blink = burst; Chain = burst pangkal cabang saja (Stormbreaker 8, bukan 32);
  Detonate = burst per ledakan; AreaAtTarget/SunStrike = burst titik jatuh; Line = burst
  berarah umur-paksa; Ward = kubah MENETAP selama Shield hidup; Zone/Vortex = jalur lama utuh.
- **`Editor/VfxPass.cs`** (Tools/Grimoire/Assign Skill VFX) — tabel 70 skill → prefab + skala,
  elemen dipegang teguh, idempotent, dengan audit bawaan (skill polos & kind-gendong berprefab
  non-loop). Audit langsung menangkap Poison Cloud CFXR yang ternyata sekali-main → Poison
  Pool pakai `Potion Bubbles (Loop)`, Plague Bloom pakai `Flies Cloud`.
- **Verifikasi sweep otomatis 74 skill** di Playground (driver `EditorApplication.update`,
  pancingan per kind: HP dipotong buat Heal, debuff buat Cleanse, ailment ke boneka buat
  Detonate, teleport ke gerombolan buat Nova/Blink/Radial): **74/74 menembakkan VFX-nya,
  0 error/warning, 158–166 fps @60 boneka**. Jebakan baru: `PlaygroundBootstrap.Update()`
  mengisi mana penuh tiap frame → skill Restore mustahil menembak di playground tanpa
  mematikan komponennya sebentar.
- **Belum dinilai mata** — proporsi & rasa menunggu playtest pemilik project. Ganti pasangan
  = edit tabel VfxPass, jangan assign manual (pass menimpa).

## Audit transform + dua bug kamera/blink — lapis 5 & 6 (2026-08-10, epilog)

**AI-HANDOFF §40 epilog.** Agent audit menyisir SEMUA penulis transform player/kamera:

- **CameraShake settle glide** (lapis 5): trauma jenuh 1,0 di keramaian; begitu musuh
  sekitar habis (= momen pemain berhenti), kamera meluncur dari offset terakhir (≤40 px)
  balik ke origin ~0,4 dtk — dunia geser searah, karakter diam. Fix: ekor trauma dikuras
  3×; puncak tak disentuh. + bug pause: kamera bergoyang selamanya di menu setelan
  (deltaTime=0 → trauma tak terkuras); kini dikuras waktu nyata.
- **Blink kotak-vs-elips** (lapis 6): CastBlink jepit KOTAK, motor tegakkan ELIPS →
  blink ke sudut disentak balik 2–5 unit. Kini elips yang sama.
- Koreksi premis auditor: "rig terkunci" salah — ia pakai arena 16×9 default kode; aset
  40×30, rig terukur jalan 24,4 unit.

**Verifikasi gabungan** (protokol satu-frame, 5×, wave 6, 15 dtk real): frame "pemain beku
tapi digeser dunia >1,5 px" = **0/986**.

## "Balik posisinya" — LAPIS KEEMPAT: dikepung = tidak pernah Idle (2026-08-10)

**AI-HANDOFF §40 penutup.** Sticky-Run masih kalah karena berhentinya SUNGGUHAN: `PlayerMotor`
memang dirancang berhenti saat terkepung (vektor kabur saling meniadakan — disengaja), jadi
pemain betul-betul stop-start tiap beberapa detik di kerumunan, dan tiap stop melepas pose
lari yang condong (~28 cm) → "badan mundur". Verifikasi lamaku juga CACAT GANDA: diuji
di wave sepi, dan metrik flip-nya memaafkan transisi (1 flip/20 dtk itu palsu — film
per-frame menunjukkan `R R I R I I…` di kecepatan 3,4).

Fix final: `PlayerAvatar.CombatRadius = 5` — musuh sedekat itu + wave aktif = pose lari
TIDAK PERNAH dilepas (lari di tempat sah; aturan bullet-heaven). Terukur di kondisi persis
keluhan (**5× — setelan kecepatan yang dipakai pemilik project**, wave 6): frame Idle saat
musuh dekat **3/838**, flip total 2/75 dtk game, dua-duanya sah.

> Pelajaran terpenting hari ini: verifikasi wajib memakai KONDISI dan METRIK yang sama
> dengan keluhan. Dua-duanya sempat salah sekaligus.

## Arsip: lapis 1-3 "rollback" (perlu tapi belum cukup)

Detail: **AI-HANDOFF.md §40 (tiga bagian)**. Kronologi lengkapnya pelajaran mahal:

1. **Lari di tempat** (resep pemilik project): XZ Bake ON + Based Upon **Center of Mass**,
   Rotation Bake ON, Y Bake OFF, applyRootMotion OFF. Kolom "Based Upon" yang tak pernah
   kusentuh itulah kuncinya.
2. **Kamera beku saat pemain diam** (bukan sekadar lebih cepat): `_stillSpeed` 0,25,
   pengecualian `_freezeWithin` 3 unit untuk Blink/teleport. Luncur balik 49 px → 2 px.
3. **Animator flapping — biang yang sebenarnya di wave 5+**: arah kabur berbalik terus di
   kerumunan, kecepatan melewati nol sesaat tiap balik → Idle menyala sekejap → pose lari
   condong disentak ke pose tegak = "mental balik". Ketangkap log burst
   (`Run@1,5 → Idle@1,3 → Run@1,2`), bukan probe agregat. Fix: **Run lengket** di
   `PlayerAvatar` (keluar hanya setelah <0,35 u/dtk selama 0,3 dtk) + blend 0,28.
   Terukur: 20 dtk wave 6, 59 musuh → **flip = 1**.

> Dua pelajaran yang dibayar mahal: (a) uji di kondisi tempat keluhan lahir — lapangan sepi
> tidak pernah membalikkan arah, bugnya mustahil muncul di sana; (b) probe agregat
> menyembunyikan kejadian sesaat — rekam URUTAN state per-frame kalau gejalanya "kadang".

## Arsip: putaran "rollback" sebelumnya

Detail: **AI-HANDOFF.md §40 lanjutan**. Dua sebab, dua-duanya nyata:

1. **Animasi**: resep lari-di-tempat dari pemilik project. Kuncinya kolom **Based Upon**, yang
   dua putaran sebelumnya tidak pernah kusentuh — Position XZ: Bake Into Pose **ON** + Based
   Upon **Center of Mass** (`keepOriginalPositionXZ = false`); Rotation: Bake **ON**;
   Position Y: Bake **OFF** (pantulan vertikal harus hidup); `applyRootMotion` **OFF**.
   Terukur: mendatar **X 0,054 · Z 0,051 m** selama 3,4 dtk Run (di tempat), pantulan
   **0,060 m** (tidak rata), lompatan transisi Run→Idle **0,224 → 0,019 m** = sama dengan
   frame biasa, jadi nol sentakan.
2. **Kamera**: bukan "lebih cepat" tapi **berhenti total**. Di bawah `_stillSpeed` 0,25 u/dtk
   kamera BEKU; pengecualian `_freezeWithin` 3 unit supaya Blink/teleport tetap disusul.
   Terukur luncur balik **49 px → 2 px**, rig tetap mengikuti normal saat lari.

## Arsip: putaran pertama "rollback" (didiagnosis KAMERA saja — belum cukup)

Detail: **AI-HANDOFF.md §40**. Tiga pengukuran memulangkan tersangka animasi: offset avatar
tetap `(0,−1,0)`, panggul tidak menumpuk selama 4 detik Run (root motion memang sudah
diekstrak — animasi SUDAH di tempat). Yang ketemu: **posisi pemain di layar** — lari 932→1219
px, berhenti **1219→1171 px selama 0,8 detik**. Kamera yang menyusul telat, dibaca mata
sebagai karakter meluncur mundur.

Fix: `ArenaCamera` punya dua waktu susul — `_smooth` 0,35 saat sasaran bergerak, `_settle`
**0,12** saat diam. Terukur: luncur balik selesai ~0,2 dtk (dulu 0,8). Knob lanjutan kalau
masih terasa: `_settle`, atau `GameBalance.CameraDeadZone` (0,22 — **tidak kusentuh**, itu
angka talaan pemilik project).

> Pelajaran: keluhan "animasinya rollback" tiga kali membuat sesi ini menyunting importer
> animasi. Yang menyelesaikannya pengukuran yang tidak menyentuh animasi sama sekali.

## Bilah demo dikecilkan + bug cuaca (2026-08-10)

Detail: **AI-HANDOFF.md §39**.

- **"Hujan gak jalan" akarnya daftar cuaca BEDA per wajah arena.** Weather-nya sehat (Badai =
  881 partikel hidup); tombolnya yang dibangun sekali dari daftar mood wajah pembuka, lalu
  indeksnya salah arti begitu malam datang — klik BADAI di malam hari menghasilkan
  `mood=Sunyi, partikel=0`. `DemoBar.SyncMoods()` sekarang membangun ulang tombol tiap daftar
  mood berubah, dan pilihan diingat lewat NAMA, bukan nomor.
- **Bilah pindah ke pojok kanan atas, 258×92** (dulu selebar layar di bawah = menutupi tas &
  panel spell). Terverifikasi 9/9 tombol jadi target teratas raycast.
- **"Flow ke arena, bukan peta" = alarm palsu dan salahku**: `MapOpensRun` benar (=1),
  runtime terbukti `stage=Choosing`. Sesi ini berkali-kali mematikannya sementara demi
  screenshot arena; siapa pun yang menekan Play di sela itu mendarat di arena.
  **Aturan baru: jangan utak-atik aset bersama demi screenshot — tempuh alurnya
  (`RunDirector.PickNode(run.Map.Nodes[0])`).**

## Bayangan, animasi lari, FX jadi prefab, bilah demo (2026-08-10)

Detail: **AI-HANDOFF.md §38**.

- **Bayangan pemain balik**: `BurnAway.shader` cuma punya pass Forward — nol ShadowCaster,
  jadi memasang materialnya mematikan bayangan tanpa error. Pass ditambah (ikut terkikis
  saat terbakar). Terukur `passCount=2`, `shadowMode=On`.
- **Animasi lari main lagi**: aturan "menembak = tahan Idle" menelan Run karena papan penuh
  menembak lebih sering dari 0,45 dtk. Sekarang gerak MENANG atas pose cast (permintaan:
  "sambil lari nge-skill juga gpp"). Terukur 5 cast sambil lari, animator tetap Run.
- **"Rollback" Run→Idle**: rig Generic butuh `motionNodeName` ditunjuk (`mixamorig:Hips`) —
  tanpa itu Bake Into Pose tercentang tapi nol pengaruh. Plus koreksi: **Bake Into Pose =
  gerakan DIPERTAHANKAN**, jadi XZ justru TIDAK boleh dipanggang. Runtime: rayapan 0,127 m,
  sentakan 0,014 m. **Jangan ukur pakai `SampleAnimation`** — ia melewati root motion Animator
  dan selalu melaporkan rayapan penuh.
- **Semua benda FX jadi prefab** di `Art/VFX/Core/<Nama>/` (10 buah: Inti Peluru, Kilatan
  Tumbukan, Meteor Jatuh, Cakram Kubangan, Telegraf Hantaman, Pecahan Mengambang, Bola
  Menggelinding, Puting Beliung, Barang Jatuh, Penjaga Pulau) lewat `FxLibrary.asset` +
  `Tools/Grimoire/Build Core FX`. Slot kosong = primitif lama, jadi aman. Siap di-QA satu-satu.
- **Bilah demo** (`View/DemoBar.cs`): 9 tombol bawah layar — SIANG/MALAM/SENJA/TENGAH MALAM +
  CERAH/BERANGIN/GERIMIS/HUJAN/BADAI. Saklar `_demoBar` di `_Bootstrap`; **matikan sebelum
  build dikirim ke klien**.

## Reaksi punya VFX sendiri (2026-08-10)

Detail: **AI-HANDOFF.md §37**. Sembilan reaksi berhenti jadi bola primitif —
`ReactionDefinition.Vfx` + wrapper per reaksi di `Art/VFX/Reactions/<Nama>/` (bisa ditukar
tangan, pass tidak menimpa), disembur lewat `SkillVfxPool`, bola primitif ditipiskan jadi
kilatan penanda warna saja. Pass: **Tools/Grimoire/Assign Reaction VFX**, ada audit reaksi
polos + efek LOOP di reaksi sekejap.

Terverifikasi: 16 reaksi meletus → 16 `Vfx_SHATTER` hidup bersamaan; BLOOD SURGE & TOXIC
BURST terlihat di screenshot `reaction_vfx.png`.

> Jebakan baru: callback `EditorApplication.update` ikut mati saat play mode berhenti — probe
> yang menunggu beberapa detik hilang tanpa jejak. Ukur sinkron dalam satu panggilan kalau
> bisa, dan SELALU catat jumlah suntikan (percobaan pertama "0 dari 9" ternyata cuma karena
> musuhnya sudah habis).

## Paket LizMage_Unity + tiga bug avatar (2026-08-10, malam)

Detail: **AI-HANDOFF.md §36**. Paket baru dari pemilik project dipasang penuh mengikuti
BACA-DULU.txt-nya: tekstur diekstrak & dijahit (`FeedTextures` — FBX-nya menunjuk file luar,
ExtractTextures tidak mengisi apa pun), filter Point, **rig Humanoid** (animasi Mixamo apa pun
kini bisa langsung retarget), klip loop, Cloth di cape.

**Putaran rasa (AI-HANDOFF §36 lanjutan 2):** cloth dijinakkan (jangkar diukur di ruang DUNIA
— ruang lokal jubah cuma 6 mm, jadi "atas"-nya derau dan jumlah jangkar berubah tiap run;
ayunan idle 3,0 → 0,023), player 1,65 → 2,10 → **3,0 unit** (~147 px; ukuran harus dinilai
dalam piksel, bukan meter), `BigNumber.Scale` 100 → **10** + titik ribuan baru muncul di
10.000 (`975→9750`, `1030→10.300`), ring AOE dikendurkan (isi 0,06 / tepi 0,4).
Terverifikasi in-game: `player_lizmage_v5.png`.

> Error `m_Targets of GameObjectInspector` + `SerializedObjectNotCreatableException` = editor
> Inspector kehilangan objek yang dipilih saat play mode berhenti / prefab dirakit ulang.
> BUKAN bug game.

**KOREKSI LARUT MALAM — Humanoid dibatalkan, balik Generic.** Muscle-space manusia meremukkan
proporsi kadal jadi jarum vertikal (preview klipnya ikut penyet = avatarnya yang rusak).
Generic aman sekarang karena Animator sudah di root FBX (akar bug "(Missing!)" sudah mati).
Konsekuensi sadar: animasi Mixamo baru tidak auto-retarget; kalau perlu, petakan avatar manual.
Terukur pulih: bahu 0,347, bounds (1,24×1,54×0,92), kadal tampil bertekstur
(`player_lizmage_v3.png`).

Tiga keluhan mata pemilik project, dua akar:
- "gepeng" + "gerak-gerak gak jelas" = **applyRootMotion** bawaan TRUE — klip menyeret &
  memiringkan root melawan PlayerMotor; dari kamera atas, miring = penyet. Dimatikan.
- "gede banget" + spam Invalid AABB = **jangkar cloth nyasar** (indeks vertex mesh 112 dipakai
  untuk partikel cloth 90 yang sudah di-las; cuma 2 terjahit, jubah terbang, bounds meledak).
  Dipetakan ulang dari `cloth.vertices` sendiri: 20/90 terjangkar di kerah, plafon ayun 0,12.
- Kurva "(Missing!)" generasi pertama = Animator di node pembungkus, satu tingkat di atas root
  FBX. Sekarang model = root prefab, Humanoid.

**Jebakan mahal**: menimpa prefab dengan ROOT BARU di path sama = GUID awet tapi fileID root
ganti → referensi scene mati DIAM-DIAM. Pass sekarang menyembuhkan prefab sehat lewat
`LoadPrefabContents` (identitas awet); rakit-ulang penuh memperingatkan cek `_playerAvatarPrefab`.

Terukur: pitch/roll 0/0, bounds badan 1,62, cloth delta 0,36 (dulu 3,0), tekstur nempel,
0 error. `BurnAway` dapat `_BaseMap` supaya model bertekstur tidak jadi siluet polos.
**Menunggu mata pemilik project.** FBX lama di root Player/ sudah yatim — boleh dihapus?

## Feedback VFX putaran 1: wrapper per skill, avatar LizMage, ring AOE (2026-08-10)

Detail teknis: **docs/AI-HANDOFF.md §35**. Empat permintaan, semua masuk:

- **Folder per skill**: `Art/VFX/Skills/<Nama>/Vfx_<Nama>.prefab` (74 buah) — piece menunjuk
  WRAPPER, ganti efek jelek = buka prefab, hapus anak, seret prefab paket lain. Pass TIDAK
  membangun ulang wrapper yang sudah ada (editan tangan aman); hapus dulu kalau mau default baru.
- **3 skill petir ramping**: Spark Bolt / Spark Shards / Storm Shards → Vefects
  `VFX_Trail_Electric` (murni TrailRenderer). **Flame Lash** → `Sword Hit FIRE (Slash)`.
  **Ashfall** dikecilkan (bake 0,22 dalam wrapper; dari ~59 unit → ~13 vs area 8).
  Pack baru: `Packs/Vefects/Trails VFX URP` (10 MB subset).
- **Ring AOE** (`Shaders/AoeRing.shader`): isi semi transparan 0,14, tepi 0,8 mulai 0,82,
  tengah tetap keisi, tepi didorong putih + denyut pelan. Dipakai cakram Zone + telegraf
  SunStrike. Screenshot: `Assets/Screenshots/vfx_aoe_ring.png`.
- **Avatar LizMage**: `Tools/Grimoire/Build Player Avatar` → importer betul (readable, loop,
  skala terukur → tinggi 1,65), `PlayerAnim.controller` Idle⇄Run, **Cloth di cape** (puncak
  dijepit, capsule collider badan), `PlayerAvatar.prefab` (create-if-missing). Runtime:
  jalan = Run, diam = Idle, **menembak menahan Idle 0,45 dtk**, badan belok ke arah jalan.
  Kapsul pensiun di run (`_playerAvatarPrefab` di _Bootstrap scene Proto; kosongkan = balik
  kapsul). PlayerBurnout membakar badan+jubah sekaligus (BurnAway dapat _HeightMin/Max).
  Screenshot: `player_lizmage.png`.

**Jebakan baru**: (1) tulis .cs dari luar → `refresh_unity` TIDAK menjamin recompile, assembly
bisa basi diam-diam — palunya `CompilationPipeline.RequestScriptCompilation()`; (2) jangan
paksa `_mapOpen=false` via reflection (tirai fade ketinggalan) — matikan `GameBalance.
MapOpensRun` sementara kalau butuh foto arena (SUDAH dikembalikan True); (3) indeks katalog
playground bergeser tiap konten nambah — cari by DisplayName.

**Belum dinilai mata**: kibaran jubah, pose cast, warna model (abu polos tanpa tekstur — mau
ditint?), proporsi ring AOE, rupa 74 VFX.

### Berikutnya

- **`productName` di Player Settings masih "My project"** — belum disamakan dengan
  GRIMOIRE MASTER karena mengubahnya memindahkan lokasi PlayerPrefs (codex & setelan yang
  sudah tersimpan bakal terbaca kosong). Butuh keputusan sadar, bukan sekalian
- **Diorama menu masih kubus abu + kapsul kuning** — dua arah yang pernah ditawarkan:
  sampul grimoire + mata jadi art utama (nol aset baru), atau diorama pakai aset hutan +
  skeleton VAT
- **Balancing tiga starter** — angkanya belum pernah diadu sungguhan
- **Tiga model musuh lagi belum terpakai.** Empat archetype sudah punya modelnya sendiri
  (Grunt←Skeleton, Cursed←Monster37, Stalker←Monster40, Spitter←Necromancer), tapi
  Monster38/39/41/42 masih menganggur — belum ada archetype yang memakainya
- Starter belum punya `Portrait` — kartunya sekarang cuma papan + teks
- **Dengar catatan rasa user setelah main** — kunang-kunang baru & gloom peta yang
  sekarang bergerak belum pernah dinilai dengan mata oleh pemilik project
- Font in-game masih Arial bawaan
- Penjaga pulau masih kapsul (dan kapsulnya pakai Default-Material bawaan
  `CreatePrimitive` — belum magenta karena `SetPropertyBlock`, tapi rapuh)
- Spam warning "no audio listeners" masih ada
- Spam error `Property <noninit> already exists in the property sheet` saat play —
  belum dilacak; kandidat: `Gloom.LateUpdate` menulis property block tiap frame

---

## Perilaku skill baru, PAKTA, dan klon jadi item (2026-08-11)

Menjawab tiga permintaan pemilik project sekaligus: skill harus punya PERILAKU lain
(bilah muter di badan, laser mantul yang corat-coret layar), "word modifier" =
**WORLD modifier** dari audit hari ini harus dibangun dan se-ekstrem mungkin dengan UI
sendiri, dan skill dikurangi tapi tiap satunya unik — kekurangannya diganti item.

### 1. Delapan `CastKind` baru — sebaran perilaku pecah

| Kind | Apa yang beda |
|---|---|
| `Orbital` | Bilah mengitari BADAN pemain, melukai yang tersapu selama beberapa detik |
| `Boomerang` | Pergi lurus, pulang mengejar posisi pemain SEKARANG — menagih dua kali |
| `Ricochet` | Sinar memantul dari musuh & dari TEPI LAYAR; coretannya tumbuh bersama build |
| `Turret` | Menara yang menembak sendiri dari tempat ditanam — pemain boleh lari |
| `Shockwave` | Cincin melebar, hanya TEPINYA melukai; mengejar yang sudah di luar jangkauan |
| `Seeker` | Rudal melengkung, satu rudal satu musuh, tidak ada yang tumpang tindih |
| `Tether` | Sinar mengunci satu musuh, damage BERJALAN — jawaban untuk boss |
| `Barrage` | Beberapa hantaman beraba-aba berurutan di titik berpencar |

Implementasi: `Systems/PlayerCasterBehaviour.cs` (partial baru). Dua helper baru di
`EnemyManager`: `DamageRing`/`PushRing` (cincin, bukan cakram — supaya gelombang menagih
tiap musuh SEKALI) dan `FirstAlongRay` (satu lintasan linear per pantulan; sampling titik
demi titik akan jadi 200 ribu perbandingan per tembakan di 500 musuh).

**18 piece baru** lewat `Editor/BehaviourPass.cs` + 18 resep yang sengaja mencampur bahan
LAMA, supaya pembuka Fireball punya jalan menuju perilaku baru.

Sebaran skill sebelum → sesudah: **74 dari 102 skill di 7 ember (73%)** menjadi
**76 skill di 26 perilaku, ember terbesar 7 (43%)**.

### 2. PAKTA — world modifier

`Data/WorldModifierDefinition.cs` (SO) + `Systems/WorldPacts.cs` (runtime, bukan
MonoBehaviour) + **22 pakta** lewat `Editor/PactPass.cs`. Berkah permanen DAN kutuk
permanen, diambil sekaligus. Node kejadian yang dulu cuma "+80 koin" sekarang menawarkan
dua pakta + tombol menolak (koin lama).

Sisi pemain DITAMBAHKAN ke stat; sisi musuh DIKALIKAN berantai (dua pakta +30% nyawa =
1,69×, bukan 1,6× — penjumlahan akan membuat pakta kelima tidak terasa).

Empat aturan yang tidak bisa ditulis sebagai stat: `ManaRegenMul` (nol = regen mati total,
dan itu TIDAK bisa ditiru lewat StatKind karena stat itu penjumlahan yang bisa dilewati
satu segel), `ManaPerKill`/`HpPerKill`, `EchoChance` (cast menembak dua kali; digabung
sebagai peluang GAGAL semua supaya tidak pernah tembus 100%), dan `ReviveAt`.

**Jebakan yang hampir memakan seluruh fitur ini:** `BuffDamageMul` dan tiga saudaranya
membaca `_buffStats`/`_debuffStats` **LANGSUNG**, bukan lewat `Total()`. Menyambung pakta
hanya ke `Total()` akan membuat pakta ber-DamagePct tampil di HUD, terbaca benar di
kartunya, dan **tidak mengubah satu pun angka damage**. Sekarang keempatnya lewat
`Temp(StatKind)` — satu tempat.

`MaxHp`/`MaxMana` sekarang BERLANTAI di 1. Baru perlu sejak pakta ada: sebelumnya semua
yang menyentuhnya menambah; tiga pakta pengurang nyawa yang bertumpuk membawa maksimumnya
ke bawah nol, dan run berakhir sebelum satu musuh pun lahir.

### 3. UI PAKTA — kolom tegak di tepi KANAN

Strip keempat, sengaja beda sisi layar dan beda arah tumbuh dari tiga strip kiri. Tiga
strip itu tempat mata mencari hal SEMENTARA; pakta tidak pernah pergi. Digambar ulang
hanya saat `WorldPacts.Version` berubah, bukan tiap frame. Slot `PactArea` ditambahkan ke
`StatusStripRig` supaya bisa ditata tangan.

`StripPactY = -210`, BUKAN sejajar strip buff (−96). Ketahuan dari screenshot: bilah demo
duduk di pojok kanan-atas sampai ≈−175 dan menimbun dua ikon pakta teratas.

### 4. Klon jadi item — DIUBAH, tidak dihapus

`Editor/ClonesToItemsPass.cs` — 16 skill kembar jadi segel. Id-nya tetap hidup, dan itu
load-bearing: **36 resep menyentuh piece-piece itu, dan nol yang rusak.** Menghapus aset
akan memutus resep penghasil, resep pemakai, dan codex pemain sekaligus.

Itemnya bukan tempelan stat — audit menyebut 28 segel lama sebagai penyumbang kebosanan
terbesar justru karena nol perilaku. Tiga `StatKind` baru membeli KATA KERJA:
`BonusBounces` (peluru & sinar mantul), `BonusForks` (cabang rantai), `BonusHits`
(semburan/bilah/rudal/hantaman). Terukur: **Fireball punya 0 pantulan di asetnya, lahir
dengan 3 pantulan** begitu dua segel duduk di papan.

`thunderclap` & `rimenova` dikonversi, BUKAN `frostnova` & `blizzard`: dua yang terakhir
punya blurb tertulis tangan di `FootprintPass.Table`, dan pass itu menimpanya tiap
dijalankan — segelnya akan menyandang keterangan skill Nova yang tidak lagi ia lakukan.

### Jaring baru di solver

`BalanceTunePass.ExpectedTargets` sekarang mengembalikan **−1** untuk kind yang belum
didaftarkan, dan `Run()` MENERIAKKANNYA lalu melewatinya. Dulu `return 1f`, dan itu
diam-diam berarti "skill ini mengenai satu musuh" — tiap kind baru yang lupa didaftarkan
otomatis mewarisi arti itu dan dikasih damage peluru tunggal walau menyapu 30 musuh.

### Terukur

- Runescrawl ★5 (Ricochet): **1239 dps** melawan target solver 1417 — pas
- Ripple ★1 (Shockwave): 27 dps melawan target 22
- Wave 8, 5 pakta bertumpuk: damage ×3,05 · cooldown ×1,30 · musuh nyawa ×2,1 · jumlah
  ×1,3 · gema 30% · regen mana 0 · HP maks 100→40. Kebangkitan: pukulan mematikan pertama
  → HP kembali ke 20 (50%), yang kedua → mati
- Musuh spawn di HP 107,9 = 51,4 (wave 5) × 2,1 pakta. Wave count 100 = 77 × 1,3

### Yang DIKOREKSI setelah diukur, bukan setelah ditebak

- **Quake ★3 radius 9** = radius bintang EMPAT (Doom Nova 9). Keluar di 680 dps melawan
  Meteor 413 di bintang yang sama. Radius adalah kekuatan papan yang lolos dari seluruh
  sistem penyeimbang → diturunkan ke 6,5. Ripple 5,5 → 4,5
- **Blade Dance cuma 8 dps** melawan targetnya 17,6. Bukan damage-nya: `PlayerMotor`
  menjauhkan pemain begitu musuh masuk `DangerRadius` 6 unit, jadi bilah berjari-jari 2,4
  hidup sepenuhnya di ruang yang SUDAH dikosongkan. Radius Orbital 2,4/3,4/4,6 →
  **3,6/5/6,6**; solver menurunkan damage per denyut sendiri karena luasannya naik

### Urutan menjalankan editor tool (berubah)

`Generate Behaviour Skills` → `Generate Pacts` → `Convert Clone Skills To Items` →
`Footprint by Rarity` → `Generate Placeholder Icons` → **`Rebalance by Throughput` TERAKHIR**.

### Berikutnya / belum beres

- **Belum dimainkan tangan manusia.** Semua angka di atas dari play mode terkendali
- **Balancing pakta belum diadu sungguhan** — 22 pakta, dan yang diuji baru 6 bertumpuk
- Skill baru belum punya `CastVfx` satu pun (masih primitif) — dan slot FxLibrary baru
  (`Blade`, `Boomerang`, `TurretBody`, `ShockRing`, `Missile`) masih kosong
- **Spam error `Can't remove Light because UniversalAdditionalLightData depends on it`**
  — SUDAH DILACAK, bukan dari perubahan sesi ini: `View/SkillVfxPool.cs:186` menghapus
  komponen `Light` dari prefab VFX, sementara URP otomatis menempelkan
  `UniversalAdditionalLightData` yang bergantung padanya. Belum diperbaiki (di luar
  permintaan) — perbaikannya: hapus data tambahannya dulu, atau matikan lampunya
  alih-alih menghapusnya
- Rolet/slot masih cuma koin & piece — rancangan jekpot berbintang di audit belum dibangun

## Ruang uji, VFX, dan teks reaksi (2026-08-11, lanjutan)

Tiga keluhan pemilik project, dan **ketiganya satu akar**: ia menilai skill di ruang uji,
dan ruang uji itu menyembunyikan separuh umpan balik game.

### "reaction gak ada text popup" — bukan dihapus, memang belum pernah ada di sana

Kabelnya utuh di `GrimoireUI.cs:444` dan terbukti hidup saat diperiksa di scene Proto
(`"STATIC FREEZE!"` di slot floater, alpha 0,31). Sebabnya lain: **`PlaygroundBootstrap`
tidak pernah membangun `GrimoireUI`** — nol floater, nol `DamagePopups`. Yang menguji
skill di sana melihat reaksi MELETUS (kilat, partikel, damage tercatat) tapi tidak pernah
melihat NAMANYA, lalu wajar menyimpulkan popup-nya rusak.

Sekarang ruang uji punya kolam floater + `DamagePopups` sendiri. Terverifikasi lewat
screenshot: `"STATIC FREEZE!"` melayang di atas gerombolan boneka.

### Ruang uji akhirnya bisa menguji SEMUANYA — 92 → 136 piece

Dulu `if (p.IsRune || p.IsPassive) continue;` membuang 44 dari 136 piece dari satu-satunya
tempat yang bisa memeriksanya, dan 14 skill lain masuk daftar tapi tidak pernah bunyi.
Semua sebabnya sudah dilacak dan ditutup:

| Yang dulu mustahil | Sebab | Penutup |
|---|---|---|
| 16 rune | ditolak masuk daftar | rune jadi ALAS papan + skill acuan berdiri di atasnya |
| 44 segel | ditolak masuk daftar | segel + skill acuan, angka skill itu yang dibaca bergeser |
| Detonate ×3 | tidak ada yang bertanda | penanda ber-ailment cocok ikut didudukkan (`sunder` → 2 spell) |
| Frenzy Release | `ConsumesCharge` di 0 | generator `GrantOnKill`-nya ikut didudukkan (11 piece, bukan 10) |
| Blink ×2 | `CrowdPressure` NOL PERSIS di lingkaran simetris | formasi **BUSUR** (tombol 5) → terukur 1,000 |
| Ward / Heal / Cleanse | pemain tidak pernah terluka/terkutuk | mode **LAWAN** (F) → perisai 33, 1 kutukan menempel |
| Restore | ruang uji **mengisi mana penuh TIAP FRAME** | pengisian dimatikan selama mode LAWAN → mana 42/80 |

> Baris `_caster.Mana = _caster.MaxMana` itu terlihat seperti kemudahan dan sebenarnya
> tembok: `CastRestore` menolak menyala di atas 60% mana, jadi selama mana dipaksa penuh
> skill itu **mustahil** diuji di sini.

Plus mode **RAPUH** (M): boneka HP 40 yang mati dan langsung diganti, supaya charge dan
panen-per-kill benar-benar berjalan.

**Daftarnya juga sempat tidak terbaca** — panel 268 piksel memotong "Blade Dance
<Orbital>" jadi "Dance <Orbital>". Penyebabnya pivot konten di TENGAH: isi yang lebih
lebar dari viewport melebar ke dua arah, dan yang ke kiri jatuh di luar layar. Pivot
dipindah ke kiri, panel 268 → 360.

### VFX

**18 skill baru tidak punya `CastVfx` sama sekali** — itu murni kelalaian sesi sebelumnya.
Sekarang nol skill polos dari 76.

Tapi memasang prefab saja tidak cukup: **tiga dari delapan perilaku baru tidak pernah
MEMBACA `CastVfx`**. Disambungkan: Orbital (aura melingkar menempel di badan, ikut berjalan
bersama pemain), Ricochet (semburan di titik pantul, dibatasi 6 titik pertama supaya 18
pantulan tidak menjenuhkan kolam dan menutupi coretannya sendiri), Tether (semburan di
ujung sinar tiap denyut).

Semua 18 memakai **satu paket saja — GabrielAguiar**. Audit menyebut gaya paket campur aduk
sebagai sebab pertama efek terbaca jelek, dan GA punya bentuk yang sama untuk delapan
elemen, jadi satu keluarga skill bisa konsisten tanpa keluar dari satu bahasa visual.

**8 sumber yang meleset diluruskan** lewat menu BARU dan berdialog:
`Tools/Grimoire/Fix Mismatched Skill VFX (TIMPA wrapper)`. Terpisah dari `Assign Skill VFX`
karena ini satu-satunya jalur yang MEMBUANG art yang sudah ada — kontrak pass utama tetap
"wrapper yang sudah ada tidak pernah dibangun ulang". Diperiksa dulu lewat git bahwa nol
wrapper lama pernah disentuh tangan sebelum dijalankan.

Guruh → sambaran petir (dulu percikan generik) · Rupture → ledakan (dulu cipratan darah,
yang tergambar organ) · Poison Pool & Plague Bloom → kubangan racun (dulu gelembung ramuan
& awan lalat) · Tornado/Maelstrom → keluarga angin (dulu tornado PASIR dan SALJU untuk
skill yang bukan keduanya) · Steam Burst → ledakan air · Void Lance → tusukan void (dulu
monster ungu meledak). Tiga di antaranya dilewati dan DILAPORKAN karena sudah jadi segel.

### Spam `Can't remove Light` — beres

`SkillVfxPool.Strip` memanggil `Destroy(light)`, sementara URP menempelkan
`UniversalAdditionalLightData` yang ber-RequireComponent terhadap Light itu. Destroy-nya
SELALU ditolak, jadi yang terjadi bukan lampu hilang melainkan satu baris error per lampu
per prefab. Sekarang lampunya `enabled = false` — membeli hal yang sama tanpa melawan
aturan komponen URP.

### Primitif dicabut habis + "skill ini efeknya apa?" (2026-08-11, lanjutan)

**Enam slot FxLibrary baru, dan lima di antaranya sempat KOSONG** — slotnya ditambahkan
tapi tidak pernah ada aset yang mengisinya, jadi bilah Orbital, bumerang, badan menara,
cincin gelombang, dan rudal lahir sebagai `CreatePrimitive` murni: kotak putih yang tidak
bisa dibuka, diseret, atau diganti tanpa menyunting C#. Persis yang `CoreFxPass` ada untuk
mencegahnya. Sekarang **16/16 slot terisi prefab**, masing-masing di folder sendiri di
`Assets/Art/VFX/Core/<Nama>/`.

Pemilik project menegaskan: **jangan pilihkan VFX, cukup sediakan prefabnya** — ia yang
mengganti isinya. Semua wrapper (`Assets/Art/VFX/Skills/<Nama>/`) dan core prefab memang
dibangun untuk itu: ganti isi prefabnya, kode tidak tahu-menahu.

**`OrbitRing` — penanda jangkauan Orbital.** Keluhannya: "gak paham skill apa itu, gak
nge-damage sama sekali tapi kaya spesial gede banget areanya". Itu Blade Dance, dan dua
hal salah sekaligus: efek partikelnya jauh lebih lebar dari radius damage 3,6 (jadi
areanya BOHONG), dan boneka ruang uji berdiri di radius 6,5 — di luar jangkauan. Sekarang
cakram tipis di kaki pemain menggambar radius SESUNGGUHNYA, digambar dari radius damage
dan tidak pernah dari skala efeknya.

**Ruang uji mendekatkan boneka sesuai jangkauan skill terpilih** (`FitSpread`). Hanya
menurunkan, tidak pernah menaikkan — menaikkannya akan diam-diam mengubah sebaran yang
barusan disetel tangan. Blade Dance: 0 dps → **184 dps terukur**.

**Panel bacaan akhirnya menjelaskan SKILLNYA, bukan cuma angkanya.** Tiap piece sudah
membawa `Blurb` yang ditulis untuk dibaca; panel ini tidak pernah menampilkannya. Plus
satu baris `Diagnose()` yang menyebut kenapa sebuah skill DIAM — separuh buku ini sengaja
menahan diri (peledak tanpa penanda, Heal di nyawa penuh, Blink di lapangan lengang), dan
semua penolakan itu terlihat persis sama dari luar: skill yang tidak melakukan apa-apa.
Panel dibesarkan 322x142 → 440x430 supaya kalimatnya muat.

## Tangga footprint dirombak + editor bentuk grid (2026-08-11, lanjutan)

Permintaan pemilik project: bintang 1 itu SATU petak dan sesekali dua, bintang 2 dua dan
sesekali tiga, seterusnya; bentuknya harus beragam tanpa aturan kaku; dan **tidak boleh ada
bentuk yang "mustahil"**.

### Tangga baru

| | Dulu | Sekarang |
|---|---|---|
| ★1 | 2-3 petak | **1**, 9% dapat 2 |
| ★2 | 4 | **2**, 18% dapat 3 |
| ★3 | 5 | **3**, 14% dapat 4 |
| ★4 | 7 | **4-7**, diperingkat dari skor OP |
| ★5 | 8-9 | **5-9**, diperingkat dari skor OP |

Skor OP = `ExpectedTargets × jangkauan`. BUKAN damage: solver sudah menyamakan throughput
seluruh anggota satu tingkat, jadi damage tidak membedakan apa pun. Yang tersisa sebagai
kekuatan nyata cuma berapa musuh tersentuh dan sejauh mana ia menjangkau papan. Dipakai
sebagai PERINGKAT, bukan nilai mentah — skor skill zone berbeda dua orde dari skill peluru.

**13 bentuk baru**, total 33. Diverifikasi otomatis: **0 kembar** (dibandingkan lewat
keempat rotasinya) dan **0 yang keluar kotak 3×3**. Tiga di antaranya sempat kembar
(`Jay`==`Ell`, `Nub`==`Tee`, `Arrow`==`Cross`) — ketahuan oleh pengecekan itu, bukan oleh
mata. Dua bentuk sengaja TERPUTUS (`Diag3`, `Twin`, `Nub`): papan cuma memeriksa tiap petak
satu per satu, tidak pernah menuntut bentuknya menyatu. Di kotak 3×3 hanya ada enam
tetromino yang muat, jadi bentuk 4-petak ketujuh HARUS yang tidak menyatu.

`RarePercent` ditulis sebagai persen, bukan `hash % 5 == 0`. Bentuk kedua terlihat setara
dan tidak: hash id pendek tidak tersebar rata di lima sisa bagi, dan yang terukur 30% —
setengah lebih banyak dari yang dimaksud.

### Kerusakan yang ditangkap SEBELUM sampai ke pemain

Fireball naik dari 1 ke 2 petak karena undian "jarang", lalu mencaplok petak tetangganya:
**`segelmata` milik Stormcaller GAGAL DUDUK** — run dibuka dengan satu piece yang dijanjikan
hilang, tanpa satu pun error. Sekarang piece yang dipakai susunan pembuka hero mana pun
**tidak pernah** dapat ukuran yang lebih besar. Terverifikasi: 3 hero, 6 piece masing-masing,
**0 gagal**.

### Konsekuensi keseimbangan yang ikut ditutup

Rata-rata footprint turun **4,3 → 2,67 petak**, jadi papan 49 petak menampung ~1,6× lebih
banyak piece — dan nafsu mana seluruh papan naik sebesar itu. `SkillsOnAFullBoard` 5 → **8**.
Membiarkannya di 5 berarti mengulang persis bug yang dulu membuat build ★5 terbaik menembak
di 10% laju nominalnya dan mati di wave 20.

### Editor Bentuk Grid — `Tools/Grimoire/Editor Bentuk Grid`

Menggambar bentuk dengan MENGKLIK petaknya, dan meluruskan art di atas bentuk itu, dalam
satu jendela. Juga bisa dibuka dari klik-kanan sebuah PieceDefinition.

Hasilnya ditulis ke `PieceDefinition.CustomCells`, dan begitu terisi **`FootprintPass`
berhenti menyentuh piece itu selamanya** — bentuk gambaran tangan adalah keputusan, dan
generator yang membatalkan keputusan membuat kedua alat itu tidak bisa dipercaya sekaligus.
`Cells` sekarang mengembalikan `CustomCells` kalau ada, jadi papan, tas, codex, ikon, dan
pratinjau evolusi semuanya ikut tanpa satu pun perlu tahu bentuk tangan itu ada.

Bidang art baru di `PieceDefinition`: `Art`, `ArtOffset` (satuan PETAK), `ArtSize` (petak),
`ArtRotation`, `ArtBehindCells`. Jendelanya menggambar art memakai angka-angka itu apa
adanya — bukan dipaskan otomatis — supaya efek tiap geseran langsung terlihat.

> **BELUM: papan sungguhan belum menggambar `Art`.** `GrimoireUI` masih menggambar piece
> sebagai petak berwarna. Bidang + editornya sudah ada dan tersimpan benar; yang kurang
> satu kolam Image di `GrimoireUI` yang membentangkan sprite-nya di atas footprint tiap
> piece terpasang.

### BUG: sinar memantul tidak pernah mencorat-coret (2026-08-11, lanjutan)

Pemilik project menagih: "yg mantul2 mana?". Ditelusuri, dan itu **bug sungguhan** — bukan
soal boneka yang berdempetan seperti yang sempat gw duga tanpa mengukur.

Lintasan Runescrawl (18 pantulan) yang terukur SEBELUM perbaikan:

```
0: (17.1, 13.9)   pemain
1: ( 8.4,  6.5)   musuh A
2: ( 7.5,  7.6)   musuh B
3: ( 8.4,  6.5)   A lagi
4: ( 7.5,  7.6)   B lagi   ... dan seterusnya sampai pantulannya habis
```

**20 titik lintasan, hanya 3 posisi berbeda.** Sinarnya ping-pong di sepasang musuh, dan
kedua puluh ruasnya tergambar menumpuk di satu garis pendek — jadi tidak ada coretan apa pun
di layar, dan skill yang seluruh janjinya adalah bentuk yang tertinggal di layar tampil
sebagai garis biasa yang mahal.

**Sebabnya aturan yang diwarisi dari peluru.** `NearestExcluding` mengecualikan SATU musuh —
yang barusan kena. Itu cukup untuk peluru yang memantul sekali (dan komentar di sana memang
menjelaskan begitu). Untuk sinar yang memantul delapan belas kali, mengecualikan satu berarti
dari A ia memilih B dan dari B ia memilih A, selamanya.

Diperbaiki: dua metode baru di `EnemyManager` — `NearestOutside` dan `FirstAlongRayOutside` —
yang mengecualikan SEKUMPULAN musuh sekaligus. `CastRicochet` mencatat tiap korban cast ini
di `_chainBuffer` dan mengecualikan seluruhnya.

Daftar korban **tidak** dikosongkan setelah memantul di tepi layar. Membiarkan sinar
menyambar ulang terdengar murah hati dan justru membunuh skill ini: gerombolan terpadat
selalu di satu tempat, jadi sinarnya akan pulang ke situ terus dan berhenti menyeberang.
Efek sampingnya yang diinginkan — begitu musuh segar habis, sinarnya TERPAKSA memantul di
tepi layar, dan pantulan tepi itulah yang mencorat-coret.

Terukur SESUDAH, 26 boneka disebar spiral radius 3,5-14,5:

```
20 titik lintasan, 20 posisi BERBEDA
(0,0) -> (3,0) -> (4.2,-2.7) -> (3,-6.3) -> (0.3,-3.8) -> (-2.7,-5.1) ->
(-1.2,-8.9) -> (2.7,-11.9) -> (7.2,-7.2) -> (12.2,-5.6) -> (8,-1.8) ->
(5.8,2.1) -> (2.6,3.3) -> (2.2,7.1) -> (-1.4,5.2) -> (-2.5,2.3) ->
(-6.1,2.5) -> (-4.5,-0.8) -> (-6.7,-3.9) -> (-7,-8.4)
```

Membentang ~19x19 unit. Belum sempat difoto: papan ruang uji disetir ulang di antara
panggilan remote, jadi tiap jepretan kena skill lain. Untuk melihatnya sendiri: pilih
Runescrawl, tekan 4 (formasi acak), renggangkan dengan +.

### Model SnakeBoss dipasang ke SEMUA boss (2026-08-11, lanjutan)

`Assets/Art/Characters/Bosses/SnakeBoss` — tiga FBX (Head / Segment / Tail) + satu albedo.
Mesinnya ternyata sudah lengkap sejak lama: `BossDefinition` punya slot mesh, `BossVisual`
membangun tiga `EnemyRenderer` dan mengembalikan null kalau slotnya kosong (jatuh balik ke
kapsul). Yang kurang cuma asetnya terisi.

Pass baru `Tools/Grimoire/Pasang Model Boss` (`BossModelPass`). **Terpisah dari `BossPass`
dengan sengaja**: model diganti jauh lebih sering daripada angka gameplay, dan menggabung
keduanya berarti tiap kali art ditukar, seluruh nyawa/kecepatan/jeda serangan boss ikut
ditulis ulang ke nilai bawaan.

**Anti-kebalik.** Slot dicocokkan lewat NAMA FILE, bukan urutan folder — ini satu-satunya
tempat kepala bisa tertukar dengan ekor, dan tertukarnya tidak akan pernah melempar error:
ularnya cuma berjalan mundur seumur build itu. Diverifikasi dengan MATA di play mode:
tengkorak kepala memimpin di ujung depan, badan berduri mengekor, tekstur tulang terpakai.

**Skala: dua kali salah sebelum benar, dan keduanya dicatat di kode.**

1. Percobaan pertama membagi dengan skala instans RATA-RATA (1,6) — kepala keluar 3,90 unit
   padahal diminta 2,40, karena kepala memakai skala instans 2,6 bukan 1,6.
2. Percobaan kedua memaksa tiap kepala jadi tepat 2,40 unit — dan ketiga boss jadi SAMA
   BESAR, termasuk `grub` yang seluruh gunanya anak buah kecil. Yang salah bukan angkanya
   melainkan idenya: memaksa panjang akhir berarti membagi habis `HeadScale`, satu-satunya
   bidang yang membedakan ukuran mereka.

Yang benar sesuai maksud bidangnya: pengali aset menormalkan mesh ke SATU UNIT (12,6 kepala
/ 16,8 badan, diturunkan dari bounds bukan diketik), lalu `HeadScale` tiap boss berarti
harfiah "panjang dalam unit dunia".

| boss | kepala | ruas | jarak | tumpang tindih |
|---|---|---|---|---|
| serpent | 2,60u | 1,73u | 1,05 | 1,6× |
| centipede | 2,90u | 1,80u | 0,85 | 2,1× |
| grub (anak buah) | 1,15u | 0,80u | 0,60 | 1,3× |

Ruas sengaja lebih panjang dari jaraknya: ruas yang panjangnya persis sama dengan jaraknya
menyisakan celah di tiap sambungan begitu ularnya membelok — sisi luar tikungan merentang,
dan yang terlihat barisan potongan, bukan satu makhluk.

Wrap mode tekstur diperiksa dan dipaksa Repeat (UV mesh keluar rentang 0..1; Clamp akan
meregangkan satu baris piksel tepi di sepanjang badan).

**Jebakan yang memakan waktu lagi:** `refresh_unity` TIDAK menjamin recompile — pass lama
terus berjalan dan angkanya tidak berubah dua kali berturut-turut, sementara console
menyimpan error compile yang tidak pernah gw lihat. Palunya `CompilationPipeline.
RequestScriptCompilation()`, DAN membaca console setelahnya. Sudah tercatat sebelumnya di
file ini; gw tetap kena.

`GameBalance.MapOpensRun` sempat dimatikan untuk memotret arena, dan **sudah dikembalikan
ke True**.

### Rotasi kepala boss & badan yang berlubang — TIGA bug (2026-08-11, lanjutan)

Keluhan: "ular rotasinya salah" dan "kok tiap badan gak nyambung". Ditelusuri, dan ternyata
**tiga sebab terpisah** — tidak satu pun soal mesh atau materialnya.

**1. Kepala tidak pernah diputar sekali pun.**
Arah hadap tiap ruas diturunkan dari selisih posisinya dengan ruas di depannya. Untuk ruas
NOL, "ruas di depannya" adalah kepala itu sendiri — dan `_trail.Insert(0, _head)` membuat
`SegmentPoint(0)` praktis SAMA dengan `HeadPos`. Selisihnya nyaris nol, penjaga
`sqrMagnitude > 0.0001f` menolaknya, dan yaw kepala tidak pernah ditulis: kepalanya menatap
utara dunia seumur pertarungan sementara seluruh badannya meliuk dengan benar.
Diperbaiki: `BossSnake.Heading` dibuka, dan ruas nol memakainya. Terukur COCOK di ketiga boss.

**2. Jejak kepala tidak berjarak seragam.**
Versi lama menyisipkan SATU titik tiap kepala bergerak sejauh `TrailStep` (0,28) — dan itu
diam-diam salah begitu ia bergerak lebih jauh dari itu dalam satu frame. Saat MENERJANG ia
melaju 15 unit/detik. `SegmentPoint` mencari ruas dengan menghitung INDEKS lalu mengalikannya
dengan TrailStep, jadi begitu jaraknya tak seragam, aritmetika itu berbohong.
Yang terlihat: badan ular MERENGGANG tepat saat ia menerjang. Terukur: jarak antar ruas
terjauh **1,96** padahal ruasnya sendiri cuma 1,26 panjangnya.
Diperbaiki: titik antaranya diisi interpolasi, dibatasi 64 supaya teleport tidak melahirkan
ribuan titik.

**3. `SegmentPoint` membulatkan indeks.**
Pembulatan mengkuantisasi jarak antar ruas ke kelipatan 0,28. Untuk boss ber-Spacing 0,6
jaraknya melompat antara 0,56 dan 0,84 — variasi 50%, dan yang 0,84 lebih panjang dari
ruasnya. Diperbaiki dengan interpolasi antara dua titik jejak.

Terukur SESUDAH ketiganya:

| | jarak antar ruas | target | ruas ekor | |
|---|---|---|---|---|
| serpent | 1,05..1,05 | 1,05 | 1,26 | NYAMBUNG |
| grub | 0,60..0,60 | 0,60 | 0,72 | NYAMBUNG |

Nol variasi, dan tiap ruas menimpa tetangganya.

**Plus: `TailScale` dipatok minimal `Spacing x 1,2`** di `BossModelPass`. Panjang satu ruas
di dunia sama dengan skalanya (pengali aset menormalkan mesh ke satu unit), dan ketiga boss
memakai TailScale LEBIH KECIL dari Spacing — jadi separuh belakang tiap boss pasti berlubang
berapa pun jejaknya dibetulkan. Yang dipatok cuma ambang minimum; nilai di atasnya tidak
diganggu.

> **Mesh-nya sendiri TIDAK terbalik.** Diperiksa lewat profil radius per irisan sepanjang Z:
> kepala meruncing ke +Z (moncong di depan), ekor meruncing ke −Z (ujung di belakang). Dua-
> duanya sesuai konvensi `Yaw = Atan2(forward)`. Dugaan awal bahwa modelnya menghadap −Z
> terbantah oleh pengukuran itu, dan untung tidak keburu "diperbaiki" dengan offset 180°.

### Rotasi model boss — akar sebenarnya: MODELNYA BERDIRI (2026-08-11, lanjutan)

Pemilik project menolak dua perbaikan sebelumnya ("masih salah rotasinya") dan menunjuk
akarnya sendiri: *"file originnya itu berdiri, lu harusnya cuma rotasi di sumbu buat
direbahkan dan cari mana moncong kepalanya."* Benar.

**Dugaan gw sebelumnya salah dua kali**, dan keduanya karena gw menyimpulkan dari STATISTIK
mesh (profil radius per irisan) alih-alih melihatnya. Yang akhirnya menjawab: memajang
ketiga mesh di rotasi NOL di play mode dan memotretnya. Di situ langsung terlihat kamera
atas menampilkan tengkorak dari SAMPING — profil rahang — bukan dari punggungnya.

Lalu lima kandidat rotasi dipajang berdampingan dalam satu frame (identitas, pitch ±90,
roll ±90). Hanya **roll +90 pada sumbu Z** yang merebahkannya.

**Kepala dan ekor diekspor menghadap arah BERLAWANAN.** Setelah direbahkan, moncong kepala
menunjuk −Z sementara ujung ekor juga −Z. Ekor memang harus begitu (menjauhi kepala);
kepala tidak. Jadi satu koreksi untuk ketiga mesh tidak akan pernah cukup — apa pun yang
membetulkan salah satunya membalik yang lain.

Koreksi final, disimpan sebagai DATA di tiap aset boss:

| mesh | koreksi |
|---|---|
| kepala | `(0, 180, 90)` |
| ruas badan | `(0, 0, 90)` |
| ekor | `(0, 0, 90)` |

`EnemyRenderer` dapat parameter `meshEuler`, dipasang di dalam matriks instans sebagai
`Quaternion.Euler(0, yaw, 0) * meshRotation`. **Urutannya bukan selera**: yang kiri berputar
di ruang dunia dan yang kanan di ruang mesh, jadi membaliknya membuat koreksi "rebahkan
model" ikut berputar bersama arah hadap — modelnya berguling tiap kali boss membelok.

Diterima sebagai **Euler, bukan Quaternion**: `default(Quaternion)` adalah (0,0,0,0), BUKAN
identitas, dan mengalikannya meruntuhkan tiap matriks jadi nol. Menjaganya butuh sentinel,
dan sentinel itu tidak bisa diperiksa dengan `==` — operator Quaternion Unity memakai dot
product, jadi `default == default` justru mengembalikan FALSE. `Euler(0,0,0)` tidak punya
lubang itu.

Koreksinya ditaruh di RENDERER, bukan diperbaiki di importer FBX: memutar mesh di importer
mengubah bounds-nya, dan seluruh perhitungan ukuran di `BossModelPass` diturunkan dari
bounds itu.

Diverifikasi dengan merakit kepala + 5 ruas + ekor sepanjang +Z memakai angka final:
tengkorak di depan menghadap arah jalan (terlihat dari ATAS, bukan profil), tulang punggung
menyambung tanpa putus, ekor meruncing di belakang.

> **Pelajaran yang mahal di sesi ini:** tiga kali gw menyimpulkan orientasi mesh dari angka
> (bounds, sebaran vertex, profil radius) dan tiga kali salah. Yang menyelesaikannya dalam
> satu percobaan: pajang di rotasi nol, foto, lihat.

### Primitif akhirnya benar-benar HILANG, bukan dikecilkan (2026-08-11, lanjutan)

Keluhan: *"beberapa skill tornado kok ada primitif object sih, gw kan bilang buang semua"*.
Benar, dan sebabnya bukan slot kosong — melainkan keputusan lama yang sengaja
MEMPERTAHANKAN primitifnya.

Tujuh tempat di kode cuma MENGECILKAN badan primitif saat skillnya punya `CastVfx`: bola
peluru jadi 0,12, bola meteor 0,3, dan tabung puting beliung ditipiskan jadi 22% transparan.
Niat aslinya masuk akal — primitif itu penanda radius damage sesungguhnya, dan partikel
hampir tidak pernah seukuran radiusnya. Tapi yang dikecilkan itu **tetap terlihat**, dan
yang terlihat adalah bola abu dan tabung tembus pandang menempel di tiap efek.

Sekarang `ShowBody()` mematikan RENDERER-nya (bukan GameObject-nya — posisi benda itu dibaca
tiap frame oleh uji tabrakan, ekor, dan pelepasan efek).

**Aturannya sempat salah sekali lagi.** Percobaan pertama: "slot FxLibrary terisi = art
sungguhan, tampilkan". Terukur gagal — `CoreFxPass` sudah mengisi KESELURUHAN 16 slot dengan
prefab yang isinya masih bentuk primitif, jadi "terisi" tidak membedakan apa pun, dan tabung
Tornado tetap muncul persis seperti sebelumnya.

Sekarang satu saklar tegas: **`FxLibrary.ShowBodiesWithVfx`, MATI secara bawaan.** Nyalakan
begitu slot-slotnya diisi model sungguhan. Benda yang TIDAK punya efek partikel tetap
digambar apa pun nilainya — tidak menggambar apa-apa lebih buruk daripada menggambar kotak.

Terverifikasi di play mode: Tornado, renderer primitif **1/1 DIMATIKAN**.

Sapuan seluruh isi buku: **0 dari 76 skill bermasalah** secara struktural (tanpa VFX,
damage 0, Detonate tanpa penanda, pemakan charge tanpa generator, Range 0, durasi 0).

> **Yang masih placeholder dan TIDAK bisa ditutup dari kode:** isi 16 prefab core
> (`Assets/Art/VFX/Core/*`) masih kubus/bola/silinder, dan isi 76 wrapper skill
> (`Assets/Art/VFX/Skills/*`) masih pilihan dari paket yang ada. Keduanya memang dirancang
> untuk ditukar tangan. Penanda tanah (cakram Zone, cincin SunStrike/Shockwave/Orbital)
> sengaja TETAP digambar — itu satu-satunya yang memberitahu jangkauan sesungguhnya, dan
> menghapusnya mengembalikan keluhan "areanya gede tapi gak nge-damage".

## Prefab dirapikan ke Assets/Prefabs/ (2026-08-11)

168 prefab milik project dikumpulkan dari 10 lokasi tersebar ke satu akar.
Dipindah lewat `AssetDatabase.MoveAsset` (GUID ikut, referensi utuh), bukan
lewat OS — memindahkan lewat Explorer akan memutus semua referensi.

    Ambient 8 | Characters 2 | Effects 5 | FX_Core 16 | Light 2
    Reactions 9 | Skills 108 | UI 6 | Weather 8 | World 4

Pack vendor (Packs/, Plugin/, Stylized3DMonster, Feyloom, ToonScapes) SENGAJA
tidak disentuh — scene demo dan referensi internalnya akan rusak.

### Yang wajib ikut pindah: 6 konstanta path
Path disimpan sebagai TEKS, jadi tidak ikut pindah sendiri. Kalau tertinggal,
pass-nya membangun ulang ~100 prefab di path lama TANPA satu pun error —
duplikat senyap. Sudah diganti:

    VfxPass.SkillRoot        -> Assets/Prefabs/Skills
    VfxPass.Lana             -> Assets/Prefabs/Effects/
    CoreFxPass.Root          -> Assets/Prefabs/FX_Core
    ReactionVfxPass.VfxRoot  -> Assets/Prefabs/Reactions
    MainMenuBuilder x2       -> Assets/Prefabs/UI/

### Verifikasi
Ketiga pass dijalankan ulang: `wrapper dibangun 0`, jumlah prefab di
Assets/Art tidak berubah (1060 -> 1060). Nol duplikat.

Sisa 2 peringatan VfxPass (Piece_tornado, Piece_kubanganracun: kind gendong
tapi efeknya sekali-main) adalah isu KONTEN lama, bukan akibat pemindahan.

### Jebakan yang kena lagi (ketiga kalinya)
`RequestScriptCompilation()` tidak menjamin compile SELESAI sebelum kode
berikutnya jalan. Run pertama memakai assembly lama dan melaporkan 4 "prefab
paket hilang" yang palsu. Cara membuktikan assembly benar: baca konstantanya
lewat refleksi (`GetRawConstantValue`) sebelum percaya hasil pass.

Juga: `AssetDatabase.CreateFolder` di dalam `StartAssetEditing()` belum
terdaftar sampai batch ditutup — 6 pemindahan berkas gagal karenanya.

### KOREKSI: duplikat itu MEMANG terjadi

Klaim "nol duplikat" di catatan di atas SALAH. Run pertama yang memakai assembly
basi membangun ulang 113 prefab di path lama (Skills 88 + Core 16 + Reactions 9).

Kenapa lolos dari pemeriksaan: hitungan before/after dijalankan SETELAH duplikatnya
terlanjur lahir, jadi selisihnya nol dan terbaca aman. Jejaknya sebenarnya terlihat
sejak awal — angka Assets/Art melompat 947 -> 1060 antara dua pengukuran, persis
+113 — tapi dibaca sebagai derau pengukuran.

Pelajarannya: membandingkan sebelum/sesudah TIDAK bisa mendeteksi kerusakan yang
sudah terjadi sebelum pengukuran dimulai. Yang mendeteksinya adalah sensus absolut
"prefab apa saja yang ada di luar Assets/Prefabs" — dan itu baru dijalankan karena
user minta dicek ulang.

Penyelesaian: 113 duplikat diverifikasi yatim (0 referensi menunjuk ke sana, 168
referensi menunjuk ke lokasi baru) dan tiap satunya punya kembaran sekaligus nama
sama di Assets/Prefabs (113/113, 0 unik). Ketiga folder lama dihapus.

Keadaan akhir terverifikasi:
    Assets/Prefabs 168, Assets/Art 947, folder lama tidak ada, referensi putus 0
    ketiga pass idempoten: sudah benar 92 / 16 / 9, dibangun 0

## Distribusi ulang VFX + bilah orbital (2026-08-12)

Mandat user: "ganti semua vfx yg gak cocok pake yg baru, kalo gak ada yg lebih
baik keep aja, dan TEST satu-satu cocok gak sama behaviornya."

### Audit dulu, selera belakangan
Semua 92 skill ber-VFX dibedah programatik: isi wrapper, loop/sekali-main,
gravitasi/velocity bawaan, jumlah ParticleSystem. Aturan yang dipakai:
- Skill HIDUP LAMA (Zone/Vortex/Turret/Orbital/Ward) wajib efek LOOP.
- Badan yang DIGERAKKAN KODE (Boomerang/Seeker/Orbital/RollingBall/Radial)
  wajib efek tanpa gerak bawaan (orb, bukan komet).
- Efek JATUH justru benar untuk meteor/rain (AreaAtTarget) — jangan "dibenerin".
- Arah sebalik (loop di skill sekali-main) aman: pool memotong di 0,85 s.

### Temuan rusak (efek once di skill yang hidup lama)
kubanganracun, tornado (dua warning lama), obelisk, sentryeye, bladedance,
ringofruin + 4 komet jatuh di Boomerang/Seeker.

### Penting: fix kemarin TIDAK PERNAH MENDARAT
Fix orb kemarin ditaruh di Map — padahal kontrak pass: Map tidak menimpa
wrapper yang sudah ada. Jalur timpa satu-satunya adalah Corrections.
Batch Corrections baru berisi 15 entri; Map disinkronkan supaya rebuild
masa depan tidak menghidupkan lagi pilihan lama.

### Bilah orbital: perubahan kode, bukan tabel
Sejak BodyVisible, bilah primitif DISEMBUNYIKAN dan aura kaki (once) mati
duluan — Orbital nyaris tak terlihat. Sekarang: satu wrapper VFX PER BILAH
(Ring.BladeVfx), dipasang EnsureBlades, digeret TickRings, dilepas
ReleaseBladeVfx saat ring mati ATAU ring dipakai ulang skill lain.
Wing/Totem/Missile sudah punya jalur ikut-badan sejak awal — tidak disentuh.

### Konsolidasi Lana (compile error CS0101)
Pack Lana sempat dobel (Plugin/ lama parsial + salinan baru di Packs/) dan
script demonya bentrok. Salinan baru dicabut; pack lama DILENGKAPI di
tempatnya (30 -> 66 prefab, Orb_lightning kini ada). LumiBrush di-skip
sesuai instruksi user.

### Batch pilihan (Corrections 2026-08-12)
Zone:    kubanganracun -> Lana MagicField_Poison | ionstorm -> MagicField_Stun
Vortex:  tornado -> Hovl Smoke vortex (asap netral, bukan pasir/salju)
Turret:  obelisk -> Hovl Red energy explosion | sentryeye -> Hovl Magic circle
Orbital: bladedance -> CFXR Lightball B | stormcircle -> Lana Orb_lightning
         ringofruin -> CFXR Fireball + Trail   (semua per-bilah, kecil, loop)
Boomerang/Seeker: chakram+hexbolts+hexstorm -> Orb_lightning,
         moonglaive -> Orb_snow (hexstorm SEMPAT salah Orb_sand — elemen bohong)
Ward:    wardpetty/aegis/bulwark -> Hovl Magic shield blue/yellow/pink
         (pagar listrik terbaca serangan, kubah terbaca perisai)

### Catatan perf (belum jadi masalah, diukur duluan)
Orb_lightning = 7 ParticleSystem. Dipakai per-bilah/per-rudal:
stormcircle 8 bilah = 56 PS, hexstorm ~8 rudal = 56 PS. Pool memakai ulang,
tapi kalau profiler nanti menjerit, penggantinya Hovl Sparks (1 PS per prefab).
Keputusan menunggu bukti profiler, bukan ditebak sekarang.

### Hasil test visual play mode (16 skill difoto satu-satu, run ke-2)

Driver screenshot v1 BUANG — CaptureScreenshot menangkap akhir frame, dan
Select skill berikutnya di tick yang sama membuat semua foto memotret UI skill
sesudahnya. v2 memisah fase capture dan select. Pelajaran: jangan pernah
capture + mutate state di tick yang sama.

LOLOS (dilihat langsung):
- kubanganracun: field racun hijau + tengkorak status di boneka, loop
- tornado: pusaran asap netral mengembara, 594 dps
- obelisk: menara oranye HDR + pusaran energi merah loop, 222 dps
- sentryeye: lingkaran sihir biru berputar di bawah menara
- stormcircle: 5 orb petir + busur listrik mengitari — INI yang diminta user
  ("pake bolt lightning"), 1019 dps
- ringofruin: cincin bara menyala, 3830 dps
- bladedance: jalan (244 dps) tapi bola cahaya 0.6 tenggelam di kerumunan
- wardaegis/bulwark: kubah perisai kuning/magenta — ward akhirnya terbaca
  PELINDUNG, bukan serangan
- ionstorm: field kuning bermuatan + rune persegi, 240 dps
- chakram/hexstorm: damage mengalir (13/698 dps), orb tak tertangkap frame
  (antara lemparan) — bukti visual orb ada di stormcircle (wrapper sama)

PENYESUAIAN setelah lihat: bladedance 0.6 -> 0.85, ringofruin 0.55 -> 0.75.
Belum diverifikasi ulang secara visual setelah penyesuaian.

BELUM DILIHAT: wardpetty (keluarga sama dengan aegis), maelstrom (tidak
diubah), moonglaive/hexbolts (timing sama dengan chakram).

### Verifikasi ulang skala (run ke-3)
bladedance 0.85 dicek visual: tiga titik cahaya + halo terbaca di kondisi
terburuk (40 boneka rapat). ringofruin 0.75 difoto juga. Selesai diterapkan:
ApplyCorrections 15/15, audit pass 92/92, masalah 0.

## Batch VFX v2 + laser beam (2026-08-12, lanjutan QA user)

Punch list user, semua diterapkan (11 koreksi + 2 kode):
- flamelash -> Flamethrower; sabetanpetir -> Hovl Electro slash. TITIK LAHIR
  VFX Line dipindah: dulu 30% panjang garis (ragnarok = 4,2 unit di depan),
  sekarang 0,9 unit dari badan — semburan lahir dari tangan. TERBUKTI di foto.
- sparkbolt -> CFXR Lightball A (Vefects trail 0-partikel dicabut);
  sparkshards/stormshards -> Orb_lightning (bahasa yang sama dgn Storm Circle)
- greaterfireball scale 1.5 ("kurang gede"); emberroll -> CFXR4 Burning Fire
  ("terlalu sama kaya fireball")
- ionstorm -> CFXR Electric Surface (MagicField_Stun "kotak2" kata user)
- Tornado Lana KEMBALI atas keputusan user: tornado=sand, maelstrom=snow,
  whirlwind=Hovl Smoke vortex. Tangga vortex: asap -> pasir -> salju.
  TERBUKTI di foto: tornado pasir mengembara, 732 dps.
- BoltPool: shader baru Grimoire/LaserBeam (Assets/Shaders/LaserBeam.shader) —
  inti memutih + halo additive + denyut halus; kontrak vertex-color/alpha
  dipertahankan jadi C# tidak berubah. Lebar pita Line 1.6x -> 0.8x halfWidth.
  TERBUKTI di foto ragnarok: pita oranye datar hilang, 2066 dps.

Catatan: error shader 'Sprite Shaders Ultimate' di console = vendor pack
(bentrok include URP 2D), BUKAN dari LaserBeam. Sudah ada sejak impor pack.

## BERIKUTNYA: boss varian (instruksi user, belum dikerjakan)
"yg uler bertanduk = Boss biasa, yg polos = ELIT; boss satunya 2 type:
yg GEDE pake CACING (worm), yg KECIL pake KELABANG (centipede)."
Aset sudah ada: SK_Snake_Head_Horned + SK_Snake_Segment_Spiked (Boss),
SK_Worm_* (3), SK_Centipede_* (3), tekstur Charred/Flesh/Moss/Pale.
Def: Boss_serpent (-> horned), Boss_centipede (-> worm?), Boss_grub (-> centipede?).
PERTANYAAN TERBUKA untuk user: "Elit" ular polos itu def baru (Minion=true)
atau apa? Belum ada def keempat.
Jalur: isi HeadMeshFile/BodyMeshFile/TailMeshFile/BoneSkinFile per def
(field niat yang selamat dari BossModelPass), jalankan Tools/Grimoire/
Pasang Model Boss, cek rotasi per mesh (worm/centipede bisa beda hadap).

### Boss varian terpasang (2026-08-12)
serpent <- Horned+Spiked (Boss) | centipede <- Worm (yg gede) | grub <- Centipede (yg kecil)
Lewat field *MeshFile (niat, selamat dari pass), BossModelPass jalan: "3 boss
memakai model SnakeBoss", 0 error. Rotasi ketiganya mewarisi koreksi ular
((0,180,90)/(0,0,90)) — BELUM diverifikasi visual; pelajaran ular: kepala vs
ekor bisa diekspor beda hadap. Cek in-game di sesi berikutnya.
BELUM: def "Elit" (ular polos) belum ada — nunggu jawaban user; tekstur varian
(Charred/Flesh/Moss/Pale) belum dipilih per boss.

## Feedback ronde 3 (2026-08-12 malam) — semua kecuali satu

1. Lightning Slash: def.Color kuning -> biru (0.38, 0.72, 1) menyamai Electro
   slash — laser, flash, dan penanda ikut biru lewat satu field.
2. Flame Lash BERUBAH KELAMIN -> "Bara Pantul", Ricochet api b1 (Hits 4,
   Radius 0.5). Alasan user: "udah kebanyakan skill kaya gini [Line]".
   Tangga ricochet kini: api b1, es b2, listrik b4, arcane b5 — pola VFX
   keluarga dipertahankan (ImpactAoE elemen di titik pantul). Wrapper lama
   "Flame Lash" dihapus. Damage di-solve ulang: 76 dmg / 1 mana.
3. Ion Storm b3 "terlalu gede dan OP": Radius 5 -> 4.2, VFX scale 1 -> 0.5
   (Electric Surface memang lebar dari sananya). Damage ikut solver: 10/tick.
4. KURVA BINTANG dicuramkan: TargetDps {22/68/190/520/1350} ->
   {22/68/170/620/1750}. Alasan terukur: rasio lama menurun per bintang dan
   nilai PER SEL b3 (~76) nyaris = b4 (~95) — "bintang 3 kaya lebih berharga
   daripada bintang 4 dan 5". Sekarang per sel: b3 ~68, b4 ~113, b5 ~219.
   64 skill di-solve ulang, BELUM diplaytest.

### BELUM DIKERJAKAN (janji ke user): penanda tanah ber-TIMING
"kalo ada timingnya dia berubah balik ke primitif padahal bisa gak pertahanin
shadernya, kalopun pake timing timingnya di shaderin"
Dugaan: telegraf hantaman / penanda isi-mengisi digambar lewat jalur berbeda
yang menskalakan primitif polos, bukan lewat Grimoire/AoeRing. Jalur kerja:
cari siapa yang menggambar telegraf timing (grep Telegraf/telegraph di
PlayerCaster + EnemyManager), lalu tambah param _Fill 0..1 di shader AoeRing
(atau varian) yang diisi dari MaterialPropertyBlock — timing pindah ke shader,
primitif tidak pernah tampil.

## DESAIN MAIN MENU (2026-08-12, disetujui untuk dibangun sesi berikutnya)

Masalah: menu sekarang "terlalu polos". Identitas game: grimoire gelap, sihir
HDR yang menyala (bloom 0.8-1.25), papan grid, bullet-haven. Menu harus
MEMAMERKAN identitas itu, bukan sekadar tombol.

### Konsep: "Buku yang Menunggu di Meja"
Diorama 3D (kerangka BuildDiorama SUDAH ADA) berisi GRIMOIRE terbuka di atas
meja batu, dilihat serong-atas. Di atas halamannya: 2-3 piece grid melayang
pelan (pakai orb yang sudah ada: Orb_lightning + CFXR Fireball, kecil), dan
SATU bilah orbital berputar lambat mengitari buku — aset yang SUDAH terbukti
cakep di game. Latar: gelap vignette (Gloom shader ada), Embers_calm Lana
melayang + Hovl "Sparks flashing blue" jarang-jarang. TIDAK usah bikin model
buku bagus dulu — kubus pipih + material emissive ungu untuk halaman pun
jalan; yang menjual adalah CAHAYA dan GERAK, bukan mesh.

### Layout (1920x1080, aman untuk mobile portrait nanti dipisah)
    ┌──────────────────────────────────────────┐
    │  GRIMOIRE HAVEN            [diorama 3D   │
    │  <subjudul kecil>           buku+orbit   │
    │                             di kanan,    │
    │  ▸ MULAI  (besar, UiGlow)   ~60% layar]  │
    │  ▸ Lanjut Run                            │
    │  ▸ Grimoire   (koleksi)                  │
    │  ▸ Pengaturan                            │
    │  ▸ Keluar                                │
    │  v0.x        [ikon2 kecil]               │
    └──────────────────────────────────────────┘
Tombol rata kiri, kolom sempit (~420px) — bukan tengah: diorama yang jadi
bintangnya. Hover tombol = teksnya menyala (UiGlow, warna elemen bergilir?
JANGAN — satu warna aksen saja: ungu arcane #B380FF, konsisten).

### Aturan visual
- SATU warna aksen (ungu arcane), sisanya netral gelap. Warna lain hanya
  boleh datang dari VFX diorama.
- Font judul: yang dipakai UI sekarang, ukuran besar + letter-spacing lebar;
  glow lembut lewat UiGlow, BUKAN outline tebal.
- Transisi masuk: judul fade + naik 20px, tombol menyusul berurutan 60ms.
- Hormati penataan tangan user di scene menu kalau ada (memory!).

### Jalur teknis
MainMenuBuilder.Build() sudah membangun _Menu + diorama + canvas. Tambah:
BuildDiorama -> meja + buku + spawner orb/bilah (komponen runtime kecil
MenuOrbit.cs: putar transform, TANPA physics); BuildBackdrop -> vignette
Gloom + partikel; tombol -> gaya baru (UiGlow material, layout kiri).
StarterPanel/SettingsPage yang sudah ada JANGAN dirombak — cukup restyle
warna aksen supaya senada.
