namespace Orbyss.Foundation.Authentication.Core;

/// <summary>An immutable account identity projected from a validated authentication ticket.</summary>
/// <remarks>Resource ownership and authorization remain application policies.</remarks>
public sealed record ValidatedAccountIdentity
{
    /// <summary>Creates a projection without normalizing its issuer or subject identity.</summary>
    /// <param name="issuer">The nonempty token-validated issuer.</param>
    /// <param name="subject">The nonempty token-validated subject.</param>
    public ValidatedAccountIdentity(string issuer, string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        Issuer = issuer;
        Subject = subject;
    }

    /// <summary>Gets the exact validated issuer.</summary>
    public string Issuer { get; }

    /// <summary>Gets the exact validated subject.</summary>
    public string Subject { get; }
}
