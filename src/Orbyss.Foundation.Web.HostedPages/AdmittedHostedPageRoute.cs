namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Stores immutable shell-local route selection; null Id denotes legacy public mode.</summary>
internal sealed record AdmittedHostedPageRoute(string? Id, string Path, string Revision,
    HostedPageAdmission Page, HostedPageAdmission Bootstrap);

