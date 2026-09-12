using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.DependencyInjection;
using CShells.Lifecycle;
using Orbyss.Foundation.WebDefaults;
using Orbyss.Foundation.WebDefaults.Probe;
using Orbyss.Foundation.Web.ProblemDetails;

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
Console.WriteLine("WebDefaults real two-shell response policy probe passed.");
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
