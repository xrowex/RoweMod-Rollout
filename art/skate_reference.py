"""Visible fitting guides only; never part of a skate export.

Transforms are the stock main-rig foot rest transforms composed with
NewMainCharacter's Blade-L/R socket offsets, converted from UE cm to Blender m.
The default component mesh is standard-4x-wheel (including its wheels).
"""
import json
from pathlib import Path

import bpy
from mathutils import Matrix

MARKER = "rowe_reference_only"


def is_reference(obj):
    if obj.get(MARKER):
        return True
    def marked(collection, inherited=False):
        inherited = inherited or bool(collection.get(MARKER))
        if inherited and obj.name in collection.objects:
            return True
        return any(marked(child, inherited) for child in collection.children)
    return marked(bpy.context.scene.collection)


def add_reference(path, kind):
    if kind not in {"boots", "frames"}:
        raise ValueError("Reference kind must be boots or frames")
    selected = list(bpy.context.selected_objects)
    active = bpy.context.view_layer.objects.active
    before = set(bpy.data.objects)
    if kind == "frames":
        from skate_rig import import_boots
        import_boots(path)
    else:
        bpy.ops.import_scene.gltf(filepath=str(path))
    imported = set(bpy.data.objects) - before
    helpers = {bone.custom_shape for arm in imported if arm.type == "ARMATURE"
               for bone in arm.pose.bones if bone.custom_shape}
    meshes = [o for o in imported if o.type == "MESH" and len(o.data.vertices)
              and o not in helpers
              and not any(c.name.startswith("glTF_not_exported") for c in o.users_collection)]
    if not meshes:
        raise RuntimeError("Reference contains no mesh: " + str(path))
    collection = bpy.data.collections.new("REFERENCE - not exported")
    collection[MARKER] = True
    bpy.context.scene.collection.children.link(collection)
    arms = [o for o in imported if o.type == "ARMATURE"]
    retained = meshes + arms
    # Keep the game armature for frame alignment, but tag it as reference-only.
    for obj in retained:
        world = obj.matrix_world.copy()
        obj.parent = None
        obj.matrix_world = world
        for old in list(obj.users_collection):
            old.objects.unlink(obj)
        collection.objects.link(obj)
    for obj in imported - set(retained):
        bpy.data.objects.remove(obj, do_unlink=True)
    transforms = json.loads(Path(__file__).with_name("skate_reference_transforms.json").read_text())
    left, right = Matrix(transforms["l"]), Matrix(transforms["r"])
    for arm in arms:
        arm.matrix_world = left.inverted() @ arm.matrix_world
        arm.name = "REFERENCE main-rig"
    for obj in meshes:
        original = obj.matrix_world.copy()
        if kind == "boots":
            other = obj.copy()
            collection.objects.link(other)
            other.matrix_world = right @ original
            other.name = "REFERENCE right frame and wheels"
            obj.matrix_world = left @ original
            obj.name = "REFERENCE left frame and wheels"
        else:
            obj.matrix_world = left.inverted() @ original
            obj.name = "REFERENCE boots"
            if arms:
                world = obj.matrix_world.copy()
                obj.parent = arms[0]
                obj.matrix_world = world
    bpy.ops.object.select_all(action="DESELECT")
    for obj in collection.objects:
        obj[MARKER] = True
        obj.hide_select = True
        obj.hide_render = True
    for obj in selected:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = active
    print("ROWE_REFERENCE_ADDED", kind, len(collection.objects))
