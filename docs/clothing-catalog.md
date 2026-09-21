# Rollout Inline clothing catalog

Game: **Rollout Inline** (`RollerSkate.exe`), Unreal Engine **5.4.4**, IoStore (`RollerSkate-Windows.utoc` / `.ucas`). No AES key required to list packages. Cooked packages have `FileVersion=0`, so CUE4Parse/FModel need a `.usmap` to deserialize properties and export meshes. Name maps and `retoc to-legacy` work without mappings.

## Skeleton

| | |
|---|---|
| Asset | `/Game/MainFolder/Character/body/main-rig/main-rig` |
| Type | `USkeleton` |
| Physics | `/Game/MainFolder/Character/body/main-rig/PA-RI-main` |
| Control rig | `/Game/MainFolder/Animations/CR_MainCharacter` |
| Character BP | `/Game/MainFolder/Blueprints/NewMainCharacter` |

Shared by `male-body-01`, `female-body-01`, `hoodie-male`, `hoodie-female`, and every other clothing skeletal mesh. Hair uses a separate skeleton (`hair-tight-braids_Skeleton`). Hats are **static meshes**, not skinned.

This is the UE5 mannequin layout (~87 bones) plus cloth wind curves (`ShirtUp`, `BackWind`, `WindRaise`, `UpperBody`).

### Bones

```
root
  pelvis
    spine_01
      spine_02
        spine_03
          spine_04
            spine_05
              neck_01
                neck_02
                  head
              clavicle_l / clavicle_r
                upperarm_*
                  upperarm_twist_01_*
                  upperarm_twist_02_*
                  lowerarm_*
                    lowerarm_twist_01_*
                    lowerarm_twist_02_*
                    hand_*
                      (index/middle/ring/pinky 01-03 + metacarpal, thumb 01-03)
    thigh_l / thigh_r
      thigh_twist_01_* / thigh_twist_02_*
      calf_*
        calf_twist_01_* / calf_twist_02_*
        foot_*
          ball_*
  ik_foot_root → ik_foot_l, ik_foot_r
  ik_hand_root → ik_hand_gun, ik_hand_l, ik_hand_r
```

Materials: body uses `MI-BodyMain` / `MI-HeadMain` / `MI-Eyes`. Clothing uses `MI-Upper` (instance of `M-cloth-master`).

`NewMainCharacter` loads slots with `Load Body`, `Load Upper`, `Load Lower`, `Load Hat` and stores `UpperRowName` / `LowerRowName` / `HatRowName` / `BodyRowName`. It also has `HandleFakeClothWind`.

## DataTables

All under `/Game/MainFolder/UI/customization/data/`.

| Table | Row struct | Slot |
|---|---|---|
| `DT-upper` | `S-upper` | Tops |
| `DT-lower` | `S-lower` | Pants / shorts |
| `DT-hats` | `S-hats` | Hats (static mesh) |
| `DT-glasses` | `S-glasses` | Glasses |
| `DT-hair` | `S-hair` | Hair |
| `DT-beard` | `S-beard` | Beard |
| `DT-bodytypes` | `S-bodytypes` | Body mesh + albedo/normal |
| `DT-skin` | `S-skin` | Skin |
| `DT-eyes` | `S-eyes` | Eyes |
| `DT-boot` | `S-boot` | Skate boots |
| `DT-frames` | `S-frames` | Frames |
| `DT-wheels` | `S-wheels` | Wheels |

UI: `W-clothing-customization`, `W-generic-item-customization`, `W-character-customization`, `W-skates-customization`, `E-customization-categories`. Save: `CustomizationSaveGame`.

### `S-upper` fields

| Property | Type | Role |
|---|---|---|
| `UpperMale` | SkeletalMesh | Male garment |
| `UpperFemale` | SkeletalMesh | Female garment (may be missing; vest is male-only) |
| `Albedo` | Texture2D | Base color |
| `Normal` | Texture2D | Normal |
| `Roughness` | Texture2D | Roughness |
| `BodyMask` | Texture2D | Hides body under clothes (`hoodie-mask`, `tshirt-body-mask`) |
| `HeadMask` | Texture2D | Hides hair/head (`tshirt-head-mask`, `short-shirt-head-mask`) |
| `PreviewImage` | Texture2D | Menu icon |
| `LocalizedName` | Text | Menu label |
| `Price` | int | Unlock cost (0 = free / already unlocked) |
| `Colour` | LinearColor | Tint |

### `DT-upper` rows (v1 clone source)

| Row | Male mesh | Female mesh |
|---|---|---|
| `hoodie-beige` / `hoodie-black` / `hoodie-white` | `hoodie-male` | `hoodie-female` |
| `tshirt-white` / `tshirt-black` / `tshirt-beige` / `tshirt-rose-*` | `tshirt-male` | `tshirt-female` |
| `layered-sleeve-*` | `layered-sleeve-male` | `layered-sleeve-female` |
| `short-shirt-*` | `short-shirt-male` | (male mesh) |
| `vest-tshirt-*` | `vest-tshirt-male` | (male mesh) |

Mesh paths live under `/Game/MainFolder/Character/upper/<item>/`.

### `DT-lower` rows

`cargos-beige/black/green`, `oversized-jeans-dark`, `short-pants-street`, `skinny-jeans-*`, `styled-pants-bright-blue`.

Hoodie-white (clone source) is 55 currency. T-shirts at the top of the table are price 0.

Exact `hoodie-white` pointers:

- Male: `/Game/MainFolder/Character/upper/hoodie/hoodie-male`
- Female: `/Game/MainFolder/Character/upper/hoodie/hoodie-female`
- Albedo/normal/roughness: `/Game/MainFolder/Character/upper/hoodie/white/FABRIC_1_15833433_001_*`
- Body mask: `hoodie-mask`
- Head mask: `tshirt-head-mask`
- LocalizedName: `Hoodie`

Live overlay examples (all `*-mod` rows sort first):

- `hoodie-navy-mod` — Track A Colour tint. Spec: `items/upper/hoodie-navy-mod.json`.
- `tshirt-baggy-mod` — Track B mesh `tshirt-baggy-male`. Spec: `items/upper/tshirt-baggy-mod.json`.
- Plus one Track A row per other slot under `items/lower`, `hats`, `glasses`, `hair`, `beard`, `body`, `skin`, `eyes`, `boots`, `frames`, `wheels`.

New-mesh and recolor workflows: [modding.md](modding.md).

## Tool notes

- FModel: `tools/FModel/FModel.exe`, UE version **GAME_UE5_4**. Point at `...\RolloutInline\RollerSkate`. Mesh export = UEFormat. Needs a `.usmap` for this build.
- retoc: `tools/retoc/retoc.exe`. `to-legacy --filter <name> --version UE5_4` and `to-zen --version UE5_4`.
- CUE4Parse.CLI: `tools/CUE4Parse.CLI/cue4parse.exe` (same usmap requirement).
- Blender: **5.1** at `C:\Program Files\Blender Foundation\Blender 5.1\blender.exe`.
- Dummy cook project: `ue/RollerSkate/RollerSkate.uproject` (paths must stay `/Game/MainFolder/...` so the overlay resolves the live `main-rig` skeleton).
