# GDD — Grimoire Haven

Dokumen utama. Isinya aturan main yang mengikat; angka detail ada di asset
(`GameBalance`, `PieceDefinition`, dst), ailment dibahas terpisah di
[ailments-and-reactions.md](ailments-and-reactions.md), jumlah konten di
[content-plan.md](content-plan.md).

---

## 1. Overview

Bullet haven top-down 3D. Pemain **tidak bergerak dan tidak menembak**. Yang
dikendalikan pemain cuma satu: **isi buku sihirnya**.

Musuh datang bergelombang. Di antara gelombang, waktu berhenti dan pemain
menyusun grimoire — meletakkan rune sebagai alas, menumpuk skill di atasnya,
menyisipkan segel, dan menyusun bahan resep agar berevolusi. Begitu tombol
MULAI ditekan, grimoire terkunci dan pemain hanya menonton hasil rancangannya.

Satu run ± 12–15 menit. Yang membuat orang main lagi adalah build yang belum
sempat dicoba, bukan level yang belum tamat.

## 2. Player Fantasy

**Kamu bukan penyihir yang bertarung. Kamu penyihir yang menulis.**

Kepuasan puncaknya bukan saat menembak, tapi saat wave berjalan dan kamu sadar
susunanmu bekerja: dua ailment saling mengunci, reaksi meletus beruntun, dan
seluruh gelombang habis tanpa kamu menyentuh apa pun. Kamu yang membuat itu
terjadi — tiga puluh detik sebelumnya, di atas kertas.

Kegagalan pun harus terasa seperti kesalahan menulis, bukan kesalahan refleks.

## 3. Core Loop

```text
FASE SUSUN (waktu berhenti)          FASE WAVE (terkunci, cuma nonton)
┌───────────────────────────┐        ┌─────────────────────────────────┐
│ ambil item yang tercecer  │        │ skill nembak sesuai cooldown    │
│ pasang rune (alas)        │  MULAI │ ailment menumpuk di musuh       │
│ tumpuk skill & segel      │ ─────► │ dua ailment ketemu -> REAKSI    │
│ susun bahan resep segaris │        │ musuh mati -> drop tercecer     │
│ jual yang nggak kepakai   │        │ musuh habis -> wave beres       │
└───────────────────────────┘        └─────────────┬───────────────────┘
             ▲                                     │
             │        evolusi resep diselesaikan   │
             └─────────────────────────────────────┘
```

Tiap 3 wave, **toko** terbuka di fase susun.

## 4. Detailed Rules

### 4.1 Grimoire — dua lapis

Grid **7×7**. Ada dua lapisan di petak yang sama:

- **Lapis bawah — RUNE.** Alas. Ditaruh di petak kosong.
- **Lapis atas — SKILL & SEGEL.** Wajib berdiri **penuh di atas rune**. Satu
  petak pun menggantung di ruang kosong = tidak boleh ditaruh.

Semua benda punya bentuk (1 petak, 2 memanjang, 3 memanjang, 2×2, 3×3, L) dan
bisa diputar. Menaruh di tempat yang sudah terisi akan **menendang keluar**
penghuni lamanya — barangnya jatuh tercecer di dekat situ, tidak hilang.

Mencabut rune yang sedang diinjak skill membuat skill itu ikut terlempar.

**Grimoire hanya bisa diubah di fase susun.** Selama wave berjalan, terkunci.

### 4.2 Rune — alas yang punya karakter

Rune bukan sekadar tempat berdiri. Tiap rune punya:

| Properti | Fungsi |
|---|---|
| **Bentuk** | menentukan berapa skill yang bisa ditampung |
| **Elemen** | skill dengan elemen sama dapat bonus damage |
| **Aura** | damage% / cooldown% / area% untuk skill di atasnya |
| **Stat** | bonus langsung ke pemain: HP, Defense, mana, dll |
| **Bintang** | rarity; ⭐2 ke atas tidak pernah jatuh dari musuh |

**Aturan paling penting — aura itu nilai TOTAL, dibagi rata ke semua petaknya.**

> Rune 3 petak dengan aura +30% memberi **+10% per petak**. Skill yang cuma
> menginjak satu petaknya dapat sepertiganya. Skill yang menutupi ketiganya
> dapat penuh.

Konsekuensinya: **rune besar bukan berarti lebih kuat.** Rune besar menyebar
kekuatannya untuk menampung banyak skill; rune kecil memusatkannya ke satu
skill. Itu pilihan, bukan tangga.

Bonus elemen mengikuti aturan pembagian yang sama.

### 4.3 Skill — dua cara meletus

Tiap skill punya **satu** dari dua pemicu:

**A. Cooldown** — jalan sendiri begitu timer habis, mana cukup, dan ada target
dalam jangkauan. Kalau tidak ada target, cooldown **berhenti di posisi siap** —
tidak berputar sia-sia dan tidak membuang mana.

**B. Ambang ailment** — skill tidak pernah menembak sendiri. Dia menunggu satu
musuh mengumpulkan sekian **poin** ailment tertentu, lalu meletus **di musuh
itu**. Contoh: *"begitu satu musuh punya 10 poin POISON, aku meledak di sana."*

Bedanya dengan reaksi: **reaksi adalah aturan dunia** (terjadi siapa pun yang
menempelkan), **pemicu adalah milik skill** (cuma terjadi kalau skill itu
terpasang). Rantai pemicu dibatasi **3 tingkat** supaya build chain tidak
mengunci frame.

Bentuk serangan: Proyektil, Nova (ledakan melingkar), Chain (menyambar N musuh),
Heal, dan Pasif.

### 4.4 Segel — item yang ikut rebutan tempat

Segel adalah benda lapis atas, sama seperti skill: **wajib berdiri di atas rune
dan memakan tempat**, tapi tidak menembak. Dia menaikkan stat pemain.

Itu inti tegangannya: **tiap segel yang kamu pasang adalah satu slot skill yang
kamu relakan.** "Fireball kedua, atau +25 mana biar Fireball pertama tidak
macet?"

Sebagian resep memakai segel sebagai **bahan**, jadi ada pilihan ketiga:
menyimpannya untuk di-craft.

### 4.5 Stat

Semua stat memakai satu daftar yang sama, dan boleh diberikan oleh rune, segel,
maupun skill:

| Kelompok | Stat |
|---|---|
| Bertahan | MaxHp, HpRegen, Defense |
| Sumber daya | MaxMana, ManaRegen, ManaCostPct |
| Menyerang | DamagePct, CooldownPct, AreaPct, RangePct, CritChance, CritDamage |
| Elemen | FireDamagePct, IceDamagePct, LightningDamagePct |
| Ailment | AilmentPoints (poin tambahan tiap kali menempel) |

**Dua lingkup dipisah tegas:**
- **Lokal** — aura rune, hanya untuk skill yang berdiri di atasnya
- **Global** — stat dari segel/rune, berlaku ke pemain dan semua skill

Keduanya dihitung **sekali** saat grimoire berubah, jadi angka datar. Saat wave
berjalan, tidak ada satu pun sistem yang membaca grid.

### 4.6 Evolusi

Resep butuh **2–3 bahan**, dan syaratnya dua-duanya harus terpenuhi:

1. Bahan **bersebelahan**
2. Semua petaknya membentuk **satu garis lurus tak terputus** (satu baris atau
   satu kolom)

Evolusi diselesaikan **di akhir wave**, bukan seketika — jadi menyusun bahan
adalah taruhan: kamu mengorbankan kekuatan wave ini demi wave berikutnya.

Umpan balik langsung di papan:
- **Garis biru** — bahan sudah segaris tapi belum lengkap
- **Garis emas** — lengkap, akan berevolusi saat wave beres

**Kunci (klik kanan)** membuat sebuah benda tidak pernah ikut kegabung. Wajib
ada begitu pemain punya skill bintang tinggi yang tidak mau jadi bahan.

Resep bisa mencampur skill **dan** segel.

### 4.7 Rarity & cara dapat

| Bintang | Cara dapat | Drop musuh | Toko |
|---|---|---|---|
| ⭐1 | drop, hadiah wave, toko | ya | ya |
| ⭐2 | resep, toko (peluang kecil) | tidak | jarang |
| ⭐3 | resep saja | tidak | tidak |
| ⭐4 | resep saja | tidak | tidak |

Aturan ini berlaku sama untuk rune maupun skill.

### 4.8 Barang, penyimpanan, dan uang

- **Tercecer** — semua drop jatuh berserakan di layar. Diambil dengan klik.
- **Tas (4×3)** — penyimpanan **khusus skill/segel**. Isinya **tidak menembak**;
  menyimpan itu ada ongkosnya.
- **Rune tidak bisa disimpan.** Langsung dipasang di grimoire, atau dijual.
- **Kotak JUAL** — lempar apa pun ke sini untuk jadi koin.
- **Yang masih tercecer saat wave dimulai otomatis terjual.** Fase susun adalah
  batas waktunya.

### 4.9 Toko — sebuah event, bukan menu

Toko **hanya terbuka tiap 3 wave**. Di luar itu tombolnya tidak ada.

- 6 slot acak, stok baru tiap event
- **Reroll naik +15 koin tiap kali dan TIDAK PERNAH reset seumur run**

Itu keputusan desain utamanya: reroll ke-10 harganya 155 koin. Jadi yang dikejar
pemain adalah **membangun build**, bukan memutar-mutar toko sampai dapat yang
diinginkan.

### 4.10 Wave

- Wave dimulai hanya lewat tombol **MULAI WAVE**, dan hanya kalau ada minimal
  satu skill terpasang
- Jumlah musuh, HP, dan kecepatan naik tiap wave
- Maksimal **200 musuh** hidup bersamaan
- Semua cooldown **di-reset penuh** saat wave dimulai, mana terisi penuh
- Wave beres → +HP, hadiah drop, evolusi diselesaikan
- Kecepatan bisa dipercepat **1× / 2× / 3× / 5×** kapan saja

## 5. Formulas

```text
# Aura rune (per petak yang diinjak skill)
kontribusi = AuraValue / jumlah_petak_rune
bonus_elemen = ElementMatchBonus / jumlah_petak_rune   (kalau elemen cocok)

# Damage skill
final = base
      × (1 + Σ aura_damage_lokal + bonus_elemen + damage_elemen_global + DamagePct)
      × crit
      × (1 + vulnerability_dari_ailment)

# Cooldown
cooldown = base × (1 − min(0.75, aura_cooldown_lokal + CooldownPct))    # lantai 0.15 detik

# Jangkauan & radius
jangkauan = base × (1 + aura_radius_lokal + RangePct)
radius    = base × (1 + aura_radius_lokal + AreaPct)

# Mana
biaya = ManaCost × clamp(1 − ManaCostPct, 0.2, 1.0)

# Ailment
poin_ditempel = AppliedPoints + AilmentPoints
dot_per_tick  = DamagePerTickPerPoint × poin

# Damage yang diterima pemain
hp_hilang = max(damage × 0.1, damage − Defense × dt)
```

## 6. Edge Cases

| Situasi | Aturan |
|---|---|
| Skill tanpa target dalam jangkauan | Cooldown berhenti di posisi siap, mana tidak terpotong |
| Mana kurang padahal cooldown siap | Menunggu; lingkaran cooldown berubah **biru** |
| Nova tanpa musuh di radius | Tidak meletus sama sekali |
| Menaruh di petak terisi | Penghuni lama terlempar tercecer di dekat situ |
| Mencabut rune yang diinjak skill | Skill ikut terlempar, tidak hilang |
| Tas penuh, drop masuk | Jatuh tercecer, bukan hilang |
| Lantai penuh (24 benda) | Kelebihannya terjual otomatis, dengan notifikasi |
| Evolusi tidak muat di bekas jejaknya | Batal, bahan dikembalikan utuh |
| Benda terkunci jadi bahan resep | Tidak pernah ikut; garis resep juga tidak menghitungnya |
| Wave dimulai sambil memegang benda | Benda dijatuhkan ke lantai, lalu terjual bersama sisanya |
| Pemain mati | Run berakhir, SPACE untuk ulang |

## 7. Dependencies

- **Ailment & reaksi** — [ailments-and-reactions.md](ailments-and-reactions.md)
- **Jumlah konten** — [content-plan.md](content-plan.md)
- **Arsitektur & performa** — [../../docs/architecture/architecture.md](../../docs/architecture/architecture.md)

## 8. Tuning Knobs

| Knob | Lokasi | Kalau dinaikkan |
|---|---|---|
| Ukuran grid | `GameBalance` | Build jadi longgar, tekanan memilih hilang |
| `RerollCostIncrement` | `GameBalance` | Toko makin sekali pakai, build makin dipaksa improvisasi |
| `ShopEveryWaves` | `GameBalance` | Ritme "belanja vs bertahan" |
| `KillDropChance` | `GameBalance` | Kecepatan pertumbuhan build |
| `BaseManaRegen` | `GameBalance` | Berapa banyak skill yang sanggup ditopang sekaligus |
| `AuraValue` rune | tiap rune | Nilai memusatkan vs menyebar |
| `ElementMatchBonus` | tiap rune | Seberapa memaksa build mono-elemen |
| `TriggerPoints` | tiap skill pemicu | Seberapa sering rantai pemicu meletus |

## 9. Acceptance Criteria

1. Pemain bisa menyelesaikan satu run 12–15 menit tanpa membaca tutorial.
2. Memindahkan satu skill ke rune lain **langsung** mengubah angka di panel
   spell — tanpa memulai wave.
3. Dua run dengan rune awal berbeda menghasilkan build yang terasa berbeda,
   bukan cuma angka berbeda.
4. Tidak ada skill ⭐1 yang tidak muncul di minimal 2 resep.
5. Di wave 10, minimal 25% damage berasal dari reaksi.
6. Tidak ada satu sumber damage pun yang melebihi 40% total.
7. 200 musuh + reaksi berantai tetap 60 FPS, GC Alloc 0 B di loop utama.
8. Menambah skill, rune, segel, status, atau resep baru **tidak memerlukan
   perubahan kode**.

## 10. Status implementasi

Bagian ini yang membedakan dokumen ini dari daftar keinginan.

**Sudah jalan:**
grid dua lapis 7×7 · rune (bentuk, elemen, aura terbagi, stat, rarity) ·
skill (cooldown & pemicu ambang) · segel · tas · barang tercecer · jual ·
toko event + reroll menanjak · resep 2–3 bahan + garis biru/emas + kunci ·
5 ailment berbasis poin · 4 reaksi · mana & regen · Defense · range ·
lingkaran cooldown + denyut · tooltip · ALT+hover resep · kecepatan 1–5× ·
seluruh data di ScriptableObject

**Dirancang, belum dibuat:**

| Fitur | Kenapa penting |
|---|---|
| **SERET** (debuff penarik) | Tanpa ini semua reaksi cuma kena 1–2 musuh |
| **Buff pemain** (BARA, ALIRAN, dll) | Rantai "reaksi → buff → skill lebih kuat" belum tersambung |
| **Damage meter** | Tanpa ini kriteria 5 dan 6 tidak bisa dibuktikan |
| **Codex** | Rasa koleksi dan penemuan |
| **Crit** | `CritChance`/`CritDamage` sudah ada sebagai data, tapi belum dipakai di pipeline damage |
| **Cooldown internal reaksi 0.25s** | Pencegah reaksi berkedip saat cooldown skill sangat pendek |
| **Sumber BLEED & POISON** | Dua ailment ini sudah jadi, tapi belum ada skill yang menempelkannya |

Empat yang paling atas adalah urutan pengerjaan yang saya sarankan.
