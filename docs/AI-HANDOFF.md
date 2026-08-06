# Handoff untuk agent AI

Dokumen ini ditulis supaya agent lain bisa lanjut kerja tanpa harus menebak.
Baca ini **sebelum** menyentuh kode. Terakhir diperbarui: sesi 2026-08-06
(skill non-serangan, kamera dead-zone, drop jadi benda nyata, solver keseimbangan —
**§13**; tes build ★5 penuh + bug anggaran mana — **§14**; konten 100 piece).

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

**Isi konten saat ini:** **100 piece**, **101 resep**, 7 status, 9 reaksi,
**10 buff**, **4 kutukan**, **4 arketipe musuh**, ikon placeholder lengkap
(0 piece tanpa ikon).

Piramida rarity — ini disengaja, bukan kebetulan. Cuma piece ★1 yang pernah
nge-drop dan cuma ★1–★2 yang masuk toko, jadi alas yang lebar itulah yang bikin
jaring resepnya lebar:

| ★ | Rune | Segel | Skill | Total |
|---|---|---|---|---|
| 1 | 6 | 8 | 20 | 34 |
| 2 | 4 | 4 | 14 | 22 |
| 3 | 2 | — | 8 | 10 |
| 4 | — | — | 4 | 4 |
| 5 | — | — | 4 | 4 |

### Varian musuh

Empat arketipe (`Assets/GameData/Enemies/`), masing-masing menanyakan pertanyaan
berbeda ke build — satu jenis musuh cuma pernah bertanya "damage-mu cukup nggak".

| Musuh | Dari wave | Ciri | Jawabannya |
|---|---|---|---|
| **Grunt** | 1 | biasa | apa saja |
| **Cursed** | 3 | besar, lambat, HP ×1,6, menempelkan kutukan saat menyentuh | bunuh duluan, atau `DebuffResist` |
| **Stalker** | 5 | **terbang**, cepat ×1,55, HP ×0,5, menukik lurus (tidak ikut mengepung) | AoE apa pun — tapi dia tiba duluan |
| **Spitter** | 7 | berhenti di jarak 11 dan **menembak** | **JANGKAUAN.** Ini satu-satunya musuh yang tidak bisa disentuh build pendek selama wave hidup |

Campurannya bergeser: wave 1 = 100% Grunt, wave 10 = 55/16/18/10, wave 20 =
37/20/25/18.

### Kutukan

Musuh bisa menempelkan efek negatif ke pemain. Masing-masing menyerang satu jenis
build, jadi apa pun yang jadi tumpuan run-mu, ada satu yang menyakitinya.

| Kutukan | Efek | Menyakiti |
|---|---|---|
| WEAKENED | damage −30% | build damage |
| SLUGGISH | cooldown +35% | build CDR |
| LEADEN | kecepatan −1,5 | build gesit — tidak bisa kabur dari kerumunan |
| DRAINED | mana regen −7, biaya +30% | build mana |

Penangkalnya: Ward Sigil ★1 → Purifier Sigil ★2 (pasif, memotong durasi), dan
Cleansing Light ★1 → Cleansing Dawn ★2 (`CastKind.Cleanse`, membuang semuanya).

---

## 3. Peta file

```text
Assets/Scripts/
├── Data/                    ScriptableObject — data murni, tanpa logika
│   ├── Enums.cs             StatKind, CastKind, CastTrigger, Layer, Element, StatModifier
│   ├── Shapes.cs            16 bentuk grid + Rotate() + NameOf() + StarText()
│   ├── PieceDefinition.cs   rune / skill / segel (SATU tipe untuk ketiganya) + Icon
│   ├── EnemyArchetype.cs    jenis musuh: tubuh, perilaku, tembakan, kutukan
│   ├── BuffDefinition.cs    buff pemain — juga dipakai untuk kutukan (Mods negatif)
│   ├── StatusDefinition.cs  ailment
│   ├── ReactionDefinition.cs  A + B -> efek, boleh memberi buff
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
│   ├── StatusStrip.cs       baris ikon + angka + hover (buff / kutukan / ailment)
│   ├── DamagePopups.cs      angka damage melayang, hit berdekatan digabung
│   ├── EnemyRenderer.cs     gambar instanced — dipakai 2x: musuh & peluru musuh
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
├── Icons/                   74 PNG placeholder — TIMPA filenya buat ganti art
├── Enemies/                 4 arketipe musuh
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
20. **Jam mengatur SPAWN, bukan wave.** Wave tetap selesai dengan menghabiskan
    lapangan. Begitu jendela spawn tutup (`Closing`), sisa musuh **ngebut 1,9×**
    dan **berhenti memutar** — itulah yang membunuh ekor mati tanpa harus
    menghapus musuh. Menghapus musuh terbaca sebagai game merampok kill-mu.
    `ClosingTimeout` cuma jaring pengaman untuk build tanpa damage sama sekali.
21. **Semua arketipe meninggalkan `PreferredRange` saat `Closing`.** Tanpa ini
    Spitter membuat build berjangkauan pendek tidak akan pernah bisa menutup wave,
    dan timeout darurat akan meletus di setiap ronde.
22. **Debuff punya 4 slot SENDIRI, terpisah dari 6 slot buff.** Kalau berbagi,
    saat dikepung dan terus dikutuk, buff hasil reaksi ketendang keluar — loop inti
    game mati justru saat pemain paling terdesak.
23. **`Separation()` dibatasi panjang 1.** Mentahnya, jumlah 14 tetangga bisa
    bermagnitude belasan dan menelan setiap suku kemudi lain yang dijumlahkan
    dengannya — Spitter yang mencoba jaga jarak malah terdorong masuk ke melee.
    Bobotnya diatur lewat `SeparationWeight`, bukan lewat magnitude mentah.
24. **Damage sesaat pakai `TakeHit`, bukan `TakeDamage`.** `TakeDamage` mengurangi
    Defense dikali `Time.deltaTime` — benar untuk sentuhan per-frame, dan hampir
    nol terhadap satu tembakan.
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
| `Generate Curses and Counters` | 4 kutukan + 4 piece penangkal + resepnya |
| `Generate Enemy Archetypes` | 4 arketipe musuh + laporan campuran per wave |
| `Generate Heroes` | loadout awal. Melaporkan balik apakah skill pembuka bersentuhan — kalau YA, dia akan melebur sendiri dan pilihannya hilang |
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
21. **`&` di path `[MenuItem]` adalah modifier Alt untuk shortcut.** Menu dengan
    `&` di namanya tidak bisa dipanggil lewat nama sama sekali. Tulis "and".
22. **Mengubah ScriptableObject saat PLAY MODE tidak kembali sendiri saat stop.**
    Beda dengan objek di scene. Menyetel `bal.CurseChanceBase = 0.6f` untuk tes
    akan **tetap begitu di memori** setelah keluar play mode, dan `SaveAssets`
    berikutnya — dari generator mana pun — menuliskannya sebagai nilai desain.
    Yang bikin ini licin: **`AssetDatabase.ImportAsset` TIDAK memuat ulang instance
    yang sudah ada**, jadi memaksa reimport bukan cara membatalkannya, dan disk
    yang masih benar bukan bukti kamu aman. Cara membatalkan yang benar: set balik
    nilainya secara eksplisit, `SetDirty`, `SaveAssets`, lalu **verifikasi lewat
    `git diff`** bukan lewat pembacaan runtime.
23. **`refresh_unity` bisa balik `success` padahal `EditorApplication.isCompiling`
    masih true.** Type yang baru ditambahkan belum ada dan menu-nya belum terdaftar.
    Cek `isCompiling` lewat `execute_code`, jangan percaya nilai baliknya saja.
24. **Masuk play mode tepat setelah menjalankan editor pass bisa memakai data BASI.**
    Pernah terjadi: `HeroPass` sukses menulis 4 seat, lalu `Init` di play mode cuma
    memasang 2. Stop, masuk lagi, dan benar. Kalau hasil runtime tidak cocok dengan
    log pass-nya, ulangi play mode sebelum mencari bug yang tidak ada.
25. **Bentuk hasil resep HARUS muat di alas yang tersedia.** `CouldSeat()` menjaga
    warnanya jujur, tapi ia tidak membuat resep mustahil jadi mungkin. Greater
    Fireball sempat berbentuk Huruf T (3 petak melintang) sementara alas pembuka
    hero cuma 2×2 — upgrade yang dijanjikan tidak akan pernah bisa terjadi. Kalau
    sebuah resep memang dimaksudkan tersedia di titik tertentu, cek bentuk hasilnya
    terhadap alas yang ada di titik itu.

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

Grid dua lapis 7×7 · tas 4×4 · rune/skill/segel dengan bentuk terikat rarity ·
**100 piece, 101 resep**, piramida ★1–★5 · 7 ailment berbasis poin · 9 reaksi · 10 buff ·
mana & regen · Defense · crit · 3 bentuk AoE · damage meter · tooltip · codex ·
main menu · setelan · camera shake · bar HP/mana beranimasi ·
**gerak otomatis pemain** (arena elips, `MoveSpeed` sebagai stat) ·
**musuh mengepung** (gaya pisah + bidik posisi depan + jalur serong) ·
**wave berbasis jam** dengan spawn mengalir dan sapu bersih di akhir ·
**rendering instanced** (500 musuh, 1 draw call) ·
**angka damage melayang** yang menggabung hit berdekatan ·
**proyektil**: jatuh dari langit, petir melompat, sinar garis, ledakan mati musuh ·
**garis evolusi** yang menghubungkan bahan · **kartu resep berikon** ·
**kabel resep dari kursor** begitu piece diangkat · **tiga strip ikon**
(buff / kutukan / ailment) dengan angka dan hover · 100 ikon placeholder ·
**9 CastKind non-serangan/berciri khas** (lihat §13) · **damage & mana dipecahkan
solver**, bukan diketik tangan (`BalanceTunePass`) · **barang jatuhan jadi benda
nyata** yang dilempar dekat pemain lalu tersedot masuk · **kamera dead-zone**
(terpasang, tapi jarak geraknya 0 — lihat §13).

## 10. Belum selesai — urutan yang disarankan

| # | Item | Kenapa |
|---|---|---|
| 1 | **Main dan nilai rasanya** | semua verifikasi sesi ini programatik. Kurva wave, tekanan grid, lompatan bintang, rasa auto-move, **arena baru 22×17** — belum pernah dinilai tangan manusia |
| 2 | **Boss** | `SpawnOne()` tetap satu-satunya tempat stat per musuh diisi, dan `EnemyArchetype` sudah jadi tempat menaruhnya — boss = satu aset lagi dengan angka besar |
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

---

## 13. Skill non-serangan, kamera, dan solver keseimbangan

Ditambahkan 2026-08-06. Tiga hal terpisah, ditulis di sini karena saling menyentuh.

### 13.1 Sembilan `CastKind` baru

Ditambahkan **di belakang** enum `CastKind`, tidak pernah disisipkan di tengah —
nilai enum tersimpan sebagai angka di aset, jadi menyisipkan satu entri akan
menggeser tiap skill di bawahnya jadi kind yang salah, tanpa error dan tanpa jejak.

| Kind | Isi | Angka `BaseDamage` artinya |
|---|---|---|
| `Orbit` | pecahan mengambang di atas kepala, meluncur sendiri saat ada yang mendekat | damage per pecahan |
| `Blink` | lompat menjauh dari kerumunan; **menolak menyala kalau lapangan lengang** | — |
| `Ward` | perisai yang **menyerap** damage sampai jatahnya habis | jumlah serapan |
| `Surge` | menempelkan `GrantOnCast` ke pemain (haste / quickcast / dsb.) | — |
| `Restore` | isi mana; 0 = penuh, >0 = jumlah tetap | jumlah mana |
| `SunStrike` | menandai tanah, menghantam setelah `TelegraphDelay` | damage |
| `RollingBall` | bola menggelinding, melindas satu musuh **sekali per bola** | damage |
| `Vortex` | menyeret + **mengangkat** + menggerus tiap 0,25 detik | damage per denyut |
| `ForcePush` | melontarkan semua menjauh | damage (kecil) |

Implementasinya di **`Systems/PlayerCasterSignature.cs`** — `partial class PlayerCaster`.
`PlayerCaster.cs` sekarang `public partial class`.

**Field `PieceDefinition` baru:** `GrantOnCast`, `TelegraphDelay`, `PushForce`,
`LiftDuration`, `TravelSpeed`.

**Field `EnemyManager.Enemy` baru:** `GroundY` (tinggi asli, dipisah karena `Pos.y`
dipakai bergantian untuk melayang bawaan dan terangkat sementara), `LiftTimer`,
`Knock`. Dua API baru: `EnemyManager.Push(center, radius, force)` — **gaya negatif
= seretan**, dipakai `Vortex` — dan `EnemyManager.Lift(center, radius, duration)`.

Musuh terangkat **benar-benar lumpuh**: tidak melangkah, tidak menembak, tidak
menyentuh. Diverifikasi: 14 musuh melayang di 2,60 unit, HP pemain tidak turun.

Konten dibuat `Editor/SignaturePass.cs` → menu **Tools/Grimoire/Generate Signature
Skills**. 19 piece + 16 resep + 4 buff. **Jalankan `Rebalance by Throughput`
setelah ini**, karena `SignaturePass` sengaja menulis damage 1 untuk skill
penyerang dan membiarkan solver yang mengisinya.

### 13.2 Solver keseimbangan (`Editor/BalanceTunePass.cs`)

Damage tidak lagi diketik tangan. Arahnya dibalik: **pilih throughput yang layak
untuk satu tier, bagi dengan berapa musuh yang disentuh arketipe itu, biarkan
angka damage-nya jatuh sendiri.**

Target: `22 / 68 / 190 / 520 / 1350` dps per bintang.

Hasil terukur (tiap kolom = dps dari `damage × sasaran / cooldown`):

| ★ | n | terendah | median | tertinggi | naik |
|---|---|---|---|---|---|
| 1 | 21 | 12 | 22 | 26 | — |
| 2 | 15 | 55 | 68 | 82 | ×3,1 |
| 3 | 13 | 95 | 188 | 226 | ×2,8 |
| 4 | 7 | 266 | 523 | 525 | ×2,8 |
| 5 | 6 | 662 | 1333 | 1619 | ×2,5 |

Sebelum solver: sebaran dalam tier sampai **×6**, dan ★3→★4 cuma **×1,5**.
Yang di bawah median tiap tier adalah skill kontrol (`Vortex` ×0,5,
`ForcePush` ×0,55) — potongan yang **disengaja**, karena mereka menjual waktu dan
ruang, bukan kill.

**Urutan menjalankan editor tool penting:** `ComboPass` dan `SignaturePass` menulis
damage mentah, `BalanceTunePass` menghitungnya ulang. Selalu solver **terakhir**.

### 13.3 Kamera dead-zone (`View/ArenaCamera.cs`)

Kamera sekarang anak dari **"Camera Rig"**; `ArenaCamera` di rig, `CameraShake`
tetap di kamera. **Ini wajib**: `CameraShake` menulis `localPosition` kamera dan
mengingat titik asalnya di `Awake`, jadi kalau keduanya menulis transform yang
sama, guncangan menarik kamera balik tiap frame dan pengikutan mati tanpa error.

Batas geraknya dihitung dari yang **benar-benar terlihat** — ukuran ortografis,
rasio layar dan sudut kemiringan semuanya ikut menentukan seberapa jauh tanah
tertangkap, dan menebaknya berarti tepi arena bisa bocor di rasio layar tertentu.

Saat pertama dipasang hasilnya **nol**: arena 16×9 sementara layar menutupi
19,6×11,9, jadi kamera tidak pernah punya alasan bergerak. Diperbaiki dengan
membesarkan arena (keputusan user):

| | Sebelum | Sesudah |
|---|---|---|
| `ArenaHalfX / Z` | 16 / 9 | **22 / 17** |
| `SpawnBoundsX / Z` | 21 / 13 | **24 / 19** |
| `WaveOpenerCount / Distance` | 6 / 0,6 | **9 / 0,5** |
| Sisa gerak kamera | 0,0 / 0,0 | **2,4 / 5,1** |

`SpawnBounds` sengaja cuma **2 unit di luar arena**. Kamera paling jauh bisa
geser 2,4/5,1, jadi tepi layar terjauh persis = tepi arena — dua unit sudah cukup
supaya musuh tidak pernah menetas di dalam layar, tanpa melebih-lebihkan jarak
jalan mereka. Arena yang membesar sudah menambah waktu tempuh; itu sebabnya
rombongan pembuka dinaikkan ke 9 dan dimulai lebih dekat.

Terukur setelah perubahan: rombongan pembuka 9 musuh di jarak 9,6–12,9 (±7,6
detik untuk tiba), pengepungan tetap **8/8 sektor** dengan sektor terpadat 22%,
60 fps. Rig terbukti bergeser sampai batas `(2,44 / 5,14)` lalu berhenti.

**Catatan perilaku:** kamera **tidak** kembali ke tengah setelah pemain masuk lagi
ke zona mati. Itu memang cara kerja dead zone, bukan bug.

Yang **sudah** diperbaiki dari keluhan yang sama: `PlayerMotor` tidak lagi
menyeret pemain balik ke tengah saat lapangan sepi. Dulu tiap kali pemain
berhasil lepas ke satu sisi, ia ditarik balik tanpa diminta.

### 13.4 Barang jatuhan jadi benda nyata (`View/DropPickups.cs`)

Drop tidak lagi langsung masuk kantong. Dilempar dari **posisi pemain** (bukan
posisi musuh mati — itu bisa di seberang peta dan tidak pernah terbaca sebagai
hadiah), memantul sekali, lalu magnetnya **menjemput** dalam radius 7 unit.

Pemain berjalan otomatis dan tidak bisa disuruh memungut, jadi ada batas waktu
6 detik yang menyerahkan barangnya begitu saja di mana pun ia berada. Kehilangan
drop karena pemain kebetulan lari ke arah lain adalah hukuman untuk sesuatu yang
bukan keputusan pemain.

Dimiliki `GrimoireUI` (`_pickups`), callback-nya `AddLoose`.

---

## 14. Tes build ★5 penuh — dan bug mana yang ditemukannya

Dijalankan 2026-08-06. Ini contoh kenapa aturan "ukur, jangan tebak" ada.

### 14.1 Papan cuma muat 4 dari 6 skill ★5

Lapisan skill 7×7 = 49 petak; footprint ★5 = 8–9 petak. Empat sudah penuh, dan
14 petak sisanya terlalu terpecah untuk menerima bentuk `Chunk` 8 petak.
**Rarity yang memakan ruang papan itu bekerja** — bukan kebetulan.

Build uji: 4 × `runebadai` + 6 × `runeasah` di lapisan rune, lalu Cataclysm,
Stormbreaker, Solar Flare, Absolute Zero di atasnya.

| Skill | Damage dasar | Setelah rune | Cooldown efektif |
|---|---|---|---|
| Solar Flare | 650 | **1 528** (crit ×2,8 → **4 277**) | 4,8 dtk |
| Stormbreaker | 175 | 665 | 2,9 dtk |
| Cataclysm | 150 | 353 | 3,7 dtk |
| Absolute Zero | 76 | 167 | 1,8 dtk |

### 14.2 Bug: anggaran mana dihitung per-skill, bukan per-papan

`TargetManaPerSecond` di `BalanceTunePass` adalah angka **per skill** (★5 =
18/dtk). Modelnya tidak pernah menanyakan *berapa skill yang dipasang sekaligus*.

Empat skill ★5 = 72/dtk, dikali 1,34 dari rune cooldown = **96,2 mana/dtk**,
melawan regen **10/dtk**. Build terbaik di game ini menembak di **10% laju
nominalnya** — dan seluruh tangga damage di §13.2 jadi tidak ada artinya di
endgame, karena skill-nya kuat tapi tidak pernah dapat giliran nyala.

**Diisolasi**: wave 20 yang sama dijalankan dua kali, **hanya regen mana yang
diubah**, damage dan cooldown dibiarkan apa adanya.

| | Mana normal | Mana tak terbatas |
|---|---|---|
| Hasil | **mati di detik ~20** | hidup terus |
| HP | 100 → 80 → 45 → 0 | stabil 91 |
| Kills | 145 | 499 |
| Musuh | 43 → 69, terus naik | ditahan 17–40 |

### 14.3 Perbaikan

`TargetManaPerSecond` sekarang dibaca sebagai anggaran **seluruh papan**, dibagi
konstanta `SkillsOnAFullBoard = 5`. Plus `BaseManaRegen` 10 → **13**.

Hasil pada build ★5 yang sama: butuh **20,9 mana/dtk** melawan regen 13 →
**62% laju nominal**. Mana tetap terasa (turun sampai 8–10 saat ramai), tapi tidak
lagi mematikan build.

### 14.4 Kurva endgame setelah perbaikan (semua mana APA ADANYA)

| Wave | HP musuh | Hasil |
|---|---|---|
| 20 | 299 | **HP pemain 100 utuh**, tidak pernah kena; 413 kills |
| 35 | 1 002 | HP 100, musuh ditahan 36–73; ~20 kill/detik |
| 45 | 2 046 | **MATI** di ~7 detik, 1 288 kills |

Dindingnya jatuh persis di tempat yang bisa dibaca dari angka: Solar Flare polos
1 528, jadi **wave 45 (HP 2 046) adalah wave pertama yang tidak bisa di-one-shot
tanpa crit**. Begitu one-shot putus, gerombolan menumpuk dan pemain kalah.

60 fps di semua tes, termasuk 177 musuh hidup di wave 45.

**Jangan hilangkan sifat ini tanpa sengaja.** Kalau `EnemyHpGrowth` atau damage
★5 diubah, dinding endgame bergeser bersamanya.
