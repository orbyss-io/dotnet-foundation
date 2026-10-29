using System.Text.Encodings.Web;
using Orbyss.Foundation.Web.DynamicPages;

internal sealed class DynamicFixtureRenderer : IDynamicPageRenderer
{
    public string Render(AdmittedDynamicPage page) => page.Heading.Contains("oversized", StringComparison.Ordinal)
        ? new string('x', 300000)
        : page.Heading.Contains("throw-renderer", StringComparison.Ordinal)
            ? throw new InvalidOperationException("private-renderer-exception")
            : "<!doctype html><html><title>Custom layout</title><main data-custom-layout=\"true\"><h1>"
                + HtmlEncoder.Default.Encode(page.Heading) + "</h1></main></html>";
}
