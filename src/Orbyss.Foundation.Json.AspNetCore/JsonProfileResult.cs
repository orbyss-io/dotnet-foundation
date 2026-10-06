using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Admits finite output before writing any success headers or body.</summary>
internal sealed class JsonProfileResult<T>(T value, int statusCode, JsonProfileCatalog catalog, JsonContractMetadata<T> metadata)
    : IResult, IStatusCodeHttpResult, IJsonProfileResult
{
    /// <inheritdoc />
    public int? StatusCode => statusCode;
    /// <inheritdoc />
    public Type ContractType => typeof(T);
    /// <inheritdoc />
    public JsonProfileKey Profile => metadata.Profile;
    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var selected = catalog.Get(metadata.Profile);
        var bytes = selected.Serialize(value, selected.TypeInfo<T>(), httpContext.RequestAborted);
        httpContext.RequestAborted.ThrowIfCancellationRequested();
        var admission = httpContext.Features.Get<JsonAdmissionState>();
        if (admission is not null) admission.Writing = true;
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        httpContext.Response.ContentLength = bytes.Length;
        await httpContext.Response.Body.WriteAsync(bytes, httpContext.RequestAborted).ConfigureAwait(false);
    }
}
