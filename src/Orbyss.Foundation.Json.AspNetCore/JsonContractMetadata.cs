namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Owns one typed contract registration and its activation-time metadata check.</summary>
public sealed class JsonContractMetadata<T>(JsonProfileKey profile, JsonContractDirection direction, JsonProfileRequirement requirement)
    : IJsonContractMetadata
{
    /// <inheritdoc />
    public Type ContractType => typeof(T);
    /// <inheritdoc />
    public JsonProfileKey Profile => profile;
    /// <inheritdoc />
    public JsonContractDirection Direction => direction;
    /// <inheritdoc />
    public JsonProfileRequirement Requirement => requirement;
    /// <inheritdoc />
    public void Validate(JsonProfileCatalog catalog)
    {
        var selected = catalog.Get(Profile);
        Requirement.Validate(selected);
        _ = selected.TypeInfo<T>();
    }
}
