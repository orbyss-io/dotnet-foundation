using CShells.AspNetCore.Features;
using CShells.Features;
using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Exercises activation rejection with actual typed contracts and a selected incomplete resolver.</summary>
[ShellFeature("JsonMissingMetadataProbe", DependsOn = [typeof(FoundationJsonFeature)])]
public sealed class MissingMetadataProbeFeature : IWebShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        AdmissionContracts.Register(services);
        services.AddSingleton<IJsonProfileExtension, MissingMetadataExtension>();
    }
    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) =>
        endpoints.MapGet("/metadata", (IJsonResponseFactory<Message> responses) => responses.Create(new("unavailable")))
            .WithJsonResponse<Message>();
}
