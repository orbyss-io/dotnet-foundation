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
        group.MapGet("/indexed", (HttpContext context) =>
        {
            context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            return Results.Text("public JSON", "application/json");
        }).WithMetadata(new WebResponseMetadata(Policy: "indexed"));
        group.MapGet("/nofollow", () => "restricted").WithMetadata(new WebResponseMetadata(Policy: "indexed") { NoFollow = true });
        group.MapGet("/noindex", () => "restricted").WithMetadata(new WebResponseMetadata(Policy: "indexed", NoIndex: true));
        group.MapGet("/policy-nofollow", () => "restricted").WithMetadata(new WebResponseMetadata(Policy: "nofollow"));
        group.MapGet("/editor", (HttpContext context) => Nonce(context)).WithMetadata(new WebResponseMetadata(Policy: "editor", Private: true));
        group.MapGet("/editor-public", (HttpContext context) => Nonce(context)).WithMetadata(new WebResponseMetadata(Policy: "editor", ImmutablePublicAsset: true));
        group.MapGet("/denied", (HttpContext context) => Nonce(context)).WithMetadata(new WebResponseMetadata(Policy: "denied"));
        group.MapGet("/late-denied", (HttpContext context) =>
        {
            var nonce = Nonce(context);
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new WebResponseMetadata(Policy: "denied")), "late"));
            return nonce;
        }).WithMetadata(new WebResponseMetadata(Policy: "editor"));
        group.MapGet("/late-denied-public", (HttpContext context) =>
        {
            var nonce = Nonce(context);
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new WebResponseMetadata(Policy: "denied-public", ImmutablePublicAsset: true)), "late-public"));
            return Results.Text($"<style nonce=\"{nonce}\">body {{ color: blue; }}</style>", "text/html");
        }).WithMetadata(new WebResponseMetadata(Policy: "editor"));
        group.MapGet("/late-private", (HttpContext context) =>
        {
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new WebResponseMetadata(Policy: "indexed", Private: true)), "late"));
            return "private";
        }).WithMetadata(new WebResponseMetadata(Policy: "indexed"));
        group.MapGet("/nonce-error", (HttpContext context) =>
        {
            _ = Nonce(context);
            return Results.StatusCode(500);
        }).WithMetadata(new WebResponseMetadata(Policy: "editor"));
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
    /// <summary>Requests the maintained contribution twice and attempts an authored header override.</summary>
    private static string Nonce(HttpContext context)
    {
        if (!context.TryGetStyleNonce(out var first)) return "denied";
        if (!context.TryGetStyleNonce(out var second) || first != second) throw new InvalidOperationException("nonce changed within response");
        context.Response.Headers.ContentSecurityPolicy = "default-src *; style-src 'unsafe-inline'";
        return first;
    }
    /// <summary>Simulates an unhandled endpoint failure.</summary>
    private static string Throw() => throw new InvalidOperationException("probe failure");
}
