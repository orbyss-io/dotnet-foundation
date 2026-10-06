"""Compare each actual non-Host owner archive with cold compiled defaults and native admission."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile
import xml.etree.ElementTree as ET
import zipfile
import validate_openapi_exporter as fixture
import settings_package_bindings as bindings


PROGRAM = r'''
using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orbyss.Foundation.Json;
using Orbyss.Foundation.Json.AspNetCore;
using Orbyss.Foundation.DomainEvents;
using Orbyss.Foundation.PostgreSql;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.WebDefaults;
using Orbyss.Foundation.Mcp.AspNetCore;
using Orbyss.Foundation.Web.Discovery;
using Orbyss.Foundation.Web.HostedPages;

var documents = JsonNode.Parse(File.ReadAllText(args[0]))!.AsArray();
var secrets = new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
foreach (var document in documents) foreach (var contract in document!["contracts"]!.AsArray()) {
 var typeName = contract!["typeName"]!.GetValue<string>();
 if (!secrets.TryGetValue(typeName,out var names)) secrets[typeName]=names=new(StringComparer.Ordinal);
 foreach(var setting in contract["settings"]!.AsArray()) if(setting!["secret"]!.GetValue<bool>()) names.Add(setting["path"]!.GetValue<string>().Split(':')[^1]);
}
JsonNode? Snapshot(object? value) {
 if(value is null)return null;
 var type=value.GetType();
 if(value is string || type.IsPrimitive || value is decimal || value is TimeSpan)return JsonSerializer.SerializeToNode(value,type);
 if(value is IDictionary dictionary) {var result=new JsonObject();foreach(DictionaryEntry item in dictionary)result.Add((string)item.Key,Snapshot(item.Value));return result;}
 if(value is IEnumerable sequence){var result=new JsonArray();foreach(var item in sequence)result.Add(Snapshot(item));return result;}
 var obj=new JsonObject();secrets.TryGetValue(type.FullName!,out var hidden);
 foreach(var property in type.GetProperties(BindingFlags.Public|BindingFlags.Instance)) if(hidden?.Contains(property.Name)!=true)obj.Add(property.Name,Snapshot(property.GetValue(value)));
 return obj;
}
int checks=0;
foreach(var document in documents) {
 var assembly=Assembly.Load(new AssemblyName(Path.GetFileNameWithoutExtension(document!["assembly"]!["name"]!.GetValue<string>())));
 foreach(var contract in document["contracts"]!.AsArray()) {
  var typeName=contract!["typeName"]!.GetValue<string>();var type=assembly.GetType(typeName);
  if(type is null){var imported=document["imports"]!.AsArray().SingleOrDefault(i=>i!["typeName"]!.GetValue<string>()==typeName);
   if(imported is not null)type=Assembly.Load(new AssemblyName(Path.GetFileNameWithoutExtension(imported["assembly"]!["name"]!.GetValue<string>()))).GetType(typeName);}
  type??=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(typeName)).FirstOrDefault(t=>t is not null);
  if(type is null)throw new Exception("Missing native settings type "+typeName);
  var instance=Activator.CreateInstance(type,nonPublic:true)!;var properties=type.GetProperties(BindingFlags.Public|BindingFlags.Instance);
  var fields=contract["settings"]!.AsArray();
  if(fields.Count!=properties.Length)throw new Exception("Incomplete native property inventory "+typeName);
  foreach(var setting in fields){var name=setting!["path"]!.GetValue<string>().Split(':')[^1];var property=properties.Single(p=>p.Name==name);
   if(setting["secret"]!.GetValue<bool>()){if(setting.AsObject().ContainsKey("default"))throw new Exception("Secret default");continue;}
   if(!JsonNode.DeepEquals(Snapshot(property.GetValue(instance)),setting["default"]))throw new Exception("Compiled default mismatch "+typeName+"."+name);
   checks++;
  }
 }
}
JsonObject Setting(string type,string property){var settings=documents.SelectMany(d=>d!["contracts"]!.AsArray()).Where(c=>c!["typeName"]!.GetValue<string>()==type)
 .SelectMany(c=>c!["settings"]!.AsArray()).Where(s=>s!["path"]!.GetValue<string>().Split(':')[^1]==property).Select(s=>s!.AsObject()).ToArray();
 if(settings.Length==0)throw new Exception("Missing native property declaration "+type+"."+property);
 foreach(var setting in settings.Skip(1))foreach(var key in new[]{"type","default","constraints","secret"})
  if(!JsonNode.DeepEquals(settings[0][key],setting[key]))throw new Exception("Imported native property authority mismatch "+type+"."+property+"."+key);
 return settings[0];}
int Bound(string type,string property,string key)=>Setting(type,property)["constraints"]![key]!.GetValue<int>();
void Reject(Action action){try{action();}catch(Exception){checks++;return;}throw new Exception("Native invalid setting admitted");}
var pageType=typeof(JsonPageOptions).FullName!;var pageMaximum=Bound(pageType,"MaximumSize","maximum");
new JsonPageOptions{DefaultSize=1,MaximumSize=pageMaximum}.Validate();
Reject(()=>new JsonPageOptions{DefaultSize=0}.Validate());Reject(()=>new JsonPageOptions{DefaultSize=1,MaximumSize=pageMaximum+1}.Validate());
Reject(()=>new JsonPageOptions{DefaultSize=4,MaximumSize=3}.Validate());
var profileType=typeof(JsonProfileSettings).FullName!;
var bytesMin=Bound(profileType,"MaxBytes","minimum");var bytesMax=Bound(profileType,"MaxBytes","maximum");
var depthMin=Bound(profileType,"MaxDepth","minimum");var depthMax=Bound(profileType,"MaxDepth","maximum");
_=new JsonProfile(new(){MaxBytes=bytesMin,MaxDepth=depthMin});_=new JsonProfile(new(){MaxBytes=bytesMax,MaxDepth=depthMax});
Reject(()=>new JsonProfile(new(){MaxBytes=bytesMin-1}));Reject(()=>new JsonProfile(new(){MaxBytes=bytesMax+1}));Reject(()=>new JsonProfile(new(){MaxDepth=depthMin-1}));Reject(()=>new JsonProfile(new(){MaxDepth=depthMax+1}));
var json = new FoundationJsonOptions();new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
 ["Foundation:Json:Profiles:strict-request:MaxBytes"]="1234",["Foundation:Json:Paging:DefaultSize"]="7"}).Build().GetSection("Foundation:Json").Bind(json);
if(json.Profiles.Count!=4||json.Profiles[JsonProfileKeys.StrictRequest].MaxBytes!=1234||json.Paging.DefaultSize!=7||json.Profiles[JsonProfileKeys.ProblemResponse].MaxBytes!=65536)throw new Exception("Native Json binder/default dictionary merge changed");checks++;
var configuredProfile=documents.Single(d=>d!["packageId"]!.GetValue<string>()=="Orbyss.Foundation.Json.AspNetCore")!["contracts"]!.AsArray().Single(c=>c!["scope"]!.GetValue<string>()=="json-configured-profile")!;
foreach(var setting in configuredProfile["settings"]!.AsArray())if(setting!["path"]!.GetValue<string>()!="Foundation:Json:Profiles:{profileName}:"+setting["path"]!.GetValue<string>().Split(':')[^1])throw new Exception("Missing native named-profile binding path");
new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
 ["Foundation:Json:Profiles:custom-profile:MaxBytes"]="4096",["Foundation:Json:Profiles:custom-profile:MaxDepth"]="7",["Foundation:Json:Profiles:custom-profile:Preset"]=JsonProfileKeys.TolerantResponse}).Build().GetSection("Foundation:Json").Bind(json);
var customProfile=json.Profiles["custom-profile"];_=new JsonProfile(customProfile);
if(json.Profiles.Count!=5||customProfile.MaxBytes!=4096||customProfile.MaxDepth!=7||customProfile.Preset!=JsonProfileKeys.TolerantResponse||json.Profiles[JsonProfileKeys.StrictRequest].MaxBytes!=1234)throw new Exception("Native custom named-profile binding changed");checks++;
var problemType=typeof(FoundationProblemResponseOptions).FullName!;
void Problem(int bytes,int depth,string preset){var services=new ServiceCollection();services.AddLogging();services.AddFoundationProblemDetails();services.Configure<FoundationProblemResponseOptions>(o=>{o.MaxBytes=bytes;o.MaxDepth=depth;o.Preset=preset;});using var provider=services.BuildServiceProvider();_=provider.GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>();}
var problemMin=Bound(problemType,"MaxBytes","minimum");var problemMax=Bound(problemType,"MaxBytes","maximum");
var problemDepthMin=Bound(problemType,"MaxDepth","minimum");var problemDepthMax=Bound(problemType,"MaxDepth","maximum");
Problem(problemMin,problemDepthMin,JsonProfileKeys.TolerantResponse);Problem(problemMax,problemDepthMax,JsonProfileKeys.TolerantResponse);
Reject(()=>Problem(problemMin-1,problemDepthMin,JsonProfileKeys.TolerantResponse));Reject(()=>Problem(problemMax+1,problemDepthMin,JsonProfileKeys.TolerantResponse));Reject(()=>Problem(problemMin,problemDepthMin-1,JsonProfileKeys.TolerantResponse));Reject(()=>Problem(problemMin,problemDepthMax+1,JsonProfileKeys.TolerantResponse));Reject(()=>Problem(problemMin,problemDepthMin,JsonProfileKeys.StrictRequest));
void ProjectedProblem(int bytes,int depth,string preset){var services=new ServiceCollection();services.AddLogging();new FoundationJsonFeature(new CShells.ShellSettings("settings-owner")).ConfigureServices(services);
 services.Configure<FoundationJsonOptions>(options=>options.Profiles[JsonProfileKeys.ProblemResponse]=new(){MaxBytes=bytes,MaxDepth=depth,Preset=preset});using var provider=services.BuildServiceProvider();
 _=provider.GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>();var projected=provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FoundationProblemResponseOptions>>().Value;
 if(projected.MaxBytes!=bytes||projected.MaxDepth!=depth||projected.Preset!=preset)throw new Exception("Native shell problem-profile projection changed");checks++;}
ProjectedProblem(problemMin,problemDepthMin,JsonProfileKeys.TolerantResponse);ProjectedProblem(problemMax,problemDepthMax,JsonProfileKeys.TolerantResponse);
Reject(()=>ProjectedProblem(problemMin-1,problemDepthMin,JsonProfileKeys.TolerantResponse));Reject(()=>ProjectedProblem(problemMin,problemDepthMin-1,JsonProfileKeys.TolerantResponse));Reject(()=>ProjectedProblem(problemMin,problemDepthMin,JsonProfileKeys.StrictRequest));
var dispatchType=typeof(DomainEventDispatchOptions).FullName!;var publicationsMin=Bound(dispatchType,"MaximumPublications","minimum");var nestedMin=Bound(dispatchType,"MaximumDepth","minimum");
new DomainEventDispatchOptions{MaximumDepth=nestedMin,MaximumPublications=publicationsMin}.Validate();Reject(()=>new DomainEventDispatchOptions{MaximumDepth=nestedMin-1}.Validate());Reject(()=>new DomainEventDispatchOptions{MaximumPublications=publicationsMin-1}.Validate());
var policyType=typeof(FoundationPostgreSqlServiceCollectionExtensions).Assembly.GetType("Orbyss.Foundation.PostgreSql.PostgreSqlPolicy")!;
var ctor=policyType.GetConstructors(BindingFlags.NonPublic|BindingFlags.Instance).Single();
object Policy(PostgreSqlOptions options)=>ctor.Invoke([options]);
var postgresType=typeof(PostgreSqlOptions).FullName!;
var operationMaximum=TimeSpan.FromMilliseconds(Setting(postgresType,nameof(PostgreSqlOptions.OperationTimeout))["constraints"]!["maximumMilliseconds"]!.GetValue<double>());
var lockMaximum=TimeSpan.FromMilliseconds(Setting(postgresType,nameof(PostgreSqlOptions.LockTimeout))["constraints"]!["maximumMilliseconds"]!.GetValue<double>());
foreach(var property in typeof(PostgreSqlOptions).GetProperties().Where(p=>p.PropertyType==typeof(TimeSpan))) {
 var maximum=Setting(postgresType,property.Name)["constraints"]!["maximumMilliseconds"]!.GetValue<double>();
 PostgreSqlOptions Options(TimeSpan duration){var options=new PostgreSqlOptions{ConnectionString="Host=localhost;Database=metadata;Username=metadata",OperationTimeout=operationMaximum};
  if(property.Name==nameof(PostgreSqlOptions.OperationTimeout)){options.ConnectionTimeout=TimeSpan.FromTicks(1);options.CommandTimeout=TimeSpan.FromTicks(1);options.LockTimeout=TimeSpan.FromTicks(1);}
  if(property.Name==nameof(PostgreSqlOptions.CommandTimeout))options.LockTimeout=TimeSpan.FromTicks(1);
  if(property.Name==nameof(PostgreSqlOptions.LockTimeout))options.CommandTimeout=lockMaximum;
  property.SetValue(options,duration);return options;}
 foreach(var duration in new[]{TimeSpan.FromTicks(1),TimeSpan.FromMilliseconds(maximum)}){var options=Options(duration);var admitted=Policy(options);
  var native=new Npgsql.NpgsqlConnectionStringBuilder((string)policyType.GetProperty("ConnectionString",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(admitted)!);
  if(native.Timeout!=Math.Max(1,(int)Math.Ceiling(options.ConnectionTimeout.TotalSeconds))||native.CommandTimeout!=Math.Max(1,(int)Math.Ceiling(options.CommandTimeout.TotalSeconds))
   ||native.CancellationTimeout!=(int)Math.Ceiling(options.CancellationTimeout.TotalMilliseconds)||native.NoResetOnClose)throw new Exception("Native PostgreSQL timeout projection differs from metadata");checks++;}
 Reject(()=>Policy(Options(TimeSpan.Zero)));Reject(()=>Policy(Options(TimeSpan.FromMilliseconds(maximum+1))));
}
Reject(()=>Policy(new(){ConnectionString="Host=localhost;Database=metadata;Username=metadata",ConnectionTimeout=TimeSpan.FromSeconds(11)}));
Reject(()=>Policy(new(){ConnectionString="Host=localhost;Database=metadata;Username=metadata",LockTimeout=TimeSpan.FromSeconds(6)}));Reject(()=>Policy(new()));
var responseType=typeof(WebResponsePolicyOptions).FullName!;var cacheMin=Bound(responseType,"PublicAssetMaxAgeSeconds","minimum");var cacheMax=Bound(responseType,"PublicAssetMaxAgeSeconds","maximum");
_=new WebResponsePolicyCatalog(new(){Policies=new(){["default"]=new(){PublicAssetMaxAgeSeconds=cacheMin}}});_=new WebResponsePolicyCatalog(new(){Policies=new(){["default"]=new(){PublicAssetMaxAgeSeconds=cacheMax}}});
Reject(()=>new WebResponsePolicyCatalog(new(){Policies=new(){["default"]=new(){PublicAssetMaxAgeSeconds=cacheMin-1}}}));Reject(()=>new WebResponsePolicyCatalog(new(){Policies=new(){["default"]=new(){PublicAssetMaxAgeSeconds=cacheMax+1}}}));
Reject(()=>new WebResponsePolicyCatalog(new(){Policies=new(){["default"]=new(){ContentSecurityPolicy="default-src 'self'; object-src 'none'; base-uri 'self'"}}}));Reject(()=>new WebResponsePolicyCatalog(new(){DefaultPolicy="missing"}));
var localeAssembly=typeof(WebResponsePolicyCatalog).Assembly;
var localeType=localeAssembly.GetType("Orbyss.Foundation.WebDefaults.FoundationWebDefaultsOptions")!;
var localeValidatorType=localeAssembly.GetType("Orbyss.Foundation.WebDefaults.FoundationWebDefaultsOptionsValidator")!;
var localeValidator=Activator.CreateInstance(localeValidatorType,nonPublic:true)!;
void Locales(string selected,string[] supported,bool valid){var options=Activator.CreateInstance(localeType,nonPublic:true)!;localeType.GetProperty("DefaultLocale")!.SetValue(options,selected);localeType.GetProperty("SupportedLocales")!.SetValue(options,supported);
 var admitted=(Microsoft.Extensions.Options.ValidateOptionsResult)localeValidatorType.GetMethod("Validate")!.Invoke(localeValidator,[null,options])!;
 if(admitted.Succeeded!=valid)throw new Exception("Native locale admission differs from declared semantic constraints");checks++;}
Locales("en-GB",["en-GB"],true);Locales("en-GB",[],false);Locales("nl-NL",["en-GB"],false);Locales("en-GB",["en-GB","EN-gb"],false);Locales("en-GB",["en-GB"," "],false);
var mcpMethod=typeof(FoundationMcpFeature).GetMethod("ValidateOptions",BindingFlags.NonPublic|BindingFlags.Static)!;
void Mcp(string route)=>mcpMethod.Invoke(null,[new FoundationMcpWebOptions{Route=route}]);
var mcpType=typeof(FoundationMcpWebOptions).FullName!;var routeMin=Bound(mcpType,"Route","minLength");var routeMax=Bound(mcpType,"Route","maxLength");
Mcp("/"+new string('a',routeMin-1));Mcp("/"+new string('a',routeMax-1));Reject(()=>Mcp("/"));Reject(()=>Mcp("/"+new string('a',routeMax)));foreach(var route in new[]{"relative","/tail/","/double//path","/template/{id}","/query?x","/fragment#x"})Reject(()=>Mcp(route));
var projectionRoot=Path.Combine(Environment.CurrentDirectory,"native-projection");Directory.CreateDirectory(Path.Combine(projectionRoot,"public"));File.WriteAllText(Path.Combine(projectionRoot,"public","publication.json"),"{\"version\":\"1.0\",\"resources\":[]}");
if(new FilePublicDocumentCatalog(projectionRoot,"public").ReadDocuments().Count!=0)throw new Exception("Native empty public projection changed");checks++;
foreach(var relative in new[]{"../outside","/absolute","bad\\path","bad:scheme","","public/../outside"})Reject(()=>new FilePublicDocumentCatalog(projectionRoot,relative).ReadDocuments());
var hostedType=typeof(HostedPageOptions).Assembly.GetType("Orbyss.Foundation.Web.HostedPages.HostedPageCatalog")!;var hostedCtor=hostedType.GetConstructors(BindingFlags.Public|BindingFlags.Instance).Single();
var hostedOptionsType=typeof(HostedPageOptions).FullName!;var hostedMin=Bound(hostedOptionsType,"MaxTotalBytes","minimum");var hostedMax=Bound(hostedOptionsType,"MaxTotalBytes","maximum");
void Hosted(int bytes,string route,bool valid){try{hostedCtor.Invoke([new HostedPageOptions{MaxTotalBytes=bytes,PagePath=route,ManifestSha256=new string('0',64)},new AssetSentinel()]);throw new Exception("Hosted sentinel not reached");}
 catch(TargetInvocationException error){if(valid&&error.InnerException is AssetSentinelReached||!valid&&error.InnerException is InvalidDataException)checks++;else throw;}}
Hosted(hostedMin,"/",true);Hosted(hostedMax,"/",true);Hosted(hostedMin-1,"/",false);Hosted(hostedMax+1,"/",false);Hosted(hostedMin,"/_foundation/owned",false);Hosted(hostedMin,"relative",false);Hosted(hostedMin,"/../outside",false);
var hashPattern=Setting(hostedOptionsType,"ManifestSha256")["constraints"]!["pattern"]!.GetValue<string>();var assetBytes=System.Text.Encoding.UTF8.GetBytes("small-native-owner-asset");
var upperHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(assetBytes));var lowerHash=upperHash.ToLowerInvariant();var mixedHash=string.Concat(upperHash.Select((letter,index)=>index%2==0?char.ToLowerInvariant(letter):letter));
var assetReader=typeof(HostedPageOptions).Assembly.GetType("Orbyss.Foundation.Web.HostedPages.HostedAssetAdmission")!.GetMethod("Read",BindingFlags.Public|BindingFlags.Static)!;
foreach(var hash in new[]{lowerHash,upperHash,mixedHash}){if(!System.Text.RegularExpressions.Regex.IsMatch(hash,hashPattern))throw new Exception("Declared hosted hash format rejects native case-insensitive hex");
 var read=(byte[])assetReader.Invoke(null,[new MemoryAssets(assetBytes),"manifest.json",hash,1024])!;if(!read.SequenceEqual(assetBytes))throw new Exception("Native case-insensitive asset hash mismatch");checks++;}
Console.WriteLine(JsonSerializer.Serialize(new{status="passed",checks,compiledDefaultsMatch=true,nativeBoundariesMatch=true,actualNativeBinding=true,storageInitialized=false,identityContacted=false}));
sealed class AssetSentinelReached:Exception {}
sealed class AssetSentinel:IHostedAssetSource {public Stream OpenRead(string relativePath)=>throw new AssetSentinelReached();}
sealed class MemoryAssets(byte[] bytes):IHostedAssetSource {public Stream OpenRead(string relativePath)=>new MemoryStream(bytes,writable:false);}
'''


def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--packages',type=Path,required=True)
    parser.add_argument('--version',default=(fixture.ROOT/'VERSION').read_text().strip()); args=parser.parse_args()
    packages=args.packages.resolve(); artifact=fixture.ROOT/'artifacts/settings-owners'; artifact.mkdir(parents=True,exist_ok=True)
    work=Path(tempfile.mkdtemp(dir=artifact)); records=[]; documents=[]; archives=[]
    for source in sorted((fixture.ROOT/'src').glob('*/settings.source.json')):
        identity=source.parent.name
        if identity=='Orbyss.Foundation.Host': continue
        matching=[p for p in packages.glob('*.nupkg') if p.name.casefold()==(identity+'.'+args.version+'.nupkg').casefold()]
        assert len(matching)==1,identity
        package=matching[0]; record=bindings.inspect(package); metadata=record.get('metadata')
        with zipfile.ZipFile(package) as archive:
            document=json.loads(archive.read('orbyss-foundation/settings.json'))
        assert document['schemaVersion']==2 and document['packageId']==identity and document['packageVersion']==args.version
        records.append(record); documents.append(document); archives.append(package)
    assert len(documents)>=16,'All declared non-Host owners must be packaged.'
    (work/'metadata.json').write_text(json.dumps(documents),encoding='utf-8')
    for name in ('Directory.Build.props','Directory.Build.targets','Directory.Packages.props'): (work/name).write_text('<Project/>')
    refs=''.join('<PackageReference Include="'+d['packageId']+'" Version="'+args.version+'"/>' for d in documents)
    central = ET.parse(fixture.ROOT/'Directory.Packages.props')
    for identity in ('CShells.Abstractions','CShells.AspNetCore.Abstractions'):
        version = next(item.get('Version') for item in central.iter('PackageVersion') if item.get('Include') == identity)
        refs += '<PackageReference Include="'+identity+'" Version="'+version+'"/>'
    project=work/'OwnersProbe.csproj'; project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/>'+refs+'</ItemGroup></Project>')
    (work/'Program.cs').write_text(PROGRAM,encoding='utf-8',newline='\n')
    config=work/'NuGet.config'; config.write_text('<configuration><packageSources><clear/><add key="actual-owner-feed" value="'+fixture.escape(str(packages))
        +'"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/><add key="cshells-preview" value="https://f.feedz.io/valence-works/cshells/nuget/index.json"/></packageSources>'
        +'<packageSourceMapping><packageSource key="actual-owner-feed"><package pattern="Orbyss.Foundation.*"/></packageSource><packageSource key="cshells-preview"><package pattern="CShells"/><package pattern="CShells.*"/></packageSource><packageSource key="nuget.org"><package pattern="*"/></packageSource></packageSourceMapping></configuration>',encoding='utf-8')
    env=dict(os.environ,NUGET_PACKAGES=str(work/'cache'),DOTNET_CLI_HOME=str(work/'dotnet-home'),MSBUILDDISABLENODEREUSE='1')
    fixture.run(['dotnet','restore',str(project),'--configfile',str(config)],work,work/'restore.log',env=env)
    fixture.run(['dotnet','restore',str(project),'--locked-mode','--configfile',str(config)],work,work/'locked-restore.log',env=env)
    fixture.run(['dotnet','build',str(project),'-c','Release','--no-restore'],work,work/'build.log',env=env)
    assets=work/'obj/project.assets.json'; runtime=work/'bin/Release/net10.0'
    for record,package in zip(records,archives): bindings.verify(record,package,assets,work/'cache',runtime)
    output=fixture.run(['dotnet',str(runtime/'OwnersProbe.dll'),str(work/'metadata.json')],work,work/'runtime.log',env=env)
    observation=json.loads(output.strip()); assert observation['status']=='passed'
    for record,package in zip(records,archives): bindings.verify(record,package,assets,work/'cache',runtime)
    result=dict(status='passed',version=args.version,owners=len(documents),ownerArchiveBindings=records,native=observation,sourceProjectReferences=False,freshPackageCache=True)
    (work/'result.json').write_text(json.dumps(result,indent=2)+'\n')
    print('Actual packaged non-Host owner defaults/admission passed; evidence: '+str(work/'result.json'))


if __name__=='__main__': main()
