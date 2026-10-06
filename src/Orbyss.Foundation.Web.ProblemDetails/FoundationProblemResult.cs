using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Admits the envelope before ASP.NET's negotiated writer or its JSON fallback executes.</summary>
internal sealed class FoundationProblemResult(Microsoft.AspNetCore.Mvc.ProblemDetails problem, Exception? exception = null)
    : IFoundationProblemResult
{
    /// <inheritdoc />
    public int? StatusCode => problem.Status;
    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext context)
    {
        if (context.Response.HasStarted)
            throw new InvalidOperationException("Cannot replace an already committed response with a problem.");
        context.RequestAborted.ThrowIfCancellationRequested();
        context.RequestServices.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value.CustomizeProblemDetails?.Invoke(
            new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem, Exception = exception });
        await Results.Problem(problem).ExecuteAsync(context).ConfigureAwait(false);
    }
}
