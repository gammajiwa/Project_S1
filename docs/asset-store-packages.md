# Asset Store packages (not in git)

Five purchased packages live under `Assets/Plugin/` and are **deliberately excluded from
version control**. A fresh clone will not render the arena correctly until they are imported.

## Why they are not committed

Together they are **3.9 GB across 3 568 files**. The remote is GitHub, and `.gitattributes`
routes `.tga`, `.hdr`, `.png` and `.fbx` through LFS — so roughly 3.5 GB of that would land in
LFS storage against a **1 GB free quota**, of which 286 MB is already spent. The push would be
rejected outright, and paying to raise the quota would still leave every clone a multi-gigabyte
download, forever, for content nobody here authored and that Unity re-encodes on import anyway.

The bulk is uncompressed source art: forty-odd `.tga` files at 64 MB each, two skybox textures
at 128 MB, and a 250 MB `.hdr`. That is normal for Asset Store art and is exactly the kind of
thing LFS exists for — just not at this ratio against a free tier.

`Packages/com.distantlands.lumen` **is** committed, and that is not an inconsistency: it is
3.3 MB, a thousandth of this.

## What to import

Import each from the Unity Asset Store using the account that purchased it, into the path
listed. The paths matter — asset GUIDs are recorded against them, and importing elsewhere
breaks every reference in the scenes and prefabs.

| Package | Publisher | Import to | Size |
|---|---|---|---|
| Ultimate Nature – Starter | Innerverse Interactive | `Assets/Plugin/InnerverseInteractive/` | 298 MB |
| Environment VFX pack | Lana Studio | `Assets/Plugin/Lana Studio/` | 22 MB |
| ToonScapes: Spring Isles | ToonScapes | `Assets/Plugin/ToonScapes/` | 3 429 MB |
| HAZE — Volumetric Fog & Lighting for URP | — | `Assets/Plugin/HAZE - Volumetric Fog & Lighting for URP/` | 9 MB |
| Handpainted Grass and Ground Textures | — | `Assets/Plugin/Handpainted_Grass_and_Ground_Textures/` | 169 MB |

ToonScapes installs two folders — `Shared Assets` (shaders, volume profiles) and
`Spring Isles` (the biome itself). Both are required; the shaders live in `Shared Assets`.

HAZE is the one package here that ships runtime code, not just art: it has a `Runtime/` folder
whose scripts the renderer feature references. Import it before opening `PC_Renderer.asset`, or
the volumetric fog feature deserialises to a missing script.

## What is safe to delete after importing

`Spring Isles/Demo/` is 224 MB of sample scenes the game never loads. Deleting it does not
affect the build. The same is true of `Ultimate Nature – Starter/Demo/`.

Do **not** delete the `Textures/` or `Terrain Textures/` folders to save space, however large
they look. Unity compresses them into the Library on import; the source files still have to be
present for the compressed form to exist.

## What depends on them

`Assets/Scripts/Data/MeshProp.cs` stores meshes and materials out of these packages rather than
their prefabs, and rescales them to this game's proportions. If the packages are missing, those
references resolve to null and the arena dresses itself with nothing.

## Paket VFX skill (ditambahkan 2026-08-10)

Tiga paket lagi, di bawah `Assets/Art/VFX/Packs/`, di-**clone dari
`D:/GameGamma/project_b/Assets/11_Plugin/`** (bukan diimpor ulang dari Asset Store — GUID-nya
harus persis sama dengan yang di project_b supaya wrapper skill tidak putus):

| Paket | Path tujuan | Besar | Dipakai untuk |
|---|---|---|---|
| GabrielAguiarProductions (Unique Magic Abilities Vol.2) | `Assets/Art/VFX/Packs/GabrielAguiarProductions/` | 256 MB | AOE elemental: MeteorRain / ArrowRain / SingleComet / ImpactAoE / BuffAoE / DebuffAoE |
| JMO Assets — Cartoon FX Remaster | `Assets/Art/VFX/Packs/JMO Assets/Cartoon FX Remaster/` | 138 MB | projectile loop, hit per elemen, explosion, barrier, portal |
| Vefects — Trails VFX URP (subset `VFX/` + `_ Extra/`) | `Assets/Art/VFX/Packs/Vefects/Trails VFX URP/` | 10 MB | trail petir ramping |

Yang **tidak** ikut disalin, dan sebaiknya tetap tidak: `GabrielAguiarProductions/Scenes/`,
`Cartoon FX Remaster/Demo Assets/` (berisi Kino Bloom lama yang berisiko gagal kompilasi),
`Cartoon FX Easy Editor/`, dan `Vefects/{Demo,Sounds,Scripts}/`.

Cara mengembalikan setelah clone bersih:

```
set SRC=D:\GameGamma\project_b\Assets\11_Plugin
robocopy "%SRC%\GabrielAguiarProductions" ^
         "Assets\Art\VFX\Packs\GabrielAguiarProductions" /E /XD Scenes
robocopy "%SRC%\JMO Assets\Cartoon FX Remaster" ^
         "Assets\Art\VFX\Packs\JMO Assets\Cartoon FX Remaster" /E /XD "Demo Assets"
robocopy "%SRC%\Vefects\Trails VFX URP\VFX" ^
         "Assets\Art\VFX\Packs\Vefects\Trails VFX URP\VFX" /E
```

File `.meta` folder induknya ikut disalin manual (robocopy tidak membawa `.meta` milik folder
yang jadi tujuan), lalu `Tools/Grimoire/Assign Skill VFX` untuk meluruskan pointer yang putus.

### Yang tetap dilacak git

`Assets/Art/VFX/Skills/` — 74 folder wrapper, satu prefab per skill. Isinya cuma root kosong
+ referensi ke prefab paket, jadi ringan, dan **di situlah pilihan efek tiap skill hidup**.
Tanpa paketnya, wrapper-nya kosong tapi strukturnya utuh; begitu paket dikembalikan,
referensinya nyambung lagi karena GUID-nya sama.

## Related

- `.gitignore` — the three ignore entries, with the same reasoning in short form
- `Packages/manifest.json` — packages that *are* version-controlled, via UPM
