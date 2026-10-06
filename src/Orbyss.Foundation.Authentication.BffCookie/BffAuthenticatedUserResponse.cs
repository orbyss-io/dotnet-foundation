namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Preserves validated session fields without exposing credentials or held tokens.</summary>
/// <param name="Issuer">The exact validated issuer.</param>
/// <param name="Subject">The exact validated subject.</param>
/// <param name="DisplayName">The optional display name, retaining an explicit null.</param>
/// <param name="Permissions">The admitted distinct ordinal permission projection.</param>
public sealed record BffAuthenticatedUserResponse(string Issuer, string Subject, string? DisplayName, IReadOnlyList<string> Permissions)
    : BffUserResponse(true);
