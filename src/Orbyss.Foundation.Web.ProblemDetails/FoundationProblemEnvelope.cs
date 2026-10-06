using System.Text.Json.Serialization;
using Orbyss.Foundation.Web.ProblemDetails.Core;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Owns the closed bounded wire shape without arbitrary object extension values.</summary>
internal sealed record FoundationProblemEnvelope(string Type, string Title, int Status, string Code,
    string CorrelationId, string TraceId, IReadOnlyList<ProblemFieldError> FieldErrors,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Detail);
