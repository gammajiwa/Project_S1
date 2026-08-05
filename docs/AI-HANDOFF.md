# Handoff untuk agent AI

Dokumen ini ditulis supaya agent lain bisa lanjut kerja tanpa harus menebak.
Baca ini **sebelum** menyentuh kode. Terakhir diperbarui: sesi 2026-08-05 (main menu).

---

## 1. Ini game apa

**Grimoire Haven** — bullet haven top-down 3D, Unity 6.3 LTS (URP), C#.

Pemain **tidak bergerak dan tidak menembak**. Yang dikendalikan cuma isi buku
sihirnya. Musuh datang bergelombang; di antara gelombang waktu berhenti dan
pemain menyusun grid: rune sebagai alas, skill dan segel di atasnya, bahan resep
disusun segaris agar berevolusi. Tekan MULAI → grid terkunci, pemain menonton.

Target: game jam, deadline 2 minggu, ± 4 jam playtime, roguelike + inkremental.

Dokumen desain:
- [design/gdd/grimoire-haven.md](../design/gdd/grimoire-haven.md) — GDD utama
- [design/gdd/ailments-and-reactions.md](../design/gdd/ailments-and-reactions.md) — ailment, buff, reaksi
- [design/gdd/content-plan.md](../design/gdd/content-plan.md) — target jumlah konten
- [docs/architecture/architecture.md](architecture/architecture.md) — arsitektur & rencana refactor

---

## 2. Status: bisa dimainkan

**Dua scene, dan urutannya penting di Build Settings:**

| # | Scene | Isi |
|---|---|---|
| 0 | `Assets/Scenes/MainMenu.unity` | menu utama, codex, setelan. **Digenerate**, jangan diedit tangan |
| 1 | `Assets/Scenes/Proto.unity` | game-nya |

Scene menu punya satu GameObject `_Menu` (`MainMenuController`) — MULAI memuat
`Proto` lewat **nama**, ESC di dalam run balik ke `MainMenu`.

Scene game punya satu GameObject `_Bootstrap` dengan komponen `ProtoBootstrap`
yang membangun seluruh scene saat runtime (kamera, cahaya, lantai, player, UI).
Tekan Play dari scene mana pun, dua-duanya jalan sendiri.

Keduanya butuh asset terisi di Inspector: `ContentDatabase.asset` dan
`GameBalance.asset` (di `Assets/GameData/`). Scene menu butuh `ContentDatabase`
saja, buat codex.

**Isi konten saat ini:** 29 piece (rune + skill + segel), 14 resep, 6 status,
4 reaksi, 6 buff.

---

## 3. Peta file

```text
Assets/Scripts/
├── Data/                    ScriptableObject — data murni, tanpa logika
│   ├── Enums.cs             StatKind, CastKind, CastTrigger, Layer, Element, StatModifier
│   ├── Shapes.cs            bentuk grid + Rotate() + StarText()
│   ├── PieceDefinition.cs   rune / skill / segel (SATU tipe untuk ketiganya)
│   ├── StatusDefinition.cs  ailment
│   ├── ReactionDefinition.cs  A + B -> efek, boleh memberi buff
│   ├── BuffDefinition.cs    buff pemain
│   ├── RecipeDefinition.cs  2-3 bahan -> hasil
│   ├── GameBalance.cs       SEMUA angka tuning
│   ├── SceneLook.cs         SO: matahari, ambient, kabut, warna permukaan
│   ├── RenderColor.cs       konversi sRGB -> linear (baca jebakan #8)
│   └── ContentDatabase.cs   satu-satunya pintu ke data + validasi
├── Proto/                   runtime game (masih gaya prototipe)
│   ├── ProtoBootstrap.cs    composition root sementara
│   ├── Grimoire.cs          Grimoire + Backpack + kompilasi + evolusi
│   ├── PlayerCaster.cs      cast, buff, crit, proyektil, zone, FX
│   ├── EnemyManager.cs      swarm, ailment, reaksi, damage API
│   ├── GrimoireUI.cs        SELURUH UI in-game (~2070 baris — utang teknis utama)
│   ├── DiscoveryLog.cs      codex.json — satu-satunya data lintas run
│   └── ProtoInput.cs        wrapper input (dua backend)
├── Menu/                    main menu — UGUI + TMP, komponen beneran
│   ├── MainMenuController.cs  pindah halaman, muat scene, keluar
│   ├── MenuTheme.cs         SO: SEMUA aset & warna yang bisa diganti
│   ├── GameSettings.cs      PlayerPrefs: layar, vsync, fps, volume
│   ├── CodexPanel.cs        daftar codex (di-scroll, bukan halaman)
│   ├── CodexEntry.cs        satu slot + siluet bentuk
│   ├── SettingsPanel.cs     baris setelan + reset codex
│   ├── MenuLine.cs          hover: warna + geser + penanda
│   └── MenuDiorama.cs       kamera latar yang hanyut pelan
└── Editor/                  generator asset, idempoten
    ├── MainMenuBuilder.cs   MEMBANGUN MainMenu.unity dari nol
    └── LookBuilder.cs       bikin aset look + volume profile + setel URP asset

Assets/GameData/
├── ContentDatabase.asset    daftar semua konten
├── GameBalance.asset        tuning
├── MenuTheme.asset          tampilan UI menu — ganti aset di sini
├── SceneLook_Game.asset     cahaya scene game (lantai GELAP)
├── SceneLook_Menu.asset     cahaya diorama menu (lantai terang)
├── Look/PP_GoldenHour.asset post-processing, dipakai dua-duanya
├── Menu/                    material placeholder diorama
├── Pieces/                  29 asset
├── Statuses/  Reactions/  Buffs/  Recipes/
```

### Kenapa look-nya ada dua

Sun, ambient, kabut, dan grading-nya sama persis — jadi menu dan game terasa satu sore
yang sama. Yang beda cuma **warna lantai**, dan itu disengaja: diorama menu boleh
terang karena isinya cuma pajangan, tapi lantai game harus gelap supaya musuh,
VFX spell, dan teks HUD yang terang tetap terbaca. Satu angka tidak bisa melayani
keduanya. Kalau mau menggeser terang/gelap, itu satu knob:
`GroundColor` di masing-masing aset.

### Aturan main scene menu

Layout ada di scene, **tampilan ada di `MenuTheme.asset`**. Ganti font/sprite/warna
di theme lalu jalankan ulang builder — hasil gantimu aman karena builder tidak
pernah menimpa aset yang sudah ada. Sebaliknya: **editan manual di scene menu akan
hilang** saat di-build ulang. Itu harga dari bisa digenerate.

---

## 4. Invarian — aturan yang dipegang kode

Melanggar ini akan merusak performa atau balance dengan cara yang sulit dilacak.

1. **Grid dikompilasi sekali saat berubah.** `Grimoire.Compile()` menghasilkan
   `CompiledSpell` berisi angka datar. Saat wave berjalan, **tidak ada** sistem
   yang membaca grid. Jangan pernah membaca `Grimoire.Placed` dari loop combat.
2. **Ailment = 4 slot tetap per musuh, `struct`, nol alokasi.** Jangan ganti ke
   `List`/`Dictionary`. Slot penuh → yang sisa durasinya terpendek ditimpa.
3. **Ailment pakai POIN, bukan stack biner.** `AppliedPoints` + `AilmentPoints`
   dari stat.
4. **Reaksi wajib mengkonsumsi minimal satu bahan.** Kalau tidak, dia memicu
   dirinya tiap frame. `ReactionDefinition.OnValidate()` sudah menolaknya.
5. **Status hasil penularan tidak boleh memicu reaksi** (`allowReaction: false`).
   Tanpa ini satu ledakan menjalar ke seluruh layar tanpa henti.
6. **Kunci reaksi 0.25 detik per musuh** (`EnemyManager.ReactionCooldown`).
7. **Rantai skill pemicu maksimal 3 tingkat** (`MaxTriggerDepth`).
8. **Aura rune = nilai TOTAL dibagi jumlah petaknya.** Skill yang menginjak 1
   dari 3 petak dapat sepertiganya. Ini keputusan desain, bukan bug.
9. **Buff pemain dihitung SAAT CAST**, bukan saat kompilasi grid — karena buff
   berubah di tengah wave. Lihat `BuffDamageMul`, `BuffCooldownMul`, dst.
10. **Grid terkunci selama wave.** Semua input penyusunan diblokir.
11. **Barang yang masih tercecer saat wave dimulai otomatis terjual.**
12. **Crit dilempar sekali per cast**, bukan per musuh — supaya AoE tidak jadi
    lotre.

---

## 5. Alur data

```text
ScriptableObject (Assets/GameData)
        │  disuntik lewat ProtoBootstrap
        ▼
Grimoire (model)  ──Compile()──►  CompiledSpell[] + Stats[]
        │                                  │
        │                                  ▼
        │                        PlayerCaster (cast, buff, crit)
        │                                  │
        │                                  ▼
        └───────────────────────►  EnemyManager (ailment, reaksi, damage)
                                           │
                                    event OnDamage / OnReaction / OnStatusApplied
                                           ▼
                                    GrimoireUI (meter, floater, dial)
```

Komunikasi antar sistem lewat **event C# biasa**, bukan referensi silang.
**Tidak ada singleton.** Jangan menambahkan satu pun.

---

## 6. Editor tools

Menu `Tools/Grimoire/...`. Semua idempoten (aman dijalankan ulang) **kecuali**
yang ditandai.

| Menu | Fungsi |
|---|---|
| `Generate Ailments` | bikin/update 5 status + 4 reaksi dasar |
| `Add Sample Rare Runes` | 3 rune bintang tinggi contoh |
| `Migrate Rune Auras` | **sekali pakai** — sudah dijalankan, jangan ulangi |
| `Add AoE Skills` | 5 skill AoE |
| `Generate Buffs, Seret & Recipes` | 6 buff, status SERET, skill Pusaran, 7 resep |
| `Build Main Menu` | bangun ulang `MainMenu.unity` + daftarkan dua scene ke Build Settings. Aman diulang, tapi **editan manual di scene itu hilang** |
| `Build Scene Look` | bikin dua aset SceneLook + volume profile, setel HDR grading & MSAA di URP asset, dan pasang look ke `ProtoBootstrap`. **Create-only** — aset yang sudah kamu setel tidak pernah ditimpa |

`Migrate Rune Auras` punya penjaga (`AuraValue >= 0.9f` dilewati), tapi tetap
jangan dijalankan tanpa alasan.

---

## 7. Jebakan yang sudah memakan waktu

Ini semua sudah pernah terjadi di sesi sebelumnya. Jangan mengulangi.

1. **MenuItem tidak terdaftar saat Play mode.** `execute_menu_item` akan gagal
   dengan "there is no menu named ...". **Selalu `manage_editor stop` dulu**
   sebelum menjalankan menu Editor.
2. **Urutan inisialisasi static field C#.** Field static yang membaca field
   static lain yang **dideklarasikan di bawahnya** akan dapat `null`. Ini pernah
   melumpuhkan seluruh UI (`DroppablePool` membaca `All`). Solusi: lazy property.
3. **Mengganti nama field serialized = data hilang.** Wajib pakai
   `[FormerlySerializedAs("NamaLama")]`. Dan itu pun **tidak selalu terbaca**
   kalau asset belum di-reimport — migrasi enum lama ke referensi SO akhirnya
   dilakukan lewat peta id eksplisit, bukan mengandalkan field lama.
4. **Screenshot MCP kadang gagal** dengan "PlayerLoop called recursively" lalu
   jatuh ke render kamera langsung — hasilnya **UI overlay tidak ikut terfoto**.
   Itu bukan bug game. Ambil ulang.
5. **Cooldown yang berputar padahal tidak menembak.** Dulu skill tanpa target
   di-retry 0.15 detik sehingga dial terlihat berputar terus. Sekarang: kalau
   tidak bisa menembak, timer **berhenti di posisi siap**. Jangan kembalikan.
6. **Nova yang meledak ke ruang kosong** menghabiskan mana. Sekarang nova
   memeriksa dulu ada musuh dalam radius.
7. **Klik/keypress nyangkut saat masuk Play mode** pernah memulai wave sendiri.
   Ada kunci input 0.4 detik di awal (`_inputLock`). Jangan dihapus.
8. **Project ini Linear color space, dan warna yang diset dari SCRIPT dipakai
   mentah sebagai nilai linear** — material, camera, maupun UGUI. Akibatnya warna
   sRGB yang kamu tulis tampil jauh lebih pucat. `MainMenuBuilder.Rendered()`
   mengonversinya. Kalau bikin UI baru dari kode, lakukan hal yang sama.
9. **EventSystem wajib `InputSystemUIInputModule`, bukan `StandaloneInputModule`.**
   Project ini `activeInputHandler: 1` (Input System baru). Kalau salah modul,
   tombol tidak mati dengan pesan error — dia cuma **diam**.
10. **Menguji tombol dengan `onClick.Invoke()` tidak membuktikan apa pun.** Itu
    melewati raycast. Uji dengan `EventSystem.RaycastAll` di posisi tombolnya.
11. **`LoadScene` selalu pakai NAMA, jangan build index.** Versi lama memuat ulang
    lewat `buildIndex` padahal `Proto.unity` belum terdaftar di Build Settings —
    jadi tombol restart itu sebenarnya sudah rusak sejak lama.
12. **Warna lampu & ambient JANGAN dikonversi** meski aturan #8 bilang konversi.
    `GraphicsSettings.lightsUseLinearIntensity` menyala, jadi Unity sudah
    melakukannya sendiri — konversi kedua bikin gelap dua kali.
13. **Lantai datar cuma menerima `sin(SunPitch)` dari matahari.** Di pitch 25
    itu 0.42 — warna yang di color picker terlihat sedang akan tampil jauh lebih
    gelap dari dugaan. Naikkan sudutnya, jangan cuma terus menaikkan albedo.
14. **Contrast & vignette di volume profile jatuh ke LANTAI**, karena lantailah
    yang memenuhi layar. Nilai yang wajar untuk scene biasa (contrast 10,
    vignette 0.28) di sini menelan lantainya bulat-bulat.
15. **Dua jalur screenshot MCP tidak sepakat soal kecerahan.** Jalur camera-render
    dan jalur game-view memberi hasil terang yang berbeda untuk scene yang sama.
    Jangan pernah menyetel warna hanya dari screenshot — buka Game view.

---

## 8. JANGAN lakukan

Penting kalau agent berjalan tanpa konfirmasi izin.

- **Jangan hapus atau regenerasi ulang `Assets/GameData/`.** Isinya sudah
  disetel tangan sebagian. Generator hanya boleh menambah/memperbarui.
- **Jangan jalankan `Migrate Rune Auras` lagi.**
- **Jangan commit** kecuali diminta. Kalau diminta: Conventional Commits,
  **tanpa** baris atribusi Claude/Co-Authored-By.
- **Jangan tambahkan singleton, `FindObjectOfType`, atau `Resources.Load`.**
- **Jangan hardcode angka gameplay.** Semua ke `GameBalance` atau asset piece.
- **Jangan optimasi performa dulu.** Musuh masih dibatasi 200 dan belum jadi
  bottleneck. Profiler dulu, baru ubah.
- **Jangan refactor `GrimoireUI.cs` diam-diam** — desainnya masih bergerak,
  merapikan sekarang berarti merapikan dua kali.
- **Jangan mengedit `MainMenu.unity` langsung.** Ubah `MenuTheme.asset` atau
  `MainMenuBuilder.cs`, lalu build ulang. Editan tangan di scene itu akan hilang.

---

## 9. Sudah selesai

Grid dua lapis 7×7 · rune (bentuk, elemen, aura terbagi per petak, stat, rarity)
· skill (pemicu cooldown & ambang ailment) · segel · tas 4×3 · barang tercecer ·
kotak jual · toko sebagai event tiap 3 wave + reroll menanjak yang tidak pernah
reset · resep 2–3 bahan + garis biru/emas + kunci · 6 ailment berbasis poin ·
4 reaksi · 6 buff pemain yang dihadiahkan reaksi · SERET (tarikan) · mana &
regen · Defense · crit · range · 3 bentuk AoE (AreaAtTarget, Line, Zone) ·
damage meter · lingkaran cooldown + denyut · tooltip · ALT+hover resep ·
kecepatan 1–5× · seluruh data di ScriptableObject ·
**codex** (siluet `???`, persisten lintas run lewat `codex.json`) ·
**main menu** (MULAI / CODEX / SETELAN / KELUAR, TMP + UGUI) ·
**setelan** (layar, resolusi, vsync, batas FPS, 3 volume, reset codex).

## 10. Belum selesai — urutan yang disarankan

| # | Item | Kenapa |
|---|---|---|
| 1 | **Varian musuh** | sekarang cuma 1 jenis. **Sengaja ditunda** oleh pemilik project — ini varian, bukan sistem |
| 2 | **Save/persistensi run** | codex sudah persisten; progres run belum |
| 3 | **Sistem audio** | slider volume sudah ada tapi belum menggerakkan apa pun |
| 4 | **Navigasi keyboard/gamepad di menu** | sekarang mouse + ESC saja. `MenuLine` sudah merespons select, tinggal state fokus |
| 5 | **Refactor Tahap 2–3** | pecah `GrimoireUI` (~2070 baris) jadi model/sistem/view, lalu `GameRoot` + event |
| 6 | **Performa** | spatial hash + instancing. Baru relevan setelah musuh benar-benar 200 |

Detail rencana refactor ada di
[architecture.md](architecture/architecture.md) bagian "Rencana migrasi bertahap".

---

## 11. Konvensi kerja

- **Bahasa ke user: Indonesia santai.** Istilah teknis tetap Inggris.
- **Komentar kode: Inggris**, dan hanya untuk menjelaskan *kenapa*, bukan *apa*.
- **Investigasi dulu sebelum mengubah** yang sudah jalan.
- **Patch kecil dan bisa di-revert sendiri-sendiri.** Hindari "sekalian rapihin".
- Sebelum menulis file, **tanya dulu** (lihat `CLAUDE.md` project).
- Setelah mengubah script: `refresh_unity` → `read_console` cek error →
  baru `manage_editor play`.
