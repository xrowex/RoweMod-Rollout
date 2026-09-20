"""Import POSI's three shirt maps and assign M_tshirt-baggy on the preview shirt."""
import setup_preview
import unreal


def main():
    cloth = setup_preview.make_textured_material()
    shirt = unreal.load_asset(setup_preview.SHIRT_PATH)
    if shirt and cloth:
        try:
            shirt.set_editor_property("used_with_skeletal_mesh", True)
        except Exception:
            pass
        slot = unreal.SkeletalMaterial()
        slot.set_editor_property("material_interface", cloth)
        slot.set_editor_property("material_slot_name", "MI-Upper")
        shirt.set_editor_property("materials", [slot])
        unreal.EditorAssetLibrary.save_asset(setup_preview.SHIRT_PATH)
    level_sub = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
    if unreal.EditorAssetLibrary.does_asset_exist(setup_preview.MAP_PATH):
        level_sub.load_level(setup_preview.MAP_PATH)
        for actor in unreal.EditorLevelLibrary.get_all_level_actors():
            if actor.get_actor_label() == "tshirt-baggy-male" and cloth:
                try:
                    actor.skeletal_mesh_component.set_material(0, cloth)
                except Exception as err:
                    unreal.log("[tex] override failed " + str(err))
        unreal.EditorLevelLibrary.set_level_viewport_camera_info(
            unreal.Vector(55.0, 70.0, 135.0),
            unreal.Rotator(-5.0, -128.0, 0.0),
        )
        level_sub.save_current_level()
    unreal.EditorAssetLibrary.save_directory(setup_preview.SHIRT_DIR, True, True)
    unreal.log("[tex] done")


if __name__ == "__main__":
    main()
