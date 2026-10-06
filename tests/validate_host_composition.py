"""Actual native Host custom Problem Details composition without Foundation packages or features."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import http.client
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import time
import uuid
import xml.etree.ElementTree as ET

import validate_contract_package_consumption as packaged


def main():
    repository = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--host', type=Path, required=True)
    parser.add_argument('--packages', type=Path, required=True, help='Retained dependency archives for an isolated offline fixture restore.')
    parser.add_argument('--dotnet', default='dotnet')
    args = parser.parse_args()
    root = repository / 'artifacts/host-composition' / (datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ-') + uuid.uuid4().hex[:8])
    root.mkdir(parents=True)
    hashes = packaged.preserve_host_payload(args.host.resolve(), root / 'host')
    source = root / 'consumer'
    shutil.copytree(repository / 'tests/host-composition/HostComposition.Fixture', source)
    configuration = json.loads((root / 'host/appsettings.json').read_text(encoding='utf-8'))
    shared = {item['Name'].casefold() for item in configuration['Nuplane']['Loading']['SharedAssemblies']}
    nuget = ET.Element('configuration')
    sources = ET.SubElement(nuget, 'packageSources')
    ET.SubElement(sources, 'clear')
    ET.SubElement(sources, 'add', key='retained-inputs', value=str(args.packages.resolve()))
    ET.ElementTree(nuget).write(source / 'NuGet.config', encoding='utf-8', xml_declaration=True)
    environment = {key: value for key, value in os.environ.items() if not key.upper().startswith(('CSHELLS__', 'FOUNDATION__', 'NUPLANE__'))
                   and not any(part in key.upper() for part in ('TOKEN', 'PASSWORD', 'SECRET', 'CREDENTIAL', 'CONNECTION_STRING'))}
    environment['NUGET_PACKAGES'] = str(root / 'package-cache')
    environment['DOTNET_CLI_HOME'] = str(root / 'dotnet-home')
    environment['MSBUILDDISABLENODEREUSE'] = '1'
    project = source / 'HostComposition.Fixture.csproj'
    for name, arguments in [('restore', ['restore', str(project), '--configfile', str(source / 'NuGet.config'), '--use-lock-file']),
                            ('locked-restore', ['restore', str(project), '--configfile', str(source / 'NuGet.config'), '--locked-mode']),
                            ('pack', ['pack', str(project), '--no-restore', '-c', 'Release', '-o', str(root / 'fixture-packages'), '-p:UseSharedCompilation=false'])]:
        with (root / (name + '.log')).open('w', encoding='utf-8') as log:
            result = subprocess.run([args.dotnet, *arguments], cwd=source, env=environment, stdout=log, stderr=subprocess.STDOUT, timeout=240)
        assert result.returncode == 0, 'Custom-only fixture failed: ' + str(root / (name + '.log'))
    assets = json.loads((source / 'obj/project.assets.json').read_text(encoding='utf-8'))
    identities = [name for name, library in assets['libraries'].items() if library['type'] == 'package']
    assert not any(name.startswith('Orbyss.Foundation.') for name in identities), 'Custom fixture has a Foundation dependency'
    archive_inputs = {}
    full_feed = root / 'feed'
    full_feed.mkdir()
    for identity in identities:
        library = assets['libraries'][identity]
        package_id, version = identity.rsplit('/', 1)
        archive = Path(library['path']) / f'{package_id.lower()}.{version.lower()}.nupkg'
        candidates = [Path(folder) / archive for folder in assets['packageFolders'] if (Path(folder) / archive).is_file()]
        assert len(candidates) == 1, identity
        shutil.copy2(candidates[0], full_feed / candidates[0].name)
        archive_inputs[candidates[0].name] = packaged.sha256(candidates[0])
    for archive in (root / 'fixture-packages').glob('*.nupkg'):
        shutil.copy2(archive, full_feed / archive.name)
        archive_inputs[archive.name] = packaged.sha256(archive)
    (root / 'inputs.json').write_text(json.dumps({'hostRuntimeFiles': hashes, 'packages': archive_inputs,
        'nativePackageIdentities': identities, 'sharedHostAssemblies': sorted(shared)}, indent=2), encoding='utf-8')
    observations = []
    failures = []
    for topology in ('prefixes', 'root'):
        runtime = root / ('runtime-' + topology)
        feed = runtime / 'packages'
        feed.mkdir(parents=True)
        for archive in full_feed.glob('*.nupkg'):
            if not any(archive.name.casefold().startswith(name + '.') for name in shared):
                shutil.copy2(archive, feed / archive.name)
        shells = {'writer': {'Features': {'HostComposition.CustomWriter': True},
                   'Configuration': {'WebRouting': {'Path': 'writer' if topology == 'prefixes' else ''}}}}
        if topology == 'prefixes':
            shells['service'] = {'Features': {'HostComposition.CustomService': True}, 'Configuration': {'WebRouting': {'Path': 'service'}}}
        (runtime / 'shells.json').write_text(json.dumps({'CShells': {'Shells': shells}}), encoding='utf-8')
        (runtime / 'appsettings.json').write_text(json.dumps(configuration), encoding='utf-8')
        (runtime / 'hostsettings.json').write_text(json.dumps({'Nuplane': {
            'Setup': {'StateFilePath': str(runtime / 'state.json')}, 'FeedResolution': {'PackageInstallRoot': str(runtime / 'installed')},
            'Loading': {'ActiveStoreRoot': str(feed / '.installed')}}}), encoding='utf-8')
        log_path = runtime / 'host.log'
        with log_path.open('w', encoding='utf-8') as log:
            process = subprocess.Popen([args.dotnet, str(root / 'host' / args.host.name), '--contentRoot', str(runtime),
                '--urls', 'http://127.0.0.1:0'], cwd=runtime, env=environment, stdout=log, stderr=subprocess.STDOUT,
                creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
            try:
                deadline = time.monotonic() + 75
                while time.monotonic() < deadline:
                    match = re.search(r'Now listening on: http://127\.0\.0\.1:(\d+)', log_path.read_text(encoding='utf-8', errors='replace'))
                    if match:
                        port = int(match.group(1))
                        break
                    assert process.poll() is None, 'Native Host exited: ' + str(log_path)
                    time.sleep(.2)
                else:
                    raise AssertionError('Native custom-only Host startup timed out: ' + str(log_path))

                def request(method, path, expected, format='custom', owner=None, accept='application/json'):
                    connection = http.client.HTTPConnection('127.0.0.1', port, timeout=15)
                    try:
                        connection.request(method, path, headers={'Accept': accept})
                        try:
                            response = connection.getresponse()
                        except http.client.RemoteDisconnected:
                            assert format == 'canceled', 'Unexpected native connection closure'
                            observations.append({'topology': topology, 'method': method, 'path': path,
                                                 'connectionClosed': True, 'bytes': 0})
                            (root / 'observations.json').write_text(json.dumps(observations, indent=2), encoding='utf-8')
                            return
                        incomplete = False
                        try:
                            body = response.read(65_537)
                        except http.client.IncompleteRead as error:
                            body = error.partial
                            incomplete = True
                        record = {'topology': topology, 'method': method, 'path': path, 'status': response.status,
                                  'contentType': response.getheader('Content-Type'), 'allow': response.getheader('Allow'),
                                  'body': body.decode('utf-8', errors='replace'), 'bytes': len(body), 'incomplete': incomplete}
                        observations.append(record)
                        (root / 'observations.json').write_text(json.dumps(observations, indent=2), encoding='utf-8')
                        if format == 'canceled':
                            assert not body and response.getheader('Content-Type') != 'application/custom-problem+json', record
                            return
                        assert response.status == expected and len(body) <= 65_536, record
                        if format == 'ready':
                            value = json.loads(body)
                            assert value['shell'] == owner and value['handlers'] == 0, record
                            assert value['foundationWriters'] == 0 and value['foundationAssemblies'] == [], record
                        elif format == 'custom':
                            value = json.loads(body)
                            assert response.getheader('Content-Type') == 'application/custom-problem+json', record
                            assert value == {'type': 'urn:host-composition:custom', 'title': 'Custom problem', 'status': expected,
                                             'extensions': {'customOwner': owner}}, record
                            assert response.getheader('Content-Length') == str(len(body)), record
                        elif format == 'native':
                            value = json.loads(body)
                            assert response.getheader('Content-Type') == 'application/problem+json' and value['status'] == expected, record
                            assert not {'code', 'correlationId', 'fieldErrors'} & set(value), record
                        elif format == 'text':
                            assert response.getheader('Content-Type').startswith('text/plain') and 'Status Code: ' + str(expected) in record['body'], record
                        elif format == 'started':
                            assert body == b'committed-prefix' and incomplete, record
                            assert response.getheader('Content-Type').startswith('text/plain'), record
                        else:
                            assert body == b'preserved custom body', record
                        if expected == 405:
                            assert response.getheader('Allow') == 'POST', record
                    except AssertionError as error:
                        failures.append(str(error))
                    finally:
                        connection.close()

                if topology == 'prefixes':
                    request('GET', '/outside-all-shells', 404, 'text')
                    request('GET', '/outside-all-shells', 404, 'text', accept='text/html')
                    for prefix in ('writer', 'service'):
                        request('GET', '/' + prefix + '/ready', 200, 'ready', prefix)
                        request('GET', '/' + prefix + '/missing', 404, owner=prefix)
                        request('DELETE', '/' + prefix + '/only-post', 405, owner=prefix)
                        request('GET', '/' + prefix + '/status/409', 409, owner=prefix)
                        request('GET', '/' + prefix + '/written', 409, 'written', owner=prefix)
                        request('GET', '/' + prefix + '/started-failure', 409, 'started', owner=prefix)
                        request('GET', '/' + prefix + '/canceled', 409, 'canceled', owner=prefix)
                else:
                    request('GET', '/ready', 200, 'ready', 'writer')
                    request('GET', '/missing', 404, owner='writer')
                    request('DELETE', '/only-post', 405, owner='writer')
                    request('GET', '/started-failure', 409, 'started', owner='writer')
                    request('GET', '/canceled', 409, 'canceled', owner='writer')
            finally:
                packaged.terminate(process)
        assert 'CUSTOM_PRIVATE_STARTED_FAILURE' not in log_path.read_text(encoding='utf-8', errors='replace'), 'Private started exception leaked into native Host log'
    assert all(packaged.sha256(root / 'host' / relative) == digest for relative, digest in hashes.items())
    assert all(packaged.sha256(full_feed / name) == digest for name, digest in archive_inputs.items())
    (root / 'result.json').write_text(json.dumps({'satisfied': not failures, 'qualificationClaimed': not failures,
        'customOnly': True, 'observations': len(observations), 'failures': failures}, indent=2), encoding='utf-8')
    assert not failures, 'Native custom-only composition failed: ' + str(root / 'result.json')
    print('Actual native custom-only problem service/writer and root/prefix ownership passed: ' + str(root))


if __name__ == '__main__':
    main()
