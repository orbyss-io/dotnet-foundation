using CShells.AspNetCore.Features;
using CShells.Features;
using Orbyss.Foundation.WebDefaults;
using Orbyss.Foundation.Json;

namespace Orbyss.Foundation.WebDefaults.Probe;

/// <summary>Exercises real shell endpoint metadata and response clearing.</summary>
[ShellFeature("PolicyProbe")]
public sealed class PolicyProbeFeature : IWebShellFeature, IMiddlewareShellFeature
{
    /// <inheritdoc />
    public int Order => -800;
    /// <inheritdoc />
    public void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment) =>
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Value?.EndsWith("/reject", StringComparison.Ordinal) == true)
                context.Response.StatusCode = 403;
            else await next(context);
        });
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) { }
    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        var group = endpoints.MapGroup("").WithMetadata(new WebResponseMetadata(Feature: "PolicyProbe"));
        group.MapGet("/ok", () => "ok");
        group.MapPost("/json", async (HttpContext context, JsonProfileCatalog profiles) =>
            await profiles.Get("strict-request").ReadAsync<Dictionary<string, string>>(context.Request.Body, context.RequestAborted));
        group.MapGet("/asset", () => Results.Text("immutable")).WithMetadata(new WebResponseMetadata(Policy: "public-asset", ImmutablePublicAsset: true));
        group.MapGet("/reject", () => "must not execute").WithMetadata(new WebResponseMetadata(Policy: "public-asset", ImmutablePublicAsset: true));
        group.MapGet("/cookie", (HttpContext context) => { context.Response.Cookies.Append("probe", "value"); return "ok"; })
            .WithMetadata(new WebResponseMetadata(Policy: "public-asset", ImmutablePublicAsset: true));
        group.MapGet("/private", () => "secret").WithMetadata(new WebResponseMetadata(Policy: "public-asset", Private: true, ImmutablePublicAsset: true));
        group.MapGet("/error", (HttpContext _) => Throw()).WithMetadata(new WebResponseMetadata(Policy: "public-asset", ImmutablePublicAsset: true));
        group.MapGet("/missing-asset", () => Results.NotFound()).WithMetadata(new WebResponseMetadata(Policy: "public-asset", ImmutablePublicAsset: true));
        group.MapGet("/conflict", (HttpContext context) =>
        {
            context.Response.Headers.XFrameOptions = "SAMEORIGIN";
            context.Response.Headers.CacheControl = "public";
            return "ok";
        });
        group.MapGet("/conditional", () => Results.StatusCode(304)).WithMetadata(new WebResponseMetadata(Policy: "public-asset", ImmutablePublicAsset: true));
    }
    /// <summary>Simulates an unhandled endpoint failure.</summary>
    private static string Throw() => throw new InvalidOperationException("probe failure");
}
