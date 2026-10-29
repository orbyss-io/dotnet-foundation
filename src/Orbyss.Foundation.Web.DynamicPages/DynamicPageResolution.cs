namespace Orbyss.Foundation.Web.DynamicPages;

/// <summary>Returns an eligibility outcome, its coherent resource version, and only the authorized projection.</summary>
/// <param name="Status">The observed admission result.</param>
/// <param name="Version">An opaque resource/publication version required for admitted results.</param>
/// <param name="Document">The response projection, present only when admitted.</param>
public sealed record DynamicPageResolution(DynamicPageStatus Status, string? Version = null, DynamicPageDocument? Document = null);
