namespace Orbyss.Foundation.Authentication;

/// <summary>Defines settings shared by the Orbyss Foundation authentication profile features.</summary>
public sealed class FoundationWebOptions
{
    /// <summary>Gets the shell-configuration section containing secure web settings.</summary>
    public const string SectionName = "Foundation:Web";

    /// <summary>Gets or sets the OIDC issuer authority.</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional server-reachable authority used only to retrieve OIDC metadata.
    /// The public <see cref="Authority"/> remains the issuer and browser-facing authority.
    /// </summary>
    public string BackchannelAuthority { get; set; } = string.Empty;

    /// <summary>Gets or sets the registered OAuth client identifier.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Gets or sets the confidential BFF client secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Gets or sets the protected API audience.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Gets or sets the scopes requested during interactive authentication. Explicit shell configuration replaces these fallback defaults.</summary>
    public string[] Scopes { get; set; } = ["openid", "profile", "offline_access", "orbyss-foundation-api"];

    /// <summary>Gets or sets the provider claim containing normalized application roles.</summary>
    public string RoleClaim { get; set; } = "roles";

    /// <summary>Gets or sets the canonical claim containing application permission identities.</summary>
    public string PermissionClaim { get; set; } = "permissions";

    /// <summary>Gets provider-role to canonical application-permission mappings.</summary>
    public Dictionary<string, string[]> RolePermissions { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets provider-scope to canonical application-permission mappings.</summary>
    public Dictionary<string, string[]> ScopePermissions { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the exact origins permitted by the direct SPA profile.</summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>Gets or sets the local OIDC authorization callback path.</summary>
    public string CallbackPath { get; set; } = "/signin-oidc";

    /// <summary>Gets or sets the local callback path used after provider logout.</summary>
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    /// <summary>Gets or sets the path receiving provider-initiated logout notifications.</summary>
    public string RemoteSignOutPath { get; set; } = "/signout-oidc";

    /// <summary>Gets or sets the interactive access-denied path.</summary>
    public string AccessDeniedPath { get; set; } = "/bff/access-denied";

    /// <summary>Gets or sets the host-only encrypted session cookie name.</summary>
    public string CookieName { get; set; } = "__Host-orbyss-foundation-session";

    /// <summary>Gets or sets the discovery and JWKS readiness budget in seconds.</summary>
    public int DiscoveryTimeoutSeconds { get; set; } = 3;

    /// <summary>Gets or sets the maximum duration of an interactive remote authentication operation.</summary>
    public int RemoteAuthenticationTimeoutSeconds { get; set; } = 10;

    /// <summary>Gets or sets the sliding server session idle lifetime in minutes.</summary>
    public int SessionIdleMinutes { get; set; } = 30;

    /// <summary>Gets or sets the non-extendable server session lifetime in minutes.</summary>
    public int SessionAbsoluteMinutes { get; set; } = 480;

    /// <summary>Gets or sets whether loopback/container HTTP is accepted during local development.</summary>
    public bool AllowHttpForLocalDevelopment { get; set; }
}
