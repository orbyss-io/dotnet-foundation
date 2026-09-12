using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.DependencyInjection;
using CShells.Lifecycle;
using Orbyss.Foundation.Web.HostedPages;
using Orbyss.Foundation.WebDefaults;

var root = Path.Combine(Path.GetTempPath(), "orbyss-hosted-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var configuration = new Dictionary<string, string?>();
    foreach (var shell in new[] { "a", "b" })
    {
        var directory = Path.Combine(root, shell);
        Directory.CreateDirectory(Path.Combine(directory, "r1", "assets"));
        var content = new Dictionary<string, (string Type, string Text)> {
            ["r1/assets/app.js"] = ("text/javascript; charset=utf-8", "import {label} from './chunk.js'; export async function mount(target,bootstrap){ const lazy = await import('./lazy.js'); target.textContent=bootstrap.title + label + lazy.value; }"),
            ["r1/assets/chunk.js"] = ("text/javascript; charset=utf-8", "export const label = ' loaded';"),
            ["r1/assets/lazy.js"] = ("text/javascript; charset=utf-8", "export const value = ' dynamically';"),
            ["r1/assets/main.css"] = ("text/css; charset=utf-8", "main { border: 1px solid rgb(1, 2, 3); }"),
            ["r1/assets/imported.css"] = ("text/css; charset=utf-8", "#foundation-form { min-height: 20px; }")
        };
        var assets = new List<HostedAssetDescriptor>();
        if (shell == "b") content["r1/assets/app.js"] = ("text/javascript; charset=utf-8", "export async function mount(){ throw new Error('fixture failure'); }");
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/iZk9HQAAAABJRU5ErkJggg==");
        File.WriteAllBytes(Path.Combine(directory, "r1/assets/logo.png"), png);
        assets.Add(new() { Id = "logo", File = "r1/assets/logo.png", ContentType = "image/png", Sha256 = Convert.ToHexStringLower(SHA256.HashData(png)), Kind = "branding", Visibility = "public" });
        var index = 0;
        foreach (var (path, item) in content)
        {
            var data = Encoding.UTF8.GetBytes(item.Text);
            File.WriteAllBytes(Path.Combine(directory, path), data);
            assets.Add(new() { Id = "asset-" + index++, File = path, ContentType = item.Type, Sha256 = Convert.ToHexStringLower(SHA256.HashData(data)), Kind = "runtime", Visibility = "public" });
        }
        var locales = new Dictionary<string, HostedBrandLocale> {
            ["en"] = new() { Title = shell + """ <script>window.pwned=true</script> " &""", Purpose = "A useful form", LogoAlt = "", LoadingText = "Loading form", FailureText = "Form unavailable", NoScriptText = "Enable JavaScript to use this form." },
            ["nl"] = new() { Title = "Nederlands", Purpose = "Een nuttig formulier", LogoAlt = "", LoadingText = "Formulier laden", FailureText = "Formulier niet beschikbaar", NoScriptText = "Schakel JavaScript in." }
        };
        var revision = new HostedPageRevision {
            Id = "release-1", FormReleaseId = "forms-1", DefaultLocale = "en", Locales = locales, Assets = assets, Entry = "src/main.ts",
            Vite = new() {
                ["src/main.ts"] = new() { File = "r1/assets/app.js", IsEntry = true, Imports = ["_chunk"], DynamicImports = ["_lazy"], Css = ["r1/assets/main.css"] },
                ["_chunk"] = new() { File = "r1/assets/chunk.js", Css = ["r1/assets/imported.css"] },
                ["_lazy"] = new() { File = "r1/assets/lazy.js", IsDynamicEntry = true }
            }
        };
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new HostedPageManifest { Version = "1", CurrentRevision = "release-1", Revisions = [revision] }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var retainedManifest = JsonNode.Parse(manifest)!;
        var oldRevision = retainedManifest["revisions"]![0]!.DeepClone();
        oldRevision["id"] = "release-0";
        retainedManifest["revisions"]!.AsArray().Add(oldRevision);
        manifest = Encoding.UTF8.GetBytes(retainedManifest.ToJsonString());
        File.WriteAllBytes(Path.Combine(directory, "manifest.json"), manifest);
        var key = $"CShells:Shells:{shell}";
        configuration[$"{key}:Features:Orbyss.Foundation.Web.HostedPages"] = "true";
        configuration[$"{key}:Features:Orbyss.Foundation.WebDefaults"] = "true";
        configuration[$"{key}:Configuration:WebRouting:Path"] = shell;
        configuration[$"{key}:Configuration:Foundation:Web:SupportedLocales:0"] = "en";
        configuration[$"{key}:Configuration:Foundation:Web:SupportedLocales:1"] = "nl";
        configuration[$"{key}:Configuration:Foundation:HostedPages:Root"] = shell;
        configuration[$"{key}:Configuration:Foundation:HostedPages:ManifestSha256"] = Convert.ToHexStringLower(SHA256.HashData(manifest));
        configuration[$"{key}:Configuration:Foundation:HostedPages:CurrentRevision"] = "release-1";
    }
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root });
    builder.Logging.ClearProviders();
    builder.Configuration.AddInMemoryCollection(configuration);
    builder.Services.AddCShellsAspNetCore(shells => shells.WithConfigurationProvider(builder.Configuration).WithWebRouting(options => options.EnablePathRouting = true));
    await using var app = builder.Build();
    app.Urls.Add("http://127.0.0.1:0");
    app.MapShells();
    foreach (var shell in new[] { "a", "b" }) await app.Services.GetRequiredService<IShellRegistry>().GetOrActivateAsync(shell);
    await app.StartAsync();
    using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    using var page = await client.GetAsync("/a/");
    var html = await page.Content.ReadAsStringAsync();
    Require(page.StatusCode == HttpStatusCode.OK, "page failed");
    Require(html.Contains("&lt;script&gt;", StringComparison.Ordinal) && !html.Contains("<script>window", StringComparison.Ordinal), "hostile title was not encoded");
    Require(html.Contains("<h1>", StringComparison.Ordinal) && html.Contains("<noscript>", StringComparison.Ordinal), "semantic or accessible HTML missing");
    Require(page.Headers.CacheControl!.ToString() == "no-store", "page cache failed");
    var urls = Regex.Matches(html, "(?:href|src|data-bootstrap)=\"([^\"]+)\"").Select(match => WebUtility.HtmlDecode(match.Groups[1].Value)).ToArray();
    Require(urls.Count(url => url.EndsWith(".css", StringComparison.Ordinal)) == 3, "imported CSS omitted");
    Require(urls.All(url => url.StartsWith("/a/_foundation/", StringComparison.Ordinal)), "shell path base missing");
    foreach (var url in urls)
    {
        using var response = await client.GetAsync(url);
        Require(response.StatusCode == HttpStatusCode.OK, "admitted resource missing " + url);
        Require(response.Headers.CacheControl!.ToString().Contains("immutable", StringComparison.Ordinal), "immutable cache missing");
        if (url.Contains("/assets/", StringComparison.Ordinal))
        {
            using var wrong = await client.GetAsync("/b" + url[2..]);
            Require(wrong.StatusCode == HttpStatusCode.NotFound, "cross-shell asset leaked");
            using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            Require((await head.Content.ReadAsByteArrayAsync()).Length == 0, "HEAD returned a body");
            using var conditional = new HttpRequestMessage(HttpMethod.Get, url);
            conditional.Headers.IfNoneMatch.Add(response.Headers.ETag!);
            using var notModified = await client.SendAsync(conditional);
            Require(notModified.StatusCode == HttpStatusCode.NotModified, "ETag conditional failed");
        }
    }
    var bootstrapUrl = urls.Single(url => url.EndsWith(".json", StringComparison.Ordinal));
    var manifestProfile = new Orbyss.Foundation.Json.JsonProfile(new() { MaxBytes = 2_000_000 });
    var admittedManifest = manifestProfile.Deserialize<HostedPageManifest>(File.ReadAllBytes(Path.Combine(root, "a", "manifest.json")));
    var retained = admittedManifest.Revisions.Single(revision => revision.Id == "release-0");
    var retainedHash = Convert.ToHexStringLower(SHA256.HashData(Orbyss.Foundation.Json.JsonCanonicalizer.Canonicalize(manifestProfile.Serialize(retained), maxBytes: 2_000_000)));
    using var oldBootstrap = await client.GetAsync("/a/_foundation/bootstrap/" + retainedHash + "/en.json");
    Require(oldBootstrap.IsSuccessStatusCode, "retained revision was removed");
    Require((await oldBootstrap.Content.ReadAsStringAsync()).Contains("release-0", StringComparison.Ordinal), "retained bootstrap revision mixed");
    var bootstrap = JsonNode.Parse(await client.GetStringAsync(bootstrapUrl))!;
    var runtime = (string)bootstrap["runtime"]!;
    Require((await client.GetAsync(runtime)).IsSuccessStatusCode, "runtime entry unavailable");
    Require(!bootstrap.AsObject().ContainsKey("private") && bootstrap["formReleaseId"]!.GetValue<string>() == "forms-1", "public projection failed");
    File.WriteAllText(Path.Combine(root, "a", "r1/assets/app.js"), "mutated after activation");
    Require((await client.GetStringAsync(runtime)).Contains("export async function mount", StringComparison.Ordinal), "serving bytes mutated after admission");
    using var languageRequest = new HttpRequestMessage(HttpMethod.Get, "/a/");
    languageRequest.Headers.AcceptLanguage.ParseAdd("en;q=0.1, nl-NL;q=0.9");
    using var localized = await client.SendAsync(languageRequest);
    Require((await localized.Content.ReadAsStringAsync()).Contains("<h1>Nederlands</h1>", StringComparison.Ordinal), "language quality/parent fallback failed");
    using var missing = await client.GetAsync("/a/_foundation/assets/missing/private.json");
    Require(missing.StatusCode == HttpStatusCode.NotFound && missing.Headers.CacheControl!.ToString() == "no-store", "missing/private asset policy failed");
    Console.WriteLine("Hosted-page two-shell HTTP probe passed: encoded HTML, Vite imports, snapshot isolation, languages, HEAD/ETag, wrong-shell 404.");
    if (args.Contains("--serve"))
    {
        Console.WriteLine("BROWSER_PROBE_URL=" + app.Urls.Single() + "/a/");
        await Task.Delay(Timeout.Infinite);
    }
    await app.StopAsync();

    // Source admission negatives do not expose arbitrary paths or linked roots.
    using var source = new LocalHostedAssetSource(root, "a");
    foreach (var bad in new[] { "../b/manifest.json", "/etc/passwd", "C:/secret", "r1/../manifest.json", "r1/%2e%2e/private", "r1/CON", "r1/assets/app.js:stream" })
    {
        try { using var ignored = source.OpenRead(bad); throw new Exception("Unsafe path accepted: " + bad); }
        catch (InvalidDataException) { }
    }
    Console.WriteLine("Hosted asset source traversal/encoding/alternate-stream negatives passed.");
    // Exercise manifest validation with a matching outer hash, so each inner check is reached.
    var originalManifest = File.ReadAllBytes(Path.Combine(root, "a", "manifest.json"));
    File.WriteAllText(Path.Combine(root, "a", "r1/assets/app.js"), "import {label} from './chunk.js'; export async function mount(target,bootstrap){ const lazy = await import('./lazy.js'); target.textContent=bootstrap.title + label + lazy.value; }", new UTF8Encoding(false));
    foreach (var mutate in new Action<JsonObject>[] {
        m => m["version"] = "2",
        m => m["currentRevision"] = "other",
        m => m["revisions"]![0]!["theme"] = "url(javascript:evil)",
        m => m["revisions"]![0]!["assets"]![0]!["file"] = "../b/manifest.json",
        m => m["revisions"]![0]!["assets"]![0]!["visibility"] = "private",
        m => m["revisions"]![0]!["assets"]![0]!["contentType"] = "text/html",
        m => m["revisions"]![0]!["assets"]![0]!["sha256"] = new string('0',64),
        m => m["revisions"]![0]!["vite"]!["_chunk"] = null,
        m => m["revisions"]![0]!["vite"]!["src/main.ts"]!["imports"] = new JsonArray("absent"),
        m => m["revisions"]![0]!["logoAssetId"] = "missing",
        m => m["revisions"]![0]!["privateResults"] = "secret"
    })
    {
        var mutated = JsonNode.Parse(originalManifest)!.AsObject();
        mutate(mutated);
        var manifestBytes = Encoding.UTF8.GetBytes(mutated.ToJsonString());
        File.WriteAllBytes(Path.Combine(root, "a", "manifest.json"), manifestBytes);
        var invalidBuilder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root });
        invalidBuilder.Logging.ClearProviders();
        var feature = new FoundationHostedPagesFeature(new CShells.ShellSettings(new CShells.ShellId("invalid"), ["Orbyss.Foundation.Web.HostedPages"]));
        feature.ConfigureServices(invalidBuilder.Services);
        invalidBuilder.Services.Configure<HostedPageOptions>(o => { o.Root="a"; o.CurrentRevision="release-1"; o.ManifestSha256=Convert.ToHexStringLower(SHA256.HashData(manifestBytes)); });
        await using var invalidApp = invalidBuilder.Build();
        try { feature.MapEndpoints(invalidApp, invalidApp.Environment); throw new Exception("Invalid manifest accepted"); }
        catch (Exception error) when (error is InvalidDataException or Orbyss.Foundation.Json.JsonProfileException) { }
    }
    File.WriteAllBytes(Path.Combine(root, "a", "manifest.json"), originalManifest);
    var link = Path.Combine(root, "linked");
    try
    {
        Directory.CreateSymbolicLink(link, Path.Combine(root, "a"));
        try { using var ignored = new LocalHostedAssetSource(root, "linked"); throw new Exception("Linked root accepted"); }
        catch (InvalidDataException) { }
        Directory.Delete(link);
        Console.WriteLine("Linked-root rejection passed.");
    }
    catch (UnauthorizedAccessException) { Console.WriteLine("Symbolic-link creation unavailable; run this case on Linux CI."); }
    Console.WriteLine("Malformed/incoherent manifest, public/private, MIME/hash and Vite dependency negatives passed.");
}
finally
{
    var resolved = Path.GetFullPath(root);
    Require(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) &&
        Path.GetFileName(resolved).StartsWith("orbyss-hosted-", StringComparison.Ordinal), "Unsafe probe cleanup target");
    Directory.Delete(resolved, recursive: true);
}
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
