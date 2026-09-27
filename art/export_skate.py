"""Export a saved frame/boot .blend or FBX without changing its source file."""
import os
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from mesh_source import source_path, load_source, protect_source
from skate_reference import is_reference

source = source_path()
dest = Path(os.environ["ROWE_OUT_GLB"])
protect_source(source, dest)
load_source(source)
rig_helpers = {bone.custom_shape for arm in bpy.context.scene.objects if arm.type == "ARMATURE"
               for bone in arm.pose.bones if bone.custom_shape}
meshes = [o for o in bpy.context.scene.objects
          if o.type == "MESH" and len(o.data.vertices) and o not in rig_helpers
          and not is_reference(o)
          and not any(c.name == "glTF_not_exported" for c in o.users_collection)]
if not meshes:
    raise RuntimeError("No mesh in the saved source. Save your .blend or re-export your FBX, then try again.")
if os.environ.get("ROWE_IMPORT_KIND") == "skeletal-skate":
    invalid = [o.name for o in meshes if not o.find_armature() or not any(v.groups for v in o.data.vertices)]
    if invalid:
        raise RuntimeError("Boot meshes missing armature or weights: " + ", ".join(invalid))
    # A rig replacement can leave a dead modifier ahead of the active one.
    # Clean only the temporary export scene, never the user's saved source.
    for obj in meshes:
        for modifier in list(obj.modifiers):
            if modifier.type == "ARMATURE" and modifier.object is None:
                obj.modifiers.remove(modifier)
bpy.ops.object.select_all(action="DESELECT")
for obj in meshes:
    obj.select_set(True)
    arm = obj.find_armature()
    if arm:
        arm.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
dest.parent.mkdir(parents=True, exist_ok=True)
staged = dest.with_name(dest.stem + ".pending.glb")
bpy.ops.export_scene.gltf(filepath=str(staged), use_selection=True, export_format="GLB",
    export_animations=False, export_skins=True, export_apply=True, export_yup=True)
if not staged.is_file() or staged.stat().st_size < 20:
    raise RuntimeError("Blender did not produce a mesh export.")
if os.environ.get("ROWE_IMPORT_KIND") == "skeletal-skate":
    from skate_bind import repair_bind
    reference = Path(os.environ.get("ROWE_BIND_GLB", ""))
    if not reference.is_file():
        raise RuntimeError("Stock boot reference is required. Get skate models from the game first.")
    repair_bind(staged, reference)
os.replace(staged, dest)
print("ROWE_SKATE_EXPORTED", dest)
