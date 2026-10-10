using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.WebDefaults;

/// <summary>Requests narrowly admitted contributions from the final Foundation response-policy writer.</summary>
public static class WebResponseContributions
{
    /// <summary>Stores response-local state under a private identity; applications cannot supply nonce bytes.</summary>
    private static readonly object StateKey = new();

    /// <summary>Gets one cryptographic style-element nonce for this response when its current policy admits it.</summary>
    /// <remarks>Denial, a started response or absence of the Foundation writer returns false. Use the value only in style markup.
    /// The final writer rechecks the policy; a later denial or error discards the contribution. No reload or reusable configured nonce is supported.</remarks>
    public static bool TryGetStyleNonce(this HttpContext context, [NotNullWhen(true)] out string? nonce)
    {
        ArgumentNullException.ThrowIfNull(context);
        nonce = null;
        if (context.Response.HasStarted || !context.Items.TryGetValue(StateKey, out var entry) || entry is not WebResponseNonceState state)
            return false;
        WebResponsePolicy policy;
        try { policy = state.Catalog.Resolve(context.GetEndpoint()); }
        catch (InvalidOperationException) { return false; }
        if (!Admits(policy)) return false;
        nonce = state.Nonce ??= Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return true;
    }

    /// <summary>Installs state owned by the shell's final writer before dispatch.</summary>
    internal static void Initialize(HttpContext context, WebResponsePolicyCatalog catalog) => context.Items[StateKey] = new WebResponseNonceState(catalog);

    /// <summary>Gets whether this response ever issued a nonce, even if its final policy subsequently denies it.</summary>
    internal static bool HasIssuedStyleNonce(HttpContext context) => context.Items.TryGetValue(StateKey, out var entry)
        && entry is WebResponseNonceState { Nonce: not null };

    /// <summary>Composes only the admitted style-element source list; protected and attribute directives stay unchanged.</summary>
    internal static string Compose(HttpContext context, WebResponsePolicy policy)
    {
        if (context.Response.StatusCode >= 400 || !Admits(policy) ||
            !context.Items.TryGetValue(StateKey, out var entry) || entry is not WebResponseNonceState { Nonce: { } nonce })
            return policy.ContentSecurityPolicy;
        var directives = Directives(policy);
        var target = directives.ContainsKey("style-src-elem") ? "style-src-elem" : "style-src";
        if (!directives.ContainsKey(target)) directives[target] = directives["default-src"];
        directives[target] += $" 'nonce-{nonce}'";
        return string.Join("; ", directives.Select(directive => $"{directive.Key} {directive.Value}"));
    }

    /// <summary>Honors an explicit deny on the effective style-element source list, including fallback.</summary>
    private static bool Admits(WebResponsePolicy policy)
    {
        if (!policy.AllowStyleNonce) return false;
        var directives = Directives(policy);
        var sources = directives.GetValueOrDefault("style-src-elem") ?? directives.GetValueOrDefault("style-src") ?? directives["default-src"];
        return !sources.Equals("'none'", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Reads the already validated complete policy.</summary>
    private static Dictionary<string, string> Directives(WebResponsePolicy policy) => policy.ContentSecurityPolicy
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(directive => directive.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries))
        .ToDictionary(parts => parts[0], parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);

}
