using System.Net;
using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.DependencyInjection;
using CShells.Lifecycle;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.WebDefaults;

internal static class CallbackRoutingProbe
{
    public static async Task RunAsync()
    {
        var configuration = new Dictionary<string, string?>();
        foreach (var shell in new[] { "default", "a", "b" })
        {
            var root = $"CShells:Shells:{shell}";
            configuration[$"{root}:Features:Orbyss.Foundation.Authentication.BffCookie"] = "true";
            configuration[$"{root}:Features:CallbackProbe"] = "true";
            if (shell != "default") configuration[$"{root}:Configuration:WebRouting:Path"] = shell;
            var web = $"{root}:Configuration:Foundation:Web";
            configuration[$"{web}:Authority"] = "https://identity.example/" + shell;
            configuration[$"{web}:ClientId"] = "probe-" + shell;
            configuration[$"{web}:ClientSecret"] = "probe-only";
            configuration[$"{web}:Audience"] = "probe-api";
            configuration[$"{web}:Scopes:0"] = "openid";
            if (shell == "b")
            {
                configuration[$"{web}:CallbackPath"] = "/oidc/return";
                configuration[$"{web}:SignedOutCallbackPath"] = "/oidc/signed-out";
                configuration[$"{web}:RemoteSignOutPath"] = "/oidc/frontchannel";
                configuration[$"{web}:AccessDeniedPath"] = "/oidc/error";
            }
        }
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Error);
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddCShellsAspNetCore(shells => shells.WithConfigurationProvider(builder.Configuration)
            .WithWebRouting(options => options.EnablePathRouting = true));
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapShells();
        foreach (var shell in new[] { "default", "a", "b" })
            await app.Services.GetRequiredService<IShellRegistry>().GetOrActivateAsync(shell);
        await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
            foreach (var prefix in new[] { "", "/a", "/b" })
            {
                var callback = prefix == "/b" ? "/oidc/return" : "/signin-oidc";
                var failure = prefix + (prefix == "/b" ? "/oidc/error" : "/bff/access-denied");
                foreach (var path in prefix == "/b"
                    ? new[] { "/oidc/return", "/oidc/signed-out", "/oidc/frontchannel" }
                    : new[] { "/signin-oidc", "/signout-callback-oidc", "/signout-oidc" })
                {
                    var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
                        .OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText == prefix + path);
                    Require(endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null, "protocol endpoint requires an existing session");
                    Require(endpoint.Metadata.GetMetadata<WebResponseMetadata>()?.Private == true, "protocol endpoint is not private");
                    Require(endpoint.Metadata.GetMetadata<IExcludeFromDescriptionMetadata>()?.ExcludeFromDescription == true, "protocol endpoint leaked into API documentation");
                    Require(endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.SequenceEqual(["GET", "POST"]), "protocol endpoint has unintended methods");
                }
                foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post })
                {
                    using var request = new HttpRequestMessage(method, prefix + callback + "?state=invalid");
                    if (method == HttpMethod.Post) request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["state"] = "invalid" });
                    using var response = await client.SendAsync(request);
                    Require(response.StatusCode == HttpStatusCode.Redirect, $"{method} {prefix + callback}: callback did not reach OIDC ({response.StatusCode})");
                    Require(response.Headers.Location?.OriginalString == failure + "?code=authentication_callback_invalid", "callback failure escaped its shell");
                    Require(response.Headers.CacheControl?.NoStore == true, "callback failure must be no-store");
                    using var error = await client.GetAsync(response.Headers.Location);
                    Require(error.StatusCode == HttpStatusCode.BadRequest, "invalid callback did not fail closed");
                }
                using var login = await client.GetAsync(prefix + "/bff/login");
                Require(login.StatusCode == HttpStatusCode.Redirect, $"challenge failed for {prefix}: {login.StatusCode}");
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(login.Headers.Location!.Query);
                Require(query["redirect_uri"] == client.BaseAddress!.GetLeftPart(UriPartial.Authority) + prefix + callback, "challenge callback URI lost its shell prefix");
                Require(query["code_challenge_method"] == "S256", "PKCE missing");
                using var skipped = await client.GetAsync(prefix + callback + "?skip-handler=1");
                Require(skipped.StatusCode == HttpStatusCode.BadRequest && skipped.Headers.CacheControl?.NoStore == true
                    && (await skipped.Content.ReadAsStringAsync()).Contains("authentication_callback_invalid", StringComparison.Ordinal), "unconsumed callback did not fail closed");
                var shellName = prefix.Length == 0 ? "default" : prefix.TrimStart('/');
                var shell = app.Services.GetRequiredService<IShellRegistry>().GetActive(shellName)!;
                var oidc = shell.ServiceProvider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
                    .Get(OpenIdConnectDefaults.AuthenticationScheme);
                var state = oidc.StateDataFormat.Protect(new AuthenticationProperties { RedirectUri = prefix + "/bff/signed-out" });
                using var missingCorrelation = await client.GetAsync(prefix + callback + "?state=" + Uri.EscapeDataString(state) + "&code=untrusted");
                Require(missingCorrelation.Headers.Location?.OriginalString == failure + "?code=authentication_callback_invalid", "callback without correlation was accepted");
                var signedOutPath = prefix == "/b" ? "/oidc/signed-out" : "/signout-callback-oidc";
                using var signedOut = await client.GetAsync(prefix + signedOutPath + "?state=" + Uri.EscapeDataString(state));
                Require(signedOut.StatusCode == HttpStatusCode.Redirect && signedOut.Headers.Location?.OriginalString == prefix + "/bff/signed-out", "signed-out callback was not handled by OIDC");
                var remoteSignOutPath = prefix == "/b" ? "/oidc/frontchannel" : "/signout-oidc";
                using var remoteSignOut = await client.GetAsync(prefix + remoteSignOutPath);
                Require(remoteSignOut.StatusCode == HttpStatusCode.OK && remoteSignOut.Headers.CacheControl?.NoStore == true, "remote-sign-out callback did not reach OIDC");
                using var user = await client.GetAsync(prefix + "/bff/user");
                Require((await user.Content.ReadAsStringAsync()).Contains("\"authenticated\":false", StringComparison.Ordinal), "invalid callback authenticated a user");
            }
            using var unknown = await client.GetAsync("/unknown/signin-oidc");
            Require(unknown.StatusCode == HttpStatusCode.NotFound, "unknown shell callback was accepted");
            Require(!((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>().Any(endpoint => endpoint.RoutePattern.RawText == "/b/signin-oidc"), "custom callback retained an unintended default route");
        }
        finally { await app.StopAsync(); }
        Console.WriteLine("Real shell OIDC callback routing, rejection, custom paths and prefix isolation passed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
