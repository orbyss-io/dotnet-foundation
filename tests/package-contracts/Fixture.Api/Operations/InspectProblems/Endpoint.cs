using Foundation.ContractFixture.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Authentication.Core;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;
namespace Foundation.ContractFixture.Api;
internal sealed class ProblemEndpoint(IProblemMapper<FixtureDenied> mapper, IAuthenticationErrorWriter authentication)
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/denied", (ProblemEndpoint endpoint) => endpoint.Denied()).AllowAnonymous();
        endpoints.MapGet("/auth/{status:int}", (ProblemEndpoint endpoint, HttpContext context, int status) => endpoint.AuthAsync(context, status)).AllowAnonymous();
        endpoints.MapGet("/status/{status:int}", (int status) => Results.StatusCode(status)).AllowAnonymous();
        endpoints.MapGet("/native-conflict", (HttpContext _) => throw new FixtureConflictException()).AllowAnonymous();
        endpoints.MapGet("/argument", (HttpContext _) => throw new ArgumentException(FixtureApiKeys.PrivateMarker + " password SQL submitted body")).AllowAnonymous();
        endpoints.MapGet("/problem-large", () => FoundationProblemResults.Problem(new ProblemDefinition(409, "fixture_conflict",
            detail: new string('漢', 1024)))).AllowAnonymous();
        endpoints.MapGet("/started", async (HttpContext context) =>
        {
            await context.Response.WriteAsync("committed");
            await context.Response.Body.FlushAsync();
            throw new InvalidOperationException(FixtureApiKeys.PrivateMarker + " password SQL submitted body");
        }).AllowAnonymous();
    }
    public IResult Denied() => FoundationProblemResults.Problem(new FixtureDenied(), mapper);
    public Task AuthAsync(HttpContext context, int status) => authentication.WriteAsync(context, status,
        status == 401 ? AuthenticationErrorCodes.AuthenticationRequired : AuthenticationErrorCodes.AuthorizationDenied);
}
