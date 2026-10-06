"""Bind compiled Host settings and native integration metadata without application startup."""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parents[1]
HOST = ROOT / 'src/Orbyss.Foundation.Host'
MAX_METADATA = 2_097_152


def require(condition, message):
    if not condition:
        raise ValueError('Host settings metadata: ' + message)


def contained(root: Path, relative: str) -> Path:
    path = PurePosixPath(relative)
    require(relative and not path.is_absolute() and '\\' not in relative and ':' not in relative
            and all(part not in ('', '.', '..') for part in path.parts), 'unsafe source path')
    result = (root / Path(*path.parts)).resolve()
    require(result.is_relative_to(root.resolve()), 'source path escapes its owner')
    return result


def normalized(path: Path) -> bytes:
    require(path.is_file() and path.stat().st_size <= 1_048_576, 'missing or excessive source')
    return path.read_bytes().decode('utf-8-sig').replace('\r\n', '\n').replace('\r', '\n').encode('utf-8')


def digest(path: Path) -> str:
    require(path.is_file() and 0 < path.stat().st_size <= 268_435_456, 'missing or excessive assembly')
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(65536), b''):
            value.update(block)
    return value.hexdigest()


def read(path: Path) -> dict:
    require(path.is_file() and 0 < path.stat().st_size <= MAX_METADATA, 'missing or excessive metadata')
    value = json.loads(path.read_bytes().decode('utf-8-sig'))
    require(isinstance(value, dict), 'metadata must be an object')
    return value


def write(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + '.assembling')
    require(not temporary.exists(), 'another assembly already owns the temporary output')
    try:
        with temporary.open('xb') as stream:
            size = 0
            for item in json.JSONEncoder(ensure_ascii=True, sort_keys=True, separators=(',', ':')).iterencode(value):
                encoded = item.encode('utf-8')
                size += len(encoded)
                require(size < MAX_METADATA, 'encoded metadata exceeds its finite limit')
                stream.write(encoded)
            stream.write(b'\n')
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()


def assemble(args) -> None:
    output = args.output.resolve()
    host_output = args.host_output.resolve()
    require(output.is_relative_to(host_output), 'output escapes the Host directory')
    transport = read(args.transport.resolve())
    vendor = read(args.vendor.resolve())
    for item in (transport, vendor):
        require(item.get('schemaVersion') == 2 and item.get('packageId') == 'Orbyss.Foundation.Host'
                and item.get('packageVersion') == args.version, 'producer or version differs')
        require(item.get('assembly') == {'name': 'Orbyss.Foundation.Host.dll',
                'sha256': digest(host_output / 'Orbyss.Foundation.Host.dll')}, 'compiled Host binding differs')
        require(item.get('imports') == [], 'unexpected imported Host type scope')
    require({item['scope'] for item in transport['contracts']} == {'host-transport', 'host-boot'},
            'Host transport/boot scopes incomplete')
    require({item['scope'] for item in vendor['contracts']} == {'host-cshells-binding', 'host-nuplane-binding'},
            'native binding scopes incomplete')
    combined = dict(vendor)
    combined['contracts'] = transport['contracts'] + vendor['contracts']
    combined['sourceSha256'] = dict(vendor['sourceSha256'])
    combined['sourceFiles'] = dict(vendor['sourceFiles'])
    copies = {}
    for relative, expected in transport['sourceSha256'].items():
        data = normalized(contained(HOST, relative))
        actual = hashlib.sha256(data).hexdigest()
        require(actual == expected, 'compiled source metadata is stale: ' + relative)
        require(relative not in combined['sourceSha256'] or combined['sourceSha256'][relative] == expected,
                'two producer source authorities conflict')
        combined['sourceSha256'][relative] = expected
        combined['sourceFiles'][relative] = '.orbyss-foundation/settings-sources/host/' + relative + '.txt'
        copies[combined['sourceFiles'][relative]] = data
    for relative, source in [('producer/assemble_host_settings_metadata.py', Path(__file__)),
                             ('settings.source.json', HOST / 'settings.source.json')]:
        data = normalized(source)
        combined['sourceSha256'][relative] = hashlib.sha256(data).hexdigest()
        combined['sourceFiles'][relative] = '.orbyss-foundation/settings-sources/host/' + relative + '.txt'
        copies[combined['sourceFiles'][relative]] = data
    require(set(combined['sourceSha256']) == set(combined['sourceFiles'])
            and len(set(combined['sourceFiles'].values())) == len(combined['sourceFiles']), 'source map is incomplete/ambiguous')
    for contract in combined['contracts']:
        contract['sources'] = dict(combined['sourceSha256'])
    expected_snapshots = set(combined['sourceFiles'].values())
    for origin in combined['origins']:
        expected_snapshots.update(origin['source']['files'])
    snapshot_root = host_output / '.orbyss-foundation/settings-sources'
    actual_snapshots = {path.relative_to(host_output).as_posix()
                        for path in snapshot_root.rglob('*') if path.is_file()}
    require(actual_snapshots == expected_snapshots if args.validate_only
            else actual_snapshots.issubset(expected_snapshots), 'source snapshot inventory differs')
    if args.validate_only:
        require(read(output) == combined, 'no-build publish cannot refresh changed metadata/source provenance')
        for relative, expected in combined['sourceSha256'].items():
            require(hashlib.sha256(normalized(contained(host_output, combined['sourceFiles'][relative]))).hexdigest() == expected,
                    'retained Host source snapshot differs: ' + relative)
        for origin in combined['origins']:
            require(digest(contained(host_output, origin['assembly']['name'])) == origin['assembly']['sha256'],
                    'retained native implementation differs')
            for relative, expected in origin['source']['files'].items():
                require(hashlib.sha256(normalized(contained(host_output, relative))).hexdigest() == expected,
                        'retained native source snapshot differs')
        print('No-build Host settings provenance validated without refreshing it.')
        return
    for relative, data in copies.items():
        path = contained(host_output, relative)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    for relative, expected in combined['sourceSha256'].items():
        require(hashlib.sha256(normalized(contained(host_output, combined['sourceFiles'][relative]))).hexdigest() == expected,
                'Host source snapshot differs before admission')
    require({path.relative_to(host_output).as_posix() for path in snapshot_root.rglob('*') if path.is_file()}
            == expected_snapshots, 'source snapshot inventory differs before admission')
    write(output, combined)
    print('Complete compiled Host transport/native settings metadata assembled: ' + str(output))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--transport', type=Path, required=True)
    parser.add_argument('--vendor', type=Path, required=True)
    parser.add_argument('--host-output', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--version', required=True)
    parser.add_argument('--validate-only', action='store_true')
    assemble(parser.parse_args())


if __name__ == '__main__':
    main()
