"""Import the navy hoodie over the game hoodie-male path so an IoStore patch can replace it."""
import unreal

FBX = r"C:\Users\xrowe\rolloutrowemod\art\hoodie-navy-male.fbx"
MESH_DIR = "/Game/MainFolder/Character/upper/hoodie"
MESH_PATH = MESH_DIR + "/hoodie-male"
MESH_NAME = "hoodie-male"


def delete_if_exists(path):
    if unreal.EditorAssetLibrary.does_asset_exist(path):
        unreal.log("Deleting " + path)
        unreal.EditorAssetLibrary.delete_asset(path)


def import_skeletal():
    task = unreal.AssetImportTask()
    task.filename = FBX
    task.destination_path = MESH_DIR
    task.destination_name = MESH_NAME
    task.replace_existing = True
    task.automated = True
    task.save = False
    options = unreal.FbxImportUI()
    options.import_mesh = True
    options.import_as_skeletal = True
    options.import_animations = False
    options.import_materials = False
    options.import_textures = False
    options.create_physics_asset = False
    options.automated_import_should_detect_type = False
    options.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH
    options.original_import_type = unreal.FBXImportType.FBXIT_SKELETAL_MESH
    task.options = options
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])


def main():
    delete_if_exists(MESH_PATH)
    delete_if_exists(MESH_DIR + "/hoodie-male_Skeleton")
    delete_if_exists("/Game/MainFolder/Character/upper/hoodie/mod/hoodie-navy-male")
    delete_if_exists("/Game/MainFolder/Character/upper/hoodie/mod/hoodie-navy-male_Skeleton")
    unreal.log("Importing replacement hoodie-male")
    import_skeletal()
    mesh = unreal.load_asset(MESH_PATH)
    if mesh is None:
        unreal.log_error("import failed")
        return
    skeleton = mesh.get_editor_property("skeleton")
    unreal.EditorAssetLibrary.save_directory("/Game/MainFolder", True, True)
    unreal.log("Done. mesh=" + mesh.get_path_name())
    unreal.log("Done. mesh.skeleton=" + (skeleton.get_path_name() if skeleton else "NONE"))


if __name__ == "__main__":
    main()
