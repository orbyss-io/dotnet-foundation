namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Selects one trusted local deployment and its exact admitted manifest.</summary>
public sealed class HostedPageOptions
{
    /// <summary>Gets or sets the read-only deployment root relative to application content root.</summary>
    public string Root { get; set; } = "hosted-pages";
    /// <summary>Gets or sets the manifest filename inside the deployment root.</summary>
    public string Manifest { get; set; } = "manifest.json";
    /// <summary>Gets or sets the SHA-256 of the exact manifest bytes; required for coherent admission.</summary>
    public string ManifestSha256 { get; set; } = "";
    /// <summary>Gets or sets the configured current revision, which must agree with the manifest.</summary>
    public string CurrentRevision { get; set; } = "";
    /// <summary>Gets or sets the literal shell-local page route.</summary>
    public string PagePath { get; set; } = "/";
    /// <summary>Gets or sets the aggregate retained asset byte budget, capped at 128 MiB.</summary>
    public int MaxTotalBytes { get; set; } = 32_000_000;
}
