"""Own a disposable PostgreSQL target for exact packaged Host contract acceptance."""
from __future__ import annotations

import argparse
import os
from pathlib import Path
import subprocess
import sys
import time
import uuid

from validate_postgresql import POSTGRES_IMAGE


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages", type=Path, default=root / "artifacts/nuget")
    parser.add_argument("--version", default=(root / "VERSION").read_text().strip())
    parser.add_argument("--host", type=Path, default=root / "src/Orbyss.Foundation.Host/bin/Release/net10.0/Orbyss.Foundation.Host.dll")
    parser.add_argument("--dotnet", default="dotnet")
    arguments = parser.parse_args()
    name = "foundation-package-contracts-" + uuid.uuid4().hex
    created = False
    try:
        subprocess.run(["docker", "run", "--detach", "--rm", "--name", name,
                        "--label", "org.orbyss.foundation.disposable=package-contracts",
                        "--publish", "127.0.0.1::5432", "--env", "POSTGRES_USER=fixture",
                        "--env", "POSTGRES_PASSWORD=foundation-contracts-fixture-only",
                        "--env", "POSTGRES_DB=foundation_contracts", POSTGRES_IMAGE],
                       check=True, capture_output=True, text=True, timeout=60)
        created = True
        for _ in range(100):
            ready = subprocess.run(["docker", "exec", name, "pg_isready", "-U", "fixture", "-d", "foundation_contracts"],
                                   capture_output=True, text=True, timeout=5)
            if ready.returncode == 0:
                break
            time.sleep(0.1)
        else:
            raise AssertionError("Disposable packaged-contract database did not become ready.")
        binding = subprocess.check_output(["docker", "port", name, "5432/tcp"], text=True, timeout=10).strip()
        port = int(binding.rsplit(":", 1)[1])
        environment = dict(os.environ, FOUNDATION_POSTGRES_TEST_CONNECTION=(
            f"Host=127.0.0.1;Port={port};Database=foundation_contracts;Username=fixture;"
            "Password=foundation-contracts-fixture-only;Maximum Pool Size=4;Include Error Detail=false"))
        subprocess.run([sys.executable, str(root / "tests/validate_contract_package_consumption.py"),
                        "--packages", str(arguments.packages.resolve()), "--version", arguments.version,
                        "--host", str(arguments.host.resolve()), "--dotnet", arguments.dotnet],
                       cwd=root, env=environment, check=True, timeout=1200)
        print("Packaged Host/PostgreSQL qualification passed; disposable database cleaned on exit.")
    finally:
        if created:
            subprocess.run(["docker", "rm", "--force", name], capture_output=True, text=True, check=True, timeout=30)


if __name__ == "__main__":
    main()
