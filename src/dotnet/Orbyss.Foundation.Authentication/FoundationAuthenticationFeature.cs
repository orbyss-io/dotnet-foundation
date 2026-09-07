using CShells;
using CShells.Features;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Orbyss.Foundation.Authentication;

/// <summary>Composes profile-independent authentication and permission services into a shell.</summary>
[ShellFeature(
    name: "Orbyss.Foundation.Authentication",
    DisplayName = "Orbyss Foundation Authentication",
    Description = "Provides validated authentication configuration and canonical permission policies.")]
public sealed class FoundationAuthenticationFeature(ShellSettings settings) : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<FoundationWebOptions>(
            settings.GetConfigurationRoot().GetSection(FoundationWebOptions.SectionName));
        services.AddSingleton<IValidateOptions<FoundationWebOptions>, FoundationWebOptionsValidator>();
        services.AddAuthorizationBuilder().SetFallbackPolicy(
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddTransient<IClaimsTransformation, PermissionClaimsTransformation>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.TryAddSingleton<IAuthenticationErrorWriter, DefaultAuthenticationErrorWriter>();
    }
}
