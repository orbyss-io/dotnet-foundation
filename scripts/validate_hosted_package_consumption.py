"""Verify settings-only hosted-page consumption through the actual Nuplane package-loading Host."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import time
import urllib.request

def main():
    repository = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages", type=Path, required=True)
    args = parser.parse_args()
    packages = args.packages.resolve()
    host = repository / "src/Orbyss.Foundation.Host/bin/Release/net10.0/Orbyss.Foundation.Host.dll"
    version = (repository / "VERSION").read_text().strip()
    with tempfile.TemporaryDirectory(prefix="orbyss-host-consumption-") as directory:
        root = Path(directory)
        (root / "packages").mkdir()
        for name in ("Json", "WebDefaults", "Web.HostedPages"):
            package = packages / f"Orbyss.Foundation.{name}.{version}.nupkg"
            shutil.copy2(package, root / "packages" / package.name)
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
        shutil.copy2(repository / "src/Orbyss.Foundation.Host/appsettings.json", root / "appsettings.json")
        log_path = root / "host.log"
        with log_path.open("w", encoding="utf-8") as log:
            process = subprocess.Popen(["dotnet", str(host), "--contentRoot", str(root), "--urls", "http://127.0.0.1:0"],
                                       cwd=root, stdout=log, stderr=subprocess.STDOUT,
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
                print("Actual Nuplane Host consumed packaged hosted pages through settings only.")
            finally:
                process.terminate()
                try: process.wait(timeout=15)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=15)

if __name__ == "__main__":
    main()
