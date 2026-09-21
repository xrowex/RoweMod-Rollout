# Item specs

One JSON file per customization row. `tools/pack_mod.ps1` patches live files under this folder.

`"sample": true` means the file is a template. DtPatcher skips it, so it does not appear in the game. Clothing → Kit templates → **Add this tint to the game** removes that flag.

`row` should end with `-mod` so DtPatcher inserts it at the front of the DataTable (the menus iterate `GetDataTableRowNames` in table order).

## Track A (recolor)

```json
{
  "table": "DT-lower",
  "cloneRow": "cargos-black",
  "row": "cargos-navy-mod",
  "localizedName": "Navy Cargos (Mod)",
  "price": 0,
  "colour": [0.035, 0.08, 0.2, 1.0]
}
```

`colour` is linear RGB. Omit `upperMale` / `lowerMale` / `refs` to keep the clone’s stock mesh.

## Track B (new mesh)

```json
{
  "table": "DT-upper",
  "cloneRow": "tshirt-white",
  "row": "tshirt-baggy-mod",
  "localizedName": "Baggy T-Shirt (Mod)",
  "price": 0,
  "upperMale": "/Game/MainFolder/Character/upper/tshirt-baggy/tshirt-baggy-male",
  "albedo": "/Game/MainFolder/Character/upper/tshirt-baggy/T_tshirt-baggy-albedo",
  "normal": "/Game/MainFolder/Character/upper/tshirt-baggy/T_tshirt-baggy-normal",
  "roughness": "/Game/MainFolder/Character/upper/tshirt-baggy/T_tshirt-baggy-mask",
  "previewImage": "/Game/MainFolder/UI/customization/mod-icons/T_tshirt-baggy-mod"
}
```

Hats use `refs.HatMesh` (StaticMesh). Glasses use `refs.GlassesMesh`. Boots use `colours.Shell` / `Sole` / `Laces` / …

See the slot folders for copy-paste starters.
