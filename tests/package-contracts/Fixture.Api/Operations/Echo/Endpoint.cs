using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orbyss.Foundation.Json.AspNetCore;
namespace Foundation.ContractFixture.Api;
internal sealed class EchoEndpoint(IJsonRequestReader<WireMessage> reader, IJsonResponseFactory<WireMessage> responses)
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/echo", (EchoEndpoint endpoint, HttpContext context) => endpoint.ExecuteAsync(context))
            .AllowAnonymous().WithJsonRequest<WireMessage>().WithJsonResponse<WireMessage>();
        endpoints.MapGet("/large", (EchoEndpoint endpoint) => endpoint.Large()).AllowAnonymous().WithJsonResponse<WireMessage>();
        endpoints.MapGet("/bypass", () => Results.Ok(new WireMessage("not-admitted"))).AllowAnonymous().WithJsonResponse<WireMessage>();
        endpoints.MapGet("/invalid-output", (EchoEndpoint endpoint) => endpoint.Invalid()).AllowAnonymous().WithJsonResponse<WireMessage>();
    }
    public async Task<IResult> ExecuteAsync(HttpContext context) => responses.Create(await reader.ReadAsync(context));
    public IResult Large() => responses.Create(new(new string('x', 800)));
    public IResult Invalid() => responses.Create(new(null!));
}
