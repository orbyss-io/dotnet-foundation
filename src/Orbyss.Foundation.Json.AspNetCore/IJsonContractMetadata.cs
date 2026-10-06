namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Describes configured typed contracts without resolving storage or application initializers.</summary>
public interface IJsonContractMetadata
{
    /// <summary>Gets the public wire type.</summary>
    Type ContractType { get; }
    /// <summary>Gets explicit configured selection.</summary>
    JsonProfileKey Profile { get; }
    /// <summary>Gets request/response ownership.</summary>
    JsonContractDirection Direction { get; }
    /// <summary>Gets the supported interval and preset.</summary>
    JsonProfileRequirement Requirement { get; }
    /// <summary>Validates actual configured capacity and required serializer metadata.</summary>
    void Validate(JsonProfileCatalog catalog);
}
