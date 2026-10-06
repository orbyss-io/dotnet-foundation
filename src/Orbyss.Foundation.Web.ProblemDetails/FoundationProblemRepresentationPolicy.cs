using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orbyss.Foundation.Web.ProblemDetails.Core;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Owns one safe envelope and request-scoped presentation contribution path.</summary>
internal sealed class FoundationProblemRepresentationPolicy(ILogger<FoundationProblemRepresentationPolicy> logger)
    : IProblemRepresentationPolicy
{
    /// <inheritdoc />
    public void Apply(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var correlation = context.HttpContext.TraceIdentifier;
        if (correlation.Length is < 1 or > 128 || !correlation.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or ':' or '.'))
            context.HttpContext.TraceIdentifier = correlation = Guid.NewGuid().ToString("N");
        ProblemDefinition definition;
        try
        {
            if (context.Exception is not null)
            {
                definition = new ProblemDefinition(500, ProblemCodes.RequestFailed);
                logger.LogError("Foundation failure {FailureType} with code {ProblemCode} and correlation {CorrelationId}.",
                    context.Exception.GetType().Name, definition.Code, correlation);
            }
            else
            {
                var status = problem.Status ?? context.HttpContext.Response.StatusCode;
                var code = problem.Extensions.TryGetValue(ProblemExtensionNames.Code, out var existing)
                    ? existing as string ?? throw new InvalidOperationException("Invalid problem code representation.")
                    : FoundationProblemResults.CodeForStatus(status);
                var fields = problem.Extensions.TryGetValue(ProblemExtensionNames.FieldErrors, out var fieldValue)
                    ? fieldValue as IEnumerable<ProblemFieldError>
                        ?? throw new InvalidOperationException("Invalid field diagnostic representation.")
                    : null;
                definition = new ProblemDefinition(statusCode: status, code: code,
                    title: problem.Title, detail: problem.Detail, fieldErrors: fields);
            }
            foreach (var enricher in context.HttpContext.RequestServices.GetServices<IProblemDetailsEnricher>())
            {
                var enriched = enricher.Enrich(context.HttpContext, definition);
                if (enriched is null || enriched.StatusCode != definition.StatusCode || enriched.Code != definition.Code)
                    throw new InvalidOperationException("Problem enrichment changed reserved failure identity.");
                definition = enriched;
            }
        }
        catch (Exception error) when (error is not OperationCanceledException
            || !context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            definition = new ProblemDefinition(500, ProblemCodes.RequestFailed);
            logger.LogError("Foundation problem representation rejected {FailureType} with correlation {CorrelationId}.",
                error.GetType().Name, correlation);
        }
        problem.Status = definition.StatusCode;
        context.HttpContext.Response.StatusCode = definition.StatusCode;
        problem.Title = definition.Title ?? TitleFor(definition.StatusCode);
        problem.Detail = definition.Detail;
        problem.Type = "about:blank";
        problem.Instance = null;
        problem.Extensions.Clear();
        problem.Extensions[ProblemExtensionNames.Code] = definition.Code;
        problem.Extensions[ProblemExtensionNames.CorrelationId] = correlation;
        problem.Extensions[ProblemExtensionNames.TraceId] = correlation;
        problem.Extensions[ProblemExtensionNames.FieldErrors] = definition.FieldErrors;
    }
    /// <summary>Supplies safe default titles without request or exception text.</summary>
    private static string TitleFor(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        405 => "Method Not Allowed",
        409 => "Conflict",
        413 => "Content Too Large",
        503 => "Service Unavailable",
        _ => "Request Failed"
    };
}
