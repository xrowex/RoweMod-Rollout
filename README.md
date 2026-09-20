# RoweMod × Rollout

Open clothing-modding kit for **[Rollout Inline](https://store.steampowered.com/app/4464990/)** (UE 5.4.4). Add items to the live customization menus instead of replacing meshes.

This repository does **not** ship ripped game assets. You need the Steam game, Unreal 5.4.4 (for new meshes), and the tools listed below.

## Two tracks

| Track | When | What you author |
|---|---|---|
| **A — recolor / new row** | The silhouette already exists | A JSON spec that clones a stock row and changes name, tint, price, or textures |
| **B — new mesh** | You need a new shape | A garment on `main-rig` + JSON that points `UpperMale` / `LowerMale` at your cooked mesh |

Every customization slot has a Track A example under `items/`. Tops also has a Track B shirt (`tshirt-baggy-mod`).

## Quick start (Track A)

Requires .NET 8 and the game installed.

```powershell
# 1. Extract live DataTables (once)
.\tools\extract_tables.ps1

# 2. Copy an example, change row / localizedName / colour
copy items\lower\cargos-navy-mod.json items\lower\my-pants.json

# 3. Patch tables + pack an IoStore overlay into the game
.\tools\pack_mod.ps1
```

Launch Rollout Inline. Mod rows are listed **first** in each menu.

## Examples

| Slot | Table | Example | Kind |
|---|---|---|---|
| Tops | `DT-upper` | `items/upper/hoodie-navy-mod.json` | Track A tint |
| Tops | `DT-upper` | `items/upper/tshirt-baggy-mod.json` | Track B new mesh |
| Bottoms | `DT-lower` | `items/lower/cargos-navy-mod.json` | Track A tint |
| Hats | `DT-hats` | `items/hats/helmet-navy-mod.json` | Track A tint |
| Glasses | `DT-glasses` | `items/glasses/sunglasses-rowe-mod.json` | Track A row |
| Hair | `DT-hair` | `items/hair/hair-rowe-mod.json` | Track A row |
| Beard | `DT-beard` | `items/beard/beard-rowe-mod.json` | Track A row |
| Body | `DT-bodytypes` | `items/body/male-rowe-mod.json` | Track A row |
| Skin | `DT-skin` | `items/skin/skin-rowe-mod.json` | Track A contrast |
| Eyes | `DT-eyes` | `items/eyes/eyes-rowe-mod.json` | Track A row |
| Boots | `DT-boot` | `items/boots/boot-navy-mod.json` | Track A part tints |
| Frames | `DT-frames` | `items/frames/frames-navy-mod.json` | Track A tint |
| Wheels | `DT-wheels` | `items/wheels/wheels-rowe-mod.json` | Track A row |

JSON fields: `table`, `cloneRow`, `row` (must end in `-mod` to sort first), `localizedName`, `price`, `colour`, `colours` (boot Shell/Sole/Laces/…), `refs` / `upperMale` / `albedo` / `previewImage`.

## Track B — new mesh

Shared skeleton: `/Game/MainFolder/Character/body/main-rig/main-rig` (87 UE5-mannequin bones). Rig kit: `art/rig/main-rig.blend`.

1. Model on `main-rig`. Save a copy so the kit stays armature-only.
2. Export with `art/export_shirt.py` (stitches weights onto live hoodie inverse-binds).
3. Import in `ue/RollerSkate/RollerSkate.uproject` at a **new** `/Game/MainFolder/...` path. Do not pack `main-rig` or `MI-Upper`.
4. Point the item JSON mesh field at that path. Cook: `ue/cook_mod.ps1`. Then `tools/pack_mod.ps1`.

Details: [docs/modding.md](docs/modding.md). Catalog: [docs/clothing-catalog.md](docs/clothing-catalog.md).

## What gets packed

`RollerSkate-Windows_P.utoc/.ucas` plus `ClothingMod_P.pak` in `RollerSkate/Content/Paks` and `Paks/~mods`. Overlay packages only load if a patched DataTable already loaded by the game hard-references them.

## Legal

MIT for the tools and original RoweMod art. **Rollout Inline** content belongs to its owners. Do not commit extracted meshes, textures, or `.uasset` dumps. This is an unofficial fan project.

## Requirements

- Windows, Steam copy of Rollout Inline
- .NET 8 SDK (DtPatcher)
- [retoc](https://github.com/trumank/retoc) in `tools/retoc/`
- Unreal Engine **5.4.4** only if you cook a new mesh
- Blender 5.1 for the shirt/rig examples
