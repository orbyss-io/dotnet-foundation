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

/// <summary>Provides deterministic test-only login and data ownership, excluded from runtime packages and Host.</summary>
[ShellFeature(name: "HostedApplicationProbe")]
public sealed class HostedApplicationProbeFeature : IWebShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IPublicDocumentCatalog, ProbePublicDocuments>();
    }
    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        endpoints.MapGet("/fixture/session/{identity}", async (HttpContext context, string identity, IOptions<FoundationWebOptions> options) =>
        {
            var permissions = identity switch {
                "alpha" => new[] { AuthenticatedPagesProbe.AlphaPermission },
                "beta" => new[] { AuthenticatedPagesProbe.BetaPermission },
                "full" => new[] { AuthenticatedPagesProbe.AlphaPermission, AuthenticatedPagesProbe.BetaPermission, AuthenticatedPagesProbe.BootstrapPermission, AuthenticatedPagesProbe.DataPermission },
                _ => Array.Empty<string>()
            };
            var claims = new List<Claim> {
                new(AuthenticationClaimTypes.ValidatedIssuer, options.Value.Authority),
                new(AuthenticationClaimTypes.ValidatedSubject, "owner")
            };
            claims.AddRange(permissions.Select(permission => new Claim(options.Value.PermissionClaim, permission)));
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, "fixture")));
            return Results.NoContent();
        }).WithMetadata(new WebResponseMetadata(Private: true)).AllowAnonymous();
        endpoints.MapGet("/api/data/{owner}", (HttpContext context, string owner) =>
            owner == context.User.FindFirst(AuthenticationClaimTypes.ValidatedSubject)?.Value ? Results.Text("synthetic owned data") : Results.NotFound())
            .WithMetadata(new WebResponseMetadata(Private: true)).RequirePermission(AuthenticatedPagesProbe.DataPermission);
        endpoints.MapGet("/fixture/error", (Func<IResult>)(() => throw new InvalidOperationException(AuthenticatedPagesProbe.PrivateMarker))).WithMetadata(new WebResponseMetadata(Private: true)).RequireAuthorization();
        endpoints.MapGet("/fixture/redirect", () => Results.Redirect("/p/session")).WithMetadata(new WebResponseMetadata(Private: true)).RequireAuthorization();
    }
}
