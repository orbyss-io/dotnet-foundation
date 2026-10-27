using Microsoft.AspNetCore.Http.Metadata;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Checks native problem response metadata without inventing runtime exception handling.</summary>
internal static class ProblemMetadataChecks
{
    /// <summary>Requires equivalent definitions/statuses, deduplication and atomic invalid-status rejection.</summary>
    internal static async Task RunAsync()
    {
        var builder = WebApplication.CreateBuilder();
        await using var app = builder.Build();
        var declared = app.MapGet("/declared", Invoke);
        var statuses = new[] { 400, 400, 503 };
        declared.ProducesFoundationProblemStatuses(statuses);

        var invalid = new[] { 401, 399 };
        try
        {
            declared.ProducesFoundationProblemStatuses(invalid);
            throw new InvalidOperationException("An invalid HTTP problem status was accepted.");
        }
        catch (ArgumentOutOfRangeException)
        {
        }

        var owned = app.MapGet("/owned", Invoke);
        var first = new ProblemDefinition(400, "first_failure");
        var second = new ProblemDefinition(400, "second_failure");
        var unavailable = new ProblemDefinition(503, "unavailable");
        var definitions = new[] { first, second, unavailable };
        owned.ProducesFoundationProblems(definitions);

        var routes = (IEndpointRouteBuilder)app;
        var source = routes.DataSources.Single();
        foreach (var endpoint in source.Endpoints)
            Verify(endpoint);
    }

    /// <summary>Provides a normal mapped delegate without runtime problem behavior.</summary>
    private static IResult Invoke() => Results.NoContent();

    /// <summary>Reads actual native metadata and requires exactly the declared bounded envelope responses.</summary>
    private static void Verify(Endpoint endpoint)
    {
        var responses = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();
        var problems = responses
            .Where(IsProblem)
            .ToArray();
        var codes = problems
            .Select(StatusCode)
            .ToArray();
        var expected = new[] { 400, 503 };
        var matches = codes.SequenceEqual(expected);
        if (!matches)
            throw new InvalidOperationException("Problem status metadata duplicated statuses or partially mutated on invalid input.");

        foreach (var problem in problems)
        {
            var mediaTypes = problem.ContentTypes;
            var correctMedia = mediaTypes.Contains("application/problem+json");
            if (!correctMedia)
                throw new InvalidOperationException("Problem status metadata lost the native envelope media type.");
        }
    }

    /// <summary>Selects Foundation envelope declarations independently from native success metadata.</summary>
    private static bool IsProblem(IProducesResponseTypeMetadata response) => response.Type == typeof(FoundationProblemEnvelope);

    /// <summary>Reads an explicitly declared HTTP failure status.</summary>
    private static int StatusCode(IProducesResponseTypeMetadata response) => response.StatusCode;
}
