using Orbyss.Foundation.WebDefaults;

namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Selects a literal application entry and one admitted revision in a shell.</summary>
public sealed class HostedPageRouteOptions
{
    /// <summary>Gets or sets the stable route identity used to partition bootstrap admission.</summary>
    public string Id { get; set; } = "";
    /// <summary>Gets or sets the literal shell-local entry path.</summary>
    public string Path { get; set; } = "";
    /// <summary>Gets or sets the admitted revision; null selects CurrentRevision.</summary>
    public string? Revision { get; set; }
    /// <summary>Gets or sets the required explicit HTML admission and policy selection.</summary>
    public HostedPageAccessOptions? Page { get; set; }
    /// <summary>Gets or sets the explicit bootstrap selection, added to the page admission.</summary>
    public HostedPageAccessOptions? Bootstrap { get; set; }
}

