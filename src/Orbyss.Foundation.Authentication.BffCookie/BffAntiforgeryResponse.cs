namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Preserves the managed antiforgery transport projection.</summary>
/// <param name="HeaderName">The managed header key.</param>
/// <param name="FormFieldName">The managed form key.</param>
/// <param name="RequestToken">The opaque request token.</param>
public sealed record BffAntiforgeryResponse(string HeaderName, string FormFieldName, string? RequestToken);
