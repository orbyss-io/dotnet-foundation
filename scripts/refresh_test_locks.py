"""Refresh maintained fixture locks before running locked-mode acceptance."""
from pathlib import Path
import subprocess
import sys
import tempfile

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tests'))
import validate_openapi_exporter as exporter

if __name__=='__main__':
    output=ROOT/'artifacts/test-lock-refresh'; output.mkdir(parents=True,exist_ok=True)
    exporter.create_feature(Path(tempfile.mkdtemp(dir=output)),refresh_lock=True)
    # These independent/adverse probes are deliberately outside the positive solution build.
    # Their direct or centrally transitive references still follow the selected central pins.
    for lock in sorted((ROOT / 'tests').glob('*/packages.lock.json')):
        if lock.parent == ROOT / 'tests/openapi-exporter':
            continue  # The dynamically generated exporter fixture was refreshed above.
        projects = list(lock.parent.glob('*.csproj'))
        if len(projects) != 1:
            raise ValueError('Maintained fixture lock needs one owning project: ' + str(lock))
        subprocess.run(['dotnet', 'restore', str(projects[0]), '--force-evaluate',
                        '--configfile', str(ROOT / 'NuGet.config')], cwd=ROOT, check=True)
    print('Refreshed exporter and independent fixture locks; acceptance still restores in locked mode.')
