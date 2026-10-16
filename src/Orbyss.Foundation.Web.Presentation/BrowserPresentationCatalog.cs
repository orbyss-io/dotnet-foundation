using System.Collections.Frozen;
using System.Globalization;

namespace Orbyss.Foundation.Web.Presentation;

/// <summary>Immutable validated presentation configuration, independent of request text.</summary>
public sealed class BrowserPresentationCatalog
{
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
            || options.SignOutConfirmation is not ("never" or "when-unsaved" or "always"))
            throw new InvalidDataException("Presentation name, locale or confirmation policy is invalid.");
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
            ["confirm"] = "Continue to sign out. Unsaved changes may be lost."
        };
        if (options.Copy is null || options.Copy.Any(item => !copy.ContainsKey(item.Key) || string.IsNullOrWhiteSpace(item.Value)
            || item.Value.Length > 256 || item.Value.Any(char.IsControl)))
            throw new InvalidDataException("Presentation copy contains an unknown key or invalid bounded text.");
        foreach (var (key, value) in options.Copy) copy[key] = value;
        Copy = copy.ToFrozenDictionary(StringComparer.Ordinal);
        ApplicationName = options.ApplicationName;
        Locale = options.Locale;
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
    /// <summary>Admits literal paths without URI decoding, templates, query strings or protocol-relative targets.</summary>
    internal static bool LocalPath(string value) => value.Length is > 0 and <= 256 && value.StartsWith('/')
        && !value.Contains("//", StringComparison.Ordinal) && !value.Split('/').Any(segment => segment is "." or "..")
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '/' or '-' or '_' or '.');
}
