namespace Orbyss.Foundation.Web.Presentation;

/// <summary>Preserves the semantic, encoded, no-script-capable default when no application override is registered.</summary>
internal sealed class DefaultBrowserRecoveryPageRenderer : IBrowserRecoveryPageRenderer
{
    /// <inheritdoc />
    public string Render(AdmittedBrowserRecoveryPage page) => BrowserPageRendering.Page(page);
}
