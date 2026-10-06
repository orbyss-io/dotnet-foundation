using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Composes existing customization and resolves the shared policy from each request scope.</summary>
internal sealed class FoundationProblemDetailsOptionsSetup : IPostConfigureOptions<ProblemDetailsOptions>
{
    /// <summary>Marks the exact admitted problem so negotiated and fallback paths enrich it once.</summary>
    private static readonly object AdmissionKey = new();
    /// <inheritdoc />
    public void PostConfigure(string? name, ProblemDetailsOptions options)
    {
        var previous = options.CustomizeProblemDetails;
        options.CustomizeProblemDetails = context =>
        {
            if (context.HttpContext.Items.TryGetValue(AdmissionKey, out var admitted)
                && ReferenceEquals(admitted, context.ProblemDetails)) return;
            try { previous?.Invoke(context); }
            catch (Exception error) when (error is not OperationCanceledException
                || !context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                context = new ProblemDetailsContext { HttpContext = context.HttpContext,
                    ProblemDetails = context.ProblemDetails, Exception = error };
            }
            context.HttpContext.RequestServices.GetRequiredService<IProblemRepresentationPolicy>().Apply(context);
            context.HttpContext.Items[AdmissionKey] = context.ProblemDetails;
        };
    }
}
