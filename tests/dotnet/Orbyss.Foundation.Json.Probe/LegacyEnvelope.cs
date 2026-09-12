namespace Orbyss.Foundation.Json.Probe;
/// <summary>Fixture-only typed view of the inspected RM-01 v1 envelope; no runtime product dependency.</summary>
/// <param name="Answers">Product-sorted answers.</param>
/// <param name="Budget">Explicit optional budget.</param>
/// <param name="JourneyId">Exact journey identity.</param>
/// <param name="References">Exact release references.</param>
/// <param name="RequestedLanguages">Ordered language preferences.</param>
/// <param name="ResolvedLanguage">Resolved language.</param>
/// <param name="SchemaVersion">Exact numeric version token.</param>
/// <param name="SelectedLanguage">Explicit nullable selection.</param>
public sealed record LegacyEnvelope(LegacyAnswer[] Answers, LegacyCapture? Budget, string JourneyId,
    Dictionary<string, string> References, string[] RequestedLanguages, string ResolvedLanguage,
    RawJsonNumber SchemaVersion, string? SelectedLanguage);
