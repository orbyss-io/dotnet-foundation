using System.Text;
using System.Text.Json;
using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.DependencyInjection;
using CShells.Lifecycle;
using Orbyss.Foundation.Json.Admission.Probe;
using Orbyss.Foundation.Web.ProblemDetails.Core;

var mode = args.FirstOrDefault() ?? "http";
if (mode.StartsWith("activation-", StringComparison.Ordinal))
{
    await RejectActivationAsync(mode["activation-".Length..]);
    return;
}
await using var app = CreateApp("JsonAdmissionProbe");
app.MapShells();
await app.Services.GetRequiredService<IShellRegistry>().GetOrActivateAsync("probe");
await app.StartAsync();
try
{
    using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(15) };
    await VerifyProblemProvenanceAsync(client, collectFailures: mode == "forged");
    if (mode == "forged")
    {
        Console.WriteLine("Forged public problem markers, invalid native problem status and ignored request are rejected before commitment; legitimate native failure remains bounded.");
        return;
    }
    foreach (var path in new[] { "early-stream", "early-writer", "early-start", "caught-early-write" })
    {
        using var response = await client.GetAsync("/probe/" + path);
        await RequireProblemAsync(response, 500, "json_response_profile_bypass");
    }
    foreach (var path in new[] { "direct-json", "direct-reader", "ignored-input" })
    {
        using var response = await client.PostAsync("/probe/" + path,
            new StringContent("{\"text\":\"one\",\"text\":\"two\"}", Encoding.UTF8, "application/json"));
        await RequireProblemAsync(response, 500, "json_request_profile_bypass");
    }
    foreach (var size in new[] { 64, 65 })
    {
        var payload = Encoding.UTF8.GetBytes("{\"text\":\"" + new string('a', size - 11) + "\"}");
        Require(payload.Length == size, "incorrect independently calculated request vector");
        using var content = new ChunkedContent(payload);
        content.Configure();
        using var response = await client.PostAsync("/probe/echo", content);
        if (size == 64)
        {
            var body = await response.Content.ReadAsByteArrayAsync();
            Require((int)response.StatusCode == 200 && body.SequenceEqual(payload),
                "exact-cap chunked request changed: " + response.StatusCode + " " + Encoding.UTF8.GetString(body));
        }
        else await RequireProblemAsync(response, 413, "json_size_exceeded");
    }
    await VerifyCancellationAsync(app, client);
    using (var response = await client.GetAsync("/probe/large-problem"))
    {
        await RequireProblemAsync(response, 500, ProblemCodes.RequestFailed);
        Require((await response.Content.ReadAsByteArrayAsync()).Length <= 512, "configured low problem profile ignored");
    }
    await VerifyOpenApiAsync(client);
    Console.WriteLine("Actual shell early stream/pipe/start rejection, direct/native request bypass, chunked exact-cap/cap-plus-one, canceled read, low problem budget and OpenAPI metadata passed.");
}
finally { await app.StopAsync(); }

static WebApplication CreateApp(string feature, Action<Dictionary<string, string?>>? amend = null, ActivationDiagnostics? diagnostics = null)
{
    const string root = "CShells:Shells:probe";
    var values = new Dictionary<string, string?>
    {
        [$"{root}:Features:{feature}"] = "true",
        [$"{root}:Configuration:WebRouting:Path"] = "probe",
        [$"{root}:Configuration:Foundation:Json:Profiles:strict-request:MaxBytes"] = "64",
        [$"{root}:Configuration:Foundation:Json:Profiles:success-response:MaxBytes"] = "256",
        [$"{root}:Configuration:Foundation:Json:Profiles:problem-response:MaxBytes"] = "512"
    };
    amend?.Invoke(values);
    var builder = WebApplication.CreateBuilder();
    builder.Logging.ClearProviders();
    if (diagnostics is not null) builder.Logging.AddProvider(diagnostics);
    builder.Configuration.AddInMemoryCollection(values);
    builder.Services.AddCShellsAspNetCore(shells => shells.WithConfigurationProvider(builder.Configuration)
        .WithWebRouting(options => options.EnablePathRouting = true));
    var app = builder.Build();
    app.Urls.Add("http://127.0.0.1:0");
    return app;
}

static async Task RejectActivationAsync(string kind)
{
    const string root = "CShells:Shells:probe:Configuration:Foundation:Json:Profiles:strict-request";
    var feature = kind == "native" ? "JsonNativeBindingProbe" : kind == "resolver" ? "JsonMissingMetadataProbe" : "JsonAdmissionProbe";
    using var diagnostics = new ActivationDiagnostics();
    await using var app = CreateApp(feature, values =>
    {
        if (kind == "capacity") values[root + ":MaxBytes"] = "0";
        if (kind == "preset") values[root + ":Preset"] = "tolerant-response";
        if (kind == "resolver") values[root + ":Extensions:0"] = "admission-missing-metadata";
    }, diagnostics);
    string? propagated = null;
    int? rejectedStatus = null;
    try
    {
        app.MapShells();
        await app.Services.GetRequiredService<IShellRegistry>().GetOrActivateAsync("probe");
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(10) };
        using var response = await client.PostAsync("/probe/native", new StringContent("{\"text\":\"accepted\"}", Encoding.UTF8, "application/json"));
        rejectedStatus = (int)response.StatusCode;
    }
    catch (Exception error)
    {
        propagated = error.ToString();
    }
    finally { await app.StopAsync(); }
    var description = diagnostics.Description + propagated;
    var expected = kind switch
    {
        "native" => "registered typed reader", "resolver" => "JsonTypeInfo",
        "preset" => "contract requirements", "capacity" => "invalid limits", _ => throw new ArgumentException("Unknown activation case")
    };
    Require(propagated is not null || rejectedStatus is >= 400, "invalid activation accepted a request");
    Require(description.Contains(expected, StringComparison.Ordinal), "missing actual activation diagnostic for " + kind + ": " + description);
    Console.WriteLine("Actual shell rejected " + kind + " with its specific diagnostic before accepting the contract; HTTP " + rejectedStatus);
}

static async Task VerifyCancellationAsync(WebApplication app, HttpClient client)
{
    var shell = app.Services.GetRequiredService<IShellRegistry>().GetActive("probe")!;
    var signals = shell.ServiceProvider.GetRequiredService<AdmissionSignals>();
    using var content = new ChunkedContent("{\"text\":\""u8.ToArray(), pauseAfterPrefix: true);
    content.Configure();
    using var cancellation = new CancellationTokenSource();
    var pending = client.PostAsync("/probe/cancel", content, cancellation.Token);
    await signals.Reading.Task.WaitAsync(TimeSpan.FromSeconds(10));
    cancellation.Cancel();
    try { using var response = await pending; throw new InvalidOperationException("Canceled input committed response " + response.StatusCode); }
    catch (OperationCanceledException) { }
    await signals.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(10));
    await signals.ReaderFinished.Task.WaitAsync(TimeSpan.FromSeconds(10));
    Require(!signals.ReturnedSuccess, "canceled partial input was admitted as a successful request");
}

static async Task VerifyOpenApiAsync(HttpClient client)
{
    using var response = await client.GetAsync("/probe/_orbyss-foundation/openapi/v1.json");
    Require((int)response.StatusCode == 200, "actual OpenAPI document unavailable");
    using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
    var echo = document.RootElement.GetProperty("paths").EnumerateObject().Single(path => path.Name.EndsWith("/echo", StringComparison.Ordinal)).Value.GetProperty("post");
    var contracts = echo.GetProperty("x-foundation-json-contracts");
    Require(contracts.GetArrayLength() == 2, "actual OpenAPI lost request/response contract metadata");
    foreach (var contract in contracts.EnumerateArray())
    {
        var request = contract.GetProperty("direction").GetString() == "request";
        Require(contract.GetProperty("profile").GetString() == (request ? "strict-request" : "success-response"), "exported wrong profile");
        Require(contract.GetProperty("preset").GetString() == (request ? "strict-request" : "tolerant-response"), "exported wrong strictness");
        Require(contract.GetProperty("maximumBytes").GetInt32() == 1024, "contract export duplicated deployment numbers");
    }
}

static async Task VerifyProblemProvenanceAsync(HttpClient client, bool collectFailures)
{
    var failures = new List<string>();
    async Task CheckAsync(string name, HttpResponseMessage response, int status, string code)
    {
        if (collectFailures) Console.WriteLine($"Provenance case {name}: HTTP {(int)response.StatusCode}, bytes {(await response.Content.ReadAsByteArrayAsync()).Length}.");
        try { await RequireProblemAsync(response, status, code); }
        catch (InvalidOperationException) when (collectFailures) { failures.Add(name); }
    }
    foreach (var path in new[] { "forged-success", "forged-problem", "native-success-problem" })
    {
        using var response = await client.GetAsync("/probe/" + path);
        await CheckAsync(path, response, 500, "json_response_profile_bypass");
    }
    using (var response = await client.PostAsync("/probe/ignored-forged-problem", new StringContent("{\"text\":\"one\",\"text\":\"two\"}", Encoding.UTF8, "application/json")))
        await CheckAsync("ignored-forged-problem", response, 500, "json_request_profile_bypass");
    using (var response = await client.GetAsync("/probe/native-problem"))
        await CheckAsync("legitimate-native-problem", response, 409, ProblemCodes.RequestFailed);
    Require(failures.Count == 0, "Provenance acceptance failed: " + string.Join(", ", failures));
}

static async Task RequireProblemAsync(HttpResponseMessage response, int status, string code)
{
    var bytes = await response.Content.ReadAsByteArrayAsync();
    Require((int)response.StatusCode == status, "expected " + status + ", got " + response.StatusCode + ": " + Encoding.UTF8.GetString(bytes));
    Require(response.Content.Headers.ContentType?.MediaType == "application/problem+json", "failure envelope content type differs");
    using var document = JsonDocument.Parse(bytes);
    Require(document.RootElement.GetProperty("code").GetString() == code, "wrong safe error code: " + Encoding.UTF8.GetString(bytes));
    Require(document.RootElement.GetProperty("correlationId").GetString() == document.RootElement.GetProperty("traceId").GetString(), "failure correlation representation differs");
    Require(!Encoding.UTF8.GetString(bytes).Contains("EARLY_UNADMITTED_BODY", StringComparison.Ordinal), "response committed unadmitted bytes before failure");
    Require(!Encoding.UTF8.GetString(bytes).Contains("FORGED_UNBOUNDED_BODY", StringComparison.Ordinal), "forged marker committed unadmitted bytes");
}

static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
