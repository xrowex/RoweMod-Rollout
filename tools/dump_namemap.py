from pathlib import Path
import json
import struct
import sys

ROOT = Path(__file__).resolve().parents[1]
LEGACY = ROOT / "dumps" / "legacy"
OUT = ROOT / "dumps" / "json"


def read_fstring(data, off):
    if off + 4 > len(data):
        return None, off
    n = struct.unpack_from("<i", data, off)[0]
    off += 4
    if n == 0:
        return "", off
    if n > 0:
        raw = data[off : off + n]
        off += n
        return raw.split(b"\x00", 1)[0].decode("utf-8", "replace"), off
    n = -n
    raw = data[off : off + n * 2]
    off += n * 2
    return raw.decode("utf-16le", "replace").split("\x00", 1)[0], off


def parse_package(path: Path):
    data = path.read_bytes()
    off = 0
    tag, legacy = struct.unpack_from("<ii", data, off)
    off += 8
    if legacy != -4:
        struct.unpack_from("<i", data, off)
        off += 4
    ver4 = struct.unpack_from("<i", data, off)[0]
    off += 4
    ver5 = None
    if legacy <= -8:
        ver5 = struct.unpack_from("<i", data, off)[0]
        off += 4
    lic = struct.unpack_from("<i", data, off)[0]
    off += 4
    ncustom = struct.unpack_from("<i", data, off)[0]
    off += 4
    if ncustom < 0 or ncustom > 256:
        raise RuntimeError(f"bad custom count {ncustom} at {off-4} in {path}")
    for _ in range(ncustom):
        off += 16 + 4
    header_size = struct.unpack_from("<i", data, off)[0]
    off += 4
    folder, off = read_fstring(data, off)
    flags = struct.unpack_from("<I", data, off)[0]
    off += 4
    name_count = struct.unpack_from("<i", data, off)[0]
    off += 4
    name_off = struct.unpack_from("<i", data, off)[0]
    names = []
    noff = name_off
    for _ in range(name_count):
        s, noff = read_fstring(data, noff)
        noff += 4
        names.append(s)
    return {
        "path": str(path.relative_to(LEGACY)).replace("\\", "/"),
        "legacy": legacy,
        "ver4": ver4,
        "ver5": ver5,
        "licensee": lic,
        "custom_versions": ncustom,
        "header_size": header_size,
        "folder": folder,
        "flags": hex(flags),
        "unversioned_properties": bool(flags & 0x20000000),
        "name_count": name_count,
        "names": names,
    }


FILES = [
    LEGACY / "RollerSkate/Content/MainFolder/Character/body/main-rig/main-rig.uasset",
    LEGACY / "RollerSkate/Content/MainFolder/Character/body/male/male-01/male-body-01.uasset",
    LEGACY / "RollerSkate/Content/MainFolder/Character/body/female/female-01/female-body-01.uasset",
    LEGACY / "RollerSkate/Content/MainFolder/Character/upper/hoodie/hoodie-male.uasset",
    LEGACY / "RollerSkate/Content/MainFolder/Character/upper/hoodie/hoodie-female.uasset",
    LEGACY / "RollerSkate/Content/MainFolder/UI/customization/data/DT-upper.uasset",
    LEGACY / "RollerSkate/Content/MainFolder/UI/customization/data/S-upper.uasset",
    LEGACY / "RollerSkate/Content/MainFolder/UI/customization/data/DT-lower.uasset",
    LEGACY / "RollerSkate/Content/MainFolder/UI/customization/data/S-lower.uasset",
    LEGACY / "RollerSkate/Content/MainFolder/UI/customization/data/DT-hats.uasset",
    LEGACY / "RollerSkate/Content/MainFolder/UI/customization/data/S-hats.uasset",
    LEGACY / "RollerSkate/Content/MainFolder/UI/customization/data/DT-bodytypes.uasset",
    LEGACY / "RollerSkate/Content/MainFolder/UI/customization/data/S-bodytypes.uasset",
    LEGACY / "RollerSkate/Content/MainFolder/Blueprints/NewMainCharacter.uasset",
    LEGACY / "RollerSkate/Content/MainFolder/materials/cloth-master/M-cloth-master.uasset",
    LEGACY / "RollerSkate/Content/MainFolder/UI/customization/data/E-customization-categories.uasset",
]


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    combined = {}
    for path in FILES:
        print("=" * 80)
        print(path.name)
        if not path.exists():
            print("MISSING")
            continue
        parsed = parse_package(path)
        combined[path.stem] = parsed
        print(
            f"legacy={parsed['legacy']} ver4={parsed['ver4']} ver5={parsed['ver5']} "
            f"flags={parsed['flags']} unversioned={parsed['unversioned_properties']} "
            f"names={parsed['name_count']} folder={parsed['folder']}"
        )
        for name in parsed["names"]:
            print(f"  {name}")
        (OUT / f"{path.stem}.namemap.json").write_text(
            json.dumps(parsed, indent=2), encoding="utf-8"
        )
    (OUT / "namemaps.json").write_text(json.dumps(combined, indent=2), encoding="utf-8")
    print("wrote", OUT / "namemaps.json")


if __name__ == "__main__":
    sys.exit(main())
