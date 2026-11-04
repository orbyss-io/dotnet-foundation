namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Initializes and updates only the configured Keycloak realm's declared settings.</summary>
public interface IKeycloakRealmAdministration
{
    /// <summary>Gets supported realm settings, or null when the configured realm is absent.</summary>
    ValueTask<KeycloakRealmSettings?> GetAsync(CancellationToken cancellationToken = default);
    /// <summary>Creates the configured realm without users, client secrets or other identity data.</summary>
    ValueTask CreateAsync(KeycloakRealmSettings settings, CancellationToken cancellationToken = default);
    /// <summary>Creates a missing realm from sanitized settings, clients, scopes and roles without users or credentials.</summary>
    ValueTask CreateAsync(KeycloakRealmInitialization initialization, CancellationToken cancellationToken = default);
    /// <summary>Updates supplied realm settings without importing or replacing realm contents.</summary>
    ValueTask UpdateSettingsAsync(KeycloakRealmSettings settings, CancellationToken cancellationToken = default);
}
