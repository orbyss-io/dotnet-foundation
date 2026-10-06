using System.Text;
using System.Text.Json;
using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.DependencyInjection;
using CShells.Lifecycle;
using Orbyss.Foundation.Json;
using Orbyss.Foundation.Json.AspNetCore;
using Orbyss.Foundation.Json.Http.Probe;

var configuration = new Dictionary<string, string?>();
foreach (var shell in new[] { "a", "b" })
{
    var root = $"CShells:Shells:{shell}";
    configuration[$"{root}:Features:JsonHttpProbe"] = "true";
    configuration[$"{root}:Configuration:WebRouting:Path"] = shell;
    configuration[$"{root}:Configuration:Foundation:Json:Profiles:strict-request:MaxBytes"] = shell == "a" ? "64" : "512";
    configuration[$"{root}:Configuration:Foundation:Json:Profiles:success-response:MaxBytes"] = shell == "a" ? "64" : "256";
}
var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.Configuration.AddInMemoryCollection(configuration);
builder.Services.AddCShellsAspNetCore(shells => shells.WithConfigurationProvider(builder.Configuration).WithWebRouting(options => options.EnablePathRouting = true));
await using var app = builder.Build();
app.Urls.Add("http://127.0.0.1:0");
app.MapShells();
foreach (var shell in new[] { "a", "b" }) await app.Services.GetRequiredService<IShellRegistry>().GetOrActivateAsync(shell);
await app.StartAsync();
try
{
    using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    foreach (var shell in new[] { "a", "b" })
    {
        using var echo = await client.PostAsync($"/{shell}/echo", new StringContent("{\"message\":\"hello\"}", Encoding.UTF8, "application/json"));
        Require((int)echo.StatusCode == 200 && await echo.Content.ReadAsStringAsync() == "{\"message\":\"hello\"}", "typed echo failed");
        using var large = await client.GetAsync($"/{shell}/large");
        Require((int)large.StatusCode == (shell == "a" ? 500 : 200), $"response shell settings leaked: {shell}/{large.StatusCode} {await large.Content.ReadAsStringAsync()}");
        if (shell == "a") await Problem(large, "json_response_size_exceeded");
        using var bypass = await client.GetAsync($"/{shell}/bypass");
        Require((int)bypass.StatusCode == 500, "success bypass accepted");
        await Problem(bypass, "json_response_profile_bypass");
        using var invalid = await client.GetAsync($"/{shell}/invalid-output");
        Require((int)invalid.StatusCode == 500, "invalid server output mislabeled");
        await Problem(invalid, "json_response_invalid_contract");
        using var unicode = await client.GetAsync($"/{shell}/unicode");
        Require((int)unicode.StatusCode == 200 && (await unicode.Content.ReadAsByteArrayAsync()).Length <= (shell == "a" ? 64 : 256), "Unicode output budget failed");
        using var malformed = await client.PostAsync($"/{shell}/echo", new StringContent("{\"message\":\"first\",\"message\":\"second\"}", Encoding.UTF8, "application/json"));
        Require((int)malformed.StatusCode == 400, "malformed request classification failed");
        await Problem(malformed, "json_duplicate_member");
        using var request = await client.PostAsync($"/{shell}/echo", new StringContent("{\"message\":\"" + new string('a', 100) + "\"}", Encoding.UTF8, "application/json"));
        Require((int)request.StatusCode == (shell == "a" ? 413 : 200), "actual request admission failed");
    }
}
finally { await app.StopAsync(); }
var settings = new CShells.ShellSettings("metadata");
var metadataBuilder = Host.CreateApplicationBuilder();
var services = metadataBuilder.Services;
new FoundationJsonFeature(settings).ConfigureServices(services);
services.AddJsonResponseContract<WireMessage>(new(JsonProfileKeys.SuccessResponse), new(JsonProfileKeys.TolerantResponse));
services.AddSingleton<PoisonStorage>(_ => throw new InvalidOperationException("storage must not start for metadata"));
using var metadataHost = metadataBuilder.Build();
var metadataProvider = metadataHost.Services;
{
    _ = metadataProvider.GetRequiredService<JsonProfileCatalog>();
    Require(metadataProvider.GetServices<IJsonContractMetadata>().Single().ContractType == typeof(WireMessage), "metadata missing");
}
var canceled = new DefaultHttpContext();
canceled.Request.Body = new MemoryStream("{\"message\":\"hello\"}"u8.ToArray());
canceled.RequestAborted = new CancellationToken(true);
{
    var response = metadataProvider.GetRequiredService<IJsonResponseFactory<WireMessage>>().Create(new("hello"));
    try { await response.ExecuteAsync(canceled); throw new InvalidOperationException("canceled response admitted"); }
    catch (OperationCanceledException) { Require(canceled.Response.ContentType is null, "cancellation committed headers"); }
}
var paging = new JsonPageOptions();
Require(paging.Admit(null) == 100 && paging.Admit(500) == 500, "page configuration failed");
try { paging.Admit(501); throw new InvalidOperationException("excessive page count admitted"); } catch (JsonProfileException) { }
Console.WriteLine("Actual two-shell typed request/response budgets, profile bypass, common problems, metadata-only resolution and cancellation passed.");
static async Task Problem(HttpResponseMessage response, string code)
{
    using var body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
    Require(body.RootElement.GetProperty("code").GetString() == code, "wrong failure direction/code");
    Require(body.RootElement.GetProperty("correlationId").GetString() == body.RootElement.GetProperty("traceId").GetString(), "problem representation differs");
}
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
