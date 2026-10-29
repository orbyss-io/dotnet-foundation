namespace Orbyss.Foundation.Web.DynamicPages;

/// <summary>Immutable bounded response fields supplied to trusted code renderers after consumer admission.</summary>
public sealed class AdmittedDynamicPage
{
    /// <summary>Copies validated fields without retaining the consumer's mutable model.</summary>
    internal AdmittedDynamicPage(DynamicPageDocument model, string bootstrapJson, string? canonicalPath, IReadOnlyList<string> modules, IReadOnlyList<string> styles)
    {
        Title = model.Title; Description = model.Description; Locale = model.Locale;
        Heading = model.Heading; Text = model.Text; BootstrapJson = bootstrapJson;
        CanonicalPath = canonicalPath; Modules = modules; Styles = styles;
    }
    /// <summary>Gets the document title; custom renderers must encode it.</summary>
    public string Title { get; }
    /// <summary>Gets the metadata description; custom renderers must attribute-encode it.</summary>
    public string Description { get; }
    /// <summary>Gets the admitted culture.</summary>
    public string Locale { get; }
    /// <summary>Gets the initial heading.</summary>
    public string Heading { get; }
    /// <summary>Gets initial plain text.</summary>
    public string Text { get; }
    /// <summary>Gets serialized, HTML-safe inert JSON; never execute it as JavaScript.</summary>
    public string BootstrapJson { get; }
    /// <summary>Gets an optional native application-local canonical URL.</summary>
    public string? CanonicalPath { get; }
    /// <summary>Gets admitted external module URLs; these refer to trusted consumer code.</summary>
    public IReadOnlyList<string> Modules { get; }
    /// <summary>Gets admitted stylesheet URLs.</summary>
    public IReadOnlyList<string> Styles { get; }
}
