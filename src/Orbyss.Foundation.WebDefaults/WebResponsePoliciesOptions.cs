namespace Orbyss.Foundation.WebDefaults;

/// <summary>Binds named policies and whole-policy selectors under Foundation:Web:ResponsePolicies.</summary>
public sealed class WebResponsePoliciesOptions
{
    /// <summary>Identifies the shell configuration section.</summary>
    public const string SectionName = "Foundation:Web:ResponsePolicies";
    /// <summary>Gets or sets the default policy identity.</summary>
    public string DefaultPolicy { get; set; } = "default";
    /// <summary>Gets or sets complete named policies.</summary>
    public Dictionary<string, WebResponsePolicyOptions> Policies { get; set; } = new(StringComparer.Ordinal)
    {
        ["default"] = new(),
        ["private"] = new(),
        ["public-asset"] = new() { PublicAssetMaxAgeSeconds = 31536000 }
    };
    /// <summary>Gets or sets policy identities selected by endpoint feature ownership.</summary>
    public Dictionary<string, string> FeaturePolicies { get; set; } = new(StringComparer.Ordinal);
}
