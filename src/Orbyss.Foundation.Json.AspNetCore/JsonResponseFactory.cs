using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Creates only admitted typed successful results.</summary>
internal sealed class JsonResponseFactory<T>(JsonProfileCatalog catalog, JsonContractMetadata<T> metadata) : IJsonResponseFactory<T>
{
    /// <inheritdoc />
    public IResult Create(T value, int statusCode = StatusCodes.Status200OK)
    {
        if (statusCode is < 200 or > 299) throw new ArgumentOutOfRangeException(nameof(statusCode));
        return new JsonProfileResult<T>(value, statusCode, catalog, metadata);
    }
}
