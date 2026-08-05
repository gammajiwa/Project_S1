# Rencana Konten — Grimoire Haven

Jumlah konten yang dituju, dan mana yang wajib ada lebih dulu. Angka di sini
adalah target; yang mengikat cuma kolom **MVP**.

## Ringkasan

| Kategori | MVP | Target penuh | Catatan |
|---|---|---|---|
| Rune (alas) | 4 | 6 | tiap rune = satu jenis aura |
| Skill ⭐1 | 6 | 8 | satu-satunya yang bisa drop |
| Skill ⭐2 | 5 | 8 | dari resep |
| Skill ⭐3 | 3 | 5 | dari resep, sebagian butuh segel |
| Skill ⭐4 | 1 | 2 | puncak build |
| **Total skill** | **15** | **23** | |
| Segel (item stat) | 6 | 10 | ikut makan tempat di grimoire |
| Status | 3 | 4 | Burn, Chill, Shock (+ Poison) |
| Reaksi | 3 | 4 | pasangan status |
| Resep | 9 | 15 | maksimal 3 bahan |
| Tipe musuh | 3 | 4 + elite + bos | |

Total "benda" yang bisa dikoleksi di codex: **MVP 25**, penuh **39**.

## Kenapa segini

23 skill terdengar sedikit, tapi yang dihitung pemain bukan jumlah item —
melainkan **jumlah kombinasi yang masuk akal**. Dengan 6 rune × penempatan ×
15 resep × 10 segel, ruang build-nya sudah jauh lebih besar dari yang bisa
dihabiskan dalam 4 jam. Menambah skill ke-30 tidak menambah kedalaman kalau
resepnya tidak bertambah.

**Aturan isi:** tiap skill ⭐1 wajib muncul di minimal 2 resep. Kalau ada skill
yang tidak jadi bahan apa pun, dia bakal langsung dijual pemain dan efektifnya
tidak ada.

## Distribusi rarity & drop

| Bintang | Cara dapat | Drop | Harga toko |
|---|---|---|---|
| ⭐1 | drop musuh, hadiah wave, toko | ya | 30 |
| ⭐2 | resep, toko (peluang kecil) | tidak | 130 |
| ⭐3 | resep saja | tidak | tidak dijual |
| ⭐4 | resep saja | tidak | tidak dijual |
| Rune | drop, toko | ya | 20 |
| Segel | drop, toko | ya | 35 |

Drop rate musuh 4%, ditambah 1 drop pasti tiap wave beres. Komposisi drop:
25% rune, 75% skill/segel.

## Rantai resep (bentuk yang dituju)

```text
⭐1 + ⭐1              → ⭐2      (4–5 resep)
⭐2 + ⭐1              → ⭐3      (2 resep)
⭐2 + ⭐1 + SEGEL      → ⭐3      (2 resep)  <- segel jadi bahan, bukan cuma stat
⭐3 + ⭐3              → ⭐4      (1–2 resep)
```

Resep yang menyertakan segel adalah bagian terpenting: dia memaksa pemain
memilih antara memakai segel sebagai penambah stat, atau menyimpannya sebagai
bahan. Minimal 3 resep harus punya bahan segel.

## Codex

Semua benda di atas terdaftar di codex sejak awal, tapi yang belum pernah
dimiliki tampil sebagai `???` — hanya siluet bentuknya yang terlihat, supaya
pemain tahu ada sesuatu yang belum ketemu tanpa tahu isinya.

Resep terbaca kalau **hasilnya** sudah pernah ditemukan.

## Musuh

| Tipe | Peran |
|---|---|
| Perayap | lambat, banyak, isi layar |
| Pelari | cepat, HP tipis, memaksa punya AoE |
| Perisai | lambat, HP tebal, memaksa punya damage tunggal besar |
| Peledak | meledak saat mati, menghukum pemain yang membiarkan menumpuk |
| Elite | muncul tiap 5 wave, drop dijamin |
| Bos | tiap 10 wave |

Maksimal musuh hidup bersamaan: **200**.

## Urutan pembuatan

1. Semua rune (4) + skill ⭐1 (6) + segel (6) — cukup untuk main satu run penuh
2. Status (3) + reaksi (3)
3. Resep ⭐2 (5) — di titik ini loop build sudah lengkap
4. Skill ⭐3 (3) + resep-nya, termasuk yang pakai segel
5. Musuh tipe 2 dan 3
6. Skill ⭐4, elite, bos
