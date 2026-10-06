using Microsoft.AspNetCore.Http;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;

namespace Orbyss.Foundation.Authentication;

/// <summary>Writes the default RFC 9457-compatible authentication error representation.</summary>
internal sealed class DefaultAuthenticationErrorWriter : IAuthenticationErrorWriter
{
    /// <inheritdoc />
    public async Task WriteAsync(HttpContext context, int statusCode, string code)
    {
        await FoundationProblemResults.Problem(new ProblemDefinition(statusCode, code))
            .ExecuteAsync(context).ConfigureAwait(false);
    }
}
