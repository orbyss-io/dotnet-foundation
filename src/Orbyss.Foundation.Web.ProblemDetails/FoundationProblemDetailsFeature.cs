using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Diagnostics;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Composes the optional Orbyss Foundation Problem Details policy into a shell.</summary>
[ShellFeature(
    name: "Orbyss.Foundation.Web.ProblemDetails",
    DisplayName = "Orbyss Foundation Problem Details",
    Description = "Provides a replaceable default exception and status-code response format.")]
public sealed class FoundationProblemDetailsFeature : IMiddlewareShellFeature
{
    /// <inheritdoc />
    public int Order => -900;

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddFoundationProblemDetails();
        services.AddExceptionHandler<FoundationBadHttpRequestExceptionHandler>();
        // Native raw-exception diagnostics can contain credentials or SQL. The policy and known
        // handlers emit bounded classifications with correlation instead, including on .NET 10.
        services.Configure<ExceptionHandlerOptions>(options =>
        {
            options.SuppressDiagnosticsCallback = _ => true;
            options.ExceptionHandler = context => new FoundationProblemResult(
                new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 500 },
                context.Features.Get<IExceptionHandlerFeature>()?.Error).ExecuteAsync(context);
        });
    }

    /// <inheritdoc />
    public void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment)
    {
        _ = app.ApplicationServices.GetRequiredService<IProblemDetailsService>();
        app.UseExceptionHandler();
        app.UseFoundationProblemStatusCodePages();
    }
}
