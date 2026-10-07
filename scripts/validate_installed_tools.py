"""Exercise the exact independently versioned tools packed from this checkout."""
from pathlib import Path
import argparse
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packages',type=Path,default=ROOT/'artifacts/tools')
    args=parser.parse_args()
    tools={}
    for identity,field in (('Orbyss.Foundation.Build','BuildToolsVersion'),('Orbyss.Foundation.OpenApi.Exporter','ExporterVersion')):
        version=ET.parse(ROOT/'src'/identity/(identity+'.csproj')).findtext('.//'+field)
        path=args.packages.resolve()/(identity+'.'+version+'.nupkg')
        if not path.is_file(): raise ValueError('Missing exact current tool archive: '+str(path))
        tools[identity]=path
    for test in ('validate_feature_build','validate_settings_build','validate_settings_graph','validate_settings_no_build'):
        subprocess.run([sys.executable,str(ROOT/'tests'/(test+'.py')),'--package',str(tools['Orbyss.Foundation.Build'])],cwd=ROOT,check=True)
    subprocess.run([sys.executable,str(ROOT/'tests/validate_legacy_assurance_export.py'),'--package',str(tools['Orbyss.Foundation.OpenApi.Exporter'])],cwd=ROOT,check=True)


if __name__=='__main__': main()
