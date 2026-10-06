"""Run compiled public collection/canonical and JSON budget/HTTP conformance probes."""
import subprocess
from pathlib import Path


def main():
    root = Path(__file__).resolve().parents[1]
    for name in ("Orbyss.Foundation.Collections.Probe", "Orbyss.Foundation.Json.Budget.Probe", "Orbyss.Foundation.Json.Http.Probe"):
        assembly = root / "tests" / "dotnet" / name / "bin" / "Release" / "net10.0" / f"{name}.dll"
        if not assembly.is_file():
            raise SystemExit(f"Build the Release probe first: {assembly}")
        subprocess.run(["dotnet", str(assembly)], cwd=root, check=True, timeout=180)
    print("Compiled public collection, canonical, JSON budget and actual HTTP contracts passed.")


if __name__ == "__main__":
    main()
