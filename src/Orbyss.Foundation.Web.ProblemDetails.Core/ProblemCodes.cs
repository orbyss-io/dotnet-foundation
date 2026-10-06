namespace Orbyss.Foundation.Web.ProblemDetails.Core;

/// <summary>Owns the existing stable generic managed-boundary error codes.</summary>
public static class ProblemCodes
{
    /// <summary>Caller authentication is required.</summary>
    public const string AuthenticationRequired = "authentication_required";
    /// <summary>The authenticated caller is denied access.</summary>
    public const string AuthorizationDenied = "authorization_denied";
    /// <summary>The request was not admitted.</summary>
    public const string InvalidRequest = "invalid_request";
    /// <summary>The request failed without exposing private diagnostic detail.</summary>
    public const string RequestFailed = "request_failed";
}
