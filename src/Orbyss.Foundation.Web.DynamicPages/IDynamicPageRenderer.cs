namespace Orbyss.Foundation.Web.DynamicPages;

/// <summary>Optional trusted application layout implementation; native admission, budgets and final verification remain outside it.</summary>
public interface IDynamicPageRenderer
{
    /// <summary>Renders a complete document, encoding text and attributes; no raw resource markup or writable response is supplied.</summary>
    string Render(AdmittedDynamicPage page);
}
