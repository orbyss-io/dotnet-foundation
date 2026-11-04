namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Implements explicit bounded client removals over the shared authenticated transport.</summary>
internal sealed class KeycloakConfigurationRemoval(KeycloakAdminTransport transport,
    IKeycloakConfigurationAdministration configuration) : IKeycloakConfigurationRemoval
{
    /// <inheritdoc />
    public async ValueTask RemoveClientScopeAsync(string clientId, string scopeId, bool optional, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        _ = await transport.SendAsync(HttpMethod.Delete, "clients/" + KeycloakAdminTransport.Escape(clientId)
            + (optional ? "/optional-client-scopes/" : "/default-client-scopes/") + KeycloakAdminTransport.Escape(scopeId),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask RemoveClientConfigurationKeysAsync(string clientId, IReadOnlyCollection<string> attributeKeys,
        IReadOnlyCollection<string> authenticationFlowBindingOverrideKeys, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        var attributes = ValidateKeys(attributeKeys, nameof(attributeKeys));
        var bindings = ValidateKeys(authenticationFlowBindingOverrideKeys, nameof(authenticationFlowBindingOverrideKeys));
        cancellationToken.ThrowIfCancellationRequested();
        if (attributes.Count == 0 && bindings.Count == 0) return;
        var current = await configuration.GetClientAsync(clientId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The configured client is absent.");
        var changes = new KeycloakClientConfiguration
        {
            Attributes = RemoveKeys(current.Attributes, attributes),
            AuthenticationFlowBindingOverrides = RemoveKeys(current.AuthenticationFlowBindingOverrides, bindings)
        };
        if (changes.Attributes is null && changes.AuthenticationFlowBindingOverrides is null) return;
        _ = await transport.SendAsync(HttpMethod.Put, "clients/" + KeycloakAdminTransport.Escape(clientId), changes,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Validates and snapshots both bounded selections before the first provider operation.</summary>
    private static HashSet<string> ValidateKeys(IReadOnlyCollection<string> keys, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(keys, parameterName);
        if (keys.Count > 128) throw new ArgumentException("At most 128 configuration keys may be removed per map.", parameterName);
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 256 || key.Any(char.IsControl) || !result.Add(key))
                throw new ArgumentException("Configuration removal keys must be unique nonblank strings of at most 256 characters without controls.", parameterName);
        }
        return result;
    }

    /// <summary>Returns a replacement map only when an explicitly named current entry is removed.</summary>
    private static IReadOnlyDictionary<string, string>? RemoveKeys(IReadOnlyDictionary<string, string>? current, HashSet<string> keys)
    {
        if (current is null || !current.Keys.Any(keys.Contains)) return null;
        return current.Where(entry => !keys.Contains(entry.Key)).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }
}
