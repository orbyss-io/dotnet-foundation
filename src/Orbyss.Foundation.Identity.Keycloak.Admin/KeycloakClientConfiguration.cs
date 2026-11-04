using System.Text.Json.Serialization;

namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Declares partial Keycloak configuration; null fields are omitted from writes.</summary>
public sealed record KeycloakClientConfiguration
{
    /// <summary>Gets the stable provider identifier returned by reads.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }
    /// <summary>Gets the declared client identifier.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientId { get; init; }
    /// <summary>Gets the client display name.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }
    /// <summary>Gets the explicit enabled-state change.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; init; }
    /// <summary>Gets whether the client is public.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PublicClient { get; init; }
    /// <summary>Gets whether the client only accepts bearer tokens.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? BearerOnly { get; init; }
    /// <summary>Gets whether machine client credentials are enabled.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ServiceAccountsEnabled { get; init; }
    /// <summary>Gets whether authorization-code login is enabled.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? StandardFlowEnabled { get; init; }
    /// <summary>Gets whether the implicit browser flow is enabled.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ImplicitFlowEnabled { get; init; }
    /// <summary>Gets whether resource-owner password login is enabled.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DirectAccessGrantsEnabled { get; init; }
    /// <summary>Gets whether browser frontchannel logout is enabled.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? FrontchannelLogout { get; init; }
    /// <summary>Gets the explicitly selected client protocol.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Protocol { get; init; }
    /// <summary>Gets the explicitly declared login callback addresses.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? RedirectUris { get; init; }
    /// <summary>Gets the explicitly declared browser origins.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? WebOrigins { get; init; }
    /// <summary>Gets initial default scope names when importing a fresh realm.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? DefaultClientScopes { get; init; }
    /// <summary>Gets initial optional scope names when importing a fresh realm.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? OptionalClientScopes { get; init; }
    /// <summary>Gets declared attribute changes; omitted existing attributes are preserved.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Attributes { get; init; }
    /// <summary>Gets declared authentication bindings; omitted existing bindings are preserved.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? AuthenticationFlowBindingOverrides { get; init; }
}
