using System.Text.Json;
using CShells.Lifecycle.Blueprints;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Host.Diagnostics;
using Orbyss.Foundation.Web.ProblemDetails.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Orbyss.Foundation.Web.ProblemDetails;

Reject(() => new ProblemDefinition(200, "bad"));
Reject(() => new ProblemDefinition(400, "Bad Code"));
Reject(() => new ProblemDefinition(400, "bad", title: "\uD800"));
Reject(() => new ProblemFieldError("field", "bad", "line\nprivate"));
Reject(() => new ProblemDefinition(400, "bad", fieldErrors: Enumerable.Range(0, 17)
    .Select(index => new ProblemFieldError(index.ToString(), "bad", "Invalid."))));
Reject(() => new ProblemDefinition(400, "bad", fieldErrors: [null!]));
var source = new List<ProblemFieldError> { new("name", "required", "Name is required.") };
var owned = new ProblemDefinition(400, "invalid_request", fieldErrors: source);
source.Clear();
Require(owned.FieldErrors.Count == 1, "Definition did not own its diagnostic snapshot.");
var logger = new RecordingLoggerProvider();
var ledger = new EnrichmentLedger();
var dispatch = new DispatchLedger();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Host.UseDefaultServiceProvider(options => { options.ValidateScopes = true; options.ValidateOnBuild = true; });
builder.Logging.ClearProviders();
builder.Logging.AddProvider(logger);
builder.Services.AddFoundationRequestDiagnosticRedaction();
builder.Services.AddSingleton(ledger);
builder.Services.AddSingleton(dispatch);
builder.Services.AddScoped<IProblemDetailsEnricher, ProbeEnricher>();
builder.Services.AddExceptionHandler<DecliningHandler>();
builder.Services.AddExceptionHandler<ConflictHandler>();
var feature = new FoundationProblemDetailsFeature();
feature.ConfigureServices(builder.Services);
builder.Services.AddFoundationProblemDetails();
await using var app = builder.Build();
feature.UseMiddleware(app, app.Environment);
app.MapGet("/status/{status:int}", (int status) => Results.StatusCode(status));
app.MapGet("/argument", (HttpContext context) => throw new ArgumentException("password=PRIVATE SQL secret"));
app.MapGet("/bad", (HttpContext context) => throw new BadHttpRequestException("PRIVATE submitted body", 413));
app.MapGet("/conflict", (HttpContext context) => throw new ProbeConflictException());
app.MapGet("/application", () => FoundationProblemResults.Problem("name", new FieldFailureMapper()));
app.MapGet("/malicious", () => Results.Problem(statusCode: 409,
    extensions: new Dictionary<string, object?> { ["code"] = new { password = "PRIVATE" }, ["sql"] = "PRIVATE" }));
app.MapGet("/reserved", () => FoundationProblemResults.Problem(new ProblemDefinition(409, "change_reserved")));
app.MapGet("/enrichment-cancel", () => FoundationProblemResults.Problem(new ProblemDefinition(409, "unexpected_cancellation")));
app.MapGet("/max", () => FoundationProblemResults.Problem(new ProblemDefinition(statusCode: 400, code: "maximum",
    title: new string('漢', 256), detail: new string('漢', 1024), fieldErrors: Enumerable.Range(0, 16).Select(index =>
        new ProblemFieldError(new string('漢', 126) + index.ToString("D2"), new string('a', 96), new string('漢', 256))))));
app.MapGet("/cancel", (HttpContext context) =>
{
    context.RequestAborted = new CancellationToken(canceled: true);
    throw new OperationCanceledException("PRIVATE cancelled input");
});
app.MapGet("/started", async (HttpContext context) =>
{
    context.Response.ContentType = "text/plain";
    await context.Response.WriteAsync("committed");
    await context.Response.Body.FlushAsync();
    throw new InvalidOperationException("password=PRIVATE; SELECT PRIVATE SQL; private submitted body");
});
await app.StartAsync();
using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
foreach (var status in new[] { 400, 401, 403, 404, 405, 409, 413, 503, 500 })
    await Verify(client, $"/status/{status}", status, FoundationProblemResults.CodeForStatus(status));
await Verify(client, "/argument", 500, ProblemCodes.RequestFailed);
await Verify(client, "/bad", 413, ProblemCodes.InvalidRequest);
await Verify(client, "/conflict", 409, "revision_conflict");
await Verify(client, "/application", 400, "invalid_name");
await Verify(client, "/malicious", 500, ProblemCodes.RequestFailed);
await Verify(client, "/reserved", 500, ProblemCodes.RequestFailed);
await Verify(client, "/enrichment-cancel", 500, ProblemCodes.RequestFailed, requireLocalization: false);
await Verify(client, "/argument", 500, ProblemCodes.RequestFailed, "text/html");
await Verify(client, "/application", 400, "invalid_name", "text/html");
using (var largest = await client.GetAsync("/max"))
{
    var bytes = await largest.Content.ReadAsByteArrayAsync();
    Require(bytes.Length < 64 * 1024, $"Largest admitted envelope exceeded64KiB: {bytes.Length}");
    using var json = JsonDocument.Parse(bytes);
    Require(json.RootElement.GetProperty("fieldErrors").GetArrayLength() == 16, "Largest fields were truncated.");
    Console.WriteLine($"Largest admitted problem envelope: {bytes.Length} bytes.");
}
using (var cancelled = await client.GetAsync("/cancel"))
    Require((int)cancelled.StatusCode == 499 && (await cancelled.Content.ReadAsStringAsync()).Length == 0,
        "Native caller cancellation became a fabricated problem response.");
Require(dispatch.ConflictHandled == 1 && dispatch.Declined >= 3, "Native ordered handler dispatch was bypassed.");
using (var startedResponse = await client.GetAsync("/started", HttpCompletionOption.ResponseHeadersRead))
{
    Require((int)startedResponse.StatusCode == 200, "Committed response status was dishonestly replaced.");
    try { _ = await startedResponse.Content.ReadAsStringAsync(); throw new Exception("Broken committed response falsely completed."); }
    catch (HttpRequestException) { }
    catch (IOException) { }
}
Require(logger.Messages.Any(message => message.Contains("Framework request failure System.InvalidOperationException")),
    "Native output-started diagnostic was lost rather than redacted.");
Require(logger.Messages.Any(message => message.Contains("Foundation failure ArgumentException")),
    ".NET10 handled suppression lost the redacted failure diagnostic.");
Require(!logger.Messages.Any(message => message.Contains("PRIVATE")), "Private exception/body/SQL reached diagnostics.");
Require(ledger.Calls.Values.All(count => count == 1), "Representation was enriched more than once per problem.");
Require(ledger.ScopeIdentities.Distinct().Count() == ledger.Calls.Count,
    "A singleton handler captured a request-scoped contribution.");
using (var scope = app.Services.CreateScope())
{
    var handler = scope.ServiceProvider.GetServices<IExceptionHandler>()
        .Single(item => item.GetType().Name == "FoundationBadHttpRequestExceptionHandler");
    var started = new DefaultHttpContext();
    started.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
    started.RequestServices = scope.ServiceProvider;
    Require(!await handler.TryHandleAsync(started, new BadHttpRequestException("committed", 400), default),
        "An output-started error falsely claimed a replacement response.");
    var committedFailure = new ArgumentException("committed output failure");
    var nativePipeline = new ApplicationBuilder(app.Services);
    feature.UseMiddleware(nativePipeline, app.Environment);
    nativePipeline.Run(_ => Task.FromException(committedFailure));
    try { await nativePipeline.Build()(started); throw new Exception("Native committed failure was swallowed."); }
    catch (ArgumentException error) { Require(ReferenceEquals(error, committedFailure), "Native failure changed after commitment."); }
    Require(started.Response.StatusCode == 200, "Native committed output was falsely replaced by safe500.");
    var cancelled = new DefaultHttpContext { RequestServices = scope.ServiceProvider,
        RequestAborted = new CancellationToken(canceled: true) };
    Require(!await handler.TryHandleAsync(cancelled, new BadHttpRequestException("cancelled", 400), default),
        "An aborted request falsely claimed handled output.");
}
await app.StopAsync();
var settings = await new ConfigurationShellBlueprint("auth-only", new ConfigurationBuilder().Build()).ComposeAsync();
var authBuilder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
authBuilder.WebHost.UseUrls("http://127.0.0.1:0");
authBuilder.Logging.ClearProviders();
new FoundationAuthenticationFeature(settings).ConfigureServices(authBuilder.Services);
await using var authApp = authBuilder.Build();
authApp.MapGet("/auth/{status:int}", async (HttpContext context, int status, IAuthenticationErrorWriter writer) =>
    await writer.WriteAsync(context, status, FoundationProblemResults.CodeForStatus(status))).AllowAnonymous();
await authApp.StartAsync();
using var authClient = new HttpClient { BaseAddress = new Uri(authApp.Urls.Single()) };
await Verify(authClient, "/auth/401", 401, ProblemCodes.AuthenticationRequired, requireLocalization: false);
await Verify(authClient, "/auth/403", 403, ProblemCodes.AuthorizationDenied, requireLocalization: false);
Require(!authApp.Services.GetServices<IExceptionHandler>().Any(), "Basic auth unexpectedly activated global handlers.");
await authApp.StopAsync();
// Byte admission is exact and a rejected public envelope gets one finite safe500,
// without recursively passing the failed payload through the representation pipeline.
var budgetDefinition = new ProblemDefinition(400, "invalid_name", detail: new string('x', 600));
var admitted = await WriteWithBudget(budgetDefinition, 65_536);
var exact = await WriteWithBudget(budgetDefinition, admitted.Bytes.Length);
Require(exact.Status == 400 && exact.Bytes.AsSpan().SequenceEqual(admitted.Bytes), "Exact problem cap failed.");
var overflow = await WriteWithBudget(budgetDefinition, admitted.Bytes.Length - 1);
Require(overflow.Status == 500 && overflow.Bytes.Length <= 512, "Cap plus one failed to use finite fallback.");
var tiny = await WriteWithBudget(budgetDefinition, 512);
Require(tiny.Status == 500 && tiny.Bytes.Length <= 512, "Minimum configured budget recursed on overflow.");
var unrelatedCancellation = await WriteWithBudget(new ProblemDefinition(409, "unexpected_cancellation"), 65_536,
    injectUnexpectedEnricher: true);
Require(unrelatedCancellation.Status == 500
    && !System.Text.Encoding.UTF8.GetString(unrelatedCancellation.Bytes).Contains("PRIVATE"),
    "Unrelated contribution cancellation escaped the safe representation without global middleware.");
var customizationCalls = 0;
var composedCustomization = await WriteWithBudget(budgetDefinition, 65_536, previousCustomizer: context =>
{
    customizationCalls++;
    context.ProblemDetails.Title = "Prior safe title";
    context.ProblemDetails.Extensions["sql"] = "PRIVATE previous extension";
});
Require(customizationCalls == 1 && composedCustomization.Status == 400
    && System.Text.Encoding.UTF8.GetString(composedCustomization.Bytes).Contains("Prior safe title")
    && !System.Text.Encoding.UTF8.GetString(composedCustomization.Bytes).Contains("PRIVATE"),
    "Existing ASP.NET customization was bypassed, repeated or escaped the admitted shape.");
var unrelatedPrevious = await WriteWithBudget(budgetDefinition, 65_536,
    previousCustomizer: _ => throw new OperationCanceledException("PRIVATE unrelated customization"));
Require(unrelatedPrevious.Status == 500, "Unrelated previous-customization cancellation escaped without global middleware.");
using (var invalidProvider = ProblemProvider(511))
{
    try { _ = invalidProvider.GetRequiredService<IProblemDetailsService>(); throw new Exception("Invalid writer budget accepted."); }
    catch (Microsoft.Extensions.Options.OptionsValidationException) { }
}
foreach (var invalid in new[] { (Depth: 2, Preset: "tolerant-response"), (Depth: 65, Preset: "tolerant-response"),
    (Depth: 32, Preset: "strict-request") })
{
    var invalidServices = new ServiceCollection();
    invalidServices.AddLogging();
    invalidServices.AddFoundationProblemDetails();
    invalidServices.Configure<FoundationProblemResponseOptions>(options =>
    {
        options.MaxDepth = invalid.Depth;
        options.Preset = invalid.Preset;
    });
    using var invalidProvider = invalidServices.BuildServiceProvider();
    try { _ = invalidProvider.GetRequiredService<IProblemDetailsService>(); throw new Exception("Unsupported problem profile accepted."); }
    catch (Microsoft.Extensions.Options.OptionsValidationException) { }
}
Console.WriteLine($"Problem exact-cap {admitted.Bytes.Length} bytes; overflow fallback {overflow.Bytes.Length} bytes.");
await using (var statusProvider = ProblemProvider(512))
{
    Require(!statusProvider.GetServices<IExceptionHandler>().Any(), "Status fallback activated global handlers.");
    await using var scope = statusProvider.CreateAsyncScope();
    var statusPipeline = new ApplicationBuilder(statusProvider);
    statusPipeline.UseFoundationProblemStatusCodePages();
    statusPipeline.Run(context =>
    {
        if (!context.Response.HasStarted) context.Response.StatusCode = 405;
        return Task.CompletedTask;
    });
    var delegatePipeline = statusPipeline.Build();
    var empty = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
    empty.Response.Body = new MemoryStream();
    await delegatePipeline(empty);
    Require(empty.Response.StatusCode == 405 && empty.Response.ContentLength is > 0 and <= 512
        && empty.Response.Headers.CacheControl == "no-store", "Native empty-status fallback was not bounded/private.");
    var aborted = new DefaultHttpContext { RequestServices = scope.ServiceProvider,
        RequestAborted = new CancellationToken(canceled: true) };
    aborted.Response.Body = new MemoryStream();
    await delegatePipeline(aborted);
    Require(aborted.Response.StatusCode == 405 && aborted.Response.ContentLength is null
        && ((MemoryStream)aborted.Response.Body).Length == 0, "Aborted status was replaced by fabricated output.");
    var started = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
    started.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
    await delegatePipeline(started);
    Require(started.Response.StatusCode == 200 && started.Response.ContentLength is null,
        "Already-started status was dishonestly replaced.");
    var defectPipeline = new ApplicationBuilder(statusProvider);
    defectPipeline.UseFoundationProblemStatusCodePages();
    var unhandled = new ArgumentException("PRIVATE unhandled without optional feature");
    defectPipeline.Run(_ => Task.FromException(unhandled));
    try { await defectPipeline.Build()(empty); throw new Exception("Status fallback activated exception handling."); }
    catch (ArgumentException error) { Require(ReferenceEquals(error, unhandled), "Unowned exception identity changed."); }
}
Console.WriteLine("Foundation Problem Details conformance passed: definitions, all envelopes, scoped enrichment, native ordering, privacy, cancellation and auth-only activation.");
static void Reject(Action action)
{
    try { action(); } catch (ArgumentException) { return; }
    throw new Exception("Invalid problem contract was accepted.");
}
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static ServiceProvider ProblemProvider(int maximum, bool injectUnexpectedEnricher = false,
    Action<ProblemDetailsContext>? previousCustomizer = null)
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddFoundationProblemDetails();
    services.Configure<FoundationProblemResponseOptions>(options => options.MaxBytes = maximum);
    if (previousCustomizer is not null)
        services.Configure<ProblemDetailsOptions>(options => options.CustomizeProblemDetails = previousCustomizer);
    if (injectUnexpectedEnricher)
    {
        services.AddSingleton(new EnrichmentLedger());
        services.AddScoped<IProblemDetailsEnricher, ProbeEnricher>();
    }
    return services.BuildServiceProvider(validateScopes: true);
}
static async Task<(int Status, byte[] Bytes)> WriteWithBudget(ProblemDefinition definition, int maximum,
    bool injectUnexpectedEnricher = false, Action<ProblemDetailsContext>? previousCustomizer = null)
{
    await using var provider = ProblemProvider(maximum, injectUnexpectedEnricher, previousCustomizer);
    await using var scope = provider.CreateAsyncScope();
    var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider, TraceIdentifier = "budget" };
    context.Response.Body = new MemoryStream();
    await FoundationProblemResults.Problem(definition).ExecuteAsync(context);
    return (context.Response.StatusCode, ((MemoryStream)context.Response.Body).ToArray());
}
static async Task Verify(HttpClient client, string path, int status, string code, string? accept = null,
    bool requireLocalization = true)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, path);
    if (accept is not null) request.Headers.Accept.ParseAdd(accept);
    using var response = await client.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();
    using var json = JsonDocument.Parse(body);
    var root = json.RootElement;
    Require((int)response.StatusCode == status && root.GetProperty("status").GetInt32() == status,
        $"Incorrect status for {path}: {body}");
    Require(root.GetProperty("code").GetString() == code, $"Incorrect failure identity for {path}: {body}");
    Require(root.GetProperty("correlationId").GetString() == root.GetProperty("traceId").GetString()
        && !string.IsNullOrEmpty(root.GetProperty("correlationId").GetString()), "Correlation contract drifted.");
    Require(root.GetProperty("fieldErrors").ValueKind == JsonValueKind.Array, "No shared field envelope.");
    Require(!body.Contains("PRIVATE") && !root.TryGetProperty("sql", out _), "Private diagnostics reached wire.");
    if (requireLocalization && path != "/reserved" && path != "/malicious")
        Require(root.GetProperty("title").GetString() == "Veilige fout", $"Scoped localization bypassed for {path}.");
}
