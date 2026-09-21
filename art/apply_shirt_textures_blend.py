"""Assign DEATH ROWE maps on the shirt in the Blender rig files."""
from __future__ import annotations

from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]
TEX = ROOT / "art" / "textures" / "tshirt-baggy"
BLENDS = [
    ROOT / "art" / "rig" / "main-rig_shirt.blend",
    ROOT / "art" / "rig" / "main-rig.blend",
]
ALBEDO = TEX / "albedo.jpg"
NORMAL = TEX / "normal.jpg"
MASK = TEX / "mask.jpg"


def load_image(path: Path, colorspace: str) -> bpy.types.Image:
    img = bpy.data.images.load(str(path), check_existing=True)
    img.colorspace_settings.name = colorspace
    return img


def make_material() -> bpy.types.Material:
    mat = bpy.data.materials.get("M_tshirt-baggy") or bpy.data.materials.new("M_tshirt-baggy")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (400, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (100, 0)
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

    if ALBEDO.exists():
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.location = (-400, 200)
        tex.image = load_image(ALBEDO, "sRGB")
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        print("ALBEDO", ALBEDO)
    if NORMAL.exists():
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.location = (-400, -200)
        tex.image = load_image(NORMAL, "Non-Color")
        nrm = nt.nodes.new("ShaderNodeNormalMap")
        nrm.location = (-120, -200)
        nt.links.new(tex.outputs["Color"], nrm.inputs["Color"])
        nt.links.new(nrm.outputs["Normal"], bsdf.inputs["Normal"])
        print("NORMAL", NORMAL)
    if MASK.exists():
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.location = (-400, 0)
        tex.image = load_image(MASK, "Non-Color")
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        sep.location = (-120, 0)
        nt.links.new(tex.outputs["Color"], sep.inputs["Color"])
        nt.links.new(sep.outputs["Green"], bsdf.inputs["Roughness"])
        print("MASK", MASK)
    return mat


def apply_to_shirt(mat: bpy.types.Material) -> None:
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        if "body" in obj.name.lower() or "hoodie" in obj.name.lower():
            continue
        print("ASSIGN", obj.name, "uvs", [uv.name for uv in obj.data.uv_layers])
        if obj.data.uv_layers:
            obj.data.uv_layers[0].active_render = True
        obj.data.materials.clear()
        obj.data.materials.append(mat)


def shade_preview() -> None:
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                for space in area.spaces:
                    if space.type == "VIEW_3D":
                        space.shading.type = "MATERIAL"


def process(path: Path) -> None:
    print("OPEN", path)
    bpy.ops.wm.open_mainfile(filepath=str(path))
    mat = make_material()
    apply_to_shirt(mat)
    shade_preview()
    bpy.ops.wm.save_mainfile()
    print("SAVED", path)


def main() -> None:
    for path in BLENDS:
        if path.exists():
            process(path)


main()
