namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Preserves the explicit local sign-out acknowledgement.</summary>
/// <param name="SignedOut">Whether the local sign-out boundary was reached.</param>
public sealed record BffSignedOutResponse(bool SignedOut);
