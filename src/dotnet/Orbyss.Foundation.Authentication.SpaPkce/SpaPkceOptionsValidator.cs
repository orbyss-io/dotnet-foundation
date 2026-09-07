using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication;

namespace Orbyss.Foundation.Authentication.SpaPkce;

/// <summary>Validates settings owned by the direct SPA-PKCE profile.</summary>
internal sealed class SpaPkceOptionsValidator : IValidateOptions<FoundationWebOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, FoundationWebOptions options)
    {
        var failures = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            failures.Add("Foundation:Web:ClientSecret must be empty for the public SPA-PKCE client.");
        }

        if (options.AllowedOrigins.Length == 0)
        {
            failures.Add("Foundation:Web:AllowedOrigins must contain at least one exact origin for SPA-PKCE.");
        }

        foreach (var origin in options.AllowedOrigins)
        {
            if (origin.Contains('*', StringComparison.Ordinal)
                || !Uri.TryCreate(origin, UriKind.Absolute, out var parsed)
                || parsed.AbsolutePath != "/"
                || !string.IsNullOrEmpty(parsed.Query)
                || !string.IsNullOrEmpty(parsed.Fragment))
            {
                failures.Add($"Foundation:Web:AllowedOrigins contains an invalid or non-exact origin: {origin}");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
