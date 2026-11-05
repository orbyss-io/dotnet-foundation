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
        VerifyPreservationComparison();
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
        await removal.RemoveClientConfigurationKeysAsync("client", ["owned", "already-absent"], ["browser", "already-absent"]);
        Require(!handler.Attributes.ContainsKey("owned") && !handler.Bindings.ContainsKey("browser"), "Explicit keys survived removal.");
        Require(handler.Attributes["operator"] == "keep" && handler.Attributes["mfa"] == "required"
            && handler.Bindings["direct_grant"] == "operator-mfa", "Unselected attributes or MFA bindings were erased.");
        var mutation = handler.Requests.Single(request => request.Method == "PUT");
        using (var payload = JsonDocument.Parse(mutation.Body!))
        {
            Require(payload.RootElement.EnumerateObject().All(property => property.Name is "attributes" or "authenticationFlowBindingOverrides"),
                "Removal replayed client scalar, identity or credential fields.");
            Require(payload.RootElement.GetProperty("attributes").EnumerateObject().Select(entry => (entry.Name, entry.Value.ValueKind))
                .SequenceEqual(new[] { ("owned", JsonValueKind.Null) })
                && payload.RootElement.GetProperty("authenticationFlowBindingOverrides").EnumerateObject().Select(entry => (entry.Name, entry.Value.ValueKind))
                .SequenceEqual(new[] { ("browser", JsonValueKind.Null) }), "Removal omitted explicit null tombstones or replayed unselected or absent keys.");
        }
        var writes = handler.Requests.Count(request => request.Method != "GET");
        await removal.RemoveClientConfigurationKeysAsync("client", ["owned"], ["browser"]);
        Require(handler.Requests.Count(request => request.Method != "GET") == writes, "Repeated key removal wrote an unchanged map.");
        await removal.RemoveClientConfigurationKeysAsync("client", [], ["direct_grant"]);
        Require(handler.Bindings.Count == 0 && handler.Attributes["mfa"] == "required", "Removing the final binding omitted its tombstone or changed attributes.");
        var finalBindingMutation = handler.Requests.Last(request => request.Method == "PUT");
        Require(finalBindingMutation.Body == "{\"authenticationFlowBindingOverrides\":{\"direct_grant\":null}}", "Final binding removal replayed the other map or omitted its null tombstone.");
        await removal.RemoveClientConfigurationKeysAsync("client", ["operator", "mfa"], []);
        Require(handler.Attributes.Count == 0 && handler.Bindings.Count == 0, "Removing the final attributes did not preserve the empty binding map.");
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

    /// <summary>Rejects substantive preservation changes while admitting only unordered map/scope enumeration.</summary>
    private static void VerifyPreservationComparison()
    {
        var client = new KeycloakClientConfiguration { Enabled = true, RedirectUris = ["https://one.test", "https://two.test"],
            DefaultClientScopes = ["one", "two"], OptionalClientScopes = ["three", "four"],
            Attributes = new Dictionary<string, string> { ["one"] = "1", ["two"] = "2" },
            AuthenticationFlowBindingOverrides = new Dictionary<string, string> { ["browser"] = "one", ["direct_grant"] = "two" } };
        Require(RealConfigurationRemovalProbe.EquivalentClients(client, client with { DefaultClientScopes = ["two", "one"], OptionalClientScopes = ["four", "three"],
            Attributes = new Dictionary<string, string> { ["two"] = "2", ["one"] = "1" },
            AuthenticationFlowBindingOverrides = new Dictionary<string, string> { ["direct_grant"] = "two", ["browser"] = "one" } }), "Preservation comparison treated unordered enumeration as a mutation.");
        foreach (var changed in new[] { client with { DefaultClientScopes = ["one", "one", "two"] }, client with { OptionalClientScopes = ["three"] },
            client with { RedirectUris = ["https://two.test", "https://one.test"] }, client with { Enabled = false },
            client with { Attributes = new Dictionary<string, string> { ["one"] = "changed", ["two"] = "2" } },
            client with { AuthenticationFlowBindingOverrides = new Dictionary<string, string> { ["browser"] = "changed", ["direct_grant"] = "two" } } })
            Require(!RealConfigurationRemovalProbe.EquivalentClients(client, changed), "Preservation comparison admitted a substantive mutation.");
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
