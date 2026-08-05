# Session State

<!-- STATUS -->
Epic: Grimoire Haven — prototipe tervalidasi
Feature: Main menu, codex, setelan, lighting golden hour
Task: Menu + lighting terpasang; kecerahan lantai tinggal dinilai mata
<!-- /STATUS -->

## Baca ini dulu

**[docs/AI-HANDOFF.md](../../docs/AI-HANDOFF.md)** — dokumen serah-terima lengkap:
peta file, invarian yang dipegang kode, jebakan yang sudah memakan waktu, dan
daftar "jangan lakukan". Semua konteks yang dibutuhkan ada di sana.

## Ringkas

Dua scene, keduanya terdaftar di Build Settings:

| # | Scene | Isi |
|---|---|---|
| 0 | `Assets/Scenes/MainMenu.unity` | menu, codex, setelan — **digenerate**, jangan diedit tangan |
| 1 | `Assets/Scenes/Proto.unity` | game |

Semua data gameplay ada di ScriptableObject di `Assets/GameData/`
(29 piece, 14 resep, 6 status, 4 reaksi, 6 buff). Tidak ada angka gameplay yang
tersisa di kode. Tampilan menu ada di `Assets/GameData/MenuTheme.asset`.

Codex **sudah jadi** dan sekarang tinggal di main menu, bukan di dalam run.
Yang di dalam run cuma pencatatannya (`DiscoveryLog` → `codex.json`).

## Dokumen

| Dokumen | Isi |
|---|---|
| [design/gdd/grimoire-haven.md](../../design/gdd/grimoire-haven.md) | GDD utama |
| [design/gdd/ailments-and-reactions.md](../../design/gdd/ailments-and-reactions.md) | ailment, buff, reaksi |
| [design/gdd/content-plan.md](../../design/gdd/content-plan.md) | target jumlah konten |
| [docs/architecture/architecture.md](../../docs/architecture/architecture.md) | arsitektur + rencana refactor |
| [docs/AI-HANDOFF.md](../../docs/AI-HANDOFF.md) | serah-terima antar agent |

## Evolusi: aturan diganti

Syaratnya sekarang **bersentuhan** (gumpalan nyambung 4 arah), bukan garis lurus.
Aturan lama membuat 6 dari 14 resep mustahil terpicu karena piece 2×2/3×3/L
sudah melar ke dua baris dengan sendirinya. GDD §4.6 sudah direvisi.
Sekarang **0 resep mustahil**.

Diperbaiki bareng itu: kelima segel tadinya `Kind = AreaAtTarget` (ikut nembak,
cooldown 0.05s) — sekarang `Passive`; dan statnya dipindah dari field legacy
tersembunyi ke `Stats[]`.

## Reaksi, stun, dan juice

Reaksi 4 -> **9** (9 dari 15 pasangan status). SERET akhirnya jadi bahan lewat
BADAI API dan PUSARAN BEKU — dua-duanya menular, jadi reaksi kena gerombolan
bukan 1-2 musuh. STUN baru (gerak x0, damage diterima +15%) dari BEKU STATIS.
Semua 6 buff sekarang punya sumber.

Camera shake berbasis trauma di `CameraShake.cs`, dipicu dari `ProtoBootstrap`
lewat event reaksi & cast AoE. Bar HP/mana beranimasi: chip tertinggal, kilatan
saat kena, denyut saat HP di bawah sepertiga.

**Belum dijawab pemilik project:** "penghilang efek negatif" itu maksudnya
musuh bisa menempelkan debuff ke pemain (sistem baru), atau sekadar skill
utilitas bertahan? Dua tafsir ini hasil kerjanya beda jauh.

## Utang dokumen yang belum dibereskan

- `design/gdd/content-plan.md` masih memakai target lama (status 3–4, reaksi 3–4,
  total skill 15–23) padahal isi sekarang sudah melewatinya.
- `ailments-and-reactions.md` mendaftar 8 reaksi MVP, baru 4 yang dibuat.

## Lighting (golden hour)

Terpasang di dua scene lewat `SceneLook_Game.asset` dan `SceneLook_Menu.asset` —
matahari, ambient, kabut, dan post-processing dipakai bareng; cuma warna lantai
yang beda. Lantai game sengaja tetap gelap supaya musuh dan HUD terbaca.

URP asset sudah diubah: HDR color grading menyala, MSAA 4x (PC) / 2x (Mobile).

**Belum diputuskan:** tingkat terang lantai game. Satu knob:
`SceneLook_Game.asset` -> `GroundColor`. Sekarang `0.20, 0.185, 0.150`.

## Berikutnya

1. Nilai sendiri kecerahan lantai game di Game view, geser `GroundColor` kalau perlu
2. Varian musuh — **sengaja ditunda** oleh pemilik project
3. Sistem audio (slider volume sudah ada, belum menggerakkan apa pun)
4. Navigasi keyboard/gamepad di menu
5. Refactor: pecah `GrimoireUI.cs` (~2070 baris)
6. Performa — baru relevan setelah musuh benar-benar 200
