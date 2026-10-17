namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Declares endpoint or native protocol-handler response purpose.</summary>
/// <param name="Purpose">The authoritative purpose; an unknown value retains machine output.</param>
public sealed record ProblemResponsePurposeMetadata(ProblemResponsePurpose Purpose);
