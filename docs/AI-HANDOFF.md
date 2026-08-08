# Handoff untuk agent AI

Dokumen ini ditulis supaya agent lain bisa lanjut kerja tanpa harus menebak.
Baca ini **sebelum** menyentuh kode. Terakhir diperbarui: sesi 2026-08-06
(skill non-serangan, kamera dead-zone, drop jadi benda nyata, solver keseimbangan —
**§13**; tes build ★5 penuh + bug anggaran mana — **§14**;
kamera/DebugConfig/Playground — **§15**; boss ular + biome — **§16**;
hutan/audio/bar boss — **§17**;
lapangan tak berujung — **§18**;
tampilan hutan / segel stat / boss kelabang — **§19**; konten 107 piece).

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

---

## 15. Kamera yang benar-benar jalan, DebugConfig, dan scene Playground

Ditambahkan 2026-08-06.

### 15.1 Spawn dilepas dari titik nol dunia — ini yang mengunci kamera

`SpawnPoint()` dulu memakai `SpawnBoundsX/Z` sebagai **koordinat dunia absolut**. Itu
diam-diam mengikat ukuran arena ke jarak tempuh musuh: memperbesar arena berarti
musuh berjalan lebih lama, jadi arena tidak pernah bisa dibesarkan tanpa merusak
tempo — dan tanpa arena besar, kamera tidak punya ruang bergerak sama sekali.

Sekarang kotaknya berpusat di **anchor**, dan anchor-nya adalah **rig kamera**
(`EnemyManager.SetSpawnAnchor`). Bukan pemain: kamera punya zona mati, jadi pemain
boleh menyimpang jauh dari pusat layar, dan kotak yang mengikuti pemain akan
menetaskan musuh di dalam layar pada sisi yang barusan ditinggalkan.

| | Sebelum | Sesudah |
|---|---|---|
| `ArenaHalfX / Z` | 22 / 17 | **40 / 30** |
| `SpawnBoundsX / Z` | 24 / 19 (dunia) | **24 / 15.5 (relatif rig)** |
| `WaveOpenerDistance` | 0,5 | **0,85** |
| Jarak jalan kamera | 2,4 / 5,1 | **20,4 / 18,1** |

**Kotak spawn WAJIB lebih besar dari layar** (layar = 19,6 × 11,9 pada ortho 11 /
16:9 / pitch 68°). Kalau tidak, potongan opener otomatis jatuh di dalam layar
berapa pun nilainya — sempat terjadi, 11 dari 26 musuh menetas di depan mata.
Terukur setelah perbaikan: **0 dari 9** opener lahir di dalam layar.

Lantai sekarang diukur dari arena (`ArenaHalf + 12`), bukan skala tetap. Tanpa itu
pemain berjalan keluar dari mesh-nya.

**Batas spatial hash adalah ±64.** Musuh terjauh sekarang ~41,9. Kalau arena
diperbesar lagi, hitung ulang: `ArenaHalf − setengahLayar + SpawnBounds < 64`.

### 15.2 Bug spatial hash yang sudah lama tidur

`_cellHead` lahir berisi **nol**, dan nol adalah indeks musuh yang sah. Jadi hash
yang belum pernah dibangun mengaku setiap selnya berpenghuni musuh nomor 0, lalu
rantainya dibaca lewat `_nextInCell` yang panjangnya masih nol →
`IndexOutOfRangeException` **tiap frame di dalam Update**, sehingga seluruh gerakan
swarm mati diam-diam: 500 musuh hidup, semuanya membeku di titik spawn, dan
`AliveCount` terkunci di 1.

Tidak pernah meletus selama ini cuma karena wave selalu dimulai dari UI, beberapa
frame setelah Update pertama. `DebugConfig.StartAtWave` memanggil `StartWave` di
dalam `Awake`, dan langsung membongkarnya.

Diperbaiki dua sisi: `_cellHead` diisi −1 sejak lahir (`NewHeads()`), dan
`_nextInCell` ikut tumbuh di `SpawnOne`/`SpawnDummy`, bukan cuma di `RebuildHash`.

### 15.3 `DebugConfig` — saklar curang buat rekaman

`Assets/GameData/DebugConfig.asset`, tersambung ke `_Bootstrap` di `Proto.unity`.

**`Enabled` adalah gerbang induk dan defaultnya MATI.** Semua pembaca lewat properti
`Cheat*` / `*Scale`, tidak pernah membaca field-nya langsung, supaya gerbangnya
mustahil terlewat di satu tempat. `ProtoBootstrap` juga menyalakan `LogWarning`
selama gerbangnya hidup.

| Saklar | Efek |
|---|---|
| `Invulnerable` | `Drain()` langsung keluar |
| `InfiniteMana` | mana dikunci penuh tiap frame |
| `NoCooldowns` | `BuffCooldownMul` jadi 0,01 |
| `DamageMultiplier` | dikali ke `BuffDamageMul` — satu tempat, kena semua cast |
| `EnemyHpMultiplier` | dikali di `SpawnOne` |
| `EnemyCountMultiplier` | dikali ke `WaveTotal` |
| `StartAtWave` | `GrimoireUI.ApplyCheats` memanggil `StartWave` setelah papan tersusun |
| `FreezeSpawns` | `TickSpawning` keluar lebih awal |
| `ForceLoadout` | menimpa `DefaultHero` — isi dengan `HeroLoadout` build bintang 5 |
| `HideUI` | `_canvas.enabled = false`, buat rekaman bersih |
| `StartTimeScale` | gerak lambat buat memperlihatkan bentuk skill |

### 15.4 Scene Playground

`Assets/Scenes/Playground.unity` — digenerate lewat **Tools/Grimoire/Build
Playground Scene**, terdaftar di Build Settings **paling akhir dan dalam keadaan
mati** supaya tidak pernah ikut ke build rilis.

Isinya `PlaygroundBootstrap`: daftar 74 skill di kiri, boneka dalam formasi yang
bisa dipilih, dan pembacaan damage terukur.

**Bonekanya DIAM.** Itu bukan penyederhanaan malas — musuh diam adalah satu-satunya
cara membaca jangkauan sebuah skill secara jujur; di tengah wave sungguhan bentuk
ledakan tertutup gerombolan yang bergerak.

Dua keputusan yang gampang salah dibaca sebagai bug:
- `SpawnDummy` memberi `Hp` jauh di atas `MaxHp`. Disengaja: besar angka damage di
  layar diukur dari **porsi HP musuh**, jadi boneka ber-HP sejuta membuat setiap
  pukulan tampil sebagai angka mungil. Memisahkan keduanya memberi boneka tahan
  lama DAN angka sebesar musuh sungguhan.
- Rune alasnya dipilih yang **tanpa aura**, supaya angka yang diukur tidak dicemari.

`PlayerCaster.CastWithoutWave` ditambahkan khusus untuk ini — buku menembak walau
tidak ada wave. Menyalakan wave palsu akan mendatangkan gerombolan sungguhan yang
menimpa boneka yang sedang diamati.

Tombol: panah atas/bawah pilih skill · R susun ulang · 1–4 formasi
(lingkaran/kisi/baris/acak) · +/− rapat-renggang · SHIFT tahan = gerak lambat ·
ESC kembali ke menu.

Terukur di sana: Cataclysm pada 40 boneka formasi lingkaran = **522 dps + 329 dps
BURN**, 58 fps.

### 15.5 Jebakan input yang memakan waktu

Project ini memakai **Input System baru**, dan `UnityEngine.Input` yang lama
melempar exception **tiap frame** di sana. Dua tempat kena saat membangun Playground:

1. `Input.GetKeyDown(...)` langsung di `Update` — seluruh sisa `Update` ikut mati
   bersamanya. Semua input WAJIB lewat `Systems/ProtoInput.cs`, yang sudah punya
   `#if ENABLE_INPUT_SYSTEM` di tiap propertinya. Ditambahkan di sesi ini:
   `ListStepDown`, `SpreadStepDown`, `SlowMotionHeld`.
2. **`StandaloneInputModule` adalah modul UI LAMA.** Kalau scene memakai Button/
   EventSystem sungguhan, modulnya harus `InputSystemUIInputModule`. Gejalanya
   jahat: UI tetap tergambar rapi, tapi tidak satu tombol pun bisa diklik, dan
   pesan errornya tidak pernah menyebut nama scene-nya.

Scene game utama tidak pernah kena ini karena `GrimoireUI` menguji klik sendiri
lewat `ProtoInput` dan tidak memakai EventSystem sama sekali.

---

## 16. Boss ular dan biome

Ditambahkan 2026-08-06.

### 16.1 Boss ular (`Systems/BossSnake.cs` + `Data/BossDefinition.cs`)

Kepalanya yang berpikir; badannya cuma menapaki jejak yang sudah dilalui kepala.

**Keputusan terpenting: tiap ruas didaftarkan sebagai `Enemy` biasa di pool.** Boss
dengan jalur damage sendiri berarti tiap skill di buku harus diajari cara
mengenainya — dan yang terjadi selalu sama: satu atau dua skill terlupakan, lalu
ada build yang secara diam-diam tidak bisa melukai boss sama sekali. Dengan ruasnya
menjadi musuh biasa, semua yang sudah bisa mengenai musuh otomatis bisa mengenai boss.

Yang membedakan cuma satu: `Enemy.Boss != null` → damage masuk ke **satu kolam HP**
dan ruasnya tidak pernah mati sendiri.

Tiga tempat yang WAJIB ikut dirutekan, dan dua di antaranya gampang terlewat:

| Tempat | Kenapa |
|---|---|
| `Damage()` | jalur utama semua skill |
| **DoT di `TickEnemies`** | tanpa ini, membakar boss malah MEMUTUS badannya |
| `SpawnOne()` reset `e.Boss = null` | slot pool dipakai ulang; tanpa reset, musuh biasa mewarisi kepemilikan ruas boss dan jadi tidak bisa dibunuh |

Ruas boss dilewati di `TickEnemies` **setelah** loop ailment (supaya DoT sempat
berdenyut) tapi **sebelum** gaya pisah / jalur serong / lontaran / damage sentuh —
empat hal yang akan langsung mencabik bentuk ularnya.

**Panjang badan = bar HP-nya.** `WantedSegments()` menurunkannya dari `HpFraction`,
jadi memendeknya badan bukan efek terpisah yang perlu diurus: ia jatuh sendiri dari
satu perhitungan dan mustahil melenceng dari HP aslinya. Pemain tidak perlu membaca
angka di sudut layar; panjang ularnya sendiri yang mengatakannya.

Perilaku: mengitari pada `OrbitRadius`, meliuk dengan Perlin (bukan acak per frame —
yang acak menghasilkan getaran, bukan lintasan), dan **menerjang** tiap
`LungeInterval`. **Menggigit hanya saat menerjang** — kepala yang melukai kapan pun
ia kebetulan lewat membuat seluruh terjangan jadi tidak berarti.

Kepala punya slot warna sendiri (`_bossHeadTint`) terpisah dari badan: itu satu-satunya
bagian yang menggigit, jadi harus bisa ditemukan sekejap di antara 24 ruas sesiluet.

Wave tidak dinyatakan beres selama `BossActive` — tanpa penjaga itu, menghabiskan
gerombolan biasa menutup wave sementara boss masih berkeliling.

Aset: **Tools/Grimoire/Generate Boss** → `Enemies/Boss_serpent.asset`, tersambung ke
`ContentDatabase.Boss`. Muncul tiap `GameBalance.BossEveryWaves` (10).

Terukur: wave 10, HP 9778, 24 ruas → 15 ruas saat HP 5040; jarak antar ruas 0,96–1,74
(target 1,05); orbit 13,8 (target 13); 59 fps. Wave 11 tidak memunculkan boss, wave 20
memunculkannya. Setelah mati: 0 ruas nyasar tertinggal.

### 16.2 Biome (`Data/BiomeDefinition.cs` + `View/BiomeDresser.cs`)

Empat wajah arena, berganti tiap `GameBalance.BiomeEveryWaves` (5):
Ashen Flats → Frostbound Waste → Emberfall → The Hollow.

Props digambar lewat **`EnemyRenderer` yang sama dengan swarm**, bukan GameObject
satu-satu. 240 batu sebagai GameObject = 240 draw call yang tidak pernah bergerak;
lewat renderer instanced, draw call-nya sebanyak jumlah **warna**. Terukur: 260 props
= **3 draw call**.

Props sengaja **tanpa collider**. Yang dibeli adalah titik acuan, bukan rintangan:
gerak pemain sudah otomatis dan musuh sudah saling mendorong, jadi rintangan padat
hanya akan membuat keduanya tersangkut dengan cara yang tidak bisa dibaca.

Tata letak di-seed dari indeks biome, bukan dari waktu — kembali ke biome yang sama
memberi tata letak yang sama persis, dan tempat itu jadi terasa seperti tempat.

Aset: **Tools/Grimoire/Generate Biomes**.

### 16.3 Dua jebakan editor yang memakan waktu di sesi ini

1. **`SerializedObject` array: ubah ukuran, Apply, `Update()`, BARU isi elemennya.**
   Mengubah `arraySize` lalu langsung mengisi elemen dalam sesi yang sama menyimpan
   ukurannya saja — referensi objeknya hilang tanpa satu pun error, dan yang
   tersimpan adalah slot kosong.
2. **Jangan `AssetDatabase.Refresh()` tepat sebelum `LoadAssetAtPath`.** Refresh
   menjadwalkan impor ulang, dan selama impor berjalan LoadAssetAtPath mengembalikan
   **null** untuk aset yang jelas-jelas ada di disk. `SaveAssets()` saja sudah cukup.

Keduanya gagal dengan cara yang sama jahatnya: sukses tanpa error, hasil kosong.

---

## 17. Hutan, zona kamera, audio, dan bar boss

Ditambahkan 2026-08-06, sesudah §16.

### 17.1 Biome dipangkas jadi SATU hutan

Empat biome bergantian dibatalkan. Empat wajah arena terdengar seperti kedalaman
dan menghasilkan sebaliknya: tidak ada satu pun yang sempat dikenali, dan tidak ada
satu pun yang bisa dipoles sampai benar-benar bagus.

Sekarang: **`Biome_forest.asset` — "Verdant Hollow"**, satu-satunya.
`BiomeDresser.OnWaveStarted` tidak melakukan apa pun kalau biome-nya cuma satu, jadi
mekanisme pergantiannya tetap ada tanpa perlu dimatikan.

**Pohon = batang + tajuk, dan hubungannya yang penting.** Satu bentuk saja tidak
pernah terbaca sebagai pohon: silinder sendirian jadi tiang, bola sendirian jadi
gundukan. Tajuk duduk di `height * 0.92` — persis di puncak menyisakan celah yang
terbaca sebagai bola melayang.

Angka setelah dikoreksi user (versi pertama terlalu padat):

| | Pertama | Sekarang |
|---|---|---|
| Pohon | 300 | **85** |
| Rumput | 380 semak bulat | **1 400 rumpun tegak** |
| Bentuk rumput | Sphere, gepeng 0,55 | **Cube, tinggi ×2,8** |

Hutan rapat menutupi gerombolan dan menghapus satu-satunya hal yang harus dibaca
pemain: musuh datang dari arah mana. Yang dicari adalah **padang terbuka dengan
pohon sebagai penanda jarak**, bukan rimba.

Terukur: 85 pohon + 1 400 rumput = **11 draw call**, 59 fps bersama boss dan
110 musuh.

**`EnemyRenderer.Add` sekarang punya varian `Vector3` skala.** Perlu ada karena
bentuk yang bukan makhluk hidup tidak pernah proporsional — batang itu tinggi-kurus,
tajuk itu lebar-pipih, dan keduanya mustahil dari satu angka skala. `_stageScale`
ikut jadi `Vector3[]`; varian `float` lama tetap ada dan meneruskan ke yang baru.

### 17.2 Zona mati kamera jadi knob

`GameBalance.CameraDeadZone` (porsi setengah layar). Dulu tetap di 0,5.

| | Dulu | Sekarang |
|---|---|---|
| Porsi | 0,5 | **0,22** |
| Zona mati | 9,8 × 5,9 unit | **4,3 × 2,6 unit** |

Terverifikasi: pemain digeser ke (12; 8) → rig menyusul ke (7,6; 5,3) lalu **berhenti**
begitu pemain kembali berada di dalam kotak. Itu perilaku dead zone yang benar —
kamera tidak pernah memusatkan ulang.

### 17.3 Audio (`Systems/AudioDirector.cs`)

Sebelum ini **tidak ada satu pun `AudioSource` di seluruh project**, dan slider
volume di menu menyimpan angka yang tidak menggerakkan apa pun.

Klipnya **dibangkitkan, bukan diimpor** — filosofi yang sama dengan ikon placeholder.
Menunggu aset audio berarti game penuh ledakan ini tetap senyap sampai entah kapan,
dan kesenyapan itu hal pertama yang terdengar di rekaman untuk client.

Delapan suara: Cast, Blast, Hit, Death, Reaction, Pickup, BossRoar, WaveStart.
Tiga generator: `Tone` (gigi gergaji bersapuan nada — sinus murni terdengar seperti
nada uji), `Noise` (derau dengan rata-rata bergerak sebagai tapis rendah murahan),
`Chime` (dua sinus).

Enam belas voice bergilir, **2D** (`spatialBlend = 0`) karena pemain selalu di tengah
layar. Tiap jenis suara punya **jeda minimum** — tanpa itu dua puluh musuh yang mati
bersamaan menumpuk jadi satu bunyi yang memekakkan. Nada diacak ±6% tiap kali, karena
suara yang persis sama berulang berhenti terdengar sebagai kejadian dan mulai
terdengar sebagai kerusakan.

**Mengganti dengan file asli**: isi array `Overrides` di komponennya. Tidak ada
pemanggil yang perlu berubah.

Skill berat dan skill ringan sengaja berbeda suara DAN nada (`Blast` 0,9 vs `Cast`
1,15) — itu cara termurah membuat papan yang penuh tetap terbaca lewat telinga saja.

### 17.4 Bar HP boss

Di atas layar, lebar penuh, dengan nama dan persen. Panjang badan ular memang sudah
menceritakan sisa HP-nya dan itu bacaan yang bagus — tapi ia satu-satunya, dan cuma
terbaca kalau seluruh ularnya kebetulan sedang di layar. Di tengah tiga ratus musuh
itu hampir tidak pernah terjadi.

`OnBossSpawned` / `OnBossDied` akhirnya punya pelanggan: banner via
`GrimoireUI.Announce()` (memakai floater yang sama dengan reaksi — widget baru untuk
tiap kabar penting hanya menambah tempat yang harus dipelajari pemain) dan raungan
audio.

`PlayerCaster.OnHurt` ditambahkan, dan sengaja **tidak menyala untuk damage yang
tertahan perisai** — Ward yang berbunyi seperti kena pukul menghapus seluruh gunanya.

### 17.5 Timer akhir wave DICOPOT

Wave sekarang berakhir **hanya kalau lapangannya benar-benar bersih**. Tidak ada jam.

Dulu ada katup pengaman: setelah `ClosingTimeout` (25 detik) semua sisa musuh dihapus
dan wave dinyatakan beres. Alasannya nyata — build tanpa damage sama sekali tidak akan
pernah bisa membersihkan lapangan — tapi harganya jauh lebih mahal dari masalah yang
dipecahkannya: pemain yang sedang menang tiba-tiba kehilangan musuh terakhir yang sudah
susah payah dikejar, dan kemenangan itu berhenti terasa miliknya.

Build tanpa damage sekarang memang menggantung, dan itu jawaban yang benar: buku yang
tidak bisa membunuh apa pun adalah build yang kalah, bukan keadaan yang perlu
diselamatkan diam-diam oleh jam.

Ikut dibuang karena jadi kode mati: `GameBalance.ClosingTimeout`, `EnemyManager.OnSweep`
(beserta langganannya di `ProtoBootstrap`), dan `_closingElapsed`.

`Closing` **tetap ada** dan masih dipakai — artinya "jendela spawn sudah tutup", yang
membuat penembak berhenti menjaga jarak dan mematikan gerakan memutar. Itu tentang
menghabisi ekor wave, bukan tentang mengakhirinya.

Terukur: wave 3, `closing` menyala di t=18,5 dengan 19 musuh sisa; di t=28,4 masih
jalan dengan 5 sisa (dulu sudah disapu di t≈25); beres di t=36,7 dengan `AliveCount 0`.

---

## 18. Lapangan tak berujung (petak ala Minecraft)

Ditambahkan 2026-08-06, sesudah §17. **Batas arena dicopot sepenuhnya.**

### 18.1 Yang HARUS ikut dilepas, dan satu yang gagal senyap

Melepas batas bukan cuma menghapus `Clamp()`. Empat hal terpaku ke titik nol dunia,
dan yang pertama adalah jebakan sesungguhnya:

| Apa | Kalau tertinggal |
|---|---|
| **Spatial hash** (`_hashOrigin`) | hash cuma seluas 128×128. Pemain di x=300 membuat SELURUH swarm terjepit ke sel tepi: gaya pisah mati, `BestCluster` menunjuk tempat salah, **tanpa satu pun error** |
| `PlayerMotor.Clamp()` | pemain tidak bisa keluar kotak lama |
| `ArenaCamera` `_limitX/_limitZ` | kamera berhenti di tepi arena yang sudah tidak ada |
| `EnemyRenderer.worldBounds` (400) | seluruh batch **hilang dari layar** begitu pemain lewat ~200 unit |

Hash punya **tiga** pembaca — `CellIndex`, `Separation`, `BestCluster` — dan ketiganya
harus dirutekan ulang. Satu saja tertinggal: hash-nya benar tapi yang membacanya
menunjuk sel yang salah, dan itu juga senyap.

`_hashOrigin` dikancing ke kelipatan sel, bukan mengikuti pemain mulus, supaya isi sel
tidak bergeser setengah sel tiap frame dan tetangga tidak berkedip masuk-keluar.

### 18.2 Hutan per petak (`View/BiomeDresser.cs`)

Petak **24 unit**, radius **2 petak** → selalu 25 petak hidup, berapa pun jauhnya
pemain sudah berjalan.

**Isi tiap petak diturunkan dari koordinat petaknya sendiri lewat hash**
(`coord.x * 73856093 ^ coord.y * 19349663`), bukan dari daftar yang disimpan. Petak
yang sama selalu menghasilkan pohon yang sama persis, berapa kali pun ia keluar-masuk
jangkauan. Tanpa itu "tak terbatas" berubah jadi "berbeda tiap kali menoleh", dan
tidak ada satu pun tempat yang bisa dikenali.

**`Random.state` WAJIB dipulihkan setelah membangkitkan petak.** Tanpa itu, satu petak
baru menggeser seluruh keacakan game — jenis musuh, sebaran drop, arah semburan skill —
dan semuanya jadi bergantung pada ke mana pemain kebetulan berjalan.

Kerapatan di aset (`TreeCount` / `ScatterCount`) masih ditulis untuk arena lama, dan
diubah jadi per-petak saat runtime dengan membaginya dengan luas arena referensi. Jadi
angka di aset tetap terbaca sebagai **"seberapa rapat"**, bukan "berapa banyak".

Petak yang tidak tersentuh satu frame langsung dibuang (`Evict`). Itulah yang membuat
biaya memorinya ditentukan **luas layar**, bukan jarak tempuh.

`View/InfiniteGround.cs` menempelkan bidang lantai ke rig kamera, dikancing ke kelipatan
10 unit — bidang yang menempel persis membuat bayangannya bergeser halus tiap frame dan
lantainya terlihat "berenang".

### 18.3 Terukur

Pemain dilempar ke **(594; −443)** — dulu mustahil, arena lama ±40×±30:

```
lantai ikut ke      (590; -440)
hash origin         (594; -442)   <- ikut pemain, bukan (0,0)
petak hutan         25            <- tetap, tidak menumpuk
pengepungan         8/8 sektor
fps                 59
```

Determinisme: petak (27,−16) dibaca, pemain lompat ke (3000; 3000) sehingga petak
dibuang, lalu kembali. Tiga batang pertama **identik sampai desimal terakhir**.

Beban gabungan di (200; 150): 141 musuh + boss 24 ruas + hutan 25 petak =
**15 draw call props, 56 fps**, pengepungan 8/8.

**Batas yang tersisa:** hash tetap 128×128 unit di sekitar pemain. Itu bukan batas
dunia, tapi batas jangkauan pandang swarm — musuh yang lebih jauh dari 64 unit tidak
ikut dalam gaya pisah maupun `BestCluster`. Karena musuh lahir dalam ~24 unit dari
kamera, itu tidak pernah tercapai dalam permainan normal.

### 18.4 Koreksi setelah dicoba user

Tiga keluhan, tiga sebab berbeda.

**1. "Kamera gerak-gerak gak jelas kalau sudah end" — ini BUG, bukan setelan.**

`ArenaCamera` menghitung sasarannya dari `transform.position`, sementara transform itu
sendiri sedang di-`SmoothDamp` MENUJU sasaran tersebut. Umpan balik yang tidak pernah
mengendap: tiap frame sasarannya dihitung ulang dari titik yang selalu tertinggal, dan
kameranya merayap pelan tanpa henti bahkan saat pemain berdiri sama sekali diam.

Diperbaiki dengan menyimpan `_focus` sebagai field terpisah dari posisi kamera.
Terverifikasi: pemain diparkir di (5; 3), rig mengendap tepat di **(0,6978; 0,3899)** —
persis sasaran teoretisnya (5 − 4,30; 3 − 2,61) — dan **identik di dua sampel terpisah
beberapa detik**.

**2. "Pohonnya kebesaran".** Pohon setinggi 8 unit di kamera ortografis menutupi
seperempat layar sendirian, dan yang tertutup selalu gerombolan. Tinggi 4–8,5 → **2,2–4**,
lebar batang 0,3–0,6 → **0,18–0,34**, jumlah 85 → **55**.

**3. "Rumputnya kebanyakan, ini berat banget."** Dua sebab, dan yang kedua jauh lebih
besar dari yang pertama:

- **Jumlah**: 1 400 → **260** (≈4 700 props tergambar → **1 117**).
- **Matriks disusun ulang TIAP FRAME.** Ini penyebab utamanya. `EnemyRenderer` memang
  menyusun ulang semuanya tiap frame — untuk swarm itu wajib, mereka berjalan. Pohon
  tidak. Membayar lima ribu perkalian matriks per frame demi hasil yang identik dengan
  frame sebelumnya adalah cara termudah menjatuhkan fps tanpa ada apa pun berubah
  di layar.

`View/PropBatch.cs` memanggang matriksnya sekali dan **hanya membangun ulang saat pemain
menyeberang batas petak**. Selama masih di petak yang sama, biayanya cuma tiga panggilan
`RenderMeshInstanced` dari buffer yang sudah ada.

Bayangan props juga **dimatikan** — tiap rumpun yang melempar bayangan berarti satu lagi
objek yang digambar ulang ke peta bayangan tiap frame. Bayangan panjang dari matahari
rendah tetap ada, dari pemain dan musuh yang jumlahnya ratusan, bukan ribuan.

Terukur sesudahnya: wave 20 dengan **158 musuh + boss 24 ruas + 1 117 props =
11 draw call props, 59–61 fps**.

### 18.5 Batas arena DIPASANG LAGI — dan kenapa melepasnya keliru

Dibalik atas permintaan user, dengan alasan yang lebih tajam dari analisis awal:

> Seluruh perilaku `PlayerMotor` adalah **menjauh dari tekanan kerumunan**. Tanpa dinding,
> menjauh SELALU berhasil. Pemain berjalan lurus selamanya, gerombolan mengekor tanpa
> pernah menyusul, dan tidak pernah ada alasan untuk melawan — wave tidak selesai karena
> tidak ada yang mati.

Ini pelajaran desain, bukan pelajaran teknis: **lapangan tak berbatas mematikan game yang
gerak pemainnya otomatis dan defensif.** Dindingnya bukan pembatas teknis, dia bagian dari
permainannya — terjepit di tepi adalah cara kalah yang sah, dan justru itu yang membuat
kerumunan jadi ancaman.

Yang dikembalikan:
- `PlayerMotor.Clamp()` (elips arena)
- `ArenaCamera._limitX/_limitZ`, dijepit pada **`_focus`** bukan pada posisi kamera —
  menjepit posisi sementara sasarannya bebas membuat kamera terus mendorong ke dinding
- Lantai jadi bidang tetap seukuran arena lagi; `View/InfiniteGround.cs` **dihapus**

Yang **tetap dipertahankan** dari kerja "tak berujung", karena semuanya perbaikan sejati:
- **Hutan berbasis petak** — biaya props ditentukan luas layar, bukan luas arena
- **`PropBatch`** — matriks dipanggang, tidak disusun ulang tiap frame
- **Spatial hash yang mengikuti pemain** — lebih tahan banting daripada terpaku titik nol
- **`worldBounds` besar** — tidak ada batch yang hilang diam-diam

Hutan sekarang dipakai untuk **membingkai dinding**: tiga kali lipat kandidat pohon
disaring, yang di LUAR arena selalu lolos dan yang di dalam cuma sepertiganya. Hasilnya
rimbun di luar, lapang di dalam — dindingnya terbaca sebagai batas pohon, bukan sebagai
batas tak kasat mata tempat kontrol tiba-tiba macet.

Terukur: pemain dilempar ke (500; 400) → ditarik balik ke (27,4; 21,9), di dalam elips;
rig berhenti tepat di batas (20,4; 18,1). Wave 4 dengan loadout default: 49 kill, wave
**beres karena lapangan bersih** di t=59,5 (bukan disapu jam), HP pemain utuh 100,
1 661 props di 11 draw call, 59 fps.

---

## 19. Tampilan hutan, segel stat, kecepatan musuh, dan boss kelabang

Ditambahkan 2026-08-06, sesudah §18.

### 19.1 Tampilan: dua kali salah sebelum benar

Target: **Cult of the Lamb / Hades — gelap tapi bersinar**. Dua percobaan meleset dari
dua arah berlawanan, dan sebabnya sama: salah membaca DI MANA kegelapannya berada.

| Percobaan | Hasil | Kenapa salah |
|---|---|---|
| 1 | terang merata, hijau kuning | menerangi lapangannya, jadi tidak ada yang bisa bersinar |
| 2 | gelap merata | melapangkan gelap ke mana-mana, jadi tidak ada yang bisa dibaca |
| **3** | **lapangan terang, bingkai gelap** | benar |

Di referensinya lantainya justru **pucat**; yang gelap adalah **tepi layar dan
kejauhan**. Dan yang mengerjakan pembingkaian itu **KABUT**, bukan warna tanah.

Geometrinya yang menentukan angkanya: kamera duduk 18,5 unit di atas dan menunduk 68°,
jadi lantai di BAWAH layar berjarak ~19 unit sementara yang di ATAS layar ~27–36.
**Kabut 21→44** karena itu hanya menyentuh bagian atas layar.

### 19.2 Tiga bug tampilan

1. **`RenderSettings.ambientLight` diabaikan di mode Trilight.** `SceneLook` menyalakan
   Trilight; `BiomeDresser` mengisi `ambientLight` — warna ambient datar. Seluruh
   pengaturan ambient biome tidak pernah berpengaruh, tanpa peringatan apa pun.
   Sekarang mengisi `ambientSkyColor` / `EquatorColor` / `GroundColor`.

2. **Semua props dan musuh mengkilap seperti plastik.** Material instanced dibuat lewat
   kode dan tidak pernah lewat `SceneLook.ApplySurface`, jadi mereka memakai smoothness
   bawaan **URP/Lit 0,5** — padahal asetnya minta 0,05. Sekarang `_Smoothness`,
   `_Metallic`, dan `_SPECULARHIGHLIGHTS_OFF` disetel eksplisit di `PropBatch` dan
   `EnemyRenderer`.

3. **Bidang satu warna tidak akan pernah terbaca sebagai rumput**, seberapa pun tepat
   warnanya — yang hilang variasi rapatnya, bukan warnanya. `BiomeDresser.GrassTexture()`
   membangkitkan tekstur dari tiga skala derau (bercak, helai, bintik) sebagai PENGALI
   di sekitar 1, jadi mengganti warna biome tidak menuntut membangkitkan ulang tekstur.

### 19.3 Awan & berkas cahaya (`View/Atmosphere.cs`)

Dua bidang mengikuti kamera dengan tekstur derau yang digulung. Yang membuatnya bekerja
bukan teksturnya, tapi **UV-nya dikunci ke koordinat DUNIA** — bidangnya ikut kamera,
polanya tidak. Tanpa itu awannya menempel di layar dan terbaca sebagai lensa kotor.

Deraunya **bisa diubin** (empat sampel dari sudut berseberangan). Perlin bawaan Unity
tidak berulang, dan tekstur yang tidak berulang memperlihatkan jahitan lurus melintasi
lapangan.

**Memakai `Sprites/Default`, bukan URP/Unlit.** URP/Unlit lahir opaque, dan menyalakan
transparansinya lewat kode menuntut `_Surface`, `_Blend`, `_SrcBlend`, `_DstBlend`,
`_ZWrite`, render queue DAN kata kunci shader semuanya benar bersamaan — satu meleset
dan materialnya tetap opaque tanpa keluhan. Itu yang terjadi di percobaan pertama.

Tidak memakai volumetrik dengan sengaja: di kamera ortografis menunduk, berkas cahaya
sebetulnya hanya terlihat sebagai **pola terang di lantai**, dan pola di lantai harganya
satu bidang, bukan satu render pass.

`View/ArenaLights.cs` menambah beberapa lampu titik lembut yang mengembara pelan
(Perlin, bukan sinus — sinus berulang persis dan mata menangkap polanya).

### 19.4 Musuh tidak pernah bisa menyentuh pemain

**Aritmetika, bukan opini:** pemain berlari **3,2** dan kabur otomatis. Grunt **1,6**,
Cursed **1,36**, dan Stalker baru muncul di **wave 5**. Wave 1–4 karena itu mustahil
menyentuh pemain — bukan karena build-nya bagus.

Bukti sebenarnya sudah ada di log sesi sebelumnya dan terlewat: "wave 4 selesai, HP
pemain utuh 100".

Diperbaiki tiga sisi sekaligus, karena satu saja tidak cukup:

| | Sebelum | Sesudah |
|---|---|---|
| Kecepatan musuh | 1,35–1,6 | **2,0–2,4** |
| Kecepatan pemain | 3,2 | **2,8** |
| `DangerRadius` | 6 | **3,5** |

Stalker (×1,55) kini **3,7 — lebih cepat dari pemain**. Itu satu-satunya musuh yang
benar-benar tidak bisa dilepas, dan itulah gunanya arketipe itu ada.

Ditambah: **damage musuh menskala per wave** (`ContactDpsFor`, `EnemyDamageScale`).
Sebelumnya musuh wave 40 menyakiti persis sama seperti wave 1, sementara HP dan damage
pemain sudah naik berkali lipat.

### 19.5 Segel stat (`Editor/SigilPass.cs`)

Dua celah nyata. **Tidak ada satu pun segel yang menaikkan damage** — hanya rune, dan
rune tinggal di lapisan bawah, jadi "aku mau memukul lebih keras" bukan keputusan yang
bisa diambil di lapisan skill sama sekali. Dan **segel berhenti di bintang dua**
sementara skill sampai bintang lima.

Empat sumbu, masing-masing sampai ★3: SERANG, NYAWA, MANA, TAHAN.

### 19.6 Boss: jamak, kelabang, dan anak buah

`EnemyManager.Boss` (tunggal) → `_bosses` (daftar). Wave 20 memunculkan 2 ekor, wave 40
tiga. **Bertambahnya jumlah, bukan HP** — satu ular ber-HP sepuluh kali lipat cuma jadi
tembok yang lebih lama dipukul; tiga ular dari tiga arah adalah masalah yang baru.

`ContentDatabase.Boss` (tunggal) → `BossKinds` (daftar).

**Kelabang penyelam** hampir tidak menambah kode. Badannya sudah menapaki jejak
kepalanya, dan **jejak itu menyimpan ketinggian** — jadi begitu kepalanya melengkung
naik lalu menukik, seluruh badan mengikuti busur itu sendiri, satu per satu. Tidak ada
satu baris pun animasi badan.

Ritmenya **lumba-lumba**, dan itu koreksi dari percobaan pertama yang bertahan 2,8 detik
di permukaan — itu bukan melompat, itu berjalan-jalan. Sekarang: **3 lompatan beruntun**
(busur 1,1 detik, tinggi 5,5) dengan celup dangkal 0,4 detik di antaranya, lalu
menghilang 5,5 detik sedalam 6 unit.

Terukur, profil ketinggian sepanjang badan:
`2.8 5.0 2.9 | -0.3 -0.6 | 2.8 5.0 2.7 | -0.4 | -6.0` — **dua gundukan terlihat
bersamaan dengan ekor masih terkubur**. Siluet cacing pasir, jatuh sendiri dari
geometrinya.

**Terbenam = kebal dan tak terlihat**, dicek di **satu tempat** (`Damage()` dan
`LateUpdate`), karena semua jalur damage bermuara di sana — jadi tidak ada skill yang
bisa lupa.

**Coilspawn** adalah kelabang yang sama dengan angka jauh lebih kecil dan flag `Minion`:
ikut wave biasa dari wave 6, tidak mengumumkan diri, tidak menampilkan bar HP boss.
Seluruh sistemnya dipakai ulang apa adanya.

### 19.7 Jebakan pengukuran yang terulang

`execute_code` menahan main thread Unity. Membaca `Time.smoothDeltaTime` di dalam
rentetan panggilan berturut-turut menghasilkan angka yang **jauh lebih buruk dari
kenyataan** — satu sesi sempat melaporkan "20 fps" dan "171 ms di lapangan kosong",
yang mustahil. Ukur fps di panggilan yang **tidak melakukan apa pun selain membacanya**.

---

## 20. Props dari aset (Ultimate Nature Starter), dan tampilan yang ikut ketahuan

Pack `Assets/Plugin/InnerverseInteractive/Ultimate Nature – Starter` dipasang menggantikan
props primitif. **Pack ini UNTRACKED di git** — kalau repo di-clone tanpa folder itu, hutannya
kosong dan `Tools/Grimoire/Generate Biomes` akan menjerit di console. Commit atau catat.

### Perubahan struktur

`BiomeDefinition` tidak lagi punya field pohon/semak primitif (`TreeCount`, `TrunkColors`,
`ScatterShape`, dst). Gantinya **satu daftar datar `PropModel[]`** — mesh, material per submesh,
kerapatan, dan aturan sebaran per entri. Tiga lapisan tetap (batang/tajuk/semak) masuk akal
selama props masih dirakit dari primitif; dengan mesh sungguhan, pohon sudah sebuah pohon.

`PropBatch` berubah dari "satu mesh + palet warna" jadi **"satu mesh + material per submesh"**.
Semua submesh memakai daftar matriks yang SAMA; yang bertambah cuma panggilan gambarnya.

Isi: 13 jenis prop — 2 cemara, rumput, bunga, semak, jamur, 2 batu, 2 kerikil, batang, tunggul,
ranting. **Tebing dan gunung sengaja tidak dipakai.**

### Angka terukur

| | |
|---|---|
| Props terlihat | ~3 900 |
| Draw call props | 19 (naik dari 11) |
| fps wave 8 | 59–63 |

**Menggambar props praktis gratis.** Diukur langsung: mematikan seluruh props menghemat 1,35 ms,
dan mematikan **bayangan pohon saja** menghemat 1,35 ms yang sama persis. Jadi seluruh biayanya
ada di pass bayangan, tepatnya alpha-clip daun cemara. Cascade 4→2 cuma menghemat 0,16 ms —
bukan di situ. Knob-nya `CastShadows` per entri; mematikan `spruce-short` saja = +0,75 ms.

### Layar itu 39 × 22 unit, BUKAN 19,6 × 11,9

Angka lama di dokumen ini salah dan menyesatkan dua keputusan sekaligus:

1. **`ClearingRadius` 10** membuat halaman kosong sebesar seluruh layar — run dimulai tanpa
   satu pun pohon terlihat. Sekarang 5,5.
2. **Pohon 3–5 unit** terlalu kecil di layar 22 unit. Sekarang **5,7–8,5**.

Rumusnya: `orthographicSize 11` → tinggi 22 unit, lebar 22 × 16/9 = 39,1.

### Kamera menunduk 68°, dan itu mengubah apa yang terlihat

Benda TEGAK setinggi *h* cuma terproyeksi **0,37 h** di layar; benda REBAH sepanjang *L*
terproyeksi 0,93 L. Rumput setengah unit karena itu tampil setinggi 0,18 unit — ia tidak
terbaca sebagai rumput, ia terbaca sebagai coretan.

`PropModel.FaceCamera` (0..1) merebahkannya ke arah kamera. **Tidak perlu billboard per-frame:**
kamera ortografis dan tidak pernah berputar, cuma bergeser — jadi ini SATU rotasi tetap yang
ikut dipanggang ke matriks, gratis. Yang direbahkan otomatis jadi dua sisi (`_Cull` 0), kalau
tidak helai yang membelakangi kamera hilang.

**0,45, dan angka ini dua sisi.** 0,7 sempat dipakai: helai yang tadinya menghadap langit jadi
menghadap kamera, berhenti menangkap matahari, dan seluruh rumpunnya membaca sebagai gumpalan
GELAP. Mengecat ulang warnanya tidak menolong — gelapnya dari arah normal, bukan dari warna.

> Kalau suatu hari kamera bisa diputar pemain, `FaceCamera` yang pertama harus dicabut:
> matriks yang sudah dipanggang tidak ikut berputar.

### Lantai: satu ubin 6 unit tidak bisa memuat bercak

Ini akar "texture ground jelek banget". Ubin selebar 6 unit di layar 39 unit **tidak bisa memuat
apa pun yang lebih besar dari seperenam layar** — seluruh variasinya terpaksa berfrekuensi
tinggi, dan bidang yang variasinya cuma berfrekuensi tinggi selalu terbaca sebagai bintik
seragam. Yang hilang bukan detailnya, yang hilang BERCAKNYA.

Sekarang dua skala: **peta dasar** untuk bercak (ubin **44 unit** — wajib lebih lebar dari layar,
kalau tidak dua salinan terlihat bersamaan dan otak membaca petak) dan **peta detail** untuk
butiran (`UNS_Terrain_Grass`, ubin 5 unit, `_DETAIL_MULX2`).

Bercaknya dua sumbu, bukan satu: terang-teduh **dan** hangat-dingin. Variasi terang saja
menghasilkan lapangan abu-abu yang diberi warna.

> `_DETAIL_MULX2` WAJIB di-enable. Tanpa keyword itu seluruh blok detail dilewati — petanya
> terpasang, angkanya benar, dan tidak ada apa pun yang berubah di layar.

> Terrain grass pack **gagal sebagai peta DASAR** (hijau pekatnya menimpa segalanya jadi hijau
> jenuh yang datar) tapi **benar sebagai peta DETAIL**. Kekuatannya 0,4 — di 0,75 ia bukan
> menambah butiran, ia menggelapkan bercak yang baru dibuat.

### Grading sudah melenceng dari generatornya

`PP_GoldenHour.asset` berisi bloom **0,95 di ambang 0,80** dan vignette **0,45**, sementara
`LookBuilder.cs` menulis 0,4 di ambang 1,1 dan 0,16. Seseorang menyetel tangan di Inspector dan
kodenya tidak ikut — dan `LoadOrCreateProfile()` **berhenti begitu asetnya ada**, jadi menjalankan
ulang pass-nya tidak pernah memperbaikinya.

Bloom 4× terlalu kuat di ambang terlalu rendah = separuh lantai yang kena matahari ikut mekar =
kabut susu di atas segalanya. **Itu penyebab terbesar lantai terlihat berlumpur, bukan teksturnya.**

Sekarang `LoadOrCreateProfile()` **menulis ulang seluruh isi profil tiap jalan** (komponen lama
dibuang dulu — tanpa itu tiap jalan menumpuk satu set baru). Bloom 0,32 @ 1,25, vignette 0,2.

### ACES terkunci di belakang Atmosphere

Demo pack memakai **Tonemapping ACES**, dan itu sempat dipasang. Hasilnya **seluruh lapangan
jadi CYAN terang.**

Penyebabnya bukan grading-nya: `View/Atmosphere.cs` menggambar berkas cahaya dan bayangan awan
dengan shader **`Sprites/Default`** — shader lawas ruang-gamma yang tidak mengerti pipeline HDR.
Berkas cahayanya ADDITIVE, dan begitu tonemap-nya filmik hasil tambahannya lewat jauh di atas
jangkauan lalu terlipat jadi warna yang salah.

**Terbukti dengan mematikan kedua bidang itu — cyan hilang; dan dengan mengembalikan Neutral
sambil kedua bidang menyala — cyan juga hilang.**

Jadi ACES adalah peningkatan nyata yang menunggu satu pekerjaan lain: memindahkan kedua bidang
`Atmosphere` ke shader URP transparan yang benar. Sampai itu selesai, **Neutral yang benar.**

### Terrain system: TIDAK, dan bukan karena selera

`Environment/Terrain/Data/UNS_Terrain.asset` di-serialize oleh **Unity 6000.5.3f1** sementara
project ini **6000.3.6f1**. Unity menolak mengimpornya — `GetMainAssetTypeAtPath` mengembalikan
`DefaultAsset`, 0 sub-aset. Itulah kenapa demo scene pack menampilkan **"Terrain Asset Missing"**
di Inspector.

**Demo scene yang terlihat cantik itu me-render tanpa terrain sama sekali.** Rumput lebatnya
datang dari grup `UNS_Vegetation` / `UNS_Rocks` / `UNS_Natural_Props` — prefab yang ditaruh,
persis cara yang dipakai di sini. Yang membuatnya cantik adalah volume profile-nya.

Terlepas dari itu, Terrain tetap salah untuk game ini: arenanya datar (heightmap tak terpakai),
hutannya deterministik dari hash koordinat (Terrain menuntut map yang dilukis tangan), dan
props kita 19 draw call ±0 ms (detail-renderer Terrain punya jalur sendiri yang lebih mahal).

### Invarian yang diuji ulang setelah perubahan

- **Determinisme petak**: petak (3,−7) dibangkitkan dua kali → 148 matriks, **beda 0**.
- **`Random.state` global utuh**: `0.5868507` sebelum dan sesudah membangkitkan petak.

## 21. Peta run ala STS, portal fisik, pulau rehat (2026-08-07)

### Arsitektur

- **`Model/RunMap.cs`** — graf act: 15 lantai × 3 lajur, boss di puncak, generator menjamin
  tiap node punya jalan masuk & keluar (teruji 200 peta: 0 yatim, 0 buntu). Murni model.
- **`Model/WaveHash.cs`** — undian deterministik per wave. JANGAN ganti dengan
  `new System.Random(wave * K)`: seed berjajar = sample pertama berjajar (wave 1–29 malam
  semua). Dipakai cuaca (salt 104729) dan siang/malam (salt 3389).
- **`Systems/RunDirector.cs`** — sutradara: spawn portal FISIK setelah wave bersih
  (klik = layar-space, tanpa collider), `PlayerMotor.WalkTo` menjalankan karakter ke portal,
  `Arrive` mengeksekusi node. Pulau rehat = teleport ke `IslandCentre (50, 42)` — DI DALAM
  jangkauan lantai (±70/±60) tapi di luar elips arena; scene terpisah butuh persistensi run
  yang belum ada.
- **`GrimoireUI.AttachRun`** — semua akibat node dieksekusi UI (pemegang gold/papan):
  toko dikocok per singgah, panel slot (hasil diundi SAAT klik, animasi cuma menunda),
  panel kejadian (modal, menelan semua klik), overlay peta (M / tombol PETA; read-only,
  memilih tetap lewat portal).

### Alur

`OnWaveCleared` → `RunDirector.SpawnPortals(Reachable())` → klik → `WalkTo` → `Arrive`:
Fight `StartWave(Wave+1)`; Elite `SetNextWaveMods(2.2/1.25/1.3)` ATAU mini-boss
(`EliteBossChance` 40%, jumlah ikut act); Boss `ForceBossNode(2+act-1, ×2.5 HP, aggro 1.6,
allKinds)` — ular DAN kelabang; Shop/Event/Gamble → pulau. Boss puncak beres → act baru,
peta baru. Tombol MULAI WAVE & toko kelipatan-3 pensiun selama `_run != null`
(`CanStartWave`, `ShopEventActive`, `OnWaveCleared`).

### Titik tumpang di EnemyManager (pola DebugConfig)

`_nextHpMul/_nextCountMul/_nextDamageMul` + `_nextBoss*` — dikalikan di `StartWave`
(jumlah), `SpawnOne` (HP), kontak & tembakan (damage), `SpawnBossNode` (Hatch dengan
hpMul+aggro). **Reset di `FinishWave`**, jadi elite tidak menular. `BossSnake.Aggro`
membagi jeda lunge/dive/spit per-EKOR — jangan tulis ke `Def` (aset bersama).

### Jebakan yang ketemu saat membangun ini

1. `CopySerialized(day, night)` menimpa daftar VFX malam LALU guard "masih kosong?" tidak
   pernah menyala — kunang-kunang tidak pernah terpasang. `BuildNight` sekarang
   menyelamatkan daftar malam sebelum menyalin.
2. Efek ikut-kamera Weather tidak pernah dibersihkan saat ganti biome — tiap pergantian
   siang/malam menumpuk satu set daun. Sekarang simpul `Ikut Kamera` + `_carriedFx`.
3. `Application.targetFrameRate = 60` (bawaan GameSettings lama) = "fps 30–60".
   Bawaan sekarang TANPA BATAS; pref `opt.framecap` lama dihapus sekali.
4. Ghost (piece di tangan) dicari SEBELUM grup emas duduk di `FindPendingGroups` —
   mengangkat kembaran bahan membajak pasangan emas (garis pindah, membiru). Grup emas
   (lengkap + `CouldSeat`) sekarang dikunci paling dulu; hanya lock yang membubarkannya.

### Verifikasi (programatik, play mode)

Portal→jalan→wave 1→bersih→portal lagi ✔; pulau: teleport, PEDAGANG+api unggun, shop
auto-buka, LANJUT→pulang→portal lanjutan ✔; elite total ×1,25 pas ✔; boss node: serpent
144.990 + centipede 177.210 HP (persis rumus ×2,5), aggro 1,6 ✔; drop ★ 82,6/11,7/4,3/1,5/0
(4000 undian) ✔; spawn acak + rig ikut ✔.

### Belum (dijanjikan ke pemilik project)

- VFX slot "dopamin ala Vampire Survivors" — panel slot masih teks; butuh pass VFX.
- Penjaga pulau belum bercerita (butuh sistem dialog).
- Skill 3 + fragment 3 jalur (merah/biru/kuning) + skill tree bertingkat.
- Event baru satu; formula slot & event masih minimum yang layak.

## 22. Putaran feedback peta & suasana (2026-08-07, lanjutan)

Empat keluhan pemilik project, semuanya diverifikasi pakai SCREENSHOT (ScreenCapture ->
baca PNG) — pertama kalinya tampilan dinilai mata di sesi AI, dan dua akar masalah baru
ketahuan justru dari gambarnya.

1. **"Masuk biome kerasa cuma refresh"** — `RunDirector.Relocate()`: tiap node
   fight/elite/boss menteleport pemain ke titik acak arena sebelum wave mulai, dan
   `Weather.Rescatter()` (dipanggil tiap `OnWaveStarted`) menyebar ulang semua kantong +
   mengacak ulang jadwal burung. Portal = gerbang, bukan pintu putar.
2. **"Peta jelek parah"** — ditulis ulang meniru `project_b RoguelikeMapUI`: KIRI->KANAN,
   jalur bezier kubik di-sample jadi 7 segmen bercelah (control point ber-seed), node
   ber-jitter posisi/ukuran/rotasi, ring status (emas=kamu, putih denyut=bisa diinjak,
   hijau=terlewati, pucat=terkunci), boss 1,35x. Layout di-cache pakai signature
   (act/At/jumlah node) — 600-an segmen tidak disusun ulang per frame. Latar SOLID:
   alpha 0,97 membocorkan papan & banner di belakangnya.
3. **"Kupu-kupu menyala di malam"** — kupu-kupu fog DICOPOT dari malam. Penggantinya
   `Assets/GameData/Look/Fireflies.prefab` BANGKITAN (`BiomePass.EnsureFireflies`):
   titik additive yang KEDIP lewat gradasi alpha bergelombang + noise wander; material
   pinjam bara Lana. Malam = kunang-kunang + bara doang yang menyala.
4. **"God ray kurang tinggi, kelihatan sumbernya"** — `AmbientVfxEntry.Stretch` (3D start
   size: tinggi butiran x2,8, lebar tetap) mendorong puncak berkas keluar layar; tint
   siang (1; 0,95; 0,8) malam (0,45; 0,6; 1) dengan ALPHA ~1 (prefab aslinya sudah 0,15 —
   mengalikan alpha kecil dua kali membuat berkasnya lenyap, itu kejadian).

### Jebakan baru: prefab debu Lana menyimpan LEMBARAN beam raksasa

"Godray ngumpul di tengah" yang asli BUKAN salah penempatan — di dalam `Light/Sunlight` &
`Light/Moonlight` ada sistem partikel lembaran ~15 unit yang di kamera atas tergambar
sebagai balok UFO menelan pemain. `AmbientVfxEntry.CullSheets` mematikan sistem
ber-startSize > 5 unit saat spawn (ambang yang sama dengan aturan Grain). Kedua entri
debu memakainya; TSI shaft tidak (butirannya memang besar).

### Tint partikel

`AmbientVfxEntry.Tint` -> `Weather.Spawn`: mode Color = dikali, mode gradien = ditimpa.
Ingat alpha prefab: tint.a adalah PENGALI di atas alpha asli yang sering sudah kecil.

### Ralat §22 butir 4 — god ray akhirnya DIBANGUN SENDIRI

Stretch pada mesh shaft TSI tetap gagal: prefab itu dirancang untuk kamera sejajar mata,
dan dari 68 derajat ia memipih jadi GENANGAN cahaya di lantai, seberapa pun ditinggikan.
Penggantinya `Assets/GameData/Look/GodRay.prefab` — bangkitan `BiomePass.EnsureGodRay`,
SELALU ditulis ulang tiap pass jalan (SaveAsPrefabAsset menimpa path yang sama = GUID awet,
rujukan aset biome tidak putus):

- TIGA pita quad menghadap kamera (`Euler(68; 0; z)` — anchor: quad Gloom `Euler(90;0;0)`
  menghadap kamera tegak lurus), z memiringkannya di bidang layar searah matahari.
- Panjang 46/42/38 unit — layar cuma 22 unit: badan pita selalu menembus tepi atas layar.
- Tekstur gradien bangkitan: pangkal fade-in 7% (tanpa tepi kotak), meluruh dan NOL di 85%
  — ujungnya tidak pernah terlihat, dari mana pun kamera memotongnya.
- Material menyalin material shaft TSI (additive yang sudah terbukti), teksturnya ditukar.
- Warna per biome lewat `Tint` — jalur mesh di `Weather.TintMeshes` (MaterialPropertyBlock;
  pengali alpha per pita dibaca dari NAMA "Blade 0.55" supaya prefab bangkitan tidak butuh
  komponen skrip).

Diverifikasi screenshot: pita diagonal panjang masuk dari luar layar, sumber tak terlihat,
siang emas / malam biru bulan.

### Ralat kedua §22 — god ray FINAL: prefab paket, MILIK USER

Keputusan pemilik project, dua-duanya mengikat:

1. God ray = `Lana .../Light/Sunlight.prefab` (siang) & `Moonlight.prefab` (malam),
   **disetel TANGAN oleh pemilik project langsung di prefab-nya** (tinggi beam dsb).
   KODE DILARANG menyetel bentuknya: entri biome untuk keduanya wajib polos —
   Stretch 1, Tint putih, Scale 1, CullSheets mati. Kode hanya mengurus PENEMPATAN:
   2 titik, jarak 14-26 (siang) / 15-28 (malam) dari pemain — "cahayanya dari
   matahari/bulan, titik lahirnya tidak boleh masuk kamera" — dan OnlyClear.
2. `GodRay.prefab` bangkitan + `EnsureGodRay` TIDAK dipakai lagi (percobaan yang
   kalah oleh keputusan di atas; generatornya masih ada kalau suatu saat perlu).
3. Grade = **profil demo UNS seutuhnya** (`AdoptSunnyGrade` mengklon
   `UNS_Forest_Volume_Profile` ke PP_Sunny/PP_Night pada GUID tetap, ACES aktif,
   matahari balik 2,8). Jalankan `Install Volumetric Fog` SETELAH `Generate Biomes`
   — klon menghapus komponen kabut HAZE.
4. `Weather.Spawn` sekarang paham `Stretch` untuk sistem yang SUDAH 3D (kalikan Y
   saja) — tapi untuk beam paket lihat butir 1: jangan dipakai.

## 23. Empat wajah arena + malam bercahaya + cuaca berangin (2026-08-07, lanjutan)

Kecelakaan jadi fitur: grade demo UNS yang sempat menimpa siang DISUKAI — tapi sebagai
SORE, bukan siang. Hasil akhirnya EMPAT wajah, diundi per wave dua tahap
(`WaveHash` salt 3389 siang/malam, salt 7717 rasa):

| # | Aset | Grade | Ciri |
|---|---|---|---|
| 0 | Biome_forest (Verdant Hollow) | PP_Sunny — look LAMA (restore git) | siang biasa, sun 2,4 |
| 1 | Biome_forest_night (MALAM) | PP_Night — look lama | "cahaya internal": 7 lampu x5,5 r24, ambient dinaikkan, kunang-kunang DIKURANGI (2 kantong, chance 0,7) |
| 2 | Biome_forest_sore (SENJA) | PP_Sore = klon profil demo UNS (ACES, hangat) | sun 2,5 pitch 16 — bayangan panjang |
| 3 | Biome_forest_midnight (TENGAH MALAM) | PP_Midnight = PP_Night exposure −1, sat −22 | sun 0,3, 4 lampu, kabut 18-85, player light 6 |

Knob: `NightChance` 0,5 / `DuskChance` 0,3 / `MidnightChance` 0,3 di GameBalance.
Urutan array `_biomes` WAJIB [siang, malam, senja, tengah-malam] — dua slot pertama
kompatibel dengan scene lama.

**Cuaca (permintaan): BERANGIN 50% / basah 40% / cerah 10%** — bobot 1/5/2/1,4/0,6
(malam 1/5/2,4/1,6). Konsekuensi yang diambil sadar: god ray Sunlight/Moonlight
DILEPAS dari OnlyClear (cerah tinggal 10% — beam yang cuma hidup di situ praktis tak
pernah terlihat); ia tampil juga saat berangin, tetap sembunyi saat hujan
(HideInRain). Kupu-kupu tetap OnlyClear sesuai permintaan lama.

**PP_Sunny tidak pernah ditimpa pass lagi** (`AdoptSunnyGrade` hanya membuat kalau
belum ada). BuildSore/BuildMidnight menyalin siang/malam dengan guard daftar-tuning
yang sama seperti BuildNight; sore/tengah-malam mewarisi suasana induknya (termasuk
beam god ray setelan tangan). `HazePass` meng-configure EMPAT profil; urutan tetap:
Generate Biomes dulu, Install Volumetric Fog sesudahnya.

## 24. Portal DIHAPUS: peta pemilih fullscreen + transisi Gloom + pulau Suaka (2026-08-09)

Permintaan pemilik project, diverifikasi screenshot DAN dimainkan tangan sendiri
(4+ wave beruntun lewat alur baru saat sesi masih berjalan).

### Alur baru (menggantikan portal fisik seluruhnya)

`OnWaveCleared` → stage **Ready** (grimoire terbuka, tombol **LANJUT (SPACE)** di
tempat bekas MULAI WAVE) → `RunDirector.Depart()` → **Closing**: `Gloom.Shut` 0→1
selama `GameBalance.MapFadeClose` — kegelapan yang sehari-hari melingkari pemain
MERAPAT sampai menelan layar → `OnMapChoose` → UI membuka **peta satu layar penuh
mode MEMILIH** → klik node berdenyut → penanda berlian emas BERJALAN menyusuri
bezier jalur (seed sama dengan yang tergambar) → tirai hitam naik → **`PickNode`
dieksekusi SELAGI GELAP** (Relocate / EnterRest / StartWave — teleport dan ganti
wajah tidak pernah terlihat prosesnya) → **Opening**: Shut 1→0 selama `MapFadeOpen`
ke nilai standar.

- **Nilai standar material Gloom TIDAK PERNAH ditulis.** `Gloom.Shut` me-lerp nilai
  yang DIBACA LIVE dari aset lewat MaterialPropertyBlock (Inner→−3, Outer→−2,5,
  Wobble→0, Ceiling→1, alpha→1). Inner MINUS itu disengaja: smoothstep berambang
  negatif membuat jarak nol pun gelap penuh — lubang terangnya menutup betul, bukan
  menyisakan titik di pemain. Menyetel aset SAAT play tetap terlihat (Shut=0 = lerp
  identitas).
- **Gelap TOTAL dijahit dua lapis** (permintaan: "bener-bener gelap"): gloom untuk
  penjalarannya, plus `_fadeCover` (Image hitam fullscreen) menumpang 45% terakhir
  `RunDirector.Fade` — ujung transisi hitam murni, HUD pun tertelan.
- **Urutan tumpukan**: semua elemen peta hidup di `_mapRoot` (satu induk).
  Saat peta terbuka `_mapRoot` diangkat DI ATAS tirai (peta tampil di layar hitam);
  saat tirai naik menelan pilihan, peta dibiarkan di bawahnya.
- Gerbang lama tetap: `CanEmbark` (≥1 skill) dicek di `Depart`, bukan per node.

### Peta: fullscreen, tegak ala STS, bisa scroll & drag

- `GrimoireLayout.MapPanelRect()` = seluruh layar. `MapNodePos`: lajur = kolom,
  lantai menumpuk BAWAH→ATAS dengan jarak TETAP (`MapFloorGap` 110 px) — bukan
  dipadatkan supaya muat.
- **Lebar pita node = `panel.width * 0.34` (cap 700), BUKAN piksel tetap.** Angka
  mati 320 px membuat seluruh act menggumpal di tengah dan dua pertiga monitor
  kosong (keluhan user). Geser-lantai & jitter ikut diukur dari `colW`
  (×0,5 dan ×0,35) — kalau tetap piksel, pita lebar membuat jalurnya lurus lagi.
  Hasil akhirnya dijepit `panel.xMin/xMax ± 44` supaya lajur terluar tidak
  terpotong di resolusi mana pun.
- **Layout organik (putaran feedback ke-3, dibandingkan peta referensi user):**
  geser acak PER LANTAI ±40 px (garis antar lantai selalu menyerong — kolom lurus
  terbaca tabel, bukan jalan) + jitter per node ±28/±19 px, dua-duanya ber-seed.
  `MapLanes` di GameBalance dinaikkan 3→4 dan generator mengisi 2–4 node per
  lantai (60% +1, 30% +1) — peta terasa PENUH pilihan.
- **`ProtoInput.ScrollY` dinormalisasi ke GERIGI (±1)** — Input System baru memang
  memberi ±1, yang lama ±120 (dibagi 120 di wrapper). JEBAKAN yang sudah kejadian:
  menebak ±120 membuat scroll bergerak <1 px = terlihat mati total.
  Satu gerigi = 90 px ≈ satu lantai.
- **Drag-pan** (`ProtoInput.LeftHeld` baru): klik yang tidak kena node = pegangan;
  peta nempel di kursor. Berlaku di mode memilih DAN intip. `MapScrollMax`
  menjepit; buka pertama auto-scroll menjemput posisi pemain; saat penanda
  berjalan keluar jendela, peta menggulung menjemputnya sendiri.
- **KARAKTER pemain tampil di peta** (permintaan user): token BULAT kuning warna
  kapsul pemain + label KAMU — berdiri di RUANG TUNGGU di bawah lantai pertama
  sebelum langkah pertama act (ruangnya disediakan `bottom = yMin+150`), lalu
  berjalan menyusuri jalur saat node dipilih. Bulatnya `CircleSprite()` buatan
  sendiri 32 px — `GetBuiltinResource("UI/Skin/Knob.psd")` GAGAL di runtime,
  jangan dicoba lagi.

### Putaran feedback ke-4 (coretan user di screenshot)

- **Pemain = simpul pertama peta**: saat `Map.At < 0`, jalur EMAS digambar dari
  ruang tunggu (`MapEntryPos`) ke SEMUA node lantai pertama (`EntrySeed` per node),
  dan travel pembuka menyusuri bezier yang sama — bukan garis lurus diam-diam.
- **Lantai pertama 3–5 node** (generator: `3 + 50% + 35%`), lantai lain 2–4;
  `MapLanes` dinaikkan lagi 4→**5** (aset). Kesan "cuma 3 arah" mati.
- **Boss DIKUNCI MATI di tengah**: `MapNodePos` mengembalikan `panel.center.x`
  tanpa geser lantai / jitter untuk lantai puncak — tujuan act tidak boleh mencong.
- **Jalur dikalemkan**: `TrailControls` satu arah lengkung per ruas, amplitudo
  `length * 0.06` (dulu 0.3 dua arah = huruf S meliuk "tidak seirama") —
  nyaris lurus ala peta STS rujukan.
- **Spawn tidak di tengah kamera = jepitan arena**: titik lahir/`Relocate` kini
  dijepit juga oleh kotak-tengah kamera (`ArenaCamera.LimitX/LimitZ` baru;
  bootstrap memakai rumus yang sama sebelum ArenaCamera lahir) — pemain selalu
  lahir TERTENGAHKAN, tidak menepi sejak frame pertama.
- **Culling `MapInView`**: node/segmen yang tergulung keluar jendela disembunyikan,
  tidak digambar menimpa judul/legenda. Signature layout ditambah scroll
  (dibulatkan) — menggulung = relayout, diam = tidak.
- **JANGAN simpan titik asal penanda saat travel** — peta bisa digulung di tengah
  perjalanan; `DrawMapMarker` menghitung ulang asal tiap frame dari `Map.At`.
- Peta intip (M / tombol PETA) tetap ada, tetap kaca — cuma sekarang juga satu
  layar penuh. Mode MEMILIH menelan M dan semua klik di luar node: memilih wajib.

### Pulau rehat = tempat LAIN betulan (`Biome_sanctum.asset`)

- `EnterRest` memasang **wajah Suaka** lewat `BiomeDresser.ShowBiome()` SELAGI
  GELAP: ungu berkabut rapat (fog 14–62), bulan ungu redup, hutan siluet 55%,
  kunang-kunang + bara saja, SATU WeatherMood kosong (tidak pernah hujan di suaka).
- Aset = duplikat malam yang di-tweak **via MCP, BUKAN BiomePass** — pass mana pun
  TIDAK meregenerasinya; dari sini dia MILIK USER untuk dituning tangan.
  Rujukannya `ProtoBootstrap._restBiome` (scene Proto); kosong = pulau memakai
  wajah wave seperti dulu.
- Wajah normal kembali SENDIRI: node tempur berikutnya memicu `OnWaveStarted` →
  undian wajah biasa. Rest beruntun cukup memasang Suaka lagi.
- `LeaveRest` terjadi di `PickNode` dalam gelap (bongkar pulau tak pernah terlihat);
  `_returnPos` dan portal LANJUT pensiun — pulang selalu lewat peta.

### Sisa & jebakan

- `PlayerMotor.WalkTo` tidak dipakai lagi (masih ada, tidak dihapus).
  `RunDirector.ClickBlocked` DIHAPUS — tidak ada lagi klik dunia yang perlu diblok.
- Knob transisi di `GameBalance` header "Transisi peta (gloom)":
  `MapFadeClose` 1,1 / `MapFadeOpen` 1,4 / `MapMarkerTravel` 0,8.
- `ScreenCapture.CaptureScreenshot` dari `execute_code` memicu error
  "PlayerLoop internal function has been called recursively" — artefak alat ukur,
  bukan bug game. Abaikan.
- "There are no audio listeners in the scene" sudah ada SEBELUM sesi ini — belum
  disentuh, kandidat kerjaan kecil berikutnya.

## 25. Folder aset, VFX diadopsi, dan slot VFX skill (2026-08-09)

Daftar aset lengkap + speknya: **docs/ASSET-LIST.md**.

### Struktur folder baru

`Assets/Art/{UI,Icons,Map,VFX,Characters,Props}` + `Assets/Audio/{Music,SFX,Ambience}`.
Tiap folder daun berisi `.gitkeep` (Unity mengabaikan berkas berawalan titik, jadi
tidak ada .meta yatim). Aturan letak cuma dua: PNG yang SUDAH dirujuk aset piece
tetap di `GameData/Icons` (ganti art = TIMPA filenya), sisanya di `Art/`/`Audio/`.

### 23 prefab VFX diadopsi keluar dari paket Lana

`Assets/Art/VFX/Prefabs/{Light,Ambient,Weather,Skill}` — dipisah per PEKERJAAN,
bukan per paket asal. Dipindah dengan `AssetDatabase.MoveAsset` (GUID awet, rujukan
biome utuh).

**Alasannya bukan kerapian saja:** `Sunlight`/`Moonlight` disetel tangan pemilik
project (§22). Selama masih di dalam paket, reimport/update paket menimpanya tanpa
peringatan. Sisa paket tidak disentuh; ~33 prefab masih nganggur di sana (Snow,
Sandstorm, Bubbles, dll) — kandidat biome salju/rawa.

### Slot VFX skill: `PieceDefinition.ZoneVfx`

Skill selama ini menggambar FX-nya dari primitif di `PlayerCaster`. Sekarang
`Kind = Zone` bisa membawa prefab sendiri:

- `ZoneVfx` (prefab) + `ZoneVfxScale`. Skala akhir = `ZoneVfxScale × max(0,35;
  radius/3)` — efek ikut membesar saat area di-buff, kalau tidak skill yang sudah
  diperbesar terlihat persis sama dengan yang belum.
- `PlayerCaster.AttachZoneVfx` dipanggil TIAP kubangan lahir dan membandingkan
  `VfxSource` dengan prefab yang diminta — **zone diambil dari pool**, dan pemilik
  sebelumnya bisa skill lain. Tanpa perbandingan itu efek lama ikut terbawa.
- Efeknya digantung di `_fxRoot`, **BUKAN** jadi anak cakramnya: cakram itu
  dipipihkan (skala Y 0,03) jadi genangan, dan anak apa pun ikut gepeng.
- Zone habis → efek di-SetActive(false), bukan dihancurkan (dipakai lagi).
  Zone yang mengembara (`ZoneDrift`) membawa efeknya ikut pindah.
- Cakramnya ditipiskan (alpha ×0,3) kalau ada `ZoneVfx` — tugasnya berubah jadi
  penanda jangkauan, bukan efeknya sendiri.

## 26. Art UI masuk: perkamen peta, gloom tepi, papan grimoire (2026-08-09)

Catatan penuh ada di `production/session-state/active.md`. Yang wajib diingat:

- **`UiTheme` SO** (`Assets/GameData/UiTheme.asset`) — kertas, bingkai, warna tinta,
  knob gloom. Boleh null: tanpa tema, UI balik jadi kotak warna datar.
- **Shader `Grimoire/GloomEdge`** — saudara KANVAS dari `Grimoire/Gloom`. Shader lama
  tidak bisa dipakai di UI (`positionWS` + pass `UniversalForward`). Yang dipinjam cuma
  keputusannya: **derau menggoyang GARIS BATAS, bukan kepekatan**. Jarak dihitung dalam
  PIKSEL (`_RectSize` dari C#) supaya peta besar dan peta intip terlihat sebahan.
- `_PaperMode = 1` → gambar spritenya sendiri + **SOBEK** tepinya. Yang membedakan sobek
  dari pudar bukan seberapa jauh alpha turun melainkan seberapa CEPAT.
- **Lapisan gloom WAJIB memakai potongan sobek yang sama** — kalau tidak, muncul kotak
  gelap membayangi kertas yang sudah tercabik.
- **`GrimoirePanel.prefab`** (`Assets/Art/UI/Prefabs/`) — lihat §27, sekarang prefabnya
  yang menentukan letak petak.

## 27. Magenta di pulau rehat, LANJUT yang tak bisa dipencet, gloom beku, petak dari prefab (2026-08-09)

Empat keluhan dari satu screenshot pemilik project. Tiga di antaranya bug diam — tidak
satu pun melempar error yang menyebut dirinya.

### Magenta menutupi setengah layar di pulau rehat

**Akar: `MagicField_pink.prefab`, anak `fog`, materialnya `{fileID: 0}`.** Renderer
bermaterial kosong di URP digambar dengan warna error, dan partikel kabut selebar
19,6 unit itu menutupi sebagian besar layar. Biome Suaka memanggilnya dengan
`Chance: 1, Count: 2` — persis dua yang muncul.

Bukan referensi yang putus saat prefab diadopsi: slotnya memang tercatat kosong dari
vendornya. Saudaranya yang sehat, `MagicField_blue`, memakai **`Additive_soft`** di anak
yang sama — itu yang dipasang, ditiru bukan ditebak.

**Empat prefab lain punya lubang yang sama** (`Tornado_sand`, `Tornado_snow`, `Rockfall`,
`SpeedBoost_front` — semuanya di renderer ROOT). Tiga aman karena renderer root-nya mati;
**`SpeedBoost_front` tidak** (renderer hidup, emisi 50/detik) dan sudah diisi `Additive`.

> **Cara mencarinya lagi:** scan `Renderer` saja TIDAK cukup — `Terrain` bukan `Renderer`,
> dan objek yang dibangun runtime tidak ada di scene saat edit mode. Yang menemukannya:
> masuk play mode, paksa `RunDirector.EnterRest` lewat refleksi, lalu daftar semua renderer
> aktif yang `sharedMaterial == null || !shader.isSupported`.

### Tombol LANJUT tidak bisa diklik di toko

`HandlePanelClick` dipanggil SEBELUM pengecekan tombol start, dan barisan terakhirnya
menelan setiap klik yang jatuh di dalam `PanelRect()` (`return true`). Di pulau rehat
panel toko **tumpang tindih** dengan tombolnya — terverifikasi:
`PanelRect (644,354,632,372)` memuat titik tengah `StartButtonRect (820,632,280,56)`.
Jadi satu-satunya tombol untuk pergi dari toko dimakan diam-diam, tanpa tanda apa pun.

Perbaikannya satu baris, dan **letaknya yang penting**: guard ditaruh SESUDAH blok peta,
kejadian, dan slot (ketiganya modal sungguhan yang memang harus menelan segalanya) tapi
SEBELUM blok toko — toko bukan modal, klik di luarnya saja sudah menutupnya.

### Gloom peta tidak bergerak

`Tune()` mengirim `_Scale`, `_Wobble`, dan knob sobek, tapi **tidak pernah mengirim
`_Churn` dan `_Drift`**. Materialnya dibuat runtime dari shader, jadi keduanya jatuh ke
bawaan shader: `Drift 6 / Scale 210` = **0,029 gumpalan per detik** — beranimasi kalau
diukur, beku kalau dilihat.

`UiTheme.GloomChurn` (0,3) dan `GloomDrift` (42) baru, disamakan dengan `Gloom.mat` milik
kegelapan arena (`Churn 0,3`, `Drift 0,6` pada `Scale 3` = 0,2 gumpalan/detik) yang sudah
dinilai bergerak benar. Terukur sesudahnya: **0,200**, tujuh kali lebih cepat.

### Petak grimoire diatur dari prefab

Dulu prefab papan cuma DIDUDUKKAN kode di pojok petak hitungan; letak petaknya sendiri
konstanta. Sekarang dibalik.

- Anak bernama **`GridArea`** di dalam prefab menentukan letak DAN ukuran petak 7x7.
  Dicari lewat nama, bukan komponen penanda — menata papan tidak boleh menuntut kode.
- `GrimoireLayout.GridOverride` (`Rect?`) menampungnya. `CellSize`/`CellGap`/
  `LooseCellSize` berhenti jadi `const` dan jadi property; `GridX`/`GridY` membaca
  override kalau ada. **Null = perilaku lama persis** — prefab tanpa `GridArea`, atau
  tanpa prefab sama sekali, tetap dapat petak yang benar.
- Ditulis ulang di SETIAP pembangunan UI termasuk dikembalikan ke null: play mode tanpa
  domain reload akan mewarisi kotak milik run sebelumnya.
- **Celah ditambahkan sebelum dibagi** (`(width + gap) / 7`): petak 7x7 memakai tujuh sel
  tapi hanya enam celah. Membagi lebar mentah membayar celah yang tidak ada dan petaknya
  berhenti 3 px sebelum tepi kanan kotak.
- Sel dijaga persegi lewat `Mathf.Min` — kotak yang tidak sebangun menyisakan ruang di
  sisi panjangnya, jauh lebih baik daripada petak gepeng.
- Letak lama (`Margin + GridInset` = 76, `Margin + 8` = 28) dipindahkan KE DALAM prefab,
  jadi papan tidak bergeser sedikit pun setelah perubahan ini. Terukur: `GridRect()` =
  `(76, 28, 298, 298)`, `CellSize` 40, `CellGap` 3 — identik dengan sebelumnya.

### Kunang-kunang dibangun ulang dari aset paket

Yang lama: partikel bangkitan `BiomePass.EnsureFireflies`, butiran 0,07–0,14 unit dengan
material pinjaman dari bara — terbaca murah. Sekarang `Fireflies.prefab` diturunkan dari
anak **`sparks` milik `MagicField_blue`** (texture sheet, size-over-lifetime, glow additive
— semua sudah disetel pembuat paketnya), lalu diberi umur 5–9 detik, noise pengembara,
warna kuning-hijau, dan gradasi kedip yang sama seperti versi lama.

> **Jebakan:** `EnsureFireflies` masih ada dan masih membangun versi LAMA yang lebih miskin.
> Ia hanya jalan kalau asetnya hilang. Kalau sampai jalan, tulis ulang lagi dari `sparks`.

Terpasang: Storm Cell←Tornado_sand, Snowstorm←Tornado_snow, Ion Storm←Orb_lightning,
Ashfall←Rockfall. Terverifikasi play mode: 2 zone, 119 partikel, pusaran terlihat.

### JEBAKAN: efek cuaca MENGABAIKAN RepeatEvery

`Weather.BuildAmbient` (penjadwal "lewat lalu pergi") hanya membaca
`BiomeDefinition.AmbientVfx`. `WeatherMood.Effects` di-spawn permanen selama mood
itu aktif — `RepeatEvery`, `Lifetime`, `MinDistance`, `Count` **tidak berlaku**
di sana. Tornado sempat dipasang di mood "Berangin" dan akan berdiri diam
sepanjang wave; dicopot, dipindah ke skill. Kalau suatu saat butuh kejadian yang
LEWAT saat cuaca tertentu, jalurnya harus dibuat dulu — belum ada.

### Peta run: angka final setelah putaran feedback

`MapFloorsPerAct` **34**, `MapLanes` **5**, gap antar lantai **170 px**, pita node
`lebar layar × 0,26` (cap 560), ruang tunggu 310 px dari tepi bawah, satu gerigi
roda = satu lantai. Toko/kejadian/slot diturunkan (0,10 / 0,07 / 0,05) supaya
jalur tidak penuh rehat. **Terukur 400 run simulasi: 27,7 wave rata-rata
(min 21, maks 34), 6,3 node rehat** — permintaan pemilik project 25–30 wave.
