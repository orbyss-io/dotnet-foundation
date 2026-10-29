using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orbyss.Foundation.Web.DynamicPages;

// Fictional package-loader fixture only; never included in the Foundation runtime family.
[ShellFeature("Foundation.DynamicPageFixture", DependsOn = [typeof(FoundationDynamicPagesFeature)])]
public sealed class DynamicHostedFixtureFeature : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<HostedStallState>();
        services.AddDynamicPageResolver<HostedStallResolver>("stalls");
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) =>
        endpoints.MapPost("/fixture/unpublish", (HostedStallState state) => { state.Published = false; return Results.NoContent(); }).AllowAnonymous();
}
