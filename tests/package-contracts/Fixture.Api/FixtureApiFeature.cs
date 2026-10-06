using CShells;
using CShells.AspNetCore.Features;
using CShells.Features;
using Foundation.ContractFixture.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Json;
using Orbyss.Foundation.Json.AspNetCore;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;
namespace Foundation.ContractFixture.Api;
[ShellFeature("ContractFixture.Api", DependsOn = [typeof(FoundationAuthenticationFeature), typeof(FoundationJsonFeature)])]
public sealed class FixtureApiFeature(ShellSettings settings) : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IJsonProfileExtension, ValueSequenceJsonExtension>();
        services.AddScoped<IProblemDetailsEnricher, FixtureProblemEnricher>();
        services.AddSingleton<IProblemMapper<FixtureDenied>, FixtureProblemMapper>();
        if (settings.GetConfigurationRoot().GetValue<bool>("Fixture:ReplaceIdentityReader"))
            services.Replace(ServiceDescriptor.Singleton<IValidatedAccountIdentityReader, ControlledFixtureIdentityReader>());
        services.AddExceptionHandler<FixtureConflictExceptionHandler>();
        services.AddScoped<SnapshotEndpoint>();
        services.AddScoped<EchoEndpoint>();
        services.AddScoped<ProblemEndpoint>();
        services.AddScoped<StorageEndpoint>();
        services.AddScoped<ReloadEndpoint>();
        services.AddScoped<CookieEndpoint>();
        services.AddJsonRequestContract<WireMessage>(new(JsonProfileKeys.StrictRequest),
            new(JsonProfileKeys.StrictRequest, maximumBytes: 4096));
        services.AddJsonResponseContract<WireMessage>(new(JsonProfileKeys.SuccessResponse),
            new(JsonProfileKeys.TolerantResponse, maximumBytes: 4096));
        foreach (var register in new Action<IServiceCollection>[]
        {
            collection => collection.AddJsonResponseContract<SnapshotResponse>(new(FixtureApiKeys.InspectionProfile), new(JsonProfileKeys.TolerantResponse)),
            collection => collection.AddJsonResponseContract<FixtureStorageObservation>(new(FixtureApiKeys.InspectionProfile), new(JsonProfileKeys.TolerantResponse)),
            collection => collection.AddJsonResponseContract<FixtureCancellationObservation>(new(FixtureApiKeys.InspectionProfile), new(JsonProfileKeys.TolerantResponse)),
            collection => collection.AddJsonResponseContract<ReloadResponse>(new(FixtureApiKeys.InspectionProfile), new(JsonProfileKeys.TolerantResponse))
        }) register(services);
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        SnapshotEndpoint.Map(endpoints);
        EchoEndpoint.Map(endpoints);
        ProblemEndpoint.Map(endpoints);
        StorageEndpoint.Map(endpoints);
        ReloadEndpoint.Map(endpoints);
        CookieEndpoint.Map(endpoints);
    }
}
