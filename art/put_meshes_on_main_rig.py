"""Put the shirt (and male body) onto art/rig/main-rig.blend."""
from __future__ import annotations

from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]
KIT = ROOT / "art" / "rig" / "main-rig.blend"
SHIRT_BLEND = ROOT / "art" / "rig" / "main-rig_shirt.blend"
BODY_GLB = ROOT / "art" / "_ref" / "male-body-01.glb"


def kit_armature() -> bpy.types.Object:
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    arm.name = "main-rig"
    if arm.data:
        arm.data.name = "main-rig"
    return arm


def bind_to_arm(mesh: bpy.types.Object, arm: bpy.types.Object) -> None:
    mesh.parent = arm
    mesh.parent_type = "OBJECT"
    for mod in list(mesh.modifiers):
        if mod.type == "ARMATURE":
            mesh.modifiers.remove(mod)
    mod = mesh.modifiers.new(name="Armature", type="ARMATURE")
    mod.object = arm
    mod.use_vertex_groups = True


def append_shirt_meshes() -> list[bpy.types.Object]:
    with bpy.data.libraries.load(str(SHIRT_BLEND), link=False) as (src, dst):
        dst.objects = [n for n in src.objects]
    added = []
    for obj in list(bpy.data.objects):
        if obj.users_collection:
            continue
        if obj.type == "ARMATURE":
            bpy.data.objects.remove(obj, do_unlink=True)
            continue
        if obj.type != "MESH":
            continue
        if "body" in obj.name.lower() and "shirt" not in obj.name.lower() and "tshirt" not in obj.name.lower():
            bpy.data.objects.remove(obj, do_unlink=True)
            continue
        bpy.context.scene.collection.objects.link(obj)
        obj.name = "tshirt-baggy-male"
        if obj.data:
            obj.data.name = "tshirt-baggy-male"
        added.append(obj)
        print("APPENDED_SHIRT", obj.name, "verts", len(obj.data.vertices), "groups", len(obj.vertex_groups))
    return added


def import_body() -> list[bpy.types.Object]:
    if not BODY_GLB.exists():
        print("NO_BODY", BODY_GLB)
        return []
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(BODY_GLB), bone_heuristic="TEMPERANCE")
    new_objs = [o for o in bpy.data.objects if o not in before]
    meshes = []
    for obj in new_objs:
        if obj.type == "ARMATURE":
            bpy.data.objects.remove(obj, do_unlink=True)
            continue
        if obj.type != "MESH":
            continue
        obj.name = "male-body-01"
        if obj.data:
            obj.data.name = "male-body-01"
        meshes.append(obj)
        print("IMPORTED_BODY", obj.name, "verts", len(obj.data.vertices))
    return meshes


def main() -> None:
    if not KIT.exists():
        raise FileNotFoundError(KIT)
    bpy.ops.wm.open_mainfile(filepath=str(KIT))
    for obj in list(bpy.data.objects):
        if obj.type == "MESH":
            print("KIT_HAD_MESH", obj.name)
            bpy.data.objects.remove(obj, do_unlink=True)
    arm = kit_armature()
    print("KIT_ARM", arm.name, "bones", len(arm.data.bones))
    for mesh in append_shirt_meshes() + import_body():
        bind_to_arm(mesh, arm)
    bpy.ops.wm.save_as_mainfile(filepath=str(KIT))
    print("WROTE", KIT)
    for obj in bpy.data.objects:
        print("FINAL", obj.type, obj.name, "parent", obj.parent.name if obj.parent else None)


main()
