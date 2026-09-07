using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.Authentication;

/// <summary>Writes the default RFC 9457-compatible authentication error representation.</summary>
internal sealed class DefaultAuthenticationErrorWriter : IAuthenticationErrorWriter
{
    /// <inheritdoc />
    public async Task WriteAsync(HttpContext context, int statusCode, string code)
    {
        context.Response.StatusCode = statusCode;
        await Results.Problem(
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = context.TraceIdentifier
            }).ExecuteAsync(context).ConfigureAwait(false);
    }
}
