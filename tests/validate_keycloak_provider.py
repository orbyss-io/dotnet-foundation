"""Run native administration against a newly created, disposable pinned Keycloak instance."""
from __future__ import annotations

import json
import io
import os
from pathlib import Path
import subprocess
import re
import tarfile
import time
import urllib.error
import urllib.request
import uuid


KEYCLOAK_IMAGE = "quay.io/keycloak/keycloak:26.8.1@sha256:a78d6b79572168cb25886b80a8b5b8d4ed4390743e3eecb810538683e209dd6b"
ROOT = Path(__file__).resolve().parents[1]
LABEL = "org.orbyss.foundation.disposable"


def import_archive(realm_file, uid, gid):
    """Copy only the fictional realm with private modes and the selected native user's ownership."""
    archive = io.BytesIO()
    with tarfile.open(fileobj=archive, mode="w") as output:
        directory = tarfile.TarInfo("import")
        directory.type, directory.mode, directory.uid, directory.gid = tarfile.DIRTYPE, 0o700, uid, gid
        output.addfile(directory)
        content = realm_file.read_bytes()
        realm = tarfile.TarInfo("import/realm.json")
        realm.mode, realm.uid, realm.gid, realm.size = 0o600, uid, gid, len(content)
        output.addfile(realm, io.BytesIO(content))
    return archive.getvalue()


def cleanup_fixture(directory, private_import, name, identity, create_attempted, command, secret):
    """Attempt independent owned cleanup and retain each failure without masking the probe outcome."""
    failures = []
    removed = False

    def attempt(operation, action):
        try:
            return action()
        except BaseException as error:
            failures.append({"operation": operation, "error": (type(error).__name__ + ": " + str(error)).replace(secret, "[REDACTED]")})
            return None

    if create_attempted:
        inspected = attempt("container-ownership", lambda: command([
            "docker", "inspect", "--format", '{{ index .Config.Labels "' + LABEL + '" }}', name]))
        if inspected is not None and inspected.stdout.strip() == identity:
            logs = attempt("container-logs", lambda: command(["docker", "logs", "--tail", "500", name], timeout=30))
            if logs is not None:
                attempt("provider-log-write", lambda: (directory / "provider.log").write_text(
                    (logs.stdout + logs.stderr).replace(secret, "[REDACTED]"), encoding="utf-8"))
            removed = attempt("container-remove", lambda: command(["docker", "rm", "--force", "--volumes", name])) is not None
        elif inspected is not None:
            failures.append({"operation": "container-ownership", "error": "Refusing to remove a container not owned by this qualifier."})
    attempt("private-realm-delete", lambda: (private_import / "realm.json").unlink(missing_ok=True))
    attempt("private-directory-delete", private_import.rmdir)
    return removed, failures


def main() -> int:
    identity = uuid.uuid4().hex
    name = "foundation-keycloak-" + identity
    directory = ROOT / "artifacts/keycloak-provider" / identity
    directory.mkdir(parents=True)
    secret = "disposable-" + uuid.uuid4().hex
    private_import = directory / "private-import"
    private_import.mkdir(mode=0o700)
    realm_file = private_import / "realm.json"
    realm = {"realm": "orbyss-foundation", "enabled": True,
        "clients": [{"clientId": "orbyss-foundation-admin-probe", "protocol": "openid-connect",
                     "enabled": True, "publicClient": False, "serviceAccountsEnabled": True, "secret": secret}],
        "users": [{"username": "service-account-orbyss-foundation-admin-probe", "enabled": True,
                   "serviceAccountClientId": "orbyss-foundation-admin-probe",
                   "clientRoles": {"realm-management": ["realm-admin"]}}]}
    created = False
    create_attempted = False
    result = {"image": KEYCLOAK_IMAGE, "container": name, "status": "failed", "consumerStateUsed": False}
    failure = None

    def command(arguments: list[str], timeout: int = 60, *, standard_input=None) -> subprocess.CompletedProcess:
        completed = subprocess.run(arguments, cwd=ROOT, input=standard_input, capture_output=True, timeout=timeout)
        for stream in ("stdout", "stderr"):
            content = getattr(completed, stream)
            if isinstance(content, bytes):
                setattr(completed, stream, content.decode("utf-8", errors="replace"))
        if completed.returncode:
            raise AssertionError("Disposable Keycloak command failed: " + arguments[0] + " " + arguments[1]
                                 + "\n" + completed.stdout.replace(secret, "[REDACTED]") + completed.stderr.replace(secret, "[REDACTED]"))
        return completed

    started = time.monotonic()
    try:
        with os.fdopen(os.open(realm_file, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), "w", encoding="utf-8") as stream:
            json.dump(realm, stream)
        # A timeout can follow a successful daemon-side create. Inspect the unique name
        # even when the CLI did not acknowledge completion; remove only a matching label.
        create_attempted = True
        command(["docker", "create", "--name", name, "--label", LABEL + "=" + identity,
                 "--publish", "127.0.0.1::8080", KEYCLOAK_IMAGE, "start-dev", "--import-realm"], timeout=120)
        created = True
        native_user = command(["docker", "inspect", "--format", "{{.Config.User}}", name]).stdout.strip()
        selected_user = re.fullmatch(r"([1-9][0-9]*)(?::([0-9]+))?", native_user)
        if selected_user is None:
            raise AssertionError("The selected Keycloak fixture must declare a numeric non-root runtime user.")
        uid, gid = int(selected_user[1]), int(selected_user[2] or "0")
        command(["docker", "cp", "--archive", "-", name + ":/opt/keycloak/data/"], standard_input=import_archive(realm_file, uid, gid))
        result["nativeUser"] = native_user
        command(["docker", "start", name])
        command(["docker", "exec", name, "/bin/sh", "-c", "test -x /opt/keycloak/data/import && test -r /opt/keycloak/data/import/realm.json"])
        result["privateImportReadableAsNativeUser"] = True
        binding = command(["docker", "port", name, "8080/tcp"]).stdout.strip()
        origin = "http://127.0.0.1:" + binding.rsplit(":", 1)[1]
        ready_by = time.monotonic() + 120
        while True:
            try:
                with urllib.request.urlopen(origin + "/realms/orbyss-foundation/.well-known/openid-configuration", timeout=2) as response:
                    if response.status == 200:
                        break
            except (urllib.error.URLError, TimeoutError, ConnectionError):
                pass
            if time.monotonic() > ready_by:
                raise AssertionError("Disposable Keycloak did not become ready within 120 seconds.")
            time.sleep(0.25)
        environment = dict(os.environ)
        environment["PROGRAM_KIT_KEYCLOAK_ADMIN_URL"] = origin
        environment["PROGRAM_KIT_KEYCLOAK_ADMIN_SECRET"] = secret
        probe = ROOT / "tests/dotnet/Orbyss.Foundation.Identity.Keycloak.Admin.Probe/bin/Release/net10.0/Orbyss.Foundation.Identity.Keycloak.Admin.Probe.dll"
        try:
            completed = subprocess.run(["dotnet", str(probe)], cwd=ROOT, env=environment, capture_output=True,
                                       text=True, encoding="utf-8", errors="replace", timeout=120)
        except subprocess.TimeoutExpired as error:
            for stream, content in (("stdout", error.stdout), ("stderr", error.stderr)):
                value = content.decode("utf-8", errors="replace") if isinstance(content, bytes) else (content or "")
                (directory / (stream + ".log")).write_text(value.replace(secret, "[REDACTED]"), encoding="utf-8")
            raise
        for stream in ("stdout", "stderr"):
            (directory / (stream + ".log")).write_text(getattr(completed, stream).replace(secret, "[REDACTED]"), encoding="utf-8")
        if completed.returncode:
            raise AssertionError("Real native Keycloak probe failed; see " + str(directory))
        result["status"] = "passed"
    except BaseException as error:
        failure = error
        result["failure"] = (type(error).__name__ + ": " + str(error)).replace(secret, "[REDACTED]")
    finally:
        removed, cleanup_failures = cleanup_fixture(directory, private_import, name, identity, create_attempted, command, secret)
        result["createAttempted"] = create_attempted
        result["createCompleted"] = created
        result["isolatedContainerRemoved"] = removed
        result["cleanupFailures"] = cleanup_failures
        if cleanup_failures:
            result["status"] = "failed"
        result["elapsedSeconds"] = round(time.monotonic() - started, 3)
        (directory / "results.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    if failure is not None:
        raise RuntimeError(result["failure"] + "; see " + str(directory / "results.json")) from None
    if cleanup_failures:
        raise RuntimeError("Disposable Keycloak cleanup failed; see " + str(directory / "results.json"))
    print(completed.stdout.strip().replace(secret, "[REDACTED]"))
    print("Disposable native Keycloak provider: " + KEYCLOAK_IMAGE)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
