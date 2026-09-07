namespace Orbyss.Foundation.Authentication;

/// <summary>Identifies one concrete Orbyss Foundation authentication profile in a shell.</summary>
public interface IFoundationAuthenticationProfile
{
    /// <summary>Gets the stable profile identity.</summary>
    string Name { get; }
}
