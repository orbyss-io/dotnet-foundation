using CShells;

namespace HostComposition.Fixture;

/// <summary>Proves the writer resolves its contributor from the actual request's shell scope.</summary>
public sealed class CustomContribution(ShellSettings settings)
{
    /// <summary>The owning shell, never a global or previously selected shell.</summary>
    public string Shell => settings.Id.ToString();
}
