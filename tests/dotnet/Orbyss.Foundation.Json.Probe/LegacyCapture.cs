namespace Orbyss.Foundation.Json.Probe;
/// <summary>Fixture-only budget capture.</summary>
/// <param name="Raw">Exact authored text.</param>
/// <param name="Normalized">Product-normalized text.</param>
public sealed record LegacyCapture(string Raw, string Normalized);
