"""List and save textures packed in the shirt blend."""
from __future__ import annotations

from pathlib import Path

import bpy

BLEND = Path(r"C:\Users\xrowe\rolloutrowemod\art\rig\main-rig_shirt.blend")
OUT = Path(r"C:\Users\xrowe\rolloutrowemod\art\textures\tshirt-baggy")


def main() -> None:
    bpy.ops.wm.open_mainfile(filepath=str(BLEND))
    OUT.mkdir(parents=True, exist_ok=True)
    print("OBJECTS")
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        print(" MESH", obj.name, "mats", [s.name if s else None for s in obj.data.materials])
        print(" UVS", [uv.name for uv in obj.data.uv_layers])
    print("MATERIALS")
    for mat in bpy.data.materials:
        print(" MAT", mat.name)
        if not mat.node_tree:
            continue
        for node in mat.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image:
                img = node.image
                dest = OUT / (img.name if Path(img.name).suffix else img.name + ".png")
                try:
                    img.filepath_raw = str(dest)
                    img.file_format = "PNG"
                    img.save()
                    print("  TEX", node.name, "->", dest, "size", tuple(img.size), "src", img.filepath)
                except Exception as err:
                    print("  TEX_FAIL", img.name, err)
    print("IMAGES")
    for img in bpy.data.images:
        print(" IMG", img.name, "size", tuple(img.size), "packed", img.packed_file is not None, "path", img.filepath)


main()
