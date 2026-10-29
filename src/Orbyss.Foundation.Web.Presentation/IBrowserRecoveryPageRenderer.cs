namespace Orbyss.Foundation.Web.Presentation;

/// <summary>Optional trusted layout override shared by native recovery landings and the default problem renderer.</summary>
public interface IBrowserRecoveryPageRenderer
{
    /// <summary>Renders bounded HTML using encoded text and the supplied native controls, without a writable response or authentication authority.</summary>
    string Render(AdmittedBrowserRecoveryPage page);
}
