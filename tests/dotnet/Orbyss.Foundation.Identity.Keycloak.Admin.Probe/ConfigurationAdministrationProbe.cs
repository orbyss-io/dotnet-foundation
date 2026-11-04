using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orbyss.Foundation.Identity.Admin;
using Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Checks configuration metadata reads and partial writes against independent provider fixtures.</summary>
internal static class ConfigurationAdministrationProbe
{
    /// <summary>Verifies omitted fields, credentials, map entries and unrelated capabilities are preserved.</summary>
    internal static async Task RunAsync()
    {
        using var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var services = new ServiceCollection();
        services.AddFoundationKeycloakAdministration(options =>
        {
            options.ServerUrl = "http://localhost:8080";
            options.Realm = "tenant realm";
            options.AdminRealm = "service realm";
            options.ClientId = "configuration";
            options.ClientSecret = "fictional-secret";
            options.AllowHttpForLocalDevelopment = true;
        }, KeycloakAdminFeatures.Configuration | KeycloakAdminFeatures.Realms, "Development");
        services.RemoveAll<IHttpClientFactory>();
        services.AddSingleton<IHttpClientFactory>(new ProbeHttpClientFactory(client));
        await using var provider = services.BuildServiceProvider();
        Require(provider.GetService<IIdentityUserAdministration>() is null, "Configuration registered user administration.");
        Require(provider.GetService<IIdentityAccessAdministration>() is null, "Configuration registered general access administration.");
        var configuration = provider.GetRequiredService<IKeycloakConfigurationAdministration>();
        var found = await configuration.GetClientsAsync("portal");
        Require(found.Single().Attributes?["owner"] == "release", "Client ownership metadata was lost.");
        var actual = await configuration.GetClientAsync("client-owned");
        Require(actual?.ServiceAccountsEnabled is true, "Service-account configuration was lost.");
        await configuration.UpdateClientAsync("client-owned", new() { Enabled = false, ImplicitFlowEnabled = false,
            Attributes = new Dictionary<string, string> { ["release"] = "new" } });
        var clientUpdate = Body(handler, "/clients/client-owned");
        Require(!clientUpdate.TryGetProperty("id", out _) && !clientUpdate.TryGetProperty("publicClient", out _)
            && !clientUpdate.TryGetProperty("serviceAccountsEnabled", out _) && !clientUpdate.TryGetProperty("secret", out _),
            "A partial client update replayed identity, defaults or a credential.");
        Require(clientUpdate.GetProperty("attributes").GetProperty("operator").GetString() == "retained", "Unrelated client attribute was erased.");
        Require(clientUpdate.GetProperty("implicitFlowEnabled").GetBoolean() == false,
            "An explicit implicit-flow restriction was omitted from the provider update.");
        await configuration.UpdateScopeAsync("scope-owned", new() { Attributes = new Dictionary<string, string> { ["release"] = "new" } });
        Require(Body(handler, "/client-scopes/scope-owned").GetProperty("attributes").GetProperty("operator").GetString() == "retained", "Unrelated scope attribute was erased.");
        await configuration.UpdateProtocolMapperAsync("scope-owned", "mapper-owned", new() { Config = new Dictionary<string, string> { ["claim.name"] = "owned" } });
        Require(Body(handler, "/protocol-mappers/models/mapper-owned").GetProperty("config").GetProperty("operator").GetString() == "retained", "Unrelated mapper setting was erased.");
        Require((await configuration.GetRolesAsync()).Single().Attributes?["owner"].Single() == "release", "Role ownership metadata was lost.");
        await configuration.UpdateRoleAsync("owned-role", new() { Attributes = new Dictionary<string, string[]> { ["release"] = ["new"] } });
        Require(Body(handler, "/roles/owned-role").GetProperty("attributes").GetProperty("operator")[0].GetString() == "retained", "Unrelated role attribute was erased.");
        await ValidateInitializationAsync(provider.GetRequiredService<IKeycloakRealmAdministration>(), handler);
        Require(handler.Requests.All(request => request.Method != "DELETE"), "Configuration implicitly deleted provider resources.");
        Require(handler.Requests.All(request => request.Body?.Contains("provider-secret-must-not-be-replayed", StringComparison.Ordinal) != true), "Read provider credential leaked into mutation.");
    }

    /// <summary>Checks sanitized initial realm import and rejects provider identifiers before any request.</summary>
    private static async Task ValidateInitializationAsync(IKeycloakRealmAdministration realms, RecordingHandler handler)
    {
        await realms.CreateAsync(new KeycloakRealmInitialization { Settings = new() { Enabled = true },
            Clients = [new() { ClientId = "owned-client", StandardFlowEnabled = true }],
            ClientScopes = [new() { Name = "owned-scope", Protocol = "openid-connect" }],
            Roles = [new() { Name = "owned-role" }] });
        using var document = JsonDocument.Parse(handler.Requests.Last(request => request.Method == "POST" && request.PathAndQuery.EndsWith("/admin/realms", StringComparison.Ordinal)).Body!);
        Require(document.RootElement.GetProperty("clients")[0].GetProperty("clientId").GetString() == "owned-client", "Sanitized initial clients were not imported.");
        Require(!document.RootElement.TryGetProperty("users", out _), "Initial configuration imported user identities.");
        var before = handler.Requests.Count;
        try
        {
            await realms.CreateAsync(new KeycloakRealmInitialization { Settings = new(), Clients = [new() { Id = "foreign-provider-id", ClientId = "owned" }] });
            throw new InvalidOperationException("Initial provider identifiers were accepted.");
        }
        catch (ArgumentException) { }
        Require(handler.Requests.Count == before, "Invalid initialization contacted the provider.");
    }

    /// <summary>Reads the last independent mutation snapshot for a provider endpoint.</summary>
    private static JsonElement Body(RecordingHandler handler, string suffix)
    {
        using var document = JsonDocument.Parse(handler.Requests.Last(request => request.Method == "PUT" && request.PathAndQuery.EndsWith(suffix, StringComparison.Ordinal)).Body!);
        return document.RootElement.Clone();
    }

    /// <summary>Fails the probe on a violated partial-update guarantee.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
