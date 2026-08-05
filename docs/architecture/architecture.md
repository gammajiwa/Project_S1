# Arsitektur — Grimoire Haven

Dokumen ini adalah peta kerja kode. Semua keputusan di sini mengikat; kalau mau
menyimpang, catat alasannya sebagai ADR di `docs/architecture/`.

## Tiga aturan yang tidak boleh dilanggar

1. **Data di ScriptableObject, bukan di kode.** Tidak ada angka gameplay yang
   di-hardcode. Menambah skill = bikin asset, bukan compile ulang.
2. **Tidak ada singleton.** Tidak ada `Instance`, tidak ada `FindObjectOfType`.
   Semua dependency diberikan dari luar lewat `Init(...)`. Satu-satunya tempat
   yang tahu cara merakit dunia adalah `GameRoot`.
3. **Model tidak tahu Unity.** Isi `Model/` adalah C# biasa — bisa di-unit-test
   tanpa membuka Editor, dan bisa di-log tanpa harus jalanin game.

Konsekuensi praktisnya: kalau satu sistem rusak, kamu bisa bikin scene kosong,
rakit sistem itu sendirian, dan lihat kelakuannya tanpa membawa seluruh game.

## Struktur folder

```text
Assets/Scripts/
├── Data/            ScriptableObject — data murni, TIDAK punya logika
├── Model/           C# biasa — state satu run, tidak kenal Unity
├── Systems/         Logika runtime, menerima dependency dari luar
├── View/            UI dan presentasi, hanya membaca
└── Composition/     GameRoot — satu-satunya yang merakit semuanya
```

---

## Data — ScriptableObject

Semua turunan `ContentDefinition` (punya `Id` unik + `DisplayName` + `Icon`).
`Id` dipakai untuk resep, codex, dan save — jangan pernah diubah setelah rilis.

| Asset | Isi |
|---|---|
| `RuneDefinition` | bentuk (petak), aura yang diberikan ke skill di atasnya |
| `SkillDefinition` | bentuk, cast kind, damage, cooldown, mana, range, radius, status yang ditempel, bintang |
| `SigilDefinition` | bentuk, daftar `StatModifier`, bintang |
| `StatusDefinition` | durasi, max stack, interval tick, damage per tick, pengali gerak, pengali damage diterima |
| `ReactionDefinition` | statusA + statusB → damage, radius, konsumsi bahan |
| `RecipeDefinition` | bahan (2–3 `ContentDefinition`), hasil |
| `EnemyDefinition` | HP, speed, damage sentuh, warna/mesh, bobot spawn |
| `WaveDefinition` | jumlah musuh, komposisi, interval spawn, event (toko/elite) |
| `GameBalance` | **satu** asset berisi stat dasar pemain, kurva harga toko, kenaikan reroll, drop rate |
| `ContentDatabase` | daftar semua definition. Satu-satunya pintu masuk data |

`ContentDatabase` juga yang memvalidasi: `OnValidate()` menolak Id duplikat,
referensi kosong, dan resep yang hasilnya tidak terdaftar. Error muncul di
Inspector, bukan saat main.

### Kenapa satu `ContentDatabase`

Supaya tidak ada satu pun sistem yang mencari asset sendiri lewat `Resources.Load`
atau path string. `GameRoot` menyuntik database ini ke sistem yang butuh, dan itu
membuat semuanya bisa diganti dengan database palsu saat testing.

---

## Model — state satu run

Semua C# biasa, tanpa `MonoBehaviour`.

| Kelas | Tanggung jawab |
|---|---|
| `Grimoire` | grid dua lapis (rune + skill/segel), penempatan, pencarian resep |
| `Backpack` | grid penyimpanan skill |
| `LootPile` | item yang tercecer + posisinya |
| `PlayerState` | HP, mana, koin saat ini |
| `StatBlock` | kumpulan stat sebagai array, bukan dictionary |
| `CompiledLoadout` | hasil kompilasi grimoire: daftar spell + angka final |
| `DiscoveryLog` | Id apa saja yang pernah didapat pemain (codex) |

**Aturan kompilasi:** `Grimoire` hanya berubah saat fase susun. Setiap perubahan
memanggil `Compile()` **satu kali**, menghasilkan `CompiledLoadout` berisi angka
datar. Saat wave berjalan, tidak ada satu pun sistem yang membaca grid — mereka
hanya membaca loadout. Ini yang menjaga combat tetap murah.

---

## Sistem stat

Satu enum, satu array. Tidak ada dictionary di jalur panas.

```csharp
public enum StatType
{
    MaxHp, HpRegen, MaxMana, ManaRegen,
    DamagePct, CooldownPct, AreaPct, RangePct,
    CritChance, CritDamage,
    FireDamagePct, IceDamagePct, LightningDamagePct,
    Count
}

public struct StatModifier { public StatType Type; public float Value; }
```

`StatBlock` = `float[(int)StatType.Count]`, punya `Add`, `Get`, `Reset`.

**Dua lingkup, jangan dicampur:**

- **Global** — dari segel yang terpasang. Mempengaruhi pemain (HP, mana, regen)
  dan semua skill (damage per elemen).
- **Lokal** — dari rune di bawah tiap petak skill. Hanya mempengaruhi skill itu.

**Urutan operasi damage (dikunci, jangan diubah diam-diam):**

```text
final = base
      × (1 + Σ additive%)        // aura rune lokal + segel elemen global
      × Π multiplicative         // sumber langka saja
      × crit                     // (1 + CritDamage) bila kena crit
      × (1 + vulnerability)      // dari status di musuh, mis. SHOCK
```

---

## Sistem buff / debuff

Dibedakan tegas, karena biayanya beda jauh:

- **Modifier** (dari rune & segel) — *bukan* sistem runtime. Diselesaikan saat
  kompilasi, jadi angka datar. Tidak pernah di-tick.
- **Status** (di musuh atau pemain) — sistem runtime sungguhan: punya durasi,
  stack, dan tick.

**Penyimpanan status: 4 slot tetap per musuh, struct, nol alokasi.**

```csharp
public struct StatusSlot { public byte DefIndex; public float Remaining; public byte Stacks; }
// per musuh: StatusSlot[4]
```

Slot penuh → status terlemah (sisa durasi terpendek) ditimpa. Tidak pakai `List`,
tidak pakai `Dictionary`, tidak ada GC spike saat 200 musuh kena AoE bersamaan.

**Reaksi** dibaca dari `ReactionDefinition`. Aturan wajib: reaksi
**mengkonsumsi kedua status**. Kalau tidak, damage meledak tak terkendali dan
balancing jadi mustahil.

---

## Sistem musuh (target 200)

200 musuh itu angka yang nyaman — cukup untuk terasa bullet haven, tapi masih
aman pakai GameObject ter-pool. Jadi:

- **Pooled GameObject**, bukan array murni. Alasannya: 3D, dan nanti kamu mau
  mesh + animasi sederhana. Debug-nya juga jauh lebih enak — bisa diklik di
  Hierarchy.
- **Satu manager, satu loop.** Tidak ada `Update()` di tiap musuh.
- **Tanpa Rigidbody, tanpa Collider.** Gerak manual, tabrakan pakai jarak kuadrat.
- **Satu material + GPU instancing**, shadow musuh dimatikan.
- **Spatial hash** ukuran sel 3 unit untuk query "musuh terdekat" dan AoE.
  Tanpa ini, tiap skill scan 200 musuh tiap frame; dengan ini hanya sel sekitar.

Budget: gerak + status + query < 2 ms pada 200 musuh. Kalau nanti tembus, baru
pindah ke Jobs + Burst — **bukan sekarang**, karena itu menukar kemudahan debug
dengan performa yang belum dibutuhkan.

VFX: quad ter-pool + tween scale. Tidak ada particle system mahal, tidak ada
post-processing berat selain color grading + bloom tipis.

---

## Sistem lain

| Sistem | Tugas | Dependency yang disuntik |
|---|---|---|
| `WaveSystem` | mulai/akhiri wave, spawn sesuai `WaveDefinition` | database, enemy system |
| `CastSystem` | tick cooldown, cek mana, tembak | loadout, enemy system, player state |
| `StatusSystem` | tick status, jalankan reaksi | database, enemy system |
| `LootSystem` | roll drop, taruh ke `LootPile`, tandai discovery | database, balance, discovery log |
| `ShopSystem` | stok event, harga, kurva reroll | database, balance, player state |
| `EvolutionSystem` | cari grup resep, gabungkan di akhir wave | database, grimoire |
| `DamageMeter` | akumulasi damage per skill sepanjang run | — (menerima event) |

**Komunikasi antar sistem pakai event C# biasa**, bukan referensi silang.
Contoh: `CastSystem` menembakkan `DamageDealt(skillId, amount)`; `DamageMeter`
dan UI angka mendengarkan. `CastSystem` tidak tahu keduanya ada.

---

## Codex (skill/item yang belum ketemu)

- `DiscoveryLog` menyimpan `HashSet<string>` Id yang pernah **dimiliki** pemain.
- Ditandai saat: drop diambil, dibeli di toko, atau lahir dari evolusi.
- `CodexView` menampilkan **semua** definition dari `ContentDatabase`; yang
  belum ada di log ditampilkan sebagai `???` dengan siluet bentuknya saja.
- Resep ikut aturan yang sama: resep baru terbaca kalau **hasilnya** sudah pernah
  ditemukan. Sebelum itu tampil `??? + ??? = ???`.
- Disimpan lintas run (JSON di `Application.persistentDataPath`). Ini satu-satunya
  data yang persisten — sisanya per run.

---

## Damage meter

Karena ini game build, pemain harus bisa menjawab "skill mana yang sebenarnya
kerja". Meter mengakumulasi per `skillId` sepanjang run, ditampilkan sebagai bar
terurut di layar akhir wave: nama, total damage, persentase, DPS efektif.

Ini juga alat balancing kamu sendiri — kalau satu skill selalu di atas 40%,
angkanya salah.

---

## GameRoot — satu-satunya yang merakit

```csharp
public class GameRoot : MonoBehaviour
{
    [SerializeField] ContentDatabase _database;
    [SerializeField] GameBalance _balance;

    void Awake()
    {
        var player   = new PlayerState(_balance);
        var grimoire = new Grimoire(_balance.GridWidth, _balance.GridHeight);
        var bag      = new Backpack(_balance.BagWidth, _balance.BagHeight);
        var loot     = new LootPile();
        var codex    = DiscoveryLog.Load();

        var enemies  = new EnemySystem(_database, _balance);
        var status   = new StatusSystem(_database, enemies);
        var cast     = new CastSystem(grimoire, enemies, player);
        var waves    = new WaveSystem(_database, enemies);
        var lootSys  = new LootSystem(_database, _balance, loot, codex);
        var shop     = new ShopSystem(_database, _balance, player, codex);
        var evo      = new EvolutionSystem(_database, grimoire);
        var meter    = new DamageMeter();

        cast.DamageDealt += meter.Record;
        waves.WaveCleared += () => { evo.Resolve(); lootSys.GrantWaveReward(); };
        // ... view di-wire di sini juga
    }
}
```

Tidak ada `static`. Kalau butuh dua instance untuk testing, tinggal bikin dua.

---

## Rencana migrasi bertahap

Game harus tetap bisa dimainkan di akhir **setiap** tahap. Jangan gabung dua tahap.

**Tahap 1 — data ke ScriptableObject**
Bikin semua `*Definition` + `ContentDatabase` + `GameBalance`. Isi asset-nya
dari isi `RuneLibrary.cs` sekarang. `RuneLibrary` dihapus di akhir tahap ini.
Untung langsung: bisa bikin 30 skill dari Inspector.

**Tahap 2 — pecah `GrimoireUI` (~1800 baris)**
Keluarkan model (`Grimoire`, `Backpack`, `LootPile`) dan sistem (`Shop`, `Loot`,
`Evolution`) dari kelas UI. Sisakan `GrimoireView` yang hanya menggambar.
UI berhenti redraw tiap frame — hanya saat ada event perubahan.

**Tahap 3 — `GameRoot` + event**
Buang semua referensi silang, ganti dengan injeksi + event. Setelah ini tiap
sistem bisa dites sendirian.

**Tahap 4 — performa musuh**
Spatial hash, instancing, matikan bayangan musuh, pool VFX. Ukur dulu pakai
Profiler, jangan tebak.

**Tahap 5 — codex, damage meter, save**

Tahap 1–3 aman dikerjakan sekarang. Tahap 4 baru berarti setelah jumlah musuh
benar-benar dinaikkan ke 200.

---

## Cara debug yang disiapkan sejak awal

- `ContentDatabase.OnValidate()` — Id duplikat & referensi kosong ketahuan di Editor.
- Overlay debug (F1): jumlah musuh, ms per sistem, alokasi per frame.
- Semua sistem bisa dirakit di scene kosong karena tidak ada singleton.
- Model tanpa dependency Unity → bisa di-unit-test (`tests/unit/`).
