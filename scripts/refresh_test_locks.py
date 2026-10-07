"""Refresh maintained exporter consumer locks before running locked-mode acceptance."""
from pathlib import Path
import sys
import tempfile

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tests'))
import validate_openapi_exporter as exporter

if __name__=='__main__':
    output=ROOT/'artifacts/test-lock-refresh'; output.mkdir(parents=True,exist_ok=True)
    exporter.create_feature(Path(tempfile.mkdtemp(dir=output)),refresh_lock=True)
    print('Refreshed exporter consumer lock; acceptance still restores in locked mode.')
