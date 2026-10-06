"""Qualify offline Host integration metadata against exact published native inputs.

The exporter never executes settings or services. This independent acceptance probe
constructs native value/options objects and evaluates binding/validation only; it does
not start the Host, reconciliation, storage, identity, or application initialization.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
from types import SimpleNamespace
import subprocess
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("host_vendor_producer", ROOT / "scripts/emit_host_vendor_settings.py")
producer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(producer)

NATIVE = r'''
using System.Text.Json;
using System.Text.Json.Serialization;
using CShells;
using CShells.Configuration;
using CShells.AspNetCore;
using CShells.Lifecycle.Blueprints;
using CShells.Lifecycle.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nuplane;
using Nuplane.Loading;
using Nuplane.Loading.Hosting.Builder;
using Nuplane.Sources.Directory.Configuration;
using Nuplane.Setup;
using Nuplane.Feeds.Configuration;
using Nuplane.Feeds.Setup;
using Nuplane.Reconciliation.Configuration;
using Nuplane.Reconciliation.Convergence;
using Nuplane.Reconciliation.LockFile;
using Nuplane.Store.Cleanup;
using Nuplane.Store.State;

void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
IConfigurationRoot Config(Dictionary<string,string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
var json = new JsonSerializerOptions(); json.Converters.Add(new JsonStringEnumConverter());
var defaults = new Dictionary<string,object> {
 [typeof(CShellsOptions).FullName!] = new CShellsOptions(),
 [typeof(ShellConfig).FullName!] = new ShellConfig(),
 [typeof(WebRoutingShellOptions).FullName!] = new WebRoutingShellOptions(),
 [typeof(NuplaneSetupOptions).FullName!] = new NuplaneSetupOptions(),
 [typeof(NuplaneFeedSetupOptions).FullName!] = new NuplaneFeedSetupOptions(),
 [typeof(NuplaneDirectoryFeedSetupOptions).FullName!] = new NuplaneDirectoryFeedSetupOptions(),
 [typeof(ReconciliationOptions).FullName!] = new ReconciliationOptions(),
 [typeof(FeedResolutionOptions).FullName!] = new FeedResolutionOptions(),
 [typeof(LockFileOptions).FullName!] = new LockFileOptions(),
 [typeof(CleanupPolicyOptions).FullName!] = new CleanupPolicyOptions(),
 [typeof(ConvergenceOptions).FullName!] = new ConvergenceOptions(),
 [typeof(Nuplane.Sources.ManifestOptions).FullName!] = new Nuplane.Sources.ManifestOptions(),
 [typeof(StoreRegistryOptions).FullName!] = new StoreRegistryOptions(),
 [typeof(LoadingOptions).FullName!] = new LoadingOptions(),
 [typeof(PackageLoadModeOverrideOptions).FullName!] = new PackageLoadModeOverrideOptions()
};
var binder = new List<object>();
foreach (var token in new string?[] { null, "", "0123456789abcdef", "not-a-token" }) {
 var cfg=Config(new(){["SharedAssemblies:0:Name"]="Native.Contract",["SharedAssemblies:0:PublicKeyToken"]=token,["SharedAssemblies:0:MajorVersion"]="0"});
 var options=new LoadingOptions(); cfg.Bind(options);
 var errors=new LoadingOptionsValidator().Validate(options);
 Check(options.SharedAssemblies.Count==(token is null?0:1),"record null constructor omission differs");
 Check((errors.Count==0)==(token is null||token=="0123456789abcdef"),"record token validation differs");
 binder.Add(new{token,count=options.SharedAssemblies.Count,errors});
}
var time = new LoadingOptions(); Config(new(){["DeactivationTimeout"]="00:00:07.125",["DefaultLoadMode"]="HostIntegrated"}).Bind(time);
Check(time.DeactivationTimeout==TimeSpan.FromMilliseconds(7125)&&time.DefaultLoadMode==PackageLoadMode.HostIntegrated,"native TimeSpan/enum binder differs");
Config(new(){["DefaultLoadMode"]="1"}).Bind(time); Check(time.DefaultLoadMode==PackageLoadMode.HostIntegrated,"integral enum binder differs");
time.DeactivationTimeout=TimeSpan.Zero; Check(new LoadingOptionsValidator().Validate(time).Count>0,"zero native timeout admitted");
time.DeactivationTimeout=TimeSpan.FromTicks(1); Check(new LoadingOptionsValidator().Validate(time).Count==0,"positive native timeout rejected");
time.DefaultLoadMode=(PackageLoadMode)12345; Check(new LoadingOptionsValidator().Validate(time).Count>0,"undefined native load mode admitted");
var feeds=NuplaneFeedSetupDeclarationReader.Read(Config(new(){
 ["Setup:Feeds:0:Name"]="local",["Setup:Feeds:0:DirectoryPath"]="legacy",
 ["Setup:Feeds:local:DirectoryPath"]="selected"}));
Check(feeds.Declarations.Count==1&&feeds.Declarations[0].Options.DirectoryPath=="selected"&&feeds.Diagnostics.Any(),"named feed precedence differs");
var mismatch=NuplaneFeedSetupDeclarationReader.Read(Config(new(){["Setup:Feeds:named:Name"]="other",["Setup:Feeds:named:DirectoryPath"]="path"}));
Check(mismatch.Diagnostics.Any(x=>x.Severity==NuplaneFeedSetupDiagnosticSeverity.Error),"named feed mismatch admitted");

// These native registrations are inspected only through IOptions. No hosted service is resolved or started.
var configuration=Config(new(){
 ["Nuplane:Setup:AutomaticReconciliation"]="true",["Nuplane:Setup:PollInterval"]="00:00:17",
 ["Nuplane:Setup:StateFilePath"]="selected-native-state.json",
 ["Nuplane:Reconciliation:EnableAutomaticReconciliation"]="false",["Nuplane:Reconciliation:PollInterval"]="00:00:29",
 ["Nuplane:StoreRegistry:StateFilePath"]="lower-priority-state.json"});
var services=new ServiceCollection(); services.AddLogging();
services.AddNuplane(configuration.GetSection("Nuplane"), n=>{n.AddDirectoryFeedsFromConfiguration(configuration.GetSection("Nuplane"));n.AutoloadPackages(configuration.GetSection("Nuplane:Loading"));});
using var provider=services.BuildServiceProvider();
var reconciliation=provider.GetRequiredService<IOptions<ReconciliationOptions>>().Value;
var store=provider.GetRequiredService<IOptions<StoreRegistryOptions>>().Value;
var loading=provider.GetRequiredService<IOptions<LoadingOptions>>().Value;
Check(reconciliation.EnableAutomaticReconciliation&&reconciliation.PollInterval==TimeSpan.FromSeconds(17),"Setup did not override dedicated reconciliation options");
Check(store.StateFilePath=="selected-native-state.json","Setup did not override dedicated persistence option");
Check(!loading.Enabled,"configuration Loading overload invented enable-by-default");
configuration["Nuplane:Setup:PollInterval"]="00:00:31";
Check(provider.GetRequiredService<IOptions<ReconciliationOptions>>().Value.PollInterval==TimeSpan.FromSeconds(17),"native IOptions unexpectedly reloaded");

var shellConfiguration=Config(new(){
 ["CShells:Shells:main:Name"]="ignored-pojo-name",
 ["CShells:Shells:main:Configuration:Chosen:Value"]="configuration",
 ["CShells:Shells:main:Features:Chosen:Value"]="feature",
 ["CShells:Shells:main:Configuration:WebRouting:Path"]=""});
var blueprints=new ConfigurationShellBlueprintProvider(shellConfiguration.GetSection("CShells:Shells"));
var provided=await blueprints.GetAsync("main"); Check(provided is not null,"native named-map reader omitted shell");
var first=await provided!.Blueprint.ComposeAsync(); var snapshot=first.GetConfigurationRoot();
Check(first.Id.Name=="main"&&snapshot["Chosen:Value"]=="feature","native named identity or feature precedence differs");
Check(snapshot["WebRouting:Path"]=="","explicit root-path control lost");
shellConfiguration["CShells:Shells:main:Features:Chosen:Value"]="next";
var second=await provided.Blueprint.ComposeAsync();
Check(snapshot["Chosen:Value"]=="feature"&&second.GetConfigurationRoot()["Chosen:Value"]=="next","compose/snapshot lifecycle differs");
var malformed=new ConfigurationShellBlueprintProvider(Config(new(){["0:Features:Chosen"]="true"}));
try { _=await malformed.GetAsync("0"); throw new Exception("numeric shell map admitted"); } catch(InvalidOperationException) {}
// Compare construction values independently of per-property wire converters (ShellConfig.Features has one).
var construction=defaults.ToDictionary(x=>x.Key,x=>x.Value.GetType().GetProperties()
 .ToDictionary(property=>property.Name,property=>property.GetValue(x.Value)));
Console.WriteLine(JsonSerializer.Serialize(new{defaults=construction,binder,checks=new{
 nativeTimeSpanAndEnum=true,loadingBoundsAndEnums=true,namedFeedPrecedence=true,namedFeedMismatchRejected=true,
 setupOverridesDedicatedOptions=true,loadingNotEnabledByDefault=true,optionsSnapshotStable=true,
 namedShellIdentity=true,featureSettingsOverrideConfiguration=true,composeRereadsSnapshotIsolated=true,
 numericShellMapRejected=true,hostStarted=false,applicationInitialized=false}},json));
'''


def run(command: list[str], work: Path, name: str) -> str:
    result = subprocess.run(command, cwd=work, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=90, check=False)
    output = result.stdout.decode("utf-8", errors="replace")
    (work / name).write_text(output, encoding="utf-8")
    if result.returncode:
        raise AssertionError("Native acceptance failed: " + name + "\n" + output[-12_000:])
    return output


def native_probe(host: Path, dotnet: Path, work: Path) -> dict:
    project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
    properties = ET.SubElement(project, "PropertyGroup")
    for name, value in dict(TargetFramework="net10.0", OutputType="Exe", ImplicitUsings="enable", Nullable="enable", UseSharedCompilation="false").items():
        ET.SubElement(properties, name).text = value
    items = ET.SubElement(project, "ItemGroup")
    ET.SubElement(items, "FrameworkReference", Include="Microsoft.AspNetCore.App")
    for binary in sorted(host.glob("*.dll")):
        reference = ET.SubElement(items, "Reference", Include=binary.stem)
        ET.SubElement(reference, "HintPath").text = str(binary)
    ET.ElementTree(project).write(work / "Probe.csproj", encoding="utf-8", xml_declaration=True)
    for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"):
        (work / name).write_text("<Project/>\n", encoding="utf-8")
    (work / "NuGet.config").write_text("<configuration><packageSources><clear/></packageSources></configuration>\n", encoding="utf-8")
    (work / "Program.cs").write_text(NATIVE, encoding="utf-8")
    run([str(dotnet), "restore", "Probe.csproj", "--configfile", "NuGet.config", "/nr:false"], work, "native-restore.log")
    run([str(dotnet), "build", "Probe.csproj", "-c", "Release", "--no-restore", "/nr:false"], work, "native-build.log")
    return json.loads(run([str(dotnet), str(work / "bin/Release/net10.0/Probe.dll")], work, "native-probe.json"))


def verify_defaults(metadata: dict, native: dict, graph: dict) -> int:
    actual = native["defaults"]
    assertions = 0
    for definition in graph["types"]:
        name = definition["typeName"]
        if name not in actual:
            continue
        properties = {x["name"]: x for x in definition["properties"]}
        assert set(properties) == set(actual[name]), "Static/native public property set differs: " + name
        for property_name, entry in properties.items():
            if entry.get("secret"):
                assert not entry["hasDefault"] and "default" not in entry, "Secret default was evaluated or exported."
                continue
            assert entry["hasDefault"] and entry.get("default") == actual[name][property_name], "Static default differs from actual native construction: " + name + "." + property_name
            assertions += 1
    for contract in metadata["contracts"]:
        for setting in contract["settings"]:
            constraints = setting["constraints"]
            if "bindingAuthority" in constraints:
                assert "default" not in setting, "Manual binding controls must not claim unused POCO constructor defaults."
                continue
            if "default" in setting:
                assert setting["default"] == actual[constraints["sourceType"]][constraints["sourceProperty"]]
                if setting["default"] is not None:
                    assert isinstance(setting["default"], {"string": str, "boolean": bool, "array": list, "object": dict, "integer": int, "number": (int,float)}[setting["type"]]), "Emitted default does not match declared kind."
            assert not(setting["secret"] and "default" in setting)
    entries = {x["path"]: x for c in metadata["contracts"] for x in c["settings"]}
    assert entries["Nuplane:Loading:SharedAssemblies:{index}:PublicKeyToken"]["required"] is True
    assert "default" not in entries["Nuplane:Loading:SharedAssemblies:{index}:PublicKeyToken"]
    assert entries["Nuplane:Loading:DeactivationTimeout"]["constraints"]["format"] == "dotnetTimeSpan"
    for prefix in ("Nuplane:Setup:Feeds:{feed}", "Nuplane:FeedResolution:Feeds:{index}"):
        assert entries[prefix + ":Credentials"]["secret"] is True and "default" not in entries[prefix + ":Credentials"]
    assert all(value is True for key,value in native["checks"].items() if key not in ("hostStarted","applicationInitialized"))
    assert native["checks"]["hostStarted"] is False and native["checks"]["applicationInitialized"] is False
    return assertions


def copy_native_archives(resources: Path, assets: Path, destination: Path) -> dict:
    restore = producer.read_json(assets, 16_777_216)
    receipts = {}
    destination.mkdir()
    for item in producer.read_json(resources / "inputs.json")["origins"]:
        library = restore["libraries"][item["packageId"] + "/" + item["packageVersion"]]
        matches = [producer.contained(Path(folder), library["path"] + "/" + item["archive"]["name"]) for folder in restore["packageFolders"]]
        source = [x for x in matches if x.is_file()]
        assert len(source) == 1 and producer.digest(source[0], producer.MAX_ARCHIVE) == item["archive"]["sha256"]
        target = destination / item["archive"]["name"]
        shutil.copyfile(source[0], target)
        assert producer.digest(target, producer.MAX_ARCHIVE) == item["archive"]["sha256"]
        receipts[target.name] = item["archive"]["sha256"]
    return receipts


def stale_guards(args: SimpleNamespace, metadata: dict, work: Path) -> list[str]:
    accepted = args.output.read_bytes()
    cases = []
    def rejects(name, action):
        try:
            action()
        except (ValueError,AssertionError) as error:
            cases.append(name)
            (work / (name + ".txt")).write_text(str(error),encoding="utf-8")
        else:
            raise AssertionError("Metadata guard admitted " + name)
        assert args.output.read_bytes() == accepted, "Rejected stale check rewrote accepted metadata."
    args.validate_only=True
    producer.emit(args)
    altered=copy.deepcopy(metadata); altered["contracts"][1]["settings"][0]["default"]=not altered["contracts"][1]["settings"][0]["default"]
    args.output.write_text(json.dumps(altered),encoding="utf-8")
    try:
        try: producer.emit(args)
        except ValueError: cases.append("changed-emitted-default")
        else: raise AssertionError("Changed emitted default admitted")
    finally: args.output.write_bytes(accepted)
    source_copy=args.host_output/metadata["sourceFiles"]["producer/emit_host_vendor_settings.py"]
    original=source_copy.read_bytes(); source_copy.write_bytes(original+b"\n# altered authority\n")
    try: rejects("changed-retained-producer-copy",lambda:producer.emit(args))
    finally: source_copy.write_bytes(original)
    binary=args.host_output/"Nuplane.dll"; original=binary.read_bytes();binary.write_bytes(original+b"tamper")
    try: rejects("changed-native-host-dll",lambda:producer.emit(args))
    finally: binary.write_bytes(original)
    vendor_copy=args.host_output/next(iter(metadata["origins"][0]["source"]["files"]))
    original=vendor_copy.read_bytes();vendor_copy.write_bytes(original+b"\n// changed native source\n")
    try: rejects("changed-retained-native-source",lambda:producer.emit(args))
    finally: vendor_copy.write_bytes(original)
    selected=args.resources/"sources"/next(iter(producer.read_json(args.resources/"inputs.json")["origins"][0]["source"]["files"]))
    original=selected.read_bytes();selected.write_bytes(original+b"\n// current source changed\n")
    try: rejects("changed-current-native-source",lambda:producer.emit(args))
    finally: selected.write_bytes(original)
    input_path=args.resources/"inputs.json";original=input_path.read_bytes(); changed=json.loads(original)
    changed["origins"][0]["source"]["commit"]="0"*40; input_path.write_text(json.dumps(changed),encoding="utf-8")
    try: rejects("changed-native-source-commit",lambda:producer.emit(args))
    finally: input_path.write_bytes(original)
    declaration_path=args.resources/"integration.json";original=declaration_path.read_bytes();changed=json.loads(original)
    changed["boundaries"][0]["semanticConstraints"].append("changed current integration authority")
    declaration_path.write_text(json.dumps(changed),encoding="utf-8")
    try: rejects("changed-current-integration",lambda:producer.emit(args))
    finally: declaration_path.write_bytes(original)
    # Substitute an identical disposable producer source for hashing, then change
    # that current input. The real checkout source remains untouched throughout.
    producer_source=work/"current-producer.py";producer_source.write_bytes(Path(producer.__file__).read_bytes())
    original_file=producer.__file__;producer.__file__=str(producer_source)
    try:
        producer.emit(args)
        producer_source.write_bytes(producer_source.read_bytes()+b"\n# changed current producer input\n")
        rejects("changed-current-producer-source",lambda:producer.emit(args))
    finally: producer.__file__=original_file
    producer.emit(args)
    return cases


def main() -> None:
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host",type=Path,default=ROOT/"src/Orbyss.Foundation.Host/bin/Release/net10.0")
    parser.add_argument("--assets",type=Path,default=ROOT/"src/Orbyss.Foundation.Host/obj/project.assets.json")
    parser.add_argument("--build-task-assembly",type=Path,default=ROOT/"src/Orbyss.Foundation.Build/bin/Release/net10.0/Orbyss.Foundation.Build.dll")
    parser.add_argument("--dotnet",type=Path,default=Path(shutil.which("dotnet") or "dotnet"))
    parser.add_argument("--version",default=(ROOT/"VERSION").read_text().strip())
    parser.add_argument("--work",type=Path,default=ROOT/"artifacts/host-vendor-settings-acceptance")
    cli=parser.parse_args(); cli.work.mkdir(parents=True,exist_ok=True)
    work=Path(tempfile.mkdtemp(prefix="run-",dir=cli.work.resolve()))
    host=work/"host";shutil.copytree(cli.host.resolve(),host)
    resources=work/"resources";shutil.copytree(producer.RESOURCES,resources)
    args=SimpleNamespace(resources=resources,assets=cli.assets.resolve(),host_output=host,
        output=host/".orbyss-foundation/host-native.settings.json",version=cli.version,
        build_task_assembly=cli.build_task_assembly.resolve(),dotnet=cli.dotnet.resolve(),work=work/"graph",validate_only=False)
    producer.emit(args)
    metadata=producer.read_json(args.output)
    graph_paths=sorted(args.work.glob("host-vendor-settings-*/graph.json"));assert len(graph_paths)==1
    graph=producer.read_json(graph_paths[0])
    native=work/"native";native.mkdir();observed=native_probe(host,cli.dotnet.resolve(),native)
    assertions=verify_defaults(metadata,observed,graph)
    archives=copy_native_archives(resources,cli.assets.resolve(),work/"native-archives")
    guards=stale_guards(args,metadata,work)
    producer.write_json(work/"result.json",dict(status="passed",nativeDefaultsCompared=assertions,
        bindingChecks=observed["checks"],guardRejections=guards,nativeArchives=archives,
        metadataSha256=producer.digest(args.output),exporterConstructorsExecuted=False,hostStarted=False,applicationInitialized=False))
    print("Native Host settings defaults/binding/provenance/stale-output acceptance passed: "+str(work/"result.json"))


if __name__=="__main__": main()
