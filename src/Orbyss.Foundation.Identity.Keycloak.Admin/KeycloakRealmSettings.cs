using System.Text.Json.Serialization;

namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Declares supported non-credential realm settings; null properties are left unchanged.</summary>
public sealed record KeycloakRealmSettings
{
    /// <summary>Gets the realm's enabled state when explicitly selected.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; init; }
    /// <summary>Gets the human-readable realm name.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; init; }
    /// <summary>Gets the selected login theme.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LoginTheme { get; init; }
    /// <summary>Gets the selected email theme.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EmailTheme { get; init; }
    /// <summary>Gets the selected account theme.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccountTheme { get; init; }
    /// <summary>Gets whether realm localization is enabled.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? InternationalizationEnabled { get; init; }
    /// <summary>Gets the declared supported locales.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? SupportedLocales { get; init; }
    /// <summary>Gets the selected default locale.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefaultLocale { get; init; }
    /// <summary>Gets the Keycloak TLS requirement policy.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SslRequired { get; init; }
    /// <summary>Gets whether public user registration is enabled.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RegistrationAllowed { get; init; }
    /// <summary>Gets whether user password recovery is enabled.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ResetPasswordAllowed { get; init; }
    /// <summary>Gets whether remembered login is enabled.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RememberMe { get; init; }
    /// <summary>Gets whether email addresses can be used for login.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LoginWithEmailAllowed { get; init; }
    /// <summary>Gets whether duplicate email addresses are allowed.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DuplicateEmailsAllowed { get; init; }
    /// <summary>Gets whether brute-force protection is enabled.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? BruteForceProtected { get; init; }
    /// <summary>Gets whether failed login can permanently lock an account.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PermanentLockout { get; init; }
    /// <summary>Gets the failed-login threshold.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FailureFactor { get; init; }
    /// <summary>Gets the fast repeated-login interval in milliseconds.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? QuickLoginCheckMilliSeconds { get; init; }
    /// <summary>Gets the minimum lockout for fast repeated login in seconds.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MinimumQuickLoginWaitSeconds { get; init; }
    /// <summary>Gets the incremental lockout duration in seconds.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? WaitIncrementSeconds { get; init; }
    /// <summary>Gets the maximum lockout duration in seconds.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxFailureWaitSeconds { get; init; }
    /// <summary>Gets the explicitly selected realm browser flow.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BrowserFlow { get; init; }
    /// <summary>Gets the SSO session idle timeout in seconds.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SsoSessionIdleTimeout { get; init; }
    /// <summary>Gets the SSO session maximum lifetime in seconds.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SsoSessionMaxLifespan { get; init; }
    /// <summary>Gets the client session idle timeout in seconds.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ClientSessionIdleTimeout { get; init; }
    /// <summary>Gets the client session maximum lifetime in seconds.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ClientSessionMaxLifespan { get; init; }
    /// <summary>Gets the declared attribute changes; unrelated existing attributes are preserved.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Attributes { get; init; }
}
