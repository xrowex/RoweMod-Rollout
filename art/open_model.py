"""Open a pulled game mesh (glb / gltf / fbx) in Blender.

`blender file.glb` tries to load it as a .blend and fails. Use this instead:

    blender --python art/open_model.py -- path/to/standard-boots.glb
"""
from __future__ import annotations

import sys
from pathlib import Path

import bpy


def _path() -> Path:
    argv = sys.argv
    if "--" in argv:
        rest = argv[argv.index("--") + 1 :]
        if rest:
            return Path(rest[0])
    raise SystemExit("Need a mesh path after --")


def _clear_startup() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def _import(path: Path) -> None:
    ext = path.suffix.lower()
    text = str(path)
    if ext in {".glb", ".gltf"}:
        bpy.ops.import_scene.gltf(filepath=text)
        return
    if ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=text)
        return
    raise SystemExit("Open a .glb, .gltf, or .fbx — not " + ext)


def _frame() -> None:
    screen = getattr(bpy.context, "screen", None)
    if screen is None:
        return
    for area in screen.areas:
        if area.type != "VIEW_3D":
            continue
        for region in area.regions:
            if region.type != "WINDOW":
                continue
            with bpy.context.temp_override(area=area, region=region):
                bpy.ops.view3d.view_all()
            return


def _frame_later() -> float | None:
    _frame()
    return None


path = _path()
if not path.is_file():
    raise SystemExit("Missing file: " + str(path))

_clear_startup()
sys.path.insert(0, str(Path(__file__).resolve().parent))
if "--skate-kind" in sys.argv and sys.argv[sys.argv.index("--skate-kind") + 1] == "boots":
    from skate_rig import import_boots
    import_boots(path)
else:
    _import(path)
bpy.context.view_layer.update()
if "--reference" in sys.argv:
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from skate_reference import add_reference
    reference = Path(sys.argv[sys.argv.index("--reference") + 1])
    kind = sys.argv[sys.argv.index("--skate-kind") + 1]
    add_reference(reference, kind)
    bpy.context.view_layer.update()
if "--save-blend" in sys.argv:
    target = Path(sys.argv[sys.argv.index("--save-blend") + 1])
    if target.exists():
        raise RuntimeError("Project already exists; open it instead: " + str(target))
    target.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(target))
    print("ROWE_PROJECT_SAVED", target)
try:
    bpy.app.timers.register(_frame_later, first_interval=0.4)
except Exception:
    _frame()
