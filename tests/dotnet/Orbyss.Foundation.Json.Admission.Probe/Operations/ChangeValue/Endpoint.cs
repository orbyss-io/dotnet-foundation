using Microsoft.AspNetCore.Mvc;
using Orbyss.Foundation.Json.Admission.Probe.Contracts;
using Orbyss.Foundation.Json.AspNetCore;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;

namespace Orbyss.Foundation.Json.Admission.Probe.Operations.ChangeValue;

/// <summary>Owns route binding, admitted JSON and the actual conditional-change response.</summary>
internal sealed class Endpoint(IJsonRequestReader<Request> requests, IJsonResponseFactory<ValueResponse> responses)
{
    /// <summary>Preserves the operation's route identity.</summary>
    internal const string Route = "/values/{valueId:guid}";
    /// <summary>Owns the stable exported operation name.</summary>
    internal const string Name = "ChangeValue";
    /// <summary>Explains a missing conditional header.</summary>
    private static readonly ProblemDefinition missingRevision = new(428, "revision_required");
    /// <summary>Explains malformed or duplicate conditional headers.</summary>
    private static readonly ProblemDefinition invalidRevision = new(400, "revision_invalid");
    /// <summary>Explains stale conditional authority.</summary>
    private static readonly ProblemDefinition staleRevision = new(412, "revision_stale");

    /// <summary>Registers the operation and its real shared response without registering effects.</summary>
    internal static void Register(IServiceCollection services)
    {
        services.AddJsonContract(Request.Contract);
        services.AddJsonContract(ValueResponse.Contract);

        services.AddJsonEndpoint<Endpoint>();
    }

    /// <summary>Publishes the same definitions used by the native readers and result factories.</summary>
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        var failures = new[] { missingRevision, invalidRevision, staleRevision };
        endpoints
            .MapPatch(Route, Invoke)
            .WithName(Name)
            .WithJsonRequest(Request.Contract)
            .WithJsonResponse(ValueResponse.Contract)
            .ProducesFoundationProblems(failures);
    }

    /// <summary>Retains binding attributes on the actual mapped method and resolves the operation per request.</summary>
    internal static Task<IResult> Invoke(
        HttpContext context,
        Guid valueId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] Endpoint endpoint
    ) => endpoint.ChangeAsync(
        context,
        valueId,
        ifMatch
    );

    /// <summary>Keeps header cardinality and value checks before reading the bounded request.</summary>
    private async Task<IResult> ChangeAsync(
        HttpContext context,
        Guid valueId,
        string? ifMatch
    )
    {
        var values = context.Request.Headers.IfMatch;
        if (values.Count == 0)
            return FoundationProblemResults.Problem(missingRevision);
        if (values.Count != 1 || ifMatch is null || ifMatch.Length != 3 || ifMatch[0] != '"' || ifMatch[2] != '"')
            return FoundationProblemResults.Problem(invalidRevision);
        if (ifMatch != "\"1\"")
            return FoundationProblemResults.Problem(staleRevision);

        var input = await requests.ReadAsync(context, context.RequestAborted);
        var scopeId = Guid.NewGuid();
        var value = new ValueResponse(
            valueId,
            input.Label,
            input.Sequence,
            scopeId
        );

        return responses.Create(value);
    }
}
