using System.Text.Json.Serialization;
namespace Orbyss.Foundation.Json.Probe;
/// <summary>Fixture-only discriminated answer contract.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LegacyChoice), "choice")]
[JsonDerivedType(typeof(LegacyDecimal), "decimal")]
public abstract record LegacyAnswer
{
    /// <summary>Gets the exact product question identity.</summary>
    public required string QuestionId { get; init; }
}
