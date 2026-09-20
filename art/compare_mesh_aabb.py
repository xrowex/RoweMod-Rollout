"""Print vertex AABBs from hoodie / shirt / gamebind glTFs."""
from __future__ import annotations

import struct
from pathlib import Path

from stitch_shirt_glb import accessor_bytes, read_glb

ROOT = Path(r"C:\Users\xrowe\rolloutrowemod")
FILES = {
    "hoodie": ROOT / "art" / "_ref" / "cue-hoodie" / "RollerSkate" / "Content" / "MainFolder" / "Character" / "upper" / "hoodie" / "hoodie-male.glb",
    "shirt": ROOT / "art" / "tshirt-baggy-male.glb",
    "gamebind": ROOT / "art" / "tshirt-baggy-male.gamebind.glb",
}


def mesh_positions(doc: dict, blob: bytes) -> list[tuple[float, float, float]]:
    prim = doc["meshes"][0]["primitives"][0]
    raw = accessor_bytes(doc, blob, prim["attributes"]["POSITION"])
    vals = struct.unpack("<" + "f" * (len(raw) // 4), raw)
    return [(vals[i], vals[i + 1], vals[i + 2]) for i in range(0, len(vals), 3)]


def aabb(points: list[tuple[float, float, float]]) -> None:
    xs, ys, zs = zip(*points)
    print(
        f"  n={len(points)} min=({min(xs):.4f},{min(ys):.4f},{min(zs):.4f}) "
        f"max=({max(xs):.4f},{max(ys):.4f},{max(zs):.4f}) "
        f"center=({(min(xs)+max(xs))/2:.4f},{(min(ys)+max(ys))/2:.4f},{(min(zs)+max(zs))/2:.4f})"
    )


def main() -> None:
    for label, path in FILES.items():
        doc, blob = read_glb(path)
        print(label, path.name)
        aabb(mesh_positions(doc, blob))


if __name__ == "__main__":
    main()
