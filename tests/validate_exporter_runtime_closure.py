"""Installed exporter acceptance against immutable public runtime closures; no consumer dependency."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import shutil
import tempfile
import urllib.request
import zipfile
import validate_openapi_exporter as fixture

ROOT = fixture.ROOT
PACKAGE = 'Orbyss.Foundation.OpenApi.ClosureProbe'
FEATURES = ['ClosureProbe', 'ClosureProbe.EffectOnly', 'Orbyss.Foundation.Json.AspNetCore', 'Orbyss.Foundation.Web.OpenApi']


def public_package(identity, version, destination):
    name = identity.lower()
    with urllib.request.urlopen(f'https://api.nuget.org/v3-flatcontainer/{name}/{version}/{name}.{version}.nupkg', timeout=60) as response:
        content = response.read(64 * 1024 * 1024 + 1)
    assert len(content) <= 64 * 1024 * 1024
    destination.write_bytes(content)
    return destination


def create_closure(work, runtime, configuration):
    project = work / 'feature'
    project.mkdir(parents=True)
    for name in ('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props'):
        (work / name).write_text('<Project/>\n')
    source = ROOT / 'tests/openapi-exporter/ClosureProbeFeature.cs'
    shutil.copy2(source, project / source.name)
    feature = lambda identity, dependencies, routes, compose: {'identity': identity, 'featureDependencies': dependencies,
        'runtimeDependencies': [], 'routes': routes, 'dormant': False, 'composeForOpenApi': compose, 'requiresContractCoverage': compose}
    fixture.write_json(project / 'feature.json', {'schemaVersion': 2, 'packageId': PACKAGE, 'features': [
        feature('ClosureProbe', FEATURES[1:], ['/probe'], True), feature('ClosureProbe.EffectOnly', [], [], False)],
        'sourceSha256': {source.name: hashlib.sha256(source.read_bytes().replace(b'\r\n', b'\n')).hexdigest()}})
    references = ''.join(f'<PackageReference Include="{name}" Version="{version}"{private}/>' for name, version, private in (
        ('Orbyss.Foundation.Json.AspNetCore', runtime, ''), ('Orbyss.Foundation.Web.OpenApi', runtime, ''),
        ('Orbyss.Foundation.Build', '0.3.1', ' PrivateAssets="all"'),
        ('CShells.Abstractions', '0.0.30', ' PrivateAssets="all"'), ('CShells.AspNetCore.Abstractions', '0.0.30', ' PrivateAssets="all"')))
    csproj = project / (PACKAGE + '.csproj')
    csproj.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile><Version>1.0.0-fixture.1</Version><FoundationFeatureDescriptorSource>feature.json</FoundationFeatureDescriptorSource></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/>' + references + '</ItemGroup></Project>')
    fixture.run(['dotnet', 'restore', str(csproj), '--configfile', str(ROOT / 'NuGet.config')], ROOT, work / 'restore.log')
    fixture.run(['dotnet', 'restore', str(csproj), '--locked-mode', '--configfile', str(ROOT / 'NuGet.config')], ROOT, work / 'locked-restore.log')
    closure = work / 'closure'
    fixture.run(['dotnet', 'pack', str(csproj), '-c', configuration, '--no-restore', '--output', str(closure)], ROOT, work / 'pack.log')
    assets = json.loads((project / 'obj/project.assets.json').read_text())
    for name, entry in assets['libraries'].items():
        if entry['type'] != 'package': continue
        targets = [items[name] for items in assets['targets'].values() if name in items]
        if not any(target.get('runtime') for target in targets): continue
        identity, version = name.split('/')
        archives = [Path(folder) / entry['path'] / (identity.lower()+'.'+version+'.nupkg') for folder in assets['packageFolders']]
        archives = [p for p in archives if p.is_file()]
        assert len(archives) == 1
        shutil.copy2(archives[0], closure / archives[0].name)
    return closure


def install_tool(package, case):
    version = fixture.package_version(package)
    feed = case / 'feed'
    feed.mkdir(parents=True)
    shutil.copy2(package, feed / (fixture.TOOL_ID+'.'+version+'.nupkg'))
    config = case / 'NuGet.config'
    config.write_text('<configuration><packageSources><clear/><add key="local" value="'+fixture.escape(str(feed))+'"/></packageSources></configuration>')
    fixture.write_json(case / '.config/dotnet-tools.json', {'version': 1, 'isRoot': True, 'tools': {
        fixture.TOOL_ID.lower(): {'version': version, 'commands': [fixture.COMMAND]}}})
    env = dict(os.environ, NUGET_PACKAGES=str(case / 'tool-cache'), DOTNET_CLI_HOME=str(case / 'dotnet-home'),
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
    fixture.run(['dotnet', 'tool', 'restore', '--configfile', str(config)], case, case / 'tool-restore.log', env=env)
    fixture.write_json(case / 'shells.json', {'CShells': {'Shells': {'probe': {'Features': {name: True for name in FEATURES},
        'Configuration': {'WebRouting': {'Path': 'probe'}, 'Foundation': {'Json': {'Profiles': {
            'strict-request': {'Preset': 'strict-request', 'MaxBytes': 1024, 'MaxDepth': 32},
            'success-response': {'Preset': 'tolerant-response', 'MaxBytes': 2048, 'MaxDepth': 32}}}}}}}}})
    fixture.write_json(case / 'hostsettings.json', {})
    fixture.write_json(case / 'contract.json', {'schemaVersion': 1, 'identity': 'public-runtime-closure', 'documentName': 'v1',
        'shell': 'probe', 'features': FEATURES, 'producer': {'kind': fixture.TOOL_ID, 'version': version}})
    return env


def export(case, closure, env, name='export', expected=0):
    output = case / (name+'.json')
    evidence = case / (name+'.evidence.json')
    log = fixture.run(['dotnet', 'tool', 'run', fixture.COMMAND, '--', '--repository', str(case), '--packages', str(closure),
        '--shells', str(case / 'shells.json'), '--hostsettings', str(case / 'hostsettings.json'), '--contract', str(case / 'contract.json'),
        '--output', str(output), '--evidence', str(evidence)], case, case / (name+'.log'), env=env, expected=expected)
    if expected:
        assert 'PKO200' in log and not output.exists() and not evidence.exists()
    else:
        document = json.loads(output.read_text())
        assert '/probe/probe' in document['paths']
        schema = document['components']['schemas']['ProbeInput']
        assert schema['properties']['count']['type'] == 'integer'
        assert schema['properties']['amount']['type'] == 'number'
        assert schema['additionalProperties'] is False
        receipt = json.loads(evidence.read_text())
        assert 'ClosureProbe.EffectOnly' not in receipt['composedFeatures']
        assert set(receipt['composedFeatures']) == set(FEATURES) - {'ClosureProbe.EffectOnly'}
        assert receipt['producer'] == json.loads((case / 'contract.json').read_text())['producer']
        assert receipt['rawDocument']['sha256'] == hashlib.sha256(output.read_bytes()).hexdigest()
        assert 'BUSINESS_' not in log and 'EFFECT_ONLY_FEATURE_EXECUTED' not in log
    return log


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', type=Path, required=True)
    parser.add_argument('--configuration', choices=('Debug', 'Release'), default='Release')
    parser.add_argument('--public-version', help='Also download and execute this exact public tool version.')
    args = parser.parse_args()
    parent = ROOT / 'artifacts/exporter-runtime-closure'
    parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix='qualification-', dir=parent))
    package = args.package.resolve()
    with zipfile.ZipFile(package) as archive:
        assert not any(Path(name).name in ('Microsoft.AspNetCore.OpenApi.dll', 'Microsoft.OpenApi.dll') for name in archive.namelist())
    rows = []
    public = public_package(fixture.TOOL_ID, args.public_version, work / ('public-'+args.public_version+'.nupkg')) if args.public_version else None
    old = public_package(fixture.TOOL_ID, '0.2.5', work / 'regression-0.2.5.nupkg')
    for runtime in ('0.3.3', '0.3.4'):
        current = work / runtime
        closure = create_closure(current, runtime, args.configuration)
        for label, tool in [('candidate', package)] + ([('public', public)] if public else []):
            case = current / label
            env = install_tool(tool, case)
            export(case, closure, env)
            # Exact producer validation still rejects before output.
            contract = json.loads((case / 'contract.json').read_text())
            contract['producer']['version'] = '0.0.0-mismatch'
            fixture.write_json(case / 'contract.json', contract)
            export(case, closure, env, name='producer-mismatch', expected=2)
            contract['producer']['version'] = fixture.package_version(tool)
            contract['features'].remove('ClosureProbe')
            fixture.write_json(case / 'contract.json', contract)
            export(case, closure, env, name='missing-coverage', expected=2)
            rows.append({'runtime': runtime, 'tool': fixture.package_version(tool), 'mode': label, 'status': 'passed'})
        regression = current / 'old-public'
        env = install_tool(old, regression)
        diagnostic = export(regression, closure, env, expected=2 if runtime == '0.3.4' else 0)
        if runtime == '0.3.4': assert 'manifest definition does not match' in diagnostic and 'Microsoft.AspNetCore.OpenApi' in diagnostic
        invalid = current / 'invalid-metadata'
        invalid_closure = current / 'invalid-closure'
        shutil.copytree(closure, invalid_closure)
        feature_package = next(invalid_closure.glob(PACKAGE+'.*.nupkg'))
        with zipfile.ZipFile(feature_package) as archive:
            entries = {item.filename: archive.read(item) for item in archive.infolist() if not item.is_dir()}
        for name in ('orbyss-foundation/feature.json', 'program-kit/feature.json'):
            if name in entries:
                descriptor = json.loads(entries[name])
                descriptor['packageId'] = 'Different.Package.Identity'
                entries[name] = json.dumps(descriptor).encode()
        with zipfile.ZipFile(feature_package, 'w') as archive:
            for name, content in entries.items(): archive.writestr(name, content)
        env = install_tool(package, invalid)
        assert 'packageId differs' in export(invalid, invalid_closure, env, expected=2)
        missing = current / 'missing-openapi'
        missing_closure = current / 'missing-openapi-closure'
        shutil.copytree(closure, missing_closure)
        for path in missing_closure.glob('microsoft.aspnetcore.openapi.*.nupkg'): path.unlink()
        env = install_tool(package, missing)
        export(missing, missing_closure, env, expected=2)
    fixture.write_json(work / 'results.json', {'status': 'passed', 'cases': rows, 'oldPublicFailureRetained': True})
    print('Installed exporter public runtime-closure acceptance passed. Evidence: '+str(work / 'results.json'))


if __name__ == '__main__': main()
