using Microsoft.AspNetCore.Mvc;
using Orbyss.Foundation.Json.Admission.Probe.Contracts;
using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe.Operations.ReadValue;

/// <summary>Shares a response contract without duplicating the other operation's implementation.</summary>
internal sealed class Endpoint(IJsonResponseFactory<ValueResponse> responses)
{
    /// <summary>Owns the read route.</summary>
    internal const string Route = "/values/{valueId:guid}";
    /// <summary>Owns the exported operation identity.</summary>
    internal const string Name = "ReadValue";

    /// <summary>Idempotently registers the shared response and the request-scoped operation.</summary>
    internal static void Register(IServiceCollection services)
    {
        services.AddJsonContract(ValueResponse.Contract);
        services.AddJsonEndpoint<Endpoint>();
    }

    /// <summary>Maps this operation using only its own handler and shared immutable contract.</summary>
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(Route, Invoke)
            .WithName(Name)
            .WithJsonResponse(ValueResponse.Contract);
    }

    /// <summary>Defers the endpoint instance to actual request scope.</summary>
    internal static IResult Invoke(Guid valueId, [FromServices] Endpoint endpoint) => endpoint.Read(valueId);

    /// <summary>Creates a known response without a storage implementation in this transport fixture.</summary>
    private IResult Read(Guid valueId)
    {
        var scopeId = Guid.NewGuid();
        var value = new ValueResponse(
            valueId,
            "existing",
            null,
            scopeId
        );

        return responses.Create(value);
    }
}
