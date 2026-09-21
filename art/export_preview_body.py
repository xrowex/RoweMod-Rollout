"""Export male-body-01.glb to an FBX for the Unreal preview map."""
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]
GLB = ROOT / "art" / "_ref" / "male-body-01.glb"
FBX = ROOT / "art" / "male-body-01.fbx"


def main() -> None:
    if not GLB.exists():
        raise FileNotFoundError(f"Missing {GLB}")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(GLB), bone_heuristic="TEMPERANCE")
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    print("ARM", arm.name, "bones", len(arm.data.bones))
    for mesh in meshes:
        print("MESH", mesh.name, "verts", len(mesh.data.vertices), "parent", mesh.parent.name if mesh.parent else None)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    for mesh in meshes:
        mesh.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=str(FBX),
        use_selection=True,
        add_leaf_bones=False,
        bake_anim=False,
        armature_nodetype="NULL",
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        object_types={"ARMATURE", "MESH"},
        use_armature_deform_only=False,
        mesh_smooth_type="FACE",
    )
    print("WROTE", FBX)


main()
