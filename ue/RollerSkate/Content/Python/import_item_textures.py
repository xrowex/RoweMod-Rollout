"""Import every item albedo / normal / roughness / preview PNG into Unreal."""
import json
import os
from pathlib import Path

import unreal

REPO = Path(__file__).resolve().parents[4]
ITEMS = REPO / "items"
TEX = REPO / "art" / "textures"
PREVIEWS = REPO / "art" / "previews"
FIELDS = ("albedo", "normal", "roughness", "previewImage")


def log(msg):
    unreal.log("[item-tex] " + msg)


def find_png(row, name):
    exact = list(TEX.rglob(name + ".png")) if TEX.is_dir() else []
    if exact:
        return exact[0]
    preview = PREVIEWS / (name + ".png")
    if preview.is_file():
        return preview
    folder = TEX / row if row else None
    if not folder or not folder.is_dir():
        return None
    want = name.lower()
    for p in folder.glob("*.png"):
        if p.stem.lower() == want:
            return p
    if "albedo" in want:
        for p in folder.glob("*.png"):
            if "albedo" in p.stem.lower():
                return p
    return None


def import_one(filepath, dest, name, srgb=True, normal=False):
    unreal.EditorAssetLibrary.make_directory(dest)
    task = unreal.AssetImportTask()
    task.filename = str(filepath)
    task.destination_path = dest
    task.destination_name = name
    task.replace_existing = True
    task.automated = True
    task.save = True
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])
    full = dest + "/" + name
    tex = unreal.load_asset(full)
    if tex is None:
        log("FAILED " + full)
        return
    try:
        tex.set_editor_property("srgb", srgb)
        tex.set_editor_property(
            "compression_settings",
            unreal.TextureCompressionSettings.TC_NORMALMAP
            if normal
            else unreal.TextureCompressionSettings.TC_DEFAULT,
        )
        unreal.EditorAssetLibrary.save_asset(full)
    except Exception as err:
        log("flags failed " + str(err))
    log("ok " + tex.get_path_name())


def main():
    if not ITEMS.is_dir():
        log("no items folder")
        return
    count = 0
    for json_path in ITEMS.rglob("*.json"):
        try:
            spec = json.loads(json_path.read_text(encoding="utf-8"))
        except Exception as err:
            log("skip " + str(json_path) + " " + str(err))
            continue
        row = spec.get("row") or json_path.stem
        jobs = []
        for key in FIELDS:
            game = spec.get(key)
            if game:
                jobs.append(game)
        refs = spec.get("refs") or {}
        if isinstance(refs, dict):
            for key, game in refs.items():
                if not game:
                    continue
                if "mesh" in str(key).lower():
                    continue
                jobs.append(game)
        for game in jobs:
            if not str(game).startswith("/Game/"):
                continue
            name = str(game).rstrip("/").split("/")[-1]
            dest = "/".join(str(game).rstrip("/").split("/")[:-1])
            png = find_png(row, name)
            if png is None:
                log("no png for " + name + " (" + row + ")")
                continue
            is_normal = "normal" in name.lower()
            srgb = not is_normal and "rough" not in name.lower() and "mask" not in name.lower()
            import_one(png, dest, name, srgb=srgb, normal=is_normal)
            count += 1
    log("imported " + str(count))


if __name__ == "__main__":
    main()
