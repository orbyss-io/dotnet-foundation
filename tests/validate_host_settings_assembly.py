"""Host metadata assembly integrity guards; synthetic inputs, no runtime or image claim."""
from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('host_settings_assembler', ROOT / 'scripts/assemble_host_settings_metadata.py')
assembler = importlib.util.module_from_spec(spec)
spec.loader.exec_module(assembler)


def json_file(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value), encoding='utf-8')


def main():
    parent = ROOT / 'artifacts/host-settings-assembly'
    parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(dir=parent))
    owner = work / 'owner'
    payload = work / 'payload'
    owner.mkdir()
    payload.mkdir()
    assembler.HOST = owner
    source = owner / 'Transport/Options.cs'
    source.parent.mkdir()
    source.write_bytes(b'public int Limit { get; set; } = 32768;\r\n')
    (owner / 'settings.source.json').write_text('{"schemaVersion":2}\n', encoding='utf-8')
    (payload / 'Orbyss.Foundation.Host.dll').write_bytes(b'synthetic-compiled-host')
    native = payload / 'CShells.dll'
    native.write_bytes(b'synthetic-native-dll')
    native_source = payload / '.orbyss-foundation/settings-sources/vendor/CShells/Options.cs.txt'
    native_source.parent.mkdir(parents=True)
    native_source.write_bytes(b'public int NativeLimit { get; set; } = 8;\n')
    origin = {'assembly': {'name': native.name, 'sha256': assembler.digest(native)},
              'source': {'files': {native_source.relative_to(payload).as_posix(): assembler.digest(native_source)}}}
    source_key = 'Transport/Options.cs'
    transport = {'schemaVersion': 2, 'packageId': 'Orbyss.Foundation.Host', 'packageVersion': '1.0.0-fixture',
                 'assembly': {'name': 'Orbyss.Foundation.Host.dll', 'sha256': assembler.digest(payload / 'Orbyss.Foundation.Host.dll')},
                 'contracts': [{'scope': 'host-transport'}, {'scope': 'host-boot'}], 'imports': [],
                 'sourceSha256': {source_key: hashlib.sha256(assembler.normalized(source)).hexdigest()}}
    vendor = {**copy.deepcopy(transport), 'contracts': [{'scope': 'host-cshells-binding'}, {'scope': 'host-nuplane-binding'}],
              'sourceSha256': {}, 'sourceFiles': {}, 'origins': [origin]}
    transport_path, vendor_path = work / 'transport.json', work / 'vendor.json'
    json_file(transport_path, transport)
    json_file(vendor_path, vendor)
    args = SimpleNamespace(transport=transport_path, vendor=vendor_path, host_output=payload,
                           output=payload / '.orbyss-foundation/host-settings.json',
                           version='1.0.0-fixture', validate_only=False)
    assembler.assemble(args)
    output = json.loads(args.output.read_text(encoding='utf-8'))
    assert {item['scope'] for item in output['contracts']} == {
        'host-transport', 'host-boot', 'host-cshells-binding', 'host-nuplane-binding'}
    assert all(item.get('sources') == output['sourceSha256'] for item in output['contracts']), \
        'Each assembled Host scope must bind the complete owning-producer source inventory'
    assert (payload / output['sourceFiles'][source_key]).read_bytes() == assembler.normalized(source)
    args.validate_only = True
    assembler.assemble(args)
    originals = {path: path.read_bytes() for path in payload.rglob('*') if path.is_file()}
    groups = ['four-producer-scopes', 'normalized-source-snapshot', 'no-build-unchanged-admission']

    def rejected(name, target, mutation, expected):
        original = target.read_bytes()
        mutation(target)
        try:
            assembler.assemble(args)
        except ValueError as error:
            assert expected in str(error), (name, str(error))
        else:
            raise AssertionError('No-build publication refreshed or admitted changed authority: ' + name)
        finally:
            target.write_bytes(original)
        assert all(path.read_bytes() == value for path, value in originals.items())
        groups.append(name)

    rejected('changed-compiled-host', payload / 'Orbyss.Foundation.Host.dll', lambda path: path.write_bytes(b'changed'),
             'compiled Host binding differs')
    rejected('changed-current-owning-source', source, lambda path: path.write_bytes(b'changed'), 'stale')
    rejected('changed-owning-declaration', owner / 'settings.source.json', lambda path: path.write_bytes(b'changed'),
             'cannot refresh')
    rejected('changed-retained-owning-source', payload / output['sourceFiles'][source_key], lambda path: path.write_bytes(b'changed'),
             'retained Host source snapshot differs')
    rejected('changed-native-dll', native, lambda path: path.write_bytes(b'changed'), 'retained native implementation differs')
    rejected('changed-native-source', native_source, lambda path: path.write_bytes(b'changed'), 'retained native source snapshot differs')
    rejected('changed-assembled-metadata', args.output,
             lambda path: json_file(path, {**output, 'sourceSha256': {}}), 'cannot refresh')
    rejected('wrong-producer-version', transport_path,
             lambda path: json_file(path, {**transport, 'packageVersion': 'wrong'}), 'producer or version differs')
    rejected('missing-transport-scope', transport_path,
             lambda path: json_file(path, {**transport, 'contracts': []}), 'transport/boot scopes incomplete')
    rejected('missing-native-scope', vendor_path,
             lambda path: json_file(path, {**vendor, 'contracts': vendor['contracts'][:1]}), 'binding scopes incomplete')
    rejected('unexpected-import', vendor_path,
             lambda path: json_file(path, {**vendor, 'imports': [{}]}), 'unexpected imported')
    rejected('unsafe-current-source', transport_path,
             lambda path: json_file(path, {**transport, 'sourceSha256': {'../escape.cs': 'a' * 64}}), 'unsafe source path')
    extra = payload / '.orbyss-foundation/settings-sources/host/obsolete.cs.txt'
    extra.parent.mkdir(parents=True, exist_ok=True)
    extra.write_bytes(b'obsolete source must not escape in the published payload')
    try:
        try:
            assembler.assemble(args)
        except ValueError as error:
            assert 'source snapshot inventory differs' in str(error)
        else:
            raise AssertionError('No-build publication admitted an unreferenced source snapshot')
    finally:
        extra.unlink()
    assert all(path.read_bytes() == value for path, value in originals.items())
    groups.append('extra-retained-source-snapshot')
    args.output = work / 'outside.json'
    try:
        assembler.assemble(args)
    except ValueError as error:
        assert 'output escapes' in str(error)
    else:
        raise AssertionError('Output path escaped the Host directory')
    assert not args.output.exists()
    groups.append('contained-output')
    json_file(work / 'results.json', {'satisfied': True, 'qualificationClaimed': False, 'groups': groups, 'count': len(groups)})
    print(f'Host settings assembly integrity guards passed ({len(groups)} groups). Evidence: {work / "results.json"}')


if __name__ == '__main__':
    main()
