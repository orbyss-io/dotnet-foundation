using System.Text.Encodings.Web;
using Orbyss.Foundation.Web.Presentation;

internal sealed class RecoveryLayoutFixtureRenderer : IBrowserRecoveryPageRenderer
{
    public string Render(AdmittedBrowserRecoveryPage page)
    {
        if (page.ApplicationName == "Oversize") return new string('x', 100000);
        if (page.ApplicationName == "Broken") throw new InvalidOperationException("private-layout-exception");
        return "<!doctype html><html lang=\"" + HtmlEncoder.Default.Encode(page.Locale)
            + "\"><title>Custom recovery</title><main data-recovery-layout=\"custom\"><h1>"
            + HtmlEncoder.Default.Encode(page.Title) + "</h1>" + page.BodyHtml + "</main></html>";
    }
}
