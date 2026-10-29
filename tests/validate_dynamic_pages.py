"""Focused live-page admission and rendering through native CShells and BFF."""
from pathlib import Path
import subprocess


def main():
    root = Path(__file__).resolve().parents[1]
    subprocess.run(["dotnet", "run", "--project", str(root / "tests/dotnet/Orbyss.Foundation.Web.DynamicPages.Probe"),
                    "-c", "Release", "--no-restore", "--no-build"], cwd=root, check=True, timeout=90)


if __name__ == "__main__":
    main()
