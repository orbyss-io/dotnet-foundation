using System.Text.Json;
using CShells;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Exercises explicit removals independently of partial-update omission semantics.</summary>
internal static class ConfigurationRemovalProbe
{
    /// <summary>Verifies capability isolation, pre-I/O rejection, precise mutations, preservation and retry convergence.</summary>
    internal static async Task RunAsync()
    {
        using var handler = new ConfigurationRemovalHandler();
        using var client = new HttpClient(handler);
        var services = new ServiceCollection();
        services.AddFoundationKeycloakAdministration(options =>
        {
            options.ServerUrl = "http://localhost:8080";
            options.Realm = "tenant";
            options.AdminRealm = "service";
            options.ClientId = "deployment";
            options.ClientSecret = "fictional-secret";
            options.AllowHttpForLocalDevelopment = true;
        }, KeycloakAdminFeatures.Configuration, "Development");
        services.RemoveAll<IHttpClientFactory>();
        services.AddSingleton<IHttpClientFactory>(new ProbeHttpClientFactory(client));
        await using var provider = services.BuildServiceProvider();
        var removal = provider.GetRequiredService<IKeycloakConfigurationRemoval>();
        await RejectBeforeIoAsync(removal, handler);
        await removal.RemoveClientConfigurationKeysAsync("client", [], []);
        Require(handler.Requests.Count == 0, "Empty key removal contacted the provider.");
        await removal.RemoveClientConfigurationKeysAsync("client", ["owned"], ["browser"]);
        Require(!handler.Attributes.ContainsKey("owned") && !handler.Bindings.ContainsKey("browser"), "Explicit keys survived removal.");
        Require(handler.Attributes["operator"] == "keep" && handler.Attributes["mfa"] == "required"
            && handler.Bindings["direct_grant"] == "operator-mfa", "Unselected attributes or MFA bindings were erased.");
        var mutation = handler.Requests.Single(request => request.Method == "PUT");
        using (var payload = JsonDocument.Parse(mutation.Body!))
            Require(payload.RootElement.EnumerateObject().All(property => property.Name is "attributes" or "authenticationFlowBindingOverrides"),
                "Removal replayed client scalar, identity or credential fields.");
        var writes = handler.Requests.Count(request => request.Method != "GET");
        await removal.RemoveClientConfigurationKeysAsync("client", ["owned"], ["browser"]);
        Require(handler.Requests.Count(request => request.Method != "GET") == writes, "Repeated key removal wrote an unchanged map.");
        await removal.RemoveClientConfigurationKeysAsync("client", [], ["direct_grant"]);
        Require(handler.Bindings.Count == 0 && handler.Attributes["mfa"] == "required", "Removing the final binding omitted the empty map or changed attributes.");
        await removal.RemoveClientScopeAsync("client/escaped", "scope/escaped", true);
        await removal.RemoveClientScopeAsync("client/escaped", "scope/escaped", false);
        var deletes = handler.Requests.Where(request => request.Method == "DELETE").ToArray();
        Require(deletes.Length == 2 && deletes[0].PathAndQuery.EndsWith("/clients/client%2Fescaped/optional-client-scopes/scope%2Fescaped", StringComparison.Ordinal)
            && deletes[1].PathAndQuery.EndsWith("/clients/client%2Fescaped/default-client-scopes/scope%2Fescaped", StringComparison.Ordinal),
            "Scope removal selected the wrong membership or failed to escape resource identifiers.");
        Require(handler.Requests.All(request => request.Body?.Contains("provider-secret-must-not-be-replayed", StringComparison.Ordinal) != true),
            "Removal replayed a provider credential.");
        var selected = new ServiceCollection();
        new FoundationKeycloakConfigurationAdminFeature(new ShellSettings(new ShellId("probe"), [])).ConfigureServices(selected);
        Require(selected.Any(descriptor => descriptor.ServiceType == typeof(IKeycloakConfigurationRemoval)), "Shell capability omitted removal registration.");
        var unselected = new ServiceCollection();
        unselected.AddFoundationKeycloakAdministration(_ => { }, KeycloakAdminFeatures.Users);
        Require(unselected.All(descriptor => descriptor.ServiceType != typeof(IKeycloakConfigurationRemoval)), "Removal leaked outside Configuration capability.");
    }

    /// <summary>Rejects the entire explicit selection before token acquisition or any administrative request.</summary>
    private static async Task RejectBeforeIoAsync(IKeycloakConfigurationRemoval removal, ConfigurationRemovalHandler handler)
    {
        foreach (var keys in new[] { new[] { "" }, new[] { "one", "one" }, new[] { "key\nvalue" }, new[] { new string('x', 257) },
            Enumerable.Range(0, 129).Select(index => "key" + index).ToArray() })
        {
            try
            {
                await removal.RemoveClientConfigurationKeysAsync("client", ["owned"], keys);
                throw new InvalidOperationException("Invalid removal keys were admitted.");
            }
            catch (ArgumentException) { }
        }
        Require(handler.Requests.Count == 0, "Invalid key selection contacted the provider.");
    }

    /// <summary>Fails the public contract probe on a violated preservation or explicit-selection guarantee.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
