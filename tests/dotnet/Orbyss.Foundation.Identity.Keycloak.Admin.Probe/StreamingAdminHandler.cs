using System.Net;
using System.Text;

/// <summary>Emulates an unbounded token body or an unbounded resource-creation body.</summary>
internal sealed class StreamingAdminHandler(bool streamCreation) : HttpMessageHandler
{
    /// <summary>Gets the body whose streaming behavior the probe observes.</summary>
    internal StreamingOversizeContent Content { get; } = new();
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = request.RequestUri!.AbsolutePath.EndsWith("/protocol/openid-connect/token", StringComparison.Ordinal);
        if (streamCreation && token)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"access_token\":\"fictional-token\",\"expires_in\":120}", Encoding.UTF8, "application/json") });
        var response = new HttpResponseMessage(streamCreation ? HttpStatusCode.Created : HttpStatusCode.OK) { Content = Content };
        response.Headers.Location = new Uri("https://identity.example/resources/created");
        return Task.FromResult(response);
    }
}
