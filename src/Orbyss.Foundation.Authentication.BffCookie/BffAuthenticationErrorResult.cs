using Microsoft.AspNetCore.Http;
using Orbyss.Foundation.Web.ProblemDetails;

namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Keeps the authentication writer adapter in deferred, admitted error execution.</summary>
internal sealed class BffAuthenticationErrorResult(IAuthenticationErrorWriter writer, int statusCode, string code) : IFoundationProblemResult
{
    /// <inheritdoc />
    public int? StatusCode => statusCode;
    /// <inheritdoc />
    public Task ExecuteAsync(HttpContext httpContext) => writer.WriteAsync(httpContext, statusCode, code);
}
