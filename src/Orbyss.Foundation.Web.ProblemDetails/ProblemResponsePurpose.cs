namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Authoritative endpoint purpose; representation preferences cannot override machine contracts.</summary>
public enum ProblemResponsePurpose
{
    /// <summary>Retains the bounded machine Problem Details contract regardless of Accept.</summary>
    Machine = 0,
    /// <summary>Permits an admitted page representation when the caller explicitly accepts HTML.</summary>
    BrowserPage = 1,
}
