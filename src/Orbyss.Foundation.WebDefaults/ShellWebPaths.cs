using CShells;

namespace Orbyss.Foundation.WebDefaults;

/// <summary>Admits the native CShells route prefix before using it in authentication protocol paths.</summary>
public static class ShellWebPaths
{
    /// <summary>Returns the canonical literal native route prefix; unsupported URI/template forms fail activation.</summary>
    public static string RoutePrefix(ShellSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var value = settings.GetConfiguration("WebRouting:RoutePrefix")?.Trim().Trim('/');
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Length > 256 || value.Split('/').Any(segment => segment is "" or "." or "..")
            || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('/' or '-' or '_' or '.')))
            throw new InvalidDataException("WebRouting:RoutePrefix must contain literal local route segments.");
        return "/" + value;
    }
}
