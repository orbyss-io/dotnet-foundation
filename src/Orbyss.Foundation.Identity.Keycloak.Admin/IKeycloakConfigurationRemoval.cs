namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Removes explicitly selected client configuration without deleting clients, identities, credentials or grants.</summary>
/// <remarks>The caller owns authorization of each object and key. Omission in partial updates never invokes removal.</remarks>
public interface IKeycloakConfigurationRemoval
{
    /// <summary>Removes one scope assignment from a client using the selected default or optional endpoint.</summary>
    /// <remarks>Keycloak removes the underlying client/scope binding regardless of its current category.
    /// Complete all removals before adding replacement assignments when moving between default and optional categories.</remarks>
    ValueTask RemoveClientScopeAsync(string clientId, string scopeId, bool optional, CancellationToken cancellationToken = default);

    /// <summary>Removes only the named attribute and authentication-binding keys, preserving all other entries.</summary>
    /// <remarks>Each collection contains at most 128 unique nonblank keys of at most 256 characters without controls. Empty collections perform no I/O.
    /// The operation reads fresh maps and sends explicit null tombstones only for selected present keys.
    /// Callers must serialize conflicting administrative changes and verify the result.</remarks>
    ValueTask RemoveClientConfigurationKeysAsync(string clientId, IReadOnlyCollection<string> attributeKeys,
        IReadOnlyCollection<string> authenticationFlowBindingOverrideKeys, CancellationToken cancellationToken = default);
}
