using System.Net;
using System.Security.Claims;
using System.Text.Json;
using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.DependencyInjection;
using CShells.Lifecycle;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.WebDefaults;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Authentication.Core;
using Orbyss.Foundation.Authentication.BffCookie;
using Orbyss.Foundation.Json;
using Orbyss.Foundation.Json.AspNetCore;

internal static class CallbackRoutingProbe
{
    public static async Task RunAsync()
    {
        var configuration = new Dictionary<string, string?>();
        foreach (var shell in new[] { "default", "a", "b", "tiny" })
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
            configuration[$"{web}:AllowHttpForLocalDevelopment"] = "true";
            if (shell == "tiny")
            {
                configuration[$"{root}:Configuration:Foundation:Json:Profiles:success-response:MaxBytes"] = "64";
                configuration[$"{root}:Configuration:Foundation:Json:Profiles:problem-response:MaxBytes"] = "512";
            }
            if (shell == "b")
            {
                configuration[$"{web}:CallbackPath"] = "/oidc/return";
                configuration[$"{web}:SignedOutCallbackPath"] = "/oidc/signed-out";
                configuration[$"{web}:RemoteSignOutPath"] = "/oidc/frontchannel";
                configuration[$"{web}:AccessDeniedPath"] = "/oidc/error";
            }
        }
        var invalidRoot = "CShells:Shells:invalid";
        configuration[$"{invalidRoot}:Features:Orbyss.Foundation.Authentication.BffCookie"] = "true";
        configuration[$"{invalidRoot}:Configuration:WebRouting:Path"] = "invalid";
        configuration[$"{invalidRoot}:Configuration:Foundation:Web:Authority"] = "https://identity.example/invalid";
        configuration[$"{invalidRoot}:Configuration:Foundation:Web:ClientId"] = "invalid";
        configuration[$"{invalidRoot}:Configuration:Foundation:Web:ClientSecret"] = "fictional";
        configuration[$"{invalidRoot}:Configuration:Foundation:Web:Audience"] = "invalid";
        configuration[$"{invalidRoot}:Configuration:Foundation:Web:RemoteAuthenticationTimeoutSeconds"] = "0";
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Error);
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddCShellsAspNetCore(shells => shells.WithConfigurationProvider(builder.Configuration)
            .WithWebRouting(options => options.EnablePathRouting = true));
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapShells();
        foreach (var shell in new[] { "default", "a", "b", "tiny" })
            await app.Services.GetRequiredService<IShellRegistry>().GetOrActivateAsync(shell);
        try
        {
            _ = await app.Services.GetRequiredService<IShellRegistry>().GetOrActivateAsync("invalid");
        }
        catch (ShellGenerationActivationException exception) when (exception.InnerException is OptionsValidationException) { }
        Require(!((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Any(endpoint => endpoint.RoutePattern.RawText == "/invalid/bff/login"), "invalid timeout published a challenge endpoint");
        await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
            foreach (var prefix in new[] { "", "/a", "/b" })
            {
                var callback = prefix == "/b" ? "/oidc/return" : "/signin-oidc";
                VerifyResponseMetadata(app, prefix);
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
                    RequireManagedPolicy(response);
                    using var error = await client.GetAsync(response.Headers.Location);
                    Require(error.StatusCode == HttpStatusCode.BadRequest, "invalid callback did not fail closed");
                    RequireManagedPolicy(error);
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
                using var anonymous = JsonDocument.Parse(await user.Content.ReadAsByteArrayAsync());
                RequireProperties(anonymous.RootElement, "authenticated");
                Require(!anonymous.RootElement.GetProperty("authenticated").GetBoolean(), "invalid callback authenticated a user");
                await VerifyPublicWireAsync(client, prefix);
                await VerifyCookieRoundtripAsync(app, client.BaseAddress!, shellName, prefix);
            }
            await VerifyLowSuccessBudgetAsync(app, client.BaseAddress!);
            using var unknown = await client.GetAsync("/unknown/signin-oidc");
            Require(unknown.StatusCode == HttpStatusCode.NotFound, "unknown shell callback was accepted");
            Require(!((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>().Any(endpoint => endpoint.RoutePattern.RawText == "/b/signin-oidc"), "custom callback retained an unintended default route");
        }
        finally { await app.StopAsync(); }
        Console.WriteLine("Real shell OIDC callback routing, cookie admission/revalidation, custom paths and identity isolation passed.");
    }

    private static async Task VerifyCookieRoundtripAsync(WebApplication app, Uri baseAddress, string shellName, string prefix)
    {
        var shell = app.Services.GetRequiredService<IShellRegistry>().GetActive(shellName)!;
        var oidc = shell.ServiceProvider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = baseAddress };
        using (var ambiguousChallenge = await client.GetAsync(prefix + "/bff/login"))
        {
            var ambiguousQuery = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(ambiguousChallenge.Headers.Location!.Query);
            var ambiguousCode = CallbackProbeFeature.IssueCode(oidc.Authority!, oidc.ClientId!, ambiguousQuery["nonce"].ToString(), ambiguousSubject: true);
            using var callback = await client.GetAsync(prefix + oidc.CallbackPath + "?code=" + ambiguousCode + "&state=" + Uri.EscapeDataString(ambiguousQuery["state"].ToString()));
            Require(callback.Headers.Location?.OriginalString == prefix + (shellName == "b" ? "/oidc/error" : "/bff/access-denied") + "?code=authentication_callback_invalid",
                "native signed OIDC token with duplicate subjects was admitted");
            using var anonymous = await client.GetAsync(prefix + "/bff/user");
            Require((await anonymous.Content.ReadAsStringAsync()).Contains("\"authenticated\":false", StringComparison.Ordinal), "invalid native token created a session");
        }
        using var challenge = await client.GetAsync(prefix + "/bff/login");
        Require(challenge.StatusCode == HttpStatusCode.Redirect, "native OIDC challenge failed");
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
        var code = CallbackProbeFeature.IssueCode(oidc.Authority!, oidc.ClientId!, query["nonce"].ToString());
        using var signIn = await client.GetAsync(prefix + oidc.CallbackPath + "?code=" + code + "&state=" + Uri.EscapeDataString(query["state"].ToString()));
        Require(signIn.StatusCode == HttpStatusCode.Redirect && signIn.Headers.Location?.OriginalString == "/"
            && signIn.Headers.Contains("Set-Cookie"), "native validated OIDC code flow failed to issue a cookie");
        using var user = await client.GetAsync(prefix + "/bff/user");
        using var projection = JsonDocument.Parse(await user.Content.ReadAsStringAsync());
        Require(user.StatusCode == HttpStatusCode.OK && projection.RootElement.GetProperty("authenticated").GetBoolean()
            && projection.RootElement.GetProperty("issuer").GetString() == "https://identity.example/" + shellName
            && projection.RootElement.GetProperty("subject").GetString() == "probe-" + shellName, "cookie roundtrip changed validated identity");
        RequireProperties(projection.RootElement, "authenticated", "issuer", "subject", "displayName", "permissions");
        Require(projection.RootElement.GetProperty("displayName").GetString() == "probe-" + shellName
            && projection.RootElement.GetProperty("permissions").GetArrayLength() == 0, "authenticated display-name/empty permission wire changed");
        var reader = shell.ServiceProvider.GetRequiredService<IValidatedAccountIdentityReader>();
        Require(reader.TryRead(new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(AuthenticationClaimTypes.ValidatedIssuer, "https://identity.example/" + shellName),
            new Claim(AuthenticationClaimTypes.ValidatedSubject, "probe-" + shellName)], "cookie")), out _), "public reader was not registered in the actual shell");
        var cookieOptions = shell.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var cookie = handler.CookieContainer.GetCookies(baseAddress)[cookieOptions.Cookie.Name!]!;
        using var foreign = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = baseAddress };
        using var crossRequest = new HttpRequestMessage(HttpMethod.Get, (prefix == "/b" ? "/a" : "/b") + "/bff/user");
        crossRequest.Headers.Add("Cookie", cookie.Name + "=" + cookie.Value);
        using var cross = await foreign.SendAsync(crossRequest);
        Require((await cross.Content.ReadAsStringAsync()).Contains("\"authenticated\":false", StringComparison.Ordinal), "another shell admitted this shell's stored ticket");
        // Corrupt the server-held ticket after issuance to exercise actual cookie revalidation.
        var reference = cookieOptions.TicketDataFormat.Unprotect(cookie.Value)!;
        var key = reference.Principal.FindFirst("Microsoft.AspNetCore.Authentication.Cookies-SessionId")!.Value;
        var store = shell.ServiceProvider.GetRequiredService<ITicketStore>();
        var stored = (await store.RetrieveAsync(key))!;
        var storedIdentity = (ClaimsIdentity)stored.Principal.Identity!;
        var permissionType = shell.ServiceProvider.GetRequiredService<IOptions<FoundationWebOptions>>().Value.PermissionClaim;
        foreach (var value in new[] { "z", "a", "z", "A" }) storedIdentity.AddClaim(new Claim(permissionType, value));
        foreach (var name in storedIdentity.FindAll("name").ToArray()) storedIdentity.RemoveClaim(name);
        await store.RenewAsync(key, stored);
        using (var projectedResponse = await client.GetAsync(prefix + "/bff/user"))
        {
            using var projectedWire = JsonDocument.Parse(await projectedResponse.Content.ReadAsByteArrayAsync());
            RequireProperties(projectedWire.RootElement, "authenticated", "issuer", "subject", "displayName", "permissions");
            Require(projectedWire.RootElement.GetProperty("displayName").ValueKind == JsonValueKind.Null,
                "nullable display name must remain an explicit wire property");
            Require(projectedWire.RootElement.GetProperty("permissions").EnumerateArray().Select(item => item.GetString())
                .SequenceEqual(new[] { "A", "a", "z" }), "permission projection lost distinct ordinal ordering");
        }
        var foreignShell = app.Services.GetRequiredService<IShellRegistry>().GetActive(shellName == "b" ? "a" : "b")!;
        var foreignStore = foreignShell.ServiceProvider.GetRequiredService<ITicketStore>();
        Require(await foreignStore.RetrieveAsync(key) is null, "a foreign store read a shared-cache ticket");
        await foreignStore.RemoveAsync(key);
        Require(await store.RetrieveAsync(key) is not null, "a foreign store removed the owning shell's ticket");
        try
        {
            await foreignStore.RenewAsync(key, stored);
            throw new Exception("a foreign store renewed the owning shell's ticket");
        }
        catch (InvalidOperationException) { }
        stored.Principal.AddIdentity(new ClaimsIdentity());
        await store.RenewAsync(key, stored);
        using var rejected = await client.GetAsync(prefix + "/bff/user");
        Require((await rejected.Content.ReadAsStringAsync()).Contains("\"authenticated\":false", StringComparison.Ordinal)
            && await store.RetrieveAsync(key) is null, "cookie revalidation retained an ambiguous ticket");
    }

    private static void VerifyResponseMetadata(WebApplication app, string prefix)
    {
        foreach (var (path, wireType) in new[]
        {
            ("/bff/user", typeof(BffUserResponse)), ("/bff/antiforgery", typeof(BffAntiforgeryResponse)),
            ("/bff/signed-out", typeof(BffSignedOutResponse))
        })
        {
            var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText == prefix + path);
            var contract = endpoint.Metadata.GetMetadata<IJsonContractMetadata>();
            Require(contract is not null && contract.ContractType == wireType && contract.Direction == JsonContractDirection.Response
                && contract.Profile.Name == JsonProfileKeys.SuccessResponse && contract.Requirement.Preset == JsonProfileKeys.TolerantResponse,
                "BFF public endpoint lost its typed response/profile metadata: " + path);
        }
    }

    private static async Task VerifyPublicWireAsync(HttpClient client, string prefix)
    {
        using (var response = await client.GetAsync(prefix + "/bff/antiforgery"))
        {
            Require(response.StatusCode == HttpStatusCode.OK, "antiforgery projection failed");
            using var wire = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
            RequireProperties(wire.RootElement, "headerName", "formFieldName", "requestToken");
            Require(wire.RootElement.GetProperty("headerName").GetString() == FoundationBffCookieFeature.AntiforgeryHeader
                && wire.RootElement.GetProperty("formFieldName").GetString() == FoundationBffCookieFeature.AntiforgeryFormField
                && !string.IsNullOrWhiteSpace(wire.RootElement.GetProperty("requestToken").GetString()), "antiforgery wire values changed");
        }
        using (var response = await client.GetAsync(prefix + "/bff/signed-out"))
        {
            Require(response.StatusCode == HttpStatusCode.OK, "signed-out projection failed");
            using var wire = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
            RequireProperties(wire.RootElement, "signedOut");
            Require(wire.RootElement.GetProperty("signedOut").GetBoolean(), "signed-out acknowledgement changed");
        }
    }

    private static async Task VerifyLowSuccessBudgetAsync(WebApplication app, Uri baseAddress)
    {
        const string prefix = "/tiny";
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = baseAddress };
        VerifyResponseMetadata(app, prefix);
        using (var response = await client.GetAsync(prefix + "/bff/user"))
        {
            Require(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadAsByteArrayAsync()).Length <= 64,
                "configured low success budget must admit the anonymous projection");
        }
        using (var response = await client.GetAsync(prefix + "/bff/signed-out"))
            Require(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadAsByteArrayAsync()).Length <= 64,
                "configured low success budget must admit signed-out acknowledgement");
        var shell = app.Services.GetRequiredService<IShellRegistry>().GetActive("tiny")!;
        var oidc = shell.ServiceProvider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnectDefaults.AuthenticationScheme);
        using var challenge = await client.GetAsync(prefix + "/bff/login");
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
        var code = CallbackProbeFeature.IssueCode(oidc.Authority!, oidc.ClientId!, query["nonce"].ToString());
        using var signIn = await client.GetAsync(prefix + oidc.CallbackPath + "?code=" + code + "&state=" + Uri.EscapeDataString(query["state"].ToString()));
        Require(signIn.StatusCode == HttpStatusCode.Redirect && signIn.Headers.Contains("Set-Cookie"), "low-budget shell did not admit legitimate native OIDC login");
        using var rejected = await client.GetAsync(prefix + "/bff/user");
        var bytes = await rejected.Content.ReadAsByteArrayAsync();
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Require(rejected.StatusCode == HttpStatusCode.InternalServerError && bytes.Length <= 512
            && rejected.Content.Headers.ContentType?.MediaType == "application/problem+json", "oversized legitimate BFF projection must be a bounded safe500 before commitment");
        using var problem = JsonDocument.Parse(bytes);
        Require(problem.RootElement.GetProperty("status").GetInt32() == 500
            && problem.RootElement.GetProperty("correlationId").GetString() == problem.RootElement.GetProperty("traceId").GetString(), "BFF output failure lost shared problem representation");
        Require(!text.Contains(oidc.Authority!, StringComparison.Ordinal) && !text.Contains(oidc.ClientId!, StringComparison.Ordinal)
            && !text.Contains("authenticated", StringComparison.Ordinal), "BFF output failure exposed raw identity or partial success");
    }

    private static void RequireManagedPolicy(HttpResponseMessage response)
    {
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        foreach (var directive in new[] { "default-src 'self'", "frame-ancestors 'none'", "object-src 'none'", "base-uri 'self'" })
            Require(csp.Contains(directive, StringComparison.Ordinal), "actual BFF callback/error lost final CSP restriction " + directive);
        Require(!csp.Contains("'nonce-", StringComparison.Ordinal), "BFF callback/error acquired an editor nonce");
        Require(response.Headers.GetValues("X-Robots-Tag").Single() == "noindex, nofollow", "actual private BFF callback/error lost final crawler denial");
        Require(response.Headers.GetValues("X-Frame-Options").Single() == "DENY", "actual BFF callback/error lost final framing denial");
        Require(response.Headers.CacheControl?.NoStore == true, "actual BFF callback/error cached");
    }

    private static void RequireProperties(JsonElement wire, params string[] expected)
    {
        var actual = wire.EnumerateObject().Select(property => property.Name).ToArray();
        Require(actual.Length == expected.Length && actual.ToHashSet(StringComparer.Ordinal).SetEquals(expected),
            "BFF public property set changed: " + string.Join(",", actual));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
