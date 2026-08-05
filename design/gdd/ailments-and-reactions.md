# GDD — Ailment, Buff, dan Reaksi

## 1. Overview

Sistem status terdiri dari **6 debuff** (menempel di musuh) dan **6 buff** (menempel
di pemain). Keduanya bukan tujuan akhir — yang dikejar pemain adalah **reaksi**:
kombinasi dua debuff pada musuh yang sama meledak jadi efek besar, dan sebagian
reaksi menghadiahi pemain sebuah buff.

Artinya rantai nilainya begini:

```text
skill  →  nempelin debuff  →  dua debuff ketemu  →  REAKSI  →  ledakan + buff pemain
                                                        ↓
                                              buff bikin skill berikutnya lebih kuat
```

Skill yang berdiri sendiri selalu kalah dari dua skill yang debuff-nya saling
mengunci. Itu inti build-nya.

## 2. Player Fantasy

Pemain bukan penembak — pemain adalah **penyusun rantai reaksi**. Kepuasannya
datang saat satu tarikan wave menghasilkan letusan berantai yang tidak dia
kendalikan langsung, tapi dia yang merancang: "gue yang bikin ini kejadian."

Momen puncaknya: melihat layar penuh musuh berubah putih (tanda dua ailment
menempel), lalu semuanya pecah bersamaan.

## 3. Detailed Rules

### 3.1 Debuff (di musuh)

| Nama | Elemen | Stack | Efek |
|---|---|---|---|
| **BURN** | Api | 3 | Damage berjalan tiap 0.5s, dikali stack |
| **CHILL** | Es | 1 | Kecepatan gerak −55% |
| **SHOCK** | Petir | 1 | Menerima damage +30% |
| **BLEED** | Fisik | 5 | Damage berjalan cepat (tiap 0.4s), dikali stack |
| **POISON** | Racun | 5 | Damage berjalan lambat + gerak −15% |
| **SERET** | Arcane | 1 | Musuh tertarik pelan ke titik asal debuff |

**SERET** ada bukan untuk damage, tapi untuk **mengumpulkan musuh**. Dia yang
mengubah reaksi tunggal jadi reaksi massal.

### 3.2 Aturan slot

- Tiap musuh punya **4 slot** ailment. Ada 6 jenis debuff — jadi **tidak mungkin
  memasang semuanya**. Ini disengaja: build harus memilih 3–4 debuff yang saling
  bereaksi, bukan menumpuk semua.
- Slot penuh → yang **sisa durasinya paling pendek** ditimpa.
- Kena debuff yang sama saat masih aktif → durasi di-refresh (bukan ditambah),
  stack naik 1 sampai batas.

### 3.3 Aturan reaksi

- Reaksi dicek **hanya saat sebuah debuff baru menempel**, bukan tiap frame.
- Satu penempelan hanya boleh memicu **satu** reaksi. Urutan pengecekan =
  urutan di `ContentDatabase`, jadi **urutan daftar adalah prioritas**.
- Reaksi **wajib** menghabiskan minimal satu bahan. Kalau tidak, dia memicu
  dirinya sendiri tiap frame.
- Debuff yang ditularkan reaksi **tidak boleh memicu reaksi lagi**. Tanpa aturan
  ini, satu ledakan bisa menjalar ke seluruh layar tanpa henti.
- Cooldown internal **0.25 detik per musuh per reaksi**, supaya reaksi tidak
  berkedip saat skill ber-cooldown pendek menembak beruntun.

### 3.4 Tabel reaksi

**MVP (8 reaksi):**

| Bahan | Nama | Efek | Hadiah buff |
|---|---|---|---|
| BURN + CHILL | **PECAH** | Burst besar di satu titik. Dua bahan habis | BARA |
| BURN + POISON | **LEDAK RACUN** | Ledakan seluas radius, POISON menular ke sekitar | — |
| BURN + BLEED | **BAKAR LUKA** | Semua stack BLEED langsung jadi damage sekaligus | — |
| CHILL + SHOCK | **BEKU STATIS** | Musuh berhenti total 1.5 detik | ALIRAN |
| SHOCK + BLEED | **ARUS DARAH** | Burst + SHOCK menular ke sekitar | SUMUR |
| BLEED + POISON | **NANAH** | Damage besar dihitung dari total stack keduanya | — |
| SERET + BURN | **BADAI API** | Menarik semua musuh dalam radius lalu membakar | — |
| SERET + CHILL | **PUSARAN BEKU** | Menarik dan membekukan satu gerombolan | FOKUS |

**Target penuh (tambahan):**

| Bahan | Nama | Efek |
|---|---|---|
| BURN + SHOCK | **NYALA BUSUR** | Menyambar 3 musuh, masing-masing kena BURN |
| CHILL + BLEED | **SERPIH ES** | CHILL habis, BLEED +2 stack |
| CHILL + POISON | **LENDIR BEKU** | Durasi POISON dua kali lipat |
| SHOCK + POISON | **RACUN BERSENGAT** | POISON tick dua kali lebih cepat |
| SHOCK + SERET | **MAGNET PETIR** | Menarik lalu menempelkan SHOCK ke semua yang tertarik |
| BLEED + SERET | **JEJAK DARAH** | Yang tertarik ikut kena BLEED |
| POISON + SERET | **KABUT RACUN** | Awan racun menetap 4 detik di titik itu |

### 3.5 Buff (di pemain)

Semua buff berdurasi, tidak menumpuk (kena lagi = durasi di-refresh), maksimal
**6 slot**. Sumber utamanya reaksi; sebagian juga dari segel.

| Nama | Efek | Sumber utama |
|---|---|---|
| **BARA** | Damage semua skill +25% | reaksi PECAH |
| **ALIRAN** | Cooldown berjalan 40% lebih cepat | reaksi BEKU STATIS |
| **SUMUR** | Regen mana ×2 | reaksi ARUS DARAH |
| **FOKUS** | Jangkauan & radius +30% | reaksi PUSARAN BEKU |
| **PERISAI** | Menyerap sejumlah damage sebelum HP berkurang | segel, hadiah wave |
| **GEMA** | Cast berikutnya diulang sekali gratis | reaksi langka / segel bintang tinggi |

**GEMA** sengaja dibuat berbasis jumlah cast, bukan durasi — supaya dia terasa
seperti sesuatu yang "disimpan", bukan yang habis lewat begitu saja.

## 4. Formulas

```text
# Damage over time
damage_per_tick = DamagePerTickPerStack × stacks
total_dot       = damage_per_tick × (durasi / TickInterval)

# Damage yang diterima musuh
damage_diterima = damage_masuk × Π(DamageTakenMultiplier semua ailment aktif)

# Kecepatan musuh
kecepatan = kecepatan_dasar × Π(MoveSpeedMultiplier semua ailment aktif)

# Ledakan reaksi
burst = BurstDamage + (BurstDamagePerStackA × stack_A)

# Urutan damage skill (dikunci, tidak boleh diubah diam-diam)
final = base
      × (1 + Σ additive%)      # aura rune + segel elemen + buff BARA
      × Π multiplicative
      × crit
      × (1 + vulnerability)    # dari SHOCK dll di musuh
```

## 5. Edge Cases

| Situasi | Aturan |
|---|---|
| Musuh mati saat reaksi berjalan | Ledakan tetap terjadi di posisi kematiannya |
| Slot penuh, debuff baru masuk | Timpa yang sisa durasinya paling pendek |
| Dua reaksi memenuhi syarat bersamaan | Ambil yang paling atas di `ContentDatabase` |
| Reaksi menempelkan debuff yang bisa bereaksi lagi | Diblokir — hasil reaksi tidak memicu reaksi |
| Debuff menular ke musuh yang sudah punya debuff itu | Durasi di-refresh, stack naik 1 |
| Buff didapat saat masih aktif | Durasi di-refresh, efek tidak ditumpuk |
| SERET dipakai ke musuh yang sudah menempel di pemain | Tidak ada efek gerak, debuff tetap menempel untuk reaksi |
| Semua debuff dari elemen yang sama | Tidak ada reaksi — ini kegagalan build yang sah dan harus terasa |

## 6. Dependencies

- **Grimoire** — menentukan skill apa yang aktif, jadi debuff apa yang tersedia
- **Rune & segel** — mengubah angka skill, bukan jenis debuff-nya
- **Sistem resep** — skill bintang tinggi umumnya membawa debuff yang lebih langka
  (SERET dan POISON sebaiknya hanya ada di bintang 2 ke atas)
- **Damage meter** — satu-satunya cara membuktikan reaksi benar-benar menyumbang
  damage, bukan cuma terlihat ramai

## 7. Tuning Knobs

Semua ada di asset, tidak ada yang di kode:

| Knob | Lokasi | Efek kalau dinaikkan |
|---|---|---|
| `MaxStacks` | StatusDefinition | Reaksi berbasis stack jadi lebih ganas |
| `DamagePerTickPerStack` | StatusDefinition | Build DoT naik, build burst tidak |
| `MoveSpeedMultiplier` | StatusDefinition | Nilai kontrol vs damage bergeser |
| `DamageTakenMultiplier` | StatusDefinition | Menguatkan semua sumber damage sekaligus — hati-hati |
| `BurstDamagePerStackA` | ReactionDefinition | Menumpuk stack jadi lebih berharga dari menyebar |
| `BurstRadius` | ReactionDefinition | Menggeser dari damage tunggal ke damage massal |
| `SpreadToNearby` | ReactionDefinition | Saklar besar: mengubah reaksi jadi mesin kerumunan |
| Durasi buff | ReactionDefinition | Seberapa sering pemain merasa "sedang kuat" |

## 8. Acceptance Criteria

1. Menempelkan dua debuff yang berpasangan **selalu** memicu reaksi dalam 1 frame.
2. Reaksi tidak pernah memicu dirinya sendiri secara beruntun (uji: pasang
   LEDAK RACUN di kerumunan 50 musuh, frame time tidak boleh melonjak).
3. Tidak ada alokasi memori saat 200 musuh kena AoE berdebuff bersamaan
   (uji lewat Profiler: GC Alloc = 0 B di `EnemyManager.Update`).
4. Musuh dengan 2 ailment terlihat berbeda tanpa membaca angka.
5. Menambah satu status baru dan satu reaksi baru **tidak memerlukan perubahan
   kode sama sekali**.
6. Di wave 10, minimal 25% total damage pemain berasal dari reaksi
   (dibaca dari damage meter). Kalau di bawah itu, reaksinya cuma hiasan.
7. Tidak ada satu reaksi pun yang menyumbang lebih dari 40% total damage.
