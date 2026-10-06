using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HostComposition.Fixture;

/// <summary>Selects a custom ASP.NET problem service with no Foundation registration.</summary>
[ShellFeature(name: "HostComposition.CustomService")]
public sealed class CustomProblemServiceFeature : IWebShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<CustomProblemWriter>();
        services.Replace(ServiceDescriptor.Singleton<IProblemDetailsService, CustomProblemService>());
        services.AddScoped<CustomContribution>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) => CustomProblemWriterFeature.Map(endpoints);
}
