using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Contributes precise client JSON failures to native ordered exception dispatch.</summary>
internal sealed class FoundationJsonRequestExceptionHandler : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not JsonProfileException failure || httpContext.Response.HasStarted || httpContext.RequestAborted.IsCancellationRequested)
            return false;
        cancellationToken.ThrowIfCancellationRequested();
        httpContext.Response.Clear();
        await FoundationProblemResults.Problem(new ProblemDefinition(failure.Code == JsonFailureCodes.SizeExceeded ? 413 : 400, failure.Code))
            .ExecuteAsync(httpContext).ConfigureAwait(false);
        return true;
    }
}
