"""Import framed catalog icons for mod rows."""
import os
import unreal

ROOT = r"C:\Users\xrowe\rolloutrowemod\art\previews"
DEST = "/Game/MainFolder/UI/customization/mod-icons"
ICONS = ("T_hoodie-navy-mod", "T_tshirt-baggy-mod")


def log(msg):
    unreal.log("[previews] " + msg)


def import_icon(name):
    filepath = os.path.join(ROOT, name + ".png")
    if not os.path.isfile(filepath):
        log("missing " + filepath)
        return
    full = DEST + "/" + name
    unreal.EditorAssetLibrary.make_directory(DEST)
    task = unreal.AssetImportTask()
    task.filename = filepath
    task.destination_path = DEST
    task.destination_name = name
    task.replace_existing = True
    task.automated = True
    task.save = True
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])
    tex = unreal.load_asset(full)
    if tex is None:
        log("FAILED " + name)
        return
    try:
        tex.set_editor_property("srgb", True)
        tex.set_editor_property("lod_group", unreal.TextureGroup.TEXTUREGROUP_UI)
        tex.set_editor_property(
            "compression_settings", unreal.TextureCompressionSettings.TC_DEFAULT
        )
        unreal.EditorAssetLibrary.save_asset(full)
    except Exception as err:
        log("flags failed " + str(err))
    log("ok " + tex.get_path_name())


def main():
    for name in ICONS:
        import_icon(name)


if __name__ == "__main__":
    main()
