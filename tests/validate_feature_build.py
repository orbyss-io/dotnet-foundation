"""Pack a real consumer using only the installed Foundation.Build package."""
import argparse
import copy
import json
import hashlib
import os
import tempfile
import zipfile
from pathlib import Path

import validate_openapi_exporter as fixture


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--package', type=Path, required=True)
    args = parser.parse_args()
    package = args.package.resolve()
    artifact = fixture.ROOT / 'artifacts/feature-build'
    artifact.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(dir=artifact))
    environment = dict(os.environ, NUGET_PACKAGES=str(work / 'nuget-cache'), DOTNET_CLI_HOME=str(work / 'dotnet-home'))
    version = fixture.package_version(package)
    (work / 'Directory.Build.props').write_text('<Project/>')
    (work / 'Directory.Build.targets').write_text('<Project/>')
    (work / 'Directory.Packages.props').write_text('<Project/>')
    project = work / 'MetadataProbe.csproj'
    project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
      <TargetFramework>net10.0</TargetFramework><PackageId>MetadataProbe</PackageId>
      <Version>1.0.0</Version><FoundationFeatureIdentity>MetadataProbe</FoundationFeatureIdentity>
      <FoundationFeatureDependencies>Companion;Companion</FoundationFeatureDependencies>
      <FoundationFeatureRoutes>/probe</FoundationFeatureRoutes>
      <FoundationComposeForOpenApi>false</FoundationComposeForOpenApi>
      </PropertyGroup><ItemGroup><PackageReference Include="Orbyss.Foundation.Build" Version="{version}" PrivateAssets="all" /></ItemGroup></Project>''')
    (work / 'Probe.cs').write_text('public sealed class Probe { }')
    config = work / 'NuGet.config'
    config.write_text(f'<configuration><packageSources><clear/><add key="local" value="{fixture.escape(str(package.parent))}"/></packageSources></configuration>')
    fixture.run(['dotnet', 'restore', str(project), '--configfile', str(config)], work, work / 'restore.log', env=environment)
    command = ['dotnet', 'pack', str(project), '-c', 'Release', '--no-restore', '--output', str(work / 'packages')]
    fixture.run(command, work, work / 'pack.log', env=environment)
    with zipfile.ZipFile(work / 'packages/MetadataProbe.1.0.0.nupkg') as archive:
        descriptor = json.loads(archive.read('orbyss-foundation/feature.json'))
        assert 'program-kit/feature.json' not in archive.namelist()
        assert b'Orbyss.Foundation.Build' not in archive.read('MetadataProbe.nuspec')
        assert descriptor['featureDependencies'] == ['Companion']
        assert descriptor['routes'] == ['/probe'] and descriptor['composeForOpenApi'] is False
    source = '''using System;
public sealed class ShellFeatureAttribute(string name) : Attribute { }
[ShellFeature("One")] public sealed class One { }
[ShellFeature("Two")] public sealed class Two { }
'''
    (work / 'Probe.cs').write_text(source, encoding='utf-8', newline='\n')
    metadata = {'schemaVersion': 2, 'packageId': 'MetadataProbe', 'features': [
        {'identity': name, 'featureDependencies': [], 'runtimeDependencies': [], 'routes': [], 'dormant': True}
        for name in ('One', 'Two')], 'sourceSha256': {'Probe.cs': hashlib.sha256(source.encode()).hexdigest()}}
    descriptor_path = work / 'feature.json'
    descriptor_path.write_text(json.dumps(metadata))
    publisher = command + ['-p:FoundationFeatureDescriptorSource=' + str(descriptor_path)]
    fixture.run(publisher, work, work / 'publisher-pack.log', env=environment)
    with zipfile.ZipFile(work / 'packages/MetadataProbe.1.0.0.nupkg') as archive:
        assert len(json.loads(archive.read('orbyss-foundation/feature.json'))['features']) == 2
    (work / 'Probe.cs').write_text(source + '// changed source\n', encoding='utf-8', newline='\n')
    diagnostic = fixture.run(publisher, work, work / 'changed-source.log', env=environment, expected=1)
    assert 'Publisher metadata source binding changed' in diagnostic
    (work / 'Probe.cs').write_text(source, encoding='utf-8', newline='\n')
    descriptor_path.write_text(json.dumps(metadata))
    fixture.run(publisher, work, work / 'restored-publisher-pack.log', env=environment)
    packed = work / 'packages/MetadataProbe.1.0.0.nupkg'
    packed_before = hashlib.sha256(packed.read_bytes()).hexdigest()
    emitted = work / 'obj/Release/net10.0/orbyss-foundation/feature.json'
    descriptor_before = emitted.read_bytes()

    invalid = []
    def case(name, mutate):
        value = copy.deepcopy(metadata)
        mutate(value)
        invalid.append((name, json.dumps(value)))

    case('empty-inventory', lambda value: value.update(features=[]))
    case('missing-inventory', lambda value: value.pop('features'))
    case('null-inventory', lambda value: value.update(features=None))
    case('mixed-schema', lambda value: value.update(identity='One'))
    case('unsupported-schema', lambda value: value.update(schemaVersion=3))
    case('wrong-package', lambda value: value.update(packageId='Other'))
    case('unknown-property', lambda value: value.update(experimentalAuthority=True))
    case('duplicate-identity', lambda value: value['features'][1].update(identity='One'))
    case('invalid-identity', lambda value: value['features'][0].update(identity='bad identity'))
    case('missing-identity', lambda value: value['features'][0].pop('identity'))
    case('missing-dependencies', lambda value: value['features'][0].pop('featureDependencies'))
    case('null-dependencies', lambda value: value['features'][0].update(featureDependencies=None))
    case('null-runtime-dependencies', lambda value: value['features'][0].update(runtimeDependencies=None))
    case('duplicate-dependencies', lambda value: value['features'][0].update(featureDependencies=['One', 'One']))
    case('missing-routes', lambda value: value['features'][0].pop('routes'))
    case('relative-route', lambda value: value['features'][0].update(routes=['probe']))
    case('duplicate-routes', lambda value: value['features'][0].update(routes=['/probe', '/probe']))
    case('nonboolean-flag', lambda value: value['features'][0].update(dormant='false'))
    case('null-flag', lambda value: value['features'][0].update(composeForOpenApi=None))
    case('unknown-feature-property', lambda value: value['features'][0].update(extra=True))
    case('prefix-without-suffixes', lambda value: value['features'][0].update(routePrefixConfigurationPath='Routes:Prefix'))
    case('suffixes-without-prefix', lambda value: value['features'][0].update(routeSuffixes=['/probe']))
    case('invalid-source-hash', lambda value: value.update(sourceSha256={'Probe.cs': 'not-a-hash'}))
    case('outside-source-path', lambda value: value.update(sourceSha256={'../Probe.cs': '0' * 64}))
    case('missing-source-inventory', lambda value: value.update(sourceSha256={}))
    case('invalid-host-version', lambda value: value.update(hostProvidedDependencies=[{'packageId': 'Example', 'minimumVersion': '*'}]))
    case('duplicate-host-dependency', lambda value: value.update(hostProvidedDependencies=[
        {'packageId': name, 'minimumVersion': '1.0.0'} for name in ('Example', 'example')]))
    invalid.append(('duplicate-json-property', json.dumps(metadata)[:-1] + ', "schemaVersion": 2}'))
    invalid.append(('malformed-json', '{'))
    single = {'schemaVersion': 1, 'packageId': 'MetadataProbe', 'identity': 'One', 'featureDependencies': [], 'routes': []}
    for name, mutate in [('single-missing-identity', lambda value: value.pop('identity')),
                         ('single-mixed-schema', lambda value: value.update(features=[]))]:
        value = copy.deepcopy(single)
        mutate(value)
        invalid.append((name, json.dumps(value)))
    results = []
    for name, text in invalid:
        descriptor_path.write_text(text)
        output = fixture.run(publisher + ['--no-build'], work, work / (name + '.log'), env=environment, expected=1)
        assert any(diagnostic in output for diagnostic in ('PKFD001',
            'Publisher metadata must bind the exact compilation source inventory',
            'Publisher metadata source binding changed')), name
        assert 'MSB4018' not in output, 'Validation raised an unhandled task exception: ' + name
        assert packed_before == hashlib.sha256(packed.read_bytes()).hexdigest(), 'Invalid metadata replaced packed output: ' + name
        assert emitted.read_bytes() == descriptor_before, 'Invalid metadata replaced descriptor output: ' + name
        results.append({'case': name, 'status': 'rejected', 'log': str(work / (name + '.log'))})
    # Both supported schemas and configurable route declarations remain packable.
    for name, value in [('schema-one', single), ('schema-two', copy.deepcopy(metadata))]:
        if name == 'schema-two':
            value['features'][0].update(routePrefixConfigurationPath='Routes:Prefix', routeSuffixes=['/probe'])
        descriptor_path.write_text(json.dumps(value))
        fixture.run(publisher + ['--no-build'], work, work / (name + '.log'), env=environment)
        with zipfile.ZipFile(packed) as archive:
            assert json.loads(archive.read('orbyss-foundation/feature.json'))['schemaVersion'] == value['schemaVersion']
            assert 'program-kit/feature.json' not in archive.namelist()
            assert b'Orbyss.Foundation.Build' not in archive.read('MetadataProbe.nuspec')
    (work / 'descriptor-validation.json').write_text(json.dumps({'package': str(package),
        'packageSha256': hashlib.sha256(package.read_bytes()).hexdigest(), 'cases': results,
        'invalidOutputPreserved': True, 'supportedSchemasPassed': [1, 2]}, indent=2) + '\n')
    print(f'Installed build package rejects {len(results)} invalid descriptors, preserves outputs, and packs both canonical schemas.')
    print('Evidence: ' + str(work / 'descriptor-validation.json'))


if __name__ == '__main__': main()
