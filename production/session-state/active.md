# Session State

<!-- STATUS -->
Epic: Grimoire Haven — arah bullet-haven
Feature: Skill non-serangan (kabur/perisai/kontrol), kamera dead-zone, drop jadi benda nyata
Task: Terpasang & terverifikasi programatik — belum pernah dinilai main tangan
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
