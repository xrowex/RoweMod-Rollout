"""Rebuild armature-only art/rig/main-rig.blend from the committed FBX.

The FBX already has aimed tails from export_main_rig.py. This strips any
mesh/empty leftovers so clones get a paint-ready kit without ripped body.
"""
from __future__ import annotations

from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "art" / "rig"
FBX = OUT_DIR / "main-rig.fbx"
BLEND = OUT_DIR / "main-rig.blend"


def main() -> None:
    if not FBX.exists():
        raise FileNotFoundError(f"Missing {FBX}")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(
        filepath=str(FBX),
        automatic_bone_orientation=False,
        ignore_leaf_bones=False,
        use_anim=False,
    )
    armatures = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if not armatures:
        raise RuntimeError("FBX had no armature")
    arm = armatures[0]
    arm.name = "main-rig"
    arm.data.name = "main-rig"
    for obj in list(bpy.data.objects):
        if obj.type != "ARMATURE":
            bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)
    for empty in list(bpy.data.objects):
        if empty.type == "EMPTY":
            bpy.data.objects.remove(empty, do_unlink=True)

    data = arm.data
    deform = data.collections.get("Deform") or data.collections.new("Deform")
    ik = data.collections.get("IK") or data.collections.new("IK")
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for bone in data.edit_bones:
        for coll in list(data.collections):
            try:
                coll.unassign(bone)
            except Exception:
                pass
        (ik if bone.name.startswith("ik_") else deform).assign(bone)
        if bone.name.startswith("ik_"):
            bone.use_deform = False
    bpy.ops.object.mode_set(mode="OBJECT")
    ik.is_visible = False
    data.display_type = "OCTAHEDRAL"
    arm.show_in_front = True

    bones = list(data.bones)
    print("BONES", len(bones))
    print("MESHES", len(bpy.data.meshes))
    pelvis = data.bones.get("pelvis")
    head = data.bones.get("head")
    if pelvis and head:
        print("PELVIS_Z", round(pelvis.head_local.z, 3), "HEAD_Z", round(head.head_local.z, 3))

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND), compress=True)
    print("WROTE", BLEND)


main()
