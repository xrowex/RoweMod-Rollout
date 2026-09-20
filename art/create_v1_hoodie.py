"""Create the UE5-mannequin main-rig armature and a v1 navy hoodie, then export FBX."""
from __future__ import annotations

from pathlib import Path

import bpy
from mathutils import Vector, Euler
from math import radians

ROOT = Path(r"C:\Users\xrowe\rolloutrowemod")
ART = ROOT / "art"
BLEND = ART / "hoodie-navy-male.blend"
FBX = ART / "hoodie-navy-male.fbx"
REF_BLEND = ART / "_ref" / "skater_armature.blend"


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in list(bpy.data.meshes):
        bpy.data.meshes.remove(block)
    for block in list(bpy.data.armatures):
        bpy.data.armatures.remove(block)
    for block in list(bpy.data.materials):
        bpy.data.materials.remove(block)


# name, parent, head, tail  (Z-up meters, A-pose)
BONES = [
    ("root", None, (0, 0, 0.0), (0, 0, 0.08)),
    ("pelvis", "root", (0, 0, 0.98), (0, 0, 1.06)),
    ("spine_01", "pelvis", (0, 0, 1.06), (0, 0, 1.16)),
    ("spine_02", "spine_01", (0, 0, 1.16), (0, 0, 1.26)),
    ("spine_03", "spine_02", (0, 0, 1.26), (0, 0, 1.36)),
    ("spine_04", "spine_03", (0, 0, 1.36), (0, 0, 1.46)),
    ("spine_05", "spine_04", (0, 0, 1.46), (0, 0, 1.54)),
    ("neck_01", "spine_05", (0, 0, 1.54), (0, 0, 1.62)),
    ("neck_02", "neck_01", (0, 0, 1.62), (0, 0, 1.68)),
    ("head", "neck_02", (0, 0, 1.68), (0, 0, 1.86)),
    ("clavicle_l", "spine_05", (0.03, 0, 1.50), (0.16, 0, 1.50)),
    ("clavicle_r", "spine_05", (-0.03, 0, 1.50), (-0.16, 0, 1.50)),
    ("upperarm_l", "clavicle_l", (0.18, 0, 1.50), (0.42, 0.08, 1.28)),
    ("upperarm_r", "clavicle_r", (-0.18, 0, 1.50), (-0.42, 0.08, 1.28)),
    ("upperarm_twist_01_l", "upperarm_l", (0.22, 0.02, 1.46), (0.30, 0.04, 1.40)),
    ("upperarm_twist_01_r", "upperarm_r", (-0.22, 0.02, 1.46), (-0.30, 0.04, 1.40)),
    ("upperarm_twist_02_l", "upperarm_l", (0.30, 0.04, 1.40), (0.38, 0.06, 1.32)),
    ("upperarm_twist_02_r", "upperarm_r", (-0.30, 0.04, 1.40), (-0.38, 0.06, 1.32)),
    ("lowerarm_l", "upperarm_l", (0.42, 0.08, 1.28), (0.58, 0.04, 1.04)),
    ("lowerarm_r", "upperarm_r", (-0.42, 0.08, 1.28), (-0.58, 0.04, 1.04)),
    ("lowerarm_twist_01_l", "lowerarm_l", (0.45, 0.07, 1.22), (0.50, 0.06, 1.14)),
    ("lowerarm_twist_01_r", "lowerarm_r", (-0.45, 0.07, 1.22), (-0.50, 0.06, 1.14)),
    ("lowerarm_twist_02_l", "lowerarm_l", (0.50, 0.06, 1.14), (0.56, 0.05, 1.06)),
    ("lowerarm_twist_02_r", "lowerarm_r", (-0.50, 0.06, 1.14), (-0.56, 0.05, 1.06)),
    ("hand_l", "lowerarm_l", (0.58, 0.04, 1.04), (0.68, 0.02, 0.98)),
    ("hand_r", "lowerarm_r", (-0.58, 0.04, 1.04), (-0.68, 0.02, 0.98)),
    ("thigh_l", "pelvis", (0.10, 0, 0.98), (0.12, 0.02, 0.54)),
    ("thigh_r", "pelvis", (-0.10, 0, 0.98), (-0.12, 0.02, 0.54)),
    ("thigh_twist_01_l", "thigh_l", (0.10, 0.0, 0.90), (0.11, 0.01, 0.78)),
    ("thigh_twist_01_r", "thigh_r", (-0.10, 0.0, 0.90), (-0.11, 0.01, 0.78)),
    ("thigh_twist_02_l", "thigh_l", (0.11, 0.01, 0.70), (0.12, 0.02, 0.58)),
    ("thigh_twist_02_r", "thigh_r", (-0.11, 0.01, 0.70), (-0.12, 0.02, 0.58)),
    ("calf_l", "thigh_l", (0.12, 0.02, 0.54), (0.12, 0.04, 0.12)),
    ("calf_r", "thigh_r", (-0.12, 0.02, 0.54), (-0.12, 0.04, 0.12)),
    ("calf_twist_01_l", "calf_l", (0.12, 0.02, 0.46), (0.12, 0.03, 0.34)),
    ("calf_twist_01_r", "calf_r", (-0.12, 0.02, 0.46), (-0.12, 0.03, 0.34)),
    ("calf_twist_02_l", "calf_l", (0.12, 0.03, 0.26), (0.12, 0.04, 0.16)),
    ("calf_twist_02_r", "calf_r", (-0.12, 0.03, 0.26), (-0.12, 0.04, 0.16)),
    ("foot_l", "calf_l", (0.12, 0.04, 0.12), (0.12, -0.10, 0.05)),
    ("foot_r", "calf_r", (-0.12, 0.04, 0.12), (-0.12, -0.10, 0.05)),
    ("ball_l", "foot_l", (0.12, -0.10, 0.05), (0.12, -0.18, 0.03)),
    ("ball_r", "foot_r", (-0.12, -0.10, 0.05), (-0.12, -0.18, 0.03)),
    ("ik_foot_root", "root", (0, 0, 0.0), (0, 0, 0.05)),
    ("ik_foot_l", "ik_foot_root", (0.12, 0.04, 0.12), (0.12, 0.04, 0.18)),
    ("ik_foot_r", "ik_foot_root", (-0.12, 0.04, 0.12), (-0.12, 0.04, 0.18)),
    ("ik_hand_root", "root", (0, 0, 0.0), (0, 0, 0.05)),
    ("ik_hand_gun", "ik_hand_root", (0, 0.1, 1.0), (0, 0.15, 1.0)),
    ("ik_hand_l", "ik_hand_root", (0.68, 0.02, 0.98), (0.68, 0.02, 1.04)),
    ("ik_hand_r", "ik_hand_root", (-0.68, 0.02, 0.98), (-0.68, 0.02, 1.04)),
]

FINGERS = ("index", "middle", "ring", "pinky")
FINGER_X = {"index": 0.02, "middle": 0.007, "ring": -0.006, "pinky": -0.018}


def add_finger_bones(side: str, sign: int):
    hand = f"hand_{side}"
    hx, hy, hz = (sign * 0.68, 0.02, 0.98)
    for finger in FINGERS:
        dx = sign * FINGER_X[finger]
        meta = f"{finger}_metacarpal_{side}"
        BONES.append((meta, hand, (hx + dx, hy, hz), (hx + dx + sign * 0.03, hy - 0.01, hz - 0.01)))
        parent = meta
        for i in range(1, 4):
            name = f"{finger}_{i:02d}_{side}"
            start = (hx + dx + sign * (0.03 + 0.025 * (i - 1)), hy - 0.01 - 0.01 * (i - 1), hz - 0.01 * i)
            end = (hx + dx + sign * (0.03 + 0.025 * i), hy - 0.01 - 0.01 * i, hz - 0.01 * (i + 1))
            BONES.append((name, parent, start, end))
            parent = name
    parent = hand
    for i in range(1, 4):
        name = f"thumb_{i:02d}_{side}"
        start = (hx + sign * (0.01 * i), hy + 0.02, hz - 0.01 * i)
        end = (hx + sign * (0.01 * (i + 1)), hy + 0.03, hz - 0.015 * i)
        BONES.append((name, parent, start, end))
        parent = name


add_finger_bones("l", 1)
add_finger_bones("r", -1)


def create_armature():
    arm_data = bpy.data.armatures.new("main-rig")
    arm_obj = bpy.data.objects.new("main-rig", arm_data)
    bpy.context.collection.objects.link(arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode="EDIT")
    created = {}
    for name, parent, head, tail in BONES:
        bone = arm_data.edit_bones.new(name)
        bone.head = Vector(head)
        bone.tail = Vector(tail)
        bone.use_connect = False
        created[name] = bone
    for name, parent, *_ in BONES:
        if parent:
            created[name].parent = created[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm_obj


def _ring(bm, z, rx, ry, y_off=0.02, n=20):
    verts = []
    for i in range(n):
        a = (i / n) * 6.283185307179586
        verts.append(bm.verts.new((rx * __import__("math").cos(a), y_off + ry * __import__("math").sin(a), z)))
    return verts


def _bridge(bm, a, b):
    n = len(a)
    faces = []
    for i in range(n):
        j = (i + 1) % n
        faces.append(bm.faces.new((a[i], a[j], b[j], b[i])))
    return faces


def _cap(bm, ring, reverse=False):
    f = bm.faces.new(list(reversed(ring)) if reverse else ring)
    return f


def create_hoodie():
    import bmesh
    import math

    mesh = bpy.data.meshes.new("hoodie-navy-male")
    hoodie = bpy.data.objects.new("hoodie-navy-male", mesh)
    bpy.context.collection.objects.link(hoodie)
    bm = bmesh.new()

    # Torso rings (Z-up meters, slightly in front of spine). Hem -> chest -> shoulders -> neck.
    torso_profile = [
        (0.96, 0.22, 0.14),
        (1.04, 0.23, 0.145),
        (1.16, 0.24, 0.15),
        (1.28, 0.25, 0.155),
        (1.40, 0.24, 0.15),
        (1.48, 0.18, 0.13),
        (1.53, 0.11, 0.10),
    ]
    torso = [_ring(bm, z, rx, ry) for z, rx, ry in torso_profile]
    for a, b in zip(torso, torso[1:]):
        _bridge(bm, a, b)
    _cap(bm, torso[0], reverse=True)

    def sleeve(sign):
        # Shoulder at clavicle/upperarm, down toward elbow.
        rings = []
        steps = [
            (sign * 0.17, 0.04, 1.48, 0.10, 0.10),
            (sign * 0.26, 0.05, 1.40, 0.095, 0.095),
            (sign * 0.36, 0.07, 1.28, 0.09, 0.09),
            (sign * 0.46, 0.08, 1.16, 0.085, 0.085),
            (sign * 0.55, 0.07, 1.04, 0.08, 0.08),
        ]
        for x, y, z, rx, ry in steps:
            verts = []
            for i in range(12):
                a = (i / 12) * math.tau
                verts.append(bm.verts.new((x + rx * math.cos(a), y + ry * math.sin(a), z)))
            rings.append(verts)
        for a, b in zip(rings, rings[1:]):
            _bridge(bm, a, b)
        _cap(bm, rings[-1])
        return rings[0]

    sl = sleeve(1)
    sr = sleeve(-1)

    # Open hood: rings from neck going up and back, open toward +Y (face).
    hood = []
    for i, z in enumerate((1.54, 1.62, 1.70, 1.76)):
        back = -0.04 - i * 0.04
        rx = 0.12 + i * 0.015
        ry = 0.13 + i * 0.025
        hood.append(_ring(bm, z, rx, ry, y_off=back, n=20))
    _bridge(bm, torso[-1], hood[0])
    for a, b in zip(hood, hood[1:]):
        _bridge(bm, a, b)
    # cap the crown only
    _cap(bm, hood[-1])

    # Front pouch as a shallow bulge of extra faces, not a cube (cubes caused spikes).
    pouch_z = (1.10, 1.22)
    pouch_x = 0.12
    v00 = bm.verts.new((-pouch_x, 0.16, pouch_z[0]))
    v10 = bm.verts.new((pouch_x, 0.16, pouch_z[0]))
    v11 = bm.verts.new((pouch_x, 0.16, pouch_z[1]))
    v01 = bm.verts.new((-pouch_x, 0.16, pouch_z[1]))
    v00b = bm.verts.new((-pouch_x, 0.20, pouch_z[0] + 0.02))
    v10b = bm.verts.new((pouch_x, 0.20, pouch_z[0] + 0.02))
    v11b = bm.verts.new((pouch_x, 0.20, pouch_z[1] - 0.02))
    v01b = bm.verts.new((-pouch_x, 0.20, pouch_z[1] - 0.02))
    bm.faces.new((v00, v10, v10b, v00b))
    bm.faces.new((v10, v11, v11b, v10b))
    bm.faces.new((v11, v01, v01b, v11b))
    bm.faces.new((v01, v00, v00b, v01b))
    bm.faces.new((v00b, v10b, v11b, v01b))

    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.004)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()

    bpy.context.view_layer.objects.active = hoodie
    hoodie.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.mesh.subdivide(number_cuts=1)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.shade_smooth()

    mat = bpy.data.materials.new("MI-Upper")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (0.05, 0.10, 0.22, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.82
        spec = bsdf.inputs.get("Specular IOR Level") or bsdf.inputs.get("Specular")
        if spec:
            spec.default_value = 0.15
    hoodie.data.materials.append(mat)
    return hoodie


DEFORM_BONES = {
    "pelvis",
    "spine_01", "spine_02", "spine_03", "spine_04", "spine_05",
    "neck_01", "neck_02", "head",
    "clavicle_l", "clavicle_r",
    "upperarm_l", "upperarm_r",
    "upperarm_twist_01_l", "upperarm_twist_01_r",
    "upperarm_twist_02_l", "upperarm_twist_02_r",
    "lowerarm_l", "lowerarm_r",
}


def skin(hoodie, armature):
    hoodie.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    for vg in list(hoodie.vertex_groups):
        if vg.name not in DEFORM_BONES:
            hoodie.vertex_groups.remove(vg)
    bpy.context.view_layer.objects.active = hoodie
    try:
        bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    except Exception:
        pass


def export_fbx(armature, hoodie):
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    hoodie.select_set(True)
    bpy.context.view_layer.objects.active = armature
    ART.mkdir(parents=True, exist_ok=True)
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
        mesh_smooth_type="FACE",
        use_armature_deform_only=True,
        object_types={"ARMATURE", "MESH"},
    )


def pose_test(armature):
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="POSE")
    bone = armature.pose.bones.get("upperarm_l")
    if bone:
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = Euler((0, 0, radians(-25)), "XYZ")
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")


def main():
    clear_scene()
    armature = create_armature()
    hoodie = create_hoodie()
    skin(hoodie, armature)
    pose_test(armature)
    ART.mkdir(parents=True, exist_ok=True)
    REF_BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    bpy.ops.wm.save_as_mainfile(filepath=str(REF_BLEND))
    # rest pose for export
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.transforms_clear()
    bpy.ops.object.mode_set(mode="OBJECT")
    export_fbx(armature, hoodie)
    print("WROTE", BLEND)
    print("WROTE", FBX)
    print("BONES", len(armature.data.bones))
    print("VERTS", len(hoodie.data.vertices))


main()
