using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.WebDefaults;

namespace Orbyss.Foundation.Authentication.SpaPkce;

/// <summary>Composes the direct SPA-PKCE bearer profile into one shell.</summary>
[ShellFeature(
    name: "Orbyss.Foundation.Authentication.SpaPkce",
    DisplayName = "Orbyss Foundation SPA-PKCE Authentication",
    Description = "Provides exact-origin CORS and JWT bearer validation for a public SPA client.",
    DependsOn = [typeof(FoundationAuthenticationFeature), typeof(FoundationWebDefaultsFeature)])]
public sealed class FoundationSpaPkceFeature : IWebShellFeature, IMiddlewareShellFeature
{
    /// <summary>Names the exact-origin direct-SPA CORS policy.</summary>
    private const string CorsPolicy = "orbyss-foundation-spa";

    /// <inheritdoc />
    public int Order => -800;

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IFoundationAuthenticationProfile, SpaPkceProfileMarker>();
        services.AddSingleton<IValidateOptions<FoundationWebOptions>, SpaPkceOptionsValidator>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(_ => { });
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<FoundationWebOptions>>((options, selected) =>
            {
                var settings = selected.Value;
                options.Authority = settings.Authority;
                if (!string.IsNullOrWhiteSpace(settings.BackchannelAuthority))
                {
                    options.MetadataAddress =
                        $"{settings.BackchannelAuthority.TrimEnd('/')}/.well-known/openid-configuration";
                }
                options.Audience = settings.Audience;
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = !settings.AllowHttpForLocalDevelopment;
                options.BackchannelTimeout = TimeSpan.FromSeconds(settings.DiscoveryTimeoutSeconds);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = settings.RoleClaim
                };
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await context.HttpContext.RequestServices.GetRequiredService<IAuthenticationErrorWriter>()
                            .WriteAsync(
                                context.HttpContext,
                                StatusCodes.Status401Unauthorized,
                                "authentication_required").ConfigureAwait(false);
                    },
                    OnForbidden = context => context.HttpContext.RequestServices
                        .GetRequiredService<IAuthenticationErrorWriter>()
                        .WriteAsync(
                            context.HttpContext,
                            StatusCodes.Status403Forbidden,
                            "authorization_denied")
                };
            });

        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<FoundationWebOptions>>((options, selected) =>
            options.AddPolicy(CorsPolicy, policy => policy
                .WithOrigins(selected.Value.AllowedOrigins)
                .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                .WithHeaders("Authorization", "Content-Type", FoundationWebDefaultsFeature.CorrelationHeaderName)
                .WithExposedHeaders(FoundationWebDefaultsFeature.CorrelationHeaderName)
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
    }

    /// <inheritdoc />
    public void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment)
    {
        app.UseCors(CorsPolicy);
        app.UseAuthentication();
        app.UseAuthorization();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
    }
}
