"""Qualify exact private Foundation packages through the actual Host/Nuplane and PostgreSQL."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import http.client
import http.cookiejar
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import time
import urllib.error
import urllib.request
import uuid
import xml.etree.ElementTree as ET
import zipfile

PROJECTS = ("Fixture.Core", "Fixture", "Fixture.PostgreSql", "Fixture.Api")
FEATURES = {
    "Fixture": ("Foundation.ContractFixture", "ContractFixture", ["Orbyss.Foundation.Execution"], []),
    "Fixture.PostgreSql": ("Foundation.ContractFixture.PostgreSql", "ContractFixture.PostgreSql", [], []),
    "Fixture.Api": ("Foundation.ContractFixture.Api", "ContractFixture.Api",
                    ["Orbyss.Foundation.Authentication", "Orbyss.Foundation.Json.AspNetCore"],
                    ["/snapshot/{mode?}", "/echo", "/large", "/bypass", "/invalid-output",
                     "/forged-problem/{status:int}", "/ignored-request-fake-problem", "/denied",
                     "/auth/{status:int}", "/status/{status:int}", "/native-conflict", "/argument",
                     "/problem-large", "/started", "/storage", "/storage-stage-cancel", "/reload/{shell}", "/fixture-cookie/{permissions:int}"]),
}
PRIVATE_MARKER = "FIXTURE_PRIVATE_SECRET"
HOST_CONTRACT_PACKAGES = {"cshells.abstractions", "cshells.aspnetcore.abstractions"}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def preserve_host_payload(host: Path, destination: Path) -> dict[str, str]:
    files = set(host.parent.glob("*.dll"))
    files.update(host.parent.glob("*.deps.json"))
    files.update(host.parent.glob("*.runtimeconfig.json"))
    files.add(host.parent / "appsettings.json")
    runtimes = host.parent / "runtimes"
    if runtimes.is_dir():
        files.update(path for path in runtimes.rglob("*") if path.is_file())
    assert all(path.is_file() for path in files), "The complete built Host payload must include its configuration."
    hashes = {path.relative_to(host.parent).as_posix(): sha256(path) for path in sorted(files)}
    for relative, expected in hashes.items():
        source = host.parent / relative
        copied = destination / relative
        copied.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, copied)
        assert sha256(copied) == expected == sha256(source), "Host payload changed while qualification captured it."
    return hashes


def run_command(command: list[str], cwd: Path, log_path: Path) -> None:
    with log_path.open("w", encoding="utf-8") as log:
        log.write(json.dumps(command) + "\n")
        log.flush()
        completed = subprocess.run(command, cwd=cwd, stdout=log, stderr=subprocess.STDOUT,
                                   timeout=180, encoding="utf-8")
    if completed.returncode:
        raise AssertionError(f"Command failed ({completed.returncode}); preserved output: {log_path}")


def write_restore_configuration(repository: Path, feed: Path, destination: Path) -> list[dict]:
    """Preserve configured preview sources while isolating candidate ownership and cache."""
    document = ET.parse(repository / "NuGet.config")
    sources = document.getroot().find("packageSources")
    mappings = document.getroot().find("packageSourceMapping")
    assert sources is not None and mappings is not None, "Repository restore sources/mapping are required."
    key = "Foundation contract candidate"
    ET.SubElement(sources, "add", {"key": key, "value": str(feed)})
    mapping = ET.SubElement(mappings, "packageSource", {"key": key})
    # Specific patterns outrank the public wildcard. Candidate dependencies cannot silently
    # resolve from a developer cache or public Foundation baseline.
    for pattern in ("Orbyss.Foundation.*", "Foundation.ContractFixture", "Foundation.ContractFixture.*"):
        ET.SubElement(mapping, "package", {"pattern": pattern})
    ET.indent(document, space="  ")
    document.write(destination, encoding="utf-8", xml_declaration=True)
    return [{"key": entry.attrib["key"], "value": entry.attrib["value"]}
            for entry in sources.findall("add")]


def candidate_packages(packages: Path, feed: Path, version: str) -> tuple[list[str], list[dict]]:
    identities = []
    excluded = []
    for package in sorted(packages.glob("*.nupkg")):
        with zipfile.ZipFile(package) as archive:
            nuspec = next(name for name in archive.namelist() if name.endswith(".nuspec"))
            metadata = ET.fromstring(archive.read(nuspec))
            identity = metadata.find("./{*}metadata/{*}id").text
            package_version = metadata.find("./{*}metadata/{*}version").text
            types = {entry.attrib.get("name", "").casefold()
                     for entry in metadata.findall("./{*}metadata/{*}packageTypes/{*}packageType")}
        if identity.startswith("Orbyss.Foundation.") and package_version == version and types.intersection({"analyzer", "dotnettool"}):
            excluded.append({"identity": identity, "packageTypes": sorted(types), "sha256": sha256(package)})
        elif identity.startswith("Orbyss.Foundation.") and package_version == version:
            assert identity not in identities, f"Duplicate candidate package: {identity}"
            identities.append(identity)
            shutil.copy2(package, feed / package.name)
    assert identities, "The supplied feed contains no exact Foundation candidate packages."
    return identities, excluded


def restore_command(dotnet: str, project: Path, config: Path, cache: Path, properties: list[str]) -> list[str]:
    return [dotnet, "restore", str(project), "--configfile", str(config), "--no-http-cache",
            "--packages", str(cache), *properties]


def restore_runtime_closure(source: Path, identities: list[str], version: str, dotnet: str,
                            config: Path, cache: Path, properties: list[str], cwd: Path, run: Path) -> None:
    directory = source / "RuntimeDependencyClosure"
    directory.mkdir()
    project = ET.Element("Project", {"Sdk": "Microsoft.NET.Sdk"})
    settings = ET.SubElement(project, "PropertyGroup")
    ET.SubElement(settings, "RestoreEnablePackagePruning").text = "false"
    references = ET.SubElement(project, "ItemGroup")
    for identity in identities:
        ET.SubElement(references, "PackageReference", {"Include": identity, "Version": f"[{version}]"})
    ET.indent(project, space="  ")
    path = directory / "RuntimeDependencyClosure.csproj"
    ET.ElementTree(project).write(path, encoding="utf-8", xml_declaration=True)
    command = restore_command(dotnet, path, config, cache, properties)
    run_command(command, cwd, run / "runtime-closure-restore.log")
    run_command([*command, "--locked-mode"], cwd, run / "runtime-closure-locked-restore.log")
    assets = json.loads((directory / "obj/project.assets.json").read_text())
    assert all(entry["type"] != "project" for entry in assets["libraries"].values())


def copy_restored_packages(cache: Path, feed: Path) -> dict[str, str]:
    hashes = {}
    retained = {path.name.casefold(): path for path in feed.glob("*.nupkg")}
    for package in sorted(cache.rglob("*.nupkg")):
        digest = sha256(package)
        destination = retained.get(package.name.casefold(), feed / package.name)
        if destination.exists():
            assert sha256(destination) == digest, f"Package identity has conflicting bytes: {package.name}"
        else:
            shutil.copy2(package, destination)
            retained[package.name.casefold()] = destination
        hashes[package.name] = digest
    assert hashes, "Fresh dependency restore retained no packages."
    return hashes


def copy_runtime_feed(feed: Path, target: Path) -> None:
    target.mkdir()
    for package in feed.glob("*.nupkg"):
        with zipfile.ZipFile(package) as archive:
            nuspec = next(name for name in archive.namelist() if name.endswith(".nuspec"))
            identity = ET.fromstring(archive.read(nuspec)).find("./{*}metadata/{*}id").text.casefold()
        if identity not in HOST_CONTRACT_PACKAGES:
            shutil.copy2(package, target / package.name)


def terminate(process: subprocess.Popen) -> None:
    if process.poll() is None:
        process.terminate()
    try:
        process.wait(timeout=15)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=15)


def qualify_transport_startup(host: Path, root: Path, dotnet: str) -> list[dict]:
    """Reject invalid process transport limits without activating features or storage."""
    observations = []
    for key, value in (("MaxRequestBodyBytes", 0), ("MaxRequestHeadersBytes", 1023)):
        runtime = root / key
        runtime.mkdir(parents=True)
        (runtime / "packages").mkdir()
        (runtime / "appsettings.json").write_text(json.dumps({
            "Foundation": {"Boot": {"EagerShellActivation": False}},
            "Nuplane": {"Setup": {"AutomaticReconciliation": False, "Feeds": [
                {"Name": "empty", "DirectoryPath": "packages", "IncludePatterns": ["*"],
                 "Directory": {"Watch": False}}]}, "Loading": {"Enabled": False}}
        }), encoding="utf-8")
        (runtime / "shells.json").write_text(json.dumps({"CShells": {"Shells": {}}}), encoding="utf-8")
        (runtime / "hostsettings.json").write_text(json.dumps({"Foundation": {"Transport": {key: value}}}), encoding="utf-8")
        environment = os.environ.copy()
        environment["ASPNETCORE_ENVIRONMENT"] = "Production"
        environment["DOTNET_ENVIRONMENT"] = "Production"
        # External shell/transport overrides would make the preserved input inconclusive.
        for entry in tuple(environment):
            if entry.upper().startswith(("CSHELLS__", "FOUNDATION__", "NUPLANE__")):
                del environment[entry]
        log_path = runtime / "host-startup.log"
        with log_path.open("w", encoding="utf-8") as log:
            process = subprocess.Popen([dotnet, str(host), "--contentRoot", str(runtime), "--urls", "http://127.0.0.1:0"],
                cwd=runtime, env=environment, stdout=log, stderr=subprocess.STDOUT,
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
            try:
                exit_code = process.wait(timeout=30)
            except subprocess.TimeoutExpired:
                terminate(process)
                raise AssertionError(f"Invalid transport configuration did not reject startup: {log_path}")
        output = log_path.read_text(encoding="utf-8", errors="replace")
        assert exit_code != 0 and "Now listening on:" not in output, output
        assert f"Foundation:Transport:{key}" in output, f"Missing precise safe transport diagnostic: {log_path}"
        assert PRIVATE_MARKER not in output and "Activated shell" not in output
        observations.append({"key": key, "value": value, "startupRejected": True,
                             "applicationActivation": False, "exitCode": exit_code, "log": str(log_path)})
    return observations


def write_descriptors(source: Path, cshells_version: str) -> None:
    for project, (package, identity, dependencies, routes) in FEATURES.items():
        directory = source / project
        bindings = {path.relative_to(directory).as_posix(): hashlib.sha256(
            path.read_text(encoding="utf-8-sig").replace("\r\n", "\n").encode()).hexdigest()
                    for path in sorted(directory.rglob("*.cs"))}
        descriptor = {
            "schemaVersion": 2, "packageId": package,
            "features": [{"identity": identity, "featureDependencies": dependencies,
                          "runtimeDependencies": [], "routes": routes, "dormant": True,
                          "composeForOpenApi": False, "requiresContractCoverage": False}],
            "sourceSha256": bindings,
            "hostProvidedDependencies": [{"packageId": "CShells.Abstractions", "minimumVersion": cshells_version}]
        }
        if project == "Fixture.Api":
            descriptor["hostProvidedDependencies"].append(
                {"packageId": "CShells.AspNetCore.Abstractions", "minimumVersion": cshells_version})
        (directory / "feature.json").write_text(json.dumps(descriptor, indent=2) + "\n", encoding="utf-8")


def settings() -> dict:
    shells = {}
    for name in ("first", "second"):
        first = name == "first"
        shells[name] = {
            "Features": {"ContractFixture": True, "ContractFixture.Api": True,
                         "ContractFixture.PostgreSql": True, "Orbyss.Foundation.Authentication.BffCookie": True,
                         "Orbyss.Foundation.Web.ProblemDetails": first},
            "Configuration": {
                "WebRouting": {"Path": name}, "Fixture": {"ReplaceIdentityReader": not first},
                "Foundation": {
                    "Web": {"Authority": "https://identity.invalid/qualification", "ClientId": "fixture",
                            "ClientSecret": "qualification-only", "Audience": "fixture", "Scopes": ["openid"],
                            "AllowHttpForLocalDevelopment": True},
                    "Json": {"Profiles": {
                        "strict-request": {"Preset": "strict-request", "MaxBytes": 64 if first else 512, "MaxDepth": 32},
                        "success-response": {"Preset": "tolerant-response", "MaxBytes": 256 if first else 1024, "MaxDepth": 32},
                        "problem-response": {"Preset": "tolerant-response", "MaxBytes": 512 if first else 65536, "MaxDepth": 32},
                        "inspection-response": {"Preset": "tolerant-response", "MaxBytes": 8192, "MaxDepth": 32,
                                                "Extensions": ["value-sequence-v1"]}}},
                    "PostgreSql": {"Policies": {"fixture": {"OperationTimeout": "00:00:05",
                        "ConnectionTimeout": "00:00:01", "CommandTimeout": "00:00:01",
                        "LockTimeout": "00:00:00.500", "CancellationTimeout": "00:00:02"}}}
                }
            }
        }
    return {"CShells": {"Shells": shells}}


def start_host(host: Path, runtime: Path, connection: str, dotnet: str) -> tuple[subprocess.Popen, str, Path]:
    environment = os.environ.copy()
    for key in tuple(environment):
        if key.upper().startswith(("CSHELLS__", "FOUNDATION__", "NUPLANE__")):
            del environment[key]
    # Controlled loopback-only cookie conformance uses the explicit supported local-development mode.
    environment["ASPNETCORE_ENVIRONMENT"] = "Development"
    environment["DOTNET_ENVIRONMENT"] = "Development"
    for shell in ("first", "second"):
        environment[f"CShells__Shells__{shell}__Configuration__Foundation__PostgreSql__Policies__fixture__ConnectionString"] = connection
    log_path = runtime / "host.log"
    log = log_path.open("w", encoding="utf-8")
    process = subprocess.Popen([dotnet, str(host), "--contentRoot", str(runtime), "--urls", "http://127.0.0.1:0"],
                               cwd=runtime, env=environment, stdout=log, stderr=subprocess.STDOUT,
                               creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    log.close()
    deadline = time.monotonic() + 60
    while time.monotonic() < deadline:
        output = log_path.read_text(encoding="utf-8", errors="replace")
        match = re.search(r"Now listening on: (http://127\.0\.0\.1:\d+)", output)
        if match:
            return process, match.group(1), log_path
        if process.poll() is not None:
            raise AssertionError(f"Package-loading host exited; preserved output: {log_path}")
        time.sleep(0.2)
    terminate(process)
    raise AssertionError(f"Package-loading host did not become ready; preserved output: {log_path}")


def request(opener: urllib.request.OpenerDirector, base: str, path: str, payload: bytes | None = None,
            method: str | None = None) -> tuple[int, dict, bytes]:
    packet = urllib.request.Request(base + path, data=payload, method=method,
                                    headers={"Content-Type": "application/json"} if payload is not None else {})
    try:
        response = opener.open(packet, timeout=15)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        body = response.read(65537)
        assert len(body) <= 65536, f"Wire envelope exceeded finite read budget for {path}"
        assert PRIVATE_MARKER.encode() not in body, f"Private defect reached wire for {path}"
        return response.status, dict(response.headers), body


def problem(packet: tuple[int, dict, bytes], expected_status: int, code: str, shell: str,
            enriched: bool = True, maximum: int | None = None) -> dict:
    status, headers, body = packet
    document = json.loads(body)
    assert status == expected_status == document["status"], (status, expected_status, document)
    assert document["code"] == code, document
    assert document["correlationId"] == document["traceId"] and document["correlationId"], document
    assert isinstance(document["fieldErrors"], list), document
    assert headers.get("Content-Type", "").startswith("application/problem+json"), headers
    if enriched:
        assert document["title"].startswith(shell + ":"), document
    if maximum is not None:
        assert len(body) <= maximum, (len(body), maximum)
    return document


def qualify(base: str, version: str, evidence: Path) -> dict:
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    snapshots = {}
    problem_titles = []
    adversaries = [("forged200", "/first/forged-problem/200", None, "json_response_profile_bypass"),
                   ("forged409", "/first/forged-problem/409", None, "json_response_profile_bypass"),
                   ("ignoredRequest", "/first/ignored-request-fake-problem", b'{"message":"ignored"}', "json_request_profile_bypass")]
    packets = [(case, request(opener, base, path, payload), code) for case, path, payload, code in adversaries]
    provenance = [{"case": case, "status": packet[0], "bytes": len(packet[2]),
                   "boundedSafeFailure": packet[0] == 500 and len(packet[2]) <= 512 and b'"forged"' not in packet[2]}
                  for case, packet, _ in packets]
    (evidence / "provenance.json").write_text(json.dumps(provenance, indent=2), encoding="utf-8")
    cancellations = {}
    for shell in ("first", "second"):
        status, _, body = request(opener, base, f"/{shell}/storage-stage-cancel")
        cancellation = json.loads(body)
        cancellation["httpStatus"] = status
        cancellations[shell] = cancellation
    (evidence / "cancellation.json").write_text(json.dumps(cancellations, indent=2), encoding="utf-8")
    cancellation_safe = all(item["httpStatus"] == 200 and item.get("stageCanceled")
        and not item.get("outerExpired") and item.get("elapsedMilliseconds", 99999) < 2500
        and item.get("followupSucceeded") and item.get("scheduledAdvanceOccurred")
        and item.get("fastWriteCanceled") and not item.get("fastWriteCommitted") for item in cancellations.values())
    assert all(item["boundedSafeFailure"] for item in provenance) and cancellation_safe, (
        "Packaged admission/cancellation guarantees failed", {"provenance": provenance, "cancellation": cancellations})
    for _, packet, code in packets:
        problem(packet, 500, code, "first")
    for shell in ("first", "second"):
        status, _, body = request(opener, base, f"/{shell}/snapshot")
        assert status == 200, body
        snapshot = json.loads(body)
        assert snapshot["shell"] == shell and snapshot["labels"][0] == shell
        assert snapshot["structuralEquality"] and snapshot["deadlineRemainingMilliseconds"] > 0
        assert snapshot["subject"] == ("first-owner" if shell == "first" else "second-replacement")
        assert snapshot["identityReader"] == ("ValidatedAccountIdentityReader" if shell == "first" else "ControlledFixtureIdentityReader")
        expected_canonical = hashlib.sha256('{"message":"😀 < > é"}'.encode()).hexdigest()
        assert snapshot["canonicalSha256"] == expected_canonical
        assert all(item["noRuntimeMechanisms"] and item["noFeatureTypes"] for item in snapshot["contracts"])
        assert all(item["informationalVersion"].startswith(version) for item in snapshot["contracts"]), snapshot
        _, _, repeated_body = request(opener, base, f"/{shell}/snapshot")
        repeated = json.loads(repeated_body)
        assert repeated["labels"][1] == snapshot["labels"][1] and repeated["labels"][2] != snapshot["labels"][2]
        snapshots[shell] = snapshot
        denied = problem(request(opener, base, f"/{shell}/denied"), 409, "fixture_conflict", shell,
                         maximum=512 if shell == "first" else 65536)
        problem_titles.append(denied["title"])
        for status in (401, 403):
            code = "authentication_required" if status == 401 else "authorization_denied"
            problem(request(opener, base, f"/{shell}/auth/{status}"), status, code, shell)
        problem(request(opener, base, f"/{shell}/bypass"), 500, "json_response_profile_bypass", shell)
        problem(request(opener, base, f"/{shell}/invalid-output"), 500, "json_response_invalid_contract", shell)
        malformed = b'{"message":"one","message":"two"}'
        problem(request(opener, base, f"/{shell}/echo", malformed), 400, "json_duplicate_member", shell)
        status, _, body = request(opener, base, f"/{shell}/large")
        if shell == "first":
            problem((status, _, body), 500, "json_response_size_exceeded", shell, maximum=512)
        else:
            assert status == 200 and len(body) <= 1024 and json.loads(body)["message"] == "x" * 800
        big_problem = request(opener, base, f"/{shell}/problem-large")
        problem(big_problem, 500 if shell == "first" else 409,
                "request_failed" if shell == "first" else "fixture_conflict", shell,
                enriched=shell != "first", maximum=512 if shell == "first" else 65536)
        status, _, body = request(opener, base, f"/{shell}/storage")
        storage = json.loads(body)
        assert status == 200 and storage["plainFactoryRejected"] and storage["closedUnitRejected"]
        assert storage["independentQueriesSucceeded"] and storage["firstContextId"] != storage["secondContextId"]
        snapshot["storage"] = storage
        snapshot["cancellation"] = cancellations[shell]
    assert snapshots["first"]["labels"][1] != snapshots["second"]["labels"][1]
    assert snapshots["first"]["storage"]["dataSourceId"] != snapshots["second"]["storage"]["dataSourceId"]
    for mode in ("duplicate", "missing", "multiple"):
        problem(request(opener, base, "/first/snapshot/" + mode), 400, "fixture_identity_invalid", "first")
    for status in (400, 401, 403, 404, 405, 409, 413, 503, 500):
        code = {400: "invalid_request", 401: "authentication_required", 403: "authorization_denied"}.get(status, "request_failed")
        problem(request(opener, base, f"/first/status/{status}"), status, code, "first")
    problem(request(opener, base, "/first/native-conflict"), 409, "fixture_native_conflict", "first")
    problem(request(opener, base, "/first/argument"), 500, "request_failed", "first")
    # Stream bytes without Content-Length; exact64 is admitted and cap+one is a client413.
    from urllib.parse import urlsplit
    parsed = urlsplit(base)
    for size, expected in ((50, 200), (51, 413)):
        connection = http.client.HTTPConnection(parsed.hostname, parsed.port, timeout=15)
        payload = b'{"message":"' + b'x' * size + b'"}'
        connection.request("POST", "/first/echo", body=(payload[index:index + 7] for index in range(0, len(payload), 7)),
                           headers={"Content-Type": "application/json"}, encode_chunked=True)
        response = connection.getresponse()
        packet = (response.status, dict(response.headers), response.read(65537))
        connection.close()
        assert packet[0] == expected, packet
        if expected == 413:
            problem(packet, 413, "json_size_exceeded", "first")
    # Packaged BFF writes use the same response contract; cookie tickets stay shell-bound.
    assert json.loads(request(opener, base, "/first/bff/user")[2])["authenticated"] is False
    assert request(opener, base, "/first/fixture-cookie/0", b"", "POST")[0] == 200
    signed = json.loads(request(opener, base, "/first/bff/user")[2])
    assert signed["authenticated"] is True and signed["subject"] == "first-owner"
    assert json.loads(request(opener, base, "/second/bff/user")[2])["authenticated"] is False
    overflow_browser = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    assert request(overflow_browser, base, "/first/fixture-cookie/100", b"", "POST")[0] == 200
    problem(request(overflow_browser, base, "/first/bff/user"), 500, "json_response_size_exceeded", "first")
    antiforgery = request(opener, base, "/second/bff/antiforgery")
    assert antiforgery[0] == 200 and json.loads(antiforgery[2])["requestToken"]
    problem(request(opener, base, "/first/bff/logout", b"{}", "POST"), 400, "invalid_antiforgery_token", "first")
    # Actual package-owned factory units keep a previous generation alive during native shell drain.
    packet = request(opener, base, "/second/reload/first", b"", "POST")
    reload = json.loads(packet[2])
    assert packet[0] == 200 and reload["drainBlockedWhileHeld"] and reload["heldUnitStillWorked"]
    assert reload["drainCompletedAfterRelease"] and reload["oldDataSourceId"] != reload["newDataSourceId"]
    snapshots["reload"] = reload
    # A committed response stays200 and aborts; it cannot become a fabricated complete problem.
    with opener.open(base + "/first/started", timeout=15) as response:
        assert response.status == 200
        try:
            response.read()
            raise AssertionError("Committed failure falsely completed.")
        except http.client.IncompleteRead as error:
            assert PRIVATE_MARKER.encode() not in error.partial
    assert len(set(problem_titles)) == len(problem_titles)
    return snapshots


def main() -> None:
    repository = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages", type=Path, required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--host", type=Path, required=True)
    parser.add_argument("--dotnet", default="dotnet", help="Installed pinned dotnet executable; defaults to PATH for CI.")
    parser.add_argument("--cshells-version", default="0.0.29-preview.147")
    parser.add_argument("--postgres-connection", default=os.environ.get("FOUNDATION_POSTGRES_TEST_CONNECTION"))
    parser.add_argument("--artifacts", type=Path, default=repository / "artifacts/contract-package-consumption")
    parser.add_argument("--sdk-working-directory", type=Path, default=repository,
                        help="Explicit development substitute only; defaults to the repository's pinned SDK for CI.")
    args = parser.parse_args()
    if not re.fullmatch(r"\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?", args.version):
        parser.error("--version must be one exact NuGet version.")
    if not args.postgres_connection:
        parser.error("Actual disposable PostgreSQL qualification requires --postgres-connection or FOUNDATION_POSTGRES_TEST_CONNECTION.")
    packages, host = args.packages.resolve(), args.host.resolve()
    if not packages.is_dir() or not host.is_file():
        parser.error("The exact candidate package feed and built Host assembly must exist.")
    run = args.artifacts.resolve() / (datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ-") + uuid.uuid4().hex[:8])
    run.mkdir(parents=True)
    source, feed, runtime = run / "consumer", run / "feed", run / "runtime"
    shutil.copytree(repository / "tests/package-contracts", source)
    feed.mkdir()
    candidate_identities, candidate_exclusions = candidate_packages(packages, feed, args.version)
    expected_runtime_identities = {project.stem
        for project in (repository / "src").glob("Orbyss.Foundation*/*.csproj")
        if project.stem not in {"Orbyss.Foundation.Host", "Orbyss.Foundation.Build",
            "Orbyss.Foundation.OpenApi.Exporter", "Orbyss.Foundation.Analyzers"}}
    assert len(expected_runtime_identities) == 29, "The explicit runtime package family requires review."
    assert set(candidate_identities) == expected_runtime_identities, (
        "Exact candidate runtime package set mismatch", {
            "missing": sorted(expected_runtime_identities - set(candidate_identities)),
            "extra": sorted(set(candidate_identities) - expected_runtime_identities)})
    for project in source.rglob("*.csproj"):
        assert "ProjectReference" not in project.read_text(), "Independent qualification cannot use source ProjectReferences."
    write_descriptors(source, args.cshells_version)
    properties = [f"-p:FoundationCandidateVersion={args.version}", f"-p:CShellsCandidateVersion={args.cshells_version}"]
    sdk_cwd = args.sdk_working_directory.resolve()
    run_command([args.dotnet, "--info"], sdk_cwd, run / "toolchain.log")
    restore_configuration = run / "NuGet.config"
    restore_sources = write_restore_configuration(repository, feed, restore_configuration)
    cache = run / "package-cache"
    host_payload = run / "host"
    host_inventory = preserve_host_payload(host, host_payload)
    qualified_host = host_payload / host.name
    manifest = {"version": args.version, "host": {"path": str(host), "sha256": sha256(host)},
                "hostRuntimeFiles": host_inventory, "executedHost": str(qualified_host),
                "packages": {path.name: sha256(path) for path in sorted(feed.glob("*.nupkg"))},
                "candidatePackageIdentities": candidate_identities,
                "excludedNonRuntimeCandidatePackages": candidate_exclusions,
                "fixtureSources": {path.relative_to(source).as_posix(): sha256(path)
                                   for path in sorted(source.rglob("*.cs"))},
                "sdkWorkingDirectory": str(sdk_cwd), "sourceProjectReferences": False,
                "restoreSources": restore_sources, "restoreConfigurationSha256": sha256(restore_configuration),
                "freshPackageCache": str(cache),
                "runtimeClosurePackagePruning": False,
                "postgresql": "actual explicit disposable target; connection supplied only to Host environment"}
    inputs = run / "inputs.json"
    inputs.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    transport = qualify_transport_startup(qualified_host, run / "transport-startup", args.dotnet)
    restore_runtime_closure(source, candidate_identities, args.version, args.dotnet, restore_configuration,
                            cache, properties, sdk_cwd, run)
    for name in PROJECTS:
        project = source / name / (name + ".csproj")
        restore = restore_command(args.dotnet, project, restore_configuration, cache, properties)
        run_command(restore, sdk_cwd, run / (name + "-restore.log"))
        run_command([*restore, "--locked-mode"], sdk_cwd, run / (name + "-locked-restore.log"))
        assets = json.loads((project.parent / "obj/project.assets.json").read_text())
        assert all(item["type"] != "project" for item in assets["libraries"].values()), "A source project escaped into packaged consumption."
        run_command([args.dotnet, "pack", str(project), "-c", "Release", "--no-restore", "-o", str(feed), *properties],
                    sdk_cwd, run / (name + "-pack.log"))
    core_package = feed / f"Foundation.ContractFixture.Core.{args.version}.nupkg"
    with zipfile.ZipFile(core_package) as archive:
        assert "orbyss-foundation/feature.json" not in archive.namelist(), "Contract-only Core was advertised as an activated feature."
    restored_packages = copy_restored_packages(cache, feed)
    runtime.mkdir()
    copy_runtime_feed(feed, runtime / "packages")
    configuration = qualified_host.parent / "appsettings.json"
    if not configuration.is_file():
        configuration = repository / "src/Orbyss.Foundation.Host/appsettings.json"
    shutil.copy2(configuration, runtime / "appsettings.json")
    (runtime / "shells.json").write_text(json.dumps(settings(), indent=2), encoding="utf-8")
    (runtime / "hostsettings.json").write_text(json.dumps({
        "Foundation": {"Transport": {"MaxRequestBodyBytes": 1024}},
        "Nuplane": {"Setup": {"StateFilePath": str(runtime / "nuplane-store-state.json")},
                    "FeedResolution": {"PackageInstallRoot": str(runtime / "installed")},
                    "Loading": {"ActiveStoreRoot": str(runtime / "packages/.installed")}}}), encoding="utf-8")
    manifest.update({"packages": {path.name: sha256(path) for path in sorted(feed.glob("*.nupkg"))},
                     "freshRestoredPackages": restored_packages, "transportStartup": transport,
                     "hostProvidedContractPackages": sorted(HOST_CONTRACT_PACKAGES)})
    inputs.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    process = None
    try:
        process, address, log_path = start_host(qualified_host, runtime, args.postgres_connection, args.dotnet)
        observations = qualify(address, args.version, run)
        (run / "observations.json").write_text(json.dumps(observations, indent=2), encoding="utf-8")
    finally:
        if process is not None:
            terminate(process)
    output = (runtime / "host.log").read_text(encoding="utf-8", errors="replace")
    assert PRIVATE_MARKER not in output, "Private password/SQL/body reached native host diagnostics."
    assert "Framework request failure System.InvalidOperationException" in output, "Committed native failure diagnostics were suppressed instead of redacted."
    (run / "result.json").write_text(json.dumps({"status": "passed", "version": args.version,
        "actualHost": True, "actualNugetPackages": True, "actualPostgreSql": True,
        "twoShells": True, "historicalEvidencePreserved": True}, indent=2), encoding="utf-8")
    print(f"Actual Nuplane/package/Core/native-handler/profile/BFF/PostgreSQL shell qualification passed. Evidence: {run}")


if __name__ == "__main__":
    main()
