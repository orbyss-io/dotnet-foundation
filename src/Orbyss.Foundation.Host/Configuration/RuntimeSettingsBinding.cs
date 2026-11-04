namespace Orbyss.Foundation.Host.Configuration;

/// <summary>Retains the requiredness and value constraint of one declared runtime setting.</summary>
internal sealed record RuntimeSettingsBinding(bool Required, string Kind);
