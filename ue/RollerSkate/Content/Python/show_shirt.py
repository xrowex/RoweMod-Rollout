"""Assign the textured material and frame the shirt so the graphic is visible."""
import setup_preview
import unreal


def main():
    cloth = setup_preview.make_textured_material()
    shirt = unreal.load_asset(setup_preview.SHIRT_PATH)
    if shirt is None:
        unreal.log_error("[show] shirt mesh missing")
        return
    if cloth:
        slot = unreal.SkeletalMaterial()
        slot.set_editor_property("material_interface", cloth)
        slot.set_editor_property("material_slot_name", "MI-Upper")
        shirt.set_editor_property("materials", [slot])
        unreal.EditorAssetLibrary.save_asset(setup_preview.SHIRT_PATH)
        unreal.log("[show] assigned " + cloth.get_path_name())

    level_sub = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
    if unreal.EditorAssetLibrary.does_asset_exist(setup_preview.MAP_PATH):
        level_sub.load_level(setup_preview.MAP_PATH)
        for actor in unreal.EditorLevelLibrary.get_all_level_actors():
            if actor.get_actor_label() == "tshirt-baggy-male" and cloth:
                try:
                    actor.skeletal_mesh_component.set_material(0, cloth)
                except Exception as err:
                    unreal.log("[show] override failed " + str(err))
        key = unreal.EditorLevelLibrary.spawn_actor_from_class(
            unreal.PointLight, unreal.Vector(90.0, 10.0, 130.0)
        )
        key.set_actor_label("ChestKey")
        try:
            key.point_light_component.set_editor_property("intensity", 8000.0)
        except Exception:
            pass
        unreal.EditorLevelLibrary.set_level_viewport_camera_info(
            unreal.Vector(140.0, 25.0, 128.0),
            unreal.Rotator(-6.0, 190.0, 0.0),
        )
        level_sub.save_current_level()
    unreal.EditorAssetLibrary.save_directory(setup_preview.SHIRT_DIR, True, True)
    unreal.log("[show] done")


if __name__ == "__main__":
    main()
