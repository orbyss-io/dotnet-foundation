using CShells.AspNetCore.Features;
using CShells.Features;
using Orbyss.Foundation.Json.AspNetCore;
using Orbyss.Foundation.Web.OpenApi;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Declares actual shell endpoints including deliberately invalid transport adaptations.</summary>
[ShellFeature("JsonAdmissionProbe", DependsOn = [typeof(FoundationJsonFeature), typeof(FoundationOpenApiFeature)])]
public sealed class AdmissionProbeFeature : IWebShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        AdmissionContracts.Register(services);
        services.AddSingleton<AdmissionSignals>();
    }
    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        endpoints.MapPost("/echo", async (HttpContext http, IJsonRequestReader<Message> requests, IJsonResponseFactory<Message> responses) =>
            responses.Create(await requests.ReadAsync(http))).WithJsonRequest<Message>().WithJsonResponse<Message>();
        endpoints.MapPost("/cancel", CancelAsync).WithJsonRequest<Message>().WithJsonResponse<Message>();
        endpoints.MapPost("/direct-json", async (HttpContext http, IJsonResponseFactory<Message> responses) =>
            responses.Create((await http.Request.ReadFromJsonAsync<Message>())!)).WithJsonRequest<Message>().WithJsonResponse<Message>();
        endpoints.MapPost("/direct-reader", async (HttpContext http, IJsonResponseFactory<Message> responses) =>
        {
            var result = await http.Request.BodyReader.ReadAsync();
            http.Request.BodyReader.AdvanceTo(result.Buffer.End);
            return responses.Create(new("bypass"));
        }).WithJsonRequest<Message>().WithJsonResponse<Message>();
        endpoints.MapPost("/ignored-input", (IJsonResponseFactory<Message> responses) =>
            responses.Create(new("ignored"))).WithJsonRequest<Message>().WithJsonResponse<Message>();
        endpoints.MapGet("/early-stream", async (HttpContext http, IJsonResponseFactory<Message> responses) =>
        {
            await http.Response.Body.WriteAsync("EARLY_UNADMITTED_BODY"u8.ToArray());
            return responses.Create(new("returned"));
        }).WithJsonResponse<Message>();
        endpoints.MapGet("/early-writer", async (HttpContext http, IJsonResponseFactory<Message> responses) =>
        {
            await http.Response.BodyWriter.WriteAsync("EARLY_UNADMITTED_BODY"u8.ToArray());
            return responses.Create(new("returned"));
        }).WithJsonResponse<Message>();
        endpoints.MapGet("/early-start", async (HttpContext http, IJsonResponseFactory<Message> responses) =>
        {
            await http.Response.StartAsync();
            return responses.Create(new("returned"));
        }).WithJsonResponse<Message>();
        endpoints.MapGet("/caught-early-write", async (HttpContext http, IJsonResponseFactory<Message> responses) =>
        {
            try { await http.Response.Body.WriteAsync("EARLY_UNADMITTED_BODY"u8.ToArray()); }
            catch (JsonResponseContractException) { }
            return responses.Create(new("caught"));
        }).WithJsonResponse<Message>();
        endpoints.MapGet("/large-problem", () => FoundationProblemResults.Problem(
            new ProblemDefinition(409, "domain_conflict", detail: new string('x', 1000)))).WithJsonResponse<Message>();
    }
    /// <summary>Signals actual cancellation while reading an incomplete streamed request.</summary>
    private static async Task<IResult> CancelAsync(HttpContext http, IJsonRequestReader<Message> requests,
        IJsonResponseFactory<Message> responses, AdmissionSignals signals)
    {
        using var aborted = http.RequestAborted.Register(() => signals.Canceled.TrySetResult());
        signals.Reading.TrySetResult();
        try
        {
            var admitted = await requests.ReadAsync(http);
            signals.ReturnedSuccess = true;
            return responses.Create(admitted);
        }
        finally { signals.ReaderFinished.TrySetResult(); }
    }
}
