using CShells;
using CShells.Features;
using CShells.AspNetCore.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
        services.AddSingleton(provider => new JsonProfileCatalog(
            provider.GetRequiredService<IOptions<FoundationJsonOptions>>().Value.Profiles,
            provider.GetServices<IJsonProfileExtension>()));
    }
    /// <inheritdoc />
    public void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment)
    {
        _ = app.ApplicationServices.GetRequiredService<JsonProfileCatalog>();
        app.Use(async (context, next) =>
        {
            try { await next(context).ConfigureAwait(false); }
            catch (JsonProfileException error) when (!context.Response.HasStarted)
            {
                context.Response.Clear();
                await Microsoft.AspNetCore.Http.Results.Problem(statusCode: error.Code == "json_size_exceeded" ? 413 : 400,
                    extensions: new Dictionary<string, object?> { ["code"] = error.Code, ["traceId"] = context.TraceIdentifier })
                    .ExecuteAsync(context).ConfigureAwait(false);
            }
        });
    }
}
