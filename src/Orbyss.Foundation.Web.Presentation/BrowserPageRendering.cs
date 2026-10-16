using System.Text.Encodings.Web;

namespace Orbyss.Foundation.Web.Presentation;

/// <summary>Encodes bounded server-owned copy into semantic, no-script-capable pages.</summary>
internal static class BrowserPageRendering
{
    /// <summary>Encodes all text and attribute values through the platform encoder.</summary>
    internal static string E(string value) => HtmlEncoder.Default.Encode(value);
    /// <summary>Builds a page with a single initial focus target and no inline executable content.</summary>
    internal static string Page(BrowserPresentationCatalog catalog, string title, string content, string pathBase) =>
        "<!doctype html><html lang=\"" + E(catalog.Locale) + "\"><head><meta charset=\"utf-8\">"
        + "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>" + E(title) + " — "
        + E(catalog.ApplicationName) + "</title><link rel=\"stylesheet\" href=\"" + E(pathBase + catalog.RoutePrefix + "/assets/v1/page.css")
        + "\"></head><body><header>" + E(catalog.ApplicationName) + "</header><main><h1 tabindex=\"-1\" autofocus>"
        + E(title) + "</h1>" + content + "<p><a href=\"" + E(pathBase + catalog.ReturnPath) + "\">"
        + E(catalog.Copy["home"]) + "</a></p></main></body></html>";
}
