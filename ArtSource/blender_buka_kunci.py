# Buka semua yang bikin objek tidak bisa diedit.
#
# Cara pakai: Blender -> tab Scripting -> Open -> pilih file ini -> Run Script (Alt+P).
# Aman dijalankan kapan saja; dia tidak mengubah geometri, cuma membuka kunci dan
# melaporkan keadaan.

import bpy


def buka_kunci():
    laporan = []

    # 1) Keluar dari mode apa pun. Ini penyebab nomor satu "kok objek lain tidak bisa diklik":
    #    di Edit/Pose/Weight-Paint, klik di viewport tidak memilih OBJEK.
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        lama = bpy.context.object.mode
        bpy.ops.object.mode_set(mode='OBJECT')
        laporan.append(f"mode {lama} -> OBJECT")

    # 2) Collection: exclude (checkbox) dan hide_viewport (ikon monitor).
    #    Yang exclude tidak akan tersentuh Alt+H sama sekali — objeknya tidak ada di view layer.
    def jalan(lc):
        yield lc
        for anak in lc.children:
            yield from jalan(anak)

    for lc in jalan(bpy.context.view_layer.layer_collection):
        if lc.exclude:
            lc.exclude = False
            laporan.append(f"collection '{lc.collection.name}' exclude -> off")
        if lc.hide_viewport:
            lc.hide_viewport = False
            laporan.append(f"collection '{lc.collection.name}' hide viewport -> off")
        if lc.holdout:
            lc.holdout = False
            laporan.append(f"collection '{lc.collection.name}' holdout -> off")

    for c in bpy.data.collections:
        if c.hide_viewport:
            c.hide_viewport = False
            laporan.append(f"collection '{c.name}' monitor -> off")
        if c.hide_select:
            c.hide_select = False
            laporan.append(f"collection '{c.name}' selectable -> on")

    # 3) Objek: tiga flag hide itu TERPISAH, dan Alt+H hanya membuka yang pertama.
    for o in bpy.context.scene.objects:
        if o.hide_get():
            o.hide_set(False)
            laporan.append(f"'{o.name}' mata -> tampil")
        if o.hide_viewport:
            o.hide_viewport = False
            laporan.append(f"'{o.name}' monitor -> tampil")
        if o.hide_select:
            o.hide_select = False
            laporan.append(f"'{o.name}' selectable -> on")

        # 4) Gembok transform: objek bisa dipilih tapi tidak bisa digeser/diputar/diskala.
        for i in range(3):
            if o.lock_location[i] or o.lock_rotation[i] or o.lock_scale[i]:
                o.lock_location[i] = o.lock_rotation[i] = o.lock_scale[i] = False
                laporan.append(f"'{o.name}' gembok transform -> dibuka")
                break

    print("=" * 60)
    if laporan:
        print(f"DIBUKA ({len(laporan)} hal):")
        for r in laporan:
            print("   -", r)
    else:
        print("Tidak ada yang terkunci. Kalau masih tidak bisa diedit, kemungkinan")
        print("objeknya MENUMPUK dengan objek lain — pilih lewat Outliner, bukan klik viewport.")

    # 5) Laporan objek yang menumpuk: penyebab "kok yang kepilih bukan yang gw klik".
    print("\nOBJEK DI SCENE (urut dari yang paling berat):")
    mesh = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.data]
    for o in sorted(mesh, key=lambda x: -len(x.data.polygons)):
        d = o.dimensions
        print(f"   {o.name:26s} {len(o.data.polygons):7d} tris  "
              f"pos=({o.location.x:+.2f},{o.location.y:+.2f},{o.location.z:+.2f})  "
              f"ukuran=({d.x:.2f},{d.y:.2f},{d.z:.2f})")

    # Dua objek dengan posisi & ukuran nyaris sama = hampir pasti duplikat yang menumpuk.
    tumpuk = []
    for i, a in enumerate(mesh):
        for b in mesh[i + 1:]:
            if (a.location - b.location).length < 0.02 and \
               abs(a.dimensions.length - b.dimensions.length) < 0.05:
                tumpuk.append((a.name, b.name))
    if tumpuk:
        print("\nPERINGATAN — kemungkinan MENUMPUK di posisi yang sama:")
        for a, b in tumpuk:
            print(f"   '{a}'  <->  '{b}'")
        print("   Kalau geometri terlihat dobel/tebal, salah satunya sembunyikan atau hapus.")
    print("=" * 60)


buka_kunci()
