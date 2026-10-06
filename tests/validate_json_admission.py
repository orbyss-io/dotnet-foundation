"""Exercise actual shell JSON admission, native transport paths and public metadata."""
from pathlib import Path
import argparse
import subprocess

root = Path(__file__).resolve().parents[1]
project = root / "tests/dotnet/Orbyss.Foundation.Json.Admission.Probe/Orbyss.Foundation.Json.Admission.Probe.csproj"
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--development-sdk-substitute", action="store_true",
    help="Use the invoking directory's SDK for explicitly selected development evidence; never changes global.json")
arguments = parser.parse_args()
working_directory = Path.cwd() if arguments.development_sdk_substitute else root
if arguments.development_sdk_substitute:
    version = subprocess.check_output(["dotnet", "--version"], cwd=working_directory, text=True).strip()
    print("Explicit development SDK substitute: " + version + "; the Foundation source pin is unchanged.", flush=True)
subprocess.run(["dotnet", "restore", str(project), "--locked-mode"], cwd=working_directory, check=True)
subprocess.run(["dotnet", "build", str(project), "-c", "Release", "--no-restore"], cwd=working_directory, check=True)
for case in ("http", "activation-native", "activation-preset", "activation-capacity", "activation-resolver"):
    subprocess.run(["dotnet", "run", "--project", str(project), "-c", "Release", "--no-build", "--", case], cwd=working_directory, check=True)
