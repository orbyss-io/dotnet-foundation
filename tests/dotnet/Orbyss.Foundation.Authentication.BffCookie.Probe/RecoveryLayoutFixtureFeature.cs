using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orbyss.Foundation.Web.Presentation;

[ShellFeature("RecoveryLayoutFixture")]
public sealed class RecoveryLayoutFixtureFeature : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services) => services.AddSingleton<IBrowserRecoveryPageRenderer, RecoveryLayoutFixtureRenderer>();
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) { }
}
