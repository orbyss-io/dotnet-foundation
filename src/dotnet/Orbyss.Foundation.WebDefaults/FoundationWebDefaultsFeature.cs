using System.Globalization;
using CShells;
using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Orbyss.Foundation.WebDefaults;

/// <summary>Composes Orbyss Foundation's replaceable web middleware defaults into a shell.</summary>
[ShellFeature(
    name: "Orbyss.Foundation.WebDefaults",
    DisplayName = "Orbyss Foundation Web Defaults",
    Description = "Provides localization, HSTS, correlation, and browser-security headers.")]
public sealed class FoundationWebDefaultsFeature(ShellSettings settings) : IMiddlewareShellFeature
{
    /// <summary>Names the correlation header emitted by the default web feature.</summary>
    public const string CorrelationHeaderName = "X-Correlation-ID";

    /// <inheritdoc />
    public int Order => -1000;

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<FoundationWebDefaultsOptions>(
            settings.GetConfigurationRoot().GetSection("Foundation:Web"));
        services.AddSingleton<IValidateOptions<FoundationWebDefaultsOptions>, FoundationWebDefaultsOptionsValidator>();
        services.AddLocalization();
        services.AddOptions<RequestLocalizationOptions>()
            .Configure<IOptions<FoundationWebDefaultsOptions>>((options, selected) =>
            {
                var cultures = selected.Value.SupportedLocales.Select(CultureInfo.GetCultureInfo).ToArray();
                options.DefaultRequestCulture = new RequestCulture(selected.Value.DefaultLocale);
                options.SupportedCultures = cultures;
                options.SupportedUICultures = cultures;
                options.ApplyCurrentCultureToResponseHeaders = true;
            });
    }

    /// <inheritdoc />
    public void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment)
    {
        app.UseMiddleware<CorrelationAndSecurityHeadersMiddleware>();
        app.UseRequestLocalization();
        if (environment?.IsDevelopment() != true)
        {
            app.UseHsts();
        }
    }
}
