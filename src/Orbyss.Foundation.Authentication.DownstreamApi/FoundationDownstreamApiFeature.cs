using CShells;
using CShells.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Orbyss.Foundation.Authentication.DownstreamApi;

/// <summary>Composes governed authenticated downstream HTTP calls into one shell.</summary>
[ShellFeature(
    name: "Orbyss.Foundation.Authentication.DownstreamApi",
    DisplayName = "Orbyss Foundation Authenticated Downstream APIs",
    Description = "Calls configured APIs with client credentials or downscoped token exchange.")]
public sealed class FoundationDownstreamApiFeature(ShellSettings settings) : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<DownstreamApiOptions>(
            settings.GetConfigurationRoot().GetSection(DownstreamApiOptions.SectionName));
        services.AddSingleton<IValidateOptions<DownstreamApiOptions>, DownstreamApiOptionsValidator>();
        services.AddHttpContextAccessor();
        services.TryAddSingleton<ICurrentAccessTokenAccessor, HttpContextAccessTokenAccessor>();
        services.AddHttpClient(DownstreamApiClient.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddSingleton<IDownstreamApiClient, DownstreamApiClient>();
    }
}
