using System.Text.Json.Serialization;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>The native writer's closed wire diagnostic; it contains no submitted value or arbitrary extensions.</summary>
/// <param name="Field">Admitted public field path.</param>
/// <param name="Code">Admitted stable diagnostic code.</param>
/// <param name="Message">Admitted safe public message.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FoundationProblemFieldError(string Field, string Code, string Message);
