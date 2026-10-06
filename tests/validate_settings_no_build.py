"""Qualify installed settings tasks with reference constants and no compilation during pack."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile
import zipfile

import validate_openapi_exporter as fixture


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def qualify(package: Path) -> Path:
    package = package.resolve()
    artifact = fixture.ROOT / 'artifacts/settings-no-build'
    artifact.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(dir=artifact))
    environment = dict(os.environ, NUGET_PACKAGES=str(work / 'cache'), DOTNET_CLI_HOME=str(work / 'dotnet-home'))
    for name in ('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props'):
        (work / name).write_text('<Project/>', encoding='utf-8')
    reference = work / 'reference'
    reference.mkdir()
    no_compile = '<Target Name="ForbidProbeCompilation" BeforeTargets="CoreCompile" Condition="\'$(SettingsProbeNoCompile)\' == \'true\'"><Error Text="No-build packing reached compilation." /></Target>'
    reference_project = reference / 'Referenced.csproj'
    reference_project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>' + no_compile + '</Project>', encoding='utf-8')
    reference_source = reference / 'Constants.cs'
    reference_source.write_text('namespace Referenced; public static class Constants { public const int Value = 17; [System.Runtime.CompilerServices.ModuleInitializer] public static void Poison() => throw new System.Exception("REFERENCE_INITIALIZER_EXECUTED"); }', encoding='utf-8')
    source = 'namespace Probe; public sealed class Options { public int Limit { get; set; } = Referenced.Constants.Value; public int FrameworkLimit { get; set; } = int.MaxValue; } public static class Startup { [System.Runtime.CompilerServices.ModuleInitializer] public static void Poison() => throw new System.Exception("PUBLISHER_INITIALIZER_EXECUTED"); }'
    source_path = work / 'Options.cs'
    source_path.write_text(source, encoding='utf-8')
    fields = lambda name: dict(property=name, path='Probe:' + name, required=False, secret=False,
        constraints={}, binding='Reference constant fixture.', precedence=['initializer'], reload='immutable', description='Static default.')
    declaration = dict(schemaVersion=1, packageId='NoBuildProbe', sourceSha256={'Options.cs': digest(source_path)},
        contracts=[dict(scope='options', typeName='Probe.Options', complete=True,
            settings=[fields('Limit'), fields('FrameworkLimit')], semanticConstraints=[])])
    declaration_path = work / 'settings.source.json'
    declaration_path.write_text(json.dumps(declaration), encoding='utf-8')
    project = work / 'NoBuildProbe.csproj'
    project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><PackageId>NoBuildProbe</PackageId><Version>1.2.3</Version><FoundationSettingsMetadataSource>settings.source.json</FoundationSettingsMetadataSource></PropertyGroup><ItemGroup><Compile Remove="reference/**/*.cs"/><ProjectReference Include="reference/Referenced.csproj"/><PackageReference Include="Orbyss.Foundation.Build" Version="' + fixture.package_version(package) + '" PrivateAssets="all"/></ItemGroup>' + no_compile + '</Project>', encoding='utf-8')
    configuration = work / 'NuGet.config'
    configuration.write_text('<configuration><packageSources><clear/><add key="local" value="' + fixture.escape(str(package.parent)) + '"/></packageSources></configuration>', encoding='utf-8')
    fixture.run(['dotnet', 'restore', str(project), '--configfile', str(configuration)], work, work / 'restore.log', env=environment)
    command = ['dotnet', 'pack', str(project), '-c', 'Release', '--no-restore', '-o', str(work / 'packages')]
    fixture.run(command, work, work / 'build-pack.log', env=environment)
    metadata = work / 'obj/Release/net10.0/orbyss-foundation/settings.json'
    values = json.loads(metadata.read_text())
    assert [entry['default'] for entry in values['contracts'][0]['settings']] == [17, 2147483647]
    binaries = {path: digest(path) for directory in (work / 'bin', work / 'obj', reference / 'bin', reference / 'obj')
        for path in directory.rglob('*.dll')}
    metadata_before = metadata.read_bytes()
    # The referenced source is deliberately uncompilable; only its already built reference assembly is admitted.
    reference_source.write_text('#error no-build must not compile a referenced project\n', encoding='utf-8')
    no_build = [*command, '--no-build', '-p:SettingsProbeNoCompile=true']
    fixture.run(no_build, work, work / 'reference-constant-no-build.log', env=environment)
    assert metadata.read_bytes() == metadata_before and all(digest(path) == expected for path, expected in binaries.items())
    packed = work / 'packages/NoBuildProbe.1.2.3.nupkg'
    with zipfile.ZipFile(packed) as archive:
        value = json.loads(archive.read('orbyss-foundation/settings.json'))
        assert value == values
        assert hashlib.sha256(archive.read('lib/net10.0/NoBuildProbe.dll')).hexdigest() == value['assembly']['sha256']
    packed_before = packed.read_bytes()
    source_path.write_text(source + '\n// reviewed source, not compiled yet\n', encoding='utf-8')
    declaration['sourceSha256']['Options.cs'] = hashlib.sha256(source_path.read_bytes().replace(b'\r\n', b'\n')).hexdigest()
    declaration_path.write_text(json.dumps(declaration), encoding='utf-8')
    rejected = fixture.run(no_build, work, work / 'stale-source-no-build.log', env=environment, expected=1)
    assert 'PKSM001' in rejected and 'rebuild the publisher' in rejected and 'No-build packing reached compilation' not in rejected
    assert metadata.read_bytes() == metadata_before and packed.read_bytes() == packed_before
    assert all(digest(path) == expected for path, expected in binaries.items())
    result = dict(package=str(package), packageSha256=digest(package), referenceDefault=17, frameworkDefault=2147483647,
        noBuildReferenceResolution=True, publisherAndReferenceNotCompiled=True, initializersNeverExecuted=True,
        staleReviewedSourceRejected=True, metadataAndPackedAssemblyMatch=True, outputsPreservedOnRejection=True)
    (work / 'results.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print('Installed no-build reference constant and stale-source qualification passed. Evidence: ' + str(work / 'results.json'))
    return work


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', type=Path, required=True)
    qualify(parser.parse_args().package)


if __name__ == '__main__':
    main()
