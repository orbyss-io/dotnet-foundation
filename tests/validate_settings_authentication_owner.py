"""Actual packaged authentication settings validators/binding, without storage or identity initialization."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile
import zipfile
from xml.etree import ElementTree as ET
import validate_openapi_exporter as fixture
import settings_package_bindings as bindings

OWNERS = ['Orbyss.Foundation.Authentication', 'Orbyss.Foundation.Authentication.BffCookie',
          'Orbyss.Foundation.Authentication.SpaPkce', 'Orbyss.Foundation.Authentication.Assurance',
          'Orbyss.Foundation.Authentication.ClientCredentials', 'Orbyss.Foundation.Authentication.TokenExchange',
          'Orbyss.Foundation.Authentication.DownstreamApi', 'Orbyss.Foundation.Authentication.DPoP',
          'Orbyss.Foundation.Identity.Keycloak.Admin']

PROGRAM = r'''using CShells;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Authentication.Core;
using Orbyss.Foundation.Authentication.BffCookie;
using Orbyss.Foundation.Authentication.SpaPkce;
using Orbyss.Foundation.Authentication.Assurance;
using Orbyss.Foundation.Authentication.ClientCredentials;
using Orbyss.Foundation.Authentication.TokenExchange;
using Orbyss.Foundation.Authentication.DownstreamApi;
using Orbyss.Foundation.Authentication.DPoP;
using Orbyss.Foundation.Identity.Keycloak.Admin;
using System.Text.Json.Nodes;

var metadata = JsonNode.Parse(File.ReadAllText(args[0]));
int checks = 0;
void Check<T>(Action<IServiceCollection, ShellSettings> feature, Action<T> seed, Action<T> mutation,
    bool valid, Action<ShellSettings> configure = null, string environment = "Production") where T : class {
    var settings = new ShellSettings(new ShellId("metadata-probe")); configure?.Invoke(settings);
    var services = new ServiceCollection(); services.AddLogging();
    services.AddSingleton<IHostEnvironment>(new ProbeEnvironment(environment)); feature(services, settings);
    services.AddSingleton<IHttpClientFactory>(_ => throw new InvalidOperationException("IDENTITY_CLIENT_RESOLVED_DURING_SETTINGS_VALIDATION"));
    services.Configure<T>(options => { seed(options); mutation(options); });
    using var provider = services.BuildServiceProvider();
    try { _ = provider.GetRequiredService<IOptions<T>>().Value; if (!valid) throw new Exception("Invalid settings admitted: " + typeof(T).Name); }
    catch (OptionsValidationException) { if (valid) throw; }
    checks++;
}
void Web(IServiceCollection services, ShellSettings settings) {
    new FoundationAuthenticationFeature(settings).ConfigureServices(services);
    new FoundationBffCookieFeature().ConfigureServices(services);
}
void SeedWeb(FoundationWebOptions options) {
    options.Authority = "https://identity.example"; options.ClientId = "fixture"; options.Audience = "fixture-api";
    options.ClientSecret = "fictional-validation-input"; options.SessionAbsoluteMinutes = 600;
}
void Bounds<T>(string owner, string scope, string property, Action<IServiceCollection, ShellSettings> feature,
    Action<T> seed, Action<T, int> assign) where T : class {
    var setting = metadata[owner]["contracts"].AsArray().Single(c => c["scope"].GetValue<string>() == scope)
        ["settings"].AsArray().Single(s => s["path"].GetValue<string>().EndsWith(":" + property, StringComparison.Ordinal));
    var limits = setting["constraints"]; var minimum = limits["minimum"].GetValue<int>(); var maximum = limits["maximum"].GetValue<int>();
    foreach (var value in new[] { minimum, maximum }) Check(feature, seed, options => assign(options, value), true);
    foreach (var value in new[] { minimum - 1, maximum + 1 }) Check(feature, seed, options => assign(options, value), false);
}
Bounds<FoundationWebOptions>("Orbyss.Foundation.Authentication", "web", "DiscoveryTimeoutSeconds", Web, SeedWeb, (o,v) => o.DiscoveryTimeoutSeconds=v);
Check<FoundationWebOptions>(Web, SeedWeb, o => o.SessionIdleMinutes=0, false);
Check<FoundationWebOptions>(Web, SeedWeb, o => o.SessionAbsoluteMinutes=o.SessionIdleMinutes-1, false);
foreach (var reserved in new[] { AuthenticationClaimTypes.ValidatedIssuer, AuthenticationClaimTypes.ValidatedSubject.ToUpperInvariant() })
    Check<FoundationWebOptions>(Web, SeedWeb, o => o.RoleClaim=reserved, false);
Check<FoundationWebOptions>(Web, SeedWeb, o => o.PermissionClaim=AuthenticationClaimTypes.ValidatedIssuer, false);
Check<FoundationWebOptions>(Web, SeedWeb, o => o.RolePermissions["operator"]=[], false);
Check<FoundationWebOptions>(Web, SeedWeb, o => o.ScopePermissions[""]=["notes.read"], false);
foreach (var scopes in new[] { new[] { "profile" }, new[] { "openid", "openid" }, new[] { "openid", "two scopes" }, new[] { "openid", "\u00e9" } })
    Check<FoundationWebOptions>(Web, SeedWeb, o => o.Scopes=scopes, false);
Check<FoundationWebOptions>(Web, SeedWeb, _ => {}, false, settings => settings.ConfigurationData["Foundation:Web:Scopes"]="");
Check<FoundationWebOptions>(Web, SeedWeb, _ => {}, false, settings => settings.ConfigurationData["Foundation:Web:Scopes:1"]="openid");
Check<FoundationWebOptions>(Web, SeedWeb, _ => {}, true, settings => settings.ConfigurationData["Foundation:Web:Scopes:0"]="openid");
Check<FoundationWebOptions>((s,c) => { Web(s,c); new FoundationSpaPkceFeature().ConfigureServices(s); }, SeedWeb, _ => {}, false);
Check<FoundationWebOptions>(Web, SeedWeb, o => o.ClientSecret="", false);
Check<FoundationWebOptions>(Web, SeedWeb, o => o.CallbackPath=o.SignedOutCallbackPath, false);
Check<FoundationWebOptions>(Web, SeedWeb, o => o.CallbackPath="/bff/user", false);
void Spa(IServiceCollection s, ShellSettings c) { new FoundationAuthenticationFeature(c).ConfigureServices(s); new FoundationSpaPkceFeature().ConfigureServices(s); }
void SeedSpa(FoundationWebOptions o) { SeedWeb(o); o.ClientSecret=""; o.AllowedOrigins=["https://application.example"]; }
Check<FoundationWebOptions>(Spa, SeedSpa, _ => {}, true);
Check<FoundationWebOptions>(Spa, SeedSpa, o => o.AllowedOrigins=[], false);
Check<FoundationWebOptions>(Spa, SeedSpa, o => o.AllowedOrigins=["https://*.example"], false);
Check<FoundationWebOptions>(Spa, SeedSpa, o => o.ClientSecret="fictional-invalid-input", false);

void Assurance(IServiceCollection s, ShellSettings c) => new FoundationAssuranceFeature(c).ConfigureServices(s);
void SeedAssurance(AssuranceOptions o) => o.Policies["fresh"] = new AssurancePolicy { MaximumAuthenticationAgeSeconds=60 };
Bounds<AssuranceOptions>("Orbyss.Foundation.Authentication.Assurance", "assurance", "ClockSkewSeconds", Assurance, SeedAssurance, (o,v)=>o.ClockSkewSeconds=v);
Check<AssuranceOptions>(Assurance, SeedAssurance, o => o.Policies["fresh"].MaximumAuthenticationAgeSeconds=-1, false);
Check<AssuranceOptions>(Assurance, SeedAssurance, o => o.Policies["fresh"].MaximumAuthenticationAgeSeconds=86401, false);
Check<AssuranceOptions>(Assurance, SeedAssurance, o => o.Policies["fresh"].MaximumAuthenticationAgeSeconds=null, false);
Check<AssuranceOptions>(Assurance, SeedAssurance, o => o.Policies["fresh"].RequiredAmrValues=["mfa", "mfa"], false);

void Credentials(IServiceCollection s, ShellSettings c) => new FoundationClientCredentialsFeature(c).ConfigureServices(s);
void SeedCredentials(ClientCredentialsOptions o) => o.Registrations["machine"] = new ClientCredentialsRegistration {
    TokenEndpoint="https://identity.example/token", ClientId="fixture", ClientSecret="fictional-validation-input" };
Bounds<ClientCredentialsOptions>("Orbyss.Foundation.Authentication.ClientCredentials", "client-credentials-registration", "TimeoutSeconds", Credentials, SeedCredentials, (o,v)=>o.Registrations["machine"].TimeoutSeconds=v);
Bounds<ClientCredentialsOptions>("Orbyss.Foundation.Authentication.ClientCredentials", "client-credentials-registration", "RefreshBeforeExpirySeconds", Credentials, SeedCredentials, (o,v)=>o.Registrations["machine"].RefreshBeforeExpirySeconds=v);
Check<ClientCredentialsOptions>(Credentials, SeedCredentials, o=>o.Registrations["machine"].ClientSecret="", false);
Check<ClientCredentialsOptions>(Credentials, SeedCredentials, o=>o.Registrations["machine"].Scopes=["two scopes"], false);
Check<ClientCredentialsOptions>(Credentials, SeedCredentials, o=>o.Registrations["machine"].TokenEndpoint="https://identity.example/token?q=1", false);
Check<ClientCredentialsOptions>(Credentials, SeedCredentials, o=> { o.Registrations["machine"].TokenEndpoint="http://localhost/token"; o.Registrations["machine"].AllowHttpForLocalDevelopment=true; }, true, environment:"Development");
Check<ClientCredentialsOptions>(Credentials, SeedCredentials, o=> { o.Registrations["machine"].TokenEndpoint="http://localhost/token"; o.Registrations["machine"].AllowHttpForLocalDevelopment=true; }, false);

void Exchange(IServiceCollection s, ShellSettings c) => new FoundationTokenExchangeFeature(c).ConfigureServices(s);
void SeedExchange(TokenExchangeOptions o) => o.Registrations["exchange"] = new TokenExchangeRegistration {
    TokenEndpoint="https://identity.example/token", ClientId="fixture", ClientSecret="fictional-validation-input" };
Bounds<TokenExchangeOptions>("Orbyss.Foundation.Authentication.TokenExchange", "token-exchange-registration", "TimeoutSeconds", Exchange, SeedExchange, (o,v)=>o.Registrations["exchange"].TimeoutSeconds=v);
Check<TokenExchangeOptions>(Exchange, SeedExchange, o=>o.Registrations["exchange"].ClientAuthenticationMethod="unknown", false);

void Downstream(IServiceCollection s, ShellSettings c) => new FoundationDownstreamApiFeature(c).ConfigureServices(s);
void SeedDownstream(DownstreamApiOptions o) => o.Registrations["api"] = new DownstreamApiRegistration {
    BaseAddress="https://api.example", TokenRegistration="machine", AccessMode="TokenExchange", Audience="api" };
Check<DownstreamApiOptions>(Downstream, SeedDownstream, _=>{}, true);
Check<DownstreamApiOptions>(Downstream, SeedDownstream, o=>o.Registrations["api"].Audience=null, false);
Check<DownstreamApiOptions>(Downstream, SeedDownstream, o=> { o.Registrations["api"].Audience=null; o.Registrations["api"].Resource="https://api.example"; }, true);
Check<DownstreamApiOptions>(Downstream, SeedDownstream, o=>o.Registrations["api"].Resource="file:///", false);
Check<DownstreamApiOptions>(Downstream, SeedDownstream, o=>o.Registrations["api"].AccessMode="unknown", false);

void Dpop(IServiceCollection s, ShellSettings c) => new FoundationDPoPFeature(c).ConfigureServices(s);
void SeedDpop(DPoPOptions o) => o.PublicOrigin="https://api.example";
Bounds<DPoPOptions>("Orbyss.Foundation.Authentication.DPoP", "dpop", "ProofMaxAgeSeconds", Dpop, SeedDpop, (o,v)=>o.ProofMaxAgeSeconds=v);
Bounds<DPoPOptions>("Orbyss.Foundation.Authentication.DPoP", "dpop", "ClockSkewSeconds", Dpop, SeedDpop, (o,v)=>o.ClockSkewSeconds=v);
Bounds<DPoPOptions>("Orbyss.Foundation.Authentication.DPoP", "dpop", "NonceLifetimeSeconds", Dpop, SeedDpop, (o,v)=>o.NonceLifetimeSeconds=v);
Check<DPoPOptions>(Dpop, SeedDpop, o=>o.PublicOrigin="https://api.example/path", false);

void Keycloak(IServiceCollection s, ShellSettings c) => new FoundationKeycloakUsersAdminFeature(c).ConfigureServices(s);
void SeedKeycloak(KeycloakAdminOptions o) { o.ServerUrl="https://identity.example"; o.Realm="fixture"; o.ClientId="fixture"; o.ClientSecret="fictional-validation-input"; }
Bounds<KeycloakAdminOptions>("Orbyss.Foundation.Identity.Keycloak.Admin", "keycloak-admin", "TimeoutSeconds", Keycloak, SeedKeycloak, (o,v)=>o.TimeoutSeconds=v);
Bounds<KeycloakAdminOptions>("Orbyss.Foundation.Identity.Keycloak.Admin", "keycloak-admin", "RefreshBeforeExpirySeconds", Keycloak, SeedKeycloak, (o,v)=>o.RefreshBeforeExpirySeconds=v);
Check<KeycloakAdminOptions>(Keycloak, SeedKeycloak, o=>o.ClientSecret="", false);
Check<KeycloakAdminOptions>(Keycloak, SeedKeycloak, o=>o.ServerUrl="http://identity.internal", false);
Console.WriteLine($"Packaged authentication metadata/native validator admission passed ({checks} cases); no identity/storage client resolved.");
sealed class ProbeEnvironment(string name) : IHostEnvironment {
    public string EnvironmentName {get;set;}=name;
    public string ApplicationName {get;set;}="settings-probe";
    public string ContentRootPath {get;set;}=AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();
}
'''


def qualify(packages: Path) -> None:
    packages = packages.resolve()
    metadata, identities, inspected = {}, {}, {}
    for owner in OWNERS:
        matches = []
        for path in packages.glob(owner + '.*.nupkg'):
            with zipfile.ZipFile(path) as archive:
                if 'orbyss-foundation/settings.json' not in archive.namelist():
                    continue
                value = json.loads(archive.read('orbyss-foundation/settings.json'))
                if value['packageId'] != owner:
                    continue
                assert value['schemaVersion'] == 2 and all(item['complete'] for item in value['contracts'])
                assert hashlib.sha256(archive.read('lib/net10.0/' + value['assembly']['name'])).hexdigest() == value['assembly']['sha256']
                for item in value['contracts']:
                    assert all('default' not in setting for setting in item['settings'] if setting['secret'])
                matches.append((path, value))
        assert len(matches) == 1, ('Missing/ambiguous actual owner archive', owner)
        path, value = matches[0]
        inspected[owner] = (path, bindings.inspect(path))
        metadata[owner] = value
        identities[owner] = {'version': value['packageVersion'], 'archiveSha256': hashlib.sha256(path.read_bytes()).hexdigest()}
    assert len({item['version'] for item in identities.values()}) == 1
    artifact = fixture.ROOT / 'artifacts/settings-authentication-owner'
    artifact.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(dir=artifact))
    for name in ('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props'):
        (work / name).write_text('<Project/>', encoding='utf-8')
    project = work / 'AuthenticationSettingsProbe.csproj'
    references = ''.join(f'<PackageReference Include="{owner}" Version="{item["version"]}"/>' for owner, item in identities.items())
    central = ET.parse(fixture.ROOT / 'Directory.Packages.props')
    for identity in ('CShells.Abstractions', 'CShells.AspNetCore.Abstractions'):
        version = next(item.get('Version') for item in central.iter('PackageVersion') if item.get('Include') == identity)
        references += f'<PackageReference Include="{identity}" Version="{version}"/>'
    project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/>' + references + '</ItemGroup></Project>', encoding='utf-8')
    config = work / 'NuGet.config'
    config.write_text('<configuration><packageSources><clear/><add key="candidate" value="' + fixture.escape(str(packages))
        + '"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/><add key="cshells-preview" value="https://f.feedz.io/valence-works/cshells/nuget/index.json"/></packageSources>'
        + '<packageSourceMapping><packageSource key="candidate"><package pattern="Orbyss.Foundation.*"/></packageSource><packageSource key="cshells-preview"><package pattern="CShells"/><package pattern="CShells.*"/></packageSource><packageSource key="nuget.org"><package pattern="*"/></packageSource></packageSourceMapping></configuration>', encoding='utf-8')
    (work / 'Program.cs').write_text(PROGRAM, encoding='utf-8')
    data = work / 'owner-metadata.json'
    data.write_text(json.dumps(metadata), encoding='utf-8')
    environment = dict(os.environ, DOTNET_CLI_HOME=str(work / 'dotnet-home'), NUGET_PACKAGES=str(work / 'cache'))
    fixture.run(['dotnet', 'restore', str(project), '--configfile', str(config)], work, work / 'restore.log', env=environment)
    fixture.run(['dotnet', 'restore', str(project), '--locked-mode', '--configfile', str(config)], work, work / 'locked-restore.log', env=environment)
    fixture.run(['dotnet', 'build', str(project), '-c', 'Release', '--no-restore'], work, work / 'build.log', env=environment)
    runtime = work / 'bin/Release/net10.0'
    for path, record in inspected.values():
        bindings.verify(record, path, work / 'obj/project.assets.json', work / 'cache', runtime)
    output = fixture.run(['dotnet', str(work / 'bin/Release/net10.0/AuthenticationSettingsProbe.dll'), str(data)], work, work / 'runtime.log', env=environment)
    assert 'admission passed' in output
    for path, record in inspected.values():
        bindings.verify(record, path, work / 'obj/project.assets.json', work / 'cache', runtime)
    result = {'status': 'passed', 'actualPackagedOwners': identities, 'nativeValidatorMetadataBounds': True,
              'nativeScopesBindingRejectedInvalidShapes': True, 'actualProfileComposition': True,
              'storageOrIdentityClientResolved': False, 'metadataSha256': hashlib.sha256(data.read_bytes()).hexdigest(),
              'nativeRestoredAndExecutedPackages': [record for _, record in inspected.values()]}
    (work / 'results.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(output.strip() + ' Evidence: ' + str(work / 'results.json'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packages', type=Path, default=fixture.ROOT / 'artifacts/nuget')
    qualify(parser.parse_args().packages)


if __name__ == '__main__':
    main()
