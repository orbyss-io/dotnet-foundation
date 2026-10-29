using System.Security.Claims;

namespace Orbyss.Foundation.Web.DynamicPages;

/// <summary>Supplies native caller identity and bounded route values without a writable response or arbitrary query text.</summary>
/// <param name="User">The principal established by the selected native authentication pipeline.</param>
/// <param name="RouteValues">The current route's bounded values, never a deployment snapshot.</param>
/// <param name="ApplicationPathBase">The native shell and outer application prefix.</param>
public sealed record DynamicPageRequest(ClaimsPrincipal User, IReadOnlyDictionary<string, string> RouteValues, string ApplicationPathBase)
{
    /// <summary>Gets the canonical UI culture selected by native request localization; consumers own translated resource projections.</summary>
    public string Locale { get; init; } = "en";
}
