namespace Orbyss.Foundation.Web.DynamicPages;

/// <summary>Configures the separately selected request-time page mechanism.</summary>
public sealed class DynamicPageOptions
{
    /// <summary>Names the shell configuration section.</summary>
    public const string SectionName = "Foundation:DynamicPages";
    /// <summary>Gets or sets at most 64 explicit routes; no implicit anonymous fallback exists.</summary>
    public List<DynamicPageRouteOptions> Routes { get; set; } = [];
    /// <summary>Gets or sets the maximum complete encoded HTML bytes, between 512 bytes and 16 MiB.</summary>
    public int MaxResponseBytes { get; set; } = 262144;
    /// <summary>Gets or sets the maximum bootstrap bytes, no greater than the complete response budget.</summary>
    public int MaxBootstrapBytes { get; set; } = 65536;
    /// <summary>Gets or sets a cooperative resolution/render/verification deadline, between one and thirty seconds.</summary>
    public int TimeoutSeconds { get; set; } = 5;
}
