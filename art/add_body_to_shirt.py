"""Add male-body-01 to the shirt blend on the game hoodie bind, and save textures."""
from __future__ import annotations

from pathlib import Path

import bpy

ROOT = Path(r"C:\Users\xrowe\rolloutrowemod")
BLEND = ROOT / "art" / "rig" / "main-rig_shirt.blend"
HOODIE = ROOT / "art" / "_ref" / "cue-hoodie" / "RollerSkate" / "Content" / "MainFolder" / "Character" / "upper" / "hoodie" / "hoodie-male.glb"
BODY_GLB = ROOT / "art" / "_ref" / "male-body-01.glb"
TEX_DIR = ROOT / "art" / "textures" / "tshirt-baggy"


def save_textures() -> None:
    TEX_DIR.mkdir(parents=True, exist_ok=True)
    for img in bpy.data.images:
        if img.size[0] <= 8 or img.size[1] <= 8:
            continue
        name = Path(img.name).stem.lower()
        suffix = ".png"
        dest_name = Path(img.name).name
        if not Path(dest_name).suffix:
            dest_name += suffix
        dest = TEX_DIR / dest_name
        try:
            img.filepath_raw = str(dest)
            img.file_format = "PNG"
            img.save()
            print("TEX", dest, "size", tuple(img.size))
        except Exception as err:
            print("TEX_FAIL", img.name, err)
        mapped = None
        if any(k in name for k in ("albedo", "base", "color", "diff", "col")):
            mapped = TEX_DIR / "albedo.png"
        elif "normal" in name or "nrm" in name or name.endswith("_n"):
            mapped = TEX_DIR / "normal.png"
        elif "rough" in name or "rgh" in name:
            mapped = TEX_DIR / "roughness.png"
        if mapped is not None and dest.exists() and mapped != dest:
            mapped.write_bytes(dest.read_bytes())
            print("TEX_MAP", dest.name, "->", mapped.name)


def import_armature(path: Path) -> tuple[bpy.types.Object, list[bpy.types.Object]]:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path), bone_heuristic="TEMPERANCE")
    new_objs = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new_objs if o.type == "ARMATURE")
    meshes = [o for o in new_objs if o.type == "MESH"]
    return arm, meshes


def parent_keep_weights(mesh: bpy.types.Object, arm: bpy.types.Object) -> None:
    mesh.parent = arm
    for mod in list(mesh.modifiers):
        if mod.type == "ARMATURE":
            mesh.modifiers.remove(mod)
    mod = mesh.modifiers.new(name="Armature", type="ARMATURE")
    mod.object = arm
    mod.use_vertex_groups = True


def main() -> None:
    if not HOODIE.exists():
        raise FileNotFoundError(HOODIE)
    if not BODY_GLB.exists():
        raise FileNotFoundError(BODY_GLB)
    bpy.ops.wm.open_mainfile(filepath=str(BLEND))
    save_textures()

    shirt = None
    for obj in bpy.data.objects:
        if obj.type == "MESH" and "body" not in obj.name.lower():
            shirt = obj
            break
    if shirt is None:
        raise RuntimeError("No shirt mesh in blend")
    print("SHIRT", shirt.name, "verts", len(shirt.data.vertices))
    print("UVS", [uv.name for uv in shirt.data.uv_layers])
    print("MATS", [m.name if m else None for m in shirt.data.materials])

    old_arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    game_arm, hoodie_meshes = import_armature(HOODIE)
    for mesh in hoodie_meshes:
        bpy.data.objects.remove(mesh, do_unlink=True)
    game_arm.name = "main-rig"
    if game_arm.data:
        game_arm.data.name = "main-rig"

    bpy.ops.object.select_all(action="DESELECT")
    shirt.select_set(True)
    bpy.context.view_layer.objects.active = shirt
    shirt.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    parent_keep_weights(shirt, game_arm)
    shirt.name = "tshirt-baggy-male"
    if shirt.data:
        shirt.data.name = "tshirt-baggy-male"

    for arm in old_arms:
        if arm != game_arm:
            bpy.data.objects.remove(arm, do_unlink=True)

    body_arm, body_meshes = import_armature(BODY_GLB)
    for i, mesh in enumerate(body_meshes):
        mesh.name = "male-body-01" if i == 0 else f"male-body-01.{i:03d}"
        parent_keep_weights(mesh, game_arm)
        print("BODY", mesh.name, "verts", len(mesh.data.vertices))
    bpy.data.objects.remove(body_arm, do_unlink=True)

    bpy.ops.wm.save_mainfile(filepath=str(BLEND))
    print("SAVED", BLEND)

    bpy.ops.object.select_all(action="DESELECT")
    game_arm.select_set(True)
    for mesh in body_meshes:
        mesh.select_set(True)
    bpy.context.view_layer.objects.active = game_arm
    body_glb = ROOT / "art" / "male-body-01.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(body_glb),
        use_selection=True,
        export_format="GLB",
        export_animations=False,
        export_skins=True,
        export_apply=True,
        export_yup=True,
    )
    print("WROTE", body_glb)


main()
