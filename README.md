# RoweMod × Rollout

Open clothing-modding kit for **[Rollout Inline](https://store.steampowered.com/app/4464990/)** (UE 5.4.4). Add items to the live customization menus instead of replacing meshes.

This repository does **not** ship ripped game assets, Unreal, Blender, or retoc binaries. You need the Steam game; Track B also needs Blender and Unreal.

## First run

The app is a real **Windows exe** (`dist\RoweMod.exe`, .NET 8 WinForms). `RoweMod.cmd` only starts that exe (or `dotnet run` if you have not published yet). The `.ps1` files are internals the exe calls. You do not run those by hand.

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download).
2. Clone [xrowex/RoweMod-Rollout](https://github.com/xrowex/RoweMod-Rollout).
3. Double-click `RoweMod.cmd`, or publish once:

   ```powershell
   .\tools\publish_rowemod.ps1
   ```

   Then double-click `dist\RoweMod.exe`.
4. Click **Setup** once. It finds the Steam game, downloads retoc, and extracts DataTables. After that the button greys out and a green **Setup complete** chip stays on.
5. **Pack and Play** once to write only live items (painted jeans + baggy tee). Dummy navy/rowe slot clones stay on disk as templates.
6. **Clothing** → pull from your Steam copy to paint, add one template tint, or start a new mesh.

| Button | What it does | Needs |
|---|---|---|
| **Setup** | retoc + extract tables | .NET 8 + Steam game |
| **Clothing** | Workshop: paint, new mesh, or kit templates | Steam game; Blender 5.1 to open/export |
| **Cook** | Unreal import + Windows cook (no overlay write) | UE **5.4.4** |
| **Pack and Play** | patch live items, write IoStore overlay, launch `steam://run/4464990` | Setup already run |
| **Mesh to game** | Export last garment → Cook → Pack and Play | all of the above |

Missing tools disable the related button and the status strip says what to install.

## Two tracks

| Track | When | What you author | Tools |
|---|---|---|---|
| **A — tint** | Same shape, new color | Edit `items/*.json`, Pack and Play | Game + .NET 8 |
| **A+ — paint** | Same shape, new texture | Pull a map, Make a paint mod, edit the PNG, Pack and Play | Track A + **UE 5.4.4** |
| **B — new mesh** | New silhouette | Garment on `main-rig`, Export, Cook, Pack and Play | Track A + **Blender 5.1** + **UE 5.4.4** |

Live packed items are the baggy tee (new mesh) and Rowe jeans (painted albedo). Every other slot JSON is a **template** (`"sample": true`) and is not written into the catalog unless you click **Add this tint to the game**.

## Examples

| Slot | Table | File | Kind |
|---|---|---|---|
| Tops | `DT-upper` | `items/upper/tshirt-baggy-mod.json` | Live new mesh |
| Bottoms | `DT-lower` | `items/lower/oversized-jeans-mod.json` | Live paint |
| Other slots | `DT-*` | `items/<slot>/*-mod.json` with `"sample": true` | Templates only |

JSON fields: `table`, `cloneRow`, `row` (must end in `-mod` to sort first), `localizedName`, `price`, `colour`, `colours` (boot Shell/Sole/Laces/…), `refs` / `upperMale` / `albedo` / `previewImage`.

## Track B — new mesh

Shared skeleton: `/Game/MainFolder/Character/body/main-rig/main-rig` (87 UE5-mannequin bones). Rig kit: `art/rig/main-rig.blend` (armature only; rebuilt from `art/rig/main-rig.fbx`).

1. **Clothing → New mesh** copies `art/rig/main-rig.blend` and writes the item JSON.
2. Model on deform bones only (not `ik_*`). Pull clothing first so Export can use the hoodie bind pose.
3. **Export this mesh**, then **Cook**, then **Pack and Play**. Do not pack `main-rig` or `MI-Upper`.

Details: [docs/modding.md](docs/modding.md). Catalog: [docs/clothing-catalog.md](docs/clothing-catalog.md).

## What gets packed

`RollerSkate-Windows_P.utoc/.ucas` in `RollerSkate/Content/Paks` and `Paks/~mods`. `ClothingMod_P.pak` is written only if UnrealPak is installed. Overlay packages only load if the game already hard-references them from a patched DataTable.

## Legal

MIT for the tools and original RoweMod art. **Rollout Inline** content belongs to its owners. Pull clothing only from a Steam copy you own. Do not commit, upload, or redistribute extracted meshes, textures, or `.uasset` dumps. This is an unofficial fan project.

## Requirements

- Windows, Steam copy of Rollout Inline
- .NET 8 SDK (launcher + DtPatcher)
- Track B: Blender **5.1** and Unreal Engine **5.4.4**
