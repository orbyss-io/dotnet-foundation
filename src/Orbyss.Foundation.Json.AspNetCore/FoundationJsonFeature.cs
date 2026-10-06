using CShells;
using CShells.Features;
using CShells.AspNetCore.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;
using Microsoft.AspNetCore.OpenApi;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Compiles shell-local JSON profiles without adding public endpoints.</summary>
[ShellFeature(name: "Orbyss.Foundation.Json.AspNetCore", DisplayName = "Typed JSON profiles")]
public sealed class FoundationJsonFeature(ShellSettings settings) : IMiddlewareShellFeature
{
    /// <inheritdoc />
    public int Order => -850;
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<FoundationJsonOptions>(settings.GetConfigurationRoot().GetSection("Foundation:Json"));
        services.AddFoundationProblemDetails();
        services.AddExceptionHandler<FoundationJsonRequestExceptionHandler>();
        services.AddExceptionHandler<FoundationJsonResponseExceptionHandler>();
        services.AddOptions<FoundationProblemResponseOptions>().Configure<JsonProfileCatalog>((options, catalog) =>
        {
            var profile = catalog.Get(new JsonProfileKey(JsonProfileKeys.ProblemResponse));
            options.MaxBytes = profile.MaxBytes;
            options.MaxDepth = profile.MaxDepth;
            options.Preset = profile.Preset;
        });
        services.Configure<OpenApiOptions>("v1", options => options.AddOperationTransformer<JsonContractOperationTransformer>());
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<FoundationJsonOptions>>().Value;
            options.Paging.Validate();
            var catalog = new JsonProfileCatalog(options.Profiles, provider.GetServices<IJsonProfileExtension>());
            var contracts = provider.GetServices<IJsonContractMetadata>().ToArray();
            if (contracts.GroupBy(contract => (contract.ContractType, contract.Direction)).Any(group => group.Count() != 1))
                throw new InvalidOperationException("Duplicate typed JSON contract registrations.");
            foreach (var contract in contracts) contract.Validate(catalog);
            return catalog;
        });
    }
    /// <inheritdoc />
    public void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment)
    {
        _ = app.ApplicationServices.GetRequiredService<JsonProfileCatalog>();
        _ = app.ApplicationServices.GetRequiredService<IProblemDetailsService>();
        app.Use(async (context, next) =>
        {
            try { await next(context).ConfigureAwait(false); }
            catch (JsonProfileException error) when (!context.Response.HasStarted)
            {
                await WriteFailureAsync(context, new ProblemDefinition(error.Code == JsonFailureCodes.SizeExceeded ? 413 : 400, error.Code)).ConfigureAwait(false);
            }
            catch (JsonResponseContractException error) when (!context.Response.HasStarted)
            {
                await WriteFailureAsync(context, new ProblemDefinition(500, error.Code)).ConfigureAwait(false);
            }
            catch (BadHttpRequestException error) when (!context.Response.HasStarted && error.StatusCode is 400 or 413)
            {
                await WriteFailureAsync(context, new ProblemDefinition(error.StatusCode,
                    error.StatusCode == 413 ? JsonFailureCodes.SizeExceeded : "invalid_request")).ConfigureAwait(false);
            }
        });
    }
    /// <summary>Restores guarded transport ownership before writing a separately bounded common failure.</summary>
    private static async Task WriteFailureAsync(HttpContext context, ProblemDefinition definition)
    {
        var original = context.Features.Get<JsonAdmissionState>()?.OriginalResponseBody;
        if (original is not null) context.Features.Set(original);
        context.Response.Clear();
        await FoundationProblemResults.Problem(definition).ExecuteAsync(context).ConfigureAwait(false);
        if (definition.StatusCode >= 500)
            context.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("Orbyss.Foundation.Json.Response")
                .LogError("Server JSON contract failed with code {FailureCode} and correlation {CorrelationId}.", definition.Code, context.TraceIdentifier);
    }
}
