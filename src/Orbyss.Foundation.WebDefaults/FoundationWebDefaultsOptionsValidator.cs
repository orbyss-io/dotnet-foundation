using Microsoft.Extensions.Options;

namespace Orbyss.Foundation.WebDefaults;

/// <summary>Rejects an incomplete or invalid locale set.</summary>
internal sealed class FoundationWebDefaultsOptionsValidator : IValidateOptions<FoundationWebDefaultsOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, FoundationWebDefaultsOptions options)
    {
        var failures = new List<string>();
        if (options.SupportedLocales is null || options.SupportedLocales.Length == 0
            || !options.SupportedLocales.Contains(options.DefaultLocale, StringComparer.OrdinalIgnoreCase))
        {
            failures.Add("Foundation:Web:SupportedLocales must contain Foundation:Web:DefaultLocale.");
        }

        if (options.SupportedLocales is not null
            && options.SupportedLocales.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.SupportedLocales.Length)
        {
            failures.Add("Foundation:Web:SupportedLocales must contain distinct locale names.");
        }

        foreach (var locale in options.SupportedLocales ?? [])
        {
            if (string.IsNullOrWhiteSpace(locale))
            {
                failures.Add("Foundation:Web:SupportedLocales must contain non-empty locale names.");
                continue;
            }
            try
            {
                _ = System.Globalization.CultureInfo.GetCultureInfo(locale);
            }
            catch (System.Globalization.CultureNotFoundException)
            {
                failures.Add($"Foundation:Web:SupportedLocales contains an unknown locale: {locale}");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
