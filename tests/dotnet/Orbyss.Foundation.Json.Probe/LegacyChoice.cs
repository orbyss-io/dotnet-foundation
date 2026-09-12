namespace Orbyss.Foundation.Json.Probe;
/// <summary>Fixture-only choice variant.</summary>
public sealed record LegacyChoice : LegacyAnswer
{
    /// <summary>Gets the admitted option identity.</summary>
    public required string OptionId { get; init; }
}
