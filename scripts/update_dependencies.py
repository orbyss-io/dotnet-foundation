"""Upgrade, restore, build and test Foundation independently of every consumer."""
from pathlib import Path
import argparse
import json
import os
import shutil
import subprocess
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]


def run(name, command):
    output = ROOT / 'artifacts/dependency-update-tests'
    output.mkdir(parents=True, exist_ok=True)
    with (output / (name+'.log')).open('w',encoding='utf-8') as log:
        result = subprocess.run(command,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,timeout=3600)
    if result.returncode:
        raise RuntimeError('Dependency update failed at '+name+'; '+str(output/(name+'.log')))
    print(name+' passed',flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--upgrade',action='store_true')
    parser.add_argument('--prepare-source',action='store_true',help='Refresh locks/vendor inputs only; no qualification or publication claim.')
    parser.add_argument('--export-knowledge',action='store_true',help='Export exact candidate knowledge after all checks; requires clean committed source.')
    args = parser.parse_args()
    if args.prepare_source and args.export_knowledge:
        parser.error('Source preparation cannot claim exact candidate knowledge')
    os.environ['MSBUILDDISABLENODEREUSE']='1'
    os.environ['DOTNET_CLI_USE_MSBUILD_SERVER']='0'
    payload=ROOT/'artifacts/dependency-update-payloads'/uuid.uuid4().hex[:8]
    packages=payload/'nuget'; tools=payload/'tools'; host=payload/'host-runtime'
    if args.upgrade:
        run('upgrade',[sys.executable,'scripts/dependency_maintenance.py','upgrade'])
    run('restore',['dotnet','restore','Orbyss.Foundation.slnx','--force-evaluate','--configfile','NuGet.config'])
    run('vendor-metadata',[sys.executable,'scripts/refresh_vendor_metadata.py'])
    run('fixture-locks',[sys.executable,'scripts/refresh_test_locks.py'])
    if args.prepare_source:
        print('Source inputs prepared; deterministic qualification and candidate knowledge remain required.')
        return
    if args.export_knowledge and subprocess.check_output(['git','status','--porcelain'],cwd=ROOT,text=True).strip():
        raise ValueError('Commit prepared source before exact candidate qualification/knowledge export')
    run('clean',['dotnet','clean','Orbyss.Foundation.slnx','-c','Release'])
    outputs=(ROOT/'src/Orbyss.Foundation.Host/bin/Release').resolve()
    for generated in outputs.glob('*/.orbyss-foundation'):
        if not generated.resolve().is_relative_to(outputs): raise ValueError('Generated metadata path escapes owned build outputs')
        shutil.rmtree(generated)
    run('locked-restore',['dotnet','restore','Orbyss.Foundation.slnx','--locked-mode','--configfile','NuGet.config'])
    run('build',['dotnet','build','Orbyss.Foundation.slnx','-c','Release','--no-restore'])
    run('pack',['dotnet','pack','Orbyss.Foundation.slnx','-c','Release','--no-build','-p:FoundationRuntimeOnly=true','-p:PackageOutputPath='+str(packages)])
    run('publish-host',['dotnet','publish','src/Orbyss.Foundation.Host/Orbyss.Foundation.Host.csproj','-c','Release','--no-build','--no-restore','-p:UseAppHost=false','-o',str(host)])
    run('foundation-tests',[sys.executable,'scripts/validate_foundation.py'])
    version=(ROOT/'VERSION').read_text().strip()
    run('packaged-web-policies',[sys.executable,'tests/validate_web_policies.py','--package',str(packages/('Orbyss.Foundation.WebDefaults.'+version+'.nupkg')),'--bff-package',str(packages/('Orbyss.Foundation.Authentication.BffCookie.'+version+'.nupkg'))])
    run('pack-build-tool',['dotnet','pack','src/Orbyss.Foundation.Build/Orbyss.Foundation.Build.csproj','-c','Release','--no-restore','--output',str(tools)])
    run('pack-exporter-tool',['dotnet','pack','src/Orbyss.Foundation.OpenApi.Exporter/Orbyss.Foundation.OpenApi.Exporter.csproj','-c','Release','--no-restore','--output',str(tools)])
    for test in ('validate_dependency_updates','validate_release_knowledge','validate_update_workflow','validate_tool_release',
                 'validate_host_settings_publish'):
        run(test,[sys.executable,'tests/'+test+'.py'])
    run('installed-tools',[sys.executable,'scripts/validate_installed_tools.py','--packages',str(tools)])
    version=(ROOT/'VERSION').read_text().strip()
    run('validate_settings_owner',[sys.executable,'tests/validate_settings_owner.py','--package',str(packages/('Orbyss.Foundation.Json.'+version+'.nupkg'))])
    for test in ('validate_package_metadata','validate_settings_authentication_owner','validate_settings_owners'):
        run(test,[sys.executable,'tests/'+test+'.py','--packages',str(packages)])
    run('hosted-package-consumption',[sys.executable,'scripts/validate_hosted_package_consumption.py','--packages',str(packages),'--host',str(host/'Orbyss.Foundation.Host.dll')])
    qualification_root=ROOT/'artifacts/contract-package-consumption'
    before=set(qualification_root.glob('*/inputs.json'))
    run('contract-package-qualification',[sys.executable,'tests/run_contract_package_qualification.py','--packages',str(packages),'--host',str(host/'Orbyss.Foundation.Host.dll')])
    new_inputs=set(qualification_root.glob('*/inputs.json'))-before
    if len(new_inputs)!=1: raise ValueError('Current qualification produced ambiguous or missing inputs')
    qualification=new_inputs.pop()
    run('host-payload',[sys.executable,'tests/validate_host_release_payload.py','--payload',str(host),'--qualification-inputs',str(qualification)])
    (ROOT/'artifacts/dependency-update-payload.json').write_text(json.dumps({'packages':packages.relative_to(ROOT).as_posix(),'tools':tools.relative_to(ROOT).as_posix(),'host':host.relative_to(ROOT).as_posix(),'qualification':qualification.relative_to(ROOT).as_posix()},indent=2)+'\n',encoding='utf-8')
    if args.export_knowledge:
        commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
        run('release-knowledge',[sys.executable,'scripts/build_release_knowledge.py','--packages',str(packages),'--version',version,'--source-commit',commit,'--host',str(host),'--output',str(payload/'foundation-knowledge-candidate.json')])
    print('All deterministic Foundation dependency-update checks passed. Publication remains the tagged release workflow.')


if __name__ == '__main__': main()
