"""Exercise an actual neutral Host with no selected Foundation features or application assets."""
from __future__ import annotations

import argparse
import hashlib
import http.client
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import time
from urllib.parse import urlsplit
import uuid

import validate_contract_package_consumption as packaged


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--host', type=Path, required=True)
    args = parser.parse_args()
    host = args.host.resolve()
    packaged.verify_neutral_host(host)
    root = Path(__file__).resolve().parents[1] / 'artifacts/neutral-browser' / uuid.uuid4().hex
    root.mkdir(parents=True)
    shutil.copy2(host.parent / 'appsettings.json', root / 'appsettings.json')
    (root / 'packages').mkdir()
    (root / 'shells.json').write_text(json.dumps({'CShells': {'Shells': {
        'default': {'Features': {}, 'Configuration': {'WebRouting': {'Path': ''}}},
        'empty': {'Features': {}, 'Configuration': {'WebRouting': {'Path': 'empty'}}}}}}))
    (root / 'hostsettings.json').write_text(json.dumps({'Nuplane': {
        'Setup': {'StateFilePath': str(root / 'state.json')},
        'FeedResolution': {'PackageInstallRoot': str(root / 'installed')},
        'Loading': {'ActiveStoreRoot': str(root / 'installed')}}}))
    environment = {key: value for key, value in os.environ.items()
                   if not key.upper().startswith(('CSHELLS__', 'FOUNDATION__', 'NUPLANE__'))}
    observations = []
    inventory = {file.relative_to(host.parent).as_posix(): packaged.sha256(file)
                 for file in host.parent.rglob('*') if file.is_file()}
    with (root / 'host.log').open('w', encoding='utf-8') as log:
        process = subprocess.Popen(['dotnet', str(host), '--contentRoot', str(root), '--urls', 'http://127.0.0.1:0'],
                                   cwd=root, env=environment, stdout=log, stderr=subprocess.STDOUT,
                                   creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    try:
        deadline = time.monotonic() + 60
        while True:
            output = (root / 'host.log').read_text(encoding='utf-8')
            match = re.search(r'Now listening on: (http://127\.0\.0\.1:\d+)', output)
            if match:
                base = urlsplit(match[1])
                break
            assert process.poll() is None and time.monotonic() < deadline, 'Neutral Host startup failed: ' + str(root / 'host.log')
            time.sleep(.1)
        for path in ['/missing', '/empty/missing', '/unknown-shell/missing']:
            for method, accept, navigation, expected in [('GET', 'text/html', True, 'text/html'),
                ('HEAD', 'text/html', True, 'text/html'), ('GET', 'text/html', False, 'text/plain'),
                ('GET', 'application/json', True, 'text/plain'), ('POST', 'text/html', True, 'text/plain'),
                ('GET', 'text/html;q=0,application/json', True, 'text/plain')]:
                connection = http.client.HTTPConnection(base.hostname, base.port, timeout=15)
                headers = {'Accept': accept}
                if navigation:
                    headers.update({'Sec-Fetch-Mode': 'navigate', 'Sec-Fetch-Dest': 'document'})
                connection.request(method, path, headers=headers)
                response = connection.getresponse()
                body = response.read()
                assert response.status == 404, (path, method, response.status)
                assert response.getheader('Content-Type', '').startswith(expected), (path, method, response.getheaders(), body)
                if expected == 'text/html':
                    assert response.getheader('Cache-Control') == 'no-store'
                    assert response.getheader('X-Robots-Tag') == 'noindex, nofollow'
                    assert response.getheader('Content-Security-Policy').startswith("default-src 'none'")
                    assert method == 'HEAD' and not body or b'Page not found' in body
                    assert b'Synthetic application' not in body
                observations.append(dict(path=path, method=method, accept=accept, navigation=navigation, status=response.status,
                                         contentType=response.getheader('Content-Type'), bodySha256=hashlib.sha256(body).hexdigest()))
                connection.close()
    finally:
        packaged.terminate(process)
        (root / 'results.json').write_text(json.dumps(dict(hostRuntimeFiles=inventory, observations=observations,
            complete=len(observations) == 18, scope='actual neutral Host; no Foundation runtime feature selected'), indent=2)+'\n')
    print('Actual neutral Host browser/machine/HEAD/root/prefixed/unknown-shell fallback passed: ' + str(root / 'results.json'))


if __name__ == '__main__':
    main()
