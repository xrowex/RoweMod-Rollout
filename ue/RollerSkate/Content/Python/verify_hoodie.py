"""Save imported assets and log the hoodie skeleton reference."""
import unreal

MESH_PATH = "/Game/MainFolder/Character/upper/hoodie/mod/hoodie-navy-male"
SKELETON_PATH = "/Game/MainFolder/Character/body/main-rig/main-rig"


def main():
    mesh = unreal.load_asset(MESH_PATH)
    skeleton = unreal.load_asset(SKELETON_PATH)
    unreal.log("mesh=" + (mesh.get_path_name() if mesh else "NONE"))
    if mesh:
        skel = mesh.get_editor_property("skeleton")
        unreal.log("mesh.skeleton=" + (skel.get_path_name() if skel else "NONE"))
    unreal.log("skeleton_asset=" + (skeleton.get_path_name() if skeleton else "NONE"))
    unreal.EditorAssetLibrary.save_directory("/Game/MainFolder", True, True)
    for path in unreal.EditorAssetLibrary.list_assets("/Game/MainFolder", recursive=True):
        loaded = unreal.load_asset(path)
        kind = loaded.get_class().get_name() if loaded else "?"
        unreal.log("asset " + path + " " + kind)
    unreal.log("saved")


if __name__ == "__main__":
    main()
