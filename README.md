# RoweMod × Rollout

Add clothes to the **[Rollout Inline](https://store.steampowered.com/app/4464990/)** customization menus. This is an unofficial fan kit. It does not replace the game’s files with ripped meshes.

You do not run PowerShell by hand. You open one app. The green **Next** card is the click to do.

## Install

Windows only. You need a Steam copy of Rollout Inline.

1. Install the **[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** (the SDK, not only the runtime).
2. Get this repo: **Code → Download ZIP** and unzip it, or `git clone https://github.com/xrowex/RoweMod-Rollout.git`.
3. Double-click **`RoweMod.cmd`**. The first launch may take a minute while it builds.

That is the whole install. Optional later:

- **[Blender 5.1](https://www.blender.org/download/)** if you want a new shape
- **Unreal Engine 5.4.4** if you want to paint a texture or cook a new mesh

## Use

The How-to panel on the right has one green **Next** card. Hover a button only if you want that button explained.

1. **Setup** (once). Finds the game and copies the clothing menus.
2. **Play**. Writes the baggy tee and Rowe jeans into the game.
3. **Create** or **Gallery** when you want to make or subscribe.

| I want to… | Clicks | Also need |
|---|---|---|
| See the examples in-game | Setup → Play | Steam game |
| Wear a community mod | Gallery → Subscribe → Play | Steam game |
| Share a mod | Cook it via Play, Gallery → Share… | Unreal 5.4.4 + GitHub |
| Paint a texture | Create → Clothes → Paint → edit PNG → Play | Unreal 5.4.4 |
| Make a new shape | Create → Clothes → Shape → Blender → Export + Play | Blender 5.1 + Unreal 5.4.4 |
| Change skates | Create → Skates → Paint or Shape → Play | Unreal 5.4.4 (Blender if you change the shape) |

The gray bones in Blender are the real game skeleton. There is no hoodie until you model one.

Color examples like Navy Hoodie stay on disk as samples. They are not packed. Paint a PNG instead.

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

Shared mods live in **[xrowex/RoweMod-Gallery](https://github.com/xrowex/RoweMod-Gallery)**. Share opens a PR. Players only need RoweMod and the Steam game — no Unreal to wear a subscribed mod. Do not upload ripped meshes.

Live speed and feel options are a **separate** repo (UE4SS, not the clothing overlay): **[xrowex/RoweMod-Gameplay](https://github.com/xrowex/RoweMod-Gameplay)**.

## Advanced

Item JSON and packing details: [items/README.md](items/README.md), [docs/modding.md](docs/modding.md), [docs/clothing-catalog.md](docs/clothing-catalog.md).

## Legal

MIT for the tools and original RoweMod art. Rollout Inline belongs to its owners. Pull clothes only from a Steam copy you own. Unofficial fan project.
