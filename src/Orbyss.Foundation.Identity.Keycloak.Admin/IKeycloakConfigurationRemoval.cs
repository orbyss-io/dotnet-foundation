namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Removes explicitly selected client configuration without deleting clients, identities, credentials or grants.</summary>
/// <remarks>The caller owns authorization of each object and key. Omission in partial updates never invokes removal.</remarks>
public interface IKeycloakConfigurationRemoval
{
    /// <summary>Removes exactly one default or optional scope assignment from a client.</summary>
    ValueTask RemoveClientScopeAsync(string clientId, string scopeId, bool optional, CancellationToken cancellationToken = default);

    /// <summary>Removes only the named attribute and authentication-binding keys, preserving all other entries.</summary>
    /// <remarks>Each collection contains at most 128 unique nonblank keys of at most 256 characters without controls. Empty collections perform no I/O.
    /// The operation reads fresh maps before writing; callers must serialize conflicting administrative changes and verify the result.</remarks>
    ValueTask RemoveClientConfigurationKeysAsync(string clientId, IReadOnlyCollection<string> attributeKeys,
        IReadOnlyCollection<string> authenticationFlowBindingOverrideKeys, CancellationToken cancellationToken = default);
}
