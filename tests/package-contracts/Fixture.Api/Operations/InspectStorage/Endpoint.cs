using Foundation.ContractFixture.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orbyss.Foundation.Json.AspNetCore;
namespace Foundation.ContractFixture.Api;
internal sealed class StorageEndpoint(IFixtureStorageProbe storage, IJsonResponseFactory<FixtureStorageObservation> responses)
{
    public static void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/storage",
        (StorageEndpoint endpoint, HttpContext context) => endpoint.ExecuteAsync(context)).AllowAnonymous()
        .WithJsonResponse<FixtureStorageObservation>();
    public async Task<IResult> ExecuteAsync(HttpContext context) => responses.Create(await storage.ObserveAsync(context.RequestAborted));
}
