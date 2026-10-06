"""Compare the real compiled Host's metadata with native binding/defaults and Kestrel admission."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile
import validate_openapi_exporter as fixture


def qualify(host: Path, metadata: Path) -> None:
    host, metadata = host.resolve(), metadata.resolve()
    value = json.loads(metadata.read_text(encoding='utf-8'))
    assert value['packageId'] == 'Orbyss.Foundation.Host' and value['schemaVersion'] == 2
    assert value['assembly'] == {'name': host.name, 'sha256': hashlib.sha256(host.read_bytes()).hexdigest()}
    contracts = {item['scope']: item for item in value['contracts']}
    assert {'host-transport', 'host-boot', 'host-cshells-binding', 'host-nuplane-binding'} == set(contracts)
    parent = fixture.ROOT / 'artifacts/host-settings-owner'
    parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(dir=parent))
    for name in ('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props'):
        (work / name).write_text('<Project/>', encoding='utf-8')
    project = work / 'HostOwnerProbe.csproj'
    project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/>
<Reference Include="Orbyss.Foundation.Host"><HintPath>{fixture.escape(str(host))}</HintPath></Reference>
</ItemGroup></Project>''', encoding='utf-8')
    program = r'''using System.Text.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Orbyss.Foundation.Host.Transport;
using Orbyss.Foundation.Host.Shells;
Console.WriteLine(JsonSerializer.Serialize(new { Transport = new FoundationTransportOptions(), Boot = new FoundationBootOptions() }));
foreach (var body in new long[] { 1, 1073741824 }) {
    var options = new KestrelServerOptions(); new FoundationTransportOptions { MaxRequestBodyBytes = body }.Apply(options);
    if (options.Limits.MaxRequestBodySize != body) throw new Exception("Native body admission differs.");
}
foreach (var headers in new[] { 1024, 1048576 }) {
    var options = new KestrelServerOptions(); new FoundationTransportOptions { MaxRequestHeadersBytes = headers }.Apply(options);
    if (options.Limits.MaxRequestHeadersTotalSize != headers) throw new Exception("Native header admission differs.");
}
foreach (var invalid in new[] { new FoundationTransportOptions { MaxRequestBodyBytes = 0 },
    new FoundationTransportOptions { MaxRequestBodyBytes = 1073741825 },
    new FoundationTransportOptions { MaxRequestHeadersBytes = 1023 },
    new FoundationTransportOptions { MaxRequestHeadersBytes = 1048577 } }) {
    try { invalid.Apply(new KestrelServerOptions()); throw new Exception("Invalid transport limit admitted."); }
    catch (InvalidOperationException) { }
}
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string> {
    ["Foundation:Boot:EagerShellActivation"] = "false", ["Foundation:Boot:FailOnShellActivationError"] = "false",
    ["Foundation:Transport:MaxRequestBodyBytes"] = "4096", ["Foundation:Transport:MaxRequestHeadersBytes"] = "8192" }).Build();
var boot = configuration.GetSection("Foundation:Boot").Get<FoundationBootOptions>();
var transport = configuration.GetSection("Foundation:Transport").Get<FoundationTransportOptions>();
if (boot.EagerShellActivation || boot.FailOnShellActivationError || transport.MaxRequestBodyBytes != 4096
    || transport.MaxRequestHeadersBytes != 8192) throw new Exception("Native configuration override differs.");
configuration["Foundation:Boot:EagerShellActivation"] = "invalid";
try { _ = configuration.GetSection("Foundation:Boot").Get<FoundationBootOptions>(); throw new Exception("Invalid native bool admitted."); }
catch (InvalidOperationException) { }
'''
    (work / 'Program.cs').write_text(program, encoding='utf-8')
    config = work / 'NuGet.config'
    config.write_text('<configuration><packageSources><clear/></packageSources></configuration>', encoding='utf-8')
    environment = dict(os.environ, DOTNET_CLI_HOME=str(work / 'dotnet-home'), NUGET_PACKAGES=str(work / 'cache'))
    fixture.run(['dotnet', 'restore', str(project), '--configfile', str(config)], work, work / 'restore.log', env=environment)
    fixture.run(['dotnet', 'build', str(project), '-c', 'Release', '--no-restore'], work, work / 'build.log', env=environment)
    output = fixture.run(['dotnet', str(work / 'bin/Release/net10.0/HostOwnerProbe.dll')], work, work / 'runtime.log', env=environment)
    defaults = json.loads(output.strip())
    for scope, key in [('host-transport', 'Transport'), ('host-boot', 'Boot')]:
        assert defaults[key] == {item['path'].rsplit(':', 1)[-1]: item['default'] for item in contracts[scope]['settings']}
    bounds = {item['path'].rsplit(':', 1)[-1]: item['constraints'] for item in contracts['host-transport']['settings']}
    assert bounds == {'MaxRequestBodyBytes': {'minimum': 1, 'maximum': 1073741824},
                      'MaxRequestHeadersBytes': {'minimum': 1024, 'maximum': 1048576}}
    result = {'status': 'passed', 'actualCompiledHost': True, 'hostAssemblySha256': value['assembly']['sha256'],
              'metadataSha256': hashlib.sha256(metadata.read_bytes()).hexdigest(), 'defaultsMatch': True,
              'nativeBindingOverrides': True, 'nativeInvalidBooleanRejected': True, 'nativeKestrelBoundsMatch': True,
              'applicationEntryPointStarted': False, 'storageOrIdentityContacted': False}
    (work / 'results.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print('Actual compiled Host defaults/native binding/Kestrel bounds passed. Evidence: ' + str(work / 'results.json'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--host', type=Path, default=fixture.ROOT / 'src/Orbyss.Foundation.Host/bin/Release/net10.0/Orbyss.Foundation.Host.dll')
    parser.add_argument('--metadata', type=Path)
    args = parser.parse_args()
    qualify(args.host, args.metadata or args.host.parent / '.orbyss-foundation/host-settings.json')


if __name__ == '__main__':
    main()
