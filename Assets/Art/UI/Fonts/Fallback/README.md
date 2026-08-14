# Font cadangan — taruh berkasnya di sini, tidak ada yang perlu ditekan

Font tampilan permainan ini **tidak punya glyph** untuk sebagian bahasa yang dikirim.
Terukur dari tabel `cmap` masing-masing berkas:

| font | Latin | Cyrillic | CJK | Hangul |
|---|---|---|---|---|
| Cinzel | ya | **tidak** | **tidak** | **tidak** |
| Barlow Semi Condensed | ya | **tidak** | **tidak** | **tidak** |
| EB Garamond | ya | ya | **tidak** | **tidak** |
| Liberation Sans (TMP bawaan) | ya | ya | **tidak** | **tidak** |

Artinya tanpa font cadangan, memilih Rusia / Cina / Jepang / Korea membuat seluruh layar jadi
kotak kosong — bukan terjemahan yang jelek, tapi permainan yang tidak bisa dibaca.

## Yang perlu diunduh

Semuanya **SIL Open Font License 1.1** — bebas dibundel dan dijual bersama game, tanpa royalti,
tanpa kewajiban membuka kode. Ambil dari <https://fonts.google.com> atau
<https://github.com/notofonts>.

| berkas | menutup | wajib untuk |
|---|---|---|
| `NotoSans-Regular.ttf` | Latin, Cyrillic, Greek | `ru` |
| `NotoSansSC-Regular.otf` | Cina Sederhana | `zh-Hans` |
| `NotoSansJP-Regular.otf` | Jepang (kana + kanji) | `ja` |
| `NotoSansKR-Regular.otf` | Korea (hangul) | `ko` |

Cukup **satu berat** (Regular) per bahasa. Berat lain hanya menambah ukuran build.

> **Jangan pakai font Windows** (`msyh.ttc`, `malgun.ttf`, `msgothic.ttc`). Semuanya punya
> glyphnya, tapi lisensinya **melarang** dibundel ulang. Aman untuk mengetes di Editor, tidak
> aman untuk dikirim ke pemain.

## Cara pakai

Salin berkasnya ke folder ini. Selesai — `FontFallbackPass` mendengarkan impor aset, jadi ia
otomatis:

1. membuat aset TMP untuk tiap font, mode atlas **Dynamic**
2. memasangnya sebagai rantai cadangan di Cinzel, Barlow, dan EB Garamond
3. memasangnya juga di fallback global TMP, sebagai jaring terakhir

Huruf Latin tetap memakai Cinzel yang jadi identitas permainan; hanya aksara yang tidak
dimilikinya yang jatuh ke Noto.

Kalau perlu dijalankan ulang manual: **Tools/Grimoire/Sambungkan Font Cadangan**.

## Kenapa Dynamic, bukan dipanggang

Font CJK memuat puluhan ribu glyph. Memanggang semuanya jadi tekstur atlas statis menghasilkan
aset ratusan megabyte yang 99% isinya tidak pernah muncul di layar. Dynamic merasterkan yang
benar-benar dipakai saja, saat dipakai.

## Yang masih tersisa

HUD di dalam run memakai `UnityEngine.UI.Text` warisan, bukan TextMeshPro, dan rantai fallback
di atas **tidak berlaku untuknya** — Text warisan tidak punya mekanisme itu. Setelah font di
folder ini ada, HUD-nya perlu diarahkan ke font yang memuat aksaranya sendiri. Belum dikerjakan
karena tidak ada gunanya sebelum ada berkas fontnya.
