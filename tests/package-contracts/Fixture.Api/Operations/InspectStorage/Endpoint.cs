using Foundation.ContractFixture.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orbyss.Foundation.Json.AspNetCore;
namespace Foundation.ContractFixture.Api;
internal sealed class StorageEndpoint(IFixtureStorageProbe storage, IJsonResponseFactory<FixtureStorageObservation> responses,
    IJsonResponseFactory<FixtureCancellationObservation> cancellations)
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/storage", (StorageEndpoint endpoint, HttpContext context) => endpoint.ExecuteAsync(context)).AllowAnonymous()
            .WithJsonResponse<FixtureStorageObservation>();
        endpoints.MapGet("/storage-stage-cancel", (StorageEndpoint endpoint, HttpContext context) => endpoint.CancellationAsync(context)).AllowAnonymous()
            .WithJsonResponse<FixtureCancellationObservation>();
    }
    public async Task<IResult> ExecuteAsync(HttpContext context) => responses.Create(await storage.ObserveAsync(context.RequestAborted));
    public async Task<IResult> CancellationAsync(HttpContext context) => cancellations.Create(await storage.ObserveCancellationAsync(context.RequestAborted));
}
