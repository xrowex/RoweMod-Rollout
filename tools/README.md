# Rollout clothing tools

| Tool | Path | Use |
|---|---|---|
| FModel | `FModel/FModel.exe` | Browse IoStore, export meshes once a `.usmap` exists |
| retoc | `retoc/retoc.exe` | Zen ↔ legacy, pack `_P` IoStore overlays |
| CUE4Parse.CLI | `CUE4Parse.CLI/cue4parse.exe` | List packages, JSON/mesh export (needs usmap) |
| UAssetGUI | `UAssetGUI.exe` | Inspect/edit legacy `.uasset` |
| dump_namemap.py | this folder | Dump FName tables from legacy packages |
| pack_mod.ps1 | this folder | Pack patched `DT-upper` (and optional cooked meshes) into `~mods` |
| pull_clothing.ps1 | this folder | Extract clothing meshes/textures from your Steam copy into `dumps/game-clothing` |
| DtPatcher | `DtPatcher/` | Clone a DataTable row from `items/*.json` |
| find_ue54.ps1 | this folder | Locate UnrealEditor 5.4 |

Game paks:

`C:\Program Files (x86)\Steam\steamapps\common\RolloutInline\RollerSkate\Content\Paks`

## FModel

1. Set directory to the `RollerSkate` folder (the one that contains `Content/Paks`).
2. UE version: `GAME_UE5_4`.
3. If packages fail to open, generate `dumps/mappings.usmap` (UE4SS USMAP dump from a game session) and load it in FModel settings.

## Packing

```powershell
.\tools\extract_tables.ps1
.\tools\pack_mod.ps1
```

Patches every `items/**/*.json` into the matching DataTable and writes `RollerSkate-Windows_P.utoc/.ucas` into the game `Content/Paks` and `Content/Paks/~mods`. See [docs/modding.md](../docs/modding.md).
