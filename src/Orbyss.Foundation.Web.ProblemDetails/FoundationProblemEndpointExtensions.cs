using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Orbyss.Foundation.Web.ProblemDetails.Core;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Declares the existing bounded Foundation failure representation without inventing runtime failure handling.</summary>
public static class FoundationProblemEndpointExtensions
{
    /// <summary>Publishes each actual failure status from the operation's owned definitions.</summary>
    public static RouteHandlerBuilder ProducesFoundationProblems(
        this RouteHandlerBuilder builder,
        params ProblemDefinition[] definitions
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(definitions);
        var statuses = new List<int>();
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            statuses.Add(definition.StatusCode);
        }

        var declared = statuses.ToArray();
        return builder.ProducesFoundationProblemStatuses(declared);
    }

    /// <summary>Publishes actual HTTP error statuses when the runtime failure definitions belong to another native owner.</summary>
    public static RouteHandlerBuilder ProducesFoundationProblemStatuses(
        this RouteHandlerBuilder builder,
        params int[] statusCodes
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(statusCodes);
        var statuses = new HashSet<int>();
        foreach (var statusCode in statusCodes)
        {
            if (statusCode is < 400 or > 599)
            {
                var parameterName = nameof(statusCodes);
                throw new ArgumentOutOfRangeException(parameterName, "Foundation problem responses require an HTTP error status.");
            }
            statuses.Add(statusCode);
        }

        foreach (var statusCode in statuses)
            builder.Produces<FoundationProblemEnvelope>(statusCode, "application/problem+json");

        return builder;
    }
}
