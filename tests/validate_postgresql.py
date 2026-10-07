from __future__ import annotations

import argparse
import os
import subprocess
import time
import uuid
from pathlib import Path


POSTGRES_IMAGE = "postgres@sha256:74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336"


def main() -> int:
    parser = argparse.ArgumentParser(description="Qualify Foundation deadlines and, by default, native PostgreSQL ownership against a disposable database.")
    parser.add_argument("--deadline-only", action="store_true", help="Run deterministic deadlines only; this does not claim native PostgreSQL acceptance.")
    arguments = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    probe = root / "tests/dotnet/Orbyss.Foundation.PostgreSql.Probe/bin/Release/net10.0/Orbyss.Foundation.PostgreSql.Probe.dll"
    if not probe.is_file():
        raise AssertionError("Build the Release PostgreSQL probe before running its disposable-database qualifier.")
    if arguments.deadline_only:
        result = subprocess.run(["dotnet", str(probe), "--deadline-only"], cwd=root,
                                capture_output=True, text=True, timeout=30, check=True)
        print(result.stdout.strip())
        return 0
    name = "foundation-contracts-postgres-" + uuid.uuid4().hex
    created = False
    try:
        subprocess.run(
            ["docker", "run", "--detach", "--rm", "--name", name,
             "--label", "org.orbyss.foundation.disposable=postgresql-qualification",
             "--publish", "127.0.0.1::5432", "--env", "POSTGRES_USER=fixture",
             "--env", "POSTGRES_PASSWORD=foundation-contracts-fixture-only",
             "--env", "POSTGRES_DB=foundation_contracts", POSTGRES_IMAGE],
            check=True, capture_output=True, text=True, timeout=60,
        )
        created = True
        for _ in range(100):
            ready = subprocess.run(["docker", "exec", name, "pg_isready", "-U", "fixture", "-d", "foundation_contracts"],
                                   capture_output=True, text=True, timeout=5)
            if ready.returncode == 0:
                break
            time.sleep(0.1)
        else:
            raise AssertionError("The disposable PostgreSQL fixture did not become ready.")
        binding = subprocess.run(["docker", "port", name, "5432/tcp"], check=True, capture_output=True,
                                 text=True, timeout=10).stdout.strip()
        port = int(binding.rsplit(":", 1)[1])
        environment = dict(os.environ)
        environment["FOUNDATION_POSTGRES_TEST_CONNECTION"] = (
            f"Host=127.0.0.1;Port={port};Database=foundation_contracts;Username=fixture;"
            "Password=foundation-contracts-fixture-only;Maximum Pool Size=4;Include Error Detail=false;Application Name=foundation-qualifier"
        )
        result = subprocess.run(["dotnet", str(probe)], cwd=root, env=environment,
                                capture_output=True, text=True, timeout=180)
        if result.returncode != 0:
            raise AssertionError("Foundation PostgreSQL qualifier failed.\n" + result.stdout + "\n" + result.stderr)
        print(result.stdout.strip())
        print("Disposable PostgreSQL image: " + POSTGRES_IMAGE)
        return 0
    finally:
        if created:
            subprocess.run(["docker", "rm", "--force", name], check=True, capture_output=True,
                           text=True, timeout=30)


if __name__ == "__main__":
    raise SystemExit(main())
