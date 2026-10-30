using System.Net;
using System.Text.RegularExpressions;
using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.DependencyInjection;
using CShells.Lifecycle;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Exercises shipped presentation with the native BFF and synthetic provider transport.</summary>
internal static class BrowserPresentationProbe
{
    internal static async Task RunAsync(bool serve = false)
    {
        var values = new Dictionary<string, string?>();
        var names = new[] { "pages", "pages-a", "pages-failure" };
        foreach (var name in names)
        {
            var root = "CShells:Shells:" + name;
            foreach (var feature in new[] { "Orbyss.Foundation.Authentication.BffCookie", "Orbyss.Foundation.Web.Presentation", "RecoveryProbe" })
                values[root + ":Features:" + feature] = "true";
            values[root + ":Configuration:WebRouting:Path"] = name == "pages" ? "" : name;
            if (name == "pages-a") values[root + ":Configuration:WebRouting:RoutePrefix"] = "app";
            var web = root + ":Configuration:Foundation:Web";
            values[web + ":Authority"] = "https://identity.example/" + name;
            values[web + ":ClientId"] = "probe-" + name;
            values[web + ":ClientSecret"] = "synthetic-only";
            values[web + ":Audience"] = "fixture-api";
            values[web + ":AllowHttpForLocalDevelopment"] = "true";
            values[web + ":LogoutLandingPath"] = "/auth/signed-out";
            values[web + ":Presentation:ApplicationName"] = name == "pages-a" ? "Synthetic <Application>" : "Synthetic application";
            values[web + ":Presentation:Locale"] = name == "pages-a" ? "nl" : "en";
            values[web + ":Presentation:Theme"] = name == "pages-a" ? "green" : name == "pages-failure" ? "slate" : "blue";
            values[web + ":Presentation:Copy:sign-in"] = name == "pages-a" ? "Aanmelden" : "Sign in";
            if (name != "pages-failure") values[web + ":Presentation:SignOutConfirmation"] = name == "pages-a" ? "always" : "never";
        }
        var collisionRoot = "CShells:Shells:pages-collision";
        foreach (var item in values.Where(item => item.Key.StartsWith("CShells:Shells:pages:", StringComparison.Ordinal)).ToArray())
            values[collisionRoot + item.Key["CShells:Shells:pages".Length..]] = item.Value;
        values[collisionRoot + ":Configuration:WebRouting:Path"] = "pages-collision";
        values[collisionRoot + ":Features:PresentationCollision"] = "true";
        const string invalidThemeRoot = "CShells:Shells:pages-theme-invalid";
        foreach (var item in values.Where(item => item.Key.StartsWith("CShells:Shells:pages:", StringComparison.Ordinal)).ToArray())
            values[invalidThemeRoot + item.Key["CShells:Shells:pages".Length..]] = item.Value;
        values[invalidThemeRoot + ":Configuration:WebRouting:Path"] = "pages-theme-invalid";
        values[invalidThemeRoot + ":Configuration:Foundation:Web:Presentation:Theme"] = "green\" style=\"background:url(unreviewed)";
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(values);
        builder.Services.AddCShellsAspNetCore(shells => shells.WithConfigurationProvider(builder.Configuration)
            .WithWebRouting(options => options.EnablePathRouting = true));
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapShells();
        var registry = app.Services.GetRequiredService<IShellRegistry>();
        foreach (var name in names) await registry.GetOrActivateAsync(name);
        await app.StartAsync();
        try
        {
            await registry.GetOrActivateAsync("pages-collision");
            throw new InvalidOperationException("Ambiguous application/presentation routes were published.");
        }
        catch (ShellGenerationActivationException error) when (error.InnerException?.GetType().Name == "ShellEndpointConflictException") { }
        try
        {
            await registry.GetOrActivateAsync("pages-theme-invalid");
            throw new InvalidOperationException("Invalid presentation theme was admitted.");
        }
        catch (ShellGenerationActivationException error) when (error.GetBaseException() is InvalidDataException invalid
            && invalid.Message == "Presentation name, locale, theme or confirmation policy is invalid.") { }
        if (serve)
        {
            foreach (var name in names) registry.GetActive(name)!.ServiceProvider.GetRequiredService<RecoveryControl>().BrowserProvider
                = app.Urls.Single() + (name == "pages" ? "" : "/" + name) + (name == "pages-a" ? "/app" : "") + "/fixture/provider";
            Console.WriteLine("PRESENTATION_BROWSER_PROBE_URL=" + app.Urls.Single());
            await app.WaitForShutdownAsync();
            return;
        }
        try
        {
            foreach (var name in names)
            {
                using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
                client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
                var prefix = name == "pages" ? "" : "/" + name + (name == "pages-a" ? "/app" : "");
                using (var bootstrap = await client.GetAsync(prefix + "/auth/configuration"))
                {
                    var selected = System.Text.Json.JsonDocument.Parse(await bootstrap.Content.ReadAsStringAsync()).RootElement;
                    Require(selected.EnumerateObject().Count() == 7, "autosave must not change the exact existing bootstrap contract");
                    Require(bootstrap.Content.Headers.ContentType?.MediaType == "application/json"
                        && selected.GetProperty("signOutConfirmation").GetString() == (name == "pages-a" ? "always" : name == "pages" ? "never" : "when-unsaved")
                        && selected.GetProperty("logoutPath").GetString() == prefix + "/bff/logout", "native selected policy/bootstrap binding failed");
                }
                using var page = await client.GetAsync(prefix + "/auth/sign-in");
                var html = await page.Content.ReadAsStringAsync();
                Require(page.StatusCode == HttpStatusCode.OK && page.Content.Headers.ContentType?.MediaType == "text/html", "configured anonymous sign-in page failed: " + html);
                Require(html.Contains("action=\"" + prefix + "/auth/sign-in\"", StringComparison.Ordinal), "form escaped native shell prefix");
                Require(page.Headers.CacheControl?.NoStore == true && page.Headers.GetValues("X-Robots-Tag").Single() == "noindex, nofollow", "private browser headers missing");
                if (name == "pages-a") Require(html.Contains("Synthetic &lt;Application&gt;", StringComparison.Ordinal) && html.Contains("Aanmelden", StringComparison.Ordinal)
                    && html.Contains("lang=\"nl\"", StringComparison.Ordinal) && html.Contains("data-theme=\"green\"", StringComparison.Ordinal), "branding/theme/locale copy leaked or was not encoded");
                foreach (var asset in new[] { "page.css", "presentation.mjs", "presentation.d.mts", "autosave.mjs", "autosave.d.mts" })
                {
                    using var response = await client.GetAsync(prefix + "/auth/assets/v1/" + asset);
                    Require(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadAsByteArrayAsync()).Length > 0, "anonymous packaged asset failed");
                    using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, prefix + "/auth/assets/v1/" + asset));
                    Require(head.StatusCode == HttpStatusCode.OK && (await head.Content.ReadAsByteArrayAsync()).Length == 0,
                        "packaged asset HEAD behavior changed");
                }
                using (var rejected = await client.PostAsync(prefix + "/auth/sign-out", new FormUrlEncodedContent([])))
                    Require(rejected.StatusCode == HttpStatusCode.BadRequest && rejected.Content.Headers.ContentType?.MediaType == "text/html", "unprotected browser logout");
                using (var login = await client.PostAsync(prefix + "/auth/sign-in", Form(html)))
                {
                    if (name == "pages-failure") Require(login.StatusCode == HttpStatusCode.ServiceUnavailable
                        && login.Content.Headers.ContentType?.MediaType == "text/html"
                        && (await login.Content.ReadAsStringAsync()).Contains(prefix + "/auth/sign-in", StringComparison.Ordinal), "native provider failure lacked admitted browser recovery");
                    else Require(login.StatusCode == HttpStatusCode.Redirect && login.Headers.Location!.Host == "identity.example", "browser login did not invoke native OIDC");
                }
                await client.GetAsync(prefix + "/fixture/session");
                using (var landing = await client.GetAsync(prefix + "/auth/signed-out?remote=unavailable"))
                    Require((await landing.Content.ReadAsStringAsync()).Contains("A local session is active.", StringComparison.Ordinal), "recovery URL pretended to prove logout");
                using (var machine = await client.GetAsync(prefix + "/bff/user")) Require(machine.Content.Headers.ContentType?.MediaType == "application/json"
                    && (await machine.Content.ReadAsStringAsync()).Contains("\"authenticated\":true", StringComparison.Ordinal), "Accept changed native machine contract");
                var signOutHtml = await client.GetStringAsync(prefix + "/auth/sign-out");
                using (var unsafeApi = await client.PostAsync(prefix + "/api/private", new FormUrlEncodedContent([])))
                    Require(unsafeApi.StatusCode == HttpStatusCode.BadRequest && unsafeApi.Content.Headers.ContentType?.MediaType == "application/problem+json",
                        "combined native route prefix bypassed API antiforgery or machine purpose");
                using (var logout = await client.PostAsync(prefix + "/auth/sign-out", Form(signOutHtml)))
                    Require(logout.StatusCode == HttpStatusCode.Redirect && (name != "pages-failure"
                        || logout.Headers.Location!.OriginalString == prefix + "/auth/signed-out?remote=unavailable"), "native signout lost selected landing");
                using (var user = await client.GetAsync(prefix + "/bff/user")) Require((await user.Content.ReadAsStringAsync()).Contains("\"authenticated\":false", StringComparison.Ordinal), "logout did not clear actual local session");
                using (var unknown = await client.GetAsync(prefix + "/fixture/unknown-private")) Require(unknown.StatusCode == HttpStatusCode.Unauthorized
                    && unknown.Content.Headers.ContentType?.MediaType == "application/problem+json", "unknown protected route inferred browser purpose");
                using (var browser = await client.GetAsync(prefix + "/fixture/browser-private")) Require(browser.StatusCode == HttpStatusCode.Redirect, "explicit browser challenge lost native redirect");
                using (var failed = await client.GetAsync(prefix + "/fixture/page-failure"))
                {
                    var body = await failed.Content.ReadAsStringAsync();
                    Require(failed.StatusCode == HttpStatusCode.InternalServerError && failed.Content.Headers.ContentType?.MediaType == "text/html"
                        && !body.Contains("private-exception", StringComparison.Ordinal) && body.Contains(prefix + "/auth/sign-in", StringComparison.Ordinal), "safe native error renderer failed");
                }
                using (var denied = await client.GetAsync(prefix + "/bff/access-denied?code=unknown")) Require(denied.StatusCode == HttpStatusCode.Forbidden
                    && denied.Content.Headers.ContentType?.MediaType == "text/html", "native access-denied route lacked safe browser representation");
                using (var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, prefix + "/auth/expired")))
                    Require(head.StatusCode == HttpStatusCode.OK && (await head.Content.ReadAsByteArrayAsync()).Length == 0, "HEAD page behavior changed");
                using (var missing = await client.GetAsync(prefix + "/unknown/path")) Require(missing.StatusCode == HttpStatusCode.NotFound
                    && missing.Content.Headers.ContentType?.MediaType == "application/problem+json", "Accept alone changed an unmatched contract");
                using var navigation = new HttpRequestMessage(HttpMethod.Get, prefix + "/unknown/path");
                navigation.Headers.Add("Sec-Fetch-Mode", "navigate");
                navigation.Headers.Add("Sec-Fetch-Dest", "document");
                using (var missing = await client.SendAsync(navigation)) Require(missing.StatusCode == HttpStatusCode.NotFound
                    && missing.Content.Headers.ContentType?.MediaType == "text/html", "known application not-found lacked browser recovery");
            }
            Console.WriteLine("Native presentation passed: root/prefixed shell pages, copy encoding/locale, anonymous embedded assets, no-store/noindex, CSRF refusal, actual OIDC challenge, provider failure, independent local-session state, local logout and unchanged machine endpoints.");
        }
        finally { await app.StopAsync(); }
    }
    private static FormUrlEncodedContent Form(string html)
    {
        var token = Regex.Match(html, "name=\"([^\"]+)\" value=\"([^\"]+)\"");
        Require(token.Success, "native antiforgery form missing");
        return new FormUrlEncodedContent([new KeyValuePair<string, string>(WebUtility.HtmlDecode(token.Groups[1].Value), WebUtility.HtmlDecode(token.Groups[2].Value))]);
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
