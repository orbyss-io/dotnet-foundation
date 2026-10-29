using System.Security.Claims;
using CShells;
using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Authentication.Core;
using Orbyss.Foundation.Web.DynamicPages;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.WebDefaults;

[ShellFeature(name: "DynamicFixture", DependsOn = [typeof(FoundationDynamicPagesFeature)])]
public sealed class DynamicFixtureFeature(ShellSettings settings) : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<DynamicFixtureState>();
        services.AddDynamicPageResolver<DynamicFixtureResolver>("stalls");
        if (settings.Id.ToString() == "duplicate-resolver") services.AddDynamicPageResolver<DynamicFixtureResolver>("stalls");
        if (settings.Id.ToString() == "custom") services.AddSingleton<IDynamicPageRenderer, DynamicFixtureRenderer>();
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        endpoints.MapGet("/fixture/session/{permission}", async (HttpContext context, IOptions<FoundationWebOptions> web) =>
        {
            var claims = new List<Claim> { new(AuthenticationClaimTypes.ValidatedIssuer, web.Value.Authority),
                new(AuthenticationClaimTypes.ValidatedSubject, "owner") };
            if ((string?)context.Request.RouteValues["permission"] == "yes") claims.Add(new Claim(web.Value.PermissionClaim, "stalls.read"));
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, "fixture")));
            return Results.NoContent();
        }).AllowAnonymous();
        endpoints.MapGet("/api/private", () => Results.Json(new { value = "private-api" }))
            .RequireAuthorization(PermissionPolicyNames.ForPermission("stalls.read"))
            .WithMetadata(new WebResponseMetadata(Private: true), new ProblemResponsePurposeMetadata(ProblemResponsePurpose.Machine));
        endpoints.MapGet("/application/map.mjs", () => Results.Text("window.dynamicApplicationLoaded=true;", "text/javascript"))
            .AllowAnonymous().WithMetadata(new WebResponseMetadata(NoIndex: true));
        endpoints.MapGet("/application/map.css", () => Results.Text("html,body{margin:0}#foundation-application{min-height:70vh;width:100%}", "text/css"))
            .AllowAnonymous().WithMetadata(new WebResponseMetadata(NoIndex: true));
    }
}
