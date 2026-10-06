"""Bind an image's portable Host payload to passed F6 inputs without rebuilding source."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET
import zipfile


SHARED_HOST = {
    "CShells.Abstractions": "0.0.29-preview.147",
    "CShells.AspNetCore.Abstractions": "0.0.29-preview.147",
}
REQUIRED_FILES = {
    "Orbyss.Foundation.Host.dll", "Orbyss.Foundation.Host.deps.json",
    "Orbyss.Foundation.Host.runtimeconfig.json", "appsettings.json", "shells.json",
    *(identity + ".dll" for identity in SHARED_HOST),
}
F6_FLAGS = ("actualHost", "actualNugetPackages", "actualPostgreSql", "twoShells", "neutralHost", "actualCustomProblemComposition")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def read_json(path: Path) -> dict:
    value = json.loads(path.read_text(encoding="utf-8"))
    require(isinstance(value, dict), "Qualification JSON must be an object: " + str(path))
    return value


def is_link(path: Path) -> bool:
    return path.is_symlink() or getattr(path, "is_junction", lambda: False)()


def runtime_path(relative: str) -> bool:
    name = PurePosixPath(relative)
    return (name.parts[0] == "runtimes" and len(name.parts) > 1) or (
        name.parts[0] == ".orbyss-foundation" and len(name.parts) == 2
        and relative.casefold().endswith(".json")) or (
        len(name.parts) == 1 and relative.casefold().endswith((".dll", ".json")))


def admitted_inventory(value: object) -> dict[str, str]:
    require(isinstance(value, dict) and bool(value), "F6 inputs must bind the complete Host runtime inventory.")
    inventory = {}
    identities = set()
    for relative, digest in value.items():
        require(isinstance(relative, str) and relative and "\\" not in relative and ":" not in relative,
                "Host inventory paths must be portable relative paths.")
        name = PurePosixPath(relative)
        require(not name.is_absolute() and all(part not in ("", ".", "..") for part in relative.split("/")),
                "Host inventory path escapes the payload: " + relative)
        require(runtime_path(relative), "Unrecognized Host runtime inventory entry: " + relative)
        require(relative.casefold() not in identities, "Duplicate case-insensitive Host runtime identity.")
        require(isinstance(digest, str) and re.fullmatch(r"[a-f0-9]{64}", digest) is not None,
                "Host runtime inventory requires canonical SHA256 hashes.")
        identities.add(relative.casefold())
        inventory[relative] = digest
    require(REQUIRED_FILES <= inventory.keys(), "F6 inventory omits required Host/shared runtime files.")
    return inventory


def payload_inventory(payload: Path) -> tuple[dict[str, str], list[str]]:
    require(payload.is_dir() and not is_link(payload), "Payload must be a real directory.")
    inventory, ancillary = {}, []
    identities = set()
    for path in sorted(payload.rglob("*")):
        require(not is_link(path), "Payload links/junctions are not portable admitted files: " + str(path))
        if not path.is_file():
            continue
        relative = path.relative_to(payload).as_posix()
        if runtime_path(relative):
            require(relative.casefold() not in identities, "Payload duplicates a case-insensitive runtime identity.")
            identities.add(relative.casefold())
            inventory[relative] = sha256(path)
        else:
            ancillary.append(relative)
    return inventory, ancillary


def read_passed_inputs(inputs_path: Path, version: str | None) -> tuple[dict, dict, str]:
    inputs = read_json(inputs_path)
    result = read_json(inputs_path.parent / "result.json")
    selected = version or inputs.get("version")
    require(isinstance(selected, str) and bool(selected), "Select an exact qualification version.")
    require(inputs.get("version") == selected == result.get("version"), "F6 input/result version mismatch.")
    require(result.get("status") == "passed" and all(result.get(flag) is True for flag in F6_FLAGS),
            "A passed actual Host/package/PostgreSQL/two-shell F6 result is required.")
    return inputs, result, selected


def select_inputs(root: Path, version: str) -> Path:
    require(root.is_dir(), "Qualification root is missing.")
    matches = []
    for result_path in sorted(root.rglob("result.json")):
        try:
            result = read_json(result_path)
        except (ValueError, OSError):
            continue
        if result.get("version") != version or result.get("status") != "passed":
            continue
        if not all(result.get(flag) is True for flag in F6_FLAGS):
            continue
        inputs_path = result_path.parent / "inputs.json"
        read_passed_inputs(inputs_path, version)
        matches.append(inputs_path)
    require(len(matches) == 1, f"Expected exactly one passed F6 result for {version}; found {len(matches)}.")
    return matches[0]


def verify_archives(packages: Path, version: str, inputs: dict, inventory: dict[str, str]) -> list[dict]:
    require(packages.is_dir(), "Runtime package directory is missing.")
    hashes = inputs.get("packages")
    require(isinstance(hashes, dict), "F6 inputs must bind runtime archive hashes.")
    by_name = {}
    for name, digest in hashes.items():
        require(isinstance(name, str) and PurePosixPath(name).name == name and "\\" not in name,
                "Qualified archive names must be plain filenames.")
        require(name.casefold() not in by_name, "Duplicate qualified archive identity.")
        by_name[name.casefold()] = digest
    found = {}
    required = {identity.casefold(): identity for identity in SHARED_HOST}
    for archive_path in sorted(packages.glob("*.nupkg")):
        require(not is_link(archive_path), "Runtime archive must not be a link.")
        with zipfile.ZipFile(archive_path) as archive:
            nuspecs = [entry for entry in archive.infolist() if entry.filename.endswith(".nuspec")]
            require(len(nuspecs) == 1 and nuspecs[0].file_size <= 256 * 1024, "Invalid package metadata inventory.")
            metadata = ET.fromstring(archive.read(nuspecs[0]))
            identity = metadata.find("./{*}metadata/{*}id")
            package_version = metadata.find("./{*}metadata/{*}version")
            require(identity is not None and identity.text and package_version is not None, "Missing package identity/version.")
            key = identity.text.casefold()
            if key not in required:
                continue
            require(key not in found, "Duplicate shared Host package: " + identity.text)
            require(package_version.text == SHARED_HOST[required[key]], "Shared Host package version differs from F6.")
            archive_hash = sha256(archive_path)
            require(by_name.get(archive_path.name.casefold()) == archive_hash,
                    "Shared Host archive bytes differ from qualified F6 inputs: " + archive_path.name)
            member = "lib/net10.0/" + required[key] + ".dll"
            members = [entry for entry in archive.infolist() if entry.filename == member]
            require(len(members) == 1 and members[0].file_size <= 256 * 1024 * 1024,
                    "Expected one finite shared net10.0 assembly: " + member)
            digest = hashlib.sha256()
            with archive.open(members[0]) as stream:
                for block in iter(lambda: stream.read(1024 * 1024), b""):
                    digest.update(block)
            require(digest.hexdigest() == inventory[required[key] + ".dll"],
                    "Shared Host DLL differs between Host and qualified archive: " + required[key])
            found[key] = {"identity": required[key], "version": package_version.text,
                          "archive": archive_path.name, "archiveSha256": archive_hash,
                          "assemblySha256": digest.hexdigest()}
    require(found.keys() == required.keys(), "Runtime feed omits a required shared Host package.")
    return [found[key] for key in sorted(found)]


def validate(payload: Path, inputs_path: Path, version: str | None, packages: Path | None) -> dict:
    inputs, _, selected = read_passed_inputs(inputs_path, version)
    expected = admitted_inventory(inputs.get("hostRuntimeFiles"))
    actual, ancillary = payload_inventory(payload)
    require(actual.keys() == expected.keys(), "Runtime file set differs from F6: missing="
            + repr(sorted(expected.keys() - actual.keys())) + "; extra=" + repr(sorted(actual.keys() - expected.keys())))
    changed = [relative for relative in sorted(expected) if actual[relative] != expected[relative]]
    require(not changed, "Runtime payload bytes differ from F6: " + repr(changed))
    require(not any(PurePosixPath(name).name.casefold().startswith("orbyss.foundation.") and name.casefold().endswith(".dll")
                    and name != "Orbyss.Foundation.Host.dll" for name in actual),
            "Neutral Host cannot provide Foundation feature runtime assemblies.")
    bindings = verify_archives(packages or inputs_path.parent / "feed", selected, inputs, expected)
    # PDB/XML/apphost output is explicitly not covered by this runtime inventory.
    return {"status": "passed", "version": selected, "qualificationInputs": str(inputs_path.resolve()),
            "qualificationInputsSha256": sha256(inputs_path),
            "qualificationResultSha256": sha256(inputs_path.parent / "result.json"),
            "payload": str(payload.resolve()), "hostRuntimeFiles": actual,
            "sharedHostPackageBindings": bindings, "ancillaryFilesOutsideRuntimeInventory": ancillary,
            "imageQualificationClaimed": False}


def fake_fixture(root: Path, version: str = "0.3.0-fixture.1") -> tuple[Path, Path, Path, str]:
    payload, evidence, packages = root / "payload", root / "qualification", root / "packages"
    for directory in (payload, evidence, packages):
        directory.mkdir(parents=True)
    for relative in sorted(REQUIRED_FILES | {"Native.Loader.dll", "runtimes/linux-x64/native/loader.so",
            "hostsettings.json", "shells.json", "Host.staticwebassets.endpoints.json",
            ".orbyss-foundation/web-profile.shells.json"}):
        path = payload / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(("fake fixture " + relative).encode())
    hashes = {}
    for identity, package_version in SHARED_HOST.items():
        package = packages / (identity + "." + package_version + ".nupkg")
        with zipfile.ZipFile(package, "w") as archive:
            archive.writestr(identity + ".nuspec", "<package><metadata><id>" + identity
                + "</id><version>" + package_version + "</version></metadata></package>")
            archive.write(payload / (identity + ".dll"), "lib/net10.0/" + identity + ".dll")
        hashes[package.name] = sha256(package)
    inputs = {"version": version, "hostRuntimeFiles": payload_inventory(payload)[0], "packages": hashes}
    (evidence / "inputs.json").write_text(json.dumps(inputs), encoding="utf-8")
    (evidence / "result.json").write_text(json.dumps({"status": "passed", "version": version,
        **{flag: True for flag in F6_FLAGS}}), encoding="utf-8")
    return payload, evidence / "inputs.json", packages, version


def self_test(root: Path) -> dict:
    observations = []
    def case(name: str, mutation=None, expected: str | None = None, root_selection=False) -> None:
        path = root / name
        payload, inputs, packages, version = fake_fixture(path)
        if mutation:
            mutation(payload, inputs, packages)
        try:
            selected = select_inputs(path / "qualification", version) if root_selection else inputs
            validate(payload, selected, version, packages)
        except (ValueError, OSError, zipfile.BadZipFile) as error:
            require(expected is not None and expected in str(error), name + " failed for an unintended reason: " + str(error))
            observations.append({"case": name, "outcome": "intended rejection", "diagnostic": str(error)})
        else:
            require(expected is None, "Seeded violation admitted: " + name)
            observations.append({"case": name, "outcome": "passed"})

    def change_json(path, field, value):
        document = read_json(path)
        document[field] = value
        path.write_text(json.dumps(document), encoding="utf-8")
    case("matching-payload")
    case("unique-root-selection", root_selection=True)
    case("changed-loader", lambda p, i, n: (p / "Native.Loader.dll").write_bytes(b"changed"), "bytes differ")
    case("missing-dll", lambda p, i, n: (p / "Native.Loader.dll").unlink(), "file set differs")
    case("extra-dll", lambda p, i, n: (p / "Unqualified.dll").write_bytes(b"extra"), "file set differs")
    case("changed-configuration", lambda p, i, n: (p / "appsettings.json").write_bytes(b"{}"), "bytes differ")
    case("changed-dependency-configuration", lambda p, i, n: (p / "Orbyss.Foundation.Host.deps.json").write_bytes(b"{}"), "bytes differ")
    case("extra-runtime-config", lambda p, i, n: (p / "Extra.runtimeconfig.json").write_bytes(b"{}"), "file set differs")
    case("changed-hostsettings", lambda p, i, n: (p / "hostsettings.json").write_bytes(b"{}"), "bytes differ")
    case("changed-shells", lambda p, i, n: (p / "shells.json").write_bytes(b"{}"), "bytes differ")
    case("changed-profile-settings", lambda p, i, n: (p / ".orbyss-foundation/web-profile.shells.json").write_bytes(b"{}"), "bytes differ")
    case("extra-root-settings", lambda p, i, n: (p / "Unqualified.json").write_bytes(b"{}"), "file set differs")
    case("missing-staticwebassets", lambda p, i, n: (p / "Host.staticwebassets.endpoints.json").unlink(), "file set differs")
    case("extra-profile-settings", lambda p, i, n: (p / ".orbyss-foundation/unqualified.json").write_bytes(b"{}"), "file set differs")
    case("extra-native-runtime", lambda p, i, n: (p / "runtimes/linux-x64/native/unqualified.so").write_bytes(b"extra"), "file set differs")
    case("failed-qualification", lambda p, i, n: change_json(i.parent / "result.json", "status", "failed"), "passed actual")
    case("version-mismatch", lambda p, i, n: change_json(i.parent / "result.json", "version", "0.3.0-stale"), "version mismatch")
    case("synthetic-not-actual", lambda p, i, n: change_json(i.parent / "result.json", "actualPostgreSql", False), "passed actual")
    case("non-neutral-qualification", lambda p, i, n: change_json(i.parent / "result.json", "neutralHost", False), "passed actual")
    def coupled_host(p, i, n):
        (p / "Orbyss.Foundation.Json.dll").write_bytes(b"unselected Foundation feature")
        document = read_json(i)
        document["hostRuntimeFiles"] = payload_inventory(p)[0]
        i.write_text(json.dumps(document), encoding="utf-8")
    case("rehashed-host-feature-coupling", coupled_host, "cannot provide Foundation feature")
    def nested_coupling(p, i, n):
        path = p / "runtimes/linux-x64/native/ORBYSS.FOUNDATION.EXECUTION.DLL"
        path.write_bytes(b"unselected native-path Foundation feature")
        document = read_json(i)
        document["hostRuntimeFiles"] = payload_inventory(p)[0]
        i.write_text(json.dumps(document), encoding="utf-8")
    case("rehashed-case-varied-native-feature-coupling", nested_coupling, "cannot provide Foundation feature")
    case("unbound-inventory", lambda p, i, n: change_json(i, "hostRuntimeFiles", {}), "complete Host runtime")
    def missing_required_shells(p, i, n):
        (p / "shells.json").unlink()
        document = read_json(i)
        del document["hostRuntimeFiles"]["shells.json"]
        i.write_text(json.dumps(document), encoding="utf-8")
    case("rehashed-inventory-missing-mandatory-shells", missing_required_shells, "omits required Host/shared")
    def escaping(p, i, n):
        document = read_json(i)
        document["hostRuntimeFiles"]["../escape.dll"] = "0" * 64
        i.write_text(json.dumps(document), encoding="utf-8")
    case("escaping-manifest", escaping, "escapes the payload")
    def ambiguous(p, i, n):
        shutil.copytree(i.parent, i.parent / "duplicate")
    case("ambiguous-root", ambiguous, "found 2", root_selection=True)
    def changed_archive(p, i, n):
        package = next(n.glob("*.nupkg"))
        with zipfile.ZipFile(package, "a") as archive:
            archive.writestr("changed-metadata.txt", "unqualified")
    case("unqualified-archive-bytes", changed_archive, "archive bytes differ")
    def changed_bound_dll(p, i, n):
        identity = next(iter(SHARED_HOST))
        package = n / (identity + "." + SHARED_HOST[identity] + ".nupkg")
        with zipfile.ZipFile(package, "w") as archive:
            archive.writestr(identity + ".nuspec", "<package><metadata><id>" + identity
                + "</id><version>" + SHARED_HOST[identity] + "</version></metadata></package>")
            archive.writestr("lib/net10.0/" + identity + ".dll", b"different assembly")
        document = read_json(i)
        document["packages"][package.name] = sha256(package)
        i.write_text(json.dumps(document), encoding="utf-8")
    case("rehashed-archive-wrong-host-dll", changed_bound_dll, "DLL differs")
    case("missing-shared-archive", lambda p, i, n: next(n.glob("*.nupkg")).unlink(), "omits a required")
    case("changed-native-runtime", lambda p, i, n: (p / "runtimes/linux-x64/native/loader.so").write_bytes(b"changed"), "bytes differ")
    case("missing-runtime-config", lambda p, i, n: (p / "Orbyss.Foundation.Host.runtimeconfig.json").unlink(), "file set differs")
    def alias(p, i, n):
        document = read_json(i)
        document["hostRuntimeFiles"]["NATIVE.LOADER.dll"] = document["hostRuntimeFiles"]["Native.Loader.dll"]
        i.write_text(json.dumps(document), encoding="utf-8")
    case("ambiguous-manifest-case", alias, "case-insensitive Host runtime identity")
    def wrong_package(p, i, n, selected_version="0.3.0-stale", framework="net10.0"):
        identity = next(iter(SHARED_HOST))
        package = n / (identity + "." + SHARED_HOST[identity] + ".nupkg")
        with zipfile.ZipFile(package, "w") as archive:
            archive.writestr(identity + ".nuspec", "<package><metadata><id>" + identity
                + "</id><version>" + selected_version + "</version></metadata></package>")
            archive.write(p / (identity + ".dll"), "lib/" + framework + "/" + identity + ".dll")
        document = read_json(i)
        document["packages"][package.name] = sha256(package)
        i.write_text(json.dumps(document), encoding="utf-8")
    case("wrong-shared-package-version", wrong_package, "package version differs")
    case("wrong-shared-target-framework", lambda p, i, n: wrong_package(p, i, n,
        selected_version=next(iter(SHARED_HOST.values())), framework="net9.0"), "one finite shared net10.0 assembly")
    # Exercise the actual CI CLI without --version, not only a helper receiving explicit versions.
    repository = Path(__file__).resolve().parents[1]
    default_version = (repository / "VERSION").read_text(encoding="utf-8").strip()
    payload, inputs, packages, _ = fake_fixture(root / "ci-default-version", default_version)
    completed = subprocess.run([sys.executable, str(Path(__file__).resolve()), "--payload", str(payload),
        "--qualification-root", str(inputs.parent), "--packages", str(packages)], capture_output=True,
        text=True, timeout=30)
    require(completed.returncode == 0, "CI default-VERSION CLI failed: " + completed.stdout + completed.stderr)
    observations.append({"case": "ci-default-version", "outcome": "passed", "version": default_version})
    return {"status": "passed", "structuralGuardOnly": True, "imageQualificationClaimed": False,
            "cases": observations}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--payload", type=Path)
    selection = parser.add_mutually_exclusive_group()
    selection.add_argument("--qualification-inputs", type=Path)
    selection.add_argument("--qualification-root", type=Path)
    parser.add_argument("--version", help="Exact version; qualification-root defaults to repository VERSION.")
    parser.add_argument("--packages", type=Path, help="Complete F6 feed; defaults to the retained feed adjacent to its passed inputs.")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    repository = Path(__file__).resolve().parents[1]
    run = repository / "artifacts/host-release-payload" / (
        datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ-") + uuid.uuid4().hex[:8])
    run.mkdir(parents=True)
    try:
        if args.self_test:
            result = self_test(run / "guards")
        else:
            require(args.payload is not None, "--payload is required.")
            if args.qualification_root:
                selected_version = args.version or (repository / "VERSION").read_text(encoding="utf-8").strip()
                require(bool(selected_version), "Repository VERSION must select an exact qualification version.")
                inputs = select_inputs(args.qualification_root, selected_version)
            else:
                require(args.qualification_inputs is not None, "Select --qualification-inputs or --qualification-root.")
                inputs = args.qualification_inputs
                selected_version = args.version
            result = validate(args.payload, inputs, selected_version, args.packages)
    except (ValueError, OSError, ET.ParseError, zipfile.BadZipFile) as error:
        result = {"status": "failed", "diagnostic": str(error), "imageQualificationClaimed": False}
        (run / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        (run / "validation.log").write_text(str(error) + "\n", encoding="utf-8")
        raise SystemExit("Host release payload rejected; evidence: " + str(run)) from error
    (run / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    (run / "validation.log").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("Host release payload guard passed; evidence: " + str(run))


if __name__ == "__main__":
    main()
