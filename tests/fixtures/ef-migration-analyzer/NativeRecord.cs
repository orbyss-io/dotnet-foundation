namespace NativeEfAnalyzer;

/// <summary>Represents fictional data without accessing a database.</summary>
public sealed class NativeRecord
{
    /// <summary>Gets or sets the native generated identity.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the bounded fictional name.</summary>
    public string Name { get; set; } = string.Empty;
}
