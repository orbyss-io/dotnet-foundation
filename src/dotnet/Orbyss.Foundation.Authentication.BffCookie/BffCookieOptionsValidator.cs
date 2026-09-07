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
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>Rejects a protocol callback that is not a local absolute path.</summary>
    private static void ValidatePath(string value, string name, ICollection<string> failures)
    {
        if (!value.StartsWith("/", StringComparison.Ordinal) || value.StartsWith("//", StringComparison.Ordinal))
        {
            failures.Add($"Foundation:Web:{name} must be a local absolute path.");
        }
    }
}
