namespace Orbyss.Foundation.Json.Admission.Probe.Contracts;

/// <summary>Owns nullable numeric members reachable only through the registered base contract.</summary>
internal sealed record NumericDerived(int? Count, decimal? Amount) : NumericBase;
