# Exercise installed publisher settings tooling without starting publisher code.
import argparse,copy,hashlib,json,os,tempfile,zipfile
from pathlib import Path
import validate_openapi_exporter as fixture

def sha(text): return hashlib.sha256(text.replace('\r\n','\n').encode()).hexdigest()
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--package',type=Path,required=True);args=parser.parse_args()
    package=args.package.resolve();artifact=fixture.ROOT/'artifacts/settings-build';artifact.mkdir(parents=True,exist_ok=True)
    work=Path(tempfile.mkdtemp(dir=artifact));version=fixture.package_version(package)
    environment=dict(os.environ,NUGET_PACKAGES=str(work/'nuget-cache'),DOTNET_CLI_HOME=str(work/'dotnet-home'))
    for name in ('Directory.Build.props','Directory.Build.targets','Directory.Packages.props'): (work/name).write_text('<Project/>')
    project=work/'SettingsProbe.csproj';project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
    <TargetFramework>net10.0</TargetFramework><PackageId>SettingsProbe</PackageId><Version>1.2.3</Version>
    <FoundationSettingsMetadataSource>settings.source.json</FoundationSettingsMetadataSource>
    <FoundationFeatureDescriptorSource>feature.json</FoundationFeatureDescriptorSource>
    </PropertyGroup><ItemGroup><PackageReference Include="Orbyss.Foundation.Build" Version="{version}" PrivateAssets="all"/></ItemGroup></Project>''')
    source='''namespace Probe;
public sealed class Options {
 public int Limit { get; set; } = 2;
 public bool Enabled { get; set; }
 public string[] Names { get; set; } = ["one", "two"];
 public string Secret { get; set; } = Poison();
 private static string Poison() => throw new System.Exception("initializer executed");
}
public static class Startup {
 [System.Runtime.CompilerServices.ModuleInitializer]
 public static void Start() => throw new System.Exception("publisher started");
}
'''
    (work/'Options.cs').write_text(source,encoding='utf-8',newline='\n')
    fields=lambda prop:dict(property=prop,path='Probe:'+prop,required=False,secret=prop=='Secret',constraints={},binding='Code-owned test declaration.',precedence=['initializer','caller'],reload='immutable',description='Fixture property.')
    contract=dict(scope='options',typeName='Probe.Options',complete=True,settings=[fields(p) for p in ('Limit','Enabled','Names','Secret')],semanticConstraints=[])
    metadata=dict(schemaVersion=1,packageId='SettingsProbe',sourceSha256={'Options.cs':sha(source)},contracts=[contract])
    path=work/'settings.source.json';path.write_text(json.dumps(metadata))
    # Multiple feature identities stay in the existing independent authority.
    (work/'feature.json').write_text(json.dumps(dict(schemaVersion=2,packageId='SettingsProbe',features=[dict(identity=n,featureDependencies=[],runtimeDependencies=[],routes=[]) for n in ('One','Two')])))
    config=work/'NuGet.config';config.write_text(f'<configuration><packageSources><clear/><add key="local" value="{fixture.escape(str(package.parent))}"/></packageSources></configuration>')
    fixture.run(['dotnet','restore',str(project),'--configfile',str(config)],work,work/'restore.log',env=environment)
    command=['dotnet','pack',str(project),'-c','Release','--no-restore','-o',str(work/'packages')]
    fixture.run(command,work,work/'pack.log',env=environment)
    packed=work/'packages/SettingsProbe.1.2.3.nupkg'
    with zipfile.ZipFile(packed) as archive:
        value=json.loads(archive.read('orbyss-foundation/settings.json'))
        assert value['packageId']=='SettingsProbe' and value['packageVersion']=='1.2.3'
        entries={s['path']:s for s in value['contracts'][0]['settings']}
        assert entries['Probe:Limit']['default']==2 and entries['Probe:Enabled']['default'] is False
        assert entries['Probe:Names']['default']==['one','two']
        assert 'default' not in entries['Probe:Secret']
        assert len(json.loads(archive.read('orbyss-foundation/feature.json'))['features'])==2
        assert b'Orbyss.Foundation.Build' not in archive.read('SettingsProbe.nuspec')
    before=hashlib.sha256(packed.read_bytes()).hexdigest();emitted=work/'obj/Release/net10.0/orbyss-foundation/settings.json';previous=emitted.read_bytes()
    invalid=[]
    def case(name,mutate):
        item=copy.deepcopy(metadata);mutate(item);invalid.append((name,json.dumps(item)))
    case('unknown-field',lambda v:v.update(defaultAuthority='handwritten'))
    case('wrong-package',lambda v:v.update(packageId='Other'))
    case('unsupported-schema',lambda v:v.update(schemaVersion=2))
    case('stale-source',lambda v:v['sourceSha256'].update({'Options.cs':'0'*64}))
    case('outside-source',lambda v:v['sourceSha256'].update({'../outside.cs':'0'*64}))
    case('incomplete-inventory',lambda v:v['sourceSha256'].clear())
    case('missing-property',lambda v:v['contracts'][0]['settings'].pop())
    case('duplicate-scope',lambda v:v['contracts'].append(copy.deepcopy(v['contracts'][0])))
    case('unknown-type',lambda v:v['contracts'][0].update(typeName='Other.Options'))
    case('duplicate-path',lambda v:v['contracts'][0]['settings'][1].update(path='probe:limit'))
    case('manual-default',lambda v:v['contracts'][0]['settings'][0].update(default=2))
    case('secret-example',lambda v:v['contracts'][0]['settings'][-1]['constraints'].update(example='value'))
    case('computed-default',lambda v:v['contracts'][0]['settings'][-1].update(secret=False))
    case('nonboolean-completeness',lambda v:v['contracts'][0].update(complete='true'))
    invalid += [('duplicate-json',json.dumps(metadata)[:-1]+',"schemaVersion":1}'),('malformed-json','{')]
    for name,text in invalid:
        path.write_text(text);output=fixture.run(command+['--no-build'],work,work/(name+'.log'),env=environment,expected=1)
        assert 'PKSM001' in output and 'MSB4018' not in output,name
        assert hashlib.sha256(packed.read_bytes()).hexdigest()==before and emitted.read_bytes()==previous,name
    source_cases = {
        'constructor': source.replace('public sealed class Options {', 'public sealed class Options { public Options() { Limit = 9; }'),
        'primary-constructor': source.replace('class Options {', 'class Options(int seed) {'),
        'inheritance': source.replace('class Options {', 'class Options : System.Object {'),
        'public-field': source.replace('class Options {', 'class Options { public int Extra = 7;'),
        'computed-accessor': source.replace('public int Limit { get; set; } = 2;', 'public int Limit { get => 2; set { } }'),
        'computed-initializer': source.replace('= 2;', '= System.Math.Abs(2);'),
        'spread-initializer': source.replace('["one", "two"]', '[..new string[] {"one", "two"}]'),
    }
    for name,text in source_cases.items():
        (work/'Options.cs').write_text(text,encoding='utf-8',newline='\n')
        item=copy.deepcopy(metadata);item['sourceSha256']['Options.cs']=sha(text);path.write_text(json.dumps(item))
        output=fixture.run(command,work,work/(name+'.log'),env=environment,expected=1)
        assert 'PKSM001' in output and 'MSB4018' not in output,name
        assert hashlib.sha256(packed.read_bytes()).hexdigest()==before and emitted.read_bytes()==previous,name
    (work/'Options.cs').write_text(source,encoding='utf-8',newline='\n');path.write_text(json.dumps(metadata))
    fixture.run(command,work,work/'source-restored.log',env=environment)
    # A reviewed source change updates the extracted default; it cannot retain a manually copied value.
    changed=source.replace('= 2;','= 3;');(work/'Options.cs').write_text(changed,encoding='utf-8',newline='\n')
    metadata['sourceSha256']['Options.cs']=sha(changed);path.write_text(json.dumps(metadata))
    output=fixture.run(command+['--no-build'],work,work/'changed-no-build.log',env=environment,expected=1)
    assert 'rebuild the publisher' in output
    fixture.run(command,work,work/'changed-default.log',env=environment)
    assert json.loads(emitted.read_text())['contracts'][0]['settings'][0]['default']==3
    # Explicit partial type coverage is permitted, never silently promoted to complete.
    metadata['contracts'][0]['complete']=False;metadata['contracts'][0]['settings'].pop();path.write_text(json.dumps(metadata))
    fixture.run(command,work,work/'partial.log',env=environment)
    assert json.loads(emitted.read_text())['contracts'][0]['complete'] is False
    (work/'results.json').write_text(json.dumps(dict(package=str(package),packageSha256=hashlib.sha256(package.read_bytes()).hexdigest(),rejected=[n for n,_ in invalid]+list(source_cases),publisherNeverStarted=True,changedDefaultDerived=True,outputsPreserved=True),indent=2)+'\n')
    print(f'Installed settings task: source-derived defaults, secret omission, independent multi-feature descriptor, {len(invalid)+len(source_cases)} rejection cases and output preservation passed. Evidence: {work}/results.json')
if __name__=='__main__':main()
