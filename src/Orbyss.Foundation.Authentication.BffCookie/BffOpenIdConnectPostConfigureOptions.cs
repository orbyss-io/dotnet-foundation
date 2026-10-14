using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Extends native default transport/retrieval with failure provenance without replacing externally supplied clients or managers.</summary>
internal sealed class BffOpenIdConnectPostConfigureOptions : IPostConfigureOptions<OpenIdConnectOptions>
{
    /// <inheritdoc />
    public void PostConfigure(string? name, OpenIdConnectOptions options)
    {
        if (name != OpenIdConnectDefaults.AuthenticationScheme) return;
        if (options.Backchannel is null)
        {
            options.Backchannel = new HttpClient(new BffProviderBackchannelHandler(options.BackchannelHttpHandler ?? new HttpClientHandler()));
            options.Backchannel.DefaultRequestHeaders.UserAgent.ParseAdd("Microsoft ASP.NET Core OpenIdConnect handler");
            options.Backchannel.Timeout = options.BackchannelTimeout;
            options.Backchannel.MaxResponseContentBufferSize = 1024 * 1024 * 10;
        }
        if (options.ConfigurationManager is not null || options.Configuration is not null) return;
        if (string.IsNullOrEmpty(options.MetadataAddress) && !string.IsNullOrEmpty(options.Authority))
            options.MetadataAddress = options.Authority.TrimEnd('/') + "/.well-known/openid-configuration";
        if (string.IsNullOrEmpty(options.MetadataAddress)) return;
        if (options.RequireHttpsMetadata && !options.MetadataAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The MetadataAddress or Authority must use HTTPS unless disabled for development.");
        // Keep the actual native BaseConfigurationManager for refresh, rollover and last-known-good token validation.
        options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(options.MetadataAddress,
            new OpenIdConnectConfigurationRetriever(), new BffProviderDocumentRetriever(
                new HttpDocumentRetriever(options.Backchannel) { RequireHttps = options.RequireHttpsMetadata }))
        {
            RefreshInterval = options.RefreshInterval,
            AutomaticRefreshInterval = options.AutomaticRefreshInterval
        };
    }
}
