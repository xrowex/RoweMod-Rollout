"""Build a lit preview map with the male body and baggy shirt on main-rig."""
import os
from pathlib import Path

import unreal

import import_shirt

REPO = Path(__file__).resolve().parents[4]
BODY_FBX = str(REPO / "art" / "male-body-01.fbx")
BODY_GLB = str(REPO / "art" / "male-body-01.glb")
TEX_DIR = str(REPO / "art" / "textures" / "tshirt-baggy")
BODY_DIR = "/Game/ModPreview"
BODY_PATH = BODY_DIR + "/male-body-01"
SHIRT_DIR = "/Game/MainFolder/Character/upper/tshirt-baggy"
SHIRT_PATH = SHIRT_DIR + "/tshirt-baggy-male"
MAP_PATH = "/Game/ModPreview/Preview"
SKIN_PATH = "/Game/ModPreview/M_PreviewSkin"
CLOTH_PATH = SHIRT_DIR + "/M_tshirt-baggy"
ALBEDO_PATH = SHIRT_DIR + "/T_tshirt-baggy-albedo"
NORMAL_PATH = SHIRT_DIR + "/T_tshirt-baggy-normal"
ROUGH_PATH = SHIRT_DIR + "/T_tshirt-baggy-roughness"


def log(msg):
    unreal.log("[preview] " + msg)


def import_texture(filepath, dest_path, dest_name, normal=False, srgb=True, mask=False):
    if not os.path.isfile(filepath):
        return None
    full = dest_path + "/" + dest_name
    task = unreal.AssetImportTask()
    task.filename = filepath
    task.destination_path = dest_path
    task.destination_name = dest_name
    task.replace_existing = True
    task.automated = True
    task.save = True
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])
    tex = unreal.load_asset(full)
    if tex is not None:
        try:
            tex.set_editor_property("srgb", srgb)
            if normal:
                tex.set_editor_property(
                    "compression_settings", unreal.TextureCompressionSettings.TC_NORMALMAP
                )
            elif mask:
                tex.set_editor_property(
                    "compression_settings", unreal.TextureCompressionSettings.TC_MASKS
                )
            unreal.EditorAssetLibrary.save_asset(full)
        except Exception as err:
            log("tex flags failed: " + str(err))
    log("texture " + (tex.get_path_name() if tex else "FAILED " + filepath))
    return tex


def add_texture_sample(mat, tex, x, y, sampler):
    sample = unreal.MaterialEditingLibrary.create_material_expression(
        mat, unreal.MaterialExpressionTextureSample, x, y
    )
    sample.set_editor_property("texture", tex)
    sample.set_editor_property("sampler_type", sampler)
    log("sample " + tex.get_name() + " " + str(sampler))
    return sample


def find_tex_file(kind):
    if not os.path.isdir(TEX_DIR):
        return None
    preferred = {
        "albedo": ("albedo.jpg", "albedo.jpeg", "albedo.png"),
        "normal": ("normal.jpg", "normal.jpeg", "normal.png"),
        "roughness": ("mask.jpg", "mask.png", "roughness.jpg", "roughness.png"),
    }[kind]
    for name in preferred:
        path = os.path.join(TEX_DIR, name)
        if os.path.isfile(path):
            return path
    keys = {
        "albedo": ("albedo", "basecolor", "basemap"),
        "normal": ("normal",),
        "roughness": ("mask", "rough"),
    }[kind]
    for name in os.listdir(TEX_DIR):
        lower = name.lower()
        if not lower.endswith((".png", ".tga", ".jpg", ".jpeg")):
            continue
        if any(k in lower for k in keys):
            return os.path.join(TEX_DIR, name)
    return None


def make_textured_material():
    unreal.EditorAssetLibrary.make_directory(SHIRT_DIR)
    albedo = import_texture(find_tex_file("albedo") or "", SHIRT_DIR, "T_tshirt-baggy-albedo")
    normal = import_texture(
        find_tex_file("normal") or "", SHIRT_DIR, "T_tshirt-baggy-normal", normal=True, srgb=False
    )
    rough = import_texture(
        find_tex_file("roughness") or "", SHIRT_DIR, "T_tshirt-baggy-mask", srgb=False, mask=True
    )
    if unreal.EditorAssetLibrary.does_asset_exist(CLOTH_PATH):
        unreal.EditorAssetLibrary.delete_asset(CLOTH_PATH)
    mat = unreal.AssetToolsHelpers.get_asset_tools().create_asset(
        "M_tshirt-baggy", SHIRT_DIR, unreal.Material, unreal.MaterialFactoryNew()
    )
    try:
        mat.set_editor_property("two_sided", True)
    except Exception:
        pass
    try:
        mat.set_editor_property("used_with_skeletal_mesh", True)
    except Exception:
        pass
    try:
        unreal.MaterialEditingLibrary.set_material_usage(mat, unreal.MaterialUsage.MUS_SKELETAL_MESH)
    except Exception:
        pass
    y = 0
    if albedo:
        sample = add_texture_sample(
            mat, albedo, -380, y, unreal.MaterialSamplerType.SAMPLERTYPE_COLOR
        )
        unreal.MaterialEditingLibrary.connect_material_property(
            sample, "RGB", unreal.MaterialProperty.MP_BASE_COLOR
        )
        try:
            unreal.MaterialEditingLibrary.connect_material_property(
                sample, "RGB", unreal.MaterialProperty.MP_EMISSIVE_COLOR
            )
        except Exception:
            pass
        y += 220
    else:
        const = unreal.MaterialEditingLibrary.create_material_expression(
            mat, unreal.MaterialExpressionConstant3Vector, -380, 0
        )
        const.set_editor_property("constant", unreal.LinearColor(0.92, 0.92, 0.90, 1.0))
        unreal.MaterialEditingLibrary.connect_material_property(
            const, "", unreal.MaterialProperty.MP_BASE_COLOR
        )
    if normal:
        sample = add_texture_sample(
            mat, normal, -380, y, unreal.MaterialSamplerType.SAMPLERTYPE_NORMAL
        )
        unreal.MaterialEditingLibrary.connect_material_property(
            sample, "RGB", unreal.MaterialProperty.MP_NORMAL
        )
        y += 220
    if rough:
        sample = add_texture_sample(
            mat, rough, -380, y, unreal.MaterialSamplerType.SAMPLERTYPE_MASKS
        )
        unreal.MaterialEditingLibrary.connect_material_property(
            sample, "G", unreal.MaterialProperty.MP_ROUGHNESS
        )
    unreal.MaterialEditingLibrary.recompile_material(mat)
    unreal.EditorAssetLibrary.save_asset(CLOTH_PATH)
    log("material " + CLOTH_PATH)
    return mat


def make_color_material(path, name, folder, color, two_sided=False):
    if unreal.EditorAssetLibrary.does_asset_exist(path):
        try:
            unreal.EditorAssetLibrary.delete_asset(path)
        except Exception as err:
            log("reuse " + path + " " + str(err))
            existing = unreal.load_asset(path)
            if existing:
                return existing
    mat = unreal.AssetToolsHelpers.get_asset_tools().create_asset(
        name, folder, unreal.Material, unreal.MaterialFactoryNew()
    )
    if mat is None:
        return unreal.load_asset(path)
    const = unreal.MaterialEditingLibrary.create_material_expression(
        mat, unreal.MaterialExpressionConstant3Vector, -350, 0
    )
    const.set_editor_property("constant", color)
    unreal.MaterialEditingLibrary.connect_material_property(
        const, "", unreal.MaterialProperty.MP_BASE_COLOR
    )
    if two_sided:
        try:
            mat.set_editor_property("two_sided", True)
        except Exception:
            pass
    unreal.MaterialEditingLibrary.recompile_material(mat)
    unreal.EditorAssetLibrary.save_asset(path)
    log("material " + path)
    return mat


def import_body(skeleton):
    if unreal.EditorAssetLibrary.does_asset_exist(BODY_PATH):
        unreal.EditorAssetLibrary.delete_asset(BODY_PATH)
    companion = BODY_DIR + "/male-body-01_Skeleton"
    if unreal.EditorAssetLibrary.does_asset_exist(companion):
        unreal.EditorAssetLibrary.delete_asset(companion)

    mesh = None
    if mesh is None:
        task = unreal.AssetImportTask()
        task.filename = BODY_FBX
        task.destination_path = BODY_DIR
        task.destination_name = "male-body-01"
        task.replace_existing = True
        task.automated = True
        task.save = False
        options = unreal.FbxImportUI()
        import_shirt.configure_fbx(options, skeleton=None)
        task.options = options
        unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])
        mesh = unreal.load_asset(BODY_PATH)
    if mesh is None:
        log("WARNING body import failed")
        return None
    if unreal.EditorAssetLibrary.does_asset_exist(companion):
        skel = mesh.get_editor_property("skeleton")
        if skel and "male-body-01_Skeleton" in skel.get_path_name():
            log("WARNING body created its own skeleton")
        else:
            unreal.EditorAssetLibrary.delete_asset(companion)
    log("body=" + mesh.get_path_name())
    log("body.skeleton=" + (mesh.get_editor_property("skeleton").get_path_name() if mesh.get_editor_property("skeleton") else "NONE"))
    return mesh


def spawn_skel(mesh, location, label, material=None):
    actor = unreal.EditorLevelLibrary.spawn_actor_from_class(unreal.SkeletalMeshActor, location)
    actor.set_actor_label(label)
    comp = actor.skeletal_mesh_component
    try:
        comp.set_skeletal_mesh_asset(mesh)
    except Exception:
        comp.set_skeletal_mesh(mesh)
    if material is not None:
        try:
            comp.set_material(0, material)
        except Exception as err:
            log("override material failed: " + str(err))
    return actor


def build_map(body, shirt, skin=None, cloth=None):
    level_sub = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
    if unreal.EditorAssetLibrary.does_asset_exist(MAP_PATH):
        level_sub.load_level(MAP_PATH)
        for actor in list(unreal.EditorLevelLibrary.get_all_level_actors()):
            try:
                unreal.EditorLevelLibrary.destroy_actor(actor)
            except Exception:
                pass
    else:
        level_sub.new_level(MAP_PATH)

    origin = unreal.Vector(0.0, 0.0, 0.0)
    if body:
        spawn_skel(body, origin, "male-body-01", skin)
    if shirt:
        spawn_skel(shirt, origin, "tshirt-baggy-male", cloth)

    floor_mesh = unreal.load_asset("/Engine/BasicShapes/Cube")
    if floor_mesh:
        floor = unreal.EditorLevelLibrary.spawn_actor_from_class(
            unreal.StaticMeshActor, unreal.Vector(0.0, 0.0, -10.0)
        )
        floor.set_actor_label("Floor")
        floor.static_mesh_component.set_static_mesh(floor_mesh)
        floor.set_actor_scale3d(unreal.Vector(25.0, 25.0, 0.2))

    sun = unreal.EditorLevelLibrary.spawn_actor_from_class(
        unreal.DirectionalLight, unreal.Vector(200.0, -180.0, 400.0)
    )
    sun.set_actor_label("Sun")
    sun.set_actor_rotation(unreal.Rotator(-35.0, -40.0, 0.0), False)

    sky = unreal.EditorLevelLibrary.spawn_actor_from_class(
        unreal.SkyLight, unreal.Vector(0.0, 0.0, 300.0)
    )
    sky.set_actor_label("Sky")

    fill = unreal.EditorLevelLibrary.spawn_actor_from_class(
        unreal.PointLight, unreal.Vector(-120.0, 160.0, 180.0)
    )
    fill.set_actor_label("Fill")

    try:
        unreal.EditorLevelLibrary.set_level_viewport_camera_info(
            unreal.Vector(140.0, 25.0, 128.0),
            unreal.Rotator(-6.0, 190.0, 0.0),
        )
    except Exception as err:
        log("viewport camera skipped: " + str(err))

    level_sub.save_current_level()
    log("map " + MAP_PATH)


def main():
    unreal.EditorAssetLibrary.make_directory(BODY_DIR)
    import_shirt.main()

    shirt = unreal.load_asset(SHIRT_PATH)
    skeleton = unreal.load_asset(import_shirt.MAIN_RIG_PATH)
    body = import_body(skeleton)

    skin = make_color_material(
        SKIN_PATH, "M_PreviewSkin", BODY_DIR, unreal.LinearColor(0.82, 0.62, 0.50, 1.0)
    )
    cloth = make_textured_material()
    if shirt and cloth:
        slot = unreal.SkeletalMaterial()
        slot.set_editor_property("material_interface", cloth)
        slot.set_editor_property("material_slot_name", "MI-Upper")
        shirt.set_editor_property("materials", [slot])
        unreal.EditorAssetLibrary.save_asset(SHIRT_PATH)
    if body:
        unreal.EditorAssetLibrary.save_asset(BODY_PATH)
    unreal.EditorAssetLibrary.save_directory("/Game/MainFolder", True, True)
    unreal.EditorAssetLibrary.save_directory(BODY_DIR, True, True)

    build_map(body, shirt, skin, cloth)
    log("done")


if __name__ == "__main__":
    main()
