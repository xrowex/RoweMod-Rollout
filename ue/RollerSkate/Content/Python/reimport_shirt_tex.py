"""Reimport the shirt glTF, then assign the textured preview material."""
import import_shirt
import setup_preview
import unreal


def main():
    import_shirt.main()
    cloth = setup_preview.make_textured_material()
    shirt = unreal.load_asset(setup_preview.SHIRT_PATH)
    if shirt and cloth:
        slot = unreal.SkeletalMaterial()
        slot.set_editor_property("material_interface", cloth)
        slot.set_editor_property("material_slot_name", "MI-Upper")
        shirt.set_editor_property("materials", [slot])
        unreal.EditorAssetLibrary.save_asset(setup_preview.SHIRT_PATH)
        unreal.log("[fix] mesh.skeleton=" + str(shirt.get_editor_property("skeleton")))
        import_shirt.log_bounds("shirt", shirt)

    level_sub = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
    if unreal.EditorAssetLibrary.does_asset_exist(setup_preview.MAP_PATH):
        level_sub.load_level(setup_preview.MAP_PATH)
        for actor in list(unreal.EditorLevelLibrary.get_all_level_actors()):
            label = actor.get_actor_label()
            if label == "tshirt-baggy-male" and shirt:
                try:
                    actor.skeletal_mesh_component.set_skeletal_mesh_asset(shirt)
                except Exception:
                    actor.skeletal_mesh_component.set_skeletal_mesh(shirt)
                if cloth:
                    actor.skeletal_mesh_component.set_material(0, cloth)
        unreal.EditorLevelLibrary.set_level_viewport_camera_info(
            unreal.Vector(140.0, 25.0, 128.0),
            unreal.Rotator(-6.0, 190.0, 0.0),
        )
        level_sub.save_current_level()
    unreal.EditorAssetLibrary.save_directory("/Game/MainFolder", True, True)
    unreal.EditorAssetLibrary.save_directory("/Game/ModPreview", True, True)
    unreal.log("[fix] done")


if __name__ == "__main__":
    main()
