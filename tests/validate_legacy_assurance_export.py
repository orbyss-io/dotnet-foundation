"""Real immutable Assurance packages must be admitted by the corrected packed tool."""
import argparse
import json
import shutil
import tempfile
import urllib.request
from pathlib import Path

import validate_openapi_exporter as fixture


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--package', type=Path, required=True)
    args = parser.parse_args()
    artifacts = fixture.ROOT / 'artifacts/assurance-export'
    artifacts.mkdir(parents=True, exist_ok=True)
    root = Path(tempfile.mkdtemp(dir=artifacts))
    original = fixture.create_feature(root,historical_lock=fixture.ROOT/'tests/openapi-exporter/legacy-public/packages.lock.json')
    public = root / 'Orbyss.Foundation.OpenApi.Exporter.0.2.3.nupkg'
    with urllib.request.urlopen('https://api.nuget.org/v3-flatcontainer/orbyss.foundation.openapi.exporter/0.2.3/orbyss.foundation.openapi.exporter.0.2.3.nupkg') as response:
        public.write_bytes(response.read())
    for version in ('0.2.2', '0.2.3'):
        work = root / ('assurance-' + version)
        work.mkdir()
        closure = work / 'closure'
        shutil.copytree(original, closure)
        identity = 'Orbyss.Foundation.Authentication.Assurance'
        package = closure / f'{identity}.{version}.nupkg'
        with urllib.request.urlopen(f'https://api.nuget.org/v3-flatcontainer/{identity.lower()}/{version}/{identity.lower()}.{version}.nupkg') as response:
            package.write_bytes(response.read())
        # Preserve the normal matching/mismatching tool admission controls.
        fixture.check_tool(args.package.resolve(), work, closure)
        tool_version = fixture.package_version(args.package)
        case = work / tool_version
        shells = json.loads((case / 'shells.json').read_text())
        shells['CShells']['Shells']['probe']['Features'][identity] = True
        fixture.write_json(case / 'shells.json', shells)
        contract = json.loads((case / 'contract.json').read_text())
        contract['producer']['version'] = tool_version
        fixture.write_json(case / 'contract.json', contract)
        arguments = ['dotnet', 'tool', 'run', fixture.COMMAND, '--', '--repository', str(case),
                     '--packages', str(closure), '--shells', str(case / 'shells.json'),
                     '--hostsettings', str(case / 'hostsettings.json'), '--contract', str(case / 'contract.json'),
                     '--output', str(case / 'assurance.json'), '--evidence', str(case / 'assurance.evidence.json')]
        env = dict(fixture.os.environ, NUGET_PACKAGES=str(case / 'nuget-cache'), DOTNET_CLI_HOME=str(case / 'dotnet-home'))
        fixture.run(arguments, case, case / 'assurance.log', env=env)
        receipt = json.loads((case / 'assurance.evidence.json').read_text())
        assert receipt['composedFeatures'] == [fixture.FEATURE_ID]
        # The immutable public failure remains an explicit regression case.
        if version == '0.2.3':
            legacy_work = work / 'public-regression'
            legacy_work.mkdir()
            fixture.check_tool(public, legacy_work, closure)
            legacy_case = legacy_work / '0.2.3'
            fixture.write_json(legacy_case / 'shells.json', shells)
            contract['producer']['version'] = '0.2.3'
            fixture.write_json(legacy_case / 'contract.json', contract)
            legacy_arguments = ['dotnet', 'tool', 'run', fixture.COMMAND, '--', '--repository', str(legacy_case),
                '--packages', str(closure), '--shells', str(legacy_case / 'shells.json'),
                '--hostsettings', str(legacy_case / 'hostsettings.json'), '--contract', str(legacy_case / 'contract.json'),
                '--output', str(legacy_case / 'assurance.json'), '--evidence', str(legacy_case / 'assurance.evidence.json')]
            legacy_env = dict(fixture.os.environ, NUGET_PACKAGES=str(legacy_case / 'nuget-cache'), DOTNET_CLI_HOME=str(legacy_case / 'dotnet-home'))
            diagnostic = fixture.run(legacy_arguments, legacy_case, legacy_case / 'assurance.log', env=legacy_env, expected=2)
            assert 'activated features have no unique staged package descriptor: ' + identity in diagnostic
            assert not (legacy_case / 'assurance.json').exists()
    print('Assurance 0.2.2/0.2.3 activated through the corrected packed exporter; version mismatch controls passed.')


if __name__ == '__main__': main()
