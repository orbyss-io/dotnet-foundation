namespace Orbyss.Foundation.WebDefaults;

/// <summary>Defines the localizations enabled by the default Orbyss Foundation web middleware.</summary>
internal sealed class FoundationWebDefaultsOptions
{
    /// <summary>Gets or sets the deterministic application fallback locale.</summary>
    public string DefaultLocale { get; set; } = "en";

    /// <summary>Gets or sets the application locales accepted by request localization.</summary>
    public string[] SupportedLocales { get; set; } = ["en"];
}
