using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Contributes server JSON contract failures without relabeling them as client input.</summary>
internal sealed class FoundationJsonResponseExceptionHandler : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not JsonResponseContractException failure || httpContext.Response.HasStarted || httpContext.RequestAborted.IsCancellationRequested)
            return false;
        cancellationToken.ThrowIfCancellationRequested();
        httpContext.Response.Clear();
        await FoundationProblemResults.Problem(new ProblemDefinition(500, failure.Code)).ExecuteAsync(httpContext).ConfigureAwait(false);
        httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Orbyss.Foundation.Json.Response")
            .LogError("Server JSON contract failed with code {FailureCode} and correlation {CorrelationId}.", failure.Code, httpContext.TraceIdentifier);
        return true;
    }
}
