"""Run native administration against a newly created, disposable pinned Keycloak instance."""
from __future__ import annotations

import json
import os
from pathlib import Path
import subprocess
import time
import urllib.error
import urllib.request
import uuid


KEYCLOAK_IMAGE = "quay.io/keycloak/keycloak:26.8.1@sha256:a78d6b79572168cb25886b80a8b5b8d4ed4390743e3eecb810538683e209dd6b"
ROOT = Path(__file__).resolve().parents[1]
LABEL = "org.orbyss.foundation.disposable"


def main() -> int:
    identity = uuid.uuid4().hex
    name = "foundation-keycloak-" + identity
    directory = ROOT / "artifacts/keycloak-provider" / identity
    directory.mkdir(parents=True)
    secret = "disposable-" + uuid.uuid4().hex
    private_import = directory / "private-import"
    private_import.mkdir()
    realm_file = private_import / "realm.json"
    realm_file.write_text(json.dumps({"realm": "orbyss-foundation", "enabled": True,
        "clients": [{"clientId": "orbyss-foundation-admin-probe", "protocol": "openid-connect",
                     "enabled": True, "publicClient": False, "serviceAccountsEnabled": True, "secret": secret}],
        "users": [{"username": "service-account-orbyss-foundation-admin-probe", "enabled": True,
                   "serviceAccountClientId": "orbyss-foundation-admin-probe",
                   "clientRoles": {"realm-management": ["realm-admin"]}}]}), encoding="utf-8")
    created = False
    result = {"image": KEYCLOAK_IMAGE, "container": name, "status": "failed", "consumerStateUsed": False}

    def command(arguments: list[str], timeout: int = 60) -> subprocess.CompletedProcess:
        completed = subprocess.run(arguments, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=timeout)
        if completed.returncode:
            raise AssertionError("Disposable Keycloak command failed: " + arguments[0] + " " + arguments[1]
                                 + "\n" + completed.stdout.replace(secret, "[REDACTED]") + completed.stderr.replace(secret, "[REDACTED]"))
        return completed

    started = time.monotonic()
    try:
        command(["docker", "create", "--name", name, "--label", LABEL + "=" + identity,
                 "--publish", "127.0.0.1::8080", KEYCLOAK_IMAGE, "start-dev", "--import-realm"], timeout=120)
        created = True
        command(["docker", "cp", str(private_import), name + ":/opt/keycloak/data/import"])
        command(["docker", "start", name])
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
        completed = subprocess.run(["dotnet", str(probe)], cwd=ROOT, env=environment, capture_output=True,
                                   text=True, encoding="utf-8", errors="replace", timeout=120)
        for stream in ("stdout", "stderr"):
            (directory / (stream + ".log")).write_text(getattr(completed, stream).replace(secret, "[REDACTED]"), encoding="utf-8")
        if completed.returncode:
            raise AssertionError("Real native Keycloak probe failed; see " + str(directory))
        result["status"] = "passed"
        print(completed.stdout.strip())
        print("Disposable native Keycloak provider: " + KEYCLOAK_IMAGE)
        return 0
    except BaseException as error:
        result["failure"] = (type(error).__name__ + ": " + str(error)).replace(secret, "[REDACTED]")
        raise
    finally:
        if created:
            ownership = command(["docker", "inspect", "--format", '{{ index .Config.Labels "' + LABEL + '" }}', name]).stdout.strip()
            if ownership != identity:
                raise AssertionError("Refusing to remove a container not owned by this qualifier.")
            logs = subprocess.run(["docker", "logs", name], capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=30)
            (directory / "provider.log").write_text((logs.stdout + logs.stderr).replace(secret, "[REDACTED]"), encoding="utf-8")
            command(["docker", "rm", "--force", "--volumes", name])
            result["isolatedContainerRemoved"] = True
        realm_file.unlink(missing_ok=True)
        private_import.rmdir()
        result["elapsedSeconds"] = round(time.monotonic() - started, 3)
        (directory / "results.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    raise SystemExit(main())
