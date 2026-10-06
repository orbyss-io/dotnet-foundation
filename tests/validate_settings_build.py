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
    # The final packed DLL, rather than obj's intermediate DLL, is the authority.
    bin_assembly=work/'bin/Release/net10.0/SettingsProbe.dll'
    obj_assembly=work/'obj/Release/net10.0/SettingsProbe.dll'
    bin_before=bin_assembly.read_bytes();obj_before=obj_assembly.read_bytes()
    current_packed=hashlib.sha256(packed.read_bytes()).hexdigest();current_metadata=emitted.read_bytes()
    bin_assembly.write_bytes(bin_before+b'bin-only mutation')
    output=fixture.run(command+['--no-build'],work,work/'changed-bin.log',env=environment,expected=1)
    assert 'PKSM001' in output and 'rebuild' in output
    assert obj_assembly.read_bytes()==obj_before
    assert hashlib.sha256(packed.read_bytes()).hexdigest()==current_packed and emitted.read_bytes()==current_metadata
    bin_assembly.write_bytes(bin_before)
    fixture.run(command+['--no-build'],work,work/'restored-bin.log',env=environment)

    # Constant defaults resolve statically from reviewed source without executing initializers.
    const_source=source.replace('= 2;','= Defaults.Limit;')+'public static class Defaults { public const int Limit = 7; }\n'
    (work/'Options.cs').write_text(const_source,encoding='utf-8',newline='\n')
    constant=copy.deepcopy(metadata);constant['sourceSha256']['Options.cs']=sha(const_source);path.write_text(json.dumps(constant))
    fixture.run(command,work,work/'constant-default.log',env=environment)
    assert json.loads(emitted.read_text())['contracts'][0]['settings'][0]['default']==7

    # FEATURE actually compiles default 9; unsupported conditional metadata is rejected.
    conditional_source=source.replace(' public int Limit { get; set; } = 2;', '#if FEATURE\n public int Limit { get; set; } = 9;\n#else\n public int Limit { get; set; } = 2;\n#endif')
    conditional_source=conditional_source.replace('= Poison();','= string.Empty;').replace('[System.Runtime.CompilerServices.ModuleInitializer]','')
    conditional_source+='public static class Program { public static void Main() => System.Console.WriteLine(new Options().Limit); }\n'
    (work/'Options.cs').write_text(conditional_source,encoding='utf-8',newline='\n')
    conditional=copy.deepcopy(metadata);conditional['sourceSha256']['Options.cs']=sha(conditional_source);path.write_text(json.dumps(conditional))
    packed_before_condition=packed.read_bytes();metadata_before_condition=emitted.read_bytes()
    output=fixture.run(command+['-p:DefineConstants=FEATURE','-p:OutputType=Exe'],work,work/'conditional-feature.log',env=environment,expected=1)
    assert 'Conditional/preprocessor settings semantics are unsupported' in output
    actual=fixture.run(['dotnet',str(bin_assembly)],work,work/'conditional-runtime.log',env=environment)
    assert actual.strip()=='9',actual
    assert packed.read_bytes()==packed_before_condition and emitted.read_bytes()==metadata_before_condition

    # Direct invocation still compiles/copies the updated source; compiler skipping cannot refresh.
    (work/'Options.cs').write_text(source,encoding='utf-8',newline='\n');path.write_text(json.dumps(metadata))
    fixture.run(command,work,work/'before-skip.log',env=environment)
    skip_source=source.replace('= 2;','= 3;');(work/'Options.cs').write_text(skip_source,encoding='utf-8',newline='\n')
    skip=copy.deepcopy(metadata);skip['sourceSha256']['Options.cs']=sha(skip_source);path.write_text(json.dumps(skip))
    skip_bin=bin_assembly.read_bytes();skip_metadata=emitted.read_bytes();skip_pack=packed.read_bytes()
    for name,args in [('skip-build',['dotnet','build',str(project),'-c','Release','--no-restore','-p:SkipCompilerExecution=true']),
                      ('skip-direct',['dotnet','msbuild',str(project),'-t:FoundationCompileSettingsMetadata','-p:Configuration=Release','-p:SkipCompilerExecution=true'])]:
        output=fixture.run(args,work,work/(name+'.log'),env=environment,expected=1)
        assert 'SkipCompilerExecution cannot authorize' in output,name
        assert emitted.read_bytes()==skip_metadata and packed.read_bytes()==skip_pack,name
    fixture.run(command+['--no-build'],work,work/'skip-pack.log',env=environment,expected=1)
    assert emitted.read_bytes()==skip_metadata and packed.read_bytes()==skip_pack and bin_assembly.read_bytes()==skip_bin
    output=fixture.run(['dotnet','msbuild',str(project),'-t:FoundationCompileSettingsMetadata','-p:Configuration=Release','-p:DesignTimeBuild=true'],work,work/'design-time.log',env=environment,expected=1)
    assert 'Design-time compilation cannot authorize' in output
    assert emitted.read_bytes()==skip_metadata and packed.read_bytes()==skip_pack
    fixture.run(command+['--no-build'],work,work/'design-time-pack.log',env=environment,expected=1)
    assert emitted.read_bytes()==skip_metadata and packed.read_bytes()==skip_pack
    direct_source=source.replace('= 2;','= 4;');(work/'Options.cs').write_text(direct_source,encoding='utf-8',newline='\n')
    direct=copy.deepcopy(metadata);direct['sourceSha256']['Options.cs']=sha(direct_source);path.write_text(json.dumps(direct))
    fixture.run(['dotnet','msbuild',str(project),'-t:FoundationCompileSettingsMetadata','-p:Configuration=Release'],work,work/'direct-compile.log',env=environment)
    assert json.loads(emitted.read_text())['contracts'][0]['settings'][0]['default']==4
    assert json.loads(emitted.read_text())['assembly']['sha256']==hashlib.sha256(bin_assembly.read_bytes()).hexdigest()
    assert bin_assembly.read_bytes()!=skip_bin
    fixture.run(command+['--no-build'],work,work/'direct-pack.log',env=environment)
    with zipfile.ZipFile(packed) as archive:
        assert hashlib.sha256(archive.read('lib/net10.0/SettingsProbe.dll')).hexdigest()==json.loads(emitted.read_text())['assembly']['sha256']

    # Bounded admission rejects large declarations/inventories/defaults before replacing output.
    (work/'Options.cs').write_text(source,encoding='utf-8',newline='\n');path.write_text(json.dumps(metadata))
    fixture.run(command,work,work/'before-limits.log',env=environment)
    limit_pack=packed.read_bytes();limit_metadata=emitted.read_bytes()
    limits=[]
    for name,mutate in [
        ('declaration-bytes',lambda v:v['contracts'][0]['settings'][0].update(description='x'*1_048_576)),
        ('contract-count',lambda v:v.update(contracts=[copy.deepcopy(v['contracts'][0]) for _ in range(33)])),
        ('setting-count',lambda v:v['contracts'][0].update(settings=[copy.deepcopy(v['contracts'][0]['settings'][0]) for _ in range(257)])),
        ('semantic-count',lambda v:v['contracts'][0].update(semanticConstraints=['x'+str(n) for n in range(129)])),
        ('constraints-bytes',lambda v:v['contracts'][0]['settings'][0].update(constraints={'pattern':'x'*16385}))]:
        value=copy.deepcopy(metadata);mutate(value);path.write_text(json.dumps(value))
        output=fixture.run(command+['--no-build'],work,work/(name+'.log'),env=environment,expected=1)
        assert 'PKSM001' in output and ('limit' in output or '1 to 32' in output),name
        assert packed.read_bytes()==limit_pack and emitted.read_bytes()==limit_metadata,name
        limits.append(name)
    for name,text in [
        ('source-bytes',source+'//'+('x'*1_048_576)),
        ('default-array',source.replace('["one", "two"]','['+','.join('"x"' for _ in range(257))+']')),
        ('default-string',source.replace('public string[] Names { get; set; } = ["one", "two"];','public string Names { get; set; } = "'+('x'*16385)+'";'))]:
        (work/'Options.cs').write_text(text,encoding='utf-8',newline='\n');value=copy.deepcopy(metadata);value['sourceSha256']['Options.cs']=sha(text);path.write_text(json.dumps(value))
        output=fixture.run(command,work,work/(name+'.log'),env=environment,expected=1)
        assert 'PKSM001' in output and 'limit' in output,name
        assert packed.read_bytes()==limit_pack and emitted.read_bytes()==limit_metadata,name
        limits.append(name)
    # Bounded source/declarations can expand into >1 GiB JSON through reused const values.
    # The encoder admits each setting before another, with one non-growing 2 MiB buffer.
    expanded_array='['+','.join('D.V' for _ in range(256))+']'
    expansions=[]
    for name,extra_count in [('expanded-single-array',0),('expanded-many-properties',252)]:
        extra=''.join(f' public string[] Extra{n} {{ get; set; }} = {expanded_array};\n' for n in range(extra_count))
        text=source.replace('["one", "two"]',expanded_array).replace(' public int Limit',extra+' public int Limit')
        text+='public static class D { public const string V = "'+('x'*16384)+'"; }\n'
        assert len(text.encode())<1_048_576
        (work/'Options.cs').write_text(text,encoding='utf-8',newline='\n')
        value=copy.deepcopy(metadata);value['sourceSha256']['Options.cs']=sha(text)
        value['contracts'][0]['settings'] += [fields('Extra'+str(n)) for n in range(extra_count)]
        declaration_text=json.dumps(value);assert len(declaration_text.encode())<1_048_576
        path.write_text(declaration_text)
        output=fixture.run(command,work,work/(name+'.log'),env=environment,expected=1)
        assert 'PKSM001' in output and 'fixed buffer; no growth' in output,name
        assert 'OutOfMemory' not in output and 'MSB4018' not in output,name
        assert packed.read_bytes()==limit_pack and emitted.read_bytes()==limit_metadata,name
        expansions.append(dict(case=name,sourceBytes=len(text.encode()),declarationBytes=len(declaration_text.encode()),
                               theoreticalDefaultBytes=(extra_count+1)*256*16384,encoderBufferBytes=2_097_152))
    # The final assembly gate rejects length before hashing, and hashes allowed files as streams.
    (work/'Options.cs').write_text(source,encoding='utf-8',newline='\n');path.write_text(json.dumps(metadata))
    saved_bin=bin_assembly.read_bytes()
    try:
        with bin_assembly.open('r+b') as stream:stream.truncate(268_435_457)
        output=fixture.run(command+['--no-build'],work,work/'assembly-limit.log',env=environment,expected=1)
        assert 'Settings assembly exceeds 256 MiB limit' in output
        assert packed.read_bytes()==limit_pack and emitted.read_bytes()==limit_metadata
    finally:bin_assembly.write_bytes(saved_bin)
    (work/'Options.cs').write_text(source,encoding='utf-8',newline='\n');path.write_text(json.dumps(metadata))
    fixture.run(command,work,work/'limits-restored.log',env=environment)
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
    (work/'results.json').write_text(json.dumps(dict(package=str(package),packageSha256=hashlib.sha256(package.read_bytes()).hexdigest(),rejected=[n for n,_ in invalid]+list(source_cases),publisherNeverStarted=True,changedDefaultDerived=True,outputsPreserved=True,binMutationRejected=True,constantDefaultDerived=True,conditionalFeatureCompiledDefault=9,conditionalMetadataRejected=True,skipCompilerRejected=True,designTimeRejected=True,directTargetCompiles=True,limitsRejected=limits,expansionRejected=expansions,assemblySizeRejected=True),indent=2)+'\n')
    print(f'Installed settings task: source-derived defaults, secret omission, independent multi-feature descriptor, {len(invalid)+len(source_cases)} rejection cases and output preservation passed. Evidence: {work}/results.json')
if __name__=='__main__':main()
