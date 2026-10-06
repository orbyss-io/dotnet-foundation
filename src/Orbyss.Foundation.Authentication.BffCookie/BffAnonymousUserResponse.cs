namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Preserves the anonymous session projection with only its authentication flag.</summary>
public sealed record BffAnonymousUserResponse() : BffUserResponse(false);
