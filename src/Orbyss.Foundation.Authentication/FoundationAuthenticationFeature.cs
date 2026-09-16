using CShells;
using CShells.Features;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
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
        var configuration = settings.GetConfigurationRoot().GetSection(FoundationWebOptions.SectionName);
        services.Configure<FoundationWebOptions>(options =>
        {
            // IConfiguration.Exists excludes explicit null/empty selections. Presence must
            // be checked on the parent so these never restore permission-broadening defaults.
            var selection = configuration.GetChildren().FirstOrDefault(child =>
                child.Key.Equals(nameof(FoundationWebOptions.Scopes), StringComparison.OrdinalIgnoreCase));
            if (selection is null) return;
            var entries = selection.GetChildren().ToArray();
            if (selection.Value is not null || entries.Length == 0
                || entries.Where((entry, index) => entry.Key != index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    || entry.Value is null || entry.GetChildren().Any()).Any())
            {
                throw new OptionsValidationException(Options.DefaultName, typeof(FoundationWebOptions),
                    ["Foundation:Web:Scopes must be a non-empty indexed array of scope tokens including openid."]);
            }
            // The binder appends to initialized arrays; clear only an explicit selection.
            options.Scopes = [];
        });
        services.Configure<FoundationWebOptions>(configuration);
        services.AddSingleton<IValidateOptions<FoundationWebOptions>, FoundationWebOptionsValidator>();
        services.AddAuthorizationBuilder().SetFallbackPolicy(
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddTransient<IClaimsTransformation, PermissionClaimsTransformation>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.TryAddSingleton<IAuthenticationErrorWriter, DefaultAuthenticationErrorWriter>();
    }
}
