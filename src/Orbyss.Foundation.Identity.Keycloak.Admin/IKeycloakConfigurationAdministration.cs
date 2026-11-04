namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Reads and updates explicitly declared Keycloak configuration without credential or identity imports.</summary>
public interface IKeycloakConfigurationAdministration
{
    /// <summary>Gets a bounded page of clients, optionally matching an exact client identifier.</summary>
    ValueTask<IReadOnlyList<KeycloakClientConfiguration>> GetClientsAsync(string? clientId = null, int first = 0, int maximum = 100, CancellationToken cancellationToken = default);
    /// <summary>Gets one client, or null when it is absent.</summary>
    ValueTask<KeycloakClientConfiguration?> GetClientAsync(string clientId, CancellationToken cancellationToken = default);
    /// <summary>Creates a client without setting or resetting its credential.</summary>
    ValueTask<string> CreateClientAsync(KeycloakClientConfiguration configuration, CancellationToken cancellationToken = default);
    /// <summary>Updates supplied client fields, preserving omitted fields and unrelated map entries.</summary>
    ValueTask UpdateClientAsync(string clientId, KeycloakClientConfiguration configuration, CancellationToken cancellationToken = default);
    /// <summary>Gets reusable scopes with their declared provider metadata.</summary>
    ValueTask<IReadOnlyList<KeycloakScopeConfiguration>> GetScopesAsync(CancellationToken cancellationToken = default);
    /// <summary>Creates a reusable scope.</summary>
    ValueTask<string> CreateScopeAsync(KeycloakScopeConfiguration configuration, CancellationToken cancellationToken = default);
    /// <summary>Updates scope settings; mapper changes use their separate operations.</summary>
    ValueTask UpdateScopeAsync(string scopeId, KeycloakScopeConfiguration configuration, CancellationToken cancellationToken = default);
    /// <summary>Gets a scope's protocol mappers.</summary>
    ValueTask<IReadOnlyList<KeycloakProtocolMapperConfiguration>> GetProtocolMappersAsync(string scopeId, CancellationToken cancellationToken = default);
    /// <summary>Creates one scope protocol mapper.</summary>
    ValueTask<string> CreateProtocolMapperAsync(string scopeId, KeycloakProtocolMapperConfiguration configuration, CancellationToken cancellationToken = default);
    /// <summary>Updates supplied mapper fields, preserving unrelated configuration keys.</summary>
    ValueTask UpdateProtocolMapperAsync(string scopeId, string mapperId, KeycloakProtocolMapperConfiguration configuration, CancellationToken cancellationToken = default);
    /// <summary>Gets a client's default or optional scope assignments.</summary>
    ValueTask<IReadOnlyList<KeycloakScopeConfiguration>> GetClientScopesAsync(string clientId, bool optional, CancellationToken cancellationToken = default);
    /// <summary>Adds a default or optional scope assignment without replacing existing assignments.</summary>
    ValueTask AddClientScopeAsync(string clientId, string scopeId, bool optional, CancellationToken cancellationToken = default);
    /// <summary>Gets realm roles including ownership metadata.</summary>
    ValueTask<IReadOnlyList<KeycloakRoleConfiguration>> GetRolesAsync(CancellationToken cancellationToken = default);
    /// <summary>Creates one realm role without assigning it to users.</summary>
    ValueTask CreateRoleAsync(KeycloakRoleConfiguration configuration, CancellationToken cancellationToken = default);
    /// <summary>Updates supplied role fields, preserving unrelated attributes and assignments.</summary>
    ValueTask UpdateRoleAsync(string roleName, KeycloakRoleConfiguration configuration, CancellationToken cancellationToken = default);
}
