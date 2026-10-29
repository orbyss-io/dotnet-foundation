using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Json;

namespace Orbyss.Foundation.Web.DynamicPages;

/// <summary>Admits and copies deployment routing and rendering limits, never resource content or eligibility.</summary>
internal sealed class DynamicPageCatalog
{
    /// <summary>Bounds inert bootstrap serialization independently of application JSON extensions.</summary>
    private readonly JsonProfile bootstrapProfile;
    /// <summary>Admits configuration and exact resolver identities before publishing endpoints.</summary>
    public DynamicPageCatalog(DynamicPageOptions options, IEnumerable<DynamicPageResolverRegistration> registrations)
    {
        if (options.Routes is null || options.Routes.Count is < 1 or > 64
            || options.MaxResponseBytes is < 512 or > 16_777_216
            || options.MaxBootstrapBytes < 1 || options.MaxBootstrapBytes > options.MaxResponseBytes
            || options.TimeoutSeconds is < 1 or > 30)
            throw new InvalidDataException("Dynamic page routes and response budgets are invalid.");
        var resolverMap = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var registration in registrations)
            if (!resolverMap.TryAdd(registration.Id, registration.Implementation))
                throw new InvalidDataException("Dynamic page resolver identities must be unique.");
        Resolvers = resolverMap.ToFrozenDictionary(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var routes = new List<DynamicPageRouteOptions>();
        foreach (var route in options.Routes)
        {
            if (route is null || !Identifier(route.Id) || !ids.Add(route.Id)
                || !RoutePath(route.Path) || !paths.Add(route.Path)
                || !Resolvers.ContainsKey(route.Resolver) || !Identifier(route.ResponsePolicy)
                || route.Mode is not ("Public" or "Authenticated")
                || route.Mode == "Public" && route.Permission is not null)
                throw new InvalidDataException("Dynamic page route selection is invalid.");
            if (route.Permission is not null) _ = PermissionPolicyNames.ForPermission(route.Permission);
            if (route.Modules is null || route.Styles is null || route.Modules.Length > 16 || route.Styles.Length > 16
                || route.Modules.Concat(route.Styles).Any(path => !LocalPath(path)))
                throw new InvalidDataException("Dynamic page assets require bounded local deployment paths.");
            routes.Add(new DynamicPageRouteOptions
            {
                Id = route.Id, Path = route.Path, Resolver = route.Resolver, Mode = route.Mode,
                Permission = route.Permission, ResponsePolicy = route.ResponsePolicy, NoIndex = route.NoIndex,
                Modules = route.Modules.ToArray(), Styles = route.Styles.ToArray()
            });
        }
        Routes = routes.AsReadOnly();
        MaxResponseBytes = options.MaxResponseBytes;
        TimeoutSeconds = options.TimeoutSeconds;
        bootstrapProfile = new JsonProfile(new JsonProfileSettings { Preset = JsonProfileKeys.TolerantResponse, MaxBytes = options.MaxBootstrapBytes });
    }
    /// <summary>Gets copied route definitions.</summary>
    public IReadOnlyList<DynamicPageRouteOptions> Routes { get; }
    /// <summary>Gets exact code-owned resolver types.</summary>
    public FrozenDictionary<string, Type> Resolvers { get; }
    /// <summary>Gets the full response budget.</summary>
    public int MaxResponseBytes { get; }
    /// <summary>Gets the cooperative request budget.</summary>
    public int TimeoutSeconds { get; }
    /// <summary>Copies and context-admits a single current resource model before invoking a renderer.</summary>
    public AdmittedDynamicPage Admit(DynamicPageDocument model, DynamicPageRouteOptions route, string pathBase, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        foreach (var text in new[] { model.Title, model.Description, model.Heading, model.Text })
            if (text is null || text.Length > 16384 || text.Any(character => char.IsControl(character) && character is not ('\n' or '\r' or '\t')))
                throw new InvalidDataException("Dynamic page text exceeds its admitted contract.");
        if (string.IsNullOrWhiteSpace(model.Title) || model.Locale is null || model.Locale.Length is < 1 or > 64
            || CultureInfo.GetCultureInfo(model.Locale).Name != model.Locale
            || model.CanonicalPath is not null && !LocalPath(model.CanonicalPath))
            throw new InvalidDataException("Dynamic page metadata is invalid.");
        var data = model.Bootstrap ?? JsonSerializer.SerializeToElement(new Dictionary<string, string>());
        var bytes = bootstrapProfile.Serialize(data, bootstrapProfile.TypeInfo<JsonElement>(), cancellationToken);
        // Defense in depth: JsonElement and future serializer changes cannot create a script-end delimiter.
        var json = Encoding.UTF8.GetString(bytes).Replace("<", "\\u003C", StringComparison.Ordinal)
            .Replace(">", "\\u003E", StringComparison.Ordinal).Replace("&", "\\u0026", StringComparison.Ordinal);
        var canonical = model.CanonicalPath is null ? null : pathBase + model.CanonicalPath;
        var modules = Array.AsReadOnly(route.Modules.Select(path => pathBase + path).ToArray());
        var styles = Array.AsReadOnly(route.Styles.Select(path => pathBase + path).ToArray());
        return new AdmittedDynamicPage(model, json, canonical, modules, styles);
    }
    /// <summary>Admits finite code/configuration identities.</summary>
    internal static bool Identifier(string? value) => value is { Length: > 0 and <= 128 }
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
    /// <summary>Admits literal local asset/canonical paths without URI decoding, query targets or traversal.</summary>
    internal static bool LocalPath(string? value) => value is { Length: > 0 and <= 2048 } && value.StartsWith('/')
        && !value.Contains("//", StringComparison.Ordinal) && !value.Split('/').Any(segment => segment is "." or "..")
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '/' or '-' or '_' or '.');
    /// <summary>Admits simple required route parameters; catchalls, optional/default parameters and reserved protocol routes are excluded.</summary>
    private static bool RoutePath(string? value)
    {
        if (value is not { Length: > 0 and <= 256 }) return false;
        var literal = Regex.Replace(value, "\\{[A-Za-z][A-Za-z0-9_]*\\}", "parameter", RegexOptions.CultureInvariant);
        return LocalPath(literal) && (literal == "/" || !literal.EndsWith('/'))
            && !new[] { "/api", "/bff", "/_foundation", "/signin", "/signout" }
                .Any(prefix => literal.Equals(prefix, StringComparison.OrdinalIgnoreCase) || literal.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase));
    }
}
