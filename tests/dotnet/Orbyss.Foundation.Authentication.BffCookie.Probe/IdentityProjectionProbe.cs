using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using CShells.Lifecycle.Blueprints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Authentication.BffCookie;
using Orbyss.Foundation.Authentication.Core;
using Microsoft.IdentityModel.JsonWebTokens;

internal static class IdentityProjectionProbe
{
    public static async Task RunAsync()
    {
        using var provider = Provider();
        VerifyReader(provider);
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        var identity = new ClaimsIdentity([
            new Claim("iss", "https://identity.example"),
            new Claim("sub", "alice"),
            new Claim("sub", "bob")], "oidc");
        var context = Context(provider, options, new ClaimsPrincipal(identity));
        await options.Events.TokenValidated(context);
        Require(context.Result?.Failure is not null, "duplicate token subject was accepted by the registered OIDC callback");
        foreach (var principal in new[]
        {
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "https://identity.example"), new Claim("sub", "alice")])),
            new ClaimsPrincipal(new[] { RawIdentity(), new ClaimsIdentity() }),
            new ClaimsPrincipal(new[] { RawIdentity(), RawIdentity() }),
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "https://identity.example"), new Claim("sub", " ")], "oidc")),
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "https://other.example"), new Claim("sub", "alice")], "oidc")),
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "https://identity.example"), new Claim("iss", "https://identity.example"), new Claim("sub", "alice")], "oidc"))
        })
        {
            var invalid = Context(provider, options, principal);
            await options.Events.TokenValidated(invalid);
            Require(invalid.Result?.Failure is not null, "OIDC admitted an ambiguous, unauthenticated or mismatched principal");
        }
        foreach (var modernToken in new[] { false, true })
        {
            var valid = Context(provider, options, new ClaimsPrincipal(RawIdentity()));
            if (modernToken) valid.SecurityToken = new JwtSecurityTokenHandler().ReadJwtToken(
                new JsonWebToken("""{"alg":"none"}""", """{"iss":"https://identity.example","sub":"alice"}""").EncodedToken);
            await options.Events.TokenValidated(valid);
            Require(valid.Result?.Failure is null, "a valid issuer-subject token was rejected");
            var reader = provider.GetRequiredService<IValidatedAccountIdentityReader>();
            Require(reader.TryRead(valid.Principal!, out var account) && account.Subject == "alice"
                && account.Issuer == "https://identity.example", "validated token projection was not canonicalized");
            using var userInfo = JsonDocument.Parse("""{"name":"Alice"}""");
            foreach (var action in options.ClaimActions) action.Run(userInfo.RootElement, (ClaimsIdentity)valid.Principal!.Identity!, "https://identity.example");
            var ticket = Ticket(valid, options);
            await options.Events.TicketReceived(ticket);
            Require(ticket.Result?.Failure is null, "ordinary native user-info claim actions lost a valid projection");
            Require(valid.HttpContext.Items.Count == 0, "ticket admission retained request identity state");
        }
        foreach (var action in new[] { "delete", "change", "duplicate", "reintroduce", "multiple-identities" })
        {
            var selected = provider.GetRequiredService<IOptionsFactory<OpenIdConnectOptions>>().Create(OpenIdConnectDefaults.AuthenticationScheme);
            var valid = Context(provider, selected, new ClaimsPrincipal(RawIdentity()));
            await selected.Events.TokenValidated(valid);
            selected.ClaimActions.Clear();
            if (action is "delete" or "change" or "reintroduce") selected.ClaimActions.DeleteClaim(AuthenticationClaimTypes.ValidatedSubject);
            if (action is "change" or "duplicate" or "reintroduce") selected.ClaimActions.MapJsonKey(AuthenticationClaimTypes.ValidatedSubject, "reserved");
            using var userInfo = JsonDocument.Parse(action == "change" ? """{"reserved":"mallory"}""" : """{"reserved":"alice"}""");
            foreach (var claimAction in selected.ClaimActions) claimAction.Run(userInfo.RootElement, (ClaimsIdentity)valid.Principal!.Identity!, "https://identity.example");
            if (action == "multiple-identities") valid.Principal!.AddIdentity(new ClaimsIdentity());
            var ticket = Ticket(valid, selected);
            await selected.Events.TicketReceived(ticket);
            Require(ticket.Result?.Failure is not null, "claim action alteration was admitted: " + action);
        }
        using var other = Provider();
        Require(!ReferenceEquals(provider.GetRequiredService<IValidatedAccountIdentityReader>(), other.GetRequiredService<IValidatedAccountIdentityReader>()),
            "two shell providers shared an identity reader instance");
        Console.WriteLine("Validated identity cardinality, token projection and native claim-action preservation passed.");
    }

    private static void VerifyReader(IServiceProvider provider)
    {
        var reader = provider.GetRequiredService<IValidatedAccountIdentityReader>();
        var valid = new ClaimsPrincipal(ProjectedIdentity());
        Require(reader.TryRead(valid, out var admitted) && admitted == new ValidatedAccountIdentity("https://identity.example", "alice"), "valid projection was rejected");
        foreach (var principal in new[]
        {
            new ClaimsPrincipal(),
            new ClaimsPrincipal(new ClaimsIdentity(ProjectedIdentity().Claims)),
            new ClaimsPrincipal(new[] { ProjectedIdentity(), new ClaimsIdentity() }),
            new ClaimsPrincipal(new[] { ProjectedIdentity(), ProjectedIdentity() }),
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthenticationClaimTypes.ValidatedIssuer, "https://identity.example")], "oidc")),
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthenticationClaimTypes.ValidatedIssuer, ""), new Claim(AuthenticationClaimTypes.ValidatedSubject, "alice")], "oidc")),
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthenticationClaimTypes.ValidatedIssuer, "https://identity.example"), new Claim(AuthenticationClaimTypes.ValidatedSubject, " ")], "oidc")),
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthenticationClaimTypes.ValidatedIssuer, "https://identity.example"), new Claim(AuthenticationClaimTypes.ValidatedSubject, "alice"), new Claim(AuthenticationClaimTypes.ValidatedSubject, "alice")], "oidc")),
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthenticationClaimTypes.ValidatedIssuer.ToUpperInvariant(), "https://identity.example"), new Claim(AuthenticationClaimTypes.ValidatedSubject, "alice")], "oidc"))
        })
        {
            Require(!reader.TryRead(principal, out var rejected) && rejected is null, "reader admitted an ambiguous, missing, empty or unauthenticated projection");
        }
        Require(AuthenticationClaimTypes.ValidatedIssuer == "urn:orbyss-foundation:authentication:validated-issuer"
            && AuthenticationClaimTypes.ValidatedSubject == "urn:orbyss-foundation:authentication:validated-subject"
            && AuthenticationErrorCodes.IdentityInvalid == "authentication_identity_invalid", "historical identity protocol vocabulary changed");
        var exact = new ValidatedAccountIdentity("https://identity.example/", " alice ");
        Require(exact.Issuer.EndsWith('/') && exact.Subject == " alice ", "identity values were normalized");
    }

    private static ClaimsIdentity ProjectedIdentity() => new([
        new Claim(AuthenticationClaimTypes.ValidatedIssuer, "https://identity.example"),
        new Claim(AuthenticationClaimTypes.ValidatedSubject, "alice")], "oidc");

    private static ClaimsIdentity RawIdentity() => new([
        new Claim("iss", "https://identity.example"), new Claim("sub", "alice"),
        new Claim(AuthenticationClaimTypes.ValidatedIssuer, "untrusted-issuer"),
        new Claim(AuthenticationClaimTypes.ValidatedSubject, "untrusted-subject"),
        new Claim(AuthenticationClaimTypes.ValidatedSubject, "another-subject")], "oidc");

    private static TicketReceivedContext Ticket(TokenValidatedContext validated, OpenIdConnectOptions options) =>
        new(validated.HttpContext, validated.Scheme, options,
            new AuthenticationTicket(validated.Principal!, validated.Properties, validated.Scheme.Name));

    private static TokenValidatedContext Context(IServiceProvider provider, OpenIdConnectOptions options, ClaimsPrincipal principal) =>
        new(new DefaultHttpContext { RequestServices = provider },
            new AuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme, null, typeof(OpenIdConnectHandler)),
            options, principal, new AuthenticationProperties())
        {
            SecurityToken = new JwtSecurityToken(issuer: "https://identity.example", claims: [new Claim("sub", "alice")])
        };

    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new ProbeEnvironment(Environments.Production));
        services.AddSingleton<IOptions<FoundationWebOptions>>(Options.Create(new FoundationWebOptions
        {
            Authority = "https://identity.example", ClientId = "probe", ClientSecret = "probe-only", Audience = "probe"
        }));
        var settings = new ConfigurationShellBlueprint(Guid.NewGuid().ToString("N"), new ConfigurationBuilder().Build())
            .ComposeAsync().GetAwaiter().GetResult();
        new FoundationAuthenticationFeature(settings).ConfigureServices(services);
        new FoundationBffCookieFeature().ConfigureServices(services);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
