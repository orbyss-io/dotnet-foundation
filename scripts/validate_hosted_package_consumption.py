"""Verify settings-only hosted-page consumption through the actual Nuplane package-loading Host."""
import argparse
from contextlib import nullcontext
import hashlib
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
    evidence = repository / "artifacts/hosted-package-consumption"
    evidence.mkdir(parents=True, exist_ok=True)
    with nullcontext(tempfile.mkdtemp(prefix="run-", dir=evidence)) as directory:
        root = Path(directory)
        feed = root / "feed"
        feed.mkdir()
        identities = []
        for name in ("Json", "WebDefaults", "Web.HostedPages"):
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
        packaged.copy_restored_packages(root / "nuget-cache", feed)
        packaged.copy_runtime_feed(feed, root / "packages")
        host_hashes = packaged.preserve_host_payload(host, root / "host")
        host = root / "host" / host.name
        package_hashes = {path.name: packaged.sha256(path) for path in feed.glob("*.nupkg")}
        (root / "inputs.json").write_text(json.dumps({"version": version,
            "packages": package_hashes, "hostRuntime": host_hashes}, indent=2) + "\n", encoding="utf-8")
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
        (root / "shells.json").write_text(json.dumps(settings))
        configuration = json.loads((root / "host/appsettings.json").read_text(encoding="utf-8"))
        paths = configuration.setdefault("Nuplane", {}).setdefault("Paths", {})
        paths.update(StateFilePath=str(root / "nuplane-state.json"), PackageInstallRoot=str(root / "installed"),
                     ActiveStoreRoot=str(root / "packages/.installed"))
        (root / "appsettings.json").write_text(json.dumps(configuration), encoding="utf-8")
        environment = {key: value for key, value in os.environ.items()
                       if not key.upper().startswith(("CSHELLS__", "FOUNDATION__", "NUPLANE__"))}
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
                assert package_hashes == {path.name: packaged.sha256(path) for path in feed.glob("*.nupkg")}
                assert all(packaged.sha256(root / "host" / relative) == digest
                           for relative, digest in host_hashes.items())
                (root / "result.json").write_text(json.dumps({"status": "qualified", "version": version,
                    "settingsOnlyActivation": True, "packageHashesVerified": True,
                    "hostRuntimeHashesVerified": True}, indent=2) + "\n", encoding="utf-8")
                print("Actual Nuplane Host consumed packaged hosted pages through settings only. Evidence: " + str(root))
            finally:
                process.terminate()
                try: process.wait(timeout=15)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=15)

if __name__ == "__main__":
    main()
