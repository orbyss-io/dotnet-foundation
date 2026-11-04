namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Selects the administration capabilities composed for a standalone caller.</summary>
[Flags]
public enum KeycloakAdminFeatures
{
    /// <summary>Selects no administration capability.</summary>
    None = 0,
    /// <summary>Selects user administration.</summary>
    Users = 1,
    /// <summary>Selects OAuth client administration.</summary>
    Applications = 2,
    /// <summary>Selects scope and claim-mapping administration.</summary>
    Scopes = 4,
    /// <summary>Selects role and group administration.</summary>
    Access = 8,
    /// <summary>Selects credential and enrollment administration.</summary>
    Enrollment = 16,
    /// <summary>Selects session administration.</summary>
    Sessions = 32,
    /// <summary>Selects Keycloak authentication-flow administration.</summary>
    AuthenticationFlows = 64,
    /// <summary>Selects Keycloak identity-provider administration.</summary>
    IdentityProviders = 128,
    /// <summary>Selects Keycloak organization administration.</summary>
    Organizations = 256,
    /// <summary>Selects Keycloak event, key metadata and login-failure operations.</summary>
    RealmOperations = 512,
    /// <summary>Selects configured-realm initialization and settings administration.</summary>
    Realms = 1024,
    /// <summary>Selects typed partial client, scope, mapper and role configuration.</summary>
    Configuration = 2048
}
