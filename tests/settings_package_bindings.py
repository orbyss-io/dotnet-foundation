"""Bind inspected settings packages to cold native restore and executed DLL bytes.

This helper inspects immutable package members only. It never loads an assembly or
starts application, storage or identity services. Its self-test uses byte fixtures;
the owning native probes call verify after their actual cold restore/build.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path, PurePosixPath
import tempfile
import zipfile
from xml.etree import ElementTree as ET

MAX_PACKAGE = 1_073_741_824
MAX_ASSEMBLY = 268_435_456
MAX_METADATA = 2_097_152
MAX_ASSETS = 33_554_432
SETTINGS_MEMBER = 'orbyss-foundation/settings.json'


def require(condition, message):
    if not condition:
        raise ValueError('Settings native package binding: ' + message)


def digest(path: Path, maximum=MAX_PACKAGE):
    require(path.is_file() and 0 < path.stat().st_size <= maximum, 'missing/excessive file: ' + str(path))
    value = hashlib.sha256()
    count = 0
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(65_536), b''):
            count += len(block)
            require(count <= maximum, 'file grew beyond admitted limit')
            value.update(block)
    return value.hexdigest()


def contained(root: Path, name: str):
    require(isinstance(name, str) and name and '\\' not in name and ':' not in name, 'unsafe retained member path')
    relative = PurePosixPath(name)
    require(not relative.is_absolute() and relative.as_posix() == name and
            all(part not in ('', '.', '..') for part in relative.parts), 'noncanonical retained member path')
    path = root.joinpath(*relative.parts).resolve()
    require(path.is_relative_to(root.resolve()), 'retained member escapes owned directory')
    return path


def loads(payload):
    def pairs(items):
        value = {}
        for key, item in items:
            require(key not in value, 'duplicate JSON member')
            value[key] = item
        return value
    def nonfinite(_):
        raise ValueError('Settings native package binding: nonfinite JSON value')
    return json.loads(payload.decode('utf-8-sig'), object_pairs_hook=pairs, parse_constant=nonfinite)


def member(archive, name, maximum):
    info = archive.getinfo(name)
    require(0 < info.file_size <= maximum, 'missing/excessive package member: ' + name)
    with archive.open(info) as stream:
        payload = stream.read(maximum + 1)
    require(len(payload) <= maximum, 'expanded package member exceeds admitted limit')
    return payload


def member_digest(archive, name):
    info = archive.getinfo(name)
    require(0 < info.file_size <= MAX_ASSEMBLY, 'missing/excessive implementation DLL')
    value = hashlib.sha256()
    with archive.open(info) as stream:
        for block in iter(lambda: stream.read(65_536), b''):
            value.update(block)
    return value.hexdigest()


def inspect(package: Path) -> dict:
    """Capture package identity plus actual companion/implementation hashes."""
    archive_sha = digest(package)
    with zipfile.ZipFile(package) as archive:
        names = archive.namelist()
        require(len(names) <= 100_000 and len(set(names)) == len(names), 'ambiguous/excessive package entries')
        for name in names:
            if not name.endswith('/'):
                contained(package.parent, name)
        nuspecs = [name for name in names if '/' not in name and name.endswith('.nuspec')]
        require(len(nuspecs) == 1, 'exactly one native nuspec is required')
        document = ET.fromstring(member(archive, nuspecs[0], MAX_METADATA))
        def field(name):
            values = [item.text for item in document.iter() if item.tag.split('}')[-1] == name]
            require(len(values) == 1 and isinstance(values[0], str) and values[0], 'native nuspec identity is ambiguous')
            return values[0]
        identity, version = field('id'), field('version')
        payload = member(archive, SETTINGS_MEMBER, MAX_METADATA)
        metadata = loads(payload)
        require(type(metadata.get('schemaVersion')) is int and metadata['schemaVersion'] in (1, 2) and
                metadata.get('packageId') == identity and metadata.get('packageVersion') == version,
                'companion identity/version differs from native nuspec')
        assembly = metadata.get('assembly')
        require(isinstance(assembly, dict) and set(assembly) == {'name', 'sha256'}, 'compiled assembly binding is required')
        require(isinstance(assembly['name'], str) and '/' not in assembly['name'] and assembly['name'].endswith('.dll'),
                'invalid implementation assembly name')
        dll_member = 'lib/net10.0/' + assembly['name']
        dll_sha = member_digest(archive, dll_member)
        require(dll_sha == assembly['sha256'], 'companion differs from actual implementation DLL')
    return {'packageId': identity, 'packageVersion': version, 'archiveSha256': archive_sha,
            'metadataSha256': hashlib.sha256(payload).hexdigest(),
            'assembly': {'name': assembly['name'], 'member': dll_member, 'sha256': dll_sha}}


def verify(record: dict, package: Path, assets: Path, cache: Path, runtime_directory: Path) -> None:
    """Require inspected feed, selected native cache and executed copy to agree."""
    require(inspect(package) == record, 'inspected feed archive changed before native acceptance')
    require(assets.is_file() and 0 < assets.stat().st_size <= MAX_ASSETS, 'missing/excessive native assets')
    selected = loads(assets.read_bytes())
    libraries = selected.get('libraries')
    require(isinstance(libraries, dict), 'native assets library inventory is missing')
    keys = [key for key in libraries if key.partition('/')[0].casefold() == record['packageId'].casefold()]
    require(len(keys) == 1 and keys[0].partition('/')[2] == record['packageVersion'],
            'native assets selected missing/ambiguous/different package version')
    library = libraries[keys[0]]
    expected_path = record['packageId'].lower() + '/' + record['packageVersion'].lower()
    require(library.get('type') == 'package' and library.get('path') == expected_path,
            'native library is not the exact cache package')
    folders = selected.get('packageFolders')
    require(isinstance(folders, dict) and cache.resolve() in {Path(folder).resolve() for folder in folders},
            'native assets do not name the owned cold cache')
    targets = selected.get('targets')
    require(isinstance(targets, dict) and targets, 'native target graph is missing')
    resolved = [target[keys[0]] for target in targets.values() if keys[0] in target]
    require(resolved and all(item.get('type') == 'package' and
            record['assembly']['member'] in item.get('runtime', {}) for item in resolved),
            'native target does not select the inspected net10 implementation')
    package_directory = contained(cache, expected_path)
    cache_archive = package_directory / (record['packageId'].lower() + '.' + record['packageVersion'].lower() + '.nupkg')
    require(inspect(cache_archive) == record, 'cold native cache archive differs from inspected feed')
    require(digest(contained(package_directory, record['assembly']['member']), MAX_ASSEMBLY) == record['assembly']['sha256'],
            'native cache extracted implementation differs from inspected archive')
    require(digest(contained(runtime_directory, record['assembly']['name']), MAX_ASSEMBLY) == record['assembly']['sha256'],
            'actual executed runtime DLL differs from inspected archive')


def self_test():
    with tempfile.TemporaryDirectory(prefix='settings-native-binding-') as directory:
        root = Path(directory)
        cache, runtime = root / 'cache', root / 'runtime'
        package_directory = cache / 'owner.options/1.2.3'; package_directory.mkdir(parents=True)
        runtime.mkdir()
        original = root / 'Owner.Options.1.2.3.nupkg'
        dll = b'synthetic implementation bytes; native qualification is performed by owning probes'
        metadata = {'schemaVersion': 2, 'packageId': 'Owner.Options', 'packageVersion': '1.2.3',
                    'assembly': {'name': 'Owner.Options.dll', 'sha256': hashlib.sha256(dll).hexdigest()}}
        def pack(path, version='1.2.3', assembly=dll, payload=metadata):
            with zipfile.ZipFile(path, 'w') as archive:
                archive.writestr('Owner.Options.nuspec', '<package><metadata><id>Owner.Options</id><version>' + version + '</version></metadata></package>')
                archive.writestr(SETTINGS_MEMBER, json.dumps(payload).encode())
                archive.writestr('lib/net10.0/Owner.Options.dll', assembly)
        pack(original)
        cached = package_directory / 'owner.options.1.2.3.nupkg'; cached.write_bytes(original.read_bytes())
        extracted = package_directory / 'lib/net10.0/Owner.Options.dll'; extracted.parent.mkdir(parents=True); extracted.write_bytes(dll)
        executed = runtime / 'Owner.Options.dll'; executed.write_bytes(dll)
        native = {'libraries': {'Owner.Options/1.2.3': {'type': 'package', 'path': 'owner.options/1.2.3'}},
                  'targets': {'net10.0': {'Owner.Options/1.2.3': {'type': 'package', 'runtime': {'lib/net10.0/Owner.Options.dll': {}}}}},
                  'packageFolders': {str(cache): {}}}
        assets = root / 'project.assets.json'; assets.write_text(json.dumps(native), encoding='utf-8')
        record = inspect(original)
        verify(record, original, assets, cache, runtime)
        checks = 1
        def rejects(operation, message):
            nonlocal checks
            try:
                operation()
            except (ValueError, KeyError, FileNotFoundError):
                checks += 1
                return
            raise AssertionError('Native binding guard admitted ' + message)
        for path in (original, cached):
            saved = path.read_bytes(); pack(path, assembly=b'changed same-version implementation')
            rejects(lambda: verify(record, original, assets, cache, runtime), 'changed same-version package')
            path.write_bytes(saved)
            changed = copy.deepcopy(metadata); changed['extra'] = 'changed companion'
            pack(path, payload=changed)
            rejects(lambda: verify(record, original, assets, cache, runtime), 'changed companion with same DLL/version')
            path.write_bytes(saved)
        for path in (extracted, executed):
            path.write_bytes(b'changed implementation')
            rejects(lambda: verify(record, original, assets, cache, runtime), 'changed extracted/runtime DLL')
            path.write_bytes(dll)
            path.unlink()
            rejects(lambda: verify(record, original, assets, cache, runtime), 'missing extracted/runtime DLL')
            path.write_bytes(dll)
        for mutation in (lambda x: x['libraries'].clear(),
                         lambda x: x['libraries'].update({'Owner.Options/1.2.4': x['libraries'].pop('Owner.Options/1.2.3')}),
                         lambda x: x['libraries'].update({'owner.options/1.2.3': copy.deepcopy(x['libraries']['Owner.Options/1.2.3'])}),
                         lambda x: x['libraries']['Owner.Options/1.2.3'].update(type='project'),
                         lambda x: x['libraries']['Owner.Options/1.2.3'].update(path='../outside'),
                         lambda x: x['packageFolders'].clear(),
                         lambda x: x['targets']['net10.0']['Owner.Options/1.2.3']['runtime'].clear()):
            changed = copy.deepcopy(native); mutation(changed); assets.write_text(json.dumps(changed), encoding='utf-8')
            rejects(lambda: verify(record, original, assets, cache, runtime), 'changed selected native assets')
        assets.write_text(json.dumps(native), encoding='utf-8')
        saved = cached.read_bytes(); cached.unlink()
        rejects(lambda: verify(record, original, assets, cache, runtime), 'missing cold archive')
        cached.write_bytes(saved)
        verify(record, original, assets, cache, runtime); checks += 1
        print('Settings native package binding guards passed (' + str(checks) + ' cases; synthetic bytes, no native execution).')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--self-test', action='store_true', required=True)
    parser.parse_args()
    self_test()
