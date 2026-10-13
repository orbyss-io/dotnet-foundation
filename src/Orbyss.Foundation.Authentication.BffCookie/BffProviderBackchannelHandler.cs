namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Attaches provenance to actual native HTTP failures while retaining successful protocol responses and metadata retries.</summary>
internal sealed class BffProviderBackchannelHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try { response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false); }
        catch (HttpRequestException failure) { BffProviderFailureProvenance.Mark(failure); throw; }
        catch (OperationCanceledException failure) { BffProviderFailureProvenance.Mark(failure); throw; }
        // Metadata GET availability responses must reach the native retriever, including its bounded retries.
        // Native initial PAR uses POST; recover its availability failure before attempting protocol JSON parsing.
        if (request.Method == HttpMethod.Post && BffProviderFailureProvenance.IsAvailabilityStatus(response.StatusCode))
        {
            var failure = new HttpRequestException("The OIDC provider backchannel is unavailable.", null, response.StatusCode);
            response.Dispose();
            BffProviderFailureProvenance.Mark(failure);
            throw failure;
        }
        return response;
    }
}
