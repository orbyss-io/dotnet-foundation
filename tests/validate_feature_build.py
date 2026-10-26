"""Pack a real consumer using only the installed Foundation.Build package."""
import argparse
import base64
import copy
import json
import hashlib
import os
import tempfile
import zipfile
from xml.etree import ElementTree
from pathlib import Path

import validate_openapi_exporter as fixture


def validate_private_ef_design(work, package, environment, version):
    """Fresh native packing keeps EF runtime while excluding private design-only closure."""
    probe = work / 'ef-runtime'
    probe.mkdir()
    versions = {item.attrib['Include']: item.attrib['Version'] for item in
                ElementTree.parse(fixture.ROOT / 'Directory.Packages.props').iter('PackageVersion')}
    ef_version = versions['Microsoft.EntityFrameworkCore']
    tooling = probe / 'tooling'
    tooling.mkdir()
    (tooling / 'BuildProbe.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
      <TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup>
      <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="{ef_version}"/>
      </ItemGroup></Project>''')
    runtime_project = probe / 'runtime'
    runtime_project.mkdir()
    (runtime_project / 'RuntimeProbe.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
      <TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup>
      <PackageReference Include="Microsoft.EntityFrameworkCore.Abstractions" Version="{ef_version}"/>
      </ItemGroup></Project>''')
    project = probe / 'EfMetadataProbe.csproj'
    project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
      <TargetFramework>net10.0</TargetFramework><Version>1.0.0</Version>
      <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
      <PackageId>EfMetadataProbe</PackageId><FoundationFeatureIdentity>EfMetadataProbe</FoundationFeatureIdentity>
      </PropertyGroup><ItemGroup>
      <Compile Include="Probe.cs"/>
      <ProjectReference Include="tooling/BuildProbe.csproj" ReferenceOutputAssembly="false" OutputItemType="Analyzer"/>
      <ProjectReference Include="runtime/RuntimeProbe.csproj"/>
      <PackageReference Include="Orbyss.Foundation.Build" Version="{version}" PrivateAssets="all" IncludeAssets="build;buildtransitive"/>
      <PackageReference Include="Microsoft.EntityFrameworkCore" Version="{ef_version}" PrivateAssets="all"/>
      <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="{ef_version}" PrivateAssets="all"
         IncludeAssets="runtime;build;native;contentfiles;analyzers;buildtransitive"/>
      </ItemGroup></Project>''')
    (probe / 'Probe.cs').write_text('public sealed class ProbeContext : Microsoft.EntityFrameworkCore.DbContext { }')
    config = probe / 'NuGet.config'
    config.write_text(f'''<configuration><packageSources><clear/>
      <add key="local" value="{fixture.escape(str(package.parent))}"/>
      <add key="nuget" value="https://api.nuget.org/v3/index.json"/>
      </packageSources></configuration>''')
    fixture.run(['dotnet', 'restore', str(project), '--configfile', str(config)], probe, probe / 'restore.log', env=environment)
    command = ['dotnet', 'pack', str(project), '-c', 'Release', '--no-restore', '--output', str(probe / 'packages')]
    fixture.run(command, probe, probe / 'pack.log', env=environment)
    packed = probe / 'packages/EfMetadataProbe.1.0.0.nupkg'
    def inspect():
        with zipfile.ZipFile(packed) as archive:
            descriptor = json.loads(archive.read('orbyss-foundation/feature.json'))
            assert b'Microsoft.EntityFrameworkCore.Design' not in archive.read('EfMetadataProbe.nuspec')
        requirements = {item['packageId']: item['minimumVersion'] for item in descriptor['hostProvidedDependencies']}
        assert requirements['Microsoft.EntityFrameworkCore'] == ef_version, requirements
        assert requirements['Microsoft.EntityFrameworkCore.Abstractions'] == ef_version, requirements
        assert 'Microsoft.Extensions.DependencyInjection.Abstractions' in requirements, requirements
        assert not {'Microsoft.EntityFrameworkCore.Design', 'Humanizer.Core', 'Microsoft.CodeAnalysis.Common'} & requirements.keys(), requirements
        return descriptor
    descriptor = inspect()
    fixture.run(command + ['--no-build'], probe, probe / 'repeat-no-build-pack.log', env=environment)
    assert inspect() == descriptor
    (probe / 'ef-metadata-validation.json').write_text(json.dumps({'package': str(package),
        'efVersion': ef_version, 'freshAndNoBuildPacking': True,
        'nativeAnalyzerProjectReferenceExcluded': True, 'nativeRuntimeProjectReferencePreserved': True,
        'descriptor': descriptor}, indent=2) + '\n')


def validate_runtime_graph(work, package, environment, version):
    """Invoke the installed task with independent restored graph/nuspec fixtures."""
    graph_work = work / 'runtime-graph'
    graph_work.mkdir()
    cache = graph_work / 'packages'
    libraries, target = {}, {}
    def node(name, dependencies=(), development=False, kind='package', version='1.2.3'):
        key = name + '/' + version
        target[key] = {'type': kind, 'runtime': {'lib/net10.0/' + name + '.dll': {}},
                       'dependencies': {child: '1.2.3' for child in dependencies}}
        libraries[key] = {'type': kind}
        if kind == 'project':
            libraries[key]['msbuildProject'] = name + '.csproj'
        if kind == 'package':
            relative = name.lower() + '/' + version
            libraries[key]['path'] = relative
            directory = cache / relative
            directory.mkdir(parents=True)
            spec = ('<package><metadata><id>' + name + '</id><version>' + version + '</version>'
                + ('<developmentDependency>true</developmentDependency>' if development else '')
                + '</metadata></package>')
            (directory / (name.lower() + '.nuspec')).write_text(spec)
            archive = directory / (name.lower() + '.' + version + '.nupkg')
            with zipfile.ZipFile(archive, 'w') as package_file:
                package_file.writestr(name + '.nuspec', spec)
            libraries[key]['sha512'] = base64.b64encode(hashlib.sha512(archive.read_bytes()).digest()).decode()
    node('Tool.Design', ['Tool.Only', 'Shared.Runtime'], development=True)
    node('Tool.Only')
    node('Shared.Runtime')
    node('Private.Runtime', ['Private.Transitive', 'Shared.Runtime'])
    node('Private.Transitive')
    node('Build.Only', ['Build.Transitive'])
    node('Build.Transitive')
    node('ProjectFeature', ['Project.Transitive'], kind='project')
    node('Project.Transitive')
    node('AnalyzerFeature', ['Analyzer.Transitive'], kind='project')
    node('Analyzer.Transitive')
    assets = {'version': 3, 'targets': {'net9.0': {}, 'net10.0': target}, 'libraries': libraries,
              'packageFolders': {str(cache): {}}, 'project': {'frameworks': {'net10.0': {'dependencies': {
                  'Tool.Design': {'suppressParent': 'All', 'include': 'Runtime, Build'},
                  'Private.Runtime': {'suppressParent': 'All'},
                  'Build.Only': {'include': 'Build, BuildTransitive'}}}}}}
    graph_path = graph_work / 'project.assets.json'
    graph_path.write_text(json.dumps(assets))
    installed = Path(environment['NUGET_PACKAGES']) / 'orbyss.foundation.build' / version / 'tasks/net10.0/Orbyss.Foundation.Build.dll'
    runner = graph_work / 'Graph.proj'
    runner.write_text(f'''<Project><ItemGroup Condition="'$(ProbeProjects)' != 'false'">
      <ProjectReference Include="ProjectFeature.csproj"/>
      <ProjectReference Include="AnalyzerFeature.csproj" ReferenceOutputAssembly="false" OutputItemType="Analyzer"/>
      </ItemGroup><UsingTask TaskName="Orbyss.Foundation.Build.EmitFeatureDescriptor"
      AssemblyFile="{fixture.escape(str(installed))}"/><Target Name="Emit">
      <Orbyss.Foundation.Build.EmitFeatureDescriptor OutputFile="{fixture.escape(str(graph_work / 'feature.json'))}"
       Identity="GraphProbe" PackageId="GraphProbe" AssetsFile="{fixture.escape(str(graph_path))}"
       ProjectDirectory="{fixture.escape(str(graph_work))}" ProjectReferences="@(ProjectReference)"
       TargetFramework="$(ProbeFramework)" RuntimeIdentifier="$(ProbeRuntime)" DescriptorSource="$(ProbeSource)"/>
      </Target></Project>''')
    command = ['dotnet', 'msbuild', str(runner), '-t:Emit', '-p:ProbeFramework=net10.0']
    fixture.run(command, graph_work, graph_work / 'private-runtime.log', env=environment)
    output = graph_work / 'feature.json'
    requirements = json.loads(output.read_text())['hostProvidedDependencies']
    expected = {'Private.Runtime': '1.2.3', 'Private.Transitive': '1.2.3',
                'Shared.Runtime': '1.2.3', 'Project.Transitive': '1.2.3'}
    assert {item['packageId']: item['minimumVersion'] for item in requirements} == expected, requirements
    declared = {'schemaVersion': 1, 'identity': 'GraphProbe', 'packageId': 'GraphProbe',
                'featureDependencies': [], 'routes': [],
                'hostProvidedDependencies': [{'packageId': 'Explicit.Runtime', 'minimumVersion': '7.8.9'}]}
    source = graph_work / 'declared.json'
    source.write_text(json.dumps(declared))
    fixture.run(command + ['-p:ProbeSource=' + str(source)], graph_work, graph_work / 'explicit-runtime.log', env=environment)
    assert {'packageId': 'Explicit.Runtime', 'minimumVersion': '7.8.9'} in json.loads(output.read_text())['hostProvidedDependencies']
    for minimum, expected_minimum in [('0.1.0', '1.2.3'), ('9.8.7', '9.8.7')]:
        declared['hostProvidedDependencies'] = [{'packageId': 'Private.Runtime', 'minimumVersion': minimum}]
        source.write_text(json.dumps(declared))
        fixture.run(command + ['-p:ProbeSource=' + str(source)], graph_work, graph_work / ('overlap-' + minimum + '.log'), env=environment)
        assert {item['packageId']: item['minimumVersion'] for item in json.loads(output.read_text())['hostProvidedDependencies']}['Private.Runtime'] == expected_minimum
    before = output.read_bytes()
    cases = []
    def reject(name, value, arguments=None):
        graph_path.write_text(json.dumps(value))
        diagnostic = fixture.run(arguments or command, graph_work, graph_work / (name + '.log'), env=environment, expected=1)
        assert 'PKFD001' in diagnostic and 'MSB4018' not in diagnostic, name
        assert output.read_bytes() == before, name
        cases.append(name)
    reject('ambiguous-target', assets, ['dotnet', 'msbuild', str(runner), '-t:Emit'])
    broken = copy.deepcopy(assets)
    broken['targets']['net10.0']['Private.Runtime/1.2.3']['dependencies']['Missing.Runtime'] = '1.2.3'
    reject('missing-graph-edge', broken)
    broken = copy.deepcopy(assets)
    broken['targets']['net10.0']['Private.Runtime/1.2.3']['dependencies']['Tool.Design'] = '1.2.3'
    reject('development-package-required-at-runtime', broken)
    broken = copy.deepcopy(assets)
    broken['targets']['net10.0']['Private.Runtime/9.8.7'] = copy.deepcopy(target['Private.Runtime/1.2.3'])
    reject('ambiguous-package', broken)
    broken = copy.deepcopy(assets)
    broken['libraries'].pop('Private.Transitive/1.2.3')
    reject('missing-library', broken)
    broken = copy.deepcopy(assets)
    broken['targets']['net10.0']['Private.Runtime/1.2.3']['dependencies'] = []
    reject('malformed-dependencies', broken)
    broken = copy.deepcopy(assets)
    broken['project']['frameworks']['net10.0']['dependencies'] = None
    reject('malformed-direct-dependencies', broken)
    # Mutable extracted metadata cannot hide a genuine private runtime root.
    extracted = cache / 'private.runtime/1.2.3/private.runtime.nuspec'
    extracted.write_text('<package><metadata><id>Private.Runtime</id><version>1.2.3</version><developmentDependency>true</developmentDependency></metadata></package>')
    graph_path.write_text(json.dumps(assets))
    fixture.run(command, graph_work, graph_work / 'extracted-metadata-tamper.log', env=environment)
    assert {item['packageId']: item['minimumVersion'] for item in json.loads(output.read_text())['hostProvidedDependencies']} == expected
    before = output.read_bytes()
    archive = cache / 'private.runtime/1.2.3/private.runtime.1.2.3.nupkg'
    original_archive = archive.read_bytes()
    archive.write_bytes(original_archive + b'tampered')
    reject('archive-integrity-tamper', assets)
    archive.write_bytes(original_archive)
    # Missing optional inventories are valid in native no-package and project-only graphs.
    empty = {'targets': {'net10.0': {}}, 'libraries': {}, 'project': {'frameworks': {'net10.0': {}}}}
    graph_path.write_text(json.dumps(empty))
    fixture.run(command + ['-p:ProbeProjects=false'], graph_work, graph_work / 'no-packages.log', env=environment)
    assert json.loads(output.read_text())['hostProvidedDependencies'] == []
    projects = copy.deepcopy(assets)
    projects['project']['frameworks']['net10.0'].pop('dependencies')
    graph_path.write_text(json.dumps(projects))
    fixture.run(command, graph_work, graph_work / 'project-only.log', env=environment)
    assert [item['packageId'] for item in json.loads(output.read_text())['hostProvidedDependencies']] == ['Project.Transitive']
    alias = copy.deepcopy(assets)
    alias['project']['frameworks'] = {'net10.0-windows7.0': dict(alias['project']['frameworks']['net10.0'], targetAlias='net10.0-windows')}
    alias['targets'] = {'net10.0-windows7.0': alias['targets']['net10.0']}
    graph_path.write_text(json.dumps(alias))
    fixture.run(command[:-1] + ['-p:ProbeFramework=net10.0-windows'], graph_work, graph_work / 'target-alias.log', env=environment)
    assert {item['packageId']: item['minimumVersion'] for item in json.loads(output.read_text())['hostProvidedDependencies']} == expected
    node('Native.Runtime', ['Managed.Only'])
    node('Managed.Only')
    target['Native.Runtime/1.2.3']['native'] = {'runtimes/win-x64/native/probe.dll': {}}
    assets['project']['frameworks']['net10.0']['dependencies']['Native.Runtime'] = {'include': 'Native', 'suppressParent': 'All'}
    graph_path.write_text(json.dumps(assets))
    fixture.run(command, graph_work, graph_work / 'native-only.log', env=environment)
    native_expected = dict(expected, **{'Native.Runtime': '1.2.3'})
    assert {item['packageId']: item['minimumVersion'] for item in json.loads(output.read_text())['hostProvidedDependencies']} == native_expected
    excluded = copy.deepcopy(assets)
    excluded['project']['frameworks']['net10.0']['dependencies']['Native.Runtime'] = {'include': 'All', 'exclude': 'Runtime', 'suppressParent': 'All'}
    graph_path.write_text(json.dumps(excluded))
    fixture.run(command, graph_work, graph_work / 'exclude-runtime.log', env=environment)
    assert {item['packageId']: item['minimumVersion'] for item in json.loads(output.read_text())['hostProvidedDependencies']} == native_expected
    graph_path.write_text(json.dumps(assets))
    rid = copy.deepcopy(target)
    rid['Private.Runtime/9.8.7'] = rid.pop('Private.Runtime/1.2.3')
    node('Private.Runtime', ['Private.Transitive', 'Shared.Runtime'], version='9.8.7')
    assets['targets']['net10.0/win-x64'] = rid
    graph_path.write_text(json.dumps(assets))
    fixture.run(command + ['-p:ProbeRuntime=win-x64'], graph_work, graph_work / 'exact-rid.log', env=environment)
    actual = {item['packageId']: item['minimumVersion'] for item in json.loads(output.read_text())['hostProvidedDependencies']}
    assert actual == dict(native_expected, **{'Private.Runtime': '9.8.7'}), actual
    (graph_work / 'graph-validation.json').write_text(json.dumps({'package': str(package),
        'privateAndSharedRuntimePreserved': expected, 'rejections': cases, 'exactRidVersion': actual}, indent=2) + '\n')


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
      <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
      <Version>1.0.0</Version><FoundationFeatureIdentity>MetadataProbe</FoundationFeatureIdentity>
      <FoundationFeatureDependencies>Companion;Companion</FoundationFeatureDependencies>
      <FoundationFeatureRoutes>/probe</FoundationFeatureRoutes>
      <FoundationComposeForOpenApi>false</FoundationComposeForOpenApi>
      </PropertyGroup><ItemGroup><Compile Include="Probe.cs"/><PackageReference Include="Orbyss.Foundation.Build" Version="{version}" PrivateAssets="all" /></ItemGroup></Project>''')
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
        assert descriptor['hostProvidedDependencies'] == [], 'Build task dependencies leaked into runtime metadata'
    validate_runtime_graph(work, package, environment, version)
    validate_private_ef_design(work, package, environment, version)
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
