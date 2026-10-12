using System.Security.Claims;
using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Authentication.Core;
using Orbyss.Foundation.WebDefaults;

namespace Foundation.HostedAccessFixture;

/// <summary>Supplies synthetic session issuance and a separately protected data endpoint only in the local HTTP fixture package.</summary>
[ShellFeature(name: "Foundation.HostedAccessFixture", DependsOn = [typeof(Orbyss.Foundation.Authentication.BffCookie.FoundationBffCookieFeature)])]
public sealed class HostedAccessFixtureFeature : IWebShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) { }
    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        endpoints.MapGet("/fixture/session/{identity}", async (HttpContext context, string identity, IOptions<FoundationWebOptions> options) =>
        {
            string[] permissions = identity switch {
                "alpha" => ["pages.alpha"], "beta" => ["pages.beta"], "full" => ["pages.alpha", "pages.beta", "data.read"], _ => []
            };
            var claims = new List<Claim> {
                new(AuthenticationClaimTypes.ValidatedIssuer, options.Value.Authority),
                new(AuthenticationClaimTypes.ValidatedSubject, "synthetic-owner")
            };
            claims.AddRange(permissions.Select(permission => new Claim(options.Value.PermissionClaim, permission)));
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, "fixture")));
            return Results.NoContent();
        }).WithMetadata(new WebResponseMetadata(Private: true)).AllowAnonymous().ExcludeFromDescription();
        endpoints.MapGet("/api/data/{owner}", (HttpContext context, string owner) =>
            owner == context.User.FindFirst(AuthenticationClaimTypes.ValidatedSubject)?.Value ? Results.Text("synthetic owned data") : Results.NotFound())
            .WithMetadata(new WebResponseMetadata(Private: true)).RequireAuthorization(PermissionPolicyNames.ForPermission("data.read"));
    }
}
