using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Orbyss.Foundation.WebDefaults;

/// <summary>Owns final response policies within one shell pipeline.</summary>
public sealed class WebResponsePoliciesMiddleware
{
    /// <summary>Stores the downstream pipeline.</summary>
    private readonly RequestDelegate next;
    /// <summary>Stores the immutable shell catalog.</summary>
    private readonly WebResponsePolicyCatalog catalog;

    /// <summary>Validates mapped endpoint selectors and captures the shell catalog.</summary>
    public WebResponsePoliciesMiddleware(RequestDelegate next, WebResponsePolicyCatalog catalog, IEnumerable<EndpointDataSource> sources)
    {
        this.next = next;
        this.catalog = catalog;
        foreach (var endpoint in sources.SelectMany(source => source.Endpoints)) _ = catalog.Resolve(endpoint);
    }

    /// <summary>Registers final header ownership before downstream middleware can clear a response.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var policy = catalog.Resolve(null);
        WebResponseContributions.Initialize(context, catalog);
        context.Response.OnStarting(() =>
        {
            // Resolve the actual endpoint again: late policy/resource changes can only retain
            // a nonce if its final policy still admits that capability.
            var finalEndpoint = context.GetEndpoint();
            try { policy = catalog.Resolve(finalEndpoint); }
            catch (InvalidOperationException)
            {
                policy = catalog.Resolve(null);
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }
            var metadata = finalEndpoint?.Metadata.GetOrderedMetadata<WebResponseMetadata>() ?? [];
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.ContentSecurityPolicy = WebResponseContributions.Compose(context, policy);
            headers["Referrer-Policy"] = policy.ReferrerPolicy;
            headers["Permissions-Policy"] = policy.PermissionsPolicy;
            var isPrivate = metadata.Any(item => item.Private);
            var publicAsset = metadata.Any(item => item.ImmutablePublicAsset);
            var cacheableStatus = context.Response.StatusCode is 200 or 206 or 304;
            var safeMethod = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
            var cache = !isPrivate && publicAsset && cacheableStatus && safeMethod &&
                !headers.ContainsKey("Set-Cookie") && !WebResponseContributions.HasIssuedStyleNonce(context) &&
                policy.PublicAssetMaxAgeSeconds > 0;
            headers.CacheControl = cache ? $"public, max-age={policy.PublicAssetMaxAgeSeconds}, immutable" : "no-store";
            if (!cache) { headers.Remove("Expires"); headers.Remove("Age"); }
            var restricted = isPrivate || context.Response.StatusCode >= 400;
            var noIndex = restricted || !policy.AllowIndexing || metadata.Any(item => item.NoIndex);
            var noFollow = restricted || !policy.AllowFollowing || metadata.Any(item => item.NoFollow);
            if (noIndex || noFollow)
                headers["X-Robots-Tag"] = noIndex && noFollow ? "noindex, nofollow" : noIndex ? "noindex" : "nofollow";
            else headers.Remove("X-Robots-Tag");
            return Task.CompletedTask;
        });
        try { policy = catalog.Resolve(endpoint); }
        catch (InvalidOperationException)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { code = "web_policy_invalid", traceId = context.TraceIdentifier },
                cancellationToken: context.RequestAborted).ConfigureAwait(false);
            return;
        }
        await next(context).ConfigureAwait(false);
    }
}
