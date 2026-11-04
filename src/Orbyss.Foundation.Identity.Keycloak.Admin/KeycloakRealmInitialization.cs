namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Defines sanitized initial configuration for a missing realm without users or credentials.</summary>
public sealed record KeycloakRealmInitialization
{
    /// <summary>Gets the realm's initial noncredential settings.</summary>
    public required KeycloakRealmSettings Settings { get; init; }
    /// <summary>Gets initial client declarations without client-secret values.</summary>
    public IReadOnlyList<KeycloakClientConfiguration> Clients { get; init; } = [];
    /// <summary>Gets initial reusable scopes and protocol mappers.</summary>
    public IReadOnlyList<KeycloakScopeConfiguration> ClientScopes { get; init; } = [];
    /// <summary>Gets initial realm roles without user assignments.</summary>
    public IReadOnlyList<KeycloakRoleConfiguration> Roles { get; init; } = [];
}
