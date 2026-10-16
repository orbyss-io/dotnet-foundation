"""Actual selected Host: PdfPig multi-assembly closure and culture-aware satellites.

The fixed Nuplane release is consumed, never patched. All documents/resources are
synthetic. Local payload evidence is separate from image/publication qualification.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import time
import urllib.request
import uuid
import xml.etree.ElementTree as ET
import zipfile

import validate_contract_package_consumption as shared

ROOT = Path(__file__).resolve().parents[1]


def pdf():
    stream = b'BT /F1 12 Tf 20 30 Td (Synthetic loader proof) Tj ET'
    objects = [b'<< /Type /Catalog /Pages 2 0 R >>', b'<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
               b'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>',
               b'<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
               b'<< /Length ' + str(len(stream)).encode() + b' >>\nstream\n' + stream + b'\nendstream']
    raw = bytearray(b'%PDF-1.4\n'); offsets = [0]
    for index, value in enumerate(objects, 1):
        offsets.append(len(raw)); raw.extend(str(index).encode() + b' 0 obj\n' + value + b'\nendobj\n')
    start = len(raw); raw.extend(b'xref\n0 6\n0000000000 65535 f \n')
    for offset in offsets[1:]: raw.extend(f'{offset:010} 00000 n \n'.encode())
    raw.extend(b'trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n' + str(start).encode() + b'\n%%EOF\n')
    return bytes(raw)


def run(command, cwd, environment, log):
    with log.open('w', encoding='utf-8') as output:
        value = subprocess.run(list(map(str, command)), cwd=cwd, env=environment, stdout=output, stderr=subprocess.STDOUT, timeout=240)
    assert value.returncode == 0, 'Native command failed; preserved ' + str(log)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--host', type=Path, required=True)
    parser.add_argument('--work', type=Path, default=ROOT / 'artifacts/host-package-loading')
    args = parser.parse_args()
    owner = args.work.resolve() / uuid.uuid4().hex[:12]; owner.mkdir(parents=True)
    result = {'status': 'running', 'scope': 'Actual local Host and locked public native packages; no public image/release/consumer acceptance'}
    def source_hashes():
        paths = [Path(__file__), ROOT / 'Directory.Packages.props', ROOT / 'NuGet.config',
                 *sorted((ROOT / 'tests/loader-fixture').glob('*'))]
        return {path.relative_to(ROOT).as_posix(): shared.sha256(path) for path in paths if path.is_file()}
    original_sources = source_hashes()
    try:
        source = owner / 'source'; shutil.copytree(ROOT / 'tests/loader-fixture', source)
        for name in ('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props'):
            (source / name).write_text('<Project />', encoding='utf-8')
        for suffix, text in (('', 'Neutral fixture'), ('.en', 'English fixture'), ('.nl', 'Nederlandse fixture')):
            resource = ET.Element('root')
            for name, value in (('resmimetype', 'text/microsoft-resx'), ('version', '2.0')):
                ET.SubElement(ET.SubElement(resource, 'resheader', name=name), 'value').text = value
            ET.SubElement(ET.SubElement(resource, 'data', name='Title'), 'value').text = text
            ET.ElementTree(resource).write(source / ('Copy' + suffix + '.resx'), encoding='utf-8', xml_declaration=True)
        configuration = json.loads((args.host.resolve().parent / 'appsettings.json').read_text())
        environment = {key: value for key, value in os.environ.items()
                       if not key.upper().startswith(('CSHELLS__', 'NUPLANE__', 'FOUNDATION__'))
                       and not any(part in key.upper() for part in ('TOKEN', 'SECRET', 'PASSWORD', 'CREDENTIAL', 'CONNECTION_STRING'))}
        environment.update(NUGET_PACKAGES=str(owner / 'cache'), DOTNET_CLI_HOME=str(owner / 'dotnet-home'),
                           MSBUILDDISABLENODEREUSE='1', DOTNET_CLI_USE_MSBUILD_SERVER='0')
        versions = {item.get('Include'): item.get('Version') for item in ET.parse(ROOT / 'Directory.Packages.props').iter('PackageVersion')}
        project = source / 'LoaderFixture.csproj'; properties = ['-p:CShellsCandidateVersion=' + versions['CShells.AspNetCore.Abstractions']]
        restore = ['dotnet', 'restore', project, '--configfile', ROOT / 'NuGet.config', '--no-http-cache', *properties]
        run(restore, source, environment, owner / 'restore.log')
        run([*restore, '--locked-mode'], source, environment, owner / 'locked-restore.log')
        packages = owner / 'fixture-packages'
        run(['dotnet', 'pack', project, '-c', 'Release', '--no-restore', '--disable-build-servers', *properties,
             '-p:UseSharedCompilation=false', '-o', packages], source, environment, owner / 'pack.log')
        assets = json.loads((source / 'obj/project.assets.json').read_text())
        feed = owner / 'feed'; feed.mkdir()
        shared_names = {row['Name'].casefold() for row in configuration['Nuplane']['Loading']['SharedAssemblies']}
        inputs = {}
        for name, library in assets['libraries'].items():
            if library['type'] != 'package': continue
            identity, version = name.rsplit('/', 1)
            archive = owner / 'cache' / library['path'] / (identity.lower() + '.' + version.lower() + '.nupkg')
            assert archive.is_file(), name
            inputs[name] = {'sha256': shared.sha256(archive)}
            if identity.casefold() not in shared_names and not identity.startswith('Nuplane'):
                shutil.copy2(archive, feed / archive.name)
        fixture = next(packages.glob('*.nupkg')); shutil.copy2(fixture, feed / fixture.name)
        assert 'PdfPig/0.1.16' in inputs
        host = owner / 'host'; host_hashes = shared.preserve_host_payload(args.host.resolve(), host)
        shared.verify_neutral_host(host / args.host.name)
        host_deps = json.loads((host / 'Orbyss.Foundation.Host.deps.json').read_text())
        result['hostNativePackages'] = {name: value for name, value in host_deps['libraries'].items() if name.startswith(('Nuplane', 'CShells'))}
        host_assets = json.loads((ROOT / 'src/Orbyss.Foundation.Host/obj/project.assets.json').read_text())
        native_archives = {}
        native_target = host_deps['targets'][next(iter(host_deps['targets']))]
        for identity in result['hostNativePackages']:
            name, version = identity.rsplit('/', 1); library = host_assets['libraries'][identity]
            candidates = [Path(folder) / library['path'] / (name.lower() + '.' + version.lower() + '.nupkg')
                          for folder in host_assets['packageFolders']]
            candidates = [archive for archive in candidates if archive.is_file()]
            assert len(candidates) == 1, 'Missing/ambiguous selected publisher archive: ' + identity
            archive = candidates[0]
            with zipfile.ZipFile(archive) as contents:
                metadata = ET.fromstring(contents.read(next(path for path in contents.namelist() if path.endswith('.nuspec'))))
                repository = metadata.find('./{*}metadata/{*}repository')
                bindings = {}
                for asset in native_target[identity].get('runtime', {}):
                    if not asset.endswith('.dll'): continue
                    runtime_dll = host / Path(asset).name
                    expected = hashlib.sha256(contents.read(asset)).hexdigest()
                    assert runtime_dll.is_file() and shared.sha256(runtime_dll) == expected, 'Host/archive mismatch: ' + identity
                    bindings[asset] = expected
                assert bindings, 'Native publisher has no verified runtime assembly: ' + identity
                native_archives[identity] = {'sha256': shared.sha256(archive), 'declaredRepository': dict(repository.attrib) if repository is not None else None,
                                             'runtimeAssemblies': bindings}
        result['nativePublisherArchives'] = native_archives
        result['hostRuntimeFiles'] = host_hashes; result['packages'] = inputs
        result['fixtureSha256'] = shared.sha256(fixture)
        result['sourceSha256'] = original_sources
        runtime = owner / 'runtime'; runtime.mkdir()
        shutil.copytree(feed, runtime / 'packages'); (runtime / 'known.pdf').write_bytes(pdf())
        (runtime / 'shells.json').write_text(json.dumps({'CShells': {'Shells': {'loader': {
            'Features': {'Foundation.LoaderFixture': True}, 'Configuration': {'WebRouting': {'Path': ''}}}}}}))
        (runtime / 'appsettings.json').write_text(json.dumps(configuration))
        (runtime / 'hostsettings.json').write_text(json.dumps({'Nuplane': {'Setup': {'StateFilePath': str(runtime / 'state.json')},
            'FeedResolution': {'PackageInstallRoot': str(runtime / 'installed')}, 'Loading': {'ActiveStoreRoot': str(runtime / 'installed')}}}))
        log_path = runtime / 'host.log'
        with log_path.open('w', encoding='utf-8') as output:
            process = subprocess.Popen(['dotnet', str(host / args.host.name), '--contentRoot', str(runtime), '--urls', 'http://127.0.0.1:0'],
                                       cwd=runtime, env=environment, stdout=output, stderr=subprocess.STDOUT,
                                       creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
            try:
                deadline = time.monotonic() + 90
                while time.monotonic() < deadline:
                    match = re.search(r'Now listening on: (http://127\.0\.0\.1:\d+)', log_path.read_text(encoding='utf-8', errors='replace'))
                    if match: address = match[1]; break
                    assert process.poll() is None, 'Native Host exited; inspect ' + str(log_path)
                    time.sleep(.2)
                else: raise AssertionError('Native loader startup timed out; inspect ' + str(log_path))
                with urllib.request.urlopen(address + '/loader-proof', timeout=20) as response:
                    observed = json.loads(response.read(65537)); assert response.status == 200
                result['observation'] = observed
                assert observed['hostIntegratedContext'] and observed['pages'] == 1 and observed['text'] == 'Synthetic loader proof'
                assert (observed['english'], observed['dutch'], observed['fallback']) == ('English fixture', 'Nederlandse fixture', 'Nederlandse fixture')
                satellites = [row for row in observed['loaded'] if row['name'] == 'LoaderFixture.resources']
                assert {row['culture'] for row in satellites} == {'en', 'nl'} and len(satellites) == 2
                assert len({row['name'] for row in observed['loaded'] if row['name'].startswith('UglyToad.PdfPig')}) >= 3
                # Bind actual loaded assemblies to exact selected package payloads.
                payloads = set()
                for archive in [*feed.glob('*.nupkg')]:
                    with zipfile.ZipFile(archive) as contents:
                        payloads.update(hashlib.sha256(contents.read(name)).hexdigest() for name in contents.namelist() if name.endswith('.dll'))
                assert all(row['sha256'] in payloads for row in observed['loaded'])
            finally:
                shared.terminate(process)
        # Same culture/simple name with a conflicting native assembly version must
        # remain an admission failure. Do not weaken it to a global name bypass.
        resx = source / 'Copy.nl.resx'
        resx.write_text(resx.read_text().replace('Nederlandse fixture', 'Conflicting synthetic resource'))
        run(['dotnet', 'build', project, '-c', 'Release', '--no-restore', '--disable-build-servers', *properties,
             '-p:UseSharedCompilation=false', '-p:AssemblyVersion=2.0.0.0'], source, environment, owner / 'conflict-producer.log')
        conflicting = source / 'bin/Release/net10.0/nl/LoaderFixture.resources.dll'
        bad_runtime = owner / 'conflict-runtime'; bad_runtime.mkdir()
        bad_feed = bad_runtime / 'packages'; shutil.copytree(feed, bad_feed)
        bad_fixture = bad_feed / fixture.name
        with zipfile.ZipFile(fixture) as original:
            expected_resource = original.read('lib/net10.0/nl/LoaderFixture.resources.dll')
            assert expected_resource != conflicting.read_bytes()
            with zipfile.ZipFile(bad_fixture, 'w') as changed:
                for entry in original.infolist(): changed.writestr(entry, original.read(entry))
                changed.writestr('lib/net10.0/Conflicting.dll', conflicting.read_bytes())
        for name in ('appsettings.json', 'shells.json', 'known.pdf'):
            shutil.copy2(runtime / name, bad_runtime / name)
        (bad_runtime / 'hostsettings.json').write_text(json.dumps({'Nuplane': {'Setup': {'StateFilePath': str(bad_runtime / 'state.json')},
            'FeedResolution': {'PackageInstallRoot': str(bad_runtime / 'installed')}, 'Loading': {'ActiveStoreRoot': str(bad_runtime / 'installed')}}}))
        bad_log = bad_runtime / 'host.log'
        with bad_log.open('w', encoding='utf-8') as output:
            process = subprocess.Popen(['dotnet', str(host / args.host.name), '--contentRoot', str(bad_runtime), '--urls', 'http://127.0.0.1:0'],
                                       cwd=bad_runtime, env=environment, stdout=output, stderr=subprocess.STDOUT,
                                       creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
            try:
                deadline = time.monotonic() + 90
                while process.poll() is None and time.monotonic() < deadline:
                    assert 'Now listening on:' not in bad_log.read_text(encoding='utf-8', errors='replace'), 'Conflicting same-culture satellite was admitted'
                    time.sleep(.2)
                assert process.poll() is not None, 'Conflict proof startup was inconclusive'
                assert process.returncode != 0, 'Conflicting same-culture satellite was admitted'
            finally:
                shared.terminate(process)
        text = bad_log.read_text(encoding='utf-8', errors='replace')
        assert 'LoaderFixture.resources' in text and re.search(r'(?i)conflict|ambiguous|duplicate|multiple', text), 'Unrelated failure cannot prove satellite conflict rejection'
        result['sameCultureConflict'] = {'status': 'rejected', 'hostExitCode': process.returncode,
            'candidateSha256': shared.sha256(bad_fixture), 'originalFixtureUnchanged': shared.sha256(fixture) == result['fixtureSha256'],
            'logSha256': shared.sha256(bad_log)}
        assert host_hashes == {path.relative_to(host).as_posix(): shared.sha256(path) for path in host.rglob('*') if path.is_file()}
        assert source_hashes() == original_sources, 'Native loader qualification sources changed during execution'
        result['cases'] = ['neutral-host-exact-public-native-closure', 'actual-seven-assembly-PdfPig-runtime',
                           'same-resource-name-two-cultures-and-parent-fallback', 'same-culture-conflicting-native-version-rejected',
                           'actual-loaded-runtime-equals-original-public-package-bytes', 'source-and-host-before-after-unchanged']
        result['status'] = 'passed'
    finally:
        (owner / 'results.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
        print('Native package loader evidence: ' + str(owner / 'results.json'))


if __name__ == '__main__': main()
