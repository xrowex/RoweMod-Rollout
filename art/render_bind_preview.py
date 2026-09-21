"""Render hoodie + stitched shirt at rest so we can see bind overlay."""
from __future__ import annotations

from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]
HOODIE = ROOT / "art" / "_ref" / "cue-hoodie" / "RollerSkate" / "Content" / "MainFolder" / "Character" / "upper" / "hoodie" / "hoodie-male.glb"
SHIRT = ROOT / "art" / "tshirt-baggy-male.gamebind.glb"
OUT = ROOT / "art" / "_preview" / "shirt_skinned.png"


def import_glb(path: Path, prefix: str) -> list:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path), bone_heuristic="TEMPERANCE")
    new_objs = [o for o in bpy.data.objects if o not in before]
    for obj in new_objs:
        obj.name = f"{prefix}_{obj.name}"
    return new_objs


def mesh_aabb(obj) -> None:
    pts = [obj.matrix_world @ v.co for v in obj.data.vertices]
    xs, ys, zs = zip(*[(p.x, p.y, p.z) for p in pts])
    print(
        obj.name,
        f"min=({min(xs):.4f},{min(ys):.4f},{min(zs):.4f})",
        f"max=({max(xs):.4f},{max(ys):.4f},{max(zs):.4f})",
    )


def main() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    hoodie_objs = import_glb(HOODIE, "hoodie")
    shirt_objs = import_glb(SHIRT, "shirt")
    for obj in hoodie_objs + shirt_objs:
        if obj.type == "ARMATURE":
            obj.hide_render = True
            obj.hide_viewport = True
        if obj.type == "MESH":
            for mod in obj.modifiers:
                if mod.type == "ARMATURE":
                    mod.show_render = False
                    mod.show_viewport = False
            mesh_aabb(obj)
            if obj.name.startswith("hoodie"):
                mat = bpy.data.materials.new("hoodie_preview")
                mat.use_nodes = True
                mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.15, 0.25, 0.7, 1)
                obj.data.materials.clear()
                obj.data.materials.append(mat)
            else:
                mat = bpy.data.materials.new("shirt_preview")
                mat.use_nodes = True
                bsdf = mat.node_tree.nodes["Principled BSDF"]
                bsdf.inputs["Base Color"].default_value = (0.92, 0.9, 0.85, 1)
                bsdf.inputs["Alpha"].default_value = 0.7
                mat.blend_method = "BLEND"
                obj.data.materials.clear()
                obj.data.materials.append(mat)

    for obj in hoodie_objs:
        if obj.type == "MESH":
            obj.hide_render = True
            obj.hide_viewport = True
    for obj in shirt_objs:
        if obj.type == "MESH":
            for mod in obj.modifiers:
                if mod.type == "ARMATURE":
                    mod.show_render = True
                    mod.show_viewport = True

    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    bpy.context.scene.collection.objects.link(cam)
    bpy.context.scene.camera = cam
    cam.location = (1.6, -1.8, 1.35)
    track = cam.constraints.new(type="TRACK_TO")
    target = bpy.data.objects.new("look", None)
    target.location = (0.0, 0.0, 1.15)
    bpy.context.scene.collection.objects.link(target)
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"

    light = bpy.data.objects.new("key", bpy.data.lights.new("key", "AREA"))
    light.data.energy = 400
    light.location = (1.5, -1.2, 2.2)
    bpy.context.scene.collection.objects.link(light)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 1280
    scene.render.filepath = str(OUT)
    scene.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(write_still=True)
    print("WROTE", OUT)


main()
