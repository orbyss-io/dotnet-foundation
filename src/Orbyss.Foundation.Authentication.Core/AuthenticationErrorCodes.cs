namespace Orbyss.Foundation.Authentication.Core;

/// <summary>Stable authentication and antiforgery failure codes.</summary>
public static class AuthenticationErrorCodes
{
    /// <summary>A session is required.</summary>
    public const string AuthenticationRequired = "authentication_required";

    /// <summary>The authenticated ticket has no unambiguous validated account projection.</summary>
    public const string IdentityInvalid = "authentication_identity_invalid";

    /// <summary>The authenticated account is forbidden from the operation.</summary>
    public const string AuthorizationDenied = "authorization_denied";

    /// <summary>The remote authentication callback could not be admitted.</summary>
    public const string CallbackInvalid = "authentication_callback_invalid";

    /// <summary>The remote identity provider was unavailable.</summary>
    public const string IdentityProviderUnavailable = "identity_provider_unavailable";

    /// <summary>The antiforgery token could not be admitted.</summary>
    public const string InvalidAntiforgeryToken = "invalid_antiforgery_token";
}
