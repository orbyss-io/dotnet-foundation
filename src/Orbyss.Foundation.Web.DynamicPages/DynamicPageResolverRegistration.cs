namespace Orbyss.Foundation.Web.DynamicPages;

/// <summary>Binds a validated consumer identity to its request-scoped implementation.</summary>
/// <param name="Id">The configured resolver key.</param>
/// <param name="Implementation">The consumer-owned implementation type.</param>
internal sealed record DynamicPageResolverRegistration(string Id, Type Implementation);
