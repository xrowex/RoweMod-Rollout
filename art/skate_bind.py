"""Use the extracted boot rig with rotations converted for UE Interchange.

CUE4Parse's glTF local rotations round-trip through Interchange conjugated
relative to the native UE reference pose. Correct the reference rotations and
rebuild inverse binds without changing authored vertices, materials or weights.
Runs in Blender's Python (mathutils).
"""
import struct
from mathutils import Matrix, Quaternion, Vector
from stitch_shirt_glb import read_glb, write_glb


def repair_bind(path, reference):
    doc, blob = read_glb(path)
    ref, _ = read_glb(reference)
    if len(doc.get("skins", [])) != 1 or len(ref.get("skins", [])) != 1:
        raise RuntimeError("Boot export requires one rig and one stock reference skin")
    skin = doc["skins"][0]
    ref_nodes = {ref["nodes"][i]["name"]: ref["nodes"][i] for i in ref["skins"][0]["joints"]}
    nodes = doc["nodes"]
    names = {nodes[i]["name"] for i in skin["joints"]}
    if names != set(ref_nodes):
        raise RuntimeError("Boot rig bones do not match the stock boot reference")
    def bone_parents(document, joints):
        joint_set = set(joints)
        parents = {child: i for i, n in enumerate(document['nodes']) for child in n.get('children', [])}
        return {document['nodes'][i]['name']:
                document['nodes'][parents[i]]['name'] if parents.get(i) in joint_set else None for i in joints}
    if bone_parents(doc, skin['joints']) != bone_parents(ref, ref['skins'][0]['joints']):
        raise RuntimeError("Boot rig hierarchy does not match the stock boot reference")
    for i in skin["joints"]:
        node = nodes[i]
        original = ref_nodes[node["name"]]
        x, y, z, w = original.get("rotation", [0, 0, 0, 1])
        node.pop("matrix", None)
        node["rotation"] = [-x, -y, -z, w]
        node["translation"] = original.get("translation", [0, 0, 0])
        node["scale"] = original.get("scale", [1, 1, 1])
    parents = {child: i for i, node in enumerate(nodes) for child in node.get("children", [])}
    cache = {}
    def world(i):
        if i not in cache:
            n = nodes[i]
            if "matrix" in n:
                m = Matrix([n["matrix"][k:k+4] for k in range(0,16,4)]).transposed()
            else:
                x,y,z,w = n.get("rotation", [0,0,0,1])
                m = Matrix.LocRotScale(Vector(n.get("translation", [0,0,0])),
                    Quaternion((w,x,y,z)), Vector(n.get("scale", [1,1,1])))
            cache[i] = world(parents[i]) @ m if i in parents else m
        return cache[i]
    matrices = []
    for i in skin["joints"]:
        inv = world(i).inverted()
        matrices.extend(inv[row][col] for col in range(4) for row in range(4))
    data = bytearray(blob)
    data.extend(b"\0" * (-len(data) % 4))
    offset = len(data)
    data.extend(struct.pack("<" + "f" * len(matrices), *matrices))
    view = len(doc["bufferViews"])
    doc["bufferViews"].append({"buffer":0,"byteOffset":offset,"byteLength":len(matrices)*4})
    skin["inverseBindMatrices"] = len(doc["accessors"])
    doc["accessors"].append({"bufferView":view,"componentType":5126,"count":len(skin["joints"]),"type":"MAT4"})
    doc["buffers"][0]["byteLength"] = len(data)
    write_glb(path, doc, bytes(data))
    print("ROWE_BOOT_BIND_CORRECTED", len(skin["joints"]), "bones")
