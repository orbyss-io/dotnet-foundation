using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Orbyss.Foundation.Web.ProblemDetails.Core;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Handles precisely ASP.NET request admission errors without capturing request-scoped services.</summary>
internal sealed class FoundationBadHttpRequestExceptionHandler(ILogger<FoundationBadHttpRequestExceptionHandler> logger)
    : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException request || context.Response.HasStarted
            || cancellationToken.IsCancellationRequested || context.RequestAborted.IsCancellationRequested)
            return false;
        var status = request.StatusCode is >= 400 and <= 499 ? request.StatusCode : 400;
        logger.LogWarning("Foundation request admission failed with status {StatusCode} and correlation {CorrelationId}.",
            status, context.TraceIdentifier);
        await FoundationProblemResults.Problem(new ProblemDefinition(status, ProblemCodes.InvalidRequest))
            .ExecuteAsync(context).ConfigureAwait(false);
        return true;
    }
}
