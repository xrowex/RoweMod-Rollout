"""Import the navy hoodie onto the game main-rig skeleton and MI-Upper slot."""
import unreal

FBX = r"C:\Users\xrowe\rolloutrowemod\art\hoodie-navy-male.fbx"
MESH_DIR = "/Game/MainFolder/Character/upper/hoodie/mod"
MESH_PATH = MESH_DIR + "/hoodie-navy-male"
MESH_NAME = "hoodie-navy-male"
MAIN_RIG_DIR = "/Game/MainFolder/Character/body/main-rig"
MAIN_RIG_PATH = MAIN_RIG_DIR + "/main-rig"
MI_UPPER_PATH = "/Game/MainFolder/Character/upper/MI-Upper"


def log(msg):
    unreal.log("[hoodie] " + msg)


def delete_if_exists(path):
    if unreal.EditorAssetLibrary.does_asset_exist(path):
        log("Deleting " + path)
        unreal.EditorAssetLibrary.delete_asset(path)


def fbx_task(dest_path, dest_name, skeleton=None, as_skeletal=True):
    task = unreal.AssetImportTask()
    task.filename = FBX
    task.destination_path = dest_path
    task.destination_name = dest_name
    task.replace_existing = True
    task.automated = True
    task.save = False
    options = unreal.FbxImportUI()
    options.import_mesh = True
    options.import_as_skeletal = as_skeletal
    options.import_animations = False
    options.import_materials = False
    options.import_textures = False
    options.create_physics_asset = False
    options.automated_import_should_detect_type = False
    options.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH
    options.original_import_type = unreal.FBXImportType.FBXIT_SKELETAL_MESH
    options.skeletal_mesh_import_data.set_editor_property("import_morph_targets", False)
    options.skeletal_mesh_import_data.set_editor_property("update_skeleton_reference_pose", True)
    options.skeletal_mesh_import_data.set_editor_property("import_meshes_in_bone_hierarchy", True)
    if skeleton is not None:
        options.skeleton = skeleton
    task.options = options
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])


def rename_asset(src, dst_dir, dst_name):
    if not unreal.EditorAssetLibrary.does_asset_exist(src):
        return None
    asset_tools = unreal.AssetToolsHelpers.get_asset_tools()
    job = unreal.AssetRenameData()
    job.asset = unreal.load_asset(src)
    job.new_package_path = dst_dir
    job.new_name = dst_name
    asset_tools.rename_assets([job])
    return unreal.load_asset(dst_dir + "/" + dst_name)


def ensure_main_rig():
    existing = unreal.load_asset(MAIN_RIG_PATH)
    if existing is not None and isinstance(existing, unreal.Skeleton):
        log("main-rig skeleton already exists")
        return existing
    if existing is not None and isinstance(existing, unreal.SkeletalMesh):
        skel = existing.get_editor_property("skeleton")
        if skel:
            log("main-rig is a mesh; using its skeleton " + skel.get_path_name())
            renamed = rename_asset(skel.get_path_name(), MAIN_RIG_DIR, "main-rig")
            return renamed or skel

    dummy_mesh = MAIN_RIG_DIR + "/skater-dummy"
    dummy_skel = MAIN_RIG_DIR + "/skater-dummy_Skeleton"
    delete_if_exists(dummy_mesh)
    delete_if_exists(dummy_skel)
    log("Importing dummy armature to create main-rig skeleton")
    fbx_task(MAIN_RIG_DIR, "skater-dummy")
    skel = rename_asset(dummy_skel, MAIN_RIG_DIR, "main-rig")
    delete_if_exists(dummy_mesh)
    if skel is None:
        skel = unreal.load_asset(MAIN_RIG_PATH)
    log("main-rig=" + (skel.get_path_name() if skel else "NONE"))
    return skel


def ensure_mi_upper():
    existing = unreal.load_asset(MI_UPPER_PATH)
    if existing is not None:
        return existing
    log("Creating dummy MI-Upper for cook-time reference")
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
    try:
        mesh.set_material(0, material)
        log("set_material slot0=" + material.get_path_name())
        return
    except Exception as err:
        log("set_material failed: " + str(err))
    try:
        mats = mesh.get_editor_property("materials")
        log("materials property=" + str(mats))
    except Exception as err:
        log("materials inspect failed: " + str(err))


def main():
    delete_if_exists(MESH_PATH)
    delete_if_exists(MESH_DIR + "/hoodie-navy-male_Skeleton")
    delete_if_exists(MESH_DIR + "/MI_HoodieNavy")
    delete_if_exists("/Game/MainFolder/Character/upper/hoodie/hoodie-male")
    delete_if_exists("/Game/MainFolder/Character/upper/hoodie/hoodie-male_Skeleton")

    skeleton = ensure_main_rig()
    mi_upper = ensure_mi_upper()

    log("Importing hoodie FBX onto main-rig")
    fbx_task(MESH_DIR, MESH_NAME, skeleton=skeleton)

    mesh = unreal.load_asset(MESH_PATH)
    if mesh is None or not isinstance(mesh, unreal.SkeletalMesh):
        unreal.log_error("Skeletal mesh import failed")
        return

    skel = mesh.get_editor_property("skeleton")
    log("mesh=" + mesh.get_path_name())
    log("mesh.skeleton=" + (skel.get_path_name() if skel else "NONE"))
    assign_material(mesh, mi_upper)

    companion = MESH_DIR + "/hoodie-navy-male_Skeleton"
    if unreal.EditorAssetLibrary.does_asset_exist(companion):
        if skel and "hoodie-navy-male_Skeleton" in skel.get_path_name():
            log("WARNING: import ignored main-rig; companion skeleton still assigned")
        else:
            delete_if_exists(companion)

    unreal.EditorAssetLibrary.save_directory("/Game/MainFolder", True, True)
    log("saved")


if __name__ == "__main__":
    main()
