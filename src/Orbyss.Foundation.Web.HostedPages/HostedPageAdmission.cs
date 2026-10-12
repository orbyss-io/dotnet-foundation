namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Stores copied access and response policy selections.</summary>
internal sealed record HostedPageAdmission(bool Private, string? Permission, string? Policy);
