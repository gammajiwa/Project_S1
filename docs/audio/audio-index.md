# Indeks Pemakaian Audio — Grimoire Master

Pustaka audio ini disalin bulat-bulat dari **project_b** — total **337 berkas** di `Assets/Audio/` — dan sampai sekarang **belum satu pun tersambung ke kode** (grep `AudioDirector` di GrimoireUI = nol; `Sound.Blast/Hit/WaveStart` masih disintesis prosedural). Jadi dokumen ini adalah **peta jalan pemasangan**, bukan laporan kondisi terpasang. Semua pemetaan di bawah sudah lolos verifikasi adversarial: path dicek ada, dan sasaran kode/asset-nya dibuktikan baris-per-baris. Yang mapping-nya gugur atau tanpa padanan dikumpulkan di bagian **Celah** paling bawah.

## Musik

| Berkas | Dipakai untuk | Catatan |
|---|---|---|
| `Audio/Music/MENU_Chase MENU LOOP.wav` | Main Menu — halaman Root (`Page.Root` di `MainMenuController`) | Layar pertama saat game dibuka; `Awake()` diakhiri `Show(Page.Root)` |
| `Audio/Music/COMBAT_Forest LOOP.wav` | Wave pertarungan aktif di biome forest — `EnemyManager.WaveActive == true` (via `StartWave()` / `OnWaveStarted`) | Forest ("Verdant Hollow") satu-satunya keluarga biome; varian lain cuma forest_night/sore/midnight |
| `Audio/Music/DEMOMAP_Item Store 2 LOOP.wav` | Panel TOKO di Pulau Rehat — `RunNodeKind.Shop`, `GrimoireUI._shopOpen` via `OnRestEntered` | Prefix `DEMOMAP_` warisan asset pack; isi "Item Store" tidak ambigu |
| `Audio/Music/HEROSELECT_Casual Level LOOP.wav` | Layar pilih starter — `StarterSelectPanel`, `Page.Starter` di `MainMenuController` | Dibuka `StartGame()` saat `_starterPage` terpasang |
| `Audio/Music/LEVELUP_Marimba SHORT WIN.wav` | Kejadian **EVOLVE** — dua rune melebur, banner "EVOLVE!" di GrimoireUI | Tidak ada sistem level-up di project (terverifikasi grep). Pemicu: `ResolveEvolutions` non-kosong (GrimoireUI.cs:2932, `hud.evolve`) |
| `Audio/Music/LOSE_Casual Lose 3.wav` | Layar GAME OVER — `Player.Alive == false`, `DrawGameOver()`/`ShowGameOver(true)` | `hud.gameover.title` = "GAME OVER" |
| `Audio/Music/WIN_Forest WIN.wav` | Wave selesai — `Enemies.OnWaveCleared`, banner "WAVE {0} BERES" | Tidak ada state menang-run terpisah (terverifikasi). Catatan desain: wave clear sering banget — stinger penuh tiap wave bisa berlebihan; alternatif `OnBossDied` (EnemyManager.cs:1074) patut ditimbang |

## SFX Elemen (`Audio/SFX/Element/`)

Skema tier LOW/MEDIUM/HIGH mengikuti `PieceDefinition.Stars` (1–5): LOW = bintang 1–2, MEDIUM = bintang 3, HIGH = bintang 4–5.

| Berkas | Dipakai untuk | Catatan |
|---|---|---|
| `Audio/SFX/Element/Fire/LOW_EM_FIRE_CAST_01.ogg` | Element.Fire — CAST, tier LOW | Nama file eksplisit CAST + prefix LOW |
| `Audio/SFX/Element/Fire/MEDIUM_EM_FIRE_IMPACT_01.ogg` | Element.Fire — IMPACT, tier MEDIUM | Satu keluarga dengan LOW_EM_FIRE_CAST |
| `Audio/SFX/Element/Fire/HIGH_Card_Game_Play_Punch_Fire_03.wav` | Element.Fire — IMPACT, tier HIGH | "Punch" dibaca sebagai dampak — ambigu tapi wajar |
| `Audio/SFX/Element/Ice/LOW_EM_ICE_CAST_IMPACT_01.ogg` | Element.Ice — CAST+IMPACT digabung satu file, tier LOW | Nama file memang gabungan CAST_IMPACT |
| `Audio/SFX/Element/Ice/HIGH_EM_ICE_CAST_IMPACT_HARD_01.ogg` | Element.Ice — CAST+IMPACT versi HARD, tier HIGH | Tier MEDIUM Ice **kosong** — lihat Celah |
| `Audio/SFX/Element/Electricity/EM_LIGHT_IMPACT_01.ogg` | Element.Lightning — IMPACT, lintas semua bintang | Satu-satunya file di folder; enum-nya `Lightning`, bukan Electricity |
| `Audio/SFX/Element/Explossion/LOW_EXPLOSION_Arcade_14_mono.wav` | `Sound.Blast` tier LOW — ledakan generik lintas elemen (Nova/AreaAtTarget/RollingBall) | Folder memang typo "Explossion". `Sound.Blast` sekarang masih disintesis (Noise) |
| `Audio/SFX/Element/Explossion/MEDIUM_EXPLOSION_Arcade_12_mono.wav` | `Sound.Blast` tier MEDIUM | Bagian tengah set 3-tier yang satu jenis bunyi |
| `Audio/SFX/Element/Explossion/HIGH_EXPLOSION_Arcade_08_mono.wav` | `Sound.Blast` tier HIGH | Melengkapi set tiering Blast |
| `Audio/SFX/Element/Magic/LOW_Card_Game_Action_Magic_Sword_01.wav` | Element.Arcane — CAST, tier LOW | Arcane tak punya folder sendiri; folder Magic rumah paling wajar |
| `Audio/SFX/Element/Magic/MEDIUM_Card_Game_Action_Water_Splash_01.wav` | **ANOMALI salah-folder** — kalau dipakai apa adanya: Arcane tier MEDIUM | Nama bertema Water di folder Magic; butuh keputusan manual sebelum dipakai |
| `Audio/SFX/Element/Magic/HIGH_Card_Game_Alert_End_Round_01.wav` | **Bukan** sfx elemen — kandidat `Sound.WaveStart` / stinger ritme wave | `Sound.WaveStart` masih disintesis (Chime). Nama bilang "End Round", slotnya Wave START — arah kebalik |
| `Audio/SFX/Element/Hit/PIERCE_Card_Game_Magic_Pierce_01.wav` | Fallback impact lintas elemen untuk `CastKind.Projectile` / `Seeker` | Prefix PIERCE selaras sifat peluru/rudal |
| `Audio/SFX/Element/Hit/SLASH_FeelBarbariansAttack4.wav` | Fallback impact untuk `CastKind.Orbital` | Orbital = "bilah yang MENGITARI pemain" — slash cocok |
| `Audio/SFX/Element/Hit/BLUNT_BLUNT_Swing_Hit_Generic_04_mono.wav` | Fallback impact untuk `CastKind.ForcePush` / `Vortex` | Benturan tumpul selaras mekanik dorong/hantam |
| `Audio/SFX/Element/Hit/GENERIC_IMPACT_Generic_09_mono.wav` | Fallback default `Sound.Hit` lintas elemen, terutama Arcane | Slot `Overrides[Hit]` siap menerima tanpa mengubah pemanggil |
| `Audio/SFX/Element/Hit/GENERIC2_IMPACT_Generic_01_mono.wav` | Variasi kedua `Sound.Hit` (rotasi dengan GENERIC_IMPACT_Generic_09) | Mekanisme rotasi multi-clip **belum ada** — lihat Celah |
| `Audio/SFX/Element/Buff/HIT_Card_Game_Effect_Bless_02.wav` | Buff menempel ke pemain — `GrantOnCast` via `Surge`/`Ward`/`Restore` | Lintas elemen; "Bless" untuk efek positif menempel |
| `Audio/SFX/Element/Buff/SELF_Card_Game_Effect_Puncture_02.wav` | CAST skill utility-ke-diri (`Surge`/`Ward` saat dilepas) | Inferensi dari prefix SELF; nama "Puncture" tak cocok tema (sudah diakui) |
| `Audio/SFX/Element/Movement/DASH_WHOOSH_Wide_Slow_stereo.wav` | `CastKind.Blink` — lompat menjauh dari titik terpadat | Padanan langsung dengan doc comment Blink |
| `Audio/SFX/Element/Movement/DROP_WHOOSH_Noisy_02_mono.wav` | `CastKind.SunStrike` — hantaman turun setelah telegraf | Alternatif lemah: `Orbit` (doc-nya tidak bilang "turun") |
| `Audio/SFX/Element/Poison/LOW_Card_Game_Action_Block_Magic_01.wav` | Kandidat: `CastKind.Ward` momen CAST, ATAU cast piece Arcane ber-`Status_poison` (Poison Pool, Plague Brand) | Tidak ada Element.Poison di enum — kedua piece itu Arcane dengan `AppliedStatus` → Status_poison |
| `Audio/SFX/Element/Water/MEDIUM_EM_WATER_LAUNCH_01.ogg` | Kandidat reuse: penambal gap tier MEDIUM Ice, atau momen launch `ForcePush`/`Vortex` | Tidak ada Element.Water maupun status "wet" — ini murni reuse produksi |
| `Audio/SFX/Element/Wind/HIGH_Card_Game_Action_Push_Wind_02.wav` | `CastKind.ForcePush` momen dorongan, tier HIGH | Dipetakan ke mekanik (Kind), bukan elemen — tidak ada Element.Wind |
| `Audio/SFX/Element/Wind/LOW_Card_Game_Action_Push_Wind_01.wav` | `CastKind.ForcePush` momen dorongan, tier LOW | Pasangan LOW dari file di atas |

## SFX Skill (`Audio/SFX/Skill/`)

57 file diperiksa: 39 lolos, 18 ditolak. Tabel ini hanya yang punya padanan nyata; yang tanpa padanan dan yang ditolak masuk bagian Celah.

| Berkas | Dipakai untuk | Catatan |
|---|---|---|
| `Audio/SFX/Skill/sfx_cat_absolutezero.wav` | Cast/hit Piece "Absolute Zero" | Match persis by Id (`Piece_absolutezero`, bintang 5, Ice) |
| `Audio/SFX/Skill/sfx_cat_tempest.wav` | Cast/hit Piece "Tempest Sigil" | Match persis by Id (`Piece_tempest`) |
| `Audio/SFX/Skill/sfx_cat_firestorm.wav` | Proc reaction "FIRESTORM" | `Reaction_badaiapi`; event `Sound.Reaction` ada di AudioDirector |
| `Audio/SFX/Skill/sfx_cat_shatter.wav` | Proc reaction "SHATTER" | `Reaction_pecah` — match persis |
| `Audio/SFX/Skill/sfx_cat_stun.wav` | Penerapan status "STUN" | `Status_stun` — match persis |
| `Audio/SFX/Skill/sfx_cat_poison.wav` | Penerapan/tick status "POISON" | `Status_poison` — match persis |
| `Audio/SFX/Skill/sfx_cat_hit.wav` | Generik kena pukul | `Sound.Hit` nyata di AudioDirector.cs |
| `Audio/SFX/Skill/sfx_cat_death.wav` | Generik musuh mati | `Sound.Death` nyata; benar kategori event, bukan skill |
| `Audio/SFX/Skill/sfx_cat_spawn.wav` | Generik musuh muncul | Spawning nyata di EnemyManager |
| `Audio/SFX/Skill/sfx_cat_heal.wav` | Generik heal — menaungi Minor Heal & Greater Heal | Plus `CastKind.Heal`; tidak ada piece literal "Heal" |
| `Audio/SFX/Skill/sfx_cat_shield.wav` | Generik ward/perisai (Bulwark, Aegis, Lesser Ward, Buff AEGIS) | Piece-piece Kind 14 terbukti; `Buff_perisai` "AEGIS" tak punya field Kind (catatan kecil) |
| `Audio/SFX/Skill/sfx_cat_buff.wav` | Generik penerapan buff positif | POWER, FRENZY, Fortify terbukti di GameData/Buffs |
| `Audio/SFX/Skill/sfx_cat_debuff.wav` | Generik penerapan debuff/kutukan | WEAKENED, SLUGGISH, LEADEN, DRAINED terbukti |
| `Audio/SFX/Skill/sfx_cat_blunt.wav` | Generik impact tumpul lintas piece | Piece berflavour tumpul nyata (Quake, Shove, Sunder, Meteor) |
| `Audio/SFX/Skill/sfx_cat_pierce.wav` | Generik impact tusukan lintas piece | Ice Lance Sigil, Sun Lance, Void Lance Sigil, Glacial Spike, Frost Shard |
| `Audio/SFX/Skill/sfx_cat_slash.wav` | Generik impact sabetan lintas piece | Lightning Slash, Whirling Blade, Blade Dance, Moon Glaive, Chakram |
| `Audio/SFX/Skill/sfx_cat_fire.wav` | Generik elemen Fire | `Element.Fire` resmi di Enums.cs |
| `Audio/SFX/Skill/sfx_cat_ice.wav` | Generik elemen Ice | `Element.Ice` resmi |
| `Audio/SFX/Skill/sfx_cat_lightning.wav` | Generik elemen Lightning | `Element.Lightning` resmi |
| `Audio/SFX/Skill/sfx_ignite.wav` | Kandidat penerapan status BURN (satu-satunya status api) | Bukan match Id persis — anchor nyata, relasi jelas |
| `Audio/SFX/Skill/sfx_staticshock.wav` | Kandidat penerapan status SHOCK (satu-satunya status petir) | Bukan match Id persis — anchor nyata |
| `Audio/SFX/Skill/sfx_stormcall.wav` | Generik cast bertema badai | Bukan match persis ke Hero "Stormcaller"; belasan piece storm + `Sound.Cast` nyata |
| `Audio/SFX/Skill/sfx_thunderbolt.wav` | Generik elemen petir (peran setara sfx_cat_lightning) | Tidak ada piece literal "Thunderbolt" — 6 piece petir dicek semua |
| `Audio/SFX/Skill/sfx_slash.wav` | Generik impact sabetan (duplikat kategori sfx_cat_slash) | Konsisten dengan sfx_cat_slash |

## SFX Antarmuka (`Audio/SFX/Card/` dan `Audio/SFX/UI/`)

Ketiga jalur interaksi piece di GrimoireUI saat ini **bisu total** (grep AudioDirector di GrimoireUI.cs = nol hasil).

| Berkas | Dipakai untuk | Catatan |
|---|---|---|
| `Audio/SFX/Card/SELECT_Card_Game_Movement_Deal_Single_03.wav` | Mengambil piece — `HandleInput()` GrimoireUI.cs: ambil dari loose (2646), dari bag (2658), dari book (2673) | Momen serah-terima piece ke tangan; ketiga jalur pickup terverifikasi |
| `Audio/SFX/Card/PLAY_Card_Game_Movement_Deal_Single_Whoosh_03.wav` | Menaruh piece — `Book.Place` (2616), `_bag.Place` (2592), `AddLoose` saat beli dari toko (2842) | Piece terbang lalu mendarat; blok pembelian toko 2833–2845 tercek |
| `Audio/SFX/Card/HOVER_Card_Game_Movement_Deal_Single_02.wav` | Hover di atas piece — `UpdateTooltip()` (4882–4978), memicu `_recipes.Show` / `ShowCard` | **Butuh gerbang debounce**: bunyi hanya saat objek hover berubah. Terpisah dari hover tombol menu (file HOVER di folder UI) |
| `Audio/SFX/Card/DISCARD_Card_Game_Movement_Shuffle_03.wav` | REROLL toko — `RollShop()` (1365) via blok RerollRect (2827) | Isi file bunyi shuffle; reroll harfiah mengocok ulang rak. Tidak ada mekanik discard piece di game. Alternatif: overflow auto-sell di `AddLoose()` (2118–2125) |
| `Audio/SFX/UI/CLICK_Card_Game_UI_General_Click_Shimmer_01.wav` | Klik tombol umum: toggle TOKO, MULAI/LANJUT, REROLL, spin gamble, TakePact/RefusePact, node peta, semua `Button.onClick` di MainMenuController via `Wire()` | 7 lokasi klik tercek baris-per-baris; MainMenuController UGUI Button polos tanpa audio. AudioDirector selama ini cuma dikabel ke kombat (ProtoBootstrap.cs) |
| `Audio/SFX/UI/EXIT_Card_Game_UI_Button_Light_Metallic_Dull_01.wav` | Menutup panel/kembali: klik-luar panel toko (2817) & gamble (2755), tombol back & Quit di MainMenuController | Gerbang cukup "panel berubah dari terbuka ke tertutup" — saat `_held != null` panel tetap tertutup, cuma kliknya yang ditelan (tak perlu logika pengecualian) |

## Pustaka Pack (`Audio/Library/`)

Kurasi dari UniversalSoundFX, GameMakersKit, dan Feel/NiceVibrations.

| Berkas | Dipakai untuk | Catatan |
|---|---|---|
| `Audio/Library/UniversalSoundFX/MAGIC_SPELLS/MAGIC_SPELL_Flame_01_mono.wav` | Cast skill Element.Fire (Projectile/Nova api) | Elemen dan kedua CastKind terbukti di Enums.cs |
| `Audio/Library/UniversalSoundFX/MAGIC_SPELLS/MAGIC_SPELL_Dark_Pulse_Echo_Subtle_stereo.wav` | Cast skill Element.Arcane (mis. Detonate/Vortex bertema arcane) | "Dark pulse" untuk elemen misterius, dibedakan dari elemen fisik |
| `Audio/Library/UniversalSoundFX/MAGIC_SPELLS/MAGIC_SPELL_Energy_Mehanical_01_mono.wav` | Cast generik `CastKind.Chain` / `Tether` | Typo "Mehanical" memang nama file aslinya |
| `Audio/Library/UniversalSoundFX/MAGIC_SPELLS/MAGIC_SPELL_Fast_Bolt_Metallic_Dirty_Tail_Subtle_stereo.wav` | Pelepasan `CastKind.Projectile` / `Seeker` | "Fast bolt" cocok doc comment Projectile |
| `Audio/Library/GameMakersKit/Card_Game/Magic/Card_Game_Magic_Ice_Shield_01.wav` | Cast Element.Ice dan/atau `CastKind.Ward` | Ward = "perisai yang MENYERAP damage" — cocok kata per kata |
| `Audio/Library/UniversalSoundFX/IMPACTS/Energy/IMPACT_Energy_Solid_11_mono.wav` | Kena-hit proyektil sihir mendarat (Projectile, Seeker, Boomerang, Ricochet) | Impact generik lintas proyektil — pola standar |
| `Audio/Library/UniversalSoundFX/IMPACTS/Energy/IMPACT_Energy_Solid_14_mono.wav` | Varian kedua impact energi, dirotasi random dengan Solid_11 | Anti-repetisi untuk game hujan proyektil |
| `Audio/Library/UniversalSoundFX/GORE_SPLATS/GORE_Splat_Hit_Deep_mono.wav` | Kena-hit berat boss cacing (body hit besar) | Boss cacing nyata: BossDefinition.cs, BossSnake.cs, mesh SK_Worm |
| `Audio/Library/UniversalSoundFX/GORE_SPLATS/SPLAT_Crunch_Crack_01_mono.wav` | Kematian musuh gerombolan (swarm death) | Ringan dan sering kepakai — sesuai genre survivor arena |
| `Audio/Library/UniversalSoundFX/THUDS_THUMPS/THUD_Squishy_05_mono.wav` | Gerakan/hentakan tubuh boss cacing (slam, pindah segmen) | BossSnake punya siklus Burrow/Surfaced/lunge — momen hentakan nyata |
| `Audio/Library/Feel/NiceVibrations/HapticSamples/Nature/CreatureRoar1.wav` | Aggro/muncul boss cacing (roar intro) | Boss memang menyembur dari bawah tanah. Klaim "masuk fase baru" tidak didukung kode — BossSnake tak punya sistem fase |
| `Audio/Library/UniversalSoundFX/WHOOSHES/Classic/WHOOSH_Wide_Fast_stereo.wav` | Cast `CastKind.Line` / `ForcePush` / `Shockwave` (sapuan cepat sekali-letup) | Pemetaan sesuai perilaku, bukan sekadar nama |
| `Audio/Library/UniversalSoundFX/WHOOSHES/Classic/WHOOSH_Wide_Slow_stereo.wav` | Cast `CastKind.Vortex` / `Orbital` (efek berdurasi beberapa detik) | Keduanya memang efek menahan/berlangsung |
| `Audio/Library/UniversalSoundFX/WHOOSHES/Mixed/WHOOSH_Noisy_02_mono.wav` | Cast `CastKind.Radial` | "Menyemburkan peluru ke SEGALA ARAH" — cocok persis |
| `Audio/Library/GameMakersKit/Card_Game/Effects/Card_Game_Effect_Bless_01.wav` | `CastKind.Heal` / `Restore` / `Surge` (efek positif ke diri sendiri) | Sejalan karakter bunyi "bless" |
| `Audio/Library/GameMakersKit/Card_Game/Actions/Card_Game_Action_Break_Charm_01.wav` | `CastKind.Cleanse` — buang semua debuff menempel | Doc comment cocok kata per kata |
| `Audio/Library/GameMakersKit/Card_Game/Alerts/Card_Game_Alert_Time_Freeze_01.wav` | Telegraf `CastKind.SunStrike` (tanda di tanah sebelum hantaman) | "Telegraf-nya bukan hiasan" — kebutuhan bunyi peringatan nyata di desain |
| `Audio/Library/GameMakersKit/Card_Game/Achievements/Card_Game_Achievement_Twinkle_Dust_01.wav` | Konfirmasi rune/piece ditempel ke slot kosong papan grimoire | `Book.Place`, drag preview, `Book.Placed` semua nyata |
| `Audio/Library/GameMakersKit/Card_Game/User_Interface/Buttons/Card_Game_UI_Button_Light_Metallic_01.wav` | Klik tombol menu umum (start, pause, konfirmasi, back) | Menu UI nyata (MainMenuBuilder, CodexPanel, StarterSelectPanel) |

## Celah

### Kebutuhan yang belum punya berkas / gap produksi

- **Tier MEDIUM Ice kosong** — folder Ice cuma punya LOW dan HIGH (diverifikasi Glob). Kandidat penambal: `Audio/SFX/Element/Water/MEDIUM_EM_WATER_LAUNCH_01.ogg`.
- **Rotasi multi-clip belum ada di AudioDirector** — `Overrides` cuma 1 clip per Sound, sekarang hanya pitch yang diacak. Dibutuhkan supaya pasangan GENERIC/GENERIC2 impact bisa dirotasi.
- **Tidak ada state "menang run" terpisah** — `WIN_Forest WIN.wav` sementara dipetakan ke wave clear; alternatif hook `OnBossDied` patut dipertimbangkan saat implementasi.
- **Tidak ada sistem level-up/XP/upgrade** — `LEVELUP_Marimba SHORT WIN.wav` dialihkan ke momen EVOLVE.

### Berkas Skill tanpa padanan di game (semua di `Audio/SFX/Skill/`)

- `sfx_cat_water.wav`, `sfx_soak.wav` — tidak ada elemen/status Water (grep = 0).
- `sfx_cat_wind.wav` — tidak ada elemen Wind.
- `sfx_cat_conductive.wav`, `sfx_cat_miasma.wav`, `sfx_cat_plasmaburst.wav`, `sfx_cat_superstorm.wav`, `sfx_cat_thunderstorm.wav`, `sfx_cat_wildfire.wav` — tidak ada mekanik/reaction/piece dengan nama itu (grep = 0; 9 reaction yang ada sudah diverifikasi satu-satu).
- `sfx_cat_cryoshock.wav` — reaction sejenis di game bernama "STATIC FREEZE".
- `sfx_cat_frozenblood.wav` — reaction bleed di game bernama "BLOOD SURGE".
- `sfx_cat_toxicflame.wav` — reaction sejenis di game bernama "TOXIC BURST".
- `sfx_cat_plaguewind.wav` — hanya mirip "Plague Sigil" / "Plague Brand", bukan match.
- `sfx_cat_steam.wav` — tidak ada reaction "Steam"; kandidat tak-langsung terbaik: Piece "Steam Burst".
- `sfx_cat_parrysuccess.wav` — nol mekanik parry di codebase.
- `sfx_cat_levelup.wav` — tidak ada sistem level-up/XP/upgrade (mapping-nya ditolak; statusnya celah, sama seperti parrysuccess).

### Berkas parkir — mapping ditolak, butuh keputusan user sebelum dipakai

- **Sisa asset-pack burung** di `Audio/SFX/Skill/` (semua tebakan makna kata tanpa anchor project): `sfx_peck.wav`, `sfx_divebomb.wav`, `sfx_clawrend.wav`, `sfx_talonslash.wav`, `sfx_razorwing.wav`, `sfx_ironplume.wav`, `sfx_crowsmolt.wav`, `sfx_evasivefeather.wav`, `sfx_frenzypeck.wav`, `sfx_piercingdive.wav`, `sfx_winggust.wav`, `sfx_flockofblades.wav`, `sfx_thunderdive.wav`, `sfx_flamespit.wav`.
- `Audio/SFX/Skill/sfx_counterstance.wav`, `Audio/SFX/Skill/sfx_battlestance.wav` — tidak ada mekanik stance/counter (grep = 0).
- `Audio/SFX/Skill/FeelBarbariansAttack4.wav` — tidak ada tema barbarian di project (3 hero penyihir, 7 musuh serangga/ular).
- `Audio/SFX/Element/Movement/STEP_WHOOSH_Wide_Fast_stereo.wav` — tidak ada hook footstep/dash/move-sound di kode (grep seluruh Assets/Scripts = nihil).
- `Audio/SFX/Element/Water/HIGH_EM_WATER_IMPACT_01.ogg` — premis "menambal HIGH Ice" salah fakta (Ice HIGH ada); tidak menambal apa pun.
- `Audio/Library/Feel/NiceVibrations/HapticSamples/Nature/Thunder1.wav` — klaim "satu-satunya bunyi petir" terbantah (`Thunder2.wav` ada di folder yang sama); nilai ulang dengan telinga, bandingkan keduanya, sebelum dipetakan ke Element.Lightning/Chain/Barrage.