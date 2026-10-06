using Npgsql;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Admits native configuration once, retaining one pool identity for this provider generation/policy.</summary>
internal sealed class PostgreSqlPolicy
{
    /// <summary>The stable validated connection string owned by the datasource.</summary>
    internal string ConnectionString { get; }
    /// <summary>The operation lifetime shared by its independent context units.</summary>
    internal TimeSpan OperationTimeout { get; }
    /// <summary>The maximum native open/pool wait stage.</summary>
    internal TimeSpan ConnectionTimeout { get; }
    /// <summary>The maximum command stage.</summary>
    internal TimeSpan CommandTimeout { get; }
    /// <summary>The native lock timeout.</summary>
    internal TimeSpan LockTimeout { get; }

    /// <summary>Validates budgets and configures native driver fallbacks once, never per operation.</summary>
    internal PostgreSqlPolicy(PostgreSqlOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);
        Validate(options.OperationTimeout);
        Validate(options.ConnectionTimeout);
        Validate(options.CommandTimeout);
        Validate(options.LockTimeout);
        Validate(options.CancellationTimeout);
        if (options.ConnectionTimeout > options.OperationTimeout || options.CommandTimeout > options.OperationTimeout
            || options.LockTimeout > options.CommandTimeout) throw new ArgumentException("PostgreSQL stage budgets must fit the operation/command budget.");
        var builder = new NpgsqlConnectionStringBuilder(options.ConnectionString)
        {
            Timeout = Seconds(options.ConnectionTimeout),
            CommandTimeout = Seconds(options.CommandTimeout),
            CancellationTimeout = checked((int)Math.Ceiling(options.CancellationTimeout.TotalMilliseconds)),
            NoResetOnClose = false
        };
        ConnectionString = builder.ConnectionString;
        OperationTimeout = options.OperationTimeout;
        ConnectionTimeout = options.ConnectionTimeout;
        CommandTimeout = options.CommandTimeout;
        LockTimeout = options.LockTimeout;
    }

    /// <summary>Projects a positive stage to the driver's whole-second fallback without disabling it.</summary>
    internal static int Seconds(TimeSpan remaining) => Math.Max(1, checked((int)Math.Ceiling(remaining.TotalSeconds)));

    /// <summary>Requires registrations sharing one policy name to select exactly one native configuration.</summary>
    internal bool Matches(PostgreSqlPolicy other) => ConnectionString.Equals(other.ConnectionString, StringComparison.Ordinal)
        && OperationTimeout == other.OperationTimeout && ConnectionTimeout == other.ConnectionTimeout
        && CommandTimeout == other.CommandTimeout && LockTimeout == other.LockTimeout;

    /// <summary>Rejects disabled and out-of-range native timer values.</summary>
    private static void Validate(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || duration.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(duration), "PostgreSQL budgets must be positive finite native timer values.");
    }
}
