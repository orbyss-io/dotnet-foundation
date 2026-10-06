"""Exercise real ASP.NET Problem Details transport and public definition contracts."""
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[1]
project = root / "tests/dotnet/Orbyss.Foundation.Web.ProblemDetails.Probe/Orbyss.Foundation.Web.ProblemDetails.Probe.csproj"
subprocess.run(["dotnet", "restore", str(project), "--locked-mode"], cwd=root, check=True)
subprocess.run(["dotnet", "run", "--project", str(project), "-c", "Release", "--no-restore"], cwd=root, check=True)
