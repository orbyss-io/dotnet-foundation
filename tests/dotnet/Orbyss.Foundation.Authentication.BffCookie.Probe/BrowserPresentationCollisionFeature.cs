using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Deliberate independent application-page collision for native publication refusal.</summary>
[ShellFeature("PresentationCollision")]
public sealed class BrowserPresentationCollisionFeature : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services) { }
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) =>
        endpoints.MapGet("/auth/sign-in", () => Results.Text("must never publish")).AllowAnonymous();
}
