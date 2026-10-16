using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;

/// <summary>Real HTTP purpose/admission/representation checks, separate from product-page acceptance.</summary>
internal static class BrowserProblemBoundaryProbe
{
    /// <summary>Runs native writer negotiation and bounded renderer failure cases.</summary>
    public static async Task RunAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var log = new RecordingLoggerProvider();
        builder.Logging.AddProvider(log);
        var renderer = new ProbePageRenderer();
        builder.Services.AddSingleton<IProblemPageRenderer>(renderer);
        var feature = new FoundationProblemDetailsFeature();
        feature.ConfigureServices(builder.Services);
        await using var app = builder.Build();
        feature.UseMiddleware(app, app.Environment);
        var browser = new ProblemResponsePurposeMetadata(ProblemResponsePurpose.BrowserPage);
        var machine = new ProblemResponsePurposeMetadata(ProblemResponsePurpose.Machine);
        app.MapMethods("/browser", ["GET", "HEAD"], () => FoundationProblemResults.Problem(
            new ProblemDefinition(403, "safe_page", title: "<script>encoded title</script>"))).WithMetadata(browser);
        app.MapGet("/machine", (HttpContext context) =>
        {
            context.Features.Set(browser); // Native endpoint purpose must win.
            return FoundationProblemResults.Problem(new ProblemDefinition(401, "machine_contract"));
        }).WithMetadata(machine);
        app.MapGet("/protocol-machine", (HttpContext context) =>
        {
            context.Features.Set(machine);
            return FoundationProblemResults.Problem(new ProblemDefinition(401, "machine_contract"));
        }).WithMetadata(browser);
        app.MapGet("/unclassified", () => FoundationProblemResults.Problem(new ProblemDefinition(404, "unclassified")));
        app.MapGet("/unknown-purpose", (HttpContext context) =>
        {
            context.Features.Set(browser);
            return FoundationProblemResults.Problem(new ProblemDefinition(400, "unknown_purpose"));
        }).WithMetadata(new ProblemResponsePurposeMetadata((ProblemResponsePurpose)99));
        app.MapGet("/machine-exception", (HttpContext context) => throw new InvalidOperationException("PRIVATE machine exception"))
            .WithMetadata(machine);
        app.MapGet("/renderer/{code}", (string code) => FoundationProblemResults.Problem(new ProblemDefinition(409, code)))
            .WithMetadata(browser);
        app.MapGet("/exception", (HttpContext context) => throw new InvalidOperationException("PRIVATE exception input"))
            .WithMetadata(browser);
        app.MapGet("/invalid", () => Results.Problem(statusCode: 400,
            extensions: new Dictionary<string, object?> { ["code"] = new { password = "PRIVATE" } })).WithMetadata(browser);
        app.MapGet("/cancel", (HttpContext context) =>
        {
            context.RequestAborted = new CancellationToken(canceled: true);
            return FoundationProblemResults.Problem(new ProblemDefinition(400, "cancelled"));
        }).WithMetadata(browser);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        foreach (var path in new[] { "/machine", "/protocol-machine", "/unclassified", "/unknown-purpose", "/machine-exception" })
            await Check(client, path, "text/html", "application/problem+json");
        Require(renderer.Seen.Count == 0, "Machine/default requests reached a page renderer.");
        foreach (var accept in new[] { "application/json", "*/*", "text/html;q=0,application/json", "text/html;q=0.5,application/json", "malformed" })
            await Check(client, "/browser", accept, "application/problem+json");
        Require(renderer.Seen.Count == 0, "Unaccepted HTML reached a page renderer.");
        var html = await Check(client, "/browser", "text/html,application/json;q=0.5", "text/html");
        Require(html.Contains("&lt;script&gt;") && !html.Contains("<script>"), "Admitted title was not HTML encoded.");
        foreach (var path in new[] { "/exception", "/invalid" })
        {
            await Check(client, path, "text/html", "text/html", 500);
            Require(renderer.Seen.Last().Code == ProblemCodes.RequestFailed, "Raw invalid problem bypassed native admission.");
        }
        foreach (var code in new[] { "renderer_throw", "renderer_large", "renderer_invalid_utf8" })
        {
            var fallback = await Check(client, "/renderer/" + code, "text/html", "text/html", 500);
            Require(System.Text.Encoding.UTF8.GetByteCount(fallback) <= 512 && fallback.Contains("Request failed"), "Page fallback exceeded the minimum budget.");
        }
        await Check(client, "/renderer/renderer_decline", "text/html", "application/problem+json", 409);
        using (var head = new HttpRequestMessage(HttpMethod.Head, "/browser"))
        {
            head.Headers.Accept.ParseAdd("text/html");
            using var response = await client.SendAsync(head);
            Require((int)response.StatusCode == 403 && response.Content.Headers.ContentLength > 0
                && (await response.Content.ReadAsByteArrayAsync()).Length == 0, "HEAD sent a body or lost representation headers.");
        }
        var beforeCancel = renderer.Seen.Count;
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/cancel"))
        {
            request.Headers.Accept.ParseAdd("text/html");
            using var response = await client.SendAsync(request);
            Require((int)response.StatusCode == 499 && (await response.Content.ReadAsByteArrayAsync()).Length == 0,
                "Cancelled page request became fabricated output.");
        }
        Require(beforeCancel == renderer.Seen.Count, "Cancelled request reached a renderer.");
        Require(!log.Messages.Any(message => message.Contains("PRIVATE")), "Renderer/request data reached diagnostics.");
        Require(typeof(AdmittedProblemPage).GetConstructors().Length == 0, "Unadmitted page input became publicly constructible.");
        await app.StopAsync();
        await VerifyAdmissionBeforeRendering();
        Console.WriteLine("Native browser representation boundary passed: purpose wins, admission precedes rendering, bounded safe fallback, HEAD and cancellation. Product pages/Host/auth integration not claimed.");
    }

    /// <summary>Proves that wire-budget failure replaces the model before invoking any page contribution.</summary>
    private static async Task VerifyAdmissionBeforeRendering()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFoundationProblemDetails();
        services.Configure<FoundationProblemResponseOptions>(options => options.MaxBytes = 512);
        var renderer = new ProbePageRenderer();
        services.AddSingleton<IProblemPageRenderer>(renderer);
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider, TraceIdentifier = "admitted-budget" };
        context.Request.Headers.Accept = "text/html";
        context.Features.Set(new ProblemResponsePurposeMetadata(ProblemResponsePurpose.BrowserPage));
        context.Response.Body = new MemoryStream();
        await FoundationProblemResults.Problem(new ProblemDefinition(400, "too_wide", detail: new string('x', 1024)))
            .ExecuteAsync(context);
        Require(renderer.Seen.Single().Code == ProblemCodes.RequestFailed && renderer.Seen.Single().Status == 500,
            "Renderer saw an envelope that failed native budget admission.");
        Require(context.Response.StatusCode == 500 && ((MemoryStream)context.Response.Body).Length <= 512,
            "Admitted minimum-budget page lost its failure status or bound.");
    }

    /// <summary>Observes actual transport, privacy and security response headers.</summary>
    private static async Task<string> Check(HttpClient client, string path, string accept, string mediaType, int? status = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Accept", accept);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Require(response.Content.Headers.ContentType?.MediaType == mediaType, "Wrong representation: " + path);
        if (status is not null) Require((int)response.StatusCode == status, "Wrong status: " + path);
        Require(response.Headers.CacheControl?.NoStore == true && !body.Contains("PRIVATE"), "Private/cacheable problem output.");
        if (mediaType == "text/html")
            Require(response.Headers.Contains("Content-Security-Policy") && response.Headers.Contains("X-Content-Type-Options"), "Missing page security headers.");
        return body;
    }

    /// <summary>Fails native qualification without altering the observed outcome.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
