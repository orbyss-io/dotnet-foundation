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
        // Keycloak merges supplied map entries. Omitting an existing key or sending an empty
        // map preserves it; only an explicit null value removes that named entry.
        var changes = new Dictionary<string, IReadOnlyDictionary<string, string?>>();
        AddRemovals(changes, "attributes", current.Attributes, attributes);
        AddRemovals(changes, "authenticationFlowBindingOverrides", current.AuthenticationFlowBindingOverrides, bindings);
        if (changes.Count == 0) return;
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

    /// <summary>Emits only explicitly selected current keys with null tombstones; never replays unrelated entries.</summary>
    private static void AddRemovals(Dictionary<string, IReadOnlyDictionary<string, string?>> changes, string property,
        IReadOnlyDictionary<string, string>? current, HashSet<string> keys)
    {
        if (current is null) return;
        var removals = keys.Where(current.ContainsKey).ToDictionary(key => key, _ => (string?)null, StringComparer.Ordinal);
        if (removals.Count > 0) changes.Add(property, removals);
    }
}
