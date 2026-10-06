"""Exercise installed schema2 source defaults, imports and secret boundaries without publisher initialization."""
import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile
import urllib.request
import zipfile
import validate_openapi_exporter as fixture


def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def declaration(package, source, types, imports=None):
    return dict(schemaVersion=2, packageId=package,
        sourceSha256={'Options.cs': hashlib.sha256(source.encode()).hexdigest()}, contracts=types, imports=imports or [])


def contract(type_name, scope, properties):
    return dict(scope=scope, typeName=type_name, complete=True, semanticConstraints=[],
        appliesTo=dict(kind='code', features=[], configuration=[]), settings=[dict(property=name, path=scope+':'+name,
        required=False, secret=name=='Secret', constraints={}, binding='Explicit code construction.',
        precedence=['source initializer', 'caller assignment'], reload='immutable', description='Installed source graph fixture.') for name in properties])


def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--package', type=Path, required=True)
    parser.add_argument('--baseline-package', type=Path); args=parser.parse_args()
    artifact=fixture.ROOT/'artifacts/settings-graph'; artifact.mkdir(parents=True, exist_ok=True)
    work=Path(tempfile.mkdtemp(dir=artifact)); package=args.package.resolve(); version=fixture.package_version(package)
    if args.baseline_package is None:
        args.baseline_package=work/'orbyss.foundation.build.0.2.0.nupkg'
        with urllib.request.urlopen('https://api.nuget.org/v3-flatcontainer/orbyss.foundation.build/0.2.0/orbyss.foundation.build.0.2.0.nupkg', timeout=45) as response:
            args.baseline_package.write_bytes(response.read(4_000_001))
    assert digest(args.baseline_package)=='0e26ec6e6e1061dcea466f7a4473b6066991e2e8db45cf868ac2be5837eb1743','Baseline must be the exact immutable signed public Build0.2.0 archive.'
    feed=work/'feed'; feed.mkdir(); shutil.copy2(package, feed/package.name)
    if args.baseline_package: shutil.copy2(args.baseline_package, feed/args.baseline_package.name)
    env=dict(os.environ, NUGET_PACKAGES=str(work/'cache'), DOTNET_CLI_HOME=str(work/'dotnet-home'), MSBUILDDISABLENODEREUSE='1')
    for name in ('Directory.Build.props','Directory.Build.targets','Directory.Packages.props'): (work/name).write_text('<Project/>')
    (work/'NuGet.config').write_text('<configuration><packageSources><clear/><add key="local" value="'+fixture.escape(str(feed))+'"/></packageSources></configuration>')
    source='''namespace Graph;
public sealed class Credentials { public string Name {get;set;} = "safe"; public string Secret {get;set;} = Poison(); private static string Poison()=>throw new Exception("SECRET INITIALIZER EXECUTED"); }
public sealed class Options {
 public const string Section="Graph";
 public int? Optional {get;set;}
 public TimeSpan Timeout {get;set;} = TimeSpan.FromSeconds(2);
 public Dictionary<string,Credentials> Registrations {get;} = new(StringComparer.Ordinal) { ["one"] = new() };
 public HashSet<string> Names {get;} = new(StringComparer.OrdinalIgnoreCase) { "One", "Two" };
}
public static class Startup { [System.Runtime.CompilerServices.ModuleInitializer] public static void Start()=>throw new Exception("PUBLISHER INITIALIZED"); }
'''
    types=[contract('Graph.Options','options',['Optional','Timeout','Registrations','Names']), contract('Graph.Credentials','credentials',['Name','Secret'])]
    metadata=declaration('GraphProbe',source,types); path=work/'settings.source.json'; path.write_text(json.dumps(metadata)); (work/'Options.cs').write_text(source,encoding='utf-8',newline='\n')
    project=work/'GraphProbe.csproj'
    def write_project(selected):
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><PackageId>GraphProbe</PackageId><Version>1.2.3</Version><FoundationSettingsMetadataSource>settings.source.json</FoundationSettingsMetadataSource></PropertyGroup><ItemGroup><PackageReference Include="Orbyss.Foundation.Build" Version="{selected}" PrivateAssets="all"/></ItemGroup><Target Name="ForbidPackCompilation" BeforeTargets="CoreCompile" Condition="\'$(NoBuild)\' == \'true\'"><Error Text="No-build must not compile the publisher."/></Target></Project>')
    command=['dotnet','pack',str(project),'-c','Release','--no-restore','-o',str(work/'packages')]
    observations=[]
    if args.baseline_package:
        write_project(fixture.package_version(args.baseline_package)); fixture.run(['dotnet','restore',str(project),'--configfile',str(work/'NuGet.config')],work,work/'baseline-restore.log',env=env)
        output=fixture.run(command,work,work/'published-build2-fail-first.log',env=env,expected=1)
        assert 'PKSM001' in output and 'PUBLISHER INITIALIZED' not in output and 'SECRET INITIALIZER EXECUTED' not in output
        observations.append('actual-published-build2-rejects-source2')
    write_project(version); fixture.run(['dotnet','restore',str(project),'--configfile',str(work/'NuGet.config')],work,work/'restore.log',env=env)
    fixture.run(command,work,work/'pack.log',env=env)
    packed=work/'packages/GraphProbe.1.2.3.nupkg'; emitted=work/'obj/Release/net10.0/orbyss-foundation/settings.json'
    value=json.loads(emitted.read_text()); assert value['schemaVersion']==2
    assert all(c['schemaVersion']==2 and c['typeName'] in ('Graph.Options','Graph.Credentials') for c in value['contracts'])
    entries={s['path']:s for c in value['contracts'] for s in c['settings']}
    assert entries['options:Optional']['default'] is None and entries['options:Optional']['constraints']['nullable'] is True
    assert entries['options:Timeout']['default']=='00:00:02'
    assert entries['options:Registrations']['default']=={'one':{'Name':'safe'}}
    assert entries['options:Names']['default']==['One','Two'] and 'default' not in entries['credentials:Secret']
    with zipfile.ZipFile(packed) as archive: assert digest(work/'bin/Release/net10.0/GraphProbe.dll')==hashlib.sha256(archive.read('lib/net10.0/GraphProbe.dll')).hexdigest()==value['assembly']['sha256']
    observations.extend(['nullable-derived','timespan-native','nested-secret-never-evaluated','ordinal-set-default','actual-packed-dll-bound'])
    prior_metadata=emitted.read_bytes();prior_assembly=digest(work/'bin/Release/net10.0/GraphProbe.dll')
    fixture.run(command+['--no-build'],work,work/'no-build.log',env=env)
    assert emitted.read_bytes()==prior_metadata and digest(work/'bin/Release/net10.0/GraphProbe.dll')==prior_assembly
    observations.append('native-implicit-usings-no-build-context')
    prior_package=digest(packed);(work/'Options.cs').write_text(source.replace('FromSeconds(2)','FromSeconds(3)'),encoding='utf-8',newline='\n')
    fixture.run(command+['--no-build','-p:IsPackable=false'],work,work/'disabled-package-no-build.log',env=env)
    assert emitted.read_bytes()==prior_metadata and digest(packed)==prior_package and digest(work/'bin/Release/net10.0/GraphProbe.dll')==prior_assembly
    output=fixture.run(command+['--no-build'],work,work/'enabled-package-stale-source.log',env=env,expected=1)
    assert 'PKSM001' in output and emitted.read_bytes()==prior_metadata and digest(packed)==prior_package
    (work/'Options.cs').write_text(source,encoding='utf-8',newline='\n');observations.append('disabled-package-skips-only-package-validation')
    def accepted_collection(name, text, expected_names):
        changed=copy.deepcopy(metadata);changed['sourceSha256']['Options.cs']=hashlib.sha256(text.encode()).hexdigest()
        (work/'Options.cs').write_text(text,encoding='utf-8',newline='\n');path.write_text(json.dumps(changed))
        fixture.run(command,work,work/(name+'.log'),env=env)
        settings={s['path']:s for c in json.loads(emitted.read_text())['contracts'] for s in c['settings']}
        assert settings['options:Names']['default']==expected_names,name
        observations.append(name)
        (work/'Options.cs').write_text(source,encoding='utf-8',newline='\n');path.write_text(json.dumps(metadata))
        fixture.run(command,work,work/(name+'-restored.log'),env=env)
    accepted_collection('native-interface-list-construction',source.replace('public HashSet<string> Names {get;} = new(StringComparer.OrdinalIgnoreCase) { "One", "Two" };','public IList<string> Names {get;} = new List<string> { "One", "Two" };'),['One','Two'])
    accepted_collection('empty-interface-native-set-construction',source.replace('public HashSet<string> Names {get;} = new(StringComparer.OrdinalIgnoreCase) { "One", "Two" };','public ISet<string> Names {get;} = new HashSet<string>();'),[])
    def rejected(name, changed_source=None, change_metadata=None):
        prior=emitted.read_bytes(); packed_hash=digest(packed)
        changed=copy.deepcopy(metadata); text=changed_source or source
        if changed_source: changed['sourceSha256']['Options.cs']=hashlib.sha256(text.encode()).hexdigest()
        if change_metadata: change_metadata(changed)
        (work/'Options.cs').write_text(text,encoding='utf-8',newline='\n'); path.write_text(json.dumps(changed))
        output=fixture.run(command,work,work/(name+'.log'),env=env,expected=1)
        assert 'PKSM001' in output and 'MSB4018' not in output,name
        assert 'PUBLISHER INITIALIZED' not in output and 'SECRET INITIALIZER EXECUTED' not in output,name
        assert emitted.read_bytes()==prior and digest(packed)==packed_hash,name
        observations.append(name)
        (work/'Options.cs').write_text(source,encoding='utf-8',newline='\n'); path.write_text(json.dumps(metadata))
        fixture.run(command,work,work/(name+'-restored.log'),env=env)
    rejected('set-duplicate-exact',source.replace('"One", "Two"','"One", "One"'))
    rejected('set-duplicate-native-ignore-case',source.replace('"One", "Two"','"One", "one"'))
    rejected('set-nonempty-implicit-collection',source.replace('new(StringComparer.OrdinalIgnoreCase) { "One", "Two" }','["One", "One"]'))
    rejected('interface-list-custom-construction',source.replace('public HashSet<string> Names {get;} = new(StringComparer.OrdinalIgnoreCase) { "One", "Two" };','public IList<string> Names {get;} = new DroppingList { "One" };')
        +'\npublic sealed class DroppingList : List<string> { public new void Add(string value) {} }\n')
    rejected('dictionary-custom-construction',source.replace('new(StringComparer.Ordinal) { ["one"] = new() }','new SeededDictionary()')
        +'\npublic sealed class SeededDictionary : Dictionary<string,Credentials> { public SeededDictionary() { this["seed"] = new(); } }\n')
    rejected('object-derived-construction',source.replace('public sealed class Credentials','public class Credentials').replace('["one"] = new()','["one"] = new DerivedCredentials()')
        +'\npublic sealed class DerivedCredentials : Credentials { public DerivedCredentials() { Name="different"; } }\n')
    rejected('nested-secret-override',source.replace('["one"] = new()','["one"] = new() { Secret = "never-export-this" }'))
    rejected('computed-default',source.replace('TimeSpan.FromSeconds(2)','Compute()').replace('public const string Section=', 'private static TimeSpan Compute()=>throw new Exception("COMPUTED EXECUTED"); public const string Section='))
    rejected('nullable-lie',change_metadata=lambda d:d['contracts'][0]['settings'][0]['constraints'].update(nullable=False))
    rejected('unknown-graph-property',change_metadata=lambda d:d['contracts'][0]['settings'][0].update(property='Unknown'))
    rejected('nested-secret-constraint-literal',change_metadata=lambda d:d['contracts'][1]['settings'][1]['constraints'].update(items={'examples':['never-export']}))
    rejected('nested-schema-secret-literal',change_metadata=lambda d:d['contracts'][0]['settings'][2]['constraints'].update(properties={'Password':{'secret':True,'default':'never-export'}}))
    fake=source+'namespace System { public sealed class TimeSpan { public static TimeSpan FromSeconds(int value)=>new(); } }\n'
    # Convert the file-scoped namespace to a block so the impostor source genuinely compiles.
    fake=fake.replace('namespace Graph;','namespace Graph {').replace('namespace System {','}\nnamespace System {')
    rejected('source-framework-timespan-impostor',fake)
    # The dependency uses its own installed emission and actual implementation DLL, never manual defaults.
    dep=work/'dependency'; dep.mkdir()
    dep_source='''namespace Dependency;
public sealed class Options { public int Limit {get;set;} = 42; public string[] Names {get;set;} = []; public Node Graph {get;set;} = new() { Next = new() }; }
public sealed class Other { public int Limit {get;set;} = 99; public string[] Names {get;set;} = []; public Node Graph {get;set;} = new(); }
public sealed class Node { public Node? Next {get;set;} public string Text {get;set;} = "safe"; public string Secret {get;set;} = Poison(); private static string Poison()=>throw new System.Exception("DEPENDENCY SECRET INITIALIZED"); }
'''
    (dep/'Options.cs').write_text(dep_source,encoding='utf-8',newline='\n')
    dep_metadata=declaration('DependencyProbe',dep_source,[contract('Dependency.Options','base',['Limit','Names','Graph']),contract('Dependency.Node','node',['Next','Text','Secret'])]); (dep/'settings.source.json').write_text(json.dumps(dep_metadata))
    dep_project=dep/'DependencyProbe.csproj'; dep_project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><FoundationSettingsMetadataSource>settings.source.json</FoundationSettingsMetadataSource></PropertyGroup><ItemGroup><PackageReference Include="Orbyss.Foundation.Build" Version="{version}" PrivateAssets="all"/></ItemGroup></Project>')
    profile=work/'profile'; profile.mkdir(); (profile/'Marker.cs').write_text('namespace Profile; public class Marker {}\n',encoding='utf-8',newline='\n')
    imported=declaration('ProfileProbe','',[contract('Dependency.Options','profile',['Limit','Names','Graph'])],[dict(typeName='Dependency.Options',packageId='DependencyProbe',scope='base')]); imported['sourceSha256']={'Marker.cs':hashlib.sha256((profile/'Marker.cs').read_bytes()).hexdigest()}
    (profile/'settings.source.json').write_text(json.dumps(imported))
    profile_project=profile/'ProfileProbe.csproj'; profile_project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><FoundationSettingsMetadataSource>settings.source.json</FoundationSettingsMetadataSource></PropertyGroup><ItemGroup><PackageReference Include="Orbyss.Foundation.Build" Version="{version}" PrivateAssets="all"/><ProjectReference Include="../dependency/DependencyProbe.csproj"/><FoundationSettingsMetadataDependency Include="../dependency/obj/$(Configuration)/$(TargetFramework)/orbyss-foundation/settings.json"/></ItemGroup></Project>')
    fixture.run(['dotnet','restore',str(profile_project),'--configfile',str(work/'NuGet.config')],work,work/'import-restore.log',env=env)
    profile_command=['dotnet','pack',str(profile_project),'-c','Release','--no-restore','-o',str(profile/'packages')]
    fixture.run(profile_command,work,work/'import-pack.log',env=env)
    profile_emitted=profile/'obj/Release/net10.0/orbyss-foundation/settings.json'; profile_value=json.loads(profile_emitted.read_text())
    assert profile_value['contracts'][0]['settings'][0]['default']==42 and profile_value['imports'][0]['typeName']=='Dependency.Options'
    assert profile_value['contracts'][0]['settings'][2]['default']=={'Next':{'Next':None,'Text':'safe'},'Text':'safe'}
    fixture.run(profile_command+['--no-build'],work,work/'import-no-build.log',env=env)
    dep_emitted=dep/'obj/Release/net10.0/orbyss-foundation/settings.json'; prior_dep=dep_emitted.read_bytes(); prior_profile=profile_emitted.read_bytes()
    wrong=json.loads(prior_dep); wrong['contracts'][0]['typeName']='Dependency.Other'; dep_emitted.write_text(json.dumps(wrong))
    output=fixture.run(profile_command+['--no-build'],work,work/'same-shape-wrong-type.log',env=env,expected=1)
    assert 'PKSM001' in output and profile_emitted.read_bytes()==prior_profile
    dep_emitted.write_bytes(prior_dep); fixture.run(profile_command+['--no-build'],work,work/'import-restored.log',env=env)
    for name, mutate in [
        ('import-wrong-scalar-kind',lambda d:d['contracts'][0]['settings'][0].update(default='42')),
        ('import-null-nonnullable',lambda d:d['contracts'][0]['settings'][0].update(default=None)),
        ('import-secret-default',lambda d:d['contracts'][0]['settings'][0].update(secret=True,default=42)),
        ('import-array-257',lambda d:d['contracts'][0]['settings'][1].update(default=['x']*257)),
        ('import-retained-byte-expansion',lambda d:d['contracts'][0]['settings'][1].update(default=['x'*16000]*100)),
        ('import-string-16385',lambda d:d['contracts'][0]['settings'][2]['default'].update(Text='x'*16385)),
        ('import-nested-secret-default',lambda d:d['contracts'][0]['settings'][2]['default'].update(Secret='never-export')),
        ('import-nested-secret-constraint',lambda d:d['contracts'][0]['settings'][0]['constraints'].update(properties={'Password':{'secret':True,'default':'never-export'}}))]:
        changed=json.loads(prior_dep);mutate(changed);dep_emitted.write_text(json.dumps(changed))
        output=fixture.run(profile_command+['--no-build'],work,work/(name+'.log'),env=env,expected=1)
        assert 'PKSM001' in output and 'MSB4018' not in output and profile_emitted.read_bytes()==prior_profile,name
        observations.append(name)
        dep_emitted.write_bytes(prior_dep)
    changed=json.loads(prior_dep);cursor=changed['contracts'][0]['settings'][2]['default']
    for _ in range(18):cursor['Next']={'Next':None,'Text':'safe'};cursor=cursor['Next']
    dep_emitted.write_text(json.dumps(changed));output=fixture.run(profile_command+['--no-build'],work,work/'import-depth-17.log',env=env,expected=1)
    assert 'PKSM001' in output and 'depth16' in output and profile_emitted.read_bytes()==prior_profile
    observations.append('import-depth-17');dep_emitted.write_bytes(prior_dep)
    changed=json.loads(prior_dep);changed['contracts'][0]['settings'][1]['default']=['x']*256;dep_emitted.write_text(json.dumps(changed))
    expanded=copy.deepcopy(imported);expanded['contracts']=[]
    for index in range(32):
        item=copy.deepcopy(imported['contracts'][0]);item['scope']='profile-'+str(index)
        for setting in item['settings']:setting['path']=item['scope']+':'+setting['property']
        expanded['contracts'].append(item)
    (profile/'settings.source.json').write_text(json.dumps(expanded));output=fixture.run(profile_command+['--no-build'],work,work/'import-global-nodes-4096.log',env=env,expected=1)
    assert 'PKSM001' in output and '4096 nodes' in output and profile_emitted.read_bytes()==prior_profile
    observations.append('import-global-nodes-4096');dep_emitted.write_bytes(prior_dep);(profile/'settings.source.json').write_text(json.dumps(imported))
    observations.extend(['actual-dependency-import','native-no-build-reference-import','same-shape-wrong-type-rejected'])
    (work/'result.json').write_text(json.dumps(dict(status='passed',package=str(package),packageSha256=digest(package),baselinePackage=str(args.baseline_package) if args.baseline_package else None,observations=observations,publisherInitialized=False),indent=2)+'\n')
    print('Installed schema2 defaults/imports/secret guards passed; evidence: '+str(work/'result.json'))


if __name__=='__main__': main()
