"""Import a skate frame (static) or boot (skeletal) from cook-target.json."""
import json
from pathlib import Path

import unreal

REPO = Path(__file__).resolve().parents[4]


def log(msg):
    unreal.log("[skate] " + msg)


def load_target():
    path = REPO / "dumps" / "cook-target.json"
    if not path.exists():
        return {}
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return {}


def delete_if_exists(path):
    if unreal.EditorAssetLibrary.does_asset_exist(path):
        log("Deleting " + path)
        unreal.EditorAssetLibrary.delete_asset(path)


def list_new_assets(folder):
    return list(unreal.EditorAssetLibrary.list_assets(folder, recursive=True, include_folder=False))


def import_gltf(filepath, dest_dir):
    unreal.EditorAssetLibrary.make_directory(dest_dir)
    try:
        mgr = unreal.InterchangeManager.get_interchange_manager_scripted()
        src = mgr.create_source_data(filepath)
        params = unreal.ImportAssetParameters()
        try:
            params.set_editor_property("replace_existing", True)
        except Exception:
            pass
        log("Interchange import " + filepath)
        mgr.import_asset(dest_dir, src, params)
    except Exception as err:
        log("Interchange failed: " + str(err))
        task = unreal.AssetImportTask()
        task.filename = filepath
        task.destination_path = dest_dir
        task.replace_existing = True
        task.automated = True
        task.save = False
        unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])


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


def main():
    target = load_target()
    kind = str(target.get("importKind") or "").lower()
    if kind not in ("static", "skeletal-skate"):
        log("skip (importKind=" + kind + ")")
        return

    mesh_dir = str(target.get("meshDir") or "")
    mesh_name = str(target.get("meshName") or "")
    glb = str(target.get("gamebind") or target.get("glb") or "")
    if not mesh_dir or not mesh_name or not glb:
        log("missing cook-target fields")
        return
    if not Path(glb).is_file():
        log("missing glb " + glb)
        return

    mesh_path = mesh_dir + "/" + mesh_name
    delete_if_exists(mesh_path)
    import_gltf(glb, mesh_dir)

    static_mesh = None
    skel_mesh = None
    for path in list_new_assets(mesh_dir):
        asset = unreal.load_asset(path)
        if isinstance(asset, unreal.StaticMesh) and static_mesh is None:
            static_mesh = asset
        if isinstance(asset, unreal.SkeletalMesh) and skel_mesh is None:
            skel_mesh = asset

    want_static = kind == "static"
    mesh = static_mesh if want_static else (skel_mesh or static_mesh)
    if mesh is None:
        unreal.log_error("skate mesh import failed")
        return
    if want_static and not isinstance(mesh, unreal.StaticMesh):
        unreal.log_error("frame cook needs a StaticMesh (BladeMesh). Got " + mesh.get_class().get_name())
        return

    current = mesh.get_path_name().split(".")[0]
    if current != mesh_path:
        renamed = rename_asset(current, mesh_dir, mesh_name)
        if renamed:
            mesh = renamed

    unreal.EditorAssetLibrary.save_asset(mesh_path)
    unreal.EditorAssetLibrary.save_directory(mesh_dir, True, True)
    log("saved " + mesh.get_path_name())


if __name__ == "__main__":
    main()
