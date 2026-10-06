"""Exercise actual no-build Host publication and reject forged intermediate settings semantics."""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import sys
import tempfile
import validate_openapi_exporter as fixture


def inventory(root: Path) -> dict:
    return {path.relative_to(root).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
            for path in root.rglob('*') if path.is_file()}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--version', default=(fixture.ROOT / 'VERSION').read_text().strip())
    args = parser.parse_args()
    project = fixture.ROOT / 'src/Orbyss.Foundation.Host/Orbyss.Foundation.Host.csproj'
    owner = project.parent
    metadata = owner / 'obj/Release/net10.0/orbyss-foundation/settings.json'
    compiled = owner / 'bin/Release/net10.0/Orbyss.Foundation.Host.dll'
    original = metadata.read_bytes()
    combined = compiled.parent / '.orbyss-foundation/host-settings.json'
    original_combined = combined.read_bytes()
    compiled_hash = hashlib.sha256(compiled.read_bytes()).hexdigest()
    parent = fixture.ROOT / 'artifacts/host-settings-publish'
    parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(dir=parent))
    output = work / 'published'
    command = ['dotnet', 'publish', str(project), '-c', 'Release', '--no-build', '--no-restore',
               '-p:UseAppHost=false', '-p:FoundationVersion=' + args.version,
               '-p:FoundationMetadataPython=' + sys.executable, '/nr:false', '-o', str(output)]
    environment = dict(os.environ, MSBUILDDISABLENODEREUSE='1', DOTNET_CLI_USE_MSBUILD_SERVER='0')
    fixture.run(command, fixture.ROOT, work / 'publish.log', env=environment)
    published = inventory(output)
    assert '.orbyss-foundation/host-settings.json' in published
    assert any(path.startswith('.orbyss-foundation/settings-sources/') for path in published)
    assert original == metadata.read_bytes()
    changed = json.loads(original)
    transport = next(contract for contract in changed['contracts'] if contract['scope'] == 'host-transport')
    transport['settings'][0]['default'] += 1
    metadata.write_text(json.dumps(changed), encoding='utf-8')
    forged = metadata.read_bytes()
    try:
        # Reassemble the forged intermediate as well: source/DLL hashes alone do not prove defaults.
        fixture.run([sys.executable, str(fixture.ROOT / 'scripts/assemble_host_settings_metadata.py'),
                     '--transport', str(metadata), '--vendor',
                     str(compiled.parent / '.orbyss-foundation/host-native.settings.json'),
                     '--host-output', str(compiled.parent), '--output', str(combined), '--version', args.version],
                    fixture.ROOT, work / 'reassemble-forged-default.log', env=environment)
        forged_combined = combined.read_bytes()
        rejection = fixture.run(command, fixture.ROOT, work / 'forged-default.log', env=environment, expected=1)
        assert 'PKSM001' in rejection, rejection
        assert metadata.read_bytes() == forged, 'No-build publication refreshed forged metadata.'
        assert combined.read_bytes() == forged_combined, 'Rejected publication refreshed the forged assembly.'
        assert inventory(output) == published, 'Rejected publication changed the accepted payload.'
        assert hashlib.sha256(compiled.read_bytes()).hexdigest() == compiled_hash
    finally:
        metadata.write_bytes(original)
        combined.write_bytes(original_combined)
    result = {'status': 'passed', 'actualNativeNoBuildPublish': True,
              'intermediateDefaultForgeryRejected': True, 'rejectedOutputPreserved': True,
              'compiledAssemblyUnchanged': True, 'packageVersion': args.version,
              'publishedFiles': published, 'hostAssemblySha256': compiled_hash}
    (work / 'results.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print('Actual no-build Host publication and forged metadata rejection passed. Evidence: ' + str(work / 'results.json'))


if __name__ == '__main__':
    main()
