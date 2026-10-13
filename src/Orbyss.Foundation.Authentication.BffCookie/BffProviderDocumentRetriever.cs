using System.Net;
using Microsoft.IdentityModel.Protocols;

namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Preserves native discovery/JWKS retrieval and marks only its structured availability-status failures.</summary>
internal sealed class BffProviderDocumentRetriever(HttpDocumentRetriever native) : IDocumentRetriever
{
    /// <inheritdoc />
    public async Task<string> GetDocumentAsync(string address, CancellationToken cancel)
    {
        try { return await native.GetDocumentAsync(address, cancel).ConfigureAwait(false); }
        catch (IOException failure) when (failure.Data[HttpDocumentRetriever.StatusCode] is HttpStatusCode status
            && BffProviderFailureProvenance.IsAvailabilityStatus(status))
        {
            BffProviderFailureProvenance.Mark(failure);
            throw;
        }
    }
}
