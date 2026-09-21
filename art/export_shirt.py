"""Export the shirt on the game hoodie bind pose (no Blender tail aiming).

The visual kit in main-rig.blend aims tails so bones are readable. That
changes rest rotations. Unreal then stores inverse-binds that do not match
the live main-rig, and the mesh collapses into an hourglass in-game.

This script keeps the user's weights, rebinds them to hoodie-male.glb's
imported joints, and writes both FBX (backup) and glTF. Cook/preview import
the glTF via Interchange so units and inverse-binds stay game-correct.
"""
from __future__ import annotations

import os
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]


def _env_path(name: str) -> Path | None:
    value = os.environ.get(name)
    return Path(value) if value else None


def _first_existing(*paths: Path | str | None) -> Path | None:
    for path in paths:
        if not path:
            continue
        candidate = Path(path)
        if candidate.exists():
            return candidate
    return None


MESH_NAME = os.environ.get("ROWE_MESH", "tshirt-baggy-male")
BLEND = _env_path("ROWE_BLEND") or (ROOT / "art" / "rig" / "main-rig_shirt.blend")
GLB = _first_existing(
    _env_path("ROWE_BIND_GLB"),
    ROOT / "art" / "_ref" / "cue-hoodie" / "RollerSkate" / "Content" / "MainFolder" / "Character" / "upper" / "hoodie" / "hoodie-male.glb",
    ROOT / "dumps" / "game-clothing" / "meshes" / "RollerSkate" / "Content" / "MainFolder" / "Character" / "upper" / "hoodie" / "hoodie-male.glb",
)
FBX = _env_path("ROWE_OUT_FBX") or (ROOT / "art" / f"{MESH_NAME}.fbx")
OUT_GLB = _env_path("ROWE_OUT_GLB") or (ROOT / "art" / f"{MESH_NAME}.glb")

FOREARM_MAP = {
    "mixamorig:LeftForeArm": "lowerarm_l",
    "mixamorig:RightForeArm": "lowerarm_r",
}


def copy_group(obj: bpy.types.Object, src_name: str, dst_name: str) -> int:
    src = obj.vertex_groups.get(src_name)
    if src is None:
        return 0
    dst = obj.vertex_groups.get(dst_name) or obj.vertex_groups.new(name=dst_name)
    copied = 0
    for i, _vert in enumerate(obj.data.vertices):
        try:
            weight = src.weight(i)
        except RuntimeError:
            continue
        if weight <= 0.0:
            continue
        dst.add([i], weight, "ADD")
        copied += 1
    return copied


def import_game_armature() -> bpy.types.Object:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(GLB), bone_heuristic="TEMPERANCE")
    new_objs = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new_objs if o.type == "ARMATURE")
    for obj in new_objs:
        if obj.type == "MESH":
            bpy.data.objects.remove(obj, do_unlink=True)
    arm.name = "main-rig"
    if arm.data:
        arm.data.name = "main-rig"
    print("GAME_ARM bones", len(arm.data.bones))
    pelvis = arm.data.bones.get("pelvis")
    if pelvis:
        print(
            "GAME_PELVIS head",
            tuple(round(c, 3) for c in pelvis.head_local),
            "x",
            tuple(round(c, 3) for c in pelvis.matrix_local.col[0].xyz),
        )
    return arm


def main() -> None:
    if GLB is None or not GLB.exists():
        raise FileNotFoundError("Missing hoodie bind pose. Pull clothing first.")
    if not Path(BLEND).exists():
        raise FileNotFoundError(f"Missing {BLEND}")
    bpy.ops.wm.open_mainfile(filepath=str(BLEND))
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    print("BLEND_MESHES", [(o.name, len(o.data.vertices)) for o in meshes])
    if not meshes:
        raise RuntimeError("This Blender file has no clothing mesh yet. Model it, then Export this mesh.")
    mesh_obj = next(
        (
            o
            for o in meshes
            if "body" not in o.name.lower()
            and "hoodie" not in o.name.lower()
            and "male-body" not in o.name.lower()
        ),
        None,
    )
    if mesh_obj is None:
        mesh_obj = max(meshes, key=lambda o: len(o.data.vertices))
    print("EXPORT_MESH", mesh_obj.name, "verts", len(mesh_obj.data.vertices))
    old_arm = bpy.data.objects.get("main-rig")

    for src, dst in FOREARM_MAP.items():
        n = copy_group(mesh_obj, src, dst)
        print(f"FOREARM {src} -> {dst} verts={n}")

    mixamo = [g.name for g in mesh_obj.vertex_groups if g.name.startswith("mixamorig:")]
    for name in mixamo:
        mesh_obj.vertex_groups.remove(mesh_obj.vertex_groups[name])
    print("REMOVED_MIXAMO", len(mixamo))
    root = mesh_obj.vertex_groups.get("root")
    if root:
        mesh_obj.vertex_groups.remove(root)
        print("REMOVED_ROOT")

    for mod in list(mesh_obj.modifiers):
        if mod.type == "ARMATURE" and (mod.object is None or mod.object == old_arm):
            mesh_obj.modifiers.remove(mod)

    bpy.ops.object.select_all(action="DESELECT")
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_obj
    mesh_obj.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    print("MESH_AFTER_APPLY loc", tuple(round(c, 4) for c in mesh_obj.location), "scale", tuple(round(s, 4) for s in mesh_obj.scale))

    if old_arm:
        bpy.data.objects.remove(old_arm, do_unlink=True)

    arm = import_game_armature()
    mesh_obj.name = MESH_NAME
    mesh_obj.data.name = MESH_NAME
    mesh_obj.parent = arm
    mod = mesh_obj.modifiers.new(name="Armature", type="ARMATURE")
    mod.object = arm
    mod.use_vertex_groups = True

    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm
    FBX.parent.mkdir(parents=True, exist_ok=True)
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
        object_types={"ARMATURE", "MESH"},
        use_armature_deform_only=False,
        mesh_smooth_type="FACE",
    )
    print("WROTE", FBX)
    bpy.ops.export_scene.gltf(
        filepath=str(OUT_GLB),
        use_selection=True,
        export_format="GLB",
        export_animations=False,
        export_skins=True,
        export_apply=True,
        export_yup=True,
    )
    print("WROTE", OUT_GLB)
    print("VERTS", len(mesh_obj.data.vertices), "BONES", len(arm.data.bones))
    import runpy

    runpy.run_path(str(ROOT / "art" / "stitch_shirt_glb.py"), run_name="__main__")


main()
