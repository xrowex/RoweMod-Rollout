"""Frame the baggy shirt in the preview map so the viewport shows the tee."""
import unreal

MAP_PATH = "/Game/ModPreview/Preview"
SHIRT_PATH = "/Game/MainFolder/Character/upper/tshirt-baggy/tshirt-baggy-male"


def log(msg):
    unreal.log("[frame] " + msg)


def main():
    mesh = unreal.load_asset(SHIRT_PATH)
    if mesh:
        try:
            bounds = mesh.get_bounds()
            log("shirt origin=" + str(bounds.origin) + " extent=" + str(bounds.box_extent))
        except Exception as err:
            log("bounds failed " + str(err))
        skel = mesh.get_editor_property("skeleton")
        log("skeleton=" + (skel.get_path_name() if skel else "NONE"))

    level_sub = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
    if unreal.EditorAssetLibrary.does_asset_exist(MAP_PATH):
        level_sub.load_level(MAP_PATH)

    unreal.EditorLevelLibrary.set_level_viewport_camera_info(
        unreal.Vector(70.0, 90.0, 145.0),
        unreal.Rotator(-6.0, -128.0, 0.0),
    )
    try:
        level_sub.save_current_level()
    except Exception as err:
        log("save skipped " + str(err))
    log("framed")


if __name__ == "__main__":
    main()
