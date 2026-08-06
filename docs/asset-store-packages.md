# Asset Store packages (not in git)

Three purchased packages live under `Assets/Plugin/` and are **deliberately excluded from
version control**. A fresh clone will not render the arena correctly until they are imported.

## Why they are not committed

Together they are **3.7 GB across 2 862 files**. The remote is GitHub, and `.gitattributes`
routes `.tga`, `.hdr`, `.png` and `.fbx` through LFS — so roughly 3.4 GB of that would land in
LFS storage against a **1 GB free quota**. The push would be rejected outright, and paying to
raise the quota would still leave every clone a multi-gigabyte download, forever, for content
nobody here authored and that Unity re-encodes on import anyway.

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

ToonScapes installs two folders — `Shared Assets` (shaders, volume profiles) and
`Spring Isles` (the biome itself). Both are required; the shaders live in `Shared Assets`.

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

## Related

- `.gitignore` — the three ignore entries, with the same reasoning in short form
- `Packages/manifest.json` — packages that *are* version-controlled, via UPM
