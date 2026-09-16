from __future__ import annotations

import subprocess
from pathlib import Path


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    project = root / (
        "tests/dotnet/Orbyss.Foundation.Authentication.BffCookie.Probe/"
        "Orbyss.Foundation.Authentication.BffCookie.Probe.csproj"
    )
    result = subprocess.run(
        [
            "dotnet",
            "run",
            "--project",
            str(project),
            "--configuration",
            "Release",
            "--no-restore",
            "--no-build",
        ],
        cwd=root,
        capture_output=True,
        text=True,
        timeout=180,
    )
    if result.returncode != 0:
        raise AssertionError(
            "Orbyss Foundation BFF cookie-options probe failed.\n"
            f"stdout:\n{result.stdout}\n"
            f"stderr:\n{result.stderr}"
        )
    print("BFF JSON configuration binding, exact scopes/locales, and local-HTTP/production cookie invariants passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
