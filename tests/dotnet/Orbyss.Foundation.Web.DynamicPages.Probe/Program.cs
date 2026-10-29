using System.Net;
using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.DependencyInjection;
using CShells.Lifecycle;
using Orbyss.Foundation.Web.DynamicPages;

var values = new Dictionary<string, string?>();
var valid = new[] { "a", "b", "custom", "public-only" };
foreach (var name in valid) Configure(values, name);
// Browser documents use the independently selected native HTML problem presentation.
foreach (var name in valid) values["CShells:Shells:" + name + ":Features:Orbyss.Foundation.Web.Presentation"] = "true";
var invalid = new[] { "missing-mode", "unknown-resolver", "private-no-bff", "public-permission", "unknown-setting", "catchall", "unknown-policy", "empty-routes", "sparse-routes", "duplicate-resolver" };
foreach (var name in invalid) Configure(values, name);
Set("missing-mode", "Routes:0:Mode", "");
Set("unknown-resolver", "Routes:0:Resolver", "missing");
values.Remove("CShells:Shells:private-no-bff:Features:Orbyss.Foundation.Authentication.BffCookie");
Set("public-permission", "Routes:0:Permission", "stalls.read");
Set("unknown-setting", "UnsafeHtml", "true");
Set("catchall", "Routes:0:Path", "/stalls/{**slug}");
Set("unknown-policy", "Routes:0:ResponsePolicy", "missing");
foreach (var key in values.Keys.Where(key => key.StartsWith("CShells:Shells:empty-routes:Configuration:Foundation:DynamicPages:Routes:", StringComparison.Ordinal)).ToArray()) values.Remove(key);
foreach (var key in values.Keys.Where(key => key.StartsWith("CShells:Shells:sparse-routes:Configuration:Foundation:DynamicPages:Routes:1:", StringComparison.Ordinal)).ToArray())
{
    values[key.Replace(":Routes:1:", ":Routes:2:", StringComparison.Ordinal)] = values[key];
    values.Remove(key);
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
builder.Logging.ClearProviders();
builder.Configuration.AddInMemoryCollection(values);
builder.Services.AddCShellsAspNetCore(shells => shells.WithConfigurationProvider(builder.Configuration).WithWebRouting(options => options.EnablePathRouting = true));
builder.Services.AddOutputCache(options => options.AddBasePolicy(policy => policy.Cache()));
await using var app = builder.Build();
app.Urls.Add("http://127.0.0.1:0");
app.UseOutputCache();
app.MapShells();
var registry = app.Services.GetRequiredService<IShellRegistry>();
foreach (var name in valid) await registry.GetOrActivateAsync(name);
foreach (var name in invalid)
{
    try { await registry.GetOrActivateAsync(name); throw new Exception("Invalid dynamic configuration activated: " + name); }
    catch (CShells.Lifecycle.ShellGenerationActivationException) { }
}
await app.StartAsync();
if (args is ["--serve"])
{
    Console.WriteLine("DYNAMIC_PAGES_PROBE_URL=" + app.Urls.Single());
    await app.WaitForShutdownAsync();
    return;
}
try
{
    using var client = Client();
    foreach (var name in valid)
    {
        var prefix = "/" + name + "/app";
        using var response = await client.GetAsync(prefix + "/stalls/market");
        var body = await response.Content.ReadAsStringAsync();
        Require(response.StatusCode == HttpStatusCode.OK, "Public request failed: " + body);
        Require(response.Headers.CacheControl?.NoStore == true && response.Headers.Contains("Content-Security-Policy"), "Final page policies missing");
        Require(!response.Headers.Contains("ETag") && response.Content.Headers.ContentLanguage.Single() == "nl", "Dynamic content used an immutable response");
        if (name != "custom")
        {
            Require(body.Contains("&lt;script&gt;", StringComparison.Ordinal) && !body.Contains("<script>window.injected", StringComparison.Ordinal), "Stored content was not encoded");
            Require(body.Contains("href=\"" + prefix + "/stalls/market\"", StringComparison.Ordinal), "Canonical URL lost native prefix");
            Require(body.Contains("src=\"" + prefix + "/application/map.mjs\"", StringComparison.Ordinal), "Application module lost native prefix");
            Require(body.Contains("\\u003C/script", StringComparison.Ordinal) && !body.Contains("</script><script>", StringComparison.Ordinal), "Bootstrap escaped inert JSON");
        }
        else Require(body.Contains("data-custom-layout", StringComparison.Ordinal), "Custom layout was ignored");
        using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, prefix + "/stalls/market"));
        Require(head.StatusCode == HttpStatusCode.OK && head.Content.Headers.ContentLength > 0 && (await head.Content.ReadAsByteArrayAsync()).Length == 0, "HEAD did not perform live admission");
        using var runtime = await client.GetAsync(prefix + "/_foundation/dynamic-pages/v1/page.mjs");
        Require(runtime.StatusCode == HttpStatusCode.OK && runtime.Content.Headers.ContentType?.MediaType == "text/javascript", "Anonymous runtime missing");
    }
    var state = registry.GetActive("a")!.ServiceProvider.GetRequiredService<DynamicFixtureState>();
    state.Published = false;
    using (var hidden = await client.GetAsync("/a/app/stalls/market")) await Rejected(hidden, 404);
    using (var conditional = new HttpRequestMessage(HttpMethod.Get, "/a/app/stalls/market"))
    {
        conditional.Headers.TryAddWithoutValidation("If-None-Match", "*");
        using var response = await client.SendAsync(conditional);
        await Rejected(response, 404);
    }
    using (var other = await client.GetAsync("/b/app/stalls/market")) Require(other.IsSuccessStatusCode, "Publication state crossed shells");
    state.Published = true;
    foreach (var (slug, status) in new[] { ("hidden",404), ("forbidden",403), ("failure",503), ("unknown",503), ("big",503), ("bad-url",503), ("timeout",503) })
    {
        using var response = await client.GetAsync("/a/app/stalls/" + slug);
        await Rejected(response, status);
    }
    foreach (var slug in new[] { "oversized", "throw-renderer" })
    {
        using var response = await client.GetAsync("/custom/app/stalls/" + slug);
        await Rejected(response, 503);
    }
    state.BlockVerification = true;
    var inFlight = client.GetAsync("/a/app/stalls/market");
    await state.VerificationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    state.Published = false;
    state.ContinueVerification.TrySetResult();
    using (var response = await inFlight) await Rejected(response, 404);
    state.BlockVerification = false;
    state.Published = true;
    state.RejectVerification = true;
    using (var response = await client.GetAsync("/a/app/stalls/market")) await Rejected(response, 503);
    state.RejectVerification = false;
    state.ThrowVerification = true;
    using (var response = await client.GetAsync("/a/app/stalls/market")) await Rejected(response, 503);
    state.ThrowVerification = false;
    state.ChangeVersionOnVerify = true;
    using (var response = await client.GetAsync("/a/app/stalls/market")) await Rejected(response, 404);
    state.ChangeVersionOnVerify = false;
    using (var cancellation = new CancellationTokenSource())
    {
        var cancelled = client.GetAsync("/a/app/stalls/cancel", cancellation.Token);
        await state.CancellationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        try { await cancelled; throw new Exception("Caller cancellation became success"); }
        catch (OperationCanceledException) { }
        await state.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
    Require(state.Reads > 10 && state.Verifications > 4 && state.ResolverInstances.Count == state.Reads, "Resolver was cached outside the request scope");
    using var anonymous = Client();
    using (var response = await anonymous.GetAsync("/a/app/workspace/market")) Require(response.StatusCode == HttpStatusCode.Redirect, "Anonymous private page admitted");
    using (var response = await anonymous.GetAsync("/a/app/api/private")) Require(response.StatusCode == HttpStatusCode.Unauthorized && response.Content.Headers.ContentType?.MediaType == "application/problem+json", "Machine admission became HTML");
    using var noPermission = Client();
    await noPermission.GetAsync("/a/app/fixture/session/no");
    using (var response = await noPermission.GetAsync("/a/app/workspace/market")) Require(response.StatusCode == HttpStatusCode.Redirect, "Missing permission admitted");
    using var owner = Client();
    await owner.GetAsync("/a/app/fixture/session/yes");
    using (var response = await owner.GetAsync("/a/app/workspace/market")) Require(response.StatusCode == HttpStatusCode.OK && response.Headers.GetValues("X-Robots-Tag").Single() == "noindex, nofollow", "Authorized private page failed");
    using (var response = await owner.GetAsync("/b/app/workspace/market")) Require(response.StatusCode == HttpStatusCode.Redirect, "Foreign shell cookie admitted");
    Console.WriteLine("Dynamic pages passed: live publication, precommit race, conditional/HEAD requests, scoped resolvers, safe metadata/bootstrap, budgets/deadline, native BFF/permissions, public-only composition, shell isolation and custom layout.");
}
finally { await app.StopAsync(); }

void Set(string name, string path, string? value) => values["CShells:Shells:" + name + ":Configuration:Foundation:DynamicPages:" + path] = value;
HttpClient Client() => new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
static async Task Rejected(HttpResponseMessage response, int expected)
{
    var body = await response.Content.ReadAsStringAsync();
    Require((int)response.StatusCode == expected, "Wrong rejection: " + response.StatusCode + " " + body);
    Require(response.Headers.CacheControl?.NoStore == true && !body.Contains("Fresh stall", StringComparison.Ordinal)
        && !body.Contains("private-", StringComparison.Ordinal) && !body.Contains("Fresh ", StringComparison.Ordinal), "Failure disclosed resource/provider content or was cacheable");
}
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void Configure(Dictionary<string, string?> values, string name)
{
    var root = "CShells:Shells:" + name;
    values[root + ":Features:DynamicFixture"] = "true";
    values[root + ":Features:Orbyss.Foundation.Web.DynamicPages"] = "true";
    if (name != "public-only") values[root + ":Features:Orbyss.Foundation.Authentication.BffCookie"] = "true";
    values[root + ":Configuration:WebRouting:Path"] = name;
    values[root + ":Configuration:WebRouting:RoutePrefix"] = "app";
    var web = root + ":Configuration:Foundation:Web";
    values[web + ":Authority"] = "https://identity.example/" + name;
    values[web + ":ClientId"] = "fixture";
    values[web + ":ClientSecret"] = "synthetic-only";
    values[web + ":Audience"] = "fixture";
    values[web + ":AllowHttpForLocalDevelopment"] = "true";
    values[web + ":LogoutLandingPath"] = "/auth/signed-out";
    var dynamic = root + ":Configuration:Foundation:DynamicPages";
    values[dynamic + ":TimeoutSeconds"] = "1";
    values[dynamic + ":Routes:0:Id"] = "stall";
    values[dynamic + ":Routes:0:Path"] = "/stalls/{slug}";
    values[dynamic + ":Routes:0:Resolver"] = "stalls";
    values[dynamic + ":Routes:0:Mode"] = "Public";
    values[dynamic + ":Routes:0:Modules:0"] = "/application/map.mjs";
    values[dynamic + ":Routes:0:Styles:0"] = "/application/map.css";
    if (name != "public-only")
    {
        values[dynamic + ":Routes:1:Id"] = "workspace";
        values[dynamic + ":Routes:1:Path"] = "/workspace/{slug}";
        values[dynamic + ":Routes:1:Resolver"] = "stalls";
        values[dynamic + ":Routes:1:Mode"] = "Authenticated";
        values[dynamic + ":Routes:1:Permission"] = "stalls.read";
    }
}
