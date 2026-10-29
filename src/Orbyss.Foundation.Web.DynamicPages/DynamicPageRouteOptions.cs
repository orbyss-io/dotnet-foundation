namespace Orbyss.Foundation.Web.DynamicPages;

/// <summary>Selects an explicitly admitted route and its code-registered consumer resolver.</summary>
public sealed class DynamicPageRouteOptions
{
    /// <summary>Gets or sets a stable route identity.</summary>
    public string Id { get; set; } = "";
    /// <summary>Gets or sets a bounded local route with optional simple required parameters.</summary>
    public string Path { get; set; } = "";
    /// <summary>Gets or sets the exact registered resolver identity.</summary>
    public string Resolver { get; set; } = "";
    /// <summary>Gets or sets Public or Authenticated; omission fails activation.</summary>
    public string Mode { get; set; } = "";
    /// <summary>Gets or sets an optional canonical permission in authenticated mode.</summary>
    public string? Permission { get; set; }
    /// <summary>Gets or sets the complete named response policy.</summary>
    public string ResponsePolicy { get; set; } = "default";
    /// <summary>Gets or sets whether indexing is denied independently of the named policy.</summary>
    public bool NoIndex { get; set; } = true;
    /// <summary>Gets or sets shell-local trusted application module paths, never resource data.</summary>
    public string[] Modules { get; set; } = [];
    /// <summary>Gets or sets shell-local trusted application stylesheets.</summary>
    public string[] Styles { get; set; } = [];
}
