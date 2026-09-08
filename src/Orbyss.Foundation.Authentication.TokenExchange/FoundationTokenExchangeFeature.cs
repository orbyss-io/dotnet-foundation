using CShells;
using CShells.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Orbyss.Foundation.Authentication.TokenExchange;

/// <summary>Composes provider-neutral OAuth token exchange into one shell.</summary>
[ShellFeature(
    name: "Orbyss.Foundation.Authentication.TokenExchange",
    DisplayName = "Orbyss Foundation OAuth Token Exchange",
    Description = "Performs named, downscoped OAuth RFC 8693 token exchanges.")]
public sealed class FoundationTokenExchangeFeature(ShellSettings settings) : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TokenExchangeOptions>(
            settings.GetConfigurationRoot().GetSection(TokenExchangeOptions.SectionName));
        services.AddSingleton<IValidateOptions<TokenExchangeOptions>, TokenExchangeOptionsValidator>();
        services.AddHttpClient(TokenExchangeService.HttpClientName);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ITokenExchangeService, TokenExchangeService>();
    }
}
