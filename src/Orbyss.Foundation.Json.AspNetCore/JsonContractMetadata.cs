namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Owns one typed contract registration and its activation-time metadata check.</summary>
public sealed class JsonContractMetadata<T>(
    JsonProfileKey profile,
    JsonContractDirection direction,
    JsonProfileRequirement requirement
)
    : IJsonContractMetadata
{
    /// <summary>Declares a request using the standard configured strict-request profile.</summary>
    public static JsonContractMetadata<T> Request(
        JsonProfileRequirement? requirement = null,
        JsonProfileKey? profile = null
    )
    {
        var selected = profile ?? new JsonProfileKey(JsonProfileKeys.StrictRequest);
        var supported = requirement ?? JsonProfileRequirement.StrictRequest();

        return new JsonContractMetadata<T>(
            selected,
            JsonContractDirection.Request,
            supported
        );
    }

    /// <summary>Declares a response using the standard configured success-response profile.</summary>
    public static JsonContractMetadata<T> Response(
        JsonProfileRequirement? requirement = null,
        JsonProfileKey? profile = null
    )
    {
        var selected = profile ?? new JsonProfileKey(JsonProfileKeys.SuccessResponse);
        var supported = requirement ?? JsonProfileRequirement.TolerantResponse();

        return new JsonContractMetadata<T>(
            selected,
            JsonContractDirection.Response,
            supported
        );
    }

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
