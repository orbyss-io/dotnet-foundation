using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CShells;
using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.AspNetCore.Features;
using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Authentication.BffCookie;
using Orbyss.Foundation.Authentication.Core;


internal static class TicketRenewalProbe
{
    public static async Task RunAsync(bool serve = false)
    {
        // Diagnostic only: synthetic validated identities, native feature configuration,
        // real HTTP/cookie handler/store/cache. No provider account or consumer is used.
        var values = new Dictionary<string, string?>();
        foreach (var name in new[] { "default", "other" })
        {
            var root = "CShells:Shells:" + name;
            values[root + ":Features:TicketRenewalProbe"] = "true";
            values[root + ":Configuration:WebRouting:Path"] = name == "default" ? "" : name;
            var web = root + ":Configuration:Foundation:Web";
            values[web + ":Authority"] = "https://synthetic.invalid/realm";
            values[web + ":ClientId"] = "race-fixture";
            values[web + ":ClientSecret"] = "synthetic-unused";
            values[web + ":Audience"] = "race-fixture";
            values[web + ":AllowHttpForLocalDevelopment"] = "true";
            values[web + ":SessionIdleMinutes"] = "2";
            values[web + ":SessionAbsoluteMinutes"] = "5";
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
        foreach (var name in new[] { "default", "other" }) await registry.GetOrActivateAsync(name);
        await app.StartAsync();
        if (serve)
        {
            Console.WriteLine("TICKET_RENEWAL_PROBE_URL=" + app.Urls.Single());
            await app.WaitForShutdownAsync();
            return;
        }
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(15) };
        var provider = registry.GetActive("default")!.ServiceProvider;
        var control = provider.GetRequiredService<TicketRenewalControl>();
        var spy = provider.GetRequiredService<TicketRenewalObservedStore>();
        Console.WriteLine(JsonSerializer.Serialize(new {
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            handler = typeof(CookieAuthenticationHandler).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            foundation = typeof(FoundationBffCookieFeature).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            store = spy.Native.GetType().FullName,
            cache = provider.GetRequiredService<IDistributedCache>().GetType().FullName
        }));
        var cases = new List<object>();
        foreach (var explicitRenew in new[] { false, true })
        foreach (var persistent in new[] { false, true })
        {
            control.Reset();
            var cookie = await Login(old: !explicitRenew, persistent);
            var key = spy.LastKey!;
            var original = await spy.Native.RetrieveAsync(key) ?? throw new Exception("No initial ticket");
            var cap = original.Properties.Items[TicketRenewalControl.Cap];
            control.Arm();
            var pending = Send(explicitRenew ? "/probe/late-explicit" : "/probe/late-slide", cookie);
            await control.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Require(await spy.Native.RetrieveAsync(key) is not null, "A must have read a valid native ticket");
            await Logout(cookie);
            Require(await spy.Native.RetrieveAsync(key) is null, "Native logout did not delete ticket");
            using var before = await Send("/probe/check", cookie);
            Require(before.StatusCode == HttpStatusCode.Unauthorized, "Cookie survived completed logout before A resumed");
            control.Release.TrySetResult();
            using var late = await pending;
            Require(late.IsSuccessStatusCode, "Late response failed");
            var lateCookie = SessionCookie(late);
            Require(lateCookie is not null, "Native renewal did not emit a late session cookie");
            var restored = await spy.Native.RetrieveAsync(key);
            using var replayLate = await Send("/probe/check", lateCookie);
            using var replayOld = await Send("/probe/check", cookie);
            Require(restored is null && replayLate.StatusCode == HttpStatusCode.Unauthorized && replayOld.StatusCode == HttpStatusCode.Unauthorized, "Late renewal restored a logged-out session");
            var renewedKey = spy.LastKey;
            Require(renewedKey == key, "Renewal created a new session instead of reusing the existing key");
            var capTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(cap!));
            control.Clock.Now = capTime.AddSeconds(1);
            using var afterCap = await Send("/probe/check", lateCookie);
            Require(afterCap.StatusCode == HttpStatusCode.Unauthorized, "Original absolute expiry was bypassed");
            cases.Add(new { name = explicitRenew ? "explicit-existing-ticket" : "native-sliding", persistent,
                deletedAfterLogout = true, deniedBeforeLateResponse = true, lateSetCookie = true,
                sameKeyRestored = false, oldCookieAccepted = false, lateCookieAccepted = false,
                deniedAfterOriginalAbsoluteCap = true });
        }

        // Ordinary idle expiry, then A arriving after the original idle deadline.
        control.Reset();
        var idleCookie = await Login(old: false, persistent: false);
        var idleTicket = await spy.Native.RetrieveAsync(spy.LastKey!) ?? throw new Exception("Missing idle ticket");
        control.Clock.Now = idleTicket.Properties.ExpiresUtc!.Value.AddSeconds(1);
        using (var expired = await Send("/probe/check", idleCookie))
            Require(expired.StatusCode == HttpStatusCode.Unauthorized, "Idle expiry was bypassed without an in-flight renewal");
        cases.Add(new { name = "ordinary-idle-expiry", denied = true });

        foreach (var explicitRenew in new[] { false, true })
        {
            control.Reset();
            var cookie = await Login(old: !explicitRenew, persistent: false);
            var key = spy.LastKey!;
            var ticket = await spy.Native.RetrieveAsync(key) ?? throw new Exception("Missing ticket");
            control.Arm();
            var pending = Send(explicitRenew ? "/probe/late-explicit" : "/probe/late-slide", cookie);
            await control.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            control.Clock.Now = ticket.Properties.ExpiresUtc!.Value.AddSeconds(1);
            Require(await spy.Native.RetrieveAsync(key) is null, "Real cache did not enforce idle expiry");
            control.Release.TrySetResult();
            using var late = await pending;
            using var replay = await Send("/probe/check", SessionCookie(late));
            Require(replay.StatusCode == HttpStatusCode.Unauthorized && await spy.Native.RetrieveAsync(key) is null, "Late renewal restored an idle-expired ticket");
            cases.Add(new { name = explicitRenew ? "explicit-after-idle-expiry" : "sliding-after-idle-expiry", expiredBeforeResume = true, restored = false });
        }

        foreach (var explicitRenew in new[] { false, true })
        {
            control.Reset();
            // Keep the ticket's idle expiry beyond the absolute cap, so denial cannot
            // pass merely because its idle window expired earlier.
            var cookie = await Login(old: !explicitRenew, persistent: true, longLife: true);
            var key = spy.LastKey!;
            var ticket = await spy.Native.RetrieveAsync(key) ?? throw new Exception("Missing absolute-cap ticket");
            var cap = ticket.Properties.Items[TicketRenewalControl.Cap];
            control.Arm();
            var pending = Send(explicitRenew ? "/probe/late-explicit" : "/probe/late-slide", cookie);
            await control.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            control.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(long.Parse(cap!)).AddSeconds(1);
            Require(ticket.Properties.ExpiresUtc > control.Clock.Now, "Absolute-cap control was masked by idle expiry");
            Require(await spy.Native.RetrieveAsync(key) is null, "Cache did not enforce original cap");
            control.Release.TrySetResult();
            using var late = await pending;
            Require(late.IsSuccessStatusCode, "Renewal past cap failed unexpectedly");
            using var replay = await Send("/probe/check", SessionCookie(late));
            Require(replay.StatusCode == HttpStatusCode.Unauthorized && await spy.Native.RetrieveAsync(key) is null,
                "Late renewal bypassed original absolute cap");
            cases.Add(new { name = explicitRenew ? "explicit-after-absolute-expiry" : "sliding-after-absolute-expiry",
                lateSetCookie = SessionCookie(late) is not null, denied = true, storedSessionAbsent = true });
        }

        foreach (var explicitRenew in new[] { false, true })
        foreach (var persistent in new[] { false, true })
        {
            control.Reset();
            var cookie = await Login(old: !explicitRenew, persistent);
            var key = spy.LastKey!;
            var original = await spy.Native.RetrieveAsync(key) ?? throw new Exception("Missing valid renewal ticket");
            control.Clock.Now = control.Clock.Now.AddSeconds(15);
            control.Arm();
            control.Release.TrySetResult();
            using var response = await Send(explicitRenew ? "/probe/late-explicit" : "/probe/late-slide", cookie);
            Require(response.IsSuccessStatusCode, "Valid renewal failed");
            var renewed = await spy.Native.RetrieveAsync(key) ?? throw new Exception("Valid renewal removed ticket");
            Require(renewed.Properties.ExpiresUtc > original.Properties.ExpiresUtc, "Valid renewal did not extend idle expiry");
            Require(renewed.Properties.Items[TicketRenewalControl.Cap] == original.Properties.Items[TicketRenewalControl.Cap]
                && renewed.Properties.IsPersistent == persistent && renewed.Properties.AllowRefresh == true
                && renewed.Properties.Items["synthetic-property"] == "preserved"
                && renewed.Properties.GetTokenValue("access_token") == "synthetic-access"
                && renewed.Properties.GetTokenValue("refresh_token") == "synthetic-refresh"
                && renewed.Principal.Claims.Select(claim => (claim.Type, claim.Value)).SequenceEqual(original.Principal.Claims.Select(claim => (claim.Type, claim.Value))),
                "Valid renewal lost cap, persistence, properties, tokens or principal");
            using var replay = await Send("/probe/check", SessionCookie(response));
            Require(replay.StatusCode == HttpStatusCode.OK, "Valid renewed cookie was rejected");
            cases.Add(new { name = explicitRenew ? "valid-explicit" : "valid-sliding", persistent, preserved = true });
        }

        // Independent new login after logout must use a new key and new cap.
        control.Reset();
        var freshCookie = await Login(old: false, persistent: false);
        var removedKey = spy.LastKey!;
        await Logout(freshCookie);
        control.Clock.Now = DateTimeOffset.UtcNow;
        var replacement = await Login(old: false, persistent: false);
        Require(spy.LastKey != removedKey && await spy.Native.RetrieveAsync(removedKey) is null, "New login reused revoked key");
        using (var fresh = await Send("/probe/check", replacement)) Require(fresh.StatusCode == HttpStatusCode.OK, "Fresh login denied");
        cases.Add(new { name = "new-login", distinctKey = true, authenticated = true });

        // Foreign shell HTTP replay plus native ownership on a deliberately shared real cache.
        using (var foreign = await Send("/other/probe/check", replacement))
            Require(foreign.StatusCode == HttpStatusCode.Unauthorized, "Foreign-shell HTTP replay succeeded");
        var other = registry.GetActive("other")!.ServiceProvider;
        var shared = provider.GetRequiredService<IDistributedCache>();
        var nativeType = spy.Native.GetType();
        var sharedOther = (ITicketStore)Activator.CreateInstance(nativeType, shared,
            other.GetRequiredService<IOptions<FoundationWebOptions>>(), other.GetRequiredService<ShellSettings>())!;
        var ownKey = spy.LastKey!;
        var ownTicket = await spy.Native.RetrieveAsync(ownKey) ?? throw new Exception("Missing own ticket");
        Require(await sharedOther.RetrieveAsync(ownKey) is null, "Foreign shell read shared ticket");
        await sharedOther.RemoveAsync(ownKey);
        Require(await spy.Native.RetrieveAsync(ownKey) is not null, "Foreign shell removed shared ticket");
        try { await sharedOther.RenewAsync(ownKey, ownTicket); throw new Exception("Foreign renewal succeeded"); }
        catch (InvalidOperationException) { }
        Require(await spy.Native.RetrieveAsync(ownKey) is not null, "Foreign renewal corrupted owning ticket");
        cases.Add(new { name = "shell-isolation", httpReplayDenied = true, sharedCacheReadDenied = true,
            foreignRemovePreservedOwner = true, foreignRenewRejected = true });

        await TicketCacheContractProbe.RunAsync(provider);
        Console.WriteLine(JsonSerializer.Serialize(new { status = "revoked-renewal-rejected", cases }, new JsonSerializerOptions { WriteIndented = true }));
        await app.StopAsync();

        async Task<HttpResponseMessage> Send(string path, string? cookie = null, string? csrf = null)
        {
            using var request = new HttpRequestMessage(csrf is null ? HttpMethod.Get : HttpMethod.Post, path);
            if (cookie is not null) request.Headers.Add("Cookie", cookie);
            if (csrf is not null) request.Headers.Add(FoundationBffCookieFeature.AntiforgeryHeader, csrf);
            return await client.SendAsync(request);
        }
        async Task<string> Login(bool old, bool persistent, bool longLife = false)
        {
            using var response = await Send($"/probe/login?old={old}&persistent={persistent}&longLife={longLife}");
            Require(response.IsSuccessStatusCode, "Synthetic native sign-in failed: " + response.StatusCode);
            return SessionCookie(response) ?? throw new Exception("Missing login cookie");
        }
        async Task Logout(string cookie)
        {
            using var csrfResponse = await Send("/bff/antiforgery", cookie);
            Require(csrfResponse.IsSuccessStatusCode, "Native antiforgery endpoint failed");
            var payload = JsonDocument.Parse(await csrfResponse.Content.ReadAsStringAsync());
            var token = payload.RootElement.GetProperty("requestToken").GetString();
            var csrfCookie = csrfResponse.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0]);
            using var response = await Send("/bff/logout", cookie + "; " + string.Join("; ", csrfCookie), token);
            Require(response.StatusCode == HttpStatusCode.Redirect, "Native logout failed: " + response.StatusCode);
            Require(response.Headers.GetValues("Set-Cookie").Any(value => value.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase)), "Missing native cookie deletion");
        }
        static string? SessionCookie(HttpResponseMessage response) => response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(value => value.StartsWith("orbyss-foundation-session-", StringComparison.Ordinal))?.Split(';')[0] : null;
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    }
}

[ShellFeature("TicketRenewalProbe", DependsOn = [typeof(FoundationBffCookieFeature)])]
public sealed class TicketRenewalProbeFeature : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<TicketRenewalControl>();
        // Inject only time into the selected real MemoryDistributedCache and cookie handler.
#pragma warning disable CS0618
        services.AddOptions<MemoryDistributedCacheOptions>().Configure<TicketRenewalControl>((options, control) => options.Clock = control.Clock);
#pragma warning restore CS0618
        var registration = services.Last(descriptor => descriptor.ServiceType == typeof(ITicketStore));
        services.Remove(registration);
        services.AddSingleton(provider => new TicketRenewalObservedStore((ITicketStore)ActivatorUtilities.CreateInstance(provider, registration.ImplementationType!)));
        services.AddSingleton<ITicketStore>(provider => provider.GetRequiredService<TicketRenewalObservedStore>());
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<TicketRenewalControl>((options, control) => options.TimeProvider = control.Clock);
        // No external provider traffic: native local-first logout takes its documented fallback.
        services.AddOptions<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>("OpenIdConnect")
            .Configure(options => options.Configuration = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration { Issuer = "https://synthetic.invalid/realm" });
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        endpoints.MapGet("/probe/page", () => Results.Content("<!doctype html><title>Synthetic renewal fixture</title><main>Native renewal fixture</main>", "text/html")).AllowAnonymous();
        endpoints.MapPost("/probe/barrier/arm", (TicketRenewalControl control) => { control.Reset(); control.Arm(); return Results.NoContent(); }).AllowAnonymous();
        endpoints.MapGet("/probe/barrier/entered", async (TicketRenewalControl control) => { await control.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); return Results.NoContent(); }).AllowAnonymous();
        endpoints.MapPost("/probe/barrier/release", (TicketRenewalControl control) => { control.Release.TrySetResult(); return Results.NoContent(); }).AllowAnonymous();
        endpoints.MapGet("/probe/login", async (HttpContext context, TicketRenewalControl control, bool old, bool persistent, bool longLife) =>
        {
            var now = control.Clock.GetUtcNow();
            var properties = new AuthenticationProperties { IssuedUtc = old ? now.AddSeconds(longLife ? -660 : -90) : now,
                ExpiresUtc = longLife ? now.AddMinutes(10) : old ? now.AddSeconds(30) : now.AddMinutes(2),
                IsPersistent = persistent, AllowRefresh = true };
            properties.Items["synthetic-property"] = "preserved";
            properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "synthetic-access" },
                new AuthenticationToken { Name = "refresh_token", Value = "synthetic-refresh" }]);
            await context.SignInAsync("Cookies", new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(AuthenticationClaimTypes.ValidatedIssuer, "https://synthetic.invalid/realm"),
                new Claim(AuthenticationClaimTypes.ValidatedSubject, "synthetic-owner")], "fixture")),
                properties);
            return Results.NoContent();
        }).AllowAnonymous();
        endpoints.MapGet("/probe/check", (HttpContext context) => context.User.Identity?.IsAuthenticated == true
            ? Results.Ok() : Results.Unauthorized()).AllowAnonymous();
        endpoints.MapGet("/probe/late-slide", async (HttpContext context, TicketRenewalControl control) =>
        {
            var ticket = await context.AuthenticateAsync("Cookies");
            if (!ticket.Succeeded) throw new Exception("A failed authentication");
            await control.Barrier();
            return Results.Ok(); // Native FinishResponseAsync performs sliding renewal.
        }).AllowAnonymous();
        endpoints.MapGet("/probe/late-explicit", async (HttpContext context, TicketRenewalControl control) =>
        {
            var ticket = await context.AuthenticateAsync("Cookies");
            if (!ticket.Succeeded) throw new Exception("A failed authentication");
            await control.Barrier();
            var properties = new AuthenticationProperties(new Dictionary<string, string?>(ticket.Properties!.Items));
            properties.IssuedUtc = control.Clock.GetUtcNow();
            properties.ExpiresUtc = properties.IssuedUtc.Value.AddMinutes(2);
            await context.SignInAsync("Cookies", ticket.Principal!, properties);
            return Results.Ok();
        }).AllowAnonymous();
    }
}
public sealed class TicketRenewalControl
{
    public const string Cap = ".orbyss-foundation.absolute-expiry";
    public TicketRenewalClock Clock { get; } = new();
    public TaskCompletionSource Entered { get; private set; } = New();
    public TaskCompletionSource Release { get; private set; } = New();
    public void Reset() { Clock.Now = DateTimeOffset.UtcNow; Entered = New(); Release = New(); }
    public void Arm() { Entered = New(); Release = New(); }
    public async Task Barrier() { Entered.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
    private static TaskCompletionSource New() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
#pragma warning disable CS0618
public sealed class TicketRenewalClock : TimeProvider, Microsoft.Extensions.Internal.ISystemClock
#pragma warning restore CS0618
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    private int blockNext;
    private readonly ManualResetEventSlim continueRead = new(false);
    public TaskCompletionSource ReadEntered { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void BlockNextRead() { ReadEntered = new(TaskCreationOptions.RunContinuationsAsynchronously); continueRead.Reset(); Interlocked.Exchange(ref blockNext, 1); }
    public void ContinueRead() => continueRead.Set();
    public DateTimeOffset UtcNow
    {
        get
        {
            if (Interlocked.Exchange(ref blockNext, 0) == 1)
            {
                ReadEntered.TrySetResult();
                if (!continueRead.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Cache read barrier timed out");
            }
            return Now;
        }
    }
    public override DateTimeOffset GetUtcNow() => Now;
}
public sealed class TicketRenewalObservedStore(ITicketStore native) : ITicketStore
{
    public ITicketStore Native { get; } = native;
    public string? LastKey { get; private set; }
    public async Task<string> StoreAsync(AuthenticationTicket ticket) => LastKey = await Native.StoreAsync(ticket);
    public Task RenewAsync(string key, AuthenticationTicket ticket) => Native.RenewAsync(key, ticket);
    public Task<AuthenticationTicket?> RetrieveAsync(string key) => Native.RetrieveAsync(key);
    public Task RemoveAsync(string key) => Native.RemoveAsync(key);
}
