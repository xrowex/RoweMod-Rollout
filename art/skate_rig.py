"""Import the stock game skeleton in its corrected bind pose, without joint gizmos."""
from pathlib import Path
import shutil
import tempfile

import bpy
from skate_bind import repair_bind


def import_boots(path):
    before = set(bpy.data.objects)
    # Correct a temporary copy only. Keep the extracted source untouched.
    with tempfile.TemporaryDirectory(prefix="rowe-boot-rig-") as folder:
        corrected = Path(folder) / Path(path).name
        shutil.copy2(path, corrected)
        repair_bind(corrected, Path(path))
        bpy.ops.import_scene.gltf(filepath=str(corrected))
    imported = set(bpy.data.objects) - before
    arms = [o for o in imported if o.type == "ARMATURE"]
    if len(arms) != 1:
        raise RuntimeError("Stock boots must contain one game armature")
    arm = arms[0]
    helpers = {b.custom_shape for b in arm.pose.bones if b.custom_shape}
    for bone in arm.pose.bones:
        bone.custom_shape = None
    for obj in helpers & imported:
        bpy.data.objects.remove(obj, do_unlink=True)
    arm.name = "main-rig"
    arm.data.name = "main-rig"
    arm.data.display_type = "STICK"
    arm.show_in_front = True
    arm["rowe_game_rig"] = True
    bpy.context.view_layer.update()
    return arm
