using System.Security.Claims;
using CShells;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Authentication.Core;
using Orbyss.Foundation.Json.AspNetCore;
namespace Foundation.ContractFixture.Api;
internal sealed class CookieEndpoint(ShellSettings settings, IOptions<FoundationWebOptions> options,
    IJsonResponseFactory<WireMessage> responses)
{
    public static void Map(IEndpointRouteBuilder endpoints) => endpoints.MapPost("/fixture-cookie/{permissions:int}",
        (CookieEndpoint endpoint, HttpContext context, int permissions) => endpoint.ExecuteAsync(context, permissions))
        .AllowAnonymous().WithJsonResponse<WireMessage>();
    public async Task<IResult> ExecuteAsync(HttpContext context, int permissions)
    {
        if (permissions is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(permissions));
        // This fixture issues controlled synthetic validated tickets solely to qualify the packaged cookie adapter.
        // Actual signed OIDC protocol acceptance remains the separate native OIDC conformance test.
        var claims = new List<Claim>
        {
            new(AuthenticationClaimTypes.ValidatedIssuer, "fixture-issuer"),
            new(AuthenticationClaimTypes.ValidatedSubject, settings.Id.ToString() + "-owner")
        };
        claims.AddRange(Enumerable.Range(0, permissions).Select(index => new Claim(options.Value.PermissionClaim,
            "fixture.permission." + index.ToString("D3"))));
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, "controlled-cookie-fixture")));
        return responses.Create(new("cookie-issued"));
    }
}
