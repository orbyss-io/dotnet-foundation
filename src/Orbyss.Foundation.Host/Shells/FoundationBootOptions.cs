namespace Orbyss.Foundation.Host.Shells;

/// <summary>Owns the Host's startup activation policy independently of selected shell features.</summary>
public sealed class FoundationBootOptions
{
    /// <summary>Gets or sets whether startup activates the configured shells before the Host starts accepting requests.</summary>
    public bool EagerShellActivation { get; set; } = true;

    /// <summary>Gets or sets whether an eager shell activation error fails Host startup.</summary>
    public bool FailOnShellActivationError { get; set; } = true;
}
