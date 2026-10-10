using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.DependencyInjection;
using CShells.Lifecycle;
using Orbyss.Foundation.WebDefaults;
using Orbyss.Foundation.WebDefaults.Probe;
using Orbyss.Foundation.Web.ProblemDetails;

if (args.Contains("--defaults", StringComparer.Ordinal))
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new WebResponsePolicyOptions()));
    return;
}
var configuration = new Dictionary<string, string?>();
foreach (var shell in new[] { "a", "b" })
{
    var root = $"CShells:Shells:{shell}";
    configuration[$"{root}:Features:Orbyss.Foundation.WebDefaults"] = "true";
    configuration[$"{root}:Features:Orbyss.Foundation.Web.ProblemDetails"] = "true";
    configuration[$"{root}:Features:PolicyProbe"] = "true";
    configuration[$"{root}:Features:Orbyss.Foundation.Json.AspNetCore"] = "true";
    configuration[$"{root}:Configuration:Foundation:Json:Profiles:strict-request:MaxBytes"] = shell == "a" ? "256" : "16";
    configuration[$"{root}:Configuration:WebRouting:Path"] = shell;
    configuration[$"{root}:Configuration:Foundation:Web:ResponsePolicies:Policies:default:ReferrerPolicy"] = shell == "a" ? "no-referrer" : "same-origin";
    var policies = $"{root}:Configuration:Foundation:Web:ResponsePolicies:Policies";
    configuration[$"{policies}:indexed:AllowIndexing"] = "true";
    configuration[$"{policies}:nofollow:AllowIndexing"] = "true";
    configuration[$"{policies}:nofollow:AllowFollowing"] = "false";
    configuration[$"{policies}:editor:AllowStyleNonce"] = shell == "a" ? "true" : "false";
    configuration[$"{policies}:editor:PublicAssetMaxAgeSeconds"] = "60";
    configuration[$"{policies}:denied:AllowStyleNonce"] = "true";
    configuration[$"{policies}:denied-public:AllowIndexing"] = "true";
    configuration[$"{policies}:denied-public:PublicAssetMaxAgeSeconds"] = "60";
    configuration[$"{policies}:denied-public:ContentSecurityPolicy"] = "default-src 'self'; style-src 'none'; frame-ancestors 'none'; object-src 'none'; base-uri 'self'";
    configuration[$"{policies}:denied:ContentSecurityPolicy"] = "default-src 'self'; style-src 'none'; frame-ancestors 'none'; object-src 'none'; base-uri 'self'";
}
var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.Configuration.AddInMemoryCollection(configuration);
builder.Services.AddCShellsAspNetCore(shells => shells
    .WithConfigurationProvider(builder.Configuration)
    .WithWebRouting(options => options.EnablePathRouting = true));
await using var app = builder.Build();
app.Urls.Add("http://127.0.0.1:0");
app.MapShells();
foreach (var shell in new[] { "a", "b" }) await app.Services.GetRequiredService<IShellRegistry>().GetOrActivateAsync(shell);
await app.StartAsync();
try
{
    using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    foreach (var shell in new[] { "a", "b" })
    {
        using var response = await client.PostAsync($"/{shell}/json", new StringContent("""{"message":"longer than sixteen bytes"}"""));
        Require((int)response.StatusCode == (shell == "a" ? 200 : 413), "JSON shell limits leaked");
        Require(response.Headers.CacheControl!.ToString() == "no-store", "JSON error cache policy missing");
    }
    var nonces = new HashSet<string>(StringComparer.Ordinal);
    foreach (var shell in new[] { "a", "b" })
    foreach (var route in new[] { "indexed", "nofollow", "noindex", "policy-nofollow", "editor", "denied", "late-denied", "late-private", "nonce-error" })
    {
        using var response = await client.GetAsync($"/{shell}/{route}");
        var body = await response.Content.ReadAsStringAsync();
        var robots = response.Headers.TryGetValues("X-Robots-Tag", out var values) ? values.Single() : null;
        Require(robots == (route switch { "indexed" => null, "nofollow" or "policy-nofollow" => "nofollow", "noindex" or "denied" or "late-denied" => "noindex", _ => "noindex, nofollow" }), $"crawler precedence {shell}/{route}: {robots}");
        Require(response.Headers.CacheControl!.ToString() == "no-store", "private/error nonce cache admission");
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Require(csp.Contains("frame-ancestors 'none'", StringComparison.Ordinal) && csp.Contains("object-src 'none'", StringComparison.Ordinal) && !csp.Contains("unsafe-inline", StringComparison.Ordinal), "authored CSP bypassed final writer");
        if (route == "editor" && shell == "a")
        {
            Require(Convert.FromBase64String(body).Length == 32 && csp.Contains($"'nonce-{body}'", StringComparison.Ordinal), "admitted nonce differs from markup");
            nonces.Add(body);
        }
        else Require(!csp.Contains("'nonce-", StringComparison.Ordinal), "denied/late-denied/error nonce escaped");
        if (route is "editor" or "denied" && (route == "denied" || shell == "b")) Require(body == "denied", "explicit or shell denial ignored");
    }
    using (var lateDeniedPublic = await client.GetAsync("/a/late-denied-public"))
    {
        var html = await lateDeniedPublic.Content.ReadAsStringAsync();
        var match = System.Text.RegularExpressions.Regex.Match(html, "nonce=\"([A-Za-z0-9+/]{43}=)\"");
        Require(match.Success && Convert.FromBase64String(match.Groups[1].Value).Length == 32, "late-denial fixture never issued/rendered a nonce");
        var csp = lateDeniedPublic.Headers.GetValues("Content-Security-Policy").Single();
        Require(csp.Contains("style-src 'none'", StringComparison.Ordinal) && !csp.Contains("'nonce-", StringComparison.Ordinal), "late public policy denial ignored");
        Require(lateDeniedPublic.Headers.CacheControl!.ToString() == "no-store", "issued nonce markup cached after late policy denial and immutable public admission");
    }
    using (var publicNonceResponse = await client.GetAsync("/a/editor-public"))
        Require(publicNonceResponse.Headers.CacheControl!.ToString() == "no-store", "nonce-bearing public response cached");
    for (var repetition = 0; repetition < 3; repetition++)
    {
        using var response = await client.GetAsync("/a/editor");
        var nonce = await response.Content.ReadAsStringAsync();
        Require(nonces.Add(nonce), "nonce reused across responses");
    }
    foreach (var shell in new[] { "a", "b" })
    foreach (var route in new[] { "ok", "asset", "private", "error", "missing-asset", "conflict", "conditional", "unknown", "reject", "cookie" })
    {
        using var response = await client.GetAsync($"/{shell}/{route}");
        var body = await response.Content.ReadAsStringAsync();
        Require(response.Headers.TryGetValues("X-Frame-Options", out var frames) && frames.Single() == "DENY", $"{shell}/{route}: missing framing {response.StatusCode} {body}");
        var cache = response.Headers.CacheControl!.ToString();
        Require(route is "asset" or "conditional" ? cache.Contains("immutable", StringComparison.Ordinal) : cache == "no-store", $"{shell}/{route}: unexpected cache {cache}");
        if (route == "ok")
            Require(response.Headers.GetValues("Referrer-Policy").Single() == (shell == "a" ? "no-referrer" : "same-origin"), "shell isolation failed");
        if (route == "error") Require((int)response.StatusCode == 500, "exception probe did not fail");
        if (route is "private" or "error" or "reject" or "missing-asset" or "unknown")
            Require(response.Headers.GetValues("X-Robots-Tag").Single() == "noindex, nofollow", "private/error crawler policy missing");
    }
}
finally { await app.StopAsync(); }
var options = new WebResponsePoliciesOptions();
options.Policies["feature"] = new() { ReferrerPolicy = "same-origin" };
options.Policies["endpoint"] = new() { ReferrerPolicy = "strict-origin" };
options.FeaturePolicies["example"] = "feature";
var catalog = new WebResponsePolicyCatalog(options);
var featureEndpoint = new Endpoint(null, new EndpointMetadataCollection(new WebResponseMetadata(Feature: "example")), "feature");
Require(catalog.Resolve(featureEndpoint).ReferrerPolicy == "same-origin", "feature selection ignored");
var endpointOverride = new Endpoint(null, new EndpointMetadataCollection(new WebResponseMetadata(Feature: "example"), new WebResponseMetadata(Policy: "endpoint")), "endpoint");
Require(catalog.Resolve(endpointOverride).ReferrerPolicy == "strict-origin", "endpoint precedence ignored");
foreach (var metadata in new[] {
    new EndpointMetadataCollection(new WebResponseMetadata(Policy: "missing")),
    new EndpointMetadataCollection(new WebResponseMetadata(Feature: "one"), new WebResponseMetadata(Feature: "two")),
    new EndpointMetadataCollection(new WebResponseMetadata(Policy: "default"), new WebResponseMetadata(Policy: "private"))
})
{
    try { catalog.Resolve(new Endpoint(null, metadata, "invalid")); throw new Exception("Invalid endpoint selection accepted"); }
    catch (InvalidOperationException) { }
}
options.Policies["default"].ReferrerPolicy = "same-origin";
Require(catalog.Resolve(null).ReferrerPolicy == "no-referrer", "catalog was mutable");
foreach (var bad in new Action<WebResponsePoliciesOptions>[] {
    o => o.DefaultPolicy = "missing",
    o => o.Policies["default"].ContentSecurityPolicy += "; style-src 'nonce-reusable'",
    o => o.Policies["default"].ContentSecurityPolicy += "; style-src 'none' 'self'",
    o => o.Policies["default"].ContentSecurityPolicy += "; style-src-elem 'none' 'nonce-reusable'",
    o => o.Policies["default"].ContentSecurityPolicy = "frame-ancestors *",
    o => o.Policies["default"].PermissionsPolicy = "camera=()\r\nX-Evil: yes",
    o => o.Policies["default"].PermissionsPolicy = "camera=(), camera=(self)",
    o => o.Policies["default"].PermissionsPolicy = "not-a-policy",
    o => o.FeaturePolicies["x"] = "missing"
})
{
    var value = new WebResponsePoliciesOptions(); bad(value);
    try { _ = new WebResponsePolicyCatalog(value); throw new Exception("invalid policy accepted"); }
    catch (InvalidOperationException) { }
}
var inactive = new DefaultHttpContext();
Require(!inactive.TryGetStyleNonce(out _), "nonce without final writer admitted");
foreach (var malformed in new[] { "true", "not-a-bool" })
{
    var settings = new CShells.ShellSettings(new CShells.ShellId("invalid-policies"));
    settings.ConfigurationData["Foundation:Web:ResponsePolicies:Policies:default:AllowStyleNonce"] = malformed;
    settings.ConfigurationData["Foundation:Web:ResponsePolicies:Policies:default:ContentSecurityPolicy"] = "default-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'self'; style-src 'nonce-static'";
    var services = new ServiceCollection();
    new FoundationWebDefaultsFeature(settings).ConfigureServices(services);
    // This isolated invalid-settings fixture deliberately builds its own provider, outside the running shell.
#pragma warning disable ASP0000
    using var provider = services.BuildServiceProvider();
#pragma warning restore ASP0000
    try { _ = provider.GetRequiredService<WebResponsePolicyCatalog>(); throw new Exception("Invalid shell policy activated"); }
    catch (InvalidOperationException) { }
}
foreach (var source in new[] { "style-src-elem 'none'", "style-src 'none'", "" })
{
    var denial = new WebResponsePoliciesOptions();
    denial.Policies["default"].AllowStyleNonce = true;
    denial.Policies["default"].ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; object-src 'none'; base-uri 'self'" + (source.Length == 0 ? "" : "; " + source);
    var middleware = new WebResponsePoliciesMiddleware(context =>
    {
        Require(!context.TryGetStyleNonce(out _), "effective explicit denial ignored");
        return Task.CompletedTask;
    }, new WebResponsePolicyCatalog(denial), []);
    await middleware.InvokeAsync(new DefaultHttpContext());
}
Console.WriteLine("WebDefaults real two-shell response policy probe passed; crawler precedence, response-local nonce admission/denial, final writer, late policy changes and source bounds verified.");
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
