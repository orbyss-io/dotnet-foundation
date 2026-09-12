using System.Collections.Frozen;
using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.WebDefaults;

/// <summary>Compiles shell-local settings into immutable, validated policies.</summary>
public sealed class WebResponsePolicyCatalog
{
    /// <summary>Stores copied policies.</summary>
    private readonly FrozenDictionary<string, WebResponsePolicy> policies;
    /// <summary>Stores copied feature selections.</summary>
    private readonly FrozenDictionary<string, string> features;
    /// <summary>Stores the default selection.</summary>
    private readonly string defaultPolicy;

    /// <summary>Compiles the complete configuration or rejects it before serving requests.</summary>
    public WebResponsePolicyCatalog(WebResponsePoliciesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Policies is null || options.FeaturePolicies is null || options.Policies.Count > 128)
            throw Invalid("Policies", "requires a bounded policy catalog");
        var compiled = new Dictionary<string, WebResponsePolicy>(StringComparer.Ordinal);
        foreach (var (name, value) in options.Policies)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128 ||
                name.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-' or '_')) ||
                value is null) throw Invalid("Policies", "has an invalid identity");
            Validate(name, value);
            compiled.Add(name, new(value.ContentSecurityPolicy, value.ReferrerPolicy, value.PermissionsPolicy,
                value.PublicAssetMaxAgeSeconds, value.AllowIndexing));
        }
        policies = compiled.ToFrozenDictionary(StringComparer.Ordinal);
        defaultPolicy = options.DefaultPolicy;
        if (defaultPolicy is null || !policies.ContainsKey(defaultPolicy)) throw Invalid("DefaultPolicy", "selects an unknown policy");
        features = options.FeaturePolicies.ToFrozenDictionary(StringComparer.Ordinal);
        foreach (var (feature, name) in features)
            if (string.IsNullOrWhiteSpace(feature) || name is null || !policies.ContainsKey(name))
                throw Invalid("FeaturePolicies", "selects an unknown policy");
    }

    /// <summary>Resolves endpoint, feature, and shell selections without merging header fields.</summary>
    public WebResponsePolicy Resolve(Endpoint? endpoint)
    {
        var metadata = endpoint?.Metadata.GetOrderedMetadata<WebResponseMetadata>() ?? [];
        var names = metadata.Where(item => item.Policy is not null).Select(item => item.Policy!).Distinct(StringComparer.Ordinal).ToArray();
        var owners = metadata.Where(item => item.Feature is not null).Select(item => item.Feature!).Distinct(StringComparer.Ordinal).ToArray();
        if (names.Length > 1 || owners.Length > 1) throw Invalid("Endpoint", "has ambiguous selections");
        var name = names.FirstOrDefault() ??
            (owners.Length == 1 && features.TryGetValue(owners[0], out var selected) ? selected : defaultPolicy);
        return policies.TryGetValue(name, out var policy) ? policy : throw Invalid("Endpoint", "selects an unknown policy");
    }

    /// <summary>Rejects injected or contradictory settings without echoing authored values.</summary>
    private static void Validate(string name, WebResponsePolicyOptions value)
    {
        var path = $"Policies:{name}";
        foreach (var header in new[] { value.ContentSecurityPolicy, value.ReferrerPolicy, value.PermissionsPolicy })
            if (string.IsNullOrWhiteSpace(header) || header.Length > 8192 || header.Any(character => character < 32 || character > 126))
                throw Invalid(path, "contains an invalid header");
        if (value.PublicAssetMaxAgeSeconds is < 0 or > 31536000) throw Invalid(path, "has an invalid cache lifetime");
        if (value.ReferrerPolicy is not ("no-referrer" or "same-origin" or "strict-origin" or "strict-origin-when-cross-origin"))
            throw Invalid(path, "has an unsupported referrer policy");
        var permissionNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var permission in value.PermissionsPolicy.Split(',', StringSplitOptions.TrimEntries))
        {
            var match = System.Text.RegularExpressions.Regex.Match(permission,
                """^([a-z][a-z0-9-]*)=\((?:self|(?:"https://[a-zA-Z0-9.-]+(?::[0-9]+)?"(?: "https://[a-zA-Z0-9.-]+(?::[0-9]+)?")*))?\)$""",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (!match.Success || !permissionNames.Add(match.Groups[1].Value))
                throw Invalid(path, "has an invalid or duplicate Permissions-Policy directive");
        }
        var directives = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directive in value.ContentSecurityPolicy.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = directive.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length != 2 || !directives.TryAdd(split[0], split[1].Trim()))
                throw Invalid(path, "has an invalid or duplicate CSP directive");
        }
        if (!directives.TryGetValue("frame-ancestors", out var frames) || frames != "'none'" ||
            !directives.TryGetValue("object-src", out var objects) || objects != "'none'" ||
            !directives.ContainsKey("default-src") || !directives.TryGetValue("base-uri", out var basis) || basis is not ("'self'" or "'none'") ||
            value.ContentSecurityPolicy.Contains("'unsafe-inline'", StringComparison.OrdinalIgnoreCase) ||
            value.ContentSecurityPolicy.Contains("'unsafe-eval'", StringComparison.OrdinalIgnoreCase))
            throw Invalid(path, "must preserve framing, object, base and script restrictions");
    }

    /// <summary>Creates a deterministic configuration diagnostic.</summary>
    private static InvalidOperationException Invalid(string path, string reason) =>
        new($"{WebResponsePoliciesOptions.SectionName}:{path} {reason}.");
}
