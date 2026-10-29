namespace Orbyss.Foundation.Web.DynamicPages;

/// <summary>Names the only consumer admission outcomes; unobserved eligibility never admits content.</summary>
public enum DynamicPageStatus
{
    /// <summary>The resource is currently eligible for this caller.</summary>
    Admitted = 1,
    /// <summary>The resource is absent or its existence must not be disclosed.</summary>
    NotFound = 2,
    /// <summary>The caller is not permitted to read this resource.</summary>
    Forbidden = 3,
    /// <summary>Current eligibility could not be established.</summary>
    Unavailable = 4
}
