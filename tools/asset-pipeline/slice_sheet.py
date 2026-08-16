#!/usr/bin/env python
"""Pemotong sprite sheet hasil AI.

Grid buatan AI tidak pernah lurus, jadi alat ini TIDAK memotong pakai grid:
ia mencari pulau piksel tiap icon lalu memotong pas di badannya. Bagian icon
yang terpisah (tiga tetes darah, garis putus-putus) direkatkan lewat pelebaran
mask sebelum pelabelan, supaya tidak pecah jadi beberapa file.

Dua mode, karena dua jenis sheet yang kita terima dari GPT:

  ink    Stempel putih di atas hitam (icon peta). Terang = tinta -> alpha dari
         luminance, seluruh hitam jadi transparan TERMASUK di dalam badan icon
         (bolong tembus perkamen, persis gaya stempel), warna dikunci ivory.

  color  Ilustrasi berwarna di atas latar hitam (icon status gaya Hades).
         Luminance-key akan MEMAKAN outline gelap di dalam icon, jadi yang
         dibuang hanya hitam yang tersambung ke TEPI gambar (flood fill).

Pakai:
  python slice_sheet.py sheet.png keluar/ --mode ink --count 10
  python slice_sheet.py sheet.png keluar/ --mode color --count 45 --names daftar.txt

--names: file teks satu nama per baris, urutan baca kiri->kanan atas->bawah.
"""

import argparse
import os
import sys
from collections import deque

from PIL import Image

# Mask kasar untuk pelabelan. 384 sisi terpanjang = ~150 ribu sel BFS pure
# python — sekejap, dan cukup halus untuk memisahkan icon bergrid 7x7.
COARSE = 384

# Pelebaran mask (dalam sel kasar) yang merekatkan bagian icon yang terpisah.
# 2 sel kasar di sheet 2048 = ~11 px — cukup buat tetes darah, terlalu kecil
# buat menyeberang selokan antar sel grid.
GLUE = 2

IVORY = (242, 237, 226)


def luminance_alpha(img):
    """Mode ink: alpha = terangnya piksel, warna dikunci ivory.

    Hitam yang DICAT di badan icon ikut jadi bolong — luminance nol ya alpha
    nol, tidak peduli dicat atau memang kosong. Sheet yang sudah punya alpha
    (GPT kadang kasih background transparan tapi interiornya dicat hitam)
    dikalikan dengan alpha lamanya, supaya RGB sampah di bawah piksel
    transparan tidak menjelma jadi hantu.
    """
    rgba = img.convert("RGBA")
    grey = rgba.convert("L").load()
    alpha = rgba.split()[-1].load()
    w, h = rgba.size

    out = Image.new("RGBA", rgba.size, IVORY + (0,))
    mask = Image.new("L", rgba.size)
    m = mask.load()

    for y in range(h):
        for x in range(w):
            m[x, y] = grey[x, y] * alpha[x, y] // 255

    out.putalpha(mask)
    return out


def edge_key_alpha(img, threshold=28):
    """Mode color: hitam yang TERSAMBUNG ke tepi = latar -> transparan.

    Hitam di dalam badan icon (outline, mulut, bayangan) tidak tersambung ke
    tepi, jadi selamat. Threshold longgar karena kompresi bikin hitamnya
    tidak persis nol.
    """
    rgba = img.convert("RGBA")
    w, h = rgba.size
    px = rgba.load()
    grey = rgba.convert("L").load()

    background = bytearray(w * h)
    queue = deque()

    for x in range(w):
        for y in (0, h - 1):
            queue.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            queue.append((x, y))

    while queue:
        x, y = queue.popleft()
        if x < 0 or x >= w or y < 0 or y >= h:
            continue
        i = y * w + x
        if background[i] or grey[x, y] > threshold:
            continue
        background[i] = 1
        queue.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))

    for y in range(h):
        base = y * w
        for x in range(w):
            if background[base + x]:
                r, g, b, _ = px[x, y]
                px[x, y] = (r, g, b, 0)

    return rgba


def find_islands(alpha_img):
    """Kotak-kotak icon dari mask kasar yang dilebarkan lalu dilabel."""
    w, h = alpha_img.size
    scale = max(w, h) / COARSE
    cw, ch = max(1, int(w / scale)), max(1, int(h / scale))

    mask_img = alpha_img.split()[-1].resize((cw, ch), Image.BILINEAR)
    m = mask_img.load()

    solid = [[1 if m[x, y] > 24 else 0 for x in range(cw)] for y in range(ch)]

    # Pelebaran GLUE sel: perekat bagian icon yang terpisah.
    glued = [row[:] for row in solid]
    for _ in range(GLUE):
        nxt = [row[:] for row in glued]
        for y in range(ch):
            for x in range(cw):
                if glued[y][x]:
                    continue
                if ((x > 0 and glued[y][x - 1]) or (x < cw - 1 and glued[y][x + 1]) or
                        (y > 0 and glued[y - 1][x]) or (y < ch - 1 and glued[y + 1][x])):
                    nxt[y][x] = 1
        glued = nxt

    seen = [[False] * cw for _ in range(ch)]
    boxes = []

    for sy in range(ch):
        for sx in range(cw):
            if not glued[sy][sx] or seen[sy][sx]:
                continue

            queue = deque([(sx, sy)])
            seen[sy][sx] = True
            x0, y0, x1, y1 = sx, sy, sx, sy
            area = 0

            while queue:
                x, y = queue.popleft()
                if solid[y][x]:
                    area += 1
                x0, x1 = min(x0, x), max(x1, x)
                y0, y1 = min(y0, y), max(y1, y)
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= nx < cw and 0 <= ny < ch and glued[ny][nx] and not seen[ny][nx]:
                        seen[ny][nx] = True
                        queue.append((nx, ny))

            # Remah kompresi (beberapa piksel nyasar) bukan icon.
            if area < 6:
                continue

            boxes.append((int(x0 * scale), int(y0 * scale),
                          int((x1 + 1) * scale), int((y1 + 1) * scale)))

    return boxes


def row_major(boxes):
    """Urut baris-per-baris. Baris = kotak yang pusat-y-nya berdekatan."""
    if not boxes:
        return boxes

    heights = sorted(b[3] - b[1] for b in boxes)
    tolerance = heights[len(heights) // 2] * 0.6

    remaining = sorted(boxes, key=lambda b: (b[1] + b[3]) / 2)
    rows = []

    for box in remaining:
        cy = (box[1] + box[3]) / 2
        for row in rows:
            if abs(row[0] - cy) <= tolerance:
                row[1].append(box)
                row[0] = sum((b[1] + b[3]) / 2 for b in row[1]) / len(row[1])
                break
        else:
            rows.append([cy, [box]])

    ordered = []
    for _, row in sorted(rows, key=lambda r: r[0]):
        ordered.extend(sorted(row, key=lambda b: b[0]))
    return ordered


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("sheet")
    ap.add_argument("outdir")
    ap.add_argument("--mode", choices=("ink", "color"), default="ink")
    ap.add_argument("--count", type=int, default=0,
                    help="jumlah icon yang diharapkan; beda = peringatan keras")
    ap.add_argument("--names", default="",
                    help="file teks: satu nama per baris, urutan kiri->kanan atas->bawah")
    ap.add_argument("--pad", type=int, default=4, help="margin transparan tiap potongan")
    args = ap.parse_args()

    img = Image.open(args.sheet)
    rgba = luminance_alpha(img) if args.mode == "ink" else edge_key_alpha(img)

    boxes = row_major(find_islands(rgba))

    names = []
    if args.names:
        with open(args.names, encoding="utf-8") as f:
            names = [line.strip() for line in f if line.strip()]

    os.makedirs(args.outdir, exist_ok=True)
    written = []

    for i, (x0, y0, x1, y1) in enumerate(boxes):
        crop = rgba.crop((max(0, x0 - 2), max(0, y0 - 2),
                          min(rgba.width, x1 + 2), min(rgba.height, y1 + 2)))

        # Rapikan pas ke badan: bbox alpha asli, lalu margin seragam.
        tight = crop.getbbox()
        if tight:
            crop = crop.crop(tight)

        padded = Image.new("RGBA",
                           (crop.width + args.pad * 2, crop.height + args.pad * 2),
                           (0, 0, 0, 0))
        padded.paste(crop, (args.pad, args.pad))

        name = names[i] if i < len(names) else f"icon_{i + 1:02d}"
        path = os.path.join(args.outdir, name + ".png")
        padded.save(path)
        written.append((name, padded.size))

    print(f"ketemu {len(boxes)} icon -> {args.outdir}")
    for name, size in written:
        print(f"  {name}.png  {size[0]}x{size[1]}")

    if args.count and len(boxes) != args.count:
        print(f"\nPERINGATAN: diharapkan {args.count}, ketemu {len(boxes)}.", file=sys.stderr)
        print("Kurang = dua icon nempel (naikkan COARSE) atau icon pudar;", file=sys.stderr)
        print("lebih = satu icon pecah (naikkan GLUE) atau ada remah di sheet.", file=sys.stderr)
        sys.exit(2)


if __name__ == "__main__":
    main()
