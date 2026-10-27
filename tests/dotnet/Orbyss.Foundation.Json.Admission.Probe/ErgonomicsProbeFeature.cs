using CShells.AspNetCore.Features;
using CShells.Features;
using Orbyss.Foundation.Json.AspNetCore;
using Orbyss.Foundation.Web.OpenApi;
using Microsoft.AspNetCore.OpenApi;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Composes operation registrations and mappings without owning their transport contracts.</summary>
[ShellFeature("JsonErgonomicsProbe", DependsOn = [typeof(FoundationJsonFeature), typeof(FoundationOpenApiFeature)])]
public sealed class ErgonomicsProbeFeature : IWebShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        Operations.ChangeValue.Endpoint.Register(services);
        Operations.ReadValue.Endpoint.Register(services);
        NumericProbeEndpoints.Register(services);
        services.AddSingleton<IJsonProfileExtension, CustomIntegerProfileExtension>();
        services.Configure<OpenApiOptions>("v1", ConfigureOpenApi);
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        Operations.ChangeValue.Endpoint.Map(endpoints);
        Operations.ReadValue.Endpoint.Map(endpoints);
        NumericProbeEndpoints.Map(endpoints);
    }

    /// <summary>Retains a custom converter's schema responsibility through the native producer extension point.</summary>
    private static void ConfigureOpenApi(OpenApiOptions options) => options.AddSchemaTransformer<CustomNumbersSchemaTransformer>();
}
