"""Put the shirt mesh on the game hoodie skin (nodes + inverse-binds).

Blender rewrites IBMs on export. Interchange of that file will not match
live main-rig. This copies shirt POSITION/NORMAL/UV/JOINTS/WEIGHTS into
hoodie-male.glb and remaps joint indices to the hoodie skin order.
"""
from __future__ import annotations

import json
import os
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def _env_path(name: str) -> Path | None:
    value = os.environ.get(name)
    return Path(value) if value else None


def _first_existing(*paths: Path | str | None) -> Path | None:
    for path in paths:
        if not path:
            continue
        candidate = Path(path)
        if candidate.exists():
            return candidate
    return None


MESH_NAME = os.environ.get("ROWE_MESH", "tshirt-baggy-male")
HOODIE = _first_existing(
    _env_path("ROWE_BIND_GLB"),
    ROOT / "art" / "_ref" / "cue-hoodie" / "RollerSkate" / "Content" / "MainFolder" / "Character" / "upper" / "hoodie" / "hoodie-male.glb",
    ROOT / "dumps" / "game-clothing" / "meshes" / "RollerSkate" / "Content" / "MainFolder" / "Character" / "upper" / "hoodie" / "hoodie-male.glb",
)
SHIRT = _env_path("ROWE_OUT_GLB") or (ROOT / "art" / f"{MESH_NAME}.glb")
OUT = _env_path("ROWE_GAMEBIND") or (ROOT / "art" / f"{MESH_NAME}.gamebind.glb")

COMPONENT = {
    5120: ("b", 1),
    5121: ("B", 1),
    5122: ("h", 2),
    5123: ("H", 2),
    5125: ("I", 4),
    5126: ("f", 4),
}
VEC_COUNT = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def read_glb(path: Path) -> tuple[dict, bytes]:
    data = path.read_bytes()
    magic, version, length = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF":
        raise ValueError(f"not glb: {path}")
    offset = 12
    json_doc = None
    bin_blob = b""
    while offset + 8 <= length:
        chunk_len, chunk_type = struct.unpack_from("<I4s", data, offset)
        offset += 8
        chunk = data[offset : offset + chunk_len]
        offset += chunk_len
        if chunk_type == b"JSON":
            json_doc = json.loads(chunk)
        elif chunk_type == b"BIN\x00":
            bin_blob = chunk
    if json_doc is None:
        raise ValueError(f"no JSON in {path}")
    return json_doc, bin_blob


def write_glb(path: Path, doc: dict, blob: bytes) -> None:
    json_bytes = json.dumps(doc, separators=(",", ":")).encode("utf-8")
    json_bytes += b" " * ((4 - (len(json_bytes) % 4)) % 4)
    blob_pad = blob + (b"\x00" * ((4 - (len(blob) % 4)) % 4))
    total = 12 + 8 + len(json_bytes) + 8 + len(blob_pad)
    out = bytearray()
    out += struct.pack("<4sII", b"glTF", 2, total)
    out += struct.pack("<I4s", len(json_bytes), b"JSON")
    out += json_bytes
    out += struct.pack("<I4s", len(blob_pad), b"BIN\x00")
    out += blob_pad
    path.write_bytes(out)


def accessor_bytes(doc: dict, blob: bytes, index: int) -> bytes:
    acc = doc["accessors"][index]
    view = doc["bufferViews"][acc["bufferView"]]
    start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    count = acc["count"] * VEC_COUNT[acc["type"]]
    size = COMPONENT[acc["componentType"]][1]
    return blob[start : start + count * size]


def read_mats(doc: dict, blob: bytes, index: int) -> list[list[float]]:
    raw = accessor_bytes(doc, blob, index)
    vals = list(struct.unpack("<" + "f" * (len(raw) // 4), raw))
    return [vals[i : i + 16] for i in range(0, len(vals), 16)]


def joint_names(doc: dict) -> list[str]:
    skin = doc["skins"][0]
    nodes = doc["nodes"]
    return [nodes[i].get("name", f"node{i}") for i in skin["joints"]]


def compare_ibms(hoodie: dict, hoodie_bin: bytes, shirt: dict, shirt_bin: bytes) -> None:
    h_names = joint_names(hoodie)
    s_names = joint_names(shirt)
    h_mats = read_mats(hoodie, hoodie_bin, hoodie["skins"][0]["inverseBindMatrices"])
    s_mats = read_mats(shirt, shirt_bin, shirt["skins"][0]["inverseBindMatrices"])
    s_lookup = {name: mat for name, mat in zip(s_names, s_mats)}
    print("HOODIE_JOINTS", len(h_names), "SHIRT_JOINTS", len(s_names))
    missing = [n for n in h_names if n not in s_lookup]
    extra = [n for n in s_names if n not in set(h_names)]
    print("MISSING_IN_SHIRT", missing)
    print("EXTRA_IN_SHIRT", extra)
    worst = 0.0
    worst_name = ""
    mismatches = 0
    for name, h_mat in zip(h_names, h_mats):
        s_mat = s_lookup.get(name)
        if s_mat is None:
            continue
        diff = max(abs(a - b) for a, b in zip(h_mat, s_mat))
        if diff > worst:
            worst = diff
            worst_name = name
        if diff > 1e-4:
            mismatches += 1
            if mismatches <= 8:
                print(f"IBM_DIFF {name} max={diff:.6f}")
    print(f"IBM_MISMATCHES {mismatches}/{len(h_names)} worst={worst_name} {worst:.6f}")


def remap_joints(raw: bytes, component_type: int, shirt_names: list[str], hoodie_index: dict[str, int]) -> bytes:
    fmt, size = COMPONENT[component_type]
    count = len(raw) // size
    values = list(struct.unpack("<" + fmt * count, raw))
    out = []
    for value in values:
        if value >= len(shirt_names):
            out.append(0)
            continue
        name = shirt_names[value]
        out.append(hoodie_index.get(name, 0))
    return struct.pack("<" + fmt * count, *out)


def stitch(hoodie: dict, hoodie_bin: bytes, shirt: dict, shirt_bin: bytes) -> tuple[dict, bytes]:
    h_names = joint_names(hoodie)
    s_names = joint_names(shirt)
    hoodie_index = {name: i for i, name in enumerate(h_names)}
    shirt_mesh = shirt["meshes"][0]
    new_blob = bytearray(hoodie_bin)
    new_views = list(hoodie.get("bufferViews", []))
    new_accs = list(hoodie.get("accessors", []))

    def append_accessor(src_doc: dict, src_blob: bytes, src_index: int, payload: bytes | None = None) -> int:
        acc = dict(src_doc["accessors"][src_index])
        data = payload if payload is not None else accessor_bytes(src_doc, src_blob, src_index)
        view = {
            "buffer": 0,
            "byteOffset": len(new_blob),
            "byteLength": len(data),
        }
        new_blob.extend(data)
        pad = (4 - (len(new_blob) % 4)) % 4
        new_blob.extend(b"\x00" * pad)
        acc["bufferView"] = len(new_views)
        acc.pop("byteOffset", None)
        new_views.append(view)
        new_accs.append(acc)
        return len(new_accs) - 1

    new_prims = []
    for prim in shirt_mesh["primitives"]:
        attrs = {}
        for key, acc_i in prim["attributes"].items():
            payload = None
            if key.startswith("JOINTS_"):
                acc = shirt["accessors"][acc_i]
                payload = remap_joints(
                    accessor_bytes(shirt, shirt_bin, acc_i),
                    acc["componentType"],
                    s_names,
                    hoodie_index,
                )
            attrs[key] = append_accessor(shirt, shirt_bin, acc_i, payload)
        new_prim = {"attributes": attrs, "mode": prim.get("mode", 4)}
        if "indices" in prim:
            new_prim["indices"] = append_accessor(shirt, shirt_bin, prim["indices"])
        new_prims.append(new_prim)

    out = json.loads(json.dumps(hoodie))
    out["bufferViews"] = new_views
    out["accessors"] = new_accs
    out["buffers"] = [{"byteLength": len(new_blob)}]
    mesh_index = 0
    if hoodie.get("meshes"):
        out["meshes"][0]["primitives"] = new_prims
        out["meshes"][0]["name"] = MESH_NAME
    else:
        out["meshes"] = [{"name": MESH_NAME, "primitives": new_prims}]
        mesh_index = 0
    # Keep hoodie nodes/skins. Point the first mesh-bearing node at mesh 0.
    for node in out["nodes"]:
        if "mesh" in node:
            node["mesh"] = mesh_index
            node["skin"] = 0
            node["name"] = MESH_NAME
            break
    else:
        out["nodes"].append({"name": MESH_NAME, "mesh": mesh_index, "skin": 0})
        out.setdefault("scenes", [{"nodes": []}])
        out["scenes"][0].setdefault("nodes", []).append(len(out["nodes"]) - 1)
    return out, bytes(new_blob)


def main() -> None:
    if HOODIE is None or not HOODIE.exists():
        raise FileNotFoundError("Missing hoodie bind pose. Pull clothing first.")
    if not SHIRT.exists():
        raise FileNotFoundError(f"Missing {SHIRT}")
    hoodie, hoodie_bin = read_glb(HOODIE)
    shirt, shirt_bin = read_glb(SHIRT)
    compare_ibms(hoodie, hoodie_bin, shirt, shirt_bin)
    out_doc, out_bin = stitch(hoodie, hoodie_bin, shirt, shirt_bin)
    write_glb(OUT, out_doc, out_bin)
    print("WROTE", OUT, "bytes", OUT.stat().st_size)


if __name__ == "__main__":
    main()
