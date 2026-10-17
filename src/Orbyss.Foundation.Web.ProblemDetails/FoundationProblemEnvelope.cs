using System.Text.Json.Serialization;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Owns the closed bounded wire shape without arbitrary object extension values.</summary>
/// <param name="Type">Admitted problem type.</param>
/// <param name="Title">Safe public title.</param>
/// <param name="Status">Actual numeric HTTP status.</param>
/// <param name="Code">Stable public problem code.</param>
/// <param name="CorrelationId">Admitted request correlation identifier.</param>
/// <param name="TraceId">Compatibility alias of the same correlation identifier.</param>
/// <param name="FieldErrors">Bounded safe public field diagnostics.</param>
/// <param name="Detail">Optional safe public detail, omitted when absent.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record FoundationProblemEnvelope(string Type, string Title, int Status, string Code,
    string CorrelationId, string TraceId, IReadOnlyList<FoundationProblemFieldError> FieldErrors,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Detail = null);
