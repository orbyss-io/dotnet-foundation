using System.Text.Json.Serialization;

namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Declares partial Keycloak configuration; null fields are omitted from writes.</summary>
public sealed record KeycloakScopeConfiguration
{
    /// <summary>Gets the stable provider identifier returned by reads.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }
    /// <summary>Gets the scope name.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }
    /// <summary>Gets the scope description.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
    /// <summary>Gets the selected protocol.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Protocol { get; init; }
    /// <summary>Gets declared attribute changes; omitted existing attributes are preserved.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Attributes { get; init; }
    /// <summary>Gets initial mapper declarations for fresh resource creation.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<KeycloakProtocolMapperConfiguration>? ProtocolMappers { get; init; }
}
