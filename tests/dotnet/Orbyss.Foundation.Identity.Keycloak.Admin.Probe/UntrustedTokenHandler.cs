using System.Net;
using System.Text;

/// <summary>Returns fictional malformed provider responses without contacting a network service.</summary>
internal sealed class UntrustedTokenHandler(string payload) : HttpMessageHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload, Encoding.UTF8, "application/json") });
}
