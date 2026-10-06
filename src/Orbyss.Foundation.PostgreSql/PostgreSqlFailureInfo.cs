using Npgsql;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Safe native failure identity; messages, SQL, detail, credentials and private input are excluded.</summary>
/// <param name="SqlState">The native five-character SQLSTATE identity.</param>
/// <param name="ConstraintName">The provider's exact optional schema constraint name.</param>
public sealed record PostgreSqlFailureInfo(string SqlState, string? ConstraintName)
{
    /// <summary>Extracts native identity through a bounded exception chain without classifying application outcomes.</summary>
    public static bool TryRead(Exception exception, out PostgreSqlFailureInfo? failure)
    {
        ArgumentNullException.ThrowIfNull(exception);
        failure = null;
        Exception? current = exception;
        for (var depth = 0; current is not null && depth < 16; depth++, current = current.InnerException)
        {
            if (current is not PostgresException postgres) continue;
            failure = new PostgreSqlFailureInfo(postgres.SqlState, postgres.ConstraintName);
            return true;
        }
        return false;
    }
}
