using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.WebDefaults;
using CShells;

namespace Orbyss.Foundation.Web.Presentation;

/// <summary>Default renderer accepts only native admitted fields; applications may replace its public interface.</summary>
internal sealed class BrowserProblemPageRenderer(BrowserPresentationCatalog catalog, ShellSettings settings, IBrowserRecoveryPageRenderer renderer) : IProblemPageRenderer
{
    /// <inheritdoc />
    public string Render(AdmittedProblemPage problem)
    {
        var key = problem.Status switch { 401 => "expired", 403 => "denied", 404 => "missing", _ => "failed" };
        var text = catalog.Select(problem.PreferredLocales);
        var messageKey = problem.Code switch
        {
            "authentication_required" => "authentication-required",
            "authentication_callback_invalid" => "callback-invalid",
            "identity_provider_unavailable" => "provider-unavailable",
            "authentication_identity_invalid" => "identity-invalid",
            "authorization_denied" => "authorization-denied",
            "invalid_antiforgery_token" or "antiforgery_invalid" => "antiforgery-invalid",
            _ => key
        };
        var pathBase = problem.ApplicationPathBase + ShellWebPaths.RoutePrefix(settings);
        var body = "<p>" + BrowserPageRendering.E(text.Copy[messageKey]) + "</p><p>"
            + BrowserPageRendering.E(text.Copy[problem.HasLocalSession switch { true => "active", false => "anonymous", _ => "unobserved" }])
            + "</p><p>" + BrowserPageRendering.E(text.Copy["reference"]) + ": "
            + BrowserPageRendering.E(problem.CorrelationId) + "</p><p><a href=\""
            + BrowserPageRendering.E(pathBase + catalog.RoutePrefix + "/sign-in") + "\">"
            + BrowserPageRendering.E(text.Copy["retry"]) + "</a></p>";
        if (problem.HasLocalSession == true)
            body += "<p><a href=\"" + BrowserPageRendering.E(pathBase + catalog.ReturnPath) + "\">"
                + BrowserPageRendering.E(text.Copy["continue"]) + "</a></p>";
        var page = new AdmittedBrowserRecoveryPage(catalog, text, text.Copy[key], body, pathBase, problem.HasLocalSession);
        var html = renderer.Render(page);
        if (System.Text.Encoding.UTF8.GetByteCount(html) > catalog.MaxPageBytes)
            throw new InvalidOperationException("Recovery page exceeds the presentation budget.");
        return html;
    }
    /// <inheritdoc />
    public string GetContentLanguage(AdmittedProblemPage problem) => catalog.Select(problem.PreferredLocales).Locale;
}
