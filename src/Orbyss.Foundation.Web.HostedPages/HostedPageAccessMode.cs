namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Defines explicit resource admission, separate from response policy selection.</summary>
public enum HostedPageAccessMode
{
    /// <summary>Rejects an omitted access selection.</summary>
    Unspecified,
    /// <summary>Admits anonymous requests.</summary>
    Public,
    /// <summary>Requires a validated BFF session and any selected permission.</summary>
    Authenticated
}
