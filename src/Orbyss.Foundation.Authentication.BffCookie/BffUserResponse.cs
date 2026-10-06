using System.Text.Json.Serialization;

namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Describes the preserved closed anonymous/authenticated session wire shapes.</summary>
/// <param name="Authenticated">Whether a validated account owns this session.</param>
[JsonDerivedType(typeof(BffAnonymousUserResponse))]
[JsonDerivedType(typeof(BffAuthenticatedUserResponse))]
public abstract record BffUserResponse(bool Authenticated);
