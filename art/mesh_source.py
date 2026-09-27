"""Load an editable source without saving over it."""
import os
from pathlib import Path

import bpy


def source_path(default=None):
    value = os.environ.get("ROWE_SOURCE") or os.environ.get("ROWE_BLEND") or default
    if not value:
        raise RuntimeError("Choose a saved .blend or .fbx file first.")
    return Path(value).resolve()


def load_source(source):
    if not source.is_file():
        raise FileNotFoundError(f"Missing mesh source: {source}")
    if source.suffix.lower() == ".blend":
        bpy.ops.wm.open_mainfile(filepath=str(source))
    elif source.suffix.lower() == ".fbx":
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(source), use_anim=False)
    else:
        raise RuntimeError("Mesh source must be a .blend or .fbx file.")


def protect_source(source, *outputs):
    for output in outputs:
        if Path(output).resolve() == source.resolve():
            raise RuntimeError(f"Export would overwrite your source: {source}")
