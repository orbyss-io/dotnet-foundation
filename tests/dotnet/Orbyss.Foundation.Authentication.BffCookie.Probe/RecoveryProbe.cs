using System.Diagnostics;
using System.Net;
using System.Text.Json;
using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.DependencyInjection;
using CShells.Lifecycle;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication.BffCookie;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Authentication.Core;

internal static class RecoveryProbe
{
    internal static async Task RunAsync(bool serve = false)
    {
        var values = new Dictionary<string, string?>();
        var names = new[] { "default", "a", "transport", "timeout", "status", "jwks", "badjson", "notfound", "programming", "program-http", "par", "par-program", "par401", "parbadjson", "abort", "missinglogout", "expired" };
        foreach (var name in names)
        {
            var root = "CShells:Shells:" + name;
            values[root + ":Features:Orbyss.Foundation.Authentication.BffCookie"] = "true";
            values[root + ":Features:Orbyss.Foundation.Web.ProblemDetails"] = "true";
            values[root + ":Features:RecoveryProbe"] = "true";
            values[root + ":Configuration:WebRouting:Path"] = name == "default" ? "" : name;
            var web = root + ":Configuration:Foundation:Web";
            values[web + ":Authority"] = "https://identity.example/" + name;
            values[web + ":ClientId"] = "probe-" + name;
            values[web + ":ClientSecret"] = "synthetic-only";
            values[web + ":Audience"] = "fixture-api";
            values[web + ":Scopes:0"] = "openid";
            values[web + ":AllowHttpForLocalDevelopment"] = "true";
            values[web + ":DiscoveryTimeoutSeconds"] = "1";
            if (name != "default") values[web + ":LogoutLandingPath"] = "/landing";
            if (name == "expired") values[web + ":RemoteAuthenticationTimeoutSeconds"] = "2";
        }
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
        if (serve)
        {
            foreach (var name in names) Control(registry, name).BrowserProvider = app.Urls.Single()
                + (name == "default" ? "" : "/" + name) + "/fixture/provider";
            Console.WriteLine("RECOVERY_BROWSER_PROBE_URL=" + app.Urls.Single());
            await app.WaitForShutdownAsync();
            return;
        }
        foreach (var name in new[] { "transport", "timeout", "status", "jwks", "par" })
        {
            using var client = Client(app);
            var control = Control(registry, name);
            var clock = Stopwatch.StartNew();
            using var failure = await client.GetAsync("/" + name + "/bff/login");
            var text = await failure.Content.ReadAsStringAsync();
            Console.WriteLine($"Recovery {name}: status={(int)failure.StatusCode}, writer={control.Writes}, requests={control.Requests}, cache={failure.Headers.CacheControl}, body={text}");
            Require(failure.StatusCode == HttpStatusCode.ServiceUnavailable && control.Writes == 1
                && text.Contains(AuthenticationErrorCodes.IdentityProviderUnavailable, StringComparison.Ordinal)
                && !text.Contains("private-", StringComparison.Ordinal) && failure.Headers.CacheControl?.NoStore == true
                && failure.Headers.GetValues("X-Robots-Tag").Single() == "noindex, nofollow", "expected native initiation failure bypassed bounded/private writer");
            Require(clock.Elapsed < TimeSpan.FromSeconds(5), "network failure used the human authentication window");
            if (name == "status") Require(control.Requests == 2, "native metadata availability retry behavior changed");
            await client.GetAsync("/" + name + "/fixture/session");
            using var request = new HttpRequestMessage(HttpMethod.Get, "/" + name + "/bff/login");
            request.Headers.Accept.ParseAdd("text/html");
            using var browser = await client.SendAsync(request);
            Require((await browser.Content.ReadAsStringAsync()).Contains("Continue to the application", StringComparison.Ordinal), "independently valid session lost browser continuation");
            using var user = await client.GetAsync("/" + name + "/bff/user");
            Require((await user.Content.ReadAsStringAsync()).Contains("\"authenticated\":true", StringComparison.Ordinal), "failed new attempt cleared a valid session");
            using var privateApi = await client.GetAsync("/" + name + "/api/private");
            Require(privateApi.StatusCode == HttpStatusCode.OK, "preserved session lost independent API admission");
        }
        foreach (var name in new[] { "badjson", "notfound", "programming", "program-http", "par-program", "par401", "parbadjson" })
        {
            using var client = Client(app);
            using var failure = await client.GetAsync("/" + name + "/bff/login");
            Require(failure.StatusCode == HttpStatusCode.InternalServerError && Control(registry, name).Writes == 0
                && (await failure.Content.ReadAsStringAsync()).Contains("request_failed", StringComparison.Ordinal), "configuration/programming failure masqueraded as provider availability");
        }
        using (var client = Client(app))
        {
            var control = Control(registry, "abort");
            using var cancellation = new CancellationTokenSource();
            var request = client.GetAsync("/abort/bff/login", cancellation.Token);
            await control.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancellation.Cancel();
            try { await request; throw new Exception("caller cancellation was ignored"); } catch (OperationCanceledException) { }
            await control.Finished.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Task.Delay(100);
            Require(control.Writes == 0, "caller abort was replaced by an availability response");
        }
        foreach (var name in new[] { "default", "a", "transport", "missinglogout" }) await LogoutAsync(app, name);
        Console.WriteLine("Native discovery/JWKS/PAR availability recovery, generic-error provenance exclusions, preserved sessions, cancellation, root/non-root logout completion/fallback and machine acknowledgement passed.");
        await app.StopAsync();
    }

    private static RecoveryControl Control(IShellRegistry registry, string name) => registry.GetActive(name)!.ServiceProvider.GetRequiredService<RecoveryControl>();
    private static HttpClient Client(WebApplication app) => new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    private static async Task LogoutAsync(WebApplication app, string name)
    {
        var prefix = name == "default" ? "" : "/" + name;
        var target = prefix + (name == "default" ? "/bff/signed-out" : "/landing");
        using var client = Client(app);
        await client.GetAsync(prefix + "/fixture/session");
        using var acknowledgement = await client.GetAsync(prefix + "/bff/signed-out");
        Require((await acknowledgement.Content.ReadAsStringAsync()).Contains("\"signedOut\":true", StringComparison.Ordinal), "machine acknowledgement contract changed");
        using var csrf = await client.GetAsync(prefix + "/bff/antiforgery");
        var token = JsonDocument.Parse(await csrf.Content.ReadAsStringAsync()).RootElement.GetProperty("requestToken").GetString();
        using var request = new HttpRequestMessage(HttpMethod.Post, prefix + "/bff/logout");
        request.Headers.Add(FoundationBffCookieFeature.AntiforgeryHeader, token);
        using var logout = await client.SendAsync(request);
        Require(logout.StatusCode == HttpStatusCode.Redirect, "native logout did not redirect");
        if (name is "transport" or "missinglogout")
            Require(logout.Headers.Location!.OriginalString == target + "?remote=unavailable", "failed provider initiation escaped selected shell landing");
        else
        {
            var query = QueryHelpers.ParseQuery(logout.Headers.Location!.Query);
            var callback = new Uri(query["post_logout_redirect_uri"].ToString());
            Require(callback.AbsolutePath == prefix + "/signout-callback-oidc", "native provider callback lost PathBase");
            using var completion = await client.GetAsync(callback.PathAndQuery + "?state=" + Uri.EscapeDataString(query["state"].ToString()));
            Require(completion.Headers.Location!.OriginalString == target, $"provider completion ignored selected logout landing: {name}, status={(int)completion.StatusCode}, location={completion.Headers.Location}, expected={target}");
        }
        using var user = await client.GetAsync(prefix + "/bff/user");
        Require((await user.Content.ReadAsStringAsync()).Contains("\"authenticated\":false", StringComparison.Ordinal), "logout landing did not correspond to cleared local session");
        using var denied = await client.GetAsync(prefix + "/api/private");
        Require(denied.StatusCode == HttpStatusCode.Unauthorized, "provider failure retained private API admission");
    }
}
