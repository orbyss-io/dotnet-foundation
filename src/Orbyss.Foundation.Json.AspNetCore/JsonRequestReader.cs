using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Adapts actual request streams to the shell-local immutable profile.</summary>
internal sealed class JsonRequestReader<T>(JsonProfileCatalog catalog, JsonContractMetadata<T> metadata) : IJsonRequestReader<T>
{
    /// <inheritdoc />
    public async Task<T> ReadAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        var selected = catalog.Get(metadata.Profile);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, cancellationToken);
        var admission = context.Features.Get<JsonAdmissionState>();
        admission?.BeginRead(typeof(T), metadata.Profile);
        try
        {
            var result = await selected.ReadAsync<T>(context.Request.Body, linked.Token).ConfigureAwait(false);
            if (admission is not null) admission.RequestAdmitted = true;
            return result;
        }
        finally { if (admission is not null) admission.Reading = false; }
    }
}
