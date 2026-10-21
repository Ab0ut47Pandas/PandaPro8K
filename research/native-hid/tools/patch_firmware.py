"""Apply/reverse exact firmware file patches offline. Never accesses a device."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

PATCHES = Path(__file__).resolve().parents[1] / 'patches'
NAMES = ('controller', 'receiver', 'controller-gate2')


def sha(data):
    return hashlib.sha256(data).hexdigest()


def read_manifest(name):
    manifest = json.loads((PATCHES / (name + '.json')).read_text(encoding='utf-8'))
    if manifest.get('schema') != 1 or manifest.get('id') != name:
        raise ValueError('Unrecognized patch manifest')
    return manifest


def transform(data, manifest, reverse=False):
    expected = manifest['result_sha256'] if reverse else manifest['source_sha256']
    wanted = manifest['source_sha256'] if reverse else manifest['result_sha256']
    if len(data) != manifest['size'] or sha(data) != expected:
        raise ValueError('Input is not the exact expected image. No file was written.\n'
                         'Expected SHA-256: ' + expected + '\nActual SHA-256:   ' + sha(data))
    result = bytearray(data)
    end = 0
    for span in manifest['changes']:
        at = span['offset']
        delta = bytes.fromhex(span['xor'])
        if not isinstance(at, int) or not delta or at < end or at + len(delta) > len(data):
            raise ValueError('Invalid, overlapping or out-of-range patch span')
        for index, value in enumerate(delta):
            result[at + index] ^= value
        end = at + len(delta)
    result = bytes(result)
    if sha(result) != wanted:
        raise ValueError('Output hash mismatch. No file was written.')
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    actions = parser.add_subparsers(dest='action', required=True)
    actions.add_parser('list', help='List known source/result hashes')
    identify = actions.add_parser('identify', help='Identify a file without changing it')
    identify.add_argument('input', type=Path)
    apply = actions.add_parser('apply', help='Generate a patched copy, or reverse it to stock')
    apply.add_argument('patch', choices=NAMES)
    apply.add_argument('input', type=Path)
    apply.add_argument('--output', type=Path, required=True)
    apply.add_argument('--reverse', action='store_true', help='Turn the exact modified file back into its stock source')
    args = parser.parse_args()
    if args.action == 'list':
        for name in NAMES:
            m = read_manifest(name)
            print(json.dumps({key: m[key] for key in ('id', 'component', 'base_version', 'source_sha256', 'result_sha256')}))
        return
    data = args.input.read_bytes()
    if args.action == 'identify':
        digest = sha(data)
        matches = []
        for name in NAMES:
            m = read_manifest(name)
            if digest == m['source_sha256']: matches.append(name + ': stock source')
            if digest == m['result_sha256']: matches.append(name + ': modified image')
        print(json.dumps({'bytes': len(data), 'sha256': digest, 'matches': matches}, indent=2))
        if not matches: raise SystemExit(1)
        return
    if args.input.resolve() == args.output.resolve():
        parser.error('Use a different output file; the source is never overwritten')
    m = read_manifest(args.patch)
    result = transform(data, m, args.reverse)
    # Exclusive create also refuses an existing file, hard link, or symlink.
    with args.output.open('xb') as target:
        target.write(result)
    print(json.dumps({'patch': args.patch, 'direction': 'restore stock file' if args.reverse else 'apply',
                      'bytes': len(result), 'sha256': sha(result), 'file_created': args.output.name,
                      'device_access': False}, indent=2))


if __name__ == '__main__':
    try:
        main()
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
