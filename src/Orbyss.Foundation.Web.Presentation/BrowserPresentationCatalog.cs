using System.Collections.Frozen;
using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.Web.Presentation;

/// <summary>Immutable validated presentation configuration, independent of request text.</summary>
public sealed class BrowserPresentationCatalog
{
    /// <summary>Stores copied locale catalogs; configuration cannot mutate live responses.</summary>
    private readonly FrozenDictionary<string, BrowserPresentationText> locales;
    /// <summary>Creates a bounded immutable configuration or fails activation.</summary>
    public BrowserPresentationCatalog(BrowserPresentationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!LocalPath(options.RoutePrefix) || options.RoutePrefix == "/" || options.RoutePrefix.EndsWith('/')
            || new[] { "/api", "/bff", "/signin", "/signout" }.Any(prefix => options.RoutePrefix.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            || !LocalPath(options.ReturnPath) || options.ReturnPath.StartsWith(options.RoutePrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Presentation requires distinct local page routes and a local application return path.");
        if (string.IsNullOrWhiteSpace(options.ApplicationName) || options.ApplicationName.Length > 128
            || options.ApplicationName.Any(char.IsControl) || options.Locale.Length is < 1 or > 64
            || CultureInfo.GetCultureInfo(options.Locale).Name != options.Locale
            || options.Theme is not ("blue" or "green" or "slate")
            || options.SignOutConfirmation is not ("never" or "when-unsaved" or "always"))
            throw new InvalidDataException("Presentation name, locale, theme or confirmation policy is invalid.");
        var copy = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sign-in"] = "Sign in", ["sign-out"] = "Sign out", ["signed-out"] = "Session status",
            ["expired"] = "Your session has expired", ["denied"] = "Access denied",
            ["failed"] = "The request could not be completed", ["missing"] = "Page not found",
            ["retry"] = "Try signing in again", ["home"] = "Return to application",
            ["active"] = "A local session is active.", ["anonymous"] = "No local session is active.",
            ["unobserved"] = "Local session status has not been confirmed.",
            ["client-authentication"] = "Authentication is managed by this application's browser client. Return to the application to continue.",
            ["remote"] = "Provider sign-out could not be started. Your provider session may still be active.",
            ["confirm"] = "Continue to sign out. Unsaved changes may be lost.",
            ["reference"] = "Reference", ["continue"] = "Continue to the application",
            ["authentication-required"] = "Sign in to continue.",
            ["callback-invalid"] = "This sign-in attempt could not be completed. Start a fresh attempt.",
            ["provider-unavailable"] = "The sign-in provider is unavailable. Please try again later.",
            ["identity-invalid"] = "Your application session could not be validated. Please sign in again.",
            ["authorization-denied"] = "You do not have access to this resource.",
            ["antiforgery-invalid"] = "This request could not be admitted. Please try again from the application."
        };
        if (options.Copy is null || options.Copy.Any(item => !copy.ContainsKey(item.Key) || string.IsNullOrWhiteSpace(item.Value)
            || item.Value.Length > 256 || item.Value.Any(char.IsControl)))
            throw new InvalidDataException("Presentation copy contains an unknown key or invalid bounded text.");
        foreach (var (key, value) in options.Copy) copy[key] = value;
        Copy = copy.ToFrozenDictionary(StringComparer.Ordinal);
        if (options.Locales is null || options.Locales.Count > 32 || options.MaxPageBytes is < 512 or > 1_048_576)
            throw new InvalidDataException("Presentation locale catalogs or response budget are invalid.");
        var selectedLocales = new Dictionary<string, BrowserPresentationText>(StringComparer.OrdinalIgnoreCase)
            { [options.Locale] = new(options.Locale, Copy) };
        foreach (var (locale, overrides) in options.Locales)
        {
            if (locale.Length is < 1 or > 64 || CultureInfo.GetCultureInfo(locale).Name != locale || overrides is null
                || overrides.Any(item => !copy.ContainsKey(item.Key) || string.IsNullOrWhiteSpace(item.Value)
                    || item.Value.Length > 256 || item.Value.Any(char.IsControl)))
                throw new InvalidDataException("Presentation locale catalog contains invalid culture or copy.");
            var translated = new Dictionary<string, string>(copy, StringComparer.Ordinal);
            foreach (var (key, value) in overrides) translated[key] = value;
            selectedLocales[locale] = new(locale, translated.ToFrozenDictionary(StringComparer.Ordinal));
        }
        locales = selectedLocales.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        MaxPageBytes = options.MaxPageBytes;
        ApplicationName = options.ApplicationName;
        Locale = options.Locale;
        Theme = options.Theme;
        RoutePrefix = options.RoutePrefix;
        ReturnPath = options.ReturnPath;
        SignOutConfirmation = options.SignOutConfirmation;
        ResponsePolicy = options.ResponsePolicy;
        AssetResponsePolicy = options.AssetResponsePolicy;
        foreach (var selector in new[] { ResponsePolicy, AssetResponsePolicy })
            if (string.IsNullOrWhiteSpace(selector) || selector.Length > 128
                || selector.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.')))
                throw new InvalidDataException("Presentation response policy selector is invalid.");
    }
    /// <summary>Gets selected bounded display copy.</summary>
    public FrozenDictionary<string, string> Copy { get; }
    /// <summary>Gets the display name.</summary>
    public string ApplicationName { get; }
    /// <summary>Gets the configured copy language.</summary>
    public string Locale { get; }
    /// <summary>Gets the admitted application theme token.</summary>
    public string Theme { get; }
    /// <summary>Gets the reserved public route prefix.</summary>
    public string RoutePrefix { get; }
    /// <summary>Gets the fixed application return path.</summary>
    public string ReturnPath { get; }
    /// <summary>Gets the shared control policy.</summary>
    public string SignOutConfirmation { get; }
    /// <summary>Gets the page response policy.</summary>
    public string ResponsePolicy { get; }
    /// <summary>Gets the asset response policy.</summary>
    public string AssetResponsePolicy { get; }
    /// <summary>Gets the maximum complete recovery page size.</summary>
    public int MaxPageBytes { get; }
    /// <summary>Selects the highest-weight configured language or culture parent, falling back to the configured Locale.</summary>
    public BrowserPresentationText Select(IEnumerable<string> preferredLocales)
    {
        foreach (var name in preferredLocales.Take(32))
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(name);
                while (culture.Name.Length > 0)
                {
                    if (locales.TryGetValue(culture.Name, out var selected)) return selected;
                    culture = culture.Parent;
                }
            }
            catch (CultureNotFoundException) { }
        }
        return locales[Locale];
    }
    /// <summary>Selects admitted request language weights without treating arbitrary headers as presentation text.</summary>
    public BrowserPresentationText Select(HttpContext context) => Select(PreferredLocales(context));
    /// <summary>Reads a bounded list of language preferences; malformed headers use the fallback.</summary>
    internal static IReadOnlyList<string> PreferredLocales(HttpContext context)
    {
        try
        {
            return context.Request.GetTypedHeaders().AcceptLanguage?
                .Where(item => (item.Quality ?? 1) > 0 && item.Value.Length <= 64)
                .OrderByDescending(item => item.Quality ?? 1).Take(32).Select(item => item.Value.ToString()).ToArray() ?? [];
        }
        catch (FormatException) { return []; }
    }
    /// <summary>Admits literal paths without URI decoding, templates, query strings or protocol-relative targets.</summary>
    internal static bool LocalPath(string value) => value.Length is > 0 and <= 256 && value.StartsWith('/')
        && !value.Contains("//", StringComparison.Ordinal) && !value.Split('/').Any(segment => segment is "." or "..")
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '/' or '-' or '_' or '.');
}
