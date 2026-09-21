# Rollout Inline clothing mods

Use **RoweMod.exe** (`RoweMod.cmd`, `dist\RoweMod.exe` after publish, or `dotnet run --project tools/RoweMod.App`) for Setup / Export / Cook / Pack and Play / Ship. This page is the detail behind those buttons.

To look at the live hoodie (and other stock clothes) in Blender, click **Export** → **Pull clothing from my game**. That writes glTF + PNG under `dumps/game-clothing` from your Steam install. Do not commit that folder. A `.usmap` from UE4SS DumpUSMAP (`dumps/mappings.usmap`) makes mesh decode much more reliable.

Two ways to add an item. Recolor an existing garment first. Only author a new mesh if the shape does not already exist.

## Skeleton to rig to

Every body and clothing skeletal mesh shares one skeleton:

| | |
|---|---|
| Asset | `/Game/MainFolder/Character/body/main-rig/main-rig` |
| Bones | 87, UE5 mannequin names (`pelvis`, `spine_01`–`spine_05`, `clavicle_*`, `upperarm_*`, …) |
| Kit file | `art/rig/main-rig.blend` and `art/rig/main-rig.fbx` |

Those kit files are **armature only** (game bind pose, no ripped clothing meshes). Parent your garment to `main-rig`, weight-paint **deform** bones only (`pelvis`, `spine_*`, `clavicle_*`, `upperarm_*`, `lowerarm_*`, `neck_*`, `head`, legs if needed). `ik_*` bones are UE mannequin IK helpers — they stay in the file for name compatibility but are hidden and `use_deform = False`. Do not paint clothing onto them.

Hats are static meshes and do not use this skeleton. Hair uses a different skeleton.

The committed kit is armature-only, rebuilt from `art/rig/main-rig.fbx`:

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" -b --factory-startup -P art\rebuild_kit_from_fbx.py
```

If a local blend accidentally contains a body mesh, strip it with `art/strip_kit_blend.py` then rebuild from the FBX.

To rebuild from a fresh FModel hoodie glTF instead, drop it at `art/_ref/hoodie-male.glb` (gitignored) and run `art/export_main_rig.py`. Fit check: also drop `male-body-01.glb` into `art/_ref/` and shrinkwrap against it locally. Do not commit extracted game meshes.

## Track A — recolor / new row (no new mesh)

This is how beige/black/white hoodies work: same `hoodie-male` / `hoodie-female`, different albedo or `Colour` tint.

1. Copy a file under `items/` (kit templates have `"sample": true` and are not packed). Change `row`, `localizedName`, `colour`, `price`, and delete `"sample"` if you want it in the game.
2. Omit `upperMale` to keep the clone’s stock mesh. Set it only if you cooked a new skeletal mesh.
3. In **RoweMod.exe**, click **Setup** once, then **Pack and Play**. Scripts still work if you prefer a terminal:

```powershell
.\tools\bootstrap.ps1
.\tools\pack_mod.ps1
```

The overlay is `RollerSkate-Windows_P.utoc/.ucas` in `Content/Paks` and `Content/Paks/~mods`. It patches the existing `DT-upper` package. UE4SS `RolloutClothing` is optional (it only injects the row if the overlay is missing).

`Colour` is linear RGB. Navy example: `[0.035, 0.08, 0.2, 1.0]`.

## Track B — new skeletal mesh

Use this when you need a new silhouette.

1. Open `art/rig/main-rig.blend`. Model on that armature. Bone names must match exactly. Save the garment as a copy (`art/rig/main-rig_shirt.blend`) so the kit stays armature-only.
2. Click **Export** in RoweMod (or run `art/export_shirt.py`). That is the gamebind path: it rebinds weights onto `hoodie-male.glb` joints **without** aiming tails, then stitches IBMs. The visual kit in `main-rig.blend` is for painting only. The live game skeleton uses the original rest rotations.
3. Click **Cook**. Import stays in the dummy project `ue/RollerSkate/RollerSkate.uproject` at a **new** `/Game/MainFolder/...` path. Blender is meters; Unreal and the game skeleton are centimeters — `import_shirt.py` scales by 100. Assign skeleton `main-rig` and material `MI-Upper`. Do not pack `main-rig` or `MI-Upper` — the live game already has them.
4. Point the item JSON `upperMale` at that path (see `items/upper/tshirt-baggy-mod.json`).
5. Click **Pack and Play** (or **Mesh to game** to run Export → Cook → pack + launch). New IoStore packages only load if something the game already loads hard-references them (the patched `DT-upper` row). `LoadAsset` from Lua is not enough.

## Slots

| Table | Row struct | Typical mesh |
|---|---|---|
| `DT-upper` | `S-upper` | SkeletalMesh (`UpperMale` / `UpperFemale`) |
| `DT-lower` | `S-lower` | SkeletalMesh |
| `DT-hats` | `S-hats` | StaticMesh |
| `DT-glasses` / `DT-hair` / `DT-beard` | matching `S-*` | mixed |

Upper fields that matter for a new row: `UpperMale`, `UpperFemale`, `Albedo`, `Normal`, `Roughness`, `BodyMask`, `HeadMask`, `PreviewImage`, `LocalizedName`, `Price`, `Colour`.

## Preview in Unreal

The cook project is `ue/RollerSkate/RollerSkate.uproject` (engine 5.4). After **Export** in RoweMod:

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" -b -P art\export_preview_body.py
.\ue\open_preview.ps1 -Setup
```

That opens `/Game/ModPreview/Preview` with the male body and your shirt on `main-rig`. If it looks right there and wrong in the game, the dummy skeleton still does not match the live `main-rig` rest pose. Do not pack `/Game/ModPreview`.

## Game / engine

Rollout Inline, UE **5.4.4**, IoStore, no AES. Cooked assets need a `.usmap` (UE4SS DumpUSMAP) for FModel/UAssetAPI. Dummy cook project engine association must stay 5.4, content paths must stay under `/Game/MainFolder/`.
