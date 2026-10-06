namespace Orbyss.Foundation.PostgreSql;

/// <summary>Deployment-owned native database budgets, validated into an immutable provider snapshot.</summary>
public sealed class PostgreSqlOptions
{
    /// <summary>Gets or sets the policy's connection string; it is never emitted as a diagnostic.</summary>
    public string ConnectionString { get; set; } = string.Empty;
    /// <summary>Gets or sets the complete operation budget.</summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>Gets or sets the capped connection/open/pool wait budget.</summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(2);
    /// <summary>Gets or sets the cap for each command.</summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>Gets or sets the native PostgreSQL lock wait cap.</summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(1);
    /// <summary>Gets or sets the driver's cancellation acknowledgement allowance.</summary>
    public TimeSpan CancellationTimeout { get; set; } = TimeSpan.FromSeconds(2);
}
