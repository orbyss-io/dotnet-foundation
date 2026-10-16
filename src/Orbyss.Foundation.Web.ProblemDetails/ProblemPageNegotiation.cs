using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Requires declared browser purpose and an explicit admissible HTML preference.</summary>
internal static class ProblemPageNegotiation
{
    /// <summary>Machine metadata wins over protocol-handler hints and every media preference.</summary>
    public static bool AllowsHtml(HttpContext context)
    {
        var purposes = new[] { context.GetEndpoint()?.Metadata.GetMetadata<ProblemResponsePurposeMetadata>(),
            context.Features.Get<IExceptionHandlerFeature>()?.Endpoint?.Metadata.GetMetadata<ProblemResponsePurposeMetadata>(),
            context.Features.Get<ProblemResponsePurposeMetadata>() };
        if (purposes.Any(item => item is not null && item.Purpose != ProblemResponsePurpose.BrowserPage)
            || !purposes.Any(item => item?.Purpose == ProblemResponsePurpose.BrowserPage)) return false;
        try
        {
            var accepted = context.Request.GetTypedHeaders().Accept;
            var html = accepted?.Where(item => item.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Quality ?? 1).DefaultIfEmpty(0).Max() ?? 0;
            var json = accepted?.Where(item => item.MediaType.Equals("application/problem+json", StringComparison.OrdinalIgnoreCase)
                    || item.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Quality ?? 1).DefaultIfEmpty(0).Max() ?? 0;
            return html > 0 && html >= json;
        }
        catch (FormatException) { return false; }
    }
}
