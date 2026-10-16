using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Admits unmatched document navigation independently of endpoint authentication or application URL conventions.</summary>
public static class BrowserNavigationAdmission
{
    /// <summary>Requires a safe navigation method, native document headers and an explicit acceptable HTML preference.</summary>
    public static bool IsDocumentNavigation(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            || request.Headers["Sec-Fetch-Mode"] != "navigate" || request.Headers["Sec-Fetch-Dest"] != "document") return false;
        try
        {
            var accepted = request.GetTypedHeaders().Accept;
            var html = accepted?.Where(item => item.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Quality ?? 1).DefaultIfEmpty(0).Max() ?? 0;
            var json = accepted?.Where(item => item.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                    || item.MediaType.Equals("application/problem+json", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Quality ?? 1).DefaultIfEmpty(0).Max() ?? 0;
            return html > 0 && html >= json;
        }
        catch (FormatException) { return false; }
    }
}
