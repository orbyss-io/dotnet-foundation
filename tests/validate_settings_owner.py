"""Compare actual packed Json settings metadata with runtime defaults and admission."""
import argparse,hashlib,json,os,tempfile,zipfile
from pathlib import Path
import validate_openapi_exporter as fixture
def main():
 parser=argparse.ArgumentParser();parser.add_argument('--package',type=Path,default=fixture.ROOT/('artifacts/nuget/Orbyss.Foundation.Json.'+(fixture.ROOT/'VERSION').read_text().strip()+'.nupkg'));args=parser.parse_args();package=args.package.resolve()
 with zipfile.ZipFile(package) as archive:
  metadata=json.loads(archive.read('orbyss-foundation/settings.json'))
  assert metadata['packageId']=='Orbyss.Foundation.Json' and metadata['packageVersion']==fixture.package_version(package)
  assert hashlib.sha256(archive.read('lib/net10.0/'+metadata['assembly']['name'])).hexdigest()==metadata['assembly']['sha256']
  contract=metadata['contracts'][0];assert contract['scope']=='json-profile' and contract['complete'] is True
 artifact=fixture.ROOT/'artifacts/settings-owner';artifact.mkdir(parents=True,exist_ok=True);work=Path(tempfile.mkdtemp(dir=artifact))
 for name in ('Directory.Build.props','Directory.Build.targets','Directory.Packages.props'):(work/name).write_text('<Project/>')
 project=work/'OwnerProbe.csproj';project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><PackageReference Include="Orbyss.Foundation.Json" Version="{metadata["packageVersion"]}"/></ItemGroup></Project>')
 (work/'Program.cs').write_text('''using Orbyss.Foundation.Json;
using System.Text.Json;
var defaults = new JsonProfileSettings();
Console.WriteLine(JsonSerializer.Serialize(defaults));
foreach (var invalid in new[] { new JsonProfileSettings { MaxBytes = 0 }, new JsonProfileSettings { MaxBytes = 16777217 }, new JsonProfileSettings { MaxDepth = 0 }, new JsonProfileSettings { MaxDepth = 65 }, new JsonProfileSettings { Preset = "unknown" }, new JsonProfileSettings { Extensions = ["unknown"] } }) {
 try { _ = new JsonProfile(invalid); throw new Exception("invalid admission succeeded"); }
 catch (InvalidOperationException) { }
}
foreach (var valid in new[] { new JsonProfileSettings { MaxBytes = 1 }, new JsonProfileSettings { MaxBytes = 16777216 }, new JsonProfileSettings { MaxDepth = 1 }, new JsonProfileSettings { MaxDepth = 64 }, new JsonProfileSettings { Preset = "tolerant-response" } }) _ = new JsonProfile(valid);
''')
 config=work/'NuGet.config';config.write_text(f'<configuration><packageSources><clear/><add key="local" value="{fixture.escape(str(package.parent))}"/></packageSources></configuration>')
 environment=dict(os.environ,NUGET_PACKAGES=str(work/'nuget-cache'),DOTNET_CLI_HOME=str(work/'dotnet-home'))
 fixture.run(['dotnet','restore',str(project),'--configfile',str(config)],work,work/'restore.log',env=environment)
 fixture.run(['dotnet','build',str(project),'-c','Release','--no-restore'],work,work/'build.log',env=environment)
 output=fixture.run(['dotnet',str(work/'bin/Release/net10.0/OwnerProbe.dll')],work,work/'runtime.log',env=environment)
 defaults=json.loads(output.strip());assert defaults=={item['path'].split(':')[-1]:item['default'] for item in contract['settings']}
 entries={item['path'].split(':')[-1]:item for item in contract['settings']}
 assert entries['MaxBytes']['constraints']==dict(minimum=1,maximum=16777216)
 assert entries['MaxDepth']['constraints']==dict(minimum=1,maximum=64)
 assert entries['Preset']['constraints']==dict(enum=['strict-request','tolerant-response'])
 (work/'results.json').write_text(json.dumps(dict(package=str(package),packageSha256=hashlib.sha256(package.read_bytes()).hexdigest(),defaultsMatch=True,boundsAndPresetMatch=True,unknownExtensionRejected=True,assemblyBindingVerified=True),indent=2)+'\n')
 print('Real Json owner defaults, validators and packed assembly binding passed. Evidence: '+str(work/'results.json'))
if __name__=='__main__':main()
