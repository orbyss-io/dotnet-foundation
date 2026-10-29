using System.Text;
using System.Text.Encodings.Web;

namespace Orbyss.Foundation.Web.DynamicPages;

/// <summary>Renders semantic initial content and a flexible application mount without a fixed form layout or inline executable code.</summary>
internal sealed class DefaultDynamicPageRenderer : IDynamicPageRenderer
{
    /// <inheritdoc />
    public string Render(AdmittedDynamicPage page)
    {
        var html = new StringBuilder("<!doctype html><html lang=\"").Append(E(page.Locale))
            .Append("\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>")
            .Append(E(page.Title)).Append("</title><meta name=\"description\" content=\"").Append(E(page.Description)).Append("\">");
        if (page.CanonicalPath is not null) html.Append("<link rel=\"canonical\" href=\"").Append(E(page.CanonicalPath)).Append("\">");
        foreach (var style in page.Styles) html.Append("<link rel=\"stylesheet\" href=\"").Append(E(style)).Append("\">");
        html.Append("</head><body><main id=\"foundation-page\"><h1>").Append(E(page.Heading))
            .Append("</h1><p>").Append(E(page.Text)).Append("</p><div id=\"foundation-application\"></div></main>")
            .Append("<script type=\"application/json\" id=\"foundation-page-data\">").Append(page.BootstrapJson).Append("</script>");
        foreach (var module in page.Modules) html.Append("<script type=\"module\" src=\"").Append(E(module)).Append("\"></script>");
        return html.Append("</body></html>").ToString();
    }
    /// <summary>Encodes every text and attribute context.</summary>
    private static string E(string value) => HtmlEncoder.Default.Encode(value);
}
