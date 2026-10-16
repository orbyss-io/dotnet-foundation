using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.WebDefaults;
using CShells;

namespace Orbyss.Foundation.Web.Presentation;

/// <summary>Default renderer accepts only native admitted fields; applications may replace its public interface.</summary>
internal sealed class BrowserProblemPageRenderer(BrowserPresentationCatalog catalog, ShellSettings settings) : IProblemPageRenderer
{
    /// <inheritdoc />
    public string Render(AdmittedProblemPage problem)
    {
        var key = problem.Status switch { 401 => "expired", 403 => "denied", 404 => "missing", _ => "failed" };
        var pathBase = problem.ApplicationPathBase + ShellWebPaths.RoutePrefix(settings);
        return BrowserPageRendering.Page(catalog, catalog.Copy[key],
            "<p>" + BrowserPageRendering.E(problem.Title) + "</p><p>"
            + BrowserPageRendering.E(catalog.Copy[problem.HasLocalSession switch { true => "active", false => "anonymous", _ => "unobserved" }])
            + "</p><p>Reference: "
            + BrowserPageRendering.E(problem.CorrelationId) + "</p><p><a href=\""
            + BrowserPageRendering.E(pathBase + catalog.RoutePrefix + "/sign-in") + "\">"
            + BrowserPageRendering.E(catalog.Copy["retry"]) + "</a></p>", pathBase);
    }
}
