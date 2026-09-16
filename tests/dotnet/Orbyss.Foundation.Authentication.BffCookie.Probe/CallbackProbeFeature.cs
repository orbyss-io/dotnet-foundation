using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Orbyss.Foundation.Authentication.BffCookie;

[ShellFeature("CallbackProbe", DependsOn = [typeof(FoundationBffCookieFeature)])]
public sealed class CallbackProbeFeature : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services) =>
        services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new OpenIdConnectConfiguration
            {
                Issuer = options.Authority,
                AuthorizationEndpoint = "https://identity.example/authorize",
                TokenEndpoint = "https://identity.example/token"
            });
            options.Events.OnMessageReceived = context =>
            {
                if (context.Request.Query.ContainsKey("skip-handler")) context.SkipHandler();
                return Task.CompletedTask;
            };
        });

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) { }
}
