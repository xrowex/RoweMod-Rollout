"""Build an armature-only main-rig from the extracted game hoodie skeleton.

Keeps bone names and bind-pose joint positions. glTF joints have no tails,
so Blender's default import points every octahedron sideways; this script
aims tails at deform children so the rig stands and reads as a body.
"""
from __future__ import annotations

from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
SOURCES = [
    ROOT / "art" / "_ref" / "hoodie-male.glb",
    ROOT / "dumps" / "meshes" / "RollerSkate" / "Content" / "MainFolder" / "Character" / "upper" / "hoodie" / "hoodie-male.glb",
]
OUT_DIR = ROOT / "art" / "rig"
BLEND = OUT_DIR / "main-rig.blend"
FBX = OUT_DIR / "main-rig.fbx"

PREFERRED_CHILD = {
    "root": "pelvis",
    "pelvis": "spine_01",
    "spine_01": "spine_02",
    "spine_02": "spine_03",
    "spine_03": "spine_04",
    "spine_04": "spine_05",
    "spine_05": "neck_01",
    "neck_01": "neck_02",
    "neck_02": "head",
    "clavicle_l": "upperarm_l",
    "clavicle_r": "upperarm_r",
    "upperarm_l": "lowerarm_l",
    "upperarm_r": "lowerarm_r",
    "lowerarm_l": "hand_l",
    "lowerarm_r": "hand_r",
    "thigh_l": "calf_l",
    "thigh_r": "calf_r",
    "calf_l": "foot_l",
    "calf_r": "foot_r",
    "foot_l": "ball_l",
    "foot_r": "ball_r",
}


def source_glb() -> Path:
    for path in SOURCES:
        if path.exists():
            return path
    raise FileNotFoundError(
        "Missing hoodie-male.glb. Export it with FModel (UEFormat/glTF) into art/_ref/ first."
    )


def skip_child(name: str) -> bool:
    lower = name.lower()
    return "twist" in lower or lower.startswith("ik_") and "root" not in lower


def aim_tails(arm: bpy.types.Object) -> None:
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    edits = arm.data.edit_bones
    for bone in edits:
        if bone.name.startswith("ik_"):
            bone.use_deform = False
            bone.tail = bone.head + Vector((0.0, 0.0, 0.04))
            bone.use_connect = False
            continue
        target_name = PREFERRED_CHILD.get(bone.name)
        child = edits.get(target_name) if target_name else None
        if child is None:
            kids = [c for c in bone.children if not skip_child(c.name)]
            child = kids[0] if kids else (bone.children[0] if bone.children else None)
        if child is None:
            direction = Vector((0.0, 0.0, 0.05))
            if bone.parent:
                along = bone.head - bone.parent.head
                if along.length > 1e-5:
                    direction = along.normalized() * max(0.04, min(0.08, along.length * 0.35))
            bone.tail = bone.head + direction
            continue
        delta = child.head - bone.head
        if delta.length < 1e-5:
            bone.tail = bone.head + Vector((0.0, 0.0, 0.05))
        else:
            bone.tail = child.head
        bone.use_connect = False
    bpy.ops.armature.select_all(action="SELECT")
    bpy.ops.armature.calculate_roll(type="GLOBAL_POS_Y")
    bpy.ops.object.mode_set(mode="OBJECT")
    hide_ik_collection(arm)
    arm.data.display_type = "OCTAHEDRAL"
    arm.show_in_front = True


def hide_ik_collection(arm: bpy.types.Object) -> None:
    data = arm.data
    deform = data.collections.get("Deform") or data.collections.new("Deform")
    ik = data.collections.get("IK") or data.collections.new("IK")
    bpy.ops.object.mode_set(mode="EDIT")
    for bone in data.edit_bones:
        for coll in list(data.collections):
            try:
                coll.unassign(bone)
            except Exception:
                pass
        (ik if bone.name.startswith("ik_") else deform).assign(bone)
    bpy.ops.object.mode_set(mode="OBJECT")
    ik.is_visible = False
    print("IK_HIDDEN", len([b for b in data.bones if b.name.startswith("ik_")]))


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    src = source_glb()
    bpy.ops.import_scene.gltf(filepath=str(src), bone_heuristic="FORTUNE")
    armatures = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if not armatures:
        raise RuntimeError("glTF had no armature")
    arm = armatures[0]
    arm.name = "main-rig"
    arm.data.name = "main-rig"
    for obj in list(bpy.data.objects):
        if obj != arm:
            bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)

    aim_tails(arm)

    pelvis = arm.data.bones.get("pelvis")
    head = arm.data.bones.get("head")
    hand_l = arm.data.bones.get("hand_l")
    print("SOURCE", src)
    print("BONES", len(arm.data.bones))
    if pelvis and head:
        print("PELVIS_Z", round(pelvis.head_local.z, 3), "HEAD_Z", round(head.head_local.z, 3))
    if hand_l:
        print("HAND_L", tuple(round(c, 3) for c in hand_l.head_local))
    for name in ("pelvis", "spine_01", "upperarm_l", "thigh_l"):
        b = arm.data.bones[name]
        d = (b.tail_local - b.head_local).normalized()
        print(f"DIR {name} {tuple(round(c, 3) for c in d)}")

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
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
    print("WROTE", BLEND)
    print("WROTE", FBX)


main()
