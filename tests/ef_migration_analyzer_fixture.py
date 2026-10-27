"""Exercise unmodified native EF migrations against one exact analyzer archive."""
from __future__ import annotations

from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import time
import uuid
import xml.etree.ElementTree as ET
import zipfile


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def validate(root: Path, package: Path, ef_version: str | None = None,
             provider_version: str | None = None) -> Path:
    """Retain native generation, packaged compilation, drift and adverse evidence without a database."""
    root, package = root.resolve(), package.resolve()
    with zipfile.ZipFile(package) as archive:
        specs = [name for name in archive.namelist() if name.endswith('.nuspec')]
        if len(specs) != 1:
            raise AssertionError('Expected one exact analyzer package identity')
        fields = {node.tag.rsplit('}', 1)[-1]: node.text for node in ET.fromstring(archive.read(specs[0])).iter()}
        if fields.get('id') != 'Orbyss.Foundation.Analyzers':
            raise AssertionError('Native EF proof requires the actual Foundation analyzer')
        version = fields['version']
    pins = {node.attrib['Include']: node.attrib['Version']
            for node in ET.parse(root / 'Directory.Packages.props').findall('.//PackageVersion')}
    ef_version = ef_version or pins['Microsoft.EntityFrameworkCore']
    provider_version = provider_version or pins['Npgsql.EntityFrameworkCore.PostgreSQL']
    run = root / 'artifacts/native-ef-analyzer' / (
        datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ-') + uuid.uuid4().hex[:8])
    consumer, feed, cache = run / 'consumer', run / 'feed', run / 'packages'
    consumer.mkdir(parents=True)
    feed.mkdir()
    copied_package = feed / package.name
    shutil.copyfile(package, copied_package)
    fixture = root / 'tests/fixtures/ef-migration-analyzer'
    for source in fixture.glob('*.cs'):
        shutil.copyfile(source, consumer / source.name)
    template = (fixture / 'NativeEf.csproj.template').read_text(encoding='utf-8')
    project = consumer / 'NativeEf.csproj'
    project.write_text(template.replace('@ANALYZER_VERSION@', version)
                       .replace('@EF_VERSION@', ef_version)
                       .replace('@PROVIDER_VERSION@', provider_version), encoding='utf-8')
    for name in ('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props'):
        (consumer / name).write_text('<Project/>', encoding='utf-8')
    shutil.copyfile(root / 'global.json', consumer / 'global.json')
    configuration = ET.Element('configuration')
    sources = ET.SubElement(configuration, 'packageSources')
    ET.SubElement(sources, 'clear')
    ET.SubElement(sources, 'add', key='exact-analyzer', value=str(feed))
    ET.SubElement(sources, 'add', key='nuget.org', value='https://api.nuget.org/v3/index.json', protocolVersion='3')
    mappings = ET.SubElement(configuration, 'packageSourceMapping')
    ET.SubElement(mappings, 'clear')
    selected = ET.SubElement(mappings, 'packageSource', key='exact-analyzer')
    ET.SubElement(selected, 'package', pattern='Orbyss.Foundation.Analyzers')
    public = ET.SubElement(mappings, 'packageSource', key='nuget.org')
    ET.SubElement(public, 'package', pattern='*')
    config = consumer / 'NuGet.Config'
    ET.ElementTree(configuration).write(config, encoding='utf-8', xml_declaration=True)
    tool_directory = consumer / '.config'
    tool_directory.mkdir()
    manifest = tool_directory / 'dotnet-tools.json'
    manifest.write_text(json.dumps({'version': 1, 'isRoot': True, 'tools': {
        'dotnet-ef': {'version': ef_version, 'commands': ['dotnet-ef'], 'rollForward': False}}}, indent=2), encoding='utf-8')
    environment = os.environ.copy()
    environment.update({'NUGET_PACKAGES': str(cache), 'DOTNET_CLI_USE_MSBUILD_SERVER': '0',
                        'MSBUILDDISABLENODEREUSE': '1', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE': '1'})
    steps = []
    result = {'status': 'running', 'package': {'path': str(package), 'sha256': sha(package), 'version': version},
              'efVersion': ef_version, 'providerVersion': provider_version,
              'validatorSha256': sha(Path(__file__)),
              'scope': 'Native EF model generation and packaged analyzer compilation; no database or migration deployment.',
              'fixtureHashes': {path.name: sha(path) for path in sorted(fixture.iterdir()) if path.is_file()},
              'steps': steps}
    evidence = run / 'results.json'

    def record() -> None:
        evidence.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')

    def execute(name: str, arguments: list[str], expected_failure: bool = False) -> str:
        started = time.monotonic()
        output = run / (name + '.log')
        command = ['dotnet', *arguments]
        try:
            with output.open('w', encoding='utf-8') as stream:
                completed = subprocess.run(command, cwd=consumer, env=environment, stdout=stream,
                                           stderr=subprocess.STDOUT, text=True, timeout=300)
            steps.append({'name': name, 'command': command, 'exitCode': completed.returncode,
                          'durationSeconds': round(time.monotonic() - started, 3), 'log': output.name})
        except subprocess.TimeoutExpired:
            steps.append({'name': name, 'command': command, 'timedOut': True, 'log': output.name})
            record()
            raise
        record()
        text = output.read_text(encoding='utf-8', errors='replace')
        if (completed.returncode == 0) == expected_failure:
            raise AssertionError('Unexpected command outcome at ' + name + '; evidence: ' + str(output))
        return text

    try:
        execute('restore', ['restore', str(project), '--configfile', str(config), '--nologo'])
        execute('locked-restore', ['restore', str(project), '--locked-mode', '--configfile', str(config), '--nologo'])
        execute('tool-restore', ['tool', 'restore', '--tool-manifest', str(manifest), '--configfile', str(config)])
        execute('initial-build', ['build', str(project), '-c', 'Release', '--no-restore', '--nologo'])
        native = ['tool', 'run', 'dotnet-ef', '--']
        options = ['--project', str(project), '--startup-project', str(project), '--context', 'NativeContext',
                   '--configuration', 'Release', '--no-build']
        execute('native-generate', [*native, 'migrations', 'add', 'Initial', '--output-dir', 'Migrations', *options])
        generated = consumer / 'Migrations'
        paths = sorted(generated.glob('*.cs'))
        main = [path for path in paths if path.name.endswith('_Initial.cs')]
        if len(paths) != 3 or len(main) != 1:
            raise AssertionError('Native EF did not produce exactly the migration, designer and model snapshot')
        shutil.copytree(generated, run / 'native-generated')
        generated_hashes = {path.name: sha(path) for path in paths}
        result['generatedHashes'] = generated_hashes
        result['nativeMainHasGeneratedHeader'] = '<auto-generated' in main[0].read_text(encoding='utf-8')
        execute('raw-generated-build', ['build', str(project), '-c', 'Release', '--no-restore', '--nologo'])
        execute('model-drift', [*native, 'migrations', 'has-pending-model-changes', *options])
        sql = run / 'native-migrations.sql'
        execute('native-script', [*native, 'migrations', 'script', '--idempotent', '--output', str(sql), *options])
        if not sql.is_file() or 'native_records' not in sql.read_text(encoding='utf-8-sig'):
            raise AssertionError('Native migration script did not retain the synthetic model')
        result['scriptSha256'] = sha(sql)
        adverse = consumer / 'AuthoredMigrationControl.cs'
        shutil.copyfile(fixture / 'AuthoredMigrationControl.cs.txt', adverse)
        shutil.copyfile(adverse, run / 'authored-adverse-migration.cs.txt')
        rejected = execute('authored-negative', ['build', str(project), '-c', 'Release', '--no-restore', '--nologo'], True)
        if 'error ORB1008' not in rejected or re.search(r'\berror (?:CS\d+|AD0001)\b', rejected):
            raise AssertionError('Authored migration experiment failed without the intended analyzer rejection')
        adverse.unlink()
        execute('restored-native-build', ['build', str(project), '-c', 'Release', '--no-restore', '--nologo'])
        if {path.name: sha(path) for path in paths} != generated_hashes:
            raise AssertionError('Native migration outputs changed during validation')
        result['status'] = 'passed'
        result['authoredRejection'] = 'ORB1008 without compiler/analyzer execution errors'
        record()
        print('Exact analyzer native EF generation, unchanged compilation, drift/script and authored rejection passed. Evidence: ' + str(evidence))
        return run
    except BaseException as error:
        result['status'] = 'failed'
        result['failure'] = type(error).__name__ + ': ' + str(error)
        record()
        raise
