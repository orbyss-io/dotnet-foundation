using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    public void ConfigureServices(IServiceCollection services) =>
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            if (!context.ProblemDetails.Extensions.TryGetValue("code", out var code) || code is null)
            {
                context.ProblemDetails.Extensions["code"] = CodeFor(context.HttpContext.Response.StatusCode);
            }
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        });

    /// <inheritdoc />
    public void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages(async statusContext =>
        {
            var response = statusContext.HttpContext.Response;
            await Results.Problem(
                statusCode: response.StatusCode,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = CodeFor(response.StatusCode),
                    ["traceId"] = statusContext.HttpContext.TraceIdentifier
                }).ExecuteAsync(statusContext.HttpContext).ConfigureAwait(false);
        });
    }

    /// <summary>Maps an HTTP status to the default stable problem code.</summary>
    private static string CodeFor(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => "authentication_required",
        StatusCodes.Status403Forbidden => "authorization_denied",
        StatusCodes.Status400BadRequest => "invalid_request",
        _ => "request_failed"
    };
}
