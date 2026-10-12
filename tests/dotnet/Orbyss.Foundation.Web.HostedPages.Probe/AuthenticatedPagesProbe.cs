using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CShells;
using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.AspNetCore.Features;
using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Authentication.BffCookie;
using Orbyss.Foundation.Authentication.Core;
using Orbyss.Foundation.Web.Discovery;
using Orbyss.Foundation.Web.HostedPages;
using Orbyss.Foundation.WebDefaults;

/// <summary>Exercises private pages through real shell dispatch and the selected BFF.</summary>
internal static class AuthenticatedPagesProbe
{
    /// <summary>Owns the alpha entry permission.</summary>
    internal const string AlphaPermission = "pages.alpha";
    /// <summary>Owns the beta entry permission.</summary>
    internal const string BetaPermission = "pages.beta";
    /// <summary>Owns the additional bootstrap permission.</summary>
    internal const string BootstrapPermission = "bootstrap.extra";
    /// <summary>Owns the independent data API permission.</summary>
    internal const string DataPermission = "data.read";
    /// <summary>Detects private content leaks in anonymous and exported responses.</summary>
    internal const string PrivateMarker = "FIXTURE_PRIVATE_PAGE";

    /// <summary>Verifies selected-shell access, logout, policies and recreation over HTTP.</summary>
    public static async Task RunAsync(string root, bool serve)
    {
        var configuration = new Dictionary<string, string?>();
        foreach (var shell in new[] { "p", "q" })
        {
            var deployment = Path.Combine(root, shell);
            Directory.CreateDirectory(Path.Combine(deployment, "r1/assets"));
            foreach (var file in Directory.GetFiles(Path.Combine(root, "a", "r1/assets")))
                File.Copy(file, Path.Combine(deployment, "r1/assets", Path.GetFileName(file)));
            var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "a", "manifest.json")))!;
            manifest["revisions"]![0]!["locales"]!["en"]!["title"] = PrivateMarker + "_" + shell;
            manifest["revisions"]![1]!["locales"]!["en"]!["title"] = "Explicit public entry";
            var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
            File.WriteAllBytes(Path.Combine(deployment, "manifest.json"), bytes);
            var key = $"CShells:Shells:{shell}";
            configuration[$"{key}:Features:{FoundationHostedPagesFeature.FeatureName}"] = "true";
            configuration[$"{key}:Features:Orbyss.Foundation.Authentication.BffCookie"] = "true";
            configuration[$"{key}:Features:Orbyss.Foundation.Web.OpenApi"] = "true";
            configuration[$"{key}:Features:Orbyss.Foundation.Web.Discovery"] = "true";
            configuration[$"{key}:Features:Orbyss.Foundation.Web.ProblemDetails"] = "true";
            configuration[$"{key}:Features:HostedApplicationProbe"] = "true";
            configuration[$"{key}:Configuration:WebRouting:Path"] = shell;
            var web = $"{key}:Configuration:{FoundationWebOptions.SectionName}";
            configuration[$"{web}:Authority"] = "https://identity.example/" + shell;
            configuration[$"{web}:ClientId"] = "synthetic-client";
            configuration[$"{web}:ClientSecret"] = "synthetic-secret";
            configuration[$"{web}:Audience"] = "synthetic-api";
            configuration[$"{web}:AllowHttpForLocalDevelopment"] = "true";
            configuration[$"{web}:ResponsePolicies:Policies:entry:ReferrerPolicy"] = "same-origin";
            configuration[$"{web}:ResponsePolicies:Policies:bootstrap:ReferrerPolicy"] = "no-referrer";
            // Even a policy permitting indexing cannot override Private metadata.
            configuration[$"{web}:ResponsePolicies:Policies:entry:AllowIndexing"] = "true";
            configuration[$"{web}:ResponsePolicies:Policies:entry:AllowFollowing"] = "true";
            var hosted = $"{key}:Configuration:{HostedPageOptions.SectionName}";
            configuration[$"{hosted}:Root"] = shell;
            configuration[$"{hosted}:ManifestSha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes));
            configuration[$"{hosted}:CurrentRevision"] = "release-1";
            ConfigureRoute(configuration, hosted, 0, "alpha", AlphaPermission);
            ConfigureRoute(configuration, hosted, 1, "beta", BetaPermission);
            ConfigureRoute(configuration, hosted, 2, "staged", AlphaPermission, BootstrapPermission);
            ConfigureRoute(configuration, hosted, 3, "session", null);
            ConfigureRoute(configuration, hosted, 4, "public", null, isPublic: true);
            configuration[$"{hosted}:Routes:4:Revision"] = "release-0";
            ConfigureRoute(configuration, hosted, 5, "mixed", null, BootstrapPermission, isPublic: true);
            configuration[$"{hosted}:Routes:5:Revision"] = "release-0";
            configuration[$"{hosted}:Routes:5:Bootstrap:Mode"] = "Authenticated";
        }
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, EnvironmentName = Environments.Development });
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddCShellsAspNetCore(shells => shells.WithConfigurationProvider(builder.Configuration).WithWebRouting(options => options.EnablePathRouting = true));
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapShells();
        var registry = app.Services.GetRequiredService<IShellRegistry>();
        foreach (var shell in new[] { "p", "q" }) await registry.GetOrActivateAsync(shell);
        await app.StartAsync();
        using var anonymous = Client(app);
        using var none = Client(app);
        using var alpha = Client(app);
        using var beta = Client(app);
        using var full = Client(app);
        foreach (var (client, identity) in new[] { (none, "none"), (alpha, "alpha"), (beta, "beta"), (full, "full") })
            Require((await client.GetAsync("/p/fixture/session/" + identity)).IsSuccessStatusCode, "fixture cookie sign-in failed");

        using var allowed = await alpha.GetAsync("/p/alpha");
        CheckPrivate(allowed, HttpStatusCode.OK, "same-origin");
        var html = await allowed.Content.ReadAsStringAsync();
        Require(html.Contains(PrivateMarker + "_p", StringComparison.Ordinal) && html.Contains("data-credentials=\"same-origin\"", StringComparison.Ordinal), "private content or credential mode missing");
        var bootstrap = BootstrapUrl(html);
        using var betaPage = await beta.GetAsync("/p/beta");
        var betaBootstrap = BootstrapUrl(await betaPage.Content.ReadAsStringAsync());
        Require(bootstrap != betaBootstrap && bootstrap.Replace("/alpha/", "/beta/", StringComparison.Ordinal) == betaBootstrap, "same revision routes did not partition bootstrap");

        foreach (var path in new[] { "/p/alpha", "/p/beta", bootstrap, betaBootstrap })
        {
            using var unauthenticated = await anonymous.GetAsync(path);
            CheckPrivate(unauthenticated, HttpStatusCode.Redirect);
            Require(unauthenticated.Headers.Location!.OriginalString.Contains("/p/bff/login", StringComparison.Ordinal), "anonymous entry did not use native BFF challenge");
            Require(!(await unauthenticated.Content.ReadAsStringAsync()).Contains(PrivateMarker, StringComparison.Ordinal), "anonymous private data leaked");
            using var forbidden = await none.GetAsync(path);
            CheckPrivate(forbidden, HttpStatusCode.Redirect);
            Require(forbidden.Headers.Location!.OriginalString.Contains("/p/bff/access-denied", StringComparison.Ordinal), "permission denial did not use native BFF forbid");
        }
        using (var response = await alpha.GetAsync(bootstrap)) CheckPrivate(response, HttpStatusCode.OK, "no-referrer");
        using (var response = await beta.GetAsync(betaBootstrap)) CheckPrivate(response, HttpStatusCode.OK);
        using (var response = await alpha.GetAsync(betaBootstrap)) CheckPrivate(response, HttpStatusCode.Redirect);
        using (var response = await beta.GetAsync(bootstrap)) CheckPrivate(response, HttpStatusCode.Redirect);
        using (var response = await alpha.GetAsync("/p/beta")) CheckPrivate(response, HttpStatusCode.Redirect);
        using (var response = await none.GetAsync("/p/session")) CheckPrivate(response, HttpStatusCode.OK);
        using var staged = await alpha.GetAsync("/p/staged");
        var stagedBootstrap = BootstrapUrl(await staged.Content.ReadAsStringAsync());
        using (var response = await alpha.GetAsync(stagedBootstrap)) CheckPrivate(response, HttpStatusCode.Redirect);
        using (var response = await full.GetAsync(stagedBootstrap)) CheckPrivate(response, HttpStatusCode.OK);
        using (var response = await full.GetAsync(bootstrap.Replace("/alpha/", "/unknown/", StringComparison.Ordinal))) Require(response.StatusCode == HttpStatusCode.NotFound, "unknown route bootstrap admitted");
        var sharedAlias = bootstrap.Replace("/alpha/", "/", StringComparison.Ordinal);
        using (var response = await full.GetAsync(sharedAlias)) Require(response.StatusCode == HttpStatusCode.NotFound, "legacy shared bootstrap bypass exists in explicit route mode");
        using (var response = await full.GetAsync(betaBootstrap.Replace("/en.json", "/unknown.json", StringComparison.Ordinal))) Require(response.StatusCode == HttpStatusCode.NotFound, "unadmitted locale served");
        using (var response = await full.SendAsync(new HttpRequestMessage(HttpMethod.Head, bootstrap))) { CheckPrivate(response, HttpStatusCode.OK); Require((await response.Content.ReadAsByteArrayAsync()).Length == 0, "private HEAD body leaked"); }

        using var publicPage = await anonymous.GetAsync("/p/public");
        Require(publicPage.IsSuccessStatusCode && !(await publicPage.Content.ReadAsStringAsync()).Contains(PrivateMarker, StringComparison.Ordinal), "explicit public page compatibility failed");
        using (var response = await anonymous.GetAsync(BootstrapUrl(await publicPage.Content.ReadAsStringAsync()))) Require(response.IsSuccessStatusCode, "explicit public bootstrap denied");
        using var mixed = await anonymous.GetAsync("/p/mixed");
        Require(mixed.IsSuccessStatusCode, "independent public HTML admission failed");
        var mixedBootstrap = BootstrapUrl(await mixed.Content.ReadAsStringAsync());
        using (var response = await anonymous.GetAsync(mixedBootstrap)) CheckPrivate(response, HttpStatusCode.Redirect);
        using (var response = await alpha.GetAsync(mixedBootstrap)) CheckPrivate(response, HttpStatusCode.Redirect);
        using (var response = await full.GetAsync(mixedBootstrap)) CheckPrivate(response, HttpStatusCode.OK);
        var urls = Regex.Matches(html, "(?:href|src)=\"([^\"]+)\"").Select(match => WebUtility.HtmlDecode(match.Groups[1].Value));
        foreach (var url in urls)
        {
            using var asset = await anonymous.GetAsync(url);
            Require(asset.IsSuccessStatusCode && asset.Headers.CacheControl!.ToString().Contains("immutable", StringComparison.Ordinal), "private application public immutable asset denied");
            Require(!(await asset.Content.ReadAsStringAsync()).Contains(PrivateMarker, StringComparison.Ordinal), "private marker embedded in public assets");
        }
        var loader = urls.Single(url => url.EndsWith(".js", StringComparison.Ordinal));
        Require((await anonymous.GetStringAsync(loader)).Contains("redirect:'error'", StringComparison.Ordinal), "loader follows authentication redirects");
        using (var response = await anonymous.GetAsync("/p/api/data/owner")) CheckPrivate(response, HttpStatusCode.Unauthorized);
        using (var response = await alpha.GetAsync("/p/api/data/owner")) CheckPrivate(response, HttpStatusCode.Forbidden);
        using (var response = await full.GetAsync("/p/api/data/foreign")) CheckPrivate(response, HttpStatusCode.NotFound);
        using (var response = await full.GetAsync("/p/api/data/owner")) { CheckPrivate(response, HttpStatusCode.OK); Require(await response.Content.ReadAsStringAsync() == "synthetic owned data", "owner data dispatch failed"); }
        using (var response = await full.GetAsync("/p/fixture/error")) { CheckPrivate(response, HttpStatusCode.InternalServerError); Require(!(await response.Content.ReadAsStringAsync()).Contains(PrivateMarker, StringComparison.Ordinal), "private exception leaked"); }
        using (var response = await full.GetAsync("/p/fixture/redirect")) CheckPrivate(response, HttpStatusCode.Redirect);
        using (var response = await anonymous.GetAsync("/p/bff/access-denied")) CheckPrivate(response, HttpStatusCode.Forbidden);

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
        foreach (var endpoint in endpoints.Where(endpoint => endpoint.RoutePattern.RawText is "/p/alpha" or "/p/beta" || endpoint.RoutePattern.RawText!.StartsWith("/p/_foundation/bootstrap/alpha/", StringComparison.Ordinal)))
        {
            Require(endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null && endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0, "protected endpoint contains anonymous admission");
            Require(endpoint.Metadata.GetMetadata<IExcludeFromDescriptionMetadata>()?.ExcludeFromDescription == true, "private endpoint exported");
        }
        var descriptions = registry.GetActive("p")!.ServiceProvider.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items.SelectMany(group => group.Items);
        Require(!descriptions.Any(description => description.RelativePath?.Contains("_foundation/bootstrap", StringComparison.Ordinal) == true || description.RelativePath is "alpha" or "beta"), "ApiExplorer exports private hosting");
        using (var response = await full.GetAsync("/p/_orbyss-foundation/openapi/v1.json"))
        {
            var exported = await response.Content.ReadAsStringAsync();
            Require(response.IsSuccessStatusCode && !exported.Contains("_foundation/bootstrap", StringComparison.Ordinal) && !exported.Contains(PrivateMarker, StringComparison.Ordinal) && !exported.Contains("\"/alpha\"", StringComparison.Ordinal), "OpenAPI leaked private page/bootstrap content");
        }
        using (var response = await anonymous.GetAsync("/p/sitemap.xml")) Require(response.IsSuccessStatusCode && !(await response.Content.ReadAsStringAsync()).Contains(PrivateMarker, StringComparison.Ordinal), "public discovery leaked hosted deployment");
        using (var response = await alpha.GetAsync("/q/alpha")) CheckPrivate(response, HttpStatusCode.Redirect);

        using var tokensResponse = await alpha.GetAsync("/p/bff/antiforgery");
        var token = JsonNode.Parse(await tokensResponse.Content.ReadAsStringAsync())!["requestToken"]!.GetValue<string>();
        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/p/bff/logout");
        logoutRequest.Headers.Add(FoundationBffCookieFeature.AntiforgeryHeader, token);
        using (var logout = await alpha.SendAsync(logoutRequest)) CheckPrivate(logout, HttpStatusCode.Redirect);
        using (var response = await alpha.GetAsync("/p/alpha")) CheckPrivate(response, HttpStatusCode.Redirect);
        using (var response = await alpha.GetAsync(bootstrap)) CheckPrivate(response, HttpStatusCode.Redirect);
        Console.WriteLine("Authenticated selected-shell HTTP checks passed: native BFF session/denial/logout, route-specific conjunctive bootstrap, separate API permission/ownership, public immutable assets, final headers, discovery/OpenAPI exclusion and cross-shell cookie isolation.");

        // A new generation picks up a policy selection, revokes old session state and owns new route names.
        builder.Configuration["CShells:Shells:p:Configuration:Foundation:HostedPages:Routes:0:Page:Permission"] = BetaPermission;
        var reload = await registry.ReloadAsync("p");
        Require(reload.Error is null && reload.NewShell is not null && reload.Drain is not null, "shell recreation failed");
        await reload.Drain!.WaitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        using (var response = await full.GetAsync("/p/alpha")) CheckPrivate(response, HttpStatusCode.Redirect);
        await alpha.GetAsync("/p/fixture/session/alpha");
        using (var response = await alpha.GetAsync("/p/alpha")) CheckPrivate(response, HttpStatusCode.Redirect);
        await beta.GetAsync("/p/fixture/session/beta");
        using (var response = await beta.GetAsync("/p/alpha")) { CheckPrivate(response, HttpStatusCode.OK); Require(BootstrapUrl(await response.Content.ReadAsStringAsync()) == bootstrap, "shell recreation changed route-specific URLs"); }
        using (var response = await beta.GetAsync(bootstrap)) CheckPrivate(response, HttpStatusCode.OK);
        builder.Configuration["CShells:Shells:p:Configuration:Foundation:HostedPages:Routes:0:Page:Permission"] = AlphaPermission;
        reload = await registry.ReloadAsync("p");
        Require(reload.Error is null, "shell reset failed");
        await reload.Drain!.WaitAsync();
        Console.WriteLine("Shell recreation revoked sessions, updated page plus bootstrap admission and preserved shell-local URL generation.");
        await VerifyInvalidSelectionsAsync(root, configuration);
        if (serve)
        {
            Console.WriteLine("PRIVATE_BROWSER_PROBE_URL=" + app.Urls.Single());
            await Task.Delay(Timeout.Infinite);
        }
        await app.StopAsync();
    }

    /// <summary>Stops the probe at the first failed behavioral assertion.</summary>
    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    /// <summary>Builds explicit route selections in shell configuration.</summary>
    private static void ConfigureRoute(Dictionary<string, string?> configuration, string hosted, int index, string id, string? permission, string? bootstrapPermission = null, bool isPublic = false)
    {
        var route = $"{hosted}:Routes:{index}";
        configuration[$"{route}:Id"] = id;
        configuration[$"{route}:Path"] = "/" + id;
        configuration[$"{route}:Page:Mode"] = isPublic ? "Public" : "Authenticated";
        configuration[$"{route}:Bootstrap:Mode"] = isPublic ? "Public" : "Authenticated";
        configuration[$"{route}:Page:ResponsePolicy"] = "entry";
        configuration[$"{route}:Bootstrap:ResponsePolicy"] = "bootstrap";
        if (permission is not null) configuration[$"{route}:Page:Permission"] = permission;
        if (bootstrapPermission is not null) configuration[$"{route}:Bootstrap:Permission"] = bootstrapPermission;
    }
    /// <summary>Creates a client retaining native cookies without following redirects.</summary>
    private static HttpClient Client(WebApplication app) => new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
    /// <summary>Reads the generated route-specific bootstrap address.</summary>
    private static string BootstrapUrl(string html) => WebUtility.HtmlDecode(Regex.Match(html, "data-bootstrap=\"([^\"]+)\"").Groups[1].Value);
    /// <summary>Checks status and final framework-owned headers.</summary>
    private static void CheckPrivate(HttpResponseMessage response, HttpStatusCode expected, string? referrer = null)
    {
        Require(response.StatusCode == expected, $"{response.RequestMessage!.RequestUri}: expected {expected}, received {response.StatusCode}");
        Require(response.Headers.CacheControl?.NoStore == true && response.Headers.GetValues("X-Robots-Tag").Single() == "noindex, nofollow", "private cache/crawler headers missing");
        Require(response.Headers.GetValues("X-Frame-Options").Single() == "DENY" && response.Headers.GetValues("X-Content-Type-Options").Single() == "nosniff" && response.Headers.Contains("Content-Security-Policy") && response.Headers.Contains("Permissions-Policy"), "final managed security headers missing");
        if (referrer is not null) Require(response.Headers.GetValues("Referrer-Policy").Single() == referrer, "configured policy selection lost");
    }
    /// <summary>Requires invalid compositions to fail before endpoints become available.</summary>
    private static async Task VerifyInvalidSelectionsAsync(string root, Dictionary<string, string?> configuration)
    {
        const string hosted = "CShells:Shells:bad:Configuration:Foundation:HostedPages";
        var baseline = configuration.Where(pair => pair.Key.StartsWith("CShells:Shells:p:", StringComparison.Ordinal)).ToDictionary(pair => pair.Key.Replace("Shells:p:", "Shells:bad:", StringComparison.Ordinal), pair => pair.Value);
        baseline["CShells:Shells:bad:Configuration:WebRouting:Path"] = "bad";
        foreach (var change in new Action<Dictionary<string, string?>>[] {
            c => c[$"{hosted}:Routes:0:Page:Mode"] = "Unspecified",
            c => c[$"{hosted}:Routes:0:Bootstrap:Mode"] = "Public",
            c => c[$"{hosted}:Routes:0:Page:Mode"] = "Public",
            c => c[$"{hosted}:Routes:0:Page:Permission"] = " ",
            c => c[$"{hosted}:Routes:0:Page:ResponsePolicy"] = "missing",
            c => c[$"{hosted}:AssetResponsePolicy"] = "missing",
            c => c[$"{hosted}:Routes:0:Revision"] = "absent",
            c => c[$"{hosted}:Routes:1:Id"] = "ALPHA",
            c => c[$"{hosted}:Routes:1:Path"] = "/ALPHA",
            c => c[$"{hosted}:Routes:0:Path"] = "/{capture}",
            c => c[$"{hosted}:Routes:0:Path"] = "/_FOUNDATION/hidden",
            c => c[$"{hosted}:Routes:0:Path"] = "/alpha/",
            c => c[$"{hosted}:PagePath"] = "/",
            c => c[$"{hosted}:Routes:0:Page:Unexpected"] = "true",
            c => { foreach (var key in c.Keys.Where(key => key.StartsWith(hosted + ":Routes:0:", StringComparison.Ordinal)).ToArray()) c.Remove(key); },
            c => { foreach (var key in c.Keys.Where(key => key.Contains(":Routes:", StringComparison.Ordinal)).ToArray()) c.Remove(key); c[$"{hosted}:Routes"] = ""; },
            c => c["CShells:Shells:bad:Features:Orbyss.Foundation.Authentication.BffCookie"] = "false",
            c => { foreach (var key in c.Keys.Where(key => key.Contains(":Routes:", StringComparison.Ordinal)).ToArray()) c.Remove(key); c[$"{hosted}:Routes"] = null; }
        })
        {
            var selected = new Dictionary<string, string?>(baseline);
            change(selected);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, EnvironmentName = Environments.Development });
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(selected);
            builder.Services.AddCShellsAspNetCore(shells => shells.WithConfigurationProvider(builder.Configuration).WithWebRouting(options => options.EnablePathRouting = true));
            await using var app = builder.Build();
            app.MapShells();
            var failed = false;
            try { await app.Services.GetRequiredService<IShellRegistry>().GetOrActivateAsync("bad"); }
            catch (Exception error) when (error is ShellGenerationActivationException or InvalidDataException or InvalidOperationException) { failed = true; }
            Require(failed && !((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().Any(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/bad/", StringComparison.Ordinal)), "invalid composition published endpoints");
        }
        Console.WriteLine("18 invalid route/access/policy/revision/BFF/unknown-settings compositions rejected before publishing shell endpoints.");
    }
}
