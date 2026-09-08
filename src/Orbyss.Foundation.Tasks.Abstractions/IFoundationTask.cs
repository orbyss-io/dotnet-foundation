namespace Orbyss.Foundation.Tasks;

/// <summary>Identifies work owned by one CShells shell generation.</summary>
public interface IFoundationTask
{
    /// <summary>A stable identity used in diagnostics.</summary>
    string Id => GetType().FullName ?? GetType().Name;
}
