using CShells.AspNetCore.Features;
using CShells.Features;
using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Seeds prohibited native body binding behind strict-request metadata.</summary>
[ShellFeature("JsonNativeBindingProbe", DependsOn = [typeof(FoundationJsonFeature)])]
public sealed class NativeBindingProbeFeature : IWebShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) => AdmissionContracts.Register(services);
    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) =>
        endpoints.MapPost("/native", (Message body, IJsonResponseFactory<Message> responses) => responses.Create(body))
            .WithJsonRequest<Message>().WithJsonResponse<Message>();
}
