"""Compare native skeleton JSON exports by bone name (CUE4Parse --format json)."""
import argparse
import json
from pathlib import Path


def skeleton(path):
    entries = json.loads(Path(path).read_bytes())
    ref = next(e for e in entries if e['Type'] == 'Skeleton')['ReferenceSkeleton']
    bones = ref['FinalRefBoneInfo']
    return {b['Name']: (None if b['ParentIndex'] < 0 else bones[b['ParentIndex']]['Name'], pose)
            for b, pose in zip(bones, ref['FinalRefBonePose'])}


def compare(reference, cooked):
    original, actual = skeleton(reference), skeleton(cooked)
    if original.keys() != actual.keys():
        raise ValueError('Skeleton bone names differ')
    rotation_error = position_error = scale_error = 0.0
    for name, (parent, a) in original.items():
        other_parent, b = actual[name]
        if parent != other_parent:
            raise ValueError('Skeleton parent differs: ' + name)
        qa, qb = ([p['Rotation'][k] for k in 'XYZW'] for p in (a, b))
        # q and -q represent the same rotation; changing only W does not.
        rotation_error = max(rotation_error, min(max(abs(x-y) for x,y in zip(qa,qb)),
                                                max(abs(x+y) for x,y in zip(qa,qb))))
        position_error = max(position_error, max(abs(a['Translation'][k]-b['Translation'][k]) for k in 'XYZ'))
        scale_error = max(scale_error, max(abs(a['Scale3D'][k]-b['Scale3D'][k]) for k in 'XYZ'))
    result = dict(bones=len(original), rotation_error=rotation_error,
                  position_error_cm=position_error, scale_error=scale_error)
    if rotation_error > 1e-5 or position_error > 0.001 or scale_error > 1e-5:
        raise ValueError('Reference pose mismatch: ' + json.dumps(result))
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference')
    parser.add_argument('cooked')
    args = parser.parse_args()
    print(json.dumps(compare(args.reference, args.cooked), indent=2))
