# Audit konten — skill, VFX, event, rolet (2026-08-11)

Dibuat sebelum restart sesi (UnityMCP baru dinyalakan). Ini temuan mentah, **belum ada
kode yang diubah**. Data pendukung: `audit-pieces.txt` (118 piece) dan
`audit-vfx-map.txt` (74 skill → prefab VFX sumbernya).

Keluhan pemilik project yang memicu audit ini:
1. "banyak banget skill tapi sebenarnya itu-itu aja"
2. "ada beberapa skill yg gak bisa di test di playground"
3. "vfx beberapa jelek banget dan aneh"
4. event kesempatan cuma ngasih gold — maunya modifier dunia (buff+debuff permanen)
5. rolet cuma ngasih duit — maunya skill/rune berbintang, jekpot susah

---

## 1. Skill homogen — 74 dari 102 duduk di 7 ember

118 piece = 16 rune + 102 skill. Sebaran `CastKind` skill:

| CastKind | n | catatan |
|---|---|---|
| **Passive (7)** | **28** | nol perilaku, cuma tempelan stat |
| AreaAtTarget (4) | 11 | |
| Zone (6) | 9 | |
| Nova (1) | 9 | |
| Line (5) | 6 | |
| Chain (2) | 6 | |
| Projectile (0) | 5 | |
| **subtotal** | **74** | **73% dari seluruh skill** |

Sisa 28 skill tersebar di 13 perilaku khas, masing-masing cuma 1–3 anggota:
Vortex 3, SunStrike 3, Ward 3, Detonate 3, Cleanse 2, Heal 2, RollingBall 2,
Surge 2, Blink 2, Orbit 2, Radial 2, ForcePush 1, Restore 1.

**Di dalam satu ember bedanya cuma ANGKA.** Ember Nova, sembilan-sembilanya:

```
emberburst   9/2.6   frostnova   9/3.0   staticfield  8/2.4
steamburst  18/2.5   blizzard   13/3.0   thunderclap 17/2.8
rimenova    32/3.2   nullsphere 40/3.4   novakiamat  72/4.2
```

Itu satu skill dikali 9 elemen/tier. Pola identik di AreaAtTarget (11) dan Zone (9).

> Jumlah jujur skill yang benar-benar berbeda: **sekitar 20**. Sisanya reskin.

### Opsi perbaikan (belum diputuskan)
- **(a) Pangkas** — gabung klon jadi ~40 skill yang tiap satunya beda. Paling jujur,
  tapi codex & save kena.
- **(b) Kasih puntiran** — 102 tetap, tiap anggota ember dikasih satu ciri unik.
  Aman ke save, kerjanya paling banyak.
- **(c) Bereskan 28 passive dulu** — penyumbang kebosanan terbesar, paling murah.

---

## 2. Playground: 58 dari 118 piece tidak bisa dinilai

### 2a. Ditolak masuk daftar — 44 piece
`Assets/Scripts/Composition/PlaygroundBootstrap.cs:167`

```csharp
if (p == null || p.IsRune || p.IsPassive) continue;
```

16 rune + 28 passive tidak pernah muncul sama sekali.

### 2b. Masuk daftar tapi tidak pernah bunyi — 14 skill

| Skill | Sebab |
|---|---|
| Detonate ×3 — `reckoning`, `rupture`, `sunder` | Butuh skill LAIN yang menempelkan ailment. `Select()` mengosongkan papan dan menaruh SATU skill → tidak ada yang bisa diledakkan |
| `lepasamuk` (Frenzy Release 3★) | `ConsumesCharge` terisi; sumber charge-nya `segelamuk` / `segeldaya` — dua-duanya passive yang ditolak playground. Selalu cast di 0 charge |
| Blink ×2 — `blinkstep`, `voidstep` | `CastBlink` (`PlayerCasterSignature.cs`) `return false` saat `CrowdPressure` ≈ 0. Formation default **Ring** simetris → tekanan saling meniadakan → tidak pernah nyala |
| Ward ×3, Heal ×2, Cleanse ×2, Restore ×1 | `EnemyManager.SpawnDummy` set `e.Speed = 0f` dan `e.Kind = null` (baris 989–997) — boneka tidak pernah menyerang. Tidak ada yang perlu ditahan/disembuhkan |

Catatan: tidak ada satu pun piece memakai `Trigger: StatusThreshold` (semua `Cooldown`).
`ConsumesCharge` cuma dipakai `lepasamuk`; `GrantOnKill` cuma `segelamuk` & `segeldaya`.

---

## 3. VFX — 74/74 wrapper kosong tanpa penyesuaian

Tiap skill yang bisa cast punya prefab `Vfx_<Nama>` di `Assets/Art/VFX/Skills/<Nama>/`.
**Semua 74 wrapper isinya persis satu `PrefabInstance` stok, tanpa modifikasi apa pun** —
posisi 0, rotasi identitas, tanpa skala, tanpa tint, tanpa penyetelan umur partikel.
Contoh utuh: `Vfx_Thunderclap.prefab` (96 baris) cuma membungkus
`CFXR4 Sparks Explosion`.

Tiga sebab "jelek dan aneh":

**(i) Gaya pack campur aduk.** Cartoon FX Remaster (CFXR — kartun terang, ala mobile)
+ GabrielAguiar (`vfx_*` — stylized RPG) + lain-lain (`Tornado_snow`, `Rockfall`,
`Orb_lightning`, `VFX_Trail_Electric`). Tiga bahasa visual berbeda di satu layar,
sementara peta pakai Gloom + HAZE volumetric fog + ToonScapes.

**(ii) Sumbernya sering tidak nyambung dengan skillnya:**

```
Thunderclap      -> CFXR4 Sparks Explosion            (percikan generik, bukan petir)
Rupture          -> CFXR2 Blood Shape Splash          (cipratan darah)
Poison Pool      -> CFXR2 Potion Bubbles (Loop)       (gelembung ramuan)
Plague Bloom     -> CFXR4 Flies Cloud                 (awan lalat)
Steam Burst      -> CFXR Smoke Poof Circle Flat
Blink Step       -> CFXR Magic Poof
Ashfall          -> Rockfall
Tornado          -> Tornado_sand                      (pasir, untuk skill non-pasir)
Void Lance       -> CFXR4 Monster Explosion Purple (Small)
Frostbite Field  -> CFXR4 Snow 'Splashes'
Whirlwind        -> CFXR4 Wind Zone
```

Peta lengkap 74 baris: `audit-vfx-map.txt`.
Pemakaian ulang: `VFX_Trail_Electric` dipakai 3 skill, `CFXR3 Iceball A + Ice Trail` 2 skill.

**(iii) Bentuk primitif tetap tergambar di atasnya.** Per komentar
`PieceDefinition.cs:76–79`, primitif sengaja TIDAK dihapus saat `CastVfx` dipasang
karena dialah penanda radius damage sesungguhnya. Jadi tiap cast = cakram/bola warna
polos **plus** partikel kartun berukuran beda, saling tumpang tindih.

Yang TIDAK bermasalah: semua 118 piece sudah punya `Icon`; tidak ada `CastVfxScale`
ekstrem (<0.4 atau >2.5); tidak ada VFX api di skill es atau sebaliknya.

---

## 4. Event → modifier dunia (rancangan, belum dibangun)

Sekarang (`GrimoireUI.cs:2385`, `:4071`): tombol A "AMBIL BERKAH +80 koin"
(`GameBalance.EventGoldGift`), tombol B tukar 45 koin (`EventTradeCost`). Cuma duit.

Rancangan **PAKTA** — satu pilihan = buff permanen + debuff permanen sekaligus:

```
DARAH TEBAL    musuh HP +30%       | damage kamu +25%
KACA           damage musuh +40%   | cooldown kamu -20%
PUASA          HP max kamu -25%    | crit +15%
KELAPARAN      mana regen -30%     | tiap kill memulihkan mana
BADAI          musuh gerak +25%    | area semua skill +30%
```

Yang perlu dibuat: `WorldModifierDefinition` (SO baru), penampung state di run,
ikon, dan strip HUD supaya pemain bisa membaca "dunia ini lagi kenapa".

---

## 5. Rolet → skill/rune berbintang (rancangan, belum dibangun)

Sekarang (`GrimoireUI.cs:4010–4016`): `GambleSmallGold 25` / `GambleBigGold 90`,
biaya `GambleCost 40`, bobot `GambleWeights = { 30, 25, 15, 18, 9, 3 }`.

Rancangan formula gulungan:

```
3 simbol sama  -> skill ATAU rune bintang 3
4 simbol sama  -> bintang 4
5 simbol sama  -> JEKPOT bintang 5   (bobot ~1-2%)
gagal          -> recehan / rune 1 bintang
```

**Ganjalan teknis wajib diputuskan:** `PieceDefinition.cs:199`

```csharp
public bool CanDrop => Stars <= 1;
```

Cuma piece 1★ yang boleh jatuh. Hadiah bintang 3/5 dari rolet **harus lewat jalur
terpisah** yang sengaja menembus aturan ini — kalau tidak, jekpotnya mustahil.

---

## Keputusan yang masih ditunggu

1. Skill 74 itu: pangkas (a), puntir (b), atau passive dulu (c)?
2. Urutan kerja — usulan: playground dulu, lalu event/pakta, lalu rolet, lalu skill.
3. Ikon pakta: placeholder generated (`Tools/Grimoire/Generate Placeholder Icons`)
   atau tunggu art beneran?
4. VFX: ganti sumber per skill satu-satu, atau seragamkan ke satu pack dulu?
