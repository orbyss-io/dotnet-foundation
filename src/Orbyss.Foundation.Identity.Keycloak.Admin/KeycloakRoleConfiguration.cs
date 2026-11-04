using System.Text.Json.Serialization;

namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Declares partial Keycloak configuration; null fields are omitted from writes.</summary>
public sealed record KeycloakRoleConfiguration
{
    /// <summary>Gets the stable provider identifier returned by reads.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }
    /// <summary>Gets the realm role name.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }
    /// <summary>Gets the role description.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
    /// <summary>Gets whether the role is composite.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Composite { get; init; }
    /// <summary>Gets declared role attributes; omitted existing attributes are preserved.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? Attributes { get; init; }
}
