using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Exercises provider merge semantics and scope category changes through the public native clients.</summary>
internal static class RealConfigurationRemovalProbe
{
    /// <summary>Verifies exact selected removals, retries, omitted fields and both scope moves against real Keycloak.</summary>
    internal static async Task RunAsync(IServiceProvider services, string clientId)
    {
        var configuration = services.GetRequiredService<IKeycloakConfigurationAdministration>();
        var removal = services.GetRequiredService<IKeycloakConfigurationRemoval>();
        var flows = await services.GetRequiredService<IKeycloakAuthenticationFlowAdministration>().GetFlowsAsync();
        var browser = flows.Single(flow => flow.GetProperty("alias").GetString() == "browser").GetProperty("id").GetString()!;
        var directGrant = flows.Single(flow => flow.GetProperty("alias").GetString() == "direct grant").GetProperty("id").GetString()!;
        await configuration.UpdateClientAsync(clientId, new()
        {
            Attributes = new Dictionary<string, string> { ["owned.remove"] = "retire", ["operator.keep"] = "retained", ["mfa.keep"] = "required" },
            AuthenticationFlowBindingOverrides = new Dictionary<string, string> { ["browser"] = browser, ["direct_grant"] = directGrant }
        });
        var before = await RequiredClientAsync(configuration, clientId);
        await configuration.UpdateClientAsync(clientId, new() { Attributes = new Dictionary<string, string>() });
        var afterEmptyUpdate = await RequiredClientAsync(configuration, clientId);
        Require(afterEmptyUpdate.Attributes?["owned.remove"] == "retire" && afterEmptyUpdate.AuthenticationFlowBindingOverrides?["browser"] == browser,
            "An empty partial-update map removed existing keys.");
        await removal.RemoveClientConfigurationKeysAsync(clientId, ["owned.remove", "already.absent"], ["browser", "already.absent"]);
        var after = await RequiredClientAsync(configuration, clientId);
        Require(after.Attributes?.ContainsKey("owned.remove") != true && after.AuthenticationFlowBindingOverrides?.ContainsKey("browser") != true,
            "Real Keycloak retained explicitly removed map keys.");
        Require(after.Attributes?["operator.keep"] == "retained" && after.Attributes?["mfa.keep"] == "required"
            && after.AuthenticationFlowBindingOverrides?["direct_grant"] == directGrant, "Removal changed unselected attributes or authentication bindings.");
        Require(Scalars(before) == Scalars(after), "Removal changed client scalar fields or login policy.");
        await removal.RemoveClientConfigurationKeysAsync(clientId, ["owned.remove"], ["browser"]);
        Require(JsonSerializer.Serialize(after) == JsonSerializer.Serialize(await RequiredClientAsync(configuration, clientId)), "Repeated real removal did not converge.");
        await removal.RemoveClientConfigurationKeysAsync(clientId, [], ["direct_grant"]);
        Require((await RequiredClientAsync(configuration, clientId)).AuthenticationFlowBindingOverrides?.Count is null or 0, "Final real flow binding survived removal.");
        await removal.RemoveClientConfigurationKeysAsync(clientId, ["operator.keep", "mfa.keep"], []);
        var final = await RequiredClientAsync(configuration, clientId);
        Require(final.Attributes?.ContainsKey("operator.keep") != true && final.Attributes?.ContainsKey("mfa.keep") != true
            && Scalars(before) == Scalars(final), "Final selected attributes survived or scalars changed.");

        // The provider stores a single client/scope binding with one category. Both DELETE
        // routes remove that binding, even when its current category differs from the route.
        var moving = await configuration.CreateScopeAsync(new() { Name = "removal-moving-scope", Protocol = "openid-connect" });
        var retained = await configuration.CreateScopeAsync(new() { Name = "removal-retained-scope", Protocol = "openid-connect" });
        try
        {
            await configuration.AddClientScopeAsync(clientId, retained, optional: true);
            await configuration.AddClientScopeAsync(clientId, moving, optional: false);
            await removal.RemoveClientScopeAsync(clientId, moving, optional: true);
            await RequireScopeAsync(configuration, clientId, moving, expectedOptional: null);
            await configuration.AddClientScopeAsync(clientId, moving, optional: false);
            await removal.RemoveClientScopeAsync(clientId, moving, optional: false);
            await configuration.AddClientScopeAsync(clientId, moving, optional: true);
            await RequireScopeAsync(configuration, clientId, moving, expectedOptional: true);
            await removal.RemoveClientScopeAsync(clientId, moving, optional: false);
            await RequireScopeAsync(configuration, clientId, moving, expectedOptional: null);
            await configuration.AddClientScopeAsync(clientId, moving, optional: true);
            await removal.RemoveClientScopeAsync(clientId, moving, optional: true);
            await configuration.AddClientScopeAsync(clientId, moving, optional: false);
            await RequireScopeAsync(configuration, clientId, moving, expectedOptional: false);
            await RequireScopeAsync(configuration, clientId, retained, expectedOptional: true);
        }
        finally
        {
            var scopes = services.GetRequiredService<Orbyss.Foundation.Identity.Admin.IIdentityScopeAdministration>();
            await scopes.DeleteAsync(moving);
            await scopes.DeleteAsync(retained);
        }
        Console.WriteLine("Real Keycloak preserved scalar/login policy and unselected maps; explicit first/final-key removals and retry converged; both scope category moves and cross-category DELETE semantics passed.");
    }

    /// <summary>Reads the created fixture client without treating absence as an empty configuration.</summary>
    private static async Task<KeycloakClientConfiguration> RequiredClientAsync(IKeycloakConfigurationAdministration configuration, string clientId) =>
        await configuration.GetClientAsync(clientId) ?? throw new InvalidOperationException("Disposable client disappeared.");

    /// <summary>Compares every typed scalar and array policy while excluding the two deliberately mutated maps.</summary>
    private static string Scalars(KeycloakClientConfiguration client) => JsonSerializer.Serialize(client with { Attributes = null, AuthenticationFlowBindingOverrides = null });

    /// <summary>Checks actual membership in both categories instead of trusting a successful mutation response.</summary>
    private static async Task RequireScopeAsync(IKeycloakConfigurationAdministration configuration, string clientId, string scopeId, bool? expectedOptional)
    {
        var defaults = await configuration.GetClientScopesAsync(clientId, optional: false);
        var optional = await configuration.GetClientScopesAsync(clientId, optional: true);
        Require(defaults.Any(scope => scope.Id == scopeId) == (expectedOptional == false)
            && optional.Any(scope => scope.Id == scopeId) == (expectedOptional == true), "Real scope membership differs from the expected category.");
    }

    /// <summary>Fails real-provider qualification on a violated native contract.</summary>
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
