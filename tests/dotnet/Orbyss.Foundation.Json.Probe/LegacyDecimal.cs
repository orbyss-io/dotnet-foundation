namespace Orbyss.Foundation.Json.Probe;
/// <summary>Fixture-only decimal variant preserving product string captures.</summary>
public sealed record LegacyDecimal : LegacyAnswer
{
    /// <summary>Gets exact authored decimal text.</summary>
    public required string Raw { get; init; }
    /// <summary>Gets the product-normalized decimal string.</summary>
    public required string Normalized { get; init; }
}
