using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HostComposition.Fixture;

/// <summary>Selects ordinary ASP.NET writer replacement through an independent feature.</summary>
[ShellFeature(name: "HostComposition.CustomWriter")]
public sealed class CustomProblemWriterFeature : IWebShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IProblemDetailsWriter, CustomProblemWriter>();
        services.AddProblemDetails();
        services.AddScoped<CustomContribution>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) => Map(endpoints);

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/ready", (HttpContext context) => new
        {
            shell = context.RequestServices.GetRequiredService<CustomContribution>().Shell,
            handlers = context.RequestServices.GetServices<IExceptionHandler>().Count(),
            foundationWriters = context.RequestServices.GetServices<IProblemDetailsWriter>()
                .Count(writer => writer.GetType().Assembly.GetName().Name!.StartsWith("Orbyss.Foundation.")),
            foundationAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly =>
                assembly.GetName().Name!.StartsWith("Orbyss.Foundation.")
                && assembly.GetName().Name != "Orbyss.Foundation.Host").Select(assembly => assembly.GetName().Name).ToArray()
        });
        endpoints.MapPost("/only-post", () => Results.NoContent());
        endpoints.MapGet("/status/{status:int}", (int status) => Results.StatusCode(status));
        endpoints.MapGet("/written", () => Results.Text("preserved custom body", statusCode: 409));
        endpoints.MapGet("/started-failure", async (HttpContext context) =>
        {
            context.Response.StatusCode = 409;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("committed-prefix");
            await context.Response.Body.FlushAsync();
            throw new InvalidOperationException("CUSTOM_PRIVATE_STARTED_FAILURE");
        });
        endpoints.MapGet("/canceled", (HttpContext context) =>
        {
            context.RequestAborted = new CancellationToken(canceled: true);
            context.Response.StatusCode = 409;
        });
    }
}
