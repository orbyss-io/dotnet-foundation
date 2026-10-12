"""Verify settings-only hosted-page consumption through the actual Nuplane package-loading Host."""
import argparse
from contextlib import nullcontext
import hashlib
import http.cookiejar
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.request
import urllib.error
import xml.etree.ElementTree as ET


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args):
        return None


def qualify_private_host(base, root):
    observations = []
    def client():
        return urllib.request.build_opener(NoRedirect(), urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    def request(opener, path, expected, private=True, data=None, headers=None):
        packet = urllib.request.Request(base + path, data=data, headers=headers or {})
        try:
            response = opener.open(packet, timeout=15)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            body = response.read(65537)
            assert len(body) <= 65536 and response.status == expected, (path, response.status, expected)
            if private:
                assert response.headers['Cache-Control'] == 'no-store'
                assert response.headers['X-Robots-Tag'] == 'noindex, nofollow'
                assert response.headers['X-Frame-Options'] == 'DENY'
                assert response.headers['X-Content-Type-Options'] == 'nosniff'
                assert response.headers['Content-Security-Policy']
            if expected != 200: assert b'Packaged private fixture' not in body
            observations.append(dict(path=path, status=response.status, cache=response.headers.get('Cache-Control')))
            return body
    anonymous, alpha, beta, none, full = [client() for _ in range(5)]
    for opener, identity in [(alpha, 'alpha'), (beta, 'beta'), (none, 'none'), (full, 'full')]:
        request(opener, '/private/fixture/session/' + identity, 204)
    html = request(alpha, '/private/alpha', 200).decode()
    bootstrap = re.search(r'data-bootstrap="([^"]+)"', html).group(1)
    assert 'data-credentials="same-origin"' in html
    beta_html = request(beta, '/private/beta', 200).decode()
    beta_bootstrap = re.search(r'data-bootstrap="([^"]+)"', beta_html).group(1)
    assert bootstrap != beta_bootstrap
    for path in ['/private/alpha', '/private/beta', bootstrap, beta_bootstrap]:
        request(anonymous, path, 302)
        request(none, path, 302)
    request(alpha, beta_bootstrap, 302)
    request(beta, bootstrap, 302)
    wire = json.loads(request(alpha, bootstrap, 200))
    assert wire['title'] == 'Packaged private fixture'
    asset = urllib.request.urlopen(base + wire['runtime'], timeout=15)
    with asset:
        assert asset.status == 200 and 'immutable' in asset.headers['Cache-Control']
        assert b'Packaged private fixture' not in asset.read()
    request(anonymous, '/private/api/data/synthetic-owner', 401)
    request(alpha, '/private/api/data/synthetic-owner', 403)
    request(full, '/private/api/data/foreign', 404)
    assert request(full, '/private/api/data/synthetic-owner', 200) == b'synthetic owned data'
    request(full, bootstrap.replace('/alpha/', '/'), 404)
    tokens = json.loads(request(alpha, '/private/bff/antiforgery', 200))
    request(alpha, '/private/bff/logout', 302, data=b'', headers={tokens['headerName']: tokens['requestToken']})
    request(alpha, '/private/alpha', 302)
    request(alpha, bootstrap, 302)
    (root / 'observations.json').write_text(json.dumps(observations, indent=2) + '\n', encoding='utf-8')
    return len(observations)


def build_fixture(repository, root, feed, version, cshells_version, dotnet, restore_config, packaged):
    fixture = root / 'fixture'
    shutil.copytree(repository / 'tests/package-hosted', fixture)
    descriptor = fixture / 'feature.json'
    metadata = json.loads(descriptor.read_text())
    metadata['sourceSha256'] = {path.name: hashlib.sha256(path.read_text().replace('\r\n', '\n').encode()).hexdigest() for path in fixture.glob('*.cs')}
    descriptor.write_text(json.dumps(metadata, indent=2) + '\n')
    project = fixture / 'HostedAccessFixture.csproj'
    properties = [f'-p:FoundationCandidateVersion={version}', f'-p:CShellsCandidateVersion={cshells_version}']
    restore = packaged.restore_command(dotnet, project, restore_config, root / 'nuget-cache', properties)
    packaged.run_command(restore, root, root / 'fixture-restore.log')
    packaged.run_command([*restore, '--locked-mode'], root, root / 'fixture-locked-restore.log')
    packaged.run_command([dotnet, 'build', str(project), '-c', 'Release', '--no-restore', *properties], root, root / 'fixture-build.log')
    packaged.run_command([dotnet, 'pack', str(project), '-c', 'Release', '--no-build', '--no-restore', '--output', str(feed), *properties], root, root / 'fixture-pack.log')

def main():
    repository = Path(__file__).resolve().parents[1]
    sys.path.insert(0, str(repository / "tests"))
    import validate_contract_package_consumption as packaged
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages", type=Path, required=True)
    parser.add_argument("--version", help="Exact private candidate version; tagged releases use VERSION.")
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--host", type=Path)
    args = parser.parse_args()
    packages = args.packages.resolve()
    host = (args.host or repository / "src/Orbyss.Foundation.Host/bin/Release/net10.0/Orbyss.Foundation.Host.dll").resolve()
    version = args.version or (repository / "VERSION").read_text().strip()
    central_packages = ET.parse(repository / "Directory.Packages.props")
    cshells_version = next(entry.attrib["Version"] for entry in central_packages.findall(".//PackageVersion")
        if entry.attrib.get("Include") == "CShells.AspNetCore.Abstractions")
    evidence = repository / "artifacts/hosted-package-consumption"
    evidence.mkdir(parents=True, exist_ok=True)
    with nullcontext(tempfile.mkdtemp(prefix="run-", dir=evidence)) as directory:
        root = Path(directory)
        feed = root / "feed"
        feed.mkdir()
        identities = []
        for name in ("Json", "WebDefaults", "Web.HostedPages", "Web.ProblemDetails", "Authentication.BffCookie"):
            package = packages / f"Orbyss.Foundation.{name}.{version}.nupkg"
            shutil.copy2(package, feed / package.name)
            identities.append("Orbyss.Foundation." + name)
        # The native loader consumes the selected TFM nuspec closure, including
        # dependencies the SDK may otherwise prune as provided by net10 itself.
        for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"):
            (root / name).write_text("<Project/>", encoding="utf-8")
        restore_config = root / "NuGet.config"
        packaged.write_restore_configuration(repository, packages, restore_config)
        packaged.restore_runtime_closure(root, identities, version, args.dotnet, restore_config,
            root / "nuget-cache", ["-p:TargetFramework=net10.0", "-p:RestorePackagesWithLockFile=true"], root, root)
        # Host contracts are deliberately private in feature nuspec dependencies.
        # Restore their exact central pins explicitly so shared-DLL admission proves
        # the native Host bytes without treating these archives as loader roots.
        host_contracts = ET.Element("Project", {"Sdk": "Microsoft.NET.Sdk"})
        properties = ET.SubElement(host_contracts, "PropertyGroup")
        ET.SubElement(properties, "RestoreEnablePackagePruning").text = "false"
        references = ET.SubElement(host_contracts, "ItemGroup")
        for identity in ("CShells.Abstractions", "CShells.AspNetCore.Abstractions"):
            ET.SubElement(references, "PackageReference", {"Include": identity, "Version": f"[{cshells_version}]"})
        host_contract_project = root / "HostProvidedContracts.csproj"
        ET.indent(host_contracts, space="  ")
        ET.ElementTree(host_contracts).write(host_contract_project, encoding="utf-8", xml_declaration=True)
        contract_restore = packaged.restore_command(args.dotnet, host_contract_project, restore_config,
            root / "nuget-cache", ["-p:TargetFramework=net10.0", "-p:RestorePackagesWithLockFile=true"])
        packaged.run_command(contract_restore, root, root / "host-contracts-restore.log")
        packaged.run_command([*contract_restore, "--locked-mode"], root, root / "host-contracts-locked-restore.log")
        build_fixture(repository, root, feed, version, cshells_version, args.dotnet, restore_config, packaged)
        packaged.copy_restored_packages(root / "nuget-cache", feed)
        host_hashes = packaged.preserve_host_payload(host, root / "host")
        host = root / "host" / host.name
        shared_bindings = packaged.copy_runtime_feed(feed, root / "packages", host, version, cshells_version)
        package_hashes = {path.name: packaged.sha256(path) for path in feed.glob("*.nupkg")}
        (root / "inputs.json").write_text(json.dumps({"version": version,
            "packages": package_hashes, "hostRuntime": host_hashes,
            "hostProvidedSharedPackages": shared_bindings}, indent=2) + "\n", encoding="utf-8")
        deployment = root / "hosted-pages"
        deployment.mkdir()
        runtime = b"export function mount(target, bootstrap) { target.textContent = bootstrap.title; }"
        (deployment / "app.js").write_bytes(runtime)
        (deployment / "vite.json").write_text(json.dumps({"main": {"file": "app.js", "isEntry": True}}))
        (deployment / "branding.json").write_text(json.dumps({
            "defaultLocale": "en", "locales": {"en": {
                "title": "Packaged hosted form", "purpose": "Settings-only package consumption.",
                "logoAlt": "", "loadingText": "Loading", "failureText": "Unavailable", "noScriptText": "Enable JavaScript."
            }}
        }))
        subprocess.run([os.sys.executable, str(repository / "scripts/create_hosted_manifest.py"),
                        "--root", str(deployment), "--vite", "vite.json", "--branding", "branding.json",
                        "--entry", "main", "--revision", "release-1", "--form-release", "forms-1"],
                       check=True, capture_output=True, text=True)
        digest = hashlib.sha256((deployment / "manifest.json").read_bytes()).hexdigest()
        settings = {
            "CShells": {"Shells": {"public": {
                "Features": {"Orbyss.Foundation.Web.HostedPages": True},
                "Configuration": {"WebRouting": {"Path": "public"}, "Foundation": {"HostedPages": {
                    "Root": "hosted-pages", "CurrentRevision": "release-1", "ManifestSha256": digest
                }}}
            }}}
        }
        private_deployment = root / 'private-pages'
        shutil.copytree(deployment, private_deployment)
        private_manifest = json.loads((private_deployment / 'manifest.json').read_text())
        private_manifest['revisions'][0]['locales']['en']['title'] = 'Packaged private fixture'
        private_bytes = json.dumps(private_manifest).encode()
        (private_deployment / 'manifest.json').write_bytes(private_bytes)
        settings['CShells']['Shells']['private'] = {
            'Features': {'Orbyss.Foundation.Web.HostedPages': True, 'Orbyss.Foundation.Authentication.BffCookie': True, 'Foundation.HostedAccessFixture': True},
            'Configuration': {'WebRouting': {'Path': 'private'}, 'Foundation': {
                'Web': {'Authority': 'https://identity.example/hosted', 'ClientId': 'fixture', 'ClientSecret': 'synthetic-secret', 'Audience': 'fixture-api', 'AllowHttpForLocalDevelopment': True},
                'HostedPages': {'Root': 'private-pages', 'CurrentRevision': 'release-1', 'ManifestSha256': hashlib.sha256(private_bytes).hexdigest(),
                    'Routes': [{'Id': name, 'Path': '/' + name, 'Page': {'Mode': 'Authenticated', 'Permission': 'pages.' + name, 'ResponsePolicy': 'private'},
                        'Bootstrap': {'Mode': 'Authenticated', 'ResponsePolicy': 'private'}} for name in ('alpha', 'beta')]}
            }}
        }
        (root / "shells.json").write_text(json.dumps(settings))
        configuration = json.loads((root / "host/appsettings.json").read_text(encoding="utf-8"))
        paths = configuration.setdefault("Nuplane", {}).setdefault("Paths", {})
        paths.update(StateFilePath=str(root / "nuplane-state.json"), PackageInstallRoot=str(root / "installed"),
                     ActiveStoreRoot=str(root / "packages/.installed"))
        (root / "appsettings.json").write_text(json.dumps(configuration), encoding="utf-8")
        environment = {key: value for key, value in os.environ.items()
                       if not key.upper().startswith(("CSHELLS__", "FOUNDATION__", "NUPLANE__"))}
        environment['ASPNETCORE_ENVIRONMENT'] = environment['DOTNET_ENVIRONMENT'] = 'Development'
        log_path = root / "host.log"
        with log_path.open("w", encoding="utf-8") as log:
            process = subprocess.Popen([args.dotnet, str(host), "--contentRoot", str(root), "--urls", "http://127.0.0.1:0"],
                                       cwd=root, env=environment, stdout=log, stderr=subprocess.STDOUT,
                                       creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
            try:
                deadline = time.monotonic() + 75
                address = None
                while time.monotonic() < deadline:
                    text = log_path.read_text(encoding="utf-8", errors="replace")
                    match = re.search(r"Now listening on: (http://127\.0\.0\.1:\d+)", text)
                    if match:
                        address = match.group(1)
                        break
                    if process.poll() is not None:
                        raise AssertionError("Package-loading Host exited:\n" + text)
                    time.sleep(0.2)
                if not address: raise AssertionError("Package-loading Host did not start:\n" + log_path.read_text())
                with urllib.request.urlopen(address + "/public/", timeout=15) as response:
                    html = response.read().decode("utf-8")
                    assert "<h1>Packaged hosted form</h1>" in html
                    assert response.headers["Cache-Control"] == "no-store"
                    assert response.headers["X-Frame-Options"] == "DENY"
                bootstrap_path = re.search(r'data-bootstrap="([^"]+)"', html).group(1)
                with urllib.request.urlopen(address + bootstrap_path, timeout=15) as response:
                    bootstrap = json.load(response)
                with urllib.request.urlopen(address + bootstrap["runtime"], timeout=15) as response:
                    assert response.read() == runtime
                    assert "immutable" in response.headers["Cache-Control"]
                private_checks = qualify_private_host(address, root)
                assert package_hashes == {path.name: packaged.sha256(path) for path in feed.glob("*.nupkg")}
                assert all(packaged.sha256(root / "host" / relative) == digest
                           for relative, digest in host_hashes.items())
                (root / "result.json").write_text(json.dumps({"status": "passed-local-http", "version": version,
                    "settingsOnlyActivation": True, "packageHashesVerified": True,
                    "hostRuntimeHashesVerified": True, "privateDispatchChecks": private_checks,
                    "releaseQualification": False}, indent=2) + "\n", encoding="utf-8")
                print("Actual Nuplane Host consumed public/private hosted pages with BFF/route/bootstrap/data/logout checks. Evidence: " + str(root))
            finally:
                process.terminate()
                try: process.wait(timeout=15)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=15)

if __name__ == "__main__":
    main()
