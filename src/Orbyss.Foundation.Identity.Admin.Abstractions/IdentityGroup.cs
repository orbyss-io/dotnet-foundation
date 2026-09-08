namespace Orbyss.Foundation.Identity.Admin;
/// <summary>A provider-neutral group.</summary>
public sealed record IdentityGroup(string Id, string Name, string? Path = null);
