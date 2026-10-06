using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace HostComposition.Fixture;

/// <summary>A custom native writer with no Foundation dependency or representation policy.</summary>
public sealed class CustomProblemWriter(IOptions<ProblemDetailsOptions> options) : IProblemDetailsWriter
{
    /// <inheritdoc />
    public bool CanWrite(ProblemDetailsContext context) => true;

    /// <inheritdoc />
    public async ValueTask WriteAsync(ProblemDetailsContext context)
    {
        context.HttpContext.RequestAborted.ThrowIfCancellationRequested();
        if (context.HttpContext.Response.HasStarted)
            throw new InvalidOperationException("Custom writer cannot replace committed output.");
        var problem = context.ProblemDetails;
        problem.Type = "urn:host-composition:custom";
        problem.Title = "Custom problem";
        problem.Extensions["customOwner"] = context.HttpContext.RequestServices.GetRequiredService<CustomContribution>().Shell;
        options.Value.CustomizeProblemDetails?.Invoke(context);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = problem.Type, title = problem.Title, status = problem.Status,
            extensions = problem.Extensions
        });
        var response = context.HttpContext.Response;
        response.ContentType = "application/custom-problem+json";
        response.ContentLength = bytes.Length;
        response.Headers.CacheControl = "no-store";
        await response.Body.WriteAsync(bytes, context.HttpContext.RequestAborted);
    }
}
