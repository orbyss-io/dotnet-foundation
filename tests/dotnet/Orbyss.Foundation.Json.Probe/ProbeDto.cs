using Orbyss.Foundation.Json;
namespace Orbyss.Foundation.Json.Probe;
/// <summary>Tests required/nullable members and lexeme preservation.</summary>
/// <param name="Name">Required non-null text.</param>
/// <param name="Optional">Required explicitly nullable text.</param>
/// <param name="Number">Exact number token.</param>
public sealed record ProbeDto(string Name, string? Optional, RawJsonNumber Number);
