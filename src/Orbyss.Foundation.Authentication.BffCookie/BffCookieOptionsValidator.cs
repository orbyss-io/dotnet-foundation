using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication;

namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Validates settings owned by the confidential BFF profile.</summary>
internal sealed class BffCookieOptionsValidator : IValidateOptions<FoundationWebOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, FoundationWebOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            failures.Add("Foundation:Web:ClientSecret is required by the BFF-cookie profile.");
        }

        ValidatePath(options.CallbackPath, "CallbackPath", failures);
        ValidatePath(options.SignedOutCallbackPath, "SignedOutCallbackPath", failures);
        ValidatePath(options.RemoteSignOutPath, "RemoteSignOutPath", failures);
        ValidatePath(options.AccessDeniedPath, "AccessDeniedPath", failures);
        var paths = new[] { options.CallbackPath, options.SignedOutCallbackPath, options.RemoteSignOutPath, options.AccessDeniedPath };
        if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length
            || paths.Intersect(["/bff/login", "/bff/user", "/bff/antiforgery", "/bff/logout", "/bff/signed-out"], StringComparer.OrdinalIgnoreCase).Any())
        {
            failures.Add("Foundation:Web callback and access-denied paths must be distinct and must not replace BFF session endpoints.");
        }
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>Rejects a protocol callback that is not a local absolute path.</summary>
    private static void ValidatePath(string value, string name, ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value) || value.EndsWith("/", StringComparison.Ordinal)
            || !value.StartsWith("/", StringComparison.Ordinal) || value.Contains("//", StringComparison.Ordinal)
            || value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)
                || character is '?' or '#' or '{' or '}' or '*' or '\\' or '%')
            || value.Split('/').Any(segment => segment is "." or ".."))
        {
            failures.Add($"Foundation:Web:{name} must be a literal local absolute path without query, fragment, escaping or route parameters.");
        }
    }
}
