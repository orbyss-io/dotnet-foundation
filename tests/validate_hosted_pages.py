from pathlib import Path
import subprocess

def main():
    root = Path(__file__).resolve().parents[1]
    subprocess.run(["dotnet", "run", "--project", str(root / "tests/dotnet/Orbyss.Foundation.Web.HostedPages.Probe"),
                    "-c", "Release", "--no-restore", "--no-build"], cwd=root, check=True, timeout=180)

if __name__ == "__main__":
    main()
