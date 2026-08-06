# Handoff untuk agent AI

Dokumen ini ditulis supaya agent lain bisa lanjut kerja tanpa harus menebak.
Baca ini **sebelum** menyentuh kode. Terakhir diperbarui: sesi 2026-08-06
(auto-move, rendering instanced, wave berbasis jam, konten 70 piece).

---

## 1. Ini game apa

**Grimoire Haven** — bullet haven top-down 3D, Unity 6.3 LTS (URP), C#.

Pemain **tidak dikendalikan**. Yang dikendalikan cuma isi buku sihirnya. Musuh
datang bergelombang; di antara gelombang waktu berhenti dan pemain menyusun grid:
rune sebagai alas, skill dan segel di atasnya, bahan resep didempetkan agar
berevolusi. Tekan MULAI → grid terkunci, pemain menonton.

Sejak sesi 2026-08-06 pemain **bergerak sendiri** menghindari kerumunan. Itu tidak
melanggar premisnya: kamu tetap tidak menyetir. Gerak jadi bagian dari build
(`StatKind.MoveSpeed`), bukan sesuatu yang dihapus.

Arah yang diminta pemilik project: **rusuh, semuanya meledak, dikeroyok banyak
orang** — rasa Vampire Survivors, tapi tetap dengan fase menyusun antar wave.
Rencana jangka menengah: boss, animasi baked, banyak VFX.

Dokumen desain:
- [design/gdd/grimoire-haven.md](../design/gdd/grimoire-haven.md) — GDD utama
- [design/gdd/ailments-and-reactions.md](../design/gdd/ailments-and-reactions.md) — ailment, buff, reaksi
- [design/gdd/content-plan.md](../design/gdd/content-plan.md) — target jumlah konten (**sudah usang**, lihat §10)
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

**Isi konten saat ini:** **70 piece**, **71 resep**, 7 status, 9 reaksi, 6 buff,
70 ikon placeholder.

Piramida rarity — ini disengaja, bukan kebetulan. Cuma piece ★1 yang pernah
nge-drop dan cuma ★1–★2 yang masuk toko, jadi alas yang lebar itulah yang bikin
jaring resepnya lebar:

| ★ | Rune | Segel | Skill | Total |
|---|---|---|---|---|
| 1 | 6 | 7 | 19 | 32 |
| 2 | 4 | 3 | 13 | 20 |
| 3 | 2 | — | 8 | 10 |
| 4 | — | — | 4 | 4 |
| 5 | — | — | 4 | 4 |

---

## 3. Peta file

```text
Assets/Scripts/
├── Data/                    ScriptableObject — data murni, tanpa logika
│   ├── Enums.cs             StatKind, CastKind, CastTrigger, Layer, Element, StatModifier
│   ├── Shapes.cs            16 bentuk grid + Rotate() + NameOf() + StarText()
│   ├── PieceDefinition.cs   rune / skill / segel (SATU tipe untuk ketiganya) + Icon
│   ├── StatusDefinition.cs  ailment
│   ├── ReactionDefinition.cs  A + B -> efek, boleh memberi buff
│   ├── BuffDefinition.cs    buff pemain
│   ├── RecipeDefinition.cs  2-3 bahan -> hasil
│   ├── GameBalance.cs       SEMUA angka tuning + kurva wave & laju spawn
│   ├── SceneLook.cs         SO: matahari, ambient, kabut, warna permukaan
│   ├── RenderColor.cs       konversi sRGB -> linear (baca jebakan #8)
│   └── ContentDatabase.cs   satu-satunya pintu ke data + validasi
├── Model/                   C# biasa, state satu run
│   ├── Grimoire.cs          Grimoire + Backpack(5x4) + kompilasi + evolusi
│   └── DiscoveryLog.cs      codex.json — satu-satunya data lintas run
├── Systems/                 logika runtime
│   ├── PlayerCaster.cs      cast, buff, crit, proyektil, descent, zone, FX
│   ├── PlayerMotor.cs       gerak otomatis menghindar + batas arena
│   ├── EnemyManager.cs      swarm, spatial hash, ailment, reaksi, damage API
│   ├── DamageMeter.cs       akumulasi damage per sumber sepanjang run
│   └── ProtoInput.cs        wrapper input (dua backend)
├── View/                    presentasi
│   ├── GrimoireUI.cs        UI in-game (~1940 baris — masih utang, lihat §12)
│   ├── GrimoireLayout.cs    SEMUA ukuran piksel & rect, statik murni
│   ├── TooltipBuilder.cs    kartu stat hover (resep sudah pindah)
│   ├── RecipePanel.cs       kartu resep ALT — ikon, hasil kiri, formula kanan
│   ├── DamagePopups.cs      angka damage melayang, hit berdekatan digabung
│   ├── EnemyRenderer.cs     gambar seluruh swarm instanced, TANPA GameObject
│   ├── BoltPool.cs          LineRenderer pool — petir rantai & sinar garis
│   └── CameraShake.cs       shake berbasis trauma
├── Composition/
│   └── ProtoBootstrap.cs    composition root (merakit seluruh scene game)
├── Menu/                    main menu — UGUI + TMP, komponen beneran
└── Editor/                  generator asset, idempoten

Assets/GameData/
├── ContentDatabase.asset    daftar semua konten
├── GameBalance.asset        tuning
├── MenuTheme.asset          tampilan UI menu
├── SceneLook_Game.asset     cahaya scene game (lantai GELAP)
├── SceneLook_Menu.asset     cahaya diorama menu (lantai terang)
├── Icons/                   70 PNG placeholder — TIMPA filenya buat ganti art
├── Pieces/  Statuses/  Reactions/  Buffs/  Recipes/
```

---

## 4. Invarian — aturan yang dipegang kode

Melanggar ini akan merusak performa atau balance dengan cara yang sulit dilacak.

1. **Grid dikompilasi sekali saat berubah.** `Grimoire.Compile()` menghasilkan
   `CompiledSpell` berisi angka datar. Saat wave berjalan, **tidak ada** sistem
   yang membaca grid.
2. **Musuh TIDAK punya GameObject.** `Enemy` cuma data (`Pos`, `Yaw`, `Phase`,
   `Flank`, `Tint`). Seluruh swarm digambar `EnemyRenderer` lewat
   `Graphics.RenderMeshInstanced`. Konsekuensinya permanen: **tidak akan pernah
   ada Animator atau SkinnedMeshRenderer per musuh.** Animasi harus baked.
3. **Warna musuh itu BUCKET, bukan nilai per-instance.** `Paint()` menulis
   `e.Tint` (indeks palet), renderer mengelompokkan. Jangan kembalikan
   `MaterialPropertyBlock` per musuh — itu yang dulu bikin 200 musuh = 200 draw call.
4. **Ailment = 4 slot tetap per musuh, `struct`, nol alokasi.** Slot penuh →
   yang sisa durasinya terpendek ditimpa.
5. **Ailment pakai POIN, bukan stack biner.**
6. **Reaksi wajib mengkonsumsi minimal satu bahan.** Kalau tidak, dia memicu
   dirinya tiap frame. `ReactionDefinition.OnValidate()` sudah menolaknya.
7. **Status hasil penularan tidak boleh memicu reaksi** (`allowReaction: false`).
8. **Kunci reaksi 0.25 detik per musuh** (`EnemyManager.ReactionCooldown`).
9. **Rantai skill pemicu maksimal 3 tingkat** (`MaxTriggerDepth`).
10. **Aura rune = nilai TOTAL dibagi jumlah petaknya.** Skill yang menginjak 1
    dari 3 petak dapat sepertiganya. Keputusan desain, bukan bug.
11. **Buff pemain dihitung SAAT CAST**, bukan saat kompilasi grid.
    **Termasuk Nova** — dulu tidak, dan itu diam-diam mengeluarkan skill terberat
    di game (sampai Doom Nova) dari loop reaksi→buff→pukul lebih keras.
12. **Crit dilempar sekali per cast**, bukan per musuh. Untuk skill yang jatuh
    dari langit, crit dilempar **saat cast** lalu dibawa turun bersama peluru.
13. **Grid terkunci selama wave.**
14. **Barang yang masih tercecer saat wave dimulai otomatis terjual.**
15. **Drop dari kill DITAHAN sampai wave beres**, lalu ditumpahkan sekaligus.
    Dibatasi `MaxDropsPerWave`; kelebihannya jadi koin.
16. **Syarat evolusi = BERSENTUHAN, bukan segaris.** `Grimoire.FormsCluster`
    memakai flood fill 4 arah.
17. **Segel WAJIB `Kind = CastKind.Passive`.** `Grimoire.Compile` menyaring spell
    lewat `Kind`, bukan lewat damage. Segel dengan Kind lain akan ikut di-cast
    dengan cooldown 0.05 detik.
18. **Stat piece hanya boleh di `Stats[]`.** Field `Stat`/`StatValue` warisan
    prototipe sudah dikosongkan — mengisi yang lama bikin statnya dobel.
19. **Rune tidak bisa jadi BAHAN resep**, cuma bisa jadi HASIL. Model cuma
    menerima piece layer-Skill sebagai bahan. Karena itu rune ★2/★3 dibuat lewat
    peleburan **segel** — itu satu-satunya jalur yang sah.
20. **Wave selesai karena JAM HABIS, bukan karena lapangan bersih.** Musuh yang
    tersisa disapu, dan **tidak membayar apa-apa** (tidak masuk hitungan kill,
    tidak roll drop). Kalau membayar, strategi terbaiknya jadi berhenti nembak.
21. **Bentuk piece terikat rarity.** ★1 = 2–3 petak, ★2 = 4, ★3 = 5, ★4 = 6–7,
    ★5 = 8–9. Semua muat dalam kotak 3×3 — itu batas keras, lihat jebakan #18.
22. **`ShapeKind` dan `StatKind` cuma boleh DITAMBAH di belakang.** Nilainya
    terserialisasi di tiap aset piece; menyisipkan di tengah mengubah bentuk dan
    stat separuh konten diam-diam.

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
                          event OnDamage / OnEnemyDamaged / OnReaction
                          / OnStatusApplied / OnKill / OnSweep
                                           ▼
                          GrimoireUI (meter, floater, popup, dial)
                          PlayerCaster (ledakan mati)
                          EnemyRenderer (gambar swarm)
```

Komunikasi antar sistem lewat **event C# biasa**, bukan referensi silang.
**Tidak ada singleton.** Jangan menambahkan satu pun.

---

## 6. Editor tools

Menu `Tools/Grimoire/...`. Semua idempoten (aman dijalankan ulang) **kecuali**
yang ditandai. Semua mencocokkan lewat **Id aset**, bukan nama tampilan.

| Menu | Fungsi |
|---|---|
| `Generate Ailments` | 5 status + 4 reaksi dasar |
| `Add Sample Rare Runes` | 3 rune bintang tinggi contoh |
| `Migrate Rune Auras` | **sekali pakai** — sudah dijalankan, jangan ulangi |
| `Add AoE Skills` | 5 skill AoE |
| `Generate Buffs, Seret & Recipes` | 6 buff, status DRAG, skill Vortex, 7 resep |
| `Build Main Menu` | bangun ulang `MainMenu.unity` + daftarkan dua scene. **Editan manual di scene itu hilang** |
| `Build Scene Look` | dua aset SceneLook + volume profile + setel URP asset. **Create-only** |
| `Generate Reactions & Stun` | status STUN + 5 reaksi baru |
| `English Naming + Balance Pass` | nama konten ke Inggris, skala damage reaksi per HP musuh, resep piece yatim |
| `Rebalance + Star 5 Tier` | tabel mana/cooldown 15 skill, Meteor jadi jatuh dari langit, 3 piece ★5 + resepnya |
| `Footprint by Rarity` | pasang tangga bentuk per bintang + tulis ulang blurb |
| `Expand Content to 70` | 38 piece baru + 48 resep baru, matriks 4 elemen × 6 arketipe, MoveSpeed ke 3 piece |
| `Generate Placeholder Icons` | PNG 64×64 per piece. **Create-only** — art buatanmu aman |
| `Regenerate Placeholder Icons (TIMPA art)` | **MERUSAK** — tulis ulang semua PNG. Pakai setelah bentuk piece berubah |

**Urutan kalau membangun dari nol:** `Generate Ailments` → `Add AoE Skills` →
`Generate Buffs...` → `Generate Reactions & Stun` → `English Naming + Balance Pass`
→ `Rebalance + Star 5 Tier` → `Expand Content to 70` → `Footprint by Rarity` →
`Generate Placeholder Icons`.

---

## 7. Jebakan yang sudah memakan waktu

Ini semua sudah pernah terjadi. Jangan mengulangi.

1. **MenuItem tidak terdaftar saat Play mode.** Selalu `manage_editor stop` dulu
   sebelum menjalankan menu Editor.
2. **Unity TIDAK recompile selama Play mode.** `refresh_unity` saat play mode
   aktif akan sukses tapi kode lamanya yang jalan — dan pengukuranmu bohong.
   Stop → refresh → play.
3. **`refresh_unity` dengan `scope: scripts` tidak mengimpor FILE BARU.** File
   `.cs` baru butuh `scope: all` + `mode: force`, kalau tidak muncul error
   "type could not be found" untuk kelas yang jelas-jelas ada.
4. **Screenshot MCP kadang gagal** dengan "PlayerLoop called recursively" lalu
   jatuh ke render kamera manual. Dulu efeknya cuma UI overlay hilang. **Sejak
   musuh digambar instanced, SELURUH SWARM ikut hilang dari foto** — render
   kamera manual ada di luar loop render normal, dan `RenderMeshInstanced`
   submit-nya per-frame ke loop itu. Layar kosong padahal 500 musuh hidup.
   **Itu bukan bug game. Bersihkan console, ambil ulang.**
5. **`UnityEditor.UnityStats` juga tidak bisa dipercaya lewat MCP.** Pernah
   melaporkan 6.439 triangle yang identik sampai digit terakhir baik saat 0
   maupun 309 musuh hidup. Jangan pakai untuk membuktikan sesuatu terender.
6. **Urutan inisialisasi static field C#.** Field static yang membaca field
   static lain yang dideklarasikan di bawahnya akan dapat `null`.
7. **Mengganti nama field serialized = data hilang.** Wajib
   `[FormerlySerializedAs("NamaLama")]`.
8. **Project ini Linear color space, dan warna yang diset dari SCRIPT dipakai
   mentah sebagai nilai linear.** `MainMenuBuilder.Rendered()` mengonversinya.
   Warna di PNG ikon **tidak** kena ini — importer menandainya sRGB, jadi warna
   yang dipilih itulah yang tampil.
9. **EventSystem wajib `InputSystemUIInputModule`.** Kalau salah modul, tombol
   tidak mati dengan pesan error — dia cuma **diam**.
10. **Menguji tombol dengan `onClick.Invoke()` tidak membuktikan apa pun.**
11. **`LoadScene` selalu pakai NAMA, jangan build index.**
12. **Warna lampu & ambient JANGAN dikonversi** meski aturan #8 bilang konversi.
13. **Lantai datar cuma menerima `sin(SunPitch)` dari matahari.**
14. **Contrast & vignette di volume profile jatuh ke LANTAI.**
15. **Bahasa konten = INGGRIS.** Teks UI in-game masih Indonesia — utang terpisah.
16. **Damage sentuh musuh MENUMPUK per musuh.** `EnemyContactDps` dikenakan tiap
    musuh yang menempel. Sekarang pemain bisa dikepung 180 musuh sekaligus, jadi
    menaikkan angka ini sedikit saja langsung mematikan.
17. **URP/Unlit mengabaikan vertex color LineRenderer.** Pakai material
    `BoltPool` (`Sprites/Default`) untuk apa pun yang butuh garis berwarna atau
    memudar.
18. **Semua bentuk piece WAJIB muat dalam kotak 3×3.** Siluet codex grid tetap
    3×3, tas 5×4, dan pool sel gambar dijatah 9 sel per piece. Bentuk 4 petak
    lebar menjebol ketiganya diam-diam.
19. **Spatial hash tidak menolong kalau semua orang ada di satu sel.** Percobaan
    memakai hash untuk `BestCluster` justru membuatnya **lebih lambat** (1,95 ms
    → 4,85 ms): begitu wave berkumpul, seluruh swarm berdiri di dalam satu radius
    ledakan. Solusinya bukan indeks yang lebih baik, tapi **berhenti bertanya ke
    musuh** — skor sel grid-nya.
20. **Dua resep dengan set bahan yang sama = salah satunya mati diam-diam.**
    `ContentExpansionPass` punya validasi untuk ini; jalankan kalau menambah resep.

---

## 8. JANGAN lakukan

- **Jangan hapus atau regenerasi ulang `Assets/GameData/`.** Generator hanya boleh
  menambah/memperbarui.
- **Jangan jalankan `Migrate Rune Auras` lagi.**
- **Jangan jalankan `Regenerate Placeholder Icons (TIMPA art)`** kecuali bentuk
  piece memang baru berubah.
- **Jangan commit** kecuali diminta. Kalau diminta: Conventional Commits,
  **tanpa** baris atribusi Claude/Co-Authored-By.
- **Jangan tambahkan singleton, `FindObjectOfType`, atau `Resources.Load`** di
  kode runtime.
- **Jangan hardcode angka gameplay.** Semua ke `GameBalance` atau asset piece.
- **Jangan kembalikan GameObject per musuh.**
- **Jangan mengedit `MainMenu.unity` langsung.**
- **Jangan pindah ke ECS tanpa alasan yang terukur.** Sudah diprofilkan sesi ini:
  500 musuh jalan 59 fps dengan 1 draw call. Hambatan berikutnya ada di FX
  `PlayerCaster` (masih GameObject satu-satu), bukan di swarm.

---

## 9. Sudah selesai

Grid dua lapis 7×7 · tas 5×4 · rune/skill/segel dengan bentuk terikat rarity ·
70 piece, 71 resep, piramida ★1–★5 · 7 ailment berbasis poin · 9 reaksi · 6 buff ·
mana & regen · Defense · crit · 3 bentuk AoE · damage meter · tooltip · codex ·
main menu · setelan · camera shake · bar HP/mana beranimasi ·
**gerak otomatis pemain** (arena elips, `MoveSpeed` sebagai stat) ·
**musuh mengepung** (gaya pisah + bidik posisi depan + jalur serong) ·
**wave berbasis jam** dengan spawn mengalir dan sapu bersih di akhir ·
**rendering instanced** (500 musuh, 1 draw call) ·
**angka damage melayang** yang menggabung hit berdekatan ·
**proyektil**: jatuh dari langit, petir melompat, sinar garis, ledakan mati musuh ·
**garis evolusi** yang menghubungkan bahan · **kartu resep berikon** ·
70 ikon placeholder.

## 10. Belum selesai — urutan yang disarankan

| # | Item | Kenapa |
|---|---|---|
| 1 | **Main dan nilai rasanya** | semua verifikasi sesi ini programatik. Kurva wave, tekanan grid, lompatan bintang, rasa auto-move — empat hal itu belum pernah dinilai tangan manusia |
| 2 | **Varian musuh + boss** | diminta pemilik project. `SpawnOne()` sudah jadi satu-satunya tempat stat per musuh diisi — boss = pengisian berbeda di method yang sama |
| 3 | **Animasi baked (VAT)** | jahitannya sudah ada: `Yaw`, `Phase`, dan `EnemyRenderer.Compose()`. Yang belum: shader VAT + tool bake-nya |
| 4 | **Optimasi FX `PlayerCaster`** | Projectile/Flash/Descent/Zone masih GameObject primitif satu-satu. Ini hambatan berikutnya begitu VFX masuk |
| 5 | **Jalur Lightning ★2–★3 sudah ada, tapi cek keseimbangan elemen** | Fire/Ice/Arcane/Lightning kini keempatnya sampai ★5 |
| 6 | **Sistem audio** | slider volume ada tapi belum menggerakkan apa pun |
| 7 | **Save/persistensi run** | codex sudah persisten; progres run belum |
| 8 | **Navigasi keyboard/gamepad di menu** |
| 9 | **Refactor `GrimoireUI`** | ~1940 baris, lihat §12 |
| 10 | **`design/gdd/content-plan.md` usang** | targetnya masih angka lama (15–23 skill); isi sekarang 70 piece |

---

## 11. Konvensi kerja

- **Bahasa ke user: Indonesia santai.** Istilah teknis tetap Inggris.
- **Komentar kode: Inggris**, dan hanya untuk menjelaskan *kenapa*, bukan *apa*.
- **Investigasi dulu sebelum mengubah** yang sudah jalan.
- **Ukur, jangan tebak.** Semua klaim performa di dokumen ini punya angka sebelum
  dan sesudah. Kalau tidak bisa diukur, bilang belum diukur.
- **Patch kecil dan bisa di-revert sendiri-sendiri.**
- Sebelum menulis file, **tanya dulu** (lihat `CLAUDE.md` project).
- Setelah mengubah script: **stop play mode** → `refresh_unity` (`scope: all`,
  `mode: force`) → `read_console` cek error → baru `manage_editor play`.

---

## 12. Antrean refactor `GrimoireUI.cs`

Sudah dikeluarkan (semuanya compile + wave jalan setelah tiap langkah):

| Ke mana | Isi |
|---|---|
| `View/GrimoireLayout.cs` | semua const piksel + rect/anchor statik |
| `View/TooltipBuilder.cs` | teks hover |
| `View/RecipePanel.cs` | kartu resep ALT berikon |
| `View/DamagePopups.cs` | angka damage melayang |
| `Systems/DamageMeter.cs` | akumulasi damage per sumber |

**Sisa antrean, urut dari yang paling gampang:**

1. **Toko** — `_shop`, `_rerollCost`, `RollShop`, cabang beli di `HandlePanelClick`,
   bagian toko di `DrawPanels` → `Systems/ShopSystem.cs` + panel view terpisah.
2. **Barang tercecer** — `AddLoose`/`RemoveLoose`/`ScatterAll`/`SellLoose`/
   `ScreenToLoose`/`RouteDrop`/`AutoPlace*` → `Model/LootPile.cs` + view.
3. **Pabrik widget** — `MakeImage`/`MakeText`/`MakeCircleSprite` → `View/UiFactory.cs`.
4. **`GameRoot`** — ganti nama `ProtoBootstrap` kalau mau lepas dari nama "Proto".

Aturan yang dipakai sejauh ini: **satu blok per patch**, stop → `refresh_unity` →
`read_console` → jalankan satu wave, baru lanjut blok berikutnya.

### Utang bahasa yang tersisa

Nama konten (piece, status, reaksi, buff) **sudah Inggris semua**. Yang **belum**:
teks UI in-game di `View/GrimoireUI.cs` (`MULAI WAVE`, `JUAL`, `TAS`, banner,
tooltip) dan `Menu/`. Sebaiknya dikerjakan sekalian saat `GrimoireUI` dipecah.
