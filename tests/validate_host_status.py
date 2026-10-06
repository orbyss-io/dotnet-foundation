"""Exercise native managed-Host routing failures through two actually loaded shells."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import http.client
from http.cookies import SimpleCookie
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import time
import uuid
import zipfile
import xml.etree.ElementTree as ET

import validate_contract_package_consumption as packaged


def main() -> None:
    repository = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages", type=Path, required=True, help="Complete qualified fixture/dependency feed.")
    parser.add_argument("--host", type=Path, required=True)
    parser.add_argument("--shells", type=Path, required=True, help="Qualified two-shell fixture configuration.")
    parser.add_argument("--dotnet", default="dotnet")
    args = parser.parse_args()
    root = repository / "artifacts/host-status" / (
        datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ-") + uuid.uuid4().hex[:8])
    root.mkdir(parents=True)
    hashes = packaged.preserve_host_payload(args.host.resolve(), root / "host")
    configuration = json.loads((root / "host/appsettings.json").read_text(encoding="utf-8"))
    shared = {item["Name"].casefold() for item in configuration["Nuplane"]["Loading"]["SharedAssemblies"]}
    runtime = root / "runtime"
    runtime.mkdir()
    feed = runtime / "packages"
    feed.mkdir()
    for package in args.packages.resolve().glob("*.nupkg"):
        with zipfile.ZipFile(package) as archive:
            nuspec = next(name for name in archive.namelist() if name.endswith(".nuspec"))
            identity = ET.fromstring(archive.read(nuspec)).find("./{*}metadata/{*}id").text.casefold()
        if identity not in shared:
            shutil.copy2(package, feed / package.name)
    shells = json.loads(args.shells.resolve().read_text(encoding="utf-8"))
    for shell in shells["CShells"]["Shells"].values():
        # Storage is irrelevant to routing admission; no database initialization or I/O runs.
        shell["Features"]["ContractFixture.PostgreSql"] = False
    # With no path opt-in, legitimate root routes select a shell by endpoint ownership.
    # Wrong-method synthetic endpoints carry no owner. Explicit Path="" would instead
    # opt into native root fallback and bring unmatched requests into the first shell.
    shells["CShells"]["Shells"]["first"]["Configuration"].pop("WebRouting", None)
    (runtime / "shells.json").write_text(json.dumps(shells), encoding="utf-8")
    (runtime / "appsettings.json").write_text(json.dumps(configuration), encoding="utf-8")
    (runtime / "hostsettings.json").write_text(json.dumps({"Nuplane": {
        "Setup": {"StateFilePath": str(runtime / "state.json")},
        "FeedResolution": {"PackageInstallRoot": str(runtime / "installed")},
        "Loading": {"ActiveStoreRoot": str(feed / ".installed")}}}), encoding="utf-8")
    environment = {key: value for key, value in os.environ.items()
                   if not key.upper().startswith(("CSHELLS__", "FOUNDATION__", "NUPLANE__"))}
    log_path = root / "host.log"
    observations = []
    with log_path.open("w", encoding="utf-8") as log:
        process = subprocess.Popen([args.dotnet, str(root / "host" / args.host.name),
            "--contentRoot", str(runtime), "--urls", "http://127.0.0.1:0"], cwd=runtime,
            env=environment, stdout=log, stderr=subprocess.STDOUT,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        try:
            deadline = time.monotonic() + 75
            while time.monotonic() < deadline:
                text = log_path.read_text(encoding="utf-8", errors="replace")
                match = re.search(r"Now listening on: http://127\.0\.0\.1:(\d+)", text)
                if match:
                    port = int(match.group(1))
                    break
                assert process.poll() is None, "Native Host exited; inspect " + str(log_path)
                time.sleep(0.2)
            else:
                raise AssertionError("Native Host startup timed out; inspect " + str(log_path))

            def request(method: str, path: str, status: int, shell_title: str | None = None,
                        code: str = "request_failed", cookie: str | None = None, platform: bool = False,
                        accept: str = "text/html") -> None:
                connection = http.client.HTTPConnection("127.0.0.1", port, timeout=15)
                try:
                    headers = {"Accept": accept}
                    if cookie:
                        headers["Cookie"] = cookie
                    connection.request(method, path, headers=headers)
                    response = connection.getresponse()
                    body = response.read()
                    record = {"method": method, "path": path, "status": response.status,
                              "contentType": response.getheader("Content-Type"),
                              "allow": response.getheader("Allow"), "bytes": len(body),
                              "body": body.decode("utf-8", errors="replace")}
                    observations.append(record)
                    (root / "observations.json").write_text(json.dumps(observations, indent=2), encoding="utf-8")
                    assert response.status == status, record
                    if platform:
                        assert len(body) <= 65_536, record
                        assert response.getheader("Content-Type", "").startswith("text/plain"), record
                        assert "Status Code: " + str(status) in record["body"], record
                        if status == 405:
                            assert "GET" in response.getheader("Allow", ""), record
                        return
                    assert response.getheader("Content-Type") == "application/problem+json", record
                    assert response.getheader("Cache-Control") == "no-store", record
                    assert response.getheader("Content-Length") == str(len(body)), record
                    assert len(body) <= 65_536, record
                    problem = json.loads(body)
                    assert problem["status"] == status and problem["code"] == code, record
                    assert problem["fieldErrors"] == [] and problem["correlationId"] == problem["traceId"], record
                    assert problem["correlationId"] and "FIXTURE_PRIVATE_SECRET" not in record["body"], record
                    if shell_title:
                        assert problem["title"].startswith(shell_title + ":"), record
                    else:
                        assert problem["title"] == {404: "Not Found", 405: "Method Not Allowed",
                                                    500: "Request Failed"}[status], record
                    if status == 405:
                        assert "GET" in response.getheader("Allow", ""), record
                finally:
                    connection.close()

            # With no root-path opt-in, these requests have no selected shell.
            # The neutral Host uses negotiated ASP.NET formatting and native text fallback.
            request("DELETE", "/snapshot", 405, platform=True, accept="application/json")
            request("GET", "/unmatched-route", 404, platform=True, accept="application/json")
            request("GET", "/outside-all-shells", 404, platform=True)
            # Shell-produced problems retain their request-scoped policy, including global feature off.
            request("GET", "/status/409", 409, "first")
            request("GET", "/second/auth/403", 403, "second", "authorization_denied")
            request("GET", "/problem-large", 500)
            request("GET", "/second/problem-large", 409, "second", "fixture_conflict")
            # A selected prefix can reach a native empty status after authorization. Its
            # request scope survives outer middleware processing; global exception feature is off.
            connection = http.client.HTTPConnection("127.0.0.1", port, timeout=15)
            try:
                connection.request("POST", "/second/fixture-cookie/0")
                response = connection.getresponse()
                assert response.status == 200 and response.read(), "Fixture cookie issue failed."
                cookies = SimpleCookie()
                for key, value in response.getheaders():
                    if key.casefold() == "set-cookie":
                        cookies.load(value)
                cookie = "; ".join(key + "=" + value.value for key, value in cookies.items())
                assert cookie, "Fixture cookie was absent."
            finally:
                connection.close()
            request("DELETE", "/second/snapshot", 405, "second", cookie=cookie)
            request("GET", "/second/unmatched-route", 404, "second", cookie=cookie)
            assert all(packaged.sha256(root / "host" / relative) == digest for relative, digest in hashes.items())
            assert "Framework request failure" not in log_path.read_text(encoding="utf-8", errors="replace")
            (root / "result.json").write_text(json.dumps({"status": "qualified", "neutralHost": True,
                "hostRuntimeFiles": hashes, "sharedHostAssemblies": sorted(shared),
                "observations": len(observations)}, indent=2), encoding="utf-8")
            print("Actual managed Host 404/405 and shell enrichment passed: " + str(root))
        finally:
            packaged.terminate(process)


if __name__ == "__main__":
    main()
