# RoweMod × Rollout

Add clothes to the **[Rollout Inline](https://store.steampowered.com/app/4464990/)** customization menus. This is an unofficial fan kit. It does not replace the game’s files with ripped meshes.

You do not run PowerShell by hand. You open one app. The green **Next** card is the click to do.

## Install

Windows only. You need a Steam copy of Rollout Inline.

1. Download **RoweMod-Windows-x64.zip** from [Releases](https://github.com/xrowex/RoweMod-Rollout/releases).
2. Extract the entire ZIP into a writable folder, such as Documents.
3. Double-click **`RoweMod.cmd`**. Keep its folders together.

**No .NET install, SDK, Git, or build step is needed.** The Windows release includes its runtime and extraction tools. GitHub's **Code → Download ZIP** contains developer source, not the ready-to-run app. If no Windows release is listed yet, the maintainer still needs to upload the package.

That is the whole install. Optional later:

- **[Blender 5.1](https://www.blender.org/download/)** if you want a new shape
- **Unreal Engine 5.4.4** if you want to paint a texture or cook a new mesh

The creator's **Get from game** importer also needs mappings for your game version (`dumps/mappings.usmap`). These are not included in the portable ZIP. Gallery subscriptions and wearing published mods do not need mappings or UE4SS.

## Use

The How-to panel on the right has one green **Next** card. Hover a button only if you want that button explained.

1. **Setup** (once). Finds the game and copies the clothing menus.
2. **Gallery → Subscribe** to the baggy tee, Rowe jeans, or another published mod.
3. **Play**. Downloads your subscriptions, writes them into the game, and launches.
4. **Create** when you want to make your own.

| I want to… | Clicks | Also need |
|---|---|---|
| Wear the shirt and jeans | Setup → Gallery → Subscribe → Play | Steam game |
| Wear a community mod | Gallery → Subscribe → Play | Steam game |
| Share a mod | Cook it via Play, Gallery → Share… | Unreal 5.4.4 + GitHub |
| Paint a texture | Create → Clothes → Paint → edit PNG → Play | Unreal 5.4.4 |
| Make a new shape | Create → Clothes → Shape → Create → Blender → Ctrl+S → Export + Play | Blender 5.1 + Unreal 5.4.4 |
| Change skates | Create → Skates → Paint, or Shape → Blender → Ctrl+S → Export + Play | Unreal 5.4.4 (Blender for shapes) |
| Delete a local mod | Create → its Delete… button → Play | Source artwork is kept |
| Restore a deleted mod | Create → Deleted mods → Restore → Play | Nothing is overwritten |

The gray bones in Blender are the real game skeleton. There is no hoodie until you model one.

Color examples like Navy Hoodie stay on disk as samples. They are not packed. Paint a PNG instead.

## Save a custom mesh

**Create** makes a separate Blender project for the mod. Garments are saved in `art/rig/<name>-mod.blend`; skate parts are saved in `art/skates/<name>-mod.blend` when Blender opens them. The mod's card shows its project path.

1. Model and weight clothes to the supplied rig. For boots, keep the imported armature and weights; frames are static meshes.
2. Press **Ctrl+S in Blender**. Keep using this `.blend` as your editable source.
3. Return to RoweMod and click the item's **Export + Play**. RoweMod exports the saved project, cooks it, and launches. An export failure stops before Play.

You do not need to export a GLB by hand. If you used Blender's **Save As** elsewhere, click **Choose mesh…** on the mod and select that project. **Open in Blender** reopens it; **Show files** locates it. **Refresh saved files** updates the list without restarting the app.

**FBX also works:** choose your `.fbx` with **Choose mesh…**, then **Export + Play**. After editing in your modeling app, export over that same FBX again. Ctrl+S in Blender saves a Blender project; it does not update an FBX. To switch to Blender saving, use **Save As → .blend** and choose that file instead. Clothes and boots must include the game armature and weights; frames can be static. Blender is still required for conversion. Your source file is never overwritten by RoweMod.

**Delete…** removes the mod from your local list and the next game's overlay, including a subscribed copy with the same row. Your Blender projects and textures stay on disk. **Deleted mods → Restore** brings its definition back. The shared online gallery is not changed. Deleted definitions are kept in `dumps/deleted-mods`; keep that folder if you want to restore them later.

## Buttons

| Button | What it does |
|---|---|
| **Setup** | One-time. Find the game, download retoc, copy clothing menus. |
| **Create** | Clothes or Skates. Paint a PNG or start a new shape. |
| **Gallery** | Subscribe, then Play. Share… opens a GitHub PR. |
| **Play** | Cooks if needed, merges local + subscribed mods, writes the overlay, launches. |

Hover a toolbar button for that button only. The log is job output, not instructions.

## What this repo ships

- The RoweMod app (`RoweMod.cmd` / `tools/RoweMod.App`)
- The shared skeleton (`art/rig/main-rig.blend`)
- Two live examples: baggy tee (new mesh) and Rowe jeans (paint)
- Color-tint templates under `items/` (`"sample": true` — not packed)

It does **not** ship the game, Unreal, Blender, retoc, or ripped clothes. **Create → Get from game** copies those from your Steam install onto this PC only. Do not commit or upload `dumps/`.

Shared mods live in **[xrowex/RoweMod-Gallery](https://github.com/xrowex/RoweMod-Gallery)**. In Gallery, **Upload mod…** submits a new cooked local item; **Update my mod…** submits a replacement for a published local item. Both open a GitHub pull request and go live after merge. **Refresh gallery** reloads the catalog; subscribed items sync when you Play. Players only need RoweMod and the Steam game — no Unreal to wear a subscribed mod. Do not upload ripped meshes.

Live speed and feel options are a **separate** repo (UE4SS, not the clothing overlay): **[xrowex/RoweMod-Gameplay](https://github.com/xrowex/RoweMod-Gameplay)**.

## Advanced

Developers only: install a .NET SDK compatible with .NET 8 and use `RoweMod.Dev.cmd` to run current source. `tools/publish_rowemod.ps1` refreshes `dist`; `tools/package_rowemod.ps1` builds a clean Windows ZIP with bundled runtimes. The package excludes local mods, extracted game files, personal settings, and credentials. The **Windows portable package** GitHub workflow produces the same ZIP as a downloadable artifact; upload it to a GitHub Release for users.

For an update, extract into a new folder and keep your old copy until you have transferred your creations. The app no longer pulls Git source changes automatically.

Item JSON and packing details: [items/README.md](items/README.md), [docs/modding.md](docs/modding.md), [docs/clothing-catalog.md](docs/clothing-catalog.md).

## Legal

MIT for the tools and original RoweMod art. Rollout Inline belongs to its owners. Pull clothes only from a Steam copy you own. Unofficial fan project.
