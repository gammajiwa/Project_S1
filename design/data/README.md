# design/data — Spreadsheet Balance (EXPORT)

> **PENTING: file-file CSV di folder ini adalah EXPORT, bukan sumber data.**
> Mengedit CSV **tidak mengubah apa pun di game**. Sumber kebenaran adalah file
> `.asset` di `Assets/GameData/` — edit di Unity Inspector, lalu regenerate CSV-nya.
> Setiap regenerate akan menimpa seluruh isi CSV di sini.

## Isi

| File | Isi | Sumber |
|---|---|---|
| `pieces.csv` | Semua piece (rune / skill / segel): elemen, bintang, bentuk, damage, cooldown, mana, status yang ditempel, aura, stats, trigger, knob zone/detonate/utility (Bounces, MaxDetonations, PushForce, dst), plus kolom turunan DPS & ManaPerDetik | `Assets/GameData/Pieces/*.asset` |
| `recipes.csv` | Resep crafting: hasil + bintangnya, sampai 3 bahan, jumlah bahan, total petak bahan | `Assets/GameData/Recipes/*.asset` |
| `statuses.csv` | Ailment (burn, chill, shock, dst): max poin, tick, damage per tick per poin, multiplier gerak/damage, pull | `Assets/GameData/Statuses/*.asset` |
| `buffs.csv` | Buff & kutukan: durasi, daftar stat modifier | `Assets/GameData/Buffs/*.asset` |
| `reactions.csv` | Reaksi antar-status: pasangan status, konsumsi poin, burst (flat / per poin / % max HP), status & buff hasil | `Assets/GameData/Reactions/*.asset` |
| `enemies.csv` | Arketipe musuh: wave muncul, bobot spawn, multiplier HP/speed, pola serang, kutukan | `Assets/GameData/Enemies/*.asset` |
| `heroes.csv` | Loadout hero awal: piece terpasang (posisi + rotasi) dan piece lepas | `Assets/GameData/Heroes/*.asset` |

Sel kosong pada kolom referensi (Status, TriggerStatus, GrantBuff, dst) berarti
referensinya memang kosong (`{fileID: 0}`) di aset — bukan data hilang.

Catatan kolom `Layer` di `pieces.csv`: piece dengan `Kind = Passive` ditulis
sebagai `Segel` supaya gampang difilter — di aset, nilai serialized-nya tetap
`Skill` (enum `Layer` hanya punya Rune/Skill).

## Cara regenerate

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "tools\export-content-csv.ps1"
```

- **Aman dijalankan kapan saja** — script hanya MEMBACA `Assets/GameData` dan
  menulis ulang `design/data/*.csv`. Tidak pernah menyentuh file `.asset`.
- Jalankan ulang setiap kali selesai mengubah aset di Unity, supaya spreadsheet
  tidak basi.
- Output: UTF-8 BOM, baris pertama `sep=,` supaya Excel langsung membaca koma
  sebagai pemisah. Kalau membaca lewat script, lewati baris pertama itu.
- Enum di-decode dari urutan di `Assets/Scripts/Data/Enums.cs` dan `Shapes.cs`;
  referensi antar-aset di-resolve lewat guid dari file `.meta`. Kalau enum atau
  field di C# berubah, script perlu disesuaikan dulu.
