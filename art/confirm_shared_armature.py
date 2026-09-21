"""Import the extracted hoodie/body glTF next to the authored navy hoodie and confirm they share bone names."""
from pathlib import Path
import bpy
import json

ROOT = Path(__file__).resolve().parents[1]
REF = ROOT / "dumps" / "meshes" / "RollerSkate" / "Content" / "MainFolder" / "Character"
OUT = ROOT / "dumps" / "json" / "armature-compare.json"
BLEND = ROOT / "art" / "_ref" / "skater_armature.blend"

bpy.ops.wm.read_factory_settings(use_empty=True)

def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    return [o for o in bpy.data.objects if o not in before]

hoodie_objs = import_glb(REF / "upper" / "hoodie" / "hoodie-male.glb")
body_objs = import_glb(REF / "body" / "male" / "male-01" / "male-body-01.glb")

# our authored file
bpy.ops.wm.append(
    filepath=str(ROOT / "art" / "hoodie-navy-male.blend") + "/Object/main-rig",
    directory=str(ROOT / "art" / "hoodie-navy-male.blend") + "/Object/",
    filename="main-rig",
)

def bone_names(obj):
    if obj.type != "ARMATURE":
        return []
    return sorted(b.name for b in obj.data.bones)

armatures = [o for o in bpy.data.objects if o.type == "ARMATURE"]
report = {
    "armatures": {o.name: bone_names(o) for o in armatures},
    "hoodie_import": [o.name + ":" + o.type for o in hoodie_objs],
    "body_import": [o.name + ":" + o.type for o in body_objs],
}
namesets = [set(bone_names(o)) for o in armatures if bone_names(o)]
if len(namesets) >= 2:
    a, b = namesets[0], namesets[1]
    report["shared_bones"] = sorted(a & b)
    report["only_first"] = sorted(a - b)
    report["only_second"] = sorted(b - a)
    report["shared_count"] = len(a & b)

OUT.write_text(json.dumps(report, indent=2), encoding="utf-8")
BLEND.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
print("ARMATURES", [o.name for o in armatures])
print("SHARED", report.get("shared_count"))
print("WROTE", OUT)
