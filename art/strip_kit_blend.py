"""Strip ripped body mesh from a main-rig blend and rewrite the committed FBX.

Used once to produce an armature-only kit. Clones should run rebuild_kit_from_fbx.py.
"""
from __future__ import annotations

from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "art" / "rig"
BLEND = OUT_DIR / "main-rig.blend"
BLEND1 = OUT_DIR / "main-rig.blend1"
FBX = OUT_DIR / "main-rig.fbx"


def strip_non_armature() -> bpy.types.Object:
    armatures = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if not armatures:
        raise RuntimeError("no armature")
    arm = armatures[0]
    arm.name = "main-rig"
    arm.data.name = "main-rig"
    for obj in list(bpy.data.objects):
        if obj.type != "ARMATURE":
            bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)
    return arm


def export_fbx(arm: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=str(FBX),
        use_selection=True,
        add_leaf_bones=False,
        bake_anim=False,
        armature_nodetype="NULL",
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        object_types={"ARMATURE"},
        use_armature_deform_only=False,
    )


def main() -> None:
    src = BLEND1 if BLEND1.exists() else BLEND
    bpy.ops.wm.open_mainfile(filepath=str(src))
    arm = strip_non_armature()
    print("BONES", len(arm.data.bones))
    print("MESHES", len(bpy.data.meshes))
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND), compress=True)
    export_fbx(arm)
    print("WROTE", BLEND)
    print("WROTE", FBX)


main()
