using System.Text.Json.Serialization;

namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Declares partial Keycloak configuration; null fields are omitted from writes.</summary>
public sealed record KeycloakProtocolMapperConfiguration
{
    /// <summary>Gets the stable provider identifier returned by reads.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }
    /// <summary>Gets the mapper name.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }
    /// <summary>Gets the selected protocol.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Protocol { get; init; }
    /// <summary>Gets the provider mapper implementation identity.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProtocolMapper { get; init; }
    /// <summary>Gets whether consent is required.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ConsentRequired { get; init; }
    /// <summary>Gets declared mapper settings; omitted existing settings are preserved.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Config { get; init; }
}
