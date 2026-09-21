# RoweMod × Rollout

Add clothes to the **[Rollout Inline](https://store.steampowered.com/app/4464990/)** customization menus. This is an unofficial fan kit. It does not replace the game’s files with ripped meshes.

You do not run PowerShell by hand. You open one app, click **Setup**, then **Pack and Play**.

## Install

Windows only. You need a Steam copy of Rollout Inline.

1. Install the **[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** (the SDK, not only the runtime).
2. Get this repo: **Code → Download ZIP** and unzip it, or `git clone https://github.com/xrowex/RoweMod-Rollout.git`.
3. Double-click **`RoweMod.cmd`**. The first launch may take a minute while it builds.

That is the whole install. Optional later:

- **[Blender 5.1](https://www.blender.org/download/)** if you want a new shape
- **Unreal Engine 5.4.4** if you want to paint a texture or cook a new mesh

## Use

The How-to panel on the right of the app tells you the next click. In short:

1. **Setup** (once). Finds the game and copies the clothing menus. The button greys out when it is done.
2. **Pack and Play**. Writes the two example items into the game and launches Steam: **Baggy T-Shirt** and **Rowe Jeans**.
3. **Clothing** when you want to make your own.

| I want to… | Clicks | Also need |
|---|---|---|
| See the examples in-game | Setup → Pack and Play | Steam game |
| Change a color | Clothing → Color tints → Add this color to the game → Pack and Play | — |
| Paint a texture | Clothing → Get clothes from my game → Make a paint mod → edit the PNG → Pack and Play | Unreal 5.4.4 |
| Make a new shape | Clothing → Create a garment → model in Blender → Export this mesh → Cook → Pack and Play | Blender 5.1 + Unreal 5.4.4 |

The gray bones in Blender are the real game skeleton. There is no hoodie until you model one.

Color examples like Navy Hoodie stay on disk as templates. They do not fill the catalog unless you add them.

## Buttons

| Button | What it does |
|---|---|
| **Setup** | One-time. Find the game, download retoc, copy clothing menus. |
| **Clothing** | Workshop: paint, tint, or start a new mesh. |
| **Cook** | Unreal prepares a PNG or mesh. Does not launch the game. |
| **Pack and Play** | Write your items into the game and launch. |
| **Mesh to game** | After you modeled: export + cook + pack + launch. |

If a button is grey, hover it. The How-to panel says what to install.

## What this repo ships

- The RoweMod app (`RoweMod.cmd` / `tools/RoweMod.App`)
- The shared skeleton (`art/rig/main-rig.blend`)
- Two live examples: baggy tee (new mesh) and Rowe jeans (paint)
- Color-tint templates under `items/` (`"sample": true` — not packed)

It does **not** ship the game, Unreal, Blender, retoc, or ripped clothes. **Clothing → Get clothes from my game** copies those from your Steam install onto this PC only. Do not commit or upload `dumps/`.

## Advanced

Item JSON and packing details: [items/README.md](items/README.md), [docs/modding.md](docs/modding.md), [docs/clothing-catalog.md](docs/clothing-catalog.md).

## Legal

MIT for the tools and original RoweMod art. Rollout Inline belongs to its owners. Pull clothes only from a Steam copy you own. Unofficial fan project.
