using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Orbyss.Foundation.OpenApi.VersionProbe;

/// <summary>Provides a real endpoint for packed exporter version admission tests.</summary>
[ShellFeature("OpenApiVersionProbe")]
public sealed class VersionProbeFeature : IWebShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) => services.AddOpenApi("v1");

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) =>
        endpoints.MapGet("/probe", () => "packed exporter");
}
