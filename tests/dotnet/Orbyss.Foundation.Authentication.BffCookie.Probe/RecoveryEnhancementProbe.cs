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
using Orbyss.Foundation.Web.Presentation;

internal static class RecoveryEnhancementProbe
{
    internal static async Task RunAsync(bool serve = false)
    {
        var names = new[] { "pages-localized", "pages-custom", "pages-oversize", "pages-broken" };
        var values = new Dictionary<string, string?>();
        foreach (var name in names)
        {
            var root = "CShells:Shells:" + name;
            foreach (var feature in new[] { "Orbyss.Foundation.Authentication.BffCookie", "Orbyss.Foundation.Web.Presentation", "RecoveryProbe" })
                values[root + ":Features:" + feature] = "true";
            if (name != "pages-localized") values[root + ":Features:RecoveryLayoutFixture"] = "true";
            values[root + ":Configuration:WebRouting:Path"] = name;
            values[root + ":Configuration:WebRouting:RoutePrefix"] = "app";
            var web = root + ":Configuration:Foundation:Web";
            values[web + ":Authority"] = "https://identity.example/" + name;
            values[web + ":ClientId"] = "fixture";
            values[web + ":ClientSecret"] = "synthetic-only";
            values[web + ":Audience"] = "fixture";
            values[web + ":AllowHttpForLocalDevelopment"] = "true";
            values[web + ":LogoutLandingPath"] = "/auth/signed-out";
            values[web + ":Presentation:ReturnPath"] = "/map";
            values[web + ":Presentation:ApplicationName"] = name switch { "pages-oversize" => "Oversize", "pages-broken" => "Broken", _ => "EetRadar <Synthetic>" };
            values[web + ":Presentation:Locales:nl:sign-in"] = "Aanmelden";
            values[web + ":Presentation:Locales:nl:denied"] = "Geen toegang";
            values[web + ":Presentation:Locales:nl:authorization-denied"] = "Je hebt geen toegang tot deze bron.";
            values[web + ":Presentation:Locales:nl:active"] = "Er is een lokale sessie actief.";
            values[web + ":Presentation:Locales:nl:reference"] = "Referentie";
            values[web + ":Presentation:Locales:nl:continue"] = "Verder naar de toepassing";
        }
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(values);
        builder.Services.AddCShellsAspNetCore(shells => shells.WithConfigurationProvider(builder.Configuration).WithWebRouting(options => options.EnablePathRouting = true));
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapShells();
        var registry = app.Services.GetRequiredService<IShellRegistry>();
        foreach (var name in names) await registry.GetOrActivateAsync(name);
        await app.StartAsync();
        if (serve)
        {
            foreach (var name in names) registry.GetActive(name)!.ServiceProvider.GetRequiredService<RecoveryControl>().BrowserProvider
                = app.Urls.Single() + "/" + name + "/app/fixture/provider";
            Console.WriteLine("RECOVERY_ENHANCEMENT_PROBE_URL=" + app.Urls.Single());
            await app.WaitForShutdownAsync();
            return;
        }
        try
        {
            foreach (var name in names)
            {
                using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
                client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
                client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("de;q=0.9,nl-BE;q=0.8,en;q=0.1");
                var prefix = "/" + name + "/app";
                using var signIn = await client.GetAsync(prefix + "/auth/sign-in");
                var html = await signIn.Content.ReadAsStringAsync();
                if (name is "pages-broken" or "pages-oversize")
                {
                    Require(signIn.StatusCode == HttpStatusCode.InternalServerError && html.Length < 512
                        && !html.Contains("private-", StringComparison.Ordinal) && signIn.Headers.CacheControl?.NoStore == true, "Bad custom recovery output escaped its budget/fallback");
                    using var problem = await client.GetAsync(prefix + "/bff/access-denied");
                    Require(problem.StatusCode == HttpStatusCode.InternalServerError && (await problem.Content.ReadAsStringAsync()).Length < 512, "Bad problem layout reentered renderer");
                    continue;
                }
                Require(signIn.StatusCode == HttpStatusCode.OK && signIn.Content.Headers.ContentLanguage.Single() == "nl"
                    && html.Contains("Aanmelden", StringComparison.Ordinal), "Quality/parent language selection failed");
                Require(signIn.Headers.Vary.Contains("Accept-Language"), "Recovery locale response lacks Vary");
                Require(name != "pages-custom" || html.Contains("data-recovery-layout", StringComparison.Ordinal), "Recovery override not used");
                if (name == "pages-localized") Require(html.Contains("EetRadar &lt;Synthetic&gt;", StringComparison.Ordinal), "Brand text was not encoded");
                var match = Regex.Match(html, "name=\"([^\"]+)\" value=\"([^\"]+)\"");
                Require(match.Success, "Native antiforgery form lost through layout");
                using var login = await client.PostAsync(prefix + "/auth/sign-in", new FormUrlEncodedContent([
                    new KeyValuePair<string,string>(WebUtility.HtmlDecode(match.Groups[1].Value),WebUtility.HtmlDecode(match.Groups[2].Value))]));
                Require(login.StatusCode == HttpStatusCode.Redirect && login.Headers.Location!.Host == "identity.example", "Custom/localized form replaced native challenge");
                using var denied = await client.GetAsync(prefix + "/bff/access-denied");
                var deniedHtml = await denied.Content.ReadAsStringAsync();
                Require(denied.StatusCode == HttpStatusCode.Forbidden && deniedHtml.Contains("Geen toegang", StringComparison.Ordinal)
                    && deniedHtml.Contains("Je hebt geen toegang", StringComparison.Ordinal) && !deniedHtml.Contains("Forbidden", StringComparison.Ordinal), "Authentication error was not fully localized");
                Require(denied.Content.Headers.ContentLanguage.Single() == "nl" && denied.Headers.Vary.Contains("Accept-Language"), "Problem language headers disagree with rendered HTML");
                using var machine = await client.GetAsync(prefix + "/bff/user");
                Require(machine.Content.Headers.ContentType?.MediaType == "application/json", "Recovery localization changed machine JSON");
                await client.GetAsync(prefix + "/fixture/session");
                var expired = await client.GetStringAsync(prefix + "/auth/expired");
                Require(expired.Contains("Er is een lokale sessie actief", StringComparison.Ordinal) && expired.Contains("Verder naar de toepassing", StringComparison.Ordinal)
                    && !expired.Contains("Your session has expired", StringComparison.Ordinal), "Expiry URL replaced independently valid session state");
                client.DefaultRequestHeaders.AcceptLanguage.Clear();
                client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("nl;q=0,en;q=1");
                using var fallback = await client.GetAsync(prefix + "/auth/sign-in");
                Require(fallback.Content.Headers.ContentLanguage.Single() == "en", "Excluded locale did not use fallback");
            }
            var options = new BrowserPresentationOptions { Locales = new() { ["nl"] = new() { ["unexpected"] = "untrusted" } } };
            try { _ = new BrowserPresentationCatalog(options); throw new Exception("Unknown locale copy key admitted"); }
            catch (InvalidDataException) { }
            Console.WriteLine("Recovery enhancements passed: weighted/parent locales, localized auth failures, native forms through custom layout, observed session state, response budgets/fallback and machine preservation.");
        }
        finally { await app.StopAsync(); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
