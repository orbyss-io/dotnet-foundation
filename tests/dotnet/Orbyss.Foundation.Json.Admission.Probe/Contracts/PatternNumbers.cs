using System.ComponentModel.DataAnnotations;

namespace Orbyss.Foundation.Json.Admission.Probe.Contracts;

/// <summary>Retains an explicitly owned annotation even when its text resembles a producer pattern.</summary>
internal sealed record PatternNumbers(
    [property: RegularExpression("^-?(?:0|[1-9]\\d*)$")] int? Count
);
