using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orbyss.Foundation.Json;
using Orbyss.Foundation.Json.AspNetCore;
using Orbyss.Foundation.Web.OpenApi;
using System.Text.Json.Serialization;

namespace Orbyss.Foundation.OpenApi.ClosureProbe;

[ShellFeature("ClosureProbe", DependsOn = [typeof(FoundationJsonFeature), typeof(FoundationOpenApiFeature), typeof(EffectOnlyProbe)])]
public sealed class ClosureProbeFeature : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddJsonRequestContract<ProbeInput>(new(JsonProfileKeys.StrictRequest), new(JsonProfileKeys.StrictRequest, maximumBytes: 4096));
        services.AddJsonResponseContract<ProbeInput>(new(JsonProfileKeys.SuccessResponse), new(JsonProfileKeys.TolerantResponse, maximumBytes: 8192));
        services.AddSingleton<IHostedService>(_ => throw new InvalidOperationException("BUSINESS_HOSTED_SERVICE_EXECUTED"));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) =>
        endpoints.MapPost("/probe", (HttpContext context) => Reject(context))
            .WithJsonRequest<ProbeInput>().WithJsonResponse<ProbeInput>();

    private static IResult Reject(HttpContext context) => throw new InvalidOperationException("BUSINESS_ENDPOINT_EXECUTED");
}

[JsonNumberHandling(JsonNumberHandling.Strict)]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProbeInput(int Count, decimal Amount);

[ShellFeature("ClosureProbe.EffectOnly")]
public sealed class EffectOnlyProbe : IShellFeature
{
    public void ConfigureServices(IServiceCollection services) => throw new InvalidOperationException("EFFECT_ONLY_FEATURE_EXECUTED");
}
