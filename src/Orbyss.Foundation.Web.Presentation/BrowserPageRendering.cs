using System.Text.Encodings.Web;

namespace Orbyss.Foundation.Web.Presentation;

/// <summary>Encodes bounded server-owned copy into semantic, no-script-capable pages.</summary>
internal static class BrowserPageRendering
{
    /// <summary>Encodes all text and attribute values through the platform encoder.</summary>
    internal static string E(string value) => HtmlEncoder.Default.Encode(value);
    /// <summary>Builds a page with a single initial focus target and no inline executable content.</summary>
    internal static string Page(AdmittedBrowserRecoveryPage page) =>
        "<!doctype html><html lang=\"" + E(page.Locale) + "\"><head><meta charset=\"utf-8\">"
        + "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>" + E(page.Title) + " — "
        + E(page.ApplicationName) + "</title><link rel=\"stylesheet\" href=\"" + E(page.StylesheetPath)
        + "\"></head><body data-theme=\"" + E(page.Theme) + "\"><header>" + E(page.ApplicationName) + "</header><main><h1 tabindex=\"-1\" autofocus>"
        + E(page.Title) + "</h1>" + page.BodyHtml + "<p><a href=\"" + E(page.HomePath) + "\">"
        + E(page.HomeLabel) + "</a></p></main><script type=\"module\" src=\"" + E(page.FocusModulePath) + "\"></script></body></html>";
}
