using CShells.AspNetCore.Features;
using CShells.Features;
using Orbyss.Foundation.Json;
using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Http.Probe;

/// <summary>Exercises actual shell-local typed JSON activation and response enforcement.</summary>
[ShellFeature("JsonHttpProbe", DependsOn = [typeof(FoundationJsonFeature)])]
public sealed class JsonHttpProbeFeature : IWebShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddJsonRequestContract<WireMessage>(new(JsonProfileKeys.StrictRequest), new(JsonProfileKeys.StrictRequest, maximumBytes: 1024));
        services.AddJsonResponseContract<WireMessage>(new(JsonProfileKeys.SuccessResponse), new(JsonProfileKeys.TolerantResponse, maximumBytes: 1024));
    }
    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        endpoints.MapPost("/echo", async (HttpContext context, IJsonRequestReader<WireMessage> reader, IJsonResponseFactory<WireMessage> responses) =>
            responses.Create(await reader.ReadAsync(context))).WithJsonRequest<WireMessage>().WithJsonResponse<WireMessage>();
        endpoints.MapGet("/large", (IJsonResponseFactory<WireMessage> responses) => responses.Create(new(new string('a', 100)))).WithJsonResponse<WireMessage>();
        endpoints.MapGet("/bypass", () => Results.Ok(new WireMessage("bypass"))).WithJsonResponse<WireMessage>();
        endpoints.MapGet("/invalid-output", (IJsonResponseFactory<WireMessage> responses) => responses.Create(new(null!))).WithJsonResponse<WireMessage>();
        endpoints.MapGet("/unicode", (IJsonResponseFactory<WireMessage> responses) => responses.Create(new("😀é"))).WithJsonResponse<WireMessage>();
    }
}
