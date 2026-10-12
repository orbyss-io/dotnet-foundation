using Orbyss.Foundation.WebDefaults;

namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Selects access independently of the response headers.</summary>
public sealed class HostedPageAccessOptions
{
    /// <summary>Gets or sets the required explicit access mode.</summary>
    public HostedPageAccessMode Mode { get; set; } = HostedPageAccessMode.Unspecified;
    /// <summary>Gets or sets an optional canonical permission, requiring Authenticated mode.</summary>
    public string? Permission { get; set; }
    /// <summary>Gets or sets the named shell response policy, which must exist at activation.</summary>
    public string ResponsePolicy { get; set; } = WebResponsePolicyNames.Default;
}
