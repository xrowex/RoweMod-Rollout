"""Import the shirt as glTF so Interchange keeps the game inverse-binds.

CUE4Parse writes live IBMs into hoodie-male.glb. Blender export rewrites
those matrices (82/87 bones drifted). Import the stitched gamebind glTF
instead: shirt verts + hoodie nodes/IBMs. FBX import overwrites rest pose.
"""
import json
from pathlib import Path

import unreal

REPO = Path(__file__).resolve().parents[4]


def _load_target():
    path = REPO / "dumps" / "cook-target.json"
    if not path.exists():
        return {}
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return {}


_TARGET = _load_target()
IMPORT_KIND = str(_TARGET.get("importKind") or "skeletal").lower()
SHIRT_FBX = str(REPO / "art" / "tshirt-baggy-male.fbx")
SHIRT_GLB = str(_TARGET.get("gamebind") or (REPO / "art" / "tshirt-baggy-male.gamebind.glb"))
HOODIE_GLB = str(
    REPO
    / "art"
    / "_ref"
    / "cue-hoodie"
    / "RollerSkate"
    / "Content"
    / "MainFolder"
    / "Character"
    / "upper"
    / "hoodie"
    / "hoodie-male.glb"
)
MESH_DIR = str(_TARGET.get("meshDir") or "/Game/MainFolder/Character/upper/tshirt-baggy")
MESH_NAME = str(_TARGET.get("meshName") or "tshirt-baggy-male")
MESH_PATH = MESH_DIR + "/" + MESH_NAME
MAIN_RIG_DIR = "/Game/MainFolder/Character/body/main-rig"
MAIN_RIG_PATH = MAIN_RIG_DIR + "/main-rig"
MI_UPPER_PATH = "/Game/MainFolder/Character/upper/MI-Upper"
HOODIE_DIR = "/Game/ModPreview/hoodie-ref"
UNIFORM_SCALE = 100.0


def log(msg):
    unreal.log("[shirt] " + msg)


def delete_if_exists(path):
    if unreal.EditorAssetLibrary.does_asset_exist(path):
        log("Deleting " + path)
        unreal.EditorAssetLibrary.delete_asset(path)


def log_bounds(label, mesh):
    if mesh is None:
        log(label + " missing")
        return
    try:
        bounds = mesh.get_bounds()
        log(
            label
            + " origin="
            + str(bounds.origin)
            + " extent="
            + str(bounds.box_extent)
        )
    except Exception as err:
        log(label + " bounds failed " + str(err))


def list_new_assets(folder):
    return list(unreal.EditorAssetLibrary.list_assets(folder, recursive=True, include_folder=False))


def import_gltf(filepath, dest_dir, label):
    unreal.EditorAssetLibrary.make_directory(dest_dir)
    imported = False
    try:
        mgr = unreal.InterchangeManager.get_interchange_manager_scripted()
        src = mgr.create_source_data(filepath)
        params = unreal.ImportAssetParameters()
        try:
            params.set_editor_property("replace_existing", True)
        except Exception:
            pass
        log("Interchange import " + label)
        mgr.import_asset(dest_dir, src, params)
        imported = True
    except Exception as err:
        log("Interchange failed: " + str(err))
    if not imported:
        task = unreal.AssetImportTask()
        task.filename = filepath
        task.destination_path = dest_dir
        task.replace_existing = True
        task.automated = True
        task.save = False
        unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])

    mesh = None
    skeleton = None
    for path in list_new_assets(dest_dir):
        asset = unreal.load_asset(path)
        if isinstance(asset, unreal.SkeletalMesh) and mesh is None:
            mesh = asset
        if isinstance(asset, unreal.Skeleton) and skeleton is None:
            skeleton = asset
    if mesh and skeleton is None:
        skeleton = mesh.get_editor_property("skeleton")
    log_bounds(label, mesh)
    return mesh, skeleton


def import_hoodie_gltf():
    return import_gltf(HOODIE_GLB, HOODIE_DIR, "hoodie")


def rename_asset(src_path, dst_dir, dst_name):
    if not src_path or not unreal.EditorAssetLibrary.does_asset_exist(src_path):
        return None
    asset_tools = unreal.AssetToolsHelpers.get_asset_tools()
    job = unreal.AssetRenameData()
    job.asset = unreal.load_asset(src_path)
    job.new_package_path = dst_dir
    job.new_name = dst_name
    asset_tools.rename_assets([job])
    return unreal.load_asset(dst_dir + "/" + dst_name)


def configure_fbx(options, skeleton=None, uniform_scale=UNIFORM_SCALE):
    options.import_mesh = True
    options.import_as_skeletal = True
    options.import_animations = False
    options.import_materials = False
    options.import_textures = False
    options.create_physics_asset = False
    options.automated_import_should_detect_type = False
    options.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH
    options.original_import_type = unreal.FBXImportType.FBXIT_SKELETAL_MESH
    data = options.skeletal_mesh_import_data
    data.set_editor_property("import_morph_targets", False)
    data.set_editor_property("update_skeleton_reference_pose", False)
    data.set_editor_property("import_meshes_in_bone_hierarchy", False)
    data.set_editor_property("import_uniform_scale", uniform_scale)
    for prop, value in (
        ("convert_scene", True),
        ("convert_scene_unit", False),
        ("force_front_x_axis", False),
        ("transform_vertex_to_absolute", True),
        ("use_t0_as_ref_pose", True),
    ):
        try:
            data.set_editor_property(prop, value)
        except Exception as err:
            log("skip " + prop + ": " + str(err))
    if skeleton is not None:
        options.skeleton = skeleton


def fbx_task(filepath, dest_path, dest_name, skeleton=None, uniform_scale=UNIFORM_SCALE):
    task = unreal.AssetImportTask()
    task.filename = filepath
    task.destination_path = dest_path
    task.destination_name = dest_name
    task.replace_existing = True
    task.automated = True
    task.save = False
    options = unreal.FbxImportUI()
    configure_fbx(options, skeleton, uniform_scale)
    task.options = options
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])


def ensure_mi_upper():
    existing = unreal.load_asset(MI_UPPER_PATH)
    if existing is not None:
        return existing
    factory = unreal.MaterialFactoryNew()
    mat = unreal.AssetToolsHelpers.get_asset_tools().create_asset(
        "MI-Upper",
        "/Game/MainFolder/Character/upper",
        unreal.Material,
        factory,
    )
    if mat:
        try:
            mat.set_editor_property("two_sided", True)
        except Exception:
            pass
        unreal.EditorAssetLibrary.save_asset(MI_UPPER_PATH)
    return mat


def assign_material(mesh, material):
    if mesh is None or material is None:
        return
    slot = unreal.SkeletalMaterial()
    slot.set_editor_property("material_interface", material)
    slot.set_editor_property("material_slot_name", "MI-Upper")
    try:
        slot.set_editor_property("imported_material_slot_name", "MI-Upper")
    except Exception:
        pass
    mesh.set_editor_property("materials", [slot])
    log("assigned materials=[MI-Upper]")


def install_skeleton(hoodie_skel):
    if hoodie_skel is None:
        return None
    path = hoodie_skel.get_path_name().split(".")[0]
    if path == MAIN_RIG_PATH:
        return hoodie_skel
    delete_if_exists(MAIN_RIG_PATH)
    renamed = rename_asset(path, MAIN_RIG_DIR, "main-rig")
    return renamed or unreal.load_asset(MAIN_RIG_PATH)


def main():
    if IMPORT_KIND in ("static", "skeletal-skate"):
        log("skip shirt import (skate cook)")
        return
    log("target mesh=" + MESH_NAME + " dir=" + MESH_DIR + " glb=" + SHIRT_GLB)
    delete_if_exists(MESH_PATH)
    delete_if_exists(MESH_DIR + "/" + MESH_NAME + "_Skeleton")
    delete_if_exists("/Game/MainFolder/Character/upper/hoodie/mod/hoodie-navy-male")
    delete_if_exists("/Game/MainFolder/Character/upper/hoodie/mod/hoodie-navy-male_Skeleton")
    delete_if_exists(MAIN_RIG_PATH)

    mi_upper = ensure_mi_upper()
    mesh, skel = import_gltf(SHIRT_GLB, MESH_DIR, "shirt")
    if mesh is None or not isinstance(mesh, unreal.SkeletalMesh):
        unreal.log_error("Skeletal mesh import failed")
        return

    if mesh.get_path_name().split(".")[0] != MESH_PATH:
        renamed_mesh = rename_asset(mesh.get_path_name().split(".")[0], MESH_DIR, MESH_NAME)
        if renamed_mesh:
            mesh = renamed_mesh

    skel = mesh.get_editor_property("skeleton") or skel
    install_skeleton(skel)
    skel = mesh.get_editor_property("skeleton")

    log("mesh=" + mesh.get_path_name())
    log("mesh.skeleton=" + (skel.get_path_name() if skel else "NONE"))
    log_bounds("shirt", mesh)
    assign_material(mesh, mi_upper)

    unreal.EditorAssetLibrary.save_asset(MESH_PATH)
    if unreal.EditorAssetLibrary.does_asset_exist(MAIN_RIG_PATH):
        unreal.EditorAssetLibrary.save_asset(MAIN_RIG_PATH)
    unreal.EditorAssetLibrary.save_directory("/Game/MainFolder", True, True)
    log("saved")


if __name__ == "__main__":
    main()
